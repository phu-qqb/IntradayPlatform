using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxShadowReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 05, 06, 10, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Default_options_disable_reader_and_run_is_blocked()
    {
        var (state, reader) = CreateReader(new LmaxShadowReaderOptions());

        var status = await reader.GetStatusAsync(CancellationToken.None);
        var result = await reader.RunAsync(new LmaxShadowReaderRunRequest("Unit test blocked run"), CancellationToken.None);

        Assert.Equal(LmaxShadowReaderStatus.Disabled, status.Status);
        Assert.Equal(LmaxShadowReaderStatus.Disabled, result.Status);
        Assert.False(result.Executed);
        Assert.False(result.Connected);
        Assert.False(result.ExternalConnectionAttempted);
        Assert.False(result.CredentialsUsed);
        Assert.False(result.OrdersSubmitted);
        Assert.False(result.PersistedToTradingTables);
        AssertFailedGate(result, "Enabled");
        AssertFailedGate(result, "ImplementationMode");
        Assert.Contains("Enabled", result.BlockedReason, StringComparison.Ordinal);
        var auditEvent = Assert.Single(state.OperatorAuditEvents, x => x.EventType == OperatorAuditEventType.LmaxShadowReaderRunBlocked);
        Assert.Equal("local-admin", auditEvent.ActorId);
        Assert.Equal("Unit test blocked run", auditEvent.Reason);
        Assert.Contains("failedGateNames", auditEvent.MetadataJson ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("Enabled", auditEvent.MetadataJson ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("password", auditEvent.MetadataJson ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reader_blocks_when_external_connections_not_allowed()
    {
        var (_, reader) = CreateReader(new LmaxShadowReaderOptions { Enabled = true });

        var result = await reader.RunAsync(new LmaxShadowReaderRunRequest("Unit test external blocked"), CancellationToken.None);

        Assert.Equal(LmaxShadowReaderStatus.Blocked, result.Status);
        AssertFailedGate(result, "AllowExternalConnections");
        Assert.False(result.ExternalConnectionAttempted);
    }

    [Fact]
    public async Task Reader_blocks_when_credentials_are_not_explicitly_allowed()
    {
        var (_, reader) = CreateReader(new LmaxShadowReaderOptions
        {
            Enabled = true,
            AllowExternalConnections = true
        });

        var result = await reader.RunAsync(new LmaxShadowReaderRunRequest("Unit test credential blocked"), CancellationToken.None);

        Assert.Equal(LmaxShadowReaderStatus.Blocked, result.Status);
        AssertFailedGate(result, "AllowCredentialUse");
        Assert.False(result.CredentialsUsed);
    }

    [Fact]
    public async Task Reader_reports_multiple_failed_gates_for_contradictory_config()
    {
        var (_, reader) = CreateReader(new LmaxShadowReaderOptions
        {
            Enabled = true,
            AllowExternalConnections = true,
            AllowCredentialUse = true,
            AllowOrderSubmission = true,
            PersistToTradingTables = true,
            PersistRawFixMessages = true,
            ReadOnly = false,
            DryRun = false,
            MaxEventsPerRun = 0
        });

        var result = await reader.RunAsync(new LmaxShadowReaderRunRequest("Unit test unsafe flags"), CancellationToken.None);

        Assert.Equal(LmaxShadowReaderStatus.Blocked, result.Status);
        AssertFailedGate(result, "AllowOrderSubmission");
        AssertFailedGate(result, "PersistToTradingTables");
        AssertFailedGate(result, "PersistRawFixMessages");
        AssertFailedGate(result, "ReadOnly");
        AssertFailedGate(result, "DryRun");
        AssertFailedGate(result, "MaxEventsPerRun");
        AssertFailedGate(result, "ImplementationMode");
        Assert.False(result.OrdersSubmitted);
        Assert.False(result.PersistedToTradingTables);
    }

    [Fact]
    public async Task Reader_blocks_when_read_only_is_false()
    {
        var (_, reader) = CreateReader(new LmaxShadowReaderOptions
        {
            Enabled = true,
            AllowExternalConnections = true,
            AllowCredentialUse = true,
            ReadOnly = false
        });

        var result = await reader.RunAsync(new LmaxShadowReaderRunRequest("Unit test readonly blocked"), CancellationToken.None);

        Assert.Equal(LmaxShadowReaderStatus.Blocked, result.Status);
        AssertFailedGate(result, "ReadOnly");
    }

    [Fact]
    public async Task Reader_blocks_when_request_disables_dry_run()
    {
        var (_, reader) = CreateReader(new LmaxShadowReaderOptions
        {
            Enabled = true,
            AllowExternalConnections = true,
            AllowCredentialUse = true
        });

        var result = await reader.RunAsync(new LmaxShadowReaderRunRequest("Unit test dry run blocked", DryRun: false), CancellationToken.None);

        Assert.Equal(LmaxShadowReaderStatus.Blocked, result.Status);
        AssertFailedGate(result, "DryRun");
    }

    [Fact]
    public async Task Reader_blocks_absurdly_high_requested_max_events()
    {
        var (_, reader) = CreateReader(new LmaxShadowReaderOptions
        {
            Enabled = true,
            AllowExternalConnections = true,
            AllowCredentialUse = true,
            MaxEventsPerRun = 10
        });

        var result = await reader.RunAsync(new LmaxShadowReaderRunRequest("Unit test max events blocked", MaxEvents: 10_000), CancellationToken.None);

        Assert.Equal(LmaxShadowReaderStatus.Blocked, result.Status);
        AssertFailedGate(result, "MaxEventsPerRun");
        Assert.Contains("10000", result.SafetyChecks.Single(x => x.Gate == "MaxEventsPerRun").ObservedValue, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reader_never_mutates_internal_trading_state()
    {
        var (state, reader) = CreateReader(new LmaxShadowReaderOptions());
        SeedState(state);
        var before = CaptureMutationGuardCounts(state);
        var auditBefore = state.OperatorAuditEvents.Count;
        var exceptionBefore = state.ExceptionCases.Count;

        await reader.RunAsync(new LmaxShadowReaderRunRequest("Unit test mutation guard"), CancellationToken.None);

        AssertMutationGuardCountsUnchanged(before, state);
        Assert.Empty(state.LmaxShadowReplayRuns);
        Assert.Empty(state.LmaxShadowObservations);
        Assert.Equal(auditBefore + 1, state.OperatorAuditEvents.Count);
        Assert.Equal(exceptionBefore, state.ExceptionCases.Count);
    }

    [Fact]
    public void Api_and_worker_remain_fakelmax_only_and_do_not_reference_connectivity_lab()
    {
        var root = FindRepoRoot();
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));

        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", apiProgram, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxVenueGateway", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxVenueGateway", workerProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectivityLab", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectivityLab", workerProgram, StringComparison.Ordinal);
    }

    private static void AssertFailedGate(LmaxShadowReaderRunResult result, string gate)
        => Assert.Contains(result.SafetyChecks, x => x.Gate == gate && x.Status == LmaxShadowReaderSafetyGateStatus.Failed && !x.Passed && !string.IsNullOrWhiteSpace(x.ObservedValue) && !string.IsNullOrWhiteSpace(x.ExpectedValue));

    private static (PlatformState State, ILmaxShadowReader Reader) CreateReader(LmaxShadowReaderOptions options)
    {
        var state = new PlatformState();
        var context = new StaticOperatorContext(OperatorAuditActorType.Operator, "local-admin", "Local Admin", "corr-reader", "req-reader");
        var audit = new OperatorAuditService(new InMemoryOperatorAuditRepository(state), context, new FixedClock(Now));
        return (state, new DisabledLmaxShadowReader(options, audit));
    }

    private static void SeedState(PlatformState state)
    {
        var fundId = FundId.New();
        var instrumentId = InstrumentId.New();
        var venueId = VenueId.New();
        var parent = new ParentOrder(ParentOrderId.New(), TradeIntentId.New(), new ClientOrderId("CO-READER"), OrderSide.Buy, 0.1m, ExecutionAlgo.MarketImmediate, OrderStatus.Filled, Now);
        var child = new ChildOrder(ChildOrderId.New(), parent.Id, venueId, new ClientOrderId("CO-READER"), OrderSide.Buy, OrderType.Market, TimeInForce.IOC, 0.1m, 0.1m, OrderStatus.Filled, Now);
        state.Funds.Add(new Fund(fundId, "QQ_MASTER", Currency.Usd));
        state.Instruments.Add(new Instrument(instrumentId, "EURUSD", AssetClass.FxSpot, Currency.Eur, Currency.Usd, 5, 1));
        state.Venues.Add(new Venue(venueId, "LMAX", VenueType.Broker));
        state.ParentOrders.Add(parent);
        state.ChildOrders.Add(child);
        state.Fills.Add(new Fill(FillId.New(), "EXEC-READER", child.Id, instrumentId, venueId, TradeSide.Buy, 0.1m, 0.1m, 1.17m, Now, Now));
        var modelRunId = ModelRunId.New();
        state.PositionLedger.Add(new PositionLedgerEvent(Guid.NewGuid(), fundId, instrumentId, PositionLedgerEventType.Fill, 0.1m, "EXEC-READER", Now));
        state.ModelRuns.Add(new ModelRun(modelRunId, fundId, "IntradayFxModel", Now, Now, Now, 15, 1_000_000m, ModelRunStatus.Processed, "hash", "reader-test", true));
        state.TargetPositions.Add(new TargetPosition(modelRunId, instrumentId, 1_000m, 0.1m, 0.1m, TargetQuantityMode.PortfolioBaseCurrencyNotional));
        state.DriftSnapshots.Add(new DriftSnapshot(modelRunId, instrumentId, 0.1m, 0.1m, 0m, 0.1m, 0.1m, 0m));
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
            ["EodReconciliationRuns"] = state.EodReconciliationRuns.Count,
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

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "QQ.Production.Intraday.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
