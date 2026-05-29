using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class FxResidualDivergenceLocalEvaluationR006Tests
{
    private static readonly DateTimeOffset ValidationTime = new(2026, 05, 28, 13, 30, 00, TimeSpan.Zero);
    private static readonly TimeSpan GridInterval = TimeSpan.FromMinutes(5);
    private static readonly string[] LockedSymbols = ["EURUSD", "GBPUSD", "AUDUSD"];
    private static readonly string[] LockedPeers = ["GBPUSD", "AUDUSD"];

    [Fact]
    public void Approved_manifest_runs_tiny_local_diagnostic_or_blocks_before_raw_data_use()
    {
        var root = FindRepoRoot();
        var artifactsPath = Path.Combine(root, "artifacts", "readiness", "intraday-fx-residual-divergence-local-eval-r006");
        Directory.CreateDirectory(artifactsPath);

        var manifestPath = Path.Combine(
            root,
            "artifacts",
            "readiness",
            "intraday-fx-bbo-manifest-generator-r005",
            "POLYGON_FX_BBO_RESEARCH_AUTHORIZATION.APPROVED.local.json");

        var parameterLockPath = Path.Combine(artifactsPath, "PARAMETER_LOCK_R006.md");
        Assert.True(File.Exists(parameterLockPath), "PARAMETER_LOCK_R006.md must exist before approved real files may be opened.");

        if (!File.Exists(manifestPath))
        {
            WriteRunSummary(artifactsPath, new
            {
                approvedManifestFound = false,
                manifestValidationPassed = false,
                realFilesOpened = false,
                localEvaluationRun = false,
                localEvaluationBlocker = "No approved local manifest file exists at the R005 expected path."
            });
            return;
        }

        var onboarding = new FxBboPolygonResearchManifestOnboarding();
        var onboardingResult = onboarding.ValidateManifestCandidates([manifestPath], ValidationTime);
        if (!onboardingResult.ApprovedManifestFound || !onboardingResult.RealDataFilesMayBeOpened)
        {
            WriteRunSummary(artifactsPath, new
            {
                approvedManifestFound = onboardingResult.CandidateManifestPaths.Count > 0,
                manifestValidationPassed = false,
                realFilesOpened = false,
                localEvaluationRun = false,
                localEvaluationBlocker = onboardingResult.BlockerReason,
                diagnostics = onboardingResult.Diagnostics.GroupBy(x => x.Reason.ToString()).ToDictionary(x => x.Key, x => x.Count())
            });
            return;
        }

        var loader = new FxBboOfflineResearchQuoteLoader();
        var load = loader.Load(new(
            ManifestPath: manifestPath,
            ValidationTimestampUtc: ValidationTime,
            AllowLocalEvaluation: true));

        var loaderReasonCounts = load.Diagnostics
            .GroupBy(x => x.Reason.ToString())
            .ToDictionary(x => x.Key, x => x.Count());

        if (load.Status != FxBboResearchDataAuthorizationStatus.Authorized || load.Quotes.Count == 0)
        {
            var onlyMissingFiles = loaderReasonCounts.Count == 1 && loaderReasonCounts.ContainsKey(nameof(FxBboOfflineResearchQuoteLoadRejectReason.FileNotFound));
            var blocker = load.Diagnostics.FirstOrDefault(x => x.Reason != FxBboOfflineResearchQuoteLoadRejectReason.Accepted)?.Message
                ?? load.LocalEvaluationGate.Reason;
            WriteRunSummary(artifactsPath, new
            {
                approvedManifestFound = true,
                manifestValidationPassed = true,
                realFilesOpened = !onlyMissingFiles,
                localEvaluationRun = false,
                localEvaluationBlocker = blocker,
                rawRowsLoaded = load.Quotes.Count,
                loaderReasonCounts
            });
            return;
        }

        var quotes = load.Quotes
            .Where(x => LockedSymbols.Contains(x.Symbol, StringComparer.Ordinal))
            .ToArray();
        if (LockedSymbols.Any(symbol => !quotes.Any(quote => quote.Symbol == symbol)))
        {
            WriteRunSummary(artifactsPath, new
            {
                approvedManifestFound = true,
                manifestValidationPassed = true,
                realFilesOpened = true,
                localEvaluationRun = false,
                localEvaluationBlocker = "Loaded quotes did not include all locked R006 symbols.",
                rawRowsLoaded = load.Quotes.Count,
                selectedRowsLoaded = quotes.Length,
                loaderReasonCounts
            });
            return;
        }

        var start = CeilToGrid(LockedSymbols.Select(symbol => quotes.Where(x => x.Symbol == symbol).Min(x => x.TimestampUtc)).Max(), GridInterval);
        var end = FloorToGrid(LockedSymbols.Select(symbol => quotes.Where(x => x.Symbol == symbol).Max(x => x.TimestampUtc)).Min(), GridInterval);
        if (end <= start)
        {
            WriteRunSummary(artifactsPath, new
            {
                approvedManifestFound = true,
                manifestValidationPassed = true,
                realFilesOpened = true,
                localEvaluationRun = false,
                localEvaluationBlocker = "Locked symbols did not have an overlapping 5-minute grid range.",
                rawRowsLoaded = load.Quotes.Count,
                selectedRowsLoaded = quotes.Length,
                loaderReasonCounts
            });
            return;
        }

        var pipeline = RunPipeline(quotes, start, end);
        var proof = RunFutureMutationProof(quotes, start, end, pipeline.Observations);

        Assert.True(proof.PriorObservationsUnchanged);
        Assert.True(proof.PriorSignalsUnchanged);
        Assert.True(proof.SourceQuoteTimestampsNotAfterGrid);
        Assert.True(proof.SourceAvailabilityNotAfterGrid);
        Assert.True(proof.EvaluationStartsAfterSignal);

        WriteRunSummary(artifactsPath, new
        {
            approvedManifestFound = true,
            manifestValidationPassed = true,
            realFilesOpened = true,
            localEvaluationRun = true,
            localEvaluationBlocker = (string?)null,
            manifestPath,
            symbols = LockedSymbols,
            targetSymbol = LockedSymbols[0],
            peerSymbols = LockedPeers,
            gridInterval = GridInterval.ToString(),
            maxQuoteAge = GridInterval.ToString(),
            startUtc = start,
            endUtc = end,
            rawRowsLoaded = load.Quotes.Count,
            selectedRowsLoaded = quotes.Length,
            loaderReasonCounts,
            samplerReasonCounts = pipeline.Sample.ReasonCounts.ToDictionary(x => x.Key.ToString(), x => x.Value),
            sampledObservationCount = pipeline.Observations.Count,
            residualCandidateCount = pipeline.Signals.Count,
            residualAcceptedSignalCount = pipeline.Summary.AcceptedSignalsCount,
            residualReasonCounts = pipeline.Summary.ReasonCodeDistribution.ToDictionary(x => x.Key.ToString(), x => x.Value),
            longCount = pipeline.Summary.LongCount,
            shortCount = pipeline.Summary.ShortCount,
            meanDiagnosticReturn = pipeline.Summary.MeanDiagnosticReturn,
            medianDiagnosticReturn = pipeline.Summary.MedianDiagnosticReturn,
            hitRate = pipeline.Summary.HitRate,
            cumulativeDiagnosticReturn = pipeline.Summary.CumulativeDiagnosticReturn,
            maxDrawdown = pipeline.Summary.MaxDrawdown,
            zScoreBuckets = ZScoreBuckets(pipeline.Signals),
            localForwardLookingProof = proof
        });
    }

    private static PipelineResult RunPipeline(IReadOnlyList<FxBboQuoteResearch> quotes, DateTimeOffset start, DateTimeOffset end)
    {
        var sampler = new FxBboToSynchronizedMidpointSamplerResearch();
        var sample = sampler.Sample(
            quotes,
            new FxBboSamplingParametersResearch(
                Symbols: LockedSymbols,
                StartUtc: start,
                EndUtc: end,
                GridInterval: GridInterval,
                MaxQuoteAge: GridInterval,
                RequireAllSymbols: true,
                RequireExactGridAlignment: true));

        var bars = sampler.ToResidualDivergenceBars(sample.Observations);
        var parameters = Parameters();
        var strategy = new FxResidualDivergenceResearchStrategy();
        var signals = strategy.GenerateSignals(bars, parameters);
        var summary = new FxResidualDivergenceDiagnosticEvaluator().Evaluate(signals, bars, parameters);

        return new(sample, sample.Observations, signals, summary);
    }

    private static FutureMutationProof RunFutureMutationProof(
        IReadOnlyList<FxBboQuoteResearch> quotes,
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyList<FxSynchronizedMidpointObservationResearch> observations)
    {
        if (observations.Count == 0)
        {
            return new(
                PriorObservationsUnchanged: true,
                PriorSignalsUnchanged: true,
                SourceQuoteTimestampsNotAfterGrid: true,
                SourceAvailabilityNotAfterGrid: true,
                EvaluationStartsAfterSignal: true,
                CutoffUtc: null,
                OriginalObservationHash: null,
                MutatedObservationHash: null,
                OriginalSignalHash: null,
                MutatedSignalHash: null);
        }

        var cutoff = observations[observations.Count / 2].TimestampUtc;
        var original = RunPipeline(quotes, start, end);
        var mutatedQuotes = quotes
            .Select(quote => quote.TimestampUtc > cutoff
                ? quote with { Bid = quote.Bid * 1.0001m, Ask = quote.Ask * 1.0001m }
                : quote)
            .ToArray();
        var mutated = RunPipeline(mutatedQuotes, start, end);

        var originalObservationHash = HashObservations(original.Observations.Where(x => x.TimestampUtc <= cutoff));
        var mutatedObservationHash = HashObservations(mutated.Observations.Where(x => x.TimestampUtc <= cutoff));
        var originalSignalHash = HashSignals(original.Signals.Where(x => x.TimestampUtc <= cutoff));
        var mutatedSignalHash = HashSignals(mutated.Signals.Where(x => x.TimestampUtc <= cutoff));

        var quotesBySymbolTimestamp = quotes
            .GroupBy(x => (x.Symbol, x.TimestampUtc))
            .ToDictionary(x => x.Key, x => x.ToArray());

        var sourceTimesOk = original.Observations.All(observation =>
            observation.SourceQuoteTimestampsUtc.All(source => source.Value <= observation.TimestampUtc));
        var availabilityOk = original.Observations.All(observation =>
            observation.SourceQuoteTimestampsUtc.All(source =>
                quotesBySymbolTimestamp.TryGetValue((source.Key, source.Value), out var candidates) &&
                candidates.Any(candidate => candidate.AvailableAtUtc is not null && candidate.AvailableAtUtc.Value <= observation.TimestampUtc)));
        var evaluationOk = original.Summary.Lines.All(line => line.EvaluationStartUtc > line.Signal.TimestampUtc);

        return new(
            originalObservationHash == mutatedObservationHash,
            originalSignalHash == mutatedSignalHash,
            sourceTimesOk,
            availabilityOk,
            evaluationOk,
            cutoff,
            originalObservationHash,
            mutatedObservationHash,
            originalSignalHash,
            mutatedSignalHash);
    }

    private static FxResidualDivergenceParameters Parameters()
        => new(
            TargetSymbol: "EURUSD",
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

    private static IReadOnlyDictionary<string, int> ZScoreBuckets(IReadOnlyList<FxResidualDivergenceResearchSignal> signals)
        => signals
            .Where(x => x.IsAccepted)
            .GroupBy(x => Math.Abs(x.ResidualZScore) switch
            {
                < 2.5 => "2.0_to_2.5",
                < 3.0 => "2.5_to_3.0",
                _ => "3.0_plus"
            })
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);

    private static string HashObservations(IEnumerable<FxSynchronizedMidpointObservationResearch> observations)
        => Hash(string.Join("\n", observations.Select(observation =>
            $"{observation.TimestampUtc:O}|{string.Join(",", observation.Midpoints.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value:F10}:{observation.SourceQuoteTimestampsUtc[x.Key]:O}:{observation.QuoteAges[x.Key].Ticks}"))}")));

    private static string HashSignals(IEnumerable<FxResidualDivergenceResearchSignal> signals)
        => Hash(string.Join("\n", signals.Select(signal =>
            $"{signal.TimestampUtc:O}|{signal.ReasonCode}|{signal.IsAccepted}|{signal.Direction}|{signal.Residual:F12}|{signal.ResidualZScore:F12}|{signal.PredictedReturn:F12}|{signal.ActualReturn:F12}|{string.Join(",", signal.BetaCoefficients.OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Value:F12}"))}")));

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static DateTimeOffset CeilToGrid(DateTimeOffset value, TimeSpan interval)
    {
        var remainder = value.Ticks % interval.Ticks;
        return remainder == 0
            ? value
            : new DateTimeOffset(value.Ticks + interval.Ticks - remainder, TimeSpan.Zero);
    }

    private static DateTimeOffset FloorToGrid(DateTimeOffset value, TimeSpan interval)
        => new(value.Ticks - (value.Ticks % interval.Ticks), TimeSpan.Zero);

    private static void WriteRunSummary(string artifactsPath, object summary)
    {
        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(artifactsPath, "local-eval-r006-run-summary.json"), json);
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

    private sealed record PipelineResult(
        FxBboSamplingResultResearch Sample,
        IReadOnlyList<FxSynchronizedMidpointObservationResearch> Observations,
        IReadOnlyList<FxResidualDivergenceResearchSignal> Signals,
        FxResidualDivergenceDiagnosticSummary Summary);

    private sealed record FutureMutationProof(
        bool PriorObservationsUnchanged,
        bool PriorSignalsUnchanged,
        bool SourceQuoteTimestampsNotAfterGrid,
        bool SourceAvailabilityNotAfterGrid,
        bool EvaluationStartsAfterSignal,
        DateTimeOffset? CutoffUtc,
        string? OriginalObservationHash,
        string? MutatedObservationHash,
        string? OriginalSignalHash,
        string? MutatedSignalHash);
}
