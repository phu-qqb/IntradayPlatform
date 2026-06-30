using System.Security.Cryptography;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxEodReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 04, 30, 16, 00, 00, TimeSpan.Zero);
    private static readonly DateOnly ReportDate = new(2026, 04, 30);

    [Fact]
    public async Task Valid_real_header_fixtures_import_successfully_and_resolve_aliases()
    {
        var services = CreateServices();

        var individual = await services.Importer.ImportIndividualTradesAsync(Fixture(services, "valid", "individual-trades.csv"), ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);
        var summary = await services.Importer.ImportTradesSummaryAsync(Fixture(services, "valid", "trades.csv"), ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);
        var wallets = await services.Importer.ImportCurrencyWalletsAsync(Fixture(services, "valid", "currency-wallets.csv"), ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Imported, individual.Status);
        Assert.Equal(LmaxReportImportStatus.Imported, summary.Status);
        Assert.Equal(LmaxReportImportStatus.Imported, wallets.Status);
        Assert.Equal(3, services.State.LmaxIndividualTrades.Count);
        Assert.Contains(services.State.LmaxIndividualTrades, x => x.LmaxSymbol == "EUR/USD" && InstrumentSymbol(services.State, x.InstrumentId) == "EURUSD");
        Assert.Contains(services.State.LmaxIndividualTrades, x => x.LmaxSymbol == "USD/JPY" && InstrumentSymbol(services.State, x.InstrumentId) == "USDJPY");
        Assert.Contains(services.State.LmaxIndividualTrades, x => x.LmaxSymbol == "GBP/USD" && InstrumentSymbol(services.State, x.InstrumentId) == "GBPUSD");
    }

    [Fact]
    public async Task Header_typo_and_missing_required_column_are_rejected()
    {
        var services = CreateServices();
        var badHeader = await services.Importer.ImportIndividualTradesAsync(Fixture(services, "invalid", "individual-trades-bad-header.csv"), ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);
        var missingColumnPath = WriteReport(services, LmaxReportType.IndividualTrades, "missing-column.csv",
        [
            string.Join(",", LmaxEodHeaders.Individual),
            "EX1,MTF1,30-04-2026 16:00:00.000,1,1.10000,30-04-2026,4001,EUR/USD,INST1,ORD1,,,30-04-2026 16:00:00.000,Market,LMAX,test-user,0,-1,LMAX_DEMO_LOCAL,10000,-11000"
        ]);

        var missingColumn = await services.Importer.ImportIndividualTradesAsync(missingColumnPath, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Rejected, badHeader.Status);
        Assert.Contains(badHeader.Issues, x => x.IssueType == LmaxReportValidationIssueType.InvalidHeader);
        Assert.Equal(LmaxReportImportStatus.Rejected, missingColumn.Status);
        Assert.Contains(missingColumn.Issues, x => x.IssueType == LmaxReportValidationIssueType.InvalidRow);
    }

    [Theory]
    [InlineData("not-a-time", "30-04-2026", "1", "1.10000", LmaxReportValidationIssueType.InvalidTimestamp)]
    [InlineData("30-04-2026 16:00:00.000", "2026/04/30", "1", "1.10000", LmaxReportValidationIssueType.InvalidDate)]
    [InlineData("30-04-2026 16:00:00.000", "30-04-2026", "bad-decimal", "1.10000", LmaxReportValidationIssueType.InvalidQuantity)]
    [InlineData("30-04-2026 16:00:00.000", "30-04-2026", "1", "bad-price", LmaxReportValidationIssueType.InvalidPrice)]
    public async Task Individual_trades_invalid_fields_are_rejected(string timestamp, string tradeDate, string quantity, string price, LmaxReportValidationIssueType issueType)
    {
        var services = CreateServices();
        var path = WriteReport(services, LmaxReportType.IndividualTrades, $"individual-invalid-{issueType}.csv",
        [
            string.Join(",", LmaxEodHeaders.Individual),
            $"EX1,MTF1,{timestamp},{quantity},{price},{tradeDate},4001,EUR/USD,INST1,ORD1,,,30-04-2026 16:00:00.000,Market,LMAX,test-user,0,-1,LMAX_DEMO_LOCAL,10000,-11000,UTI1"
        ]);

        var result = await services.Importer.ImportIndividualTradesAsync(path, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Rejected, result.Status);
        Assert.Contains(result.Issues, x => x.IssueType == issueType);
    }

    [Theory]
    [InlineData("XXX/YYY", "9999", LmaxReportValidationIssueType.UnknownInstrument)]
    [InlineData("EUR/USD", "4001", LmaxReportValidationIssueType.DuplicateExecutionId)]
    [InlineData("EUR/USD", "4001", LmaxReportValidationIssueType.DuplicateTradeUti)]
    public async Task Individual_trades_unknown_symbol_and_duplicates_are_rejected(string symbol, string instrumentId, LmaxReportValidationIssueType issueType)
    {
        var services = CreateServices();
        var execution2 = issueType == LmaxReportValidationIssueType.DuplicateExecutionId ? "EX1" : "EX2";
        var uti2 = issueType == LmaxReportValidationIssueType.DuplicateTradeUti ? "UTI1" : "UTI2";
        var path = WriteReport(services, LmaxReportType.IndividualTrades, $"individual-{issueType}.csv",
        [
            string.Join(",", LmaxEodHeaders.Individual),
            $"EX1,MTF1,30-04-2026 16:00:00.000,1,1.10000,30-04-2026,{instrumentId},{symbol},INST1,ORD1,,,30-04-2026 16:00:00.000,Market,LMAX,test-user,0,-1,LMAX_DEMO_LOCAL,10000,-11000,UTI1",
            $"{execution2},MTF2,30-04-2026 16:01:00.000,1,1.10000,30-04-2026,{instrumentId},{symbol},INST2,ORD2,,,30-04-2026 16:01:00.000,Market,LMAX,test-user,0,-1,LMAX_DEMO_LOCAL,10000,-11000,{uti2}"
        ]);

        var result = await services.Importer.ImportIndividualTradesAsync(path, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Rejected, result.Status);
        Assert.Contains(result.Issues, x => x.IssueType == issueType);
    }

    [Fact]
    public async Task Individual_trades_allows_blank_trade_uti_from_lmax_portal()
    {
        var services = CreateServices();
        AddBrokerAccount(services, "LMAX_SYNTH_ACCOUNT_A_LOCAL", "9900000001");
        var path = WriteReport(services, LmaxReportType.IndividualTrades, "individual-blank-trade-uti.csv",
        [
            string.Join(",", LmaxEodHeaders.Individual),
            "EX1,MTF1,30-04-2026 16:00:00.000,1,1.10000,30-04-2026,4001,EUR/USD,INST1,ORD1,,,30-04-2026 16:00:00.000,Market,LMAX,test-user,0,-1,9900000001,10000,-11000,",
            "EX2,MTF2,30-04-2026 16:01:00.000,1,1.10000,30-04-2026,4001,EUR/USD,INST2,ORD2,,,30-04-2026 16:01:00.000,Market,LMAX,test-user,0,-1,9900000001,10000,-11000,"
        ]);

        var result = await services.Importer.ImportIndividualTradesAsync(path, ReportDate, "LMAX", "LMAX_SYNTH_ACCOUNT_A_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Imported, result.Status);
        Assert.Equal(0, result.BlockingIssueCount);
        Assert.DoesNotContain(result.Issues, x => x.IssueType == LmaxReportValidationIssueType.DuplicateTradeUti);
        Assert.Equal(2, services.State.LmaxIndividualTrades.Count);
        Assert.All(services.State.LmaxIndividualTrades, x => Assert.True(string.IsNullOrWhiteSpace(x.TradeUti)));
    }

    [Fact]
    public async Task Known_disabled_for_trading_alias_imports_with_warning_only_and_does_not_change_trading_state()
    {
        var services = CreateServices();
        var gbp = services.State.Instruments.Single(x => x.Symbol == "GBPUSD");
        Assert.False(gbp.IsEnabled);

        var result = await services.Importer.ImportIndividualTradesAsync(Fixture(services, "valid", "individual-trades.csv"), ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Imported, result.Status);
        Assert.Equal(0, result.BlockingIssueCount);
        Assert.Contains(result.Issues, x => x.IssueType == LmaxReportValidationIssueType.DisabledInstrument && x.Severity == LmaxReportValidationSeverity.Warning);
        Assert.False(services.State.Instruments.Single(x => x.Symbol == "GBPUSD").IsEnabled);
        Assert.Empty(services.State.ParentOrders);
        Assert.Empty(services.State.Fills);
    }

    [Fact]
    public async Task Trades_summary_invalid_fields_and_unknown_symbol_are_rejected()
    {
        var services = CreateServices();
        var invalidDate = WriteReport(services, LmaxReportType.TradesSummary, "summary-invalid-date.csv",
        [
            string.Join(",", LmaxEodHeaders.Summary),
            "not-a-date,EUR/USD,Market,USD,1,1.10000,0.22,11000,EUR/USD,test-user,0.220000,LMAX_DEMO_LOCAL"
        ]);
        var invalidDecimal = WriteReport(services, LmaxReportType.TradesSummary, "summary-invalid-decimal.csv",
        [
            string.Join(",", LmaxEodHeaders.Summary),
            "4/30/2026 16:00,EUR/USD,Market,USD,bad,1.10000,0.22,11000,EUR/USD,test-user,0.220000,LMAX_DEMO_LOCAL"
        ]);
        var unknown = WriteReport(services, LmaxReportType.TradesSummary, "summary-unknown.csv",
        [
            string.Join(",", LmaxEodHeaders.Summary),
            "4/30/2026 16:00,XXX/YYY,Market,USD,1,1.10000,0.22,11000,XXX/YYY,test-user,0.220000,LMAX_DEMO_LOCAL"
        ]);

        var invalidDateResult = await services.Importer.ImportTradesSummaryAsync(invalidDate, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);
        var invalidDecimalResult = await services.Importer.ImportTradesSummaryAsync(invalidDecimal, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);
        var unknownResult = await services.Importer.ImportTradesSummaryAsync(unknown, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Contains(invalidDateResult.Issues, x => x.IssueType == LmaxReportValidationIssueType.InvalidTimestamp);
        Assert.Contains(invalidDecimalResult.Issues, x => x.IssueType == LmaxReportValidationIssueType.InvalidQuantity);
        Assert.Contains(unknownResult.Issues, x => x.IssueType == LmaxReportValidationIssueType.UnknownInstrument);
    }

    [Fact]
    public async Task Trades_summary_preserves_absolute_notional_and_commission()
    {
        var services = CreateServices();

        await services.Importer.ImportTradesSummaryAsync(Fixture(services, "valid", "trades.csv"), ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.All(services.State.LmaxTradeSummaries, x =>
        {
            Assert.True(x.NotionalValue > 0);
            Assert.True(x.CommissionRounded >= 0);
            Assert.True(x.CommissionFullPrecision >= 0);
        });
    }

    [Fact]
    public async Task Currency_wallet_invalid_data_and_account_mismatch_are_rejected()
    {
        var services = CreateServices();
        var invalid = WriteReport(services, LmaxReportType.CurrencyWallets, "wallet-invalid.csv",
        [
            string.Join(",", LmaxEodHeaders.Wallet),
            "USD,1000,0,0,10,bad,0,0,1010,1,LMAX_DEMO_LOCAL",
            "EUR,100,0,0,1,0,0,0,101,0,LMAX_DEMO_LOCAL",
            "GBP,100,0,0,1,0,0,0,101,1.25,OTHER_ACCOUNT",
            "JPY,100,0,0,1,0,0,0,999,0.0065,LMAX_DEMO_LOCAL",
            "JPY,100,0,0,1,0,0,0,101,0.0065,LMAX_DEMO_LOCAL"
        ]);

        var result = await services.Importer.ImportCurrencyWalletsAsync(invalid, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Rejected, result.Status);
        Assert.Contains(result.Issues, x => x.IssueType == LmaxReportValidationIssueType.InvalidRow);
        Assert.Contains(result.Issues, x => x.IssueType == LmaxReportValidationIssueType.InvalidRateToBaseCcy);
        Assert.Contains(result.Issues, x => x.IssueType == LmaxReportValidationIssueType.AccountIdMismatch);
        Assert.Contains(result.Issues, x => x.IssueType == LmaxReportValidationIssueType.WalletBalanceMismatch);
        Assert.Contains(result.Issues, x => x.IssueType == LmaxReportValidationIssueType.DuplicateCurrencyWallet);
    }

    [Fact]
    public async Task Usd_rate_not_one_is_warning_not_blocking()
    {
        var services = CreateServices();
        var path = WriteReport(services, LmaxReportType.CurrencyWallets, "wallet-usd-rate-warning.csv",
        [
            string.Join(",", LmaxEodHeaders.Wallet),
            "USD,1000,0,0,10,-2,0,0,1008,1.02,LMAX_DEMO_LOCAL"
        ]);

        var result = await services.Importer.ImportCurrencyWalletsAsync(path, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Imported, result.Status);
        Assert.Contains(result.Issues, x => x.IssueType == LmaxReportValidationIssueType.InvalidRateToBaseCcy && x.Severity == LmaxReportValidationSeverity.Warning);
    }

    [Fact]
    public async Task Wallet_pnl_summary_converts_each_base_usd_field_and_totals_net_pnl()
    {
        var services = CreateServices();

        await services.Importer.ImportCurrencyWalletsAsync(Fixture(services, "valid", "currency-wallets.csv"), ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);
        var summary = await services.Pnl.GetSummaryAsync(ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.NotNull(summary);
        var jpy = summary.CurrencyRows.Single(x => x.Currency == "JPY");
        Assert.Equal(656.3375m, jpy.WalletBalanceBaseUsd);
        Assert.Equal(6.5m, jpy.ProfitLossBaseUsd);
        Assert.Equal(-0.13m, jpy.CommissionBaseUsd);
        Assert.Equal(0m, jpy.DividendsBaseUsd);
        Assert.Equal(-0.0325m, jpy.FinancingBaseUsd);
        Assert.Equal(summary.TotalProfitLossUsd + summary.TotalCommissionUsd + summary.TotalDividendsUsd + summary.TotalFinancingUsd, summary.TotalNetPnlUsd);
        Assert.DoesNotContain("Position", nameof(EodPnlCurrencyRow));
    }

    [Fact]
    public async Task Report_set_rollup_accepts_matching_totals_and_warns_on_group_count_only()
    {
        var services = CreateServices();
        var individual = WriteReport(services, LmaxReportType.IndividualTrades, "rollup-group-individual.csv",
        [
            string.Join(",", LmaxEodHeaders.Individual),
            "EX1,MTF1,30-04-2026 16:00:00.000,1,1.10000,30-04-2026,4001,EUR/USD,INST1,ORD-SAME,,,30-04-2026 16:00:00.000,Market,LMAX,test-user,0,-0.100000,LMAX_DEMO_LOCAL,5000,-5500,UTI1",
            "EX2,MTF2,30-04-2026 16:01:00.000,1,1.10000,30-04-2026,4001,EUR/USD,INST2,ORD-DIFFERENT,,,30-04-2026 16:01:00.000,Market,LMAX,test-user,0,-0.120000,LMAX_DEMO_LOCAL,5000,-5500,UTI2"
        ]);
        var summary = WriteReport(services, LmaxReportType.TradesSummary, "rollup-group-summary.csv",
        [
            string.Join(",", LmaxEodHeaders.Summary),
            "4/30/2026 16:00,EUR/USD,Market,USD,2,1.10000,0.22,11000,EUR/USD,test-user,0.220000,LMAX_DEMO_LOCAL"
        ]);

        var result = await services.Importer.ImportReportSetAsync(individual, summary, Fixture(services, "valid", "currency-wallets.csv"), ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Imported, result.Status);
        Assert.Contains(result.Issues, x => x.IssueType == LmaxReportValidationIssueType.SummaryMismatch && x.Severity == LmaxReportValidationSeverity.Warning);
        Assert.DoesNotContain(result.Issues, x => x.Severity == LmaxReportValidationSeverity.Blocking);
    }

    [Theory]
    [InlineData("total-notional", 7, "12000")]
    [InlineData("total-commission", 10, "10.000000")]
    [InlineData("per-symbol-notional", 8, "EUR/USD|USD/JPY|EUR/USD")]
    [InlineData("per-symbol-commission", 10, "0.500000")]
    public async Task Report_set_rollup_mismatches_are_blocking(string name, int column, string value)
    {
        var services = CreateServices();
        var summaryLines = File.ReadAllLines(Fixture(services, "valid", "trades.csv"));
        if (name == "per-symbol-notional")
        {
            var parts = value.Split('|');
            summaryLines[2] = ReplaceColumn(summaryLines[2], column, parts[2]);
        }
        else
        {
            summaryLines[1] = ReplaceColumn(summaryLines[1], column, value);
        }

        var summary = WriteReport(services, LmaxReportType.TradesSummary, $"summary-{name}.csv", summaryLines);
        var result = await services.Importer.ImportReportSetAsync(Fixture(services, "valid", "individual-trades.csv"), summary, Fixture(services, "valid", "currency-wallets.csv"), ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Rejected, result.Status);
        Assert.Contains(result.Issues, x => x.IssueType == LmaxReportValidationIssueType.SummaryMismatch && x.Severity == LmaxReportValidationSeverity.Blocking);
    }

    [Fact]
    public async Task Fake_generator_writes_actual_lmax_headers_and_mutations_change_expected_files()
    {
        foreach (var mode in Enum.GetValues<LmaxEodMutationMode>())
        {
            var services = CreateServices();
            AddInternalFill(services.State);

            var result = await services.Generator.GenerateAsync(ReportDate, "LMAX", "LMAX_DEMO_LOCAL", mode, CancellationToken.None);
            var individual = File.ReadAllLines(result.IndividualTradesPath);
            var summary = File.ReadAllLines(result.TradesSummaryPath);
            var wallet = File.ReadAllLines(result.CurrencyWalletsPath);

            Assert.Equal(string.Join(",", LmaxEodHeaders.Individual), individual[0]);
            Assert.Equal(string.Join(",", LmaxEodHeaders.Summary), summary[0]);
            Assert.Equal(string.Join(",", LmaxEodHeaders.Wallet), wallet[0]);
            if (mode == LmaxEodMutationMode.DropOneExecution) Assert.Single(individual);
            if (mode == LmaxEodMutationMode.AddUnknownExecution) Assert.Contains(individual, x => x.Contains("UNKNOWN-", StringComparison.Ordinal));
            if (mode == LmaxEodMutationMode.ChangeExecutionQuantity) Assert.Contains(individual, x => x.Contains("-9000", StringComparison.Ordinal));
            if (mode == LmaxEodMutationMode.ChangeExecutionPrice) Assert.Contains(individual, x => x.Contains("1.11000", StringComparison.Ordinal));
            if (mode == LmaxEodMutationMode.ChangeExecutionSide) Assert.Contains(individual, x => x.Contains(",10000,", StringComparison.Ordinal));
            if (mode == LmaxEodMutationMode.ChangeWalletBalance) Assert.Contains(wallet, x => x.Contains("1000123.230000", StringComparison.Ordinal));
            if (mode == LmaxEodMutationMode.ChangeWalletRate) Assert.Contains(wallet, x => x.EndsWith(",1.2,LMAX_DEMO_LOCAL", StringComparison.Ordinal));
            if (mode == LmaxEodMutationMode.DropCurrencyWallet) Assert.Single(wallet);
        }
    }

    [Theory]
    [InlineData(LmaxEodMutationMode.None, null)]
    [InlineData(LmaxEodMutationMode.DropOneExecution, ReconciliationBreakType.InternalFillMissingInBrokerReport)]
    [InlineData(LmaxEodMutationMode.AddUnknownExecution, ReconciliationBreakType.BrokerFillMissingInternally)]
    [InlineData(LmaxEodMutationMode.ChangeExecutionQuantity, ReconciliationBreakType.QuantityMismatch)]
    [InlineData(LmaxEodMutationMode.ChangeExecutionPrice, ReconciliationBreakType.PriceMismatch)]
    [InlineData(LmaxEodMutationMode.ChangeExecutionSide, ReconciliationBreakType.SideMismatch)]
    public async Task Eod_reconciliation_reports_expected_break_for_mutation(LmaxEodMutationMode mode, ReconciliationBreakType? expectedBreak)
    {
        var services = CreateServices();
        AddInternalFill(services.State);
        var generated = await services.Generator.GenerateAsync(ReportDate, "LMAX", "LMAX_DEMO_LOCAL", mode, CancellationToken.None);
        await services.Importer.ImportReportSetAsync(generated.IndividualTradesPath, generated.TradesSummaryPath, generated.CurrencyWalletsPath, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        var reconciliation = await services.Reconciliation.RunAsync(ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        if (expectedBreak is null)
        {
            Assert.Equal(0, reconciliation.BlockingBreakCount);
        }
        else
        {
            Assert.Contains(reconciliation.Breaks, x => x.Type == expectedBreak);
        }
    }

    [Fact]
    public async Task Eod_reconciliation_detects_instrument_mismatch_and_no_fill_orders_are_not_breaks()
    {
        var services = CreateServices();
        AddInternalFill(services.State);
        var fill = services.State.Fills.Single();
        var gbp = services.State.Instruments.Single(x => x.Symbol == "GBPUSD");
        services.State.LmaxIndividualTrades.Add(new LmaxIndividualTrade(LmaxIndividualTradeId.New(), LmaxReportImportRunId.New(), ReportDate, services.State.Venues.Single().Id, services.State.BrokerAccounts.Single().Id, fill.BrokerExecutionId, null, Now, -1m, fill.Price, ReportDate, "4002", "GBP/USD", gbp.Id, null, "ORD1", null, null, null, "Market", null, null, 0m, -0.1m, "LMAX_DEMO_LOCAL", -fill.BaseQuantity, fill.BaseQuantity * fill.Price, "UTI-INST-MISMATCH", null, Now));
        services.State.ChildOrders.Add(new ChildOrder(ChildOrderId.New(), ParentOrderId.New(), services.State.Venues.Single().Id, new ClientOrderId("NO-FILL"), OrderSide.Buy, OrderType.Market, TimeInForce.IOC, 1m, 1m, OrderStatus.Expired, Now));

        var reconciliation = await services.Reconciliation.RunAsync(ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Contains(reconciliation.Breaks, x => x.Type == ReconciliationBreakType.InstrumentMismatch);
        Assert.DoesNotContain(reconciliation.Breaks, x => x.Description.Contains("NO-FILL", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Missing_report_file_is_rejected_without_rows_or_hash()
    {
        var services = CreateServices();
        var missing = Path.Combine(services.Options.DataRoot, "valid", "missing-individual-trades.csv");

        var result = await services.Importer.ImportIndividualTradesAsync(missing, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Rejected, result.Status);
        Assert.Equal(0, result.RowCount);
        Assert.Contains(result.Issues, x => x.IssueType == LmaxReportValidationIssueType.MissingFile);
        var run = services.State.LmaxReportImportRuns.Single(x => x.Id == result.ImportRunId);
        Assert.Null(run.FileHash);
        Assert.Empty(services.State.LmaxIndividualTrades);
    }

    [Fact]
    public async Task Successful_import_records_file_hash_import_run_and_row_provenance()
    {
        var services = CreateServices();
        var path = Fixture(services, "valid", "individual-trades.csv");

        var result = await services.Importer.ImportIndividualTradesAsync(path, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        var expectedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var run = services.State.LmaxReportImportRuns.Single(x => x.Id == result.ImportRunId);
        Assert.Equal(LmaxReportImportStatus.Imported, result.Status);
        Assert.Equal(expectedHash, run.FileHash);
        Assert.All(services.State.LmaxIndividualTrades, trade =>
        {
            Assert.Equal(result.ImportRunId, trade.ImportRunId);
            Assert.False(string.IsNullOrWhiteSpace(trade.RawLine));
        });
    }

    [Fact]
    public async Task Repeated_imports_are_idempotent_for_trades_summaries_and_wallets()
    {
        var services = CreateServices();
        var individualPath = Fixture(services, "valid", "individual-trades.csv");
        var summaryPath = Fixture(services, "valid", "trades.csv");
        var walletPath = Fixture(services, "valid", "currency-wallets.csv");

        await services.Importer.ImportIndividualTradesAsync(individualPath, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);
        var individualCount = services.State.LmaxIndividualTrades.Count;
        await services.Importer.ImportIndividualTradesAsync(individualPath, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);
        Assert.Equal(individualCount, services.State.LmaxIndividualTrades.Count);

        await services.Importer.ImportTradesSummaryAsync(summaryPath, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);
        var summaryCount = services.State.LmaxTradeSummaries.Count;
        await services.Importer.ImportTradesSummaryAsync(summaryPath, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);
        Assert.Equal(summaryCount, services.State.LmaxTradeSummaries.Count);

        await services.Importer.ImportCurrencyWalletsAsync(walletPath, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);
        var walletCount = services.State.LmaxCurrencyWallets.Count;
        await services.Importer.ImportCurrencyWalletsAsync(walletPath, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);
        Assert.Equal(walletCount, services.State.LmaxCurrencyWallets.Count);
    }

    [Fact]
    public async Task Individual_trades_wrong_account_and_wrong_report_date_are_rejected()
    {
        var services = CreateServices();
        var path = WriteReport(services, LmaxReportType.IndividualTrades, "individual-wrong-account-date.csv",
        [
            string.Join(",", LmaxEodHeaders.Individual),
            "EX1,MTF1,30-04-2026 16:00:00.000,1,1.10000,29-04-2026,4001,EUR/USD,INST1,ORD1,,,30-04-2026 16:00:00.000,Market,LMAX,test-user,0,-1,OTHER_ACCOUNT,10000,-11000,UTI1"
        ]);

        var result = await services.Importer.ImportIndividualTradesAsync(path, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Rejected, result.Status);
        Assert.Contains(result.Issues, x => x.IssueType == LmaxReportValidationIssueType.AccountIdMismatch);
        Assert.Contains(result.Issues, x => x.IssueType == LmaxReportValidationIssueType.ReportDateMismatch);
    }

    [Fact]
    public async Task Trades_summary_wrong_account_and_wrong_report_date_are_rejected()
    {
        var services = CreateServices();
        var path = WriteReport(services, LmaxReportType.TradesSummary, "summary-wrong-account-date.csv",
        [
            string.Join(",", LmaxEodHeaders.Summary),
            "4/29/2026 16:00,EUR/USD,Market,USD,1,1.10000,0.22,11000,EUR/USD,test-user,0.220000,OTHER_ACCOUNT"
        ]);

        var result = await services.Importer.ImportTradesSummaryAsync(path, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Rejected, result.Status);
        Assert.Contains(result.Issues, x => x.IssueType == LmaxReportValidationIssueType.AccountIdMismatch);
        Assert.Contains(result.Issues, x => x.IssueType == LmaxReportValidationIssueType.ReportDateMismatch);
    }

    [Fact]
    public async Task Partial_report_set_with_missing_file_is_rejected()
    {
        var services = CreateServices();
        var missingWalletPath = Path.Combine(services.Options.DataRoot, "valid", "missing-currency-wallets.csv");

        var result = await services.Importer.ImportReportSetAsync(
            Fixture(services, "valid", "individual-trades.csv"),
            Fixture(services, "valid", "trades.csv"),
            missingWalletPath,
            ReportDate,
            "LMAX",
            "LMAX_DEMO_LOCAL",
            CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Rejected, result.Status);
        Assert.Contains(result.Issues, x => x.IssueType == LmaxReportValidationIssueType.MissingFile);
    }

    [Fact]
    public async Task Unit_import_path_uses_in_memory_repository_without_database()
    {
        var services = CreateServices();

        Assert.IsType<InMemoryLmaxEodReportRepository>(services.EodRepository);
        await services.Importer.ImportIndividualTradesAsync(Fixture(services, "valid", "individual-trades.csv"), ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(3, services.State.LmaxIndividualTrades.Count);
        Assert.Empty(services.State.EodReconciliationRuns);
    }

    [Fact]
    public async Task Trades_summary_accepts_legacy_and_real_lmax_timestamp_formats()
    {
        var services = CreateServices();
        var path = WriteReport(services, LmaxReportType.TradesSummary, "summary-real-and-legacy-timestamps.csv",
        [
            string.Join(",", LmaxEodHeaders.Summary),
            "4/30/2026 16:00,EUR/USD,Market,USD,1,1.10000,0.22,11000,EUR/USD,test-user,0.220000,LMAX_DEMO_LOCAL",
            "2026-04-30 16:01:02,EUR/USD,Market,USD,1,1.10001,0.22,11001,EUR/USD,test-user,0.220000,LMAX_DEMO_LOCAL"
        ]);

        var result = await services.Importer.ImportTradesSummaryAsync(path, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Imported, result.Status);
        Assert.Equal(LmaxEodImportClassifications.ImportedPartialNonAuthority, result.Message);
        Assert.Equal(2, services.State.LmaxTradeSummaries.Count);
    }

    [Fact]
    public async Task Trades_summary_unsupported_timestamp_format_is_blocked_and_not_persisted()
    {
        var services = CreateServices();
        var path = WriteReport(services, LmaxReportType.TradesSummary, "summary-unsupported-timestamp.csv",
        [
            string.Join(",", LmaxEodHeaders.Summary),
            "30/04/2026 16:00,EUR/USD,Market,USD,1,1.10000,0.22,11000,EUR/USD,test-user,0.220000,LMAX_DEMO_LOCAL"
        ]);

        var result = await services.Importer.ImportTradesSummaryAsync(path, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Rejected, result.Status);
        Assert.Equal(LmaxEodImportClassifications.BlockedUnsupportedTimestampFormat, result.Message);
        Assert.Empty(services.State.LmaxTradeSummaries);
    }

    [Fact]
    public async Task Unknown_account_is_quarantined_and_rows_are_not_persisted()
    {
        var services = CreateServices();
        var path = WriteReport(services, LmaxReportType.TradesSummary, "summary-unknown-account.csv",
        [
            string.Join(",", LmaxEodHeaders.Summary),
            "2026-04-30 16:00:00,EUR/USD,Market,USD,1,1.10000,0.22,11000,EUR/USD,test-user,0.220000,9900009999"
        ]);

        var result = await services.Importer.ImportTradesSummaryAsync(path, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Rejected, result.Status);
        Assert.Equal(LmaxEodImportClassifications.QuarantineUnknownAccount, result.Message);
        Assert.Empty(services.State.LmaxTradeSummaries);
    }

    [Fact]
    public async Task Recognized_synthetic_account_mapping_allows_local_partial_non_authority_import()
    {
        var services = CreateServices();
        AddBrokerAccount(services, "LMAX_SYNTH_ACCOUNT_A_LOCAL", "9900000001");
        var path = WriteReport(services, LmaxReportType.TradesSummary, "summary-synthetic-account.csv",
        [
            string.Join(",", LmaxEodHeaders.Summary),
            "2026-04-30 16:00:00,EUR/USD,Market,USD,1,1.10000,0.22,11000,EUR/USD,test-user,0.220000,9900000001"
        ]);

        var result = await services.Importer.ImportTradesSummaryAsync(path, ReportDate, "LMAX", "LMAX_SYNTH_ACCOUNT_A_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Imported, result.Status);
        Assert.Equal(LmaxEodImportClassifications.ImportedPartialNonAuthority, result.Message);
        Assert.Single(services.State.LmaxTradeSummaries);
    }

    [Fact]
    public async Task Outside_data_root_is_blocked_before_import()
    {
        var services = CreateServices();
        var outside = Path.Combine(Path.GetTempPath(), $"lmax-outside-{Guid.NewGuid():N}.csv");
        await File.WriteAllLinesAsync(outside,
        [
            string.Join(",", LmaxEodHeaders.Summary),
            "2026-04-30 16:00:00,EUR/USD,Market,USD,1,1.10000,0.22,11000,EUR/USD,test-user,0.220000,LMAX_DEMO_LOCAL"
        ]);

        await Assert.ThrowsAsync<DomainRuleViolationException>(() => services.Importer.ImportTradesSummaryAsync(outside, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None));
        Assert.Empty(services.State.LmaxTradeSummaries);
    }

    [Fact]
    public void Staging_contract_resolves_controlled_wallet_report_date_and_blocks_missing_metadata()
    {
        var services = CreateServices();
        var staged = LmaxEodReportStagingContract.BuildInboxPath(services.Options.DataRoot, "9900000001", ReportDate, LmaxReportType.CurrencyWallets);
        var missingDate = Path.Combine(services.Options.DataRoot, "inbox", "9900000001", "currency-wallets.csv");
        var outside = Path.Combine(Path.GetTempPath(), "currency-wallets.csv");

        Assert.True(LmaxEodReportStagingContract.TryResolveReportDateFromStagedPath(staged, services.Options.DataRoot, out var resolved, out var blockReason));
        Assert.Equal(ReportDate, resolved);
        Assert.Null(blockReason);
        Assert.False(LmaxEodReportStagingContract.TryResolveReportDateFromStagedPath(missingDate, services.Options.DataRoot, out _, out blockReason));
        Assert.Equal(LmaxEodImportClassifications.BlockedMissingReportDate, blockReason);
        Assert.False(LmaxEodReportStagingContract.TryResolveReportDateFromStagedPath(outside, services.Options.DataRoot, out _, out blockReason));
        Assert.Equal(LmaxEodImportClassifications.BlockedOutsideDataRoot, blockReason);
    }

    [Fact]
    public async Task Wallet_with_explicit_report_date_metadata_imports_as_partial_non_authority()
    {
        var services = CreateServices();

        var result = await services.Importer.ImportCurrencyWalletsAsync(Fixture(services, "valid", "currency-wallets.csv"), ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Imported, result.Status);
        Assert.Equal(LmaxEodImportClassifications.ImportedPartialNonAuthority, result.Message);
        Assert.NotEmpty(services.State.LmaxCurrencyWallets);
    }

    [Fact]
    public async Task Complete_report_set_is_candidate_authority_but_single_report_is_partial()
    {
        var services = CreateServices();

        var partial = await services.Importer.ImportTradesSummaryAsync(Fixture(services, "valid", "trades.csv"), ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);
        var complete = await services.Importer.ImportReportSetAsync(Fixture(services, "valid", "individual-trades.csv"), Fixture(services, "valid", "trades.csv"), Fixture(services, "valid", "currency-wallets.csv"), ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxEodImportClassifications.ImportedPartialNonAuthority, partial.Message);
        Assert.Equal(LmaxEodImportClassifications.ImportedCompleteCandidateAuthority, complete.Message);
    }

    [Fact]
    public async Task Cross_account_rows_are_forbidden_even_when_both_accounts_are_mapped()
    {
        var services = CreateServices();
        AddBrokerAccount(services, "LMAX_SYNTH_ACCOUNT_A_LOCAL", "9900000001");
        AddBrokerAccount(services, "LMAX_SYNTH_ACCOUNT_B_LOCAL", "9900000002");
        var path = WriteReport(services, LmaxReportType.TradesSummary, "summary-cross-account.csv",
        [
            string.Join(",", LmaxEodHeaders.Summary),
            "2026-04-30 16:00:00,EUR/USD,Market,USD,1,1.10000,0.22,11000,EUR/USD,test-user,0.220000,9900000002"
        ]);

        var result = await services.Importer.ImportTradesSummaryAsync(path, ReportDate, "LMAX", "LMAX_SYNTH_ACCOUNT_A_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Rejected, result.Status);
        Assert.Equal(LmaxEodImportClassifications.QuarantineUnknownAccount, result.Message);
        Assert.Empty(services.State.LmaxTradeSummaries);
    }

    [Fact]
    public async Task Missing_lmax_report_alias_blocks_authority_and_rows_are_not_persisted()
    {
        var services = CreateServices();
        var path = WriteReport(services, LmaxReportType.TradesSummary, "summary-missing-alias.csv",
        [
            string.Join(",", LmaxEodHeaders.Summary),
            "2026-04-30 16:00:00,USD/SEK,Market,SEK,1,9.90000,0.22,99000,USD/SEK,test-user,0.220000,LMAX_DEMO_LOCAL"
        ]);

        var result = await services.Importer.ImportTradesSummaryAsync(path, ReportDate, "LMAX", "LMAX_DEMO_LOCAL", CancellationToken.None);

        Assert.Equal(LmaxReportImportStatus.Rejected, result.Status);
        Assert.Equal(LmaxEodImportClassifications.BlockedUnsupportedSymbol, result.Message);
        Assert.Empty(services.State.LmaxTradeSummaries);
    }
    [Fact]
    public void Portal_account_metadata_uses_selected_account_and_ignores_amount_like_fields()
    {
        var resolution = LmaxPortalAccountMetadataContract.ResolveAccountMetadata(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Account Value"] = "1234567890.10",
                ["Margin on Open Position"] = "9876543210"
            },
            new LmaxPortalAcquisitionMetadata("9900000001", "9900000001", null),
            ["9900000001"]);

        Assert.Equal(LmaxEodImportClassifications.AccountMetadataFromPortalSelection, resolution.Status);
        Assert.Equal("9900000001", resolution.AccountId);
        Assert.False(resolution.Blocking);
        Assert.True(resolution.AccountIdFromPortalSelection);
    }

    [Fact]
    public void Portal_account_metadata_blocks_ambiguous_account_when_only_amount_like_fields_exist()
    {
        var resolution = LmaxPortalAccountMetadataContract.ResolveAccountMetadata(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Account Value"] = "9900000001",
                ["Open Profit / Loss"] = "42.0"
            },
            new LmaxPortalAcquisitionMetadata(null, null, null),
            ["9900000001"]);

        Assert.Equal(LmaxEodImportClassifications.BlockedAmbiguousAccountMetadata, resolution.Status);
        Assert.Null(resolution.AccountId);
        Assert.True(resolution.Blocking);
    }

    [Fact]
    public void Portal_account_metadata_accepts_matching_pdf_supporting_evidence_and_blocks_mismatch()
    {
        var ok = LmaxPortalAccountMetadataContract.ResolveAccountMetadata(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new LmaxPortalAcquisitionMetadata("9900000001", "9900000001", "9900000001"),
            ["9900000001"]);
        var mismatch = LmaxPortalAccountMetadataContract.ResolveAccountMetadata(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new LmaxPortalAcquisitionMetadata("9900000001", "9900000001", "9900000002"),
            ["9900000001", "9900000002"]);

        Assert.Equal(LmaxEodImportClassifications.AccountMetadataFromPdfSupportingEvidence, ok.Status);
        Assert.True(ok.PdfSupportingEvidenceUsed);
        Assert.False(ok.Blocking);
        Assert.Equal(LmaxEodImportClassifications.BlockedAccountMismatch, mismatch.Status);
        Assert.True(mismatch.Blocking);
    }

    [Fact]
    public void Portal_account_metadata_blocks_staging_mismatch_and_unknown_account()
    {
        var stagingMismatch = LmaxPortalAccountMetadataContract.ResolveAccountMetadata(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new LmaxPortalAcquisitionMetadata("9900000001", "9900000002", null),
            ["9900000001", "9900000002"]);
        var unknown = LmaxPortalAccountMetadataContract.ResolveAccountMetadata(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Account Id"] = "9900009999" },
            new LmaxPortalAcquisitionMetadata(null, null, null),
            ["9900000001"]);

        Assert.Equal(LmaxEodImportClassifications.BlockedAccountMismatch, stagingMismatch.Status);
        Assert.True(stagingMismatch.Blocking);
        Assert.Equal(LmaxEodImportClassifications.QuarantineUnknownAccount, unknown.Status);
        Assert.True(unknown.Blocking);
    }

    [Fact]
    public void Staging_contract_resolves_broker_account_from_controlled_inbox_path()
    {
        var services = CreateServices();
        var staged = LmaxEodReportStagingContract.BuildInboxPath(services.Options.DataRoot, "9900000001", ReportDate, LmaxReportType.TradesSummary);
        var missingInbox = Path.Combine(services.Options.DataRoot, "quarantine", "9900000001", "trades.csv");
        var outside = Path.Combine(Path.GetTempPath(), "trades.csv");

        Assert.True(LmaxEodReportStagingContract.TryResolveBrokerAccountFromStagedPath(staged, services.Options.DataRoot, out var brokerAccount, out var blockReason));
        Assert.Equal("9900000001", brokerAccount);
        Assert.Null(blockReason);
        Assert.False(LmaxEodReportStagingContract.TryResolveBrokerAccountFromStagedPath(missingInbox, services.Options.DataRoot, out _, out blockReason));
        Assert.Equal(LmaxEodImportClassifications.BlockedAmbiguousAccountMetadata, blockReason);
        Assert.False(LmaxEodReportStagingContract.TryResolveBrokerAccountFromStagedPath(outside, services.Options.DataRoot, out _, out blockReason));
        Assert.Equal(LmaxEodImportClassifications.BlockedOutsideDataRoot, blockReason);
    }

    [Fact]
    public void Open_positions_report_is_candidate_eod_evidence_not_live_broker_state_authority()
    {
        var lines = new[]
        {
            string.Join(",", LmaxEodHeaders.OpenPositions),
            "USD/JPY,JPY,1,16195,161.953,161.95,-30,0.0061747,USD/JPY,9900000001,"
        };

        var result = LmaxOpenPositionsEvidenceClassifier.Classify(lines, accountSummaryMargin: 16195m);

        Assert.Equal(LmaxEodImportClassifications.EodOpenPositionsCandidateEvidence, result.Status);
        Assert.Equal(1, result.RowCount);
        Assert.True(result.IsEodPositionCandidateEvidence);
        Assert.False(result.IsLiveBrokerStateAuthority);
        Assert.False(result.IsPreTradeAuthority);
        Assert.False(result.IsOpenOrderAuthority);
        Assert.False(result.Blocking);
        Assert.Contains(LmaxEodImportClassifications.OpenPositionsContractPending, result.Warnings);
        Assert.Contains(LmaxEodImportClassifications.NotLiveBrokerStateAuthority, result.Warnings);
        Assert.Contains(LmaxEodImportClassifications.NotPreTradeAuthority, result.Warnings);
        Assert.Contains(LmaxEodImportClassifications.NotOpenOrderAuthority, result.Warnings);
    }

    [Fact]
    public void Open_positions_margin_rollup_mismatch_is_warning_not_authority_upgrade()
    {
        var lines = new[]
        {
            string.Join(",", LmaxEodHeaders.OpenPositions),
            "USD/JPY,JPY,1,16195,161.953,161.95,-30,0.0061747,USD/JPY,9900000001,"
        };

        var result = LmaxOpenPositionsEvidenceClassifier.Classify(lines, accountSummaryMargin: 10m);

        Assert.Equal(LmaxEodImportClassifications.EodOpenPositionsCandidateEvidence, result.Status);
        Assert.Contains(LmaxEodImportClassifications.OpenPositionsMarginRollupWarning, result.Warnings);
        Assert.False(result.Blocking);
        Assert.False(result.IsLiveBrokerStateAuthority);
        Assert.False(result.IsPreTradeAuthority);
        Assert.False(result.IsOpenOrderAuthority);
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
        AddReportInstrument(state, "USDJPY", "USD/JPY", "4004", "USD", "JPY", false);
        AddReportInstrument(state, "GBPUSD", "GBP/USD", "4002", "GBP", "USD", false);
        AddReportInstrument(state, "AUDUSD", "AUD/USD", "4007", "AUD", "USD", false);
        AddReportInstrument(state, "NZDUSD", "NZD/USD", "100613", "NZD", "USD", false);
        AddReportInstrument(state, "USDCAD", "USD/CAD", "4013", "USD", "CAD", false);
        AddReportInstrument(state, "USDCHF", "USD/CHF", "4010", "USD", "CHF", false);
        var clock = new FixedClock(Now);
        var intradayRepository = new InMemoryIntradayRepository(state);
        var eodRepository = new InMemoryLmaxEodReportRepository(state);
        var options = new LmaxEodReportOptions { DataRoot = FixturesRoot(), SummaryTolerance = 0.01m };
        var consistency = new LmaxReportPairConsistencyService(eodRepository, clock, options);
        var importer = new LmaxEodReportImportService(intradayRepository, eodRepository, consistency, clock, options);
        var reconciliation = new EodReconciliationService(intradayRepository, eodRepository, clock);
        var pnl = new EodPnlSummaryService(intradayRepository, eodRepository);
        var generator = new FakeLmaxEodReportGenerator(intradayRepository, clock, options);
        return new Services(state, options, eodRepository, generator, importer, reconciliation, pnl);
    }


    private static BrokerAccount AddBrokerAccount(Services services, string accountCode, string externalAccountId)
    {
        var account = new BrokerAccount(BrokerAccountId.New(), services.State.Funds.Single().Id, accountCode, true, externalAccountId);
        services.State.BrokerAccounts.Add(account);
        return account;
    }
    private static void AddReportInstrument(PlatformState state, string internalSymbol, string externalSymbol, string externalId, string baseCurrency, string quoteCurrency, bool isEnabled)
    {
        if (state.InstrumentAliases.Any(x => x.Source == "LMAX_REPORT" && x.ExternalSymbol == externalSymbol))
        {
            return;
        }

        var instrument = new Instrument(
            new InstrumentId(Guid.Parse($"11111111-1111-1111-1111-{int.Parse(externalId):D12}")),
            internalSymbol,
            AssetClass.FxSpot,
            new Currency(baseCurrency),
            new Currency(quoteCurrency),
            quoteCurrency == "JPY" ? 3 : 5,
            2,
            isEnabled);
        state.Instruments.Add(instrument);
        state.InstrumentAliases.Add(new InstrumentAlias(new InstrumentAliasId(Guid.Parse($"33333333-3333-3333-3333-{int.Parse(externalId):D12}")), instrument.Id, "LMAX_REPORT", externalSymbol, externalId, true, Now));
    }

    private static string Fixture(Services services, string folder, string fileName)
        => Path.Combine(services.Options.DataRoot, folder, fileName);

    private static string WriteReport(Services services, LmaxReportType type, string fileName, IEnumerable<string> lines)
    {
        var folder = Path.Combine(services.Options.DataRoot, "generated", type.ToString(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, fileName);
        File.WriteAllLines(path, lines);
        return path;
    }

    private static string ReplaceColumn(string line, int column, string value)
    {
        var parts = line.Split(',');
        parts[column] = value;
        return string.Join(",", parts);
    }

    private static string InstrumentSymbol(PlatformState state, InstrumentId? instrumentId)
        => state.Instruments.Single(x => x.Id == instrumentId).Symbol;

    private static string FixturesRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "tests", "fixtures", "lmax-eod");
            if (Directory.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate tests/fixtures/lmax-eod.");
    }

    private sealed record Services(
        PlatformState State,
        LmaxEodReportOptions Options,
        ILmaxEodReportRepository EodRepository,
        IFakeLmaxEodReportGenerator Generator,
        ILmaxEodReportImportService Importer,
        IEodReconciliationService Reconciliation,
        IEodPnlSummaryService Pnl);
}

