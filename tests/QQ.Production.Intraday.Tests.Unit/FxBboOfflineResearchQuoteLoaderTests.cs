using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class FxBboOfflineResearchQuoteLoaderTests
{
    private static readonly DateTimeOffset ValidationTime = new(2026, 01, 05, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 01, 05, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public void Missing_manifest_blocks_file_loading()
    {
        var result = Load(Path.Combine(CreateTempDirectory(), "missing.json"));

        Assert.Equal(FxBboResearchDataAuthorizationStatus.Blocked, result.Status);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.MissingManifest);
        Assert.False(result.LocalEvaluationGate.CanRun);
    }

    [Fact]
    public void Authorized_for_research_false_blocks_before_data_file_is_opened()
    {
        var directory = CreateTempDirectory();
        var manifestPath = WriteManifest(directory, Manifest(authorized: false, filePath: "does-not-exist.csv"));

        var result = Load(manifestPath);

        Assert.Equal(FxBboResearchDataAuthorizationStatus.Blocked, result.Status);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.AuthorizedForResearchFalse);
        Assert.DoesNotContain(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.FileNotFound);
    }

    [Fact]
    public void Expired_authorization_blocks_file_loading()
    {
        var directory = CreateTempDirectory();
        var filePath = WriteCsv(directory, "quotes.csv", ValidCsv());
        var manifestPath = WriteManifest(directory, Manifest(expiresUtc: ValidationTime.AddSeconds(-1), filePath: filePath));

        var result = Load(manifestPath);

        Assert.Equal(FxBboResearchDataAuthorizationStatus.Blocked, result.Status);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.AuthorizationExpired);
    }

    [Fact]
    public void File_approved_false_blocks_file_loading()
    {
        var directory = CreateTempDirectory();
        var filePath = WriteCsv(directory, "quotes.csv", ValidCsv());
        var manifestPath = WriteManifest(directory, Manifest(filePath: filePath, fileApproved: false));

        var result = Load(manifestPath);

        Assert.Equal(FxBboResearchDataAuthorizationStatus.Blocked, result.Status);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.FileApprovedFalse);
    }

    [Fact]
    public void Hash_mismatch_blocks_file_loading()
    {
        var directory = CreateTempDirectory();
        var filePath = WriteCsv(directory, "quotes.csv", ValidCsv());
        var manifestPath = WriteManifest(directory, Manifest(filePath: filePath, sha256: new string('A', 64)));

        var result = Load(manifestPath, allowLocalEvaluation: true);

        Assert.Equal(FxBboResearchDataAuthorizationStatus.Blocked, result.Status);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.FileHashMismatch);
        Assert.Empty(result.Quotes);
    }

    [Fact]
    public void Unknown_availability_mode_blocks_local_sample_evaluation()
    {
        var directory = CreateTempDirectory();
        var filePath = WriteCsv(directory, "quotes.csv", ValidCsv());
        var manifestPath = WriteManifest(directory, Manifest(filePath: filePath, availabilityMode: FxBboResearchAvailabilityMode.Unknown));

        var result = Load(manifestPath);

        Assert.Equal(FxBboResearchDataAuthorizationStatus.Blocked, result.Status);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.UnknownAvailabilityMode);
    }

    [Fact]
    public void Event_timestamp_as_availability_proxy_blocks_without_override()
    {
        var directory = CreateTempDirectory();
        var filePath = WriteCsv(directory, "quotes.csv", ValidCsv(includeAvailableAt: false));
        var manifestPath = WriteManifest(directory, Manifest(
            filePath: filePath,
            availabilityMode: FxBboResearchAvailabilityMode.EventTimestampAsAvailabilityProxy,
            availableAtColumn: null));

        var result = Load(manifestPath);

        Assert.Equal(FxBboResearchDataAuthorizationStatus.Blocked, result.Status);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.EventTimestampAvailabilityProxyBlocked);
    }

    [Fact]
    public void Event_timestamp_plus_configured_delay_requires_positive_delay()
    {
        var directory = CreateTempDirectory();
        var filePath = WriteCsv(directory, "quotes.csv", ValidCsv(includeAvailableAt: false));
        var manifestPath = WriteManifest(directory, Manifest(
            filePath: filePath,
            availabilityMode: FxBboResearchAvailabilityMode.EventTimestampPlusConfiguredDelay,
            availableAtColumn: null,
            assumedAvailabilityDelay: TimeSpan.Zero));

        var result = Load(manifestPath);

        Assert.Equal(FxBboResearchDataAuthorizationStatus.Blocked, result.Status);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.NonPositiveAvailabilityDelay);
    }

    [Fact]
    public void Required_columns_missing_blocks_loading()
    {
        var directory = CreateTempDirectory();
        var filePath = WriteCsv(directory, "quotes.csv", "symbol,timestampUtc,bid\nEURUSD,2026-01-05T12:00:00Z,1.1000\n");
        var manifestPath = WriteManifest(directory, Manifest(filePath: filePath));

        var result = Load(manifestPath, allowLocalEvaluation: true);

        Assert.Equal(FxBboResearchDataAuthorizationStatus.Blocked, result.Status);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.RequiredColumnMissing);
    }

    [Fact]
    public void Valid_synthetic_csv_row_loads_into_research_quote()
    {
        var directory = CreateTempDirectory();
        var filePath = WriteCsv(directory, "quotes.csv", ValidCsv());
        var manifestPath = WriteManifest(directory, Manifest(filePath: filePath, sha256: Sha256(filePath)));

        var result = Load(manifestPath, allowLocalEvaluation: true);

        Assert.Equal(FxBboResearchDataAuthorizationStatus.Authorized, result.Status);
        var quote = Assert.Single(result.Quotes);
        Assert.Equal("EURUSD", quote.Symbol);
        Assert.Equal(Start, quote.TimestampUtc);
        Assert.Equal(Start.AddSeconds(1), quote.AvailableAtUtc);
        Assert.Equal(17, quote.SequenceId);
        Assert.Equal(1.1000m, quote.Bid);
        Assert.Equal(1.1002m, quote.Ask);
    }

    [Fact]
    public void Non_positive_bid_is_rejected()
    {
        var result = LoadSingleRow("symbol,timestampUtc,availableAtUtc,bid,ask,sequenceId\nEURUSD,2026-01-05T12:00:00Z,2026-01-05T12:00:01Z,0,1.1002,17\n");

        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.NonPositiveBid);
        Assert.Empty(result.Quotes);
    }

    [Fact]
    public void Non_positive_ask_is_rejected()
    {
        var result = LoadSingleRow("symbol,timestampUtc,availableAtUtc,bid,ask,sequenceId\nEURUSD,2026-01-05T12:00:00Z,2026-01-05T12:00:01Z,1.1000,0,17\n");

        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.NonPositiveAsk);
        Assert.Empty(result.Quotes);
    }

    [Fact]
    public void Ask_below_bid_is_rejected()
    {
        var result = LoadSingleRow("symbol,timestampUtc,availableAtUtc,bid,ask,sequenceId\nEURUSD,2026-01-05T12:00:00Z,2026-01-05T12:00:01Z,1.1002,1.1000,17\n");

        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.CrossedQuote);
        Assert.Empty(result.Quotes);
    }

    [Fact]
    public void Timestamp_parse_failure_is_rejected()
    {
        var result = LoadSingleRow("symbol,timestampUtc,availableAtUtc,bid,ask,sequenceId\nEURUSD,not-a-time,2026-01-05T12:00:01Z,1.1000,1.1002,17\n");

        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.TimestampParseFailed);
        Assert.Empty(result.Quotes);
    }

    [Fact]
    public void Missing_symbol_is_rejected()
    {
        var result = LoadSingleRow("symbol,timestampUtc,availableAtUtc,bid,ask,sequenceId\n,2026-01-05T12:00:00Z,2026-01-05T12:00:01Z,1.1000,1.1002,17\n");

        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.MissingSymbol);
        Assert.Empty(result.Quotes);
    }

    [Fact]
    public void Received_at_column_can_be_used_as_availability()
    {
        var directory = CreateTempDirectory();
        var filePath = WriteCsv(directory, "quotes.csv", "symbol,timestampUtc,receivedAtUtc,bid,ask,sequenceId\nEURUSD,2026-01-05T12:00:00Z,2026-01-05T12:00:02Z,1.1000,1.1002,17\n");
        var manifestPath = WriteManifest(directory, Manifest(
            filePath: filePath,
            availabilityMode: FxBboResearchAvailabilityMode.ExplicitReceivedAtColumn,
            availableAtColumn: null,
            receivedAtColumn: "receivedAtUtc"));

        var result = Load(manifestPath, allowLocalEvaluation: true);

        var quote = Assert.Single(result.Quotes);
        Assert.Equal(Start.AddSeconds(2), quote.AvailableAtUtc);
    }

    [Fact]
    public void Event_timestamp_plus_configured_delay_computes_availability()
    {
        var directory = CreateTempDirectory();
        var filePath = WriteCsv(directory, "quotes.csv", "symbol,timestampUtc,bid,ask,sequenceId\nEURUSD,2026-01-05T12:00:00Z,1.1000,1.1002,17\n");
        var manifestPath = WriteManifest(directory, Manifest(
            filePath: filePath,
            availabilityMode: FxBboResearchAvailabilityMode.EventTimestampPlusConfiguredDelay,
            availableAtColumn: null,
            assumedAvailabilityDelay: TimeSpan.FromSeconds(3)));

        var result = Load(manifestPath, allowLocalEvaluation: true);

        var quote = Assert.Single(result.Quotes);
        Assert.Equal(Start.AddSeconds(3), quote.AvailableAtUtc);
    }

    [Fact]
    public void Row_limit_is_enforced()
    {
        var directory = CreateTempDirectory();
        var csv = ValidCsv() + "EURUSD,2026-01-05T12:01:00Z,2026-01-05T12:01:01Z,1.1001,1.1003,18\n";
        var filePath = WriteCsv(directory, "quotes.csv", csv);
        var manifestPath = WriteManifest(directory, Manifest(filePath: filePath, maxRows: 1));

        var result = Load(manifestPath, allowLocalEvaluation: true);

        Assert.Equal(FxBboResearchDataAuthorizationStatus.Blocked, result.Status);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.RowLimitExceeded);
        Assert.Empty(result.Quotes);
    }

    [Fact]
    public void Quote_available_after_grid_timestamp_is_not_used_by_sampler()
    {
        var quote = new FxBboQuoteResearch("EURUSD", Start, 1.1000m, 1.1002m, AvailableAtUtc: Start.AddSeconds(1));
        var peers = new[]
        {
            new FxBboQuoteResearch("GBPUSD", Start, 1.2500m, 1.2502m, AvailableAtUtc: Start),
            new FxBboQuoteResearch("USDJPY", Start, 155.00m, 155.01m, AvailableAtUtc: Start)
        };

        var result = Sample([quote, .. peers], Start, Start);

        Assert.Empty(result.Observations);
        Assert.Contains(result.Diagnostics, x => x.Symbol == "EURUSD" && x.Reason == FxBboSamplingRejectReasonResearch.MissingQuoteAtOrBeforeGridTime);
    }

    [Fact]
    public void Quote_available_at_or_before_grid_timestamp_can_be_used_by_sampler()
    {
        var result = Sample(
            [
                new("EURUSD", Start, 1.1000m, 1.1002m, AvailableAtUtc: Start),
                new("GBPUSD", Start, 1.2500m, 1.2502m, AvailableAtUtc: Start),
                new("USDJPY", Start, 155.00m, 155.01m, AvailableAtUtc: Start)
            ],
            Start,
            Start);

        Assert.Single(result.Observations);
    }

    [Fact]
    public void Changing_future_available_rows_does_not_change_earlier_observations()
    {
        var quotes = ScenarioCsvRows(20);
        var baseline = LoadScenario(quotes);
        var mutated = LoadScenario(quotes.Concat(ScenarioCsvRows(5, Start.AddMinutes(20), 1.50m)).ToArray());

        var baselineSample = Sample(baseline.Quotes, Start, Start.AddMinutes(10));
        var mutatedSample = Sample(mutated.Quotes, Start, Start.AddMinutes(10));

        Assert.Equal(
            baselineSample.Observations.Select(x => x.Midpoints["EURUSD"]).ToArray(),
            mutatedSample.Observations.Select(x => x.Midpoints["EURUSD"]).ToArray());
    }

    [Fact]
    public void Local_sample_evaluation_is_blocked_when_caller_does_not_explicitly_allow_it()
    {
        var directory = CreateTempDirectory();
        var filePath = WriteCsv(directory, "quotes.csv", ValidCsv());
        var manifestPath = WriteManifest(directory, Manifest(filePath: filePath));

        var result = Load(manifestPath, allowLocalEvaluation: false);

        Assert.Equal(FxBboResearchDataAuthorizationStatus.Blocked, result.Status);
        Assert.Empty(result.Quotes);
        Assert.False(result.LocalEvaluationGate.CanRun);
        Assert.Contains("Caller did not request", result.LocalEvaluationGate.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Authorized_synthetic_file_loads_then_samples_then_feeds_residual_strategy()
    {
        var load = LoadScenario(ScenarioCsvRows(110, shockIndex: 75, shockReturn: 0.012));
        var sample = Sample(load.Quotes, Start, Start.AddMinutes(109));
        var bars = new FxBboToSynchronizedMidpointSamplerResearch().ToResidualDivergenceBars(sample.Observations);

        var signals = new FxResidualDivergenceResearchStrategy().GenerateSignals(bars, StrategyParameters());
        var signal = Assert.Single(signals, x => x.IsAccepted);

        Assert.True(signal.IsAccepted);
        Assert.Equal(FxResidualDivergenceEligibleTiming.NextBarOnly, signal.EligibleExecutionTiming);
        Assert.True(signal.DiagnosticOnly);
    }

    [Fact]
    public void Future_file_rows_do_not_change_prior_observations_or_residual_signals()
    {
        var baselineLoad = LoadScenario(ScenarioCsvRows(110, shockIndex: 75, shockReturn: 0.012));
        var extendedLoad = LoadScenario(ScenarioCsvRows(110, shockIndex: 75, shockReturn: 0.012)
            .Concat(ScenarioCsvRows(20, Start.AddMinutes(110), 2.00m))
            .ToArray());
        var baselineSample = Sample(baselineLoad.Quotes, Start, Start.AddMinutes(109));
        var extendedSample = Sample(extendedLoad.Quotes, Start, Start.AddMinutes(129));
        var decisionTime = Start.AddMinutes(75);

        Assert.Equal(
            baselineSample.Observations.Where(x => x.TimestampUtc <= decisionTime).Select(x => x.Midpoints["EURUSD"]).ToArray(),
            extendedSample.Observations.Where(x => x.TimestampUtc <= decisionTime).Select(x => x.Midpoints["EURUSD"]).ToArray());

        var baselineSignals = GenerateSignals(baselineSample).Where(x => x.TimestampUtc <= decisionTime).ToArray();
        var extendedSignals = GenerateSignals(extendedSample).Where(x => x.TimestampUtc <= decisionTime).ToArray();
        Assert.Equal(baselineSignals.Length, extendedSignals.Length);
        for (var index = 0; index < baselineSignals.Length; index++)
        {
            Assert.Equal(baselineSignals[index].ReasonCode, extendedSignals[index].ReasonCode);
            Assert.Equal(baselineSignals[index].ResidualZScore, extendedSignals[index].ResidualZScore, 10);
            Assert.Equal(baselineSignals[index].PredictedReturn, extendedSignals[index].PredictedReturn, 10);
        }
    }

    [Fact]
    public void Loader_code_does_not_bind_execution_sizing_or_production_outputs()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxBboOfflineResearchQuoteLoader.cs"));

        Assert.DoesNotContain("MarketDataSnapshot", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetNotional", source, StringComparison.Ordinal);
        Assert.DoesNotContain("QuantityPolicy", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetWeight", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CoreExecution", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CoreNetting", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lmax", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Loader_is_not_referenced_from_production_execution_or_sizing_paths()
    {
        var root = FindRepoRoot();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxBboOfflineResearchQuoteLoader.cs")),
            Path.GetFullPath(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxBboPolygonResearchManifestOnboarding.cs")),
            Path.GetFullPath(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxBboPolygonResearchManifestGenerator.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxBboOfflineResearchQuoteLoaderTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceResearchTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceBboSamplingResearchTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxBboPolygonResearchManifestOnboardingTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxBboPolygonResearchManifestGeneratorTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceLocalEvaluationR006Tests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceLocalSmokeEvalR011Tests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceCoverageSmokeEvalR014Tests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergencePreregisteredEvalR015RunTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceExtendedR016ApprovalEvalR017Tests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceZScoreRobustnessAuditR018Tests.cs"))
        };

        var references = Directory
            .GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("FxBboOfflineResearchQuoteLoader", StringComparison.Ordinal))
            .Select(Path.GetFullPath)
            .Where(path => !allowed.Contains(path))
            .ToArray();

        Assert.Empty(references);
    }

    private static FxBboOfflineResearchQuoteLoadResult LoadSingleRow(string csv)
    {
        var directory = CreateTempDirectory();
        var filePath = WriteCsv(directory, "quotes.csv", csv);
        var manifestPath = WriteManifest(directory, Manifest(filePath: filePath));
        return Load(manifestPath, allowLocalEvaluation: true);
    }

    private static FxBboOfflineResearchQuoteLoadResult LoadScenario(IReadOnlyList<string> rows)
    {
        var directory = CreateTempDirectory();
        var csv = "symbol,timestampUtc,availableAtUtc,bid,ask,sequenceId\n" + string.Concat(rows);
        var filePath = WriteCsv(directory, "quotes.csv", csv);
        var manifestPath = WriteManifest(directory, Manifest(filePath: filePath, maxRows: rows.Count));
        return Load(manifestPath, allowLocalEvaluation: true);
    }

    private static FxBboOfflineResearchQuoteLoadResult Load(string manifestPath, bool allowLocalEvaluation = false)
        => new FxBboOfflineResearchQuoteLoader().Load(new(
            manifestPath,
            ValidationTime,
            AllowLocalEvaluation: allowLocalEvaluation));

    private static FxBboSamplingResultResearch Sample(IReadOnlyList<FxBboQuoteResearch> quotes, DateTimeOffset start, DateTimeOffset end)
        => new FxBboToSynchronizedMidpointSamplerResearch().Sample(
            quotes,
            new FxBboSamplingParametersResearch(
                ["EURUSD", "GBPUSD", "USDJPY"],
                start,
                end,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(1)));

    private static IReadOnlyList<FxResidualDivergenceResearchSignal> GenerateSignals(FxBboSamplingResultResearch sample)
    {
        var bars = new FxBboToSynchronizedMidpointSamplerResearch().ToResidualDivergenceBars(sample.Observations);
        return new FxResidualDivergenceResearchStrategy().GenerateSignals(bars, StrategyParameters());
    }

    private static FxResidualDivergenceParameters StrategyParameters()
        => new(
            TargetSymbol: "EURUSD",
            PeerSymbols: ["GBPUSD", "USDJPY"],
            RegressionLookbackBars: 30,
            ResidualZLookbackBars: 20,
            MinRegressionObservations: 20,
            EntryZScore: 3.0,
            MaxAbsBeta: 5.0,
            MinPeerCount: 2,
            EvaluationHorizonBars: 5);

    private static FxBboResearchDataAuthorizationManifest Manifest(
        bool authorized = true,
        DateTimeOffset? expiresUtc = null,
        string? filePath = null,
        bool fileApproved = true,
        string? sha256 = null,
        FxBboResearchAvailabilityMode availabilityMode = FxBboResearchAvailabilityMode.ExplicitAvailableAtColumn,
        string? availableAtColumn = "availableAtUtc",
        string? receivedAtColumn = null,
        TimeSpan? assumedAvailabilityDelay = null,
        int? maxRows = 1000)
        => new(
            ManifestVersion: "fx-bbo-research-auth.v1",
            DatasetName: "Synthetic FX BBO Test",
            DatasetVendor: "Synthetic",
            DatasetKind: "FxBboOfflineQuotes",
            AuthorizedForResearch: authorized,
            AuthorizedBy: "unit-test",
            AuthorizationTimestampUtc: ValidationTime.AddMinutes(-1),
            AuthorizationExpiresUtc: expiresUtc ?? ValidationTime.AddDays(1),
            Files:
            [
                new(
                    Path: filePath ?? "quotes.csv",
                    Sha256: sha256,
                    Symbol: null,
                    Format: FxBboResearchFileFormat.Csv,
                    TimestampColumn: "timestampUtc",
                    BidColumn: "bid",
                    AskColumn: "ask",
                    SymbolColumn: "symbol",
                    AvailableAtColumn: availableAtColumn,
                    ReceivedAtColumn: receivedAtColumn,
                    SequenceIdColumn: "sequenceId",
                    TimeZone: "UTC",
                    TimestampSemantics: "Source quote event timestamp UTC.",
                    AvailabilityMode: availabilityMode,
                    AssumedAvailabilityDelay: assumedAvailabilityDelay,
                    MaxAllowedReadRows: maxRows,
                    Approved: fileApproved)
            ]);

    private static string ValidCsv(bool includeAvailableAt = true)
        => includeAvailableAt
            ? "symbol,timestampUtc,availableAtUtc,bid,ask,sequenceId\nEURUSD,2026-01-05T12:00:00Z,2026-01-05T12:00:01Z,1.1000,1.1002,17\n"
            : "symbol,timestampUtc,bid,ask,sequenceId\nEURUSD,2026-01-05T12:00:00Z,1.1000,1.1002,17\n";

    private static IReadOnlyList<string> ScenarioCsvRows(
        int length,
        DateTimeOffset? start = null,
        decimal priceMultiplier = 1.0m,
        int? shockIndex = null,
        double shockReturn = 0.0)
    {
        var scenarioStart = start ?? Start;
        var peer1 = new double[length];
        var peer2 = new double[length];
        var target = new double[length];
        for (var index = 1; index < length; index++)
        {
            peer1[index] = 0.00020 * Math.Sin(index * 0.19) + 0.00003 * ((index % 5) - 2);
            peer2[index] = -0.00016 * Math.Cos(index * 0.13) + 0.000025 * ((index % 7) - 3);
            target[index] = 0.00001 + (0.70 * peer1[index]) - (0.40 * peer2[index]) + 0.000035 * Math.Sin(index * 0.37);
            if (shockIndex == index)
            {
                target[index] += shockReturn;
            }
        }

        return RowsFromReturns("EURUSD", target, 1.1000m * priceMultiplier, scenarioStart)
            .Concat(RowsFromReturns("GBPUSD", peer1, 1.2500m * priceMultiplier, scenarioStart))
            .Concat(RowsFromReturns("USDJPY", peer2, 155.00m * priceMultiplier, scenarioStart))
            .ToArray();
    }

    private static IEnumerable<string> RowsFromReturns(
        string symbol,
        IReadOnlyList<double> returns,
        decimal startPrice,
        DateTimeOffset scenarioStart)
    {
        var price = (double)startPrice;
        for (var index = 0; index < returns.Count; index++)
        {
            if (index > 0)
            {
                price *= Math.Exp(returns[index]);
            }

            var timestamp = scenarioStart.AddMinutes(index);
            var midpoint = (decimal)price;
            var bid = midpoint - 0.00001m;
            var ask = midpoint + 0.00001m;
            yield return FormattableString.Invariant(
                $"{symbol},{timestamp:O},{timestamp.AddSeconds(1):O},{bid},{ask},{index + 1}\n");
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "fx-bbo-loader-auth-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string WriteCsv(string directory, string name, string content)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    private static string WriteManifest(string directory, FxBboResearchDataAuthorizationManifest manifest)
    {
        var path = Path.Combine(directory, "manifest.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        return path;
    }

    private static string Sha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "QQ.Production.Intraday.sln")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        return current.FullName;
    }
}
