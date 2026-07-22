using FluentAssertions;
using MarqSpec.AgentForge.Llm;

namespace MarqSpec.AgentForge.IntegrationTests.Llm;

/// <summary>
/// Exercises the real Anthropic Messages API end-to-end - the actual contract-drift coverage a
/// unit test faking IAnthropicMessagesApi can't provide (does the real API actually accept our
/// request shape and return what our response mapper expects?). Kept to a small number of cheap,
/// low-token calls since each one is a real, billed API call.
/// </summary>
public sealed class AnthropicLlmProviderEndToEndTests : IClassFixture<AnthropicQaFixture>
{
    private readonly AnthropicQaFixture _fixture;

    public AnthropicLlmProviderEndToEndTests(AnthropicQaFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CompleteAsync_MinimalGreeting_ReturnsNonEmptyTextWithPositiveTokenUsage()
    {
        var request = new LlmRequest(
            SystemPrompt: "Reply with exactly one short sentence.",
            Messages: [LlmMessage.FromText(LlmRole.User, "Say hello.")],
            MaxOutputTokens: 64);

        var response = await _fixture.Provider.CompleteAsync(request, CancellationToken.None);

        response.Content.Should().NotBeNullOrWhiteSpace();
        response.StopReason.Should().Be(LlmStopReason.EndTurn);
        response.Usage.InputTokens.Should().BePositive();
        response.Usage.OutputTokens.Should().BePositive();
    }

    [Fact]
    public async Task CompleteAsync_ToolOffered_RealApiAcceptsSchemaAndRequestsToolUse()
    {
        // Guards: the real API's tolerance of our tool JSON-schema wire format
        // (AnthropicRequestMapper) and our ability to parse a real tool_use content block back
        // out (AnthropicResponseMapper) - a unit test faking IAnthropicMessagesApi can't tell us
        // whether the live API actually accepts the shape we're sending.
        // The tool schema requires patientId, so the prompt must supply one - otherwise the model
        // has no way to satisfy a required parameter and reasonably asks a clarifying question
        // instead of calling the tool (observed live: this was the actual source of flakiness
        // here, not a real API/schema-tolerance problem).
        var request = new LlmRequest(
            SystemPrompt: "When asked about a patient's labs, you must call get_labs. Do not answer from your own knowledge.",
            Messages: [LlmMessage.FromText(LlmRole.User, "What is the most recent INR for patient P12345?")],
            Tools:
            [
                new LlmToolDefinition(
                    "get_labs",
                    "Fetches lab results for the patient currently in context.",
                    """{"type":"object","properties":{"patientId":{"type":"string"}},"required":["patientId"]}"""),
            ],
            MaxOutputTokens: 256);

        var response = await _fixture.Provider.CompleteAsync(request, CancellationToken.None);

        response.StopReason.Should().Be(LlmStopReason.ToolUse);
        response.ToolCalls.Should().ContainSingle().Which.ToolName.Should().Be("get_labs");
    }
}
