namespace GauntletAI.AgentForge.Api.Launch;

/// <summary>
/// A SMART EHR launch callback could not be completed safely - a state mismatch (possible CSRF)
/// or a token response missing the launch patient context this single-patient-scoped product
/// requires. Distinct from a transport/API failure: this is a rejection, not a retry candidate.
/// </summary>
public sealed class SmartLaunchException : Exception
{
    /// <summary>Creates a new <see cref="SmartLaunchException"/>.</summary>
    public SmartLaunchException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
