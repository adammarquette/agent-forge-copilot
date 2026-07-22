using FluentAssertions;
using MarqSpec.AgentForge.Integration.OpenEmr.Auth;

namespace MarqSpec.AgentForge.UnitTests.Integration.OpenEmr.Auth;

public sealed class AuthorizeUrlBuilderTests
{
    [Fact]
    public void Build_ValidRequest_UsesAuthorizeEndpointAsBase()
    {
        var uri = AuthorizeUrlBuilder.Build(CreateValidRequest());

        uri.GetLeftPart(UriPartial.Path).Should().Be("https://emr.example.org/oauth2/default/authorize");
    }

    [Fact]
    public void Build_ValidRequest_SetsAuthorizationCodeResponseType()
    {
        var query = ParseQuery(AuthorizeUrlBuilder.Build(CreateValidRequest()).Query);

        query["response_type"].Should().Be("code");
    }

    [Fact]
    public void Build_ValidRequest_IncludesClientIdAndRedirectUri()
    {
        var query = ParseQuery(AuthorizeUrlBuilder.Build(CreateValidRequest()).Query);

        query["client_id"].Should().Be("sidecar-client");
        query["redirect_uri"].Should().Be("https://sidecar.example.org/callback");
    }

    [Fact]
    public void Build_ValidRequest_JoinsScopesWithSpaces()
    {
        var query = ParseQuery(AuthorizeUrlBuilder.Build(CreateValidRequest()).Query);

        query["scope"].Should().Be("launch patient/patient.read openid fhirUser");
    }

    [Fact]
    public void Build_ValidRequest_IncludesStateForCsrfProtection()
    {
        var query = ParseQuery(AuthorizeUrlBuilder.Build(CreateValidRequest()).Query);

        query["state"].Should().Be("state-123");
    }

    [Fact]
    public void Build_ValidRequest_IncludesPkceChallengeAndS256Method()
    {
        var query = ParseQuery(AuthorizeUrlBuilder.Build(CreateValidRequest()).Query);

        query["code_challenge"].Should().Be("challenge-xyz");
        query["code_challenge_method"].Should().Be("S256");
    }

    [Fact]
    public void Build_LaunchProvided_IncludesLaunchParameter()
    {
        var request = CreateValidRequest() with { Launch = "launch-token-abc" };

        var query = ParseQuery(AuthorizeUrlBuilder.Build(request).Query);

        query["launch"].Should().Be("launch-token-abc");
    }

    [Fact]
    public void Build_LaunchNotProvided_OmitsLaunchParameter()
    {
        var query = ParseQuery(AuthorizeUrlBuilder.Build(CreateValidRequest()).Query);

        query.Should().NotContainKey("launch");
    }

    [Fact]
    public void Build_AudProvided_IncludesAudParameterForSmartV2()
    {
        var request = CreateValidRequest() with { Aud = "https://emr.example.org/apis/default/fhir" };

        var query = ParseQuery(AuthorizeUrlBuilder.Build(request).Query);

        query["aud"].Should().Be("https://emr.example.org/apis/default/fhir");
    }

    [Fact]
    public void Build_ScopeContainingReservedCharacters_PercentEncodesTheScopeValue()
    {
        var uri = AuthorizeUrlBuilder.Build(CreateValidRequest());

        uri.Query.Should().Contain("patient%2Fpatient.read");
    }

    private static AuthorizeRequest CreateValidRequest() => new(
        AuthorizeEndpoint: "https://emr.example.org/oauth2/default/authorize",
        ClientId: "sidecar-client",
        RedirectUri: "https://sidecar.example.org/callback",
        Scopes: ["launch", "patient/patient.read", "openid", "fhirUser"],
        State: "state-123",
        Pkce: new PkceChallenge("verifier-abc", "challenge-xyz", "S256"));

    private static Dictionary<string, string> ParseQuery(string query) =>
        query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(kv => Uri.UnescapeDataString(kv[0]), kv => Uri.UnescapeDataString(kv[1]));
}
