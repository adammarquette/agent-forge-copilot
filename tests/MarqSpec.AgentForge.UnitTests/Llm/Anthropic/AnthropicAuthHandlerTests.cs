using System.Net;
using FluentAssertions;
using MarqSpec.AgentForge.Llm;
using MarqSpec.AgentForge.Llm.Anthropic;
using MarqSpec.AgentForge.UnitTests.TestSupport;
using Microsoft.Extensions.Options;

namespace MarqSpec.AgentForge.UnitTests.Llm.Anthropic;

public sealed class AnthropicAuthHandlerTests
{
    [Fact]
    public async Task SendAsync_Always_AttachesApiKeyAndAnthropicVersionHeaders()
    {
        var options = Options.Create(new LlmProviderOptions
        {
            ApiKey = "sk-ant-test-123",
            Model = "claude-sonnet-5",
            InputPricePerMillionTokensUsd = 3.00m,
            OutputPricePerMillionTokensUsd = 15.00m,
        });
        var capturing = new CapturingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new AnthropicAuthHandler(options) { InnerHandler = capturing };
        using var invoker = new HttpMessageInvoker(handler);

        await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages"), CancellationToken.None);

        capturing.LastRequest!.Headers.GetValues("x-api-key").Should().ContainSingle().Which.Should().Be("sk-ant-test-123");
        capturing.LastRequest.Headers.GetValues("anthropic-version").Should().ContainSingle()
            .Which.Should().Be(AnthropicAuthHandler.AnthropicVersion);
    }
}
