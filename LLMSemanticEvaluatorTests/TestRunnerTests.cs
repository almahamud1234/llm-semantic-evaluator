using LLMSemanticEvaluator;
using LLMSemanticEvaluator.Configuration;
using LLMSemanticEvaluator.Interfaces;
using LLMSemanticEvaluator.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace LLMSemanticEvaluatorTests;

/// <summary>
/// Unit tests for <see cref="TestRunner"/>.
///
/// <para>
/// <see cref="TestRunner"/> orchestrates the full evaluation pipeline: calls the LLM,
/// invokes both validators, applies majority-vote logic, and aggregates scores.
/// A bug here could silently pass failing LLMs, fail passing ones, or miscount runs —
/// invalidating every metric in the report.
/// </para>
///
/// <para>
/// <strong>Why FakeChatClient instead of Mock&lt;IChatClient&gt;:</strong><br/>
/// Moq requires the mocked member to be virtual or a true interface method.
/// In this version of <c>Microsoft.Extensions.AI</c>, the interface methods are
/// <c>GetResponseAsync</c>, <c>GetStreamingResponseAsync</c>, and
/// <c>GetService(Type, object?)</c>. The hand-written <see cref="FakeChatClient"/>
/// implements exactly these members, so it compiles and works regardless of which
/// minor version of the package is installed.
/// </para>
///
/// <para>
/// <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/> and
/// <see cref="ISimilarityCalculator"/> are still mocked with Moq because their
/// methods are straightforward interface methods with no extension-method complications.
/// </para>
///
/// <para>Run with: <c>dotnet test --filter TestCategory!=Integration</c></para>
/// </summary>
[TestClass]
public class TestRunnerTests
{
    // =========================================================================
    // FakeChatClient — lightweight test double for IChatClient
    // =========================================================================

    /// <summary>
    /// In-memory <see cref="IChatClient"/> that returns pre-staged responses from a queue.
    ///
    /// <para>
    /// In <see cref="TestRunner"/> the chat client is called twice per run:
    /// once for the prompt (returns the LLM's answer) and once for the judge prompt
    /// (returns the judge's score string). The queue handles both calls — just enqueue
    /// the answer first, then the judge response.
    /// </para>
    ///
    /// <para>
    /// Pass an <see cref="Exception"/> instance in the queue to simulate an API failure
    /// at that call position. If the queue empties unexpectedly,
    /// <see cref="InvalidOperationException"/> is thrown immediately so the test fails
    /// with a clear "too many calls" message rather than a silent null result.
    /// </para>
    /// </summary>
    private sealed class FakeChatClient : IChatClient
    {
        private readonly Queue<object> _responses; // string or Exception

        /// <summary>
        /// Pass strings for successful responses and/or <see cref="Exception"/> instances
        /// for positions that should simulate an API failure.
        /// </summary>
        public FakeChatClient(params object[] responses)
            => _responses = new Queue<object>(responses);

        /// <inheritdoc />
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions?             options           = null,
            CancellationToken        cancellationToken = default)
        {
            if (_responses.Count == 0)
                throw new InvalidOperationException(
                    "FakeChatClient: no more queued responses. " +
                    "Add more items to the constructor for additional expected calls.");

            object next = _responses.Dequeue();

            if (next is Exception ex)
                throw ex;

            var message  = new ChatMessage(ChatRole.Assistant, (string)next);
            var response = new ChatResponse(message);
            return Task.FromResult(response);
        }

        /// <inheritdoc />
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions?             options           = null,
            CancellationToken        cancellationToken = default)
            => throw new NotSupportedException("Streaming is not used in unit tests.");

        /// <inheritdoc />
        public object? GetService(Type serviceType, object? key = null) => null;

        /// <inheritdoc />
        public void Dispose() { }
    }

    // =========================================================================
    // Shared mocks for embedding dependencies
    // =========================================================================

    /// <summary>
    /// Mock for IEmbeddingGenerator — its methods are real interface methods,
    /// so Moq handles them correctly.
    /// </summary>
    private readonly Mock<IEmbeddingGenerator<string, Embedding<float>>> _embeddingsMock = new();

    /// <summary>Mock for ISimilarityCalculator — controls cosine similarity scores.</summary>
    private readonly Mock<ISimilarityCalculator> _calculatorMock = new();

    /// <summary>
    /// Dummy embedding vector — content is irrelevant because the calculator is mocked.
    /// Non-empty to avoid the "empty vector" short-circuit inside EmbeddingValidator.
    /// </summary>
    private static readonly float[] DummyVec = { 0.1f, 0.2f, 0.3f };

    // =========================================================================
    // Builder helpers
    // =========================================================================

    /// <summary>
    /// Builds <see cref="IOptions{TestConfiguration}"/> with the given run settings.
    /// <c>RequestDelayMs = 0</c> so unit tests run without artificial sleeps.
    /// </summary>
    private static IOptions<TestConfiguration> BuildOptions(
        int numberOfRuns = 1, int minPassRun = 1)
        => Options.Create(new TestConfiguration
        {
            NumberOfRuns       = numberOfRuns,
            MinimumPassingRuns = minPassRun,
            EmbeddingThreshold = 0.85,
            JudgeThreshold     = 8,
            ChatModel          = "gpt-4o-mini",
            Temperature        = 0.0,
            RequestDelayMs     = 0
        });

    /// <summary>
    /// Builds a <see cref="TestRunner"/> wired to the given fake chat client and
    /// the shared embedding mocks.
    /// </summary>
    private TestRunner BuildRunner(
        FakeChatClient fakeClient,
        int            numberOfRuns = 1,
        int            minPassRun   = 1)
    {
        var options = BuildOptions(numberOfRuns, minPassRun);

        var embeddingValidator = new EmbeddingValidator(
            _embeddingsMock.Object,
            _calculatorMock.Object,
            options,
            NullLogger<EmbeddingValidator>.Instance);

        var judgeValidator = new LLMJudgeValidator(
            fakeClient,
            options,
            NullLogger<LLMJudgeValidator>.Instance);

        return new TestRunner(
            fakeClient,
            embeddingValidator,
            judgeValidator,
            options,
            NullLogger<TestRunner>.Instance);
    }

    /// <summary>Returns a minimal valid <see cref="TestCase"/>.</summary>
    private static TestCase MakeTestCase(string id = "t1", string category = "factual") => new()
    {
        Id             = id,
        Category       = category,
        Prompt         = "What is the capital of France?",
        ExpectedOutput = "Paris"
    };

    /// <summary>
    /// Sets up the embedding mocks so every input returns <see cref="DummyVec"/>
    /// and the calculator always returns <paramref name="similarity"/>.
    /// </summary>
    private void SetupEmbeddings(double similarity)
    {
        var embedding = new Embedding<float>(DummyVec);
        var generated = new GeneratedEmbeddings<Embedding<float>>([embedding]);

        _embeddingsMock
            .Setup(e => e.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(generated);

        _calculatorMock
            .Setup(c => c.CalculateCosineSimilarity(
                It.IsAny<float[]>(), It.IsAny<float[]>()))
            .Returns(similarity);
    }

    /// <summary>
    /// Sets up the embedding mocks to return different similarity scores on successive
    /// calls — used for majority-vote tests where some runs pass and some fail.
    /// </summary>
    private void SetupEmbeddingsSequence(params double[] similarities)
    {
        var embedding = new Embedding<float>(DummyVec);
        var generated = new GeneratedEmbeddings<Embedding<float>>([embedding]);

        _embeddingsMock
            .Setup(e => e.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(generated);

        var seq = _calculatorMock.SetupSequence(
            c => c.CalculateCosineSimilarity(
                It.IsAny<float[]>(), It.IsAny<float[]>()));

        foreach (double s in similarities)
            seq.Returns(s);
    }

    // =========================================================================
    // RunAllAsync — result structure
    // =========================================================================

    /// <summary>
    /// <see cref="TestRunner.RunAllAsync"/> must return exactly one
    /// <see cref="TestResult"/> per <see cref="TestCase"/> input.
    /// A mismatch means tests were silently dropped or duplicated, corrupting the
    /// pass-rate percentage in the report.
    /// </summary>
    [TestMethod]
    public async Task RunAllAsync_SingleTestCase_ReturnsOneResult()
    {
        SetupEmbeddings(0.92);
        var runner = BuildRunner(new FakeChatClient("Paris", "SCORE: 9"));

        var results = await runner.RunAllAsync(new List<TestCase> { MakeTestCase() });

        // One input test case must produce exactly one output result.
        Assert.AreEqual(1, results.Count,
            "RunAllAsync must return exactly one TestResult per TestCase input.");
    }

    /// <summary>
    /// Each <see cref="TestResult"/> must be populated with the metadata from its
    /// corresponding <see cref="TestCase"/>. If ID or Category are wrong, every
    /// report entry and log line references the wrong test.
    /// </summary>
    [TestMethod]
    public async Task RunAllAsync_ResultFields_MappedFromTestCase()
    {
        SetupEmbeddings(0.92);
        var testCase = MakeTestCase(id: "factual_001", category: "factual");
        var runner   = BuildRunner(new FakeChatClient("Paris", "SCORE: 9"));

        var results = await runner.RunAllAsync(new List<TestCase> { testCase });

        // Metadata must be copied verbatim from the input TestCase.
        Assert.AreEqual("factual_001",           results[0].TestId,
            "TestId must be copied from TestCase.Id.");
        Assert.AreEqual("factual",               results[0].Category,
            "Category must be copied from TestCase.Category.");
        Assert.AreEqual(testCase.Prompt,         results[0].Prompt,
            "Prompt must be copied from TestCase.Prompt.");
        Assert.AreEqual(testCase.ExpectedOutput, results[0].ExpectedOutput,
            "ExpectedOutput must be copied from TestCase.ExpectedOutput.");
    }

    /// <summary>
    /// Multiple test cases must each produce a result in the correct order.
    /// Order matters because the report lists tests sequentially and operators
    /// cross-reference log output with the report.
    /// </summary>
    [TestMethod]
    public async Task RunAllAsync_MultipleTestCases_ReturnsResultForEach()
    {
        // Two test cases × (1 LLM answer + 1 judge score) = 4 queued strings
        SetupEmbeddings(0.92);
        var runner = BuildRunner(new FakeChatClient(
            "Paris",  "SCORE: 9",   // test case 1
            "Berlin", "SCORE: 9")); // test case 2

        var results = await runner.RunAllAsync(
            new List<TestCase> { MakeTestCase("t1"), MakeTestCase("t2") });

        Assert.AreEqual(2, results.Count,
            "Two test cases must produce two results.");
        Assert.AreEqual("t1", results[0].TestId,
            "First result must correspond to the first test case.");
        Assert.AreEqual("t2", results[1].TestId,
            "Second result must correspond to the second test case.");
    }

    // =========================================================================
    // Pass / Fail — OR logic between the two validators
    // =========================================================================

    /// <summary>
    /// When embedding similarity exceeds the threshold the run passes regardless of
    /// the judge score (OR logic). This is critical: embedding similarity is unreliable
    /// for short expected outputs, so either validator passing is sufficient.
    /// </summary>
    [TestMethod]
    public async Task RunAllAsync_EmbeddingSimilarityAboveThreshold_TestPasses()
    {
        // High similarity (passes embedding), low judge score (would alone fail)
        SetupEmbeddings(0.92);
        var runner = BuildRunner(new FakeChatClient("Paris", "SCORE: 5"));

        var results = await runner.RunAllAsync(new List<TestCase> { MakeTestCase() });

        // Embedding pass triggers the OR — test must pass despite low judge score.
        Assert.IsTrue(results[0].Passed,
            "High embedding similarity must pass the run even if judge score is below threshold.");
    }

    /// <summary>
    /// When both validators fail, the run must fail. Verifies the OR logic does not
    /// become AND — if neither validator passes there is no safety net.
    /// </summary>
    [TestMethod]
    public async Task RunAllAsync_BothValidatorsFail_TestFails()
    {
        SetupEmbeddings(0.60);
        var runner = BuildRunner(new FakeChatClient("Wrong", "SCORE: 3"));

        var results = await runner.RunAllAsync(new List<TestCase> { MakeTestCase() });

        // Both validators failed — OR produces false → test fails.
        Assert.IsFalse(results[0].Passed,
            "When both embedding and judge validators fail, the test must fail.");
    }

    // =========================================================================
    // Majority Vote — requires 3 runs, minPassRun = 2
    // =========================================================================

    /// <summary>
    /// With 3 runs and a 2-of-3 threshold, 2 passing runs must produce
    /// <c>Passed = true</c>. This is the standard majority-vote configuration for
    /// handling LLM non-determinism — one bad run is tolerated.
    /// </summary>
    [TestMethod]
    public async Task RunAllAsync_TwoOfThreeRunsPass_TestPasses()
    {
        // Runs: pass, pass, fail
        // Each run = 1 LLM answer + 1 judge call → 6 queued strings for 3 runs
        SetupEmbeddingsSequence(0.92, 0.92, 0.50);

        var runner = BuildRunner(new FakeChatClient(
            "Paris", "SCORE: 9",   // run 1: pass
            "Paris", "SCORE: 9",   // run 2: pass
            "Wrong", "SCORE: 2"),  // run 3: fail
            numberOfRuns: 3, minPassRun: 2);

        var results = await runner.RunAllAsync(new List<TestCase> { MakeTestCase() });

        // 2 of 3 runs passed → majority vote → overall pass.
        Assert.IsTrue(results[0].Passed,
            "2 of 3 runs passing must satisfy the majority-vote threshold of 2.");
        Assert.AreEqual(2, results[0].PassedRunsCount,
            "PassedRunsCount must reflect exactly 2 passing runs.");
        Assert.AreEqual(3, results[0].TotalRunsCount,
            "TotalRunsCount must reflect all 3 runs regardless of outcome.");
    }

    /// <summary>
    /// Only 1 of 3 runs passing is insufficient. Verifies the majority-vote threshold
    /// is correctly enforced — a single lucky pass must not elevate an unreliable LLM.
    /// </summary>
    [TestMethod]
    public async Task RunAllAsync_OneOfThreeRunsPass_TestFails()
    {
        SetupEmbeddingsSequence(0.92, 0.50, 0.50);

        var runner = BuildRunner(new FakeChatClient(
            "Paris", "SCORE: 9",   // run 1: pass
            "Wrong", "SCORE: 2",   // run 2: fail
            "Wrong", "SCORE: 2"),  // run 3: fail
            numberOfRuns: 3, minPassRun: 2);

        var results = await runner.RunAllAsync(new List<TestCase> { MakeTestCase() });

        // Only 1 of 3 passed — below threshold of 2 → overall fail.
        Assert.IsFalse(results[0].Passed,
            "Only 1 of 3 runs passing must not reach the majority-vote threshold of 2.");
        Assert.AreEqual(1, results[0].PassedRunsCount,
            "PassedRunsCount must reflect exactly 1 passing run.");
    }

    // =========================================================================
    // Aggregated score fields
    // =========================================================================

    /// <summary>
    /// Average embedding and judge scores must be computed across all runs.
    /// These averages appear in the report to show how close the LLM was to passing
    /// even when the test failed. Wrong averages misrepresent LLM performance.
    /// </summary>
    [TestMethod]
    public async Task RunAllAsync_AverageScores_CalculatedCorrectly()
    {
        SetupEmbeddings(0.90);
        var runner = BuildRunner(new FakeChatClient("Paris", "SCORE: 8"));

        var results = await runner.RunAllAsync(new List<TestCase> { MakeTestCase() });

        // With one run, average == that run's score.
        Assert.AreEqual(0.90, results[0].AverageEmbeddingScore, delta: 0.01,
            "AverageEmbeddingScore must equal the similarity score from the single run.");
        Assert.AreEqual(8.0, results[0].AverageJudgeScore, delta: 0.1,
            "AverageJudgeScore must equal the judge score from the single run.");
    }

    // =========================================================================
    // Error handling — API failures during a run
    // =========================================================================

    /// <summary>
    /// If the LLM API throws on every run, all runs are marked failed and the result
    /// must be returned without throwing. An unhandled exception here would crash the
    /// entire evaluation, discarding all results collected so far.
    /// </summary>
    [TestMethod]
    public async Task RunAllAsync_LlmThrowsEveryRun_TestFailsGracefully()
    {
        // Queue a single exception — no embedding setup needed (exception happens first)
        var runner = BuildRunner(
            new FakeChatClient(new HttpRequestException("API unavailable")));

        // Must NOT throw — exceptions inside a run must be caught by TestRunner.
        var results = await runner.RunAllAsync(new List<TestCase> { MakeTestCase() });

        Assert.AreEqual(1, results.Count,
            "A result must be returned even when the LLM API threw on every run.");
        Assert.IsFalse(results[0].Passed,
            "All runs failing must produce an overall failing result.");
        Assert.AreEqual(0, results[0].PassedRunsCount,
            "PassedRunsCount must be 0 when every run encountered an API error.");
        Assert.AreEqual(1, results[0].TotalRunsCount,
            "TotalRunsCount must still reflect the attempted number of runs.");
    }

    /// <summary>
    /// When one run fails (API error) but the others succeed, the error is recorded
    /// in that run's Response and the passing runs still count towards the majority vote.
    /// A single transient error must not corrupt the surrounding successful runs.
    /// </summary>
    [TestMethod]
    public async Task RunAllAsync_OneRunFails_OtherRunsStillCounted()
    {
        // Run 1: throws. Runs 2 and 3: succeed → 2/3 pass (majority).
        SetupEmbeddings(0.92);

        var runner = BuildRunner(new FakeChatClient(
            new HttpRequestException("Timeout"),  // run 1: API error
            "Paris", "SCORE: 9",                  // run 2: pass
            "Paris", "SCORE: 9"),                 // run 3: pass
            numberOfRuns: 3, minPassRun: 2);

        var results = await runner.RunAllAsync(new List<TestCase> { MakeTestCase() });

        // 2 of 3 runs passed despite run 1 throwing — majority vote must hold.
        Assert.IsTrue(results[0].Passed,
            "2 of 3 runs passing must satisfy the majority vote even when one run errored.");
        Assert.AreEqual(3, results[0].TotalRunsCount,
            "TotalRunsCount must include the failed run — it was still attempted.");
        Assert.AreEqual(2, results[0].PassedRunsCount,
            "PassedRunsCount must reflect only the 2 runs that actually passed.");
        Assert.IsTrue(results[0].Runs[0].Response.Contains("ERROR"),
            "The failed run's Response must contain 'ERROR' so the report shows which run errored.");
    }
}