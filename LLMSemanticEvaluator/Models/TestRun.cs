namespace LLMSemanticEvaluator.Models;

/// <summary>
/// Represents a single execution of a test
/// </summary>
public class TestRun
{
    /// The actual response from the LLM
    public string Response { get; set; } = string.Empty;

    /// Embedding similarity score (0.0 to 1.0)
    public double EmbeddingScore { get; set; }

    /// LLM judge score (1-10)
    public int JudgeScore { get; set; }

    /// LLM judge reasoning
    public string JudgeReasoning { get; set; } = string.Empty;

    /// Whether embedding validation passed
    public bool EmbeddingPassed { get; set; }

    /// Whether judge validation passed
    public bool JudgePassed { get; set; }

    /// When this run was executed
    public DateTime ExecutedAt { get; set; }

    /// Overall pass status (both validators must pass)
    public bool Passed => EmbeddingPassed || JudgePassed;
}
