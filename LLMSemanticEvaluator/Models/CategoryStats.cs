namespace LLMSemanticEvaluator.Models;

/// <summary>
/// Statistics for a specific test category
/// </summary>
public class CategoryStats
{
    /// <summary>
    /// Category name
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// Total tests in this category
    /// </summary>
    public int TotalTests { get; set; }

    /// <summary>
    /// Tests that passed in this category
    /// </summary>
    public int PassedTests { get; set; }

    /// <summary>
    /// Tests that failed in this category
    /// </summary>
    public int FailedTests { get; set; }

    /// <summary>
    /// Pass percentage for this category
    /// </summary>
    public double PassPercentage => TotalTests > 0 
        ? (PassedTests / (double)TotalTests) * 100 
        : 0;
}