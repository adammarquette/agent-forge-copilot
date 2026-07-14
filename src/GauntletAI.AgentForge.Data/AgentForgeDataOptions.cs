using System.ComponentModel.DataAnnotations;

namespace GauntletAI.AgentForge.Data;

/// <summary>
/// Strongly-typed configuration for the data tier, bound from configuration and validated on start
/// (ENGINEERING_STANDARDS.md §6). The connection string is a secret and comes from the environment / secret
/// store — never from source.
/// </summary>
public sealed class AgentForgeDataOptions
{
    /// <summary>Configuration section these options bind from.</summary>
    public const string SectionName = "AgentForgeData";

    /// <summary>Npgsql connection string for the Postgres + pgvector store.</summary>
    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; set; } = string.Empty;
}
