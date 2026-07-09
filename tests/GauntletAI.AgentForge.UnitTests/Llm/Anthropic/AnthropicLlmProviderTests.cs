using FakeItEasy;
using FluentAssertions;
using GauntletAI.AgentForge.Llm;
using GauntletAI.AgentForge.Llm.Anthropic;
using Microsoft.Extensions.Options;

namespace GauntletAI.AgentForge.UnitTests.Llm.Anthropic;

public sealed class AnthropicLlmProviderTests
{
    private static readonly IOptions<LlmProviderOptions> Options = Microsoft.Extensions.Options.Options.Create(
        new LlmProviderOptions
        {
            ApiKey = "k",
            Model = "claude-sonnet-5",
            InputPricePerMillionTokensUsd = 3.00m,
            OutputPricePerMillionTokensUsd = 15.00m,
        });

    [Fact]
    public async Task CompleteAsync_ValidRequest_MapsRequestWithConfiguredModelBeforeCallingApi()
    {
        var api = A.Fake<IAnthropicMessagesApi>();
        AnthropicMessageRequest? captured = null;
        var wireResponse = new AnthropicMessageResponse(
            "msg_1", [new AnthropicContentBlock("text", "Hello", null, null, null)], "end_turn", new AnthropicUsage(10, 5));
        A.CallTo(() => api.CreateMessageAsync(A<AnthropicMessageRequest>._, A<CancellationToken>._))
            .Invokes((AnthropicMessageRequest req, CancellationToken _) => captured = req)
            .Returns(Task.FromResult(wireResponse));
        var provider = new AnthropicLlmProvider(api, Options);

        var result = await provider.CompleteAsync(new LlmRequest("system prompt", []), CancellationToken.None);

        captured!.Model.Should().Be("claude-sonnet-5");
        captured.System.Should().Be("system prompt");
        result.Content.Should().Be("Hello");
        result.Usage.InputTokens.Should().Be(10);
    }

    [Fact]
    public async Task CompleteAsync_ApiResponse_MapsUsingConfiguredPricing()
    {
        var api = A.Fake<IAnthropicMessagesApi>();
        var wireResponse = new AnthropicMessageResponse(
            "msg_2", [], "end_turn", new AnthropicUsage(1_000_000, 1_000_000));
        A.CallTo(() => api.CreateMessageAsync(A<AnthropicMessageRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(wireResponse));
        var provider = new AnthropicLlmProvider(api, Options);

        var result = await provider.CompleteAsync(new LlmRequest("system", []), CancellationToken.None);

        result.Usage.EstimatedCostUsd.Should().Be(3.00m + 15.00m);
    }

    [Fact]
    public async Task CompleteAsync_Always_PassesCancellationTokenThrough()
    {
        var api = A.Fake<IAnthropicMessagesApi>();
        A.CallTo(() => api.CreateMessageAsync(A<AnthropicMessageRequest>._, A<CancellationToken>._))
            .Returns(Task.FromResult(new AnthropicMessageResponse("msg_3", [], "end_turn", new AnthropicUsage(1, 1))));
        var provider = new AnthropicLlmProvider(api, Options);
        using var cts = new CancellationTokenSource();

        await provider.CompleteAsync(new LlmRequest("system", []), cts.Token);

        A.CallTo(() => api.CreateMessageAsync(A<AnthropicMessageRequest>._, cts.Token)).MustHaveHappenedOnceExactly();
    }
}
