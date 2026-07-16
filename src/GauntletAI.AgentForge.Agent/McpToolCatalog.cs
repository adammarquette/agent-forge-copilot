using GauntletAI.AgentForge.Llm;

namespace GauntletAI.AgentForge.Agent;

/// <summary>
/// Describes the MCP tools (ARCHITECTURE.md §8.1) to the model. Deliberately excludes
/// patientId and site from every schema - which patient and which OpenEMR site is session-bound,
/// resolved by the orchestrator from the authenticated launch context, never something the model
/// fills in on a tool call. This is the schema-level half of FR-CHAT-3's patient-scoping
/// enforcement; the tool dispatcher that forces the session's patient id regardless of what a
/// tool call argument says is the other half.
/// </summary>
public static class McpToolCatalog
{
    /// <summary>Every tool the orchestrator may offer the model.</summary>
    public static IReadOnlyList<LlmToolDefinition> AllTools { get; } =
    [
        new LlmToolDefinition(
            "get_patient_summary",
            "Demographics, active problems, active medications, and allergies for the patient in " +
            "context, as one bounded bundle. Call this first, at the start of a brief.",
            """{"type":"object","properties":{}}"""),

        new LlmToolDefinition(
            "get_interval_changes",
            "Medication changes, new labs, and interval encounters since a given date - the " +
            "\"what changed since last visit\" diff. since_date is required.",
            """
            {
              "type": "object",
              "properties": {
                "since_date": {
                  "type": "string",
                  "description": "FHIR date-prefixed filter, e.g. 'ge2026-01-01' (prefix required: eq/ne/gt/lt/ge/le/sa/eb/ap)."
                }
              },
              "required": ["since_date"]
            }
            """),

        new LlmToolDefinition(
            "get_labs",
            "Lab Observations (INR, K+, creatinine, lipids, BNP, etc.) with values, units, dates, " +
            "and reference ranges. Optionally bounded to an interval.",
            """
            {
              "type": "object",
              "properties": {
                "since_date": {
                  "type": "string",
                  "description": "FHIR date-prefixed filter, e.g. 'ge2026-01-01'. Omit for all labs on file."
                }
              }
            }
            """),

        new LlmToolDefinition(
            "get_vitals",
            "Vital-signs Observations (blood pressure, heart rate). Optionally bounded to an interval.",
            """
            {
              "type": "object",
              "properties": {
                "since_date": {
                  "type": "string",
                  "description": "FHIR date-prefixed filter, e.g. 'ge2026-01-01'. Omit for all vitals on file."
                }
              }
            }
            """),

        new LlmToolDefinition(
            "get_recent_encounters",
            "A thin list (date, type, reason) of the most recent encounters. Request detail on a " +
            "specific one explicitly rather than assuming it from this list.",
            """
            {
              "type": "object",
              "properties": {
                "count": {
                  "type": "integer",
                  "minimum": 1,
                  "maximum": 20,
                  "description": "How many of the most recent encounters to return. Defaults to 3."
                }
              }
            }
            """),

        new LlmToolDefinition(
            "get_documents",
            "Echo/EF reports and device interrogation narratives (DiagnosticReport and " +
            "DocumentReference). Values here are narrative text, not structured fields - any " +
            "specific value you extract from them (e.g. an ejection-fraction percentage) must be " +
            "labeled as derived, not stated as a directly-recorded fact.",
            """
            {
              "type": "object",
              "properties": {
                "document_type": {
                  "type": "string",
                  "description": "Case-insensitive substring filter on document type, e.g. 'echo'. Omit for all document types."
                }
              }
            }
            """),
    ];
}
