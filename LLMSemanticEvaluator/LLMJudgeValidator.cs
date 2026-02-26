using LLMSemanticEvaluator.Models;
using LLMSemanticEvaluator.Interfaces;
using System.Text.RegularExpressions;

namespace LLMSemanticEvaluator;

/// <summary>
/// Validates LLM responses using a second LLM as a judge.
/// Sends a structured prompt asking the judge to score the answer 1-10,
/// then passes if the score meets the configured threshold.
/// </summary>
public class LLMJudgeValidator
{
    private readonly ILLMClient _llmClient;
    private readonly int _threshold;

    // Matches the first standalone integer between 1 and 10 in the response.
    // \b word boundaries prevent matching "10" inside "100" etc.
    private static readonly Regex ScorePattern = new(@"\b([1-9]|10)\b", RegexOptions.Compiled);

    public LLMJudgeValidator(ILLMClient llmClient, int threshold = 8)
    {
        _llmClient = llmClient;
        _threshold = threshold;
    }

    /// <summary>
    /// Asks the judge LLM to score how well <paramref name="actual"/> answers
    /// the <paramref name="prompt"/> compared to <paramref name="expected"/>.
    /// Returns a <see cref="ValidationResult"/> with Score (1-10) and Passed flag.
    /// </summary>
    public async Task<ValidationResult> ValidateAsync(
        string prompt, string expected, string actual)
    {
        try
        {
            string judgePrompt = BuildJudgePrompt(prompt, expected, actual);
            string response    = await _llmClient.SendPromptAsync(judgePrompt);
            int score          = ParseScore(response);

            return new ValidationResult
            {
                ValidatorName = "LLMJudge",
                Score         = score,
                Passed        = score >= _threshold,
                Reasoning     = response.Trim()
            };
        }
        catch (Exception ex)
        {
            return new ValidationResult
            {
                ValidatorName = "LLMJudge",
                Score         = 0,
                Passed        = false,
                Reasoning     = $"Error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Builds a strict judge prompt that instructs the LLM to reply with a number only.
    /// Keeping the prompt strict minimises noise in the response and makes parsing reliable.
    /// </summary>
    private static string BuildJudgePrompt(string prompt, string expected, string actual)
    {
        return $"""
            You are evaluating an AI response. Score it from 1-10.

            Question: {prompt}
            Expected Answer: {expected}
            Actual Answer: {actual}

            Rate how well the actual answer matches the expected answer on a scale of 1-10:
            - 10 = Perfect match (semantically identical)
            - 8-9 = Very good (same meaning, minor wording differences)
            - 6-7 = Acceptable (correct but incomplete or awkwardly worded)
            - 4-5 = Partially correct
            - 1-3 = Wrong or irrelevant

            Respond with ONLY a number from 1 to 10. Nothing else.
            """;
    }

    /// <summary>
    /// Extracts a 1-10 score from the judge's response.
    /// Strategy: regex for a standalone integer 1-10 → fallback to scanning
    /// individual words → returns 0 if nothing valid is found.
    /// </summary>
    private static int ParseScore(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return 0;

        string trimmed = response.Trim();

        // Fast path: entire response is already just a number (ideal case)
        if (int.TryParse(trimmed, out int direct) && direct >= 1 && direct <= 10)
            return direct;

        // Regex path: find first standalone 1-10 integer anywhere in the text.
        // Handles responses like "Score: 9", "I'd give it an 8/10", "9." etc.
        Match match = ScorePattern.Match(trimmed);
        if (match.Success && int.TryParse(match.Value, out int regexScore))
            return regexScore;

        // Last-resort word scan: strip punctuation from each word and try parsing.
        // Catches edge cases the regex might miss with unusual whitespace or encoding.
        foreach (string word in trimmed.Split(' ', '\n', '\r', '\t'))
        {
            string clean = word.Trim('.', ',', '/', '(', ')', '[', ']', ':');
            if (int.TryParse(clean, out int wordScore) && wordScore >= 1 && wordScore <= 10)
                return wordScore;
        }

        // Could not find a valid score — treat as failure
        return 0;
    }
}