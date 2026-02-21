using LLMSemanticEvaluator;

namespace LLMSemanticEvaluatorTests;

/// <summary>
/// Unit tests for CosineSimilarityCalculator
/// Note: In a real project, use a testing framework like xUnit, NUnit, or MSTest
/// This file demonstrates the test cases that should be implemented
/// </summary>
public class CosineSimilarityCalculatorTests
{
    private readonly CosineSimilarityCalculator _calculator;

    public CosineSimilarityCalculatorTests()
    {
        _calculator = new CosineSimilarityCalculator();
    }

    // ============================================================================
    // Basic Functionality Tests
    // ============================================================================

    public void IdenticalVectors_ShouldReturnOne()
    {
        // Arrange
        var vectorA = new float[] { 1.0f, 2.0f, 3.0f };
        var vectorB = new float[] { 1.0f, 2.0f, 3.0f };

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(vectorA, vectorB);

        // Assert
        AssertEquals(1.0, similarity, 0.0001, "Identical vectors should have similarity 1.0");
    }

    public void ScaledVectors_ShouldReturnOne()
    {
        // Arrange
        var vectorA = new float[] { 1.0f, 2.0f, 3.0f };
        var vectorB = new float[] { 2.0f, 4.0f, 6.0f }; // vectorA × 2

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(vectorA, vectorB);

        // Assert
        AssertEquals(1.0, similarity, 0.0001, "Scaled vectors should have similarity 1.0");
    }

    public void OrthogonalVectors_ShouldReturnZero()
    {
        // Arrange
        var vectorA = new float[] { 1.0f, 0.0f };
        var vectorB = new float[] { 0.0f, 1.0f };

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(vectorA, vectorB);

        // Assert
        AssertEquals(0.0, similarity, 0.0001, "Orthogonal vectors should have similarity 0.0");
    }

    public void OppositeVectors_ShouldReturnNegativeOne()
    {
        // Arrange
        var vectorA = new float[] { 1.0f, 2.0f, 3.0f };
        var vectorB = new float[] { -1.0f, -2.0f, -3.0f };

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(vectorA, vectorB);

        // Assert
        AssertEquals(-1.0, similarity, 0.0001, "Opposite vectors should have similarity -1.0");
    }

    // ============================================================================
    // Realistic Scenarios
    // ============================================================================

    public void VerySimilarVectors_ShouldReturnHighSimilarity()
    {
        // Arrange - Simulates "Paris" vs "The capital is Paris"
        var vectorA = new float[] { 0.5f, 0.3f, 0.2f, 0.8f, 0.1f };
        var vectorB = new float[] { 0.52f, 0.29f, 0.21f, 0.79f, 0.11f };

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(vectorA, vectorB);

        // Assert
        AssertGreaterThan(similarity, 0.99, "Very similar vectors should have > 0.99 similarity");
    }

    public void ModeratelySimilarVectors_ShouldReturnModerateSimilarity()
    {
        // Arrange
        var vectorA = new float[] { 1.0f, 2.0f, 3.0f, 0.0f };
        var vectorB = new float[] { 1.0f, 2.0f, 0.0f, 3.0f };

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(vectorA, vectorB);

        // Assert
        AssertInRange(similarity, 0.7, 0.8, "Moderately similar vectors should be in 0.7-0.8 range");
    }

    // ============================================================================
    // Large Dimension Tests
    // ============================================================================

    public void LargeDimensionVectors_ShouldCalculateCorrectly()
    {
        // Arrange - Simulates OpenAI ada-002 (1536 dimensions)
        const int dimensions = 1536;
        var random = new Random(42);
        
        var vectorA = new float[dimensions];
        var vectorB = new float[dimensions];
        
        for (int i = 0; i < dimensions; i++)
        {
            vectorA[i] = (float)(random.NextDouble() * 2 - 1);
            vectorB[i] = vectorA[i] + (float)(random.NextDouble() * 0.1 - 0.05);
        }

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(vectorA, vectorB);

        // Assert
        AssertInRange(similarity, 0.9, 1.0, "Large similar vectors should have high similarity");
    }

    // ============================================================================
    // Edge Cases
    // ============================================================================

    public void ZeroVectors_ShouldReturnZero()
    {
        // Arrange
        var vectorA = new float[] { 0.0f, 0.0f, 0.0f };
        var vectorB = new float[] { 0.0f, 0.0f, 0.0f };

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(vectorA, vectorB);

        // Assert
        AssertEquals(0.0, similarity, 0.0001, "Zero vectors should return 0.0");
    }

    public void OneZeroVector_ShouldReturnZero()
    {
        // Arrange
        var vectorA = new float[] { 1.0f, 2.0f, 3.0f };
        var vectorB = new float[] { 0.0f, 0.0f, 0.0f };

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(vectorA, vectorB);

        // Assert
        AssertEquals(0.0, similarity, 0.0001, "One zero vector should return 0.0");
    }

    public void SingleDimensionVectors_ShouldWork()
    {
        // Arrange
        var vectorA = new float[] { 5.0f };
        var vectorB = new float[] { 3.0f };

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(vectorA, vectorB);

        // Assert
        AssertEquals(1.0, similarity, 0.0001, "Single dimension same-sign vectors should be 1.0");
    }

    public void VerySmallValues_ShouldHandlePrecision()
    {
        // Arrange
        var vectorA = new float[] { 0.0001f, 0.0002f, 0.0003f };
        var vectorB = new float[] { 0.0001f, 0.0002f, 0.0003f };

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(vectorA, vectorB);

        // Assert
        AssertEquals(1.0, similarity, 0.0001, "Small values should maintain precision");
    }

    public void VeryLargeValues_ShouldNotOverflow()
    {
        // Arrange
        var vectorA = new float[] { 1000000f, 2000000f, 3000000f };
        var vectorB = new float[] { 1000000f, 2000000f, 3000000f };

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(vectorA, vectorB);

        // Assert
        AssertEquals(1.0, similarity, 0.0001, "Large values should not cause overflow");
    }

    // ============================================================================
    // Error Handling Tests
    // ============================================================================

    public void NullVectorA_ShouldThrowArgumentNullException()
    {
        // Arrange
        float[]? vectorA = null;
        var vectorB = new float[] { 1.0f, 2.0f };

        // Act & Assert
        AssertThrows<ArgumentNullException>(() => 
            _calculator.CalculateCosineSimilarity(vectorA!, vectorB));
    }

    public void NullVectorB_ShouldThrowArgumentNullException()
    {
        // Arrange
        var vectorA = new float[] { 1.0f, 2.0f };
        float[]? vectorB = null;

        // Act & Assert
        AssertThrows<ArgumentNullException>(() => 
            _calculator.CalculateCosineSimilarity(vectorA, vectorB!));
    }

    public void EmptyVectorA_ShouldThrowArgumentException()
    {
        // Arrange
        var vectorA = new float[0];
        var vectorB = new float[] { 1.0f, 2.0f };

        // Act & Assert
        AssertThrows<ArgumentException>(() => 
            _calculator.CalculateCosineSimilarity(vectorA, vectorB));
    }

    public void EmptyVectorB_ShouldThrowArgumentException()
    {
        // Arrange
        var vectorA = new float[] { 1.0f, 2.0f };
        var vectorB = new float[0];

        // Act & Assert
        AssertThrows<ArgumentException>(() => 
            _calculator.CalculateCosineSimilarity(vectorA, vectorB));
    }

    public void MismatchedDimensions_ShouldThrowArgumentException()
    {
        // Arrange
        var vectorA = new float[] { 1.0f, 2.0f };
        var vectorB = new float[] { 1.0f, 2.0f, 3.0f };

        // Act & Assert
        AssertThrows<ArgumentException>(() => 
            _calculator.CalculateCosineSimilarity(vectorA, vectorB));
    }

    // ============================================================================
    // Mathematical Properties Tests
    // ============================================================================

    public void Symmetry_ShouldBeCommutative()
    {
        // Arrange
        var vectorA = new float[] { 1.0f, 2.0f, 3.0f };
        var vectorB = new float[] { 4.0f, 5.0f, 6.0f };

        // Act
        var similarityAB = _calculator.CalculateCosineSimilarity(vectorA, vectorB);
        var similarityBA = _calculator.CalculateCosineSimilarity(vectorB, vectorA);

        // Assert
        AssertEquals(similarityAB, similarityBA, 0.0001, "Similarity should be symmetric");
    }

    public void BoundedRange_ShouldBeWithinMinusOneToOne()
    {
        // Arrange - Random vectors
        var random = new Random(42);
        for (int test = 0; test < 100; test++)
        {
            var vectorA = Enumerable.Range(0, 10)
                .Select(_ => (float)(random.NextDouble() * 2 - 1))
                .ToArray();
            var vectorB = Enumerable.Range(0, 10)
                .Select(_ => (float)(random.NextDouble() * 2 - 1))
                .ToArray();

            // Act
            var similarity = _calculator.CalculateCosineSimilarity(vectorA, vectorB);

            // Assert
            AssertInRange(similarity, -1.0, 1.0, "Similarity must be in [-1, 1] range");
        }
    }

    public void SelfSimilarity_ShouldBeOne()
    {
        // Arrange
        var vector = new float[] { 1.5f, 2.7f, 3.2f, 4.1f };

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(vector, vector);

        // Assert
        AssertEquals(1.0, similarity, 0.0001, "Vector should have similarity 1.0 with itself");
    }

    // ============================================================================
    // Threshold Tests
    // ============================================================================

    public void PassesThreshold_WithHighSimilarity_ShouldReturnTrue()
    {
        // Arrange
        double similarity = 0.92;
        double threshold = 0.85;

        // Act
        var passes = CosineSimilarityCalculator.PassesThreshold(similarity, threshold);

        // Assert
        AssertTrue(passes, "0.92 similarity should pass 0.85 threshold");
    }

    public void PassesThreshold_WithLowSimilarity_ShouldReturnFalse()
    {
        // Arrange
        double similarity = 0.75;
        double threshold = 0.85;

        // Act
        var passes = CosineSimilarityCalculator.PassesThreshold(similarity, threshold);

        // Assert
        AssertFalse(passes, "0.75 similarity should not pass 0.85 threshold");
    }

    public void PassesThreshold_WithExactThreshold_ShouldReturnTrue()
    {
        // Arrange
        double similarity = 0.85;
        double threshold = 0.85;

        // Act
        var passes = CosineSimilarityCalculator.PassesThreshold(similarity, threshold);

        // Assert
        AssertTrue(passes, "Exact threshold match should pass");
    }

    public void PassesThreshold_WithInvalidThreshold_ShouldThrow()
    {
        // Arrange
        double similarity = 0.85;
        double threshold = 1.5; // Invalid

        // Act & Assert
        AssertThrows<ArgumentOutOfRangeException>(() => 
            CosineSimilarityCalculator.PassesThreshold(similarity, threshold));
    }

    // ============================================================================
    // Interpretation Tests
    // ============================================================================

    public void InterpretScore_WithVariousScores_ShouldReturnCorrectInterpretation()
    {
        var testCases = new[]
        {
            (score: 1.0, expected: "Identical"),
            (score: 0.96, expected: "Extremely similar"),
            (score: 0.92, expected: "Very similar"),
            (score: 0.87, expected: "Similar"),
            (score: 0.82, expected: "Moderately similar"),
            (score: 0.72, expected: "Somewhat similar"),
            (score: 0.55, expected: "Slightly similar"),
            (score: 0.25, expected: "Different"),
            (score: -0.5, expected: "Opposite")
        };

        foreach (var (score, expected) in testCases)
        {
            var interpretation = CosineSimilarityCalculator.InterpretScore(score);
            AssertEquals(expected, interpretation, 
                $"Score {score} should be interpreted as '{expected}'");
        }
    }

    // ============================================================================
    // Helper Methods (Simple Test Framework)
    // ============================================================================

    private void AssertEquals(double expected, double actual, double tolerance, string message)
    {
        if (System.Math.Abs(expected - actual) > tolerance)
        {
            throw new Exception($"FAIL: {message}. Expected: {expected}, Actual: {actual}");
        }
        Console.WriteLine($"✓ PASS: {message}");
    }

    private void AssertEquals(string expected, string actual, string message)
    {
        if (expected != actual)
        {
            throw new Exception($"FAIL: {message}. Expected: '{expected}', Actual: '{actual}'");
        }
        Console.WriteLine($"✓ PASS: {message}");
    }

    private void AssertGreaterThan(double actual, double threshold, string message)
    {
        if (actual <= threshold)
        {
            throw new Exception($"FAIL: {message}. Expected > {threshold}, Actual: {actual}");
        }
        Console.WriteLine($"✓ PASS: {message}");
    }

    private void AssertInRange(double actual, double min, double max, string message)
    {
        if (actual < min || actual > max)
        {
            throw new Exception($"FAIL: {message}. Expected in [{min}, {max}], Actual: {actual}");
        }
        Console.WriteLine($"✓ PASS: {message}");
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"FAIL: {message}");
        }
        Console.WriteLine($"✓ PASS: {message}");
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            throw new Exception($"FAIL: {message}");
        }
        Console.WriteLine($"✓ PASS: {message}");
    }

    private void AssertThrows<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
            throw new Exception($"FAIL: Expected {typeof(TException).Name} but no exception was thrown");
        }
        catch (TException)
        {
            Console.WriteLine($"✓ PASS: Correctly threw {typeof(TException).Name}");
        }
        catch (Exception ex)
        {
            throw new Exception($"FAIL: Expected {typeof(TException).Name} but got {ex.GetType().Name}");
        }
    }

    // ============================================================================
    // Test Runner
    // ============================================================================

    public static void RunAllTests()
    {
        Console.WriteLine("=".PadRight(70, '='));
        Console.WriteLine("Running CosineSimilarityCalculator Unit Tests");
        Console.WriteLine("=".PadRight(70, '='));
        Console.WriteLine();

        var tests = new CosineSimilarityCalculatorTests();
        int passed = 0;
        int failed = 0;

        var testMethods = typeof(CosineSimilarityCalculatorTests)
            .GetMethods()
            .Where(m => m.IsPublic && m.ReturnType == typeof(void) && 
                        m.Name != "RunAllTests" && 
                        m.DeclaringType == typeof(CosineSimilarityCalculatorTests));

        foreach (var method in testMethods)
        {
            Console.WriteLine($"Running: {method.Name}");
            try
            {
                method.Invoke(tests, null);
                passed++;
            }
            catch (Exception ex)
            {
                var innerEx = ex.InnerException ?? ex;
                Console.WriteLine($"✗ FAILED: {innerEx.Message}");
                failed++;
            }
            Console.WriteLine();
        }

        Console.WriteLine("=".PadRight(70, '='));
        Console.WriteLine($"Test Results: {passed} passed, {failed} failed");
        Console.WriteLine("=".PadRight(70, '='));
    }
}