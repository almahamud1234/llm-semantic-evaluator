// File: Core/Interfaces/ITestRunner.cs
namespace LLMSemanticEvaluator.Interfaces;

// using LLMSemanticEvaluator.Models;

/// <summary>
/// Interface for test execution orchestration
/// </summary>
public interface ITestRunner
{
    /// <summary>
    /// Runs all test cases
    /// </summary>
    /// <param name="testCases">List of test cases to run</param>
    /// <param name="numberOfRuns">Number of times to run each test (default: 3)</param>
    /// <returns>Complete test report</returns>
    // Task<TestReport> RunTestsAsync(List<TestCase> testCases, int numberOfRuns = 3);
    Task RunTestsAsync(List<string> testCases, int numberOfRuns = 3);

    /// <summary>
    /// Event fired when a test completes
    /// </summary>
    // event EventHandler<TestProgressEventArgs>? TestCompleted;
    event EventHandler? TestCompleted;
}
