using FluentAssertions;
using GauntletAI.AgentForge.Llm;
using GauntletAI.AgentForge.Llm.Anthropic;

namespace GauntletAI.AgentForge.UnitTests.Llm.Anthropic;

public sealed class AnthropicResponseMapperTests
{
    [Fact]
    public void Map_TextOnlyResponse_ExtractsText()
    {
        var response = new AnthropicMessageResponse(
            "msg_1",
            [new AnthropicContentBlock("text", "Her most recent INR was 2.3.", null, null, null)],
            "end_turn",
            new AnthropicUsage(100, 20));

        var mapped = AnthropicResponseMapper.Map(response, inputPricePerMillion: 3.00m, outputPricePerMillion: 15.00m);

        mapped.Content.Should().Be("Her most recent INR was 2.3.");
        mapped.ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public void Map_MultipleTextBlocks_ConcatenatesThem()
    {
        var response = new AnthropicMessageResponse(
            "msg_2",
            [
                new AnthropicContentBlock("text", "Part one. ", null, null, null),
                new AnthropicContentBlock("text", "Part two.", null, null, null),
            ],
            "end_turn",
            new AnthropicUsage(10, 5));

        var mapped = AnthropicResponseMapper.Map(response, 3.00m, 15.00m);

        mapped.Content.Should().Be("Part one. Part two.");
    }

    [Fact]
    public void Map_ToolUseBlock_MapsToLlmToolCallWithIdNameAndArguments()
    {
        var response = new AnthropicMessageResponse(
            "msg_3",
            [new AnthropicContentBlock("tool_use", null, "toolu_123", "get_labs", System.Text.Json.Nodes.JsonNode.Parse("""{"patientId":"1"}"""))],
            "tool_use",
            new AnthropicUsage(50, 10));

        var mapped = AnthropicResponseMapper.Map(response, 3.00m, 15.00m);

        mapped.ToolCalls.Should().ContainSingle();
        var call = mapped.ToolCalls[0];
        call.Id.Should().Be("toolu_123");
        call.ToolName.Should().Be("get_labs");
        call.ArgumentsJson.Should().Be("""{"patientId":"1"}""");
    }

    [Theory]
    [InlineData("end_turn", LlmStopReason.EndTurn)]
    [InlineData("tool_use", LlmStopReason.ToolUse)]
    [InlineData("max_tokens", LlmStopReason.MaxTokens)]
    [InlineData("stop_sequence", LlmStopReason.Other)]
    [InlineData(null, LlmStopReason.Other)]
    public void Map_StopReason_MapsToExpectedLlmStopReason(string? wireStopReason, LlmStopReason expected)
    {
        var response = new AnthropicMessageResponse("msg_4", [], wireStopReason, new AnthropicUsage(1, 1));

        var mapped = AnthropicResponseMapper.Map(response, 3.00m, 15.00m);

        mapped.StopReason.Should().Be(expected);
    }

    [Fact]
    public void Map_Usage_ComputesCostFromConfiguredPerMillionTokenPricing()
    {
        var response = new AnthropicMessageResponse(
            "msg_5", [], "end_turn", new AnthropicUsage(InputTokens: 1_000_000, OutputTokens: 500_000));

        var mapped = AnthropicResponseMapper.Map(response, inputPricePerMillion: 3.00m, outputPricePerMillion: 15.00m);

        mapped.Usage.InputTokens.Should().Be(1_000_000);
        mapped.Usage.OutputTokens.Should().Be(500_000);
        mapped.Usage.EstimatedCostUsd.Should().Be(3.00m + 7.50m);
    }
}
