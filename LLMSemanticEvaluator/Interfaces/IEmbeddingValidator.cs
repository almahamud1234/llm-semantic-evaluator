using LLMSemanticEvaluator.Models;

namespace LLMSemanticEvaluator.Interfaces;

/// <summary>
/// Validates LLM output by measuring semantic similarity
/// between the expected and actual response using embedding vectors.
/// </summary>
public interface IEmbeddingValidator
{
    /// <param name="expected">Reference answer from the test case JSON.</param>
    /// <param name="actual">Response returned by the LLM under test.</param>
    Task<ValidationResult> ValidateAsync(string expected, string actual);
}