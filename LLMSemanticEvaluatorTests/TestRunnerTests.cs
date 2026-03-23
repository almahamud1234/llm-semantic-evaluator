using LLMSemanticEvaluator;
using LLMSemanticEvaluator.Interfaces;
using LLMSemanticEvaluator.Models;
using Moq;

namespace LLMSemanticEvaluatorTests;

/// <summary>
/// Unit tests for <see cref="TestRunner"/>.
///
/// TestRunner takes concrete EmbeddingValidator and LLMJudgeValidator (not interfaces),
/// so we control their behaviour by mocking their upstream dependencies:
///   - ILLMClient          → controls what the tested LLM responds
///   - IEmbeddingProvider  → controls embedding vectors returned
///   - ISimilarityCalculator → controls cosine similarity scores
///
/// To keep tests fast, runsPerTest is set to 1 wherever the majority-vote
/// logic itself is not under test (avoids 3 × Task.Delay(500) per test).
///
/// Run with: dotnet test
/// </summary>
public class TestRunnerTests
{
    // ── Shared mocks ──────────────────────────────────────────────────────────
    private readonly Mock<ILLMClient>            _llmClientMock    = new();
    private readonly Mock<IEmbeddingProvider>    _embeddingsMock   = new();
    private readonly Mock<ISimilarityCalculator> _calculatorMock   = new();

    // Dummy embedding vector — content doesn't matter, calculator is mocked
    private static readonly float[] DummyVec = { 0.1f, 0.2f, 0.3f };

    // ── Builder helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds a TestRunner wired to all shared mocks.
    /// minPassRun defaults to 1 for single-run tests, matching runsPerTest: 1.
    /// Pass minPassRun: 2 explicitly for majority-vote (3-run) tests.
    /// </summary>
    private TestRunner BuildRunner(int runsPerTest = 1, int minPassRun = 1)
    {
        var embeddingValidator = new EmbeddingValidator(
            _embeddingsMock.Object, _calculatorMock.Object, threshold: 0.85);

        var judgeValidator = new LLMJudgeValidator(
            _llmClientMock.Object, threshold: 8);

        return new TestRunner(
            _llmClientMock.Object,
            embeddingValidator,
            judgeValidator,
            runsPerTest,
            minPassRun);
    }

    /// <summary>
    /// Returns a minimal valid TestCase.
    /// </summary>
    private static TestCase MakeTestCase(string id = "t1", string category = "factual") => new()
    {
        Id             = id,
        Category       = category,
        Prompt         = "What is the capital of France?",
        ExpectedOutput = "Paris"
    };

    /// <summary>
    /// Sets up mocks so one full run succeeds with the given similarity score
    /// and judge score string (e.g. "9").
    /// The LLM is called twice per run: once for the prompt, once for the judge.
    /// We use SetupSequence so the first call returns the answer and the second
    /// returns the judge score.
    /// </summary>
    private void SetupPassingRun(double similarity, string judgeResponse = "9")
    {
        // First SendPromptAsync call → LLM answer; second → judge response
        _llmClientMock
            .SetupSequence(c => c.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Paris")       // LLM answer
            .ReturnsAsync(judgeResponse); // Judge score

        _embeddingsMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DummyVec);

        _calculatorMock
            .Setup(c => c.CalculateCosineSimilarity(It.IsAny<float[]>(), It.IsAny<float[]>()))
            .Returns(similarity);
    }

    // =========================================================================
    // RunAllAsync — result structure
    // =========================================================================

    /// <summary>
    /// RunAllAsync must return exactly one TestResult per TestCase.
    /// </summary>
    [Fact]
    public async Task RunAllAsync_SingleTestCase_ReturnsOneResult()
    {
        // Arrange
        SetupPassingRun(similarity: 0.92);
        var runner = BuildRunner(runsPerTest: 1, minPassRun: 1);

        // Act
        var results = await runner.RunAllAsync(new List<TestCase> { MakeTestCase() });

        // Assert
        Assert.Single(results);
    }

    /// <summary>
    /// TestResult fields must be populated from the original TestCase.
    /// </summary>
    [Fact]
    public async Task RunAllAsync_ResultFields_MappedFromTestCase()
    {
        // Arrange
        SetupPassingRun(similarity: 0.92);
        var runner   = BuildRunner(runsPerTest: 1, minPassRun: 1);
        var testCase = MakeTestCase(id: "factual_001", category: "factual");

        // Act
        var results = await runner.RunAllAsync(new List<TestCase> { testCase });

        // Assert
        Assert.Equal("factual_001", results[0].TestId);
        Assert.Equal("factual",     results[0].Category);
        Assert.Equal(testCase.Prompt,         results[0].Prompt);
        Assert.Equal(testCase.ExpectedOutput, results[0].ExpectedOutput);
    }

    /// <summary>
    /// RunAllAsync must return one result per test case when given multiple cases.
    /// </summary>
    [Fact]
    public async Task RunAllAsync_MultipleTestCases_ReturnsResultForEach()
    {
        // Arrange — LLM alternates answer/judge for each test case
        _llmClientMock
            .SetupSequence(c => c.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Paris")   // test 1 answer
            .ReturnsAsync("9")       // test 1 judge
            .ReturnsAsync("Berlin")  // test 2 answer
            .ReturnsAsync("9");      // test 2 judge

        _embeddingsMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DummyVec);

        _calculatorMock
            .Setup(c => c.CalculateCosineSimilarity(It.IsAny<float[]>(), It.IsAny<float[]>()))
            .Returns(0.92);

        var runner = BuildRunner(runsPerTest: 1, minPassRun: 1);
        var cases  = new List<TestCase> { MakeTestCase("t1"), MakeTestCase("t2") };

        // Act
        var results = await runner.RunAllAsync(cases);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("t1", results[0].TestId);
        Assert.Equal("t2", results[1].TestId);
    }

    // =========================================================================
    // Pass / Fail — embedding validator path
    // =========================================================================

    /// <summary>
    /// When embedding similarity exceeds threshold, the test must pass.
    /// </summary>
    [Fact]
    public async Task RunAllAsync_EmbeddingSimilarityAboveThreshold_TestPasses()
    {
        // Arrange — high similarity, judge score irrelevant (OR logic)
        SetupPassingRun(similarity: 0.92, judgeResponse: "5");
        var runner = BuildRunner(runsPerTest: 1, minPassRun: 1);

        // Act
        var results = await runner.RunAllAsync(new List<TestCase> { MakeTestCase() });

        // Assert
        Assert.True(results[0].Passed);
    }

    /// <summary>
    /// When both validators fail, the test must fail.
    /// </summary>
    [Fact]
    public async Task RunAllAsync_BothValidatorsFail_TestFails()
    {
        // Arrange — low similarity AND low judge score
        SetupPassingRun(similarity: 0.60, judgeResponse: "3");
        var runner = BuildRunner(runsPerTest: 1, minPassRun: 1);

        // Act
        var results = await runner.RunAllAsync(new List<TestCase> { MakeTestCase() });

        // Assert
        Assert.False(results[0].Passed);
    }

    // =========================================================================
    // Majority Vote (requires runsPerTest: 3, minPassRun: 2)
    // =========================================================================

    /// <summary>
    /// 2 out of 3 runs passing must result in overall Passed = true (majority vote).
    /// </summary>
    [Fact]
    public async Task RunAllAsync_TwoOfThreeRunsPass_TestPasses()
    {
        // Arrange — runs: pass, pass, fail
        // Each run = 1 LLM answer + 1 judge response → 6 calls total for 3 runs
        _llmClientMock
            .SetupSequence(c => c.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Paris") .ReturnsAsync("9")  // run 1: pass
            .ReturnsAsync("Paris") .ReturnsAsync("9")  // run 2: pass
            .ReturnsAsync("Wrong") .ReturnsAsync("2"); // run 3: fail

        _embeddingsMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DummyVec);

        // Similarity: high for first two calls, low for the third
        _calculatorMock
            .SetupSequence(c => c.CalculateCosineSimilarity(It.IsAny<float[]>(), It.IsAny<float[]>()))
            .Returns(0.92)   // run 1
            .Returns(0.92)   // run 2
            .Returns(0.50);  // run 3

        var runner = BuildRunner(runsPerTest: 3, minPassRun: 2);

        // Act
        var results = await runner.RunAllAsync(new List<TestCase> { MakeTestCase() });

        // Assert
        Assert.True(results[0].Passed);
        Assert.Equal(2, results[0].PassedRunsCount);
        Assert.Equal(3, results[0].TotalRunsCount);
    }

    /// <summary>
    /// Only 1 out of 3 runs passing must result in overall Passed = false.
    /// </summary>
    [Fact]
    public async Task RunAllAsync_OneOfThreeRunsPass_TestFails()
    {
        // Arrange — runs: pass, fail, fail
        _llmClientMock
            .SetupSequence(c => c.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Paris") .ReturnsAsync("9")  // run 1: pass
            .ReturnsAsync("Wrong") .ReturnsAsync("2")  // run 2: fail
            .ReturnsAsync("Wrong") .ReturnsAsync("2"); // run 3: fail

        _embeddingsMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DummyVec);

        _calculatorMock
            .SetupSequence(c => c.CalculateCosineSimilarity(It.IsAny<float[]>(), It.IsAny<float[]>()))
            .Returns(0.92)
            .Returns(0.50)
            .Returns(0.50);

        var runner = BuildRunner(runsPerTest: 3, minPassRun: 2);

        // Act
        var results = await runner.RunAllAsync(new List<TestCase> { MakeTestCase() });

        // Assert
        Assert.False(results[0].Passed);
        Assert.Equal(1, results[0].PassedRunsCount);
    }

    // =========================================================================
    // Aggregated score fields
    // =========================================================================

    /// <summary>
    /// AverageEmbeddingScore and AverageJudgeScore must be correctly computed
    /// across all runs.
    /// </summary>
    [Fact]
    public async Task RunAllAsync_AverageScores_CalculatedCorrectly()
    {
        // Arrange — single run, known scores
        SetupPassingRun(similarity: 0.90, judgeResponse: "8");
        var runner = BuildRunner(runsPerTest: 1, minPassRun: 1);

        // Act
        var results = await runner.RunAllAsync(new List<TestCase> { MakeTestCase() });

        // Assert
        Assert.Equal(0.90, results[0].AverageEmbeddingScore, precision: 2);
        Assert.Equal(8.0,  results[0].AverageJudgeScore,     precision: 1);
    }

    // =========================================================================
    // Error handling — API failures during a run
    // =========================================================================

    /// <summary>
    /// If the LLM throws on every run, all runs are counted as failed
    /// and the test must fail — without crashing the test suite.
    /// </summary>
    [Fact]
    public async Task RunAllAsync_LlmThrowsEveryRun_TestFailsGracefully()
    {
        // Arrange
        _llmClientMock
            .Setup(c => c.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("API unavailable"));

        var runner = BuildRunner(runsPerTest: 1, minPassRun: 1);

        // Act
        var results = await runner.RunAllAsync(new List<TestCase> { MakeTestCase() });

        // Assert — must not throw; result must exist and be failed
        Assert.Single(results);
        Assert.False(results[0].Passed);
        Assert.Equal(0, results[0].PassedRunsCount);
        Assert.Equal(1, results[0].TotalRunsCount);
    }

    /// <summary>
    /// A failed run (API error) must be recorded in Runs list with an error response,
    /// and the other successful runs must still be counted correctly.
    /// </summary>
    [Fact]
    public async Task RunAllAsync_OneRunFails_OtherRunsStillCounted()
    {
        // Arrange — run 1 throws, run 2 succeeds, run 3 succeeds → 2/3 pass
        _llmClientMock
            .SetupSequence(c => c.SendPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Timeout")) // run 1 fails
            .ReturnsAsync("Paris").ReturnsAsync("9")          // run 2 passes
            .ReturnsAsync("Paris").ReturnsAsync("9");         // run 3 passes

        _embeddingsMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DummyVec);

        _calculatorMock
            .Setup(c => c.CalculateCosineSimilarity(It.IsAny<float[]>(), It.IsAny<float[]>()))
            .Returns(0.92);

        var runner = BuildRunner(runsPerTest: 3, minPassRun: 2);

        // Act
        var results = await runner.RunAllAsync(new List<TestCase> { MakeTestCase() });

        // Assert
        Assert.True(results[0].Passed);       // 2/3 majority
        Assert.Equal(3, results[0].TotalRunsCount);
        Assert.Equal(2, results[0].PassedRunsCount);
        Assert.Contains("ERROR", results[0].Runs[0].Response); // first run logged error
    }
}