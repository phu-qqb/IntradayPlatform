using System.Globalization;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public sealed record QubesFxWeightsIngestionRequest(
    QubesRunId QubesRunId,
    DateTimeOffset ProducedAtUtc,
    DateTimeOffset EffectiveAtUtc,
    int CadenceMinutes,
    string FundCode,
    string ModelName,
    decimal NavUsd,
    TargetQuantityMode TargetQuantityMode,
    IReadOnlyList<string> RawLines);

public enum QubesFxWeightsIngestionIssueCode
{
    MissingRunId,
    InvalidTimestamp,
    InvalidCadence,
    MissingRows,
    InvalidRowShape,
    MalformedTicker,
    InvalidWeight,
    ExposureNotZeroSum
}

public sealed record QubesFxWeightsIngestionIssue(
    QubesFxWeightsIngestionIssueCode Code,
    int? RowNumber,
    string Message);

public sealed record QubesFxRawWeightRow(
    int RowNumber,
    string BloombergTicker,
    string Pair,
    string BaseCurrency,
    string QuoteCurrency,
    decimal Weight);

public sealed record QubesUsdQuoteTargetWeight(
    string BloombergTicker,
    string Symbol,
    string Currency,
    decimal Weight);

public sealed record QubesFxWeightsIngestionResult(
    QubesRunId QubesRunId,
    ModelWeightSourceSystem SourceSystem,
    DateTimeOffset ProducedAtUtc,
    DateTimeOffset EffectiveAtUtc,
    int CadenceMinutes,
    int RawInputRowCount,
    int NormalizedOutputRowCount,
    decimal TotalCurrencyExposure,
    IReadOnlyDictionary<string, decimal> CurrencyExposures,
    IReadOnlyList<QubesFxRawWeightRow> RawRows,
    IReadOnlyList<QubesUsdQuoteTargetWeight> NormalizedWeights,
    IReadOnlyList<QubesFxWeightsIngestionIssue> Issues,
    CreateFakeModelWeightBatchRequest? ModelWeightBatchRequest)
{
    public bool Succeeded => Issues.Count == 0;
}

public sealed class QubesFxWeightsFixtureIngestionService
{
    private const int RequiredCadenceMinutes = 15;
    private const decimal DefaultTolerance = 0.0000000001m;

    public QubesFxWeightsIngestionResult ParseNormalizeAndMap(
        QubesFxWeightsIngestionRequest request,
        decimal zeroTolerance = DefaultTolerance)
    {
        var issues = new List<QubesFxWeightsIngestionIssue>();
        var rawRows = new List<QubesFxRawWeightRow>();
        var exposures = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(request.QubesRunId.Value))
        {
            issues.Add(new QubesFxWeightsIngestionIssue(QubesFxWeightsIngestionIssueCode.MissingRunId, null, "Qubes run id is required."));
        }

        if (request.ProducedAtUtc.Offset != TimeSpan.Zero || request.EffectiveAtUtc.Offset != TimeSpan.Zero || request.EffectiveAtUtc < request.ProducedAtUtc)
        {
            issues.Add(new QubesFxWeightsIngestionIssue(QubesFxWeightsIngestionIssueCode.InvalidTimestamp, null, "Produced and effective timestamps must be UTC, and effective must not precede produced."));
        }

        if (request.CadenceMinutes != RequiredCadenceMinutes)
        {
            issues.Add(new QubesFxWeightsIngestionIssue(QubesFxWeightsIngestionIssueCode.InvalidCadence, null, "Qubes FX target weights cadence must be 15 minutes."));
        }

        if (request.RawLines.Count == 0 || request.RawLines.All(string.IsNullOrWhiteSpace))
        {
            issues.Add(new QubesFxWeightsIngestionIssue(QubesFxWeightsIngestionIssueCode.MissingRows, null, "At least one Qubes FX weight row is required."));
        }

        for (var index = 0; index < request.RawLines.Count; index++)
        {
            var rawLine = request.RawLines[index]?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var rowNumber = index + 1;
            var parts = rawLine.Split(';', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                issues.Add(new QubesFxWeightsIngestionIssue(QubesFxWeightsIngestionIssueCode.InvalidRowShape, rowNumber, "Qubes row must be '<BloombergTicker>;<weight>'."));
                continue;
            }

            if (!TryParseBloombergFxTicker(parts[0], out var pair, out var baseCurrency, out var quoteCurrency))
            {
                issues.Add(new QubesFxWeightsIngestionIssue(QubesFxWeightsIngestionIssueCode.MalformedTicker, rowNumber, $"Bloomberg FX ticker '{parts[0]}' is not recognized."));
                continue;
            }

            if (!decimal.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var weight))
            {
                issues.Add(new QubesFxWeightsIngestionIssue(QubesFxWeightsIngestionIssueCode.InvalidWeight, rowNumber, $"Weight '{parts[1]}' is not finite numeric decimal."));
                continue;
            }

            rawRows.Add(new QubesFxRawWeightRow(rowNumber, $"{pair} Curncy", pair, baseCurrency, quoteCurrency, weight));
            AddExposure(exposures, baseCurrency, weight);
            AddExposure(exposures, quoteCurrency, -weight);
        }

        var totalExposure = exposures.Values.Sum();
        if (Math.Abs(totalExposure) > zeroTolerance)
        {
            issues.Add(new QubesFxWeightsIngestionIssue(QubesFxWeightsIngestionIssueCode.ExposureNotZeroSum, null, "Total currency exposure must sum to zero after FX pair netting."));
        }

        var normalized = exposures
            .Where(x => !x.Key.Equals("USD", StringComparison.OrdinalIgnoreCase) && Math.Abs(x.Value) > zeroTolerance)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => new QubesUsdQuoteTargetWeight($"{x.Key.ToUpperInvariant()}USD Curncy", $"{x.Key.ToUpperInvariant()}USD", x.Key.ToUpperInvariant(), x.Value))
            .ToArray();

        var batchRequest = issues.Count == 0
            ? new CreateFakeModelWeightBatchRequest(
                request.QubesRunId.Value,
                ModelWeightSourceSystem.Qubes,
                string.IsNullOrWhiteSpace(request.FundCode) ? "QQ_MASTER" : request.FundCode,
                string.IsNullOrWhiteSpace(request.ModelName) ? "IntradayFxModel" : request.ModelName,
                request.ProducedAtUtc,
                request.EffectiveAtUtc,
                request.CadenceMinutes,
                request.NavUsd,
                request.TargetQuantityMode,
                ModelWeightBatchStatus.Ready,
                normalized.Select(x => new CreateFakeModelWeightRowRequest(x.BloombergTicker, x.Symbol, x.Weight)).ToArray())
            : null;

        return new QubesFxWeightsIngestionResult(
            request.QubesRunId,
            ModelWeightSourceSystem.Qubes,
            request.ProducedAtUtc,
            request.EffectiveAtUtc,
            request.CadenceMinutes,
            request.RawLines.Count(x => !string.IsNullOrWhiteSpace(x)),
            normalized.Length,
            totalExposure,
            new SortedDictionary<string, decimal>(exposures, StringComparer.OrdinalIgnoreCase),
            rawRows,
            normalized,
            issues,
            batchRequest);
    }

    public static bool TryParseBloombergFxTicker(string ticker, out string pair, out string baseCurrency, out string quoteCurrency)
    {
        pair = string.Empty;
        baseCurrency = string.Empty;
        quoteCurrency = string.Empty;

        var parts = ticker.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !parts[1].Equals("Curncy", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidate = parts[0].ToUpperInvariant();
        if (candidate.Length != 6 || candidate.Any(x => x < 'A' || x > 'Z'))
        {
            return false;
        }

        var parsedBase = candidate[..3];
        var parsedQuote = candidate[3..];
        if (parsedBase == parsedQuote)
        {
            return false;
        }

        pair = candidate;
        baseCurrency = parsedBase;
        quoteCurrency = parsedQuote;
        return true;
    }

    private static void AddExposure(Dictionary<string, decimal> exposures, string currency, decimal delta)
    {
        exposures.TryGetValue(currency, out var current);
        exposures[currency.ToUpperInvariant()] = current + delta;
    }
}
