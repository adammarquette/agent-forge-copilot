using MarqSpec.AgentForge.Llm;

namespace MarqSpec.AgentForge.Agent;

/// <summary>
/// Executes a single tool call the model requested. Extracted as an interface so the
/// orchestrator's unit tests can fake tool dispatch without needing a real
/// <see cref="Mcp.IMcpToolServer"/> behind it - the same reason <c>IOpenEmrFhirClient</c> was
/// extracted in Epic 2.
/// </summary>
public interface IMcpToolDispatcher
{
    /// <summary>Executes <paramref name="toolCall"/> and returns its result, linked back to the call's id.</summary>
    Task<LlmToolResultContent> DispatchAsync(
        string site, string patientId, LlmToolCall toolCall, CancellationToken cancellationToken);
}
