# Unit Testing

The project includes unit tests across six groups, covering all core components independently of any live API.

[Back to README](../README.md)

---

## Running the Tests

```bash
cd LLMSemanticEvaluatorTests
dotnet test
```

Expected output:
```
Test run for LLMSemanticEvaluatorTests.dll (.NETCoreApp,Version=v8.0)
Microsoft (R) Test Execution Command Line Tool

Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed: 0, Skipped: 0
```

---

## Test Groups

### 1. File Handling — `JsonTestLoaderTests.cs`

Tests for `JsonTestLoader` under diverse input conditions:

| Test | Scenario |
|---|---|
| `LoadTestsAsync_ValidArrayFormat_ReturnsTestCases` | Loads a bare JSON array successfully |
| `LoadTestsAsync_WrappedFormat_ReturnsTestCases` | Loads `{ "tests": [...] }` wrapped format |
| `LoadTestsAsync_MissingOptionalFields_SetsDefaults` | Sets `category` to `"general"` and populates `evaluationCriteria` when absent |
| `LoadTestsAsync_CaseInsensitiveProperties_ParsesCorrectly` | Property names are parsed case-insensitively |
| `LoadTestsAsync_MultipleTestCases_ReturnsAll` | All test cases are loaded and order is preserved |
| `LoadTestsAsync_NullOrEmptyPath_ThrowsArgumentException` | Rejects a null or empty file path |
| `LoadTestsAsync_NonExistentFile_ThrowsFileNotFoundException` | Throws `FileNotFoundException` for a non-existent path |
| `LoadTestsAsync_EmptyFile_ThrowsJsonException` | Throws `JsonException` for an empty file |
| `LoadTestsAsync_MalformedJson_ThrowsJsonException` | Throws `JsonException` for invalid JSON |
| `LoadTestsAsync_EmptyArray_ThrowsInvalidOperationException` | Throws `InvalidOperationException` for an empty test array |
| `LoadTestsAsync_MissingId_ThrowsInvalidOperationException` | Throws on a test case with no `id` field |
| `LoadTestsAsync_MissingPrompt_ThrowsInvalidOperationException` | Throws on a test case with no `prompt` field |
| `LoadTestsAsync_MissingExpectedOutput_ThrowsInvalidOperationException` | Throws on a test case with no `expectedOutput` |
| `LoadTestsAsync_DuplicateIds_ThrowsInvalidOperationException` | Throws when two test cases share the same `id` |

---

### 2. Similarity Calculation — `CosineSimilarityCalculatorTests.cs`

Tests for `CosineSimilarityCalculator` against analytically known results:

| Test | Scenario |
|---|---|
| `IdenticalVectors_ShouldReturnOne` | Same vector → `1.0` |
| `ScaledVectors_ShouldReturnOne` | Scaled vector (same direction) → `1.0` |
| `OrthogonalVectors_ShouldReturnZero` | Perpendicular vectors → `0.0` |
| `OppositeVectors_ShouldReturnNegativeOne` | Opposite direction → `-1.0` |
| `Result_ShouldAlwaysBeWithinValidRange` | 100 random pairs always stay within `[-1, 1]` |
| `Similarity_ShouldBeSymmetric` | `sim(A,B) == sim(B,A)` |
| `ZeroVector_ShouldReturnZero` | Zero magnitude → `0.0` (no exception) |
| `NullVectorA_ShouldThrowArgumentNullException` | Null first argument → `ArgumentNullException` |
| `NullVectorB_ShouldThrowArgumentNullException` | Null second argument → `ArgumentNullException` |
| `EmptyVector_ShouldThrowArgumentException` | Empty array → `ArgumentException` |
| `MismatchedDimensions_ShouldThrowArgumentException` | Different-length vectors → `ArgumentException` |
| `PassesThreshold_*` (3 tests) | Above / below / exact threshold → correct bool |
| `PassesThreshold_InvalidThreshold_ShouldThrow` | Out-of-range threshold → `ArgumentOutOfRangeException` |
| `InterpretScore_ShouldReturnCorrectLabel` | Theory covering all score bands (Identical → Opposite) |

---

### 3. Embedding Validation — `EmbeddingValidatorTests.cs`

Tests for `EmbeddingValidator` using mocked `IEmbeddingProvider` and `ISimilarityCalculator`:

| Test | Scenario |
|---|---|
| `ValidateAsync_SimilarityAboveThreshold_ReturnsPassed` | High similarity → `Passed = true` |
| `ValidateAsync_SimilarityBelowThreshold_ReturnsFailed` | Low similarity → `Passed = false` |
| `ValidateAsync_SimilarityExactlyAtThreshold_ReturnsPassed` | Boundary is inclusive |
| `ValidateAsync_EmptyOrNullActual_ReturnsFailedWithoutCallingApi` | Empty/null actual → fails without calling API |
| `ValidateAsync_EmptyEmbeddingVectors_ReturnsFailedSafely` | API returns empty vectors → safe fail |
| `ValidateAsync_EmbeddingApiThrows_ReturnsFailedWithErrorReasoning` | API throws → failed result with error in `Reasoning` |

---

### 4. LLM Judge Validation — `LLMJudgeValidatorTests.cs`

Tests for `LLMJudgeValidator` using a mocked `ILLMClient`, focusing on score parsing and pass/fail logic:

| Test | Scenario |
|---|---|
| `ValidateAsync_ScoreAboveThreshold_ReturnsPassed` | Score > threshold → `Passed = true` |
| `ValidateAsync_ScoreBelowThreshold_ReturnsFailed` | Score < threshold → `Passed = false` |
| `ValidateAsync_ScoreExactlyAtThreshold_ReturnsPassed` | Boundary is inclusive |
| `ValidateAsync_VariousJudgeResponseFormats_ParsesScoreCorrectly` | Theory: bare number, `Score: 9`, natural language, punctuation, whitespace |
| `ValidateAsync_UnparsableJudgeResponse_ReturnsFailed` | No valid 1-10 score → `Score = 0`, `Passed = false` |
| `ValidateAsync_EmptyOrNullActual_ReturnsFailedWithoutCallingApi` | Empty/null actual → fails without calling judge |
| `ValidateAsync_JudgeReturnsEmptyString_ReturnsFailed` | Empty judge response → safe fail |
| `ValidateAsync_LlmClientThrows_ReturnsFailedWithErrorReasoning` | API throws → failed result with error in `Reasoning` |

---

### 5. Report Generation — `ReportGeneratorTests.cs`

Tests for `ReportGenerator` writing to a real temp folder:

| Test | Scenario |
|---|---|
| `GenerateAsync_WithResults_CreatesAllThreeFiles` | All three files (`txt`, `json`, `csv`) are created |
| `GenerateAsync_EmptyResults_CreatesNoFiles` | Empty results list → no files written, no crash |
| `GenerateAsync_TextReport_ContainsKeySections` | TXT report contains OVERALL SUMMARY, CATEGORY BREAKDOWN, PER-TEST DETAILS |
| `GenerateAsync_TextReport_ShowsFailedTest` | Failed test appears as FAIL |
| `GenerateAsync_JsonReport_IsValidJsonWithSummary` | JSON is valid and summary fields are correct |
| `GenerateAsync_JsonReport_ContainsTestResults` | Per-test results include `testId` and `passed` |
| `GenerateAsync_CsvReport_ContainsHeaderAndDataRows` | CSV has header row and one data row per result |
| `GenerateAsync_CsvReport_EscapesCommasAndQuotes` | Commas and double-quotes in fields are correctly escaped |
| `GenerateAsync_MultipleCategories_AllAppearsInReport` | All category names appear in the breakdown |

---

### 6. Test Runner — `TestRunnerTests.cs`

Tests for `TestRunner` using mocked `ILLMClient`, `IEmbeddingProvider`, and `ISimilarityCalculator`:

| Test | Scenario |
|---|---|
| `RunAllAsync_SingleTestCase_ReturnsOneResult` | One result per test case |
| `RunAllAsync_ResultFields_MappedFromTestCase` | `TestId`, `Category`, `Prompt`, `ExpectedOutput` mapped correctly |
| `RunAllAsync_MultipleTestCases_ReturnsResultForEach` | One result per test case for multiple cases |
| `RunAllAsync_EmbeddingSimilarityAboveThreshold_TestPasses` | Embedding pass (OR logic) → overall pass |
| `RunAllAsync_BothValidatorsFail_TestFails` | Both validators fail → overall fail |
| `RunAllAsync_TwoOfThreeRunsPass_TestPasses` | 2/3 majority → `Passed = true` |
| `RunAllAsync_OneOfThreeRunsPass_TestFails` | 1/3 → `Passed = false` |
| `RunAllAsync_AverageScores_CalculatedCorrectly` | `AverageEmbeddingScore` and `AverageJudgeScore` computed correctly |
| `RunAllAsync_LlmThrowsEveryRun_TestFailsGracefully` | All runs throw → result exists, `Passed = false`, no crash |
| `RunAllAsync_OneRunFails_OtherRunsStillCounted` | Failed run logged as ERROR; remaining runs counted correctly |

---

## Coverage Notes

The unit tests deliberately do not make any live API calls. All external dependencies (`ILLMClient`, `IEmbeddingProvider`, `ISimilarityCalculator`) are mocked with Moq. This means the full suite runs in under 5 seconds without any API key or internet connection.

---

[Back to README](../README.md)