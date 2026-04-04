namespace LLMSemanticEvaluator.Models;

/// <summary>
/// Helper class for deserializing JSON with "tests" wrapper
/// </summary>
public class TestCaseCollection
{
    public List<TestCase> Tests { get; set; } = new();
}