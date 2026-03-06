using LLMSemanticEvaluator.Configuration;
using LLMSemanticEvaluator.Models;

namespace LLMSemanticEvaluator
{
    public class Program
    {
        
        static async Task Main(string[] args)
        // async static void Main(string[] args)
        {
            // ─── Setup ───────────────────────────────────────────────────────────────────
            var config = TestConfiguration.Load();
            using var client = new OpenAIClient(config);

            var embeddingValidator = new EmbeddingValidator(client, new CosineSimilarityCalculator(), threshold: 0.75);
            var judgeValidator     = new LLMJudgeValidator(client, threshold: 8);
            var runner             = new TestRunner(client, embeddingValidator, judgeValidator, runsPerTest: 3);
            var loader             = new JsonTestLoader();

            // ─── Load Test Cases ──────────────────────────────────────────────────────────
            List<TestCase> testCases;

            try
            {
                testCases = await loader.LoadTestsAsync("data/sample_test_cases.json");
                Console.WriteLine($"Loaded {testCases.Count} test cases.\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to load test cases: {ex.Message}");
                return;
            }

            // ─── Run Tests ────────────────────────────────────────────────────────────────
            List<TestResult> results;

            try
            {
                results = await runner.RunAllAsync(testCases);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Test run failed: {ex.Message}");
                return;
            }

            // ─── Quick Summary ────────────────────────────────────────────────────────────
            int passed = results.Count(r => r.Passed);
            int failed = results.Count - passed;

            Console.WriteLine("\n=== Summary ===");
            Console.WriteLine($"Total:  {results.Count}");
            Console.WriteLine($"Passed: {passed}");
            Console.WriteLine($"Failed: {failed}");
            Console.WriteLine($"Pass Rate: {(double)passed / results.Count * 100:F1}%");
        }
    }
}
