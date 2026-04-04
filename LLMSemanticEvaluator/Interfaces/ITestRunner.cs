using LLMSemanticEvaluator.Models;
namespace LLMSemanticEvaluator.Interfaces;

/// <summary>
/// Defines the contract for running semantic evaluation test cases
/// against a large language model and returning their results.
/// </summary>
public interface ITestRunner
{
    /// <summary>
    /// Runs all provided test cases against the LLM and returns one
    /// <see cref="TestResult"/> per case.
    /// </summary>
    /// <param name="testCases">
    /// The list of test cases to evaluate. Each case contains a prompt,
    /// expected output, and optional evaluation criteria.
    /// </param>
    /// <param name="cancellationToken">
    /// Token to observe for cancellation requests, such as host shutdown or Ctrl+C.
    /// </param>
    /// <returns>
    /// A list of <see cref="TestResult"/> objects, one per test case,
    /// containing scores, pass/fail status, and per-run details.
    /// </returns>
    Task<List<TestResult>> RunAllAsync(
        List<TestCase> testCases,
        CancellationToken cancellationToken = default);
}
