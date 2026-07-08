namespace GauntletAI.AgentForge.Llm;

/// <summary>
/// Describes one MCP tool (Epic 4) to the model so it can plan and invoke it. The schema is the
/// contract source of truth (NFR-CONTRACT-1) - this just carries it to the model, it doesn't
/// define it.
/// </summary>
/// <param name="Name">Tool name, matching the MCP tool server's registered name.</param>
/// <param name="Description">What the tool does and when to call it, in the model's own words.</param>
/// <param name="InputJsonSchema">JSON Schema for the tool's input, as a raw JSON string.</param>
public sealed record LlmToolDefinition(string Name, string Description, string InputJsonSchema);
