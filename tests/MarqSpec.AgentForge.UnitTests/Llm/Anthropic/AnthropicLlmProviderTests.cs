using System.Net;
using FakeItEasy;
using FluentAssertions;
using MarqSpec.AgentForge.Llm;
using MarqSpec.AgentForge.Llm.Anthropic;
using Microsoft.Extensions.Options;
using Refit;

namespace MarqSpec.AgentForge.UnitTests.Llm.Anthropic;

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

    [Fact]
    public async Task CompleteAsync_ApiReturnsAnErrorResponse_ThrowsWithTheResponseBodyInTheMessage()
    {
        // Confirmed live against the deployed app (2026-07-10): Refit.ApiException.Message is just
        // "Response status code does not indicate success: 400 (Bad Request)." - the actual reason
        // (Anthropic's error JSON) lives in .Content, which was being dropped entirely, making a
        // real ~10-15% invalid-request rate undiagnosable from AgentOrchestrator's fallback logs
        // (ARCHITECTURE.md §13.1 - "never fail silently" requires the detail to be observable
        // somewhere). AgentOrchestrator just logs ex.Message verbatim on any non-cancellation
        // exception, so enriching it here - the one place that actually knows about Refit - reaches
        // the log without coupling the provider-agnostic orchestrator to a Refit-specific type.
        var api = A.Fake<IAnthropicMessagesApi>();
        var httpResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"type":"error","error":{"type":"invalid_request_error","message":"max_tokens: field required"}}""")
        };
        var apiException = await ApiException.Create(
            new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages"),
            HttpMethod.Post, httpResponse, new RefitSettings());
        A.CallTo(() => api.CreateMessageAsync(A<AnthropicMessageRequest>._, A<CancellationToken>._))
            .ThrowsAsync(apiException);
        var provider = new AnthropicLlmProvider(api, Options);

        var act = () => provider.CompleteAsync(new LlmRequest("system", []), CancellationToken.None);

        (await act.Should().ThrowAsync<HttpRequestException>())
            .WithMessage("*max_tokens: field required*");
    }
}
