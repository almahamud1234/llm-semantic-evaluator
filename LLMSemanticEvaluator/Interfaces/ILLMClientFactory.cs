using Microsoft.Extensions.AI;

namespace LLMSemanticEvaluator;

/// <summary>
/// Creates the IChatClient and IEmbeddingGenerator for the configured provider.
/// Separating this into an interface allows the factory to be mocked in unit tests.
/// </summary>
public interface ILLMClientFactory
{
    IChatClient CreateChatClient();
    IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator();
}