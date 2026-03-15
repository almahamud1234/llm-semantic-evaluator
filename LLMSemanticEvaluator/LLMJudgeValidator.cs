using LLMSemanticEvaluator.Models;
using LLMSemanticEvaluator.Interfaces;
using System.Text.RegularExpressions;

namespace LLMSemanticEvaluator;

/// <summary>
/// Validates LLM responses using a second LLM as a judge (G-Eval style).
///
/// HOW IT WORKS:
///   1. Sends a structured prompt asking the judge to reason step-by-step first,
///      then output a score on the final line in the format "SCORE: N".
///   2. The reasoning and score are extracted separately from the response.
///   3. Reasoning is stored in ValidationResult.Reasoning for use in reports.
///   4. Passes if the score meets or exceeds the configured threshold (default: 8/10).
///
/// WHY G-EVAL STYLE:
///   Asking the judge to reason before scoring produces more accurate and consistent
///   scores than asking for a number directly. The chain-of-thought forces the judge
///   to actually evaluate the answer rather than pattern-matching to a number.
/// </summary>
public class LLMJudgeValidator
{
    private readonly ILLMClient _llmClient;
    private readonly int        _threshold;

    // Matches "SCORE: N" or "SCORE:N" (our structured output format).
    private static readonly Regex ScoreLinePattern = new(
        @"SCORE:\s*([1-9]|10)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Fallback: matches the first standalone integer between 1 and 10.
    // \b word boundaries prevent matching "10" inside "100" etc.
    private static readonly Regex ScoreFallbackPattern = new(
        @"\b([1-9]|10)\b", RegexOptions.Compiled);

    public LLMJudgeValidator(ILLMClient llmClient, int threshold = 8)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _threshold = threshold;
    }

    // =========================================================================
    // Public entry point
    // =========================================================================

    /// <summary>
    /// Asks the judge LLM to score how well <paramref name="actual"/> answers
    /// the <paramref name="prompt"/> compared to <paramref name="expected"/>.
    /// </summary>
    /// <param name="prompt">The original test question sent to the LLM under test.</param>
    /// <param name="expected">The correct/expected answer from the test case JSON.</param>
    /// <param name="actual">The actual response returned by the LLM under test.</param>
    /// <param name="criteria">
    ///     Optional evaluation criteria from the test case (e.g. "Must demonstrate
    ///     logical deduction"). Injected into the judge prompt for more targeted scoring.
    /// </param>
    /// <returns>
    ///     A <see cref="ValidationResult"/> with:
    ///     - Score: 1–10
    ///     - Passed: true if Score >= threshold
    ///     - Reasoning: the judge's step-by-step reasoning (valuable for reports)
    /// </returns>
    public async Task<ValidationResult> ValidateAsync(
        string prompt,
        string expected,
        string actual,
        string criteria = "")
    {
        // Treat empty/null LLM response as an automatic fail — nothing to judge
        if (string.IsNullOrWhiteSpace(actual))
        {
            return FailResult("LLM returned an empty response");
        }

        try
        {
            string judgePrompt = BuildJudgePrompt(prompt, expected, actual, criteria);
            string response    = await _llmClient.SendPromptAsync(judgePrompt);

            if (string.IsNullOrWhiteSpace(response))
                return FailResult("Judge returned an empty response");

            // Extract score and reasoning from the structured response
            int    score     = ParseScore(response);
            string reasoning = ExtractReasoning(response);

            if (score == 0)
            {
                return new ValidationResult
                {
                    ValidatorName = "LLMJudge",
                    Score         = 0,
                    Passed        = false,
                    Reasoning     = $"Could not parse a score from judge response: '{response.Trim()}'"
                };
            }

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
            return FailResult($"Error: {ex.Message}");
        }
    }

    // =========================================================================
    // Prompt builder (G-Eval style)
    // =========================================================================

    /// <summary>
    /// Builds a G-Eval style judge prompt: reason step-by-step first, then score.
    /// The structured "SCORE: N" format on the final line makes parsing reliable
    /// while preserving the full reasoning text for reports.
    /// If <paramref name="criteria"/> is provided, it is injected to give the judge
    /// targeted guidance specific to that test case.
    /// </summary>
    private static string BuildJudgePrompt(
        string prompt,
        string expected,
        string actual,
        string criteria)
    {
        // Only include the criteria line if the test case actually has one
        string criteriaSection = string.IsNullOrWhiteSpace(criteria)
            ? string.Empty
            : $"\nEvaluation Criteria: {criteria}";

        return $"""
            You are an expert evaluator assessing the quality of an AI response.

            Query: {prompt}
            Expected Answer: {expected}
            Actual Answer: {actual}{criteriaSection}

            Step 1 — Reason step by step (write 2–4 sentences):
            - Does the actual answer correctly address the query?
            - Does it capture the same meaning as the expected answer?
            - Are there factual errors, key omissions, or irrelevant content?

            Step 2 — Assign a score using this scale:
            10 = Perfect: semantically identical to the expected answer
            8–9 = Very good: same core meaning, minor wording differences
            6–7 = Acceptable: mostly correct but incomplete or awkwardly worded
            4–5 = Partially correct: some relevant content but missing key points
            1–3 = Wrong, irrelevant, or significantly misleading

            Write your reasoning first, then on the very last line write ONLY:
            SCORE: <number>
            """;
    }

    // =========================================================================
    // Score parser
    // =========================================================================

    /// <summary>
    /// Extracts a 1–10 score from the judge's response.
    ///
    /// Strategy (in order):
    ///   1. Look for "SCORE: N" anywhere in the text (our structured format).
    ///   2. Fallback: find the first standalone integer 1–10 (handles non-compliant responses).
    ///   3. Return 0 if nothing valid is found (caller treats this as a parse failure).
    /// </summary>
    private static int ParseScore(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return 0;

        string trimmed = response.Trim();

        // Primary: structured "SCORE: N" format
        Match scoreLine = ScoreLinePattern.Match(trimmed);
        if (scoreLine.Success &&
            int.TryParse(scoreLine.Groups[1].Value, out int structured) &&
            structured >= 1 && structured <= 10)
            return structured;

        // Fallback: first standalone integer 1–10 anywhere in the text.
        // Handles edge cases like "I give it an 8/10" or a plain "9" response.
        Match fallback = ScoreFallbackPattern.Match(trimmed);
        if (fallback.Success &&
            int.TryParse(fallback.Groups[1].Value, out int fallbackScore) &&
            fallbackScore >= 1 && fallbackScore <= 10)
            return fallbackScore;

        return 0;
    }

    // =========================================================================
    // Reasoning extractor
    // =========================================================================

    /// <summary>
    /// Extracts just the reasoning portion of the judge's response (everything
    /// before the "SCORE: N" line). This is stored separately in reports so that
    /// the reasoning is human-readable without the score line cluttering it.
    ///
    /// Falls back to returning the full response if no "SCORE:" line is found
    /// (e.g. when the judge did not follow the structured format).
    /// </summary>
    private static string ExtractReasoning(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return string.Empty;

        // Find the index of the SCORE: line and take everything before it
        Match match = ScoreLinePattern.Match(response);
        if (match.Success && match.Index > 0)
        {
            // Trim trailing whitespace/newlines from the reasoning portion
            return response[..match.Index].Trim();
        }

        // No structured SCORE: line — return the whole response as reasoning
        return response.Trim();
    }

    // =========================================================================
    // Helper
    // =========================================================================

    private static ValidationResult FailResult(string reason) => new()
    {
        ValidatorName = "LLMJudge",
        Score         = 0,
        Passed        = false,
        Reasoning     = reason
    };
}