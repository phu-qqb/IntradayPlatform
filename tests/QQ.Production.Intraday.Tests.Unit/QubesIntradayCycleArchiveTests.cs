using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesIntradayCycleArchiveTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task R006_cycle_output_can_be_archived_no_externally()
    {
        var result = await CreateArchivedCycleAsync();

        Assert.True(result.Persisted);
        Assert.False(result.AlreadyArchived);
        Assert.True(result.ArchiveRecord.NoExternal);
        Assert.Equal("NoExternalFixtureOnly", result.ArchiveRecord.SafetyStatus);
    }

    [Fact]
    public async Task Cycle_run_id_and_qubes_run_id_are_preserved()
    {
        var result = await CreateArchivedCycleAsync("cycle-r007-identity", "qubes-r007-identity");

        Assert.Equal("cycle-r007-identity", result.ArchiveRecord.CycleRunId);
        Assert.Equal("qubes-r007-identity", result.ArchiveRecord.QubesRunId);
        Assert.Equal("cycle-r007-identity", result.OperatorReport.CycleRunId);
        Assert.Equal("qubes-r007-identity", result.OperatorReport.QubesRunId);
    }

    [Fact]
    public async Task Qubes_lineage_references_are_preserved()
    {
        var result = await CreateArchivedCycleAsync();

        Assert.NotEqual(default, result.ArchiveRecord.QubesAuditBatchId);
        Assert.NotNull(result.ArchiveRecord.ModelWeightBatchId);
        Assert.NotNull(result.ArchiveRecord.ModelRunId);
        Assert.Equal(17, result.ArchiveRecord.RawQubesRowAuditCount);
        Assert.Equal(13, result.ArchiveRecord.NormalizedWeightAuditCount);
    }

    [Fact]
    public async Task Target_portfolio_summary_is_archived()
    {
        var result = await CreateArchivedCycleAsync();

        Assert.Equal(13, result.ArchiveRecord.TargetPortfolioPositionCount);
        Assert.Contains("13 theoretical USD-quote target positions archived", result.OperatorReport.TargetPortfolioSummary);
    }

    [Fact]
    public async Task Theoretical_pnl_summary_is_archived()
    {
        var result = await CreateArchivedCycleAsync();

        Assert.Equal(6804.66m, Math.Round(result.ArchiveRecord.TheoreticalTotalPnl, 2));
        Assert.Equal(PnLComputationStatus.MissingMark, result.ArchiveRecord.TheoreticalPnlStatus);
        Assert.Contains("6804.66", result.OperatorReport.TheoreticalPnlSummary);
    }

    [Fact]
    public async Task Reconciliation_summary_is_archived()
    {
        var result = await CreateArchivedCycleAsync();

        Assert.Equal(13, result.ArchiveRecord.ReconciliationLineCount);
        Assert.True(result.ArchiveRecord.ReconciliationDriftCount > 0);
        Assert.True(result.ArchiveRecord.ReconciliationMissingActualCount > 0);
        Assert.True(result.ArchiveRecord.ReconciliationMissingMarkCount > 0);
        Assert.Contains("reconciliation rows archived", result.OperatorReport.ReconciliationSummary);
    }

    [Fact]
    public async Task Theoretical_vs_real_summary_is_archived()
    {
        var result = await CreateArchivedCycleAsync();

        Assert.Equal(TheoreticalVsRealStatus.Drift, result.ArchiveRecord.ComparatorStatus);
        Assert.Equal(6704.66m, Math.Round(result.ArchiveRecord.ActualFixtureTotalPnl, 2));
        Assert.Equal(-100.00m, Math.Round(result.ArchiveRecord.PnLDifference, 2));
        Assert.Contains("Comparator status Drift", result.OperatorReport.TheoreticalVsRealSummary);
    }

    [Fact]
    public async Task Rebalance_intents_are_archived_as_non_executable()
    {
        var result = await CreateArchivedCycleAsync();

        Assert.Equal(13, result.ArchiveRecord.RebalanceIntentCount);
        Assert.False(result.ArchiveRecord.RebalanceIntentsExecutable);
        Assert.Contains("non-executable", result.OperatorReport.RebalanceIntentSummary);
    }

    [Fact]
    public async Task Missing_and_stale_mark_status_is_preserved_in_warning_report()
    {
        var result = await CreateArchivedCycleAsync();

        Assert.True(result.ArchiveRecord.MissingOrStaleMarkWarningCount > 0);
        Assert.NotEmpty(result.OperatorReport.MissingStaleMarkWarnings);
        Assert.Contains("missing/stale mark warning rows are preserved", result.OperatorReport.MissingStaleMarkWarnings.Single());
        Assert.Contains("## Missing/Stale Mark Warnings", result.OperatorReportMarkdown);
    }

    [Fact]
    public async Task Operator_report_includes_no_external_and_no_trading_disclaimer()
    {
        var result = await CreateArchivedCycleAsync();

        Assert.Contains("No external broker call occurred.", result.OperatorReportMarkdown);
        Assert.Contains("No live market data was requested.", result.OperatorReportMarkdown);
        Assert.Contains("No orders were created.", result.OperatorReportMarkdown);
        Assert.Contains("No trading occurred.", result.OperatorReportMarkdown);
        Assert.Contains("fixture-based", result.OperatorReportMarkdown);
    }

    [Fact]
    public async Task Duplicate_cycle_run_id_is_idempotent()
    {
        var services = CreateServices();
        var cycle = await RunCycleAsync(services, "cycle-r007-idempotent", "qubes-r007-idempotent");
        var first = await services.Archive.ArchiveAsync(cycle, CancellationToken.None);
        var second = await services.Archive.ArchiveAsync(cycle, CancellationToken.None);

        Assert.True(first.Persisted);
        Assert.False(first.AlreadyArchived);
        Assert.False(second.Persisted);
        Assert.True(second.AlreadyArchived);
        Assert.Equal(first.ArchiveRecord.CycleRunId, second.ArchiveRecord.CycleRunId);
        Assert.Equal(first.ArchiveRecord.QubesAuditBatchId, second.ArchiveRecord.QubesAuditBatchId);
    }

    [Fact]
    public async Task No_order_submission_or_executable_order_is_archived()
    {
        var result = await CreateArchivedCycleAsync();

        Assert.False(result.ArchiveRecord.RebalanceIntentsExecutable);
        Assert.Contains("No orders were created.", result.OperatorReport.Disclaimers);
    }

    [Fact]
    public void Archive_source_introduces_no_broker_socket_tls_fix_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesIntradayCycleArchive.cs"));

        Assert.DoesNotContain("SendOrderAsync", source);
        Assert.DoesNotContain("SubmitOrder", source);
        Assert.DoesNotContain("TcpClient", source);
        Assert.DoesNotContain("SslStream", source);
        Assert.DoesNotContain("MarketDataRequest", source);
        Assert.DoesNotContain("MarketDataResponse", source);
        Assert.DoesNotContain("FixSession", source);
    }

    [Fact]
    public void Archive_source_introduces_no_scheduler_timer_polling_or_background_job()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesIntradayCycleArchive.cs"));

        Assert.DoesNotContain("AddHostedService", source);
        Assert.DoesNotContain("IHostedService", source);
        Assert.DoesNotContain("BackgroundService", source);
        Assert.DoesNotContain("PeriodicTimer", source);
        Assert.DoesNotContain("Task.Delay", source);
        Assert.DoesNotContain("System.Threading.Timer", source);
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
    public async Task Audusd_is_not_misclassified_as_failed()
    {
        var result = await CreateArchivedCycleAsync();
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

    private static async Task<IntradayCycleArchiveResult> CreateArchivedCycleAsync(
        string cycleRunId = "cycle-r007-sample",
        string qubesRunId = "qubes-r007-sample")
    {
        var services = CreateServices();
        var cycle = await RunCycleAsync(services, cycleRunId, qubesRunId);
        return await services.Archive.ArchiveAsync(cycle, CancellationToken.None);
    }

    private static async Task<QubesIntradayCycleFixtureResult> RunCycleAsync(
        TestServices services,
        string cycleRunId,
        string qubesRunId)
        => await services.Cycle.RunOneCycleAsync(new QubesIntradayCycleFixtureRequest(
            cycleRunId,
            new QubesRunId(qubesRunId),
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

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var normalized = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId("qubes-r007-reference"),
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

        return new TestServices(state, idsBySymbol, cycle, archive);
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
        IntradayCycleArchiveService Archive);
}
