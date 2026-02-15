using LLMSemanticEvaluator.Interfaces;

namespace LLMSemanticEvaluator;

/// <summary>
/// Calculates cosine similarity between embedding vectors
/// </summary>
/// <remarks>
/// Cosine similarity measures the cosine of the angle between two vectors.
/// Formula: similarity = (A · B) / (||A|| × ||B||)
/// 
/// Result interpretation:
///  1.0 = Identical direction (same meaning)
///  0.9+ = Very similar
///  0.8+ = Similar enough
///  0.7  = Somewhat similar
/// &lt;0.7  = Different
///  0.0  = Orthogonal (completely different)
/// -1.0  = Opposite direction (antonyms)
/// </remarks>
public class CosineSimilarityCalculator : ISimilarityCalculator
{
    /// <summary>
    /// Calculates cosine similarity between two embedding vectors
    /// </summary>
    /// <param name="vectorA">First embedding vector</param>
    /// <param name="vectorB">Second embedding vector</param>
    /// <returns>Similarity score between -1.0 and 1.0 (typically 0.0 to 1.0 for embeddings)</returns>
    /// <exception cref="ArgumentNullException">If either vector is null</exception>
    /// <exception cref="ArgumentException">If vectors are empty or have different dimensions</exception>
    public double CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        // Input validation
        ValidateInputs(vectorA, vectorB);

        // Calculate dot product and magnitudes in a single pass for efficiency
        double dotProduct = 0.0;
        double magnitudeA = 0.0;
        double magnitudeB = 0.0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            double a = vectorA[i];
            double b = vectorB[i];

            dotProduct += a * b;
            magnitudeA += a * a;
            magnitudeB += b * b;
        }

        // Calculate magnitudes (Euclidean norms)
        magnitudeA = System.Math.Sqrt(magnitudeA);
        magnitudeB = System.Math.Sqrt(magnitudeB);

        // Handle zero vectors
        if (magnitudeA == 0.0 || magnitudeB == 0.0)
        {
            // If either vector is all zeros, similarity is undefined (we return 0)
            return 0.0;
        }

        // Calculate cosine similarity
        double similarity = dotProduct / (magnitudeA * magnitudeB);

        // Clamp to [-1, 1] range to handle floating-point precision issues
        // (Theoretically should always be in this range, but floating-point math can cause tiny deviations)
        similarity = System.Math.Max(-1.0, System.Math.Min(1.0, similarity));

        return similarity;
    }

    /// <summary>
    /// Validates input vectors
    /// </summary>
    private void ValidateInputs(float[] vectorA, float[] vectorB)
    {
        if (vectorA == null)
        {
            throw new ArgumentNullException(nameof(vectorA), "First vector cannot be null");
        }

        if (vectorB == null)
        {
            throw new ArgumentNullException(nameof(vectorB), "Second vector cannot be null");
        }

        if (vectorA.Length == 0)
        {
            throw new ArgumentException("First vector cannot be empty", nameof(vectorA));
        }

        if (vectorB.Length == 0)
        {
            throw new ArgumentException("Second vector cannot be empty", nameof(vectorB));
        }

        if (vectorA.Length != vectorB.Length)
        {
            throw new ArgumentException(
                $"Vectors must have the same dimensions. " +
                $"VectorA: {vectorA.Length}, VectorB: {vectorB.Length}");
        }
    }

    /// <summary>
    /// Helper method to interpret similarity scores
    /// </summary>
    public static string InterpretScore(double similarity)
    {
        return similarity switch
        {
            >= 1.0 => "Identical",
            >= 0.95 => "Extremely similar",
            >= 0.90 => "Very similar",
            >= 0.85 => "Similar",
            >= 0.80 => "Moderately similar",
            >= 0.70 => "Somewhat similar",
            >= 0.50 => "Slightly similar",
            >= 0.0 => "Different",
            _ => "Opposite"
        };
    }

    /// <summary>
    /// Helper method to check if similarity passes a threshold
    /// </summary>
    public static bool PassesThreshold(double similarity, double threshold)
    {
        if (threshold < -1.0 || threshold > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(threshold), 
                threshold, 
                "Threshold must be between -1.0 and 1.0");
        }

        return similarity >= threshold;
    }
}