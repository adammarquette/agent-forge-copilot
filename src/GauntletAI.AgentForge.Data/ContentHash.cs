using System.Security.Cryptography;

namespace GauntletAI.AgentForge.Data;

/// <summary>
/// Computes the content hash that keys idempotent document ingestion
/// (<see cref="Entities.IngestedDocument.ContentHash"/>, W2_ARCHITECTURE.md §4): re-ingesting the same bytes
/// resolves to the same key, so the source document is never written or recorded twice.
/// </summary>
public static class ContentHash
{
    /// <summary>SHA-256 of the content as lowercase hex. Same bytes → same key; any change → a different key.</summary>
    public static string Compute(ReadOnlySpan<byte> content) => Convert.ToHexStringLower(SHA256.HashData(content));
}
