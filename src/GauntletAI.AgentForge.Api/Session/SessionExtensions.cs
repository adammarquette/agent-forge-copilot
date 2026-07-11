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
    private const string ClinicianIdentityKey = "patient-session.clinician-identity";

    /// <summary>Saves <paramref name="context"/> into <paramref name="session"/>.</summary>
    public static void SavePatientSession(this ISession session, PatientSessionContext context)
    {
        session.SetString(AccessTokenKey, context.AccessToken);
        session.SetString(SiteKey, context.Site);
        session.SetString(PatientIdKey, context.PatientId);
        session.SetString(ClinicianIdentityKey, context.ClinicianIdentity);
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
        var clinicianIdentity = session.GetString(ClinicianIdentityKey);

        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(site) ||
            string.IsNullOrEmpty(patientId) || string.IsNullOrEmpty(clinicianIdentity))
        {
            return null;
        }

        return new PatientSessionContext(accessToken, site, patientId, clinicianIdentity);
    }

    /// <summary>Removes the patient session from <paramref name="session"/>, if present.</summary>
    public static void ClearPatientSession(this ISession session)
    {
        session.Remove(AccessTokenKey);
        session.Remove(SiteKey);
        session.Remove(PatientIdKey);
        session.Remove(ClinicianIdentityKey);
    }

    private const string AgendaAccessTokenKey = "agenda-session.access-token";
    private const string AgendaSiteKey = "agenda-session.site";
    private const string AgendaClinicianIdentityKey = "agenda-session.clinician-identity";

    /// <summary>
    /// Saves <paramref name="context"/> into <paramref name="session"/>, under a distinct key
    /// prefix from <see cref="SavePatientSession"/> so a clinician mid-flow on both a single-patient
    /// launch and an agenda launch in the same browser session can't have one clobber the other
    /// (ARCHITECTURE.md §19).
    /// </summary>
    public static void SaveAgendaSession(this ISession session, AgendaSessionContext context)
    {
        session.SetString(AgendaAccessTokenKey, context.AccessToken);
        session.SetString(AgendaSiteKey, context.Site);
        session.SetString(AgendaClinicianIdentityKey, context.ClinicianIdentity);
    }

    /// <summary>
    /// Reads the agenda session from <paramref name="session"/>, or <see langword="null"/> if no
    /// session was saved or it is only partially populated - a half-populated session must never be
    /// treated as authenticated.
    /// </summary>
    public static AgendaSessionContext? TryGetAgendaSession(this ISession session)
    {
        var accessToken = session.GetString(AgendaAccessTokenKey);
        var site = session.GetString(AgendaSiteKey);
        var clinicianIdentity = session.GetString(AgendaClinicianIdentityKey);

        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(site) || string.IsNullOrEmpty(clinicianIdentity))
        {
            return null;
        }

        return new AgendaSessionContext(accessToken, site, clinicianIdentity);
    }

    /// <summary>Removes the agenda session from <paramref name="session"/>, if present.</summary>
    public static void ClearAgendaSession(this ISession session)
    {
        session.Remove(AgendaAccessTokenKey);
        session.Remove(AgendaSiteKey);
        session.Remove(AgendaClinicianIdentityKey);
    }
}
