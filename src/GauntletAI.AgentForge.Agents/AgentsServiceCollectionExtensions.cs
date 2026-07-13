using Microsoft.Extensions.DependencyInjection;

namespace GauntletAI.AgentForge.Agents;

/// <summary>DI wiring for the Week 2 multi-agent supervisor.</summary>
public static class AgentsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the evidence-agent supervisor. Assumes <c>IDocumentExtractor</c>, <c>IEvidenceRetriever</c>,
    /// <c>ILlmProvider</c>, and <c>IClinicalResponseVerifier</c> are registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddAgentForgeEvidenceAgent(this IServiceCollection services)
    {
        services.AddScoped<IEvidenceAgentSupervisor, EvidenceAgentSupervisor>();
        return services;
    }
}
