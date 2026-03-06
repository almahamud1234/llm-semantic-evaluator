using System.IO;
using LLMSemanticEvaluator.Models;

namespace LLMSemanticEvaluator;

/// <summary>
/// Demo program to test the JsonTestLoader
/// </summary>
public class TestLoaderDemo
{
    public static async Task MainTestLoader(string[] args)
    {
        System.Console.WriteLine("=".PadRight(60, '='));
        System.Console.WriteLine("LLM Semantic Evaluator - TestLoader Demo");
        System.Console.WriteLine("=".PadRight(60, '='));
        System.Console.WriteLine();

        var loader = new JsonTestLoader();
        var basePath = AppContext.BaseDirectory;

        // // Test 1: Load sample test cases
        // await TestLoadFile(loader, "../data/sample_test_cases.json", "Sample Test Cases (with wrapper)");

        // System.Console.WriteLine();

        // Test 2: Load quick tests
        var filePath = Path.Combine(basePath, "data", "sample_test_cases.json");
        await TestLoadFile(loader, filePath, "Quick Tests (direct array)");

        System.Console.WriteLine();
        System.Console.WriteLine("=".PadRight(60, '='));
        System.Console.WriteLine("Demo Complete!");
        System.Console.WriteLine("=".PadRight(60, '='));
    }

    private static async Task TestLoadFile(JsonTestLoader loader, string filePath, string description)
    {
        System.Console.WriteLine($"Testing: {description}");
        System.Console.WriteLine($"File: {filePath}");
        System.Console.WriteLine("-".PadRight(60, '-'));

        try
        {
            var testCases = await loader.LoadTestsAsync(filePath);

            System.Console.WriteLine($"✓ Successfully loaded {testCases.Count} test cases");
            System.Console.WriteLine();

            // Display summary by category
            var categories = testCases.GroupBy(t => t.Category);
            System.Console.WriteLine("Test Cases by Category:");
            foreach (var category in categories)
            {
                System.Console.WriteLine($"  - {category.Key}: {category.Count()} tests");
            }

            System.Console.WriteLine();
            System.Console.WriteLine("Sample Test Cases:");

            // Display first 3 tests
            foreach (var test in testCases.Take(3))
            {
                System.Console.WriteLine($"\n  ID: {test.Id}");
                System.Console.WriteLine($"  Category: {test.Category}");
                System.Console.WriteLine($"  Prompt: {test.Prompt}");
                System.Console.WriteLine($"  Expected: {test.ExpectedOutput}");
            }

            if (testCases.Count > 3)
            {
                System.Console.WriteLine($"\n  ... and {testCases.Count - 3} more tests");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"✗ Error: {ex.Message}");
            System.Console.WriteLine($"  Type: {ex.GetType().Name}");
        }
    }
}