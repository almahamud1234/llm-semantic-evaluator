# ML 25/26-08 – Implementing Tests for LLM Prompts with Semantic Assertions

A semantic evaluation framework for testing Large Language Model (LLM) prompts using AI-powered assertions. This project addresses the challenge of testing non-deterministic LLM outputs by implementing semantic validation instead of exact string matching.

Developed by **LLM QA Lab**, this project provides an automated, reproducible approach to validating LLM responses by combining embedding-based similarity checks and LLM-as-a-judge evaluation techniques.

---

## The Problem

When testing LLM outputs, traditional assertion methods fail because the same prompt can generate semantically equivalent but lexically different responses:

- *"The capital of France is Paris."*
- *"Paris is France's capital city."*
- *"Paris."*

Traditional testing (`assert output == "Paris"`) fails all three. This framework uses **semantic understanding** to correctly identify all three as passing.

---

## How It Works

Each test case runs through a two-stage validation pipeline:

```
Prompt → OpenAI → LLM Response
                       ↓
          ┌────────────────────────┐
          │   EmbeddingValidator   │  cosine similarity ≥ threshold e.g. 0.75
          │   LLMJudgeValidator    │  judge score ≥ threshold e.g. 8/10
          └────────────────────────┘
                       ↓
            Pass if either validator passes
                       ↓
              Repeat 3 times per test
                       ↓
          Pass if 2/3 runs pass (majority vote)
```

---

## Key Features

- Console-based evaluation framework built with **.NET 8 / C#**
- Automated execution of prompt test cases loaded from JSON
- **Embedding-based semantic similarity** using cosine similarity
- **LLM-as-a-judge** evaluation for nuanced correctness checks
- **Multiple test runs** with majority vote to handle LLM non-determinism
- Comprehensive reporting in **TXT, JSON, and CSV** formats
- Configurable similarity thresholds and pass criteria
- Graceful error handling for API failures, rate limits, and malformed input
- Support for 100+ test cases across multiple categories

---

## Project Structure

```
LLMSemanticEvaluator/
├── Configuration/
│   └── CommandLineOptions.cs
│   └── TestConfiguration.cs      # Loads appsettings.json
├── data/
│   └── sample_test_cases.json
├── Interfaces/
│   ├── IEmbeddingProvider.cs
│   ├── ILLMClient.cs
│   ├── IReportGenerator.cs
│   ├── ISimilarityCalculator.cs
│   └── ITestLoader.cs
│   └── ITestRunner.cs
│   └── ITestValidator.cs
├── Models/
│   ├── CategoryStats.cs
│   ├── TestCase.cs               # Input: prompt + expected output
│   ├── TestProgressEventArgs.cs
│   ├── TestReport.cs
│   ├── TestResult.cs             # Output: aggregated pass/fail
│   ├── TestRun.cs                # Single run scores
│   └── ValidationResult.cs      # Per-validator outcome
├── reports/                      # Auto-generated on each run
│   ├── report.txt
│   ├── report.json
│   └── report.csv
├── appsettings.json
├── CosineSimilarityCalculator.cs
├── EmbeddingValidator.cs         # Validates via embedding similarity
├── JsonTestLoader.cs             # Loads test cases from JSON
├── LLMJudgeValidator.cs          # Validates via LLM-as-judge
├── LLMSemanticEvaluator.csproj
├── OpenAIClient.cs               # Handles all OpenAI API calls
├── Program.cs                    # Entry point
├── ReportGenerator.cs            # Generates txt/json/csv reports
└── TestRunner.cs                 # Orchestrates test execution
```

---

## Requirements

- .NET 8.0 SDK or higher
- OpenAI API key
- C# 12.0

---

## Installation

**1. Clone the repository**
```bash
git clone https://github.com/almahamud1234/llm-semantic-evaluator.git
cd llm-semantic-evaluator
```

**2. Restore dependencies**
```bash
dotnet restore
```

**3. Configure your API key**

Copy the example config and add your key:
```bash
cp appsettings.example.json appsettings.json
```

Open `appsettings.json` and replace the placeholder:
```json
{
  "ApiKey": "sk-your-actual-key-here",
  "Model": "gpt-4o-mini",
  "EmbeddingModel": "text-embedding-ada-002"
}
```

> Note: `appsettings.json` is listed in `.gitignore` — your key will never be committed.

**4. Build the project**
```bash
dotnet build
```

---

## Usage

```bash
dotnet run
```

Console output during a run:
```
Loading test cases...
  Loaded 120 test cases from sample_test_cases.json

Starting test run: 120 tests, 3 runs each

[1/120]  factual_001 ... ✅ PASS (3/3 runs passed)
[2/120]  factual_002 ... ✅ PASS (2/3 runs passed)
[3/120]  math_001    ... ✅ PASS (3/3 runs passed)
...

=== Report Summary ===
Total  : 120
Passed : 108
Failed : 12
Rate   : 90.0%

Reports saved to: /path/to/reports/
  report.txt  — human-readable summary
  report.json — full data for visualization
  report.csv  — flat data for Excel/charts
```

---

## Test Case Format

Test cases are stored in `data/sample_tests.json` as a JSON array:

```json
[
  {
    "id": "factual_001",
    "category": "factual",
    "prompt": "What is the capital of France?",
    "expected_output": "Paris",
    "evaluation_criteria": "Response must identify Paris as the capital of France"
  }
]
```

| Field | Required | Description |
|-------|----------|-------------|
| `id` | ✅ | Unique identifier for the test |
| `prompt` | ✅ | The question sent to the LLM |
| `expected_output` | ✅ | The expected correct answer |
| `category` | ❌ | Groups tests in reports (defaults to `general`) |
| `evaluation_criteria` | ❌ | Human-readable grading hint |

---

## Test Categories

The 120 included test cases cover six categories:

| Category | Count | Examples |
|----------|-------|---------|
| Factual | 20 | Capitals, geography, basic facts |
| Math | 20 | Arithmetic, percentages, sequences |
| Definitions | 20 | Scientific, economic, technical terms |
| Reasoning | 20 | Logic puzzles, syllogisms, sequences |
| Science | 20 | Physics, biology, chemistry |
| History | 20 | Dates, people, events |

---

## Evaluation Strategies

### 1. Embedding-Based Semantic Similarity

Converts text to high-dimensional vectors and computes cosine similarity:

```
similarity = (A · B) / (||A|| × ||B||)
```

- Score range: 0.0 to 1.0
- Default threshold: 0.75 (configurable)
- Pass condition: `similarity ≥ threshold`

### 2. LLM-as-Judge Validation

Uses a secondary LLM to evaluate responses based on factual correctness, semantic equivalence, and criteria compliance:

- Score range: 1–10
- Default threshold: 8 (configurable)
- Pass condition: `score ≥ threshold`

### 3. Combined Validation (Majority Vote)

A single **run** passes if **either** validator passes.
A **test** passes overall if **2 out of 3 runs** pass.

This approach handles LLM non-determinism — a single unlucky response won't fail an otherwise reliable test.

---

## Configuration

All thresholds are adjustable in `Program.cs`:

```csharp
// Lower = more lenient embedding matching
var embeddingValidator = new EmbeddingValidator(client, new CosineSimilarityCalculator(), threshold: 0.75);

// Lower = more lenient judge scoring
var judgeValidator = new LLMJudgeValidator(client, threshold: 8);

// Fewer runs = faster and cheaper (minimum recommended: 3)
var runner = new TestRunner(client, embeddingValidator, judgeValidator, runsPerTest: 3);
```

---

## Reports

Three report files are auto-generated in the `reports/` folder after each run:

**report.txt** — human-readable with overall summary, category breakdown, and per-test details including individual run scores.

**report.json** — full structured data including all runs and scores, useful for custom visualizations or further analysis.

**report.csv** — one row per test case for Excel or Google Sheets. Columns:
`TestId, Category, Passed, PassedRuns, TotalRuns, AvgEmbeddingScore, AvgJudgeScore, Prompt, ExpectedOutput`

### Quick Visualization
1. Open `reports/report.csv` in Excel or Google Sheets
2. Select the `Category` and `Passed` columns
3. Insert → Chart → Bar chart

---

## Error Handling

The framework handles the following gracefully without crashing:

| Scenario | Behaviour |
|----------|-----------|
| Missing or placeholder API key | Detected at startup, exits with a clear message |
| Malformed JSON in test file | Specific error message, file skipped |
| Individual test missing required fields | Test skipped with a warning, rest continue |
| Empty LLM response | Counted as a failed run automatically |
| Request timeout | Counted as a failed run, execution continues |

---

## License

This project is licensed under the MIT License.

---

## Team

**LLM QA Lab**
- [Md Abdulla AL Mahamud Rosi](https://github.com/almahamud1234)

---

## References

- [OpenAI API Documentation](https://platform.openai.com/docs)
- [Cosine Similarity — Wikipedia](https://en.wikipedia.org/wiki/Cosine_similarity)
- [LLM-as-a-Judge Paper](https://arxiv.org/abs/2306.05685)