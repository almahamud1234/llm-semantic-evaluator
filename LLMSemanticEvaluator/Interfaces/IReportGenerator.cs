// File: Core/Interfaces/IReportGenerator.cs
namespace LLMSemanticEvaluator.Interfaces;

// using LLMSemanticEvaluator.Models;

/// <summary>
/// Interface for generating test reports
/// </summary>
public interface IReportGenerator
{
    /// <summary>
    /// Generates a report from test results
    /// </summary>
    /// <param name="report">The test report data</param>
    /// <param name="outputPath">Path where to save the report</param>
    // Task GenerateReportAsync(TestReport report, string outputPath);
    Task GenerateReportAsync(string report, string outputPath);

    /// <summary>
    /// Generates a console-friendly summary
    /// </summary>
    /// <param name="report">The test report data</param>
    /// <returns>Formatted report string</returns>
    string GenerateConsoleSummary(string report);
}