using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class BrokerAuthorityReconciliationTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 25, 8, 0, 0, TimeSpan.Zero);
    private static readonly FundId Fund = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static readonly BrokerAccountId Account = new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    private static readonly VenueId Venue = new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    private static readonly InstrumentId Instrument = new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static readonly BrokerAuthorityScope Scope = new("QQ", "Intraday", "INTRADAY", "ANUBIS", "Demo", "LMAX-DEMO-1", "LMAX");

    [Fact]
    public void Clean_authoritative_state_allows_trade_and_subtracts_reserved_working_leaves()
    {
        var internalWorking = Working("C1", TradeSide.Buy, leaves: 2m, BrokerOrderLifecycleStatus.Accepted);
        var brokerWorking = Open("C1", leaves: 2m, BrokerOrderLifecycleStatus.Accepted);
        var result = Reconcile(internalPosition: 10m, brokerPosition: 10m, internalWorkingOrders: [internalWorking], brokerOpenOrders: [brokerWorking], target: 20m);

        Assert.Empty(result.Breaks);
        Assert.Equal(BrokerAuthorityOperationalGate.GO, result.Readiness.Gate);
        Assert.Equal(BrokerAuthorityReadinessDecision.CAN_TRADE, result.Readiness.Decision);
        var delta = Assert.Single(result.RemainingDeltas);
        Assert.Equal(8m, delta.RemainingDelta);
    }

    [Fact]
    public void Partial_then_final_fills_reconcile_without_breaks()
    {
        var fills = new[] { Fill("E1", 4m), Fill("E2", 6m) };
        var broker = new[] { BrokerFill("E1", 4m, leaves: 6m, BrokerExecutionEventType.PartialFill, seq: 1), BrokerFill("E2", 6m, leaves: 0m, BrokerExecutionEventType.Fill, seq: 2) };
        var result = Reconcile(internalFills: fills, brokerExecutions: broker, internalPosition: 10m, brokerPosition: 10m);

        Assert.Empty(result.Breaks);
        Assert.Equal("BROKER_AUTHORITY_CAN_TRADE", result.Readiness.StatusCode);
    }

    [Fact]
    public void Duplicate_exec_id_with_exact_payload_is_idempotent()
    {
        var broker = BrokerFill("E1", 10m, rawPayloadHash: "same");
        var result = Reconcile(internalFills: [Fill("E1", 10m)], brokerExecutions: [broker, broker with { PossDup = true }], internalPosition: 10m, brokerPosition: 10m);

        Assert.DoesNotContain(result.Breaks, x => x.Type == BrokerReconciliationBreakType.DUPLICATE_EXECUTION);
        Assert.Equal(BrokerAuthorityReadinessDecision.CAN_TRADE, result.Readiness.Decision);
    }

    [Fact]
    public void Duplicate_exec_id_with_different_payload_is_critical_and_blocks()
    {
        var result = Reconcile(
            internalFills: [Fill("E1", 10m)],
            brokerExecutions: [BrokerFill("E1", 10m, rawPayloadHash: "a"), BrokerFill("E1", 9m, rawPayloadHash: "b")],
            internalPosition: 10m,
            brokerPosition: 10m);

        Assert.Contains(result.Breaks, x => x.Type == BrokerReconciliationBreakType.DUPLICATE_EXECUTION && x.Severity == BrokerReconciliationSeverity.Critical);
        Assert.Equal(BrokerAuthorityReadinessDecision.EMERGENCY_STOP, result.Readiness.Decision);
    }

    [Fact]
    public void Possdup_out_of_order_without_sequence_gap_replays_idempotently()
    {
        var input = Input(
            internalFills: [Fill("E1", 4m), Fill("E2", 6m)],
            brokerExecutions: [BrokerFill("E2", 6m, seq: 2, possDup: true), BrokerFill("E1", 4m, seq: 1)],
            internalPosition: 10m,
            brokerPosition: 10m);
        var first = new BrokerAuthorityReconciler().Reconcile(input);
        var second = new BrokerAuthorityReconciler().Reconcile(input);

        Assert.Empty(first.Breaks);
        Assert.Equal(first.Run.InputHash, second.Run.InputHash);
        Assert.Equal(first.Readiness.StatusCode, second.Readiness.StatusCode);
    }

    [Fact]
    public void Missing_internal_fill_is_blocking_break()
    {
        var result = Reconcile(brokerExecutions: [BrokerFill("E-MISSING", 1m)], internalPosition: 0m, brokerPosition: 0m);

        Assert.Contains(result.Breaks, x => x.Type == BrokerReconciliationBreakType.MISSING_INTERNAL_FILL && x.Blocking);
        Assert.Equal(BrokerAuthorityReadinessDecision.BLOCK_NEW_ORDERS, result.Readiness.Decision);
    }

    [Fact]
    public void Missing_broker_fill_is_blocking_break()
    {
        var result = Reconcile(internalFills: [Fill("E-INTERNAL", 1m)], internalPosition: 1m, brokerPosition: 1m);

        Assert.Contains(result.Breaks, x => x.Type == BrokerReconciliationBreakType.MISSING_BROKER_FILL && x.Blocking);
        Assert.Equal(BrokerAuthorityReadinessDecision.BLOCK_NEW_ORDERS, result.Readiness.Decision);
    }

    [Fact]
    public void Position_quantity_mismatch_is_critical()
    {
        var result = Reconcile(internalPosition: 5m, brokerPosition: 7m);

        Assert.Contains(result.Breaks, x => x.Type == BrokerReconciliationBreakType.POSITION_QUANTITY_MISMATCH && x.Severity == BrokerReconciliationSeverity.Critical);
        Assert.Equal(BrokerAuthorityReadinessDecision.EMERGENCY_STOP, result.Readiness.Decision);
    }

    [Fact]
    public void Broker_open_order_missing_internally_blocks_new_orders()
    {
        var result = Reconcile(brokerOpenOrders: [Open("C-BROKER", 3m, BrokerOrderLifecycleStatus.Accepted)]);

        Assert.Contains(result.Breaks, x => x.Type == BrokerReconciliationBreakType.OPEN_ORDER_MISSING_INTERNAL);
        Assert.Equal(BrokerAuthorityReadinessDecision.BLOCK_NEW_ORDERS, result.Readiness.Decision);
    }

    [Fact]
    public void Internal_working_order_missing_at_broker_blocks_new_orders()
    {
        var result = Reconcile(internalWorkingOrders: [Working("C-INTERNAL", TradeSide.Buy, 3m, BrokerOrderLifecycleStatus.Accepted)]);

        Assert.Contains(result.Breaks, x => x.Type == BrokerReconciliationBreakType.OPEN_ORDER_MISSING_BROKER);
        Assert.Equal(BrokerAuthorityReadinessDecision.BLOCK_NEW_ORDERS, result.Readiness.Decision);
    }

    [Fact]
    public void Leaves_mismatch_blocks_new_orders()
    {
        var result = Reconcile(
            internalWorkingOrders: [Working("C1", TradeSide.Buy, 3m, BrokerOrderLifecycleStatus.Accepted)],
            brokerOpenOrders: [Open("C1", 2m, BrokerOrderLifecycleStatus.Accepted)]);

        Assert.Contains(result.Breaks, x => x.Type == BrokerReconciliationBreakType.LEAVES_MISMATCH);
    }

    [Fact]
    public void Cancel_pending_reserves_leaves_and_blocks_until_terminal_evidence()
    {
        var result = Reconcile(
            internalPosition: 10m,
            brokerPosition: 10m,
            internalWorkingOrders: [Working("C1", TradeSide.Sell, 4m, BrokerOrderLifecycleStatus.PendingCancel)],
            brokerOpenOrders: [Open("C1", 4m, BrokerOrderLifecycleStatus.Accepted)],
            target: 20m);

        Assert.Contains(result.Breaks, x => x.Type == BrokerReconciliationBreakType.UNRESOLVED_CANCEL_PENDING);
        Assert.Equal(14m, Assert.Single(result.RemainingDeltas).RemainingDelta);
    }

    [Fact]
    public void Stale_broker_snapshot_blocks()
    {
        var stale = Source("positions", BrokerSourceQuality.AUTHORITATIVE, Now.AddMinutes(-10), "pos-old");
        var result = Reconcile(positionSource: stale, internalPosition: 0m, brokerPosition: 0m);

        Assert.Contains(result.Breaks, x => x.Type == BrokerReconciliationBreakType.STALE_BROKER_SNAPSHOT);
        Assert.Equal(BrokerAuthorityOperationalGate.NO_GO, result.Readiness.Gate);
    }

    [Fact]
    public void Sequence_gap_is_critical()
    {
        var result = Reconcile(
            internalFills: [Fill("E1", 1m), Fill("E3", 1m)],
            brokerExecutions: [BrokerFill("E1", 1m, seq: 1), BrokerFill("E3", 1m, seq: 3)],
            internalPosition: 2m,
            brokerPosition: 2m);

        Assert.Contains(result.Breaks, x => x.Type == BrokerReconciliationBreakType.SEQUENCE_GAP && x.Severity == BrokerReconciliationSeverity.Critical);
    }

    [Fact]
    public void Demo_live_and_multi_account_exec_id_collision_does_not_cross_scope()
    {
        var liveScope = Scope with { Environment = "Live" };
        var otherAccount = Scope with { Account = "LMAX-DEMO-2" };
        var result = Reconcile(
            internalFills: [Fill("E1", 1m)],
            brokerExecutions:
            [
                BrokerFill("E1", 1m, scope: Scope, rawPayloadHash: "demo"),
                BrokerFill("E1", 9m, scope: liveScope, rawPayloadHash: "live"),
                BrokerFill("E1", 8m, scope: otherAccount, rawPayloadHash: "acct2")
            ],
            internalPosition: 1m,
            brokerPosition: 1m);

        Assert.DoesNotContain(result.Breaks, x => x.Type == BrokerReconciliationBreakType.DUPLICATE_EXECUTION);
        Assert.DoesNotContain(result.Breaks, x => x.Type == BrokerReconciliationBreakType.MISSING_INTERNAL_FILL);
    }

    [Fact]
    public void Manual_ui_evidence_is_not_authoritative_broker_fill()
    {
        var manual = BrokerFill("E1", 1m, quality: BrokerSourceQuality.MANUAL_EVIDENCE, source: "LMAX.UI.Export");
        var result = Reconcile(internalFills: [Fill("E1", 1m)], brokerExecutions: [manual], internalPosition: 1m, brokerPosition: 1m);

        Assert.Contains(result.Breaks, x => x.Type == BrokerReconciliationBreakType.MISSING_BROKER_FILL && x.Blocking);
        Assert.Contains(result.Breaks, x => x.Description.Contains("Manual broker UI/export", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_source_produces_no_go_not_synthetic_authority()
    {
        var missing = Source("positions", BrokerSourceQuality.UNKNOWN, Now, null, "M3B source adapter missing");
        var result = Reconcile(positionSource: missing, internalPosition: 0m, brokerPosition: 0m);

        Assert.Equal(BrokerAuthorityOperationalGate.NO_GO, result.Readiness.Gate);
        Assert.Equal("NO_GO_BROKER_AUTHORITY_SOURCE_NOT_READY", result.Readiness.StatusCode);
        Assert.Contains(result.Breaks, x => x.Type == BrokerReconciliationBreakType.UNKNOWN_ACCOUNT_SCOPE);
    }

    [Fact]
    public void Reconstructed_position_source_is_not_authority_and_blocks()
    {
        var source = Source("positions", BrokerSourceQuality.RECONSTRUCTED, Now, "pos-reconstructed");
        var result = Reconcile(positionSource: source, internalPosition: 0m, brokerPosition: 0m);

        Assert.Contains(result.Breaks, x => x.Type == BrokerReconciliationBreakType.UNACCEPTABLE_BROKER_AUTHORITY_SOURCE && x.Blocking);
        Assert.Equal(BrokerAuthorityOperationalGate.NO_GO, result.Readiness.Gate);
        Assert.False(result.Readiness.PositionAuthorityReady);
    }

    [Fact]
    public void Manual_position_source_is_not_authority_and_blocks()
    {
        var source = Source("positions", BrokerSourceQuality.MANUAL_EVIDENCE, Now, "pos-manual");
        var result = Reconcile(positionSource: source, internalPosition: 0m, brokerPosition: 0m);

        Assert.Contains(result.Breaks, x => x.Type == BrokerReconciliationBreakType.UNACCEPTABLE_BROKER_AUTHORITY_SOURCE && x.Blocking);
        Assert.Equal(BrokerAuthorityOperationalGate.NO_GO, result.Readiness.Gate);
        Assert.False(result.Readiness.PositionAuthorityReady);
    }

    [Fact]
    public void Reconstructed_position_evidence_row_is_not_authority_and_blocks()
    {
        var input = Input(internalPosition: 0m, brokerPosition: 0m) with
        {
            BrokerPositions =
            [
                new BrokerPositionSnapshotEvidence(Scope, new BrokerPositionSnapshot(Account, Instrument, 0m, Now), "EURUSD", BrokerSourceQuality.RECONSTRUCTED, "dropcopy.replay", "pos-reconstructed-row")
            ]
        };

        var result = new BrokerAuthorityReconciler().Reconcile(input);

        Assert.Contains(result.Breaks, x => x.Type == BrokerReconciliationBreakType.UNACCEPTABLE_BROKER_AUTHORITY_SOURCE && x.Description.Contains("Broker position evidence", StringComparison.Ordinal));
        Assert.Equal(BrokerAuthorityReadinessDecision.EMERGENCY_STOP, result.Readiness.Decision);
    }

    [Fact]
    public void Reconstructed_open_order_source_is_not_authority_and_blocks()
    {
        var source = Source("open-orders", BrokerSourceQuality.RECONSTRUCTED, Now, "oo-reconstructed");
        var result = Reconcile(openOrderSource: source);

        Assert.Contains(result.Breaks, x => x.Type == BrokerReconciliationBreakType.UNACCEPTABLE_BROKER_AUTHORITY_SOURCE && x.Blocking);
        Assert.Equal(BrokerAuthorityOperationalGate.NO_GO, result.Readiness.Gate);
        Assert.False(result.Readiness.OpenOrderAuthorityReady);
    }

    [Fact]
    public void Reconstructed_open_order_evidence_row_is_not_authority_and_blocks()
    {
        var input = Input(brokerOpenOrders: [Open("C-RECON", 1m, BrokerOrderLifecycleStatus.Accepted, BrokerSourceQuality.RECONSTRUCTED)]);

        var result = new BrokerAuthorityReconciler().Reconcile(input);

        Assert.Contains(result.Breaks, x => x.Type == BrokerReconciliationBreakType.UNACCEPTABLE_BROKER_AUTHORITY_SOURCE && x.Description.Contains("Broker open-order evidence", StringComparison.Ordinal));
        Assert.Equal(BrokerAuthorityReadinessDecision.EMERGENCY_STOP, result.Readiness.Decision);
    }

    private static BrokerReconciliationResult Reconcile(
        IReadOnlyList<Fill>? internalFills = null,
        IReadOnlyList<BrokerExecutionEvent>? brokerExecutions = null,
        IReadOnlyList<BrokerInternalWorkingOrderSnapshot>? internalWorkingOrders = null,
        IReadOnlyList<BrokerOpenOrderSnapshot>? brokerOpenOrders = null,
        BrokerAuthoritySourceState? executionSource = null,
        BrokerAuthoritySourceState? positionSource = null,
        BrokerAuthoritySourceState? openOrderSource = null,
        decimal internalPosition = 0m,
        decimal brokerPosition = 0m,
        decimal target = 0m)
        => new BrokerAuthorityReconciler().Reconcile(Input(
            internalFills,
            brokerExecutions,
            internalWorkingOrders,
            brokerOpenOrders,
            executionSource,
            positionSource,
            openOrderSource,
            internalPosition,
            brokerPosition,
            target));

    private static BrokerAuthorityReconciliationInput Input(
        IReadOnlyList<Fill>? internalFills = null,
        IReadOnlyList<BrokerExecutionEvent>? brokerExecutions = null,
        IReadOnlyList<BrokerInternalWorkingOrderSnapshot>? internalWorkingOrders = null,
        IReadOnlyList<BrokerOpenOrderSnapshot>? brokerOpenOrders = null,
        BrokerAuthoritySourceState? executionSource = null,
        BrokerAuthoritySourceState? positionSource = null,
        BrokerAuthoritySourceState? openOrderSource = null,
        decimal internalPosition = 0m,
        decimal brokerPosition = 0m,
        decimal target = 0m)
        => new(
            Scope,
            Now,
            executionSource ?? Source("execution", BrokerSourceQuality.AUTHORITATIVE, Now, "exec-src"),
            positionSource ?? Source("positions", BrokerSourceQuality.AUTHORITATIVE, Now, "pos-src"),
            openOrderSource ?? Source("open-orders", BrokerSourceQuality.AUTHORITATIVE, Now, "oo-src"),
            internalFills ?? [],
            [new InternalPositionSnapshot(Fund, Instrument, internalPosition, Now)],
            internalWorkingOrders ?? [],
            brokerExecutions ?? [],
            [new BrokerPositionSnapshotEvidence(Scope, new BrokerPositionSnapshot(Account, Instrument, brokerPosition, Now), "EURUSD", BrokerSourceQuality.AUTHORITATIVE, "fixture.positions", "pos-row")],
            brokerOpenOrders ?? [],
            [],
            [new BrokerTargetPosition(Instrument, "EURUSD", target)],
            TimeSpan.FromMinutes(5));

    private static BrokerAuthoritySourceState Source(string name, BrokerSourceQuality quality, DateTimeOffset asOf, string? hash, string? reason = null)
        => new(name, quality, asOf, hash, reason);

    private static Fill Fill(string execId, decimal qty, TradeSide side = TradeSide.Buy)
        => new(FillId.New(), execId, ChildOrderId.New(), Instrument, Venue, side, qty, qty, 1.1m, Now, Now);

    private static BrokerExecutionEvent BrokerFill(
        string execId,
        decimal qty,
        decimal leaves = 0m,
        BrokerExecutionEventType type = BrokerExecutionEventType.Fill,
        long? seq = null,
        bool possDup = false,
        string? rawPayloadHash = null,
        BrokerAuthorityScope? scope = null,
        BrokerSourceQuality quality = BrokerSourceQuality.AUTHORITATIVE,
        string source = "LMAX.ExecutionReport")
        => new(
            scope ?? Scope,
            Instrument,
            "EURUSD",
            "C1",
            "O1",
            execId,
            type,
            leaves == 0m ? BrokerOrderLifecycleStatus.Filled : BrokerOrderLifecycleStatus.PartiallyFilled,
            TradeSide.Buy,
            qty,
            1.1m,
            leaves,
            qty,
            Now,
            Now,
            quality,
            source,
            rawPayloadHash ?? $"hash-{execId}-{qty}-{leaves}-{source}",
            possDup,
            seq,
            rawPayloadHash);

    private static BrokerInternalWorkingOrderSnapshot Working(string clientOrderId, TradeSide side, decimal leaves, BrokerOrderLifecycleStatus status)
        => new(Scope, ChildOrderId.New(), Instrument, "EURUSD", clientOrderId, "O-" + clientOrderId, side, leaves, 0m, status, Now);

    private static BrokerOpenOrderSnapshot Open(string clientOrderId, decimal leaves, BrokerOrderLifecycleStatus status, BrokerSourceQuality quality = BrokerSourceQuality.AUTHORITATIVE)
        => new(Scope, Instrument, "EURUSD", clientOrderId, "O-" + clientOrderId, TradeSide.Buy, leaves, 0m, status, Now, quality, "fixture.open-orders", "open-" + clientOrderId);
}

