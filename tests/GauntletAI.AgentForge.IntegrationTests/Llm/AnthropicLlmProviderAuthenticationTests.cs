using System.Net;
using FluentAssertions;
using GauntletAI.AgentForge.Llm;

namespace GauntletAI.AgentForge.IntegrationTests.Llm;

/// <summary>
/// Confirms the real Anthropic API rejects an invalid key - independent of
/// <see cref="AnthropicQaFixture"/>, since it needs no valid key configured at all (unlike every
/// other test in this suite). Not an <see cref="IClassFixture{TFixture}"/> of the shared fixture
/// on purpose: that fixture's constructor requires a valid key to be configured, which would
/// block this test even though it doesn't need one.
/// </summary>
public sealed class AnthropicLlmProviderAuthenticationTests
{
    private const string PlaceholderModel = "claude-sonnet-5";

    [Fact]
    public async Task CompleteAsync_InvalidApiKey_RejectsWithUnauthorizedFromRealApi()
    {
        var provider = AnthropicQaFixture.BuildProviderWithInvalidKey(PlaceholderModel);
        var request = new LlmRequest("system", [LlmMessage.FromText(LlmRole.User, "hello")], MaxOutputTokens: 16);

        var act = () => provider.CompleteAsync(request, CancellationToken.None);

        // AnthropicLlmProvider re-throws Refit's ApiException as a plain HttpRequestException with
        // the response body folded into the message (GitLab issue #36) - the real Anthropic 401
        // body is asserted here as the regression guard for that unwrap.
        var exception = await act.Should().ThrowAsync<HttpRequestException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        exception.Which.Message.Should().Contain("\"type\":\"error\"", "the real response body should be folded into the message, not just the bare status line");
    }
}
