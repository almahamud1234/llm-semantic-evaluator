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
    /// OpenAI model to use for chat completions (e.g., "gpt-4o-mini")
    /// </summary>
    public string ChatModel { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// OpenAI model to use for embeddings (e.g., "text-embedding-3-small")
    /// </summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

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

    // -----------------------------------------------------------------------
    // Validation
    // -----------------------------------------------------------------------

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if any required field is missing
    /// or any value is out of range.
    /// </summary>
    public void Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(OpenAIApiKey) ||
            OpenAIApiKey == "YOUR_OPENAI_API_KEY_HERE")
            errors.Add("OpenAIApiKey is not set. Add it to appsettings.json or as environment variable.");

        if (string.IsNullOrWhiteSpace(ChatModel))
            errors.Add("ChatModel must not be empty.");

        if (string.IsNullOrWhiteSpace(EmbeddingModel))
            errors.Add("EmbeddingModel must not be empty.");

        if (EmbeddingThreshold is < 0 or > 1)
            errors.Add("EmbeddingThreshold must be between 0.0 and 1.0.");

        if (JudgeThreshold is < 1 or > 10)
            errors.Add("JudgeThreshold must be between 1 and 10.");

        if (NumberOfRuns < 1)
            errors.Add("NumberOfRuns must be at least 1.");

        if (MinimumPassingRuns < 1 || MinimumPassingRuns > NumberOfRuns)
            errors.Add($"MinimumPassingRuns ({MinimumPassingRuns}) must be between 1 and NumberOfRuns ({NumberOfRuns}).");

        if (MaxConcurrency < 1)
            errors.Add("MaxConcurrency must be at least 1.");

        if (RequestDelayMs < 0)
            errors.Add("RequestDelayMs must be >= 0.");

        if (TimeoutSeconds < 1)
            errors.Add("TimeoutSeconds must be at least 1.");

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "TestConfiguration validation failed:\n• " + string.Join("\n• ", errors));
    }
}