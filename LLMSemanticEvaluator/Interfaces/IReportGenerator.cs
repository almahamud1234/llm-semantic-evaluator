using LLMSemanticEvaluator.Models;

namespace LLMSemanticEvaluator.Interfaces;

/// <summary>
/// Defines the contract for generating evaluation reports
/// from a completed set of test results.
/// </summary>
public interface IReportGenerator
{
    /// <summary>
    /// Generates all four report formats (TXT, JSON, CSV, HTML) from the
    /// provided test results and saves them to the configured reports folder.
    /// Also logs a summary and auto-opens the HTML dashboard in the browser.
    /// </summary>
    /// <param name="results">
    /// The list of <see cref="TestResult"/> objects produced by <see cref="ITestRunner"/>.
    /// </param>
    Task GenerateAsync(List<TestResult> results);
}