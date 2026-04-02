using LLMSemanticEvaluator.Configuration;
using LLMSemanticEvaluator.Interfaces;
using LLMSemanticEvaluator.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LLMSemanticEvaluator.Services;

/// <summary>
/// Service that runs the evaluation: loads test cases, runs them
/// against the LLM, and writes the reports.
///
/// BackgroundService integrates with the Microsoft.Extensions.Hosting lifecycle:
///   - ExecuteAsync is called by the host after all services are built and ready.
///   - CancellationToken is wired to Ctrl+C / host shutdown signals automatically.
///   - StopApplication() signals the host to shut down cleanly after the run.
///
/// All dependencies are injected by the DI container.
/// This class never constructs another class with "new" and never reads
/// configuration directly — it receives IOptions&lt;TestConfiguration&gt;.
/// </summary>
public class EvaluatorService : BackgroundService
{
    private readonly TestConfiguration         _config;
    private readonly ITestLoader               _loader;
    private readonly TestRunner                _runner;
    private readonly ReportGenerator           _reportGenerator;
    private readonly IHostApplicationLifetime  _lifetime;
    private readonly ILogger<EvaluatorService> _logger;

    public EvaluatorService(
        IOptions<TestConfiguration> options,
        ITestLoader                 loader,
        TestRunner                  runner,
        ReportGenerator             reportGenerator,
        IHostApplicationLifetime    lifetime,
        ILogger<EvaluatorService>   logger)
    {
        _config          = options.Value;
        _loader          = loader;
        _runner          = runner;
        _reportGenerator = reportGenerator;
        _lifetime        = lifetime;
        _logger          = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            LogStartupBanner();

            // Load test cases from JSON
            _logger.LogInformation("Loading test cases...");
            List<TestCase> testCases =
                await _loader.LoadTestsAsync(_config.TestCasesPath);

            if (testCases.Count == 0)
            {
                _logger.LogError("No test cases were loaded. Aborting.");
                return;
            }

            _logger.LogInformation("Loaded {Count} test cases.", testCases.Count);

            // Run all tests
            List<TestResult> results =
                await _runner.RunAllAsync(testCases, stoppingToken);

            // Generate reports
            await _reportGenerator.GenerateAsync(results);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Evaluation cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during evaluation.");
        }
        finally
        {
            // Tell the host the application is done.
            // Without this call the host waits indefinitely after ExecuteAsync returns.
            _lifetime.StopApplication();
        }
    }

    /// <summary>
    /// Logs the active configuration at startup so the operator can verify
    /// the correct provider, model, and thresholds before the run begins.
    /// </summary>
    private void LogStartupBanner()
    {
        _logger.LogInformation("══════════════════════════════════════════");
        _logger.LogInformation("  LLM Semantic Evaluator");
        _logger.LogInformation("══════════════════════════════════════════");
        _logger.LogInformation("  Chat provider       : {Provider}",          _config.Provider);
        _logger.LogInformation("  Embedding provider  : {EmbeddingProvider}", _config.EmbeddingProvider);
        _logger.LogInformation("  Chat model          : {ChatModel}",         _config.ChatModel);
        _logger.LogInformation("  Embedding model     : {EmbeddingModel}",    _config.EmbeddingModel);
        _logger.LogInformation("  Runs per test       : {Runs}",              _config.NumberOfRuns);
        _logger.LogInformation("  Min passing runs    : {MinPass}",           _config.MinimumPassingRuns);
        _logger.LogInformation("  Embedding threshold : {EmbThreshold}",      _config.EmbeddingThreshold);
        _logger.LogInformation("  Judge threshold     : {JudgeThreshold}/10", _config.JudgeThreshold);
        _logger.LogInformation("══════════════════════════════════════════");
    }
}
