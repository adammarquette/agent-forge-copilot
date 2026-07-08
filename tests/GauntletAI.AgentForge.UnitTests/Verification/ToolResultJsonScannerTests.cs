using System.Text.Json;
using FluentAssertions;
using GauntletAI.AgentForge.Integration.OpenEmr.Fhir;
using GauntletAI.AgentForge.Mcp;
using GauntletAI.AgentForge.Verification;

namespace GauntletAI.AgentForge.UnitTests.Verification;

public sealed class ToolResultJsonScannerTests
{
    private static readonly JsonSerializerOptions SerializeOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void Scan_PatientSummaryResultJson_CollectsCitationsForEveryNestedResource()
    {
        var result = new PatientSummaryResult(
            Patient: null,
            ActiveProblems: [new ConditionRecord(new ClinicalSourceRef("Condition", "1"), "Atrial fibrillation", "active", null)],
            ActiveMedications: [new MedicationRecord(new ClinicalSourceRef("MedicationRequest", "456"), "Warfarin 5mg", "5mg daily", "active", null)],
            Allergies: []);
        var json = JsonSerializer.Serialize(result, SerializeOptions);

        var scan = ToolResultJsonScanner.Scan([json]);

        scan.Citations.Should().Contain(["Condition/1", "MedicationRequest/456"]);
    }

    [Fact]
    public void Scan_PatientSummaryResultJson_ReconstructsTypedMedicationsAndProblems()
    {
        var result = new PatientSummaryResult(
            Patient: null,
            ActiveProblems: [new ConditionRecord(new ClinicalSourceRef("Condition", "1"), "Atrial fibrillation", "active", null)],
            ActiveMedications: [new MedicationRecord(new ClinicalSourceRef("MedicationRequest", "456"), "Warfarin 5mg", "5mg daily", "active", null)],
            Allergies: []);
        var json = JsonSerializer.Serialize(result, SerializeOptions);

        var scan = ToolResultJsonScanner.Scan([json]);

        scan.Input.ActiveMedications.Should().ContainSingle().Which.MedicationDisplay.Should().Be("Warfarin 5mg");
        scan.Input.ActiveProblems.Should().ContainSingle().Which.ProblemDisplay.Should().Be("Atrial fibrillation");
    }

    [Fact]
    public void Scan_LabsResultJson_ReconstructsTypedObservations()
    {
        var result = new LabsResult([new ObservationRecord(new ClinicalSourceRef("Observation", "9"), "laboratory", "INR", 3.5, null, 0.8, 1.2, null, "final")]);
        var json = JsonSerializer.Serialize(result, SerializeOptions);

        var scan = ToolResultJsonScanner.Scan([json]);

        scan.Input.Labs.Should().ContainSingle().Which.CodeDisplay.Should().Be("INR");
        scan.Citations.Should().Contain("Observation/9");
    }

    [Fact]
    public void Scan_MultipleToolResultBlobs_MergesEverythingAcrossAllOfThem()
    {
        var labsJson = JsonSerializer.Serialize(
            new LabsResult([new ObservationRecord(new ClinicalSourceRef("Observation", "9"), "laboratory", "INR", 3.5, null, 0.8, 1.2, null, "final")]),
            SerializeOptions);
        var summaryJson = JsonSerializer.Serialize(
            new PatientSummaryResult(null, [], [new MedicationRecord(new ClinicalSourceRef("MedicationRequest", "1"), "Warfarin 5mg", null, "active", null)], []),
            SerializeOptions);

        var scan = ToolResultJsonScanner.Scan([labsJson, summaryJson]);

        scan.Input.Labs.Should().ContainSingle();
        scan.Input.ActiveMedications.Should().ContainSingle();
        scan.Citations.Should().Contain(["Observation/9", "MedicationRequest/1"]);
    }

    [Fact]
    public void Scan_MalformedOrErrorShapedBlob_SkipsItRatherThanThrowing()
    {
        var act = () => ToolResultJsonScanner.Scan(["""{"error":"tool call failed"}""", "not even json"]);

        act.Should().NotThrow();
        var scan = ToolResultJsonScanner.Scan(["""{"error":"tool call failed"}""", "not even json"]);
        scan.Citations.Should().BeEmpty();
        scan.Input.ActiveMedications.Should().BeEmpty();
    }

    [Fact]
    public void Scan_NoToolResults_ReturnsEmptyScan()
    {
        var scan = ToolResultJsonScanner.Scan([]);

        scan.Citations.Should().BeEmpty();
        scan.Input.ActiveMedications.Should().BeEmpty();
        scan.Input.Labs.Should().BeEmpty();
        scan.Input.ActiveProblems.Should().BeEmpty();
    }
}
