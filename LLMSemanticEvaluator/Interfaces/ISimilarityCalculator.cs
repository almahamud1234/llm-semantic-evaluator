// File: Core/Interfaces/ISimilarityCalculator.cs
namespace LLMSemanticEvaluator.Interfaces;

/// <summary>
/// Interface for similarity calculations
/// </summary>
public interface ISimilarityCalculator
{
    /// <summary>
    /// Calculates cosine similarity between two vectors
    /// </summary>
    /// <param name="vectorA">First vector</param>
    /// <param name="vectorB">Second vector</param>
    /// <returns>Similarity score (0.0 to 1.0)</returns>
    double CalculateCosineSimilarity(float[] vectorA, float[] vectorB);
}