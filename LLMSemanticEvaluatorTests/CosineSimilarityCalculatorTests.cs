using LLMSemanticEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LLMSemanticEvaluatorTests;

/// <summary>
/// Unit tests for <see cref="CosineSimilarityCalculator"/>.
///
/// <para>
/// Cosine similarity is the mathematical backbone of the embedding validator.
/// Getting it wrong would silently accept semantically incorrect LLM responses
/// or reject correct ones — making the entire evaluation framework unreliable.
/// These tests cover the full domain: ideal geometric cases, boundary conditions,
/// and all invalid-input paths.
/// </para>
///
/// <para>Run with: <c>dotnet test</c></para>
/// </summary>
[TestClass]
public class CosineSimilarityCalculatorTests
{
    /// <summary>
    /// The system under test. Stateless, so one instance is shared across all tests.
    /// </summary>
    private readonly CosineSimilarityCalculator _calculator = new();

    // =========================================================================
    // Core Math
    // =========================================================================

    /// <summary>
    /// Two identical vectors point in exactly the same direction.
    /// Cosine of 0° = 1.0, so the result must be exactly 1.0.
    /// If this fails, the dot-product or magnitude calculation is fundamentally broken.
    /// </summary>
    [TestMethod]
    public void IdenticalVectors_ShouldReturnOne()
    {
        double result = _calculator.CalculateCosineSimilarity(
            new float[] { 1f, 2f, 3f },
            new float[] { 1f, 2f, 3f });

        // Similarity of a vector with itself must be 1.0 — the maximum possible value.
        Assert.AreEqual(1.0, result, delta: 0.0001,
            "Identical vectors must yield cosine similarity of 1.0.");
    }

    /// <summary>
    /// Scaling a vector changes its magnitude but not its direction.
    /// Cosine similarity measures direction only, so the result must remain 1.0.
    /// This is critical because LLM embeddings may be L2-normalised differently
    /// across providers.
    /// </summary>
    [TestMethod]
    public void ScaledVectors_ShouldReturnOne()
    {
        double result = _calculator.CalculateCosineSimilarity(
            new float[] { 1f, 2f, 3f },
            new float[] { 2f, 4f, 6f });

        // Direction is unchanged by scalar multiplication — must still be 1.0.
        Assert.AreEqual(1.0, result, delta: 0.0001,
            "A scaled copy of a vector must have the same direction, hence similarity 1.0.");
    }

    /// <summary>
    /// Perpendicular vectors share no directional component.
    /// Cosine of 90° = 0.0.
    /// </summary>
    [TestMethod]
    public void OrthogonalVectors_ShouldReturnZero()
    {
        double result = _calculator.CalculateCosineSimilarity(
            new float[] { 1f, 0f },
            new float[] { 0f, 1f });

        // No shared direction → similarity must be 0.0.
        Assert.AreEqual(0.0, result, delta: 0.0001,
            "Orthogonal vectors have no shared direction component; similarity must be 0.0.");
    }

    /// <summary>
    /// Opposite vectors point in exactly contrary directions.
    /// Cosine of 180° = -1.0. Validates that the sign of the dot product is preserved.
    /// </summary>
    [TestMethod]
    public void OppositeVectors_ShouldReturnNegativeOne()
    {
        double result = _calculator.CalculateCosineSimilarity(
            new float[] { 1f, 2f, 3f },
            new float[] { -1f, -2f, -3f });

        // Fully opposite direction — minimum similarity value.
        Assert.AreEqual(-1.0, result, delta: 0.0001,
            "Vectors in opposite directions must yield cosine similarity of -1.0.");
    }

    /// <summary>
    /// Cosine similarity is mathematically bounded to [-1, 1].
    /// Testing 100 random pairs guards against floating-point overflow or precision
    /// errors that could corrupt pass/fail decisions in the evaluation pipeline.
    /// </summary>
    [TestMethod]
    public void Result_ShouldAlwaysBeWithinValidRange()
    {
        var random = new Random(42); // fixed seed for reproducibility

        for (int i = 0; i < 100; i++)
        {
            var a = Enumerable.Range(0, 10)
                              .Select(_ => (float)(random.NextDouble() * 2 - 1))
                              .ToArray();
            var b = Enumerable.Range(0, 10)
                              .Select(_ => (float)(random.NextDouble() * 2 - 1))
                              .ToArray();

            double result = _calculator.CalculateCosineSimilarity(a, b);

            // Any out-of-range result would silently corrupt threshold comparisons.
            Assert.IsTrue(result >= -1.0 && result <= 1.0,
                $"Result {result} for random pair {i} is outside the valid range [-1, 1].");
        }
    }

    /// <summary>
    /// Cosine similarity is symmetric: sim(A, B) == sim(B, A).
    /// If this breaks, the order of expected/actual embeddings would change the verdict.
    /// </summary>
    [TestMethod]
    public void Similarity_ShouldBeSymmetric()
    {
        var a = new float[] { 1f, 2f, 3f };
        var b = new float[] { 4f, 5f, 6f };

        double ab = _calculator.CalculateCosineSimilarity(a, b);
        double ba = _calculator.CalculateCosineSimilarity(b, a);

        // sim(A,B) and sim(B,A) must be identical to 6 decimal places.
        Assert.AreEqual(ab, ba, delta: 0.000001,
            "Cosine similarity must be symmetric: sim(A,B) == sim(B,A).");
    }

    // =========================================================================
    // Edge Cases
    // =========================================================================

    /// <summary>
    /// A zero vector has no direction — cosine similarity is mathematically undefined.
    /// The implementation must return 0 by convention rather than divide-by-zero.
    /// </summary>
    [TestMethod]
    public void ZeroVector_ShouldReturnZero()
    {
        double result = _calculator.CalculateCosineSimilarity(
            new float[] { 1f, 2f, 3f },
            new float[] { 0f, 0f, 0f });

        // Convention: undefined similarity defaults to 0 (safe fail, not a crash).
        Assert.AreEqual(0.0, result, delta: 0.0001,
            "Zero vector has no direction; by convention similarity should be 0.0.");
    }

    // =========================================================================
    // Error Handling — invalid inputs must throw immediately and clearly
    // =========================================================================

    /// <summary>
    /// A null vector A must throw <see cref="ArgumentNullException"/> immediately.
    /// Failing silently would produce an uninformative stack trace deep inside the maths.
    /// </summary>
    [TestMethod]
    public void NullVectorA_ShouldThrowArgumentNullException()
    {
        // The validator must reject null input with a meaningful exception,
        // not propagate a NullReferenceException from inside the calculation.
        Assert.Throws<ArgumentNullException>(() =>
            _calculator.CalculateCosineSimilarity(null!, new float[] { 1f }));
    }

    /// <summary>
    /// A null vector B must throw <see cref="ArgumentNullException"/> immediately.
    /// The fail-fast contract must be enforced on both parameters.
    /// </summary>
    [TestMethod]
    public void NullVectorB_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _calculator.CalculateCosineSimilarity(new float[] { 1f }, null!));
    }

    /// <summary>
    /// An empty array has no components to calculate a dot product from.
    /// <see cref="ArgumentException"/> must be thrown so the caller knows the
    /// embedding generator returned degenerate output.
    /// </summary>
    [TestMethod]
    public void EmptyVector_ShouldThrowArgumentException()
    {
        // An empty embedding indicates a provider error — surface it clearly.
        Assert.Throws<ArgumentException>(() =>
            _calculator.CalculateCosineSimilarity(Array.Empty<float>(), new float[] { 1f }));
    }

    /// <summary>
    /// Vectors of different dimensions cannot be compared.
    /// Silently truncating or padding would produce a meaningless result.
    /// <see cref="ArgumentException"/> must be thrown so mismatched configurations
    /// are caught immediately.
    /// </summary>
    [TestMethod]
    public void MismatchedDimensions_ShouldThrowArgumentException()
    {
        // Dimension mismatch = incompatible embedding models; must not silently corrupt scores.
        Assert.Throws<ArgumentException>(() =>
            _calculator.CalculateCosineSimilarity(
                new float[] { 1f, 2f },
                new float[] { 1f, 2f, 3f }));
    }

    // =========================================================================
    // PassesThreshold
    // =========================================================================

    /// <summary>
    /// A similarity score above the threshold must return true.
    /// This is the normal "pass" case for a semantically correct LLM response.
    /// </summary>
    [TestMethod]
    public void PassesThreshold_AboveThreshold_ShouldReturnTrue()
    {
        // Score 0.92 exceeds threshold 0.85 → test should pass.
        Assert.IsTrue(CosineSimilarityCalculator.PassesThreshold(0.92, 0.85),
            "Score above threshold must yield a passing verdict.");
    }

    /// <summary>
    /// A similarity score below the threshold must return false.
    /// This is the "fail" case — the LLM response is semantically too distant.
    /// </summary>
    [TestMethod]
    public void PassesThreshold_BelowThreshold_ShouldReturnFalse()
    {
        Assert.IsFalse(CosineSimilarityCalculator.PassesThreshold(0.75, 0.85),
            "Score below threshold must yield a failing verdict.");
    }

    /// <summary>
    /// A score exactly equal to the threshold must pass.
    /// The threshold is an inclusive lower bound — responses meeting it exactly
    /// satisfy the quality bar and must not be penalised.
    /// </summary>
    [TestMethod]
    public void PassesThreshold_ExactMatch_ShouldReturnTrue()
    {
        // Boundary is inclusive: score == threshold → pass.
        Assert.IsTrue(CosineSimilarityCalculator.PassesThreshold(0.85, 0.85),
            "Score exactly equal to threshold must pass (inclusive boundary).");
    }

    /// <summary>
    /// A threshold value outside [0, 1] is not a valid cosine similarity range
    /// and must throw <see cref="ArgumentOutOfRangeException"/>.
    /// Allowing nonsensical thresholds would silently corrupt every verdict.
    /// </summary>
    [TestMethod]
    public void PassesThreshold_InvalidThreshold_ShouldThrow()
    {
        // Threshold of 1.5 is outside the valid cosine range — must be rejected.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CosineSimilarityCalculator.PassesThreshold(0.85, 1.5));
    }

    // =========================================================================
    // InterpretScore — human-readable labels used in generated reports
    // =========================================================================

    /// <summary>
    /// Each score band must map to the correct human-readable label.
    /// These labels appear in reports that operators read to assess LLM quality —
    /// a wrong label would misrepresent evaluation results.
    /// </summary>
    /// <param name="score">Cosine similarity score to interpret.</param>
    /// <param name="expected">Expected descriptive label for that score.</param>
    [TestMethod]
    [DataRow(1.0,  "Identical")]
    [DataRow(0.96, "Extremely similar")]
    [DataRow(0.92, "Very similar")]
    [DataRow(0.87, "Similar")]
    [DataRow(0.82, "Moderately similar")]
    [DataRow(0.72, "Somewhat similar")]
    [DataRow(0.55, "Slightly similar")]
    [DataRow(0.25, "Different")]
    [DataRow(-0.5, "Opposite")]
    public void InterpretScore_ShouldReturnCorrectLabel(double score, string expected)
    {
        string actual = CosineSimilarityCalculator.InterpretScore(score);

        // Report labels must exactly match defined bands — any mismatch produces
        // misleading output that operators cannot trust.
        Assert.AreEqual(expected, actual,
            $"Score {score} must be labelled '{expected}', got '{actual}'.");
    }
}