// File: Core/Models/TestReport.cs
namespace LLMPromptTesting.Console.Core.Models;

/// <summary>
/// Complete test execution report
/// </summary>
public class TestReport
{
    /// <summary>
    /// When the report was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Total number of tests executed
    /// </summary>
    public int TotalTests { get; set; }

    /// <summary>
    /// Number of tests that passed
    /// </summary>
    public int PassedTests { get; set; }

    /// <summary>
    /// Number of tests that failed
    /// </summary>
    public int FailedTests { get; set; }

    /// <summary>
    /// Overall pass percentage
    /// </summary>
    public double PassPercentage => TotalTests > 0 
        ? (PassedTests / (double)TotalTests) * 100 
        : 0;

    /// <summary>
    /// Statistics grouped by category
    /// </summary>
    public Dictionary<string, CategoryStats> CategoryStatistics { get; set; } = new();

    /// <summary>
    /// All individual test results
    /// </summary>
    public List<TestResult> Results { get; set; } = new();

    /// <summary>
    /// Average embedding score across all tests
    /// </summary>
    public double AverageEmbeddingScore { get; set; }

    /// <summary>
    /// Average judge score across all tests
    /// </summary>
    public double AverageJudgeScore { get; set; }

    /// <summary>
    /// Total execution time
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }
}