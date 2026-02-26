using LLMSemanticEvaluator.Models;
using LLMSemanticEvaluator.Interfaces;

namespace LLMSemanticEvaluator;

/// <summary>
/// Validates LLM responses by comparing semantic similarity using embeddings.
/// Passes if cosine similarity >= threshold (default 0.85).
/// </summary>
public class EmbeddingValidator
{
    private readonly IEmbeddingProvider _embeddings;
    private readonly ISimilarityCalculator _calculator;
    private readonly double _threshold;

    public EmbeddingValidator(
        IEmbeddingProvider embeddings,
        ISimilarityCalculator calculator,
        double threshold = 0.85)
    {
        _embeddings = embeddings;
        _calculator = calculator;
        _threshold = threshold;
    }

    public async Task<ValidationResult> ValidateAsync(string expected, string actual)
    {
        try
        {
            var embExpected = await _embeddings.GenerateEmbeddingAsync(expected);
            var embActual   = await _embeddings.GenerateEmbeddingAsync(actual);

            double similarity = _calculator.CalculateCosineSimilarity(embExpected, embActual);

            return new ValidationResult
            {
                ValidatorName = "Embedding",
                Score         = similarity,
                Passed        = similarity >= _threshold
            };
        }
        catch (Exception ex)
        {
            return new ValidationResult
            {
                ValidatorName = "Embedding",
                Score         = 0,
                Passed        = false,
                Reasoning     = $"Error: {ex.Message}"
            };
        }
    }
}