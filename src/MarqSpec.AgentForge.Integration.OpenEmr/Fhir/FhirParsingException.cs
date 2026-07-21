namespace MarqSpec.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// A FHIR response could not be parsed at all (e.g. invalid JSON). Distinct from a resource that
/// parses but is missing optional clinical fields, which mappers degrade gracefully rather than
/// throw for (NFR-REL-1) - this exception is for input malformed enough that no safe degradation
/// is possible.
/// </summary>
public sealed class FhirParsingException : Exception
{
    /// <summary>Creates a new <see cref="FhirParsingException"/>.</summary>
    public FhirParsingException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
