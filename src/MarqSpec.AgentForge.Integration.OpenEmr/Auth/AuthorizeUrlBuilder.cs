using System.Text;

namespace MarqSpec.AgentForge.Integration.OpenEmr.Auth;

/// <summary>
/// Builds the SMART EHR launch authorization-code + PKCE redirect URL
/// (INTERFACE_CONTROL.md Interface A.3). The browser is navigated to this URL directly - it is
/// never called as an API request from the sidecar.
/// </summary>
public static class AuthorizeUrlBuilder
{
    /// <summary>Builds the full authorize URL for <paramref name="request"/>.</summary>
    public static Uri Build(AuthorizeRequest request)
    {
        var query = new StringBuilder()
            .AppendParameter("response_type", "code", first: true)
            .AppendParameter("client_id", request.ClientId)
            .AppendParameter("redirect_uri", request.RedirectUri)
            .AppendParameter("scope", string.Join(' ', request.Scopes))
            .AppendParameter("state", request.State)
            .AppendParameter("code_challenge", request.Pkce.CodeChallenge)
            .AppendParameter("code_challenge_method", request.Pkce.CodeChallengeMethod);

        if (!string.IsNullOrEmpty(request.Launch))
        {
            query.AppendParameter("launch", request.Launch);
        }

        if (!string.IsNullOrEmpty(request.Aud))
        {
            query.AppendParameter("aud", request.Aud);
        }

        return new Uri($"{request.AuthorizeEndpoint}?{query}");
    }

    private static StringBuilder AppendParameter(
        this StringBuilder builder, string name, string value, bool first = false)
    {
        if (!first)
        {
            builder.Append('&');
        }

        return builder
            .Append(Uri.EscapeDataString(name))
            .Append('=')
            .Append(Uri.EscapeDataString(value));
    }
}
