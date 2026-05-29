namespace QQ.Production.Intraday.Application;

public enum FxResidualDivergenceDirection
{
    None,
    LongResidualReversion,
    ShortResidualReversion
}

public enum FxResidualDivergenceEligibleTiming
{
    NextBarOnly
}

public enum FxResidualDivergenceReasonCode
{
    AcceptedLongResidualReversion,
    AcceptedShortResidualReversion,
    InsufficientHistory,
    InsufficientResidualHistory,
    MissingTargetBar,
    MissingPeerBar,
    TimestampAlignmentFailed,
    NonPositivePrice,
    RegressionFitFailed,
    ResidualSigmaUnavailable,
    ResidualZScoreTooSmall,
    BetaMagnitudeTooLarge,
    PeerCountTooLow,
    SpreadTooWide,
    UnknownTimestampSemantics,
    UnsafeDataSurface,
    BlockedNoSafeFxData,
    BlockedNoSafeResearchLocation
}

public sealed record FxResidualDivergenceBar(
    string Symbol,
    DateTimeOffset TimestampUtc,
    decimal CloseOrMid,
    bool IsCompletedBar = true,
    decimal? Open = null,
    decimal? High = null,
    decimal? Low = null,
    decimal? Volume = null,
    decimal? Bid = null,
    decimal? Ask = null,
    decimal? SpreadBps = null,
    string? SessionDate = null,
    string? ExchangeTimeZone = null);

public sealed record FxResidualDivergenceParameters(
    string TargetSymbol,
    IReadOnlyList<string> PeerSymbols,
    int RegressionLookbackBars = 120,
    int ResidualZLookbackBars = 120,
    int MinRegressionObservations = 60,
    double EntryZScore = 2.0,
    double ExitZScore = 0.5,
    double MaxAbsBeta = 5.0,
    int MinPeerCount = 2,
    double MaxMissingPeerFraction = 0.0,
    bool RequireExactTimestampAlignment = true,
    int EvaluationHorizonBars = 5,
    bool OneSignalPerSymbolPerTimestamp = true,
    decimal? MaxSpreadBps = null,
    double? MinRollingVolBps = null,
    double? MaxRollingVolBps = null);

public sealed record FxResidualDivergenceResearchSignal(
    string TargetSymbol,
    DateTimeOffset TimestampUtc,
    FxResidualDivergenceDirection Direction,
    double Residual,
    double ResidualZScore,
    double PredictedReturn,
    double ActualReturn,
    IReadOnlyList<string> PeerSymbolsUsed,
    IReadOnlyDictionary<string, double> BetaCoefficients,
    FxResidualDivergenceReasonCode ReasonCode,
    bool IsAccepted,
    FxResidualDivergenceEligibleTiming EligibleExecutionTiming,
    bool DiagnosticOnly,
    double ResidualMean,
    double ResidualSigma,
    int RegressionObservationCount,
    DateTimeOffset? RegressionWindowEndUtc);

public sealed record FxResidualDivergenceDiagnosticLine(
    FxResidualDivergenceResearchSignal Signal,
    DateTimeOffset EvaluationStartUtc,
    DateTimeOffset EvaluationEndUtc,
    double ResidualForwardReturn,
    double DirectionalDiagnosticReturn,
    bool IsHit);

public sealed record FxResidualDivergenceDiagnosticSummary(
    int CandidateRowsCount,
    int AcceptedSignalsCount,
    IReadOnlyDictionary<FxResidualDivergenceReasonCode, int> ReasonCodeDistribution,
    int LongCount,
    int ShortCount,
    double MeanDiagnosticReturn,
    double MedianDiagnosticReturn,
    double HitRate,
    double CumulativeDiagnosticReturn,
    double MaxDrawdown,
    IReadOnlyList<FxResidualDivergenceDiagnosticLine> Lines);

public sealed class FxResidualDivergenceResearchStrategy
{
    private const double Epsilon = 1e-12;

    public IReadOnlyList<FxResidualDivergenceResearchSignal> GenerateSignals(
        IReadOnlyList<FxResidualDivergenceBar> bars,
        FxResidualDivergenceParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(bars);
        ArgumentNullException.ThrowIfNull(parameters);

        var targetSymbol = NormalizeSymbol(parameters.TargetSymbol);
        var peerSymbols = parameters.PeerSymbols.Select(NormalizeSymbol).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).ToArray();

        if (targetSymbol.Length == 0 || !ValidateParameterShape(parameters, peerSymbols))
        {
            return [];
        }

        var grouped = bars
            .Select(x => x with { Symbol = NormalizeSymbol(x.Symbol) })
            .Where(x => x.Symbol.Length > 0)
            .GroupBy(x => x.Symbol, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);

        if (!grouped.TryGetValue(targetSymbol, out var targetBars) || targetBars.Length == 0)
        {
            return [];
        }

        if (peerSymbols.Length < parameters.MinPeerCount)
        {
            return BuildTargetRejections(targetBars, targetSymbol, FxResidualDivergenceReasonCode.PeerCountTooLow);
        }

        foreach (var symbol in peerSymbols)
        {
            if (!grouped.ContainsKey(symbol))
            {
                return BuildTargetRejections(targetBars, targetSymbol, FxResidualDivergenceReasonCode.MissingPeerBar);
            }
        }

        var allSymbols = new[] { targetSymbol }.Concat(peerSymbols).ToArray();
        var validationIssue = ValidateBars(grouped, allSymbols, out var sortedBySymbol);
        if (validationIssue is not null)
        {
            return BuildTargetRejections(targetBars, targetSymbol, validationIssue.Value);
        }

        var returnsBySymbol = sortedBySymbol.ToDictionary(
            x => x.Key,
            x => BuildReturns(x.Value),
            StringComparer.Ordinal);

        if (!returnsBySymbol.TryGetValue(targetSymbol, out var targetReturns) || targetReturns.Count == 0)
        {
            return [];
        }

        var peerReturnMaps = peerSymbols.ToDictionary(
            x => x,
            x => returnsBySymbol[x].ToDictionary(y => y.TimestampUtc, y => y.LogReturn),
            StringComparer.Ordinal);

        var barsBySymbolTimestamp = sortedBySymbol.ToDictionary(
            x => x.Key,
            x => x.Value.ToDictionary(y => y.TimestampUtc),
            StringComparer.Ordinal);

        var targetReturnMap = targetReturns.ToDictionary(x => x.TimestampUtc, x => x.LogReturn);
        var residualHistory = new List<ResidualObservation>();
        var signals = new List<FxResidualDivergenceResearchSignal>();

        foreach (var current in targetReturns.OrderBy(x => x.TimestampUtc))
        {
            if (!HasCurrentPeerReturns(peerSymbols, peerReturnMaps, current.TimestampUtc))
            {
                signals.Add(Rejected(targetSymbol, current.TimestampUtc, FxResidualDivergenceReasonCode.MissingPeerBar));
                continue;
            }

            var targetBar = barsBySymbolTimestamp[targetSymbol][current.TimestampUtc];
            if (parameters.MaxSpreadBps is not null &&
                targetBar.SpreadBps is not null &&
                targetBar.SpreadBps.Value > parameters.MaxSpreadBps.Value)
            {
                signals.Add(Rejected(targetSymbol, current.TimestampUtc, FxResidualDivergenceReasonCode.SpreadTooWide));
                continue;
            }

            var fitRows = BuildRegressionRows(
                    peerSymbols,
                    targetReturnMap,
                    peerReturnMaps,
                    current.TimestampUtc,
                    parameters.RegressionLookbackBars)
                .ToArray();

            if (fitRows.Length < parameters.MinRegressionObservations)
            {
                signals.Add(Rejected(targetSymbol, current.TimestampUtc, FxResidualDivergenceReasonCode.InsufficientHistory));
                continue;
            }

            if (!FitOls(fitRows, peerSymbols.Length, out var beta))
            {
                signals.Add(Rejected(targetSymbol, current.TimestampUtc, FxResidualDivergenceReasonCode.RegressionFitFailed));
                continue;
            }

            if (beta.Skip(1).Any(x => Math.Abs(x) > parameters.MaxAbsBeta))
            {
                signals.Add(Rejected(
                    targetSymbol,
                    current.TimestampUtc,
                    FxResidualDivergenceReasonCode.BetaMagnitudeTooLarge,
                    beta: ToBetaDictionary(peerSymbols, beta),
                    actualReturn: current.LogReturn,
                    regressionObservationCount: fitRows.Length,
                    regressionWindowEndUtc: fitRows[^1].TimestampUtc));
                continue;
            }

            var currentPeers = peerSymbols.Select(x => peerReturnMaps[x][current.TimestampUtc]).ToArray();
            var predicted = Predict(beta, currentPeers);
            var residual = current.LogReturn - predicted;

            if (residualHistory.Count < parameters.ResidualZLookbackBars)
            {
                residualHistory.Add(new ResidualObservation(current.TimestampUtc, residual));
                signals.Add(Rejected(
                    targetSymbol,
                    current.TimestampUtc,
                    FxResidualDivergenceReasonCode.InsufficientResidualHistory,
                    residual: residual,
                    predictedReturn: predicted,
                    actualReturn: current.LogReturn,
                    beta: ToBetaDictionary(peerSymbols, beta),
                    regressionObservationCount: fitRows.Length,
                    regressionWindowEndUtc: fitRows[^1].TimestampUtc));
                continue;
            }

            var zWindow = residualHistory.TakeLast(parameters.ResidualZLookbackBars).Select(x => x.Residual).ToArray();
            var residualMean = zWindow.Average();
            var residualSigma = SampleStandardDeviation(zWindow);
            if (residualSigma <= Epsilon)
            {
                residualHistory.Add(new ResidualObservation(current.TimestampUtc, residual));
                signals.Add(Rejected(
                    targetSymbol,
                    current.TimestampUtc,
                    FxResidualDivergenceReasonCode.ResidualSigmaUnavailable,
                    residual: residual,
                    predictedReturn: predicted,
                    actualReturn: current.LogReturn,
                    beta: ToBetaDictionary(peerSymbols, beta),
                    residualMean: residualMean,
                    residualSigma: residualSigma,
                    regressionObservationCount: fitRows.Length,
                    regressionWindowEndUtc: fitRows[^1].TimestampUtc));
                continue;
            }

            var zScore = (residual - residualMean) / residualSigma;
            var betaDictionary = ToBetaDictionary(peerSymbols, beta);

            FxResidualDivergenceResearchSignal signal;
            if (zScore >= parameters.EntryZScore)
            {
                signal = new FxResidualDivergenceResearchSignal(
                    targetSymbol,
                    current.TimestampUtc,
                    FxResidualDivergenceDirection.ShortResidualReversion,
                    residual,
                    zScore,
                    predicted,
                    current.LogReturn,
                    peerSymbols,
                    betaDictionary,
                    FxResidualDivergenceReasonCode.AcceptedShortResidualReversion,
                    IsAccepted: true,
                    FxResidualDivergenceEligibleTiming.NextBarOnly,
                    DiagnosticOnly: true,
                    residualMean,
                    residualSigma,
                    fitRows.Length,
                    fitRows[^1].TimestampUtc);
            }
            else if (zScore <= -parameters.EntryZScore)
            {
                signal = new FxResidualDivergenceResearchSignal(
                    targetSymbol,
                    current.TimestampUtc,
                    FxResidualDivergenceDirection.LongResidualReversion,
                    residual,
                    zScore,
                    predicted,
                    current.LogReturn,
                    peerSymbols,
                    betaDictionary,
                    FxResidualDivergenceReasonCode.AcceptedLongResidualReversion,
                    IsAccepted: true,
                    FxResidualDivergenceEligibleTiming.NextBarOnly,
                    DiagnosticOnly: true,
                    residualMean,
                    residualSigma,
                    fitRows.Length,
                    fitRows[^1].TimestampUtc);
            }
            else
            {
                signal = Rejected(
                    targetSymbol,
                    current.TimestampUtc,
                    FxResidualDivergenceReasonCode.ResidualZScoreTooSmall,
                    residual: residual,
                    zScore: zScore,
                    predictedReturn: predicted,
                    actualReturn: current.LogReturn,
                    beta: betaDictionary,
                    residualMean: residualMean,
                    residualSigma: residualSigma,
                    regressionObservationCount: fitRows.Length,
                    regressionWindowEndUtc: fitRows[^1].TimestampUtc);
            }

            signals.Add(signal);
            residualHistory.Add(new ResidualObservation(current.TimestampUtc, residual));
        }

        return signals;
    }

    private static bool ValidateParameterShape(FxResidualDivergenceParameters parameters, IReadOnlyList<string> peerSymbols)
        => parameters.RegressionLookbackBars > 0 &&
           parameters.ResidualZLookbackBars > 1 &&
           parameters.MinRegressionObservations > 0 &&
           parameters.MinRegressionObservations <= parameters.RegressionLookbackBars &&
           parameters.EntryZScore > 0 &&
           parameters.MaxAbsBeta > 0 &&
           parameters.MinPeerCount > 0 &&
           peerSymbols.Count > 0 &&
           parameters.MaxMissingPeerFraction == 0.0 &&
           parameters.RequireExactTimestampAlignment &&
           parameters.EvaluationHorizonBars > 0;

    private static FxResidualDivergenceReasonCode? ValidateBars(
        IReadOnlyDictionary<string, FxResidualDivergenceBar[]> grouped,
        IReadOnlyList<string> symbols,
        out Dictionary<string, FxResidualDivergenceBar[]> sortedBySymbol)
    {
        sortedBySymbol = new Dictionary<string, FxResidualDivergenceBar[]>(StringComparer.Ordinal);
        foreach (var symbol in symbols)
        {
            var bars = grouped[symbol];
            if (bars.Any(x => x.TimestampUtc.Offset != TimeSpan.Zero || !x.IsCompletedBar))
            {
                return FxResidualDivergenceReasonCode.UnknownTimestampSemantics;
            }

            if (bars.Any(x => x.CloseOrMid <= 0m || x.Bid is <= 0m || x.Ask is <= 0m || x.SpreadBps is < 0m))
            {
                return FxResidualDivergenceReasonCode.NonPositivePrice;
            }

            if (bars.GroupBy(x => x.TimestampUtc).Any(x => x.Count() > 1))
            {
                return FxResidualDivergenceReasonCode.TimestampAlignmentFailed;
            }

            sortedBySymbol[symbol] = bars.OrderBy(x => x.TimestampUtc).ToArray();
        }

        return null;
    }

    private static IReadOnlyList<FxResidualDivergenceResearchSignal> BuildTargetRejections(
        IReadOnlyList<FxResidualDivergenceBar> targetBars,
        string targetSymbol,
        FxResidualDivergenceReasonCode reason)
        => targetBars
            .Where(x => x.TimestampUtc.Offset == TimeSpan.Zero)
            .OrderBy(x => x.TimestampUtc)
            .Skip(1)
            .Select(x => Rejected(targetSymbol, x.TimestampUtc, reason))
            .ToArray();

    private static IReadOnlyList<ReturnObservation> BuildReturns(IReadOnlyList<FxResidualDivergenceBar> bars)
    {
        var rows = new List<ReturnObservation>();
        for (var index = 1; index < bars.Count; index++)
        {
            var previous = (double)bars[index - 1].CloseOrMid;
            var current = (double)bars[index].CloseOrMid;
            rows.Add(new ReturnObservation(bars[index].TimestampUtc, Math.Log(current / previous)));
        }

        return rows;
    }

    private static bool HasCurrentPeerReturns(
        IReadOnlyList<string> peerSymbols,
        IReadOnlyDictionary<string, Dictionary<DateTimeOffset, double>> peerReturnMaps,
        DateTimeOffset timestampUtc)
        => peerSymbols.All(symbol => peerReturnMaps[symbol].ContainsKey(timestampUtc));

    private static IEnumerable<RegressionRow> BuildRegressionRows(
        IReadOnlyList<string> peerSymbols,
        IReadOnlyDictionary<DateTimeOffset, double> targetReturnMap,
        IReadOnlyDictionary<string, Dictionary<DateTimeOffset, double>> peerReturnMaps,
        DateTimeOffset decisionTimestampUtc,
        int lookback)
    {
        return targetReturnMap
            .Where(x => x.Key < decisionTimestampUtc)
            .OrderBy(x => x.Key)
            .Where(x => peerSymbols.All(symbol => peerReturnMaps[symbol].ContainsKey(x.Key)))
            .TakeLast(lookback)
            .Select(x => new RegressionRow(
                x.Key,
                x.Value,
                peerSymbols.Select(symbol => peerReturnMaps[symbol][x.Key]).ToArray()));
    }

    private static bool FitOls(IReadOnlyList<RegressionRow> rows, int peerCount, out double[] beta)
    {
        var dimension = peerCount + 1;
        var xtx = new double[dimension, dimension];
        var xty = new double[dimension];

        foreach (var row in rows)
        {
            var x = new double[dimension];
            x[0] = 1.0;
            for (var index = 0; index < peerCount; index++)
            {
                x[index + 1] = row.PeerReturns[index];
            }

            for (var r = 0; r < dimension; r++)
            {
                xty[r] += x[r] * row.TargetReturn;
                for (var c = 0; c < dimension; c++)
                {
                    xtx[r, c] += x[r] * x[c];
                }
            }
        }

        return SolveLinearSystem(xtx, xty, out beta);
    }

    private static bool SolveLinearSystem(double[,] matrix, double[] vector, out double[] solution)
    {
        var n = vector.Length;
        var a = new double[n, n + 1];
        for (var r = 0; r < n; r++)
        {
            for (var c = 0; c < n; c++)
            {
                a[r, c] = matrix[r, c];
            }

            a[r, n] = vector[r];
        }

        for (var pivot = 0; pivot < n; pivot++)
        {
            var bestRow = pivot;
            var best = Math.Abs(a[pivot, pivot]);
            for (var r = pivot + 1; r < n; r++)
            {
                var value = Math.Abs(a[r, pivot]);
                if (value > best)
                {
                    best = value;
                    bestRow = r;
                }
            }

            if (best <= Epsilon)
            {
                solution = [];
                return false;
            }

            if (bestRow != pivot)
            {
                for (var c = pivot; c <= n; c++)
                {
                    (a[pivot, c], a[bestRow, c]) = (a[bestRow, c], a[pivot, c]);
                }
            }

            var divisor = a[pivot, pivot];
            for (var c = pivot; c <= n; c++)
            {
                a[pivot, c] /= divisor;
            }

            for (var r = 0; r < n; r++)
            {
                if (r == pivot)
                {
                    continue;
                }

                var factor = a[r, pivot];
                for (var c = pivot; c <= n; c++)
                {
                    a[r, c] -= factor * a[pivot, c];
                }
            }
        }

        solution = new double[n];
        for (var r = 0; r < n; r++)
        {
            solution[r] = a[r, n];
        }

        return true;
    }

    private static double Predict(IReadOnlyList<double> beta, IReadOnlyList<double> peers)
    {
        var value = beta[0];
        for (var index = 0; index < peers.Count; index++)
        {
            value += beta[index + 1] * peers[index];
        }

        return value;
    }

    private static IReadOnlyDictionary<string, double> ToBetaDictionary(IReadOnlyList<string> peers, IReadOnlyList<double> beta)
    {
        var values = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["Intercept"] = beta.Count == 0 ? 0.0 : beta[0]
        };

        for (var index = 0; index < peers.Count && index + 1 < beta.Count; index++)
        {
            values[peers[index]] = beta[index + 1];
        }

        return values;
    }

    private static double SampleStandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
        {
            return 0.0;
        }

        var mean = values.Average();
        var variance = values.Sum(x => Math.Pow(x - mean, 2)) / (values.Count - 1);
        return Math.Sqrt(variance);
    }

    private static FxResidualDivergenceResearchSignal Rejected(
        string targetSymbol,
        DateTimeOffset timestampUtc,
        FxResidualDivergenceReasonCode reason,
        double residual = 0.0,
        double zScore = 0.0,
        double predictedReturn = 0.0,
        double actualReturn = 0.0,
        IReadOnlyDictionary<string, double>? beta = null,
        double residualMean = 0.0,
        double residualSigma = 0.0,
        int regressionObservationCount = 0,
        DateTimeOffset? regressionWindowEndUtc = null)
        => new(
            targetSymbol,
            timestampUtc,
            FxResidualDivergenceDirection.None,
            residual,
            zScore,
            predictedReturn,
            actualReturn,
            [],
            beta ?? new Dictionary<string, double>(),
            reason,
            IsAccepted: false,
            FxResidualDivergenceEligibleTiming.NextBarOnly,
            DiagnosticOnly: true,
            residualMean,
            residualSigma,
            regressionObservationCount,
            regressionWindowEndUtc);

    private static string NormalizeSymbol(string? symbol)
        => string.IsNullOrWhiteSpace(symbol)
            ? string.Empty
            : symbol.Replace("/", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();

    private sealed record ReturnObservation(DateTimeOffset TimestampUtc, double LogReturn);
    private sealed record ResidualObservation(DateTimeOffset TimestampUtc, double Residual);
    private sealed record RegressionRow(DateTimeOffset TimestampUtc, double TargetReturn, double[] PeerReturns);
}

public sealed class FxResidualDivergenceDiagnosticEvaluator
{
    public FxResidualDivergenceDiagnosticSummary Evaluate(
        IReadOnlyList<FxResidualDivergenceResearchSignal> signals,
        IReadOnlyList<FxResidualDivergenceBar> bars,
        FxResidualDivergenceParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(bars);
        ArgumentNullException.ThrowIfNull(parameters);

        var targetSymbol = parameters.TargetSymbol.Replace("/", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
        var peerSymbols = parameters.PeerSymbols
            .Select(x => x.Replace("/", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var bySymbol = bars
            .Select(x => x with { Symbol = x.Symbol.Replace("/", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant() })
            .Where(x => x.TimestampUtc.Offset == TimeSpan.Zero && x.IsCompletedBar && x.CloseOrMid > 0m)
            .GroupBy(x => x.Symbol, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.OrderBy(y => y.TimestampUtc).ToArray(), StringComparer.Ordinal);

        if (!bySymbol.TryGetValue(targetSymbol, out var targetBars))
        {
            return Empty(signals);
        }

        var targetByTime = targetBars.ToDictionary(x => x.TimestampUtc);
        var peerByTime = peerSymbols
            .Where(bySymbol.ContainsKey)
            .ToDictionary(
                x => x,
                x => bySymbol[x].ToDictionary(y => y.TimestampUtc),
                StringComparer.Ordinal);

        var lines = new List<FxResidualDivergenceDiagnosticLine>();
        foreach (var signal in signals.Where(x => x.IsAccepted).OrderBy(x => x.TimestampUtc))
        {
            var signalIndex = Array.FindIndex(targetBars, x => x.TimestampUtc == signal.TimestampUtc);
            if (signalIndex < 0 || signalIndex + parameters.EvaluationHorizonBars >= targetBars.Length)
            {
                continue;
            }

            var start = targetBars[signalIndex + 1];
            var end = targetBars[signalIndex + parameters.EvaluationHorizonBars];
            var targetForward = Math.Log((double)(end.CloseOrMid / targetByTime[signal.TimestampUtc].CloseOrMid));
            var peerForward = 0.0;

            var peersAvailable = true;
            foreach (var peer in peerSymbols)
            {
                if (!peerByTime.TryGetValue(peer, out var peerMap) ||
                    !peerMap.TryGetValue(signal.TimestampUtc, out var peerAtSignal) ||
                    !peerMap.TryGetValue(end.TimestampUtc, out var peerAtEnd) ||
                    !signal.BetaCoefficients.TryGetValue(peer, out var beta))
                {
                    peersAvailable = false;
                    break;
                }

                peerForward += beta * Math.Log((double)(peerAtEnd.CloseOrMid / peerAtSignal.CloseOrMid));
            }

            if (!peersAvailable)
            {
                continue;
            }

            var intercept = signal.BetaCoefficients.TryGetValue("Intercept", out var b0) ? b0 : 0.0;
            var residualForward = targetForward - ((intercept * parameters.EvaluationHorizonBars) + peerForward);
            var unitDirection = signal.Direction == FxResidualDivergenceDirection.LongResidualReversion ? 1.0 : -1.0;
            var diagnostic = unitDirection * residualForward;
            lines.Add(new FxResidualDivergenceDiagnosticLine(
                signal,
                start.TimestampUtc,
                end.TimestampUtc,
                residualForward,
                diagnostic,
                diagnostic > 0.0));
        }

        var returns = lines.Select(x => x.DirectionalDiagnosticReturn).ToArray();
        var reasonDistribution = signals
            .GroupBy(x => x.ReasonCode)
            .ToDictionary(x => x.Key, x => x.Count());
        var cumulative = 0.0;
        var peak = 0.0;
        var maxDrawdown = 0.0;
        foreach (var value in returns)
        {
            cumulative += value;
            peak = Math.Max(peak, cumulative);
            maxDrawdown = Math.Min(maxDrawdown, cumulative - peak);
        }

        return new FxResidualDivergenceDiagnosticSummary(
            CandidateRowsCount: signals.Count,
            AcceptedSignalsCount: signals.Count(x => x.IsAccepted),
            reasonDistribution,
            LongCount: signals.Count(x => x.Direction == FxResidualDivergenceDirection.LongResidualReversion),
            ShortCount: signals.Count(x => x.Direction == FxResidualDivergenceDirection.ShortResidualReversion),
            MeanDiagnosticReturn: returns.Length == 0 ? 0.0 : returns.Average(),
            MedianDiagnosticReturn: Median(returns),
            HitRate: returns.Length == 0 ? 0.0 : returns.Count(x => x > 0.0) / (double)returns.Length,
            CumulativeDiagnosticReturn: cumulative,
            MaxDrawdown: maxDrawdown,
            lines);
    }

    private static FxResidualDivergenceDiagnosticSummary Empty(IReadOnlyList<FxResidualDivergenceResearchSignal> signals)
        => new(
            signals.Count,
            signals.Count(x => x.IsAccepted),
            signals.GroupBy(x => x.ReasonCode).ToDictionary(x => x.Key, x => x.Count()),
            signals.Count(x => x.Direction == FxResidualDivergenceDirection.LongResidualReversion),
            signals.Count(x => x.Direction == FxResidualDivergenceDirection.ShortResidualReversion),
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            []);

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        var sorted = values.OrderBy(x => x).ToArray();
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[middle]
            : (sorted[middle - 1] + sorted[middle]) / 2.0;
    }
}
