using System.Text;
using System.Text.Json;
using LLMSemanticEvaluator.Models;

namespace LLMSemanticEvaluator;

/// <summary>
/// Generates four report formats from a list of TestResults:
///   1. reports/report.txt  — human-readable formatted text
///   2. reports/report.json — full structured data
///   3. reports/report.csv  — flat data for Excel/charts
///   4. reports/report.html — interactive visual dashboard
///
/// The HTML dashboard is built from ReportTemplate.html (located next to this
/// file in the project). ReportGenerator reads the template, replaces all
/// %%PLACEHOLDER%% tokens with real values, and writes the final HTML file.
/// This avoids putting HTML inside a C# interpolated string, which causes
/// "{variable}" JS syntax to be misread as C# expressions.
///
/// To add ReportTemplate.html as an embedded resource, add to your .csproj:
///   &lt;ItemGroup&gt;
///     &lt;EmbeddedResource Include="ReportTemplate.html" /&gt;
///   &lt;/ItemGroup&gt;
///
/// Or to copy it to the output directory instead, add to your .csproj:
///   &lt;ItemGroup&gt;
///     &lt;Content Include="ReportTemplate.html"&gt;
///       &lt;CopyToOutputDirectory&gt;PreserveNewest&lt;/CopyToOutputDirectory&gt;
///     &lt;/Content&gt;
///   &lt;/ItemGroup&gt;
/// </summary>
public class ReportGenerator
{
    private readonly string _reportFolder;

    // ── Token names matching %%PLACEHOLDER%% markers in ReportTemplate.html ──
    // Using constants avoids typos when calling Replace().
    private const string T_Total       = "%%TOTAL%%";
    private const string T_TotalRuns   = "%%TOTAL_RUNS%%";
    private const string T_Categories  = "%%CATEGORIES%%";
    private const string T_GeneratedAt = "%%GENERATED_AT%%";
    private const string T_PassBadge   = "%%PASS_BADGE_CLASS%%";
    private const string T_PassRate    = "%%PASS_RATE%%";
    private const string T_CatCount    = "%%CAT_COUNT%%";
    private const string T_Passed      = "%%PASSED%%";
    private const string T_AvgEmb      = "%%AVG_EMB%%";
    private const string T_EmbColor    = "%%EMB_COLOR%%";
    private const string T_EmbNote     = "%%EMB_NOTE%%";
    private const string T_AvgJudge    = "%%AVG_JUDGE%%";
    private const string T_JudgeColor  = "%%JUDGE_COLOR%%";
    private const string T_JudgeNote   = "%%JUDGE_NOTE%%";
    private const string T_JsonData    = "%%JSON_DATA%%";

    /// <param name="reportFolder">Folder where all report files will be saved (default: reports).</param>
    public ReportGenerator(string reportFolder = "reports")
    {
        _reportFolder = reportFolder;
    }

    // =========================================================================
    // Public entry point
    // =========================================================================

    /// <summary>
    /// Generates all four report formats and prints a summary to console.
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
            Directory.CreateDirectory(_reportFolder);

            await WriteTextReportAsync(results);
            await WriteJsonReportAsync(results);
            await WriteCsvReportAsync(results);
            await WriteHtmlReportAsync(results);

            PrintConsoleSummary(results);

            Console.WriteLine($"\nReports saved to: {Path.GetFullPath(_reportFolder)}/");
            Console.WriteLine("  report.txt  — human-readable summary");
            Console.WriteLine("  report.json — full data for visualization");
            Console.WriteLine("  report.csv  — flat data for Excel/charts");
            Console.WriteLine("  report.html — interactive visual dashboard");

            OpenInBrowser(Path.Combine(_reportFolder, "report.html"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReportGenerator Error] Could not write reports: {ex.Message}");
        }
    }

    // =========================================================================
    // Text report
    // =========================================================================

    private async Task WriteTextReportAsync(List<TestResult> results)
    {
        string path    = Path.Combine(_reportFolder, "report.txt");
        string content = BuildTextReport(results);
        await File.WriteAllTextAsync(path, content);
    }

    private static string BuildTextReport(List<TestResult> results)
    {
        var    sb        = new StringBuilder();
        string separator = new string('=', 60);
        string thin      = new string('-', 60);

        sb.AppendLine(separator);
        sb.AppendLine("  LLM SEMANTIC EVALUATOR — TEST REPORT");
        sb.AppendLine($"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(separator);
        sb.AppendLine();

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

    // =========================================================================
    // JSON report
    // =========================================================================

    private async Task WriteJsonReportAsync(List<TestResult> results)
    {
        string path   = Path.Combine(_reportFolder, "report.json");
        int    total  = results.Count;
        int    passed = results.Count(r => r.Passed);

        var report = new
        {
            generatedAt = DateTime.Now,
            summary = new
            {
                totalTests        = total,
                passed            = passed,
                failed            = total - passed,
                passRatePercent   = Math.Round((double)passed / total * 100, 1),
                avgEmbeddingScore = Math.Round(results.Average(r => r.AverageEmbeddingScore), 2),
                avgJudgeScore     = Math.Round(results.Average(r => r.AverageJudgeScore), 1)
            },
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
            testResults = results.Select(r => new
            {
                testId            = r.TestId,
                category          = r.Category,
                prompt            = r.Prompt,
                expectedOutput    = r.ExpectedOutput,
                passed            = r.Passed,
                passedRuns        = r.PassedRunsCount,
                totalRuns         = r.TotalRunsCount,
                avgEmbeddingScore = Math.Round(r.AverageEmbeddingScore, 2),
                avgJudgeScore     = Math.Round(r.AverageJudgeScore, 1),
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
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, options));
    }

    // =========================================================================
    // CSV report
    // =========================================================================

    private async Task WriteCsvReportAsync(List<TestResult> results)
    {
        string path = Path.Combine(_reportFolder, "report.csv");
        var    sb   = new StringBuilder();

        sb.AppendLine("TestId,Category,Passed,PassedRuns,TotalRuns,AvgEmbeddingScore,AvgJudgeScore,Prompt,ExpectedOutput");

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

    // =========================================================================
    // HTML dashboard report
    // =========================================================================

    /// <summary>
    /// Reads ReportTemplate.html, replaces all %%PLACEHOLDER%% tokens with
    /// real computed values, and writes report.html to the reports folder.
    /// </summary>
    private async Task WriteHtmlReportAsync(List<TestResult> results)
    {
        string template = LoadHtmlTemplate();

        // ── Compute all substitution values ───────────────────────────────────
        int    total      = results.Count;
        int    passed     = results.Count(r => r.Passed);
        double passRate   = Math.Round((double)passed / total * 100, 1);
        double avgEmb     = Math.Round(results.Average(r => r.AverageEmbeddingScore), 2);
        double avgJudge   = Math.Round(results.Average(r => r.AverageJudgeScore),     1);
        int    totalRuns  = results.Sum(r => r.TotalRunsCount);
        int    catCount   = results.Select(r => r.Category).Distinct().Count();
        string categories = string.Join(", ", results.Select(r => r.Category)
                                                     .Distinct()
                                                     .OrderBy(c => c));

        string passBadge  = passRate >= 100 ? "badge-pass" : passRate >= 80 ? "badge-warn" : "badge-fail";
        string embColor   = avgEmb   >= 0.85 ? "#3B6D11" : "#BA7517";
        string embNote    = avgEmb   >= 0.85 ? "Above 0.85 threshold" : "Below 0.85 threshold";
        string judgeColor = avgJudge >= 8    ? "#3B6D11" : "#BA7517";
        string judgeNote  = avgJudge >= 8    ? "Above threshold (>=8)" : "Below threshold (>=8)";

        // ── Serialize results into compact JSON for the dashboard JS ──────────
        string jsonData = JsonSerializer.Serialize(results.Select(r => new
        {
            id        = r.TestId,
            cat       = r.Category,
            passed    = r.Passed,
            runs      = r.PassedRunsCount,
            total     = r.TotalRunsCount,
            avgEmb    = Math.Round(r.AverageEmbeddingScore, 2),
            avgJudge  = Math.Round(r.AverageJudgeScore,     1),
            embRuns   = r.Runs.Select(x => Math.Round(x.EmbeddingScore, 2)).ToList(),
            judgeRuns = r.Runs.Select(x => (int)x.JudgeScore).ToList()
        }));

        // ── Replace every %%PLACEHOLDER%% token with the real value ───────────
        string html = template
            .Replace(T_Total,       total.ToString())
            .Replace(T_TotalRuns,   totalRuns.ToString())
            .Replace(T_Categories,  categories)
            .Replace(T_GeneratedAt, DateTime.Now.ToString("dd MMM yyyy HH:mm"))
            .Replace(T_PassBadge,   passBadge)
            .Replace(T_PassRate,    passRate.ToString("F1"))
            .Replace(T_CatCount,    catCount.ToString())
            .Replace(T_Passed,      passed.ToString())
            .Replace(T_AvgEmb,      avgEmb.ToString("F2"))
            .Replace(T_EmbColor,    embColor)
            .Replace(T_EmbNote,     embNote)
            .Replace(T_AvgJudge,    avgJudge.ToString("F1"))
            .Replace(T_JudgeColor,  judgeColor)
            .Replace(T_JudgeNote,   judgeNote)
            .Replace(T_JsonData,    jsonData);

        await File.WriteAllTextAsync(Path.Combine(_reportFolder, "report.html"), html);
    }

    /// <summary>
    /// Loads the HTML template string from either an embedded resource or a
    /// file next to the executable. See the class-level XML comment for the
    /// .csproj snippets needed for each option.
    /// </summary>
    private static string LoadHtmlTemplate()
    {
        // Strategy 1: embedded resource (recommended for production builds)
        var    assembly = System.Reflection.Assembly.GetExecutingAssembly();
        string resName  = assembly.GetManifestResourceNames()
                                  .FirstOrDefault(n => n.EndsWith("ReportTemplate.html"))
                          ?? string.Empty;

        if (!string.IsNullOrEmpty(resName))
        {
            using var stream = assembly.GetManifestResourceStream(resName)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        // Strategy 2: file next to the executable (easiest during development)
        string templatePath = Path.Combine(AppContext.BaseDirectory, "ReportTemplate.html");

        if (File.Exists(templatePath))
            return File.ReadAllText(templatePath);

        // Neither found — give a clear, actionable error message
        throw new FileNotFoundException(
            "ReportTemplate.html not found. " +
            "Either add it as an EmbeddedResource in your .csproj, " +
            "or set CopyToOutputDirectory to PreserveNewest. " +
            "See the XML comment on ReportGenerator for the exact .csproj snippets.",
            templatePath);
    }

    // =========================================================================
    // Auto-open browser
    // =========================================================================

    /// <summary>
    /// Opens the HTML report in the system default browser.
    /// Works on Windows, macOS, and Linux.
    /// Silently skips if the browser cannot be launched (e.g. headless CI).
    /// </summary>
    private static void OpenInBrowser(string filePath)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = Path.GetFullPath(filePath),
                UseShellExecute = true
            });
        }
        catch
        {
            Console.WriteLine("  (Could not auto-open browser — open report.html manually)");
        }
    }

    // =========================================================================
    // Shared helpers
    // =========================================================================

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

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "...";
    }

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}