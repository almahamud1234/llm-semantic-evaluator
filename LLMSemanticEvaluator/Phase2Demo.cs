using LLMSemanticEvaluator.Configuration;

namespace LLMSemanticEvaluator;

/// <summary>
/// Phase 2 integration demo.
/// Loads config → creates OpenAIClient → runs a chat completion and an embedding.
/// No real API calls are made when the key is still the placeholder.
/// </summary>
public static class Phase2Demo
{
    public static async Task MainPhase(string[] args)
    {
        PrintBanner();

        // ── 1. Load configuration ────────────────────────────────────────────
        Section("Step 1 — Load Configuration");

        TestConfiguration config;
        try
        {
            config = TestConfiguration.Load("appsettings.json");
            Console.WriteLine($"  ChatModel        : {config.ChatModel}");
            Console.WriteLine($"  EmbeddingModel   : {config.EmbeddingModel}");
            Console.WriteLine($"  EmbeddingThreshold: {config.EmbeddingThreshold}");
            Console.WriteLine($"  Runs per test    : {config.NumberOfRuns}  (pass if ≥ {config.MinimumPassingRuns})");
            Console.WriteLine($"  API key set      : {(IsRealKey(config.OpenAIApiKey) ? "YES ✓" : "NO — running in mock mode")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Could not load config: {ex.Message}");
            return;
        }

        // ── 2. Similarity calculator (always works — no API needed) ──────────
        Section("Step 2 — Cosine Similarity (no API required)");
        DemoSimilarity();

        // ── 3. OpenAI calls (real or mock) ───────────────────────────────────
        Section("Step 3 — OpenAI Integration");

        if (IsRealKey(config.OpenAIApiKey))
            await DemoRealOpenAI(config);
        else
            DemoMockMode();

        PrintFooter();
    }

    // ────────────────────────────────────────────────────────────────────────

    private static void DemoSimilarity()
    {
        var calc = new CosineSimilarityCalculator();

        var pairs = new (string label, float[] a, float[] b)[]
        {
            ("Identical",          [1, 2, 3],          [1, 2, 3]),
            ("Very similar",       [0.9f, 0.1f, 0.4f], [0.91f, 0.09f, 0.41f]),
            ("Moderately similar", [1, 0, 0, 1],        [1, 0, 1, 0]),
            ("Orthogonal",         [1, 0],              [0, 1]),
        };

        foreach (var (label, a, b) in pairs)
        {
            var score  = calc.CalculateCosineSimilarity(a, b);
            var interp = CosineSimilarityCalculator.InterpretScore(score);
            var status = score >= 0.85 ? "PASS ✓" : "FAIL ✗";
            Console.WriteLine($"  {label,-22}  score={score:F4}  ({interp})  → {status}");
        }
    }

    private static async Task DemoRealOpenAI(TestConfiguration config)
    {
        Console.WriteLine("  Using real OpenAI API...");
        using var client = new OpenAIClient(config);
        var calc         = new CosineSimilarityCalculator();

        // Chat
        Console.WriteLine();
        try
        {
            var prompt   = "What is the capital of France? Answer in one word.";
            var response = await client.SendPromptAsync(prompt);
            Console.WriteLine($"  Prompt   : {prompt}");
            Console.WriteLine($"  Response : {response}");
            Console.WriteLine("  ✓ Chat completion working");
        }
        catch (Exception ex) { Console.WriteLine($"  ✗ Chat error: {ex.Message}"); }

        // Embeddings
        Console.WriteLine();
        try
        {
            var textA = "Paris";
            var textB = "The capital of France is Paris";
            var embA  = await client.GenerateEmbeddingAsync(textA);
            var embB  = await client.GenerateEmbeddingAsync(textB);
            var sim   = calc.CalculateCosineSimilarity(embA, embB);
            var passes = sim >= config.EmbeddingThreshold;

            Console.WriteLine($"  \"{textA}\"  vs  \"{textB}\"");
            Console.WriteLine($"  Similarity : {sim:F4} ({CosineSimilarityCalculator.InterpretScore(sim)})");
            Console.WriteLine($"  Threshold  : {config.EmbeddingThreshold}  →  {(passes ? "PASS ✓" : "FAIL ✗")}");
        }
        catch (Exception ex) { Console.WriteLine($"  ✗ Embedding error: {ex.Message}"); }
    }

    private static void DemoMockMode()
    {
        Console.WriteLine("  No real key found — showing simulated output.");
        Console.WriteLine();

        var calc = new CosineSimilarityCalculator();
        float[] mockA = [0.23f, -0.45f, 0.71f, 0.12f, -0.33f, 0.58f, -0.11f, 0.87f];
        float[] mockB = [0.24f, -0.43f, 0.70f, 0.14f, -0.31f, 0.56f, -0.10f, 0.85f];

        var sim    = calc.CalculateCosineSimilarity(mockA, mockB);
        var passes = sim >= 0.85;

        Console.WriteLine("  [Chat]  prompt=\"What is the capital of France?\"  response=\"Paris\"  ✓");
        Console.WriteLine($"  [Embed] \"Paris\" vs \"The capital of France is Paris\"");
        Console.WriteLine($"          simulated similarity={sim:F4}  →  {(passes ? "PASS ✓" : "FAIL ✗")}");
        Console.WriteLine();
        Console.WriteLine("  To switch to real calls: set OpenAIApiKey in appsettings.json");
    }

    // ────────────────────────────────────────────────────────────────────────

    private static bool IsRealKey(string key) =>
        !string.IsNullOrWhiteSpace(key) &&
        key.StartsWith("sk-") &&
        key != "YOUR_OPENAI_API_KEY_HERE";

    private static void PrintBanner()
    {
        Console.WriteLine(new string('=', 65));
        Console.WriteLine("  LLM Prompt Testing Framework — Phase 2: LLM Integration");
        Console.WriteLine(new string('=', 65));
    }

    private static void PrintFooter()
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 65));
        Console.WriteLine("  Phase 2 complete! ✓");
        Console.WriteLine("  Next: Phase 3 — EmbeddingValidator + LLMJudgeValidator");
        Console.WriteLine(new string('=', 65));
    }

    private static void Section(string title) =>
        Console.WriteLine($"\n── {title} {new string('─', Math.Max(0, 61 - title.Length))}");
}