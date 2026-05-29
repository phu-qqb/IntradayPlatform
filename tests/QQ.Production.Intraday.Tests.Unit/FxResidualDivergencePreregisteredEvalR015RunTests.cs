using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class FxResidualDivergencePreregisteredEvalR015RunTests
{
    private static readonly DateTimeOffset ValidationTime = new(2026, 05, 28, 18, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset LockedStartUtc = new(2025, 07, 07, 00, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset LockedEndUtc = new(2025, 07, 09, 23, 59, 59, TimeSpan.Zero);
    private static readonly TimeSpan GridInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AvailabilityDelay = TimeSpan.FromSeconds(5);
    private static readonly string[] LockedSymbols = ["C:EURUSD", "C:GBPUSD", "C:AUDUSD"];
    private static readonly string[] LockedPeers = ["C:GBPUSD", "C:AUDUSD"];

    [Fact]
    public void Approved_r015_manifest_runs_locked_preregistered_evaluation()
    {
        var root = FindRepoRoot();
        var artifactsPath = ArtifactsPath(root);
        Directory.CreateDirectory(artifactsPath);

        var parameterLockPath = Path.Combine(artifactsPath, "PARAMETER_LOCK_R015.md");
        Assert.True(File.Exists(parameterLockPath), "PARAMETER_LOCK_R015.md must exist before approved R015 quote rows may be opened.");

        var manifestPath = ApprovedManifestPath(root);
        var load = LoadApprovedManifest(manifestPath);
        var quotes = load.Quotes.Where(x => LockedSymbols.Contains(x.Symbol, StringComparer.Ordinal)).ToArray();

        Assert.Equal(FxBboResearchDataAuthorizationStatus.Authorized, load.Status);
        Assert.Equal(559596, quotes.Length);
        Assert.All(quotes, quote =>
        {
            Assert.NotNull(quote.AvailableAtUtc);
            Assert.Equal(AvailabilityDelay, quote.AvailableAtUtc!.Value - quote.TimestampUtc);
        });

        var pipeline = RunPipeline(quotes, LockedStartUtc, LockedEndUtc);
        var proof = RunFutureMutationProof(quotes, LockedStartUtc, LockedEndUtc, pipeline.Observations);
        var residualDiagnostics = ResidualDiagnostics(pipeline.Signals);

        Assert.True(proof.PriorObservationsUnchanged);
        Assert.True(proof.PriorSignalsUnchanged);
        Assert.True(proof.PriorEvaluationUnchanged);
        Assert.True(proof.SourceQuoteTimestampsNotAfterGrid);
        Assert.True(proof.SourceAvailabilityNotAfterGrid);
        Assert.True(proof.EvaluationStartsAfterSignal);
        Assert.True(proof.BetaWindowEndsBeforeSignal);
        Assert.True(proof.ResidualZScoreUsesPriorResiduals);

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
            dateRange = new { startUtc = LockedStartUtc, endUtc = LockedEndUtc },
            gridInterval = GridInterval.ToString(),
            maxQuoteAge = GridInterval.ToString(),
            availabilityMode = FxBboResearchAvailabilityMode.EventTimestampPlusConfiguredDelay.ToString(),
            assumedAvailabilityDelay = AvailabilityDelay.ToString(),
            loaderRowsValidTotal = quotes.Length,
            loaderRowsValidBySymbol = quotes.GroupBy(x => x.Symbol).ToDictionary(x => x.Key, x => x.Count()),
            loaderReasonCounts = load.Diagnostics.GroupBy(x => x.Reason.ToString()).ToDictionary(x => x.Key, x => x.Count()),
            quoteTimestampRangeBySymbol = TimestampRanges(quotes.Select(x => (x.Symbol, x.TimestampUtc))),
            simulatedAvailabilityRangeBySymbol = TimestampRanges(quotes.Select(x => (x.Symbol, TimestampUtc: x.AvailableAtUtc!.Value))),
            samplerReasonCounts = pipeline.Sample.ReasonCounts.ToDictionary(x => x.Key.ToString(), x => x.Value),
            sampledObservationsCount = pipeline.Observations.Count,
            gridTimestampsConsidered = CountGridTimestamps(LockedStartUtc, LockedEndUtc, GridInterval),
            candidateResidualRows = pipeline.Signals.Count,
            rowsAfterRegressionWarmup = residualDiagnostics.RowsAfterRegressionWarmup,
            rowsAfterResidualZScoreWarmup = residualDiagnostics.RowsAfterResidualZScoreWarmup,
            acceptedSignalsCount = pipeline.Summary.AcceptedSignalsCount,
            residualReasonCounts = pipeline.Summary.ReasonCodeDistribution.ToDictionary(x => x.Key.ToString(), x => x.Value),
            longCount = pipeline.Summary.LongCount,
            shortCount = pipeline.Summary.ShortCount,
            residualZScoreMin = residualDiagnostics.ZScoreMin,
            residualZScoreMax = residualDiagnostics.ZScoreMax,
            residualZScoreMean = residualDiagnostics.ZScoreMean,
            residualZScoreMedian = residualDiagnostics.ZScoreMedian,
            maxAbsoluteResidualZScore = residualDiagnostics.MaxAbsoluteZScore,
            zscoreGe0_5 = residualDiagnostics.ZScoreGe0_5,
            zscoreGe1_0 = residualDiagnostics.ZScoreGe1_0,
            zscoreGe1_5 = residualDiagnostics.ZScoreGe1_5,
            zscoreGe2_0 = residualDiagnostics.ZScoreGe2_0,
            zScoreBuckets = ZScoreBuckets(pipeline.Signals),
            betaFitFailureCount = CountReason(pipeline.Signals, FxResidualDivergenceReasonCode.RegressionFitFailed),
            excessiveBetaCount = CountReason(pipeline.Signals, FxResidualDivergenceReasonCode.BetaMagnitudeTooLarge),
            insufficientHistoryCount = CountReason(pipeline.Signals, FxResidualDivergenceReasonCode.InsufficientHistory),
            residualSigmaUnavailableCount = CountReason(pipeline.Signals, FxResidualDivergenceReasonCode.ResidualSigmaUnavailable),
            zScoreTooSmallCount = CountReason(pipeline.Signals, FxResidualDivergenceReasonCode.ResidualZScoreTooSmall),
            diagnosticMetricsAvailable = pipeline.Summary.Lines.Count > 0,
            evaluatedSignalsCount = pipeline.Summary.Lines.Count,
            meanDiagnosticReturn = pipeline.Summary.MeanDiagnosticReturn,
            medianDiagnosticReturn = pipeline.Summary.MedianDiagnosticReturn,
            hitRate = pipeline.Summary.HitRate,
            cumulativeDiagnosticReturn = pipeline.Summary.CumulativeDiagnosticReturn,
            maxDrawdown = pipeline.Summary.MaxDrawdown,
            localForwardLookingProof = proof,
            metricsTrusted = proof.Passed,
            datasetTruncatedWarning = false,
            availabilitySimulatedWarning = true,
            recommendedNextPackage = RecommendNextPackage(pipeline.Summary.AcceptedSignalsCount, pipeline.Summary.Lines.Count, residualDiagnostics.MaxAbsoluteZScore, pipeline.Summary.MeanDiagnosticReturn)
        });
    }

    [Fact]
    public void Synthetic_r015_availability_and_quote_boundaries_are_enforced()
    {
        var grid = LockedStartUtc.AddMinutes(5);
        var quotes = new[]
        {
            Quote("C:EURUSD", grid, 1.1000m, availableAtUtc: grid.AddSeconds(5)),
            Quote("C:GBPUSD", grid.AddSeconds(1), 1.2500m, availableAtUtc: grid.AddSeconds(1)),
            Quote("C:AUDUSD", grid, 0.6500m, availableAtUtc: grid)
        };

        var sampleAtGrid = Sample(quotes, grid, grid);
        var sampleAfterDelay = Sample(
            [
                Quote("C:EURUSD", grid, 1.1000m, availableAtUtc: grid),
                Quote("C:GBPUSD", grid, 1.2500m, availableAtUtc: grid),
                Quote("C:AUDUSD", grid, 0.6500m, availableAtUtc: grid)
            ],
            grid,
            grid);

        Assert.Empty(sampleAtGrid.Observations);
        Assert.Contains(sampleAtGrid.Diagnostics, x => x.Symbol == "C:EURUSD" && x.Reason == FxBboSamplingRejectReasonResearch.MissingQuoteAtOrBeforeGridTime);
        Assert.Contains(sampleAtGrid.Diagnostics, x => x.Symbol == "C:GBPUSD" && x.Reason == FxBboSamplingRejectReasonResearch.MissingQuoteAtOrBeforeGridTime);
        Assert.Single(sampleAfterDelay.Observations);
    }

    [Fact]
    public void Synthetic_r015_diagnostic_evaluation_starts_at_next_grid_step()
    {
        var quotes = SyntheticScenario(length: 275, shockIndex: 250, shockReturn: 0.012);
        var pipeline = RunPipeline(quotes, LockedStartUtc, LockedStartUtc.AddMinutes(5 * 274));

        var line = Assert.Single(pipeline.Summary.Lines);

        Assert.True(line.EvaluationStartUtc > line.Signal.TimestampUtc);
        Assert.Equal(line.Signal.TimestampUtc.Add(GridInterval), line.EvaluationStartUtc);
        Assert.Equal(FxResidualDivergenceEligibleTiming.NextBarOnly, line.Signal.EligibleExecutionTiming);
    }

    [Fact]
    public void Synthetic_r015_future_rows_do_not_change_prior_pipeline_or_contained_evaluation()
    {
        var quotes = SyntheticScenario(length: 285, shockIndex: 250, shockReturn: 0.012);
        var baseline = RunPipeline(quotes, LockedStartUtc, LockedStartUtc.AddMinutes(5 * 284));
        var cutoff = LockedStartUtc.AddMinutes(5 * 260);
        var mutatedQuotes = quotes
            .Select(quote => quote.TimestampUtc > cutoff
                ? quote with { Bid = quote.Bid * 1.03m, Ask = quote.Ask * 1.03m }
                : quote)
            .ToArray();
        var mutated = RunPipeline(mutatedQuotes, LockedStartUtc, LockedStartUtc.AddMinutes(5 * 284));

        Assert.Equal(HashObservations(baseline.Observations.Where(x => x.TimestampUtc <= cutoff)), HashObservations(mutated.Observations.Where(x => x.TimestampUtc <= cutoff)));
        Assert.Equal(HashSignals(baseline.Signals.Where(x => x.TimestampUtc <= cutoff)), HashSignals(mutated.Signals.Where(x => x.TimestampUtc <= cutoff)));
        Assert.Equal(
            HashEvaluationLines(baseline.Summary.Lines.Where(x => x.EvaluationEndUtc <= cutoff)),
            HashEvaluationLines(mutated.Summary.Lines.Where(x => x.EvaluationEndUtc <= cutoff)));
    }

    [Fact]
    public void R015_run_code_does_not_bind_execution_sizing_or_production_outputs()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "tests", "QQ.Production.Intraday.Tests.Unit", "FxResidualDivergencePreregisteredEvalR015RunTests.cs"));

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
        Assert.True(File.Exists(manifestPath), "The R015 approved manifest must exist.");
        var load = new FxBboOfflineResearchQuoteLoader().Load(new(
            ManifestPath: manifestPath,
            ValidationTimestampUtc: ValidationTime,
            AllowLocalEvaluation: true));
        Assert.Equal(FxBboResearchDataAuthorizationStatus.Authorized, load.Status);
        return load;
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

    private static FutureMutationProof RunFutureMutationProof(
        IReadOnlyList<FxBboQuoteResearch> quotes,
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyList<FxSynchronizedMidpointObservationResearch> observations)
    {
        if (observations.Count == 0)
        {
            return FutureMutationProof.Failed("No synchronized observations were available for proof.");
        }

        var cutoffIndex = Math.Min(Math.Max(300, observations.Count / 2), observations.Count - 1);
        var cutoff = observations[cutoffIndex].TimestampUtc;
        var original = RunPipeline(quotes, start, end);
        var mutatedQuotes = quotes
            .Select(quote => quote.TimestampUtc > cutoff
                ? quote with { Bid = quote.Bid * 1.0003m, Ask = quote.Ask * 1.0003m }
                : quote)
            .ToArray();
        var mutated = RunPipeline(mutatedQuotes, start, end);

        var originalObservationHash = HashObservations(original.Observations.Where(x => x.TimestampUtc <= cutoff));
        var mutatedObservationHash = HashObservations(mutated.Observations.Where(x => x.TimestampUtc <= cutoff));
        var originalSignalHash = HashSignals(original.Signals.Where(x => x.TimestampUtc <= cutoff));
        var mutatedSignalHash = HashSignals(mutated.Signals.Where(x => x.TimestampUtc <= cutoff));
        var originalEvaluationHash = HashEvaluationLines(original.Summary.Lines.Where(x => x.EvaluationEndUtc <= cutoff));
        var mutatedEvaluationHash = HashEvaluationLines(mutated.Summary.Lines.Where(x => x.EvaluationEndUtc <= cutoff));

        var availabilityBySymbolTimestamp = quotes
            .Where(x => x.AvailableAtUtc is not null)
            .GroupBy(x => (x.Symbol, x.TimestampUtc))
            .ToDictionary(x => x.Key, x => x.Min(y => y.AvailableAtUtc!.Value));

        var sourceTimesOk = original.Observations.All(observation =>
            observation.SourceQuoteTimestampsUtc.All(source => source.Value <= observation.TimestampUtc));
        var availabilityOk = original.Observations.All(observation =>
            observation.SourceQuoteTimestampsUtc.All(source =>
                availabilityBySymbolTimestamp.TryGetValue((source.Key, source.Value), out var availableAtUtc) &&
                availableAtUtc <= observation.TimestampUtc));
        var evaluationOk = original.Summary.Lines.All(line => line.EvaluationStartUtc > line.Signal.TimestampUtc);
        var betaWindowOk = original.Signals
            .Where(x => x.RegressionWindowEndUtc is not null)
            .All(x => x.RegressionWindowEndUtc < x.TimestampUtc);
        var residualZOk = original.Signals
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

    private static string RecommendNextPackage(int acceptedSignals, int evaluatedSignals, double? maxAbsoluteZScore, double meanDiagnosticReturn)
    {
        if (acceptedSignals > 0 && evaluatedSignals > 0 && meanDiagnosticReturn > 0.0)
        {
            return "NEXT_INTRADAY_FX_RESIDUAL_DIVERGENCE_EXTENDED_PREREGISTERED_BACKFILL_R016";
        }

        if (acceptedSignals > 0 && evaluatedSignals > 0)
        {
            return "NEXT_INTRADAY_FX_RESIDUAL_DIVERGENCE_MODEL_DIAGNOSTICS_R016";
        }

        if (acceptedSignals == 0 && maxAbsoluteZScore is >= 1.75)
        {
            return "NEXT_INTRADAY_FX_RESIDUAL_DIVERGENCE_PREREGISTERED_SENSITIVITY_R016";
        }

        return "NEXT_INTRADAY_FX_RESIDUAL_DIVERGENCE_MODEL_DIAGNOSTICS_R016";
    }

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
                _ => "2.5_plus"
            })
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);

    private static int CountReason(IReadOnlyList<FxResidualDivergenceResearchSignal> signals, FxResidualDivergenceReasonCode reason)
        => signals.Count(x => x.ReasonCode == reason);

    private static double Median(IReadOnlyList<double> sortedValues)
    {
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
        return new(symbol, timestampUtc, midpoint - halfSpread, midpoint + halfSpread, AvailableAtUtc: availableAtUtc, Source: "SyntheticR015");
    }

    private static IReadOnlyList<FxBboQuoteResearch> SyntheticScenario(int length, int? shockIndex, double shockReturn = 0.0)
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

        return QuotesFromReturns("C:EURUSD", target, 1.1000m)
            .Concat(QuotesFromReturns("C:GBPUSD", peer1, 1.2500m))
            .Concat(QuotesFromReturns("C:AUDUSD", peer2, 0.6500m))
            .ToArray();
    }

    private static IEnumerable<FxBboQuoteResearch> QuotesFromReturns(string symbol, IReadOnlyList<double> returns, decimal startPrice)
    {
        var price = (double)startPrice;
        for (var index = 0; index < returns.Count; index++)
        {
            if (index > 0)
            {
                price *= Math.Exp(returns[index]);
            }

            var timestamp = LockedStartUtc.AddTicks(GridInterval.Ticks * index);
            yield return Quote(symbol, timestamp, (decimal)price, timestamp.Add(AvailabilityDelay));
        }
    }

    private static string HashObservations(IEnumerable<FxSynchronizedMidpointObservationResearch> observations)
        => Hash(string.Join("\n", observations.Select(observation =>
            $"{observation.TimestampUtc:O}|{string.Join(",", observation.Midpoints.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value:F10}:{observation.SourceQuoteTimestampsUtc[x.Key]:O}:{observation.QuoteAges[x.Key].Ticks}"))}")));

    private static string HashSignals(IEnumerable<FxResidualDivergenceResearchSignal> signals)
        => Hash(string.Join("\n", signals.Select(signal =>
            $"{signal.TimestampUtc:O}|{signal.ReasonCode}|{signal.IsAccepted}|{signal.Direction}|{signal.Residual:F12}|{signal.ResidualZScore:F12}|{signal.PredictedReturn:F12}|{signal.ActualReturn:F12}|{signal.RegressionWindowEndUtc:O}|{string.Join(",", signal.BetaCoefficients.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value:F12}"))}")));

    private static string HashEvaluationLines(IEnumerable<FxResidualDivergenceDiagnosticLine> lines)
        => Hash(string.Join("\n", lines.Select(line =>
            $"{line.Signal.TimestampUtc:O}|{line.EvaluationStartUtc:O}|{line.EvaluationEndUtc:O}|{line.ResidualForwardReturn:F12}|{line.DirectionalDiagnosticReturn:F12}|{line.IsHit}")));

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void WriteRunSummary(string artifactsPath, object summary)
    {
        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(artifactsPath, "local-preregistered-eval-r015-run-summary.json"), json);
    }

    private static string ApprovedManifestPath(string root)
        => Path.Combine(
            root,
            "artifacts",
            "readiness",
            "intraday-fx-residual-divergence-preregistered-eval-r015",
            "POLYGON_FX_TICK_PREREGISTERED_R015_APPROVED.local.json");

    private static string ArtifactsPath(string root)
        => Path.Combine(root, "artifacts", "readiness", "intraday-fx-residual-divergence-preregistered-eval-r015");

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

    private sealed record PipelineResult(
        FxBboSamplingResultResearch Sample,
        IReadOnlyList<FxSynchronizedMidpointObservationResearch> Observations,
        IReadOnlyList<FxResidualDivergenceResearchSignal> Signals,
        FxResidualDivergenceDiagnosticSummary Summary);

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
}

