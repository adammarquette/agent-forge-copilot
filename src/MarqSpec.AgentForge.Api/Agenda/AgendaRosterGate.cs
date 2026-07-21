namespace MarqSpec.AgentForge.Api.Agenda;

/// <summary>
/// The drill-down authorization check (ARCHITECTURE.md §19.1 step 5): a client selecting a
/// patient from the agenda may only select one this session's own agenda already returned. Kept
/// as a pure function, independent of session/HTTP plumbing, so the one decision that matters here
/// is trivially unit-testable.
/// </summary>
public static class AgendaRosterGate
{
    /// <summary>Whether <paramref name="requestedPatientId"/> was part of <paramref name="rosterPatientIds"/>.</summary>
    public static bool Authorize(IReadOnlySet<string> rosterPatientIds, string requestedPatientId) =>
        rosterPatientIds.Contains(requestedPatientId);
}
