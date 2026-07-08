using GauntletAI.AgentForge.Integration.OpenEmr.Http;

namespace GauntletAI.AgentForge.IntegrationTests.Support;

/// <summary>Fixed correlation id for integration tests - full correlation-id propagation is Epic 9.</summary>
internal sealed class FixedCorrelationIdAccessor(string correlationId) : ICorrelationIdAccessor
{
    public string CorrelationId { get; } = correlationId;
}
