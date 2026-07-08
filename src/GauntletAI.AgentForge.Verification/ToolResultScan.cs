namespace GauntletAI.AgentForge.Verification;

/// <summary>Everything <see cref="ToolResultJsonScanner"/> extracted from one turn's tool result JSON.</summary>
/// <param name="Citations">Every <c>{ResourceType}/{Id}</c> key actually returned by a tool this turn.</param>
/// <param name="Input">The structured clinical data reconstructed from the same tool results.</param>
public sealed record ToolResultScan(IReadOnlyCollection<string> Citations, DomainConstraintInput Input);
