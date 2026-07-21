namespace MarqSpec.AgentForge.SeedDemoPatients;

/// <summary>A synthetic demo patient's demographics (GitLab issue #26 follow-up).</summary>
public sealed record DemoPatient(string GivenName, string FamilyName, string BirthDate, string Gender);

/// <summary>
/// The fixed set of 20 synthetic demo patients seeded into QA OpenEMR. Every given name carries a
/// "Demo " prefix (matching the existing "Ada Testpatient" convention) so the cohort is identifiable
/// by substring for manual cleanup. <see cref="Gender"/> uses FHIR's lowercase values
/// ("male"/"female") as required by <c>POST /fhir/Patient</c> - distinct from the legacy REST API's
/// "Male"/"Female" convention.
/// </summary>
public static class DemoPatientCatalog
{
    public static readonly IReadOnlyList<DemoPatient> Patients =
    [
        new("Demo Eleanor", "Castellano", "1952-03-14", "female"),
        new("Demo Harold", "Whitfield", "1948-07-22", "male"),
        new("Demo Rosalind", "Ng", "1955-11-02", "female"),
        new("Demo Walter", "Ibekwe", "1960-01-09", "male"),
        new("Demo Consuela", "Marchetti", "1946-05-30", "female"),
        new("Demo Bartholomew", "Okafor", "1953-09-17", "male"),
        new("Demo Ingrid", "Solberg", "1958-02-25", "female"),
        new("Demo Desmond", "Achterberg", "1949-12-04", "male"),
        new("Demo Fatima", "Al-Rashid", "1962-06-11", "female"),
        new("Demo Gustaf", "Lindqvist", "1951-08-19", "male"),
        new("Demo Priya", "Chandrasekaran", "1990-04-03", "female"),
        new("Demo Marcus", "Delacroix", "1985-10-28", "male"),
        new("Demo Wren", "Okonkwo", "2001-02-14", "female"),
        new("Demo Otis", "Vandermeer", "1975-06-30", "male"),
        new("Demo Saoirse", "Kavanagh", "1998-12-09", "female"),
        new("Demo Reuben", "Okafor-Smith", "1968-03-21", "male"),
        new("Demo Ingeborg", "Falk", "1982-09-15", "female"),
        new("Demo Percival", "Nakamura", "1993-07-07", "male"),
        new("Demo Adaeze", "Umeh", "1971-05-26", "female"),
        new("Demo Tobias", "Renard", "1988-11-12", "male"),
    ];
}
