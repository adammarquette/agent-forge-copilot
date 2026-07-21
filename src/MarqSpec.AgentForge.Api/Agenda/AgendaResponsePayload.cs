namespace MarqSpec.AgentForge.Api.Agenda;

/// <summary>Wire shape for <c>GET /agenda</c> - an <see cref="AgendaResult"/>, ordered soonest-first.</summary>
/// <param name="Rows">One row per not-yet-seen patient today.</param>
/// <param name="AsOf">The instant this agenda was computed.</param>
public sealed record AgendaResponsePayload(IReadOnlyList<AgendaRowPayload> Rows, DateTimeOffset AsOf);
