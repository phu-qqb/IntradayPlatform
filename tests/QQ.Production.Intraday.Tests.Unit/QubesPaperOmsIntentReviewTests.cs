using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesPaperOmsIntentReviewTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task Promote_to_paper_ready_cycle_can_enter_paper_oms_review()
    {
        var context = await CreateContextAsync(OperatorDecisionType.PromoteToPaperReady, missingMarksAcknowledged: true, driftAcknowledged: true);
        var report = CreateReport(context);

        Assert.True(report.PromoteToPaperReadyGatePresent);
        Assert.False(report.CycleWithoutPromotionBlocked);
        Assert.NotEmpty(report.Lines);
        Assert.True(report.Status is PaperOmsReviewStatus.AcceptedForPaperReview or PaperOmsReviewStatus.InconclusiveSafe);
    }

    [Fact]
    public async Task Cycle_without_promote_to_paper_ready_is_blocked()
    {
        var context = await CreateContextAsync(OperatorDecisionType.Hold);
        var report = CreateReport(context);

        Assert.False(report.PromoteToPaperReadyGatePresent);
        Assert.True(report.CycleWithoutPromotionBlocked);
        Assert.Equal(PaperOmsReviewStatus.Blocked, report.Status);
        Assert.All(report.Lines, x => Assert.Contains(x.Checks, check => check.Result == PaperPreTradeRiskResultCategory.BlockedMissingPromotion));
    }

    [Fact]
    public async Task Rebalance_intents_remain_non_executable()
    {
        var context = await CreateContextAsync(OperatorDecisionType.PromoteToPaperReady, missingMarksAcknowledged: true, driftAcknowledged: true);
        var report = CreateReport(context);

        Assert.True(report.RebalanceIntentsRemainNonExecutable);
        Assert.DoesNotContain(report.Lines, x => x.IsExecutable);
    }

    [Fact]
    public async Task Executable_intent_is_blocked()
    {
        var context = await CreateContextAsync(OperatorDecisionType.PromoteToPaperReady, missingMarksAcknowledged: true, driftAcknowledged: true);
        var candidates = PaperOmsIntentReviewService.CreateCandidates(context.Cycle.TheoreticalPortfolioDiff.RebalanceIntents).ToList();
        candidates[0] = candidates[0] with { IsExplicitlyNonExecutable = false };
        var report = CreateReport(context, candidates);

        Assert.Contains(report.Lines, x => x.Result == PaperPreTradeRiskResultCategory.BlockedIntentExecutable);
        Assert.False(report.RebalanceIntentsRemainNonExecutable);
    }

    [Fact]
    public async Task Missing_mark_blocks_affected_intent_line()
    {
        var context = await CreateContextAsync(OperatorDecisionType.PromoteToPaperReady, missingMarksAcknowledged: true, driftAcknowledged: true);
        var report = CreateReport(context);

        Assert.Contains(report.Lines, x => x.Symbol == "JPYUSD" && x.Result == PaperPreTradeRiskResultCategory.BlockedMissingMark);
    }

    [Fact]
    public async Task Stale_mark_blocks_affected_intent_line()
    {
        var context = await CreateContextAsync(OperatorDecisionType.PromoteToPaperReady, missingMarksAcknowledged: true, driftAcknowledged: true);
        var report = CreateReport(context);

        Assert.Contains(report.Lines, x => x.Symbol == "NOKUSD" && x.Result == PaperPreTradeRiskResultCategory.BlockedStaleMark);
    }

    [Fact]
    public async Task Max_delta_weight_limit_is_enforced()
    {
        var context = await CreateContextAsync(OperatorDecisionType.PromoteToPaperReady, missingMarksAcknowledged: true, driftAcknowledged: true);
        var report = CreateReport(context, limits: Limits(maxDeltaWeight: 0.001m, approvedSymbols: ApprovedSymbols(context)));

        Assert.Contains(report.Lines, x => x.Result == PaperPreTradeRiskResultCategory.BlockedLimitExceeded);
    }

    [Fact]
    public async Task Max_target_weight_limit_is_enforced()
    {
        var context = await CreateContextAsync(OperatorDecisionType.PromoteToPaperReady, missingMarksAcknowledged: true, driftAcknowledged: true);
        var report = CreateReport(context, limits: Limits(maxTargetWeight: 0.001m, approvedSymbols: ApprovedSymbols(context)));

        Assert.Contains(report.Lines, x => x.Result == PaperPreTradeRiskResultCategory.BlockedLimitExceeded);
    }

    [Fact]
    public async Task Max_notional_change_limit_is_enforced()
    {
        var context = await CreateContextAsync(OperatorDecisionType.PromoteToPaperReady, missingMarksAcknowledged: true, driftAcknowledged: true);
        var report = CreateReport(context, limits: Limits(maxPerInstrumentNotional: 10m, maxGrossNotional: 100m, approvedSymbols: ApprovedSymbols(context)));

        Assert.Contains(report.Lines, x => x.Result == PaperPreTradeRiskResultCategory.BlockedLimitExceeded);
    }

    [Fact]
    public async Task Unsupported_instrument_is_blocked()
    {
        var context = await CreateContextAsync(OperatorDecisionType.PromoteToPaperReady, missingMarksAcknowledged: true, driftAcknowledged: true);
        var candidates = PaperOmsIntentReviewService.CreateCandidates(context.Cycle.TheoreticalPortfolioDiff.RebalanceIntents).ToList();
        candidates[0] = candidates[0] with { Symbol = "BADPAIR" };
        var report = CreateReport(context, candidates);

        Assert.Contains(report.Lines, x => x.Symbol == "BADPAIR" && x.Result == PaperPreTradeRiskResultCategory.BlockedUnsupportedInstrument);
    }

    [Fact]
    public async Task Non_approved_instrument_is_blocked()
    {
        var context = await CreateContextAsync(OperatorDecisionType.PromoteToPaperReady, missingMarksAcknowledged: true, driftAcknowledged: true);
        var approved = ApprovedSymbols(context).Where(x => !x.Equals("AUDUSD", StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var report = CreateReport(context, limits: Limits(approvedSymbols: approved));

        Assert.Contains(report.Lines, x => x.Symbol == "AUDUSD" && x.Result == PaperPreTradeRiskResultCategory.BlockedNonApprovedInstrument);
    }

    [Fact]
    public async Task Drift_acknowledgement_is_preserved()
    {
        var context = await CreateContextAsync(OperatorDecisionType.PromoteToPaperReady, missingMarksAcknowledged: true, driftAcknowledged: true);
        var report = CreateReport(context);

        Assert.True(report.DriftAcknowledgementPreserved);
        Assert.True(context.OperatorReview.Decision.TheoreticalVsRealDriftPreserved);
    }

    [Fact]
    public async Task Missing_stale_mark_acknowledgement_is_preserved()
    {
        var context = await CreateContextAsync(OperatorDecisionType.PromoteToPaperReady, missingMarksAcknowledged: true, driftAcknowledged: true);
        var report = CreateReport(context);

        Assert.True(report.MissingStaleMarkAcknowledgementPreserved);
        Assert.True(context.OperatorReview.Decision.MissingStaleMarkWarningsPreserved);
    }

    [Fact]
    public async Task Paper_review_report_preserves_cycle_run_id_and_qubes_run_id()
    {
        var context = await CreateContextAsync(OperatorDecisionType.PromoteToPaperReady, missingMarksAcknowledged: true, driftAcknowledged: true);
        var report = CreateReport(context);

        Assert.Equal("cycle-r009-sample", report.CycleRunId);
        Assert.Equal("qubes-r009-sample", report.QubesRunId);
        Assert.Equal(context.OperatorReview.Decision.OperatorDecisionId, report.OperatorDecisionId);
    }

    [Fact]
    public async Task No_oms_broker_parent_or_child_order_is_created()
    {
        var context = await CreateContextAsync(OperatorDecisionType.PromoteToPaperReady, missingMarksAcknowledged: true, driftAcknowledged: true);
        var report = CreateReport(context);

        Assert.False(report.CreatedOmsOrder);
        Assert.False(report.CreatedBrokerOrder);
        Assert.False(report.CreatedParentOrder);
        Assert.False(report.CreatedChildOrder);
        Assert.DoesNotContain(report.Lines, x => x.CreatesOrder);
    }

    [Fact]
    public async Task No_order_submission_path_is_introduced()
    {
        var context = await CreateContextAsync(OperatorDecisionType.PromoteToPaperReady, missingMarksAcknowledged: true, driftAcknowledged: true);
        var report = CreateReport(context);

        Assert.False(report.SubmittedOrders);
        Assert.False(report.CreatedExecutableOrder);
    }

    [Fact]
    public void Paper_review_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperOmsIntentReview.cs"));

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
    public void Paper_review_source_introduces_no_scheduler_timer_polling_or_background_job()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesPaperOmsIntentReview.cs"));

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
        var context = await CreateContextAsync(OperatorDecisionType.PromoteToPaperReady, missingMarksAcknowledged: true, driftAcknowledged: true);
        var report = CreateReport(context);
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

        Assert.Contains(report.Lines, x => x.Symbol == "AUDUSD");
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

    private static PaperOmsIntentReviewReport CreateReport(
        TestContext context,
        IReadOnlyList<PaperOmsIntentCandidate>? candidates = null,
        PaperPreTradeRiskLimits? limits = null)
        => new PaperOmsIntentReviewService().Review(new PaperOmsIntentReviewRequest(
            context.Cycle,
            context.OperatorReview,
            limits ?? Limits(approvedSymbols: ApprovedSymbols(context)),
            candidates));

    private static PaperPreTradeRiskLimits Limits(
        decimal maxDeltaWeight = 2m,
        decimal maxTargetWeight = 2m,
        decimal maxGrossNotional = 5_000_000m,
        decimal maxPerInstrumentNotional = 2_000_000m,
        IReadOnlySet<string>? approvedSymbols = null)
        => new(
            maxDeltaWeight,
            maxTargetWeight,
            maxGrossNotional,
            maxPerInstrumentNotional,
            approvedSymbols ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    private static IReadOnlySet<string> ApprovedSymbols(TestContext context)
        => context.Cycle.TheoreticalPortfolioDiff.RebalanceIntents
            .Select(x => x.Symbol)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static async Task<TestContext> CreateContextAsync(
        OperatorDecisionType decisionType,
        bool missingMarksAcknowledged = false,
        bool driftAcknowledged = false)
    {
        var services = CreateServices();
        var cycle = await services.Cycle.RunOneCycleAsync(new QubesIntradayCycleFixtureRequest(
            "cycle-r009-sample",
            new QubesRunId("qubes-r009-sample"),
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
        var archive = await services.Archive.ArchiveAsync(cycle, CancellationToken.None);
        var review = await services.OperatorReview.ReviewAsync(
            archive.ArchiveRecord,
            new IntradayCycleOperatorDecisionRequest(
                OperatorDecisionId.New(),
                decisionType,
                "operator-placeholder",
                decisionType == OperatorDecisionType.PromoteToPaperReady
                    ? OperatorDecisionReasonCategory.AcceptedForPaperReview
                    : OperatorDecisionReasonCategory.HeldDueToMissingMarks,
                "No-external paper OMS review fixture.",
                missingMarksAcknowledged,
                driftAcknowledged),
            CancellationToken.None);

        return new TestContext(cycle, archive, review);
    }

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var normalized = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId("qubes-r009-reference"),
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

    private sealed record TestContext(
        QubesIntradayCycleFixtureResult Cycle,
        IntradayCycleArchiveResult Archive,
        IntradayCycleOperatorReviewResult OperatorReview);

    private sealed record TestServices(
        PlatformState State,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol,
        QubesIntradayCycleFixtureService Cycle,
        IntradayCycleArchiveService Archive,
        IntradayCycleOperatorReviewService OperatorReview);
}
