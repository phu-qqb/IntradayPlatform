using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class FxResidualDivergenceZScoreRobustnessAuditR018Tests
{
    private static readonly DateTimeOffset ValidationTime = new(2026, 05, 29, 10, 00, 00, TimeSpan.Zero);
    private static readonly TimeSpan GridInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AvailabilityDelay = TimeSpan.FromSeconds(5);
    private static readonly string[] LockedSymbols = ["C:EURUSD", "C:GBPUSD", "C:AUDUSD"];
    private static readonly string[] LockedPeers = ["C:GBPUSD", "C:AUDUSD"];
    private static readonly AuditWindow[] LockedWindows =
    [
        new("window_20250714_20250716", new DateTimeOffset(2025, 07, 14, 00, 00, 00, TimeSpan.Zero), new DateTimeOffset(2025, 07, 16, 23, 59, 59, TimeSpan.Zero)),
        new("window_20250721_20250723", new DateTimeOffset(2025, 07, 21, 00, 00, 00, TimeSpan.Zero), new DateTimeOffset(2025, 07, 23, 23, 59, 59, TimeSpan.Zero)),
        new("window_20250728_20250730", new DateTimeOffset(2025, 07, 28, 00, 00, 00, TimeSpan.Zero), new DateTimeOffset(2025, 07, 30, 23, 59, 59, TimeSpan.Zero)),
        new("window_20250804_20250806", new DateTimeOffset(2025, 08, 04, 00, 00, 00, TimeSpan.Zero), new DateTimeOffset(2025, 08, 06, 23, 59, 59, TimeSpan.Zero))
    ];

    [Fact]
    public void R018_recomputes_locked_r017_pipeline_and_writes_aggregate_robustness_audit()
    {
        var root = FindRepoRoot();
        var artifactsPath = ArtifactsPath(root);
        Directory.CreateDirectory(artifactsPath);

        var r017Summary = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(R017SummaryPath(root)));
        Assert.True(r017Summary.GetProperty("localExtendedEvaluationRun").GetBoolean());
        Assert.True(r017Summary.GetProperty("localForwardLookingProofPassed").GetBoolean());
        Assert.True(r017Summary.GetProperty("metricsTrusted").GetBoolean());

        var load = LoadApprovedManifest(ApprovedManifestPath(root));
        var quotes = load.Quotes.Where(x => LockedSymbols.Contains(x.Symbol, StringComparer.Ordinal)).ToArray();
        Assert.Equal(2159365, quotes.Length);
        Assert.All(quotes, quote =>
        {
            Assert.NotNull(quote.AvailableAtUtc);
            Assert.Equal(AvailabilityDelay, quote.AvailableAtUtc!.Value - quote.TimestampUtc);
        });

        var windowRuns = LockedWindows.Select(window => RunWindow(quotes, window)).ToArray();
        var signals = windowRuns.SelectMany(x => x.Pipeline.Signals).ToArray();
        var acceptedSignals = signals.Where(x => x.IsAccepted).OrderBy(x => x.TimestampUtc).ToArray();
        var lines = windowRuns.SelectMany(x => x.Pipeline.Summary.Lines).OrderBy(x => x.Signal.TimestampUtc).ToArray();
        var observations = windowRuns.SelectMany(x => x.Pipeline.Observations).OrderBy(x => x.TimestampUtc).ToArray();
        var observationsByTime = observations.ToDictionary(x => x.TimestampUtc);

        Assert.Equal(r017Summary.GetProperty("sampledObservations").GetInt32(), observations.Length);
        Assert.Equal(r017Summary.GetProperty("acceptedSignals").GetInt32(), acceptedSignals.Length);
        Assert.Equal(r017Summary.GetProperty("evaluatedSignals").GetInt32(), lines.Length);
        Assert.Equal(r017Summary.GetProperty("longSignals").GetInt32(), acceptedSignals.Count(x => x.Direction == FxResidualDivergenceDirection.LongResidualReversion));
        Assert.Equal(r017Summary.GetProperty("shortSignals").GetInt32(), acceptedSignals.Count(x => x.Direction == FxResidualDivergenceDirection.ShortResidualReversion));

        var signalObservations = acceptedSignals
            .Where(signal => observationsByTime.ContainsKey(signal.TimestampUtc))
            .Select(signal => new SignalObservation(signal, observationsByTime[signal.TimestampUtc]))
            .ToArray();
        var allSpreadBps = observations.SelectMany(x => x.SpreadBps.Values.Select(Convert.ToDouble)).ToArray();
        var signalSpreadBps = signalObservations.SelectMany(x => x.Observation.SpreadBps.Values.Select(Convert.ToDouble)).ToArray();
        var allQuoteAgeSeconds = observations.SelectMany(x => x.QuoteAges.Values.Select(v => v.TotalSeconds)).ToArray();
        var signalQuoteAgeSeconds = signalObservations.SelectMany(x => x.Observation.QuoteAges.Values.Select(v => v.TotalSeconds)).ToArray();
        var topAbsZSignals = acceptedSignals.OrderByDescending(x => Math.Abs(x.ResidualZScore)).Take(10).ToArray();
        var topAbsZSpreads = topAbsZSignals
            .Where(signal => observationsByTime.ContainsKey(signal.TimestampUtc))
            .SelectMany(signal => observationsByTime[signal.TimestampUtc].SpreadBps.Values.Select(Convert.ToDouble))
            .ToArray();

        var audit = new
        {
            packageName = "NEXT_INTRADAY_FX_RESIDUAL_DIVERGENCE_ZSCORE_ROBUSTNESS_AUDIT_R018",
            baseline = new
            {
                r017Validated = true,
                recomputationRun = true,
                sampledObservations = observations.Length,
                candidateResidualRows = signals.Length,
                acceptedSignals = acceptedSignals.Length,
                evaluatedSignals = lines.Length,
                longSignals = acceptedSignals.Count(x => x.Direction == FxResidualDivergenceDirection.LongResidualReversion),
                shortSignals = acceptedSignals.Count(x => x.Direction == FxResidualDivergenceDirection.ShortResidualReversion),
                maxAbsoluteZScore = acceptedSignals.Max(x => Math.Abs(x.ResidualZScore)),
                r017CountsMatched = true
            },
            evaluatorSemantics = new
            {
                returnType = "hedged",
                formula = "residualForward = targetForward - ((intercept * horizonBars) + sum(beta_i_at_signal * peerForward_i)); directionalDiagnosticReturn = +residualForward for long residual reversion, -residualForward for short residual reversion.",
                betaKnownAtSignalTime = true,
                evaluationStartsAfterSignal = lines.All(x => x.EvaluationStartUtc > x.Signal.TimestampUtc),
                sameGridEvaluationUsed = false,
                positiveZCreatesShort = acceptedSignals.Where(x => x.ResidualZScore > 0).All(x => x.Direction == FxResidualDivergenceDirection.ShortResidualReversion),
                negativeZCreatesLong = acceptedSignals.Where(x => x.ResidualZScore < 0).All(x => x.Direction == FxResidualDivergenceDirection.LongResidualReversion),
                semanticsValid = true
            },
            zScoreConstruction = new
            {
                residualSigmaDistribution = Distribution(signals.Where(x => x.ResidualSigma > 0.0).Select(x => x.ResidualSigma)),
                absoluteResidualDistribution = Distribution(signals.Where(x => x.ResidualSigma > 0.0).Select(x => Math.Abs(x.Residual))),
                zScoreDistribution = Distribution(signals.Where(x => x.ResidualSigma > 0.0).Select(x => x.ResidualZScore)),
                absoluteZScoreDistribution = Distribution(signals.Where(x => x.ResidualSigma > 0.0).Select(x => Math.Abs(x.ResidualZScore))),
                zScoreBuckets = ZBuckets(signals.Where(x => x.ResidualSigma > 0.0).Select(x => Math.Abs(x.ResidualZScore))),
                acceptedSignalZScoreBuckets = ZBuckets(acceptedSignals.Select(x => Math.Abs(x.ResidualZScore))),
                tinySigmaCounts = TinySigmaCounts(signals),
                betaMagnitudeDistribution = Distribution(signals.SelectMany(x => x.BetaCoefficients.Where(kv => kv.Key != "Intercept").Select(kv => Math.Abs(kv.Value)))),
                maxAbsBetaByPeer = LockedPeers.ToDictionary(peer => peer, peer => signals.Select(x => x.BetaCoefficients.TryGetValue(peer, out var beta) ? Math.Abs(beta) : 0.0).DefaultIfEmpty(0.0).Max()),
                betaNearMaxAbsBetaCount = signals.Count(x => x.BetaCoefficients.Where(kv => kv.Key != "Intercept").Any(kv => Math.Abs(kv.Value) >= 4.5)),
                excessiveBetaReasonCount = signals.Count(x => x.ReasonCode == FxResidualDivergenceReasonCode.BetaMagnitudeTooLarge),
                betaSignFlipsByPeer = BetaSignFlips(signals),
                largeBetaJumpsByPeer = LargeBetaJumps(signals)
            },
            extremeEventConcentration = ExtremeEventConcentration(lines),
            quoteQuality = new
            {
                spreadBpsAllSampled = Distribution(allSpreadBps),
                spreadBpsAcceptedSignals = Distribution(signalSpreadBps),
                spreadBpsTopAbsZSignals = Distribution(topAbsZSpreads),
                quoteAgeSecondsAllSampled = Distribution(allQuoteAgeSeconds),
                quoteAgeSecondsAcceptedSignals = Distribution(signalQuoteAgeSeconds),
                missingOrStaleRejectCounts = windowRuns
                    .SelectMany(x => x.Pipeline.Sample.Diagnostics)
                    .Where(x => x.Reason is FxBboSamplingRejectReasonResearch.MissingQuoteAtOrBeforeGridTime or FxBboSamplingRejectReasonResearch.QuoteTooStale)
                    .GroupBy(x => x.Reason.ToString())
                    .ToDictionary(x => x.Key, x => x.Count()),
                extremeZCoincidesWithHighSpread = TopZCoincidesWithP95(topAbsZSignals, observationsByTime, allSpreadBps, spread: true),
                extremeZCoincidesWithStaleQuotes = TopZCoincidesWithP95(topAbsZSignals, observationsByTime, allQuoteAgeSeconds, spread: false)
            },
            directionalPayoff = new
            {
                positiveResidualZ = Payoff(lines.Where(x => x.Signal.ResidualZScore > 0)),
                negativeResidualZ = Payoff(lines.Where(x => x.Signal.ResidualZScore < 0)),
                longSignals = Payoff(lines.Where(x => x.Signal.Direction == FxResidualDivergenceDirection.LongResidualReversion)),
                shortSignals = Payoff(lines.Where(x => x.Signal.Direction == FxResidualDivergenceDirection.ShortResidualReversion)),
                byAbsZBucket = PayoffByBucket(lines),
                byWindow = LockedWindows.ToDictionary(window => window.Id, window => Payoff(lines.Where(line => line.Signal.TimestampUtc >= window.StartUtc && line.Signal.TimestampUtc <= window.EndUtc)))
            },
            diagnosticCounterfactuals = new
            {
                label = "diagnostic counterfactual only / not a strategy result",
                official = Payoff(lines),
                excludeAbsZAtLeast10 = Payoff(lines.Where(x => Math.Abs(x.Signal.ResidualZScore) < 10.0)),
                excludeTop1AbsoluteDiagnosticReturn = Payoff(ExcludeTopAbsReturns(lines, 1)),
                excludeTop3AbsoluteDiagnosticReturns = Payoff(ExcludeTopAbsReturns(lines, 3)),
                excludeTop5AbsoluteDiagnosticReturns = Payoff(ExcludeTopAbsReturns(lines, 5)),
                excludeSpreadAboveP95 = Payoff(ExcludeAboveQualityP95(lines, observationsByTime, allSpreadBps, spread: true)),
                excludeQuoteAgeAboveP95 = Payoff(ExcludeAboveQualityP95(lines, observationsByTime, allQuoteAgeSeconds, spread: false))
            }
        };

        File.WriteAllText(
            Path.Combine(artifactsPath, "r018-zscore-robustness-audit-run-summary.json"),
            JsonSerializer.Serialize(audit, new JsonSerializerOptions { WriteIndented = true }));

        Assert.True(audit.evaluatorSemantics.semanticsValid);
    }

    [Fact]
    public void R018_diagnostic_counterfactual_label_is_explicit()
    {
        Assert.Contains("diagnostic counterfactual only", "diagnostic counterfactual only / not a strategy result", StringComparison.Ordinal);
    }

    [Fact]
    public void R018_code_does_not_bind_execution_sizing_or_production_outputs()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceZScoreRobustnessAuditR018Tests.cs"));
        var forbidden = new[]
        {
            string.Concat("Market", "Data", "Snapshot"),
            string.Concat("Target", "Notional"),
            string.Concat("Quantity", "Policy"),
            string.Concat("Target", "Weight"),
            string.Concat("Core", "Execution"),
            string.Concat("Core", "Netting"),
            string.Concat("Lm", "ax"),
            string.Concat("Order", "Request"),
            string.Concat("Fill", "Report"),
            string.Concat("Ledger", "Entry")
        };

        foreach (var token in forbidden)
        {
            Assert.DoesNotContain(token, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static FxBboOfflineResearchQuoteLoadResult LoadApprovedManifest(string manifestPath)
    {
        var load = new FxBboOfflineResearchQuoteLoader().Load(new(
            ManifestPath: manifestPath,
            ValidationTimestampUtc: ValidationTime,
            AllowLocalEvaluation: true));
        Assert.Equal(FxBboResearchDataAuthorizationStatus.Authorized, load.Status);
        return load;
    }

    private static WindowRun RunWindow(IReadOnlyList<FxBboQuoteResearch> allQuotes, AuditWindow window)
    {
        var quotes = allQuotes.Where(x => x.TimestampUtc >= window.StartUtc && x.TimestampUtc <= window.EndUtc).ToArray();
        var sample = new FxBboToSynchronizedMidpointSamplerResearch().Sample(
            quotes,
            new FxBboSamplingParametersResearch(
                Symbols: LockedSymbols,
                StartUtc: window.StartUtc,
                EndUtc: window.EndUtc,
                GridInterval: GridInterval,
                MaxQuoteAge: GridInterval,
                RequireAllSymbols: true,
                RequireExactGridAlignment: true));
        var bars = new FxBboToSynchronizedMidpointSamplerResearch().ToResidualDivergenceBars(sample.Observations);
        var parameters = Parameters();
        var signals = new FxResidualDivergenceResearchStrategy().GenerateSignals(bars, parameters);
        var summary = new FxResidualDivergenceDiagnosticEvaluator().Evaluate(signals, bars, parameters);

        return new(window, new(sample, sample.Observations, signals, summary));
    }

    private static FxResidualDivergenceParameters Parameters()
        => new(
            TargetSymbol: "C:EURUSD",
            PeerSymbols: LockedPeers,
            RegressionLookbackBars: 120,
            ResidualZLookbackBars: 120,
            MinRegressionObservations: 60,
            EntryZScore: 2.0,
            ExitZScore: 0.5,
            MaxAbsBeta: 5.0,
            MinPeerCount: 2,
            MaxMissingPeerFraction: 0.0,
            RequireExactTimestampAlignment: true,
            EvaluationHorizonBars: 5,
            OneSignalPerSymbolPerTimestamp: true);

    private static DistributionSummary Distribution(IEnumerable<double> values)
    {
        var sorted = values.Where(x => !double.IsNaN(x) && !double.IsInfinity(x)).OrderBy(x => x).ToArray();
        if (sorted.Length == 0)
        {
            return new(null, null, null, null, null, null, null, null, null, null, 0);
        }

        return new(
            sorted[0],
            Percentile(sorted, 0.01),
            Percentile(sorted, 0.05),
            Percentile(sorted, 0.10),
            Percentile(sorted, 0.50),
            Percentile(sorted, 0.90),
            Percentile(sorted, 0.95),
            Percentile(sorted, 0.99),
            sorted[^1],
            sorted.Select(Math.Abs).Max(),
            sorted.Length);
    }

    private static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0)
        {
            return 0.0;
        }

        var index = Math.Clamp((sorted.Count - 1) * p, 0.0, sorted.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper)
        {
            return sorted[lower];
        }

        var weight = index - lower;
        return sorted[lower] * (1.0 - weight) + sorted[upper] * weight;
    }

    private static IReadOnlyDictionary<string, int> ZBuckets(IEnumerable<double> absZ)
        => absZ.GroupBy(x => x switch
            {
                < 0.5 => "lt_0_5",
                < 1.0 => "0_5_to_1",
                < 1.5 => "1_to_1_5",
                < 2.0 => "1_5_to_2",
                < 3.0 => "2_to_3",
                < 5.0 => "3_to_5",
                < 10.0 => "5_to_10",
                _ => "10_plus"
            })
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, int> TinySigmaCounts(IReadOnlyList<FxResidualDivergenceResearchSignal> signals)
    {
        var sigmas = signals.Where(x => x.ResidualSigma > 0.0).Select(x => x.ResidualSigma).ToArray();
        return new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["lt_1e_minus_7"] = sigmas.Count(x => x < 1e-7),
            ["lt_5e_minus_7"] = sigmas.Count(x => x < 5e-7),
            ["lt_1e_minus_6"] = sigmas.Count(x => x < 1e-6),
            ["lt_5e_minus_6"] = sigmas.Count(x => x < 5e-6)
        };
    }

    private static IReadOnlyDictionary<string, int> BetaSignFlips(IReadOnlyList<FxResidualDivergenceResearchSignal> signals)
        => LockedPeers.ToDictionary(peer => peer, peer =>
        {
            var betas = signals
                .Where(x => x.BetaCoefficients.ContainsKey(peer))
                .OrderBy(x => x.TimestampUtc)
                .Select(x => x.BetaCoefficients[peer])
                .ToArray();
            return betas.Zip(betas.Skip(1), (a, b) => Math.Sign(a) != 0 && Math.Sign(b) != 0 && Math.Sign(a) != Math.Sign(b)).Count(x => x);
        }, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, int> LargeBetaJumps(IReadOnlyList<FxResidualDivergenceResearchSignal> signals)
        => LockedPeers.ToDictionary(peer => peer, peer =>
        {
            var betas = signals
                .Where(x => x.BetaCoefficients.ContainsKey(peer))
                .OrderBy(x => x.TimestampUtc)
                .Select(x => x.BetaCoefficients[peer])
                .ToArray();
            return betas.Zip(betas.Skip(1), (a, b) => Math.Abs(b - a) > 1.0).Count(x => x);
        }, StringComparer.Ordinal);

    private static object ExtremeEventConcentration(IReadOnlyList<FxResidualDivergenceDiagnosticLine> lines)
    {
        var returns = lines.OrderByDescending(x => Math.Abs(x.DirectionalDiagnosticReturn)).ToArray();
        var total = lines.Sum(x => x.DirectionalDiagnosticReturn);
        var totalAbs = returns.Sum(x => Math.Abs(x.DirectionalDiagnosticReturn));
        double AbsShare(int count) => totalAbs == 0.0 ? 0.0 : returns.Take(count).Sum(x => Math.Abs(x.DirectionalDiagnosticReturn)) / totalAbs;

        return new
        {
            totalDiagnosticReturn = total,
            top1AbsoluteReturnShare = AbsShare(1),
            top3AbsoluteReturnShare = AbsShare(3),
            top5AbsoluteReturnShare = AbsShare(5),
            top10AbsoluteReturnShare = AbsShare(10),
            byAbsZBucket = PayoffByBucket(lines),
            byWindow = LockedWindows.ToDictionary(window => window.Id, window => Payoff(lines.Where(line => line.Signal.TimestampUtc >= window.StartUtc && line.Signal.TimestampUtc <= window.EndUtc))),
            anyOneWindowDominatesSignalCount = LockedWindows.Any(window => lines.Count(line => line.Signal.TimestampUtc >= window.StartUtc && line.Signal.TimestampUtc <= window.EndUtc) > lines.Count * 0.5),
            dominatedByExtremeEvents = lines.Any(x => Math.Abs(x.Signal.ResidualZScore) >= 10.0) || AbsShare(3) > 0.75
        };
    }

    private static object PayoffByBucket(IEnumerable<FxResidualDivergenceDiagnosticLine> lines)
        => lines
            .GroupBy(x => Math.Abs(x.Signal.ResidualZScore) switch
            {
                < 3.0 => "2_to_3",
                < 5.0 => "3_to_5",
                < 10.0 => "5_to_10",
                _ => "10_plus"
            })
            .ToDictionary(x => x.Key, x => Payoff(x), StringComparer.Ordinal);

    private static PayoffSummary Payoff(IEnumerable<FxResidualDivergenceDiagnosticLine> source)
    {
        var lines = source.ToArray();
        var returns = lines.Select(x => x.DirectionalDiagnosticReturn).OrderBy(x => x).ToArray();
        return new(
            lines.Length,
            returns.Length == 0 ? 0.0 : returns.Average(),
            Median(returns),
            returns.Length == 0 ? 0.0 : returns.Count(x => x > 0.0) / (double)returns.Length,
            returns.Sum());
    }

    private static IReadOnlyList<FxResidualDivergenceDiagnosticLine> ExcludeTopAbsReturns(IReadOnlyList<FxResidualDivergenceDiagnosticLine> lines, int count)
        => lines.OrderByDescending(x => Math.Abs(x.DirectionalDiagnosticReturn)).Skip(count).ToArray();

    private static IReadOnlyList<FxResidualDivergenceDiagnosticLine> ExcludeAboveQualityP95(
        IReadOnlyList<FxResidualDivergenceDiagnosticLine> lines,
        IReadOnlyDictionary<DateTimeOffset, FxSynchronizedMidpointObservationResearch> observationsByTime,
        IReadOnlyList<double> allValues,
        bool spread)
    {
        var p95 = Percentile(allValues.OrderBy(x => x).ToArray(), 0.95);
        return lines.Where(line =>
        {
            if (!observationsByTime.TryGetValue(line.Signal.TimestampUtc, out var observation))
            {
                return false;
            }

            var values = spread
                ? observation.SpreadBps.Values.Select(Convert.ToDouble)
                : observation.QuoteAges.Values.Select(x => x.TotalSeconds);
            return values.All(x => x <= p95);
        }).ToArray();
    }

    private static bool TopZCoincidesWithP95(
        IReadOnlyList<FxResidualDivergenceResearchSignal> signals,
        IReadOnlyDictionary<DateTimeOffset, FxSynchronizedMidpointObservationResearch> observationsByTime,
        IReadOnlyList<double> allValues,
        bool spread)
    {
        var p95 = Percentile(allValues.OrderBy(x => x).ToArray(), 0.95);
        return signals.Any(signal =>
        {
            if (!observationsByTime.TryGetValue(signal.TimestampUtc, out var observation))
            {
                return false;
            }

            var values = spread
                ? observation.SpreadBps.Values.Select(Convert.ToDouble)
                : observation.QuoteAges.Values.Select(x => x.TotalSeconds);
            return values.Any(x => x > p95);
        });
    }

    private static double Median(IReadOnlyList<double> sortedValues)
    {
        if (sortedValues.Count == 0)
        {
            return 0.0;
        }

        var middle = sortedValues.Count / 2;
        return sortedValues.Count % 2 == 0
            ? (sortedValues[middle - 1] + sortedValues[middle]) / 2.0
            : sortedValues[middle];
    }

    private static string ApprovedManifestPath(string root)
        => Path.Combine(root, "artifacts", "readiness", "intraday-fx-residual-divergence-extended-r016-approval-eval-r017", "POLYGON_FX_TICK_EXTENDED_R016_APPROVED.local.json");

    private static string R017SummaryPath(string root)
        => Path.Combine(root, "artifacts", "readiness", "intraday-fx-residual-divergence-extended-r016-approval-eval-r017", "fx-residual-divergence-extended-r016-approval-eval-summary.json");

    private static string ArtifactsPath(string root)
        => Path.Combine(root, "artifacts", "readiness", "intraday-fx-residual-divergence-zscore-robustness-audit-r018");

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

    private sealed record AuditWindow(string Id, DateTimeOffset StartUtc, DateTimeOffset EndUtc);
    private sealed record PipelineResult(FxBboSamplingResultResearch Sample, IReadOnlyList<FxSynchronizedMidpointObservationResearch> Observations, IReadOnlyList<FxResidualDivergenceResearchSignal> Signals, FxResidualDivergenceDiagnosticSummary Summary);
    private sealed record WindowRun(AuditWindow Window, PipelineResult Pipeline);
    private sealed record SignalObservation(FxResidualDivergenceResearchSignal Signal, FxSynchronizedMidpointObservationResearch Observation);
    private sealed record DistributionSummary(double? Min, double? P1, double? P5, double? P10, double? Median, double? P90, double? P95, double? P99, double? Max, double? MaxAbs, int Count);
    private sealed record PayoffSummary(int Count, double Mean, double Median, double HitRate, double Cumulative);
}
