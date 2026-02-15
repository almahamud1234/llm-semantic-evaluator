namespace LLMSemanticEvaluator;

/// <summary>
/// Demo program to test the CosineSimilarityCalculator
/// </summary>
public class SimilarityCalculatorDemo
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=".PadRight(70, '='));
        Console.WriteLine("LLM Prompt Testing Framework - Similarity Calculator Demo");
        Console.WriteLine("=".PadRight(70, '='));
        Console.WriteLine();

        var calculator = new CosineSimilarityCalculator();

        // TODO: different test scenario
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
}