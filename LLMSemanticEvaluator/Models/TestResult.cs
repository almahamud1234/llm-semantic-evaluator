// File: Core/Models/TestResult.cs
namespace LLMSemanticEvaluator.Models;

/// <summary>
/// Represents the aggregated result of multiple test runs
/// </summary>
public class TestResult
{
    /// <summary>
    /// The test case ID
    /// </summary>
    public string TestId { get; set; } = string.Empty;

    /// <summary>
    /// Test category
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Original prompt
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Expected output
    /// </summary>
    public string ExpectedOutput { get; set; } = string.Empty;

    /// <summary>
    /// Whether the test passed overall
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// All runs of this test
    /// </summary>
    public List<TestRun> Runs { get; set; } = new();

    /// <summary>
    /// Average embedding score across all runs
    /// </summary>
    public double AverageEmbeddingScore { get; set; }

    /// <summary>
    /// Average judge score across all runs
    /// </summary>
    public double AverageJudgeScore { get; set; }

    /// <summary>
    /// Number of runs that passed
    /// </summary>
    public int PassedRunsCount { get; set; }

    /// <summary>
    /// Total number of runs
    /// </summary>
    public int TotalRunsCount { get; set; }
}