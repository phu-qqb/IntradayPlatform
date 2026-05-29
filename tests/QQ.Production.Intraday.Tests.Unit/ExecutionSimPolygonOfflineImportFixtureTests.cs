using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimPolygonOfflineImportFixtureTests
{
    [Fact]
    public void Offline_polygon_quote_file_contract_exists()
    {
        var contract = ExecutionSimR004PolygonOfflineImportFixtures.CreateImportContract();

        Assert.Contains("JSON", contract.SupportedFormats);
        Assert.Contains("NDJSON", contract.SupportedFormats);
        Assert.Contains("CSV", contract.SupportedFormats);
        Assert.Contains("providerSymbol", contract.RequiredFields);
        Assert.Contains("timestampUtc or timestampUnixNanos or timestampUnixMillis", contract.RequiredFields);
        Assert.Contains("bid", contract.RequiredFields);
        Assert.Contains("ask", contract.RequiredFields);
        Assert.True(contract.LocalFilesOnly);
        Assert.False(contract.PolygonApiCalled);
        Assert.False(contract.RawProviderPayloadDumpAllowed);
    }

    [Fact]
    public void Sanitized_quote_fixture_row_schema_exists()
    {
        var schema = ExecutionSimR004PolygonOfflineImportFixtures.CreateSanitizedRowSchema();

        Assert.Equal("PolygonOfflineFixture", schema.QuoteProvider);
        Assert.Contains("QuoteFixtureRowId", schema.RequiredFields);
        Assert.Contains("ExecutionTradableSymbol", schema.RequiredFields);
        Assert.Contains("NormalizedPortfolioSymbol", schema.RequiredFields);
        Assert.Contains("RequiresInversion", schema.RequiredFields);
        Assert.Contains("TimestampUtc", schema.RequiredFields);
        Assert.Contains("Bid", schema.RequiredFields);
        Assert.Contains("Ask", schema.RequiredFields);
        Assert.Contains("Mid", schema.RequiredFields);
        Assert.Contains("Spread", schema.RequiredFields);
        Assert.Contains("SpreadBps", schema.RequiredFields);
        Assert.Contains("RawPayloadSerialized", schema.RequiredFields);
        Assert.False(schema.RawPayloadSerialized);
        Assert.False(schema.SecretSerialized);
    }

    [Fact]
    public void Provider_to_internal_mapping_exists()
    {
        var mapping = ExecutionSimR004PolygonOfflineImportFixtures.CreateProviderFieldMapping();

        Assert.Equal("TimestampUtc", mapping.Mapping["provider timestamp"]);
        Assert.Equal("Bid", mapping.Mapping["provider bid"]);
        Assert.Equal("Ask", mapping.Mapping["provider ask"]);
        Assert.Equal("ProviderSymbol", mapping.Mapping["provider symbol"]);
        Assert.True(mapping.MapsVenueToSanitizedAvailabilityOnly);
        Assert.True(mapping.MapsSizesToAvailabilityOnlyByDefault);
        Assert.False(mapping.RawProviderPayloadDumpAllowed);
    }

    [Fact]
    public void Valid_eurusd_offline_quote_fixture_imports_successfully()
    {
        var result = ExecutionSimR004PolygonOfflineImportFixtures.Import(
            ExecutionSimR004PolygonOfflineImportFixtures.CreateValidEurusdFixture());

        Assert.Equal(PolygonOfflineImportStatus.ImportReady, result.Status);
        Assert.True(result.AcceptedRowCount > 0);
        Assert.Equal(0, result.RejectedRowCount);
        Assert.All(result.AcceptedRows, row =>
        {
            Assert.Equal("PolygonOfflineFixture", row.QuoteProvider);
            Assert.Equal("EURUSD", row.ExecutionTradableSymbol);
            Assert.Equal("EURUSD", row.NormalizedPortfolioSymbol);
            Assert.False(row.RequiresInversion);
            Assert.False(row.RawPayloadSerialized);
        });
    }

    [Fact]
    public void Valid_usdjpy_offline_quote_fixture_imports_with_inversion_mapping_and_caveat_context()
    {
        var result = ExecutionSimR004PolygonOfflineImportFixtures.Import(
            ExecutionSimR004PolygonOfflineImportFixtures.CreateValidUsdjpyFixture());

        Assert.Equal(PolygonOfflineImportStatus.ImportReady, result.Status);
        Assert.All(result.AcceptedRows, row =>
        {
            Assert.Equal("USDJPY", row.ExecutionTradableSymbol);
            Assert.Equal("JPYUSD", row.NormalizedPortfolioSymbol);
            Assert.True(row.RequiresInversion);
            Assert.False(row.RawPayloadSerialized);
        });
    }

    [Fact]
    public void Direct_cross_eurgbp_is_blocked_as_execution_symbol()
    {
        var result = ExecutionSimR004PolygonOfflineImportFixtures.Import(
            ExecutionSimR004PolygonOfflineImportFixtures.CreateDirectCrossFixture());

        Assert.Empty(result.AcceptedRows);
        Assert.Contains(result.RejectedRows, row => row.FailureCategory == PolygonOfflineImportFailureCategory.DirectCrossExecutionDisabled);
        Assert.Contains(result.RejectedRows, row => row.Status == PolygonOfflineImportStatus.ImportBlockedDirectCrossExecutionDisabled);
    }

    [Fact]
    public void Missing_timestamp_missing_bid_ask_and_invalid_bid_ask_rows_are_rejected()
    {
        var result = ExecutionSimR004PolygonOfflineImportFixtures.Import(
            ExecutionSimR004PolygonOfflineImportFixtures.CreateInvalidFixture());

        Assert.Empty(result.AcceptedRows);
        Assert.Contains(result.RejectedRows, row => row.FailureCategory == PolygonOfflineImportFailureCategory.MissingTimestamp);
        Assert.Contains(result.RejectedRows, row => row.FailureCategory == PolygonOfflineImportFailureCategory.MissingBid);
        Assert.Contains(result.RejectedRows, row => row.FailureCategory == PolygonOfflineImportFailureCategory.InvalidBidAsk);
    }

    [Fact]
    public void Out_of_order_quotes_are_sorted_and_duplicate_rows_are_handled_deterministically()
    {
        var result = ExecutionSimR004PolygonOfflineImportFixtures.Import(
            ExecutionSimR004PolygonOfflineImportFixtures.CreateOutOfOrderDuplicateFixture());

        Assert.True(result.OutOfOrderRowsSorted);
        Assert.True(result.DuplicateRowsHandledDeterministically);
        Assert.Contains(result.RejectedRows, row => row.FailureCategory == PolygonOfflineImportFailureCategory.DuplicateRows);
        Assert.True(result.AcceptedRows.SequenceEqual(result.AcceptedRows.OrderBy(x => x.TimestampUtc)));
    }

    [Fact]
    public void Mid_spread_and_spread_bps_are_derived_safely()
    {
        var row = ExecutionSimR004PolygonOfflineImportFixtures.Import(
            ExecutionSimR004PolygonOfflineImportFixtures.CreateValidEurusdFixture()).AcceptedRows.First();

        Assert.Equal((row.Bid + row.Ask) / 2m, row.Mid);
        Assert.Equal(row.Ask - row.Bid, row.Spread);
        Assert.True(row.SpreadBps > 0m);
    }

    [Fact]
    public void Quote_window_extraction_computes_t_minus_13_to_close_metrics()
    {
        var result = ExecutionSimR004PolygonOfflineImportFixtures.Import(
            ExecutionSimR004PolygonOfflineImportFixtures.CreateValidEurusdFixture());
        var targetClose = new DateTimeOffset(2026, 05, 20, 15, 00, 00, TimeSpan.Zero);
        var knownAt = targetClose.AddMinutes(-13);
        var window = ExecutionSimR004PolygonOfflineImportFixtures.ExtractWindow(result.AcceptedRows, "EURUSD", targetClose, knownAt);

        Assert.Equal(TimeSpan.FromMinutes(13), window.TargetCloseTimestampUtc - window.WindowStartUtc);
        Assert.True(window.QuoteCount > 0);
        Assert.True(window.QuoteCountLastMinute > 0);
        Assert.True(window.MaxQuoteGap > TimeSpan.Zero);
        Assert.True(window.LastQuoteAgeAtClose >= TimeSpan.Zero);
        Assert.Equal(1m, window.BidAskAvailabilityRatio);
        Assert.Equal(1m, window.MidAvailabilityRatio);
    }

    [Fact]
    public void Close_benchmark_is_constructed_from_last_valid_quote_before_close()
    {
        var package = ExecutionSimR004PolygonOfflineImportFixtures.CreatePackage();

        Assert.Equal("EURUSD", package.CloseBenchmark.ExecutionTradableSymbol);
        Assert.Equal(HistoricalCloseBenchmarkStatus.Available, package.CloseBenchmark.CloseBenchmarkStatus);
        Assert.Equal(CloseConstructionMethod.BidAskClose, package.CloseBenchmark.CloseConstructionMethod);
        Assert.NotNull(package.CloseBenchmark.LastValidBidBeforeClose);
        Assert.NotNull(package.CloseBenchmark.LastValidAskBeforeClose);
        Assert.NotNull(package.CloseBenchmark.LastValidMidBeforeClose);
    }

    [Fact]
    public void Gap_stale_and_wide_spread_near_close_produce_safe_statuses()
    {
        var targetClose = new DateTimeOffset(2026, 05, 20, 15, 00, 00, TimeSpan.Zero);
        var knownAt = targetClose.AddMinutes(-13);

        var gap = BenchmarkFor(ExecutionSimR004PolygonOfflineImportFixtures.CreateGapNearCloseFixture(), targetClose, knownAt);
        var stale = BenchmarkFor(ExecutionSimR004PolygonOfflineImportFixtures.CreateStaleNearCloseFixture(), targetClose, knownAt);
        var wide = BenchmarkFor(ExecutionSimR004PolygonOfflineImportFixtures.CreateWideSpreadNearCloseFixture(), targetClose, knownAt);

        Assert.Equal(HistoricalCloseBenchmarkStatus.NoQuoteNearClose, gap.CloseBenchmarkStatus);
        Assert.Equal(HistoricalCloseBenchmarkStatus.StaleAtClose, stale.CloseBenchmarkStatus);
        Assert.Equal(HistoricalCloseBenchmarkStatus.SpreadTooWide, wide.CloseBenchmarkStatus);
    }

    [Fact]
    public void Feed_quality_score_and_bucket_are_produced()
    {
        var package = ExecutionSimR004PolygonOfflineImportFixtures.CreatePackage();

        Assert.True(package.FeedQualityScore.QuoteCountTMinus13ToClose > 0);
        Assert.True(package.FeedQualityScore.QuoteCountLastMinute > 0);
        Assert.True(package.FeedQualityScore.MaxGapSeconds > 0m);
        Assert.True(package.FeedQualityScore.MedianSpreadBps > 0m);
        Assert.True(package.FeedQualityScore.FeedQualityScore > 0m);
        Assert.Equal(HistoricalFeedQualityBucket.Good, package.FeedQualityScore.FeedQualityBucket);
    }

    [Fact]
    public void Package_is_no_external_and_creates_no_order_domain_records()
    {
        var package = ExecutionSimR004PolygonOfflineImportFixtures.CreatePackage();

        Assert.False(package.PolygonApiCalled);
        Assert.False(package.LmaxCalled);
        Assert.False(package.ExternalApiCalled);
        Assert.False(package.BrokerMarketDataRuntimeActionDetected);
        Assert.False(package.OrdersCreated);
        Assert.False(package.FillsCreated);
        Assert.False(package.ExecutionReportsCreated);
        Assert.False(package.RoutesCreated);
        Assert.False(package.SubmissionsCreated);
        Assert.False(package.RawPayloadSerialized);
        Assert.False(package.SecretsSerialized);
    }

    [Fact]
    public void Source_introduces_no_external_api_broker_marketdata_runtime_or_scheduler_shape()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "QQ.Production.Intraday.Application",
            "ExecutionSimCloseSeekingFoundation.cs"));

        Assert.DoesNotContain("HttpClient", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GetAsync", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PostAsync", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SendAsync", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WebSocket", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TcpClient", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SslStream", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MarketDataRequest", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MarketDataResponse", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FixSession", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectAsync", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BackgroundService", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IHostedService", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PeriodicTimer", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Audusd_is_not_misclassified_as_failed_and_usdjpy_caveat_remains_preserved()
    {
        var mapping = ExecutionSimR004PolygonOfflineImportFixtures.CreateSymbolMapping();

        Assert.True(mapping.ContainsKey("AUDUSD"));
        Assert.True(mapping.ContainsKey("USDJPY"));
        Assert.Equal("JPYUSD", mapping["USDJPY"].NormalizedPortfolioSymbol);
        Assert.True(mapping["USDJPY"].RequiresInversion);
    }

    private static PolygonCloseBenchmarkFromImportedQuotes BenchmarkFor(
        IReadOnlyList<PolygonOfflineQuoteRecord> records,
        DateTimeOffset targetClose,
        DateTimeOffset knownAt)
    {
        var result = ExecutionSimR004PolygonOfflineImportFixtures.Import(records);
        var window = ExecutionSimR004PolygonOfflineImportFixtures.ExtractWindow(result.AcceptedRows, "EURUSD", targetClose, knownAt);
        return ExecutionSimR004PolygonOfflineImportFixtures.CreateCloseBenchmark(window);
    }
}
