namespace MarqSpec.AgentForge.Llm;

/// <summary>
/// One piece of a message's content. A message can carry more than one block - e.g. an assistant
/// turn with both explanatory text and a tool call, or a user turn that is purely a tool result
/// being handed back. Deferred from Epic 6 until the multi-turn tool-calling loop (this epic)
/// actually needed to represent tool_use/tool_result history, not just plain text.
/// </summary>
public abstract record LlmContent;

/// <summary>Plain text content.</summary>
/// <param name="Text">The text.</param>
public sealed record LlmTextContent(string Text) : LlmContent;

/// <summary>
/// A tool call the model made in a past turn, replayed as history so the model can see its own
/// prior action when the conversation continues.
/// </summary>
/// <param name="Id">The provider-assigned call id this tool use is known by.</param>
/// <param name="ToolName">Which tool was called.</param>
/// <param name="ArgumentsJson">The arguments it was called with, as raw JSON.</param>
public sealed record LlmToolUseContent(string Id, string ToolName, string ArgumentsJson) : LlmContent;

/// <summary>
/// The result of executing a tool call, linked back to the <see cref="LlmToolUseContent.Id"/> it
/// answers. Every <see cref="LlmToolUseContent"/> the model produces must be followed by exactly
/// one of these before the conversation can continue - providers reject a turn otherwise.
/// </summary>
/// <param name="ToolUseId">Must match the <see cref="LlmToolUseContent.Id"/> being answered.</param>
/// <param name="ResultJson">The tool's result, as raw JSON.</param>
/// <param name="IsError">Whether the tool call failed - lets the model see and react to the failure.</param>
public sealed record LlmToolResultContent(string ToolUseId, string ResultJson, bool IsError = false) : LlmContent;

/// <summary>
/// A binary image handed to the model for vision (Week 2 multimodal extraction), base64-encoded.
/// </summary>
/// <param name="MediaType">IANA media type, e.g. <c>image/png</c> or <c>image/jpeg</c>.</param>
/// <param name="Base64Data">The image bytes, base64-encoded.</param>
public sealed record LlmImageContent(string MediaType, string Base64Data) : LlmContent;

/// <summary>
/// A binary document (e.g. a PDF) handed to the model. Anthropic reads PDFs natively, including
/// scanned pages via vision (Week 2 lab-PDF ingestion), base64-encoded.
/// </summary>
/// <param name="MediaType">IANA media type, e.g. <c>application/pdf</c>.</param>
/// <param name="Base64Data">The document bytes, base64-encoded.</param>
public sealed record LlmDocumentContent(string MediaType, string Base64Data) : LlmContent;
