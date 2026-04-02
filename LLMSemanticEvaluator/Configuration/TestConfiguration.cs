namespace LLMSemanticEvaluator.Configuration;

/// <summary>
/// Strongly-typed representation of all settings in appsettings.json.
///
/// This class is a plain data object (POCO). It does not load files, parse JSON,
/// or validate itself. Those responsibilities are handled by the framework:
///   - Loading  : Microsoft.Extensions.Configuration reads appsettings.json.
///   - Binding  : services.Configure&lt;TestConfiguration&gt;() maps every JSON key
///                to the matching property here.
///   - Injection: services receive IOptions&lt;TestConfiguration&gt; and read .Value.
///
/// To change a setting, edit appsettings.json — never hard-code values in code.
/// </summary>
public class TestConfiguration
{
    // ── Provider ───────────────────────────────────────────────────────────────

    /// <summary>Chat LLM provider. Accepted values: "openai" | "grok" | "ollama".</summary>
    public string Provider { get; set; } = "openai";

    /// <summary>
    /// Embedding provider. Accepted values: "openai" | "ollama".
    /// Grok does not expose an embeddings endpoint and is not supported here.
    /// </summary>
    public string EmbeddingProvider { get; set; } = "openai";

    // ── Authentication ─────────────────────────────────────────────────────────

    /// <summary>
    /// API key for the active provider (OpenAI or Grok).
    /// A single ApiKey field is used for all cloud providers so that switching
    /// providers requires only changing Provider and ApiKey — not renaming fields.
    /// Not required when Provider is "ollama".
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    // ── Ollama ─────────────────────────────────────────────────────────────────

    /// <summary>Base URL of the locally running Ollama instance.</summary>
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

    // ── Models ─────────────────────────────────────────────────────────────────

    /// <summary>Model name used for all chat completion requests.</summary>
    public string ChatModel { get; set; } = "gpt-4o-mini";

    /// <summary>Model name used to generate embedding vectors.</summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    // ── Generation ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Sampling temperature. 0.0 produces near-deterministic output.
    /// Strongly recommended for reproducible test results.
    /// </summary>
    public double Temperature { get; set; } = 0.0;

    // ── Validation thresholds ──────────────────────────────────────────────────

    /// <summary>
    /// Minimum cosine similarity (0.0–1.0) for the embedding validator to pass a run.
    /// Default 0.85 — empirically validated on the 130-case test dataset.
    /// </summary>
    public double EmbeddingThreshold { get; set; } = 0.85;

    /// <summary>
    /// Minimum judge score (1–10) for the LLM judge validator to pass a run.
    /// Default 8, consistent with G-Eval convention: 8–10 = correct and relevant.
    /// </summary>
    public int JudgeThreshold { get; set; } = 8;

    // ── Test execution ─────────────────────────────────────────────────────────

    /// <summary>
    /// How many times each test case is executed. Default 3.
    /// Repeated runs account for LLM non-determinism.
    /// </summary>
    public int NumberOfRuns { get; set; } = 3;

    /// <summary>
    /// Minimum number of passing runs for a test case to be marked as passed.
    /// Default 2 of 3 (majority vote) — tolerates one outlier run per test.
    /// </summary>
    public int MinimumPassingRuns { get; set; } = 2;

    // ── HTTP ───────────────────────────────────────────────────────────────────

    /// <summary>Per-request HTTP timeout in seconds. Increase for slow local models.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Milliseconds to wait between API requests — guards against rate limiting.</summary>
    public int RequestDelayMs { get; set; } = 200;

    public string TestCasesPath { get; set; } = "data/sample_test_cases.json";
}