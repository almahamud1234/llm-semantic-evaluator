using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LLMSemanticEvaluator.Configuration;
using LLMSemanticEvaluator.Interfaces;

namespace LLMSemanticEvaluator;

/// <summary>
/// Communicates with a locally running Ollama instance (http://localhost:11434 by default).
///
/// DIFFERENCES FROM OpenAIClient:
///   - No API key — Ollama runs locally and requires no authentication.
///   - Chat endpoint : POST /api/chat    (response field: message.content)
///   - Embedding endpoint: POST /api/embeddings (response field: embedding — a flat float[])
///   - Requires "stream": false to get a single JSON response instead of a stream.
///
/// SETUP:
///   1. Install Ollama from https://ollama.com
///   2. Pull a model: `ollama pull llama3`
///   3. Set ChatModel to "llama3" (or whichever model you pulled) in appsettings.json
///   4. Set EmbeddingModel to "nomic-embed-text" (good general-purpose embedding model)
///      and pull it first: `ollama pull nomic-embed-text`
/// </summary>
public sealed class OllamaClient : ILLMClient, IEmbeddingProvider, IDisposable
{
    private readonly string                _chatEndpoint;
    private readonly string                _embeddingEndpoint;
    private readonly HttpClient            _httpClient;
    private readonly TestConfiguration    _config;
    private readonly JsonSerializerOptions _jsonOptions;

    public OllamaClient(TestConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        var baseUrl        = config.OllamaBaseUrl.TrimEnd('/');
        _chatEndpoint      = $"{baseUrl}/api/chat";
        _embeddingEndpoint = $"{baseUrl}/api/embeddings";

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
        };

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

        var requestBody = new OllamaChatRequest
        {
            Model    = _config.ChatModel,
            Messages = [new OllamaChatMessage { Role = "user", Content = prompt }],
            Stream   = false,           // get one complete JSON response, not a stream
            Options  = new OllamaOptions { Temperature = _config.Temperature }
        };

        var responseJson = await PostAsync(_chatEndpoint, requestBody, cancellationToken);
        var response     = Deserialize<OllamaChatResponse>(responseJson);

        var content = response?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Ollama returned an empty chat response.");

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

        var requestBody = new OllamaEmbeddingRequest
        {
            Model  = _config.EmbeddingModel,
            Prompt = text
        };

        var responseJson = await PostAsync(_embeddingEndpoint, requestBody, cancellationToken);
        var response     = Deserialize<OllamaEmbeddingResponse>(responseJson);

        if (response?.Embedding == null || response.Embedding.Length == 0)
            throw new InvalidOperationException("Ollama returned an empty embedding vector.");

        return response.Embedding;
    }

    // -------------------------------------------------------------------------
    // Core HTTP helper
    // -------------------------------------------------------------------------

    private async Task<string> PostAsync(
        string endpoint,
        object requestBody,
        CancellationToken cancellationToken)
    {
        if (_config.RequestDelayMs > 0)
            await Task.Delay(_config.RequestDelayMs, cancellationToken);

        var json    = JsonSerializer.Serialize(requestBody, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var httpResponse = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        var body         = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Ollama error {(int)httpResponse.StatusCode}: {body}");

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
                $"Failed to deserialize Ollama response as {typeof(T).Name}.");
        return result;
    }

    public void Dispose() => _httpClient.Dispose();

    // -------------------------------------------------------------------------
    // Private DTOs — Chat
    // -------------------------------------------------------------------------

    private sealed class OllamaChatRequest
    {
        public string                  Model    { get; set; } = string.Empty;
        public List<OllamaChatMessage> Messages { get; set; } = [];
        public bool                    Stream   { get; set; } = false;
        public OllamaOptions?          Options  { get; set; }
    }

    private sealed class OllamaChatMessage
    {
        public string Role    { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OllamaOptions
    {
        public double Temperature { get; set; }
    }

    private sealed class OllamaChatResponse
    {
        public OllamaChatMessage? Message { get; set; }
    }

    // -------------------------------------------------------------------------
    // Private DTOs — Embeddings
    // -------------------------------------------------------------------------

    private sealed class OllamaEmbeddingRequest
    {
        public string Model  { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;   // Ollama uses "prompt", not "input"
    }

    private sealed class OllamaEmbeddingResponse
    {
        public float[]? Embedding { get; set; }   // flat array, not wrapped in data[]
    }
}