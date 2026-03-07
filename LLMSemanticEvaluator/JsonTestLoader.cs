using System.Text.Json;
using LLMSemanticEvaluator.Interfaces;
using LLMSemanticEvaluator.Models;

namespace LLMSemanticEvaluator;

/// <summary>
/// Loads test cases from JSON file.
/// Supports two formats:
///   - Direct array:           [ {...}, {...} ]
///   - Object with wrapper:    { "tests": [ {...}, {...} ] }
/// </summary>
public class JsonTestLoader : ITestLoader
{
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonTestLoader()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true  // handles "Id" vs "id" etc.
        };
    }

    /// <summary>
    /// Loads test cases from a JSON file.
    /// Skips individual invalid test cases with a warning instead of throwing.
    /// </summary>
    /// <param name="filePath">Path to the JSON file.</param>
    /// <returns>List of test cases</returns>
    /// <exception cref="ArgumentException">If file path is null or empty.</exception>
    /// <exception cref="FileNotFoundException">If file doesn't exist</exception>
    /// <exception cref="JsonException">If the JSON is malformed and cannot be parsed at all.</exception>
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

        string jsonContent;

        try
        {
            // Read file content
            jsonContent = await File.ReadAllTextAsync(filePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not read file '{filePath}': {ex.Message}", ex);
        }

        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            throw new JsonException("Test file is empty");
        }

        // Parse JSON - expecting either an array or an object with "tests" property
        var testCases = new List<TestCase>();

        try
        {
            testCases = JsonSerializer.Deserialize<List<TestCase>>(jsonContent, _jsonOptions)
                        ?? throw new JsonException("JSON parsed to null");
        }
        catch (JsonException)
        {
            // Try wrapped format { "tests": [...] } as fallback
            try
            {
                var collection = JsonSerializer.Deserialize<TestCaseCollection>(jsonContent, _jsonOptions);
                testCases = collection?.Tests
                            ?? throw new JsonException("Unable to parse test cases from JSON — expected an array or object with 'tests' property");
            }
            catch (JsonException ex)
            {
                throw new JsonException($"Invalid JSON in '{filePath}': {ex.Message}", ex);
            }
        }

        // Validate test cases
        ValidateTestCases(testCases);

        return testCases;
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

        // Validate each test case individually — skip bad ones with a warning
        // instead of throwing and losing all valid tests in the file
        var validTests    = new List<TestCase>();
        var duplicateCheck = new HashSet<string>();

        for (int i = 0; i < testCases.Count; i++)
        {
            var test = testCases[i];
            var errors = new List<string>();

            // Required fields
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