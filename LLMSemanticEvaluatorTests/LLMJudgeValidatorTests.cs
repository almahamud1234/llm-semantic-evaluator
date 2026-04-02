using LLMSemanticEvaluator;
using LLMSemanticEvaluator.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LLMSemanticEvaluatorTests;

/// <summary>
/// Unit tests for <see cref="LLMJudgeValidator"/>.
///
/// <para>
/// <see cref="LLMJudgeValidator"/> uses a second LLM (the "judge") to score how
/// well the tested LLM answered a question. The judge responds in free-form text,
/// so the validator must extract a numeric score using regex. Score parsing is the
/// most fragile and business-critical logic in this class — a misread "9" as "0"
/// falsely rejects a correct answer; accepting "11" corrupts threshold logic.
/// </para>
///
/// <para>
/// <strong>Why FakeChatClient instead of Mock&lt;IChatClient&gt;:</strong><br/>
/// <c>GetResponseAsync</c> in some versions of <c>Microsoft.Extensions.AI</c> is
/// defined directly on the interface, but Moq requires the type to be mockable
/// (virtual or interface). Using a hand-written <see cref="FakeChatClient"/> that
/// implements every interface member explicitly is the safest, version-independent
/// approach — it compiles against whatever interface shape is installed.
/// </para>
///
/// <para>Run with: <c>dotnet test --filter TestCategory!=Integration</c></para>
/// </summary>
[TestClass]
public class LLMJudgeValidatorTests
{
    // =========================================================================
    // FakeChatClient — lightweight test double for IChatClient
    // =========================================================================

    /// <summary>
    /// Minimal in-memory implementation of <see cref="IChatClient"/>.
    ///
    /// <para>
    /// Responses are queued in the constructor and dequeued in order.
    /// Each call to <c>GetResponseAsync</c> returns the next queued string wrapped
    /// in a <see cref="ChatResponse"/>. Passing an <see cref="Exception"/> in the
    /// queue causes that call to throw, simulating infrastructure failures.
    /// </para>
    /// <para>
    /// If the queue is exhausted unexpectedly, <see cref="InvalidOperationException"/>
    /// is thrown immediately so the test fails with a clear "too many calls" message.
    /// </para>
    /// </summary>
    private sealed class FakeChatClient : IChatClient
    {
        private readonly Queue<object> _responses; // string or Exception

        /// <summary>
        /// Pass strings for successful responses, or <see cref="Exception"/> instances
        /// for positions that should simulate an API failure.
        /// Pass nothing to create a client whose first call will throw —
        /// used to assert a code path does NOT call the API.
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

    /// <summary>
    /// Fake client that always throws a given exception — used to verify that the
    /// validator catches infrastructure failures gracefully instead of crashing.
    /// </summary>
    private sealed class ThrowingChatClient(Exception exception) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions?             options           = null,
            CancellationToken        cancellationToken = default)
            => throw exception;

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions?             options           = null,
            CancellationToken        cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? key = null) => null;
        public void Dispose() { }
    }

    // =========================================================================
    // Builder helper
    // =========================================================================

    /// <summary>
    /// Builds the validator under test with the given client and threshold.
    /// A factory method keeps each test's Arrange section focused on what varies.
    /// </summary>
    private static LLMJudgeValidator Build(IChatClient client, int threshold = 8)
        => new(
            client,
            Options.Create(new TestConfiguration
            {
                JudgeThreshold = threshold,
                ChatModel      = "gpt-4o-mini",
                Temperature    = 0.0
            }),
            NullLogger<LLMJudgeValidator>.Instance);

    // =========================================================================
    // Happy Path — Pass / Fail based on score vs threshold
    // =========================================================================

    /// <summary>
    /// A judge score above the threshold means the LLM response is deemed correct.
    /// <see cref="ValidationResult.Passed"/> must be true and the exact score stored
    /// so the TestRunner can include it in the final report.
    /// </summary>
    [TestMethod]
    public async Task ValidateAsync_ScoreAboveThreshold_ReturnsPassed()
    {
        var validator = Build(new FakeChatClient("SCORE: 9"), threshold: 8);

        var result = await validator.ValidateAsync("Q", "Expected", "Actual");

        // Score 9 ≥ threshold 8 → the response quality bar is met.
        Assert.IsTrue(result.Passed,
            "A judge score above the threshold must produce a passing verdict.");
        Assert.AreEqual(9, (int)result.Score,
            "The parsed score must be stored in the result for the report.");
        Assert.AreEqual("LLMJudge", result.ValidatorName,
            "ValidatorName identifies which validator produced this result in the report.");
    }

    /// <summary>
    /// A judge score below the threshold means the response does not meet the quality
    /// bar. The TestRunner will count this run as failed.
    /// </summary>
    [TestMethod]
    public async Task ValidateAsync_ScoreBelowThreshold_ReturnsFailed()
    {
        var validator = Build(new FakeChatClient("SCORE: 5"), threshold: 8);

        var result = await validator.ValidateAsync("Q", "Expected", "Actual");

        // Score 5 < threshold 8 → the LLM answer does not meet the quality bar.
        Assert.IsFalse(result.Passed,
            "A judge score below the threshold must produce a failing verdict.");
        Assert.AreEqual(5, (int)result.Score,
            "The score must still be stored so the report can show how close it was.");
    }

    /// <summary>
    /// The threshold is an inclusive lower bound: a score exactly equal to it must pass.
    /// An off-by-one error here causes responses that just meet the quality bar
    /// to appear as failures, skewing pass-rate statistics.
    /// </summary>
    [TestMethod]
    public async Task ValidateAsync_ScoreExactlyAtThreshold_ReturnsPassed()
    {
        var validator = Build(new FakeChatClient("SCORE: 8"), threshold: 8);

        var result = await validator.ValidateAsync("Q", "Expected", "Actual");

        // Inclusive boundary: score == threshold must pass, not fail.
        Assert.IsTrue(result.Passed,
            "A score exactly equal to the threshold must pass (inclusive lower bound).");
        Assert.AreEqual(8, (int)result.Score,
            "The boundary score must be stored correctly.");
    }

    // =========================================================================
    // Score Parsing — the most critical logic unique to this class
    // =========================================================================

    /// <summary>
    /// Judges write responses in many styles. The validator must extract the correct
    /// integer from each format because prompt engineering cannot fully control a real
    /// LLM's output style. Failing any case silently misclassifies LLM responses.
    /// </summary>
    [TestMethod]
    [DataRow("SCORE: 9",             9,  "Primary format: labelled score on its own line")]
    [DataRow("9",                    9,  "Minimal format: bare number")]
    [DataRow("Score: 9",             9,  "Label with different capitalisation")]
    [DataRow("I'd give it 8/10",     8,  "Natural language with fractional notation")]
    [DataRow("9.",                   9,  "Trailing full stop after the digit")]
    [DataRow("  10  ",              10,  "Leading and trailing whitespace")]
    [DataRow("Rating: 7 out of 10",  7,  "Verbose format — first valid 1–10 match wins")]
    public async Task ValidateAsync_VariousJudgeResponseFormats_ParsesScoreCorrectly(
        string judgeResponse, int expectedScore, string scenario)
    {
        // Threshold 1 isolates score parsing from the pass/fail decision.
        var validator = Build(new FakeChatClient(judgeResponse), threshold: 1);

        var result = await validator.ValidateAsync("Q", "Expected", "Actual");

        Assert.AreEqual(expectedScore, (int)result.Score,
            $"Scenario '{scenario}': expected score {expectedScore} from '{judgeResponse}'.");
    }

    /// <summary>
    /// When no valid 1–10 integer appears in the judge's response the validator must
    /// return Score = 0 and Passed = false. Returning a non-zero score would mean a
    /// hallucinated response was accepted as a passing evaluation.
    /// </summary>
    [TestMethod]
    [DataRow("Great answer!",  "No number at all")]
    [DataRow("11",             "Out of valid range (above 10)")]
    [DataRow("0",              "Out of valid range (below 1)")]
    [DataRow("zero",           "Written-out number, not a digit")]
    public async Task ValidateAsync_UnparsableJudgeResponse_ReturnsFailed(
        string judgeResponse, string scenario)
    {
        var validator = Build(new FakeChatClient(judgeResponse));

        var result = await validator.ValidateAsync("Q", "Expected", "Actual");

        Assert.IsFalse(result.Passed,
            $"Scenario '{scenario}': unparseable response must produce a failing verdict.");
        Assert.AreEqual(0, (int)result.Score,
            $"Scenario '{scenario}': score must be 0 when parsing fails.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Reasoning),
            $"Scenario '{scenario}': Reasoning must explain why parsing failed.");
    }

    // =========================================================================
    // Empty / Null Actual Response — short-circuit before calling the judge
    // =========================================================================

    /// <summary>
    /// An empty, whitespace-only, or null actual response means the LLM produced no
    /// output. The validator must fail immediately without calling the judge LLM —
    /// calling it would waste tokens while returning a meaningless score.
    /// The empty FakeChatClient queue ensures any unexpected API call throws instantly.
    /// </summary>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    public async Task ValidateAsync_EmptyOrNullActual_ReturnsFailedWithoutCallingApi(string? actual)
    {
        // Empty queue — any call to GetResponseAsync would throw InvalidOperationException,
        // surfacing the unexpected call as a clear test failure.
        var validator = Build(new FakeChatClient());

        var result = await validator.ValidateAsync("Q", "Expected", actual!);

        Assert.IsFalse(result.Passed,
            "An empty LLM response has nothing to judge — must fail immediately.");
        Assert.AreEqual(0, (int)result.Score,
            "Score must be 0 when the LLM produced no response.");
    }

    // =========================================================================
    // API / Infrastructure Failures — graceful degradation
    // =========================================================================

    /// <summary>
    /// If the judge LLM returns an empty string the validator must fail safely with
    /// a descriptive Reasoning message rather than propagating a null-reference error.
    /// </summary>
    [TestMethod]
    public async Task ValidateAsync_JudgeReturnsEmptyString_ReturnsFailed()
    {
        var validator = Build(new FakeChatClient(string.Empty));

        var result = await validator.ValidateAsync("Q", "Expected", "Actual");

        Assert.IsFalse(result.Passed,
            "An empty judge response means scoring failed — must produce a failing verdict.");
        Assert.AreEqual(0, (int)result.Score,
            "Score must be 0 when the judge produced no usable output.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Reasoning),
            "Reasoning must explain the empty response so operators can investigate.");
    }

    /// <summary>
    /// If the LLM client throws (network error, rate-limit, bad API key) the validator
    /// must catch the exception and return a failed result — not re-throw.
    /// Re-throwing would propagate to TestRunner and abort the entire test run,
    /// discarding all results collected so far.
    /// </summary>
    [TestMethod]
    public async Task ValidateAsync_LlmClientThrows_ReturnsFailedWithErrorReasoning()
    {
        var validator = Build(
            new ThrowingChatClient(new HttpRequestException("Rate limit exceeded")));

        // Must NOT throw — exceptions inside a run must be caught by the validator.
        var result = await validator.ValidateAsync("Q", "Expected", "Actual");

        Assert.IsFalse(result.Passed,
            "A judge API exception must produce a failing result, not crash the evaluation.");
        Assert.AreEqual(0, (int)result.Score,
            "Score must be 0 when the judge could not be reached.");
        Assert.IsTrue((result.Reasoning ?? string.Empty).Contains("Rate limit exceeded"),
            "The error message must appear in Reasoning so operators can diagnose the outage.");
    }
}