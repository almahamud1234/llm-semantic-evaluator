// File: Core/Models/TestProgressEventArgs.cs
namespace LLMSemanticEvaluator.Models;

/// <summary>
/// Event args for test progress updates
/// </summary>
public class TestProgressEventArgs : EventArgs
{
    /// <summary>
    /// Test ID that completed
    /// </summary>
    public string TestId { get; set; } = string.Empty;

    /// <summary>
    /// Current test number
    /// </summary>
    public int CurrentTest { get; set; }

    /// <summary>
    /// Total number of tests
    /// </summary>
    public int TotalTests { get; set; }

    /// <summary>
    /// Whether this test passed
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Progress message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}