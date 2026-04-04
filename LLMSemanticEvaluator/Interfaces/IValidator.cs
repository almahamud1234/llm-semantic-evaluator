using LLMSemanticEvaluator.Models;

namespace LLMSemanticEvaluator.Interfaces;

/// <summary>
/// Defines the contract for validating an LLM response
/// against an expected output.
/// </summary>
public interface IValidator
{
    /// <param name="prompt">The original test question sent to the LLM under test.</param>
    /// <param name="expected">Reference answer from the test case JSON.</param>
    /// <param name="actual">Response returned by the LLM under test.</param>
    /// <param name="criteria">Optional per-test evaluation guidance.</param>
    Task<ValidationResult> ValidateAsync(
        string expected,
        string actual,
        string prompt = "",
        string criteria = "");
}