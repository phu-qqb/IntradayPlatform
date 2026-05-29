using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QQ.Production.Intraday.Application;

public enum FxBboResearchAvailabilityMode
{
    ExplicitAvailableAtColumn,
    ExplicitReceivedAtColumn,
    EventTimestampPlusConfiguredDelay,
    EventTimestampAsAvailabilityProxy,
    Unknown
}

public enum FxBboResearchDataAuthorizationStatus
{
    Authorized,
    Blocked
}

public enum FxBboResearchFileFormat
{
    Csv,
    Jsonl,
    Parquet,
    Unknown
}

public enum FxBboOfflineResearchQuoteLoadRejectReason
{
    Accepted,
    MissingManifest,
    ManifestParseFailed,
    ManifestInvalid,
    AuthorizedForResearchFalse,
    AuthorizationExpired,
    NoApprovedFiles,
    FileApprovedFalse,
    FileNotFound,
    FileHashMismatch,
    UnsupportedFileFormat,
    RequiredColumnMissing,
    UnknownAvailabilityMode,
    EventTimestampAvailabilityProxyBlocked,
    NonPositiveAvailabilityDelay,
    RowLimitExceeded,
    RowParseFailed,
    MissingSymbol,
    TimestampParseFailed,
    UnknownTimestampSemantics,
    NonPositiveBid,
    NonPositiveAsk,
    CrossedQuote,
    AvailabilityTimestampParseFailed,
    AvailabilityBeforeQuoteTimestamp,
    UnsafeDataSurface
}

public sealed record FxBboResearchDataAuthorizationManifest(
    string ManifestVersion,
    string DatasetName,
    string DatasetVendor,
    string DatasetKind,
    bool AuthorizedForResearch,
    string? AuthorizedBy,
    DateTimeOffset? AuthorizationTimestampUtc,
    DateTimeOffset? AuthorizationExpiresUtc,
    IReadOnlyList<FxBboResearchAuthorizedFileEntry> Files);

public sealed record FxBboResearchAuthorizedFileEntry(
    string Path,
    string? Sha256,
    string? Symbol,
    FxBboResearchFileFormat Format,
    string TimestampColumn,
    string BidColumn,
    string AskColumn,
    string? SymbolColumn,
    string? AvailableAtColumn,
    string? ReceivedAtColumn,
    string? SequenceIdColumn,
    string TimeZone,
    string TimestampSemantics,
    FxBboResearchAvailabilityMode AvailabilityMode,
    TimeSpan? AssumedAvailabilityDelay,
    int? MaxAllowedReadRows,
    bool Approved,
    bool AllowEventTimestampAvailabilityProxyForResearch = false,
    string? AvailabilityJustification = null);

public sealed record FxBboOfflineResearchQuoteLoadParameters(
    string ManifestPath,
    DateTimeOffset ValidationTimestampUtc,
    bool AllowLocalEvaluation = false);

public sealed record FxBboOfflineResearchQuoteLoadDiagnostic(
    string? FilePath,
    int? RowNumber,
    FxBboOfflineResearchQuoteLoadRejectReason Reason,
    string Message);

public sealed record FxBboLocalEvaluationGateDecision(
    bool CanRun,
    string Reason,
    IReadOnlyList<string> Issues);

public sealed record FxBboOfflineResearchQuoteLoadResult(
    FxBboResearchDataAuthorizationStatus Status,
    IReadOnlyList<FxBboQuoteResearch> Quotes,
    IReadOnlyList<FxBboOfflineResearchQuoteLoadDiagnostic> Diagnostics,
    FxBboLocalEvaluationGateDecision LocalEvaluationGate)
{
    public IReadOnlyDictionary<FxBboOfflineResearchQuoteLoadRejectReason, int> ReasonCounts
        => Diagnostics.GroupBy(x => x.Reason).ToDictionary(x => x.Key, x => x.Count());
}

public sealed class FxBboResearchDataAuthorizationValidator
{
    public FxBboLocalEvaluationGateDecision ValidateForLocalEvaluation(
        FxBboResearchDataAuthorizationManifest? manifest,
        DateTimeOffset validationTimestampUtc)
    {
        var issues = ValidateManifest(manifest, validationTimestampUtc).ToList();
        if (issues.Count > 0)
        {
            return new(false, issues[0].Message, issues.Select(x => x.Message).ToArray());
        }

        var approvedFiles = manifest!.Files.Where(x => x.Approved).ToArray();
        if (approvedFiles.Length == 0)
        {
            return new(false, "No approved file entries are present.", ["No approved file entries are present."]);
        }

        return new(true, "Authorized manifest has explicit/conservative row-level availability semantics.", []);
    }

    public IReadOnlyList<FxBboOfflineResearchQuoteLoadDiagnostic> ValidateManifest(
        FxBboResearchDataAuthorizationManifest? manifest,
        DateTimeOffset validationTimestampUtc)
    {
        if (manifest is null)
        {
            return [Diagnostic(null, null, FxBboOfflineResearchQuoteLoadRejectReason.MissingManifest, "Manifest is missing.")];
        }

        var diagnostics = new List<FxBboOfflineResearchQuoteLoadDiagnostic>();
        if (validationTimestampUtc.Offset != TimeSpan.Zero)
        {
            diagnostics.Add(Diagnostic(null, null, FxBboOfflineResearchQuoteLoadRejectReason.UnknownTimestampSemantics, "Validation timestamp must be UTC."));
        }

        if (string.IsNullOrWhiteSpace(manifest.ManifestVersion) ||
            string.IsNullOrWhiteSpace(manifest.DatasetName) ||
            string.IsNullOrWhiteSpace(manifest.DatasetKind))
        {
            diagnostics.Add(Diagnostic(null, null, FxBboOfflineResearchQuoteLoadRejectReason.ManifestInvalid, "Manifest version, dataset name, and dataset kind are required."));
        }

        if (!manifest.AuthorizedForResearch)
        {
            diagnostics.Add(Diagnostic(null, null, FxBboOfflineResearchQuoteLoadRejectReason.AuthorizedForResearchFalse, "AuthorizedForResearch is false."));
        }

        if (manifest.AuthorizationExpiresUtc is not null)
        {
            if (manifest.AuthorizationExpiresUtc.Value.Offset != TimeSpan.Zero)
            {
                diagnostics.Add(Diagnostic(null, null, FxBboOfflineResearchQuoteLoadRejectReason.UnknownTimestampSemantics, "AuthorizationExpiresUtc must be UTC."));
            }
            else if (manifest.AuthorizationExpiresUtc.Value < validationTimestampUtc)
            {
                diagnostics.Add(Diagnostic(null, null, FxBboOfflineResearchQuoteLoadRejectReason.AuthorizationExpired, "Authorization has expired."));
            }
        }

        if (manifest.Files.Count == 0)
        {
            diagnostics.Add(Diagnostic(null, null, FxBboOfflineResearchQuoteLoadRejectReason.NoApprovedFiles, "Manifest contains no file entries."));
        }

        foreach (var file in manifest.Files)
        {
            diagnostics.AddRange(ValidateFileEntry(file));
        }

        return diagnostics;
    }

    public IReadOnlyList<FxBboOfflineResearchQuoteLoadDiagnostic> ValidateFileEntry(FxBboResearchAuthorizedFileEntry file)
    {
        var diagnostics = new List<FxBboOfflineResearchQuoteLoadDiagnostic>();

        if (!file.Approved)
        {
            diagnostics.Add(Diagnostic(file.Path, null, FxBboOfflineResearchQuoteLoadRejectReason.FileApprovedFalse, "File entry is not approved."));
        }

        if (string.IsNullOrWhiteSpace(file.Path) ||
            file.Format is FxBboResearchFileFormat.Unknown or FxBboResearchFileFormat.Parquet ||
            string.IsNullOrWhiteSpace(file.TimestampColumn) ||
            string.IsNullOrWhiteSpace(file.BidColumn) ||
            string.IsNullOrWhiteSpace(file.AskColumn) ||
            (string.IsNullOrWhiteSpace(file.Symbol) && string.IsNullOrWhiteSpace(file.SymbolColumn)))
        {
            diagnostics.Add(Diagnostic(file.Path, null, FxBboOfflineResearchQuoteLoadRejectReason.ManifestInvalid, "File path, supported format, required columns, and symbol mapping are required."));
        }

        if (!string.Equals(file.TimeZone, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(Diagnostic(file.Path, null, FxBboOfflineResearchQuoteLoadRejectReason.UnknownTimestampSemantics, "File time zone must be explicitly UTC."));
        }

        if (string.IsNullOrWhiteSpace(file.TimestampSemantics))
        {
            diagnostics.Add(Diagnostic(file.Path, null, FxBboOfflineResearchQuoteLoadRejectReason.UnknownTimestampSemantics, "Timestamp semantics must be documented."));
        }

        switch (file.AvailabilityMode)
        {
            case FxBboResearchAvailabilityMode.ExplicitAvailableAtColumn:
                if (string.IsNullOrWhiteSpace(file.AvailableAtColumn))
                {
                    diagnostics.Add(Diagnostic(file.Path, null, FxBboOfflineResearchQuoteLoadRejectReason.RequiredColumnMissing, "AvailableAtColumn is required for ExplicitAvailableAtColumn mode."));
                }

                break;
            case FxBboResearchAvailabilityMode.ExplicitReceivedAtColumn:
                if (string.IsNullOrWhiteSpace(file.ReceivedAtColumn))
                {
                    diagnostics.Add(Diagnostic(file.Path, null, FxBboOfflineResearchQuoteLoadRejectReason.RequiredColumnMissing, "ReceivedAtColumn is required for ExplicitReceivedAtColumn mode."));
                }

                break;
            case FxBboResearchAvailabilityMode.EventTimestampPlusConfiguredDelay:
                if (file.AssumedAvailabilityDelay is null || file.AssumedAvailabilityDelay.Value <= TimeSpan.Zero)
                {
                    diagnostics.Add(Diagnostic(file.Path, null, FxBboOfflineResearchQuoteLoadRejectReason.NonPositiveAvailabilityDelay, "EventTimestampPlusConfiguredDelay requires a positive delay."));
                }

                break;
            case FxBboResearchAvailabilityMode.EventTimestampAsAvailabilityProxy:
                if (!file.AllowEventTimestampAvailabilityProxyForResearch || string.IsNullOrWhiteSpace(file.AvailabilityJustification))
                {
                    diagnostics.Add(Diagnostic(file.Path, null, FxBboOfflineResearchQuoteLoadRejectReason.EventTimestampAvailabilityProxyBlocked, "EventTimestampAsAvailabilityProxy is blocked without explicit override and justification."));
                }

                break;
            default:
                diagnostics.Add(Diagnostic(file.Path, null, FxBboOfflineResearchQuoteLoadRejectReason.UnknownAvailabilityMode, "Availability mode is unknown."));
                break;
        }

        return diagnostics;
    }

    private static FxBboOfflineResearchQuoteLoadDiagnostic Diagnostic(
        string? filePath,
        int? rowNumber,
        FxBboOfflineResearchQuoteLoadRejectReason reason,
        string message)
        => new(filePath, rowNumber, reason, message);
}

public sealed class FxBboOfflineResearchQuoteLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly FxBboResearchDataAuthorizationValidator validator = new();

    public FxBboOfflineResearchQuoteLoadResult Load(FxBboOfflineResearchQuoteLoadParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (string.IsNullOrWhiteSpace(parameters.ManifestPath) || !File.Exists(parameters.ManifestPath))
        {
            var missingManifestDiagnostics = new[]
            {
                new FxBboOfflineResearchQuoteLoadDiagnostic(parameters.ManifestPath, null, FxBboOfflineResearchQuoteLoadRejectReason.MissingManifest, "Manifest file does not exist.")
            };
            return Blocked(missingManifestDiagnostics, "Manifest file does not exist.");
        }

        FxBboResearchDataAuthorizationManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<FxBboResearchDataAuthorizationManifest>(
                File.ReadAllText(parameters.ManifestPath),
                JsonOptions);
        }
        catch (JsonException exception)
        {
            var parseDiagnostics = new[]
            {
                new FxBboOfflineResearchQuoteLoadDiagnostic(parameters.ManifestPath, null, FxBboOfflineResearchQuoteLoadRejectReason.ManifestParseFailed, exception.Message)
            };
            return Blocked(parseDiagnostics, "Manifest JSON could not be parsed.");
        }

        var manifestDiagnostics = validator.ValidateManifest(manifest, parameters.ValidationTimestampUtc);
        if (manifestDiagnostics.Count > 0)
        {
            return Blocked(manifestDiagnostics, manifestDiagnostics[0].Message);
        }

        var gate = validator.ValidateForLocalEvaluation(manifest, parameters.ValidationTimestampUtc);
        if (!gate.CanRun || !parameters.AllowLocalEvaluation)
        {
            var reason = !gate.CanRun
                ? gate.Reason
                : "Caller did not request local evaluation after authorization validation.";
            return new(FxBboResearchDataAuthorizationStatus.Blocked, [], [], new(false, reason, gate.Issues));
        }

        var manifestDirectory = Path.GetDirectoryName(Path.GetFullPath(parameters.ManifestPath)) ?? Directory.GetCurrentDirectory();
        var diagnostics = new List<FxBboOfflineResearchQuoteLoadDiagnostic>();
        var quotes = new List<FxBboQuoteResearch>();

        foreach (var file in manifest!.Files.Where(x => x.Approved))
        {
            var filePath = ResolvePath(manifestDirectory, file.Path);
            if (!File.Exists(filePath))
            {
                diagnostics.Add(new(file.Path, null, FxBboOfflineResearchQuoteLoadRejectReason.FileNotFound, "Authorized file was not found."));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(file.Sha256))
            {
                var actualHash = ComputeSha256(filePath);
                if (!string.Equals(actualHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(new(file.Path, null, FxBboOfflineResearchQuoteLoadRejectReason.FileHashMismatch, "Authorized file SHA-256 does not match manifest."));
                    continue;
                }
            }

            var fileResult = file.Format switch
            {
                FxBboResearchFileFormat.Csv => LoadCsvFile(filePath, file),
                FxBboResearchFileFormat.Jsonl => LoadJsonlFile(filePath, file),
                _ => new FileLoad([], [new(file.Path, null, FxBboOfflineResearchQuoteLoadRejectReason.UnsupportedFileFormat, "Only CSV and JSONL are supported by the research loader.")])
            };

            diagnostics.AddRange(fileResult.Diagnostics);
            quotes.AddRange(fileResult.Quotes);
        }

        IReadOnlyList<FxBboOfflineResearchQuoteLoadDiagnostic> acceptedDiagnostics = quotes.Count == 0
            ? diagnostics
            : diagnostics.Concat([new FxBboOfflineResearchQuoteLoadDiagnostic(null, null, FxBboOfflineResearchQuoteLoadRejectReason.Accepted, $"Loaded {quotes.Count} research quote rows.")]).ToArray();

        var status = diagnostics.Any(x => x.Reason != FxBboOfflineResearchQuoteLoadRejectReason.Accepted)
            ? FxBboResearchDataAuthorizationStatus.Blocked
            : FxBboResearchDataAuthorizationStatus.Authorized;

        return new(status, quotes, acceptedDiagnostics.ToArray(), gate);
    }

    private FileLoad LoadCsvFile(string filePath, FxBboResearchAuthorizedFileEntry file)
    {
        var lines = File.ReadLines(filePath).ToArray();
        if (lines.Length == 0)
        {
            return new([], [new(file.Path, null, FxBboOfflineResearchQuoteLoadRejectReason.RequiredColumnMissing, "CSV file is empty.")]);
        }

        var headers = ParseCsvLine(lines[0]);
        var headerMap = headers
            .Select((name, index) => (Name: name, Index: index))
            .ToDictionary(x => x.Name, x => x.Index, StringComparer.Ordinal);

        var missing = RequiredColumns(file).Where(column => !headerMap.ContainsKey(column)).ToArray();
        if (missing.Length > 0)
        {
            return new([], [new(file.Path, null, FxBboOfflineResearchQuoteLoadRejectReason.RequiredColumnMissing, $"Missing required columns: {string.Join(", ", missing)}.")]);
        }

        var diagnostics = new List<FxBboOfflineResearchQuoteLoadDiagnostic>();
        var quotes = new List<FxBboQuoteResearch>();
        var dataRowCount = 0;

        for (var rowNumber = 2; rowNumber <= lines.Length; rowNumber++)
        {
            if (string.IsNullOrWhiteSpace(lines[rowNumber - 1]))
            {
                continue;
            }

            dataRowCount++;
            if (file.MaxAllowedReadRows is not null && dataRowCount > file.MaxAllowedReadRows.Value)
            {
                return new([], [new(file.Path, rowNumber, FxBboOfflineResearchQuoteLoadRejectReason.RowLimitExceeded, "MaxAllowedReadRows was exceeded.")]);
            }

            var fields = ParseCsvLine(lines[rowNumber - 1]);
            var row = headerMap.ToDictionary(x => x.Key, x => x.Value < fields.Count ? fields[x.Value] : string.Empty, StringComparer.Ordinal);
            var parsed = ParseQuoteRow(file, file.Path, rowNumber, row);
            if (parsed.Diagnostic is not null)
            {
                diagnostics.Add(parsed.Diagnostic);
                continue;
            }

            quotes.Add(parsed.Quote!);
        }

        return new(quotes, diagnostics);
    }

    private FileLoad LoadJsonlFile(string filePath, FxBboResearchAuthorizedFileEntry file)
    {
        var diagnostics = new List<FxBboOfflineResearchQuoteLoadDiagnostic>();
        var quotes = new List<FxBboQuoteResearch>();
        var rowNumber = 0;

        foreach (var line in File.ReadLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            rowNumber++;
            if (file.MaxAllowedReadRows is not null && rowNumber > file.MaxAllowedReadRows.Value)
            {
                return new([], [new(file.Path, rowNumber, FxBboOfflineResearchQuoteLoadRejectReason.RowLimitExceeded, "MaxAllowedReadRows was exceeded.")]);
            }

            Dictionary<string, string> row;
            try
            {
                using var document = JsonDocument.Parse(line);
                row = document.RootElement.EnumerateObject()
                    .ToDictionary(x => x.Name, x => x.Value.ValueKind == JsonValueKind.String ? x.Value.GetString() ?? string.Empty : x.Value.ToString(), StringComparer.Ordinal);
            }
            catch (JsonException exception)
            {
                diagnostics.Add(new(file.Path, rowNumber, FxBboOfflineResearchQuoteLoadRejectReason.RowParseFailed, exception.Message));
                continue;
            }

            var missing = RequiredColumns(file).Where(column => !row.ContainsKey(column)).ToArray();
            if (missing.Length > 0)
            {
                diagnostics.Add(new(file.Path, rowNumber, FxBboOfflineResearchQuoteLoadRejectReason.RequiredColumnMissing, $"Missing required columns: {string.Join(", ", missing)}."));
                continue;
            }

            var parsed = ParseQuoteRow(file, file.Path, rowNumber, row);
            if (parsed.Diagnostic is not null)
            {
                diagnostics.Add(parsed.Diagnostic);
                continue;
            }

            quotes.Add(parsed.Quote!);
        }

        return new(quotes, diagnostics);
    }

    private ParsedQuote ParseQuoteRow(
        FxBboResearchAuthorizedFileEntry file,
        string filePath,
        int rowNumber,
        IReadOnlyDictionary<string, string> row)
    {
        var symbol = string.IsNullOrWhiteSpace(file.Symbol)
            ? Read(row, file.SymbolColumn)
            : file.Symbol;

        if (string.IsNullOrWhiteSpace(symbol))
        {
            return Rejected(filePath, rowNumber, FxBboOfflineResearchQuoteLoadRejectReason.MissingSymbol, "Symbol is missing.");
        }

        if (!TryParseUtc(Read(row, file.TimestampColumn), out var timestampUtc))
        {
            return Rejected(filePath, rowNumber, FxBboOfflineResearchQuoteLoadRejectReason.TimestampParseFailed, "Quote timestamp could not be parsed as explicit UTC.");
        }

        if (!decimal.TryParse(Read(row, file.BidColumn), NumberStyles.Float, CultureInfo.InvariantCulture, out var bid))
        {
            return Rejected(filePath, rowNumber, FxBboOfflineResearchQuoteLoadRejectReason.NonPositiveBid, "Bid could not be parsed.");
        }

        if (!decimal.TryParse(Read(row, file.AskColumn), NumberStyles.Float, CultureInfo.InvariantCulture, out var ask))
        {
            return Rejected(filePath, rowNumber, FxBboOfflineResearchQuoteLoadRejectReason.NonPositiveAsk, "Ask could not be parsed.");
        }

        if (bid <= 0m)
        {
            return Rejected(filePath, rowNumber, FxBboOfflineResearchQuoteLoadRejectReason.NonPositiveBid, "Bid must be positive.");
        }

        if (ask <= 0m)
        {
            return Rejected(filePath, rowNumber, FxBboOfflineResearchQuoteLoadRejectReason.NonPositiveAsk, "Ask must be positive.");
        }

        if (ask < bid)
        {
            return Rejected(filePath, rowNumber, FxBboOfflineResearchQuoteLoadRejectReason.CrossedQuote, "Ask must be greater than or equal to bid.");
        }

        if (!TryComputeAvailability(file, row, timestampUtc, out var availableAtUtc))
        {
            return Rejected(filePath, rowNumber, FxBboOfflineResearchQuoteLoadRejectReason.AvailabilityTimestampParseFailed, "Availability timestamp could not be parsed or computed.");
        }

        if (availableAtUtc < timestampUtc)
        {
            return Rejected(filePath, rowNumber, FxBboOfflineResearchQuoteLoadRejectReason.AvailabilityBeforeQuoteTimestamp, "AvailableAtUtc must be greater than or equal to quote timestamp.");
        }

        var sequenceId = TryParseSequenceId(Read(row, file.SequenceIdColumn));

        return new(new FxBboQuoteResearch(
            symbol.Trim().ToUpperInvariant(),
            timestampUtc,
            bid,
            ask,
            sequenceId,
            SourceEventId: null,
            AvailableAtUtc: availableAtUtc,
            Source: filePath,
            Venue: file.DatasetVenue()), null);
    }

    private static bool TryComputeAvailability(
        FxBboResearchAuthorizedFileEntry file,
        IReadOnlyDictionary<string, string> row,
        DateTimeOffset timestampUtc,
        out DateTimeOffset availableAtUtc)
    {
        switch (file.AvailabilityMode)
        {
            case FxBboResearchAvailabilityMode.ExplicitAvailableAtColumn:
                return TryParseUtc(Read(row, file.AvailableAtColumn), out availableAtUtc);
            case FxBboResearchAvailabilityMode.ExplicitReceivedAtColumn:
                return TryParseUtc(Read(row, file.ReceivedAtColumn), out availableAtUtc);
            case FxBboResearchAvailabilityMode.EventTimestampPlusConfiguredDelay:
                availableAtUtc = timestampUtc.Add(file.AssumedAvailabilityDelay!.Value);
                return true;
            case FxBboResearchAvailabilityMode.EventTimestampAsAvailabilityProxy:
                availableAtUtc = timestampUtc;
                return file.AllowEventTimestampAvailabilityProxyForResearch;
            default:
                availableAtUtc = default;
                return false;
        }
    }

    private static IReadOnlyList<string> RequiredColumns(FxBboResearchAuthorizedFileEntry file)
    {
        var columns = new List<string> { file.TimestampColumn, file.BidColumn, file.AskColumn };
        if (string.IsNullOrWhiteSpace(file.Symbol) && !string.IsNullOrWhiteSpace(file.SymbolColumn))
        {
            columns.Add(file.SymbolColumn);
        }

        if (file.AvailabilityMode == FxBboResearchAvailabilityMode.ExplicitAvailableAtColumn && !string.IsNullOrWhiteSpace(file.AvailableAtColumn))
        {
            columns.Add(file.AvailableAtColumn);
        }

        if (file.AvailabilityMode == FxBboResearchAvailabilityMode.ExplicitReceivedAtColumn && !string.IsNullOrWhiteSpace(file.ReceivedAtColumn))
        {
            columns.Add(file.ReceivedAtColumn);
        }

        if (!string.IsNullOrWhiteSpace(file.SequenceIdColumn))
        {
            columns.Add(file.SequenceIdColumn);
        }

        return columns.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool TryParseUtc(string value, out DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !(value.EndsWith("Z", StringComparison.OrdinalIgnoreCase) ||
              value.Contains("+00:00", StringComparison.Ordinal) ||
              value.Contains("-00:00", StringComparison.Ordinal)))
        {
            timestamp = default;
            return false;
        }

        return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out timestamp) &&
            timestamp.Offset == TimeSpan.Zero;
    }

    private static long? TryParseSequenceId(string value)
        => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequenceId)
            ? sequenceId
            : null;

    private static string Read(IReadOnlyDictionary<string, string> row, string? column)
        => string.IsNullOrWhiteSpace(column) ? string.Empty : row.TryGetValue(column, out var value) ? value : string.Empty;

    private static string ResolvePath(string baseDirectory, string path)
        => Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(baseDirectory, path));

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var value = line[index];
            if (value == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (value == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(value);
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static ParsedQuote Rejected(
        string filePath,
        int rowNumber,
        FxBboOfflineResearchQuoteLoadRejectReason reason,
        string message)
        => new(null, new(filePath, rowNumber, reason, message));

    private static FxBboOfflineResearchQuoteLoadResult Blocked(
        IReadOnlyList<FxBboOfflineResearchQuoteLoadDiagnostic> diagnostics,
        string reason)
        => new(
            FxBboResearchDataAuthorizationStatus.Blocked,
            [],
            diagnostics,
            new(false, reason, diagnostics.Select(x => x.Message).ToArray()));

    private sealed record FileLoad(
        IReadOnlyList<FxBboQuoteResearch> Quotes,
        IReadOnlyList<FxBboOfflineResearchQuoteLoadDiagnostic> Diagnostics);

    private sealed record ParsedQuote(
        FxBboQuoteResearch? Quote,
        FxBboOfflineResearchQuoteLoadDiagnostic? Diagnostic);
}

internal static class FxBboResearchAuthorizedFileEntryExtensions
{
    public static string? DatasetVenue(this FxBboResearchAuthorizedFileEntry file)
        => file.Symbol is null ? null : "OfflineResearch";
}
