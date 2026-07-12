namespace GauntletAI.AgentForge.Api.Launch;

/// <summary>
/// A Daily Agenda SMART launch callback could not be completed safely - a state mismatch
/// (possible CSRF) or a token introspection that can't establish an audited clinician identity.
/// Distinct from a transport/API failure: this is a rejection, not a retry candidate. Kept
/// separate from <see cref="SmartLaunchException"/> even though the failure modes rhyme, matching
/// how <see cref="AgendaLaunchService"/> itself is a standalone copy of
/// <see cref="SmartLaunchService"/>, not a shared/branched implementation (ARCHITECTURE.md §19).
/// </summary>
public sealed class AgendaLaunchException : Exception
{
    /// <summary>Creates a new <see cref="AgendaLaunchException"/>.</summary>
    public AgendaLaunchException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
