# ML 25/26-08 – Implementing Tests for LLM Prompts with Semantic Assertions

A **.NET 8 / C#** testing framework for evaluating LLM prompts using **semantic assertions** rather than exact string matching. It handles the inherent non-determinism of LLM outputs by applying two independent validation methods — embedding-based cosine similarity and a G-Eval style LLM-as-a-Judge — combined with a majority-vote strategy across repeated test runs.

Supports **OpenAI GPT**, **xAI Grok**, and **locally hosted Ollama** models with no code changes needed to switch between them.

---

## Table of Contents

1. [Abstract](#abstract)
2. [Features](#features)
3. [System Requirements](#system-requirements)
4. [Architecture](#architecture)
5. [Installation](#installation)
   - [Prerequisites](#prerequisites)
   - [Setup Steps](#setup-steps)
   - [Ollama Setup](#ollama-setup)
6. [Configuration](#configuration)
7. [Test Case Format](#test-case-format)
8. [Usage](#usage)
9. [Results & Visualisation](docs/results.md)
10. [Unit Testing](docs/unit-testing.md)
11. [Project Structure](#project-structure)

---

## Abstract

Traditional software testing asserts correctness through exact string equality — a strategy that fails entirely for Large Language Model outputs. An LLM responding to *"What is the capital of France?"* may answer *"Paris"*, *"The capital of France is Paris"*, or *"Paris serves as France's capital city"*. All three are semantically correct; exact matching would only accept one.

This project implements the LLM Semantic Evaluator, a .NET/C# framework that replaces lexical matching with two semantic validation approaches required by ML project 25/26-08:

- **Approach 1 — Embedding Cosine Similarity:** Both the expected and actual outputs are converted to high-dimensional embedding vectors. The cosine similarity between them is measured. A run passes if `similarity ≥ EmbeddingThreshold` (default 0.85).
- **Approach 2 — LLM-as-a-Judge (G-Eval style):** A secondary LLM reasons step-by-step about the response before assigning a score from 1–10. A run passes if `score ≥ JudgeThreshold` (default 8).

A run passes if **either** validator succeeds (logical OR). A test case passes overall if at least **2 out of 3 runs** pass (configurable majority vote). Evaluated on 130 test cases, the framework achieved **97.7% pass rate** with OpenAI `gpt-4o-mini` and **44.6%** with Ollama `llama3.2:1b` — with the gap explained by judge model miscalibration rather than factual response errors.

---

## Features

- **Two semantic validation methods** — cosine similarity on embeddings and G-Eval style LLM judge
- **Hybrid OR logic** — a run passes if either validator succeeds, compensating for short-expected-output cases where embedding similarity is structurally low
- **Majority-vote aggregation** — configurable runs per test (default 3) with a configurable pass threshold (default 2/3)
- **Multi-provider support** — OpenAI, xAI Grok (OpenAI-compatible REST API), and local Ollama (no API key or internet required)
- **JSON test case loader** — accepts bare arrays or `{ "tests": [...] }` wrapped format, with full validation
- **Four report formats** — plain text, JSON (with per-run judge reasoning), CSV, and an interactive HTML dashboard
- **HTML dashboard** — auto-opens in browser with metric cards, score distribution charts, category breakdown, and a per-test heatmap table
- **Zero-friction provider switching** — change `Provider` in `appsettings.json`, no code changes required
- **Robust error handling** — API timeouts are caught per-run, counted as failures, and the suite continues
- **85 unit tests across 6 groups** — file handling, cosine similarity, embedding validation, LLM judge validation, report generation, and test runner logic; all external dependencies are mocked with Moq so the full suite runs in under 5 seconds without any API key or internet connection
---

## System Requirements

| Requirement | Minimum |
|---|---|
| Operating System | Windows 10+ or macOS 10.15+ |
| .NET SDK | 10.0 |
| Memory | 4 GB RAM (8 GB+ recommended for Ollama models) |
| Internet | Required for OpenAI / Grok; not required for Ollama |
| API Access | OpenAI or Grok API key **or** a local Ollama installation |


Note: .NET 10 (the current LTS release) was chosen for its modern async runtime, and long-term Microsoft support, making it suitable for a production-grade evaluation framework.

---

## Architecture

The application follows a linear pipeline architecture. Each stage is a dedicated C# class communicating through a well-defined interface, keeping components independently testable and replaceable.

<img width="952" height="778" alt="system-architecture-design" src="https://github.com/user-attachments/assets/055a1145-2c98-4293-bca2-f9e6bcd2e4a6" />

*Fig 1: System Architecture Diagram*

**Interface contracts — each can be replaced without modifying other components:**

| Interface | Purpose | Implemented by |
|---|---|---|
| `ILLMClient` | Send a prompt, receive a response string | `OpenAIClient`, `OllamaClient` |
| `IEmbeddingProvider` | Generate a `float[]` embedding vector for a text | `OpenAIClient`, `OllamaClient` |
| `ISimilarityCalculator` | Compute similarity between two vectors | `CosineSimilarityCalculator` |
| `ITestLoader` | Load a `List<TestCase>` from a source | `JsonTestLoader` |

---

## Installation

### Prerequisites

1. **Install .NET 10.0 SDK**

   Download from the [Microsoft .NET website](https://dotnet.microsoft.com/download/dotnet/10.0) and verify:
   ```bash
   dotnet --version
   # Expected: 10.0.x
   ```

2. **Choose your LLM provider:**
   - **OpenAI** — create an API key at [platform.openai.com/api-keys](https://platform.openai.com/api-keys)
   - **Grok (xAI)** — create an API key at [console.x.ai](https://console.x.ai)
   - **Ollama** — free, fully local — see [Ollama Setup](#ollama-setup) below

---

### Setup Steps

1. **Clone the repository**
   ```bash
   git clone https://github.com/almahamud1234/llm-semantic-evaluator
   cd LLMSemanticEvaluator
   ```

2. **Create your configuration file**
   ```bash
   cp appsettings_example.json appsettings.json
   ```
   Edit `appsettings.json` with your API key and settings. See [Configuration](#configuration) for all fields.

3. **Restore and build**
   ```bash
   dotnet restore
   dotnet build
   ```

4. **Run**
   ```bash
   dotnet run
   ```
   Test cases load from `data/sample_test_cases.json`, all tests run, reports are saved to `reports/`, and the HTML dashboard opens automatically in your browser.

---

### Ollama Setup (Local Models)

Ollama runs LLMs entirely on your machine — no API key, no usage costs, no internet required after the initial download. The recommended way to run Ollama for this project is via Docker Compose, which is already included in the repository.

1. **Start the Ollama container**
   ```bash
   docker compose up
   ```

2. **Pull the required models into the container**
   ```bash
   docker exec -it ollama ollama pull llama3.2:1b       # chat model (also used as the judge)
   docker exec -it ollama ollama pull nomic-embed-text  # embedding model
   ```

   > **Tip:** `llama3.2:1b` is too small to be a reliable judge. See [Results](docs/results.md) for details. For better judge accuracy prefer `llama3.2:3b` or `llama3:8b`.

3. **Verify the models are available**
   ```bash
   docker exec -it ollama ollama list
   ```
   You should see both llama3.2:1b and nomic-embed-text listed.

4. **Update `appsettings.json`**
   ```json
   {
     "Provider": "ollama",
     "EmbeddingProvider": "ollama",
     "ChatModel": "llama3.2:1b",
     "EmbeddingModel": "nomic-embed-text",
     "OllamaBaseUrl": "http://localhost:11434"
   }
   ```

---

## Configuration

All settings live in `appsettings.json`. Copy `appsettings_example.json` as a starting point and **never commit a file containing a real API key** to version control.

```json
{
  "Provider": "openai",
  "EmbeddingProvider": "openai",
  "OpenAIApiKey": "YOUR_OPENAI_API_KEY_HERE",
  "GrokApiKey": "YOUR_GROK_API_KEY_HERE",
  "OllamaBaseUrl": "http://localhost:11434",
  "ChatModel": "gpt-4o-mini",
  "EmbeddingModel": "text-embedding-3-small",
  "Temperature": 0.0,
  "EmbeddingThreshold": 0.85,
  "JudgeThreshold": 8,
  "NumberOfRuns": 3,
  "MinimumPassingRuns": 2,
  "TimeoutSeconds": 30,
  "RequestDelayMs": 200
}
```

| Field | Type | Default | Description |
|---|---|---|---|
| `Provider` | string | `openai` | Chat LLM provider: `openai` \| `grok` \| `ollama` |
| `EmbeddingProvider` | string | `openai` | Embedding provider: `openai` \| `ollama`. **Grok not supported.** |
| `OpenAIApiKey` | string | — | Required when Provider or EmbeddingProvider is `openai`. |
| `GrokApiKey` | string | — | Required when Provider is `grok`. |
| `OllamaBaseUrl` | string | `http://localhost:11434` | Base URL of your running Ollama instance. |
| `ChatModel` | string | `gpt-5-mini` | Model name sent with every chat completion request. |
| `EmbeddingModel` | string | `text-embedding-3-small` | Model used to generate embedding vectors. |
| `Temperature` | double | `0.0` | Sampling temperature. `0.0` = near-deterministic. Strongly recommended for testing. |
| `EmbeddingThreshold` | double | `0.85` | Minimum cosine similarity (0.0–1.0) for the embedding validator to pass a run. |
| `JudgeThreshold` | int | `8` | Minimum judge score (1–10) for the judge validator to pass a run. |
| `NumberOfRuns` | int | `3` | How many times each test case is executed. |
| `MinimumPassingRuns` | int | `2` | Passing runs needed for the overall test to pass (majority vote). |
| `TimeoutSeconds` | int | `30` | HTTP request timeout per API call. Increase for slow local models. |
| `RequestDelayMs` | int | `200` | Delay (ms) between requests — rate-limit guard. |

### Quick-start configs per provider

<details>
<summary><strong>OpenAI</strong></summary>

```json
{
  "Provider": "openai",
  "EmbeddingProvider": "openai",
  "OpenAIApiKey": "sk-...",
  "ChatModel": "gpt-5-mini",
  "EmbeddingModel": "text-embedding-3-small"
}
```
</details>

<details>
<summary><strong>Grok (xAI)</strong></summary>

```json
{
  "Provider": "grok",
  "EmbeddingProvider": "openai",
  "GrokApiKey": "xai-...",
  "OpenAIApiKey": "sk-...",
  "ChatModel": "grok-3-mini",
  "EmbeddingModel": "text-embedding-3-small"
}
```
> Grok shares the OpenAI REST API shape. Because xAI does not provide an embeddings endpoint, supply an OpenAI key for embeddings alongside your Grok key.
</details>

<details>
<summary><strong>Ollama (fully local)</strong></summary>

```json
{
  "Provider": "ollama",
  "EmbeddingProvider": "ollama",
  "OllamaBaseUrl": "http://localhost:11434",
  "ChatModel": "llama3.2:1b",
  "EmbeddingModel": "nomic-embed-text",
  "TimeoutSeconds": 60
}
```
> Increase `TimeoutSeconds` for larger models. 7B+ parameter models on CPU can take 20–40 seconds per response.
</details>

---

## Test Case Format

Test cases are stored in `data/sample_test_cases.json`. The loader accepts both a bare JSON array and a `{ "tests": [...] }` wrapped format, and validates all required fields at startup.

### Schema

| Field | Required | Description |
|---|---|---|
| `id` | Yes | Unique identifier. Recommended format: `category_NNN` (e.g. `factual_001`). |
| `prompt` | Yes | The query sent verbatim to the LLM under test. |
| `expected_output` | Yes | The reference answer used by both validators. |
| `category` | — | Label for report grouping. Defaults to `general` if omitted. |
| `evaluation_criteria` | — | Custom scoring guidance injected into the judge prompt. |

### Example

```json
[
  {
    "id": "factual_021",
    "category": "factual",
    "prompt": "What is the capital of France?",
    "expected_output": "Paris",
    "evaluation_criteria": "The response must correctly identify Paris as the capital"
  },
  {
    "id": "reasoning_006",
    "category": "reasoning",
    "prompt": "How can you measure exactly 4 litres using a 3L and a 5L jug?",
    "expected_output": "Fill the 5L jug. Pour into the 3L jug until full — 2L remain. Empty the 3L jug. Pour the 2L into it. Refill the 5L jug. Pour until the 3L jug is full — 1L fits, leaving 4L in the 5L jug.",
    "evaluation_criteria": "The response must describe valid steps that correctly arrive at exactly 4 litres."
  },
  {
    "id": "math_012",
    "category": "math",
    "prompt": "What is 15% of 240?",
    "expected_output": "36",
    "evaluation_criteria": "The response must calculate the correct percentage"
  }
]
```

### Tips for writing effective test cases

- **Avoid single-word expected outputs** like `"Paris"` or `"36"`. A single word produces cosine similarity in the range 0.30–0.55 against a correct full-sentence response — far below the 0.85 threshold. Use a full sentence as the expected output or add an `evaluation_criteria` to guide the judge instead.
- **Use `evaluation_criteria`** for complex questions where many valid phrasings exist — reasoning tasks, open-ended explanations, or multi-step problems.
- **Use descriptive IDs** with a category prefix (`factual_001`, `history_020`) so reports group and label results automatically.

---

## Usage

### Running the evaluator

```bash
dotnet run
```

### Console output

During the run the console logs real-time progress. Each line shows the test index, ID, result, and how many of the three runs passed:

<img width="882" height="668" alt="OpenAI Console Output" src="https://github.com/user-attachments/assets/996a5042-d24e-4d75-9c92-898deff5601b" />

Fig 2: Console output for OpenAI run*

### How the pass / fail logic works

```
For each run (repeated NumberOfRuns times):
  EmbeddingScore >= EmbeddingThreshold  →  EmbeddingPassed = true
  JudgeScore     >= JudgeThreshold      →  JudgePassed     = true
  RunPassed      =  EmbeddingPassed  OR  JudgePassed

After all runs:
  TestPassed = (PassedRunsCount >= MinimumPassingRuns)
```

The OR combination means a test passes if the embedding similarity is high **or** the judge considers the response correct. This is critical for test cases with short expected outputs where embedding similarity is structurally low even for correct responses. In the OpenAI run, nearly all 127 passes are driven by the judge path — average embedding score was 0.51, well below the 0.85 threshold.

---

## Project Structure

```
LLMSemanticEvaluator/
│
├── appsettings.json              ← Your local config (never commit this)
├── appsettings_example.json      ← Template — copy and rename
│
├── data/
│   └── sample_test_cases.json   ← 130 test cases across 7 categories
│
├── reports/                     ← Generated after each run (auto-created)
│   ├── report.txt
│   ├── report.json
│   ├── report.csv
│   └── report.html
│
├── Program.cs                   ← Entry point — wires all components
├── TestConfiguration.cs         ← Deserialises appsettings.json + Validate()
├── LLMClientFactory.cs          ← Factory: creates OpenAIClient or OllamaClient
├── OpenAIClient.cs              ← ILLMClient + IEmbeddingProvider for OpenAI/Grok
├── OllamaClient.cs              ← ILLMClient + IEmbeddingProvider for local Ollama
├── JsonTestLoader.cs            ← Loads and validates test cases from JSON
├── TestRunner.cs                ← Orchestrates runs, majority vote, aggregation
├── EmbeddingValidator.cs        ← Embedding cosine similarity validation
├── CosineSimilarityCalculator.cs← (A·B)/(‖A‖×‖B‖), clamped, edge-case safe
├── LLMJudgeValidator.cs         ← G-Eval prompt builder + 2-stage score parser
├── ReportGenerator.cs           ← Writes .txt / .json / .csv / .html reports
├── ReportTemplate.html          ← HTML dashboard template (%%PLACEHOLDER%% tokens)

LLMSemanticEvaluatorTests/
├── CosineSimilarityCalculatorTests.cs
├── EmbeddingValidatorTests.cs
├── JsonTestLoaderTests.cs
├── LLMJudgeValidatorTests.cs
├── ReportGeneratorTests.cs
└── TestRunnerTests.cs
```

---

**[View Full Results & Visualisation →](results.md)**

**[View Unit Testing Documentation →](unit-testing.md)**

---
