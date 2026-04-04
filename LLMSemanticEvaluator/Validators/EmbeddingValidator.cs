using LLMSemanticEvaluator.Configuration;
using LLMSemanticEvaluator.Interfaces;
using LLMSemanticEvaluator.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LLMSemanticEvaluator.Validators;

/// <summary>
/// Validates an LLM response by measuring semantic similarity between the expected
/// and actual output using embedding vectors and cosine similarity.
///
/// How it works:
///   1. Both texts are converted to float[] vectors via IEmbeddingGenerator.
///   2. Cosine similarity is computed between the two vectors by ISimilarityCalculator.
///   3. The run passes if similarity >= EmbeddingThreshold (default 0.85).
///
/// IEmbeddingGenerator&lt;string, Embedding&lt;float&gt;&gt; is the standard Microsoft.Extensions.AI
/// interface. The concrete implementation (OpenAI or Ollama) is resolved by
/// LLMClientFactory and injected here — this class never knows which provider is used.
///
/// Score interpretation:
///   1.00       — identical meaning
///   0.90–0.99  — very similar, minor wording differences
///   0.85–0.89  — similar enough → PASS at default threshold
///   0.70–0.84  — related but not close enough → FAIL
///   below 0.70 — clearly different meaning → FAIL
///
/// Known limitation: single-word expected outputs (e.g. "Paris") produce similarity
/// scores of 0.30–0.55 against correct full-sentence responses, well below the threshold.
/// This is why OR logic with the LLM judge is essential — see TestRunner.
/// </summary>
public class EmbeddingValidator: IValidator
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ISimilarityCalculator                         _calculator;
    private readonly double                                        _threshold;
    private readonly ILogger<EmbeddingValidator>                   _logger;

    public EmbeddingValidator(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ISimilarityCalculator                         calculator,
        IOptions<TestConfiguration>                   options,
        ILogger<EmbeddingValidator>                   logger)
    {
        _embeddingGenerator = embeddingGenerator;
        _calculator         = calculator;
        _threshold          = options.Value.EmbeddingThreshold;
        _logger             = logger;
    }

    /// <summary>
    /// Validates whether the actual LLM output is semantically equivalent to
    /// the expected output using cosine similarity on embedding vectors.
    /// </summary>
    /// <param name="expected">Reference answer from the test case JSON.</param>
    /// <param name="actual">Response returned by the LLM under test.</param>
    public async Task<ValidationResult> ValidateAsync(string expected, string actual, string prompt = "", string criteria = "")
    {
        if (string.IsNullOrWhiteSpace(actual))
            return Fail("LLM returned an empty response.");

        try
        {
            // GenerateAsync returns GeneratedEmbeddings<Embedding<float>>.
            // Each Embedding<float> holds the vector in its Vector property.
            var embExpected = (await _embeddingGenerator.GenerateAsync([expected]))
                                  .First().Vector.ToArray();

            var embActual   = (await _embeddingGenerator.GenerateAsync([actual]))
                                  .First().Vector.ToArray();

            if (embExpected.Length == 0 || embActual.Length == 0)
                return Fail("Embedding generator returned an empty vector.");

            double similarity = _calculator.CalculateCosineSimilarity(embExpected, embActual);

            _logger.LogDebug(
                "Embedding similarity: {Score:F4} (threshold {Threshold})",
                similarity, _threshold);

            return new ValidationResult
            {
                ValidatorName = "Embedding",
                Score         = similarity,
                Passed        = similarity >= _threshold
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Embedding validation error: {Error}", ex.Message);
            return Fail($"Error: {ex.Message}");
        }
    }

    private static ValidationResult Fail(string reason) => new()
    {
        ValidatorName = "Embedding",
        Score         = 0,
        Passed        = false,
        Reasoning     = reason
    };
}