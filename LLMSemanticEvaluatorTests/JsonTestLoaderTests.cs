using System.Text.Json;
using LLMSemanticEvaluator;

namespace LLMSemanticEvaluatorTests;

/// <summary>
/// Unit tests for <see cref="JsonTestLoader"/>.
///
/// Because the loader reads real files, each test writes a temporary file,
/// runs the loader, then deletes the file via IAsyncDisposable (TempFile helper).
/// No mocking is required — this keeps the tests simple and close to reality.
///
/// Run with: dotnet test
/// </summary>
public class JsonTestLoaderTests
{
    private readonly JsonTestLoader _loader = new();

    // =========================================================================
    // Helper — creates a real temp file and deletes it after the test
    // =========================================================================

    /// <summary>
    /// Writes content to a temp file and returns its path.
    /// The caller is responsible for deleting it (handled in each test's finally block).
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
    /// Direct array format [ {...}, {...} ] is the primary supported format.
    /// Verifies fields are correctly mapped onto TestCase properties.
    /// </summary>
    [Fact]
    public async Task LoadTestsAsync_ValidArrayFormat_ReturnsTestCases()
    {
        // Arrange
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
            // Act
            var result = await _loader.LoadTestsAsync(path);

            // Assert
            Assert.Single(result);
            Assert.Equal("test_001", result[0].Id);
            Assert.Equal("factual",  result[0].Category);
            Assert.Equal("Paris",    result[0].ExpectedOutput);
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// Wrapped format { "tests": [...] } is the fallback format.
    /// Verifies the loader falls back correctly when direct-array parse fails.
    /// </summary>
    [Fact]
    public async Task LoadTestsAsync_WrappedFormat_ReturnsTestCases()
    {
        // Arrange
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
            // Act
            var result = await _loader.LoadTestsAsync(path);

            // Assert
            Assert.Single(result);
            Assert.Equal("test_001", result[0].Id);
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// Fields 'category' and 'evaluation_criteria' are optional.
    /// When absent, the loader should set sensible defaults so downstream
    /// code never receives null/empty values for these fields.
    /// </summary>
    [Fact]
    public async Task LoadTestsAsync_MissingOptionalFields_SetsDefaults()
    {
        // Arrange — no category, no evaluation_criteria
        string path = WriteTempFile("""
            [
              { "id": "t1", "prompt": "Hello?", "expected_output": "Hi" }
            ]
            """);

        try
        {
            // Act
            var result = await _loader.LoadTestsAsync(path);

            // Assert — defaults must be applied
            Assert.Equal("general", result[0].Category);
            Assert.False(string.IsNullOrWhiteSpace(result[0].EvaluationCriteria));
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// JSON property name matching is case-insensitive (e.g. "ID" vs "id",
    /// "EXPECTED_OUTPUT" vs "expected_output").
    /// Verifies the PropertyNameCaseInsensitive option is working.
    /// </summary>
    [Fact]
    public async Task LoadTestsAsync_CaseInsensitiveProperties_ParsesCorrectly()
    {
        // Arrange — uppercase snake_case property names
        string path = WriteTempFile("""
            [
              { "ID": "t1", "PROMPT": "Hi?", "EXPECTED_OUTPUT": "Hello" }
            ]
            """);

        try
        {
            // Act
            var result = await _loader.LoadTestsAsync(path);

            // Assert
            Assert.Equal("t1",    result[0].Id);
            Assert.Equal("Hi?",   result[0].Prompt);
            Assert.Equal("Hello", result[0].ExpectedOutput);
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// Verifies multiple test cases all load correctly and preserve order.
    /// </summary>
    [Fact]
    public async Task LoadTestsAsync_MultipleTestCases_ReturnsAll()
    {
        // Arrange
        string path = WriteTempFile("""
            [
              { "id": "t1", "prompt": "Q1", "expected_output": "A1" },
              { "id": "t2", "prompt": "Q2", "expected_output": "A2" },
              { "id": "t3", "prompt": "Q3", "expected_output": "A3" }
            ]
            """);

        try
        {
            // Act
            var result = await _loader.LoadTestsAsync(path);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal("t1", result[0].Id);
            Assert.Equal("t3", result[2].Id);
        }
        finally { File.Delete(path); }
    }

    // =========================================================================
    // Input Validation
    // =========================================================================

    /// <summary>Null or whitespace file path must throw ArgumentException immediately.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoadTestsAsync_NullOrEmptyPath_ThrowsArgumentException(string? path)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _loader.LoadTestsAsync(path!));
    }

    /// <summary>A path that does not exist on disk must throw FileNotFoundException.</summary>
    [Fact]
    public async Task LoadTestsAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _loader.LoadTestsAsync("C:/does/not/exist.json"));
    }

    /// <summary>An empty file must throw JsonException — there is nothing to parse.</summary>
    [Fact]
    public async Task LoadTestsAsync_EmptyFile_ThrowsJsonException()
    {
        string path = WriteTempFile(string.Empty);
        try
        {
            await Assert.ThrowsAsync<JsonException>(() =>
                _loader.LoadTestsAsync(path));
        }
        finally { File.Delete(path); }
    }

    /// <summary>Completely broken JSON must throw JsonException.</summary>
    [Fact]
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
    // TestCase Validation
    // =========================================================================

    /// <summary>An array with zero elements must throw — there is nothing to run.</summary>
    [Fact]
    public async Task LoadTestsAsync_EmptyArray_ThrowsInvalidOperationException()
    {
        string path = WriteTempFile("[]");
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _loader.LoadTestsAsync(path));
        }
        finally { File.Delete(path); }
    }

    /// <summary>A test case without a required 'id' field must throw InvalidOperationException.</summary>
    [Fact]
    public async Task LoadTestsAsync_MissingId_ThrowsInvalidOperationException()
    {
        string path = WriteTempFile("""
            [{ "prompt": "Hello?", "expected_output": "Hi" }]
            """);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _loader.LoadTestsAsync(path));
        }
        finally { File.Delete(path); }
    }

    /// <summary>A test case without a required 'prompt' field must throw InvalidOperationException.</summary>
    [Fact]
    public async Task LoadTestsAsync_MissingPrompt_ThrowsInvalidOperationException()
    {
        string path = WriteTempFile("""
            [{ "id": "t1", "expected_output": "Hi" }]
            """);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _loader.LoadTestsAsync(path));
        }
        finally { File.Delete(path); }
    }

    /// <summary>A test case without a required 'expected_output' field must throw InvalidOperationException.</summary>
    [Fact]
    public async Task LoadTestsAsync_MissingExpectedOutput_ThrowsInvalidOperationException()
    {
        string path = WriteTempFile("""
            [{ "id": "t1", "prompt": "Hello?" }]
            """);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _loader.LoadTestsAsync(path));
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// Duplicate IDs within the same file must throw — each test must be uniquely
    /// identifiable in reports.
    /// </summary>
    [Fact]
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

            // The exception message should name the offending ID
            Assert.Contains("t1", ex.Message);
        }
        finally { File.Delete(path); }
    }
}