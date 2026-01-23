// File: Core/Interfaces/ITestValidator.cs
namespace LLMSemanticEvaluator.Interfaces;

// using LLMSemanticEvaluator.Models;

/// <summary>
/// Interface for test validation strategies
/// </summary>
public interface ITestValidator
{
    /// <summary>
    /// Validates the actual output against expected output
    /// </summary>
    /// <param name="expectedOutput">The expected output</param>
    /// <param name="actualOutput">The actual LLM output</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with score and pass/fail status</returns>
    // Task<ValidationResult> ValidateAsync(
    Task ValidateAsync(
        string expectedOutput, 
        string actualOutput, 
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Name of the validator (e.g., "Embedding", "LLMJudge")
    /// </summary>
    string ValidatorName { get; }
}
