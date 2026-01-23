// File: Core/Interfaces/ITestLoader.cs
namespace LLMPromptTesting.Console.Core.Interfaces;

// using LLMPromptTesting.Console.Core.Models;

/// <summary>
/// Interface for loading test cases
/// </summary>
public interface ITestLoader
{
    /// <summary>
    /// Loads test cases from a file
    /// </summary>
    /// <param name="filePath">Path to the test cases file</param>
    /// <returns>List of test cases</returns>
    // Task<List<TestCase>> LoadTestsAsync(string filePath);
    Task LoadTestsAsync(string filePath);
}