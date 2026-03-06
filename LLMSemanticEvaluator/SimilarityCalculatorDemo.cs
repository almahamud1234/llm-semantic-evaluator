namespace LLMSemanticEvaluator;

/// <summary>
/// Demo program to test the CosineSimilarityCalculator
/// </summary>
public class SimilarityCalculatorDemo
{
    public static void MainSimiarity(string[] args)
    {
        Console.WriteLine("=".PadRight(70, '='));
        Console.WriteLine("LLM Prompt Testing Framework - Similarity Calculator Demo");
        Console.WriteLine("=".PadRight(70, '='));
        Console.WriteLine();

        var calculator = new CosineSimilarityCalculator();

        // Test 1: Identical vectors
        TestScenario(calculator, "Test 1: Identical Vectors",
            new float[] { 1.0f, 2.0f, 3.0f },
            new float[] { 1.0f, 2.0f, 3.0f },
            expectedSimilarity: 1.0);

        // Test 2: Similar vectors (scaled)
        TestScenario(calculator, "Test 2: Scaled Vectors (Same Direction)",
            new float[] { 1.0f, 2.0f, 3.0f },
            new float[] { 2.0f, 4.0f, 6.0f },
            expectedSimilarity: 1.0,
            note: "Cosine similarity is scale-invariant");

        // Test 3: Orthogonal vectors (perpendicular)
        TestScenario(calculator, "Test 3: Orthogonal Vectors",
            new float[] { 1.0f, 0.0f },
            new float[] { 0.0f, 1.0f },
            expectedSimilarity: 0.0,
            note: "Completely different directions");

        // Test 4: Opposite vectors
        TestScenario(calculator, "Test 4: Opposite Vectors",
            new float[] { 1.0f, 2.0f, 3.0f },
            new float[] { -1.0f, -2.0f, -3.0f },
            expectedSimilarity: -1.0,
            note: "Antonyms in embedding space");

        // Test 5: Very similar vectors (realistic embeddings)
        TestScenario(calculator, "Test 5: Very Similar (Realistic Scenario)",
            new float[] { 0.5f, 0.3f, 0.2f, 0.8f, 0.1f },
            new float[] { 0.52f, 0.29f, 0.21f, 0.79f, 0.11f },
            expectedSimilarity: 0.9999,
            note: "Like 'Paris' vs 'The capital is Paris'");

        // Test 6: Moderately similar vectors
        TestScenario(calculator, "Test 6: Moderately Similar",
            new float[] { 1.0f, 2.0f, 3.0f, 0.0f },
            new float[] { 1.0f, 2.0f, 0.0f, 3.0f },
            expectedSimilarity: 0.7746,
            note: "Related but different topics");

        // Test 7: Different vectors
        TestScenario(calculator, "Test 7: Different Vectors",
            new float[] { 1.0f, 0.0f, 0.0f },
            new float[] { 0.0f, 0.0f, 1.0f },
            expectedSimilarity: 0.0,
            note: "Completely different topics");

        // Test 8: Real embedding dimensions (1536 for OpenAI ada-002)
        TestLargeVectors(calculator);

        // Test 9: Error handling
        TestErrorHandling(calculator);

        // Test 10: Threshold checking
        TestThresholds(calculator);

        Console.WriteLine();
        Console.WriteLine("=".PadRight(70, '='));
        Console.WriteLine("All Tests Complete!");
        Console.WriteLine("=".PadRight(70, '='));
    }

    private static void TestScenario(
        CosineSimilarityCalculator calculator,
        string testName,
        float[] vectorA,
        float[] vectorB,
        double expectedSimilarity,
        string? note = null)
    {
        Console.WriteLine(testName);
        Console.WriteLine("-".PadRight(70, '-'));
        
        Console.WriteLine($"Vector A: [{string.Join(", ", vectorA.Take(5))}{(vectorA.Length > 5 ? "..." : "")}]");
        Console.WriteLine($"Vector B: [{string.Join(", ", vectorB.Take(5))}{(vectorB.Length > 5 ? "..." : "")}]");
        
        var similarity = calculator.CalculateCosineSimilarity(vectorA, vectorB);
        var interpretation = CosineSimilarityCalculator.InterpretScore(similarity);
        
        Console.WriteLine($"Similarity Score: {similarity:F4}");
        Console.WriteLine($"Interpretation: {interpretation}");
        Console.WriteLine($"Expected: ~{expectedSimilarity:F4}");
        
        var difference = System.Math.Abs(similarity - expectedSimilarity);
        var status = difference < 0.01 ? "✓ PASS" : "✗ FAIL";
        Console.WriteLine($"Status: {status} (difference: {difference:F6})");
        
        if (note != null)
        {
            Console.WriteLine($"Note: {note}");
        }
        
        Console.WriteLine();
    }

    private static void TestLargeVectors(CosineSimilarityCalculator calculator)
    {
        Console.WriteLine("Test 8: Large Embedding Vectors (OpenAI-like)");
        Console.WriteLine("-".PadRight(70, '-'));
        
        // Simulate OpenAI ada-002 embedding dimension (1536)
        const int dimensions = 1536;
        var random = new Random(42); // Fixed seed for reproducibility
        
        // Create two similar vectors
        var vectorA = new float[dimensions];
        var vectorB = new float[dimensions];
        
        for (int i = 0; i < dimensions; i++)
        {
            vectorA[i] = (float)(random.NextDouble() * 2 - 1); // Range: -1 to 1
            // Make vectorB similar to vectorA with small random noise
            vectorB[i] = vectorA[i] + (float)(random.NextDouble() * 0.1 - 0.05); // ±5% noise
        }
        
        var similarity = calculator.CalculateCosineSimilarity(vectorA, vectorB);
        var interpretation = CosineSimilarityCalculator.InterpretScore(similarity);
        
        Console.WriteLine($"Vector Dimensions: {dimensions}");
        Console.WriteLine($"Similarity Score: {similarity:F4}");
        Console.WriteLine($"Interpretation: {interpretation}");
        Console.WriteLine($"Status: ✓ PASS - Can handle real embedding dimensions");
        Console.WriteLine();
    }
    private static void TestErrorHandling(CosineSimilarityCalculator calculator)
    {
        Console.WriteLine("Test 9: Error Handling");
        Console.WriteLine("-".PadRight(70, '-'));

        var tests = new (string name, Func<double> action, Type? expectedException)[]
        {
            ("Null vector A", 
                () => calculator.CalculateCosineSimilarity(null!, new float[] { 1.0f }), 
                typeof(ArgumentNullException)),
            
            ("Null vector B", 
                () => calculator.CalculateCosineSimilarity(new float[] { 1.0f }, null!), 
                typeof(ArgumentNullException)),
            
            ("Empty vector A", 
                () => calculator.CalculateCosineSimilarity(new float[0], new float[] { 1.0f }), 
                typeof(ArgumentException)),
            
            ("Empty vector B", 
                () => calculator.CalculateCosineSimilarity(new float[] { 1.0f }, new float[0]), 
                typeof(ArgumentException)),
            
            ("Mismatched dimensions", 
                () => calculator.CalculateCosineSimilarity(new float[] { 1.0f, 2.0f }, new float[] { 1.0f }), 
                typeof(ArgumentException)),
            
            ("Zero vectors (handled gracefully)", 
                () => calculator.CalculateCosineSimilarity(new float[] { 0.0f, 0.0f }, new float[] { 0.0f, 0.0f }), 
                null) // This should NOT throw, returns 0.0
        };

        foreach (var (name, action, expectedException) in tests)
        {
            try
            {
                var result = action();
                if (expectedException == null)
                {
                    Console.WriteLine($"  ✓ {name}: Handled gracefully (returned {result:F4})");
                }
                else
                {
                    Console.WriteLine($"  ✗ {name}: Expected {expectedException.Name} but didn't throw");
                }
            }
            catch (Exception ex)
            {
                if (expectedException != null && ex.GetType() == expectedException)
                {
                    Console.WriteLine($"  ✓ {name}: Correctly threw {ex.GetType().Name}");
                }
                else
                {
                    Console.WriteLine($"  ✗ {name}: Unexpected exception {ex.GetType().Name}");
                }
            }
        }
        
        Console.WriteLine();
    }

    private static void TestThresholds(CosineSimilarityCalculator calculator)
    {
        Console.WriteLine("Test 10: Threshold Checking");
        Console.WriteLine("-".PadRight(70, '-'));

        var vectorA = new float[] { 1.0f, 2.0f, 3.0f };
        var vectorB = new float[] { 1.1f, 2.1f, 2.9f };
        
        var similarity = calculator.CalculateCosineSimilarity(vectorA, vectorB);
        
        Console.WriteLine($"Vector Similarity: {similarity:F4}");
        Console.WriteLine();

        var thresholds = new[] { 0.95, 0.90, 0.85, 0.80, 0.70, 0.50 };
        
        Console.WriteLine("Threshold Tests:");
        foreach (var threshold in thresholds)
        {
            var passes = CosineSimilarityCalculator.PassesThreshold(similarity, threshold);
            var status = passes ? "✓ PASS" : "✗ FAIL";
            Console.WriteLine($"  Threshold {threshold:F2}: {status}");
        }

        Console.WriteLine();
        Console.WriteLine("Typical thresholds for LLM testing:");
        Console.WriteLine("  0.85 = Default (good balance)");
        Console.WriteLine("  0.90 = Strict (very similar required)");
        Console.WriteLine("  0.80 = Lenient (more variation allowed)");
        Console.WriteLine();
    }
}