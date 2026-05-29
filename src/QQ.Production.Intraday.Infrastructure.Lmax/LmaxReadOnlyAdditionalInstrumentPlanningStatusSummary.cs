using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyAdditionalInstrumentPlanningStatusDecision
{
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyAdditionalInstrumentPlanningStatusInstrument(
    string Symbol,
    string SlashSymbol,
    string PlanningSecurityId,
    string SecurityIdSource,
    string PipelineDecision,
    string PlanningManifestDecision,
    string SafetyGateDecision,
    string PreflightDecision,
    string ApprovalEnvelopeDecision,
    string DryRunDecision,
    string AttemptGateDecision,
    string ExecutionPlanDecision,
    string OperatorSignoffDecision,
    string FinalReadinessDecision,
    bool IsApprovedForExternalRun,
    bool CanRunExternalSnapshot,
    bool EligibleForManualSnapshotAttempt,
    string RecommendedNextAction);

public sealed record LmaxReadOnlyAdditionalInstrumentPlanningStatusIssue(
    string Severity,
    string Code,
    string Path,
    string Message);

public sealed record LmaxReadOnlyAdditionalInstrumentPlanningStatusSummary(
    string SummaryId,
    DateTimeOffset CreatedAtUtc,
    string AggregateDecision,
    int InstrumentCount,
    int ReadyForFutureManualConsiderationCount,
    int ExecutableCount,
    bool RuntimeShadowReplaySubmit,
    bool SchedulerOrPolling,
    bool OrderSubmission,
    bool GatewayRegistration,
    bool TradingMutation,
    string ApiWorkerGatewayMode,
    IReadOnlyList<LmaxReadOnlyAdditionalInstrumentPlanningStatusInstrument> Instruments,
    bool NoSensitiveContent,
    IReadOnlyList<LmaxReadOnlyAdditionalInstrumentPlanningStatusIssue> Issues);

public sealed record LmaxReadOnlyAdditionalInstrumentPlanningStatusValidation(
    LmaxReadOnlyAdditionalInstrumentPlanningStatusDecision FinalDecision,
    LmaxReadOnlyAdditionalInstrumentPlanningStatusSummary Summary,
    IReadOnlyList<LmaxReadOnlyAdditionalInstrumentPlanningStatusIssue> Issues);

public static class LmaxReadOnlyAdditionalInstrumentPlanningStatusSummaryValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex SensitivePattern = new("(password\\s*[:=]|secret\\s*[:=]|token\\s*[:=]|apikey\\s*[:=]|api_key\\s*[:=]|privatekey\\s*[:=]|private_key\\s*[:=]|authorization\\s*[:=]|bearer\\s+|\\b553=|\\b554=|host\\s*=|user\\s*=|account\\s*=)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyAdditionalInstrumentPlanningStatusValidation FromPipelineManifestFile(string? pipelineManifestFile, string apiWorkerGatewayMode)
    {
        if (string.IsNullOrWhiteSpace(pipelineManifestFile) || !File.Exists(pipelineManifestFile))
        {
            var missing = new LmaxReadOnlyAdditionalInstrumentPlanningStatusIssue("Warning", "PipelineManifestMissing", pipelineManifestFile ?? "", "Additional-instrument planning pipeline manifest was not found.");
            var summary = new LmaxReadOnlyAdditionalInstrumentPlanningStatusSummary(
                "lmax-readonly-additional-instrument-planning-status-missing",
                DateTimeOffset.UtcNow,
                "PASS_WITH_KNOWN_WARNINGS",
                0,
                0,
                0,
                RuntimeShadowReplaySubmit: false,
                SchedulerOrPolling: false,
                OrderSubmission: false,
                GatewayRegistration: false,
                TradingMutation: false,
                apiWorkerGatewayMode,
                Array.Empty<LmaxReadOnlyAdditionalInstrumentPlanningStatusInstrument>(),
                NoSensitiveContent: true,
                new[] { missing });
            return new(LmaxReadOnlyAdditionalInstrumentPlanningStatusDecision.PASS_WITH_KNOWN_WARNINGS, summary, new[] { missing });
        }

        var raw = File.ReadAllText(pipelineManifestFile);
        var manifest = JsonSerializer.Deserialize<LmaxReadOnlyAdditionalInstrumentPlanningPipelineManifest>(raw, JsonOptions)
            ?? throw new InvalidOperationException("Could not deserialize additional-instrument planning pipeline manifest.");
        return FromPipelineManifest(manifest, raw, apiWorkerGatewayMode);
    }

    public static LmaxReadOnlyAdditionalInstrumentPlanningStatusValidation FromPipelineManifest(
        LmaxReadOnlyAdditionalInstrumentPlanningPipelineManifest manifest,
        string rawManifestText,
        string apiWorkerGatewayMode)
    {
        var issues = new List<LmaxReadOnlyAdditionalInstrumentPlanningStatusIssue>();
        var pipelineValidation = LmaxReadOnlyAdditionalInstrumentPlanningPipelineManifestValidator.Validate(manifest, rawManifestText);
        issues.AddRange(pipelineValidation.Checks
            .Where(x => x.Decision == LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision.FAIL)
            .Select(x => new LmaxReadOnlyAdditionalInstrumentPlanningStatusIssue("Error", x.Name, "", x.Detail)));

        if (SensitivePattern.IsMatch(rawManifestText))
        {
            issues.Add(new("Error", "SensitiveContentDetected", "", "Pipeline manifest contains credential-shaped content."));
        }

        if (!string.Equals(apiWorkerGatewayMode, "FakeLmaxGateway", StringComparison.Ordinal))
        {
            issues.Add(new("Error", "ApiWorkerGatewayNotFake", "", "API/Worker gateway mode must remain FakeLmaxGateway."));
        }

        var instruments = manifest.Instruments
            .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(x => new LmaxReadOnlyAdditionalInstrumentPlanningStatusInstrument(
                x.Symbol,
                x.SlashSymbol,
                x.PlanningSecurityId,
                x.SecurityIdSource,
                PipelineDecision: x.FinalReadinessDecision == "PASS" && !x.CanRunExternalSnapshot && !x.IsApprovedForExternalRun ? "PASS" : "FAIL",
                PlanningManifestDecision: x.PlanningValuePresent ? "AcceptedForPlanning" : "Missing",
                x.SafetyGateDecision,
                x.PreflightDecision,
                x.ApprovalEnvelopeDecision,
                x.DryRunDecision,
                x.AttemptGateDecision,
                x.ExecutionPlanDecision,
                x.OperatorSignoffDecision,
                x.FinalReadinessDecision,
                x.IsApprovedForExternalRun,
                x.CanRunExternalSnapshot,
                x.EligibleForManualSnapshotAttempt,
                "Wait for an explicit future one-instrument operator-approved market-hours phase; this summary is read-only."))
            .ToList();

        var finalDecision = issues.Any(x => x.Severity == "Error")
            ? LmaxReadOnlyAdditionalInstrumentPlanningStatusDecision.FAIL
            : LmaxReadOnlyAdditionalInstrumentPlanningStatusDecision.PASS;

        var summary = new LmaxReadOnlyAdditionalInstrumentPlanningStatusSummary(
            $"lmax-readonly-additional-instrument-planning-status-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}",
            DateTimeOffset.UtcNow,
            manifest.FinalDecision.ToString(),
            manifest.InstrumentCount,
            manifest.ReadyForFutureManualConsiderationCount,
            manifest.ExecutableCount,
            RuntimeShadowReplaySubmit: false,
            SchedulerOrPolling: false,
            OrderSubmission: false,
            GatewayRegistration: false,
            TradingMutation: false,
            apiWorkerGatewayMode,
            instruments,
            manifest.NoSensitiveContent && issues.All(x => x.Code != "SensitiveContentDetected"),
            issues);

        return new(finalDecision, summary, issues);
    }
}
