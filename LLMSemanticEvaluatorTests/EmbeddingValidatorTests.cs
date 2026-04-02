using LLMSemanticEvaluator;
using LLMSemanticEvaluator.Configuration;
using LLMSemanticEvaluator.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace LLMSemanticEvaluatorTests;

/// <summary>
/// Unit tests for <see cref="EmbeddingValidator"/>.
///
/// <para>
/// <see cref="EmbeddingValidator"/> decides whether an LLM response is semantically
/// close enough to the expected answer by converting both strings to embedding vectors
/// and computing cosine similarity. A wrong verdict here directly misclassifies LLM
/// quality — false passes let bad answers through; false failures reject correct ones.
/// </para>
///
/// <para>
/// Dependencies (<see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> and
/// <see cref="ISimilarityCalculator"/>) are mocked with Moq so tests are instant
/// and deterministic — no real API key or network call is needed.
/// </para>
///
/// <para>Run with: <c>dotnet test</c></para>
/// </summary>
[TestClass]
public class EmbeddingValidatorTests
{
    /// <summary>
    /// Mock for the Microsoft.Extensions.AI embedding generator.
    /// Controls what embedding vector is returned without a real API call.
    /// </summary>
    private readonly Mock<IEmbeddingGenerator<string, Embedding<float>>> _embeddingsMock = new();

    /// <summary>
    /// Mock for the cosine similarity calculator.
    /// Controls the similarity score independently of the maths implementation.
    /// </summary>
    private readonly Mock<ISimilarityCalculator> _calculatorMock = new();

    /// <summary>
    /// Dummy embedding vector — content is irrelevant because the calculator is mocked.
    /// Using a non-trivial value avoids accidental "empty vector" short-circuit paths.
    /// </summary>
    private static readonly float[] DummyVector = { 0.1f, 0.2f, 0.3f };

    /// <summary>
    /// Builds the validator under test wired to the shared mocks and the given threshold.
    /// Using a factory method keeps the Arrange sections of each test minimal.
    /// </summary>
    private EmbeddingValidator Build(double threshold = 0.85)
    {
        var options = Options.Create(new TestConfiguration { EmbeddingThreshold = threshold });
        return new EmbeddingValidator(
            _embeddingsMock.Object,
            _calculatorMock.Object,
            options,
            NullLogger<EmbeddingValidator>.Instance);
    }

    /// <summary>
    /// Configures both mocks so any text input returns <see cref="DummyVector"/>
    /// and the similarity calculator returns <paramref name="similarity"/>.
    /// This represents the "happy path" where the embedding API works correctly.
    /// </summary>
    private void SetupSuccess(double similarity)
    {
        var embedding = new Embedding<float>(DummyVector);
        var generated = new GeneratedEmbeddings<Embedding<float>>([embedding]);

        _embeddingsMock
            .Setup(e => e.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(generated);

        _calculatorMock
            .Setup(c => c.CalculateCosineSimilarity(
                It.IsAny<float[]>(), It.IsAny<float[]>()))
            .Returns(similarity);
    }

    // =========================================================================
    // Happy Path
    // =========================================================================

    /// <summary>
    /// When similarity exceeds the threshold, the validator must report Passed = true
    /// with the exact score returned by the calculator.
    /// This is the normal "correct answer" path — the most important case.
    /// </summary>
    [TestMethod]
    public async Task ValidateAsync_SimilarityAboveThreshold_ReturnsPassed()
    {
        // Arrange — similarity 0.92 exceeds threshold 0.85
        SetupSuccess(similarity: 0.92);
        var validator = Build(threshold: 0.85);

        // Act
        var result = await validator.ValidateAsync("Paris", "The capital is Paris");

        // The validator must surface the passing verdict and exact score to the TestRunner,
        // which uses both to build the final TestResult report.
        Assert.IsTrue(result.Passed,
            "Similarity above threshold must produce a passing result.");
        Assert.AreEqual(0.92, result.Score, delta: 0.0001,
            "Score in the result must equal the value returned by the similarity calculator.");
        Assert.AreEqual("Embedding", result.ValidatorName,
            "ValidatorName identifies which validator produced this result in the report.");
    }

    /// <summary>
    /// When similarity falls below the threshold, the response is semantically too
    /// distant from the expected answer. The validator must report Passed = false.
    /// </summary>
    [TestMethod]
    public async Task ValidateAsync_SimilarityBelowThreshold_ReturnsFailed()
    {
        // Arrange — similarity 0.70 is below threshold 0.85
        SetupSuccess(similarity: 0.70);
        var validator = Build(threshold: 0.85);

        // Act
        var result = await validator.ValidateAsync("Paris", "London is a city");

        // A score below the threshold means the LLM answer is not semantically equivalent.
        Assert.IsFalse(result.Passed,
            "Similarity below threshold must produce a failing result.");
        Assert.AreEqual(0.70, result.Score, delta: 0.0001,
            "The exact score must still be stored so the report can show how close it was.");
    }

    /// <summary>
    /// The threshold is an inclusive lower bound. A score exactly equal to it must pass.
    /// This is a boundary condition: off-by-one here causes flapping results when an LLM
    /// consistently scores right at the threshold.
    /// </summary>
    [TestMethod]
    public async Task ValidateAsync_SimilarityExactlyAtThreshold_ReturnsPassed()
    {
        // Arrange — similarity equals the threshold exactly
        SetupSuccess(similarity: 0.85);
        var validator = Build(threshold: 0.85);

        // Act
        var result = await validator.ValidateAsync("Paris", "Paris");

        // Inclusive boundary: score == threshold must pass, not fail.
        Assert.IsTrue(result.Passed,
            "Score exactly equal to the threshold must pass (inclusive lower bound).");
    }

    // =========================================================================
    // Empty / Null Actual Response
    // =========================================================================

    /// <summary>
    /// An empty, whitespace-only, or null actual response means the LLM produced no
    /// usable output. The validator must fail immediately without calling the embedding
    /// API — calling it with an empty string would waste tokens and return a misleading
    /// similarity score.
    /// </summary>
    /// <param name="actual">Various representations of "no response".</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    public async Task ValidateAsync_EmptyOrNullActual_ReturnsFailedWithoutCallingApi(string? actual)
    {
        // Arrange — mocks are NOT set up; any call to them would be caught by Moq
        var validator = Build();

        // Act
        var result = await validator.ValidateAsync("Paris", actual!);

        // The validator must short-circuit: report the failure and leave the API untouched.
        Assert.IsFalse(result.Passed,
            "An empty LLM response must always fail — there is nothing to evaluate.");
        Assert.AreEqual(0, result.Score, delta: 0.0001,
            "Score must be 0 when there is no response to compare.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Reasoning),
            "Reasoning must explain why the result failed so operators can diagnose it.");

        // Verify no API call was made — calling the embedding API with empty input
        // wastes quota and could return misleading near-zero similarity scores.
        _embeddingsMock.Verify(
            e => e.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "Embedding API must not be called when the LLM returned no response.");
    }

    // =========================================================================
    // API / Infrastructure Failures
    // =========================================================================

    /// <summary>
    /// If the embedding API returns empty float arrays, cosine similarity cannot be
    /// computed. The validator must return a safe failure rather than propagating
    /// an <see cref="ArgumentException"/> that would crash the entire test run.
    /// </summary>
    [TestMethod]
    public async Task ValidateAsync_EmptyEmbeddingVectors_ReturnsFailedSafely()
    {
        // Arrange — API returns a zero-length embedding (degenerate provider response)
        var emptyEmbedding = new Embedding<float>(Array.Empty<float>());
        var generated      = new GeneratedEmbeddings<Embedding<float>>([emptyEmbedding]);

        _embeddingsMock
            .Setup(e => e.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(generated);

        var validator = Build();

        // Act
        var result = await validator.ValidateAsync("Paris", "Paris");

        // A degenerate provider response must produce a safe fail — never a crash.
        Assert.IsFalse(result.Passed,
            "Empty embedding vectors mean similarity cannot be computed — must fail safely.");
        Assert.AreEqual(0, result.Score, delta: 0.0001,
            "Score must be 0 when the embedding provider returned no usable data.");
    }

    /// <summary>
    /// If the embedding API throws (network outage, bad API key, rate limit), the
    /// validator must catch the exception and return a failed result — never re-throw.
    /// Re-throwing would kill the entire test run; one API failure must not abort
    /// all remaining test cases.
    /// </summary>
    [TestMethod]
    public async Task ValidateAsync_EmbeddingApiThrows_ReturnsFailedWithErrorReasoning()
    {
        // Arrange — simulate a transient network failure
        _embeddingsMock
            .Setup(e => e.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API unavailable"));

        var validator = Build();

        // Act — must NOT throw
        var result = await validator.ValidateAsync("Paris", "Paris");

        // The exception must be caught and surfaced as a failed result, not re-thrown.
        Assert.IsFalse(result.Passed,
            "An API exception must produce a failing result, not crash the evaluation.");
        Assert.AreEqual(0, result.Score, delta: 0.0001,
            "Score must be 0 when the API could not be reached.");
        Assert.IsTrue(result.Reasoning!.Contains("API unavailable"),
            "The error message must be captured in Reasoning so operators can diagnose the failure.");
    }
}