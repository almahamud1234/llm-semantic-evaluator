using LLMSemanticEvaluator.Configuration;
using LLMSemanticEvaluator.Infrastructure;
using LLMSemanticEvaluator.Interfaces;
using LLMSemanticEvaluator.Services;
using LLMSemanticEvaluator.Validators;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LLMSemanticEvaluator;

/// <summary>
/// Application entry point.
///
/// Uses Microsoft.Extensions.Hosting — the same infrastructure used in every
/// production .NET service and ASP.NET Core application — to provide:
///
///   Configuration  : appsettings.json is loaded automatically by CreateDefaultBuilder.
///                    All settings are bound to TestConfiguration via IOptions&lt;T&gt;.
///                    No class reads the JSON file directly.
///
///   Dependency Injection : every service is registered in the DI container and
///                    receives its dependencies through its constructor.
///                    No class instantiates another class with "new".
///
///   Logging        : ILogger&lt;T&gt; is injected into every service.
///                    Output is written through the logging infrastructure, not
///                    Console.WriteLine, so it works in any hosting environment
///                    (console, Windows Service, Docker container, CI pipeline).
///
///   MS AI Framework : IChatClient and IEmbeddingGenerator&lt;string, Embedding&lt;float&gt;&gt;
///                    are the standard Microsoft.Extensions.AI interfaces.
///                    LLMClientFactory resolves the correct implementation
///                    (OpenAI, Grok, or Ollama) based on appsettings.json.
///                    Switching providers requires only a config change — no code changes.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((_, cfg) =>
            {
                // appsettings.json is already added by CreateDefaultBuilder.
                // This call is made explicit so the configuration source is
                // visible and readable at the entry point.
                cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            })
            .ConfigureServices((context, services) =>
            {
                // ── Configuration binding ───────────────────────────────────────
                // Maps every key in appsettings.json to TestConfiguration properties.
                // Services receive IOptions<TestConfiguration>; they never touch the file.
                services.Configure<TestConfiguration>(context.Configuration);

                // ── LLM client factory ──────────────────────────────────────────
                // The factory reads IOptions<TestConfiguration> and creates the
                // correct client. Switching providers means changing appsettings.json;
                // no code changes are required anywhere else.
                services.AddSingleton<ILLMClientFactory, LLMClientFactory>();

                // ── Microsoft.Extensions.AI standard interfaces ─────────────────
                // IChatClient and IEmbeddingGenerator are the MS AI framework interfaces.
                // All services that need to call an LLM or generate embeddings
                // depend on these interfaces — never on a concrete provider class.
                services.AddSingleton<IChatClient>(sp =>
                    sp.GetRequiredService<ILLMClientFactory>().CreateChatClient());

                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
                    sp.GetRequiredService<ILLMClientFactory>().CreateEmbeddingGenerator());

                // ── Core services ───────────────────────────────────────────────
                services.AddSingleton<ISimilarityCalculator, CosineSimilarityCalculator>();
                services.AddSingleton<IValidator, EmbeddingValidator>();
                services.AddSingleton<IValidator, LLMJudgeValidator>();
                services.AddSingleton<IJsonTestLoader, JsonTestLoader>();
                services.AddSingleton<ITestRunner, TestRunner>();
                services.AddSingleton<IReportGenerator, ReportGenerator>();

                // ── Hosted service ──────────────────────────────────────────────
                // EvaluatorService is the application's single hosted service.
                // The host calls StartAsync(), waits for the run to complete,
                // then shuts down and disposes all registered services.
                services.AddHostedService<EvaluatorService>();
            })
            .Build();

        await host.RunAsync();
    }
}