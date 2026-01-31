namespace LLMSemanticEvaluator.Configuration;

/// <summary>
/// Command-line options for the application
/// </summary>
public class CommandLineOptions
{
    /// <summary>
    /// Path to test cases JSON file
    /// </summary>
    public string TestFile { get; set; } = "data/test_cases.json";

    /// <summary>
    /// Path to output report file
    /// </summary>
    public string OutputPath { get; set; } = "report.txt";

    /// <summary>
    /// Embedding similarity threshold override
    /// </summary>
    public double? EmbeddingThreshold { get; set; }

    /// <summary>
    /// Judge score threshold override
    /// </summary>
    public int? JudgeThreshold { get; set; }

    /// <summary>
    /// Number of runs override
    /// </summary>
    public int? Runs { get; set; }

    /// <summary>
    /// Whether to show verbose output
    /// </summary>
    public bool Verbose { get; set; }

    /// <summary>
    /// Parse command-line arguments
    /// </summary>
    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--test-file":
                case "-t":
                    if (i + 1 < args.Length)
                        options.TestFile = args[++i];
                    break;

                case "--output":
                case "-o":
                    if (i + 1 < args.Length)
                        options.OutputPath = args[++i];
                    break;

                case "--embedding-threshold":
                case "-e":
                    if (i + 1 < args.Length && double.TryParse(args[++i], out var embedThreshold))
                        options.EmbeddingThreshold = embedThreshold;
                    break;

                case "--judge-threshold":
                case "-j":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var judgeThreshold))
                        options.JudgeThreshold = judgeThreshold;
                    break;

                case "--runs":
                case "-r":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var runs))
                        options.Runs = runs;
                    break;

                case "--verbose":
                case "-v":
                    options.Verbose = true;
                    break;

                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
            }
        }

        return options;
    }

    /// <summary>
    /// Print help message
    /// </summary>
    private static void PrintHelp()
    {
        Console.WriteLine(@"
            LLM Prompt Testing Framework
            =============================

            Usage: dotnet run -- [options]

            Options:
            -t, --test-file <path>           Path to test cases JSON file (default: data/test_cases.json)
            -o, --output <path>              Output report path (default: report.txt)
            -e, --embedding-threshold <num>  Cosine similarity threshold (default: 0.85)
            -j, --judge-threshold <num>      LLM judge score threshold (default: 8)
            -r, --runs <num>                 Number of runs per test (default: 3)
            -v, --verbose                    Show verbose output
            -h, --help                       Show this help message

            Examples:
            dotnet run -- --test-file tests.json
            dotnet run -- --test-file tests.json --embedding-threshold 0.90 --runs 5
            dotnet run -- -t tests.json -o results.txt -v
        ");
    }
}