// File: Core/Models/TestRun.cs
namespace LLMPromptTesting.Console.Core.Models;

/// <summary>
/// Represents a single execution of a test
/// </summary>
public class TestRun
{
    /// <summary>
    /// The actual response from the LLM
    /// </summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// Embedding similarity score (0.0 to 1.0)
    /// </summary>
    public double EmbeddingScore { get; set; }

    /// <summary>
    /// LLM judge score (1-10)
    /// </summary>
    public int JudgeScore { get; set; }

    /// <summary>
    /// Whether embedding validation passed
    /// </summary>
    public bool EmbeddingPassed { get; set; }

    /// <summary>
    /// Whether judge validation passed
    /// </summary>
    public bool JudgePassed { get; set; }

    /// <summary>
    /// When this run was executed
    /// </summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>
    /// Overall pass status (both validators must pass)
    /// </summary>
    public bool Passed => EmbeddingPassed && JudgePassed;
}
