using LLMSemanticEvaluator.Models;

namespace LLMSemanticEvaluator;

/// <summary>
/// Demo program to test the JsonTestLoader
/// </summary>
public class TestLoaderDemo
{
    public static async Task Main(string[] args)
    {
        System.Console.WriteLine("=".PadRight(60, '='));
        System.Console.WriteLine("LLM Semantic Evaluator - TestLoader Demo");
        System.Console.WriteLine("=".PadRight(60, '='));
        System.Console.WriteLine();

        var loader = new JsonTestLoader();

        // // Test 1: Load sample test cases
        // await TestLoadFile(loader, "../data/sample_test_cases.json", "Sample Test Cases (with wrapper)");

        // System.Console.WriteLine();

        // Test 2: Load quick tests
        await TestLoadFile(loader, "../data/quick_tests.json", "Quick Tests (direct array)");

        System.Console.WriteLine();
        System.Console.WriteLine("=".PadRight(60, '='));
        System.Console.WriteLine("Demo Complete!");
        System.Console.WriteLine("=".PadRight(60, '='));
    }

    private static async Task TestLoadFile(JsonTestLoader loader, string filePath, string description)
    {
        Console.WriteLine(loader);
        Console.WriteLine(filePath);
        return;
    }
}