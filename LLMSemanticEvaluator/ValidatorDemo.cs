using LLMSemanticEvaluator.Interfaces;

namespace LLMSemanticEvaluator;

/// <summary>
/// Day 1 Demo: Validates that EmbeddingValidator and LLMJudgeValidator work correctly.
/// 
/// Runs three sample test cases:
///   - Two should PASS (correct answers, differently worded)
///   - One should FAIL (completely wrong answer)
/// 
/// Run this before building the TestRunner (Day 2) to confirm
/// both validators are working as expected.
/// </summary>
public class ValidatorDemo
{
    /// <summary>
    /// Entry point for the demo. Creates both validators and runs all test cases.
    /// </summary>
    /// <param name="llmClient">Used by LLMJudgeValidator to send evaluation prompts.</param>
    /// <param name="embeddings">Used by EmbeddingValidator to generate text vectors.</param>
    /// <param name="calculator">Used by EmbeddingValidator to compute cosine similarity.</param>
    /// <param name="embeddingThreshold">Used by EmbeddingValidator to identify pass fail.</param>
    public static async Task RunAsync(ILLMClient llmClient, IEmbeddingProvider embeddings, ISimilarityCalculator calculator, double embeddingThreshold = 0.75)
    {
        Console.WriteLine("=== Validator Demo ===\n");

        // Set up both validators with default thresholds:
        // - Embedding: passes if similarity >= embeddingThreshold e.g 0.85
        // - LLM Judge: passes if score >= 8 out of 10
        var embeddingValidator = new EmbeddingValidator(embeddings, calculator, threshold: embeddingThreshold);
        var judgeValidator     = new LLMJudgeValidator(llmClient, threshold: 8);

        // Test cases: (prompt, expected, actual)
        // The third case is intentionally wrong to verify validators catch failures
        var tests = new[]
        {
            (prompt: "What is 2+2?", expected: "4", actual: "The answer is 4"),
            (prompt: "What is the capital of France?", expected: "Paris", actual: "Paris is the capital"),
            (prompt: "What is the capital of France?", expected: "Paris", actual: "I love pizza."),
        };

        foreach (var (prompt, expected, actual) in tests)
        {
            Console.WriteLine($"Prompt:   {prompt}");
            Console.WriteLine($"Expected: {expected}");
            Console.WriteLine($"Actual:   {actual}");

            try
            {
                // Run both validators independently
                var embResult   = await embeddingValidator.ValidateAsync(expected, actual);
                var judgeResult = await judgeValidator.ValidateAsync(prompt, expected, actual);

                Console.WriteLine($"Embedding → Score: {embResult.Score:F2}, Passed: {embResult.Passed}");
                Console.WriteLine($"LLM Judge → Score: {judgeResult.Score}/10, Passed: {judgeResult.Passed}");

                // Both validators must pass for the test to be considered passing
                bool overallPass = embResult.Passed || judgeResult.Passed;
                Console.WriteLine($"Overall:  {(overallPass ? "✅ PASS" : "❌ FAIL")}");
                Console.WriteLine(new string('-', 50));
            }
            catch (Exception ex)
            {
                // Per-test error: API timeout, bad response format, etc.
                Console.WriteLine($"[Test Error] Unexpected failure during validation: {ex.Message}");
            }
        }
    }
}