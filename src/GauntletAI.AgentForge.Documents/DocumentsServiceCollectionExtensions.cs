using Microsoft.Extensions.DependencyInjection;

namespace GauntletAI.AgentForge.Documents;

/// <summary>DI wiring for the Week 2 document-ingestion tier.</summary>
public static class DocumentsServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IDocumentExtractor"/>. Assumes an <c>ILlmProvider</c> is registered.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddAgentForgeDocuments(this IServiceCollection services)
    {
        services.AddScoped<IDocumentExtractor, DocumentExtractor>();
        return services;
    }
}
