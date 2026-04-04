using LLMSemanticEvaluator.Models;

namespace LLMSemanticEvaluator.Interfaces;

/// <summary>
/// Validates LLM output using a second LLM as a judge (G-Eval style).
/// </summary>
public interface ILLMJudgeValidator
{
    /// <param name="prompt">The original test question sent to the LLM under test.</param>
    /// <param name="expected">Reference answer from the test case JSON.</param>
    /// <param name="actual">Response returned by the LLM under test.</param>
    /// <param name="criteria">Optional per-test evaluation guidance.</param>
    Task<ValidationResult> ValidateAsync(
        string prompt,
        string expected,
        string actual,
        string criteria = "");
}