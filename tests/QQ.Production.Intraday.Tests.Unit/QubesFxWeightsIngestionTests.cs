using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesFxWeightsIngestionTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 19, 12, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);

    [Fact]
    public void Valid_qubes_semicolon_fixture_parses_successfully()
    {
        var result = Parse(SampleLines());

        Assert.True(result.Succeeded);
        Assert.Equal(17, result.RawInputRowCount);
        Assert.Equal(13, result.NormalizedOutputRowCount);
        Assert.Empty(result.Issues);
    }

    [Theory]
    [InlineData("NZDUSD Curncy", "NZDUSD", "NZD", "USD")]
    [InlineData("EURGBP Curncy", "EURGBP", "EUR", "GBP")]
    [InlineData("AUDJPY Curncy", "AUDJPY", "AUD", "JPY")]
    public void Bloomberg_tickers_parse_into_base_and_quote(string ticker, string pair, string baseCurrency, string quoteCurrency)
    {
        Assert.True(QubesFxWeightsFixtureIngestionService.TryParseBloombergFxTicker(ticker, out var parsedPair, out var parsedBase, out var parsedQuote));
        Assert.Equal(pair, parsedPair);
        Assert.Equal(baseCurrency, parsedBase);
        Assert.Equal(quoteCurrency, parsedQuote);
    }

    [Fact]
    public void Pair_weights_net_into_currency_exposures()
    {
        var result = Parse(["EURGBP Curncy;0.25", "GBPUSD Curncy;0.10"]);

        Assert.Equal(0.25m, result.CurrencyExposures["EUR"]);
        Assert.Equal(-0.15m, result.CurrencyExposures["GBP"]);
        Assert.Equal(-0.10m, result.CurrencyExposures["USD"]);
        Assert.Equal(0m, result.TotalCurrencyExposure);
    }

    [Fact]
    public void Normalized_output_uses_usd_as_quote_for_non_usd_exposures()
    {
        var result = Parse(["EURGBP Curncy;0.25", "GBPUSD Curncy;0.10"]);

        Assert.Contains(result.NormalizedWeights, x => x.BloombergTicker == "EURUSD Curncy" && x.Weight == 0.25m);
        Assert.Contains(result.NormalizedWeights, x => x.BloombergTicker == "GBPUSD Curncy" && x.Weight == -0.15m);
        Assert.DoesNotContain(result.NormalizedWeights, x => x.BloombergTicker == "USDUSD Curncy" || x.Symbol == "USDUSD");
    }

    [Fact]
    public void Total_exposure_sums_to_zero_within_tolerance()
    {
        var result = Parse(SampleLines());

        Assert.Equal(0m, result.TotalCurrencyExposure);
        Assert.True(Math.Abs(result.CurrencyExposures.Values.Sum()) <= 0.0000000001m);
    }

    [Fact]
    public void Sample_fixture_produces_expected_normalized_weights()
    {
        var expected = new Dictionary<string, decimal>
        {
            ["AUDUSD Curncy"] = 0.086178m,
            ["CADUSD Curncy"] = 0.017426m,
            ["CHFUSD Curncy"] = 0.002553m,
            ["CNHUSD Curncy"] = 1.186292m,
            ["EURUSD Curncy"] = 0.134196m,
            ["GBPUSD Curncy"] = -0.460092m,
            ["JPYUSD Curncy"] = -0.008443m,
            ["MXNUSD Curncy"] = 0.148627m,
            ["NOKUSD Curncy"] = 0.160180m,
            ["NZDUSD Curncy"] = -0.560724m,
            ["SEKUSD Curncy"] = -0.261092m,
            ["SGDUSD Curncy"] = -0.335555m,
            ["ZARUSD Curncy"] = -0.396527m
        };

        var result = Parse(SampleLines());

        Assert.Equal(expected.Count, result.NormalizedWeights.Count);
        foreach (var row in result.NormalizedWeights)
        {
            Assert.True(expected.TryGetValue(row.BloombergTicker, out var expectedWeight), row.BloombergTicker);
            Assert.Equal(expectedWeight, row.Weight);
        }
    }

    [Fact]
    public void Qubes_run_id_and_source_are_preserved()
    {
        var result = Parse(["EURUSD Curncy;0.10"], runId: "qubes-r002-run-001");

        Assert.Equal("qubes-r002-run-001", result.QubesRunId.Value);
        Assert.Equal(ModelWeightSourceSystem.Qubes, result.SourceSystem);
        Assert.NotNull(result.ModelWeightBatchRequest);
        Assert.Equal("qubes-r002-run-001", result.ModelWeightBatchRequest.ExternalBatchId);
        Assert.Equal(ModelWeightSourceSystem.Qubes, result.ModelWeightBatchRequest.SourceSystem);
    }

    [Fact]
    public void Fifteen_minute_cadence_is_accepted_and_wrong_cadence_is_rejected()
    {
        Assert.True(Parse(["EURUSD Curncy;0.10"], cadenceMinutes: 15).Succeeded);

        var wrongCadence = Parse(["EURUSD Curncy;0.10"], cadenceMinutes: 5);

        Assert.False(wrongCadence.Succeeded);
        Assert.Contains(wrongCadence.Issues, x => x.Code == QubesFxWeightsIngestionIssueCode.InvalidCadence);
        Assert.Null(wrongCadence.ModelWeightBatchRequest);
    }

    [Fact]
    public void Missing_run_id_is_rejected_safely()
    {
        var result = Parse(["EURUSD Curncy;0.10"], runId: "");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, x => x.Code == QubesFxWeightsIngestionIssueCode.MissingRunId);
        Assert.Null(result.ModelWeightBatchRequest);
    }

    [Theory]
    [InlineData("EURGBP Equity;0.1")]
    [InlineData("EURGB Curncy;0.1")]
    [InlineData("EURGBP Curncy;not-a-number")]
    [InlineData("EURGBP Curncy")]
    public void Malformed_ticker_or_weight_is_rejected_safely(string line)
    {
        var result = Parse([line]);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Issues);
        Assert.Null(result.ModelWeightBatchRequest);
    }

    [Fact]
    public async Task Unknown_normalized_instruments_are_rejected_by_existing_modelweight_validation()
    {
        var services = CreateServices();
        var result = Parse(SampleLines());

        var batch = await services.Generator.CreateFakeBatchAsync(result.ModelWeightBatchRequest!, CancellationToken.None);
        var validation = await services.Promotion.ValidateBatchAsync(batch.Id, CancellationToken.None);

        Assert.False(validation.Succeeded);
        Assert.Contains(validation.Issues, x => x.IssueType == ModelWeightValidationIssueType.UnknownInstrument);
    }

    [Fact]
    public void Audusd_and_usdjpy_live_validation_gaps_do_not_block_target_ingestion()
    {
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");
        var usdjpy = universe.Single(x => x.InternalInstrumentKey == "USDJPY");
        var result = Parse(["AUDUSD Curncy;0.10", "USDJPY Curncy;0.20"]);

        Assert.True(result.Succeeded);
        Assert.Equal(ApprovedInstrumentValidationStatus.PausedTlsBoundaryInconclusiveNotFailed, audusd.ValidationStatus);
        Assert.Equal(ApprovedInstrumentValidationStatus.NotProvenNotFailed, usdjpy.ValidationStatus);
        Assert.Contains(result.NormalizedWeights, x => x.BloombergTicker == "AUDUSD Curncy");
        Assert.Contains(result.NormalizedWeights, x => x.BloombergTicker == "JPYUSD Curncy");
    }

    [Fact]
    public void Usdjpy_caveat_remains_preserved()
    {
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var usdjpy = universe.Single(x => x.InternalInstrumentKey == "USDJPY");

        Assert.Equal("4004", usdjpy.SecurityId);
        Assert.Equal("8", usdjpy.SecurityIdSource);
        Assert.Equal(ApprovedInstrumentValidationStatus.NotProvenNotFailed, usdjpy.ValidationStatus);
        Assert.Contains("Not proven, not failed", usdjpy.ScopeNote);
    }

    [Fact]
    public async Task Valid_normalized_batch_maps_and_promotes_into_existing_modelweight_pipeline()
    {
        var services = CreateServices();
        var result = Parse(["EURUSD Curncy;0.10"], runId: "qubes-r002-promote");

        var batch = await services.Generator.CreateFakeBatchAsync(result.ModelWeightBatchRequest!, CancellationToken.None);
        var promotion = await services.Promotion.PromoteBatchAsync(batch.Id, CancellationToken.None);

        Assert.True(promotion.Succeeded);
        Assert.NotNull(promotion.ModelRunId);
        Assert.Equal(ModelWeightSourceSystem.Qubes, batch.SourceSystem);
        Assert.Contains(services.State.TargetWeights, x => x.ModelRunId == promotion.ModelRunId && x.RawSecurityId == "EURUSD Curncy" && x.Weight == 0.10m);
        Assert.DoesNotContain(services.State.ParentOrders, x => services.State.TradeIntents.Any(t => t.Id == x.TradeIntentId && t.ModelRunId == promotion.ModelRunId));
    }

    [Fact]
    public void Rebalance_intent_remains_non_executable_theoretical_only()
    {
        var instrumentId = InstrumentId.New();
        var current = new PortfolioSnapshot(ProducedAt, PortfolioStateSource.Theoretical, 1_000_000m, [], []);
        var target = new PortfolioSnapshot(ProducedAt, PortfolioStateSource.Theoretical, 1_000_000m, [
            new PortfolioPosition(instrumentId, 0m, null, 100_000m, 0.1m, new Dictionary<string, decimal> { ["USD"] = 100_000m })
        ], []);

        var intent = new RebalanceIntentCalculator().Calculate(current, target, EffectiveAt, 1m);

        Assert.False(intent.IsExecutable);
        Assert.Equal(IntentStatus.NotExecutable, intent.IntentStatus);
    }

    [Fact]
    public void Api_and_worker_remain_fake_lmax_gateway_only()
    {
        var apiProgram = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Api/Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Worker/Program.cs"));

        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", apiProgram);
        Assert.Contains("AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>", workerProgram);
        Assert.DoesNotContain("AddSingleton<IVenueExecutionGateway, RealLmaxGateway>", apiProgram + workerProgram);
        Assert.DoesNotContain("AddScoped<IVenueExecutionGateway, RealLmaxGateway>", apiProgram + workerProgram);
    }

    [Fact]
    public void Ingestion_implementation_contains_no_broker_or_marketdata_runtime_action()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/QubesFxWeightsIngestion.cs"));

        Assert.DoesNotContain("TcpClient", source);
        Assert.DoesNotContain("SslStream", source);
        Assert.DoesNotContain("RawFix", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FixSession", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MarketDataRequest", source);
        Assert.DoesNotContain("Lmax", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SendOrderAsync", source);
    }

    [Fact]
    public void Runtime_safety_defaults_remain_no_external()
    {
        var apiSettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Api/appsettings.json"))).RootElement;
        var workerSettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Worker/appsettings.json"))).RootElement;

        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(apiSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(workerSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
    }

    private static QubesFxWeightsIngestionResult Parse(IReadOnlyList<string> lines, string runId = "qubes-r002-sample", int cadenceMinutes = 15)
        => new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId(runId),
            ProducedAt,
            EffectiveAt,
            cadenceMinutes,
            "QQ_MASTER",
            "IntradayFxModel",
            1_000_000m,
            TargetQuantityMode.PortfolioBaseCurrencyNotional,
            lines));

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

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        var clock = new FixedClock(ProducedAt);
        var intradayRepository = new InMemoryIntradayRepository(state);
        var batchRepository = new InMemoryModelWeightBatchRepository(state);
        var integrity = new ReferenceDataIntegrityService(intradayRepository, clock);
        return new TestServices(state, new FakeModelWeightGenerator(batchRepository, clock), new ModelWeightPromotionService(batchRepository, intradayRepository, integrity, clock));
    }

    private sealed record TestServices(PlatformState State, IFakeModelWeightGenerator Generator, IModelWeightPromotionService Promotion);
}
