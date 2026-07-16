namespace GauntletAI.AgentForge.Integration.OpenEmr.Fhir;

/// <summary>
/// The raw bytes of a document fetched from FHIR <c>Binary</c> (the attachment a <c>DocumentReference</c>
/// points at), with its media type — the source file the production click-to-source overlay renders
/// (gitlab#109).
/// </summary>
/// <param name="Content">The document bytes.</param>
/// <param name="ContentType">IANA media type (e.g. <c>application/pdf</c>).</param>
public sealed record BinaryDocument(byte[] Content, string ContentType);
