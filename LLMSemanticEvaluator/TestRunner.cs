using LLMSemanticEvaluator.Configuration;
using LLMSemanticEvaluator.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LLMSemanticEvaluator;

/// <summary>
/// Runs all test cases against the LLM and returns one TestResult per case.
///
/// For each test case, TestRunner:
///   1. Sends the prompt to the LLM under test via IChatClient.GetResponseAsync.
///   2. Runs EmbeddingValidator and LLMJudgeValidator independently on the response.
///   3. Repeats steps 1–2 for NumberOfRuns iterations to handle LLM non-determinism.
///   4. Aggregates results with majority-vote logic:
///        RunPassed  = EmbeddingPassed OR JudgePassed
///        TestPassed = PassedRunsCount >= MinimumPassingRuns
///
/// OR logic is used (not AND) because the two validators have complementary failure
/// modes: embedding similarity is structurally low for short expected outputs, while
/// the judge compensates by evaluating meaning rather than vector distance.
///
/// IChatClient is the standard Microsoft.Extensions.AI interface. The concrete
/// implementation (OpenAI or Ollama) is resolved by LLMClientFactory and injected
/// here — this class never knows which provider is used.
///
/// All settings come from IOptions&lt;TestConfiguration&gt; — no hard-coded values.
/// ILogger replaces Console.WriteLine for environment-independent output.
/// </summary>
public class TestRunner
{
    private readonly IChatClient          _chatClient;
    private readonly ChatOptions          _chatOptions;
    private readonly EmbeddingValidator   _embeddingValidator;
    private readonly LLMJudgeValidator    _judgeValidator;
    private readonly TestConfiguration    _config;
    private readonly ILogger<TestRunner>  _logger;

    public TestRunner(
        IChatClient                 chatClient,
        EmbeddingValidator          embeddingValidator,
        LLMJudgeValidator           judgeValidator,
        IOptions<TestConfiguration> options,
        ILogger<TestRunner>         logger)
    {
        _chatClient         = chatClient;
        _embeddingValidator = embeddingValidator;
        _judgeValidator     = judgeValidator;
        _config             = options.Value;
        _logger             = logger;

        // Some models (gpt-5-mini, o1, o3 etc.) do not accept a temperature parameter
        // and will return HTTP 400 if it is set. For those models ChatOptions is left
        // empty so the provider uses its own default. For all other models, Temperature
        // is read from appsettings.json and applied on every call.
        _chatOptions = SupportsTemperature(_config.ChatModel)
            ? new ChatOptions { Temperature = (float)_config.Temperature }
            : new ChatOptions();
    }

    /// <summary>
    /// Runs all test cases and returns one TestResult per case.
    /// Logs a one-line summary after each test so progress is visible in real time.
    /// Respects the CancellationToken — stops cleanly on Ctrl+C or host shutdown.
    /// </summary>
    public async Task<List<TestResult>> RunAllAsync(
        List<TestCase>    testCases,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TestResult>();
        int total   = testCases.Count;

        _logger.LogInformation(
            "Starting: {Total} tests × {Runs} runs each", total, _config.NumberOfRuns);

        for (int i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var testCase = testCases[i];
            var result   = await RunSingleTestAsync(testCase, cancellationToken);
            results.Add(result);

            _logger.LogInformation(
                "[{Index}/{Total}] {Id} → {Status} ({Passed}/{Runs} runs passed)",
                i + 1,
                total,
                testCase.Id,
                result.Passed ? "PASS" : "FAIL",
                result.PassedRunsCount,
                result.TotalRunsCount);
        }

        _logger.LogInformation(
            "Complete. Passed: {Passed}/{Total}",
            results.Count(r => r.Passed), total);

        return results;
    }

    /// <summary>
    /// Runs one test case for NumberOfRuns iterations.
    /// If an individual run throws (e.g. network timeout), it is recorded as a
    /// failed run and execution continues — one error does not abort the full suite.
    /// </summary>
    private async Task<TestResult> RunSingleTestAsync(
        TestCase          testCase,
        CancellationToken cancellationToken)
    {
        var result = new TestResult
        {
            TestId         = testCase.Id,
            Category       = testCase.Category,
            Prompt         = testCase.Prompt,
            ExpectedOutput = testCase.ExpectedOutput
        };

        for (int run = 0; run < _config.NumberOfRuns; run++)
        {
            try
            {
                // Send the prompt to the LLM under test.
                // ChatOptions carries Temperature from appsettings.json.
                // GetResponseAsync is the Microsoft.Extensions.AI standard method.
                ChatResponse chatResponse = await _chatClient.GetResponseAsync(
                    testCase.Prompt, _chatOptions, cancellationToken);

                string actual = chatResponse.Text;

                // Run both validators independently on the response.
                var embResult   = await _embeddingValidator.ValidateAsync(
                    testCase.ExpectedOutput, actual);

                var judgeResult = await _judgeValidator.ValidateAsync(
                    testCase.Prompt, testCase.ExpectedOutput,
                    actual, testCase.EvaluationCriteria);

                // Record the run. RunPassed = EmbeddingPassed OR JudgePassed.
                // OR is used because the two validators have complementary failure modes.
                result.Runs.Add(new TestRun
                {
                    Response        = actual,
                    EmbeddingScore  = embResult.Score,
                    JudgeScore      = (int)judgeResult.Score,
                    EmbeddingPassed = embResult.Passed,
                    JudgePassed     = judgeResult.Passed,
                    JudgeReasoning  = judgeResult.Reasoning ?? string.Empty,
                    ExecutedAt      = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "[{Id}] run {Run}/{Total} failed: {Error}",
                    testCase.Id, run + 1, _config.NumberOfRuns, ex.Message);

                result.Runs.Add(new TestRun
                {
                    Response   = $"ERROR: {ex.Message}",
                    ExecutedAt = DateTime.UtcNow
                    // EmbeddingPassed and JudgePassed both default to false
                });
            }

            // Wait between runs to avoid hitting API rate limits.
            // RequestDelayMs is read from appsettings.json — never hard-coded.
            if (run < _config.NumberOfRuns - 1)
                await Task.Delay(_config.RequestDelayMs, cancellationToken);
        }

        result.TotalRunsCount        = result.Runs.Count;
        result.PassedRunsCount       = result.Runs.Count(r => r.Passed);
        result.AverageEmbeddingScore = result.Runs.Average(r => r.EmbeddingScore);
        result.AverageJudgeScore     = result.Runs.Average(r => r.JudgeScore);
        result.Passed                = result.PassedRunsCount >= _config.MinimumPassingRuns;

        return result;
    }

    /// <summary>
    /// Returns false for models that reject the temperature parameter entirely.
    /// gpt-5 and OpenAI reasoning models (o1, o3) only accept the default value
    /// and return HTTP 400 if temperature is set explicitly.
    /// </summary>
    private static bool SupportsTemperature(string modelName)
    {
        string m = modelName.ToLowerInvariant();
        return !m.StartsWith("gpt-5")
            && !m.StartsWith("o1")
            && !m.StartsWith("o3");
    }
}