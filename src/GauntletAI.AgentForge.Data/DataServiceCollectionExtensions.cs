using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pgvector.EntityFrameworkCore;

namespace GauntletAI.AgentForge.Data;

/// <summary>Dependency-injection wiring for the Week 2 data tier.</summary>
public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AgentForgeDbContext"/> (Npgsql + pgvector) and its validated options. The schema
    /// is <b>not</b> created here — migrations are applied explicitly as a deploy step via
    /// <see cref="DatabaseMigrator.MigrateAgentForgeDataAsync"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration providing the <c>AgentForgeData</c> section.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddAgentForgeData(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AgentForgeDataOptions>()
            .Bind(configuration.GetSection(AgentForgeDataOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<AgentForgeDbContext>((serviceProvider, options) =>
        {
            var dataOptions = serviceProvider.GetRequiredService<IOptions<AgentForgeDataOptions>>().Value;
            options.UseNpgsql(dataOptions.ConnectionString, npgsql =>
            {
                npgsql.UseVector();
                npgsql.MigrationsAssembly(typeof(AgentForgeDbContext).Assembly.FullName);
            });
        });

        services.AddScoped<IDerivedFactStore, DerivedFactStore>();

        return services;
    }
}
