using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision
{
    PASS,
    PASS_WITH_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyAdditionalInstrumentFinalPreRunGate(
    string GateId,
    DateTimeOffset CreatedAtUtc,
    string RequestedByOperatorId,
    string Reason,
    string Symbol,
    string SlashSymbol,
    string PlanningSecurityId,
    string SecurityIdSource,
    string EnvironmentName,
    string VenueProfileName,
    string RequestMode,
    string SymbolEncodingMode,
    int MarketDepth,
    string SourceFinalReadinessPath,
    string SourceExecutionPlanPath,
    string SourceOperatorSignoffPath,
    string SourceExecutionChecklistPath,
    string SourceFinalReadinessDecision,
    string SourceExecutionPlanDecision,
    string SourceOperatorSignoffDecision,
    string SourceExecutionChecklistDecision,
    bool OneInstrumentAtATime,
    bool BatchExecutionAllowed,
    bool ExternalRunAuthorized,
    bool CanRunExternalSnapshot,
    bool EligibleForManualSnapshotAttempt,
    bool IsApprovedForExternalRun,
    bool SchedulerOrPolling,
    bool RuntimeShadowReplaySubmit,
    bool OrderSubmission,
    bool TradingMutation,
    bool GatewayRegistration,
    string ApiWorkerGatewayMode,
    bool NoSensitiveContent,
    LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision FinalDecision);

public sealed record LmaxReadOnlyAdditionalInstrumentFinalPreRunGateCheck(
    string Name,
    LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision Decision,
    string Detail);

public sealed record LmaxReadOnlyAdditionalInstrumentFinalPreRunGateValidation(
    LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision FinalDecision,
    LmaxReadOnlyAdditionalInstrumentFinalPreRunGate Gate,
    IReadOnlyList<LmaxReadOnlyAdditionalInstrumentFinalPreRunGateCheck> Checks);

public static class LmaxReadOnlyAdditionalInstrumentFinalPreRunGateValidator
{
    private static readonly Regex SensitivePattern = new(
        "(password\\s*[:=]|secret\\s*[:=]|token\\s*[:=]|apikey\\s*[:=]|api_key\\s*[:=]|privatekey\\s*[:=]|private_key\\s*[:=]|authorization\\s*[:=]|bearer\\s+|\\b553=|\\b554=|host\\s*=|user\\s*=|account\\s*=|raw\\s*fix|sendercompid|targetcompid)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AuthorizationOrRuntimePattern = new(
        "(NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatus|SubmitOrder|production\\s+(run|environment|authorization|execution)|uat\\s+(run|environment|authorization|execution)|environmentName\"?\\s*[:=]\\s*\"?(Production|UAT)|run\\s+is\\s+authorized|external\\s+run\\s+authorized|can\\s+run\\s+external|batch\\s+execution\\s+allowed|automatic\\s+retry|run\\s+automatically|ReplaySubmitAsync|PeriodicTimer|LmaxScheduler|MarketDataPolling|SecurityListPolling)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyAdditionalInstrumentFinalPreRunGateValidation Validate(
        LmaxReadOnlyAdditionalInstrumentFinalPreRunGate gate,
        string rawText = "")
    {
        var checks = new List<LmaxReadOnlyAdditionalInstrumentFinalPreRunGateCheck>();
        var supported = LmaxReadOnlyAdditionalInstrumentSnapshotClosureValidator.TryGetDefinition(gate.Symbol, out var definition);
        checks.Add(Check("SupportedSymbol", supported, "Symbol must be a supported additional instrument."));
        if (supported)
        {
            checks.Add(Check("InstrumentIdentity", gate.SlashSymbol == definition.SlashSymbol && gate.PlanningSecurityId == definition.SecurityId, "Slash symbol and SecurityID must match the supported DemoLondon value."));
        }

        checks.AddRange([
            Check("OperatorAndReason", HasText(gate.RequestedByOperatorId) && HasText(gate.Reason), "Operator id and reason are required."),
            Check("SecurityIdSource8", gate.SecurityIdSource == "8", "SecurityIDSource must be 8."),
            Check("Tokyo600xNotSelected", !gate.PlanningSecurityId.StartsWith("6", StringComparison.Ordinal), "Tokyo 600x variants must not be selected for DemoLondon."),
            Check("DemoProfile", gate.EnvironmentName == "Demo" && gate.VenueProfileName == "DemoLondon", "Environment must be Demo / DemoLondon."),
            Check("SnapshotProfile", gate.RequestMode == "SnapshotPlusUpdates" && gate.SymbolEncodingMode == "SecurityIdOnly" && gate.MarketDepth == 1, "Snapshot profile must be SnapshotPlusUpdates / SecurityIdOnly / MarketDepth=1."),
            Check("SourceFinalReadinessPresent", HasText(gate.SourceFinalReadinessPath), "Source final-readiness path is required."),
            Check("SourceFinalReadinessPass", gate.SourceFinalReadinessDecision == "PASS", "Source final readiness must be PASS."),
            Check("SourcePlanningArtifactsSafe", SourceDecisionSafe(gate.SourceExecutionPlanDecision, "PASS", allowMissing: true) && SourceDecisionSafe(gate.SourceOperatorSignoffDecision, "SignedForPlanning", allowMissing: true) && SourceDecisionSafe(gate.SourceExecutionChecklistDecision, "PASS", allowMissing: true), "Optional source execution plan/signoff/checklist decisions must be safe if present."),
            Check("ManualSingleInstrumentOnly", gate.OneInstrumentAtATime && !gate.BatchExecutionAllowed, "One-instrument-at-a-time must be true and batch execution false."),
            Check("RunEligibilityFalse", !gate.ExternalRunAuthorized && !gate.CanRunExternalSnapshot && !gate.EligibleForManualSnapshotAttempt && !gate.IsApprovedForExternalRun, "Run authorization and eligibility flags must remain false."),
            Check("RuntimePowerFalse", !gate.SchedulerOrPolling && !gate.RuntimeShadowReplaySubmit && !gate.OrderSubmission && !gate.TradingMutation && !gate.GatewayRegistration, "Scheduler, runtime replay submit, order, mutation, and gateway registration flags must remain false."),
            Check("FakeGatewayOnly", gate.ApiWorkerGatewayMode == "FakeLmaxGateway", "API/Worker gateway mode must remain FakeLmaxGateway."),
            Check("NoSensitiveContentTrue", gate.NoSensitiveContent, "noSensitiveContent must be true."),
            Check("FinalDecisionPass", gate.FinalDecision == LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision.PASS, "Complete safe final pre-run gate should be PASS.")
        ]);

        var combined = string.Join(" ",
            gate.GateId,
            gate.RequestedByOperatorId,
            gate.Reason,
            gate.Symbol,
            gate.SlashSymbol,
            gate.PlanningSecurityId,
            gate.SecurityIdSource,
            gate.EnvironmentName,
            gate.VenueProfileName,
            gate.RequestMode,
            gate.SymbolEncodingMode,
            gate.SourceFinalReadinessPath,
            gate.SourceExecutionPlanPath,
            gate.SourceOperatorSignoffPath,
            gate.SourceExecutionChecklistPath,
            gate.SourceFinalReadinessDecision,
            gate.SourceExecutionPlanDecision,
            gate.SourceOperatorSignoffDecision,
            gate.SourceExecutionChecklistDecision,
            gate.ApiWorkerGatewayMode,
            rawText);

        if (SensitivePattern.IsMatch(combined))
        {
            checks.Add(Fail("NoSensitiveText", "Final pre-run gate contains credential-shaped or raw FIX content."));
        }

        if (AuthorizationOrRuntimePattern.IsMatch(combined))
        {
            checks.Add(Fail("NoAuthorizationOrRuntimeText", "Final pre-run gate must not imply current authorization, automation, order, scheduler, production/UAT, runtime replay submit, or batch execution."));
        }

        var final = checks.Any(x => x.Decision == LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision.FAIL)
            ? LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision.FAIL
            : LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision.PASS;

        return new(final, gate with { FinalDecision = final }, checks);
    }

    public static LmaxReadOnlyAdditionalInstrumentFinalPreRunGateValidation ValidateJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var gate = new LmaxReadOnlyAdditionalInstrumentFinalPreRunGate(
            GetString(root, "gateId", GetString(root, "readinessId", "unknown")),
            GetDateTime(root, "createdAtUtc") ?? DateTimeOffset.UtcNow,
            GetString(root, "requestedByOperatorId"),
            GetString(root, "reason"),
            GetString(root, "symbol"),
            GetString(root, "slashSymbol"),
            GetString(root, "planningSecurityId"),
            GetString(root, "securityIdSource"),
            GetString(root, "environmentName"),
            GetString(root, "venueProfileName"),
            GetString(root, "requestMode"),
            GetString(root, "symbolEncodingMode"),
            GetInt(root, "marketDepth"),
            GetString(root, "sourceFinalReadinessPath", GetString(root, "sourceFinalReadinessFile")),
            GetString(root, "sourceExecutionPlanPath"),
            GetString(root, "sourceOperatorSignoffPath"),
            GetString(root, "sourceExecutionChecklistPath"),
            GetString(root, "sourceFinalReadinessDecision", GetString(root, "readinessDecision")),
            GetString(root, "sourceExecutionPlanDecision", GetString(root, "executionPlanDecision")),
            GetString(root, "sourceOperatorSignoffDecision", GetString(root, "operatorSignoffDecision")),
            GetString(root, "sourceExecutionChecklistDecision"),
            GetBool(root, "oneInstrumentAtATime"),
            GetBool(root, "batchExecutionAllowed"),
            GetBool(root, "externalRunAuthorized"),
            GetBool(root, "canRunExternalSnapshot"),
            GetBool(root, "eligibleForManualSnapshotAttempt"),
            GetBool(root, "isApprovedForExternalRun"),
            GetBool(root, "schedulerOrPolling"),
            GetBool(root, "runtimeShadowReplaySubmit"),
            GetBool(root, "orderSubmission"),
            GetBool(root, "tradingMutation"),
            GetBool(root, "gatewayRegistration"),
            GetString(root, "apiWorkerGatewayMode"),
            GetBool(root, "noSensitiveContent"),
            ParseDecision(GetString(root, "finalDecision", GetString(root, "readinessDecision"))));
        return Validate(gate, json);
    }

    private static bool SourceDecisionSafe(string actual, string expected, bool allowMissing)
        => string.Equals(actual, expected, StringComparison.Ordinal)
            || (allowMissing && string.IsNullOrWhiteSpace(actual));

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    private static LmaxReadOnlyAdditionalInstrumentFinalPreRunGateCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlyAdditionalInstrumentFinalPreRunGateCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision.FAIL, detail);

    private static string GetString(JsonElement root, string name, string fallback = "")
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? fallback,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => fallback
        };
    }

    private static int GetInt(JsonElement root, string name, int fallback = 0)
        => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : fallback;

    private static bool GetBool(JsonElement root, string name, bool fallback = false)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => fallback
        };
    }

    private static DateTimeOffset? GetDateTime(JsonElement root, string name)
        => root.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(value.GetString(), out var timestamp)
                ? timestamp
                : null;

    private static LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision ParseDecision(string value)
        => Enum.TryParse<LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision>(value, ignoreCase: false, out var decision)
            ? decision
            : LmaxReadOnlyAdditionalInstrumentFinalPreRunGateDecision.FAIL;
}
