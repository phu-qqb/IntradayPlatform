using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Application;

public enum NqTickKind { Trades, Quotes, All }

public sealed record NqTickExportRequest(
    string RunKey,
    string OutputRoot,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? Contract,
    int? MaxRows,
    NqTickKind? TickKind,
    string? AssumeSourceTimezone,
    bool NoExecution);

public sealed record NqTickCandidateTable(
    string Schema,
    string Table,
    IReadOnlyList<string> Columns,
    string? TimestampColumn,
    string? RootColumn,
    string? ContractColumn,
    string? PriceColumn,
    string? SizeColumn,
    string? BidPriceColumn,
    string? BidSizeColumn,
    string? AskPriceColumn,
    string? AskSizeColumn,
    string? ExchangeColumn,
    string? SequenceColumn,
    string? ConditionsColumn,
    string? SourceColumn,
    string? SourceRowIdColumn,
    bool SupportsTrades,
    bool SupportsQuotes,
    string Reason);

public sealed record NqTickSourceRow(
    DateTimeOffset? TimestampUtc,
    string SourceTimestamp,
    string SourceTimezone,
    string? Root,
    string? Contract,
    string EventType,
    string? Price,
    string? Size,
    string? BidPrice,
    string? BidSize,
    string? AskPrice,
    string? AskSize,
    string? Exchange,
    string? Sequence,
    string? Conditions,
    string SourceTable,
    string? SourceRowId);

public interface INqTickExportSource
{
    Task<IReadOnlyList<NqTickCandidateTable>> DiscoverTablesAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<NqTickSourceRow> ReadRowsAsync(NqTickExportQuery query, CancellationToken cancellationToken);
    string DatabaseDescription { get; }
}

public sealed record NqTickExportQuery(
    IReadOnlyList<NqTickCandidateTable> Tables,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? Contract,
    int? MaxRows,
    NqTickKind TickKind,
    string? AssumeSourceTimezone);

public sealed record NqTickExportResult(NqTickExportMetadata Metadata, string? DataFilePath, string ValidationDirectory);

public sealed record NqTickExportMetadata(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    string ExportStatus,
    bool ExportIsComplete,
    NqTickDataFileMetadata DataFile,
    NqTickInstrumentMetadata Instrument,
    NqTickCoverageMetadata Coverage,
    NqTickSourceMetadata Source,
    NqTickSchemaMetadata Schema,
    NqTickQualityMetadata Quality,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Failures);

public sealed record NqTickDataFileMetadata(string RelativePath, string Format, string? Sha256, long Bytes, long RowCount);
public sealed record NqTickInstrumentMetadata(string Requested, string Definition, IReadOnlyList<string> ExcludedRoots, IReadOnlyList<string> ContractsIncluded, IReadOnlyList<string> ContractsDetectedButExcluded);
public sealed record NqTickCoverageMetadata(DateTimeOffset? FirstTimestampUtc, DateTimeOffset? LastTimestampUtc, IReadOnlyList<NqTickContractCoverage> PerContract);
public sealed record NqTickContractCoverage(string Contract, long RowCount, DateTimeOffset? FirstTimestampUtc, DateTimeOffset? LastTimestampUtc, IReadOnlyDictionary<string, long> EventTypeCounts);
public sealed record NqTickSourceMetadata(string Database, IReadOnlyList<string> TablesScanned, IReadOnlyList<string> TablesUsed, bool QueryIsReadOnly);
public sealed record NqTickSchemaMetadata(IReadOnlyList<NqTickColumnMetadata> Columns);
public sealed record NqTickColumnMetadata(string Name, string Type, string Description);
public sealed record NqTickQualityMetadata(long DuplicateRowsDetected, long NullTimestampCount, long NullPriceTradeCount, long NullSizeTradeCount, long NegativePriceCount, long ZeroOrNegativeSizeTradeCount, bool TimezoneAssumptionUsed, bool TimestampMonotonicAfterSort);

public static class NqTickExportService
{
    public static readonly string[] CsvColumns =
    [
        "timestamp_utc",
        "source_timestamp",
        "source_timezone",
        "root",
        "contract",
        "event_type",
        "price",
        "size",
        "bid_price",
        "bid_size",
        "ask_price",
        "ask_size",
        "exchange",
        "sequence",
        "conditions",
        "source_table",
        "source_row_id"
    ];

    private static readonly Regex NqContractPattern = new(@"^NQ[FGHJKMNQUVXZ](\d{2}|\d{4})(\b|[^A-Z0-9].*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MnqContractPattern = new(@"^MNQ[FGHJKMNQUVXZ](\d{2}|\d{4})(\b|[^A-Z0-9].*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static async Task<NqTickExportResult> ExportAsync(INqTickExportSource source, NqTickExportRequest request, CancellationToken cancellationToken)
    {
        if (!request.NoExecution)
        {
            throw new InvalidOperationException("NQ tick export is read-only and requires --no-execution true.");
        }

        var shareDirectory = Path.Combine(request.OutputRoot, "share");
        var validationDirectory = Path.Combine(request.OutputRoot, "10_validation");
        Directory.CreateDirectory(shareDirectory);
        Directory.CreateDirectory(validationDirectory);

        var warnings = new List<string>();
        var failures = new List<string>();
        var tables = await source.DiscoverTablesAsync(cancellationToken);
        var tickKind = request.TickKind ?? InferDefaultTickKind(tables, warnings);
        var query = new NqTickExportQuery(tables, request.FromUtc, request.ToUtc, request.Contract, request.MaxRows, tickKind, request.AssumeSourceTimezone);

        var relativeDataPath = $"share/nq_ticks_{request.RunKey}.csv.gz";
        var dataPath = Path.Combine(request.OutputRoot, relativeDataPath.Replace('/', Path.DirectorySeparatorChar));
        var metadataPath = Path.Combine(shareDirectory, $"nq_ticks_{request.RunKey}.metadata.json");
        EnsureOutputFilesDoNotExist(request.OutputRoot, dataPath, metadataPath);
        long rowCount = 0;
        long nullTimestamp = 0;
        long nullPriceTrade = 0;
        long nullSizeTrade = 0;
        long negativePrice = 0;
        long zeroOrNegativeTradeSize = 0;
        long duplicates = 0;
        var contracts = new SortedDictionary<string, MutableContractCoverage>(StringComparer.OrdinalIgnoreCase);
        var excluded = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var tablesUsed = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        string? previousLine = null;
        DateTimeOffset? previousTimestamp = null;
        var monotonic = true;
        var limitReached = false;
        var wroteDataFile = false;

        await using (var file = File.Create(dataPath))
        await using (var gzip = new GZipStream(file, CompressionLevel.SmallestSize))
        await using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
        {
            await writer.WriteLineAsync(string.Join(",", CsvColumns));
            await foreach (var sourceRow in source.ReadRowsAsync(query, cancellationToken))
            {
                var classification = ClassifyInstrument(sourceRow.Root, sourceRow.Contract);
                if (classification == InstrumentClassification.Mnq)
                {
                    if (!string.IsNullOrWhiteSpace(sourceRow.Contract)) excluded.Add(sourceRow.Contract);
                    continue;
                }

                if (classification != InstrumentClassification.Nq)
                {
                    continue;
                }

                if (sourceRow.TimestampUtc is null)
                {
                    nullTimestamp++;
                    failures.AddOnce("Timestamp timezone/source is ambiguous; row was not exported.");
                    continue;
                }

                if (request.FromUtc is not null && sourceRow.TimestampUtc < request.FromUtc) continue;
                if (request.ToUtc is not null && sourceRow.TimestampUtc > request.ToUtc) continue;
                if (!string.IsNullOrWhiteSpace(request.Contract) && !string.Equals(NormalizeContract(sourceRow.Contract), NormalizeContract(request.Contract), StringComparison.OrdinalIgnoreCase)) continue;
                if (!EventTypeAllowed(sourceRow.EventType, tickKind)) continue;
                if (request.MaxRows is not null && rowCount >= request.MaxRows.Value)
                {
                    limitReached = true;
                    break;
                }

                var normalized = Normalize(sourceRow);
                if (normalized.EventType == "trade")
                {
                    if (string.IsNullOrWhiteSpace(normalized.Price)) nullPriceTrade++;
                    if (string.IsNullOrWhiteSpace(normalized.Size)) nullSizeTrade++;
                    if (TryParseDecimal(normalized.Size, out var size) && size <= 0m) zeroOrNegativeTradeSize++;
                }

                if (TryParseDecimal(normalized.Price, out var price) && price < 0m) negativePrice++;
                if (previousTimestamp is not null && normalized.TimestampUtc < previousTimestamp) monotonic = false;

                var line = ToCsvLine(normalized);
                if (line == previousLine) duplicates++;
                await writer.WriteLineAsync(line);
                previousLine = line;
                previousTimestamp = normalized.TimestampUtc;
                rowCount++;
                wroteDataFile = true;
                tablesUsed.Add(normalized.SourceTable);
                UpdateCoverage(contracts, normalized);
            }
        }

        if (!wroteDataFile)
        {
            File.Delete(dataPath);
            failures.AddOnce("No NQ tick rows were exported.");
        }

        if (limitReached)
        {
            warnings.Add("--max-rows was applied explicitly; export is incomplete.");
        }

        if (excluded.Count > 0)
        {
            warnings.Add("MNQ contracts were detected and excluded from the NQ export.");
        }

        if (nullTimestamp > 0 && string.IsNullOrWhiteSpace(request.AssumeSourceTimezone))
        {
            failures.AddOnce("Timezone could not be proven for at least one candidate NQ row and --assume-source-timezone was not provided.");
        }

        var sha = wroteDataFile ? Sha256File(dataPath) : null;
        var bytes = wroteDataFile ? new FileInfo(dataPath).Length : 0;
        var metadata = BuildMetadata(
            request,
            source,
            tables,
            tablesUsed,
            excluded,
            contracts,
            rowCount,
            relativeDataPath,
            sha,
            bytes,
            exportIsComplete: !limitReached,
            duplicateRows: duplicates,
            nullTimestamp,
            nullPriceTrade,
            nullSizeTrade,
            negativePrice,
            zeroOrNegativeTradeSize,
            monotonic,
            warnings,
            failures);

        await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(metadata, JsonOptions()), cancellationToken);
        await WriteValidationReportAsync(validationDirectory, metadata, cancellationToken);
        await WriteManifestAsync(request.OutputRoot, metadata, metadataPath, wroteDataFile ? dataPath : null, cancellationToken);
        return new NqTickExportResult(metadata, wroteDataFile ? dataPath : null, validationDirectory);
    }

    public static IReadOnlyList<NqTickCandidateTable> InferCandidateTables(IEnumerable<(string Schema, string Table, string Column)> columns)
    {
        return columns
            .GroupBy(x => $"{x.Schema}\u001f{x.Table}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var parts = group.Key.Split('\u001f');
                var names = group.Select(x => x.Column).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var tableName = parts[1];
                var looksByName = ContainsAny(tableName, "Tick", "Ticks", "Trade", "Trades", "Quote", "Quotes", "Bbo", "Level1", "MarketData", "Futures", "Polygon", "Massive", "Raw");
                var timestamp = Find(names, "timestamp", "time", "ts", "event_time", "sip_timestamp", "participant_timestamp", "exchange_timestamp");
                var root = FindExactOrContains(names, "root");
                var contract = Find(names, "symbol", "ticker", "contract", "instrument");
                var price = Find(names, "trade_price", "last_price", "price");
                var size = Find(names, "quantity", "volume", "size");
                var bidPrice = Find(names, "bid_price", "best_bid", "bid");
                var bidSize = Find(names, "bid_size");
                var askPrice = Find(names, "ask_price", "best_ask", "ask");
                var askSize = Find(names, "ask_size");
                var exchange = FindExactOrContains(names, "exchange");
                var sequence = FindExactOrContains(names, "sequence");
                var conditions = FindExactOrContains(names, "conditions");
                var source = FindExactOrContains(names, "source");
                var sourceRowId = Find(names, "id", "row_id", "source_row_id");
                var supportsTrades = price is not null || tableName.Contains("Trade", StringComparison.OrdinalIgnoreCase);
                var supportsQuotes = bidPrice is not null || askPrice is not null || tableName.Contains("Quote", StringComparison.OrdinalIgnoreCase) || tableName.Contains("Bbo", StringComparison.OrdinalIgnoreCase);
                var compatible = looksByName && timestamp is not null && (root is not null || contract is not null) && (supportsTrades || supportsQuotes);
                return compatible
                    ? new NqTickCandidateTable(parts[0], parts[1], names, timestamp, root, contract, price, size, bidPrice, bidSize, askPrice, askSize, exchange, sequence, conditions, source, sourceRowId, supportsTrades, supportsQuotes, "Candidate tick table inferred from name, timestamp, instrument/root, and trade or quote columns.")
                    : null;
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderBy(x => x.Schema, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Table, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static NqTickKind InferDefaultTickKind(IReadOnlyList<NqTickCandidateTable> tables, List<string> warnings)
    {
        if (tables.Any(x => x.SupportsQuotes) && tables.Any(x => x.SupportsTrades)) return NqTickKind.All;
        if (tables.Any(x => x.SupportsQuotes) && !tables.Any(x => x.SupportsTrades)) return NqTickKind.Quotes;
        warnings.Add("Tick kind was not provided; defaulted to trades because candidate schema does not expose quote/BBO columns.");
        return NqTickKind.Trades;
    }

    private static NqTickMetadataRow Normalize(NqTickSourceRow row)
    {
        var contract = NormalizeContract(row.Contract) ?? string.Empty;
        var eventType = NormalizeEventType(row.EventType);
        return new NqTickMetadataRow(
            row.TimestampUtc!.Value,
            row.SourceTimestamp,
            row.SourceTimezone,
            "NQ",
            contract,
            eventType,
            Clean(row.Price),
            Clean(row.Size),
            Clean(row.BidPrice),
            Clean(row.BidSize),
            Clean(row.AskPrice),
            Clean(row.AskSize),
            Clean(row.Exchange),
            Clean(row.Sequence),
            Clean(row.Conditions),
            row.SourceTable,
            Clean(row.SourceRowId));
    }

    private static InstrumentClassification ClassifyInstrument(string? root, string? contract)
    {
        if (!string.IsNullOrWhiteSpace(root))
        {
            if (root.Equals("NQ", StringComparison.OrdinalIgnoreCase)) return InstrumentClassification.Nq;
            if (root.Equals("MNQ", StringComparison.OrdinalIgnoreCase)) return InstrumentClassification.Mnq;
            return InstrumentClassification.Other;
        }

        var normalized = NormalizeContract(contract);
        if (string.IsNullOrWhiteSpace(normalized)) return InstrumentClassification.Unknown;
        if (MnqContractPattern.IsMatch(normalized)) return InstrumentClassification.Mnq;
        if (NqContractPattern.IsMatch(normalized)) return InstrumentClassification.Nq;
        return InstrumentClassification.Other;
    }

    private static bool EventTypeAllowed(string eventType, NqTickKind kind)
    {
        var normalized = NormalizeEventType(eventType);
        return kind == NqTickKind.All ||
               kind == NqTickKind.Trades && normalized == "trade" ||
               kind == NqTickKind.Quotes && normalized is "quote" or "bbo";
    }

    private static string NormalizeEventType(string eventType)
    {
        if (eventType.Equals("trade", StringComparison.OrdinalIgnoreCase) || eventType.Equals("trades", StringComparison.OrdinalIgnoreCase)) return "trade";
        if (eventType.Equals("quote", StringComparison.OrdinalIgnoreCase) || eventType.Equals("quotes", StringComparison.OrdinalIgnoreCase)) return "quote";
        if (eventType.Equals("bbo", StringComparison.OrdinalIgnoreCase) || eventType.Equals("level1", StringComparison.OrdinalIgnoreCase)) return "bbo";
        return "unknown";
    }

    private static string? NormalizeContract(string? contract)
        => string.IsNullOrWhiteSpace(contract) ? null : contract.Trim().ToUpperInvariant();

    private static string ToCsvLine(NqTickMetadataRow row)
        => string.Join(",", new[]
        {
            Csv(row.TimestampUtc.ToString("O", CultureInfo.InvariantCulture)),
            Csv(row.SourceTimestamp),
            Csv(row.SourceTimezone),
            Csv(row.Root),
            Csv(row.Contract),
            Csv(row.EventType),
            Csv(row.Price),
            Csv(row.Size),
            Csv(row.BidPrice),
            Csv(row.BidSize),
            Csv(row.AskPrice),
            Csv(row.AskSize),
            Csv(row.Exchange),
            Csv(row.Sequence),
            Csv(row.Conditions),
            Csv(row.SourceTable),
            Csv(row.SourceRowId)
        });

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }

    private static void UpdateCoverage(SortedDictionary<string, MutableContractCoverage> contracts, NqTickMetadataRow row)
    {
        if (!contracts.TryGetValue(row.Contract, out var coverage))
        {
            coverage = new MutableContractCoverage(row.Contract);
            contracts[row.Contract] = coverage;
        }

        coverage.RowCount++;
        coverage.FirstTimestampUtc = coverage.FirstTimestampUtc is null || row.TimestampUtc < coverage.FirstTimestampUtc ? row.TimestampUtc : coverage.FirstTimestampUtc;
        coverage.LastTimestampUtc = coverage.LastTimestampUtc is null || row.TimestampUtc > coverage.LastTimestampUtc ? row.TimestampUtc : coverage.LastTimestampUtc;
        coverage.EventTypeCounts[row.EventType] = coverage.EventTypeCounts.GetValueOrDefault(row.EventType) + 1;
    }

    private static NqTickExportMetadata BuildMetadata(
        NqTickExportRequest request,
        INqTickExportSource source,
        IReadOnlyList<NqTickCandidateTable> tables,
        IEnumerable<string> tablesUsed,
        IEnumerable<string> excluded,
        SortedDictionary<string, MutableContractCoverage> contracts,
        long rowCount,
        string dataRelativePath,
        string? sha,
        long bytes,
        bool exportIsComplete,
        long duplicateRows,
        long nullTimestamp,
        long nullPriceTrade,
        long nullSizeTrade,
        long negativePrice,
        long zeroOrNegativeTradeSize,
        bool monotonic,
        List<string> warnings,
        List<string> failures)
    {
        var status = failures.Count > 0 ? "FAIL" : warnings.Count > 0 ? "WARN" : "PASS";
        var perContract = contracts.Values
            .Select(x => new NqTickContractCoverage(x.Contract, x.RowCount, x.FirstTimestampUtc, x.LastTimestampUtc, FillEventCounts(x.EventTypeCounts)))
            .ToArray();
        return new NqTickExportMetadata(
            request.RunKey,
            DateTimeOffset.UtcNow,
            status,
            exportIsComplete,
            new NqTickDataFileMetadata(dataRelativePath, "csv.gz", sha, bytes, rowCount),
            new NqTickInstrumentMetadata("NQ", "E-mini Nasdaq-100 futures", ["MNQ"], contracts.Keys.ToArray(), excluded.ToArray()),
            new NqTickCoverageMetadata(perContract.Select(x => x.FirstTimestampUtc).Where(x => x is not null).DefaultIfEmpty().Min(), perContract.Select(x => x.LastTimestampUtc).Where(x => x is not null).DefaultIfEmpty().Max(), perContract),
            new NqTickSourceMetadata(source.DatabaseDescription, tables.Select(x => $"{x.Schema}.{x.Table}").ToArray(), tablesUsed.ToArray(), true),
            new NqTickSchemaMetadata(CsvColumns.Select(ColumnMetadata).ToArray()),
            new NqTickQualityMetadata(duplicateRows, nullTimestamp, nullPriceTrade, nullSizeTrade, negativePrice, zeroOrNegativeTradeSize, !string.IsNullOrWhiteSpace(request.AssumeSourceTimezone), monotonic),
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            failures.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static IReadOnlyDictionary<string, long> FillEventCounts(IReadOnlyDictionary<string, long> counts)
        => new SortedDictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            ["trade"] = counts.GetValueOrDefault("trade"),
            ["quote"] = counts.GetValueOrDefault("quote"),
            ["bbo"] = counts.GetValueOrDefault("bbo"),
            ["unknown"] = counts.GetValueOrDefault("unknown")
        };

    private static NqTickColumnMetadata ColumnMetadata(string name)
        => new(name, "string", name == "timestamp_utc" ? "UTC event timestamp in ISO-8601 format" : $"Normalized NQ tick export column: {name}");

    private static async Task WriteValidationReportAsync(string validationDirectory, NqTickExportMetadata metadata, CancellationToken cancellationToken)
    {
        var jsonPath = Path.Combine(validationDirectory, "nq_tick_export_report.json");
        var mdPath = Path.Combine(validationDirectory, "nq_tick_export_report.md");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(metadata, JsonOptions()), cancellationToken);
        await File.WriteAllTextAsync(mdPath, RenderMarkdown(metadata), cancellationToken);
    }

    private static void EnsureOutputFilesDoNotExist(string outputRoot, string dataPath, string metadataPath)
    {
        string[] paths =
        [
            dataPath,
            metadataPath,
            Path.Combine(outputRoot, "10_validation", "nq_tick_export_report.json"),
            Path.Combine(outputRoot, "10_validation", "nq_tick_export_report.md"),
            Path.Combine(outputRoot, "manifest.json"),
            Path.Combine(outputRoot, "manifest.sha256"),
            Path.Combine(outputRoot, "hashes.json")
        ];

        var existing = paths.FirstOrDefault(File.Exists);
        if (existing is not null)
        {
            throw new IOException($"NQ tick export refuses to overwrite existing artifact: {existing}");
        }
    }

    private static async Task WriteManifestAsync(string outputRoot, NqTickExportMetadata metadata, string metadataPath, string? dataPath, CancellationToken cancellationToken)
    {
        var files = new List<string> { metadataPath, Path.Combine(outputRoot, "10_validation", "nq_tick_export_report.json"), Path.Combine(outputRoot, "10_validation", "nq_tick_export_report.md") };
        if (dataPath is not null) files.Insert(0, dataPath);
        var hashes = files.Select(path => new { path = Path.GetRelativePath(outputRoot, path).Replace('\\', '/'), sha256 = Sha256File(path) }).ToArray();
        var manifest = new
        {
            packageKind = "NqTickExportForExternalAnalysis",
            packageVersion = "1.0",
            metadata.RunKey,
            metadata.ExportStatus,
            metadata.ExportIsComplete,
            readOnly = true,
            noQubesSignalAudit = true,
            noLegacyAhiGenerated = true,
            files = hashes.Select(x => x.path).ToArray(),
            supportFiles = new[] { "hashes.json", "manifest.sha256" }
        };
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "manifest.json"), JsonSerializer.Serialize(manifest, JsonOptions()), cancellationToken);
        var manifestHash = Sha256File(Path.Combine(outputRoot, "manifest.json"));
        var allHashes = hashes.Append(new { path = "manifest.json", sha256 = manifestHash }).ToArray();
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "hashes.json"), JsonSerializer.Serialize(allHashes, JsonOptions()), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "manifest.sha256"), manifestHash + "  manifest.json" + Environment.NewLine, Encoding.ASCII, cancellationToken);
    }

    private static string RenderMarkdown(NqTickExportMetadata metadata)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# NQ Tick Export Report");
        builder.AppendLine();
        builder.AppendLine($"1. Exported ticks: `{metadata.DataFile.RowCount}`");
        builder.AppendLine($"2. Coverage: `{metadata.Coverage.FirstTimestampUtc:O}` to `{metadata.Coverage.LastTimestampUtc:O}`");
        builder.AppendLine($"3. Contracts: `{string.Join(", ", metadata.Instrument.ContractsIncluded)}`");
        builder.AppendLine($"4. Event types: `{string.Join(", ", metadata.Coverage.PerContract.SelectMany(x => x.EventTypeCounts.Where(kv => kv.Value > 0).Select(kv => kv.Key)).Distinct())}`");
        builder.AppendLine($"5. Timezone certain: `{!metadata.Quality.TimezoneAssumptionUsed && metadata.Failures.All(x => !x.Contains("Timezone", StringComparison.OrdinalIgnoreCase))}`");
        builder.AppendLine($"6. Tables used: `{string.Join(", ", metadata.Source.TablesUsed)}`");
        builder.AppendLine($"7. Warnings: `{string.Join(" | ", metadata.Warnings)}`");
        builder.AppendLine($"8. Complete: `{metadata.ExportIsComplete}`");
        builder.AppendLine($"9. File for Yannik: `{metadata.DataFile.RelativePath}`");
        builder.AppendLine($"10. SHA256: `{metadata.DataFile.Sha256}`");
        return builder.ToString();
    }

    private static bool TryParseDecimal(string? value, out decimal result)
        => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result);

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Find(IReadOnlyList<string> names, params string[] candidates)
        => candidates.Select(candidate => names.FirstOrDefault(x => x.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(x => x is not null) ??
           candidates.Select(candidate => names.FirstOrDefault(x => x.Contains(candidate, StringComparison.OrdinalIgnoreCase)))
               .FirstOrDefault(x => x is not null);

    private static string? FindExactOrContains(IReadOnlyList<string> names, string candidate)
        => names.FirstOrDefault(x => x.Equals(candidate, StringComparison.OrdinalIgnoreCase)) ??
           names.FirstOrDefault(x => x.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsAny(string value, params string[] fragments)
        => fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static JsonSerializerOptions JsonOptions()
        => new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, Converters = { new JsonStringEnumConverter() } };

    private enum InstrumentClassification { Nq, Mnq, Other, Unknown }

    private sealed record NqTickMetadataRow(
        DateTimeOffset TimestampUtc,
        string SourceTimestamp,
        string SourceTimezone,
        string Root,
        string Contract,
        string EventType,
        string? Price,
        string? Size,
        string? BidPrice,
        string? BidSize,
        string? AskPrice,
        string? AskSize,
        string? Exchange,
        string? Sequence,
        string? Conditions,
        string SourceTable,
        string? SourceRowId);

    private sealed class MutableContractCoverage(string contract)
    {
        public string Contract { get; } = contract;
        public long RowCount { get; set; }
        public DateTimeOffset? FirstTimestampUtc { get; set; }
        public DateTimeOffset? LastTimestampUtc { get; set; }
        public Dictionary<string, long> EventTypeCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static void AddOnce(this List<string> values, string value)
    {
        if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            values.Add(value);
        }
    }
}
