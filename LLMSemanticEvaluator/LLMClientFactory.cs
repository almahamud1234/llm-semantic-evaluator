using LLMSemanticEvaluator.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OpenAI;

namespace LLMSemanticEvaluator;

/// <summary>
/// Creates the correct IChatClient and IEmbeddingGenerator for the provider
/// configured in appsettings.json.
///
/// Uses Microsoft.Extensions.AI standard interfaces so no other class
/// needs to know which provider is active.
///
/// OpenAI / Grok : Microsoft.Extensions.AI.OpenAI wraps the official OpenAI SDK.
///                 Grok uses the same API shape with a different base URL,
///                 which is why it reuses the same OpenAIClient — only the
///                 endpoint changes.
/// Ollama        : OllamaSharp is the Microsoft-recommended Ollama client.
///                 It implements IChatClient and IEmbeddingGenerator natively.
///                 A custom HttpClient is passed in so the timeout is controlled
///                 by TimeoutSeconds in appsettings.json instead of the .NET
///                 default of 100 seconds, which local models can easily exceed.
///                 Requires OllamaSharp v4 or later (HttpClient constructor overload).
///
/// Base URLs for cloud providers (OpenAI, Grok) are defined here and nowhere
/// else. OllamaBaseUrl is read from appsettings.json because users may run
/// Ollama on a non-default host or port.
///
/// To add a new provider:
///   1. Add its settings to TestConfiguration and appsettings.json.
///   2. Add one case to each switch below.
///   No other file needs to change.
/// </summary>
public class LLMClientFactory : ILLMClientFactory
{
    private const string OpenAiBaseUrl = "https://api.openai.com/v1";
    private const string GrokBaseUrl   = "https://api.x.ai/v1";

    private readonly TestConfiguration         _config;
    private readonly ILogger<LLMClientFactory> _logger;

    public LLMClientFactory(
        IOptions<TestConfiguration> options,
        ILogger<LLMClientFactory>   logger)
    {
        _config = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Creates an HttpClient configured with the Ollama base URL and the
    /// timeout from <see cref="TestConfiguration.TimeoutSeconds"/>.
    ///
    /// <para>
    /// The OllamaApiClient(HttpClient) constructor overload (available in
    /// OllamaSharp v4+) is the only way to inject a custom timeout. The Uri
    /// goes on the HttpClient as BaseAddress — it is not a separate argument.
    /// </para>
    ///
    /// <para>
    /// Set TimeoutSeconds to 0 or negative for <see cref="Timeout.InfiniteTimeSpan"/>,
    /// which is useful on slow hardware or in CI where a fixed deadline causes
    /// flaky failures.
    /// </para>
    /// </summary>
    private HttpClient CreateOllamaHttpClient()
    {
        var timeout = _config.TimeoutSeconds > 0
            ? TimeSpan.FromSeconds(_config.TimeoutSeconds)
            : Timeout.InfiniteTimeSpan;

        _logger.LogInformation(
            "Ollama HttpClient timeout set to {Timeout}",
            _config.TimeoutSeconds > 0 ? $"{_config.TimeoutSeconds}s" : "infinite");

        return new HttpClient
        {
            BaseAddress = new Uri(_config.OllamaBaseUrl),
            Timeout     = timeout
        };
    }

    /// <summary>
    /// Returns an IChatClient for the provider set in appsettings.json.
    /// </summary>
    public IChatClient CreateChatClient()
    {
        _logger.LogInformation(
            "Creating chat client for provider '{Provider}', model '{Model}'",
            _config.Provider, _config.ChatModel);

        return _config.Provider.ToLowerInvariant() switch
        {
            "openai" => new OpenAIClient(
                            new System.ClientModel.ApiKeyCredential(_config.ApiKey))
                            .GetChatClient(_config.ChatModel)
                            .AsIChatClient(),

            "grok"   => new OpenAIClient(
                            new System.ClientModel.ApiKeyCredential(_config.ApiKey),
                            new OpenAIClientOptions { Endpoint = new Uri(GrokBaseUrl) })
                            .GetChatClient(_config.ChatModel)
                            .AsIChatClient(),

            "ollama" => new OllamaApiClient(CreateOllamaHttpClient())
                            { SelectedModel = _config.ChatModel },

            _ => throw new InvalidOperationException(
                     $"Unknown Provider '{_config.Provider}' in appsettings.json. " +
                     "Valid values: openai | grok | ollama")
        };
    }

    /// <summary>
    /// Returns an IEmbeddingGenerator for the provider set in appsettings.json.
    /// Grok is excluded because it does not expose an embeddings endpoint.
    /// </summary>
    public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator()
    {
        _logger.LogInformation(
            "Creating embedding generator for provider '{EmbeddingProvider}', model '{Model}'",
            _config.EmbeddingProvider, _config.EmbeddingModel);

        return _config.EmbeddingProvider.ToLowerInvariant() switch
        {
            "openai" => new OpenAIClient(
                            new System.ClientModel.ApiKeyCredential(_config.ApiKey))
                            .GetEmbeddingClient(_config.EmbeddingModel)
                            .AsIEmbeddingGenerator(),

            "ollama" => new OllamaApiClient(CreateOllamaHttpClient())
                            { SelectedModel = _config.EmbeddingModel },

            "grok"   => throw new InvalidOperationException(
                            "Grok does not provide an embeddings endpoint. " +
                            "Set EmbeddingProvider to 'openai' or 'ollama' in appsettings.json."),

            _ => throw new InvalidOperationException(
                     $"Unknown EmbeddingProvider '{_config.EmbeddingProvider}' in appsettings.json. " +
                     "Valid values: openai | ollama")
        };
    }
}