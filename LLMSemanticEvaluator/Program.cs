using LLMSemanticEvaluator.Configuration;
using LLMSemanticEvaluator.Models;
using LLMSemanticEvaluator.Interfaces;

namespace LLMSemanticEvaluator
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            // ─── Load & validate configuration ───────────────────────────────────────
            TestConfiguration config;
            try
            {
                config = TestConfiguration.Load();
                config.Validate();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to load configuration: {ex.Message}");
                return;
            }

            Console.WriteLine($"Chat provider      : {config.Provider}");
            Console.WriteLine($"Embedding provider : {config.EmbeddingProvider}");
            Console.WriteLine($"Chat model         : {config.ChatModel}");
            Console.WriteLine($"Embedding model    : {config.EmbeddingModel}");
            Console.WriteLine($"Runs               : {config.NumberOfRuns}  |  " +
                              $"Emb threshold: {config.EmbeddingThreshold}  |  " +
                              $"Judge threshold: {config.JudgeThreshold}/10\n");

            // ─── Create chat client (used by LLM judge + sending prompts) ─────────────
            ILLMClient chatClient;
            try
            {
                chatClient = LLMClientFactory.Create(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Could not create chat client: {ex.Message}");
                return;
            }

            using var chatDisposable = chatClient as IDisposable;

            // ─── Create embedding client (always OpenAI or Ollama) ────────────────────
            IEmbeddingProvider embeddingClient;
            try
            {
                embeddingClient = LLMClientFactory.CreateEmbeddingProvider(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Could not create embedding client: {ex.Message}");
                return;
            }

            using var embDisposable = embeddingClient as IDisposable;

            // ─── Setup pipeline ───────────────────────────────────────────────────────
            var embeddingValidator = new EmbeddingValidator(
                                        embeddingClient,
                                        new CosineSimilarityCalculator(),
                                        threshold: config.EmbeddingThreshold);

            var judgeValidator     = new LLMJudgeValidator(chatClient,
                                        threshold: config.JudgeThreshold);

            var runner             = new TestRunner(chatClient, embeddingValidator, judgeValidator,
                                        runsPerTest: config.NumberOfRuns, minPassRun: config.MinimumPassingRuns);
            var loader             = new JsonTestLoader();
            var reportGenerator    = new ReportGenerator("reports");

            // ─── Load test cases ──────────────────────────────────────────────────────
            List<TestCase> testCases;
            try
            {
                Console.WriteLine("Loading test cases...");
                testCases = await loader.LoadTestsAsync("data/quick_tests.json");
                if (testCases.Count == 0)
                {
                    Console.WriteLine("[Error] No test cases were loaded.");
                    return;
                }
                Console.WriteLine($"Loaded {testCases.Count} test cases.\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to load test cases: {ex.Message}");
                return;
            }

            // ─── Run tests ────────────────────────────────────────────────────────────
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

            // ─── Generate reports ─────────────────────────────────────────────────────
            await reportGenerator.GenerateAsync(results);
        }
    }
}