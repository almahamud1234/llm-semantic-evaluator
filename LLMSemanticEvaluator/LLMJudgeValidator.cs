using LLMSemanticEvaluator.Models;
using LLMSemanticEvaluator.Interfaces;

namespace LLMSemanticEvaluator;

/// <summary>
/// Validates LLM responses by asking a second LLM to score the answer 1-10.
/// Passes if score >= threshold (default 8).
/// </summary>
public class LLMJudgeValidator
{
    private readonly ILLMClient _llmClient;
    private readonly int _threshold;

    public LLMJudgeValidator(ILLMClient llmClient, int threshold = 8)
    {
        _llmClient = llmClient;
        _threshold = threshold;
    }

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

    private static int ParseScore(string response)
    {
        // Extract first number found in the response
        foreach (var word in response.Trim().Split(' ', '\n'))
        {
            if (int.TryParse(word.Trim('.', ','), out int score) && score >= 1 && score <= 10)
                return score;
        }
        return 0; // Could not parse = treat as fail
    }
}