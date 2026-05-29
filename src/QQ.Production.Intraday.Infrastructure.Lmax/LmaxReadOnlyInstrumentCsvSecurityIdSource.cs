using System.Text;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxReadOnlyInstrumentCsvSecurityIdSource(
    string SourceName,
    string VenueProfileName,
    string CsvText);

public sealed record LmaxReadOnlyInstrumentCsvSecurityIdRow(
    string SourceName,
    string InstrumentName,
    string LmaxId,
    string LmaxSymbol,
    bool IsSelectedForVenueProfile,
    bool IsTokyoProfile);

public sealed record LmaxReadOnlyInstrumentCsvSecurityIdCandidate(
    string Symbol,
    string SlashSymbol,
    string ExpectedDemoLondonSecurityId,
    string? SelectedSecurityId,
    string? SelectedSourceName,
    IReadOnlyList<string> ObservedTokyoSecurityIds,
    bool IsApprovedForExternalRun);

public sealed record LmaxReadOnlyInstrumentCsvSecurityIdExtractionIssue(
    LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity Severity,
    string Code,
    string Path,
    string Message);

public sealed record LmaxReadOnlyInstrumentCsvSecurityIdExtractionResult(
    LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision Decision,
    string VenueProfileName,
    IReadOnlyList<LmaxReadOnlyInstrumentCsvSecurityIdCandidate> Candidates,
    IReadOnlyList<LmaxReadOnlyInstrumentCsvSecurityIdRow> Rows,
    IReadOnlyList<LmaxReadOnlyInstrumentCsvSecurityIdExtractionIssue> Issues,
    bool ExternalConnectionAttempted,
    bool ExternalApiCallAttempted,
    bool SecurityListRequestAttempted,
    bool MarketDataSnapshotAttempted,
    bool ReplayAttempted,
    bool RuntimeShadowReplaySubmit,
    bool SchedulerOrPollingAdded,
    bool OrderSubmissionAdded,
    bool GatewayRegistrationAdded,
    bool TradingMutationAdded,
    bool CredentialValuesReturned,
    bool IsApprovedForExternalRun,
    bool NoSensitiveContent)
{
    public IReadOnlyList<LmaxReadOnlyInstrumentCsvSecurityIdExtractionIssue> Errors =>
        Issues.Where(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error).ToArray();
}

public static class LmaxReadOnlyInstrumentCsvSecurityIdExtractor
{
    private static readonly IReadOnlyDictionary<string, string> ExpectedDemoLondonIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["GBPUSD"] = "4002",
        ["EURGBP"] = "4003",
        ["USDJPY"] = "4004",
        ["AUDUSD"] = "4007"
    };

    private static readonly Regex SensitivePattern = new(
        "(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\\b553=|\\b554=|host=|user=|account)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyInstrumentCsvSecurityIdExtractionResult Extract(
        IEnumerable<LmaxReadOnlyInstrumentCsvSecurityIdSource> sources,
        string venueProfileName = "DemoLondon")
    {
        var issues = new List<LmaxReadOnlyInstrumentCsvSecurityIdExtractionIssue>();
        var sourceArray = sources.ToArray();
        var rows = new List<LmaxReadOnlyInstrumentCsvSecurityIdRow>();

        foreach (var source in sourceArray)
        {
            if (SensitivePattern.IsMatch(source.CsvText) || SensitivePattern.IsMatch(source.SourceName))
            {
                issues.Add(Error("SensitiveContentDetected", source.SourceName, "Instrument CSV source contains credential-shaped or sensitive content."));
                continue;
            }

            var records = ParseCsv(source.CsvText);
            foreach (var record in records)
            {
                if (!record.TryGetValue("Instrument Name", out var instrumentName)
                    || !record.TryGetValue("LMAX ID", out var lmaxId)
                    || !record.TryGetValue("LMAX symbol", out var lmaxSymbol))
                {
                    issues.Add(Error("RequiredColumnsMissing", source.SourceName, "Instrument CSV must include Instrument Name, LMAX ID, and LMAX symbol columns."));
                    break;
                }

                if (string.IsNullOrWhiteSpace(instrumentName) || string.IsNullOrWhiteSpace(lmaxId))
                {
                    continue;
                }

                rows.Add(new(
                    SourceName: source.SourceName,
                    InstrumentName: instrumentName.Trim(),
                    LmaxId: lmaxId.Trim(),
                    LmaxSymbol: lmaxSymbol.Trim(),
                    IsSelectedForVenueProfile: IsSelectedId(lmaxId, venueProfileName),
                    IsTokyoProfile: IsTokyoId(lmaxId)));
            }
        }

        var candidates = new List<LmaxReadOnlyInstrumentCsvSecurityIdCandidate>();
        foreach (var (symbol, slashSymbol) in LmaxReadOnlySecurityListDiscovery.CandidateSymbols)
        {
            var expectedId = ExpectedDemoLondonIds[symbol];
            var matchingRows = rows.Where(x => IsCandidateRow(x, symbol, slashSymbol)).ToArray();
            var selectedRows = matchingRows.Where(x => x.IsSelectedForVenueProfile).ToArray();
            var tokyoIds = matchingRows.Where(x => x.IsTokyoProfile).Select(x => x.LmaxId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var selectedIds = selectedRows.Select(x => x.LmaxId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            if (matchingRows.Length == 0)
            {
                issues.Add(Error("CandidateRowMissing", $"$.{symbol}", $"{slashSymbol} was not found in the supplied LMAX instrument CSV source."));
            }
            else if (selectedIds.Length == 0)
            {
                issues.Add(Error("SelectedProfileIdMissing", $"$.{symbol}", $"{slashSymbol} did not have a DemoLondon/NewYork 400x SecurityID in the supplied CSV source."));
            }
            else if (selectedIds.Length > 1)
            {
                issues.Add(Error("ConflictingSelectedProfileIds", $"$.{symbol}", $"{slashSymbol} has conflicting DemoLondon/NewYork 400x SecurityIDs."));
            }
            else if (!selectedIds[0].Equals(expectedId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("UnexpectedDemoLondonSecurityId", $"$.{symbol}", $"{slashSymbol} expected {expectedId} for DemoLondon/NewYork but extracted {selectedIds[0]}."));
            }

            candidates.Add(new(
                Symbol: symbol,
                SlashSymbol: slashSymbol,
                ExpectedDemoLondonSecurityId: expectedId,
                SelectedSecurityId: selectedIds.Length == 1 ? selectedIds[0] : null,
                SelectedSourceName: selectedRows.FirstOrDefault()?.SourceName,
                ObservedTokyoSecurityIds: tokyoIds,
                IsApprovedForExternalRun: false));
        }

        var decision = issues.Any(x => x.Severity == LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error)
            ? LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.FAIL
            : LmaxReadOnlyInstrumentSecurityIdConfirmationRecordValidationDecision.PASS;

        return new(
            Decision: decision,
            VenueProfileName: venueProfileName,
            Candidates: candidates,
            Rows: rows,
            Issues: issues,
            ExternalConnectionAttempted: false,
            ExternalApiCallAttempted: false,
            SecurityListRequestAttempted: false,
            MarketDataSnapshotAttempted: false,
            ReplayAttempted: false,
            RuntimeShadowReplaySubmit: false,
            SchedulerOrPollingAdded: false,
            OrderSubmissionAdded: false,
            GatewayRegistrationAdded: false,
            TradingMutationAdded: false,
            CredentialValuesReturned: false,
            IsApprovedForExternalRun: false,
            NoSensitiveContent: !issues.Any(x => x.Code == "SensitiveContentDetected"));
    }

    private static bool IsCandidateRow(LmaxReadOnlyInstrumentCsvSecurityIdRow row, string symbol, string slashSymbol)
        => Normalize(row.InstrumentName).Equals(Normalize(symbol), StringComparison.OrdinalIgnoreCase)
           || Normalize(row.InstrumentName).Equals(Normalize(slashSymbol), StringComparison.OrdinalIgnoreCase)
           || Normalize(row.LmaxSymbol).Equals(Normalize(symbol), StringComparison.OrdinalIgnoreCase)
           || Normalize(row.LmaxSymbol).Equals(Normalize(slashSymbol), StringComparison.OrdinalIgnoreCase);

    private static bool IsSelectedId(string lmaxId, string venueProfileName)
        => venueProfileName.Equals("DemoLondon", StringComparison.OrdinalIgnoreCase)
           ? lmaxId.StartsWith("400", StringComparison.Ordinal)
           : !IsTokyoId(lmaxId);

    private static bool IsTokyoId(string lmaxId)
        => lmaxId.StartsWith("600", StringComparison.Ordinal);

    private static string Normalize(string? value)
        => (value ?? string.Empty).Replace("/", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    private static IReadOnlyList<Dictionary<string, string>> ParseCsv(string text)
    {
        var rows = ParseRows(text);
        if (rows.Count == 0)
        {
            return Array.Empty<Dictionary<string, string>>();
        }

        var headers = rows[0];
        return rows.Skip(1)
            .Where(row => row.Any(x => !string.IsNullOrWhiteSpace(x)))
            .Select(row =>
            {
                var record = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < headers.Count; i++)
                {
                    record[headers[i].Trim()] = i < row.Count ? row[i] : string.Empty;
                }

                return record;
            })
            .ToArray();
    }

    private static List<List<string>> ParseRows(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if ((c == '\r' || c == '\n') && !inQuotes)
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    private static LmaxReadOnlyInstrumentCsvSecurityIdExtractionIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyInstrumentSecurityIdConfirmationRecordIssueSeverity.Error, code, path, message);
}
