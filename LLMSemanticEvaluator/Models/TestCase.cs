using System.Text.Json.Serialization;

namespace LLMSemanticEvaluator.Models;

/// <summary>
/// Represents a single test case
/// </summary>
public class TestCase
{
    /// <summary>
    /// Unique identifier for the test
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Category of the test (e.g., "factual", "math", "reasoning")
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The prompt to send to the LLM
    /// </summary>
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Expected output from the LLM
    /// </summary>
    [JsonPropertyName("expected_output")]
    public string ExpectedOutput { get; set; } = string.Empty;

    /// <summary>
    /// Criteria for evaluating the response
    /// </summary>
    [JsonPropertyName("evaluation_criteria")]
    public string EvaluationCriteria { get; set; } = string.Empty;
}
