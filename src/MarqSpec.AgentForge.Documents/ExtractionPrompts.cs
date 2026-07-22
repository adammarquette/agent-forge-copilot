using MarqSpec.AgentForge.Data.Entities;

namespace MarqSpec.AgentForge.Documents;

/// <summary>Doc-type extraction prompts. The schema described here mirrors the strict contracts exactly.</summary>
internal static class ExtractionPrompts
{
    /// <summary>The user-turn instruction accompanying the document content block.</summary>
    public const string UserInstruction =
        "Extract the structured data from the attached document. Return ONLY the JSON object.";

    /// <summary>Returns the system prompt for the given document type.</summary>
    public static string SystemFor(ClinicalDocumentType documentType) => documentType switch
    {
        ClinicalDocumentType.LabPdf => LabSystem,
        ClinicalDocumentType.IntakeForm => IntakeSystem,
        _ => throw new ArgumentOutOfRangeException(nameof(documentType), documentType, "Unknown document type."),
    };

    private const string GroundingRules = """
        Rules:
        - Return ONLY the JSON object. No markdown, no code fences, no commentary.
        - Every fact MUST include a "citation" whose "quote" is text copied VERBATIM from the document that
          shows the value, and whose "page" is the 1-based page it appears on.
        - Each "citation" MUST also include a "bounding_box": [x, y, width, height] locating where the "quote"
          appears on that page. Coordinates are normalized to the page: (0,0) is the top-left corner and
          (1,1) is the bottom-right, so every number is between 0 and 1 and width/height are fractions of the
          page size. Enclose the quoted text as tightly as you reasonably can; give your best visual estimate
          of its position. Locating printed text is not inventing data. Use null for "bounding_box" ONLY if you
          genuinely cannot see where the quote sits on the page.
        - Use null for any field that is not present. NEVER invent, infer, or normalize a value that is not
          printed. If you cannot read a value, omit that item rather than guessing.
        """;

    private const string LabSystem = $$"""
        You are a clinical document extraction service. You are given a laboratory report (PDF or image).
        Extract every lab result into a single JSON object with exactly this shape (snake_case keys):

        {
          "tests": [
            {
              "test_name": string,
              "value": string,
              "unit": string | null,
              "reference_range": string | null,
              "collection_date": string | null,
              "abnormal_flag": boolean | null,
              "citation": { "page": integer, "quote": string, "bounding_box": [number, number, number, number] | null }
            }
          ]
        }

        {{GroundingRules}}
        """;

    private const string IntakeSystem = $$"""
        You are a clinical document extraction service. You are given a patient intake form (PDF or image).
        Extract it into a single JSON object with exactly this shape (snake_case keys):

        {
          "demographics": { "full_name": string | null, "date_of_birth": string | null, "sex": string | null },
          "chief_concern": string | null,
          "current_medications": [ { "name": string, "dose": string | null, "citation": { "page": integer, "quote": string, "bounding_box": [number, number, number, number] | null } } ],
          "allergies": [ string ],
          "family_history": [ string ],
          "citation": { "page": integer, "quote": string, "bounding_box": [number, number, number, number] | null }
        }

        {{GroundingRules}}
        """;
}
