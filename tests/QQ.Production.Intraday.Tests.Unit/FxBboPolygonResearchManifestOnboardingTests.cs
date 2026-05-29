using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class FxBboPolygonResearchManifestOnboardingTests
{
    private static readonly DateTimeOffset ValidationTime = new(2026, 01, 05, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 01, 05, 12, 00, 00, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void No_approved_manifest_produces_template_with_authorized_false()
    {
        var onboarding = new FxBboPolygonResearchManifestOnboarding();
        var discovery = onboarding.ValidateManifestCandidates([], ValidationTime);
        var template = onboarding.CreateTemplateManifest();

        Assert.False(discovery.ApprovedManifestFound);
        Assert.False(discovery.RealDataFilesMayBeOpened);
        Assert.False(template.AuthorizedForResearch);
        Assert.All(template.Files, file => Assert.False(file.Approved));
        Assert.Equal("Polygon", template.DatasetVendor);
        Assert.Equal("FxBboOfflineQuotes", template.DatasetKind);
    }

    [Fact]
    public void Template_json_uses_string_enums_and_does_not_approve_real_files()
    {
        var json = new FxBboPolygonResearchManifestOnboarding().CreateTemplateJson();

        Assert.Contains("\"AuthorizedForResearch\": false", json, StringComparison.Ordinal);
        Assert.Contains("\"Approved\": false", json, StringComparison.Ordinal);
        Assert.Contains("\"Format\": \"Csv\"", json, StringComparison.Ordinal);
        Assert.Contains("\"AvailabilityMode\": \"ExplicitAvailableAtColumn\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("1.1000", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Manifest_with_authorized_false_blocks_real_data_loading_before_file_open()
    {
        var directory = CreateTempDirectory();
        var manifestPath = WriteManifest(directory, Manifest(authorized: false, filePath: "missing-real-file.csv"));

        var result = Load(manifestPath, allowLocalEvaluation: true);

        Assert.Equal(FxBboResearchDataAuthorizationStatus.Blocked, result.Status);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.AuthorizedForResearchFalse);
        Assert.DoesNotContain(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.FileNotFound);
    }

    [Fact]
    public void Manifest_with_file_approved_false_blocks_file_loading()
    {
        var directory = CreateTempDirectory();
        var filePath = WriteCsv(directory, "quotes.csv", ValidCsv());
        var manifestPath = WriteManifest(directory, Manifest(filePath: filePath, fileApproved: false));

        var result = Load(manifestPath, allowLocalEvaluation: true);

        Assert.Equal(FxBboResearchDataAuthorizationStatus.Blocked, result.Status);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.FileApprovedFalse);
    }

    [Fact]
    public void Unknown_availability_mode_blocks_local_evaluation()
    {
        var directory = CreateTempDirectory();
        var filePath = WriteCsv(directory, "quotes.csv", ValidCsv());
        var manifestPath = WriteManifest(directory, Manifest(filePath: filePath, availabilityMode: FxBboResearchAvailabilityMode.Unknown));

        var discovery = new FxBboPolygonResearchManifestOnboarding().ValidateManifestCandidates([manifestPath], ValidationTime);

        Assert.False(discovery.ApprovedManifestFound);
        Assert.False(discovery.RealDataFilesMayBeOpened);
        Assert.Contains(discovery.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.UnknownAvailabilityMode);
    }

    [Fact]
    public void Event_timestamp_as_availability_proxy_blocks_by_default()
    {
        var directory = CreateTempDirectory();
        var filePath = WriteCsv(directory, "quotes.csv", ValidCsv(includeAvailableAt: false));
        var manifestPath = WriteManifest(directory, Manifest(
            filePath: filePath,
            availabilityMode: FxBboResearchAvailabilityMode.EventTimestampAsAvailabilityProxy,
            availableAtColumn: null));

        var discovery = new FxBboPolygonResearchManifestOnboarding().ValidateManifestCandidates([manifestPath], ValidationTime);

        Assert.False(discovery.ApprovedManifestFound);
        Assert.Contains(discovery.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.EventTimestampAvailabilityProxyBlocked);
    }

    [Fact]
    public void Event_timestamp_plus_positive_delay_passes_availability_mode_validation()
    {
        var directory = CreateTempDirectory();
        var filePath = WriteCsv(directory, "quotes.csv", ValidCsv(includeAvailableAt: false));
        var manifestPath = WriteManifest(directory, Manifest(
            filePath: filePath,
            availabilityMode: FxBboResearchAvailabilityMode.EventTimestampPlusConfiguredDelay,
            availableAtColumn: null,
            assumedAvailabilityDelay: TimeSpan.FromSeconds(5)));

        var discovery = new FxBboPolygonResearchManifestOnboarding().ValidateManifestCandidates([manifestPath], ValidationTime);

        Assert.True(discovery.ApprovedManifestFound);
        Assert.True(discovery.RealDataFilesMayBeOpened);
        Assert.DoesNotContain(discovery.Diagnostics, x => x.Reason == FxBboOfflineResearchQuoteLoadRejectReason.NonPositiveAvailabilityDelay);
    }

    [Fact]
    public void Approved_synthetic_manifest_loader_sampler_residual_still_works()
    {
        var load = LoadScenario(ScenarioCsvRows(110, shockIndex: 75, shockReturn: 0.012));
        var sample = Sample(load.Quotes, Start, Start.AddMinutes(109));
        var signals = GenerateSignals(sample);

        var signal = Assert.Single(signals, x => x.IsAccepted);
        Assert.True(signal.DiagnosticOnly);
        Assert.Equal(FxResidualDivergenceEligibleTiming.NextBarOnly, signal.EligibleExecutionTiming);
    }

    [Fact]
    public void Quote_and_available_timestamp_boundaries_are_enforced_end_to_end()
    {
        var load = LoadScenario(
        [
            Row("EURUSD", Start, Start.AddMinutes(1), 1.1000m),
            Row("GBPUSD", Start, Start, 1.2500m),
            Row("USDJPY", Start, Start, 155.00m)
        ]);

        var sample = Sample(load.Quotes, Start, Start);

        Assert.Empty(sample.Observations);
        Assert.Contains(sample.Diagnostics, x =>
            x.Symbol == "EURUSD" &&
            x.Reason == FxBboSamplingRejectReasonResearch.MissingQuoteAtOrBeforeGridTime);
    }

    [Fact]
    public void Future_rows_appended_to_approved_synthetic_file_do_not_change_prior_observations_or_signals()
    {
        var baselineLoad = LoadScenario(ScenarioCsvRows(110, shockIndex: 75, shockReturn: 0.012));
        var extendedLoad = LoadScenario(ScenarioCsvRows(110, shockIndex: 75, shockReturn: 0.012)
            .Concat(ScenarioCsvRows(20, Start.AddMinutes(110), 2.0m))
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
    public void Template_artifact_mode_does_not_write_raw_rows()
    {
        var directory = CreateTempDirectory();
        var path = Path.Combine(directory, "POLYGON_FX_BBO_RESEARCH_AUTHORIZATION_TEMPLATE.json");

        File.WriteAllText(path, new FxBboPolygonResearchManifestOnboarding().CreateTemplateJson(), Encoding.UTF8);
        var content = File.ReadAllText(path);

        Assert.DoesNotContain("timestampUtc,bid,ask", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1.1000", content, StringComparison.Ordinal);
        Assert.Contains("\"AuthorizedForResearch\": false", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Onboarding_code_does_not_bind_execution_sizing_or_production_outputs()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxBboPolygonResearchManifestOnboarding.cs"));

        Assert.DoesNotContain("MarketDataSnapshot", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetNotional", source, StringComparison.Ordinal);
        Assert.DoesNotContain("QuantityPolicy", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetWeight", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CoreExecution", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CoreNetting", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lmax", source, StringComparison.OrdinalIgnoreCase);
    }

    private static FxBboOfflineResearchQuoteLoadResult LoadScenario(IReadOnlyList<string> rows)
    {
        var directory = CreateTempDirectory();
        var csv = "symbol,timestampUtc,availableAtUtc,bid,ask,sequenceId\n" + string.Concat(rows);
        var filePath = WriteCsv(directory, "quotes.csv", csv);
        var manifestPath = WriteManifest(directory, Manifest(filePath: filePath, maxRows: rows.Count));
        return Load(manifestPath, allowLocalEvaluation: true);
    }

    private static FxBboOfflineResearchQuoteLoadResult Load(string manifestPath, bool allowLocalEvaluation)
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
        return new FxResidualDivergenceResearchStrategy().GenerateSignals(bars, new(
            TargetSymbol: "EURUSD",
            PeerSymbols: ["GBPUSD", "USDJPY"],
            RegressionLookbackBars: 30,
            ResidualZLookbackBars: 20,
            MinRegressionObservations: 20,
            EntryZScore: 3.0,
            MaxAbsBeta: 5.0,
            MinPeerCount: 2,
            EvaluationHorizonBars: 5));
    }

    private static FxBboResearchDataAuthorizationManifest Manifest(
        bool authorized = true,
        string? filePath = null,
        bool fileApproved = true,
        FxBboResearchAvailabilityMode availabilityMode = FxBboResearchAvailabilityMode.ExplicitAvailableAtColumn,
        string? availableAtColumn = "availableAtUtc",
        TimeSpan? assumedAvailabilityDelay = null,
        int? maxRows = 1000)
        => new(
            ManifestVersion: "fx-bbo-research-auth.v1",
            DatasetName: "Synthetic Polygon FX BBO Test",
            DatasetVendor: "Polygon",
            DatasetKind: "FxBboOfflineQuotes",
            AuthorizedForResearch: authorized,
            AuthorizedBy: "unit-test",
            AuthorizationTimestampUtc: ValidationTime.AddMinutes(-1),
            AuthorizationExpiresUtc: ValidationTime.AddDays(1),
            Files:
            [
                new(
                    Path: filePath ?? "quotes.csv",
                    Sha256: null,
                    Symbol: null,
                    Format: FxBboResearchFileFormat.Csv,
                    TimestampColumn: "timestampUtc",
                    BidColumn: "bid",
                    AskColumn: "ask",
                    SymbolColumn: "symbol",
                    AvailableAtColumn: availableAtColumn,
                    ReceivedAtColumn: null,
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
            yield return Row(symbol, timestamp, timestamp.AddSeconds(1), midpoint, index + 1);
        }
    }

    private static string Row(string symbol, DateTimeOffset timestamp, DateTimeOffset availableAt, decimal midpoint, int sequenceId = 1)
        => FormattableString.Invariant($"{symbol},{timestamp:O},{availableAt:O},{midpoint - 0.00001m},{midpoint + 0.00001m},{sequenceId}\n");

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "fx-bbo-polygon-onboarding-tests", Guid.NewGuid().ToString("N"));
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
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions), Encoding.UTF8);
        return path;
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
