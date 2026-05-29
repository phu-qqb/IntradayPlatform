using System.Globalization;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Application;

public sealed record SyntheticPmsFixtureAdapterRequest(
    IReadOnlyList<string> RawLines,
    bool AllowSyntheticPmsFixture,
    bool AllowNotQubesEconomicOutputFixture);

public enum SyntheticPmsFixtureAdapterIssueCode
{
    MissingAllowance,
    MissingRows,
    InvalidRowShape,
    BloombergTickerRejected,
    UnsupportedSymbol,
    DuplicateSymbol,
    InvalidWeight,
    NonF8Weight
}

public sealed record SyntheticPmsFixtureAdapterIssue(
    SyntheticPmsFixtureAdapterIssueCode Code,
    int? RowNumber,
    string Message);

public sealed record SyntheticPmsFixtureAdapterRow(
    int RowNumber,
    string Symbol,
    decimal Weight,
    string InternalPaperInputLine);

public sealed record SyntheticPmsFixtureAdapterResult(
    string InputFormat,
    string SourceType,
    bool NotQubesEconomicOutput,
    bool PaperOnly,
    bool NonExecutable,
    IReadOnlyList<string> AcceptedSymbols,
    IReadOnlyList<SyntheticPmsFixtureAdapterRow> Rows,
    IReadOnlyList<string> InternalPaperInputLines,
    IReadOnlyList<SyntheticPmsFixtureAdapterIssue> Issues)
{
    public bool Succeeded => Issues.Count == 0;
}

public sealed class SyntheticPmsFixtureAdapter
{
    private static readonly Regex F8Decimal = new(@"^-?\d+\.\d{8}$", RegexOptions.Compiled);
    private static readonly HashSet<string> AcceptedSymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        "EURUSD"
    };

    public SyntheticPmsFixtureAdapterResult Adapt(SyntheticPmsFixtureAdapterRequest request)
    {
        var issues = new List<SyntheticPmsFixtureAdapterIssue>();
        var rows = new List<SyntheticPmsFixtureAdapterRow>();
        var seenSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!request.AllowSyntheticPmsFixture || !request.AllowNotQubesEconomicOutputFixture)
        {
            issues.Add(new SyntheticPmsFixtureAdapterIssue(
                SyntheticPmsFixtureAdapterIssueCode.MissingAllowance,
                null,
                "Synthetic PMS fixture input requires explicit synthetic and not-Qubes-economic-output allowances."));
        }

        if (request.RawLines.Count == 0 || request.RawLines.All(string.IsNullOrWhiteSpace))
        {
            issues.Add(new SyntheticPmsFixtureAdapterIssue(
                SyntheticPmsFixtureAdapterIssueCode.MissingRows,
                null,
                "At least one CanonicalModelSymbol;WeightDecimalF8 row is required."));
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
                issues.Add(new SyntheticPmsFixtureAdapterIssue(
                    SyntheticPmsFixtureAdapterIssueCode.InvalidRowShape,
                    rowNumber,
                    "Synthetic PMS row must be '<CanonicalModelSymbol>;<WeightDecimalF8>'."));
                continue;
            }

            var symbol = parts[0].ToUpperInvariant();
            if (symbol.Contains(' ', StringComparison.OrdinalIgnoreCase) ||
                symbol.Contains("Curncy", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new SyntheticPmsFixtureAdapterIssue(
                    SyntheticPmsFixtureAdapterIssueCode.BloombergTickerRejected,
                    rowNumber,
                    "Synthetic PMS fixture must use canonical symbols, not Bloomberg tickers."));
                continue;
            }

            if (!AcceptedSymbols.Contains(symbol))
            {
                issues.Add(new SyntheticPmsFixtureAdapterIssue(
                    SyntheticPmsFixtureAdapterIssueCode.UnsupportedSymbol,
                    rowNumber,
                    $"Synthetic PMS symbol '{parts[0]}' is not accepted by this paper-readiness gate."));
                continue;
            }

            if (!seenSymbols.Add(symbol))
            {
                issues.Add(new SyntheticPmsFixtureAdapterIssue(
                    SyntheticPmsFixtureAdapterIssueCode.DuplicateSymbol,
                    rowNumber,
                    $"Duplicate synthetic PMS symbol '{symbol}' is not allowed."));
                continue;
            }

            if (parts[1].Equals("NaN", StringComparison.OrdinalIgnoreCase) ||
                parts[1].Equals("Infinity", StringComparison.OrdinalIgnoreCase) ||
                parts[1].Equals("+Infinity", StringComparison.OrdinalIgnoreCase) ||
                parts[1].Equals("-Infinity", StringComparison.OrdinalIgnoreCase) ||
                !decimal.TryParse(parts[1], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var weight))
            {
                issues.Add(new SyntheticPmsFixtureAdapterIssue(
                    SyntheticPmsFixtureAdapterIssueCode.InvalidWeight,
                    rowNumber,
                    $"Weight '{parts[1]}' is not a finite decimal."));
                continue;
            }

            if (!F8Decimal.IsMatch(parts[1]))
            {
                issues.Add(new SyntheticPmsFixtureAdapterIssue(
                    SyntheticPmsFixtureAdapterIssueCode.NonF8Weight,
                    rowNumber,
                    $"Weight '{parts[1]}' must use WeightDecimalF8."));
                continue;
            }

            rows.Add(new SyntheticPmsFixtureAdapterRow(
                rowNumber,
                symbol,
                weight,
                $"{symbol} Curncy;{weight.ToString("0.00000000", CultureInfo.InvariantCulture)}"));
        }

        return new SyntheticPmsFixtureAdapterResult(
            "CanonicalModelSymbol;WeightDecimalF8",
            "SyntheticOperatorFixture",
            NotQubesEconomicOutput: true,
            PaperOnly: true,
            NonExecutable: true,
            AcceptedSymbols.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            rows,
            rows.Select(x => x.InternalPaperInputLine).ToArray(),
            issues);
    }
}
