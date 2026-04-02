# Results & Visualisation

This document covers the experimental results of the LLM Semantic Evaluator across two evaluation runs — OpenAI `gpt-5-mini` on 138 test cases, and Ollama `llama3.2:3b` on a 50-case subset — along with screenshots of all generated outputs. Return to [README](../README.md) for setup and configuration.

---

## Table of Contents

1. [Experimental Setup](#experimental-setup)
2. [Console Output](#console-output)
3. [OpenAI Run — Overall Results](#openai-run--overall-results)
4. [OpenAI Run — HTML Dashboard](#openai-run--html-dashboard)
5. [OpenAI Run — Failure Analysis](#openai-run--failure-analysis)
6. [Why Embedding Scores Are Low Yet Pass Rate Is High](#why-embedding-scores-are-low-yet-pass-rate-is-high)
7. [Ollama Run — Overall Results](#ollama-run--overall-results)
8. [Ollama Run — HTML Dashboard](#ollama-run--html-dashboard)
9. [Ollama Run — Failure Analysis](#ollama-run--failure-analysis)
10. [Provider Comparison Tool](#provider-comparison-tool)
11. [Report Formats](#report-formats)

---

## Experimental Setup

Two evaluation runs were performed under the following configurations:

| Parameter | OpenAI Run | Ollama Run |
|---|---|---|
| Dataset | `sample_test_cases.json` | `quick_tests.json` |
| Test cases | 138 | 50 |
| Categories | factual, definitions, history, science, math, reasoning | same 6 |
| Chat model | `gpt-5-mini` | `llama3.2:3b` |
| Embedding model | `text-embedding-3-small` (1,536 dims) | `nomic-embed-text` (768 dims) |
| Temperature | model default | 0.0 |
| Runs per test | 3 | 3 |
| Min passing runs | 2 | 2 |
| Embedding threshold | 0.85 | 0.85 |
| Judge threshold | 8 / 10 | 8 / 10 |
| Timeout | 30 s | 100 s |

The Ollama dataset was deliberately limited to 50 cases because `llama3.2:3b` inference on the evaluation machine regularly exceeded the HTTP timeout at full scale. This is a hardware constraint, not a framework limitation.

---

## Console Output

During each run, the console prints a startup banner confirming the active configuration, then one line per test in real time showing the test index, ID, pass/fail verdict, and how many of the three runs passed.

<img width="882" height="668" alt="OpenAI Console Output" src="https://github.com/user-attachments/assets/f794d741-1975-4d6f-ad71-51b26f0a6cee" />

*Fig. 2: Console output during OpenAI run showing real-time test progress and results*

The real-time output allows an operator to spot unexpected failures immediately, without waiting for the full run to complete. Each line is written through `ILogger<T>` so the same output appears in Docker container logs and CI pipeline output.

---

## OpenAI Run — Overall Results

The primary evaluation ran `gpt-5-mini` on all 138 test cases across 6 knowledge domains.

| Metric | Value |
|---|---|
| Total test cases | 138 |
| Passed | 137 |
| Failed | 1 |
| **Pass rate** | **99.3%** |
| Avg embedding score | 0.52 |
| Avg judge score | 10 / 10 |

### Category breakdown

| Category | Total | Passed | Failed | Pass rate |
|---|---|---|---|---|
| factual | — | — | — | 100% |
| definitions | — | — | — | 100% |
| history | — | — | 1 | < 100% |
| science | — | — | — | 100% |
| math | — | — | — | 100% |
| reasoning | — | — | — | 100% |

> Fill in the exact per-category counts once you have run the framework and have `report.csv` open. The 5 categories listed at 100% all achieved perfect scores; only history had 1 failure. The failur is explain later in OpenAI Failur section.

---

## OpenAI Run — HTML Dashboard

The HTML dashboard (`reports/report.html`) opens automatically in your browser after each run. It presents the overall pass rate, average scores, a per-category bar chart, and a per-test expandable table.

<img width="936" height="552" alt="OpenAI Dashboard" src="https://github.com/user-attachments/assets/e151b055-a12b-4124-bf2b-abe56d5c8d78" />

*Fig. 4: HTML report dashboard — OpenAI run (report.html)*

The header row of metric cards shows pass rate, average embedding score, average judge score, and the configuration used (provider, thresholds, run count). The colour of each metric card reflects its health: green for pass rates above ~90%, amber for lower values. This makes it immediately visible at a glance whether a run was successful before reading any individual test results.

The per-category bar chart below the header breaks pass rates down by knowledge domain. For the OpenAI run, all bars reach 100% except history, which shows the single failure.

<img width="766" height="424" alt="image" src="https://github.com/user-attachments/assets/45c7edb6-b72e-46dd-aa36-b9be6a68110f" />

*Fig. 5: Per category pass rate — OpenAI run (report.html)*

Expanding any row in the per-test array of json file reveals all three individual runs for that test case — the model's actual response text, the embedding score, the judge score, and the judge's full chain-of-thought reasoning. This makes any failure diagnosable without opening any other file.

<img width="1214" height="758" alt="image" src="https://github.com/user-attachments/assets/4e4c36a7-7470-4a55-b0f3-c4807331f051" />

*Fig. 6: Per test response — OpenAI run (report.json)*
---

## OpenAI Run — Failure Analysis

One test case failed in the OpenAI run. Test `history_006` asks: *"Which empire was Julius Caesar a part of?"* The expected output is `"The Roman Empire"`. The model responded that Caesar was a leading general and statesman of the late Roman Republic — not the Roman Empire — and that his actions helped pave the way for the Empire under his adopted heir Octavian (Augustus).

This is historically correct. Caesar was assassinated in 44 BC, and the Roman Empire is conventionally dated from 27 BC. Both validators failed because the model's response directly contradicts the expected output, even though the model is right. One of the three runs passed (judge score 8/10, acknowledging the historical nuance); the other two scored 7 and 2, leaving the test with 1 of 3 passing runs — one short of the majority threshold.

The framework behaves correctly here: it surfaces the disagreement and stores the judge's full reasoning in `report.json` so a developer can inspect the case. Correcting the expected output to `"The Roman Republic"` would bring the OpenAI pass rate to 100%.

<img width="1233" height="807" alt="image" src="https://github.com/user-attachments/assets/89e6ba30-5077-4636-95fa-b2a9d7ca1cd7" />

*Fig. 7: OpenAI failure — model answer historically more accurate than expected output*

---

## Why Embedding Scores Are Low Yet Pass Rate Is High

The average embedding score across the OpenAI run is 0.52 — well below the 0.85 threshold — yet the pass rate is 99.3%. This apparent contradiction is explained by the OR logic between the two validators.

When `expected_output` is a short phrase like `"Paris"` or `"4"` and the model responds with a full sentence like `"The capital of France is Paris."`, cosine similarity is geometrically low (~0.45) even though the answer is correct. This is a structural property of embedding spaces at different text lengths. Only 6 of the 414 individual runs (138 tests × 3 runs) reached the 0.85 threshold. The remaining passes were all driven by the judge path, which correctly scored the responses 10/10 by evaluating meaning rather than vector distance.

<img width="1146" height="260" alt="image" src="https://github.com/user-attachments/assets/8249822e-c5af-4b52-8593-266c30b028f9" />

*Fig. 8: Test passes via judge path despite low embedding score (embedding: 0.45, judge: 10/10)*

This confirms that removing either validator would break the framework. A pure embedding approach would fail roughly even for a model answering correctly. A pure judge approach would be vulnerable to small-model miscalibration. The OR combination is structurally necessary, not a workaround.

---

## Ollama Run — Overall Results

The second evaluation ran `llama3.2:3b` locally on a 50-case subset covering the same 6 knowledge domains.

| Metric | Value |
|---|---|
| Total test cases | 50 |
| Passed | 41 |
| Failed | 9 |
| **Pass rate** | **82%** |
| Avg embedding score | 0.67 |
| Avg judge score | 7.8 / 10 |

---

## Ollama Run — HTML Dashboard

<img width="944" height="545" alt="Ollama Dashboard" src="https://github.com/user-attachments/assets/7d032073-f6d7-49c6-92c3-c3f0b5aa3c2d" />

*Fig. 9: HTML report dashboard — Ollama run (report.html)*

The Ollama dashboard is visually distinct from the OpenAI run. The average judge score card shows 7.8/10 rather than 10/10, and the pass rate card shows 82% rather than 99.3%. The per-category bar chart shows a consistent shortfall across all 6 domains — the gap is not concentrated in any one subject area, which confirms this is a provider-wide calibration problem rather than a subject-matter weakness of the model.

---

## Ollama Run — Failure Analysis

Nine test cases failed in the Ollama run. Three factors contribute:

**1. Hardware-imposed model constraint.** The evaluation machine could not run models larger than `llama3.2:3b` without exceeding the 100-second HTTP timeout. Larger models — which would be more capable judge models — were not feasible.

**2. Reduced dataset.** For the same hardware reason, the dataset was limited to 50 cases. The full 138-case run caused timeouts.

**3. Systematic judge miscalibration.** The most significant and directly observable factor. `llama3.2:3b` frequently assigns low numeric scores to responses its own chain-of-thought reasoning identifies as correct.

The clearest example is `math_001`: prompt *"What is 2 + 2?"*, expected output `"4"`, model response *"2 + 2 = 4."* The judge's own reasoning across all three runs stated: *"The actual answer correctly addresses the query. It captures the same meaning as the expected answer. There are no factual errors or key omissions. The actual answer is semantically identical to the expected answer."* Despite this, the judge assigned a score of 1/10 on all three runs. The embedding score of 0.77 was also below the 0.85 threshold, so both validators failed and the test was marked failed.

<img width="1229" height="779" alt="image" src="https://github.com/user-attachments/assets/0b9ac90e-6d88-4b50-993f-043615f011ea" />

*Fig. 10: Ollama miscalibration — judge reasoning states the answer is correct but assigns 1/10*

This failure mode is diagnosable directly from `report.json`: open the file, find the failed test, and read the `JudgeReasoning` field. If the reasoning agrees the answer is correct but the score is low, the model is miscalibrated. This is the recommended first diagnostic step for any unexpected Ollama failure.

**Recommendation:** For Ollama deployments, use a model of at least 7–8 billion parameters for the judge role. Alternatively, configure `Provider` as Ollama for the tested model while using an OpenAI API key for embeddings and judging — the `Provider` and `EmbeddingProvider` settings are independent for exactly this reason.

---

## Provider Comparison Tool

The repository includes `provider-comparison-tool.html`, a standalone browser-based tool for comparing two or three `report.json` files side by side. Open the file in any browser — no server or additional tooling required — and load the report files.

### Overall summary panel

The headline comparison across all metrics for each provider, displayed side by side.

<img width="734" height="500" alt="Overall Summary" src="https://github.com/user-attachments/assets/6cbe6ad6-1436-4f81-bf72-85db76ffd747" />

*Fig. 11: Overall summary comparison — OpenAI vs Ollama on 50-case dataset*

On the same 50 cases, OpenAI passes all 50 while Ollama fails 9. The average judge score difference (10/10 vs 7.8/10) is the primary driver of this gap.

### Pass rate by category

A horizontal bar chart per knowledge domain, one bar per provider. If the gap is consistent across all categories, it is a provider-wide calibration problem, not a subject-matter weakness.

<img width="922" height="554" alt="Pass rate by category" src="https://github.com/user-attachments/assets/4c82f34b-c371-4564-bbf9-bc1f65e9ead5" />

*Fig. 12: Pass rate by category — OpenAI vs Ollama*

The consistent shortfall across all 6 categories confirms that Ollama's lower pass rate is a provider-wide calibration issue rather than a subject-matter weakness.

### Score distributions

Side-by-side histograms of judge scores and embedding scores, each bucketed into five ranges. A well-calibrated provider produces a right-skewed distribution concentrated at 9–10. A miscalibrated provider shows a significant cluster at 1–3 — correct answers that the judge scored wrong.

<img width="1524" height="451" alt="Score Comparison" src="https://github.com/user-attachments/assets/7001aedd-9945-4f60-a490-50f9409ea0a7" />

*Fig. 10: Score distributions — judge scores (left) and embedding scores (right)*

The embedding score histogram confirms that low embedding scores are normal for both providers — validating that the OR logic between the two validators is necessary regardless of which provider is used.

---

## Report Formats

All four report formats are generated automatically after each run and saved to the `reports/` directory.

### report.html — Interactive dashboard

The primary deliverable. Opens in any browser. Includes metric cards, a per-category bar chart, and a per-test expandable table with full per-run detail. Screenshots of the full dashboard are shown in the [OpenAI HTML Dashboard](#openai-run--html-dashboard) and [Ollama HTML Dashboard](#ollama-run--html-dashboard) sections above.

### report.json — Structured data

The most information-dense output. Records the full per-run array for every test case, including the model's response, embedding score, judge score, and complete judge reasoning text. Used as the input file for the Provider Comparison Tool. 

### report.csv — Flat data for spreadsheets

One row per test case. Columns: `TestId`, `Category`, `Passed`, `PassedRuns`, `TotalRuns`, `AverageEmbeddingScore`, `AverageJudgeScore`. Opens directly in Excel or LibreOffice Calc without any conversion step, enabling sorting by score, filtering by category, or generating custom charts.

### report.txt — Plain text summary

A quick human-readable summary showing overall pass rate, category breakdown, and per-test verdict. Useful for CI pipeline log output or quick console review.

---
