// File: Core/Interfaces/IEmbeddingProvider.cs
namespace LLMSemanticEvaluator.Interfaces;

/// <summary>
/// Interface for generating embeddings
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Generates embedding vector for text
    /// </summary>
    /// <param name="text">Text to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Embedding vector</returns>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}