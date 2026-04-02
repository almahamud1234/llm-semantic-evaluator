using TestResult = LLMSemanticEvaluator.Models.TestResult;
using System.Text.Json;
using LLMSemanticEvaluator;
using LLMSemanticEvaluator.Configuration;
using LLMSemanticEvaluator.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LLMSemanticEvaluatorTests;

/// <summary>
/// Unit tests for <see cref="ReportGenerator"/>.
///
/// <para>
/// <see cref="ReportGenerator"/> is the final stage of the evaluation pipeline.
/// A bug here does not crash the pipeline — it silently produces wrong numbers,
/// missing categories, or malformed files that mislead decision-makers.
/// These tests verify file creation, content correctness, structural validity (JSON),
/// CSV escaping, and edge-case handling (empty results).
/// </para>
///
/// <para>
/// Each test creates a dedicated temp folder and deletes it in a <c>finally</c>
/// block so failed tests never leave orphaned files on disk.
/// </para>
///
/// <para>Run with: <c>dotnet test</c></para>
/// </summary>
[TestClass]
public class ReportGeneratorTests
{
    // ── Test-data builders ────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal but complete <see cref="TestResult"/> with one run.
    /// Parameters let individual tests control only what they care about.
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
            TestId                = id,
            Category              = category,
            Prompt                = "What is the capital of France?",
            ExpectedOutput        = "Paris",
            Passed                = passed,
            PassedRunsCount       = passed ? 1 : 0,
            TotalRunsCount        = 1,
            AverageEmbeddingScore = embScore,
            AverageJudgeScore     = judScore,
            Runs                  = new List<TestRun> { run }
        };
    }

    /// <summary>
    /// Creates a unique temp directory for one test.
    /// The caller must delete it (handled in each test's <c>finally</c> block).
    /// </summary>
    private static string CreateTempFolder()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Creates a <see cref="ReportGenerator"/> with default configuration and a
    /// NullLogger so tests only need to supply the output folder.
    /// </summary>
    private static ReportGenerator CreateGenerator(string folder)
    {
        var options = Options.Create(new TestConfiguration
        {
            EmbeddingThreshold  = 0.85,
            JudgeThreshold      = 8,
            NumberOfRuns        = 3,
            MinimumPassingRuns  = 2,
        });
        var logger = NullLogger<ReportGenerator>.Instance;
        return new ReportGenerator(options, logger, folder);
    }

    // =========================================================================
    // File creation
    // =========================================================================

    /// <summary>
    /// After <c>GenerateAsync</c> completes, all three primary report files must exist.
    /// If any file is missing, the output that operators rely on is incomplete.
    /// </summary>
    [TestMethod]
    public async Task GenerateAsync_WithResults_CreatesAllThreeFiles()
    {
        string folder = CreateTempFolder();
        try
        {
            var generator = CreateGenerator(folder);
            var results   = new List<TestResult> { MakeResult() };

            await generator.GenerateAsync(results);

            // All three report files must be written — operators depend on each format.
            Assert.IsTrue(File.Exists(Path.Combine(folder, "report.txt")),
                "report.txt must be created — it is the human-readable summary operators read first.");
            Assert.IsTrue(File.Exists(Path.Combine(folder, "report.json")),
                "report.json must be created — it provides structured data for automated systems.");
            Assert.IsTrue(File.Exists(Path.Combine(folder, "report.csv")),
                "report.csv must be created — it enables further analysis in Excel or charting tools.");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    /// <summary>
    /// With an empty results list there is nothing to report. The generator must
    /// exit silently without creating any files and without throwing.
    /// Creating empty files would mislead operators into thinking a run completed.
    /// </summary>
    [TestMethod]
    public async Task GenerateAsync_EmptyResults_CreatesNoFiles()
    {
        string folder = CreateTempFolder();
        try
        {
            var generator = CreateGenerator(folder);

            // Must not throw — an empty run is a valid (if unusual) outcome.
            await generator.GenerateAsync(new List<TestResult>());

            // No files must be written — empty reports are worse than no reports.
            Assert.IsFalse(File.Exists(Path.Combine(folder, "report.txt")),
                "report.txt must not be created when there are no results.");
            Assert.IsFalse(File.Exists(Path.Combine(folder, "report.json")),
                "report.json must not be created when there are no results.");
            Assert.IsFalse(File.Exists(Path.Combine(folder, "report.csv")),
                "report.csv must not be created when there are no results.");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    // =========================================================================
    // TXT report content
    // =========================================================================

    /// <summary>
    /// The text report must contain the three structural sections that convey the
    /// complete picture: overall summary, category breakdown, and per-test details.
    /// Missing any section forces operators to read raw JSON instead.
    /// </summary>
    [TestMethod]
    public async Task GenerateAsync_TextReport_ContainsKeySections()
    {
        string folder = CreateTempFolder();
        try
        {
            var generator = CreateGenerator(folder);
            var results   = new List<TestResult> { MakeResult(id: "factual_001", passed: true) };

            await generator.GenerateAsync(results);
            string content = await File.ReadAllTextAsync(Path.Combine(folder, "report.txt"));

            // Each assertion targets a different required section.
            Assert.IsTrue(content.Contains("OVERALL SUMMARY"),
                "The overall pass-rate summary must appear at the top of the report.");
            Assert.IsTrue(content.Contains("CATEGORY BREAKDOWN"),
                "Category breakdown must appear so operators can identify weak domains.");
            Assert.IsTrue(content.Contains("PER-TEST DETAILS"),
                "Per-test details must appear so operators can identify specific failures.");
            Assert.IsTrue(content.Contains("factual_001"),
                "The test ID must appear in the details section for traceability.");
            Assert.IsTrue(content.Contains("PASS"),
                "The verdict 'PASS' must appear to confirm this test passed.");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    /// <summary>
    /// A failed test must be clearly marked as FAIL in the text report.
    /// If failures are not surfaced, operators might incorrectly conclude the LLM
    /// passed all tests.
    /// </summary>
    [TestMethod]
    public async Task GenerateAsync_TextReport_ShowsFailedTest()
    {
        string folder = CreateTempFolder();
        try
        {
            var generator = CreateGenerator(folder);
            var results   = new List<TestResult> { MakeResult(passed: false) };

            await generator.GenerateAsync(results);
            string content = await File.ReadAllTextAsync(Path.Combine(folder, "report.txt"));

            // The word FAIL must appear so failures are immediately visible.
            Assert.IsTrue(content.Contains("FAIL"),
                "A failed test must be marked 'FAIL' in the report — missing this hides quality problems.");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    // =========================================================================
    // JSON report content
    // =========================================================================

    /// <summary>
    /// The JSON report is consumed by automated systems (CI/CD pipelines, dashboards).
    /// It must be valid JSON — a parse error breaks all downstream automation.
    /// The summary section must contain aggregated counts and pass-rate so consuming
    /// systems can display a headline metric without processing every test.
    /// </summary>
    [TestMethod]
    public async Task GenerateAsync_JsonReport_IsValidJsonWithSummary()
    {
        string folder = CreateTempFolder();
        try
        {
            var generator = CreateGenerator(folder);
            var results   = new List<TestResult>
            {
                MakeResult(id: "t1", passed: true),
                MakeResult(id: "t2", passed: false)
            };

            await generator.GenerateAsync(results);
            string json = await File.ReadAllTextAsync(Path.Combine(folder, "report.json"));

            // Parse must succeed — malformed JSON silently breaks all downstream automation.
            using var doc     = JsonDocument.Parse(json);
            var       summary = doc.RootElement.GetProperty("summary");

            Assert.AreEqual(2, summary.GetProperty("totalTests").GetInt32(),
                "totalTests must equal the number of TestResult objects passed in.");
            Assert.AreEqual(1, summary.GetProperty("passed").GetInt32(),
                "passed count must equal the number of results where Passed = true.");
            Assert.AreEqual(1, summary.GetProperty("failed").GetInt32(),
                "failed count must equal the number of results where Passed = false.");
            Assert.AreEqual(50.0, summary.GetProperty("passRatePercent").GetDouble(), delta: 0.01,
                "passRatePercent must be 50% when 1 of 2 tests passed.");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    /// <summary>
    /// Each test result must appear in the testResults array with its ID and verdict
    /// so consuming systems can look up individual outcomes programmatically.
    /// </summary>
    [TestMethod]
    public async Task GenerateAsync_JsonReport_ContainsTestResults()
    {
        string folder = CreateTempFolder();
        try
        {
            var generator = CreateGenerator(folder);
            var results   = new List<TestResult> { MakeResult(id: "factual_001", passed: true) };

            await generator.GenerateAsync(results);
            string json = await File.ReadAllTextAsync(Path.Combine(folder, "report.json"));

            using var doc        = JsonDocument.Parse(json);
            var       testResult = doc.RootElement
                                      .GetProperty("testResults")
                                      .EnumerateArray()
                                      .First();

            // Both fields are required for any downstream system to correlate result to test case.
            Assert.AreEqual("factual_001", testResult.GetProperty("testId").GetString(),
                "testId must match the original TestResult.TestId for traceability.");
            Assert.IsTrue(testResult.GetProperty("passed").GetBoolean(),
                "passed must be true for a passing result.");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    // =========================================================================
    // CSV report content
    // =========================================================================

    /// <summary>
    /// The CSV file is opened in spreadsheet tools for charting and analysis.
    /// It must have a header row so column names are visible, and one data row per
    /// test result. A missing header or row would cause analysts to work with wrong data.
    /// </summary>
    [TestMethod]
    public async Task GenerateAsync_CsvReport_ContainsHeaderAndDataRows()
    {
        string folder = CreateTempFolder();
        try
        {
            var generator = CreateGenerator(folder);
            var results   = new List<TestResult>
            {
                MakeResult(id: "t1"),
                MakeResult(id: "t2")
            };

            await generator.GenerateAsync(results);
            string[] lines = await File.ReadAllLinesAsync(Path.Combine(folder, "report.csv"));

            // Header row must be present and precede data rows.
            Assert.IsTrue(lines[0].Contains("TestId"),
                "The first CSV line must be a header row containing 'TestId'.");
            Assert.IsTrue(lines.Length >= 3,
                "There must be at least 3 lines: 1 header + 2 data rows.");
            Assert.IsTrue(lines[1].Contains("t1"),
                "The second line must be the data row for the first test result.");
            Assert.IsTrue(lines[2].Contains("t2"),
                "The third line must be the data row for the second test result.");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    /// <summary>
    /// CSV fields containing commas or double-quotes must be properly escaped —
    /// the field wrapped in double-quotes and internal quotes doubled ("").
    /// Without this, a comma in a prompt string splits into extra columns,
    /// shifting all subsequent values and silently corrupting the data row.
    /// </summary>
    [TestMethod]
    public async Task GenerateAsync_CsvReport_EscapesCommasAndQuotes()
    {
        string folder = CreateTempFolder();
        try
        {
            // The prompt contains both a comma and a double-quote — the two characters
            // that require RFC 4180 CSV escaping.
            var result = MakeResult();
            result.Prompt = "What is Paris, the \"City of Light\"?";

            var generator = CreateGenerator(folder);
            await generator.GenerateAsync(new List<TestResult> { result });
            string csv = await File.ReadAllTextAsync(Path.Combine(folder, "report.csv"));

            // RFC 4180: the whole field is wrapped in quotes, and internal quotes are doubled.
            Assert.IsTrue(
                csv.Contains("\"What is Paris, the \"\"City of Light\"\"?\""),
                "Fields containing commas or quotes must be RFC 4180 escaped to produce valid CSV.");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    // =========================================================================
    // Category breakdown
    // =========================================================================

    /// <summary>
    /// The category breakdown shows pass rates per domain (e.g. "factual", "math").
    /// Every category present in the results must appear in the report — omitting one
    /// hides a whole domain's performance from the operator.
    /// </summary>
    [TestMethod]
    public async Task GenerateAsync_MultipleCategories_AllAppearInReport()
    {
        string folder = CreateTempFolder();
        try
        {
            var generator = CreateGenerator(folder);
            var results   = new List<TestResult>
            {
                MakeResult(id: "t1", category: "factual", passed: true),
                MakeResult(id: "t2", category: "math",    passed: false)
            };

            await generator.GenerateAsync(results);
            string content = await File.ReadAllTextAsync(Path.Combine(folder, "report.txt"));

            // Both category names must appear in the category breakdown section.
            Assert.IsTrue(content.Contains("factual"),
                "'factual' category must appear in the report's category breakdown.");
            Assert.IsTrue(content.Contains("math"),
                "'math' category must appear in the report's category breakdown.");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }
}