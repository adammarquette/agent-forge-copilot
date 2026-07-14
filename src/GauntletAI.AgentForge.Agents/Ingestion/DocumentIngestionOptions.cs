namespace GauntletAI.AgentForge.Agents.Ingestion;

/// <summary>Options for document ingestion write-back (bound via the Options pattern).</summary>
public sealed class DocumentIngestionOptions
{
    /// <summary>Configuration section name this type binds to.</summary>
    public const string SectionName = "DocumentIngestion";

    /// <summary>OpenEMR document category path that uploaded source documents are filed under.</summary>
    public string CategoryPath { get; init; } = "AgentForge";
}
