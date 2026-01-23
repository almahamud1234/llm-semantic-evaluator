namespace LLMSemanticEvaluator.Interfaces;

/// <summary>
/// Interface for LLM communication
/// </summary>
public interface ILLMClient
{
    /// <summary>
    /// Sends a prompt to the LLM and gets a response
    /// </summary>
    /// <param name="prompt">The prompt to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The LLM's response text</returns>
    Task<string> SendPromptAsync(string prompt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets embedding vector for the given text
    /// </summary>
    /// <param name="text">Text to convert to embedding</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Float array representing the embedding vector</returns>
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}