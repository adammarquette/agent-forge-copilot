namespace GauntletAI.AgentForge.Api.Mcp;

/// <summary>
/// A tool call's input failed contract validation (NFR-CONTRACT-1) - rejected at the boundary
/// before any FHIR call is made.
/// </summary>
public sealed class McpToolContractException : Exception
{
    /// <summary>The tool that rejected the request.</summary>
    public string ToolName { get; }

    /// <summary>Human-readable validation failure messages.</summary>
    public IReadOnlyList<string> ValidationErrors { get; }

    /// <summary>Creates a new <see cref="McpToolContractException"/>.</summary>
    public McpToolContractException(string toolName, IReadOnlyList<string> validationErrors)
        : base($"Tool '{toolName}' rejected invalid input: {string.Join("; ", validationErrors)}")
    {
        ToolName = toolName;
        ValidationErrors = validationErrors;
    }
}
