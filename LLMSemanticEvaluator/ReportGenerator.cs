using System.Text;
using System.Text.Json;
using LLMSemanticEvaluator.Configuration;
using LLMSemanticEvaluator.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly ILogger<ReportGenerator>  _logger;

    // Config values read once at construction time and reused across all
    // report-writing methods — avoids threading the same parameters everywhere.
    private readonly double _embeddingThreshold;
    private readonly int    _judgeThreshold;
    private readonly int    _numberOfRuns;
    private readonly int    _minimumPassingRuns;

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
    private const string T_RunsPerTest  = "%%RUNS_PER_TEST%%";
    private const string T_EmbThresh   = "%%EMB_THRESHOLD%%";
    private const string T_JudgeThresh = "%%JUDGE_THRESHOLD%%";
    private const string T_MinPassRuns = "%%MIN_PASS_RUNS%%";
    private const string T_Insights    = "%%INSIGHTS_HTML%%";
    private const string T_Failures    = "%%FAILURES_HTML%%";

    /// <summary>
    /// Constructs the generator, reading threshold and run-count settings from
    /// configuration so the HTML report reflects the exact values that were used
    /// during the test run — not hard-coded defaults.
    /// </summary>
    /// <param name="options">Bound configuration from appsettings.json via IOptions.</param>
    /// <param name="logger">Logger injected by the DI container.</param>
    /// <param name="reportFolder">Output folder; defaults to "reports".</param>
    public ReportGenerator(
        IOptions<TestConfiguration>  options,
        ILogger<ReportGenerator>     logger,
        string reportFolder = "reports")
    {
        _logger       = logger;
        _reportFolder = reportFolder;
        
        // repeating options.Value.EmbeddingThreshold throughout.
        var cfg             = options.Value;
        _embeddingThreshold = cfg.EmbeddingThreshold;
        _judgeThreshold     = cfg.JudgeThreshold;
        _numberOfRuns       = cfg.NumberOfRuns;
        _minimumPassingRuns = cfg.MinimumPassingRuns;
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
            _logger.LogWarning("No results to report.");
            return;
        }

        try
        {
            Directory.CreateDirectory(_reportFolder);

            await WriteTextReportAsync(results);
            await WriteJsonReportAsync(results);
            await WriteCsvReportAsync(results);
            await WriteHtmlReportAsync(results);

            LogSummary(results);

            string folder = Path.GetFullPath(_reportFolder);
            _logger.LogInformation("Reports saved to: {Folder}/", folder);
            _logger.LogInformation("  report.txt  — human-readable summary");
            _logger.LogInformation("  report.json — full data for visualization");
            _logger.LogInformation("  report.csv  — flat data for Excel/charts");
            _logger.LogInformation("  report.html — interactive visual dashboard");

            OpenInBrowser(Path.Combine(_reportFolder, "report.html"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not write reports.");
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
                    judgeReasoning = run.JudgeReasoning,
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

        sb.AppendLine("TestId,Category,Prompt,ExpectedOutput,TestPassed,PassedRuns,TotalRuns," +
              "AvgEmbeddingScore,AvgJudgeScore,RunNumber,RunPassed,EmbeddingScore," +
              "JudgeScore,LLMResponse,JudgeReasoning,ExecutedAt");

        foreach (var result in results)
        {
            for (int i = 0; i < result.Runs.Count; i++)
            {
                var run = result.Runs[i];
                sb.AppendLine(string.Join(",",
                    CsvEscape(result.TestId),
                    CsvEscape(result.Category),
                    CsvEscape(result.Prompt),
                    CsvEscape(result.ExpectedOutput),
                    result.Passed,
                    result.PassedRunsCount,
                    result.TotalRunsCount,
                    result.AverageEmbeddingScore.ToString("F2"),
                    result.AverageJudgeScore.ToString("F1"),
                    i + 1, // run number
                    run.Passed,
                    run.EmbeddingScore.ToString("F2"),
                    run.JudgeScore,
                    CsvEscape(run.Response),
                    CsvEscape(run.JudgeReasoning),
                    CsvEscape(run.ExecutedAt.ToString("o"))
                ));
            }
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
        string embColor   = avgEmb   >= _embeddingThreshold ? "#3B6D11" : "#BA7517";
        string embNote    = avgEmb   >= _embeddingThreshold
                            ? $"Above {_embeddingThreshold:F2} threshold"
                            : $"Below {_embeddingThreshold:F2} threshold";
        string judgeColor = avgJudge >= _judgeThreshold ? "#3B6D11" : "#BA7517";
        string judgeNote  = avgJudge >= _judgeThreshold
                            ? $"Above threshold (>={_judgeThreshold})"
                            : $"Below threshold (>={_judgeThreshold})";
        string insightsHtml = BuildInsightsHtml(results, _embeddingThreshold, _judgeThreshold, _numberOfRuns, _minimumPassingRuns);
        string failuresHtml = BuildFailuresHtml(results);

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
            .Replace(T_JsonData,    jsonData)
            .Replace(T_RunsPerTest,  _numberOfRuns.ToString())
            .Replace(T_EmbThresh,    _embeddingThreshold.ToString("F2"))
            .Replace(T_JudgeThresh,  _judgeThreshold.ToString())
            .Replace(T_MinPassRuns,  _minimumPassingRuns.ToString())
            .Replace(T_Insights,     insightsHtml)
            .Replace(T_Failures,     failuresHtml);


        await File.WriteAllTextAsync(Path.Combine(_reportFolder, "report.html"), html);
    }
    
    private static string BuildInsightsHtml(
        List<TestResult> results,
        double embeddingThreshold,
        int    judgeThreshold,
        int    numberOfRuns,
        int    minimumPassingRuns)
    {
        int    total      = results.Count;
        double avgEmb     = Math.Round(results.Average(r => r.AverageEmbeddingScore), 2);
        double passRate   = Math.Round((double)results.Count(r => r.Passed) / total * 100, 1);

        // Count how many tests are rescued by the judge (pass via judge, not embedding)
        int judgeRescued = results.Count(r =>
            r.Passed &&
            r.AverageEmbeddingScore < embeddingThreshold &&
            r.AverageJudgeScore     >= judgeThreshold);

        var sb = new StringBuilder();

        sb.Append($"""
            <div class="insight-box">
              <strong>Why is the average embedding score low ({avgEmb:F2}) yet the pass rate is {passRate:F1}%?</strong><br>
              Short expected outputs produce low cosine similarity against full-sentence LLM responses because the vector spaces do not overlap well at different lengths. This is a known limitation of embedding-based evaluation. The LLM judge correctly scores these as high quality, and the OR logic ensures they still pass. {judgeRescued} of {total} tests passed via the judge validator alone, confirming the dual-validator design is essential.
            </div>
            """);

        sb.Append($"""
            <div class="insight-box" style="margin-bottom:0;">
              <strong>Why run each test {numberOfRuns} times?</strong><br>
              LLMs are non-deterministic — the same prompt can produce different wording across runs. Running {numberOfRuns} times and requiring {minimumPassingRuns}/{numberOfRuns} to pass (majority vote) eliminates single-run flukes without being too strict. Consistent results across all runs indicate a stable, reliable model for that task type.
            </div>
            """);

        return sb.ToString();
    }

    private static string BuildFailuresHtml(List<TestResult> results)
    {
        var failures = results.Where(r => !r.Passed).ToList();

        if (failures.Count == 0)
        {
            return """
                <div class="insight-box" style="margin-bottom:0;background:#f0faf4;border-left-color:#1D9E75;">
                  <strong style="color:#1D9E75;">No failures detected.</strong><br>
                  All test cases passed. The model is performing reliably across all categories and runs.
                </div>
                """;
        }

        var sb = new StringBuilder();
        foreach (var r in failures)
        {
            string prompt   = Truncate(r.Prompt,         120);
            string expected = Truncate(r.ExpectedOutput,  80);
            double avgJudge = Math.Round(r.AverageJudgeScore, 1);

            sb.Append($"""
                <div class="failure-box" style="margin-bottom:0.8rem;">
                  <strong>Test: {r.TestId}</strong> &nbsp;<span style="font-size:11px;color:#888780;">({r.Category})</span><br>
                  <strong>Prompt:</strong> &ldquo;{HtmlEncode(prompt)}&rdquo;<br>
                  <strong>Expected:</strong> &ldquo;{HtmlEncode(expected)}&rdquo;<br>
                  <strong>Result:</strong> {r.PassedRunsCount}/{r.TotalRunsCount} runs passed &nbsp;|&nbsp; Avg Judge: {avgJudge}/10<br>
                </div>
                """);
        }

        return sb.ToString();
    }

    private static string HtmlEncode(string text)
        => System.Web.HttpUtility.HtmlEncode(text ?? string.Empty);

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
    private void OpenInBrowser(string filePath)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = Path.GetFullPath(filePath),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not auto-open browser — open report.html manually.");
        }
    }

    // =========================================================================
    // Shared helpers
    // =========================================================================
    /// <summary>
    
    /// Logs the overall pass/fail summary through the logging infrastructure
    /// so it appears in any hosting environment (console, Docker, CI pipeline).
    /// </summary>
    private void LogSummary(List<TestResult> results)
    {
        int    total  = results.Count;
        int    passed = results.Count(r => r.Passed);
        double rate   = (double)passed / total * 100;

        _logger.LogInformation("══════════════════════════════════════════");
        _logger.LogInformation("  Report Summary");
        _logger.LogInformation("══════════════════════════════════════════");
        _logger.LogInformation("  Total  : {Total}",          total);
        _logger.LogInformation("  Passed : {Passed}",         passed);
        _logger.LogInformation("  Failed : {Failed}",         total - passed);
        _logger.LogInformation("  Rate   : {Rate:F1}%",       rate);
        _logger.LogInformation("══════════════════════════════════════════");
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