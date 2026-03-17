using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LLMSemanticEvaluator.Configuration;
using LLMSemanticEvaluator.Interfaces;

namespace LLMSemanticEvaluator;

/// <summary>
/// Communicates with any OpenAI-compatible REST API.
/// Used for both OpenAI and Grok (which shares the same API shape).
/// The base URL and API key are injected by <see cref="LLMClientFactory"/>,
/// so this class never needs to know which provider it is talking to.
/// </summary>

public sealed class OpenAIClient : ILLMClient, IEmbeddingProvider, IDisposable
{
    private readonly string _chatEndpoint;
    private readonly string _embeddingEndpoint;

    private readonly HttpClient            _httpClient;
    private readonly TestConfiguration    _config;
    private readonly JsonSerializerOptions _jsonOptions;


    /// <param name="config">Shared configuration (timeout, delay, models, temperature).</param>
    /// <param name="baseUrl">API base URL — e.g. https://api.openai.com/v1 or https://api.x.ai/v1</param>
    /// <param name="apiKey">Bearer token for this provider.</param>
    public OpenAIClient(TestConfiguration config, string baseUrl, string apiKey)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        // _config.Validate();

        // Normalise base URL (strip trailing slash) and build endpoints
        baseUrl            = baseUrl.TrimEnd('/');
        _chatEndpoint      = $"{baseUrl}/chat/completions";
        _embeddingEndpoint = $"{baseUrl}/embeddings";

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
        };

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Accept
            .Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull
        };
    }

    // -------------------------------------------------------------------------
    // Chat Completions
    // -------------------------------------------------------------------------

    public async Task<string> SendPromptAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt must not be empty.", nameof(prompt));
                                    
        var requestBody = new ChatRequest
        {
            Model    = _config.ChatModel,
            Messages = [new ChatMessage { Role = "user", Content = prompt }]
        };

        if (_config.ChatModel.StartsWith("gpt-5"))
            requestBody.MaxCompletionTokens = 1000;
        else
            requestBody.MaxTokens = 1000;

        if (!_config.ChatModel.StartsWith("gpt-5"))
        {
            requestBody.Temperature = _config.Temperature;
        }

        var responseJson = await PostAsync(_chatEndpoint, requestBody, cancellationToken);
        var response     = Deserialize<ChatResponse>(responseJson);

        var content = response?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Provider returned an empty chat response.");

        return content;
    }

    // -------------------------------------------------------------------------
    // Embeddings
    // -------------------------------------------------------------------------

    public async Task<float[]> GetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
        => await GenerateEmbeddingAsync(text, cancellationToken);

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text must not be empty.", nameof(text));

        var requestBody = new EmbeddingRequest
        {
            Model = _config.EmbeddingModel,
            Input = text
        };

        var responseJson = await PostAsync(_embeddingEndpoint, requestBody, cancellationToken);
        var response     = Deserialize<EmbeddingResponse>(responseJson);

        var vector = response?.Data?.FirstOrDefault()?.Embedding;
        if (vector == null || vector.Length == 0)
            throw new InvalidOperationException("Provider returned an empty embedding vector.");

        return vector;
    }

    // -------------------------------------------------------------------------
    // Core HTTP helper
    // -------------------------------------------------------------------------

    private async Task<string> PostAsync(
        string endpoint,
        object requestBody,
        CancellationToken cancellationToken)
    {
        // Optional per-request delay (rate-limit guard)
        if (_config.RequestDelayMs > 0)
            await Task.Delay(_config.RequestDelayMs, cancellationToken);

        var json    = JsonSerializer.Serialize(requestBody, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var httpResponse = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        var body         = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"API error {(int)httpResponse.StatusCode}: {body}");

        return body;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private T Deserialize<T>(string json)
    {
        var result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
        if (result == null)
            throw new InvalidOperationException(
                $"Failed to deserialize response as {typeof(T).Name}.");
        return result;
    }

    public void Dispose() => _httpClient.Dispose();

    // -------------------------------------------------------------------------
    // Private DTOs — Chat
    // -------------------------------------------------------------------------

    private sealed class ChatRequest
    {
        public string            Model       { get; set; } = string.Empty;
        public List<ChatMessage> Messages    { get; set; } = [];
        public double?            Temperature { get; set; }

        // Older models use max_tokens, newer models (gpt-5-mini, o1, o3 etc.) use max_completion_tokens.
        // Only one should be serialized — the other stays null and is excluded via WhenWritingNull.
        [JsonPropertyName("max_tokens")]
        public int? MaxTokens           { get; set; }

        [JsonPropertyName("max_completion_tokens")]
        public int? MaxCompletionTokens { get; set; }
    }

    private sealed class ChatMessage
    {
        public string Role    { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private sealed class ChatResponse
    {
        public List<ChatChoice>? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        public ChatMessage? Message { get; set; }
    }

    // -------------------------------------------------------------------------
    // Private DTOs — Embeddings
    // -------------------------------------------------------------------------

    private sealed class EmbeddingRequest
    {
        public string Model { get; set; } = string.Empty;
        public string Input { get; set; } = string.Empty;
    }

    private sealed class EmbeddingResponse
    {
        public List<EmbeddingData>? Data { get; set; }
    }

    private sealed class EmbeddingData
    {
        public float[]? Embedding { get; set; }
    }
}