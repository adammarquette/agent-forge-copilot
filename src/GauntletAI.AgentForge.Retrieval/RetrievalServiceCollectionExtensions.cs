using GauntletAI.AgentForge.Agents;
using GauntletAI.AgentForge.Retrieval.Cohere;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refit;

namespace GauntletAI.AgentForge.Retrieval;

/// <summary>DI wiring for the Week 2 retrieval tier (hybrid RAG, W2_ARCHITECTURE.md §5).</summary>
public static class RetrievalServiceCollectionExtensions
{
    /// <summary>
    /// Registers the hybrid evidence retriever (sparse FTS + dense pgvector → RRF → rerank) and the corpus
    /// seeder. The Cohere embed/rerank clients are wired only when <c>Cohere:ApiKey</c> is configured;
    /// otherwise no-op providers are registered and the retriever degrades to sparse-only. Assumes
    /// <c>AddAgentForgeData</c> has registered <c>AgentForgeDbContext</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration providing the <c>Cohere</c> section.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddAgentForgeRetrieval(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CohereOptions>(configuration.GetSection(CohereOptions.SectionName));

        services.AddScoped<ISparseRetriever, FtsEvidenceRetriever>();
        services.AddScoped<IDenseRetriever, DenseEvidenceRetriever>();
        services.AddScoped<IEvidenceRetriever, HybridEvidenceRetriever>();
        services.AddScoped<GuidelineCorpusSeeder>();

        var cohere = configuration.GetSection(CohereOptions.SectionName).Get<CohereOptions>() ?? new CohereOptions();
        if (cohere.IsEnabled)
        {
            services.AddTransient<CohereAuthHandler>();
            services.AddRefitClient<ICohereApi>()
                .ConfigureHttpClient((serviceProvider, client) =>
                {
                    var options = serviceProvider.GetRequiredService<IOptions<CohereOptions>>().Value;
                    client.BaseAddress = new Uri(options.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
                })
                .AddHttpMessageHandler<CohereAuthHandler>()
                .AddStandardResilienceHandler();

            services.AddScoped<IEmbeddingProvider, CohereEmbeddingProvider>();
            services.AddScoped<IReranker, CohereReranker>();
        }
        else
        {
            // No key configured: dense + rerank are disabled, the hybrid retriever falls back to sparse FTS.
            services.AddScoped<IEmbeddingProvider, DisabledEmbeddingProvider>();
            services.AddScoped<IReranker, DisabledReranker>();
        }

        return services;
    }
}
