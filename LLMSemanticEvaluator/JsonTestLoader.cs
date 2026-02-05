using System.Text.Json;
using LLMSemanticEvaluator.Interfaces;
using LLMSemanticEvaluator.Models;

namespace LLMSemanticEvaluator;

/// <summary>
/// Loads test cases from JSON files
/// </summary>
public class JsonTestLoader : ITestLoader
{
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonTestLoader()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }

    /// <summary>
    /// Loads test cases from a JSON file
    /// </summary>
    /// <param name="filePath">Path to the JSON file</param>
    /// <returns>List of test cases</returns>
    /// <exception cref="FileNotFoundException">If file doesn't exist</exception>
    /// <exception cref="JsonException">If JSON is invalid</exception>
    public async Task<List<TestCase>> LoadTestsAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Test file not found: {filePath}", filePath);
        }

        try
        {
            // Read file content
            string jsonContent = await File.ReadAllTextAsync(filePath);

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                throw new JsonException("Test file is empty");
            }

            // Parse JSON - expecting either an array or an object with "tests" property
            var testCases = new List<TestCase>();

            // Try to parse as TestCaseCollection first (with "tests" wrapper)
            try
            {
                var collection = JsonSerializer.Deserialize<TestCaseCollection>(jsonContent, _jsonOptions);
                if (collection?.Tests != null)
                {
                    testCases = collection.Tests;
                }
            }
            catch (JsonException)
            {
                // If that fails, try parsing as direct array
                var directArray = JsonSerializer.Deserialize<List<TestCase>>(jsonContent, _jsonOptions);
                if (directArray != null)
                {
                    testCases = directArray;
                }
                else
                {
                    throw new JsonException("Unable to parse test cases from JSON");
                }
            }

            // Validate test cases
            ValidateTestCases(testCases);

            return testCases;
        }
        catch (JsonException ex)
        {
            throw new JsonException($"Invalid JSON format in file: {filePath}. {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not FileNotFoundException and not ArgumentException)
        {
            throw new InvalidOperationException($"Error loading test cases from {filePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates that test cases have required fields
    /// </summary>
    private void ValidateTestCases(List<TestCase> testCases)
    {
        if (testCases == null || testCases.Count == 0)
        {
            throw new InvalidOperationException("No test cases found in file");
        }

        for (int i = 0; i < testCases.Count; i++)
        {
            var test = testCases[i];
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(test.Id))
            {
                errors.Add($"Test at index {i} is missing 'Id'");
            }

            if (string.IsNullOrWhiteSpace(test.Prompt))
            {
                errors.Add($"Test '{test.Id}' is missing 'Prompt'");
            }

            if (string.IsNullOrWhiteSpace(test.ExpectedOutput))
            {
                errors.Add($"Test '{test.Id}' is missing 'ExpectedOutput'");
            }

            // Category is optional but should be set to "general" if missing
            if (string.IsNullOrWhiteSpace(test.Category))
            {
                test.Category = "general";
            }

            // EvaluationCriteria is optional but helpful
            if (string.IsNullOrWhiteSpace(test.EvaluationCriteria))
            {
                test.EvaluationCriteria = "The response should match the expected output semantically";
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException($"Validation errors: {string.Join(", ", errors)}");
            }
        }

        // Check for duplicate IDs
        var duplicateIds = testCases
            .GroupBy(t => t.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Count > 0)
        {
            throw new InvalidOperationException($"Duplicate test IDs found: {string.Join(", ", duplicateIds)}");
        }
    }

    /// <summary>
    /// Helper class for deserializing JSON with "tests" wrapper
    /// </summary>
    private class TestCaseCollection
    {
        public List<TestCase> Tests { get; set; } = new();
    }
}