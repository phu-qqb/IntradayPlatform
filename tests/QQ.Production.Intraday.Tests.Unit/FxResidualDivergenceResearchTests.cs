using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class FxResidualDivergenceResearchTests
{
    private static readonly DateTimeOffset Start = new(2026, 01, 05, 12, 00, 00, TimeSpan.Zero);

    [Fact]
    public void No_signal_before_regression_warmup()
    {
        var bars = Scenario(length: 25, shockIndex: null);
        var parameters = Parameters(regressionLookback: 20, minRegressionObservations: 20, residualLookback: 5);

        var signals = Generate(bars, parameters);

        Assert.DoesNotContain(signals, x => x.IsAccepted);
        Assert.Contains(signals, x => x.ReasonCode == FxResidualDivergenceReasonCode.InsufficientHistory);
    }

    [Fact]
    public void No_signal_before_residual_zscore_warmup()
    {
        var bars = Scenario(length: 55, shockIndex: 40, shockReturn: 0.01);
        var parameters = Parameters(regressionLookback: 20, minRegressionObservations: 15, residualLookback: 35);

        var signals = Generate(bars, parameters);

        Assert.DoesNotContain(signals, x => x.IsAccepted);
        Assert.Contains(signals, x => x.ReasonCode == FxResidualDivergenceReasonCode.InsufficientResidualHistory);
    }

    [Fact]
    public void Positive_residual_zscore_above_threshold_emits_short_residual_reversion()
    {
        var shockIndex = 75;
        var bars = Scenario(length: 105, shockIndex: shockIndex, shockReturn: 0.012);
        var parameters = Parameters();

        var signal = Generate(bars, parameters).Single(x => x.TimestampUtc == Start.AddMinutes(shockIndex));

        Assert.True(signal.IsAccepted);
        Assert.Equal(FxResidualDivergenceReasonCode.AcceptedShortResidualReversion, signal.ReasonCode);
        Assert.Equal(FxResidualDivergenceDirection.ShortResidualReversion, signal.Direction);
        Assert.True(signal.ResidualZScore > parameters.EntryZScore);
        Assert.True(signal.DiagnosticOnly);
        Assert.Equal(FxResidualDivergenceEligibleTiming.NextBarOnly, signal.EligibleExecutionTiming);
    }

    [Fact]
    public void Negative_residual_zscore_below_threshold_emits_long_residual_reversion()
    {
        var shockIndex = 75;
        var bars = Scenario(length: 105, shockIndex: shockIndex, shockReturn: -0.012);
        var parameters = Parameters();

        var signal = Generate(bars, parameters).Single(x => x.TimestampUtc == Start.AddMinutes(shockIndex));

        Assert.True(signal.IsAccepted);
        Assert.Equal(FxResidualDivergenceReasonCode.AcceptedLongResidualReversion, signal.ReasonCode);
        Assert.Equal(FxResidualDivergenceDirection.LongResidualReversion, signal.Direction);
        Assert.True(signal.ResidualZScore < -parameters.EntryZScore);
    }

    [Fact]
    public void Residual_zscore_below_threshold_is_rejected_as_too_small()
    {
        var bars = Scenario(length: 95, shockIndex: null);
        var parameters = Parameters(entryZScore: 5.0);

        var signals = Generate(bars, parameters);

        Assert.Contains(signals, x => x.ReasonCode == FxResidualDivergenceReasonCode.ResidualZScoreTooSmall);
        Assert.DoesNotContain(signals, x => x.IsAccepted);
    }

    [Fact]
    public void Missing_peer_bar_fails_closed()
    {
        var missingTimestamp = Start.AddMinutes(70);
        var bars = Scenario(length: 100, shockIndex: null)
            .Where(x => !(x.Symbol == "GBPUSD" && x.TimestampUtc == missingTimestamp))
            .ToArray();

        var signal = Generate(bars, Parameters()).Single(x => x.TimestampUtc == missingTimestamp);

        Assert.False(signal.IsAccepted);
        Assert.Equal(FxResidualDivergenceReasonCode.MissingPeerBar, signal.ReasonCode);
    }

    [Fact]
    public void Non_positive_prices_fail_closed()
    {
        var bars = Scenario(length: 90, shockIndex: null)
            .Select(x => x.Symbol == "EURUSD" && x.TimestampUtc == Start.AddMinutes(40)
                ? x with { CloseOrMid = 0m }
                : x)
            .ToArray();

        var signals = Generate(bars, Parameters());

        Assert.NotEmpty(signals);
        Assert.All(signals, x => Assert.Equal(FxResidualDivergenceReasonCode.NonPositivePrice, x.ReasonCode));
        Assert.DoesNotContain(signals, x => x.IsAccepted);
    }

    [Fact]
    public void Unsorted_input_is_sorted_deterministically()
    {
        var bars = Scenario(length: 105, shockIndex: 75, shockReturn: 0.012);
        var sortedSignal = Generate(bars, Parameters()).Single(x => x.IsAccepted);
        var unsorted = bars.Reverse().ToArray();

        var unsortedSignal = Generate(unsorted, Parameters()).Single(x => x.IsAccepted);

        Assert.Equal(sortedSignal.TimestampUtc, unsortedSignal.TimestampUtc);
        Assert.Equal(sortedSignal.ReasonCode, unsortedSignal.ReasonCode);
        Assert.Equal(sortedSignal.ResidualZScore, unsortedSignal.ResidualZScore, 10);
    }

    [Fact]
    public void Duplicate_timestamps_are_rejected_explicitly()
    {
        var bars = Scenario(length: 90, shockIndex: null).ToList();
        bars.Add(bars.First(x => x.Symbol == "EURUSD" && x.TimestampUtc == Start.AddMinutes(30)));

        var signals = Generate(bars, Parameters());

        Assert.NotEmpty(signals);
        Assert.All(signals, x => Assert.Equal(FxResidualDivergenceReasonCode.TimestampAlignmentFailed, x.ReasonCode));
    }

    [Fact]
    public void Singular_regression_fails_closed()
    {
        var bars = SingularScenario(length: 90);

        var signals = Generate(bars, Parameters());

        Assert.Contains(signals, x => x.ReasonCode == FxResidualDivergenceReasonCode.RegressionFitFailed);
        Assert.DoesNotContain(signals, x => x.IsAccepted);
    }

    [Fact]
    public void Excessive_beta_magnitude_fails_closed()
    {
        var bars = HighBetaScenario(length: 90);
        var parameters = Parameters(maxAbsBeta: 1.5);

        var signals = Generate(bars, parameters);

        Assert.Contains(signals, x => x.ReasonCode == FxResidualDivergenceReasonCode.BetaMagnitudeTooLarge);
        Assert.DoesNotContain(signals, x => x.IsAccepted);
    }

    [Fact]
    public void Exact_timestamp_alignment_is_enforced_by_default()
    {
        var missingTimestamp = Start.AddMinutes(60);
        var bars = Scenario(length: 90, shockIndex: null)
            .Where(x => !(x.Symbol == "USDJPY" && x.TimestampUtc == missingTimestamp))
            .ToArray();

        var signal = Generate(bars, Parameters()).Single(x => x.TimestampUtc == missingTimestamp);

        Assert.Equal(FxResidualDivergenceReasonCode.MissingPeerBar, signal.ReasonCode);
    }

    [Fact]
    public void Empty_input_returns_no_accepted_signals()
    {
        var signals = Generate([], Parameters());

        Assert.Empty(signals);
    }

    [Fact]
    public void Changing_future_target_bars_does_not_change_earlier_decision_fields()
    {
        var decisionIndex = 75;
        var bars = Scenario(length: 110, shockIndex: decisionIndex, shockReturn: 0.012);
        var baseline = Generate(bars, Parameters()).Single(x => x.TimestampUtc == Start.AddMinutes(decisionIndex));
        var mutated = bars
            .Select(x => x.Symbol == "EURUSD" && x.TimestampUtc > baseline.TimestampUtc
                ? x with { CloseOrMid = x.CloseOrMid * 1.25m }
                : x)
            .ToArray();

        var afterMutation = Generate(mutated, Parameters()).Single(x => x.TimestampUtc == baseline.TimestampUtc);

        AssertDecisionEqual(baseline, afterMutation);
    }

    [Fact]
    public void Changing_future_peer_bars_does_not_change_earlier_decision_fields()
    {
        var decisionIndex = 75;
        var bars = Scenario(length: 110, shockIndex: decisionIndex, shockReturn: 0.012);
        var baseline = Generate(bars, Parameters()).Single(x => x.TimestampUtc == Start.AddMinutes(decisionIndex));
        var mutated = bars
            .Select(x => x.Symbol == "GBPUSD" && x.TimestampUtc > baseline.TimestampUtc
                ? x with { CloseOrMid = x.CloseOrMid * 0.75m }
                : x)
            .ToArray();

        var afterMutation = Generate(mutated, Parameters()).Single(x => x.TimestampUtc == baseline.TimestampUtc);

        AssertDecisionEqual(baseline, afterMutation);
    }

    [Fact]
    public void Regression_beta_at_t_excludes_target_return_at_t()
    {
        var decisionIndex = 75;
        var bars = Scenario(length: 105, shockIndex: decisionIndex, shockReturn: 0.012);
        var baseline = Generate(bars, Parameters()).Single(x => x.TimestampUtc == Start.AddMinutes(decisionIndex));
        var mutated = bars
            .Select(x => x.Symbol == "EURUSD" && x.TimestampUtc == baseline.TimestampUtc
                ? x with { CloseOrMid = x.CloseOrMid * 1.08m }
                : x)
            .ToArray();

        var afterMutation = Generate(mutated, Parameters()).Single(x => x.TimestampUtc == baseline.TimestampUtc);

        Assert.Equal(baseline.RegressionWindowEndUtc, afterMutation.RegressionWindowEndUtc);
        Assert.True(baseline.RegressionWindowEndUtc < baseline.TimestampUtc);
        Assert.Equal(baseline.BetaCoefficients["GBPUSD"], afterMutation.BetaCoefficients["GBPUSD"], 10);
        Assert.Equal(baseline.BetaCoefficients["USDJPY"], afterMutation.BetaCoefficients["USDJPY"], 10);
        Assert.NotEqual(baseline.Residual, afterMutation.Residual);
    }

    [Fact]
    public void Residual_zscore_at_t_excludes_current_residual_from_mean_and_sigma()
    {
        var decisionIndex = 75;
        var parameters = Parameters(residualLookback: 20);
        var signals = Generate(Scenario(length: 105, shockIndex: decisionIndex, shockReturn: 0.012), parameters);
        var decision = signals.Single(x => x.TimestampUtc == Start.AddMinutes(decisionIndex));
        var priorResiduals = signals
            .Where(x => x.TimestampUtc < decision.TimestampUtc && x.RegressionObservationCount > 0)
            .Select(x => x.Residual)
            .TakeLast(parameters.ResidualZLookbackBars)
            .ToArray();
        var priorMean = priorResiduals.Average();
        var priorSigma = SampleStandardDeviation(priorResiduals);

        Assert.Equal(parameters.ResidualZLookbackBars, priorResiduals.Length);
        Assert.Equal(priorMean, decision.ResidualMean, 10);
        Assert.Equal(priorSigma, decision.ResidualSigma, 10);
    }

    [Fact]
    public void Signal_at_t_is_marked_next_bar_only()
    {
        var signal = Generate(Scenario(length: 105, shockIndex: 75, shockReturn: 0.012), Parameters()).Single(x => x.IsAccepted);

        Assert.Equal(FxResidualDivergenceEligibleTiming.NextBarOnly, signal.EligibleExecutionTiming);
        Assert.True(signal.DiagnosticOnly);
    }

    [Fact]
    public void Diagnostic_evaluator_starts_after_signal_bar()
    {
        var bars = Scenario(length: 110, shockIndex: 75, shockReturn: 0.012);
        var parameters = Parameters(evaluationHorizon: 5);
        var signals = Generate(bars, parameters);

        var summary = new FxResidualDivergenceDiagnosticEvaluator().Evaluate(signals, bars, parameters);
        var line = Assert.Single(summary.Lines);

        Assert.True(line.EvaluationStartUtc > line.Signal.TimestampUtc);
        Assert.Equal(line.Signal.TimestampUtc.AddMinutes(1), line.EvaluationStartUtc);
        Assert.Equal(line.Signal.TimestampUtc.AddMinutes(5), line.EvaluationEndUtc);
        Assert.Equal(1, summary.AcceptedSignalsCount);
        Assert.Equal(1, summary.ShortCount);
    }

    [Fact]
    public void Implementation_is_not_referenced_from_execution_or_sizing_paths()
    {
        var root = FindRepoRoot();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxResidualDivergenceResearch.cs")),
            Path.GetFullPath(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxResidualDivergenceBboSamplingResearch.cs")),
            Path.GetFullPath(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxBboOfflineResearchQuoteLoader.cs")),
            Path.GetFullPath(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxBboPolygonResearchManifestOnboarding.cs")),
            Path.GetFullPath(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxBboPolygonResearchManifestGenerator.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceResearchTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceBboSamplingResearchTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxBboOfflineResearchQuoteLoaderTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxBboPolygonResearchManifestOnboardingTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxBboPolygonResearchManifestGeneratorTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "PolygonFxTickBackfillResearchTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceLocalEvaluationR006Tests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceLocalSmokeEvalR011Tests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceCoverageSmokeEvalR014Tests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergencePreregisteredEvalR015Tests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergencePreregisteredEvalR015RunTests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceExtendedPreregisteredR016Tests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceExtendedR016ApprovalEvalR017Tests.cs")),
            Path.GetFullPath(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceZScoreRobustnessAuditR018Tests.cs"))
        };

        var references = Directory
            .GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("FxResidualDivergence", StringComparison.Ordinal))
            .Select(Path.GetFullPath)
            .Where(path => !allowed.Contains(path))
            .ToArray();

        Assert.Empty(references);
    }

    [Fact]
    public void Implementation_does_not_bind_market_snapshot_notional_quantity_or_production_outputs()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Application", "FxResidualDivergenceResearch.cs"));

        Assert.DoesNotContain("MarketDataSnapshot", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetNotional", source, StringComparison.Ordinal);
        Assert.DoesNotContain("QuantityPolicy", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetWeight", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Pms", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CoreExecution", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CoreNetting", source, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<FxResidualDivergenceResearchSignal> Generate(
        IReadOnlyList<FxResidualDivergenceBar> bars,
        FxResidualDivergenceParameters parameters)
        => new FxResidualDivergenceResearchStrategy().GenerateSignals(bars, parameters);

    private static FxResidualDivergenceParameters Parameters(
        int regressionLookback = 30,
        int minRegressionObservations = 20,
        int residualLookback = 20,
        double entryZScore = 3.0,
        double maxAbsBeta = 5.0,
        int evaluationHorizon = 5)
        => new(
            TargetSymbol: "EURUSD",
            PeerSymbols: ["GBPUSD", "USDJPY"],
            RegressionLookbackBars: regressionLookback,
            ResidualZLookbackBars: residualLookback,
            MinRegressionObservations: minRegressionObservations,
            EntryZScore: entryZScore,
            MaxAbsBeta: maxAbsBeta,
            MinPeerCount: 2,
            EvaluationHorizonBars: evaluationHorizon);

    private static FxResidualDivergenceBar[] Scenario(int length, int? shockIndex, double shockReturn = 0.0)
    {
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

        return FromReturns("EURUSD", target, 1.1000m)
            .Concat(FromReturns("GBPUSD", peer1, 1.2500m))
            .Concat(FromReturns("USDJPY", peer2, 155.00m))
            .ToArray();
    }

    private static FxResidualDivergenceBar[] SingularScenario(int length)
    {
        var peer = new double[length];
        var target = new double[length];
        for (var index = 1; index < length; index++)
        {
            peer[index] = 0.0002 * Math.Sin(index * 0.17);
            target[index] = 0.5 * peer[index] + 0.00001 * Math.Cos(index * 0.11);
        }

        return FromReturns("EURUSD", target, 1.1000m)
            .Concat(FromReturns("GBPUSD", peer, 1.2500m))
            .Concat(FromReturns("USDJPY", peer, 155.00m))
            .ToArray();
    }

    private static FxResidualDivergenceBar[] HighBetaScenario(int length)
    {
        var peer1 = new double[length];
        var peer2 = new double[length];
        var target = new double[length];
        for (var index = 1; index < length; index++)
        {
            peer1[index] = 0.00020 * Math.Sin(index * 0.19) + 0.00004 * ((index % 5) - 2);
            peer2[index] = 0.00015 * Math.Cos(index * 0.11) + 0.00003 * ((index % 7) - 3);
            target[index] = (3.0 * peer1[index]) + (0.1 * peer2[index]) + 0.00001 * Math.Sin(index * 0.31);
        }

        return FromReturns("EURUSD", target, 1.1000m)
            .Concat(FromReturns("GBPUSD", peer1, 1.2500m))
            .Concat(FromReturns("USDJPY", peer2, 155.00m))
            .ToArray();
    }

    private static IEnumerable<FxResidualDivergenceBar> FromReturns(string symbol, IReadOnlyList<double> returns, decimal startPrice)
    {
        var price = (double)startPrice;
        for (var index = 0; index < returns.Count; index++)
        {
            if (index > 0)
            {
                price *= Math.Exp(returns[index]);
            }

            yield return new FxResidualDivergenceBar(
                symbol,
                Start.AddMinutes(index),
                (decimal)price,
                IsCompletedBar: true,
                SpreadBps: 0.5m,
                ExchangeTimeZone: "UTC");
        }
    }

    private static void AssertDecisionEqual(FxResidualDivergenceResearchSignal expected, FxResidualDivergenceResearchSignal actual)
    {
        Assert.Equal(expected.TimestampUtc, actual.TimestampUtc);
        Assert.Equal(expected.ReasonCode, actual.ReasonCode);
        Assert.Equal(expected.Direction, actual.Direction);
        Assert.Equal(expected.Residual, actual.Residual, 10);
        Assert.Equal(expected.ResidualZScore, actual.ResidualZScore, 10);
        Assert.Equal(expected.PredictedReturn, actual.PredictedReturn, 10);
        Assert.Equal(expected.ActualReturn, actual.ActualReturn, 10);
        Assert.Equal(expected.RegressionWindowEndUtc, actual.RegressionWindowEndUtc);
        Assert.Equal(expected.BetaCoefficients["GBPUSD"], actual.BetaCoefficients["GBPUSD"], 10);
        Assert.Equal(expected.BetaCoefficients["USDJPY"], actual.BetaCoefficients["USDJPY"], 10);
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
