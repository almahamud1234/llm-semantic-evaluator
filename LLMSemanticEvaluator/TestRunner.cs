using LLMSemanticEvaluator.Models;
using LLMSemanticEvaluator.Interfaces;

namespace LLMSemanticEvaluator;

/// <summary>
/// Orchestrates the full test execution pipeline:
///   1. Receives loaded test cases
///   2. For each test: sends prompt to LLM, validates response, repeats 3 times
///   3. Aggregates results using majority vote (2/3 runs must pass)
/// </summary>
public class TestRunner
{
    private readonly ILLMClient         _llmClient;
    private readonly EmbeddingValidator _embeddingValidator;
    private readonly LLMJudgeValidator  _judgeValidator;
    private readonly int                _runsPerTest;
    private readonly int                _minPassRun;

    /// <param name="llmClient">Sends prompts to OpenAI and gets responses.</param>
    /// <param name="embeddingValidator">Validates using cosine similarity.</param>
    /// <param name="judgeValidator">Validates using LLM-as-judge scoring.</param>
    /// <param name="runsPerTest">How many times to run each test.</param>
    /// <param name="minPassRun">Minimum passing number out of total test run.</param>
    public TestRunner(
        ILLMClient         llmClient,
        EmbeddingValidator embeddingValidator,
        LLMJudgeValidator  judgeValidator,
        int                runsPerTest,
        int                minPassRun)
    {
        _llmClient          = llmClient;
        _embeddingValidator = embeddingValidator;
        _judgeValidator     = judgeValidator;
        _runsPerTest        = runsPerTest;
        _minPassRun         = minPassRun;
    }

    /// <summary>
    /// Runs all test cases and returns a result for each one.
    /// Prints progress to console as tests complete.
    /// </summary>
    public async Task<List<TestResult>> RunAllAsync(List<TestCase> testCases)
    {
        var results = new List<TestResult>();
        int total   = testCases.Count;

        Console.WriteLine($"Starting test run: {total} tests, {_runsPerTest} runs each\n");

        for (int i = 0; i < total; i++)
        {
            var testCase = testCases[i];
            Console.Write($"[{i + 1}/{total}] {testCase.Id} ... ");

            var result = await RunSingleTestAsync(testCase);
            results.Add(result);

            Console.WriteLine($"{(result.Passed ? "✅ PASS" : "❌ FAIL")} " +
                              $"({result.PassedRunsCount}/{result.TotalRunsCount} runs passed)");
        }

        Console.WriteLine($"\nDone. Passed: {results.Count(r => r.Passed)}/{total}");
        return results;
    }

    /// <summary>
    /// Runs a single test case 3 times and aggregates the results.
    /// If a run fails due to an API error, it is counted as a failed run.
    /// </summary>
    private async Task<TestResult> RunSingleTestAsync(TestCase testCase)
    {
        var result = new TestResult
        {
            TestId         = testCase.Id,
            Category       = testCase.Category,
            Prompt         = testCase.Prompt,
            ExpectedOutput = testCase.ExpectedOutput
        };

        for (int run = 0; run < _runsPerTest; run++)
        {
            try
            {
                // Step 1: Send prompt to LLM
                string actual = await _llmClient.SendPromptAsync(testCase.Prompt);

                // Step 2: Validate with both validators independently
                var embResult   = await _embeddingValidator.ValidateAsync(testCase.ExpectedOutput, actual);
                var judgeResult = await _judgeValidator.ValidateAsync(testCase.Prompt, testCase.ExpectedOutput, actual);

                // Step 3: Record each validator's outcome separately.
                // TestRun.Passed is a computed property: true if EmbeddingPassed OR JudgePassed
                result.Runs.Add(new TestRun
                {
                    Response        = actual,
                    EmbeddingScore  = embResult.Score,
                    JudgeScore      = (int)judgeResult.Score,
                    EmbeddingPassed = embResult.Passed,
                    JudgePassed     = judgeResult.Passed,
                    ExecutedAt      = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                // API failure — log it and count as a failed run
                Console.WriteLine($"\n  [Run {run + 1} Error] {ex.Message}");

                result.Runs.Add(new TestRun
                {
                    Response   = $"ERROR: {ex.Message}",
                    ExecutedAt = DateTime.UtcNow
                    // EmbeddingPassed + JudgePassed default to false → Passed = false
                });
            }

            // Small delay between runs to avoid hitting API rate limits
            if (run < _runsPerTest - 1)
                await Task.Delay(500);
        }

        // Aggregate all runs into the final TestResult
        result.TotalRunsCount        = result.Runs.Count;
        result.PassedRunsCount       = result.Runs.Count(r => r.Passed);
        result.AverageEmbeddingScore = result.Runs.Average(r => r.EmbeddingScore);
        result.AverageJudgeScore     = result.Runs.Average(r => r.JudgeScore);
        result.Passed                = result.PassedRunsCount >= _minPassRun; // majority vote: 2/3

        return result;
    }
}