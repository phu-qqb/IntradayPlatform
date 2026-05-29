using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesIntradayCycleOperatorReviewTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task R007_archived_cycle_can_be_reviewed_by_operator_decision_fixture()
    {
        var result = await ReviewAsync(OperatorDecisionType.Hold, OperatorDecisionReasonCategory.HeldDueToMissingMarks);

        Assert.True(result.Persisted);
        Assert.False(result.AlreadyRecorded);
        Assert.Equal("cycle-r008-sample", result.Decision.CycleRunId);
        Assert.Equal("qubes-r008-sample", result.Decision.QubesRunId);
    }

    [Fact]
    public async Task Approve_decision_is_recorded_without_external_action()
    {
        var result = await ReviewAsync(
            OperatorDecisionType.Approve,
            OperatorDecisionReasonCategory.AcceptedForPaperReview,
            missingMarksAcknowledged: true,
            driftAcknowledged: true);

        Assert.Equal(OperatorDecisionStatus.Recorded, result.Decision.DecisionStatus);
        Assert.Equal(CycleReviewStatus.ApprovedForPaperReview, result.Decision.ResultingCycleReviewStatus);
        Assert.False(result.Decision.CallsBrokerGateway);
        Assert.False(result.Decision.RequestsLiveMarketData);
        Assert.False(result.Decision.SubmitsOrders);
        Assert.False(result.Decision.EnablesLiveTrading);
    }

    [Fact]
    public async Task Hold_decision_is_recorded_for_missing_and_stale_marks()
    {
        var result = await ReviewAsync(OperatorDecisionType.Hold, OperatorDecisionReasonCategory.HeldDueToMissingMarks);

        Assert.Equal(CycleReviewStatus.Held, result.Decision.ResultingCycleReviewStatus);
        Assert.True(result.Decision.MissingStaleMarkWarningsPreserved);
        Assert.Equal(OperatorDecisionReasonCategory.HeldDueToMissingMarks, result.Decision.ReasonCategory);
    }

    [Fact]
    public async Task Request_data_fix_decision_is_recorded_safely()
    {
        var result = await ReviewAsync(OperatorDecisionType.RequestDataFix, OperatorDecisionReasonCategory.DataFixRequested);

        Assert.Equal(CycleReviewStatus.DataFixRequested, result.Decision.ResultingCycleReviewStatus);
        Assert.False(result.Decision.StartsSchedulerOrBackgroundJob);
        Assert.False(result.Decision.MutatesLiveTradingState);
        Assert.Contains("Data fix requested no-externally", result.Decision.GateMessage);
    }

    [Fact]
    public async Task Promote_to_paper_ready_is_non_executable()
    {
        var result = await ReviewAsync(
            OperatorDecisionType.PromoteToPaperReady,
            OperatorDecisionReasonCategory.AcceptedForPaperReview,
            missingMarksAcknowledged: true,
            driftAcknowledged: true);

        Assert.Equal(CycleReviewStatus.PaperReadyNoExternal, result.Decision.ResultingCycleReviewStatus);
        Assert.True(result.Decision.PromotionIsNoExternalPaperReadyOnly);
        Assert.False(result.Decision.CreatesExecutableOrder);
        Assert.False(result.Decision.SubmitsOrders);
    }

    [Fact]
    public async Task Promote_to_paper_ready_does_not_create_orders()
    {
        var result = await ReviewAsync(
            OperatorDecisionType.PromoteToPaperReady,
            OperatorDecisionReasonCategory.AcceptedForPaperReview,
            missingMarksAcknowledged: true,
            driftAcknowledged: true);

        Assert.False(result.Decision.CreatesOmsOrder);
        Assert.False(result.Decision.CreatesParentOrder);
        Assert.False(result.Decision.CreatesChildOrder);
        Assert.False(result.Decision.CreatesBrokerOrder);
        Assert.False(result.Decision.SubmitsOrders);
    }

    [Fact]
    public async Task Promote_to_paper_ready_does_not_mutate_live_trading_state()
    {
        var result = await ReviewAsync(
            OperatorDecisionType.PromoteToPaperReady,
            OperatorDecisionReasonCategory.AcceptedForPaperReview,
            missingMarksAcknowledged: true,
            driftAcknowledged: true);

        Assert.False(result.Decision.MutatesLiveTradingState);
        Assert.False(result.Decision.EnablesLiveTrading);
        Assert.False(result.Decision.StartsApiOrWorker);
    }

    [Fact]
    public async Task Completed_with_missing_marks_requires_acknowledgement_before_paper_promotion()
    {
        var result = await ReviewAsync(
            OperatorDecisionType.PromoteToPaperReady,
            OperatorDecisionReasonCategory.InconclusiveSafe,
            missingMarksAcknowledged: false,
            driftAcknowledged: true);

        Assert.False(result.Persisted);
        Assert.Equal(OperatorDecisionStatus.RejectedByGate, result.Decision.DecisionStatus);
        Assert.Equal(CycleReviewStatus.InconclusiveSafe, result.Decision.ResultingCycleReviewStatus);
        Assert.Contains("Missing/stale marks must be acknowledged", result.Decision.GateMessage);
    }

    [Fact]
    public async Task Drift_status_is_preserved_in_operator_review()
    {
        var result = await ReviewAsync(OperatorDecisionType.Hold, OperatorDecisionReasonCategory.HeldDueToDrift);

        Assert.True(result.Decision.TheoreticalVsRealDriftPreserved);
        Assert.Equal(TheoreticalVsRealStatus.Drift, result.ArchiveRecord.ComparatorStatus);
    }

    [Fact]
    public async Task Rebalance_intents_remain_non_executable_after_operator_decision()
    {
        var result = await ReviewAsync(
            OperatorDecisionType.PromoteToPaperReady,
            OperatorDecisionReasonCategory.AcceptedForPaperReview,
            missingMarksAcknowledged: true,
            driftAcknowledged: true);

        Assert.True(result.Decision.RebalanceIntentsRemainNonExecutable);
        Assert.False(result.ArchiveRecord.RebalanceIntentsExecutable);
    }

    [Fact]
    public async Task Duplicate_operator_decision_handling_is_idempotent()
    {
        var services = CreateServices();
        var archive = await CreateArchiveAsync(services);
        var decisionId = new OperatorDecisionId("decision-r008-duplicate");
        var request = Request(decisionId, OperatorDecisionType.Hold, OperatorDecisionReasonCategory.HeldDueToMissingMarks);

        var first = await services.Review.ReviewAsync(archive.ArchiveRecord, request, CancellationToken.None);
        var second = await services.Review.ReviewAsync(archive.ArchiveRecord, request, CancellationToken.None);

        Assert.True(first.Persisted);
        Assert.False(first.AlreadyRecorded);
        Assert.False(second.Persisted);
        Assert.True(second.AlreadyRecorded);
        Assert.Equal(OperatorDecisionStatus.DuplicateReturned, second.Decision.DecisionStatus);
        Assert.Equal(first.Decision.OperatorDecisionId, second.Decision.OperatorDecisionId);
    }

    [Fact]
    public async Task Qubes_run_id_and_cycle_run_id_are_preserved()
    {
        var result = await ReviewAsync(OperatorDecisionType.Hold, OperatorDecisionReasonCategory.HeldDueToMissingMarks);

        Assert.Equal("cycle-r008-sample", result.Decision.CycleRunId);
        Assert.Equal("qubes-r008-sample", result.Decision.QubesRunId);
        Assert.Equal(result.ArchiveRecord.CycleRunId, result.Decision.CycleRunId);
        Assert.Equal(result.ArchiveRecord.QubesRunId, result.Decision.QubesRunId);
    }

    [Fact]
    public async Task Qubes_lineage_references_are_preserved()
    {
        var result = await ReviewAsync(OperatorDecisionType.Hold, OperatorDecisionReasonCategory.HeldDueToMissingMarks);

        Assert.NotEqual(default, result.ArchiveRecord.QubesAuditBatchId);
        Assert.NotNull(result.ArchiveRecord.ModelWeightBatchId);
        Assert.NotNull(result.ArchiveRecord.ModelRunId);
        Assert.Equal(17, result.ArchiveRecord.RawQubesRowAuditCount);
        Assert.Equal(13, result.ArchiveRecord.NormalizedWeightAuditCount);
    }

    [Fact]
    public void Operator_review_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesIntradayCycleOperatorReview.cs"));

        Assert.DoesNotContain("SendOrderAsync", source);
        Assert.DoesNotContain("SubmitOrder", source);
        Assert.DoesNotContain("TcpClient", source);
        Assert.DoesNotContain("SslStream", source);
        Assert.DoesNotContain("MarketDataRequest", source);
        Assert.DoesNotContain("MarketDataResponse", source);
        Assert.DoesNotContain("FixSession", source);
    }

    [Fact]
    public void Api_and_worker_live_gateway_remain_disabled()
    {
        var apiProgram = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Api/Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Worker/Program.cs"));
        var apiSettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Api/appsettings.json"))).RootElement;
        var workerSettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Worker/appsettings.json"))).RootElement;

        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", apiProgram);
        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", workerProgram);
        Assert.DoesNotContain("RealLmaxGateway", apiProgram + workerProgram);
        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(apiSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(workerSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
    }

    [Fact]
    public void Operator_review_source_introduces_no_scheduler_timer_polling_or_background_job()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesIntradayCycleOperatorReview.cs"));

        Assert.DoesNotContain("AddHostedService", source);
        Assert.DoesNotContain("IHostedService", source);
        Assert.DoesNotContain("BackgroundService", source);
        Assert.DoesNotContain("PeriodicTimer", source);
        Assert.DoesNotContain("Task.Delay", source);
        Assert.DoesNotContain("System.Threading.Timer", source);
    }

    [Fact]
    public async Task Audusd_is_not_misclassified_as_failed()
    {
        var result = await ReviewAsync(OperatorDecisionType.Hold, OperatorDecisionReasonCategory.HeldDueToMissingMarks);
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

        Assert.True(result.ArchiveRecord.NoExternal);
        Assert.Equal(ApprovedInstrumentValidationStatus.PausedTlsBoundaryInconclusiveNotFailed, audusd.ValidationStatus);
    }

    [Fact]
    public void Usdjpy_caveat_remains_preserved()
    {
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var usdjpy = universe.Single(x => x.InternalInstrumentKey == "USDJPY");

        Assert.Equal("4004", usdjpy.SecurityId);
        Assert.Equal("8", usdjpy.SecurityIdSource);
        Assert.Equal(ApprovedInstrumentValidationStatus.NotProvenNotFailed, usdjpy.ValidationStatus);
    }

    private static async Task<IntradayCycleOperatorReviewResult> ReviewAsync(
        OperatorDecisionType decisionType,
        OperatorDecisionReasonCategory reason,
        bool missingMarksAcknowledged = false,
        bool driftAcknowledged = false)
    {
        var services = CreateServices();
        var archive = await CreateArchiveAsync(services);
        return await services.Review.ReviewAsync(
            archive.ArchiveRecord,
            Request(OperatorDecisionId.New(), decisionType, reason, missingMarksAcknowledged, driftAcknowledged),
            CancellationToken.None);
    }

    private static IntradayCycleOperatorDecisionRequest Request(
        OperatorDecisionId decisionId,
        OperatorDecisionType decisionType,
        OperatorDecisionReasonCategory reason,
        bool missingMarksAcknowledged = false,
        bool driftAcknowledged = false)
        => new(
            decisionId,
            decisionType,
            "operator-placeholder",
            reason,
            "No-external fixture review comment.",
            missingMarksAcknowledged,
            driftAcknowledged);

    private static async Task<IntradayCycleArchiveResult> CreateArchiveAsync(TestServices services)
    {
        var cycle = await services.Cycle.RunOneCycleAsync(new QubesIntradayCycleFixtureRequest(
            "cycle-r008-sample",
            new QubesRunId("qubes-r008-sample"),
            ProducedAt,
            EffectiveAt,
            "QQ_MASTER",
            "IntradayFxModel",
            PortfolioNotional,
            TargetQuantityMode.PortfolioBaseCurrencyNotional,
            SampleLines(),
            services.InstrumentIdsBySymbol,
            ProducedAt,
            EffectiveAt,
            0.0001m,
            100m,
            10m,
            TimeSpan.FromMinutes(20),
            1),
            CancellationToken.None);
        return await services.Archive.ArchiveAsync(cycle, CancellationToken.None);
    }

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var normalized = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId("qubes-r008-reference"),
            ProducedAt,
            EffectiveAt,
            15,
            "QQ_MASTER",
            "IntradayFxModel",
            PortfolioNotional,
            TargetQuantityMode.PortfolioBaseCurrencyNotional,
            SampleLines())).NormalizedWeights;
        EnsureInstruments(state, normalized.Select(x => x.Symbol));
        var idsBySymbol = normalized.ToDictionary(
            x => x.Symbol,
            x => state.Instruments.Single(instrument => instrument.Symbol.Equals(x.Symbol, StringComparison.OrdinalIgnoreCase)).Id,
            StringComparer.OrdinalIgnoreCase);
        var clock = new FixedClock(ProducedAt);
        var intradayRepository = new InMemoryIntradayRepository(state);
        var batchRepository = new InMemoryModelWeightBatchRepository(state);
        var auditRepository = new InMemoryQubesWeightAuditRepository();
        var integrity = new ReferenceDataIntegrityService(intradayRepository, clock);
        var cycle = new QubesIntradayCycleFixtureService(
            new FakeModelWeightGenerator(batchRepository, clock),
            new ModelWeightPromotionService(batchRepository, intradayRepository, integrity, clock),
            new QubesWeightPersistenceService(auditRepository, clock));
        var archive = new IntradayCycleArchiveService(new InMemoryIntradayCycleArchiveRepository(), clock);
        var review = new IntradayCycleOperatorReviewService(new InMemoryIntradayCycleOperatorDecisionRepository(), clock);

        return new TestServices(state, idsBySymbol, cycle, archive, review);
    }

    private static void EnsureInstruments(PlatformState state, IEnumerable<string> symbols)
    {
        foreach (var symbol in symbols.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (state.Instruments.Any(x => x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            state.Instruments.Add(new Instrument(
                InstrumentId.New(),
                symbol,
                AssetClass.FxSpot,
                new Currency(symbol[..3]),
                Currency.Usd,
                5,
                1));
        }
    }

    private static IReadOnlyList<string> SampleLines()
        => File.ReadAllLines(Path.Combine(RepoRoot(), "tests/fixtures/qubes-fx/qubes-fx-weights-r002-sample.csv"));

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be found.");
    }

    private sealed record TestServices(
        PlatformState State,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol,
        QubesIntradayCycleFixtureService Cycle,
        IntradayCycleArchiveService Archive,
        IntradayCycleOperatorReviewService Review);
}
