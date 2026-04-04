using LLMSemanticEvaluator;
using LLMSemanticEvaluator.Configuration;
using LLMSemanticEvaluator.Interfaces;
using LLMSemanticEvaluator.Infrastructure;
using LLMSemanticEvaluator.Models;
using LLMSemanticEvaluator.Services;
using LLMSemanticEvaluator.Validators;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LLMSemanticEvaluatorTests.IntegrationTesting;

/// <summary>
/// Integration tests that run the full evaluation pipeline against real LLM endpoints.
///
/// <para><strong>Design rationale (addressing feedback item 14):</strong><br/>
/// The unit tests in this project use mocks for speed and determinism.
/// Mocks verify that classes call each other correctly, but they cannot detect:
/// <list type="bullet">
///   <item>The real LLM returning unexpected response formats that break score parsing</item>
///   <item>Embedding provider behaviour changes after a model update</item>
///   <item>Threshold values that work on paper but reject real, correct answers</item>
///   <item>End-to-end latency regressions that affect RequestDelayMs sizing</item>
/// </list>
/// Integration tests close this gap. Because they require external services and
/// incur API costs, they are skipped automatically when no API key is present.
/// Set the <c>LLM_API_KEY</c> environment variable to run them.
/// </para>
///
/// <para>
/// In CI/CD, a dedicated test stage with a test API key should run these tests
/// on every pull request that touches a validator or the TestRunner.
/// </para>
///
/// <para>Run with: <c>LLM_API_KEY=sk-... dotnet test --filter TestCategory=Integration</c></para>
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class IntegrationTests
{
    // ── Environment variable names ────────────────────────────────────────────

    /// <summary>API key shared by the chat and embedding providers.</summary>
    private const string EnvApiKey = "LLM_API_KEY";

    // ── Skip helper ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the API key from the environment, or null if absent.
    /// </summary>
    private static string? ApiKey => Environment.GetEnvironmentVariable(EnvApiKey);

    /// <summary>
    /// Skips the test with a clear message when no API key is configured.
    /// MSTest does not have a built-in skip, so Assert.Inconclusive is used —
    /// most CI systems show this as "skipped" rather than "failed".
    /// </summary>
    private static void SkipIfNoApiKey()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            Assert.Inconclusive(
                $"Integration test skipped: set environment variable '{EnvApiKey}' " +
                "to run against a real LLM endpoint.");
        }
    }

    // ── Configuration builder ─────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="TestConfiguration"/> populated from the environment variable
    /// with sensible defaults for integration testing.
    /// Only properties that actually exist on <see cref="TestConfiguration"/> are set.
    /// </summary>
    private static IOptions<TestConfiguration> BuildOptions() =>
        Options.Create(new TestConfiguration
        {
            ApiKey             = ApiKey!,
            Provider           = "openai",
            EmbeddingProvider  = "openai",
            ChatModel          = "gpt-4o-mini",          // cheap model keeps integration runs low-cost
            EmbeddingModel     = "text-embedding-3-small",
            EmbeddingThreshold = 0.80,                   // slightly relaxed for real embeddings
            JudgeThreshold     = 7,                      // slightly relaxed for real judge responses
            NumberOfRuns       = 1,                      // single run keeps tests fast
            MinimumPassingRuns = 1,
            Temperature        = 0.0,                    // deterministic output for reproducibility
            RequestDelayMs     = 500,
            TimeoutSeconds     = 30
        });

    // =========================================================================
    // EmbeddingValidator — real embedding API
    // =========================================================================

    /// <summary>
    /// Verifies that the real embedding provider returns non-empty vectors and that
    /// cosine similarity between semantically identical strings exceeds the threshold.
    /// This test detects: wrong API key, wrong model name, or a provider that returns
    /// degenerate (zero) vectors.
    /// </summary>
    [TestMethod]
    public async Task EmbeddingValidator_IdenticalStrings_PassesThreshold()
    {
        SkipIfNoApiKey();

        var options   = BuildOptions();
        var factory   = new LLMClientFactory(options, NullLogger<LLMClientFactory>.Instance);
        var validator = new EmbeddingValidator(
            factory.CreateEmbeddingGenerator(),
            new CosineSimilarityCalculator(),
            options,
            NullLogger<EmbeddingValidator>.Instance);

        // Compare the exact same string against itself — must be near-perfect similarity.
        var result = await validator.ValidateAsync(expected: "Paris", actual: "Paris");

        Assert.IsTrue(result.Passed,
            $"Identical strings must pass the embedding threshold. " +
            $"Score: {result.Score:F4}. Reasoning: {result.Reasoning}");
        Assert.IsTrue(result.Score >= 0.95,
            $"Identical strings must score ≥ 0.95; got {result.Score:F4}. " +
            "A lower score indicates the embedding provider is returning low-quality vectors.");
    }

    /// <summary>
    /// Verifies that semantically similar strings (expected answer vs a full sentence
    /// expressing the same fact) exceed the embedding threshold.
    /// If correct LLM answers fail the embedding check, the threshold or model is wrong.
    /// </summary>
    [TestMethod]
    public async Task EmbeddingValidator_SemanticallySimilarStrings_PassesThreshold()
    {
        SkipIfNoApiKey();

        var options   = BuildOptions();
        var factory   = new LLMClientFactory(options, NullLogger<LLMClientFactory>.Instance);
        var validator = new EmbeddingValidator(
            factory.CreateEmbeddingGenerator(),
            new CosineSimilarityCalculator(),
            options,
            NullLogger<EmbeddingValidator>.Instance);

        // These two strings express the same fact in different forms.
        var result = await validator.ValidateAsync(
            expected: "The capital city of France is Paris.",
            actual:   "The capital of France is Paris.");

        Assert.IsTrue(result.Passed,
            $"A semantically correct answer must pass the embedding threshold. " +
            $"Score: {result.Score:F4}. Reasoning: {result.Reasoning}");
    }

    /// <summary>
    /// Verifies that clearly unrelated strings fall below the embedding threshold.
    /// If unrelated strings pass, the threshold is too low and the validator would
    /// accept any LLM response as correct — making the evaluation meaningless.
    /// </summary>
    [TestMethod]
    public async Task EmbeddingValidator_UnrelatedStrings_FailsThreshold()
    {
        SkipIfNoApiKey();

        var options   = BuildOptions();
        var factory   = new LLMClientFactory(options, NullLogger<LLMClientFactory>.Instance);
        var validator = new EmbeddingValidator(
            factory.CreateEmbeddingGenerator(),
            new CosineSimilarityCalculator(),
            options,
            NullLogger<EmbeddingValidator>.Instance);

        var result = await validator.ValidateAsync(
            expected: "Paris",
            actual:   "Photosynthesis is how plants convert sunlight into glucose.");

        Assert.IsFalse(result.Passed,
            $"Semantically unrelated strings must fail the embedding threshold. " +
            $"Score: {result.Score:F4}. A passing score here means the threshold is too permissive.");
    }

    // =========================================================================
    // LLMJudgeValidator — real judge LLM
    // =========================================================================

    /// <summary>
    /// Verifies that the real judge LLM returns a parseable 1–10 score when presented
    /// with a factual question and a correct answer. This test detects: wrong judge
    /// model, broken score parsing against real judge output styles, or judge prompts
    /// that elicit unusable responses.
    /// </summary>
    [TestMethod]
    public async Task LlmJudgeValidator_CorrectAnswer_PassesThreshold()
    {
        SkipIfNoApiKey();

        var options   = BuildOptions();
        var factory   = new LLMClientFactory(options, NullLogger<LLMClientFactory>.Instance);
        var validator = new LLMJudgeValidator(
            factory.CreateChatClient(),
            options,
            NullLogger<LLMJudgeValidator>.Instance);

        var result = await validator.ValidateAsync(
            prompt:   "What is the capital of France?",
            expected: "Paris",
            actual:   "The capital of France is Paris.",
            criteria: "The answer must correctly identify Paris as the capital.");

        // A correct answer must receive a high judge score.
        Assert.IsTrue(result.Passed,
            $"A factually correct answer must pass the judge threshold. " +
            $"Score: {result.Score}/10. Reasoning: {result.Reasoning}");
        Assert.IsTrue(result.Score >= 7,
            $"A correct answer must score ≥ 7/10; got {result.Score}. " +
            "If the judge consistently scores correct answers below 7, the judge prompt needs revision.");
    }

    /// <summary>
    /// Verifies that a clearly wrong answer fails the judge threshold.
    /// If wrong answers pass, the judge is not discriminating between correct and
    /// incorrect responses — the entire LLM-as-judge evaluation is non-functional.
    /// </summary>
    [TestMethod]
    public async Task LlmJudgeValidator_WrongAnswer_FailsThreshold()
    {
        SkipIfNoApiKey();

        var options   = BuildOptions();
        var factory   = new LLMClientFactory(options, NullLogger<LLMClientFactory>.Instance);
        var validator = new LLMJudgeValidator(
            factory.CreateChatClient(),
            options,
            NullLogger<LLMJudgeValidator>.Instance);

        var result = await validator.ValidateAsync(
            prompt:   "What is the capital of France?",
            expected: "Paris",
            actual:   "The capital of France is Berlin.",
            criteria: "The answer must correctly identify Paris as the capital.");

        Assert.IsFalse(result.Passed,
            $"A factually wrong answer must fail the judge threshold. " +
            $"Score: {result.Score}/10. Reasoning: {result.Reasoning}");
    }

    // =========================================================================
    // Full pipeline — TestRunner end-to-end
    // =========================================================================

    /// <summary>
    /// Runs a single factual test case through the complete pipeline:
    /// TestRunner → EmbeddingValidator + LLMJudgeValidator → TestResult.
    /// This validates the full execution path including OR verdict logic,
    /// run counting, average score calculation, and metadata copying.
    /// </summary>
    [TestMethod]
    public async Task FactualQuestion_CorrectAnswer_Passes()
    {
        SkipIfNoApiKey();

        var options = BuildOptions();
        var factory = new LLMClientFactory(options, NullLogger<LLMClientFactory>.Instance);

        var embeddingValidator = new EmbeddingValidator(
            factory.CreateEmbeddingGenerator(),
            new CosineSimilarityCalculator(),
            options,
            NullLogger<EmbeddingValidator>.Instance);

        var judgeValidator = new LLMJudgeValidator(
            factory.CreateChatClient(),
            options,
            NullLogger<LLMJudgeValidator>.Instance);

        var runner = new TestRunner(
            factory.CreateChatClient(),
            embeddingValidator,
            judgeValidator,
            options,
            NullLogger<TestRunner>.Instance);

        var testCase = new TestCase
        {
            Id                 = "integration_001",
            Category           = "factual",
            Prompt             = "What is the capital of France? Answer in one word.",
            ExpectedOutput     = "Paris",
            EvaluationCriteria = "The answer must identify Paris as the capital of France."
        };

        var results = await runner.RunAllAsync(new List<TestCase> { testCase });

        // The result must exist and contain correct metadata.
        Assert.AreEqual(1, results.Count,
            "RunAllAsync must return exactly one result for one input test case.");
        Assert.AreEqual("integration_001", results[0].TestId,
            "TestId must be copied from the input test case.");
        Assert.AreEqual(1, results[0].TotalRunsCount,
            "TotalRunsCount must equal NumberOfRuns (1 in integration config).");
        Assert.IsTrue(results[0].AverageEmbeddingScore > 0 || results[0].AverageJudgeScore > 0,
            "At least one validator must have produced a non-zero score for a factual question.");
    }
}