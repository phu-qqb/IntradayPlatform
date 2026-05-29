namespace QQ.Production.Intraday.Infrastructure.Lmax;

public interface ILmaxReadOnlyExternalSessionTransport
{
    Task<LmaxReadOnlyExternalSessionFakeTransportResult> ReadAsync(
        LmaxReadOnlyExternalSessionFakeTransportScript script,
        int maxEvents,
        CancellationToken cancellationToken = default);
}

public sealed record LmaxReadOnlyExternalSessionFakeTransportMessage(
    string MessageId,
    LmaxReadOnlyExternalSessionEventType EventType,
    DateTimeOffset ObservedAtUtc,
    string SanitizedPayloadJson,
    string? ClientOrderId = null,
    string? BrokerOrderId = null,
    string? BrokerExecutionId = null,
    string? InstrumentId = null,
    string? Symbol = null);

public sealed record LmaxReadOnlyExternalSessionFakeTransportScript(
    string ScriptId,
    IReadOnlyList<LmaxReadOnlyExternalSessionFakeTransportMessage> Messages)
{
    public static LmaxReadOnlyExternalSessionFakeTransportScript Empty(string scriptId = "empty")
        => new(scriptId, []);
}

public sealed record LmaxReadOnlyExternalSessionFakeTransportResult(
    string ScriptId,
    IReadOnlyList<LmaxReadOnlyExternalSessionEvent> Events,
    LmaxReadOnlyExternalSessionCounters Counters,
    bool MaxEventsReached,
    string Message);

public sealed class LmaxReadOnlyExternalSessionFakeTransport : ILmaxReadOnlyExternalSessionTransport
{
    public Task<LmaxReadOnlyExternalSessionFakeTransportResult> ReadAsync(
        LmaxReadOnlyExternalSessionFakeTransportScript script,
        int maxEvents,
        CancellationToken cancellationToken = default)
    {
        if (maxEvents <= 0)
        {
            return Task.FromResult(new LmaxReadOnlyExternalSessionFakeTransportResult(
                script.ScriptId,
                [],
                new LmaxReadOnlyExternalSessionCounters(0, 0, 0, 0, 0, 0),
                MaxEventsReached: true,
                "MaxEvents must be positive; no fake messages were emitted."));
        }

        var selected = script.Messages
            .Take(maxEvents)
            .Select(x => new LmaxReadOnlyExternalSessionEvent(
                x.MessageId,
                x.EventType,
                x.ObservedAtUtc,
                x.SanitizedPayloadJson,
                x.ClientOrderId,
                x.BrokerOrderId,
                x.BrokerExecutionId,
                x.InstrumentId,
                x.Symbol))
            .ToList();
        var counters = Count(selected);
        var capped = script.Messages.Count > selected.Count;
        return Task.FromResult(new LmaxReadOnlyExternalSessionFakeTransportResult(
            script.ScriptId,
            selected,
            counters,
            capped,
            capped ? "Fake in-memory transport stopped at MaxEventsPerRun." : "Fake in-memory transport emitted all scripted read-only events."));
    }

    private static LmaxReadOnlyExternalSessionCounters Count(IReadOnlyCollection<LmaxReadOnlyExternalSessionEvent> events)
        => new(
            events.Count(x => x.EventType == LmaxReadOnlyExternalSessionEventType.MarketDataSnapshot),
            events.Count(x => x.EventType == LmaxReadOnlyExternalSessionEventType.TradeCaptureReport),
            events.Count(x => x.EventType == LmaxReadOnlyExternalSessionEventType.OrderStatusReport),
            events.Count(x => x.EventType == LmaxReadOnlyExternalSessionEventType.ProtocolReject),
            events.Count(x => x.EventType == LmaxReadOnlyExternalSessionEventType.SessionWarning),
            events.Count(x => x.EventType == LmaxReadOnlyExternalSessionEventType.SessionError));
}

public enum LmaxReadOnlyExternalSessionFakeScenario
{
    EmptyReadOnly,
    MarketDataOnly,
    TradeCaptureOnly,
    OrderStatusOnly,
    ProtocolRejectOnly,
    MixedReadOnly,
    WarningOnly,
    ErrorOnly
}

public static class LmaxReadOnlyExternalSessionFakeScenarioBuilder
{
    private static readonly DateTimeOffset Timestamp = new(2026, 05, 06, 17, 05, 00, TimeSpan.Zero);

    public static IReadOnlyList<string> ScenarioNames => Enum.GetNames<LmaxReadOnlyExternalSessionFakeScenario>();

    public static bool TryBuild(string? scenarioName, out LmaxReadOnlyExternalSessionFakeTransportScript script)
    {
        if (Enum.TryParse<LmaxReadOnlyExternalSessionFakeScenario>(scenarioName, ignoreCase: true, out var scenario))
        {
            script = Build(scenario);
            return true;
        }

        script = LmaxReadOnlyExternalSessionFakeTransportScript.Empty("unknown");
        return false;
    }

    public static LmaxReadOnlyExternalSessionFakeTransportScript Build(LmaxReadOnlyExternalSessionFakeScenario scenario)
        => scenario switch
        {
            LmaxReadOnlyExternalSessionFakeScenario.EmptyReadOnly => new("EmptyReadOnly", []),
            LmaxReadOnlyExternalSessionFakeScenario.MarketDataOnly => new("MarketDataOnly", [Market()]),
            LmaxReadOnlyExternalSessionFakeScenario.TradeCaptureOnly => new("TradeCaptureOnly", [TradeCapture()]),
            LmaxReadOnlyExternalSessionFakeScenario.OrderStatusOnly => new("OrderStatusOnly", [OrderStatus()]),
            LmaxReadOnlyExternalSessionFakeScenario.ProtocolRejectOnly => new("ProtocolRejectOnly", [ProtocolReject()]),
            LmaxReadOnlyExternalSessionFakeScenario.MixedReadOnly => new("MixedReadOnly", [Market(), TradeCapture(), OrderStatus(), ProtocolReject()]),
            LmaxReadOnlyExternalSessionFakeScenario.WarningOnly => new("WarningOnly", [Warning()]),
            LmaxReadOnlyExternalSessionFakeScenario.ErrorOnly => new("ErrorOnly", [Error()]),
            _ => LmaxReadOnlyExternalSessionFakeTransportScript.Empty("Unknown")
        };

    private static LmaxReadOnlyExternalSessionFakeTransportMessage Market()
        => Message("md-preview-1", LmaxReadOnlyExternalSessionEventType.MarketDataSnapshot, """{"bestBid":1.17370,"bestAsk":1.17376,"bidSize":75,"askSize":125}""");

    private static LmaxReadOnlyExternalSessionFakeTransportMessage TradeCapture()
        => Message("tc-preview-1", LmaxReadOnlyExternalSessionEventType.TradeCaptureReport, """{"side":"1","lastQty":0.1,"lastPx":1.17372,"tradeDate":"20260506","transactTimeUtc":"2026-05-06T17:05:00Z","securityIdSource":"8","tradeReportId":"TR-PREVIEW-1"}""", brokerExecutionId: "EX-PREVIEW-1");

    private static LmaxReadOnlyExternalSessionFakeTransportMessage OrderStatus()
        => Message("os-preview-1", LmaxReadOnlyExternalSessionEventType.OrderStatusReport, """{"orderStatus":"Filled","cumQty":0.1,"leavesQty":0,"execId":"status-preview-1","executionType":"OrderStatus","securityIdSource":"8"}""");

    private static LmaxReadOnlyExternalSessionFakeTransportMessage ProtocolReject()
        => Message("reject-preview-1", LmaxReadOnlyExternalSessionEventType.ProtocolReject, """{"refMsgType":"AD","refSeqNum":"4","rejectContext":"ReadOnlyRecoveryRequest","message":"Synthetic read-only reject"}""");

    private static LmaxReadOnlyExternalSessionFakeTransportMessage Warning()
        => Message("warning-preview-1", LmaxReadOnlyExternalSessionEventType.SessionWarning, """{"message":"Synthetic session warning"}""");

    private static LmaxReadOnlyExternalSessionFakeTransportMessage Error()
        => Message("error-preview-1", LmaxReadOnlyExternalSessionEventType.SessionError, """{"message":"Synthetic session error"}""");

    private static LmaxReadOnlyExternalSessionFakeTransportMessage Message(string id, LmaxReadOnlyExternalSessionEventType eventType, string payload, string? brokerExecutionId = null)
        => new(
            id,
            eventType,
            Timestamp,
            payload,
            ClientOrderId: "CO-PREVIEW-1",
            BrokerOrderId: "BO-PREVIEW-1",
            BrokerExecutionId: brokerExecutionId,
            InstrumentId: "4001",
            Symbol: "EURUSD");
}

public sealed class LmaxReadOnlyExternalSessionFake(
    LmaxReadOnlyRuntimeAdapterOptions options,
    LmaxReadOnlyExternalSessionFakeTransportScript script,
    ILmaxReadOnlyExternalSessionTransport? transport = null) : ILmaxReadOnlyExternalSession
{
    private readonly ILmaxReadOnlyExternalSessionTransport _transport = transport ?? new LmaxReadOnlyExternalSessionFakeTransport();

    public Task<LmaxReadOnlyExternalSessionStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var safety = EvaluateFakeSafety(new LmaxReadOnlyExternalSessionRequest("status check"));
        return Task.FromResult(new LmaxReadOnlyExternalSessionStatus(
            safety.RunStatus,
            options.ImplementationMode,
            options.RequestedActivationLevel,
            ExternalSessionImplementationAvailable: true,
            SocketImplementationAvailable: false,
            ReadOnly: true,
            "LMAX read-only external session fake transport harness is in-memory only. It has no socket, no credential use, no order commands, no scheduler, and no shadow replay submit.",
            new LmaxReadOnlyExternalSessionCounters(0, 0, 0, 0, 0, 0),
            safety.Gates));
    }

    public Task<LmaxReadOnlyExternalSessionSafetyEvaluation> EvaluateSafetyAsync(LmaxReadOnlyExternalSessionRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(EvaluateFakeSafety(request));

    public async Task<LmaxReadOnlyExternalSessionResult> RunAsync(LmaxReadOnlyExternalSessionRequest request, CancellationToken cancellationToken = default)
    {
        var safety = EvaluateFakeSafety(request);
        if (!safety.Passed)
        {
            return new LmaxReadOnlyExternalSessionResult(
                safety.RunStatus,
                "LMAX read-only external fake transport run is blocked. " + safety.BlockedReason,
                ExternalSessionImplementationAvailable: true,
                SocketOpened: false,
                CredentialsUsed: false,
                EvidenceCreated: false,
                SubmittedToShadowReplay: false,
                new LmaxReadOnlyExternalSessionCounters(0, 0, 0, 0, 0, 0),
                safety);
        }

        var maxEvents = request.MaxEvents ?? options.MaxEventsPerRun;
        var transportResult = await _transport.ReadAsync(script, maxEvents, cancellationToken);
        var preview = request.PreviewEvidence
            ? new LmaxReadOnlyExternalSessionEvidencePreviewMapper().Map(transportResult)
            : null;
        return new LmaxReadOnlyExternalSessionResult(
            LmaxReadOnlyRuntimeRunStatus.Completed,
            transportResult.Message + (preview is null ? " No evidence was created and nothing was submitted to shadow replay." : " Sanitized evidence preview was created locally; nothing was submitted to shadow replay."),
            ExternalSessionImplementationAvailable: true,
            SocketOpened: false,
            CredentialsUsed: false,
            EvidenceCreated: preview is not null,
            SubmittedToShadowReplay: false,
            transportResult.Counters,
            safety)
        {
            EvidencePreview = preview
        };
    }

    private LmaxReadOnlyExternalSessionSafetyEvaluation EvaluateFakeSafety(LmaxReadOnlyExternalSessionRequest request)
    {
        var maxEvents = request.MaxEvents ?? options.MaxEventsPerRun;
        var maxRuntime = request.MaxRuntimeSeconds ?? options.MaxRuntimeSeconds;
        var gates = new List<LmaxReadOnlyRuntimeSafetyGateResult>
        {
            Gate("Enabled", options.Enabled, "true for fake transport test mode", options.Enabled ? "true" : "false"),
            Gate("ImplementationMode", options.ImplementationMode == LmaxReadOnlyRuntimeImplementationMode.FakeInMemory, "FakeInMemory", options.ImplementationMode.ToString()),
            Gate("ActivationLevel", request.RequestedActivationLevel == LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit && request.RequestedActivationLevel <= options.MaxAllowedActivationLevel, "Level4RuntimeManualReadOnlyConnectionNoReplaySubmit within max allowed level", request.RequestedActivationLevel.ToString()),
            Gate("Reason", !string.IsNullOrWhiteSpace(request.Reason), "non-empty manual reason", string.IsNullOrWhiteSpace(request.Reason) ? "missing" : "present"),
            Gate("AllowExternalConnections", !options.AllowExternalConnections, "false for no-network fake transport", options.AllowExternalConnections.ToString()),
            Gate("AllowCredentialUse", !options.AllowCredentialUse, "false for no-credential fake transport", options.AllowCredentialUse.ToString()),
            Gate("AllowOrderSubmission", !options.AllowOrderSubmission, "false", options.AllowOrderSubmission.ToString()),
            Gate("PersistToTradingTables", !options.PersistToTradingTables, "false", options.PersistToTradingTables.ToString()),
            Gate("PersistRawFixMessages", !options.PersistRawFixMessages, "false", options.PersistRawFixMessages.ToString()),
            Gate("SchedulerEnabled", !options.SchedulerEnabled, "false", options.SchedulerEnabled.ToString()),
            Gate("SubmitToShadowReplay", !options.SubmitToShadowReplay, "false", options.SubmitToShadowReplay.ToString()),
            Gate("MaxEventsPerRun", maxEvents > 0 && maxEvents <= LmaxReadOnlyRuntimeAdapterOptions.SafeMaxEventsPerRun, $"1..{LmaxReadOnlyRuntimeAdapterOptions.SafeMaxEventsPerRun}", maxEvents.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Gate("MaxRuntimeSeconds", maxRuntime > 0 && maxRuntime <= LmaxReadOnlyRuntimeAdapterOptions.SafeMaxRuntimeSeconds, $"1..{LmaxReadOnlyRuntimeAdapterOptions.SafeMaxRuntimeSeconds}", maxRuntime.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("SocketImplementationAvailable", LmaxReadOnlyRuntimeSafetyGateStatus.Informational, "false", "false in Phase 4B fake transport harness", "No socket implementation exists or is used by the fake harness.")
        };

        var failed = gates.Where(x => x.BlocksRun).Select(x => x.Name).ToList();
        var status = !options.Enabled
            ? LmaxReadOnlyRuntimeRunStatus.Disabled
            : failed.Count > 0
                ? LmaxReadOnlyRuntimeRunStatus.Blocked
                : LmaxReadOnlyRuntimeRunStatus.DryRun;
        var reason = failed.Count == 0
            ? "Fake transport safety gates passed; no socket implementation exists or is used."
            : "Blocked by fake transport safety gates: " + string.Join(", ", failed);

        return new LmaxReadOnlyExternalSessionSafetyEvaluation(status, reason, gates);
    }

    private static LmaxReadOnlyRuntimeSafetyGateResult Gate(string name, bool passed, string expected, string observed)
        => new(name, passed ? LmaxReadOnlyRuntimeSafetyGateStatus.Passed : LmaxReadOnlyRuntimeSafetyGateStatus.Failed, observed, expected, observed);
}
