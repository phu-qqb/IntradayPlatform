using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class FxResidualDivergenceBboSamplingResearchTests
{
    private static readonly DateTimeOffset Start = new(2026, 01, 05, 12, 00, 00, TimeSpan.Zero);
    private static readonly string[] Symbols = ["EURUSD", "GBPUSD", "USDJPY"];

    [Fact]
    public void Quote_after_grid_timestamp_is_not_used_for_observation_at_t()
    {
        var grid = Start.AddMinutes(1);
        var quotes = new[]
        {
            Quote("EURUSD", grid, 1.1000m),
            Quote("EURUSD", grid.AddTicks(1), 1.5000m),
            Quote("GBPUSD", grid, 1.2500m),
            Quote("USDJPY", grid, 155.00m)
        };

        var result = Sample(quotes, Start.AddMinutes(1), Start.AddMinutes(1));
        var observation = Assert.Single(result.Observations);

        Assert.Equal(grid, observation.TimestampUtc);
        Assert.Equal(grid, observation.SourceQuoteTimestampsUtc["EURUSD"]);
        Assert.Equal(1.1000m, observation.Midpoints["EURUSD"]);
    }

    [Fact]
    public void Changing_future_target_quotes_does_not_change_prior_observations()
    {
        var decisionTime = Start.AddMinutes(75);
        var baseline = Sample(ScenarioQuotes(length: 110, shockIndex: 75, shockReturn: 0.012), Start, Start.AddMinutes(109));
        var mutatedQuotes = ScenarioQuotes(length: 110, shockIndex: 75, shockReturn: 0.012)
            .Select(x => x.Symbol == "EURUSD" && x.TimestampUtc > decisionTime
                ? Quote(x.Symbol, x.TimestampUtc, ((x.Bid + x.Ask) / 2m) * 1.30m, x.SequenceId, x.AvailableAtUtc)
                : x)
            .ToArray();

        var afterMutation = Sample(mutatedQuotes, Start, Start.AddMinutes(109));

        AssertObservationsEqual(
            baseline.Observations.Where(x => x.TimestampUtc <= decisionTime).ToArray(),
            afterMutation.Observations.Where(x => x.TimestampUtc <= decisionTime).ToArray());
    }

    [Fact]
    public void Changing_future_peer_quotes_does_not_change_prior_observations_or_signals()
    {
        var decisionTime = Start.AddMinutes(75);
        var quotes = ScenarioQuotes(length: 110, shockIndex: 75, shockReturn: 0.012);
        var baselineSample = Sample(quotes, Start, Start.AddMinutes(109));
        var baselineSignals = GenerateSignals(baselineSample);
        var mutatedQuotes = quotes
            .Select(x => x.Symbol == "GBPUSD" && x.TimestampUtc > decisionTime
                ? Quote(x.Symbol, x.TimestampUtc, ((x.Bid + x.Ask) / 2m) * 0.70m, x.SequenceId, x.AvailableAtUtc)
                : x)
            .ToArray();

        var afterSample = Sample(mutatedQuotes, Start, Start.AddMinutes(109));
        var afterSignals = GenerateSignals(afterSample);

        AssertObservationsEqual(
            baselineSample.Observations.Where(x => x.TimestampUtc <= decisionTime).ToArray(),
            afterSample.Observations.Where(x => x.TimestampUtc <= decisionTime).ToArray());
        AssertSignalsEqual(
            baselineSignals.Where(x => x.TimestampUtc <= decisionTime).ToArray(),
            afterSignals.Where(x => x.TimestampUtc <= decisionTime).ToArray());
    }

    [Fact]
    public void Availability_timestamp_after_grid_timestamp_is_rejected()
    {
        var grid = Start.AddMinutes(1);
        var quotes = new[]
        {
            Quote("EURUSD", grid, 1.1000m, availableAtUtc: grid.AddTicks(1)),
            Quote("GBPUSD", grid, 1.2500m),
            Quote("USDJPY", grid, 155.00m)
        };

        var result = Sample(quotes, grid, grid);

        Assert.Empty(result.Observations);
        Assert.Contains(result.Diagnostics, x =>
            x.Symbol == "EURUSD" &&
            x.Reason == FxBboSamplingRejectReasonResearch.MissingQuoteAtOrBeforeGridTime);
    }

    [Fact]
    public void Max_quote_age_prevents_stale_forward_fill()
    {
        var grid = Start.AddMinutes(2);
        var stale = grid.AddMinutes(-2).AddTicks(-1);
        var quotes = new[]
        {
            Quote("EURUSD", stale, 1.1000m),
            Quote("GBPUSD", grid, 1.2500m),
            Quote("USDJPY", grid, 155.00m)
        };

        var result = Sample(quotes, grid, grid, maxQuoteAge: TimeSpan.FromMinutes(2));

        Assert.Empty(result.Observations);
        Assert.Contains(result.Diagnostics, x =>
            x.Symbol == "EURUSD" &&
            x.Reason == FxBboSamplingRejectReasonResearch.QuoteTooStale);
    }

    [Fact]
    public void Missing_peer_quote_causes_synchronized_row_rejection()
    {
        var grid = Start.AddMinutes(1);
        var quotes = new[]
        {
            Quote("EURUSD", grid, 1.1000m),
            Quote("GBPUSD", grid, 1.2500m)
        };

        var result = Sample(quotes, grid, grid);

        Assert.Empty(result.Observations);
        Assert.Contains(result.Diagnostics, x =>
            x.Symbol == "USDJPY" &&
            x.Reason == FxBboSamplingRejectReasonResearch.MissingSymbol);
        Assert.Contains(result.Diagnostics, x =>
            x.Symbol == "USDJPY" &&
            x.Reason == FxBboSamplingRejectReasonResearch.MissingRequiredPeer);
    }

    [Fact]
    public void Asynchronous_quotes_are_aligned_to_grid_timestamp()
    {
        var grid = Start.AddMinutes(1);
        var quotes = new[]
        {
            Quote("EURUSD", grid.AddSeconds(-12), 1.1000m),
            Quote("GBPUSD", grid.AddSeconds(-20), 1.2500m),
            Quote("USDJPY", grid.AddSeconds(-5), 155.00m)
        };

        var observation = Assert.Single(Sample(quotes, grid, grid).Observations);

        Assert.Equal(grid, observation.TimestampUtc);
        Assert.All(observation.SourceQuoteTimestampsUtc.Values, sourceTimestamp => Assert.True(sourceTimestamp < grid));
        Assert.All(observation.QuoteAges.Values, age => Assert.True(age > TimeSpan.Zero));
    }

    [Fact]
    public void Source_quote_timestamps_are_never_after_grid_timestamp()
    {
        var result = Sample(ScenarioQuotes(length: 20, shockIndex: null), Start, Start.AddMinutes(19));

        Assert.All(result.Observations, observation =>
            Assert.All(observation.SourceQuoteTimestampsUtc.Values, sourceTimestamp =>
                Assert.True(sourceTimestamp <= observation.TimestampUtc)));
    }

    [Fact]
    public void Non_positive_bid_is_rejected()
    {
        var result = Sample(
            [new("EURUSD", Start, 0m, 1.1m), Quote("GBPUSD", Start, 1.25m), Quote("USDJPY", Start, 155m)],
            Start,
            Start);

        Assert.Empty(result.Observations);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboSamplingRejectReasonResearch.NonPositiveBid);
    }

    [Fact]
    public void Non_positive_ask_is_rejected()
    {
        var result = Sample(
            [new("EURUSD", Start, 1.1m, 0m), Quote("GBPUSD", Start, 1.25m), Quote("USDJPY", Start, 155m)],
            Start,
            Start);

        Assert.Empty(result.Observations);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboSamplingRejectReasonResearch.NonPositiveAsk);
    }

    [Fact]
    public void Ask_below_bid_is_rejected_as_crossed_quote()
    {
        var result = Sample(
            [new("EURUSD", Start, 1.2m, 1.1m), Quote("GBPUSD", Start, 1.25m), Quote("USDJPY", Start, 155m)],
            Start,
            Start);

        Assert.Empty(result.Observations);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboSamplingRejectReasonResearch.CrossedQuote);
    }

    [Fact]
    public void Spread_too_wide_is_rejected_when_max_spread_is_configured()
    {
        var result = Sample(
            [new("EURUSD", Start, 1.0000m, 1.0020m), Quote("GBPUSD", Start, 1.25m), Quote("USDJPY", Start, 155m)],
            Start,
            Start,
            maxSpreadBps: 5m);

        Assert.Empty(result.Observations);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboSamplingRejectReasonResearch.SpreadTooWide);
    }

    [Fact]
    public void Ambiguous_duplicate_quote_timestamp_fails_closed_without_sequence()
    {
        var result = Sample(
            [
                Quote("EURUSD", Start, 1.1000m),
                Quote("EURUSD", Start, 1.2000m),
                Quote("GBPUSD", Start, 1.2500m),
                Quote("USDJPY", Start, 155.00m)
            ],
            Start,
            Start);

        Assert.Empty(result.Observations);
        Assert.Contains(result.Diagnostics, x =>
            x.Symbol == "EURUSD" &&
            x.Reason == FxBboSamplingRejectReasonResearch.AmbiguousDuplicateQuoteTimestamp);
    }

    [Fact]
    public void Duplicate_quote_timestamp_with_sequence_is_deterministic()
    {
        var result = Sample(
            [
                Quote("EURUSD", Start, 1.1000m, sequenceId: 1),
                Quote("EURUSD", Start, 1.2000m, sequenceId: 2),
                Quote("GBPUSD", Start, 1.2500m),
                Quote("USDJPY", Start, 155.00m)
            ],
            Start,
            Start);

        var observation = Assert.Single(result.Observations);
        Assert.Equal(1.2000m, observation.Midpoints["EURUSD"]);
    }

    [Fact]
    public void Empty_quote_input_returns_no_synchronized_observations_and_clear_diagnostics()
    {
        var result = Sample([], Start, Start);

        Assert.Empty(result.Observations);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboSamplingRejectReasonResearch.MissingSymbol);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboSamplingRejectReasonResearch.MissingRequiredPeer);
    }

    [Fact]
    public void Unknown_timestamp_semantics_fail_closed()
    {
        var result = Sample(
            [
                Quote("EURUSD", Start, 1.1000m) with { IsTimestampUtcKnown = false },
                Quote("GBPUSD", Start, 1.2500m),
                Quote("USDJPY", Start, 155.00m)
            ],
            Start,
            Start);

        Assert.Empty(result.Observations);
        Assert.Contains(result.Diagnostics, x => x.Reason == FxBboSamplingRejectReasonResearch.UnknownTimestampSemantics);
    }

    [Fact]
    public void Sampler_output_can_feed_residual_divergence_research()
    {
        var sample = Sample(ScenarioQuotes(length: 105, shockIndex: 75, shockReturn: 0.012), Start, Start.AddMinutes(104));
        var bars = new FxBboToSynchronizedMidpointSamplerResearch().ToResidualDivergenceBars(sample.Observations);

        var signals = new FxResidualDivergenceResearchStrategy().GenerateSignals(bars, StrategyParameters());

        Assert.Contains(signals, x => x.TimestampUtc == Start.AddMinutes(75) && x.IsAccepted);
        Assert.All(signals.Where(x => x.IsAccepted), x =>
            Assert.Equal(FxResidualDivergenceEligibleTiming.NextBarOnly, x.EligibleExecutionTiming));
    }

    [Fact]
    public void Diagnostic_evaluator_uses_next_grid_step_not_same_grid()
    {
        var sample = Sample(ScenarioQuotes(length: 110, shockIndex: 75, shockReturn: 0.012), Start, Start.AddMinutes(109));
        var bars = new FxBboToSynchronizedMidpointSamplerResearch().ToResidualDivergenceBars(sample.Observations);
        var parameters = StrategyParameters(evaluationHorizon: 5);
        var signals = new FxResidualDivergenceResearchStrategy().GenerateSignals(bars, parameters);

        var summary = new FxResidualDivergenceDiagnosticEvaluator().Evaluate(signals, bars, parameters);
        var line = Assert.Single(summary.Lines);

        Assert.True(line.EvaluationStartUtc > line.Signal.TimestampUtc);
        Assert.Equal(line.Signal.TimestampUtc.AddMinutes(1), line.EvaluationStartUtc);
    }

    [Fact]
    public void Regression_beta_at_t_excludes_current_target_return_when_fed_by_sampler()
    {
        var decisionTime = Start.AddMinutes(75);
        var quotes = ScenarioQuotes(length: 105, shockIndex: 75, shockReturn: 0.012);
        var baseline = GenerateSignals(Sample(quotes, Start, Start.AddMinutes(104))).Single(x => x.TimestampUtc == decisionTime);
        var mutated = quotes
            .Select(x => x.Symbol == "EURUSD" && x.TimestampUtc == decisionTime
                ? Quote(x.Symbol, x.TimestampUtc, ((x.Bid + x.Ask) / 2m) * 1.08m, x.SequenceId, x.AvailableAtUtc)
                : x)
            .ToArray();

        var afterMutation = GenerateSignals(Sample(mutated, Start, Start.AddMinutes(104))).Single(x => x.TimestampUtc == decisionTime);

        Assert.Equal(baseline.RegressionWindowEndUtc, afterMutation.RegressionWindowEndUtc);
        Assert.True(baseline.RegressionWindowEndUtc < baseline.TimestampUtc);
        Assert.Equal(baseline.BetaCoefficients["GBPUSD"], afterMutation.BetaCoefficients["GBPUSD"], 10);
        Assert.Equal(baseline.BetaCoefficients["USDJPY"], afterMutation.BetaCoefficients["USDJPY"], 10);
        Assert.NotEqual(baseline.Residual, afterMutation.Residual);
    }

    [Fact]
    public void Residual_zscore_at_t_excludes_current_residual_when_fed_by_sampler()
    {
        var decisionTime = Start.AddMinutes(75);
        var signals = GenerateSignals(Sample(ScenarioQuotes(length: 105, shockIndex: 75, shockReturn: 0.012), Start, Start.AddMinutes(104)));
        var decision = signals.Single(x => x.TimestampUtc == decisionTime);
        var priorResiduals = signals
            .Where(x => x.TimestampUtc < decision.TimestampUtc && x.RegressionObservationCount > 0)
            .Select(x => x.Residual)
            .TakeLast(StrategyParameters().ResidualZLookbackBars)
            .ToArray();

        Assert.Equal(priorResiduals.Average(), decision.ResidualMean, 10);
        Assert.Equal(SampleStandardDeviation(priorResiduals), decision.ResidualSigma, 10);
    }

    [Fact]
    public void Future_quote_mutation_does_not_alter_earlier_betas_zscores_signals_or_reason_codes()
    {
        var decisionTime = Start.AddMinutes(75);
        var quotes = ScenarioQuotes(length: 115, shockIndex: 75, shockReturn: 0.012);
        var baseline = GenerateSignals(Sample(quotes, Start, Start.AddMinutes(114)));
        var mutated = quotes
            .Select(x => x.TimestampUtc > decisionTime
                ? Quote(x.Symbol, x.TimestampUtc, ((x.Bid + x.Ask) / 2m) * 1.40m, x.SequenceId, x.AvailableAtUtc)
                : x)
            .ToArray();

        var afterMutation = GenerateSignals(Sample(mutated, Start, Start.AddMinutes(114)));

        AssertSignalsEqual(
            baseline.Where(x => x.TimestampUtc <= decisionTime).ToArray(),
            afterMutation.Where(x => x.TimestampUtc <= decisionTime).ToArray());
    }

    [Fact]
    public void Full_sample_contamination_does_not_change_prior_observations_or_signals()
    {
        var decisionTime = Start.AddMinutes(75);
        var baselineQuotes = ScenarioQuotes(length: 110, shockIndex: 75, shockReturn: 0.012);
        var futureRegime = ScenarioQuotes(length: 40, shockIndex: null, start: Start.AddMinutes(110), priceMultiplier: 3.0m);
        var extendedQuotes = baselineQuotes.Concat(futureRegime).ToArray();

        var baselineSample = Sample(baselineQuotes, Start, Start.AddMinutes(109));
        var extendedSample = Sample(extendedQuotes, Start, Start.AddMinutes(149));

        AssertObservationsEqual(
            baselineSample.Observations.Where(x => x.TimestampUtc <= decisionTime).ToArray(),
            extendedSample.Observations.Where(x => x.TimestampUtc <= decisionTime).ToArray());
        AssertSignalsEqual(
            GenerateSignals(baselineSample).Where(x => x.TimestampUtc <= decisionTime).ToArray(),
            GenerateSignals(extendedSample).Where(x => x.TimestampUtc <= decisionTime).ToArray());
    }

    [Fact]
    public void Sampler_code_does_not_bind_execution_sizing_or_market_snapshot_contracts()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxResidualDivergenceBboSamplingResearch.cs"));

        Assert.DoesNotContain("MarketDataSnapshot", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetNotional", source, StringComparison.Ordinal);
        Assert.DoesNotContain("QuantityPolicy", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetWeight", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Pms", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CoreExecution", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CoreNetting", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lmax", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sampler_is_not_referenced_from_production_execution_or_sizing_paths()
    {
        var root = FindRepoRoot();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxResidualDivergenceBboSamplingResearch.cs")),
            Path.GetFullPath(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxBboOfflineResearchQuoteLoader.cs")),
            Path.GetFullPath(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxBboPolygonResearchManifestOnboarding.cs")),
            Path.GetFullPath(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxBboPolygonResearchManifestGenerator.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceBboSamplingResearchTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxBboOfflineResearchQuoteLoaderTests.cs")),
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
            .Where(path => File.ReadAllText(path).Contains("FxBboToSynchronizedMidpointSamplerResearch", StringComparison.Ordinal))
            .Select(Path.GetFullPath)
            .Where(path => !allowed.Contains(path))
            .ToArray();

        Assert.Empty(references);
    }

    private static FxBboSamplingResultResearch Sample(
        IReadOnlyList<FxBboQuoteResearch> quotes,
        DateTimeOffset start,
        DateTimeOffset end,
        TimeSpan? maxQuoteAge = null,
        decimal? maxSpreadBps = null)
        => new FxBboToSynchronizedMidpointSamplerResearch().Sample(
            quotes,
            new FxBboSamplingParametersResearch(
                Symbols,
                start,
                end,
                TimeSpan.FromMinutes(1),
                maxQuoteAge ?? TimeSpan.FromMinutes(1),
                MaxSpreadBps: maxSpreadBps));

    private static IReadOnlyList<FxResidualDivergenceResearchSignal> GenerateSignals(FxBboSamplingResultResearch sample)
    {
        var bars = new FxBboToSynchronizedMidpointSamplerResearch().ToResidualDivergenceBars(sample.Observations);
        return new FxResidualDivergenceResearchStrategy().GenerateSignals(bars, StrategyParameters());
    }

    private static FxResidualDivergenceParameters StrategyParameters(int evaluationHorizon = 5)
        => new(
            TargetSymbol: "EURUSD",
            PeerSymbols: ["GBPUSD", "USDJPY"],
            RegressionLookbackBars: 30,
            ResidualZLookbackBars: 20,
            MinRegressionObservations: 20,
            EntryZScore: 3.0,
            MaxAbsBeta: 5.0,
            MinPeerCount: 2,
            EvaluationHorizonBars: evaluationHorizon);

    private static FxBboQuoteResearch Quote(
        string symbol,
        DateTimeOffset timestamp,
        decimal midpoint,
        long? sequenceId = null,
        DateTimeOffset? availableAtUtc = null,
        decimal spreadBps = 0.5m)
    {
        var halfSpread = midpoint * spreadBps / 10000m / 2m;
        return new(
            symbol,
            timestamp,
            midpoint - halfSpread,
            midpoint + halfSpread,
            SequenceId: sequenceId,
            AvailableAtUtc: availableAtUtc ?? timestamp,
            Source: "SyntheticResearch",
            Venue: "SyntheticBbo");
    }

    private static FxBboQuoteResearch[] ScenarioQuotes(
        int length,
        int? shockIndex,
        double shockReturn = 0.0,
        DateTimeOffset? start = null,
        decimal priceMultiplier = 1.0m)
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

        return FromReturns("EURUSD", target, 1.1000m * priceMultiplier, scenarioStart)
            .Concat(FromReturns("GBPUSD", peer1, 1.2500m * priceMultiplier, scenarioStart))
            .Concat(FromReturns("USDJPY", peer2, 155.00m * priceMultiplier, scenarioStart))
            .ToArray();
    }

    private static IEnumerable<FxBboQuoteResearch> FromReturns(
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

            yield return Quote(symbol, scenarioStart.AddMinutes(index), (decimal)price, sequenceId: index + 1);
        }
    }

    private static void AssertObservationsEqual(
        IReadOnlyList<FxSynchronizedMidpointObservationResearch> expected,
        IReadOnlyList<FxSynchronizedMidpointObservationResearch> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index].TimestampUtc, actual[index].TimestampUtc);
            foreach (var symbol in Symbols)
            {
                Assert.Equal(expected[index].Midpoints[symbol], actual[index].Midpoints[symbol]);
                Assert.Equal(expected[index].SourceQuoteTimestampsUtc[symbol], actual[index].SourceQuoteTimestampsUtc[symbol]);
                Assert.Equal(expected[index].QuoteAges[symbol], actual[index].QuoteAges[symbol]);
                Assert.Equal(expected[index].SpreadBps[symbol], actual[index].SpreadBps[symbol]);
            }
        }
    }

    private static void AssertSignalsEqual(
        IReadOnlyList<FxResidualDivergenceResearchSignal> expected,
        IReadOnlyList<FxResidualDivergenceResearchSignal> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index].TimestampUtc, actual[index].TimestampUtc);
            Assert.Equal(expected[index].ReasonCode, actual[index].ReasonCode);
            Assert.Equal(expected[index].Direction, actual[index].Direction);
            Assert.Equal(expected[index].Residual, actual[index].Residual, 10);
            Assert.Equal(expected[index].ResidualZScore, actual[index].ResidualZScore, 10);
            Assert.Equal(expected[index].PredictedReturn, actual[index].PredictedReturn, 10);
            Assert.Equal(expected[index].ActualReturn, actual[index].ActualReturn, 10);
            Assert.Equal(expected[index].RegressionWindowEndUtc, actual[index].RegressionWindowEndUtc);
            foreach (var beta in expected[index].BetaCoefficients)
            {
                Assert.Equal(beta.Value, actual[index].BetaCoefficients[beta.Key], 10);
            }
        }
    }

    private static double SampleStandardDeviation(IReadOnlyList<double> values)
    {
        var mean = values.Average();
        var variance = values.Sum(x => Math.Pow(x - mean, 2)) / (values.Count - 1);
        return Math.Sqrt(variance);
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
