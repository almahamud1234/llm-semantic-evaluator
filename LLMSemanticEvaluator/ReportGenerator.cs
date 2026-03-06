using System.Text;
using System.Text.Json;
using LLMSemanticEvaluator.Models;

namespace LLMSemanticEvaluator;

/// <summary>
/// Generates three report formats from a list of TestResults:
///   1. reports/report.txt  — human-readable formatted text
///   2. reports/report.json — full data for programmatic use
///   3. reports/report.csv  — flat data for charts/Excel/visualization
///
/// Report sections:
///   - Overall summary  (total, passed, failed, pass rate, avg scores)
///   - Category breakdown  (pass rate per category)
///   - Per-test details  (each test's scores across all runs)
/// </summary>
public class ReportGenerator
{
    private readonly string _reportFolder;

    /// <param name="reportFolder">Folder where all report files will be saved (default: reports).</param>
    public ReportGenerator(string reportFolder = "reports")
    {
        _reportFolder = reportFolder;
    }

    /// <summary>
    /// Generates all three report formats and prints a summary to console.
    /// </summary>
    public async Task GenerateAsync(List<TestResult> results)
    {
        if (results.Count == 0)
        {
            Console.WriteLine("[ReportGenerator] No results to report.");
            return;
        }

        try
        {
            // Auto-create the reports/ folder if it doesn't exist
            Directory.CreateDirectory(_reportFolder);

            await WriteTextReportAsync(results);
            await WriteJsonReportAsync(results);
            await WriteCsvReportAsync(results);

            PrintConsoleSummary(results);

            Console.WriteLine($"\nReports saved to: {Path.GetFullPath(_reportFolder)}/");
            Console.WriteLine("  report.txt  — human-readable summary");
            Console.WriteLine("  report.json — full data for visualization");
            Console.WriteLine("  report.csv  — flat data for Excel/charts");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReportGenerator Error] Could not write reports: {ex.Message}");
        }
    }

    // ── Text Report ───────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a formatted, human-readable .txt report.
    /// </summary>
    private async Task WriteTextReportAsync(List<TestResult> results)
    {
        string path    = Path.Combine(_reportFolder, "report.txt");
        string content = BuildTextReport(results);
        await File.WriteAllTextAsync(path, content);
    }

    private static string BuildTextReport(List<TestResult> results)
    {
        var sb           = new StringBuilder();
        string separator = new string('=', 60);
        string thin      = new string('-', 60);

        // Header
        sb.AppendLine(separator);
        sb.AppendLine("  LLM SEMANTIC EVALUATOR — TEST REPORT");
        sb.AppendLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(separator);
        sb.AppendLine();

        // Section 1: Overall Summary
        int    total    = results.Count;
        int    passed   = results.Count(r => r.Passed);
        double passRate = (double)passed / total * 100;

        sb.AppendLine("OVERALL SUMMARY");
        sb.AppendLine(thin);
        sb.AppendLine($"Total Tests         : {total}");
        sb.AppendLine($"Passed              : {passed}");
        sb.AppendLine($"Failed              : {total - passed}");
        sb.AppendLine($"Pass Rate           : {passRate:F1}%");
        sb.AppendLine($"Avg Embedding Score : {results.Average(r => r.AverageEmbeddingScore):F2}");
        sb.AppendLine($"Avg Judge Score     : {results.Average(r => r.AverageJudgeScore):F1}/10");
        sb.AppendLine();

        // Section 2: Category Breakdown
        sb.AppendLine("CATEGORY BREAKDOWN");
        sb.AppendLine(thin);

        foreach (var group in results.GroupBy(r => r.Category).OrderBy(g => g.Key))
        {
            int    catTotal  = group.Count();
            int    catPassed = group.Count(r => r.Passed);
            double catRate   = (double)catPassed / catTotal * 100;
            sb.AppendLine($"{group.Key,-20} {catPassed}/{catTotal} passed  ({catRate:F1}%)");
        }
        sb.AppendLine();

        // Section 3: Per-Test Details
        sb.AppendLine("PER-TEST DETAILS");
        sb.AppendLine(thin);

        foreach (var result in results)
        {
            sb.AppendLine($"[{(result.Passed ? "PASS" : "FAIL")}] {result.TestId}  (Category: {result.Category})");
            sb.AppendLine($"  Prompt  : {Truncate(result.Prompt, 80)}");
            sb.AppendLine($"  Expected: {Truncate(result.ExpectedOutput, 80)}");
            sb.AppendLine($"  Runs    : {result.PassedRunsCount}/{result.TotalRunsCount} passed");
            sb.AppendLine($"  Avg Embedding: {result.AverageEmbeddingScore:F2}  Avg Judge: {result.AverageJudgeScore:F1}/10");

            for (int i = 0; i < result.Runs.Count; i++)
            {
                var run = result.Runs[i];
                sb.AppendLine($"    Run {i + 1} [{(run.Passed ? "pass" : "fail")}] " +
                              $"Emb: {run.EmbeddingScore:F2}  Judge: {run.JudgeScore}/10  " +
                              $"→ {Truncate(run.Response, 60)}");
            }
            sb.AppendLine();
        }

        sb.AppendLine(separator);
        sb.AppendLine("END OF REPORT");
        sb.AppendLine(separator);

        return sb.ToString();
    }

    // ── JSON Report ───────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a structured .json file containing full results plus summary stats.
    /// Useful for building charts or feeding into other tools.
    /// </summary>
    private async Task WriteJsonReportAsync(List<TestResult> results)
    {
        string path = Path.Combine(_reportFolder, "report.json");

        int    total    = results.Count;
        int    passed   = results.Count(r => r.Passed);

        // Build a summary + full results object
        var report = new
        {
            generatedAt  = DateTime.Now,
            summary = new
            {
                totalTests          = total,
                passed              = passed,
                failed              = total - passed,
                passRatePercent     = Math.Round((double)passed / total * 100, 1),
                avgEmbeddingScore   = Math.Round(results.Average(r => r.AverageEmbeddingScore), 2),
                avgJudgeScore       = Math.Round(results.Average(r => r.AverageJudgeScore), 1)
            },
            // One entry per category
            categoryBreakdown = results
                .GroupBy(r => r.Category)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    category        = g.Key,
                    total           = g.Count(),
                    passed          = g.Count(r => r.Passed),
                    passRatePercent = Math.Round((double)g.Count(r => r.Passed) / g.Count() * 100, 1)
                }),
            // Full per-test results including individual runs
            testResults = results.Select(r => new
            {
                testId              = r.TestId,
                category            = r.Category,
                prompt              = r.Prompt,
                expectedOutput      = r.ExpectedOutput,
                passed              = r.Passed,
                passedRuns          = r.PassedRunsCount,
                totalRuns           = r.TotalRunsCount,
                avgEmbeddingScore   = Math.Round(r.AverageEmbeddingScore, 2),
                avgJudgeScore       = Math.Round(r.AverageJudgeScore, 1),
                runs = r.Runs.Select((run, i) => new
                {
                    runNumber      = i + 1,
                    passed         = run.Passed,
                    embeddingScore = Math.Round(run.EmbeddingScore, 2),
                    judgeScore     = run.JudgeScore,
                    response       = run.Response,
                    executedAt     = run.ExecutedAt
                })
            })
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(report, options);
        await File.WriteAllTextAsync(path, json);
    }

    // ── CSV Report ────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a flat .csv file — one row per test case.
    /// Easy to open in Excel or use with charting libraries.
    /// Columns: TestId, Category, Passed, PassedRuns, TotalRuns,
    ///          AvgEmbeddingScore, AvgJudgeScore, Prompt, ExpectedOutput
    /// </summary>
    private async Task WriteCsvReportAsync(List<TestResult> results)
    {
        string path = Path.Combine(_reportFolder, "report.csv");
        var    sb   = new StringBuilder();

        // Header row
        sb.AppendLine("TestId,Category,Passed,PassedRuns,TotalRuns,AvgEmbeddingScore,AvgJudgeScore,Prompt,ExpectedOutput");

        // One row per test
        foreach (var r in results)
        {
            sb.AppendLine(string.Join(",",
                CsvEscape(r.TestId),
                CsvEscape(r.Category),
                r.Passed,
                r.PassedRunsCount,
                r.TotalRunsCount,
                r.AverageEmbeddingScore.ToString("F2"),
                r.AverageJudgeScore.ToString("F1"),
                CsvEscape(r.Prompt),
                CsvEscape(r.ExpectedOutput)
            ));
        }

        await File.WriteAllTextAsync(path, sb.ToString());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Prints a short pass/fail summary to the console.
    /// </summary>
    private static void PrintConsoleSummary(List<TestResult> results)
    {
        int    total  = results.Count;
        int    passed = results.Count(r => r.Passed);
        double rate   = (double)passed / total * 100;

        Console.WriteLine("\n=== Report Summary ===");
        Console.WriteLine($"Total  : {total}");
        Console.WriteLine($"Passed : {passed}");
        Console.WriteLine($"Failed : {total - passed}");
        Console.WriteLine($"Rate   : {rate:F1}%");
    }

    /// <summary>
    /// Truncates a string to maxLength and appends "..." if cut.
    /// Prevents long prompts from breaking report formatting.
    /// </summary>
    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "...";
    }

    /// <summary>
    /// Wraps a CSV field in quotes and escapes any internal quotes.
    /// Prevents commas or newlines in prompts from breaking the CSV.
    /// </summary>
    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}