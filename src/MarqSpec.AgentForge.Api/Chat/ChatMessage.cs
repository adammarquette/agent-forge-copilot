namespace MarqSpec.AgentForge.Api.Chat;

/// <summary>
/// One outbound chat message, sequenced globally so a reconnecting client can ask for everything
/// after the highest sequence it has already seen (ENGINEERING_STANDARDS.md §12 - idempotent resume).
/// </summary>
/// <param name="Sequence">Strictly increasing across every session; never reused.</param>
/// <param name="Kind">Discriminator for the client (e.g. <c>"brief"</c>, <c>"answer"</c>).</param>
/// <param name="PayloadJson">The message body, already serialized.</param>
public sealed record ChatMessage(long Sequence, string Kind, string PayloadJson);
