using System.Text.Json;
using FluentAssertions;
using GauntletAI.AgentForge.Llm;
using GauntletAI.AgentForge.Llm.Anthropic;

namespace GauntletAI.AgentForge.UnitTests.Llm.Anthropic;

public sealed class AnthropicRequestMapperTests
{
    [Fact]
    public void Map_ValidRequest_MapsModelSystemPromptMaxTokensAndTemperature()
    {
        var request = new LlmRequest(
            SystemPrompt: "You are a cardiology co-pilot.",
            Messages: [],
            Temperature: 0.2,
            MaxOutputTokens: 2048);

        var wire = AnthropicRequestMapper.Map(request, "claude-sonnet-5");

        wire.Model.Should().Be("claude-sonnet-5");
        wire.System.Should().Be("You are a cardiology co-pilot.");
        wire.MaxTokens.Should().Be(2048);
        wire.Temperature.Should().Be(0.2);
    }

    [Fact]
    public void Map_MessagesWithBothRoles_MapsToLowercaseWireRoleStrings()
    {
        var request = new LlmRequest(
            SystemPrompt: "system",
            Messages:
            [
                LlmMessage.FromText(LlmRole.User, "Is her INR therapeutic?"),
                LlmMessage.FromText(LlmRole.Assistant, "Her most recent INR was 2.3."),
            ]);

        var wire = AnthropicRequestMapper.Map(request, "claude-sonnet-5");

        wire.Messages.Should().HaveCount(2);
        wire.Messages[0].Role.Should().Be("user");
        wire.Messages[0].Content.Should().ContainSingle().Which.Text.Should().Be("Is her INR therapeutic?");
        wire.Messages[1].Role.Should().Be("assistant");
        wire.Messages[1].Content.Should().ContainSingle().Which.Text.Should().Be("Her most recent INR was 2.3.");
    }

    [Fact]
    public void Map_TextContent_MapsToTextBlock()
    {
        var request = new LlmRequest("system", [new LlmMessage(LlmRole.User, [new LlmTextContent("hello")])]);

        var wire = AnthropicRequestMapper.Map(request, "claude-sonnet-5");

        var block = wire.Messages[0].Content.Should().ContainSingle().Which;
        block.Type.Should().Be("text");
        block.Text.Should().Be("hello");
    }

    [Fact]
    public void Map_ToolUseContent_MapsToToolUseBlockWithParsedInput()
    {
        var request = new LlmRequest(
            "system",
            [new LlmMessage(LlmRole.Assistant, [new LlmToolUseContent("toolu_1", "get_labs", """{"patientId":"1"}""")])]);

        var wire = AnthropicRequestMapper.Map(request, "claude-sonnet-5");

        var block = wire.Messages[0].Content.Should().ContainSingle().Which;
        block.Type.Should().Be("tool_use");
        block.Id.Should().Be("toolu_1");
        block.Name.Should().Be("get_labs");
        block.Input!["patientId"]!.GetValue<string>().Should().Be("1");
    }

    [Fact]
    public void Map_ToolResultContent_MapsToToolResultBlock()
    {
        var request = new LlmRequest(
            "system",
            [new LlmMessage(LlmRole.User, [new LlmToolResultContent("toolu_1", """{"labs":[]}""", IsError: false)])]);

        var wire = AnthropicRequestMapper.Map(request, "claude-sonnet-5");

        var block = wire.Messages[0].Content.Should().ContainSingle().Which;
        block.Type.Should().Be("tool_result");
        block.ToolUseId.Should().Be("toolu_1");
        block.ToolResultContent.Should().Be("""{"labs":[]}""");
        block.IsError.Should().Be(false);
    }

    [Fact]
    public void Map_NoToolsOffered_WireToolsIsNull()
    {
        var request = new LlmRequest("system", [], Tools: null);

        var wire = AnthropicRequestMapper.Map(request, "claude-sonnet-5");

        wire.Tools.Should().BeNull();
    }

    [Fact]
    public void Map_NoToolsOffered_SerializedRequestOmitsToolsField()
    {
        // Regression test: the real Anthropic API rejects an explicit "tools": null with
        // 400 invalid_request_error "tools: Input should be a valid array" - the field must be
        // omitted entirely, not sent as a JSON null (confirmed against the real API; see #25).
        var request = new LlmRequest("system", [], Tools: null);

        var wire = AnthropicRequestMapper.Map(request, "claude-sonnet-5");
        var json = JsonSerializer.Serialize(wire);

        json.Should().NotContain("\"tools\"");
    }

    [Fact]
    public void Map_ToolsOffered_MapsNameDescriptionAndParsedInputSchema()
    {
        var request = new LlmRequest(
            "system",
            [],
            Tools:
            [
                new LlmToolDefinition(
                    "get_labs",
                    "Fetches lab results for the patient in context.",
                    """{"type":"object","properties":{"patientId":{"type":"string"}},"required":["patientId"]}"""),
            ]);

        var wire = AnthropicRequestMapper.Map(request, "claude-sonnet-5");

        wire.Tools.Should().ContainSingle();
        var tool = wire.Tools![0];
        tool.Name.Should().Be("get_labs");
        tool.Description.Should().Be("Fetches lab results for the patient in context.");
        tool.InputSchema["type"]!.GetValue<string>().Should().Be("object");
        tool.InputSchema["required"]![0]!.GetValue<string>().Should().Be("patientId");
    }
}
