using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimOperatorQuoteFileValidationFixtureTests
{
    [Fact]
    public void Validation_fixture_contract_and_local_files_manifest_exist()
    {
        var contract = ExecutionSimR007OperatorQuoteFileValidationFixtures.CreateValidationFixtureContract();
        var files = ExecutionSimR007OperatorQuoteFileValidationFixtures.CreateLocalFixtureFilesManifest();

        Assert.True(contract.LocalFixtureFilesOnly);
        Assert.True(contract.ValidatesAsOperatorProvidedOfflineFiles);
        Assert.False(contract.RunsImportBacktest);
        Assert.False(contract.PolygonApiCalled);
        Assert.False(contract.LmaxCalled);
        Assert.False(contract.ExternalApiCalled);
        Assert.Contains("NDJSON", contract.ConcreteFixtureFormats);
        Assert.Contains("JSON", contract.ContractOnlyFixtureFormats);
        Assert.Contains("CSV", contract.ContractOnlyFixtureFormats);
        Assert.Contains(files, x => x.FixtureFileId == "r007-valid-eurusd");
        Assert.Contains(files, x => x.FixtureFileId == "r007-direct-cross-eurgbp");
        Assert.All(files, x => Assert.Equal("NDJSON", x.FileFormat));
    }

    [Fact]
    public void Deterministic_local_sample_files_exist_and_do_not_contain_serialized_secrets_or_raw_payload_dumps()
    {
        foreach (var file in ExecutionSimR007OperatorQuoteFileValidationFixtures.CreateLocalFixtureFilesManifest())
        {
            var path = Path.Combine(RepoRoot(), file.SafeFixturePathCategory.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(path), $"Missing fixture file {file.SafeFixturePathCategory}");
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("apiKey", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("credential", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CompID", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("rawFix", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Valid_usd_pair_quote_files_are_accepted()
    {
        var package = ExecutionSimR007OperatorQuoteFileValidationFixtures.CreatePackage();

        AssertAccepted(package, "C:EUR-USD", "EURUSD", requiresInversion: false);
        AssertAccepted(package, "C:USD-JPY", "USDJPY", requiresInversion: true);
        AssertAccepted(package, "C:AUD-USD", "AUDUSD", requiresInversion: false);
        Assert.Equal(3, package.AcceptedFileManifests.Count);
        Assert.All(package.AcceptedFileManifests, x => Assert.Equal(OfflineQuoteFileIntakeStatus.AcceptedForSanitizedImport, x.IntakeStatus));
    }

    [Fact]
    public void Quarantined_files_cover_direct_cross_missing_convention_malformed_and_row_rejection_cases()
    {
        var runs = ExecutionSimR007OperatorQuoteFileValidationFixtures.CreatePackage().ValidationRuns;

        AssertRun(runs, "C:EUR-GBP", OfflineQuoteFileIntakeStatus.QuarantinedDirectCrossExecutionDisabled, PolygonOfflineImportFailureCategory.DirectCrossExecutionDisabled);
        AssertRun(runs, "C:SGD-USD", OfflineQuoteFileIntakeStatus.QuarantinedUnsupportedSymbol, PolygonOfflineImportFailureCategory.MissingInstrumentConvention);
        AssertRun(runs, "r007-malformed-file", OfflineQuoteFileIntakeStatus.QuarantinedMalformedFile, PolygonOfflineImportFailureCategory.InconclusiveSafe);
        AssertRun(runs, "r007-missing-timestamp", OfflineQuoteFileIntakeStatus.QuarantinedMissingTimestamp, PolygonOfflineImportFailureCategory.MissingTimestamp);
        AssertRun(runs, "r007-missing-bidask", OfflineQuoteFileIntakeStatus.QuarantinedMissingBidAsk, PolygonOfflineImportFailureCategory.MissingBid);
        AssertRun(runs, "r007-invalid-bidask", OfflineQuoteFileIntakeStatus.QuarantinedInvalidBidAsk, PolygonOfflineImportFailureCategory.InvalidBidAsk);
    }

    [Fact]
    public void Duplicate_secret_and_raw_payload_risk_files_are_handled_safely()
    {
        var runs = ExecutionSimR007OperatorQuoteFileValidationFixtures.CreatePackage().ValidationRuns;

        var duplicate = runs.Single(x => x.IntakeStatus == OfflineQuoteFileIntakeStatus.DuplicateReturned);
        Assert.Equal(PolygonOfflineImportFailureCategory.DuplicateRows, duplicate.QuarantineReason);
        Assert.False(duplicate.SanitizedImportReady);

        var secret = runs.Single(x => x.IntakeStatus == OfflineQuoteFileIntakeStatus.QuarantinedSecretLeakRisk);
        Assert.True(secret.SecretMaterialDetected);
        Assert.False(secret.SecretMaterialSerialized);
        Assert.False(secret.SanitizedImportReady);

        var raw = runs.Single(x => x.IntakeStatus == OfflineQuoteFileIntakeStatus.QuarantinedRawPayloadLeakRisk);
        Assert.False(raw.RawPayloadSerialized);
        Assert.False(raw.SecretMaterialSerialized);
        Assert.False(raw.SanitizedImportReady);
    }

    [Fact]
    public void Accepted_files_produce_sanitized_import_readiness_quote_window_benchmark_and_feed_quality_outputs()
    {
        var package = ExecutionSimR007OperatorQuoteFileValidationFixtures.CreatePackage();

        Assert.Equal(3, package.SanitizedImportReadinessOutputs.Count);
        Assert.Equal(3, package.QuoteWindowReadinessResults.Count);
        Assert.Equal(3, package.CloseBenchmarkReadinessResults.Count);
        Assert.Equal(3, package.FeedQualityReadinessResults.Count);
        Assert.All(package.SanitizedImportReadinessOutputs, output =>
        {
            Assert.True(output.SanitizedImportReady);
            Assert.True(output.FixtureOnly);
            Assert.False(output.RawPayloadSerialized);
            Assert.False(output.SecretMaterialSerialized);
            Assert.True(output.AcceptedRowCount > 0);
            Assert.True(output.QuoteWindow.QuoteCount > 0);
            Assert.True(output.QuoteWindow.QuoteCountLastMinute > 0);
            Assert.Equal(HistoricalCloseBenchmarkStatus.Available, output.CloseBenchmark.CloseBenchmarkStatus);
            Assert.Equal(HistoricalFeedQualityBucket.Good, output.FeedQualityScore.FeedQualityBucket);
        });
    }

    [Fact]
    public void Quarantined_files_do_not_produce_accepted_import_ready_outputs()
    {
        var package = ExecutionSimR007OperatorQuoteFileValidationFixtures.CreatePackage();
        var quarantinedRunIds = package.QuarantinedFileManifests.Select(x => x.QuoteFileManifestId.Replace(":manifest", string.Empty)).ToHashSet();

        Assert.NotEmpty(quarantinedRunIds);
        Assert.DoesNotContain(package.SanitizedImportReadinessOutputs, output => quarantinedRunIds.Contains(output.FileValidationRunId));
    }

    [Fact]
    public void Operator_validation_summary_reports_accepted_quarantined_and_duplicate_counts()
    {
        var summary = ExecutionSimR007OperatorQuoteFileValidationFixtures.CreatePackage().OperatorValidationSummary;

        Assert.Equal(3, summary.AcceptedFileCount);
        Assert.Equal(8, summary.QuarantinedFileCount);
        Assert.Equal(1, summary.DuplicateFileCount);
        Assert.Contains("Review accepted manifests before later import", summary.OperatorReviewSteps);
        Assert.Contains("Use a later explicit gate for imported-quote backtest dry run", summary.OperatorReviewSteps);
        Assert.False(summary.ExternalApiCalled);
        Assert.False(summary.OrdersFillsReportsRoutesSubmissionsCreated);
    }

    [Fact]
    public void Package_is_no_external_and_creates_no_order_domain_records()
    {
        var package = ExecutionSimR007OperatorQuoteFileValidationFixtures.CreatePackage();

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
        Assert.False(package.SecretMaterialSerialized);
    }

    [Fact]
    public void Source_introduces_no_external_api_broker_marketdata_runtime_or_scheduler_shape()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/ExecutionSimCloseSeekingFoundation.cs"));

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
        var package = ExecutionSimR007OperatorQuoteFileValidationFixtures.CreatePackage();

        Assert.Contains(package.AcceptedFileManifests, x => x.ExecutionTradableSymbol == "AUDUSD");
        Assert.Contains(package.SanitizedImportReadinessOutputs, x => x.ExecutionTradableSymbol == "USDJPY" && x.NormalizedPortfolioSymbol == "JPYUSD" && x.RequiresInversion);
    }

    private static void AssertAccepted(
        OperatorQuoteFileValidationFixturePackage package,
        string providerSymbol,
        string executionSymbol,
        bool requiresInversion)
    {
        var run = package.ValidationRuns.Single(x => x.ProviderSymbol == providerSymbol && x.IntakeStatus == OfflineQuoteFileIntakeStatus.AcceptedForSanitizedImport);
        var output = package.SanitizedImportReadinessOutputs.Single(x => x.ProviderSymbol == providerSymbol);

        Assert.Equal(executionSymbol, run.ExecutionTradableSymbol);
        Assert.True(run.SanitizedImportReady);
        Assert.True(run.AcceptedRowCount > 0);
        Assert.Equal(HistoricalQuoteReadinessStatus.ReadyForFixtureImportOnly, run.QuoteWindowReadinessStatus);
        Assert.Equal(HistoricalCloseBenchmarkStatus.Available, run.CloseBenchmarkReadinessStatus);
        Assert.Equal(HistoricalFeedQualityBucket.Good, run.FeedQualityReadinessStatus);
        Assert.Equal(requiresInversion, output.RequiresInversion);
    }

    private static void AssertRun(
        IReadOnlyList<OperatorQuoteFileValidationRun> runs,
        string lookup,
        OfflineQuoteFileIntakeStatus status,
        PolygonOfflineImportFailureCategory reason)
    {
        var run = runs.Single(x => x.ProviderSymbol == lookup || x.FileValidationRunId.Contains(lookup, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(status, run.IntakeStatus);
        Assert.Equal(reason, run.QuarantineReason);
        Assert.False(run.SanitizedImportReady);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
