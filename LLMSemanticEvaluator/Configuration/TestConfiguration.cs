namespace LLMSemanticEvaluator.Configuration;

/// <summary>
/// Configuration for test execution
/// </summary>
public class TestConfiguration
{
    /// <summary>
    /// OpenAI API key
    /// </summary>
    public string OpenAIApiKey { get; set; } = string.Empty;

    /// <summary>
    /// OpenAI model to use for chat completions (e.g., "gpt-4")
    /// </summary>
    public string ChatModel { get; set; } = "gpt-4";

    /// <summary>
    /// OpenAI model to use for embeddings (e.g., "text-embedding-ada-002")
    /// </summary>
    public string EmbeddingModel { get; set; } = "text-embedding-ada-002";

    /// <summary>
    /// Minimum cosine similarity score to pass (0.0 to 1.0)
    /// </summary>
    public double EmbeddingThreshold { get; set; } = 0.85;

    /// <summary>
    /// Minimum judge score to pass (1-10)
    /// </summary>
    public int JudgeThreshold { get; set; } = 8;

    /// <summary>
    /// Number of times to run each test
    /// </summary>
    public int NumberOfRuns { get; set; } = 3;

    /// <summary>
    /// Minimum number of runs that must pass for overall pass
    /// </summary>
    public int MinimumPassingRuns { get; set; } = 2;

    /// <summary>
    /// Maximum number of concurrent API requests
    /// </summary>
    public int MaxConcurrency { get; set; } = 5;

    /// <summary>
    /// Delay between API requests (milliseconds)
    /// </summary>
    public int RequestDelayMs { get; set; } = 100;

    /// <summary>
    /// Timeout for API requests (seconds)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}