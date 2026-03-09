using LLMSemanticEvaluator;
using LLMSemanticEvaluator.Interfaces;
using Moq;

namespace LLMSemanticEvaluatorTests;

/// <summary>
/// Unit tests for <see cref="EmbeddingValidator"/>.
///
/// Dependencies (IEmbeddingProvider, ISimilarityCalculator) are mocked with Moq
/// so tests run instantly without any real API calls.
///
/// Run with: dotnet test
/// </summary>
public class EmbeddingValidatorTests
{
    // =========================================================================
    // Shared mocks — reset per test by creating new instances in each method
    // =========================================================================
    private readonly Mock<IEmbeddingProvider>   _embeddingsMock  = new();
    private readonly Mock<ISimilarityCalculator> _calculatorMock  = new();

    // Dummy vectors — content doesn't matter because the calculator is mocked
    private static readonly float[] VecA = { 0.1f, 0.2f, 0.3f };
    private static readonly float[] VecB = { 0.1f, 0.2f, 0.3f };

    /// <summary>
    /// Convenience: builds the validator with the shared mocks and given threshold.
    /// </summary>
    private EmbeddingValidator Build(double threshold = 0.85)
        => new(_embeddingsMock.Object, _calculatorMock.Object, threshold);

    /// <summary>
    /// Sets up the mocks so both embedding calls return vectors
    /// and the calculator returns the given similarity score.
    /// </summary>
    private void SetupSuccess(double similarity)
    {
        _ = _embeddingsMock
            .Setup(static e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(VecA);

        _ = _calculatorMock
            .Setup(static c => c.CalculateCosineSimilarity(It.IsAny<float[]>(), It.IsAny<float[]>()))
            .Returns(similarity);
    }

    // =========================================================================
    // Happy Path
    // =========================================================================

    /// <summary>
    /// When similarity exceeds the threshold the result must be Passed = true
    /// and Score must equal the value returned by the calculator.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_SimilarityAboveThreshold_ReturnsPassed()
    {
        // Arrange
        SetupSuccess(similarity: 0.92);
        var validator = Build(threshold: 0.85);

        // Act
        var result = await validator.ValidateAsync("Paris", "The capital is Paris");

        // Assert
        Assert.True(result.Passed);
        Assert.Equal(0.92, result.Score);
        Assert.Equal("Embedding", result.ValidatorName);
    }

    /// <summary>
    /// When similarity is below the threshold the result must be Passed = false.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_SimilarityBelowThreshold_ReturnsFailed()
    {
        // Arrange
        SetupSuccess(similarity: 0.70);
        var validator = Build(threshold: 0.85);

        // Act
        var result = await validator.ValidateAsync("Paris", "London is a city");

        // Assert
        Assert.False(result.Passed);
        Assert.Equal(0.70, result.Score);
    }

    /// <summary>
    /// Similarity exactly equal to the threshold should pass (boundary is inclusive).
    /// </summary>
    [Fact]
    public async Task ValidateAsync_SimilarityExactlyAtThreshold_ReturnsPassed()
    {
        // Arrange
        SetupSuccess(similarity: 0.85);
        var validator = Build(threshold: 0.85);

        // Act
        var result = await validator.ValidateAsync("Paris", "Paris");

        // Assert
        Assert.True(result.Passed);
    }

    // =========================================================================
    // Empty / Null Actual Response
    // =========================================================================

    /// <summary>
    /// An empty actual response must fail immediately — no API calls should be made.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ValidateAsync_EmptyOrNullActual_ReturnsFailedWithoutCallingApi(string? actual)
    {
        // Arrange — mocks are NOT set up; any call to them would throw
        var validator = Build();

        // Act
        var result = await validator.ValidateAsync("Paris", actual!);

        // Assert
        Assert.False(result.Passed);
        Assert.Equal(0, result.Score);
        Assert.False(string.IsNullOrWhiteSpace(result.Reasoning));

        // Embedding API must NOT have been called
        _embeddingsMock.Verify(
            static e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    // API / Infrastructure Failures
    // =========================================================================

    /// <summary>
    /// If the embedding API returns empty vectors, the result must be a safe fail
    /// (Score = 0, Passed = false) rather than crashing.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_EmptyEmbeddingVectors_ReturnsFailedSafely()
    {
        // Arrange — API returns empty arrays
        _ = _embeddingsMock
            .Setup(static e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<float>());

        var validator = Build();

        // Act
        var result = await validator.ValidateAsync("Paris", "Paris");

        // Assert
        Assert.False(result.Passed);
        Assert.Equal(0, result.Score);
    }

    /// <summary>
    /// If the embedding API throws (e.g. network error, bad API key),
    /// the validator must catch it and return a failed result with the error message.
    /// The test run must NOT crash.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_EmbeddingApiThrows_ReturnsFailedWithErrorReasoning()
    {
        // Arrange
        _ = _embeddingsMock
            .Setup(static e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API unavailable"));

        var validator = Build();

        // Act
        var result = await validator.ValidateAsync("Paris", "Paris");

        // Assert
        Assert.False(result.Passed);
        Assert.Equal(0, result.Score);
        Assert.Contains("API unavailable", result.Reasoning);
    }
}