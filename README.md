# ML 25/26-08 – Implementing Tests for LLM Prompts with Semantic Assertions

A semantic evaluation framework for testing Large Language Model (LLM) prompts using AI-powered assertions. This project addresses the challenge of testing non-deterministic LLM outputs by implementing semantic validation instead of exact string matching.

Developed by **LLM QA Lab**, this project provides an automated, reproducible approach to validating LLM responses by combining embedding-based similarity checks and LLM-as-a-judge evaluation techniques.


## Project Overview

When testing LLM outputs, traditional assertion methods fail because the same prompt can generate semantically equivalent but lexically different responses. This framework implements intelligent validation using:

- **Embedding-based Semantic Similarity**: Converts outputs to vector embeddings and compares using cosine similarity
- **LLM-as-a-Judge**: Uses a secondary LLM to evaluate response correctness based on custom criteria


## Key Features

- Console-based evaluation framework built with **.NET / C#**
- Automated execution of prompt test cases loaded from JSON
- Execute prompts against multiple LLM providers (OpenAI, Ollama)
- **Embedding-based semantic similarity assertions** using cosine similarity
- **LLM-as-a-judge** evaluation for nuanced correctness checks
- Support for **multiple test runs** to handle LLM non-determinism
- Comprehensive reporting with pass/fail statistics
- Configurable similarity thresholds and pass criteria
- Human-readable evaluation summaries and machine-friendly output
- Support for 100+ test cases


## Requirements

- .NET 8.0 SDK or higher
- OpenAI API key (or compatible LLM service)
- C# 12.0


## Installation

1. Clone the repository:
```bash
git clone https://github.com/almahamud1234/llm-semantic-evaluator.git
cd llm-semantic-evaluator
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Configure API keys:
```bash
# Create appsettings.json or use environment variables
export OPENAI_API_KEY="your-api-key-here"
```

4. Build the project:
```bash
dotnet build
```

## Usage

### Basic Usage
```bash
dotnet run -- --test-file tests.json
```

### Command Line Options
```bash
dotnet run -- [options]

Options:
  --test-file            Path to test cases JSON file (required)
  --embedding-threshold   Cosine similarity threshold (default: 0.85)
  --judge-threshold       LLM judge score threshold (default: 8)
  --runs                  Number of runs per test (default: 3)
  --output               Output report path (default: report.txt)
```

### Example
```bash
dotnet run -- --test-file data/tests.json --embedding-threshold 0.90 --runs 5 --output results/report.txt
```

## Project Structure


## Test Case Format

Create test cases in JSON format:
```json
{
  "tests": [
    {
      "id": "test_001",
      "category": "factual",
      "prompt": "What is the capital of France?",
      "expected_output": "Paris",
      "evaluation_criteria": "Response must correctly identify Paris as the capital of France"
    },
    {
      "id": "test_002",
      "category": "math",
      "prompt": "What is 15% of 200?",
      "expected_output": "30",
      "evaluation_criteria": "Response must contain the correct numerical answer"
    }
  ]
}
```

## Sample Output
```
=======================================
TEST EXECUTION REPORT
=======================================
Total Tests: 150
Passed: 142 (94.7%)
Failed: 8 (5.3%)

By Category:
- Factual Questions: 50/50 (100%)
- Math Problems: 45/48 (93.8%)
- Reasoning Tasks: 47/52 (90.4%)

Average Embedding Score: 0.89
Average Judge Score: 8.7/10

Validation Method Performance:
- Embedding Validator: 94.0% accuracy
- LLM Judge: 95.3% accuracy
- Combined: 94.7% accuracy

Failed Tests:
1. test_073 - Category: Math - Judge: 6/10 - Embedding: 0.72
2. test_089 - Category: Reasoning - Judge: 7/10 - Embedding: 0.68
...

Execution Time: 8m 34s
```

## Evaluation Strategies

### 1. Embedding-Based Semantic Similarity

- Converts text to high-dimensional vectors and computes cosine similarity:
```
similarity = (A · B) / (||A|| × ||B||)
```
- Score range: 0.0 to 1.0
- Threshold: 0.85 (configurable)
- Pass condition: `similarity ≥ threshold`

### 2. LLM-as-Judge Validation

Uses a secondary LLM to evaluate responses based on:
- Factual correctness
- Semantic equivalence
- Criteria compliance

- Score range: 1-10
- Threshold: 8 (configurable)
- Pass condition: `score ≥ threshold`

### 3. Combined Validation

A test passes if:
- At least 2 out of 3 runs pass embedding validation, **AND**
- At least 2 out of 3 runs pass judge validation


## License

This project is licensed under the MIT License - see the .... file for details.


## Team

**LLM QA Lab**
- [Md Abdulla AL Mahamud Rosi](https://github.com/almahamud1234)


## References

- [OpenAI API Documentation](https://platform.openai.com/docs)
- [Semantic Similarity in NLP](https://arxiv.org/abs/example)

