using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ExecutionSimRealHistoricalQuoteFileIntakeReadinessTests
{
    [Fact]
    public void Offline_quote_file_intake_contract_and_manifest_contract_exist()
    {
        var intake = ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateIntakeContract();
        var manifest = ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateManifestContract();

        Assert.Contains(OfflineQuoteFileProviderIdentity.PolygonOfflineFile, intake.ProviderIdentities);
        Assert.Contains(OfflineQuoteFileProviderIdentity.FixtureOnly, intake.ProviderIdentities);
        Assert.Contains(OfflineQuoteFileProviderIdentity.LMAXArchiveFuture, intake.ProviderIdentities);
        Assert.True(intake.OperatorProvidedFilesOnly);
        Assert.False(intake.PolygonApiCalled);
        Assert.False(intake.LmaxCalled);
        Assert.False(intake.ExternalApiCalled);
        Assert.False(intake.RawPayloadDumpAllowed);
        Assert.Contains("QuoteFileManifestId", manifest.RequiredFields);
        Assert.Contains("FileHash", manifest.RequiredFields);
        Assert.Contains("ContainsSecrets", manifest.RequiredFields);
        Assert.Contains("ContainsRawProviderPayload", manifest.RequiredFields);
        Assert.False(manifest.SecretsAllowed);
        Assert.False(manifest.RawProviderPayloadAllowed);
    }

    [Fact]
    public void Logical_locations_and_supported_formats_are_defined()
    {
        var intake = ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateIntakeContract();

        Assert.Contains("data/offline-quotes/polygon/incoming/", intake.LogicalLocations);
        Assert.Contains("data/offline-quotes/polygon/quarantine/", intake.LogicalLocations);
        Assert.Contains("data/offline-quotes/polygon/accepted/", intake.LogicalLocations);
        Assert.Contains("data/offline-quotes/polygon/sanitized/", intake.LogicalLocations);
        Assert.Contains("data/offline-quotes/polygon/processed/", intake.LogicalLocations);
        Assert.Contains("artifacts/readiness/execution-sim/", intake.LogicalLocations);
        Assert.Contains("JSON", intake.SupportedFileFormats);
        Assert.Contains("NDJSON", intake.SupportedFileFormats);
        Assert.Contains("CSV", intake.SupportedFileFormats);
    }

    [Fact]
    public void Valid_manifest_is_accepted_for_sanitized_import()
    {
        var result = ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.ValidateManifest(
            ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateValidManifest(),
            fileExists: true,
            duplicateHashes: new HashSet<string>());

        Assert.Equal(OfflineQuoteFileIntakeStatus.AcceptedForSanitizedImport, result.IntakeStatus);
        Assert.True(result.FileHashComputed);
        Assert.True(result.AcceptedForSanitizedImport);
        Assert.False(result.Quarantined);
    }

    [Fact]
    public void Missing_file_unsupported_symbol_and_direct_cross_are_quarantined_safely()
    {
        var valid = ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateValidManifest();
        var missing = ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.ValidateManifest(valid, fileExists: false, duplicateHashes: new HashSet<string>());
        var unsupported = ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.ValidateManifest(valid with { ProviderSymbol = "C:ABC-XYZ" }, fileExists: true, duplicateHashes: new HashSet<string>());
        var directCross = ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.ValidateManifest(valid with { ProviderSymbol = "C:EUR-GBP" }, fileExists: true, duplicateHashes: new HashSet<string>());

        Assert.Equal(OfflineQuoteFileIntakeStatus.QuarantinedMalformedFile, missing.IntakeStatus);
        Assert.Equal(PolygonOfflineImportFailureCategory.MissingFile, missing.FailureCategory);
        Assert.Equal(OfflineQuoteFileIntakeStatus.QuarantinedUnsupportedSymbol, unsupported.IntakeStatus);
        Assert.Equal(OfflineQuoteFileIntakeStatus.QuarantinedDirectCrossExecutionDisabled, directCross.IntakeStatus);
        Assert.Equal(PolygonOfflineImportFailureCategory.DirectCrossExecutionDisabled, directCross.FailureCategory);
    }

    [Fact]
    public void Duplicate_file_hash_returns_duplicate_returned_safely()
    {
        var valid = ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateValidManifest();
        var result = ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.ValidateManifest(
            valid,
            fileExists: true,
            duplicateHashes: new HashSet<string> { valid.FileHash });

        Assert.Equal(OfflineQuoteFileIntakeStatus.DuplicateReturned, result.IntakeStatus);
        Assert.True(result.DuplicateHashHandledDeterministically);
        Assert.False(result.AcceptedForSanitizedImport);
    }

    [Fact]
    public void Secret_and_raw_payload_leak_risks_are_quarantined()
    {
        var valid = ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateValidManifest();
        var secret = ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.ValidateManifest(valid with { ContainsSecrets = true }, fileExists: true, duplicateHashes: new HashSet<string>());
        var raw = ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.ValidateManifest(valid with { ContainsRawProviderPayload = true }, fileExists: true, duplicateHashes: new HashSet<string>());

        Assert.Equal(OfflineQuoteFileIntakeStatus.QuarantinedSecretLeakRisk, secret.IntakeStatus);
        Assert.Equal(PolygonOfflineImportFailureCategory.SecretLeakRisk, secret.FailureCategory);
        Assert.Equal(OfflineQuoteFileIntakeStatus.QuarantinedRawPayloadLeakRisk, raw.IntakeStatus);
        Assert.Equal(PolygonOfflineImportFailureCategory.RawPayloadLeakRisk, raw.FailureCategory);
    }

    [Fact]
    public void File_row_quote_window_close_benchmark_and_feed_quality_checks_are_represented()
    {
        Assert.Contains("file exists", ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateFileLevelValidationRules());
        Assert.Contains("timestamp parseable", ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateRowLevelValidationRules());
        Assert.Contains("bid finite positive", ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateRowLevelValidationRules());
        Assert.Contains("ask greater than or equal to bid", ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateRowLevelValidationRules());
        Assert.Contains("quote count last minute sufficient", ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateQuoteWindowReadinessChecks());
        Assert.Contains("last valid bid before close exists", ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateCloseBenchmarkReadinessChecks());
        Assert.Contains("FeedQualityBucket", ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateFeedQualityReadinessChecks());
    }

    [Fact]
    public void Operator_workflow_artifact_steps_are_represented()
    {
        var steps = ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateOperatorWorkflowSteps();

        Assert.Contains("Obtain quote files outside this system", steps);
        Assert.Contains("Place files in data/offline-quotes/polygon/incoming/", steps);
        Assert.Contains("Include sanitized manifest metadata without secrets", steps);
        Assert.Contains("Run no-external validation/import readiness", steps);
        Assert.Contains("Inspect accepted, quarantined, rejected, and duplicate results", steps);
    }

    [Fact]
    public void Package_is_no_external_and_creates_no_order_domain_records()
    {
        var package = ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreatePackage();

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
        var source = File.ReadAllText(SourcePath());

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
        var valid = ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.CreateValidManifest();
        var usdjpy = valid with { ProviderSymbol = "C:USD-JPY", ExecutionTradableSymbol = "USDJPY", FileHash = "sha256:valid-usdjpy" };
        var result = ExecutionSimR006RealHistoricalQuoteFileIntakeReadiness.ValidateManifest(usdjpy, fileExists: true, duplicateHashes: new HashSet<string>());

        Assert.Equal(OfflineQuoteFileIntakeStatus.AcceptedForSanitizedImport, result.IntakeStatus);
        Assert.True(ExecutionSimR004PolygonOfflineImportFixtures.CreateSymbolMapping().ContainsKey("AUDUSD"));
        Assert.True(ExecutionSimR004PolygonOfflineImportFixtures.CreateSymbolMapping()["USDJPY"].RequiresInversion);
    }

    private static string SourcePath()
        => Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/ExecutionSimCloseSeekingFoundation.cs");

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
