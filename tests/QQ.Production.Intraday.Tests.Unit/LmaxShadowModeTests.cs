using System.Text.Json;
using System.Text.Json.Serialization;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Lmax.ConnectivityLab;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxShadowModeTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 06, 09, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Replay_with_matching_execution_report_fill_creates_info_match()
    {
        var (state, service) = CreateService();
        var fill = SeedFilledOrder(state, "exec-1", "CO-1", OrderStatus.Filled);

        var run = await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.SyntheticFixture,
            [new LmaxShadowExecutionReportInput("exec-1", "BO-1", "CO-1", "Trade", "Filled", fill.InstrumentId, "EURUSD", "Buy", 0.1m, 1.17m, 0m, 0.1m, 1.17m, Now)],
            [],
            [],
            [],
            "Unit test replay"), CancellationToken.None);

        Assert.Equal(LmaxShadowReplayStatus.Completed, run.Status);
        Assert.Contains(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.ExecutionReportMatchesInternalFill && x.Severity == LmaxShadowObservationSeverity.Info);
    }

    [Fact]
    public async Task Replay_with_missing_internal_fill_creates_warning()
    {
        var (state, service) = CreateService();

        await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.SyntheticFixture,
            [new LmaxShadowExecutionReportInput("missing-exec", "BO-2", "CO-2", "F", "Filled", null, "EURUSD", "Buy", 0.1m, 1.17m, 0m, 0.1m, 1.17m, Now)],
            [],
            [],
            [],
            "Unit test replay"), CancellationToken.None);

        Assert.Contains(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.ExecutionReportMissingInternalFill && x.Severity == LmaxShadowObservationSeverity.Warning);
    }

    [Fact]
    public async Task Trade_capture_match_creates_info_observation_and_trade_uti_warning_only()
    {
        var (state, service) = CreateService();
        var fill = SeedFilledOrder(state, "exec-2", "CO-3", OrderStatus.Filled);

        await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.SyntheticFixture,
            [],
            [new LmaxShadowTradeCaptureInput("exec-2", null, "BO-3", "CO-3", fill.InstrumentId, "EURUSD", "Buy", 0.1m, 1.17m, DateOnly.FromDateTime(Now.UtcDateTime), Now, null, true)],
            [],
            [],
            "Unit test replay"), CancellationToken.None);

        var observation = Assert.Single(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.TradeCaptureMatchesInternalFill);
        Assert.Equal(LmaxShadowObservationSeverity.Info, observation.Severity);
        Assert.Contains("TradeUTI", observation.DifferenceJson);
    }

    [Fact]
    public async Task Trade_capture_only_lab_evidence_replays_without_trading_state_mutation()
    {
        var (state, service) = CreateService();
        var beforeCounts = CaptureMutationGuardCounts(state);

        var run = await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.LabEvidenceFile,
            [],
            [new LmaxShadowTradeCaptureInput("exec-real-like", "mtf-real-like", "BO-REAL", "CO-REAL", null, "EURUSD", "Buy", 0.1m, 1.17478m, new DateOnly(2026, 5, 6), new DateTimeOffset(2026, 5, 6, 17, 2, 33, TimeSpan.Zero), null, true, new { securityId = "4001", securityIdSource = "8" })],
            [],
            [],
            "TradeCapture-only evidence replay"), CancellationToken.None);

        Assert.Equal(LmaxShadowReplayStatus.CompletedWithWarnings, run.Status);
        Assert.Equal(1, run.InputEventCount);
        var observation = Assert.Single(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.TradeCaptureMissingInternalFill);
        Assert.Equal(LmaxShadowObservationSeverity.Warning, observation.Severity);
        var policy = LmaxShadowModeService.ExtractPolicyMetadata(observation);
        Assert.Equal("LMAX_SHADOW_TC_MISSING_INTERNAL_FILL_READONLY", policy.PolicyCode);
        Assert.Equal("TradeCaptureOnly", policy.EvidenceMode);
        Assert.False(policy.CreatesExceptionCase);
        Assert.Empty(state.ExceptionCases);
        AssertMutationGuardCountsUnchanged(beforeCounts, state);
    }

    [Fact]
    public async Task Order_status_report_does_not_create_fill_observation()
    {
        var (state, service) = CreateService();
        SeedFilledOrder(state, "exec-3", "CO-4", OrderStatus.Filled);

        await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.SyntheticFixture,
            [new LmaxShadowExecutionReportInput("status-1", "BO-4", "CO-4", "I", "Filled", null, "EURUSD", "Buy", null, null, 0m, 0.1m, 1.17m, Now)],
            [],
            [],
            [],
            "Unit test replay"), CancellationToken.None);

        Assert.DoesNotContain(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.ExecutionReportMatchesInternalFill);
        Assert.Contains(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.OrderStatusMatchesInternalOrder);
    }

    [Fact]
    public async Task Protocol_reject_creates_blocking_observation_exception_and_audit()
    {
        var (state, service) = CreateService();

        await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.SyntheticFixture,
            [],
            [],
            [],
            [new LmaxShadowProtocolRejectInput("D", 21, 0, "UnknownTag", "CO-5", null)],
            "Unit test protocol reject"), CancellationToken.None);

        var observation = Assert.Single(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.ProtocolRejectObserved && x.Severity == LmaxShadowObservationSeverity.Blocking);
        var policy = LmaxShadowModeService.ExtractPolicyMetadata(observation);
        Assert.Equal("LMAX_SHADOW_PROTOCOL_REJECT_ORDER_PATH", policy.PolicyCode);
        Assert.True(policy.CreatesExceptionCase);
        Assert.Contains(state.ExceptionCases, x => x.EntityType == "LmaxShadowObservation");
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.LmaxShadowObservationCreated);
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.LmaxShadowReplayCompleted);
    }

    [Fact]
    public async Task Lifecycle_evidence_fixture_replays_to_shadow_without_treating_order_status_as_fill()
    {
        var (state, service) = CreateService();
        SeedFilledOrder(state, "aJBPhQAAAACZXTEG", "DL26050607454402", OrderStatus.Filled);
        var beforeCounts = CaptureMutationGuardCounts(state);
        var request = LoadLifecycleEvidenceFixture();

        var run = await service.ReplayAsync(request, CancellationToken.None);

        Assert.Equal(LmaxShadowInputSource.LabEvidenceFile, run.InputSource);
        Assert.Equal(LmaxShadowReplayStatus.Completed, run.Status);
        AssertMutationGuardCountsUnchanged(beforeCounts, state);
        Assert.Contains(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.ExecutionReportMatchesInternalFill);
        Assert.Contains(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.TradeCaptureMatchesInternalFill);
        Assert.Contains(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.OrderStatusMatchesInternalOrder);
        Assert.All(state.LmaxShadowObservations, x => Assert.Equal("SyntheticLifecycle", LmaxShadowModeService.ExtractPolicyMetadata(x).EvidenceMode));
        Assert.Contains(state.LmaxShadowObservations, x => LmaxShadowModeService.ExtractPolicyMetadata(x).PolicyCode == "LMAX_SHADOW_ER_FILL_MATCH");
        Assert.Contains(state.LmaxShadowObservations, x => LmaxShadowModeService.ExtractPolicyMetadata(x).PolicyCode == "LMAX_SHADOW_TC_FILL_MATCH");
        Assert.DoesNotContain(state.LmaxShadowObservations, x => x.BrokerExecutionId == "status-1" && x.Type == LmaxShadowObservationType.ExecutionReportMatchesInternalFill);
        Assert.All(state.LmaxShadowObservations, x => Assert.False(string.IsNullOrWhiteSpace(x.Fingerprint)));
    }

    [Theory]
    [InlineData("lmax-readonly-empty-evidence-v1.json", LmaxShadowReplayStatus.Completed, 0)]
    [InlineData("lmax-marketdata-only-evidence-v1.json", LmaxShadowReplayStatus.Completed, 0)]
    public async Task Empty_and_market_data_only_evidence_replay_with_zero_observations(string fixtureName, LmaxShadowReplayStatus expectedStatus, int expectedObservationCount)
    {
        var (state, service) = CreateService();
        var beforeCounts = CaptureMutationGuardCounts(state);
        var request = LoadEvidenceFixture(fixtureName);

        var run = await service.ReplayAsync(request, CancellationToken.None);

        Assert.Equal(expectedStatus, run.Status);
        Assert.Equal(0, run.InputEventCount);
        Assert.Equal(expectedObservationCount, run.ObservationCount);
        Assert.Empty(state.LmaxShadowObservations);
        AssertMutationGuardCountsUnchanged(beforeCounts, state);
    }

    [Fact]
    public async Task Trade_capture_only_evidence_fixture_replays_safely()
    {
        var (state, service) = CreateService();
        var beforeCounts = CaptureMutationGuardCounts(state);
        var request = LoadEvidenceFixture("lmax-tradecapture-only-evidence-v1.json");

        var run = await service.ReplayAsync(request, CancellationToken.None);

        Assert.Equal(LmaxShadowReplayStatus.CompletedWithWarnings, run.Status);
        Assert.Equal(1, run.InputEventCount);
        var observation = Assert.Single(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.TradeCaptureMissingInternalFill);
        var policy = LmaxShadowModeService.ExtractPolicyMetadata(observation);
        Assert.Equal("LMAX_SHADOW_TC_MISSING_INTERNAL_FILL_READONLY", policy.PolicyCode);
        Assert.Equal("TradeCaptureOnly", policy.EvidenceMode);
        Assert.False(policy.CreatesExceptionCase);
        Assert.Empty(state.ExceptionCases);
        AssertMutationGuardCountsUnchanged(beforeCounts, state);
    }

    [Fact]
    public async Task Order_status_only_evidence_is_status_only_and_does_not_create_fill()
    {
        var (state, service) = CreateService();
        var beforeCounts = CaptureMutationGuardCounts(state);
        var request = LoadEvidenceFixture("lmax-orderstatus-only-evidence-v1.json");

        var run = await service.ReplayAsync(request, CancellationToken.None);

        Assert.Equal(LmaxShadowReplayStatus.CompletedWithWarnings, run.Status);
        Assert.Equal(1, run.InputEventCount);
        var observation = Assert.Single(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.UnknownLmaxOrder);
        var policy = LmaxShadowModeService.ExtractPolicyMetadata(observation);
        Assert.Equal("LMAX_SHADOW_ORDER_STATUS_UNKNOWN_ORDER_READONLY", policy.PolicyCode);
        Assert.Equal("OrderStatusOnly", policy.EvidenceMode);
        Assert.False(policy.CreatesExceptionCase);
        Assert.Empty(state.ExceptionCases);
        Assert.DoesNotContain(state.LmaxShadowObservations, x => x.Type is LmaxShadowObservationType.ExecutionReportMatchesInternalFill or LmaxShadowObservationType.ExecutionReportMissingInternalFill);
        AssertMutationGuardCountsUnchanged(beforeCounts, state);
    }

    [Fact]
    public async Task Protocol_reject_only_evidence_fixture_creates_blocking_observation()
    {
        var (state, service) = CreateService();
        var beforeCounts = CaptureMutationGuardCounts(state);
        var request = LoadEvidenceFixture("lmax-protocolreject-only-evidence-v1.json");

        var run = await service.ReplayAsync(request, CancellationToken.None);

        Assert.Equal(LmaxShadowReplayStatus.CompletedWithWarnings, run.Status);
        Assert.Equal(1, run.InputEventCount);
        var observation = Assert.Single(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.ProtocolRejectObserved && x.Severity == LmaxShadowObservationSeverity.Blocking);
        var policy = LmaxShadowModeService.ExtractPolicyMetadata(observation);
        Assert.Equal("LMAX_SHADOW_PROTOCOL_REJECT_ORDER_PATH", policy.PolicyCode);
        Assert.Equal("ProtocolRejectOnly", policy.EvidenceMode);
        Assert.True(policy.CreatesExceptionCase);
        Assert.Single(state.ExceptionCases);
        Assert.Contains(policy.PolicyCode!, state.ExceptionCases[0].MetadataJson);
        Assert.Contains(policy.EvidenceMode!, state.ExceptionCases[0].MetadataJson);
        AssertMutationGuardCountsUnchanged(beforeCounts, state);
    }

    [Fact]
    public async Task Read_only_protocol_reject_is_warning_without_exception_case()
    {
        var (state, service) = CreateService();
        var beforeCounts = CaptureMutationGuardCounts(state);

        var run = await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.LabEvidenceFile,
            [],
            [],
            [],
            [new LmaxShadowProtocolRejectInput("AD", 568, 6, "Trade request id max length 16", null, null)],
            "Read-only reject policy test",
            "ProtocolRejectOnly"), CancellationToken.None);

        Assert.Equal(LmaxShadowReplayStatus.CompletedWithWarnings, run.Status);
        var observation = Assert.Single(state.LmaxShadowObservations);
        Assert.Equal(LmaxShadowObservationSeverity.Warning, observation.Severity);
        var policy = LmaxShadowModeService.ExtractPolicyMetadata(observation);
        Assert.Equal("LMAX_SHADOW_PROTOCOL_REJECT_READONLY", policy.PolicyCode);
        Assert.False(policy.CreatesExceptionCase);
        Assert.Empty(state.ExceptionCases);
        AssertMutationGuardCountsUnchanged(beforeCounts, state);
    }

    [Fact]
    public async Task Mixed_read_only_evidence_fixture_replays_without_mutation()
    {
        var (state, service) = CreateService();
        var beforeCounts = CaptureMutationGuardCounts(state);
        var request = LoadEvidenceFixture("lmax-mixed-readonly-evidence-v1.json");

        var run = await service.ReplayAsync(request, CancellationToken.None);

        Assert.Equal(LmaxShadowReplayStatus.CompletedWithWarnings, run.Status);
        Assert.Equal(2, run.InputEventCount);
        Assert.Contains(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.TradeCaptureMissingInternalFill);
        Assert.Contains(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.UnknownLmaxOrder);
        Assert.All(state.LmaxShadowObservations, x => Assert.Equal("MixedReadOnly", LmaxShadowModeService.ExtractPolicyMetadata(x).EvidenceMode));
        AssertMutationGuardCountsUnchanged(beforeCounts, state);
    }

    [Fact]
    public async Task Replay_deduplicates_duplicate_events_within_one_run_and_counts_them()
    {
        var (state, service) = CreateService();
        var duplicate = new LmaxShadowExecutionReportInput("dup-exec", "BO-D", "CO-D", "F", "Filled", null, "EURUSD", "Buy", 0.1m, 1.17m, 0m, 0.1m, 1.17m, Now);

        var run = await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.SyntheticFixture,
            [duplicate, duplicate],
            [],
            [],
            [],
            "Duplicate unit test replay"), CancellationToken.None);

        Assert.Equal(2, run.InputEventCount);
        Assert.Equal(1, run.UniqueEventCount);
        Assert.Equal(1, run.DuplicateEventCount);
        Assert.Single(state.LmaxShadowObservations, x => x.ReplayRunId == run.Id && x.Type == LmaxShadowObservationType.ExecutionReportMissingInternalFill);
        Assert.Single(state.LmaxShadowObservations, x => x.ReplayRunId == run.Id && x.Type == LmaxShadowObservationType.DuplicateExecutionObserved);
        Assert.All(state.LmaxShadowObservations, x => Assert.False(string.IsNullOrWhiteSpace(x.Fingerprint)));
    }

    [Fact]
    public async Task Replaying_same_payload_creates_new_run_with_same_fingerprint()
    {
        var (state, service) = CreateService();
        var request = new LmaxShadowReplayRequest(
            LmaxShadowInputSource.SyntheticFixture,
            [new LmaxShadowExecutionReportInput("repeat-exec", "BO-R", "CO-R", "F", "Filled", null, "EURUSD", "Buy", 0.1m, 1.17m, 0m, 0.1m, 1.17m, Now)],
            [],
            [],
            [],
            "Repeated unit test replay");

        var first = await service.ReplayAsync(request, CancellationToken.None);
        var second = await service.ReplayAsync(request, CancellationToken.None);

        Assert.NotEqual(first.Id, second.Id);
        var firstFingerprint = Assert.Single(state.LmaxShadowObservations, x => x.ReplayRunId == first.Id).Fingerprint;
        var secondFingerprint = Assert.Single(state.LmaxShadowObservations, x => x.ReplayRunId == second.Id).Fingerprint;
        Assert.Equal(firstFingerprint, secondFingerprint);
        var firstPolicy = LmaxShadowModeService.ExtractPolicyMetadata(Assert.Single(state.LmaxShadowObservations, x => x.ReplayRunId == first.Id)).PolicyCode;
        var secondPolicy = LmaxShadowModeService.ExtractPolicyMetadata(Assert.Single(state.LmaxShadowObservations, x => x.ReplayRunId == second.Id)).PolicyCode;
        Assert.Equal(firstPolicy, secondPolicy);
    }

    [Fact]
    public async Task Shadow_replay_only_mutates_shadow_audit_and_configured_blocking_exceptions()
    {
        var (state, service) = CreateService();
        SeedFilledOrder(state, "exec-guard", "CO-G", OrderStatus.Filled);
        var beforeCounts = CaptureMutationGuardCounts(state);

        await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.SyntheticFixture,
            [new LmaxShadowExecutionReportInput("missing-guard", "BO-G", "CO-G2", "F", "Filled", null, "EURUSD", "Buy", 0.1m, 1.17m, 0m, 0.1m, 1.17m, Now)],
            [],
            [],
            [],
            "Mutation guard replay"), CancellationToken.None);

        AssertMutationGuardCountsUnchanged(beforeCounts, state);
        Assert.Single(state.LmaxShadowReplayRuns);
        Assert.NotEmpty(state.LmaxShadowObservations);
        Assert.NotEmpty(state.OperatorAuditEvents);
    }

    [Fact]
    public async Task Duplicate_blocking_observation_in_same_replay_creates_one_exception_case()
    {
        var (state, service) = CreateService();
        var reject = new LmaxShadowProtocolRejectInput("D", 21, 0, "UnknownTag", "CO-B", null);

        var run = await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.SyntheticFixture,
            [],
            [],
            [],
            [reject, reject],
            "Duplicate blocking replay"), CancellationToken.None);

        Assert.Equal(2, run.InputEventCount);
        Assert.Equal(1, run.UniqueEventCount);
        Assert.Equal(1, run.DuplicateEventCount);
        var observation = Assert.Single(state.LmaxShadowObservations, x => x.Type == LmaxShadowObservationType.ProtocolRejectObserved);
        var exceptionCase = Assert.Single(state.ExceptionCases);
        Assert.Contains(observation.Fingerprint, exceptionCase.MetadataJson);
        Assert.Contains(observation.Id.Value.ToString("D"), exceptionCase.MetadataJson);
        Assert.Contains(run.Id.Value.ToString("D"), exceptionCase.MetadataJson);
    }

    [Fact]
    public async Task Warning_observation_does_not_create_exception_case_by_default()
    {
        var (state, service) = CreateService();

        await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.SyntheticFixture,
            [new LmaxShadowExecutionReportInput("warn-exec", "BO-W", "CO-W", "F", "Filled", null, "EURUSD", "Buy", 0.1m, 1.17m, 0m, 0.1m, 1.17m, Now)],
            [],
            [],
            [],
            "Warning replay"), CancellationToken.None);

        Assert.Contains(state.LmaxShadowObservations, x => x.Severity == LmaxShadowObservationSeverity.Warning);
        Assert.Empty(state.ExceptionCases);
    }

    [Fact]
    public void Lifecycle_evidence_fixture_is_sanitized()
    {
        var path = FindRepoFile("tests", "fixtures", "lmax-shadow", "lmax-fix-lifecycle-evidence-v1.json");
        var json = File.ReadAllText(path);

        Assert.DoesNotContain("554=", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("authorization", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Observation_actions_require_reason_and_create_audit()
    {
        var (state, service) = CreateService();
        await service.ReplayAsync(new LmaxShadowReplayRequest(
            LmaxShadowInputSource.SyntheticFixture,
            [],
            [],
            [],
            [new LmaxShadowProtocolRejectInput("D", 21, 0, "UnknownTag", "CO-6", null)],
            "Unit test protocol reject"), CancellationToken.None);
        var observation = state.LmaxShadowObservations[0];

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => service.AcknowledgeObservationAsync(observation.Id, "", CancellationToken.None));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => service.ResolveObservationAsync(observation.Id, "", CancellationToken.None));
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => service.IgnoreObservationAsync(observation.Id, "", CancellationToken.None));
        var updated = await service.AcknowledgeObservationAsync(observation.Id, "Operator reviewed", CancellationToken.None);

        Assert.Equal(LmaxShadowObservationStatus.Acknowledged, updated.Status);
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.LmaxShadowObservationAcknowledged && x.Reason == "Operator reviewed");
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.LmaxShadowObservationAcknowledged && x.MetadataJson?.Contains(observation.Fingerprint, StringComparison.OrdinalIgnoreCase) == true);
        var resolved = await service.ResolveObservationAsync(observation.Id, "Issue resolved", CancellationToken.None);
        Assert.Equal(LmaxShadowObservationStatus.Resolved, resolved.Status);
        Assert.Contains(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.LmaxShadowObservationResolved && x.Reason == "Issue resolved");
        await Assert.ThrowsAsync<DomainRuleViolationException>(() => service.IgnoreObservationAsync(observation.Id, "Too late", CancellationToken.None));
    }

    private static (PlatformState State, ILmaxShadowReplayService Service) CreateService()
    {
        var state = new PlatformState();
        var context = new StaticOperatorContext(OperatorAuditActorType.Operator, "local-admin", "Local Admin", "corr-shadow", "req-shadow");
        var clock = new FixedClock(Now);
        var audit = new OperatorAuditService(new InMemoryOperatorAuditRepository(state), context, clock);
        var intraday = new InMemoryIntradayRepository(state);
        var exceptionCases = new ExceptionCaseService(new InMemoryExceptionCaseRepository(state), audit, context, clock, intraday);
        var service = new LmaxShadowModeService(new InMemoryLmaxShadowRepository(state), intraday, audit, exceptionCases, context, clock);
        return (state, service);
    }

    private static Fill SeedFilledOrder(PlatformState state, string brokerExecutionId, string clientOrderId, OrderStatus childStatus)
    {
        var fundId = FundId.New();
        var instrumentId = InstrumentId.New();
        var venueId = VenueId.New();
        var tradeIntentId = TradeIntentId.New();
        var parent = new ParentOrder(ParentOrderId.New(), tradeIntentId, new ClientOrderId(clientOrderId), OrderSide.Buy, 0.1m, ExecutionAlgo.MarketImmediate, childStatus, Now);
        var child = new ChildOrder(ChildOrderId.New(), parent.Id, venueId, new ClientOrderId(clientOrderId), OrderSide.Buy, OrderType.Market, TimeInForce.IOC, 0.1m, 0.1m, childStatus, Now);
        var report = new ExecutionReport(ExecutionReportId.New(), child.Id, venueId, "BO-1", brokerExecutionId, new ClientOrderId(clientOrderId), ExecutionReportType.Fill, 0.1m, 1.17m, 0m, 0.1m, 1.17m, Now);
        var fill = new Fill(FillId.New(), brokerExecutionId, child.Id, instrumentId, venueId, TradeSide.Buy, 0.1m, 0.1m, 1.17m, Now, Now);
        state.Funds.Add(new Fund(fundId, "QQ_MASTER", Currency.Usd));
        state.Instruments.Add(new Instrument(instrumentId, "EURUSD", AssetClass.FxSpot, Currency.Eur, Currency.Usd, 5, 1));
        state.Venues.Add(new Venue(venueId, "LMAX", VenueType.Broker));
        state.ParentOrders.Add(parent);
        state.ChildOrders.Add(child);
        state.ExecutionReports.Add(report);
        state.Fills.Add(fill);
        return fill;
    }

    private static LmaxShadowReplayRequest LoadLifecycleEvidenceFixture()
        => LoadEvidenceFixture("lmax-fix-lifecycle-evidence-v1.json");

    private static LmaxShadowReplayRequest LoadEvidenceFixture(string fileName)
    {
        var path = FindRepoFile("tests", "fixtures", "lmax-shadow", fileName);
        var validation = LmaxEvidenceContractValidator.ValidateAndNormalize(File.ReadAllText(path));
        Assert.True(validation.IsValid);
        using var document = JsonDocument.Parse(validation.NormalizedJson);
        var root = document.RootElement;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());

        return new LmaxShadowReplayRequest(
            LmaxShadowInputSource.LabEvidenceFile,
            DeserializeList<LmaxShadowExecutionReportInput>(root, "executionReports", options),
            DeserializeList<LmaxShadowTradeCaptureInput>(root, "tradeCaptureReports", options),
            DeserializeList<LmaxShadowOrderStatusInput>(root, "orderStatuses", options),
            DeserializeList<LmaxShadowProtocolRejectInput>(root, "protocolRejects", options),
            "Replay LMAX lab lifecycle evidence fixture");
    }

    private static IReadOnlyList<T> DeserializeList<T>(JsonElement root, string propertyName, JsonSerializerOptions options)
        => root.TryGetProperty(propertyName, out var value)
            ? JsonSerializer.Deserialize<IReadOnlyList<T>>(value.GetRawText(), options) ?? []
            : [];

    private static string FindRepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repo file {Path.Combine(parts)} from {AppContext.BaseDirectory}.");
    }

    private static Dictionary<string, int> CaptureMutationGuardCounts(PlatformState state)
        => new()
        {
            ["ParentOrders"] = state.ParentOrders.Count,
            ["ChildOrders"] = state.ChildOrders.Count,
            ["Fills"] = state.Fills.Count,
            ["PositionLedger"] = state.PositionLedger.Count,
            ["RiskDecisions"] = state.RiskDecisions.Count,
            ["ReconciliationBreaks"] = state.ReconciliationBreaks.Count,
            ["ModelRuns"] = state.ModelRuns.Count,
            ["TargetPositions"] = state.TargetPositions.Count,
            ["DriftSnapshots"] = state.DriftSnapshots.Count
        };

    private static void AssertMutationGuardCountsUnchanged(IReadOnlyDictionary<string, int> before, PlatformState state)
    {
        var after = CaptureMutationGuardCounts(state);
        foreach (var (key, value) in before)
        {
            Assert.Equal(value, after[key]);
        }
    }
}
