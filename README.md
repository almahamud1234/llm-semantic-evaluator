# ML 25/26-08 – Implementing Tests for LLM Prompts with Semantic Assertions

## Problem Statement

Testing an LLM is not like testing a calculator. A calculator has one correct answer. An LLM has thousands of valid ones.

Ask an LLM *"What is the capital of France?"* and it might reply *"Paris"*, or *"The capital of France is Paris."*, or *"Paris has served as France's capital since medieval times."*. All three are factually correct. A traditional `assert output == "Paris"` would accept only one of them and silently reject the rest — not because the LLM was wrong, but because the test was too rigid.

Standard metrics like BLEU and ROUGE have the same problem: they penalise valid paraphrases. They were designed for tasks with one canonical output; LLM responses do not have one.

This project solves that problem. The goal, as defined by project ML 25/26-08, is to build a testing framework for LLM prompts that evaluates **meaning** rather than wording. Instead of checking whether two strings match character-for-character, it checks whether two answers mean the same thing.

---

## What the Framework Does

The **LLM Semantic Evaluator** is a .NET/C# console application built on Microsoft.Extensions.Hosting. You give it a list of test cases in JSON, each containing a prompt and an expected answer. It sends each prompt to an LLM, receives the response, and runs two independent semantic checks to decide whether the response is correct:

**Approach 1 — Embedding Cosine Similarity:** Both the expected and actual answers are converted into high-dimensional embedding vectors. The cosine similarity between the two vectors is measured — a score of 1 means semantically identical, a score near 0 means unrelated. A run passes if `similarity ≥ 0.85` (configurable).

**Approach 2 — LLM-as-a-Judge (G-Eval style):** A secondary LLM reads the original prompt, the expected answer, and the actual response, then reasons step by step before assigning a quality score from 1 to 10. This directly implements the G-Eval methodology, where requiring chain-of-thought reasoning before scoring produces judgements more strongly correlated with human opinion than asking for a number directly. The judge's reasoning text is stored in the reports so every decision is auditable. A run passes if `score ≥ 8/10` (configurable).

A single run passes if **either** validator succeeds (OR logic). This is not a convenience shortcut — it is a structural necessity. The two validators have complementary blind spots:

- The **embedding validator** is fast and consistent, but produces low similarity scores when the expected output is a short phrase and the actual response is a full sentence. For example, if the expected output is `"Paris"` and the model responds `"The capital of France is Paris."`, cosine similarity is approximately 0.45 — well below 0.85 — even though the answer is correct.
- The **judge validator** handles these cases by evaluating meaning directly, but depends on the judge model's ability to follow a structured scoring rubric. Small models fail at this.

The OR combination means the framework stays correct even when one validator has structural limitations.

Because LLMs are non-deterministic, each test case is run **three times** by default. A test passes overall if at least **two of three runs** pass (majority vote). A single network error or atypical response does not fail a test that would otherwise pass consistently. Two independent failures on the same test are treated as a genuine signal that the model's answer is wrong.

---

## Key Results

Two evaluation runs were performed. The primary run used `sample_test_cases.json`, a **138-case dataset** across 6 knowledge domains, with OpenAI `gpt-5-mini`. The second run used `quick_tests.json`, a **50-case subset**, with Ollama `llama3.2:3b` running locally.

### Full-dataset run (OpenAI, 138 cases)

| Metric | Value |
|---|---|
| Total test cases | 138 |
| Passed | 137 |
| Failed | 1 |
| **Pass rate** | **99.3%** |
| Avg embedding score | 0.52 |
| Avg judge score | 10 / 10 |

5 of the 6 categories achieved 100%. The single failure is a **dataset quality issue**, not a model error: test case `history_006` asks *"Which empire was Julius Caesar a part of?"* with expected output `"The Roman Empire"`. The model correctly responded that Caesar was a general of the late Roman Republic — he was assassinated in 44 BC, and the Roman Empire is conventionally dated from 27 BC. The model's answer is historically more accurate than the reference. Correcting the expected output to `"The Roman Republic"` would bring the pass rate to 100%.

The average embedding score of 0.52 is far below the 0.85 threshold, yet the pass rate is 99.3%. This is explained by the OR logic: only 6 of the 414 individual runs (138 tests × 3 runs) reached the embedding threshold. Nearly all passes were driven by the judge path, which correctly scored the responses 10/10. This empirically confirms that the dual-validator design is essential — a pure embedding approach would fail approximately 98.5% of runs even for a model that is answering correctly.

### Controlled comparison on the same 50-case subset

To isolate provider behaviour from dataset size, both providers were compared under identical conditions:

| Metric | OpenAI (`gpt-5-mini`) | Ollama (`llama3.2:3b`) |
|---|---|---|
| Total tests | 50 | 50 |
| Passed | 50 | 41 |
| Failed | 0 | 9 |
| **Pass rate** | **100%** | **82%** |
| Avg embedding score | 0.57 | 0.67 |
| Avg judge score | 10 / 10 | 7.8 / 10 |

The performance gap is isolated entirely to the judge path — Ollama's average embedding score (0.67) is actually *higher* than OpenAI's (0.57), because `nomic-embed-text`'s 768-dimensional vectors cluster common factual expressions more tightly than `text-embedding-3-small`'s 1,536-dimensional space. The 9 Ollama failures are caused by judge miscalibration: `llama3.2:3b` systematically assigns low scores to responses its own chain-of-thought reasoning identifies as correct.

The clearest example is test case `math_001`: prompt *"What is 2 + 2?"*, expected output `"4"`, model response *"2 + 2 = 4."* The judge's own reasoning stated: *"The actual answer correctly addresses the query. It captures the same meaning as the expected answer. There are no factual errors or key omissions."* Despite this, the judge assigned a score of 1/10 on all three runs. This demonstrates that `llama3.2:3b` cannot reliably map its own chain-of-thought reasoning to a numeric score. **For Ollama, a model of at least 7–8 billion parameters is recommended for the judge role.**

The Ollama dataset was limited to 50 cases because `llama3.2:3b` inference times on the evaluation machine regularly caused HTTP requests to exceed the 100-second timeout, making the full 138-case run impractical. This is a hardware constraint of the evaluation machine, not a framework limitation.

---

## Architecture

The application is structured as a **Microsoft.Extensions.Hosting generic host** — the same dependency injection and configuration infrastructure used in production .NET services. Every component is independently testable because no class instantiates another class with `new`; all dependencies are injected by the DI container. All runtime output is written through `ILogger<T>` rather than `Console.WriteLine`, so the application works correctly in Docker containers and CI pipelines where a console may not be available.

The central design decision is using `Microsoft.Extensions.AI`'s `IChatClient` and `IEmbeddingGenerator` standard interfaces rather than writing custom HTTP clients. All services that send prompts or generate embeddings depend on these interfaces, never on a concrete provider class. Switching from OpenAI to Grok or Ollama requires only a change to `appsettings.json`.

<img width="952" height="778" alt="system-architecture-design" src="https://github.com/user-attachments/assets/ffa6e6f5-b57d-4487-a4fe-bd5678f6afcd" />

*Fig 1: System Architecture Diagram*

**Interface contracts — each component can be replaced without changing any other:**

| Interface | Purpose | Implemented by |
|---|---|---|
| `IChatClient` | Send a prompt, receive a response string | `OpenAIClient`, `OllamaApiClient` |
| `IEmbeddingGenerator<string, Embedding<float>>` | Generate a `float[]` embedding vector | `OpenAIClient`, `OllamaApiClient` |
| `ISimilarityCalculator` | Compute similarity between two vectors | `CosineSimilarityCalculator` |
| `ITestLoader` | Load a `List<TestCase>` from a source | `JsonTestLoader` |

---

## Getting Started

### Prerequisites

1. **.NET 10 SDK** — [Download from Microsoft](https://dotnet.microsoft.com/download/dotnet/10.0)

   ```bash
   dotnet --version
   # Expected: 10.0.x
   ```

2. **An LLM provider** — choose one:
   - **OpenAI** — API key from [platform.openai.com/api-keys](https://platform.openai.com/api-keys)_
   - **Ollama** — free and fully local; see [Ollama Setup](#ollama-setup-local-models) below

---

### Setup Steps

**1. Clone the repository**
```bash
git clone https://github.com/almahamud1234/llm-semantic-evaluator
cd LLMSemanticEvaluator
```

**2. Create your configuration file**
```bash
cp appsettings_example.json appsettings.json
```
Open `appsettings.json` and fill in your API key and model names. See [Configuration](#configuration) for all fields.

**3. Build and run**
```bash
dotnet restore
dotnet build
dotnet run
```

The framework loads test cases from `data/sample_test_cases.json`, runs all tests, saves four report files to `reports/`, and automatically opens the HTML dashboard in your browser.

---

### Ollama Setup (Local Models)

Ollama runs models entirely on your machine — no API key, no usage costs, no internet connection required after the initial model download. A `docker-compose.yml` is included in the repository.

```bash
# Start the Ollama container
docker compose up

# Pull the required models
docker exec -it ollama ollama pull llama3.2:3b       # chat model (also used as judge)
docker exec -it ollama ollama pull nomic-embed-text  # embedding model

# Verify both are available
docker exec -it ollama ollama list
```

> **Important:** Models smaller than ~7–8B parameters cannot reliably follow the judge's structured scoring rubric. `llama3.2:3b` was used in evaluation and produced systematic judge miscalibration (see [Key Results](#key-results)). For reliable judge scoring, prefer `llama3:8b` or larger. Alternatively, you can keep the tested model on Ollama while routing the judge calls through OpenAI — the `Provider` and `EmbeddingProvider` settings are independent for exactly this reason.

Then update `appsettings.json`:
```json
{
  "Provider": "ollama",
  "EmbeddingProvider": "ollama",
  "ChatModel": "llama3.2:3b",
  "EmbeddingModel": "nomic-embed-text",
  "OllamaBaseUrl": "http://localhost:11434",
  "TimeoutSeconds": 100
}
```

> Increase `TimeoutSeconds` for larger models. `llama3.2:3b` on CPU requires 100 seconds or more per request on a typical machine.

---

## Configuration

All settings live in `appsettings.json`. Copy `appsettings_example.json` to get started — it contains every field with placeholder values. The file is bound to a strongly-typed `TestConfiguration` class via `IOptions<T>`; no service reads the JSON directly.

| Field | Type | Default | Purpose |
|---|---|---|---|
| `Provider` | string | `openai` | Chat LLM provider: `openai`, or `ollama` |
| `EmbeddingProvider` | string | `openai` | Embedding provider: `openai` or `ollama`. Grok does not expose an embeddings endpoint. |
| `ApiKey` | string | — | API key for OpenAI or Grok. Not required for Ollama. |
| `OllamaBaseUrl` | string | `http://localhost:11434` | Base URL of your local Ollama instance |
| `ChatModel` | string | `gpt-5-mini` | Model name for chat completions (the model under test) |
| `EmbeddingModel` | string | `text-embedding-3-small` | Model name for generating embedding vectors |
| `Temperature` | float | `0.0` | Sampling temperature. `0.0` produces near-deterministic output — recommended for reproducible results |
| `EmbeddingThreshold` | float | `0.85` | Minimum cosine similarity for the embedding validator to pass a run |
| `JudgeThreshold` | int | `8` | Minimum score (1–10) for the judge validator to pass a run |
| `NumberOfRuns` | int | `3` | How many times each test case is executed |
| `MinimumPassingRuns` | int | `2` | How many of those runs must pass for the test case to pass overall (majority vote) |
| `TimeoutSeconds` | int | `30` | HTTP timeout per API request. Increase significantly for local Ollama models. |
| `RequestDelayMs` | int | `200` | Delay between requests — rate-limit protection |
| `TestCasesPath` | string | `data/sample_test_cases.json` | Path to your test case JSON file |

### Quick-start configs

<details>
<summary><strong>OpenAI</strong></summary>

```json
{
  "Provider": "openai",
  "EmbeddingProvider": "openai",
  "ApiKey": "sk-...",
  "ChatModel": "gpt-5-mini",
  "EmbeddingModel": "text-embedding-3-small"
}
```
</details>

<details>
<summary><strong>Ollama (fully local)</strong></summary>

```json
{
  "Provider": "ollama",
  "EmbeddingProvider": "ollama",
  "OllamaBaseUrl": "http://localhost:11434",
  "ChatModel": "llama3.2:3b",
  "EmbeddingModel": "nomic-embed-text",
  "TimeoutSeconds": 100
}
```

> For 7B+ models, `TimeoutSeconds` of 180 or higher is recommended. Per-request inference time depends on available hardware.
</details>

---

## Test Case Format

Test cases are stored as a JSON array in `data/sample_test_cases.json` (configurable via `TestCasesPath`). The loader accepts both a bare array `[...]` and a wrapped format `{ "tests": [...] }`, and validates all required fields at startup with clear error messages.

### Fields

| Field | Required | Description |
|---|---|---|
| `id` | Yes | Unique identifier. Recommended format: `category_NNN` (e.g. `factual_021`). Used to label results in all reports. |
| `prompt` | Yes | The question or instruction sent verbatim to the LLM under test. |
| `expected_output` | Yes | The reference answer used by both validators. |
| `category` | — | Groups results in reports and the HTML dashboard. Defaults to `"general"` if omitted. |
| `evaluation_criteria` | — | Task-specific scoring guidance injected into the judge prompt. Especially useful for questions with many valid phrasings. |

### Example

```json
[
  {
    "id": "factual_021",
    "category": "factual",
    "prompt": "What is the capital of France?",
    "expected_output": "Paris is the capital of France.",
    "evaluation_criteria": "The response must identify Paris as the capital"
  },
  {
    "id": "reasoning_002",
    "category": "reasoning",
    "prompt": "A bat and a ball cost $1.10 in total. The bat costs $1.00 more than the ball. How much does the ball cost?",
    "expected_output": "5 cents",
    "evaluation_criteria": "Response must state 5 cents, not 10 cents"
  }
]
```

### Important: avoid single-word expected outputs

Writing `"expected_output": "Paris"` will produce cosine similarity scores of approximately 0.45 against a correct full-sentence response — well below the 0.85 threshold. This is a geometric property of embedding spaces, not a bug. There are two ways to handle it:

- Write the expected output as a full sentence: `"expected_output": "Paris is the capital of France."`
- Keep the short expected output but add `evaluation_criteria` so the judge path handles it: `"evaluation_criteria": "The response must identify Paris as the capital"`

The second option works because the judge evaluates meaning, not vector distance. With OR logic, the test passes on the judge path even when embedding similarity is low. The 138-case dataset in this repository includes both styles.

---

## Running the Evaluator

```bash
dotnet run
```

The console logs a startup banner confirming the active configuration, then prints one line per test in real time:

<img width="882" height="668" alt="OpenAI Console Output" src="https://github.com/user-attachments/assets/2223a45e-4050-4765-9cbd-6229885da4ec" />

*Fig 2: Console output OpenAI run*

### Understanding the four report formats

Each format serves a different purpose:

**`report.html`** is the primary deliverable. It opens in any browser and shows the overall pass rate, a per-category bar chart, and a per-test table where each row is colour-coded pass/fail. Expanding a row reveals all three runs — the model's actual response, the embedding score, the judge score, and the judge's full chain-of-thought reasoning. Any failure is immediately diagnosable without opening any other file.

**`report.json`** is the most information-dense output. It records the prompt, expected output, verdict, average scores, and the complete per-run array for every test case, including the judge's full reasoning text per run. This is the file consumed by the Provider Comparison Tool.

**`report.csv`** provides one flat row per test case with columns for test ID, category, pass/fail verdict, passed run count, average embedding score, and average judge score. It opens directly in Excel or LibreOffice Calc without any conversion step.

**`report.txt`** provides a quick plain-text summary for console-level review or CI log output.

If the browser does not open automatically (e.g. in a headless CI environment), open `reports/report.html` manually.

---

## Provider Comparison Tool

The repository includes a standalone browser-based tool (`provider-comparison-tool.html`) for comparing results across providers. Load two or three `report.json` files generated by the framework and it renders:

- **Overall summary panel** — pass rate, average embedding score, average judge score, and total test count per provider side by side. On the OpenAI vs Ollama comparison on the 50-case dataset, the headline gap (100% vs 82%) is immediately visible.
- **Pass rate by category** — horizontal bar chart per knowledge domain. If a gap is consistent across all categories, it confirms a provider-wide calibration problem rather than a subject-matter weakness. The OpenAI vs Ollama comparison shows exactly this.
- **Score distributions** — side-by-side histograms of judge scores and embedding scores in five buckets. A well-calibrated provider produces a right-skewed distribution concentrated at 9–10; a miscalibrated provider shows a significant cluster at 1–3 for factually correct answers that the judge scored wrong. The embedding histogram confirms low embedding scores are normal for both providers, validating the OR logic regardless of which provider is used.
- **Automatic recommendation** — names the provider with the highest overall pass rate and lists the categories where it leads.

Open `provider-comparison-tool.html` in any browser — no server or additional tooling required.

---

## Unit Tests

The test suite comprises **86 unit tests** and **6 integration tests** across 6 test classes, using MSTest.

```bash
# Run unit tests only (no API key required — all external dependencies are mocked)
dotnet test

# Run integration tests (requires LLM_API_KEY environment variable set to a valid OpenAI key)
dotnet test --filter "TestCategory=Integration"
```

### Unit test coverage

| Test class | Tests | What it covers |
|---|---|---|
| `CosineSimilarityCalculatorTests` | 16 | Geometric correctness, floating-point edge cases, zero-vector handling, threshold boundary conditions |
| `EmbeddingValidatorTests` | 6 | Threshold pass/fail logic, early exit for empty responses, safe handling of API exceptions — `IEmbeddingGenerator` and `ISimilarityCalculator` mocked with Moq |
| `LLMJudgeValidatorTests` | 8 | Score parsing across seven real judge response formats, `SCORE: N` primary pattern and integer fallback, reasoning extraction — uses a hand-written `FakeChatClient` test double |
| `JsonTestLoaderTests` | 14 | Bare array vs. wrapped format, correct field mapping, optional field defaults, fail-fast behaviour for malformed and semantically invalid inputs — reads real temporary files |
| `TestRunnerTests` | 10 | OR pass logic, majority-vote decision (2-of-3), score aggregation, graceful handling of per-run API exceptions |
| `ReportGeneratorTests` | 9 | All four output files created with correct content, category grouping, score formatting — writes to temporary directories |

`FakeChatClient` is used instead of Moq for `IChatClient` because Moq can interfere with extension methods layered on top of the interface. It manages a queue of pre-staged responses: each call to `GetResponseAsync` dequeues the next item, or throws it as an exception to simulate API failures. If the queue is exhausted unexpectedly, it throws `InvalidOperationException` with a clear message so tests fail with a meaningful explanation rather than a silent null result.

### Integration tests

The 6 integration tests run against real OpenAI endpoints to verify behaviour that mocks cannot simulate. They confirm that identical strings produce cosine similarity ≥ 0.95, that a correct full-sentence answer passes the threshold against a short expected output, that clearly unrelated strings fall below it, that a factually correct answer scores at least 7/10 from the judge, that a factually wrong answer fails the judge threshold, and that a complete end-to-end pipeline run produces a result with the correct structure. Integration tests are skipped automatically when `LLM_API_KEY` is not set in the environment.

---

## Project Structure

```
LLMSemanticEvaluator/
│
├── appsettings.json                  ← Your local config (do not commit — contains your API key)
├── appsettings_example.json          ← Template to copy; all fields with placeholder values
│
├── data/
│   ├── sample_test_cases.json        ← 138 test cases across 6 categories (primary dataset)
│   └── quick_tests.json              ← 50-case subset used for the Ollama evaluation run
│
├── reports/                          ← Generated after each run (auto-created if missing)
│   ├── report.txt
│   ├── report.json
│   ├── report.csv
│   └── report.html
│
├── provider-comparison-tool.html     ← Standalone browser tool for side-by-side report.json comparison
│
├── Program.cs                        ← Entry point; registers all services in the DI container
├── TestConfiguration.cs              ← Strongly-typed model for appsettings.json
├── LLMClientFactory.cs               ← Creates OpenAI, Grok, or Ollama client from config
├── EvaluatorService.cs               ← Hosted service: load → run → report lifecycle
├── JsonTestLoader.cs                 ← Loads and validates test cases from JSON
├── TestRunner.cs                     ← Repeats each test N times; applies OR logic and majority vote
├── EmbeddingValidator.cs             ← Calls IEmbeddingGenerator twice; delegates to CosineSimilarityCalculator
├── CosineSimilarityCalculator.cs     ← (A·B) / (‖A‖ × ‖B‖), clamped to [-1, 1], zero-vector safe
├── LLMJudgeValidator.cs              ← Builds G-Eval judge prompt; parses SCORE: N; stores reasoning
├── ReportGenerator.cs                ← Writes all four report formats; auto-opens report.html
└── ReportTemplate.html               ← HTML dashboard template with %%PLACEHOLDER%% tokens

LLMSemanticEvaluatorTests/
├── CosineSimilarityCalculatorTests.cs
├── EmbeddingValidatorTests.cs
├── JsonTestLoaderTests.cs
├── LLMJudgeValidatorTests.cs
├── ReportGeneratorTests.cs
├── TestRunnerTests.cs
└── IntegrationTests.cs           ← 6 tests against real OpenAI endpoints; skipped without LLM_API_KEY
```

---

## Troubleshooting

**Ollama tests are failing even though the model's answers look correct**

Open `reports/report.json` and look at the `JudgeReasoning` field for the failed runs. If you see coherent reasoning that *agrees* the answer is correct but the score is still 1 or 2, the model is experiencing judge miscalibration — it cannot reliably map its own reasoning to a numeric score. This is the documented failure mode of `llama3.2:3b` and smaller models (see [Key Results](#key-results)). Switch to a larger model (7B+ parameters) or lower `JudgeThreshold` in `appsettings.json` as a temporary workaround.

**HTTP 400 errors with OpenAI reasoning models**

Models in the `o1`, `o3`, and `gpt-5` family do not accept the `temperature` parameter and return HTTP 400 if it is set. The framework detects these model name prefixes in `SupportsTemperature()` and omits the parameter automatically. If you are using a newer model with a different naming convention, add its prefix to `SupportsTemperature()` in both `TestRunner.cs` and `LLMJudgeValidator.cs`.

**HTTP timeout errors with Ollama**

Each inference request on a local model can take 30–120 seconds depending on model size and available hardware. Increase `TimeoutSeconds` in `appsettings.json`. For `llama3.2:3b`, a value of `100` is a reasonable minimum; for 7B+ models, `180` or higher may be needed.

**All embedding scores are low (< 0.6) but tests are passing**

This is expected and correct. When `expected_output` values are short phrases like `"Paris"` or `"4"` and model responses are full sentences, cosine similarity is geometrically low even for semantically identical content. The judge path compensates. The average embedding score of 0.52 across the OpenAI run on 138 cases with a 99.3% pass rate confirms this is normal behaviour, not a problem.

---

## Further Reading

- [Results & Visualisation](results.md) — Full result tables, per-category breakdowns, and HTML dashboard screenshots for both OpenAI and Ollama runs
- [Output Folder](https://github.com/almahamud1234/llm-semantic-evaluator/tree/main/LLMSemanticEvaluator/reports)
