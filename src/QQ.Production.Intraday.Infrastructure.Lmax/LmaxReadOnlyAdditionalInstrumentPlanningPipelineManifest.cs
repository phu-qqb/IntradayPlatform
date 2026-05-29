using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision
{
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyAdditionalInstrumentPlanningPipelineInstrument(
    string Symbol,
    string SlashSymbol,
    string PlanningSecurityId,
    string SecurityIdSource,
    bool PlanningValuePresent,
    string SafetyGateDecision,
    string PreflightDecision,
    string ApprovalEnvelopeDecision,
    string DryRunDecision,
    string AttemptGateDecision,
    string ExecutionPlanDecision,
    string OperatorSignoffDecision,
    string FinalReadinessDecision,
    string ApprovalEnvelopePath,
    string DryRunReportPath,
    string AttemptGatePath,
    string ExecutionPlanPath,
    string OperatorSignoffPath,
    string FinalReadinessPath,
    bool IsApprovedForExternalRun,
    bool EligibleForManualSnapshotAttempt,
    bool CanRunExternalSnapshot,
    bool ExternalConnectionAttempted,
    bool SnapshotAttempted,
    bool ReplayAttempted,
    bool OrderSubmissionAttempted,
    bool ShadowReplaySubmitAttempted,
    bool TradingMutationAttempted,
    bool SchedulerStarted,
    bool NoSensitiveContent);

public sealed record LmaxReadOnlyAdditionalInstrumentPlanningPipelineManifest(
    string ManifestId,
    DateTimeOffset CreatedAtUtc,
    string RequestedByOperatorId,
    string ReviewedByOperatorId,
    string Reason,
    string SourcePlanningManifestPath,
    string SourceSafetyGateManifestPath,
    string SourcePreflightManifestPath,
    IReadOnlyList<LmaxReadOnlyAdditionalInstrumentPlanningPipelineInstrument> Instruments,
    int InstrumentCount,
    int ReadyForFutureManualConsiderationCount,
    int ExecutableCount,
    bool IsApprovedForExternalRun,
    bool CanRunExternalSnapshot,
    bool EligibleForManualSnapshotAttempt,
    bool ExternalConnectionAttempted,
    bool SnapshotAttempted,
    bool ReplayAttempted,
    bool SchedulerStarted,
    bool OrderSubmissionAttempted,
    bool ShadowReplaySubmitAttempted,
    bool TradingMutationAttempted,
    string ApiWorkerGatewayMode,
    bool NoSensitiveContent,
    LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision FinalDecision);

public sealed record LmaxReadOnlyAdditionalInstrumentPlanningPipelineCheck(
    string Name,
    LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision Decision,
    string Detail);

public sealed record LmaxReadOnlyAdditionalInstrumentPlanningPipelineValidation(
    LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision FinalDecision,
    IReadOnlyList<LmaxReadOnlyAdditionalInstrumentPlanningPipelineCheck> Checks);

public static class LmaxReadOnlyAdditionalInstrumentPlanningPipelineManifestValidator
{
    private static readonly IReadOnlyDictionary<string, (string SlashSymbol, string SecurityId)> Expected = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
    {
        ["GBPUSD"] = ("GBP/USD", "4002"),
        ["EURGBP"] = ("EUR/GBP", "4003"),
        ["USDJPY"] = ("USD/JPY", "4004"),
        ["AUDUSD"] = ("AUD/USD", "4007")
    };

    private static readonly Regex SensitivePattern = new("(password\\s*[:=]|secret\\s*[:=]|token\\s*[:=]|apikey\\s*[:=]|api_key\\s*[:=]|privatekey\\s*[:=]|private_key\\s*[:=]|authorization\\s*[:=]|bearer\\s+|\\b553=|\\b554=|host\\s*=|user\\s*=|account\\s*=)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OrderPattern = new("(newordersingle|ordercancelrequest|ordercancelreplacerequest|orderstatusrequest|submitorder|order submission)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyAdditionalInstrumentPlanningPipelineValidation Validate(
        LmaxReadOnlyAdditionalInstrumentPlanningPipelineManifest manifest,
        string rawManifestText = "")
    {
        var checks = new List<LmaxReadOnlyAdditionalInstrumentPlanningPipelineCheck>
        {
            Check("InstrumentCount", manifest.InstrumentCount == 4 && manifest.Instruments.Count == 4, "Pipeline must cover all four additional instruments."),
            Check("ExecutableCountZero", manifest.ExecutableCount == 0, "No instrument may be executable."),
            Check("ReadyCount", manifest.ReadyForFutureManualConsiderationCount == 4, "All four instruments should be ready for future manual consideration."),
            Check("AggregateNonExecutable", !manifest.IsApprovedForExternalRun && !manifest.CanRunExternalSnapshot && !manifest.EligibleForManualSnapshotAttempt, "Aggregate executable flags must remain false."),
            Check("AggregateAttemptFlagsFalse", !manifest.ExternalConnectionAttempted && !manifest.SnapshotAttempted && !manifest.ReplayAttempted && !manifest.SchedulerStarted && !manifest.OrderSubmissionAttempted && !manifest.ShadowReplaySubmitAttempted && !manifest.TradingMutationAttempted, "Aggregate attempt/mutation flags must remain false."),
            Check("FakeGatewayOnly", manifest.ApiWorkerGatewayMode == "FakeLmaxGateway", "API/Worker gateway mode must remain FakeLmaxGateway."),
            Check("NoSensitiveContent", manifest.NoSensitiveContent, "noSensitiveContent must be true."),
            Check("FinalDecisionPass", manifest.FinalDecision == LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision.PASS, "Final decision must be PASS when complete.")
        };

        foreach (var expected in Expected)
        {
            var instrument = manifest.Instruments.FirstOrDefault(x => x.Symbol.Equals(expected.Key, StringComparison.OrdinalIgnoreCase));
            checks.Add(Check($"InstrumentPresent:{expected.Key}", instrument is not null, $"{expected.Key} must be present."));
            if (instrument is null)
            {
                continue;
            }

            checks.Add(Check($"InstrumentScope:{expected.Key}", instrument.SlashSymbol == expected.Value.SlashSymbol && instrument.PlanningSecurityId == expected.Value.SecurityId && instrument.SecurityIdSource == "8", $"{expected.Key} must have expected slash symbol, SecurityID, and source 8."));
            checks.Add(Check($"PlanningValue:{expected.Key}", instrument.PlanningValuePresent, $"{expected.Key} planning value must be present."));
            checks.Add(Check($"Decisions:{expected.Key}", instrument.SafetyGateDecision == "PASS" && instrument.PreflightDecision == "PASS" && instrument.ApprovalEnvelopeDecision == "AcceptedForPlanning" && instrument.DryRunDecision == "PASS" && instrument.AttemptGateDecision == "PASS" && instrument.ExecutionPlanDecision == "PASS" && instrument.OperatorSignoffDecision == "SignedForPlanning" && instrument.FinalReadinessDecision == "PASS", $"{expected.Key} source decisions must be safe expected values."));
            checks.Add(Check($"Artifacts:{expected.Key}", HasPath(instrument.ApprovalEnvelopePath) && HasPath(instrument.DryRunReportPath) && HasPath(instrument.AttemptGatePath) && HasPath(instrument.ExecutionPlanPath) && HasPath(instrument.OperatorSignoffPath) && HasPath(instrument.FinalReadinessPath), $"{expected.Key} must reference every planning pipeline artifact."));
            checks.Add(Check($"NonExecutable:{expected.Key}", !instrument.IsApprovedForExternalRun && !instrument.EligibleForManualSnapshotAttempt && !instrument.CanRunExternalSnapshot, $"{expected.Key} executable flags must remain false."));
            checks.Add(Check($"NoAttempts:{expected.Key}", !instrument.ExternalConnectionAttempted && !instrument.SnapshotAttempted && !instrument.ReplayAttempted && !instrument.OrderSubmissionAttempted && !instrument.ShadowReplaySubmitAttempted && !instrument.TradingMutationAttempted && !instrument.SchedulerStarted, $"{expected.Key} attempt/mutation flags must remain false."));
            checks.Add(Check($"Sanitized:{expected.Key}", instrument.NoSensitiveContent, $"{expected.Key} noSensitiveContent must be true."));
        }

        if (SensitivePattern.IsMatch(rawManifestText))
        {
            checks.Add(Fail("NoSensitiveText", "Manifest contains credential-shaped content."));
        }

        if (OrderPattern.IsMatch(rawManifestText))
        {
            checks.Add(Fail("NoOrderSurfaceText", "Manifest contains order message surface."));
        }

        var final = checks.Any(x => x.Decision == LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision.FAIL)
            ? LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision.FAIL
            : LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision.PASS;
        return new(final, checks);
    }

    private static bool HasPath(string value) => !string.IsNullOrWhiteSpace(value);

    private static LmaxReadOnlyAdditionalInstrumentPlanningPipelineCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlyAdditionalInstrumentPlanningPipelineCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlyAdditionalInstrumentPlanningPipelineDecision.FAIL, detail);
}
