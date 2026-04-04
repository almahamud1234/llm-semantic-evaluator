using System.Text.RegularExpressions;
using LLMSemanticEvaluator.Configuration;
using LLMSemanticEvaluator.Interfaces;
using LLMSemanticEvaluator.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LLMSemanticEvaluator.Validators;

/// <summary>
/// Validates an LLM response using a second LLM as a judge (G-Eval style).
///
/// How it works:
///   1. A structured prompt is built asking the judge to reason step-by-step,
///      then output a score in the format "SCORE: N" on the final line.
///   2. The score and reasoning are extracted separately from the judge's response.
///   3. The run passes if score >= JudgeThreshold (default 8/10).
///
/// IChatClient is the standard Microsoft.Extensions.AI interface. The concrete
/// implementation (OpenAI or Ollama) is resolved by LLMClientFactory and injected
/// here — this class never knows which provider is used.
///
/// Why G-Eval style:
///   Asking the judge to reason before scoring produces more accurate and
///   consistent scores than requesting a number directly. Chain-of-thought
///   forces the judge to evaluate the content rather than pattern-match to a digit.
///   The reasoning text is stored in reports, making every judge decision auditable.
///
/// Known limitation: models smaller than ~3B parameters cannot reliably apply a
/// structured scoring rubric. See results documentation for evidence.
/// </summary>
public class LLMJudgeValidator: IValidator
{
    private readonly IChatClient                 _chatClient;
    private readonly ChatOptions                 _chatOptions;
    private readonly int                         _threshold;
    private readonly ILogger<LLMJudgeValidator>  _logger;

    // Matches "SCORE: N" — the structured output format requested in the judge prompt.
    private static readonly Regex ScoreLinePattern = new(
        @"SCORE:\s*([1-9]|10)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Fallback: first standalone integer 1–10 anywhere in the response.
    // Word boundaries prevent matching "10" inside "100".
    private static readonly Regex ScoreFallbackPattern = new(
        @"\b([1-9]|10)\b", RegexOptions.Compiled);

    public LLMJudgeValidator(
        IChatClient                 chatClient,
        IOptions<TestConfiguration> options,
        ILogger<LLMJudgeValidator>  logger)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _threshold  = options.Value.JudgeThreshold;
        _logger     = logger;

        // Some models (gpt-5-mini, o1, o3 etc.) do not accept a temperature parameter
        // and will return HTTP 400 if it is set. For those models ChatOptions is left
        // empty so the provider uses its own default. For all other models, Temperature
        // is read from appsettings.json and applied on every call.
        _chatOptions = SupportsTemperature(options.Value.ChatModel)
            ? new ChatOptions { Temperature = (float)options.Value.Temperature }
            : new ChatOptions();
    }

    /// <summary>
    /// Asks the judge LLM to score how well <paramref name="actual"/> answers
    /// <paramref name="prompt"/> relative to the <paramref name="expected"/> answer.
    /// </summary>
    /// <param name="prompt">The original test question sent to the LLM under test.</param>
    /// <param name="expected">Reference answer from the test case JSON.</param>
    /// <param name="actual">Response returned by the LLM under test.</param>
    /// <param name="criteria">Optional per-test evaluation guidance injected into the prompt.</param>
    public async Task<ValidationResult> ValidateAsync(
        string expected,
        string actual,
        string prompt = "",
        string criteria = "")
    {
        if (string.IsNullOrWhiteSpace(actual))
            return Fail("LLM returned an empty response.");

        try
        {
            string judgePrompt = BuildJudgePrompt(prompt, expected, actual, criteria);

            // GetResponseAsync is the Microsoft.Extensions.AI standard method.
            // ChatOptions carries Temperature from appsettings.json.
            ChatResponse response = await _chatClient.GetResponseAsync(
                judgePrompt, _chatOptions);

            string responseText = response.Text;

            if (string.IsNullOrWhiteSpace(responseText))
                return Fail("Judge returned an empty response.");

            int    score     = ParseScore(responseText);
            string reasoning = ExtractReasoning(responseText);

            _logger.LogDebug(
                "Judge score: {Score}/10 (threshold {Threshold})", score, _threshold);

            if (score == 0)
                return Fail($"Could not parse a valid score from judge response: '{responseText.Trim()}'");

            return new ValidationResult
            {
                ValidatorName = "LLMJudge",
                Score         = score,
                Passed        = score >= _threshold,
                Reasoning     = reasoning
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Judge validation error: {Error}", ex.Message);
            return Fail($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds a structured judge prompt: reason first, then score on the final line.
    /// The "SCORE: N" format makes parsing reliable while keeping the full reasoning
    /// available for reports. Optional criteria is injected when present.
    /// </summary>
    private static string BuildJudgePrompt(
        string prompt, string expected, string actual, string criteria)
    {
        string criteriaSection = string.IsNullOrWhiteSpace(criteria)
            ? string.Empty
            : $"\nEvaluation Criteria: {criteria}";

        return $"""
            You are an expert evaluator assessing the quality of an AI response.

            Query: {prompt}
            Expected Answer: {expected}
            Actual Answer: {actual}{criteriaSection}

            Step 1 — Reason step by step (2–4 sentences):
            - Does the actual answer correctly address the query?
            - Does it capture the same meaning as the expected answer?
            - Are there factual errors, key omissions, or irrelevant content?

            Step 2 — Assign a score:
            10 = Perfect: semantically identical to the expected answer
            8–9 = Very good: same core meaning, minor wording differences
            6–7 = Acceptable: mostly correct but incomplete or awkward
            4–5 = Partially correct: some relevant content, missing key points
            1–3 = Wrong, irrelevant, or significantly misleading

            Write your reasoning first, then on the very last line write ONLY:
            SCORE: <number>
            """;
    }

    /// <summary>
    /// Extracts a 1–10 integer score from the judge response.
    /// Tries "SCORE: N" format first, then falls back to the first standalone
    /// integer 1–10. Returns 0 if no valid score is found.
    /// </summary>
    private static int ParseScore(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return 0;

        string trimmed = response.Trim();

        Match primary = ScoreLinePattern.Match(trimmed);
        if (primary.Success &&
            int.TryParse(primary.Groups[1].Value, out int s1) &&
            s1 is >= 1 and <= 10)
            return s1;

        Match fallback = ScoreFallbackPattern.Match(trimmed);
        if (fallback.Success &&
            int.TryParse(fallback.Groups[1].Value, out int s2) &&
            s2 is >= 1 and <= 10)
            return s2;

        return 0;
    }

    /// <summary>
    /// Returns everything before the "SCORE: N" line as the reasoning text.
    /// Falls back to the full response if no score line is found.
    /// </summary>
    private static string ExtractReasoning(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return string.Empty;

        Match match = ScoreLinePattern.Match(response);
        return match.Success && match.Index > 0
            ? response[..match.Index].Trim()
            : response.Trim();
    }

    private static ValidationResult Fail(string reason) => new()
    {
        ValidatorName = "LLMJudge",
        Score         = 0,
        Passed        = false,
        Reasoning     = reason
    };

    /// <summary>
    /// Returns false for models that reject the temperature parameter entirely.
    /// gpt-5 and OpenAI reasoning models (o1, o3) only accept the default value
    /// and return HTTP 400 if temperature is set explicitly.
    /// </summary>
    private static bool SupportsTemperature(string modelName)
    {
        string m = modelName.ToLowerInvariant();
        return !m.StartsWith("gpt-5")
            && !m.StartsWith("o1")
            && !m.StartsWith("o3");
    }
}