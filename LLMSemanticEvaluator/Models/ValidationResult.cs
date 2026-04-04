namespace LLMSemanticEvaluator.Models;

/// <summary>
/// Result of a validation check
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the validation passed
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Numerical score (0.0 to 1.0 for embedding, 1-10 for judge)
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Optional reasoning or explanation
    /// </summary>
    public string? Reasoning { get; set; }

    /// <summary>
    /// Name of the validator that produced this result
    /// </summary>
    public string ValidatorName { get; set; } = string.Empty;
}