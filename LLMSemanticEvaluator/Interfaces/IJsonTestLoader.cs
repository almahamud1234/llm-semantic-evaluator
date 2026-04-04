using LLMSemanticEvaluator.Models;

namespace LLMSemanticEvaluator.Interfaces;

/// <summary>
/// Interface for loading test cases
/// </summary>
public interface IJsonTestLoader
{
    /// <summary>
    /// Loads test cases from a file
    /// </summary>
    /// <param name="filePath">Path to the test cases file</param>
    /// <returns>List of test cases</returns>
    Task<List<TestCase>> LoadTestsAsync(string filePath);
}