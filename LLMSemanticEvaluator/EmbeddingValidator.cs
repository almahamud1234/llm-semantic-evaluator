using LLMSemanticEvaluator.Models;
using LLMSemanticEvaluator.Interfaces;

namespace LLMSemanticEvaluator;

/// <summary>
/// Validates LLM responses by measuring semantic similarity between the expected
/// and actual output using vector embeddings and cosine similarity.
///
/// HOW IT WORKS:
///   1. Both the expected and actual texts are converted into numeric vectors
///      (embeddings) via the OpenAI Embeddings API.
///   2. Cosine similarity is calculated between the two vectors, producing a
///      score between 0.0 (completely different) and 1.0 (identical meaning).
///   3. If the score meets or exceeds the threshold, the test PASSES.
/// 
/// SCORE GUIDE:
///   1.0       = Identical meaning
///   0.90–0.99 = Very similar (minor wording differences)
///   0.85–0.89 = Similar enough → PASS at default threshold
///   0.70–0.84 = Related but not close enough → FAIL
///   below 0.70 = Clearly different meaning → FAIL
/// </summary>
public class EmbeddingValidator
{
    private readonly IEmbeddingProvider _embeddings; // Calls OpenAI to generate vectors
    private readonly ISimilarityCalculator _calculator; // Computes cosine similarity
    private readonly double _threshold; // Minimum score required to pass

    /// <summary>
    /// Creates a new EmbeddingValidator.
    /// </summary>
    /// <param name="embeddings">Provider that converts text to float[] vectors (e.g. OpenAIClient).</param>
    /// <param name="calculator">Calculator that computes cosine similarity between two vectors.</param>
    /// <param name="threshold">
    ///     Minimum similarity score (0.0–1.0) required to pass. Defaults to 0.85.
    ///     Lower = more lenient, Higher = stricter. Recommended range: 0.80–0.90.
    /// </param>
    public EmbeddingValidator(
        IEmbeddingProvider embeddings,
        ISimilarityCalculator calculator,
        double threshold = 0.85)
    {
        _embeddings = embeddings;
        _calculator = calculator;
        _threshold = threshold;
    }

    /// <summary>
    /// Validates whether the actual LLM output is semantically close enough to the expected output.
    /// </summary>
    /// <param name="expected">The correct/expected answer (from your test case JSON).</param>
    /// <param name="actual">The actual response returned by the LLM being tested.</param>
    /// <returns>
    ///     A <see cref="ValidationResult"/> containing:
    ///     - Passed: true if similarity >= threshold
    ///     - Score: the raw cosine similarity value (0.0–1.0)
    ///     - Reasoning: only set if an error occurred
    /// </returns>
    public async Task<ValidationResult> ValidateAsync(string expected, string actual)
    {
        // Treat empty/null LLM response as an automatic fail
        if (string.IsNullOrWhiteSpace(actual))
        {
            return new ValidationResult
            {
                ValidatorName = "Embedding",
                Score         = 0,
                Passed        = false,
                Reasoning     = "LLM returned an empty response"
            };
        }

        try
        {
            // Step 1: Convert both texts to embedding vectors via OpenAI API
            var embExpected = await _embeddings.GenerateEmbeddingAsync(expected);
            var embActual   = await _embeddings.GenerateEmbeddingAsync(actual);

            // Check embeddings came back with data
            if (embExpected == null || embExpected.Length == 0 ||
                embActual   == null || embActual.Length   == 0)
            {
                return new ValidationResult
                {
                    ValidatorName = "Embedding",
                    Score         = 0,
                    Passed        = false,
                    Reasoning     = "Embedding API returned empty vectors"
                };
            }

            // Step 2: Calculate how similar the two vectors are (0.0 to 1.0)
            double similarity = _calculator.CalculateCosineSimilarity(embExpected, embActual);

            // Step 3: Return result — pass if similarity meets the threshold
            return new ValidationResult
            {
                ValidatorName = "Embedding",
                Score         = similarity,
                Passed        = similarity >= _threshold
            };
        }
        catch (Exception ex)
        {
            // If the API call fails (e.g. no key, network issue), treat as a failed validation
            // and surface the error message for debugging rather than crashing the test run.
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