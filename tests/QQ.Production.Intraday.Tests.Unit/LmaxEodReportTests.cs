using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxEodReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 04, 30, 16, 00, 00, TimeSpan.Zero);
    private static readonly DateOnly ReportDate = new(2026, 04, 30);

    [Fact]
    public async Task Fake_generator_writes_actual_lmax_headers()
    {
        var services = CreateServices();
        AddInternalFill(services.State);

        var result = await services.Generator.GenerateAsync(ReportDate, "LMAX", "LMAX_DEMO_LOCAL", LmaxEodMutationMode.None, CancellationToken.None);

        Assert.Equal(string.Join(",", LmaxEodHeaders.Individual), File.ReadLines(result.IndividualTradesPath).First());
        Assert.Equal(string.Join(",", LmaxEodHeaders.Summary), File.ReadLines(result.TradesSummaryPath).First());
        Assert.Equal(string.Join(",", LmaxEodHeaders.Wallet), File.ReadLines(result.CurrencyWalletsPath).First());
    }

    [Fact]
    public async Task Import_report_set_and_reconcile_clean_report_has_no_blocking_breaks()
    {
        var services = CreateServices();
        AddInternalFill(services.State);
        var generated = await services.Generator.GenerateAsync(ReportDate, "LMAX", "LMAX_DEMO_LOCAL", LmaxEodMutationMode.None, CancellationToken.None);

        var import = await services.Importer.ImportReportSetAsync(generated.IndividualTradesPath, generated.TradesSummaryPath, generated.CurrencyWalletsPath, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);
        var reconciliation = await services.Reconciliation.RunAsync(ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Imported, import.Status);
        Assert.Equal(0, import.BlockingIssueCount);
        Assert.Equal(0, reconciliation.BlockingBreakCount);
    }

    [Fact]
    public async Task Unknown_lmax_symbol_is_blocking_validation_issue()
    {
        var services = CreateServices();
        var path = Path.Combine(services.Options.DataRoot, "samples", $"individual-unknown-{Guid.NewGuid():N}.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllLinesAsync(path,
        [
            string.Join(",", LmaxEodHeaders.Individual),
            "EX1,MTF1,30-04-2026 16:00:00.000,1,1.10000,30-04-2026,9999,XXX/YYY,INST1,ORD1,,,30-04-2026 16:00:00.000,Market,LMAX,LOCAL,0,-1,LMAX_DEMO_LOCAL,10000,-11000,UTI1"
        ]);

        var result = await services.Importer.ImportIndividualTradesAsync(path, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Rejected, result.Status);
        Assert.Contains(result.Issues, x => x.IssueType == LmaxReportValidationIssueType.UnknownInstrument);
    }

    [Fact]
    public async Task Known_lmax_report_alias_imports_even_when_instrument_is_disabled_for_trading()
    {
        var services = CreateServices();
        var disabledUsdJpy = new Instrument(
            new InstrumentId(Guid.Parse("11111111-1111-1111-1111-000000004004")),
            "USDJPY",
            AssetClass.FxSpot,
            new Currency("USD"),
            new Currency("JPY"),
            3,
            2,
            IsEnabled: false);
        services.State.Instruments.Add(disabledUsdJpy);
        services.State.InstrumentAliases.Add(new InstrumentAlias(
            new InstrumentAliasId(Guid.Parse("33333333-3333-3333-3333-000000004004")),
            disabledUsdJpy.Id,
            "LMAX_REPORT",
            "USD/JPY",
            "4004",
            true,
            Now));

        var path = Path.Combine(services.Options.DataRoot, "samples", $"individual-usdjpy-disabled-{Guid.NewGuid():N}.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllLinesAsync(path,
        [
            string.Join(",", LmaxEodHeaders.Individual),
            "EX1,MTF1,30-04-2026 16:00:00.000,1,155.123,30-04-2026,4004,USD/JPY,INST1,ORD1,,,30-04-2026 16:00:00.000,Market,LMAX,LOCAL,0,-1,LMAX_DEMO_LOCAL,10000,-1551230,UTI1"
        ]);

        var result = await services.Importer.ImportIndividualTradesAsync(path, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Imported, result.Status);
        Assert.Equal(0, result.BlockingIssueCount);
        Assert.Single(services.State.LmaxIndividualTrades, x => x.InstrumentId == disabledUsdJpy.Id);
    }

    [Fact]
    public async Task Usd_jpy_alias_resolves_to_usdjpy()
    {
        var services = CreateServices();
        var usdJpy = new Instrument(
            new InstrumentId(Guid.Parse("11111111-1111-1111-1111-000000004004")),
            "USDJPY",
            AssetClass.FxSpot,
            new Currency("USD"),
            new Currency("JPY"),
            3,
            2,
            IsEnabled: false);
        services.State.Instruments.Add(usdJpy);
        services.State.InstrumentAliases.Add(new InstrumentAlias(
            new InstrumentAliasId(Guid.Parse("33333333-3333-3333-3333-000000004004")),
            usdJpy.Id,
            "LMAX_REPORT",
            "USD/JPY",
            "4004",
            true,
            Now));

        var path = Path.Combine(services.Options.DataRoot, "samples", $"individual-usdjpy-{Guid.NewGuid():N}.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllLinesAsync(path,
        [
            string.Join(",", LmaxEodHeaders.Individual),
            "EX1,MTF1,30-04-2026 16:00:00.000,1,155.123,30-04-2026,4004,USD/JPY,INST1,ORD1,,,30-04-2026 16:00:00.000,Market,LMAX,LOCAL,0,-1,LMAX_DEMO_LOCAL,10000,-1551230,UTI1"
        ]);

        await services.Importer.ImportIndividualTradesAsync(path, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Single(services.State.LmaxIndividualTrades, x => x.InstrumentId == usdJpy.Id);
    }

    [Fact]
    public async Task Wallet_pnl_summary_converts_to_usd_and_totals_net_pnl()
    {
        var services = CreateServices();
        var path = Path.Combine(services.Options.DataRoot, "samples", $"wallet-{Guid.NewGuid():N}.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllLinesAsync(path,
        [
            string.Join(",", LmaxEodHeaders.Wallet),
            "USD,1000,0,0,10,-2,1,-0.5,1008.5,1,LMAX_DEMO_LOCAL",
            "EUR,100,0,0,5,-1,0,-0.25,103.75,1.1,LMAX_DEMO_LOCAL"
        ]);

        var import = await services.Importer.ImportCurrencyWalletsAsync(path, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);
        var summary = await services.Pnl.GetSummaryAsync(ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Imported, import.Status);
        Assert.NotNull(summary);
        Assert.Equal(15.5m, summary.TotalProfitLossUsd);
        Assert.Equal(-3.1m, summary.TotalCommissionUsd);
        Assert.Equal(1m, summary.TotalDividendsUsd);
        Assert.Equal(-0.775m, summary.TotalFinancingUsd);
        Assert.Equal(12.625m, summary.TotalNetPnlUsd);
    }

    [Fact]
    public async Task Mutated_unknown_execution_creates_blocking_eod_break()
    {
        var services = CreateServices();
        AddInternalFill(services.State);
        var generated = await services.Generator.GenerateAsync(ReportDate, "LMAX", "LMAX_DEMO_LOCAL", LmaxEodMutationMode.AddUnknownExecution, CancellationToken.None);
        await services.Importer.ImportReportSetAsync(generated.IndividualTradesPath, generated.TradesSummaryPath, generated.CurrencyWalletsPath, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        var reconciliation = await services.Reconciliation.RunAsync(ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.True(reconciliation.BlockingBreakCount > 0);
        Assert.Contains(reconciliation.Breaks, x => x.Type == ReconciliationBreakType.BrokerFillMissingInternally);
    }

    private static void AddInternalFill(PlatformState state)
    {
        var instrument = state.Instruments.Single(x => x.Symbol == "EURUSD");
        var venue = state.Venues.Single(x => x.Name == "LMAX");
        var childOrderId = ChildOrderId.New();
        state.Fills.Add(new Fill(new FillId(Guid.Parse("99999999-9999-9999-9999-999999999999")), "BRK-EXEC-1", childOrderId, instrument.Id, venue.Id, TradeSide.Sell, 10000m, 1m, 1.10000m, Now, Now));
    }

    private static Services CreateServices()
    {
        var state = SeedData.Create(Now);
        var clock = new FixedClock(Now);
        var intradayRepository = new InMemoryIntradayRepository(state);
        var eodRepository = new InMemoryLmaxEodReportRepository(state);
        var options = new LmaxEodReportOptions { DataRoot = Path.GetFullPath(Path.Combine("data", "lmax-eod")), SummaryTolerance = 0.01m };
        var consistency = new LmaxReportPairConsistencyService(eodRepository, clock, options);
        var importer = new LmaxEodReportImportService(intradayRepository, eodRepository, consistency, clock, options);
        var reconciliation = new EodReconciliationService(intradayRepository, eodRepository, clock);
        var pnl = new EodPnlSummaryService(intradayRepository, eodRepository);
        var generator = new FakeLmaxEodReportGenerator(intradayRepository, clock, options);
        return new Services(state, options, generator, importer, reconciliation, pnl);
    }

    private sealed record Services(
        PlatformState State,
        LmaxEodReportOptions Options,
        IFakeLmaxEodReportGenerator Generator,
        ILmaxEodReportImportService Importer,
        IEodReconciliationService Reconciliation,
        IEodPnlSummaryService Pnl);
}
