using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimHistoricalQuoteReadinessTests
{
    [Fact]
    public void Historical_quote_schema_contract_exists()
    {
        var contract = ExecutionSimR003HistoricalQuoteReadiness.CreateSchemaContract();

        Assert.Contains("QuoteProvider", contract.RequiredFields);
        Assert.Contains("ProviderSymbol", contract.RequiredFields);
        Assert.Contains("ExecutionTradableSymbol", contract.RequiredFields);
        Assert.Contains("NormalizedPortfolioSymbol", contract.RequiredFields);
        Assert.Contains("RequiresInversion", contract.RequiredFields);
        Assert.Contains("TimestampUtc", contract.RequiredFields);
        Assert.Contains("Bid", contract.RequiredFields);
        Assert.Contains("Ask", contract.RequiredFields);
        Assert.Contains("Mid", contract.RequiredFields);
        Assert.Contains("Spread", contract.RequiredFields);
        Assert.Contains("SpreadBps", contract.RequiredFields);
        Assert.Contains("BidSize", contract.RequiredFields);
        Assert.Contains("AskSize", contract.RequiredFields);
        Assert.Contains("VenueOrExchangeId", contract.RequiredFields);
        Assert.Contains("SequenceId", contract.RequiredFields);
        Assert.Contains("SourceLatencyCategory", contract.RequiredFields);
        Assert.Contains("QuoteQualityStatus", contract.RequiredFields);
        Assert.True(contract.NoRawPayloadSerialization);
    }

    [Fact]
    public void Quote_window_extraction_contract_exists()
    {
        var contract = ExecutionSimR003HistoricalQuoteReadiness.CreateWindowExtractionContract();

        Assert.Equal(TimeSpan.FromMinutes(13), contract.TargetCloseTimestampUtc - contract.WindowStartUtc);
        Assert.Equal(contract.TargetCloseTimestampUtc, contract.WindowEndUtc);
        Assert.Equal(TimeSpan.FromMinutes(15), contract.RequiredCadenceWindow);
        Assert.True(contract.QuoteCount > 0);
        Assert.True(contract.QuoteCountLastMinute > 0);
        Assert.True(contract.MaxQuoteGap > TimeSpan.Zero);
        Assert.True(contract.LastQuoteAgeAtClose >= TimeSpan.Zero);
        Assert.True(contract.HasBidAsk);
        Assert.True(contract.HasMid);
        Assert.True(contract.HasCloseBenchmark);
    }

    [Fact]
    public void Close_benchmark_construction_contract_exists()
    {
        var contract = ExecutionSimR003HistoricalQuoteReadiness.CreateCloseBenchmarkContract();

        Assert.Equal("Close15mBenchmarkFromQuotes", contract.BenchmarkName);
        Assert.True(contract.RequiresLastValidBidBeforeClose);
        Assert.True(contract.RequiresLastValidAskBeforeClose);
        Assert.True(contract.RequiresLastValidMidBeforeClose);
        Assert.True(contract.RequiresLastValidQuoteTimestampUtc);
        Assert.True(contract.IncludesCloseQuoteAge);
        Assert.True(contract.IncludesCloseSpreadBps);
        Assert.Contains(CloseConstructionMethod.LastValidQuoteBeforeClose, contract.ConstructionMethods);
        Assert.Contains(CloseConstructionMethod.LastValidMidBeforeClose, contract.ConstructionMethods);
        Assert.Contains(CloseConstructionMethod.BidAskClose, contract.ConstructionMethods);
        Assert.Contains(CloseConstructionMethod.InconclusiveSafe, contract.ConstructionMethods);
        Assert.Contains(HistoricalCloseBenchmarkStatus.Available, contract.BenchmarkStatuses);
        Assert.Contains(HistoricalCloseBenchmarkStatus.MissingBidAsk, contract.BenchmarkStatuses);
        Assert.Contains(HistoricalCloseBenchmarkStatus.StaleAtClose, contract.BenchmarkStatuses);
        Assert.Contains(HistoricalCloseBenchmarkStatus.NoQuoteNearClose, contract.BenchmarkStatuses);
        Assert.Contains(HistoricalCloseBenchmarkStatus.SpreadTooWide, contract.BenchmarkStatuses);
        Assert.Contains(HistoricalCloseBenchmarkStatus.InconclusiveSafe, contract.BenchmarkStatuses);
    }

    [Fact]
    public void Feed_quality_scoring_contract_exists()
    {
        var contract = ExecutionSimR003HistoricalQuoteReadiness.CreateFeedQualityContract();

        Assert.Contains("QuoteCountTMinus13ToClose", contract.Metrics);
        Assert.Contains("QuoteCountLastMinute", contract.Metrics);
        Assert.Contains("MaxGapSeconds", contract.Metrics);
        Assert.Contains("MedianGapSeconds", contract.Metrics);
        Assert.Contains("P95GapSeconds", contract.Metrics);
        Assert.Contains("LastQuoteAgeAtCloseSeconds", contract.Metrics);
        Assert.Contains("MedianSpreadBps", contract.Metrics);
        Assert.Contains("P95SpreadBps", contract.Metrics);
        Assert.Contains("MaxSpreadBps", contract.Metrics);
        Assert.Contains("BidAskAvailabilityRatio", contract.Metrics);
        Assert.Contains("MidAvailabilityRatio", contract.Metrics);
        Assert.Contains("BenchmarkAvailabilityRatio", contract.Metrics);
        Assert.Contains("GapNearCloseFlag", contract.Metrics);
        Assert.Contains("StaleNearCloseFlag", contract.Metrics);
        Assert.Contains("SpreadWideNearCloseFlag", contract.Metrics);
        Assert.Contains("FeedQualityScore", contract.Metrics);
        Assert.Contains("FeedQualityBucket", contract.Metrics);
        Assert.Contains(HistoricalFeedQualityBucket.Excellent, contract.Buckets);
        Assert.Contains(HistoricalFeedQualityBucket.Good, contract.Buckets);
        Assert.Contains(HistoricalFeedQualityBucket.Usable, contract.Buckets);
        Assert.Contains(HistoricalFeedQualityBucket.Marginal, contract.Buckets);
        Assert.Contains(HistoricalFeedQualityBucket.Unusable, contract.Buckets);
        Assert.Contains(HistoricalFeedQualityBucket.InconclusiveSafe, contract.Buckets);
    }

    [Fact]
    public void Provider_comparison_contract_exists()
    {
        var providers = ExecutionSimR003HistoricalQuoteReadiness.CreateProviderComparison();

        Assert.Contains(providers, x => x.ProviderName == HistoricalQuoteProviderName.Polygon);
        Assert.Contains(providers, x => x.ProviderName == HistoricalQuoteProviderName.LMAXArchive);
        Assert.Contains(providers, x => x.ProviderName == HistoricalQuoteProviderName.FixtureOnly);
    }

    [Fact]
    public void Polygon_is_docs_backed_candidate_only_and_not_called()
    {
        var polygon = Provider(HistoricalQuoteProviderName.Polygon);

        Assert.True(polygon.DocsBackedCandidateOnly);
        Assert.True(polygon.SupportsHistoricalBidAsk);
        Assert.True(polygon.SupportsTimestamps);
        Assert.True(polygon.SupportsPagination);
        Assert.Equal(HistoricalQuoteReadinessStatus.RequiresProviderApiKeyDesign, polygon.ReadinessStatus);
        Assert.False(polygon.ApiCalled);
        Assert.False(polygon.BrokerCalled);
    }

    [Fact]
    public void Lmax_archive_is_not_ready_until_archive_exists()
    {
        var lmaxArchive = Provider(HistoricalQuoteProviderName.LMAXArchive);

        Assert.Equal(HistoricalQuoteReadinessStatus.NotReadyUntilArchiveExists, lmaxArchive.ReadinessStatus);
        Assert.False(lmaxArchive.SupportsHistoricalBidAsk);
        Assert.False(lmaxArchive.SupportsTimestamps);
        Assert.False(lmaxArchive.ApiCalled);
        Assert.False(lmaxArchive.BrokerCalled);
    }

    [Fact]
    public void Fixture_only_provider_remains_available_for_deterministic_tests()
    {
        var fixture = Provider(HistoricalQuoteProviderName.FixtureOnly);

        Assert.Equal(HistoricalQuoteReadinessStatus.ReadyForFixtureImportOnly, fixture.ReadinessStatus);
        Assert.True(fixture.SupportsHistoricalBidAsk);
        Assert.True(fixture.SupportsTimestamps);
        Assert.True(fixture.SupportsFullTMinus13Window);
        Assert.False(fixture.ApiCalled);
        Assert.False(fixture.BrokerCalled);
    }

    [Fact]
    public void Missing_bid_ask_and_missing_timestamp_block_readiness()
    {
        Assert.Equal(
            HistoricalQuoteReadinessStatus.BlockedMissingBidAsk,
            ExecutionSimR003HistoricalQuoteReadiness.EvaluateQuoteReadiness(hasBidAsk: false, hasTimestamp: true));
        Assert.Equal(
            HistoricalQuoteReadinessStatus.BlockedMissingTimestamp,
            ExecutionSimR003HistoricalQuoteReadiness.EvaluateQuoteReadiness(hasBidAsk: true, hasTimestamp: false));
    }

    [Fact]
    public void Close_benchmark_safely_handles_gap_stale_and_wide_spread()
    {
        Assert.Equal(
            HistoricalCloseBenchmarkStatus.NoQuoteNearClose,
            ExecutionSimR003HistoricalQuoteReadiness.EvaluateCloseBenchmark(hasQuoteNearClose: false, staleNearClose: false, spreadTooWide: false, hasBidAsk: true));
        Assert.Equal(
            HistoricalCloseBenchmarkStatus.StaleAtClose,
            ExecutionSimR003HistoricalQuoteReadiness.EvaluateCloseBenchmark(hasQuoteNearClose: true, staleNearClose: true, spreadTooWide: false, hasBidAsk: true));
        Assert.Equal(
            HistoricalCloseBenchmarkStatus.SpreadTooWide,
            ExecutionSimR003HistoricalQuoteReadiness.EvaluateCloseBenchmark(hasQuoteNearClose: true, staleNearClose: false, spreadTooWide: true, hasBidAsk: true));
    }

    [Fact]
    public void Usd_pair_coverage_list_exists()
    {
        var coverage = ExecutionSimR003HistoricalQuoteReadiness.CreateUsdPairCoverageRequirements();

        Assert.Contains("AUDUSD", coverage);
        Assert.Contains("EURUSD", coverage);
        Assert.Contains("GBPUSD", coverage);
        Assert.Contains("NZDUSD", coverage);
        Assert.Contains("USDJPY", coverage);
        Assert.Contains("USDCAD", coverage);
        Assert.Contains("USDCHF", coverage);
        Assert.Contains("USDMXN", coverage);
        Assert.Contains("USDCNH", coverage);
        Assert.Contains("USDNOK", coverage);
        Assert.Contains("USDSEK", coverage);
        Assert.Contains("USDSGD or SGDUSD if convention configured", coverage);
        Assert.Contains("USDZAR", coverage);
    }

    [Fact]
    public void Direct_crosses_remain_signal_only()
    {
        var directCrosses = ExecutionSimR003HistoricalQuoteReadiness.CreateDirectCrossSignalOnlySymbols();

        Assert.Contains("EURGBP", directCrosses);
        Assert.Contains("CADJPY", directCrosses);
        Assert.Contains("AUDCNH", directCrosses);
        Assert.Contains("CNHSGD", directCrosses);
        Assert.Contains("EURZAR", directCrosses);
        Assert.Contains("MXNNOK", directCrosses);
    }

    [Fact]
    public void Package_is_no_external_and_creates_no_order_domain_records()
    {
        var package = ExecutionSimR003HistoricalQuoteReadiness.CreatePackage();

        Assert.False(package.PolygonApiCalled);
        Assert.False(package.LmaxCalled);
        Assert.False(package.ExternalApiCalled);
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
        var package = ExecutionSimR003HistoricalQuoteReadiness.CreatePackage();

        Assert.Contains("AUDUSD", package.UsdPairCoverageRequirements);
        Assert.Contains("USDJPY", package.UsdPairCoverageRequirements);
        Assert.Contains(package.SafeFailureCategories, x => x == HistoricalQuoteFailureCategory.InconclusiveSafe);
        Assert.DoesNotContain(package.SafeFailureCategories, x => x.ToString().Contains("AudusdFailed", StringComparison.OrdinalIgnoreCase));
    }

    private static HistoricalProviderCapabilityRecord Provider(HistoricalQuoteProviderName name)
        => ExecutionSimR003HistoricalQuoteReadiness.CreateProviderComparison().Single(x => x.ProviderName == name);
}
