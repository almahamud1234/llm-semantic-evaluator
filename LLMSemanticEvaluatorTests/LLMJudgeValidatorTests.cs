using LLMSemanticEvaluator;
using LLMSemanticEvaluator.Interfaces;
using Moq;

namespace LLMSemanticEvaluatorTests;

/// <summary>
/// Unit tests for <see cref="LLMJudgeValidator"/>.
///
/// ILLMClient is mocked with Moq so tests run without real API calls.
/// The most important thing to test here is score parsing — the regex and
/// fallback logic that extracts a 1-10 integer from free-form judge responses.
///
/// Run with: dotnet test
/// </summary>
public class LLMJudgeValidatorTests
{
    private readonly Mock<ILLMClient> _llmClientMock = new();

    /// <summary>
    /// Convenience: builds the validator with the shared mock and given threshold.
    /// </summary>
    private LLMJudgeValidator Build(int threshold = 8)
        => new(_llmClientMock.Object, threshold);

    /// <summary>
    /// Sets up the mock LLM client to return the given judge response string.
    /// </summary>
    private void SetupJudgeResponse(string response)
    {
        _llmClientMock
            .Setup(c => c.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    // =========================================================================
    // Happy Path — Pass / Fail based on score vs threshold
    // =========================================================================

    /// <summary>
    /// Judge returns a score above the threshold → Passed = true.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ScoreAboveThreshold_ReturnsPassed()
    {
        // Arrange
        SetupJudgeResponse("9");
        var validator = Build(threshold: 8);

        // Act
        var result = await validator.ValidateAsync("Q", "Expected", "Actual");

        // Assert
        Assert.True(result.Passed);
        Assert.Equal(9, result.Score);
        Assert.Equal("LLMJudge", result.ValidatorName);
    }

    /// <summary>
    /// Judge returns a score below the threshold → Passed = false.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ScoreBelowThreshold_ReturnsFailed()
    {
        // Arrange
        SetupJudgeResponse("5");
        var validator = Build(threshold: 8);

        // Act
        var result = await validator.ValidateAsync("Q", "Expected", "Actual");

        // Assert
        Assert.False(result.Passed);
        Assert.Equal(5, result.Score);
    }

    /// <summary>
    /// Score exactly at the threshold must pass (boundary is inclusive).
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ScoreExactlyAtThreshold_ReturnsPassed()
    {
        // Arrange
        SetupJudgeResponse("8");
        var validator = Build(threshold: 8);

        // Act
        var result = await validator.ValidateAsync("Q", "Expected", "Actual");

        // Assert
        Assert.True(result.Passed);
        Assert.Equal(8, result.Score);
    }

    // =========================================================================
    // Score Parsing — the core logic unique to this class
    // =========================================================================

    /// <summary>
    /// Judges don't always return a bare number — they often add context.
    /// The validator must correctly extract the score from each common format.
    /// </summary>
    [Theory]
    [InlineData("9",                 9)]  // Ideal: bare number
    [InlineData("Score: 9",          9)]  // With label
    [InlineData("I'd give it 8/10",  8)]  // Natural language
    [InlineData("9.",                9)]  // Trailing punctuation
    [InlineData("  10  ",           10)]  // Surrounding whitespace
    [InlineData("Rating: 7 out of 10", 7)] // Verbose format — first valid match wins
    public async Task ValidateAsync_VariousJudgeResponseFormats_ParsesScoreCorrectly(
        string judgeResponse, int expectedScore)
    {
        // Arrange
        SetupJudgeResponse(judgeResponse);
        var validator = Build(threshold: 1); // Low threshold so we can isolate parsing

        // Act
        var result = await validator.ValidateAsync("Q", "Expected", "Actual");

        // Assert
        Assert.Equal(expectedScore, result.Score);
    }

    /// <summary>
    /// If the judge response contains no parseable 1-10 number, the validator
    /// must return Score = 0, Passed = false, and include a useful Reasoning message.
    /// </summary>
    [Theory]
    [InlineData("Great answer!")]    // No number at all
    [InlineData("11")]               // Out of valid range
    [InlineData("0")]                // Below valid range
    [InlineData("zero")]             // Written-out number
    public async Task ValidateAsync_UnparsableJudgeResponse_ReturnsFailed(string judgeResponse)
    {
        // Arrange
        SetupJudgeResponse(judgeResponse);
        var validator = Build();

        // Act
        var result = await validator.ValidateAsync("Q", "Expected", "Actual");

        // Assert
        Assert.False(result.Passed);
        Assert.Equal(0, result.Score);
        Assert.False(string.IsNullOrWhiteSpace(result.Reasoning));
    }

    // =========================================================================
    // Empty / Null Actual Response
    // =========================================================================

    /// <summary>
    /// An empty actual response must fail immediately — the judge LLM must NOT be called.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ValidateAsync_EmptyOrNullActual_ReturnsFailedWithoutCallingApi(string? actual)
    {
        // Arrange — mock not set up; any call would throw
        var validator = Build();

        // Act
        var result = await validator.ValidateAsync("Q", "Expected", actual!);

        // Assert
        Assert.False(result.Passed);
        Assert.Equal(0, result.Score);

        // Judge API must NOT have been called
        _llmClientMock.Verify(
            c => c.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    // API / Infrastructure Failures
    // =========================================================================

    /// <summary>
    /// If the judge LLM itself returns an empty string, treat as a safe fail
    /// with a clear Reasoning message.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_JudgeReturnsEmptyString_ReturnsFailed()
    {
        // Arrange
        SetupJudgeResponse(string.Empty);
        var validator = Build();

        // Act
        var result = await validator.ValidateAsync("Q", "Expected", "Actual");

        // Assert
        Assert.False(result.Passed);
        Assert.Equal(0, result.Score);
        Assert.False(string.IsNullOrWhiteSpace(result.Reasoning));
    }

    /// <summary>
    /// If the LLM client throws (network error, rate limit, etc.),
    /// the validator must catch it and return a failed result — not crash the test run.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_LlmClientThrows_ReturnsFailedWithErrorReasoning()
    {
        // Arrange
        _llmClientMock
            .Setup(c => c.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Rate limit exceeded"));

        var validator = Build();

        // Act
        var result = await validator.ValidateAsync("Q", "Expected", "Actual");

        // Assert
        Assert.False(result.Passed);
        Assert.Equal(0, result.Score);
        Assert.Contains("Rate limit exceeded", result.Reasoning);
    }
}