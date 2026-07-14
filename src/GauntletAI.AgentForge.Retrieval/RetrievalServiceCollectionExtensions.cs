using GauntletAI.AgentForge.Agents;
using Microsoft.Extensions.DependencyInjection;

namespace GauntletAI.AgentForge.Retrieval;

/// <summary>DI wiring for the Week 2 retrieval tier.</summary>
public static class RetrievalServiceCollectionExtensions
{
    /// <summary>
    /// Registers the FTS evidence-retriever and the corpus seeder. Assumes <c>AddAgentForgeData</c> has
    /// registered <c>AgentForgeDbContext</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddAgentForgeRetrieval(this IServiceCollection services)
    {
        services.AddScoped<IEvidenceRetriever, FtsEvidenceRetriever>();
        services.AddScoped<GuidelineCorpusSeeder>();
        return services;
    }
}
