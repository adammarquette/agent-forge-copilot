using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MarqSpec.AgentForge.Data;

/// <summary>
/// Applies EF Core migrations as an explicit deploy step. Migrations are the <b>only</b> schema-change and
/// script-running mechanism for this database (W2-D14); do not auto-migrate silently on a request path.
/// </summary>
public static class DatabaseMigrator
{
    /// <summary>
    /// Brings the database schema up to the latest migration — creating the database and the <c>vector</c>
    /// extension if needed, and running any SQL/seed scripts embedded in migrations. Intended to run once at
    /// deploy/startup, not per request.
    /// </summary>
    /// <param name="services">A provider that can resolve <see cref="AgentForgeDbContext"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task MigrateAgentForgeDataAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AgentForgeDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
