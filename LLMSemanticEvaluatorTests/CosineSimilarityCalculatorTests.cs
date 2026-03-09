using LLMSemanticEvaluator;

namespace LLMSemanticEvaluatorTests;

/// <summary>
/// Essential unit tests for <see cref="CosineSimilarityCalculator"/>.
/// Uses xUnit — run with: dotnet test
/// </summary>
public class CosineSimilarityCalculatorTests
{
    private readonly CosineSimilarityCalculator _calculator = new();

    // =========================================================================
    // Core Math
    // =========================================================================

    [Fact] // Same direction → must be 1.0
    public void IdenticalVectors_ShouldReturnOne()
    {
        var result = _calculator.CalculateCosineSimilarity(
            new float[] { 1f, 2f, 3f },
            new float[] { 1f, 2f, 3f });
        Assert.Equal(1.0, result, precision: 4);
    }

    [Fact] // Scaling doesn't change direction → still 1.0
    public void ScaledVectors_ShouldReturnOne()
    {
        var result = _calculator.CalculateCosineSimilarity(
            new float[] { 1f, 2f, 3f },
            new float[] { 2f, 4f, 6f });
        Assert.Equal(1.0, result, precision: 4);
    }

    [Fact] // Perpendicular vectors → 0.0
    public void OrthogonalVectors_ShouldReturnZero()
    {
        var result = _calculator.CalculateCosineSimilarity(
            new float[] { 1f, 0f },
            new float[] { 0f, 1f });
        Assert.Equal(0.0, result, precision: 4);
    }

    [Fact] // Opposite direction → -1.0
    public void OppositeVectors_ShouldReturnNegativeOne()
    {
        var result = _calculator.CalculateCosineSimilarity(
            new float[] { 1f, 2f, 3f },
            new float[] { -1f, -2f, -3f });
        Assert.Equal(-1.0, result, precision: 4);
    }

    [Fact] // Result must always stay within [-1, 1] across random inputs
    public void Result_ShouldAlwaysBeWithinValidRange()
    {
        var random = new Random(42);
        for (int i = 0; i < 100; i++)
        {
            var a = Enumerable.Range(0, 10).Select(_ => (float)(random.NextDouble() * 2 - 1)).ToArray();
            var b = Enumerable.Range(0, 10).Select(_ => (float)(random.NextDouble() * 2 - 1)).ToArray();
            Assert.InRange(_calculator.CalculateCosineSimilarity(a, b), -1.0, 1.0);
        }
    }

    [Fact] // sim(A,B) must equal sim(B,A)
    public void Similarity_ShouldBeSymmetric()
    {
        var a = new float[] { 1f, 2f, 3f };
        var b = new float[] { 4f, 5f, 6f };
        Assert.Equal(
            _calculator.CalculateCosineSimilarity(a, b),
            _calculator.CalculateCosineSimilarity(b, a),
            precision: 6);
    }

    // =========================================================================
    // Edge Cases
    // =========================================================================

    [Fact] // Zero magnitude → undefined, returns 0 by convention
    public void ZeroVector_ShouldReturnZero()
    {
        var result = _calculator.CalculateCosineSimilarity(
            new float[] { 1f, 2f, 3f },
            new float[] { 0f, 0f, 0f });
        Assert.Equal(0.0, result, precision: 4);
    }

    // =========================================================================
    // Error Handling
    // =========================================================================

    [Fact]
    public void NullVectorA_ShouldThrowArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
            _calculator.CalculateCosineSimilarity(null!, new float[] { 1f }));

    [Fact]
    public void NullVectorB_ShouldThrowArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
            _calculator.CalculateCosineSimilarity(new float[] { 1f }, null!));

    [Fact]
    public void EmptyVector_ShouldThrowArgumentException() =>
        Assert.Throws<ArgumentException>(() =>
            _calculator.CalculateCosineSimilarity(Array.Empty<float>(), new float[] { 1f }));

    [Fact]
    public void MismatchedDimensions_ShouldThrowArgumentException() =>
        Assert.Throws<ArgumentException>(() =>
            _calculator.CalculateCosineSimilarity(new float[] { 1f, 2f }, new float[] { 1f, 2f, 3f }));

    // =========================================================================
    // PassesThreshold
    // =========================================================================

    [Fact]
    public void PassesThreshold_AboveThreshold_ShouldReturnTrue() =>
        Assert.True(CosineSimilarityCalculator.PassesThreshold(0.92, 0.85));

    [Fact]
    public void PassesThreshold_BelowThreshold_ShouldReturnFalse() =>
        Assert.False(CosineSimilarityCalculator.PassesThreshold(0.75, 0.85));

    [Fact]
    public void PassesThreshold_ExactMatch_ShouldReturnTrue() =>
        Assert.True(CosineSimilarityCalculator.PassesThreshold(0.85, 0.85));

    [Fact]
    public void PassesThreshold_InvalidThreshold_ShouldThrow() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CosineSimilarityCalculator.PassesThreshold(0.85, 1.5));

    // =========================================================================
    // InterpretScore — one [Theory] covers all score bands
    // =========================================================================

    [Theory]
    [InlineData(1.0,  "Identical")]
    [InlineData(0.96, "Extremely similar")]
    [InlineData(0.92, "Very similar")]
    [InlineData(0.87, "Similar")]
    [InlineData(0.82, "Moderately similar")]
    [InlineData(0.72, "Somewhat similar")]
    [InlineData(0.55, "Slightly similar")]
    [InlineData(0.25, "Different")]
    [InlineData(-0.5, "Opposite")]
    public void InterpretScore_ShouldReturnCorrectLabel(double score, string expected) =>
        Assert.Equal(expected, CosineSimilarityCalculator.InterpretScore(score));
}