using System.Text.Json;

namespace LLMSemanticEvaluator.Configuration;

/// <summary>
/// All settings for the test framework.
/// Edit appsettings.json to change any value.
/// </summary>
public class TestConfiguration
{
    // ── Required ────────────────────────────────────────────────────────────
    public string OpenAIApiKey     { get; set; } = string.Empty;

    // ── OpenAI models ───────────────────────────────────────────────────────
    public string ChatModel        { get; set; } = "gpt-4o-mini";
    public string EmbeddingModel   { get; set; } = "text-embedding-3-small";
    public double Temperature { get; set; } = 0.0;  // default 0 for reproducibility

    // ── Thresholds ──────────────────────────────────────────────────────────
    public double EmbeddingThreshold { get; set; } = 0.85;  // 0.0 – 1.0
    public int    JudgeThreshold     { get; set; } = 8;      // 1 – 10

    // ── Test execution ──────────────────────────────────────────────────────
    public int NumberOfRuns       { get; set; } = 3;
    public int MinimumPassingRuns { get; set; } = 2;

    // ── HTTP ────────────────────────────────────────────────────────────────
    public int TimeoutSeconds { get; set; } = 30;
    public int RequestDelayMs { get; set; } = 200;   // pause between calls

    // ────────────────────────────────────────────────────────────────────────
    // Load from appsettings.json
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads appsettings.json and returns a populated <see cref="TestConfiguration"/>.
    /// </summary>
    public static TestConfiguration Load(string path = "appsettings.json")
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Settings file not found: {path}");

        var json    = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var config  = JsonSerializer.Deserialize<TestConfiguration>(json, options)
                      ?? throw new InvalidOperationException("appsettings.json could not be parsed.");

        return config;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Basic validation
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Throws if the API key is missing or still set to the placeholder value.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(OpenAIApiKey) ||
            OpenAIApiKey == "YOUR_OPENAI_API_KEY_HERE")
            throw new InvalidOperationException(
                "OpenAIApiKey is not set. Edit appsettings.json and add your key.");
    }
}