using Microsoft.AspNetCore.Http;

namespace GauntletAI.AgentForge.Api.Session;

/// <summary>
/// Reads and writes <see cref="PatientSessionContext"/> to the ASP.NET Core session - the
/// server-side, cookie-keyed store the token never leaves (ARCHITECTURE.md D11).
/// </summary>
public static class SessionExtensions
{
    private const string AccessTokenKey = "patient-session.access-token";
    private const string SiteKey = "patient-session.site";
    private const string PatientIdKey = "patient-session.patient-id";

    /// <summary>Saves <paramref name="context"/> into <paramref name="session"/>.</summary>
    public static void SavePatientSession(this ISession session, PatientSessionContext context)
    {
        session.SetString(AccessTokenKey, context.AccessToken);
        session.SetString(SiteKey, context.Site);
        session.SetString(PatientIdKey, context.PatientId);
    }

    /// <summary>
    /// Reads the patient session from <paramref name="session"/>, or <see langword="null"/> if no
    /// session was saved or it is only partially populated - a half-populated session must never
    /// be treated as authenticated (the product is single-patient-scoped for its entire lifetime).
    /// </summary>
    public static PatientSessionContext? TryGetPatientSession(this ISession session)
    {
        var accessToken = session.GetString(AccessTokenKey);
        var site = session.GetString(SiteKey);
        var patientId = session.GetString(PatientIdKey);

        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(site) || string.IsNullOrEmpty(patientId))
        {
            return null;
        }

        return new PatientSessionContext(accessToken, site, patientId);
    }

    /// <summary>Removes the patient session from <paramref name="session"/>, if present.</summary>
    public static void ClearPatientSession(this ISession session)
    {
        session.Remove(AccessTokenKey);
        session.Remove(SiteKey);
        session.Remove(PatientIdKey);
    }
}
