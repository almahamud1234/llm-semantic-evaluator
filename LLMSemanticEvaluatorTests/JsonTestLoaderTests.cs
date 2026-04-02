using System.Text.Json;
using LLMSemanticEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LLMSemanticEvaluatorTests;

/// <summary>
/// Unit tests for <see cref="JsonTestLoader"/>.
///
/// <para>
/// <see cref="JsonTestLoader"/> is the single entry point for test-case data.
/// If it loads garbage, applies wrong defaults, or silently ignores duplicate IDs,
/// every downstream component — TestRunner, EmbeddingValidator, ReportGenerator —
/// operates on corrupt input. Catching these failures here, before any LLM call
/// is made, saves expensive API quota and produces actionable error messages.
/// </para>
///
/// <para>
/// Because the loader reads real files, each test writes a temporary file via the
/// <c>WriteTempFile</c> helper and deletes it in a <c>finally</c> block.
/// No mocking is needed — the I/O itself is part of the contract being tested.
/// </para>
///
/// <para>Run with: <c>dotnet test</c></para>
/// </summary>
[TestClass]
public class JsonTestLoaderTests
{
    /// <summary>
    /// The system under test. Stateless, so one instance is shared across all tests.
    /// </summary>
    private readonly JsonTestLoader _loader = new();

    // ── Helper ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes <paramref name="content"/> to a system temp file and returns its path.
    /// The caller must delete the file (handled in each test's <c>finally</c> block).
    /// </summary>
    private static string WriteTempFile(string content)
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }

    // =========================================================================
    // Happy Path — loading valid JSON
    // =========================================================================

    /// <summary>
    /// The primary supported format is a direct JSON array: <c>[ {...}, {...} ]</c>.
    /// Verifies all required fields map correctly onto <see cref="TestCase"/> properties.
    /// If the deserialiser mis-maps even one field, every evaluation based on that
    /// test case produces the wrong result.
    /// </summary>
    [TestMethod]
    public async Task LoadTestsAsync_ValidArrayFormat_ReturnsTestCases()
    {
        string path = WriteTempFile("""
            [
              {
                "id": "test_001",
                "category": "factual",
                "prompt": "What is the capital of France?",
                "expected_output": "Paris",
                "evaluation_criteria": "Must identify Paris"
              }
            ]
            """);

        try
        {
            var result = await _loader.LoadTestsAsync(path);

            // All three core fields must deserialise to the correct string values.
            Assert.AreEqual(1, result.Count,
                "Exactly one test case is present in the JSON.");
            Assert.AreEqual("test_001", result[0].Id,
                "Id must match the 'id' property in the JSON.");
            Assert.AreEqual("factual", result[0].Category,
                "Category must match the 'category' property in the JSON.");
            Assert.AreEqual("Paris", result[0].ExpectedOutput,
                "ExpectedOutput must match 'expected_output' — this is what LLM answers are judged against.");
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// The fallback format wraps the array in an object: <c>{ "tests": [...] }</c>.
    /// Some test-suite authors use this convention; the loader must handle it
    /// transparently so operators are not forced to reformat existing test files.
    /// </summary>
    [TestMethod]
    public async Task LoadTestsAsync_WrappedFormat_ReturnsTestCases()
    {
        string path = WriteTempFile("""
            {
              "tests": [
                {
                  "id": "test_001",
                  "prompt": "What is 2 + 2?",
                  "expected_output": "4"
                }
              ]
            }
            """);

        try
        {
            var result = await _loader.LoadTestsAsync(path);

            // The loader must recognise the wrapper and extract the test array correctly.
            Assert.AreEqual(1, result.Count,
                "Wrapped format must yield the same number of test cases as direct array.");
            Assert.AreEqual("test_001", result[0].Id,
                "Id must be extracted correctly from the wrapped format.");
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// The <c>category</c> and <c>evaluation_criteria</c> fields are optional.
    /// When absent, the loader must apply sensible defaults so downstream components
    /// never receive null or empty values that would cause null-reference exceptions
    /// or misleading output.
    /// </summary>
    [TestMethod]
    public async Task LoadTestsAsync_MissingOptionalFields_SetsDefaults()
    {
        string path = WriteTempFile("""
            [
              { "id": "t1", "prompt": "Hello?", "expected_output": "Hi" }
            ]
            """);

        try
        {
            var result = await _loader.LoadTestsAsync(path);

            // Default category "general" groups uncategorised tests in reports.
            Assert.AreEqual("general", result[0].Category,
                "Missing 'category' must default to 'general' so the report category breakdown works.");

            // EvaluationCriteria is passed verbatim to the judge prompt — it must never be blank.
            Assert.IsFalse(string.IsNullOrWhiteSpace(result[0].EvaluationCriteria),
                "Missing 'evaluation_criteria' must be given a default so the judge prompt is always coherent.");
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// JSON property matching must be case-insensitive (e.g. <c>"ID"</c> matches the
    /// <c>Id</c> C# property). Test-suite authors may use different casing conventions
    /// and the loader must accept all of them without silent data loss.
    /// </summary>
    [TestMethod]
    public async Task LoadTestsAsync_CaseInsensitiveProperties_ParsesCorrectly()
    {
        string path = WriteTempFile("""
            [
              { "ID": "t1", "PROMPT": "Hi?", "EXPECTED_OUTPUT": "Hello" }
            ]
            """);

        try
        {
            var result = await _loader.LoadTestsAsync(path);

            // All-uppercase keys must map to the same C# properties as lowercase keys.
            Assert.AreEqual("t1",    result[0].Id,             "Uppercase 'ID' must map to Id.");
            Assert.AreEqual("Hi?",   result[0].Prompt,         "Uppercase 'PROMPT' must map to Prompt.");
            Assert.AreEqual("Hello", result[0].ExpectedOutput, "Uppercase 'EXPECTED_OUTPUT' must map to ExpectedOutput.");
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// Multiple test cases must all load and preserve their original order.
    /// Order matters because the TestRunner logs progress as "[1/N] id → PASS/FAIL",
    /// and operators expect output order to match input order.
    /// </summary>
    [TestMethod]
    public async Task LoadTestsAsync_MultipleTestCases_ReturnsAll()
    {
        string path = WriteTempFile("""
            [
              { "id": "t1", "prompt": "Q1", "expected_output": "A1" },
              { "id": "t2", "prompt": "Q2", "expected_output": "A2" },
              { "id": "t3", "prompt": "Q3", "expected_output": "A3" }
            ]
            """);

        try
        {
            var result = await _loader.LoadTestsAsync(path);

            // Count must match and order must be preserved.
            Assert.AreEqual(3, result.Count,
                "All three test cases must be loaded — none may be silently dropped.");
            Assert.AreEqual("t1", result[0].Id, "First test case must retain its position.");
            Assert.AreEqual("t3", result[2].Id, "Last test case must retain its position.");
        }
        finally { File.Delete(path); }
    }

    // =========================================================================
    // Input Validation — fail-fast on bad paths
    // =========================================================================

    /// <summary>
    /// A null, empty, or whitespace file path cannot point to a real file.
    /// <see cref="ArgumentException"/> must be thrown immediately so the caller
    /// receives a clear "bad path" message rather than a cryptic file-system error.
    /// </summary>
    /// <param name="path">Invalid path representations that must be rejected.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task LoadTestsAsync_NullOrEmptyPath_ThrowsArgumentException(string? path)
    {
        // Null/empty paths must be rejected before any I/O attempt.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _loader.LoadTestsAsync(path!));
    }

    /// <summary>
    /// A syntactically valid but non-existent path must throw
    /// <see cref="FileNotFoundException"/>. This surfaces configuration errors
    /// (e.g. wrong <c>TestCasesPath</c> in appsettings.json) before any LLM quota
    /// is consumed.
    /// </summary>
    [TestMethod]
    public async Task LoadTestsAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // A clearly invalid path must produce FileNotFoundException.
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _loader.LoadTestsAsync("C:/does/not/exist.json"));
    }

    /// <summary>
    /// An empty file has no JSON content to parse.
    /// <see cref="JsonException"/> must be thrown so the operator knows the file is
    /// blank rather than the loader silently returning zero test cases.
    /// </summary>
    [TestMethod]
    public async Task LoadTestsAsync_EmptyFile_ThrowsJsonException()
    {
        string path = WriteTempFile(string.Empty);
        try
        {
            // An empty file cannot be a valid JSON document.
            await Assert.ThrowsAsync<JsonException>(() =>
                _loader.LoadTestsAsync(path));
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// Completely malformed JSON must throw <see cref="JsonException"/>.
    /// A clear parse error is more helpful than a null-reference crash downstream.
    /// </summary>
    [TestMethod]
    public async Task LoadTestsAsync_MalformedJson_ThrowsJsonException()
    {
        string path = WriteTempFile("{ this is not valid json !!! }");
        try
        {
            await Assert.ThrowsAsync<JsonException>(() =>
                _loader.LoadTestsAsync(path));
        }
        finally { File.Delete(path); }
    }

    // =========================================================================
    // TestCase Validation — semantic checks after successful JSON parse
    // =========================================================================

    /// <summary>
    /// An empty JSON array <c>[]</c> means there are no test cases to run.
    /// <see cref="InvalidOperationException"/> must be thrown so the evaluation does
    /// not start, waste API quota, and write an empty report.
    /// </summary>
    [TestMethod]
    public async Task LoadTestsAsync_EmptyArray_ThrowsInvalidOperationException()
    {
        string path = WriteTempFile("[]");
        try
        {
            // An empty test suite is a configuration error, not a valid run.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _loader.LoadTestsAsync(path));
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// The <c>id</c> field is required because every report entry, every log line,
    /// and every duplicate-check relies on it. A missing ID must throw
    /// <see cref="InvalidOperationException"/> rather than propagating null IDs.
    /// </summary>
    [TestMethod]
    public async Task LoadTestsAsync_MissingId_ThrowsInvalidOperationException()
    {
        string path = WriteTempFile("""
            [{ "prompt": "Hello?", "expected_output": "Hi" }]
            """);
        try
        {
            // A test case without an ID cannot be uniquely tracked in reports.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _loader.LoadTestsAsync(path));
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// The <c>prompt</c> field is what gets sent to the LLM. Without it there is
    /// nothing to test. <see cref="InvalidOperationException"/> must be thrown rather
    /// than sending an empty string to the LLM API.
    /// </summary>
    [TestMethod]
    public async Task LoadTestsAsync_MissingPrompt_ThrowsInvalidOperationException()
    {
        string path = WriteTempFile("""
            [{ "id": "t1", "expected_output": "Hi" }]
            """);
        try
        {
            // A missing prompt means there is no question to ask the LLM.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _loader.LoadTestsAsync(path));
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// The <c>expected_output</c> field is the ground-truth answer that both validators
    /// compare the LLM response against. Without it, neither validator can operate.
    /// <see cref="InvalidOperationException"/> must be thrown before any API call.
    /// </summary>
    [TestMethod]
    public async Task LoadTestsAsync_MissingExpectedOutput_ThrowsInvalidOperationException()
    {
        string path = WriteTempFile("""
            [{ "id": "t1", "prompt": "Hello?" }]
            """);
        try
        {
            // No expected output = no ground truth = impossible to evaluate the LLM.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _loader.LoadTestsAsync(path));
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// Duplicate IDs in a single file must be detected and rejected.
    /// If two test cases share an ID, report entries collide, logs are ambiguous,
    /// and operators cannot tell which test passed or failed.
    /// The exception message must include the offending ID.
    /// </summary>
    [TestMethod]
    public async Task LoadTestsAsync_DuplicateIds_ThrowsInvalidOperationException()
    {
        string path = WriteTempFile("""
            [
              { "id": "t1", "prompt": "Q1", "expected_output": "A1" },
              { "id": "t1", "prompt": "Q2", "expected_output": "A2" }
            ]
            """);
        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _loader.LoadTestsAsync(path));

            // The error message must name the duplicate ID so operators can fix it quickly.
            Assert.IsTrue(ex.Message.Contains("t1"),
                "The exception message must identify the duplicate ID ('t1') for easy diagnosis.");
        }
        finally { File.Delete(path); }
    }
}