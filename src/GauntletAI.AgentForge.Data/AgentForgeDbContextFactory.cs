using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace GauntletAI.AgentForge.Data;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations</c> can construct the context without the full host. Reads
/// the connection string from <c>AgentForgeData__ConnectionString</c>; falls back to a local dev DSN for
/// tooling only (never a deployment path). Secrets never come from source (ENGINEERING_STANDARDS.md §6).
/// </summary>
public sealed class AgentForgeDbContextFactory : IDesignTimeDbContextFactory<AgentForgeDbContext>
{
    /// <inheritdoc />
    public AgentForgeDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("AgentForgeData__ConnectionString")
            ?? "Host=localhost;Port=5432;Database=agentforge;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AgentForgeDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseVector();
                npgsql.MigrationsAssembly(typeof(AgentForgeDbContext).Assembly.FullName);
            })
            .Options;

        return new AgentForgeDbContext(options);
    }
}
