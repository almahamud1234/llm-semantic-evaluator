// File: Core/Models/TestCase.cs
namespace LLMSemanticEvaluator.Models;

/// <summary>
/// Represents a single test case
/// </summary>
public class TestCase
{
    /// <summary>
    /// Unique identifier for the test
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Category of the test (e.g., "factual", "math", "reasoning")
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The prompt to send to the LLM
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Expected output from the LLM
    /// </summary>
    public string ExpectedOutput { get; set; } = string.Empty;

    /// <summary>
    /// Criteria for evaluating the response
    /// </summary>
    public string EvaluationCriteria { get; set; } = string.Empty;
}
