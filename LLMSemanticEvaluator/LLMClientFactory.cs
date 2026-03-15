using LLMSemanticEvaluator.Configuration;
using LLMSemanticEvaluator.Interfaces;

namespace LLMSemanticEvaluator;

/// <summary>
/// Creates the correct LLM client based on the "Provider" field in appsettings.json.
///
/// SUPPORTED PROVIDERS:
///   "openai" — OpenAI API  (requires OpenAIApiKey)
///   "grok"   — xAI Grok    (requires GrokApiKey; shares the OpenAI-compatible API shape)
///   "ollama" — Local Ollama (requires Ollama running locally; no API key needed)
///
/// HOW TO ADD A NEW PROVIDER IN FUTURE:
///   1. Add its API key / base URL fields to TestConfiguration.
///   2. Either create a new client class (if the API shape is different)
///      or reuse OpenAIClient with a different base URL (if OpenAI-compatible).
///   3. Add one case to the switch in Create() and CreateEmbeddingProvider() below.
///   That's it — nothing else in the codebase needs to change.
/// </summary>
public static class LLMClientFactory
{
    /// <summary>
    /// Returns an <see cref="ILLMClient"/> for the configured provider.
    /// The caller is responsible for disposing the returned object.
    /// </summary>
    public static ILLMClient Create(TestConfiguration config)
    {
        config.Validate();

        return config.Provider.ToLowerInvariant() switch
        {
            "openai" => new OpenAIClient(config,
                            baseUrl: "https://api.openai.com/v1",
                            apiKey:  config.OpenAIApiKey),

            "grok"   => new OpenAIClient(config,
                            baseUrl: "https://api.x.ai/v1",
                            apiKey:  config.GrokApiKey),

            "ollama" => new OllamaClient(config),

            _ => throw new InvalidOperationException(
                     $"Unknown provider '{config.Provider}'. Valid values: openai, grok, ollama.")
        };
    }

    /// <summary>
    /// Returns an <see cref="IEmbeddingProvider"/> for the configured provider.
    /// Note: the same client instance implements both interfaces, so in Program.cs
    /// you can cast the ILLMClient to IEmbeddingProvider rather than creating two objects.
    /// </summary>
    public static IEmbeddingProvider CreateEmbeddingProvider(TestConfiguration config)
    {
        var provider = config.EmbeddingProvider.ToLowerInvariant();

        ILLMClient client = provider switch
        {
            "openai" => new OpenAIClient(config,
                            baseUrl: "https://api.openai.com/v1",
                            apiKey:  config.OpenAIApiKey),

            "ollama" => new OllamaClient(config),

            "grok"   => throw new InvalidOperationException(
                            "Grok does not support embeddings. " +
                            "Set EmbeddingProvider to \"openai\" or \"ollama\" in appsettings.json."),

            _ => throw new InvalidOperationException(
                    $"Unknown embedding provider '{config.EmbeddingProvider}'. Valid values: openai, ollama.")
        };

        return (IEmbeddingProvider)client;
    }
}