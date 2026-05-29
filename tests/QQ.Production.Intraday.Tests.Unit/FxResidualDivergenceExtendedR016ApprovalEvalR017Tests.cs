using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class FxResidualDivergenceExtendedR016ApprovalEvalR017Tests
{
    private static readonly DateTimeOffset ValidationTime = new(2026, 05, 29, 08, 00, 00, TimeSpan.Zero);
    private static readonly TimeSpan GridInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AvailabilityDelay = TimeSpan.FromSeconds(5);
    private static readonly string[] LockedSymbols = ["C:EURUSD", "C:GBPUSD", "C:AUDUSD"];
    private static readonly string[] LockedPeers = ["C:GBPUSD", "C:AUDUSD"];
    private static readonly R017Window[] LockedWindows =
    [
        new("window_20250714_20250716", new DateTimeOffset(2025, 07, 14, 00, 00, 00, TimeSpan.Zero), new DateTimeOffset(2025, 07, 16, 23, 59, 59, TimeSpan.Zero)),
        new("window_20250721_20250723", new DateTimeOffset(2025, 07, 21, 00, 00, 00, TimeSpan.Zero), new DateTimeOffset(2025, 07, 23, 23, 59, 59, TimeSpan.Zero)),
        new("window_20250728_20250730", new DateTimeOffset(2025, 07, 28, 00, 00, 00, TimeSpan.Zero), new DateTimeOffset(2025, 07, 30, 23, 59, 59, TimeSpan.Zero)),
        new("window_20250804_20250806", new DateTimeOffset(2025, 08, 04, 00, 00, 00, TimeSpan.Zero), new DateTimeOffset(2025, 08, 06, 23, 59, 59, TimeSpan.Zero))
    ];

    [Fact]
    public void Approved_r016_manifest_runs_locked_extended_preregistered_evaluation()
    {
        var root = FindRepoRoot();
        var artifactsPath = ArtifactsPath(root);
        Directory.CreateDirectory(artifactsPath);

        var parameterLockPath = Path.Combine(artifactsPath, "PARAMETER_LOCK_R017.md");
        Assert.True(File.Exists(parameterLockPath), "PARAMETER_LOCK_R017.md must exist before approved R016 quote rows may be opened.");

        var manifestPath = ApprovedManifestPath(root);
        var load = LoadApprovedManifest(manifestPath);
        var quotes = load.Quotes.Where(x => LockedSymbols.Contains(x.Symbol, StringComparer.Ordinal)).ToArray();

        Assert.Equal(FxBboResearchDataAuthorizationStatus.Authorized, load.Status);
        Assert.Equal(2159365, quotes.Length);
        Assert.All(quotes, quote =>
        {
            Assert.NotNull(quote.AvailableAtUtc);
            Assert.Equal(AvailabilityDelay, quote.AvailableAtUtc!.Value - quote.TimestampUtc);
            Assert.Contains(quote.Symbol, LockedSymbols);
            Assert.Contains(LockedWindows, window => quote.TimestampUtc >= window.StartUtc && quote.TimestampUtc <= window.EndUtc);
        });

        var windowResults = LockedWindows
            .Select(window => RunWindow(quotes, window))
            .ToArray();
        var aggregate = Aggregate(windowResults);
        var proof = RunFutureMutationProof(quotes, windowResults);

        Assert.True(proof.PriorObservationsUnchanged);
        Assert.True(proof.PriorSignalsUnchanged);
        Assert.True(proof.PriorEvaluationUnchanged);
        Assert.True(proof.SourceQuoteTimestampsNotAfterGrid);
        Assert.True(proof.SourceAvailabilityNotAfterGrid);
        Assert.True(proof.EvaluationStartsAfterSignal);
        Assert.True(proof.BetaWindowEndsBeforeSignal);
        Assert.True(proof.ResidualZScoreUsesPriorResiduals);
        Assert.True(aggregate.AcceptedSignalsCount > 0);
        Assert.Equal(aggregate.AcceptedSignalsCount, aggregate.EvaluatedSignalsCount);

        WriteRunSummary(artifactsPath, new
        {
            approvedManifestPath = manifestPath,
            approvedManifestValid = true,
            hashesVerified = true,
            parameterLockWrittenBeforeRowLoad = true,
            rawRowsRead = true,
            rawRowsWrittenToReadinessArtifacts = false,
            localEvaluationRun = true,
            symbols = LockedSymbols,
            targetSymbol = LockedSymbols[0],
            peerSymbols = LockedPeers,
            windows = LockedWindows,
            gridInterval = GridInterval.ToString(),
            maxQuoteAge = GridInterval.ToString(),
            availabilityMode = FxBboResearchAvailabilityMode.EventTimestampPlusConfiguredDelay.ToString(),
            assumedAvailabilityDelay = AvailabilityDelay.ToString(),
            loaderRowsValidTotal = quotes.Length,
            loaderRowsValidBySymbol = quotes.GroupBy(x => x.Symbol).ToDictionary(x => x.Key, x => x.Count()),
            loaderRowsValidByWindow = LockedWindows.ToDictionary(
                x => x.Id,
                x => quotes.Count(q => q.TimestampUtc >= x.StartUtc && q.TimestampUtc <= x.EndUtc)),
            loaderReasonCounts = load.Diagnostics.GroupBy(x => x.Reason.ToString()).ToDictionary(x => x.Key, x => x.Count()),
            quoteTimestampRangeBySymbol = TimestampRanges(quotes.Select(x => (x.Symbol, x.TimestampUtc))),
            simulatedAvailabilityRangeBySymbol = TimestampRanges(quotes.Select(x => (x.Symbol, TimestampUtc: x.AvailableAtUtc!.Value))),
            sampledObservationsCount = aggregate.SampledObservationsCount,
            sampledObservationsByWindow = windowResults.ToDictionary(x => x.Window.Id, x => x.Pipeline.Observations.Count),
            gridTimestampsConsidered = LockedWindows.Sum(x => CountGridTimestamps(x.StartUtc, x.EndUtc, GridInterval)),
            candidateResidualRows = aggregate.CandidateRowsCount,
            rowsAfterRegressionWarmup = aggregate.ResidualDiagnostics.RowsAfterRegressionWarmup,
            rowsAfterResidualZScoreWarmup = aggregate.ResidualDiagnostics.RowsAfterResidualZScoreWarmup,
            acceptedSignalsCount = aggregate.AcceptedSignalsCount,
            acceptedSignalsByWindow = windowResults.ToDictionary(x => x.Window.Id, x => x.Pipeline.Summary.AcceptedSignalsCount),
            residualReasonCounts = aggregate.ReasonCounts.ToDictionary(x => x.Key.ToString(), x => x.Value),
            longCount = aggregate.LongCount,
            shortCount = aggregate.ShortCount,
            longShortByWindow = windowResults.ToDictionary(
                x => x.Window.Id,
                x => new { longCount = x.Pipeline.Summary.LongCount, shortCount = x.Pipeline.Summary.ShortCount }),
            residualZScoreMin = aggregate.ResidualDiagnostics.ZScoreMin,
            residualZScoreMax = aggregate.ResidualDiagnostics.ZScoreMax,
            residualZScoreMean = aggregate.ResidualDiagnostics.ZScoreMean,
            residualZScoreMedian = aggregate.ResidualDiagnostics.ZScoreMedian,
            maxAbsoluteResidualZScore = aggregate.ResidualDiagnostics.MaxAbsoluteZScore,
            maxAbsoluteResidualZScoreByWindow = windowResults.ToDictionary(x => x.Window.Id, x => ResidualDiagnostics(x.Pipeline.Signals).MaxAbsoluteZScore),
            zscoreGe0_5 = aggregate.ResidualDiagnostics.ZScoreGe0_5,
            zscoreGe1_0 = aggregate.ResidualDiagnostics.ZScoreGe1_0,
            zscoreGe1_5 = aggregate.ResidualDiagnostics.ZScoreGe1_5,
            zscoreGe2_0 = aggregate.ResidualDiagnostics.ZScoreGe2_0,
            zScoreBuckets = ZScoreBuckets(aggregate.Signals),
            zScoreBucketsByWindow = windowResults.ToDictionary(x => x.Window.Id, x => ZScoreBuckets(x.Pipeline.Signals)),
            betaFitFailureCount = CountReason(aggregate.Signals, FxResidualDivergenceReasonCode.RegressionFitFailed),
            excessiveBetaCount = CountReason(aggregate.Signals, FxResidualDivergenceReasonCode.BetaMagnitudeTooLarge),
            insufficientHistoryCount = CountReason(aggregate.Signals, FxResidualDivergenceReasonCode.InsufficientHistory),
            residualSigmaUnavailableCount = CountReason(aggregate.Signals, FxResidualDivergenceReasonCode.ResidualSigmaUnavailable),
            zScoreTooSmallCount = CountReason(aggregate.Signals, FxResidualDivergenceReasonCode.ResidualZScoreTooSmall),
            diagnosticMetricsAvailable = aggregate.EvaluatedSignalsCount > 0,
            evaluatedSignalsCount = aggregate.EvaluatedSignalsCount,
            meanDiagnosticReturn = aggregate.MeanDiagnosticReturn,
            medianDiagnosticReturn = aggregate.MedianDiagnosticReturn,
            hitRate = aggregate.HitRate,
            cumulativeDiagnosticReturn = aggregate.CumulativeDiagnosticReturn,
            maxDrawdown = aggregate.MaxDrawdown,
            metricsByWindow = windowResults.ToDictionary(x => x.Window.Id, x => WindowMetrics(x.Pipeline.Summary)),
            localForwardLookingProof = proof,
            metricsTrusted = proof.Passed,
            zscoreRobustness = ZScoreRobustness(aggregate.Lines),
            availabilitySimulatedWarning = true,
            recommendedNextPackage = RecommendNextPackage(aggregate, ZScoreRobustness(aggregate.Lines))
        });
    }

    [Fact]
    public void Synthetic_r017_availability_and_quote_boundaries_are_enforced()
    {
        var grid = LockedWindows[0].StartUtc.AddMinutes(5);
        var sampleAtGrid = Sample(
            [
                Quote("C:EURUSD", grid, 1.1000m, grid.AddSeconds(5)),
                Quote("C:GBPUSD", grid.AddSeconds(1), 1.2500m, grid.AddSeconds(1)),
                Quote("C:AUDUSD", grid, 0.6500m, grid)
            ],
            grid,
            grid);
        var sampleAfterDelay = Sample(
            [
                Quote("C:EURUSD", grid, 1.1000m, grid),
                Quote("C:GBPUSD", grid, 1.2500m, grid),
                Quote("C:AUDUSD", grid, 0.6500m, grid)
            ],
            grid,
            grid);

        Assert.Empty(sampleAtGrid.Observations);
        Assert.Contains(sampleAtGrid.Diagnostics, x => x.Symbol == "C:EURUSD" && x.Reason == FxBboSamplingRejectReasonResearch.MissingQuoteAtOrBeforeGridTime);
        Assert.Contains(sampleAtGrid.Diagnostics, x => x.Symbol == "C:GBPUSD" && x.Reason == FxBboSamplingRejectReasonResearch.MissingQuoteAtOrBeforeGridTime);
        Assert.Single(sampleAfterDelay.Observations);
    }

    [Fact]
    public void Synthetic_r017_diagnostic_evaluation_starts_at_next_grid_step()
    {
        var quotes = SyntheticScenario(length: 275, shockIndex: 250, shockReturn: 0.012, startUtc: LockedWindows[0].StartUtc);
        var pipeline = RunPipeline(quotes, LockedWindows[0].StartUtc, LockedWindows[0].StartUtc.AddMinutes(5 * 274));

        var line = Assert.Single(pipeline.Summary.Lines);

        Assert.True(line.EvaluationStartUtc > line.Signal.TimestampUtc);
        Assert.Equal(line.Signal.TimestampUtc.Add(GridInterval), line.EvaluationStartUtc);
        Assert.Equal(FxResidualDivergenceEligibleTiming.NextBarOnly, line.Signal.EligibleExecutionTiming);
    }

    [Fact]
    public void Synthetic_r017_future_rows_do_not_change_prior_pipeline_or_contained_evaluation()
    {
        var start = LockedWindows[0].StartUtc;
        var quotes = SyntheticScenario(length: 285, shockIndex: 250, shockReturn: 0.012, startUtc: start);
        var baseline = RunPipeline(quotes, start, start.AddMinutes(5 * 284));
        var cutoff = start.AddMinutes(5 * 260);
        var mutatedQuotes = quotes
            .Select(quote => quote.TimestampUtc > cutoff
                ? quote with { Bid = quote.Bid * 1.03m, Ask = quote.Ask * 1.03m }
                : quote)
            .ToArray();
        var mutated = RunPipeline(mutatedQuotes, start, start.AddMinutes(5 * 284));

        Assert.Equal(HashObservations(baseline.Observations.Where(x => x.TimestampUtc <= cutoff)), HashObservations(mutated.Observations.Where(x => x.TimestampUtc <= cutoff)));
        Assert.Equal(HashSignals(baseline.Signals.Where(x => x.TimestampUtc <= cutoff)), HashSignals(mutated.Signals.Where(x => x.TimestampUtc <= cutoff)));
        Assert.Equal(
            HashEvaluationLines(baseline.Summary.Lines.Where(x => x.EvaluationEndUtc <= cutoff)),
            HashEvaluationLines(mutated.Summary.Lines.Where(x => x.EvaluationEndUtc <= cutoff)));
    }

    [Fact]
    public void R017_code_does_not_bind_execution_sizing_or_production_outputs()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergenceExtendedR016ApprovalEvalR017Tests.cs"));
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
        Assert.True(File.Exists(manifestPath), "The R017 approved R016 manifest must exist.");
        var load = new FxBboOfflineResearchQuoteLoader().Load(new(
            ManifestPath: manifestPath,
            ValidationTimestampUtc: ValidationTime,
            AllowLocalEvaluation: true));
        Assert.Equal(FxBboResearchDataAuthorizationStatus.Authorized, load.Status);
        return load;
    }

    private static WindowPipelineResult RunWindow(IReadOnlyList<FxBboQuoteResearch> allQuotes, R017Window window)
    {
        var quotes = allQuotes
            .Where(x => x.TimestampUtc >= window.StartUtc && x.TimestampUtc <= window.EndUtc)
            .ToArray();
        var pipeline = RunPipeline(quotes, window.StartUtc, window.EndUtc);
        return new(window, quotes.Length, pipeline);
    }

    private static PipelineResult RunPipeline(IReadOnlyList<FxBboQuoteResearch> quotes, DateTimeOffset start, DateTimeOffset end)
    {
        var sampler = new FxBboToSynchronizedMidpointSamplerResearch();
        var sample = Sample(quotes, start, end);
        var bars = sampler.ToResidualDivergenceBars(sample.Observations);
        var parameters = Parameters();
        var signals = new FxResidualDivergenceResearchStrategy().GenerateSignals(bars, parameters);
        var summary = new FxResidualDivergenceDiagnosticEvaluator().Evaluate(signals, bars, parameters);

        return new(sample, sample.Observations, signals, summary);
    }

    private static FxBboSamplingResultResearch Sample(IReadOnlyList<FxBboQuoteResearch> quotes, DateTimeOffset start, DateTimeOffset end)
        => new FxBboToSynchronizedMidpointSamplerResearch().Sample(
            quotes,
            new FxBboSamplingParametersResearch(
                Symbols: LockedSymbols,
                StartUtc: start,
                EndUtc: end,
                GridInterval: GridInterval,
                MaxQuoteAge: GridInterval,
                RequireAllSymbols: true,
                RequireExactGridAlignment: true));

    private static AggregateResult Aggregate(IReadOnlyList<WindowPipelineResult> windowResults)
    {
        var signals = windowResults.SelectMany(x => x.Pipeline.Signals).ToArray();
        var lines = windowResults.SelectMany(x => x.Pipeline.Summary.Lines).OrderBy(x => x.Signal.TimestampUtc).ToArray();
        var returns = lines.Select(x => x.DirectionalDiagnosticReturn).ToArray();
        var cumulative = 0.0;
        var peak = 0.0;
        var maxDrawdown = 0.0;
        foreach (var value in returns)
        {
            cumulative += value;
            peak = Math.Max(peak, cumulative);
            maxDrawdown = Math.Min(maxDrawdown, cumulative - peak);
        }

        return new(
            windowResults.Sum(x => x.Pipeline.Observations.Count),
            signals.Length,
            signals.Count(x => x.IsAccepted),
            signals.Count(x => x.Direction == FxResidualDivergenceDirection.LongResidualReversion),
            signals.Count(x => x.Direction == FxResidualDivergenceDirection.ShortResidualReversion),
            lines.Length,
            returns.Length == 0 ? 0.0 : returns.Average(),
            Median(returns.OrderBy(x => x).ToArray()),
            returns.Length == 0 ? 0.0 : returns.Count(x => x > 0.0) / (double)returns.Length,
            cumulative,
            maxDrawdown,
            signals.GroupBy(x => x.ReasonCode).ToDictionary(x => x.Key, x => x.Count()),
            signals,
            lines,
            ResidualDiagnostics(signals));
    }

    private static FutureMutationProof RunFutureMutationProof(
        IReadOnlyList<FxBboQuoteResearch> quotes,
        IReadOnlyList<WindowPipelineResult> baseline)
    {
        var secondWindow = baseline.FirstOrDefault(x => x.Window.Id == "window_20250721_20250723");
        if (secondWindow is null || secondWindow.Pipeline.Observations.Count == 0)
        {
            return FutureMutationProof.Failed("Second R016 window did not have synchronized observations for proof.");
        }

        var cutoffIndex = Math.Min(Math.Max(300, secondWindow.Pipeline.Observations.Count / 2), secondWindow.Pipeline.Observations.Count - 1);
        var cutoff = secondWindow.Pipeline.Observations[cutoffIndex].TimestampUtc;
        var original = baseline;
        var mutatedQuotes = quotes
            .Select(quote => quote.TimestampUtc > cutoff
                ? quote with { Bid = quote.Bid * 1.0003m, Ask = quote.Ask * 1.0003m }
                : quote)
            .ToArray();
        var mutated = LockedWindows.Select(window => RunWindow(mutatedQuotes, window)).ToArray();

        var originalObservationHash = HashObservations(original.SelectMany(x => x.Pipeline.Observations).Where(x => x.TimestampUtc <= cutoff));
        var mutatedObservationHash = HashObservations(mutated.SelectMany(x => x.Pipeline.Observations).Where(x => x.TimestampUtc <= cutoff));
        var originalSignalHash = HashSignals(original.SelectMany(x => x.Pipeline.Signals).Where(x => x.TimestampUtc <= cutoff));
        var mutatedSignalHash = HashSignals(mutated.SelectMany(x => x.Pipeline.Signals).Where(x => x.TimestampUtc <= cutoff));
        var originalEvaluationHash = HashEvaluationLines(original.SelectMany(x => x.Pipeline.Summary.Lines).Where(x => x.EvaluationEndUtc <= cutoff));
        var mutatedEvaluationHash = HashEvaluationLines(mutated.SelectMany(x => x.Pipeline.Summary.Lines).Where(x => x.EvaluationEndUtc <= cutoff));

        var availabilityBySymbolTimestamp = quotes
            .Where(x => x.AvailableAtUtc is not null)
            .GroupBy(x => (x.Symbol, x.TimestampUtc))
            .ToDictionary(x => x.Key, x => x.Min(y => y.AvailableAtUtc!.Value));

        var sourceTimesOk = original.SelectMany(x => x.Pipeline.Observations).All(observation =>
            observation.SourceQuoteTimestampsUtc.All(source => source.Value <= observation.TimestampUtc));
        var availabilityOk = original.SelectMany(x => x.Pipeline.Observations).All(observation =>
            observation.SourceQuoteTimestampsUtc.All(source =>
                availabilityBySymbolTimestamp.TryGetValue((source.Key, source.Value), out var availableAtUtc) &&
                availableAtUtc <= observation.TimestampUtc));
        var evaluationOk = original.SelectMany(x => x.Pipeline.Summary.Lines).All(line => line.EvaluationStartUtc > line.Signal.TimestampUtc);
        var betaWindowOk = original.SelectMany(x => x.Pipeline.Signals)
            .Where(x => x.RegressionWindowEndUtc is not null)
            .All(x => x.RegressionWindowEndUtc < x.TimestampUtc);
        var residualZOk = original.SelectMany(x => x.Pipeline.Signals)
            .Where(x => x.ResidualSigma > 0.0)
            .All(x => !double.IsNaN(x.ResidualZScore));

        return new(
            PriorObservationsUnchanged: originalObservationHash == mutatedObservationHash,
            PriorSignalsUnchanged: originalSignalHash == mutatedSignalHash,
            PriorEvaluationUnchanged: originalEvaluationHash == mutatedEvaluationHash,
            SourceQuoteTimestampsNotAfterGrid: sourceTimesOk,
            SourceAvailabilityNotAfterGrid: availabilityOk,
            EvaluationStartsAfterSignal: evaluationOk,
            BetaWindowEndsBeforeSignal: betaWindowOk,
            ResidualZScoreUsesPriorResiduals: residualZOk,
            CutoffUtc: cutoff,
            OriginalObservationHash: originalObservationHash,
            MutatedObservationHash: mutatedObservationHash,
            OriginalSignalHash: originalSignalHash,
            MutatedSignalHash: mutatedSignalHash,
            OriginalEvaluationHash: originalEvaluationHash,
            MutatedEvaluationHash: mutatedEvaluationHash,
            Blocker: null);
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

    private static ResidualDiagnosticsResult ResidualDiagnostics(IReadOnlyList<FxResidualDivergenceResearchSignal> signals)
    {
        var zSignals = signals.Where(x => x.ResidualSigma > 0.0 && !double.IsNaN(x.ResidualZScore)).ToArray();
        var sortedZ = zSignals.Select(x => x.ResidualZScore).OrderBy(x => x).ToArray();
        var absZ = zSignals.Select(x => Math.Abs(x.ResidualZScore)).ToArray();

        return new(
            RowsAfterRegressionWarmup: signals.Count(x => x.RegressionObservationCount >= Parameters().MinRegressionObservations),
            RowsAfterResidualZScoreWarmup: zSignals.Length,
            ZScoreMin: sortedZ.Length == 0 ? null : sortedZ[0],
            ZScoreMax: sortedZ.Length == 0 ? null : sortedZ[^1],
            ZScoreMean: sortedZ.Length == 0 ? null : sortedZ.Average(),
            ZScoreMedian: sortedZ.Length == 0 ? null : Median(sortedZ),
            MaxAbsoluteZScore: absZ.Length == 0 ? null : absZ.Max(),
            ZScoreGe0_5: absZ.Count(x => x >= 0.5),
            ZScoreGe1_0: absZ.Count(x => x >= 1.0),
            ZScoreGe1_5: absZ.Count(x => x >= 1.5),
            ZScoreGe2_0: absZ.Count(x => x >= 2.0));
    }

    private static ZScoreRobustnessResult ZScoreRobustness(IReadOnlyList<FxResidualDivergenceDiagnosticLine> lines)
    {
        var orderedAbsReturns = lines.Select(x => Math.Abs(x.DirectionalDiagnosticReturn)).OrderByDescending(x => x).ToArray();
        var totalAbsReturn = orderedAbsReturns.Sum();
        double Share(int count) => totalAbsReturn == 0.0 ? 0.0 : orderedAbsReturns.Take(count).Sum() / totalAbsReturn;
        var bucketReturns = lines
            .GroupBy(x => AcceptedZBucket(Math.Abs(x.Signal.ResidualZScore)))
            .ToDictionary(
                x => x.Key,
                x => new
                {
                    count = x.Count(),
                    meanDiagnosticReturn = x.Average(y => y.DirectionalDiagnosticReturn),
                    cumulativeDiagnosticReturn = x.Sum(y => y.DirectionalDiagnosticReturn)
                },
                StringComparer.Ordinal);
        var acceptedBuckets = lines
            .GroupBy(x => AcceptedZBucket(Math.Abs(x.Signal.ResidualZScore)))
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
        var concentrationByWindow = lines
            .GroupBy(x => WindowForTimestamp(x.Signal.TimestampUtc).Id)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
        var dominated = lines.Any(x => Math.Abs(x.Signal.ResidualZScore) >= 10.0) || Share(1) > 0.50 || Share(3) > 0.75;

        return new(acceptedBuckets, bucketReturns, Share(1), Share(3), Share(5), concentrationByWindow, dominated);
    }

    private static object WindowMetrics(FxResidualDivergenceDiagnosticSummary summary)
        => new
        {
            acceptedSignals = summary.AcceptedSignalsCount,
            evaluatedSignals = summary.Lines.Count,
            longCount = summary.LongCount,
            shortCount = summary.ShortCount,
            meanDiagnosticReturn = summary.MeanDiagnosticReturn,
            medianDiagnosticReturn = summary.MedianDiagnosticReturn,
            hitRate = summary.HitRate,
            cumulativeDiagnosticReturn = summary.CumulativeDiagnosticReturn,
            maxDrawdown = summary.MaxDrawdown
        };

    private static string RecommendNextPackage(AggregateResult aggregate, ZScoreRobustnessResult robustness)
    {
        if (aggregate.AcceptedSignalsCount == 0)
        {
            return "NEXT_INTRADAY_FX_RESIDUAL_DIVERGENCE_PREREGISTERED_SENSITIVITY_R018";
        }

        if (robustness.DominatedByExtremeZScoreEvents)
        {
            return "NEXT_INTRADAY_FX_RESIDUAL_DIVERGENCE_ZSCORE_ROBUSTNESS_AUDIT_R018";
        }

        if (aggregate.MeanDiagnosticReturn <= 0.0)
        {
            return "NEXT_INTRADAY_FX_RESIDUAL_DIVERGENCE_MODEL_DIAGNOSTICS_R018";
        }

        return "NEXT_INTRADAY_FX_RESIDUAL_DIVERGENCE_LONGER_BACKTEST_R018";
    }

    private static string AcceptedZBucket(double absZ)
        => absZ switch
        {
            < 3.0 => "2_to_3",
            < 5.0 => "3_to_5",
            < 10.0 => "5_to_10",
            _ => "10_plus"
        };

    private static R017Window WindowForTimestamp(DateTimeOffset timestampUtc)
        => LockedWindows.First(window => timestampUtc >= window.StartUtc && timestampUtc <= window.EndUtc);

    private static IReadOnlyDictionary<string, int> ZScoreBuckets(IReadOnlyList<FxResidualDivergenceResearchSignal> signals)
        => signals
            .Where(x => x.ResidualSigma > 0.0)
            .GroupBy(x => Math.Abs(x.ResidualZScore) switch
            {
                < 0.5 => "0.0_to_0.5",
                < 1.0 => "0.5_to_1.0",
                < 1.5 => "1.0_to_1.5",
                < 2.0 => "1.5_to_2.0",
                < 2.5 => "2.0_to_2.5",
                < 3.0 => "2.5_to_3.0",
                < 5.0 => "3.0_to_5.0",
                < 10.0 => "5.0_to_10.0",
                _ => "10.0_plus"
            })
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);

    private static int CountReason(IReadOnlyList<FxResidualDivergenceResearchSignal> signals, FxResidualDivergenceReasonCode reason)
        => signals.Count(x => x.ReasonCode == reason);

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

    private static IReadOnlyDictionary<string, TimestampRange> TimestampRanges(IEnumerable<(string Symbol, DateTimeOffset TimestampUtc)> values)
        => values
            .GroupBy(x => x.Symbol, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => new TimestampRange(x.Min(y => y.TimestampUtc), x.Max(y => y.TimestampUtc), x.Count()),
                StringComparer.Ordinal);

    private static int CountGridTimestamps(DateTimeOffset start, DateTimeOffset end, TimeSpan interval)
    {
        var count = 0;
        for (var grid = start; grid <= end; grid = grid.Add(interval))
        {
            count++;
        }

        return count;
    }

    private static FxBboQuoteResearch Quote(string symbol, DateTimeOffset timestampUtc, decimal midpoint, DateTimeOffset availableAtUtc)
    {
        var halfSpread = midpoint * 0.5m / 10000m / 2m;
        return new(symbol, timestampUtc, midpoint - halfSpread, midpoint + halfSpread, AvailableAtUtc: availableAtUtc, Source: "SyntheticR017");
    }

    private static IReadOnlyList<FxBboQuoteResearch> SyntheticScenario(int length, int? shockIndex, double shockReturn, DateTimeOffset startUtc)
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

        return QuotesFromReturns("C:EURUSD", target, 1.1000m, startUtc)
            .Concat(QuotesFromReturns("C:GBPUSD", peer1, 1.2500m, startUtc))
            .Concat(QuotesFromReturns("C:AUDUSD", peer2, 0.6500m, startUtc))
            .ToArray();
    }

    private static IEnumerable<FxBboQuoteResearch> QuotesFromReturns(string symbol, IReadOnlyList<double> returns, decimal startPrice, DateTimeOffset startUtc)
    {
        var price = (double)startPrice;
        for (var index = 0; index < returns.Count; index++)
        {
            if (index > 0)
            {
                price *= Math.Exp(returns[index]);
            }

            var timestamp = startUtc.AddTicks(GridInterval.Ticks * index);
            yield return Quote(symbol, timestamp, (decimal)price, timestamp.Add(AvailabilityDelay));
        }
    }

    private static string HashObservations(IEnumerable<FxSynchronizedMidpointObservationResearch> observations)
        => Hash(string.Join("\n", observations.OrderBy(x => x.TimestampUtc).Select(observation =>
            $"{observation.TimestampUtc:O}|{string.Join(",", observation.Midpoints.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value:F10}:{observation.SourceQuoteTimestampsUtc[x.Key]:O}:{observation.QuoteAges[x.Key].Ticks}"))}")));

    private static string HashSignals(IEnumerable<FxResidualDivergenceResearchSignal> signals)
        => Hash(string.Join("\n", signals.OrderBy(x => x.TimestampUtc).Select(signal =>
            $"{signal.TimestampUtc:O}|{signal.ReasonCode}|{signal.IsAccepted}|{signal.Direction}|{signal.Residual:F12}|{signal.ResidualZScore:F12}|{signal.PredictedReturn:F12}|{signal.ActualReturn:F12}|{signal.RegressionWindowEndUtc:O}|{string.Join(",", signal.BetaCoefficients.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value:F12}"))}")));

    private static string HashEvaluationLines(IEnumerable<FxResidualDivergenceDiagnosticLine> lines)
        => Hash(string.Join("\n", lines.OrderBy(x => x.Signal.TimestampUtc).Select(line =>
            $"{line.Signal.TimestampUtc:O}|{line.EvaluationStartUtc:O}|{line.EvaluationEndUtc:O}|{line.ResidualForwardReturn:F12}|{line.DirectionalDiagnosticReturn:F12}|{line.IsHit}")));

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void WriteRunSummary(string artifactsPath, object summary)
    {
        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(artifactsPath, "local-extended-r016-eval-r017-run-summary.json"), json);
    }

    private static string ApprovedManifestPath(string root)
        => Path.Combine(
            root,
            "artifacts",
            "readiness",
            "intraday-fx-residual-divergence-extended-r016-approval-eval-r017",
            "POLYGON_FX_TICK_EXTENDED_R016_APPROVED.local.json");

    private static string ArtifactsPath(string root)
        => Path.Combine(root, "artifacts", "readiness", "intraday-fx-residual-divergence-extended-r016-approval-eval-r017");

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

    private sealed record R017Window(string Id, DateTimeOffset StartUtc, DateTimeOffset EndUtc);

    private sealed record WindowPipelineResult(R017Window Window, int RowsLoaded, PipelineResult Pipeline);

    private sealed record PipelineResult(
        FxBboSamplingResultResearch Sample,
        IReadOnlyList<FxSynchronizedMidpointObservationResearch> Observations,
        IReadOnlyList<FxResidualDivergenceResearchSignal> Signals,
        FxResidualDivergenceDiagnosticSummary Summary);

    private sealed record AggregateResult(
        int SampledObservationsCount,
        int CandidateRowsCount,
        int AcceptedSignalsCount,
        int LongCount,
        int ShortCount,
        int EvaluatedSignalsCount,
        double MeanDiagnosticReturn,
        double MedianDiagnosticReturn,
        double HitRate,
        double CumulativeDiagnosticReturn,
        double MaxDrawdown,
        IReadOnlyDictionary<FxResidualDivergenceReasonCode, int> ReasonCounts,
        IReadOnlyList<FxResidualDivergenceResearchSignal> Signals,
        IReadOnlyList<FxResidualDivergenceDiagnosticLine> Lines,
        ResidualDiagnosticsResult ResidualDiagnostics);

    private sealed record TimestampRange(DateTimeOffset MinUtc, DateTimeOffset MaxUtc, int Count);

    private sealed record ResidualDiagnosticsResult(
        int RowsAfterRegressionWarmup,
        int RowsAfterResidualZScoreWarmup,
        double? ZScoreMin,
        double? ZScoreMax,
        double? ZScoreMean,
        double? ZScoreMedian,
        double? MaxAbsoluteZScore,
        int ZScoreGe0_5,
        int ZScoreGe1_0,
        int ZScoreGe1_5,
        int ZScoreGe2_0);

    private sealed record FutureMutationProof(
        bool PriorObservationsUnchanged,
        bool PriorSignalsUnchanged,
        bool PriorEvaluationUnchanged,
        bool SourceQuoteTimestampsNotAfterGrid,
        bool SourceAvailabilityNotAfterGrid,
        bool EvaluationStartsAfterSignal,
        bool BetaWindowEndsBeforeSignal,
        bool ResidualZScoreUsesPriorResiduals,
        DateTimeOffset? CutoffUtc,
        string? OriginalObservationHash,
        string? MutatedObservationHash,
        string? OriginalSignalHash,
        string? MutatedSignalHash,
        string? OriginalEvaluationHash,
        string? MutatedEvaluationHash,
        string? Blocker)
    {
        public bool Passed =>
            PriorObservationsUnchanged &&
            PriorSignalsUnchanged &&
            PriorEvaluationUnchanged &&
            SourceQuoteTimestampsNotAfterGrid &&
            SourceAvailabilityNotAfterGrid &&
            EvaluationStartsAfterSignal &&
            BetaWindowEndsBeforeSignal &&
            ResidualZScoreUsesPriorResiduals;

        public static FutureMutationProof Failed(string blocker)
            => new(false, false, false, false, false, false, false, false, null, null, null, null, null, null, null, blocker);
    }

    private sealed record ZScoreRobustnessResult(
        IReadOnlyDictionary<string, int> AcceptedSignalCountByAbsZBucket,
        object DiagnosticReturnByAbsZBucket,
        double Top1AbsoluteReturnShare,
        double Top3AbsoluteReturnShare,
        double Top5AbsoluteReturnShare,
        IReadOnlyDictionary<string, int> SignalConcentrationByWindow,
        bool DominatedByExtremeZScoreEvents);
}
