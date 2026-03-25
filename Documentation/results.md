# Results & Visualisation

This page documents the full experimental results from running the LLM Semantic Evaluator on a **130-case dataset** across seven knowledge domains, using two provider configurations.

[Back to README](../README.md)

---

## Table of Contents

1. [Experimental Setup](#experimental-setup)
2. [Overall Results](#overall-results)
3. [HTML Dashboard — OpenAI Run](#html-dashboard--openai-run)
4. [HTML Dashboard — Ollama Run](#html-dashboard--ollama-run)
5. [Per-Category Breakdown](#per-category-breakdown)
6. [JSON Report Deep-Dive](#json-report-deep-dive)
   - [Passing Test with Low Embedding Score](#passing-test-with-low-embedding-score)
   - [OpenAI Failure Cases](#openai-failure-cases)
   - [Ollama Judge Miscalibration](#ollama-judge-miscalibration)
7. [Key Takeaways](#key-takeaways)

---

## Experimental Setup

| Parameter | OpenAI Run | Ollama Run |
|---|---|---|
| Chat model | `gpt-5-mini` | `llama3.2:1b` |
| Embedding model | `text-embedding-3-small` | `nomic-embed-text` |
| Embedding dimensions | 1,536 | 768 |
| Temperature | 0.0 | 0.0 |
| Runs per test | 3 | 3 |
| Minimum passing runs | 2 | 2 |
| Embedding threshold | 0.85 | 0.85 |
| Judge threshold | 8 | 8 |
| Total test cases | 130 | 130 |

---

## Overall Results

| Metric | OpenAI (`gpt-5-mini`) | Ollama (`llama3.2:1b`) |
|---|---|---|
| Total tests | 130 | 130 |
| Passed | **127** | 58 |
| Failed | 3 | **72** |
| Pass rate | **97.7%** | 44.6% |
| Avg embedding score | 0.51 | 0.64 |
| Avg judge score | **9.7 / 10** | 5.8 / 10 |

The 53-point gap between the two runs is not caused by factual errors in Ollama's responses. It is caused by **judge miscalibration** in `llama3.2:1b` — see [Ollama Judge Miscalibration](#ollama-judge-miscalibration) below.

---

## HTML Dashboard — OpenAI Run

The HTML report opens automatically in the browser after every run. The metric cards at the top give an at-a-glance summary.

*Fig 1: OpenAI run — overview metric cards (130 tests · 97.7% pass rate · avg judge score 9.7/10)*
![OpenAI report header](Images/Fig 5 OpenAI Report Overview.png)

Notice that the average embedding score (0.51) is flagged **amber** (below the 0.85 threshold), while the average judge score (9.7/10) is **green**. This confirms that nearly all 127 passing tests passed via the judge path rather than the embedding path — a direct consequence of the short-expected-output problem (see [Key Takeaways](#key-takeaways)).

*Fig 2: OpenAI run — score distribution and category pass-rate charts*
![OpenAI charts](images/openai_charts.png)

The left chart shows the embedding score distribution: most runs cluster in the 0.30–0.60 band, confirming that few tests pass via the embedding validator alone. The right chart shows the per-category pass rate — math and reasoning hit 100%, while factual, history, and science each record one failure.

*Fig 3: OpenAI run — per-test heatmap table (top rows)*
![OpenAI results table](images/openai_table.png)

Each row shows one test case. The three columns under "Embedding score / run" and "Judge score / run" are colour-coded: green cells meet the threshold, red cells do not. The "Status" column reflects the majority-vote outcome.

---

## HTML Dashboard — Ollama Run

*Fig 4: Ollama run — overview metric cards (130 tests · 44.6% pass rate · avg judge score 5.8/10)*
![Ollama report header](images/ollama_header.png)

Both the embedding score (0.64) and judge score (5.8/10) are flagged amber. The judge score being below the threshold (8/10) is the dominant cause of failure — the embedding score of 0.64 is actually *higher* than the OpenAI run (0.51), which is discussed below.

*Fig 5: Ollama run — score distribution and category pass-rate charts*
![Ollama charts](images/ollama_charts.png)

The category chart shows a stark difference from the OpenAI run: definitions and history both sit at around 25%, while math (65.2%) and reasoning (59.1%) are the strongest categories. The bimodal judge score pattern — many low scores and some high scores, with a gap in the middle — is the visual signature of miscalibration.

---

## Per-Category Breakdown

| Category | N | OpenAI Pass | OpenAI % | Ollama Pass | Ollama % |
|---|---|---|---|---|---|
| definition | 2 | 2 | **100%** | 1 | 50.0% |
| definitions | 20 | 20 | **100%** | 5 | 25.0% |
| factual | 23 | 22 | 95.7% | 9 | 39.1% |
| history | 20 | 19 | 95.0% | 5 | 25.0% |
| math | 23 | 23 | **100%** | 15 | 65.2% |
| reasoning | 22 | 22 | **100%** | 13 | 59.1% |
| science | 20 | 19 | 95.0% | 10 | 50.0% |

---

## JSON Report Deep-Dive

The JSON report (`reports/report.json`) stores the most granular data: every run's LLM response, embedding score, judge score, judge reasoning text, and per-validator pass/fail flags.

### Passing Test with Low Embedding Score

The test case below illustrates why the OR logic is essential. The expected output is the single word `"Paris"`. The actual response is the sentence `"The capital of France is Paris."` — semantically identical, but the embedding score is only **0.45**, well below the 0.85 threshold. The judge correctly scores it **10/10** in all three runs, so the test passes via the judge path.

```json
{
  "testId": "factual_021",
  "category": "factual",
  "prompt": "What is the capital of France?",
  "expectedOutput": "Paris",
  "passed": true,
  "passedRuns": 3,
  "totalRuns": 3,
  "avgEmbeddingScore": 0.45,
  "avgJudgeScore": 10.0,
  "runs": [
    {
      "runNumber": 1,
      "passed": true,
      "embeddingScore": 0.45,
      "judgeScore": 10,
      "embeddingPassed": false,
      "judgePassed": true,
      "judgeReasoning": "The actual answer correctly and directly addresses the query by
        naming Paris as the capital of France. It captures the same meaning as the
        expected answer with no factual errors, omissions, or irrelevant content.
        The phrasing is slightly more formal but semantically identical.",
      "response": "The capital of France is Paris."
    }
  ]
}
```

This pattern — low embedding score, high judge score, overall PASS — applies to the majority of the 127 passing OpenAI tests.

---

### OpenAI Failure Cases

Only 3 tests failed in the OpenAI run. In all three, the model produced a response that is **more scientifically accurate** than the expected output.

**`history_020` — Circumnavigation of the globe**

```json
{
  "testId": "history_020",
  "prompt": "Who was the first person to circumnavigate the globe?",
  "expectedOutput": "Ferdinand Magellan",
  "passed": false,
  "passedRuns": 0,
  "avgEmbeddingScore": 0.56,
  "avgJudgeScore": 2.3,
  "runs": [
    {
      "runNumber": 1,
      "embeddingScore": 0.57,
      "judgeScore": 2,
      "response": "The first person to complete a circumnavigation was Juan Sebastián Elcano.
        He took command of Ferdinand Magellan's expedition after Magellan was killed
        in the Philippines and brought the ship Victoria back to Spain on 6 September
        1522. Magellan led the expedition but did not survive to finish it.",
      "judgeReasoning": "The actual answer names Juan Sebastián Elcano as the first to
        complete a circumnavigation and notes Magellan led the expedition but was killed
        before finishing. This does not match the expected answer (Ferdinand Magellan).
        Although the response gives historically accurate nuance, it fails the specific
        evaluation criterion to identify Ferdinand Magellan."
    }
  ]
}
```

The model is historically correct: Elcano completed the voyage, not Magellan. The expected output in the test case is the popular (but imprecise) answer.

**`factual_014` — World's largest desert**

The model correctly identified the Antarctic Desert (~14 million km²) as the world's largest desert by area, while the expected output listed the Sahara (~9.2 million km²). Deserts are defined by low precipitation rather than temperature, making Antarctica a desert. The expected output reflects common knowledge rather than strict geographic accuracy.

**`science_013` — Smallest unit of matter**

The model correctly answered *elementary particles* (quarks and leptons per the Standard Model), while the expected output was *an atom*. The model's response is scientifically accurate at the particle physics level; the expected output reflects a chemistry/classical physics perspective.

---

### Ollama Judge Miscalibration

The most striking finding from the Ollama run is systematic judge miscalibration. The judge (`llama3.2:1b`) repeatedly assigned scores of 1/10 to factually correct, well-structured responses while citing errors that are not present in the actual text.

**`factual_022` — Who wrote Romeo and Juliet?**

```json
{
  "testId": "factual_022",
  "prompt": "Who wrote Romeo and Juliet?",
  "expectedOutput": "William Shakespeare",
  "passed": false,
  "passedRuns": 0,
  "avgEmbeddingScore": 0.62,
  "avgJudgeScore": 1.0,
  "runs": [
    {
      "runNumber": 1,
      "embeddingScore": 0.62,
      "judgeScore": 1,
      "response": "Romeo and Juliet is a tragic play written by the English playwright
        William Shakespeare. It is one of his most famous works and is considered a
        classic of world literature.",
      "judgeReasoning": "The actual answer provided does not correctly identify
        Shakespeare as the author of Romeo and Juliet. The response states that it is
        'one of his most famous works' without specifying the playwright's name, which
        is incorrect. Additionally, the response omits the crucial detail that Romeo
        and Juliet was written by William Shakespeare."
    }
  ]
}
```

The judge claims the response does not identify Shakespeare — yet the response explicitly states *"written by the English playwright William Shakespeare"* in the first sentence. The judge's reasoning contradicts the literal text of the response. This is the clearest example of hallucination-in-evaluation from the Ollama run.

**`definition_014` — What is DNA?**

```json
{
  "testId": "definition_014",
  "prompt": "What is DNA?",
  "expectedOutput": "Deoxyribonucleic acid, a molecule that carries the genetic
    instructions for the development and functioning of living organisms",
  "passed": false,
  "avgEmbeddingScore": 0.55,
  "avgJudgeScore": 0.7,
  "runs": [
    {
      "runNumber": 1,
      "embeddingScore": 0.82,
      "judgeScore": 1,
      "response": "DNA, or deoxyribonucleic acid, is a molecule that contains the genetic
        instructions used in the development and function of all living organisms...",
      "judgeReasoning": "The actual answer mentions that DNA contains genetic instructions
        for living organisms but fails to specify what those instructions are or how they
        function. The answer also inaccurately refers to DNA as a molecule that 'carries
        the genetic information' without clarifying its role in the development and
        functioning of living organisms."
    }
  ]
}
```

Notably, the embedding score of **0.82** is close to the 0.85 threshold — nearly passing through the embedding path alone — yet the judge scored it 1/10. The judge claims the response does not clarify DNA's role in development and function, despite the response explicitly doing so in multiple sentences.

**Why this happens**

`llama3.2:1b` at one billion parameters lacks the instruction-following stability to apply a structured rubric consistently. When asked to reason step-by-step and then score, the model generates plausible-sounding reasoning that does not accurately reflect the content of the response being evaluated. This is a known limitation of sub-2B parameter models: they can produce fluent text but cannot reliably perform the meta-cognitive task of evaluation.

**The embedding paradox**

The Ollama run produced a *higher* average embedding score (0.64) than the OpenAI run (0.51), despite a much lower pass rate. This is because `nomic-embed-text` uses a 768-dimensional vector space that clusters common factual phrases more tightly than `text-embedding-3-small`'s 1,536-dimensional space. Even with this advantage, most embedding scores remained below 0.85, and the miscalibrated judge prevented the OR logic from compensating.

---

## Key Takeaways

**1. The OR logic is essential.**
Without it, the majority of correct OpenAI responses would fail the embedding validator (average score 0.51 vs threshold 0.85). The judge path rescues all tests where the expected output is a short word or number.

**2. Judge model size matters enormously.**
`llama3.2:1b` cannot reliably apply a scoring rubric. For Ollama deployments, a minimum of 7–8 billion parameter model is recommended for the judge role. A mixed configuration is also supported: run the tested model locally via Ollama, and use the OpenAI API as the judge by setting `Provider: "ollama"` and `EmbeddingProvider: "openai"`.

**3. Avoid single-word expected outputs.**
Single words produce cosine similarity in the range 0.30–0.55 against correct full-sentence responses, well below the 0.85 threshold. Use full sentences as expected outputs or add `evaluationCriteria` to guide the judge.

**4. The three OpenAI failures are test data quality issues, not model failures.**
All three expected outputs reflect popular convention rather than strict scientific accuracy. The model's responses are correct; the test cases are not.

---

[Back to README](../README.md)
