using LLMSemanticEvaluator.Configuration;
using LLMSemanticEvaluator.Models;

namespace LLMSemanticEvaluator
{
    public class Program
    {
        
        static async Task Main(string[] args)
        {
            // ─── Startup API key che──────────────────────────────────────────────────────
            TestConfiguration config;
            try
            {
                config = TestConfiguration.Load();

                // Fail fast — don't waste time loading 120 tests if key is missing
                if (string.IsNullOrWhiteSpace(config.OpenAIApiKey) || config.OpenAIApiKey == "your-api-key-here")
                {
                    Console.WriteLine("[Error] OpenAI API key is missing or is still the placeholder.");
                    Console.WriteLine("Set your key in appsettings.json before running.");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to load configuration: {ex.Message}");
                return;
            }

            // ─── Setup ───────────────────────────────────────────────────────────────────
            using var client = new OpenAIClient(config);

            var embeddingValidator = new EmbeddingValidator(client, new CosineSimilarityCalculator(), threshold: 0.75);
            var judgeValidator     = new LLMJudgeValidator(client, threshold: 8);
            var runner             = new TestRunner(client, embeddingValidator, judgeValidator, runsPerTest: 3);
            var loader             = new JsonTestLoader();
            var reportGenerator    = new ReportGenerator("reports");

            // ─── Load Test Cases ──────────────────────────────────────────────────────────
            List<TestCase> testCases;

            try
            {
                Console.WriteLine("Loading test cases...");
                testCases = await loader.LoadTestsAsync("data/sample_test_cases.json");
                if (testCases.Count == 0)
                {
                    Console.WriteLine("[Error] No test cases were loaded. Check your data/sample_test_cases file.");
                    return;
                }
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
                Console.WriteLine($"[Error] Test run failed unexpectedly: {ex.Message}");
                return;
            }

            // ─── Generate Report ──────────────────────────────────────────────────────────
            await reportGenerator.GenerateAsync(results);

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
