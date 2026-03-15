using System.Text.Json;

namespace LLMSemanticEvaluator.Configuration;

/// <summary>
/// All settings for the test framework.
/// Edit appsettings.json to change any value.
/// </summary>
public class TestConfiguration
{
    // ── Provider selection ──────────────────────────────────────────────────
    /// >Which LLM provider to use: "openai" | "grok" | "ollama"
    public string Provider { get; set; } = "openai";

    /// Provider used exclusively for embeddings. Always "openai" recommended.
    public string EmbeddingProvider { get; set; } = "openai";

    // ── API keys ────────────────────────────────────────────────────────────
    public string OpenAIApiKey { get; set; } = string.Empty;
    public string GrokApiKey   { get; set; } = string.Empty;

    // ── Ollama (local, no key needed) ───────────────────────────────────────
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

    // ── Models ──────────────────────────────────────────────────────────────
    public string ChatModel      { get; set; } = "gpt-4o-mini";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    // ── Generation ──────────────────────────────────────────────────────────
    public double Temperature { get; set; } = 0.0;

    // ── Thresholds ──────────────────────────────────────────────────────────
    public double EmbeddingThreshold { get; set; } = 0.85;
    public int    JudgeThreshold     { get; set; } = 8;

    // ── Test execution ──────────────────────────────────────────────────────
    public int NumberOfRuns       { get; set; } = 3;
    public int MinimumPassingRuns { get; set; } = 2;

    // ── HTTP ────────────────────────────────────────────────────────────────
    public int TimeoutSeconds { get; set; } = 30;
    public int RequestDelayMs { get; set; } = 200;

    // ────────────────────────────────────────────────────────────────────────
    // Load
    // ────────────────────────────────────────────────────────────────────────

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
    // Validation
    // ────────────────────────────────────────────────────────────────────────

    public void Validate()
    {
        var p = Provider.ToLowerInvariant();

        if (p == "openai" &&
            (string.IsNullOrWhiteSpace(OpenAIApiKey) || OpenAIApiKey == "YOUR_OPENAI_API_KEY_HERE"))
            throw new InvalidOperationException(
                "OpenAIApiKey is not set. Edit appsettings.json and add your key.");

        if (p == "grok" &&
            (string.IsNullOrWhiteSpace(GrokApiKey) || GrokApiKey == "YOUR_GROK_API_KEY_HERE"))
            throw new InvalidOperationException(
                "GrokApiKey is not set. Edit appsettings.json and add your key.");

        if (p == "ollama" && string.IsNullOrWhiteSpace(OllamaBaseUrl))
            throw new InvalidOperationException(
                "OllamaBaseUrl is not set. Default is http://localhost:11434.");

        if (p != "openai" && p != "grok" && p != "ollama")
            throw new InvalidOperationException(
                $"Unknown provider '{Provider}'. Valid values: openai, grok, ollama.");
    }
}