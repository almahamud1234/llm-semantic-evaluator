using System.Text.Json;
using LLMSemanticEvaluator;
using LLMSemanticEvaluator.Models;

namespace LLMSemanticEvaluatorTests;

/// <summary>
/// Unit tests for <see cref="ReportGenerator"/>.
///
/// ReportGenerator writes real files, so each test uses a dedicated temp folder
/// (created in the system temp directory) that is deleted in a finally block.
///
/// What we test:
///   - All three files are created (txt, json, csv)
///   - Key content appears in each file format
///   - CSV escaping handles commas and quotes in field values
///   - Empty results list → no files written, no crash
///
/// Run with: dotnet test
/// </summary>
public class ReportGeneratorTests
{
    // ── Shared test data ──────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal TestResult with one run, controllable pass/fail.
    /// </summary>
    private static TestResult MakeResult(
        string id       = "t1",
        string category = "factual",
        bool   passed   = true,
        double embScore = 0.92,
        int    judScore = 9)
    {
        var run = new TestRun
        {
            Response        = "Paris",
            EmbeddingScore  = embScore,
            JudgeScore      = judScore,
            EmbeddingPassed = passed,
            JudgePassed     = passed,
            ExecutedAt      = DateTime.UtcNow
        };

        return new TestResult
        {
            TestId               = id,
            Category             = category,
            Prompt               = "What is the capital of France?",
            ExpectedOutput       = "Paris",
            Passed               = passed,
            PassedRunsCount      = passed ? 1 : 0,
            TotalRunsCount       = 1,
            AverageEmbeddingScore = embScore,
            AverageJudgeScore    = judScore,
            Runs                 = new List<TestRun> { run }
        };
    }

    /// <summary>
    /// Creates a unique temp folder for one test and returns its path.
    /// Caller is responsible for deleting it.
    /// </summary>
    private static string CreateTempFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    // =========================================================================
    // File creation
    // =========================================================================

    /// <summary>
    /// After GenerateAsync, all three report files must exist in the output folder.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_WithResults_CreatesAllThreeFiles()
    {
        // Arrange
        string folder = CreateTempFolder();
        try
        {
            var generator = new ReportGenerator(folder);
            var results   = new List<TestResult> { MakeResult() };

            // Act
            await generator.GenerateAsync(results);

            // Assert
            Assert.True(File.Exists(Path.Combine(folder, "report.txt")));
            Assert.True(File.Exists(Path.Combine(folder, "report.json")));
            Assert.True(File.Exists(Path.Combine(folder, "report.csv")));
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    /// <summary>
    /// An empty results list must not create any files and must not throw.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_EmptyResults_CreatesNoFiles()
    {
        // Arrange
        string folder = CreateTempFolder();
        try
        {
            var generator = new ReportGenerator(folder);

            // Act — must not throw
            await generator.GenerateAsync(new List<TestResult>());

            // Assert
            Assert.False(File.Exists(Path.Combine(folder, "report.txt")));
            Assert.False(File.Exists(Path.Combine(folder, "report.json")));
            Assert.False(File.Exists(Path.Combine(folder, "report.csv")));
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    // =========================================================================
    // TXT report content
    // =========================================================================

    /// <summary>
    /// The text report must contain all key sections and the test's data.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_TextReport_ContainsKeySections()
    {
        // Arrange
        string folder = CreateTempFolder();
        try
        {
            var generator = new ReportGenerator(folder);
            var results   = new List<TestResult> { MakeResult(id: "factual_001", passed: true) };

            // Act
            await generator.GenerateAsync(results);
            string content = await File.ReadAllTextAsync(Path.Combine(folder, "report.txt"));

            // Assert — key sections and data must appear
            Assert.Contains("OVERALL SUMMARY",   content);
            Assert.Contains("CATEGORY BREAKDOWN", content);
            Assert.Contains("PER-TEST DETAILS",   content);
            Assert.Contains("factual_001",         content);
            Assert.Contains("PASS",                content);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    /// <summary>
    /// A failed test must appear as FAIL in the text report.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_TextReport_ShowsFailedTest()
    {
        // Arrange
        string folder = CreateTempFolder();
        try
        {
            var generator = new ReportGenerator(folder);
            var results   = new List<TestResult> { MakeResult(passed: false) };

            // Act
            await generator.GenerateAsync(results);
            string content = await File.ReadAllTextAsync(Path.Combine(folder, "report.txt"));

            // Assert
            Assert.Contains("FAIL", content);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    // =========================================================================
    // JSON report content
    // =========================================================================

    /// <summary>
    /// The JSON report must be valid JSON and contain the expected summary fields.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_JsonReport_IsValidJsonWithSummary()
    {
        // Arrange
        string folder = CreateTempFolder();
        try
        {
            var generator = new ReportGenerator(folder);
            var results   = new List<TestResult>
            {
                MakeResult(id: "t1", passed: true),
                MakeResult(id: "t2", passed: false)
            };

            // Act
            await generator.GenerateAsync(results);
            string json = await File.ReadAllTextAsync(Path.Combine(folder, "report.json"));

            // Assert — must parse without throwing
            using var doc     = JsonDocument.Parse(json);
            var       summary = doc.RootElement.GetProperty("summary");

            Assert.Equal(2, summary.GetProperty("totalTests").GetInt32());
            Assert.Equal(1, summary.GetProperty("passed").GetInt32());
            Assert.Equal(1, summary.GetProperty("failed").GetInt32());
            Assert.Equal(50.0, summary.GetProperty("passRatePercent").GetDouble());
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    /// <summary>
    /// The JSON report must include per-test results with testId and passed fields.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_JsonReport_ContainsTestResults()
    {
        // Arrange
        string folder = CreateTempFolder();
        try
        {
            var generator = new ReportGenerator(folder);
            var results   = new List<TestResult> { MakeResult(id: "factual_001", passed: true) };

            // Act
            await generator.GenerateAsync(results);
            string json = await File.ReadAllTextAsync(Path.Combine(folder, "report.json"));

            // Assert
            using var doc        = JsonDocument.Parse(json);
            var       testResult = doc.RootElement
                                      .GetProperty("testResults")
                                      .EnumerateArray()
                                      .First();

            Assert.Equal("factual_001", testResult.GetProperty("testId").GetString());
            Assert.True(testResult.GetProperty("passed").GetBoolean());
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    // =========================================================================
    // CSV report content
    // =========================================================================

    /// <summary>
    /// The CSV report must contain a header row and one data row per test result.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_CsvReport_ContainsHeaderAndDataRows()
    {
        // Arrange
        string folder = CreateTempFolder();
        try
        {
            var generator = new ReportGenerator(folder);
            var results   = new List<TestResult>
            {
                MakeResult(id: "t1"),
                MakeResult(id: "t2")
            };

            // Act
            await generator.GenerateAsync(results);
            string[] lines = await File.ReadAllLinesAsync(Path.Combine(folder, "report.csv"));

            // Assert — header + 2 data rows (+ possible trailing empty line)
            Assert.Contains("TestId", lines[0]);         // header present
            Assert.True(lines.Length >= 3);              // header + 2 data rows
            Assert.Contains("t1", lines[1]);
            Assert.Contains("t2", lines[2]);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    /// <summary>
    /// Fields containing commas or double-quotes must be wrapped in quotes
    /// and internal quotes must be escaped as "" so the CSV stays valid.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_CsvReport_EscapesCommasAndQuotes()
    {
        // Arrange — prompt contains both a comma and a double-quote
        string folder = CreateTempFolder();
        try
        {
            var result = MakeResult();
            result.Prompt = "What is Paris, the \"City of Light\"?";

            var generator = new ReportGenerator(folder);

            // Act
            await generator.GenerateAsync(new List<TestResult> { result });
            string csv = await File.ReadAllTextAsync(Path.Combine(folder, "report.csv"));

            // Assert — the field must be quoted and internal quotes doubled
            Assert.Contains("\"What is Paris, the \"\"City of Light\"\"?\"", csv);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    // =========================================================================
    // Category breakdown
    // =========================================================================

    /// <summary>
    /// Results from multiple categories must each appear in the text report's
    /// category breakdown section.
    /// </summary>
    [Fact]
    public async Task GenerateAsync_MultipleCategories_AllAppearsInReport()
    {
        // Arrange
        string folder = CreateTempFolder();
        try
        {
            var generator = new ReportGenerator(folder);
            var results   = new List<TestResult>
            {
                MakeResult(id: "t1", category: "factual", passed: true),
                MakeResult(id: "t2", category: "math",    passed: false)
            };

            // Act
            await generator.GenerateAsync(results);
            string content = await File.ReadAllTextAsync(Path.Combine(folder, "report.txt"));

            // Assert — both category names must appear in the breakdown
            Assert.Contains("factual", content);
            Assert.Contains("math",    content);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }
}