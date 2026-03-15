using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LLMSemanticEvaluator.Configuration;
using LLMSemanticEvaluator.Interfaces;

namespace LLMSemanticEvaluator;

/// <summary>
/// Communicates with the OpenAI REST API.
/// Supports chat completions and embeddings.
/// </summary>
public sealed class OpenAIClient : ILLMClient, IEmbeddingProvider, IDisposable
{
    private const string ChatEndpoint      = "https://api.openai.com/v1/chat/completions";
    private const string EmbeddingEndpoint = "https://api.openai.com/v1/embeddings";

    private readonly HttpClient            _httpClient;
    private readonly TestConfiguration    _config;
    private readonly JsonSerializerOptions _jsonOptions;

    public OpenAIClient(TestConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _config.Validate();

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
        };
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", config.OpenAIApiKey);
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
            Messages = [new ChatMessage { Role = "user", Content = prompt }],
            Temperature = _config.Temperature
        };

        var responseJson = await PostAsync(ChatEndpoint, requestBody, cancellationToken);
        var response     = Deserialize<ChatResponse>(responseJson);

        var content = response?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("OpenAI returned an empty chat response.");

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

        var responseJson = await PostAsync(EmbeddingEndpoint, requestBody, cancellationToken);
        var response     = Deserialize<EmbeddingResponse>(responseJson);

        var vector = response?.Data?.FirstOrDefault()?.Embedding;
        if (vector == null || vector.Length == 0)
            throw new InvalidOperationException("OpenAI returned an empty embedding vector.");

        return vector;
    }

    // -------------------------------------------------------------------------
    // Core HTTP helper — single attempt, no retry complexity
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
                $"OpenAI API error {(int)httpResponse.StatusCode}: {body}");

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
                $"Failed to deserialize OpenAI response as {typeof(T).Name}.");
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
        public int               MaxTokens   { get; set; } = 1000;
        public double            Temperature { get; set; }
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