using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

var options = CliOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine("Usage: dotnet run --project tools/QQ.Production.Intraday.Tools.NqTickExportFailureDiagnostic -- --run-key <RunKey> --output-root <path> [--connection-string <sql>] --no-execution true");
    return 0;
}

if (!options.NoExecution)
{
    Console.Error.WriteLine("NQ tick export failure diagnostic is read-only and requires --no-execution true.");
    return 2;
}

var runKey = options.RunKey ?? "nq-ticks-yannik-001";
var outputRoot = options.OutputRoot ?? Path.Combine("artifacts", "nq-tick-export", runKey);
var validationRoot = Path.Combine(outputRoot, "10_validation");
Directory.CreateDirectory(validationRoot);

var existingArtifacts = ExistingArtifacts.Read(outputRoot, runKey);
var connection = ResolveConnectionString(options.ConnectionString);
var source = new SqlDiagnosticSource(connection.ConnectionString);
var diagnostic = await DiagnosticBuilder.BuildAsync(runKey, outputRoot, existingArtifacts, source, CancellationToken.None);
await DiagnosticWriter.WriteAsync(outputRoot, diagnostic, CancellationToken.None);

Console.WriteLine($"failure_reason={diagnostic.FailureDiagnostic.NqTickExportFailureReason}");
Console.WriteLine($"nq_tick_data_exists={diagnostic.FailureDiagnostic.NqTickDataExists}");
Console.WriteLine($"minimal_patch_required={diagnostic.FailureDiagnostic.MinimalPatchRequired}");
Console.WriteLine($"safe_to_share_with_yannik={diagnostic.FailureDiagnostic.SafeToShareWithYannik}");
Console.WriteLine($"diagnostic={Path.Combine(validationRoot, "nq_tick_export_failure_diagnostic.json")}");
Console.WriteLine($"symbol_discovery={Path.Combine(validationRoot, "nq_tick_symbol_discovery_report.json")}");
return 0;

static ResolvedConnectionString ResolveConnectionString(string? explicitConnectionString)
{
    if (!string.IsNullOrWhiteSpace(explicitConnectionString))
    {
        return new ResolvedConnectionString(NormalizeReadOnlyConnectionString(explicitConnectionString), "cli:--connection-string");
    }

    var env = Environment.GetEnvironmentVariable("QQ_INTRADAY_SQLSERVER_CONNECTIONSTRING");
    if (!string.IsNullOrWhiteSpace(env))
    {
        return new ResolvedConnectionString(NormalizeReadOnlyConnectionString(env), "env:QQ_INTRADAY_SQLSERVER_CONNECTIONSTRING");
    }

    var appsettingsPath = Path.Combine("src", "QQ.Production.Intraday.Api", "appsettings.json");
    var discovered = TryReadConnectionString(appsettingsPath);
    if (!string.IsNullOrWhiteSpace(discovered))
    {
        return new ResolvedConnectionString(NormalizeReadOnlyConnectionString(discovered), appsettingsPath);
    }

    throw new InvalidOperationException("No DB connection string found. Provide --connection-string, QQ_INTRADAY_SQLSERVER_CONNECTIONSTRING, or appsettings.json ConnectionStrings:IntradaySqlServer.");
}

static string? TryReadConnectionString(string path)
{
    if (!File.Exists(path)) return null;
    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    return doc.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings) &&
           connectionStrings.TryGetProperty("IntradaySqlServer", out var intraday)
        ? intraday.GetString()
        : null;
}

static string NormalizeReadOnlyConnectionString(string connectionString)
{
    var builder = new SqlConnectionStringBuilder(connectionString)
    {
        ApplicationIntent = ApplicationIntent.ReadOnly
    };
    return builder.ConnectionString;
}

internal sealed record ResolvedConnectionString(string ConnectionString, string Source);

internal sealed record CliOptions(string? RunKey, string? OutputRoot, string? ConnectionString, bool NoExecution, bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        string? runKey = null;
        string? outputRoot = null;
        string? connectionString = null;
        var noExecution = false;
        var showHelp = false;

        for (var index = 0; index < args.Length; index++)
        {
            var current = args[index];
            if (current is "--help" or "-h")
            {
                showHelp = true;
                continue;
            }

            var value = index + 1 < args.Length ? args[index + 1] : null;
            if (value is null || value.StartsWith("--", StringComparison.Ordinal)) continue;
            switch (current)
            {
                case "--run-key": runKey = value; index++; break;
                case "--output-root": outputRoot = value; index++; break;
                case "--connection-string": connectionString = value; index++; break;
                case "--no-execution": noExecution = bool.TryParse(value, out var parsed) && parsed; index++; break;
            }
        }

        return new CliOptions(runKey, outputRoot, connectionString, noExecution, showHelp);
    }
}

internal sealed record ExistingArtifacts(
    string MetadataPath,
    string ReportJsonPath,
    string ReportMarkdownPath,
    JsonElement? Metadata,
    JsonElement? ReportJson,
    string? ReportMarkdown)
{
    public static ExistingArtifacts Read(string outputRoot, string runKey)
    {
        var metadataPath = Path.Combine(outputRoot, "share", $"nq_ticks_{runKey}.metadata.json");
        var reportJsonPath = Path.Combine(outputRoot, "10_validation", "nq_tick_export_report.json");
        var reportMarkdownPath = Path.Combine(outputRoot, "10_validation", "nq_tick_export_report.md");
        return new ExistingArtifacts(
            metadataPath,
            reportJsonPath,
            reportMarkdownPath,
            ReadJson(metadataPath),
            ReadJson(reportJsonPath),
            File.Exists(reportMarkdownPath) ? File.ReadAllText(reportMarkdownPath) : null);
    }

    private static JsonElement? ReadJson(string path)
    {
        if (!File.Exists(path)) return null;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.Clone();
    }
}

internal sealed class SqlDiagnosticSource(string connectionString)
{
    private static readonly string[] TableNameFragments =
    [
        "Tick", "Ticks", "Trade", "Trades", "Quote", "Quotes", "Bbo", "Level1",
        "MarketData", "Futures", "Polygon", "Massive", "Raw", "Bar", "Bars"
    ];

    public string DatabaseDescription
    {
        get
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "(configured database)" : builder.InitialCatalog;
        }
    }

    public async Task<IReadOnlyList<TableDiagnostic>> InspectAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var columnRows = await ReadCandidateColumnsAsync(connection, cancellationToken);
        var instrumentSymbolsById = await ReadInstrumentSymbolsByIdAsync(connection, cancellationToken);
        var tables = new List<TableDiagnostic>();
        foreach (var group in columnRows.GroupBy(x => (x.Schema, x.Table)).OrderBy(x => x.Key.Schema).ThenBy(x => x.Key.Table))
        {
            var columns = group.Select(x => new ColumnDiagnostic(x.Column, x.DataType)).ToArray();
            var inferred = SchemaInference.Infer(group.Key.Schema, group.Key.Table, columns);
            var rowCount = await CountRowsAsync(connection, group.Key.Schema, group.Key.Table, cancellationToken);
            var minMax = inferred.TimestampColumns.Count > 0
                ? await ReadMinMaxAsync(connection, group.Key.Schema, group.Key.Table, inferred.TimestampColumns[0], cancellationToken)
                : new TimestampRange(null, null);
            var symbols = new List<SymbolColumnDiagnostic>();
            foreach (var column in inferred.SymbolColumns)
            {
                symbols.Add(await ReadSymbolsAsync(connection, group.Key.Schema, group.Key.Table, column, instrumentSymbolsById, cancellationToken));
            }

            tables.Add(inferred with
            {
                RowCount = rowCount,
                FirstTimestamp = minMax.First,
                LastTimestamp = minMax.Last,
                SymbolDiscovery = symbols
            });
        }

        return tables;
    }

    private static async Task<IReadOnlyList<(string Schema, string Table, string Column, string DataType)>> ReadCandidateColumnsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var clauses = TableNameFragments.Select((_, i) => $"TABLE_NAME LIKE @p{i}").ToArray();
        var sql = $"""
            SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE {string.Join(" OR ", clauses)}
            ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION;
            """;
        await using var command = new SqlCommand(sql, connection);
        for (var index = 0; index < TableNameFragments.Length; index++)
        {
            command.Parameters.Add(new SqlParameter($"@p{index}", SqlDbType.NVarChar, 128) { Value = $"%{TableNameFragments[index]}%" });
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<(string Schema, string Table, string Column, string DataType)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        }

        return rows;
    }

    private static async Task<long?> CountRowsAsync(SqlConnection connection, string schema, string table, CancellationToken cancellationToken)
    {
        var sql = $"SELECT COUNT_BIG(*) FROM {Quote(schema)}.{Quote(table)};";
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    private static async Task<TimestampRange> ReadMinMaxAsync(SqlConnection connection, string schema, string table, string column, CancellationToken cancellationToken)
    {
        var sql = $"SELECT MIN({Quote(column)}), MAX({Quote(column)}) FROM {Quote(schema)}.{Quote(table)};";
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new TimestampRange(null, null);
        return new TimestampRange(ReadString(reader, 0), ReadString(reader, 1));
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ReadInstrumentSymbolsByIdAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string existsSql = """
            SELECT COUNT_BIG(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Instruments';
            """;
        await using (var existsCommand = new SqlCommand(existsSql, connection))
        {
            var exists = Convert.ToInt64(await existsCommand.ExecuteScalarAsync(cancellationToken));
            if (exists == 0) return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        const string sql = """
            SELECT CAST(Id AS nvarchar(64)) AS InstrumentId, CAST(Symbol AS nvarchar(4000)) AS SymbolValue
            FROM dbo.Instruments
            WHERE Symbol IS NOT NULL
            UNION ALL
            SELECT CAST(InstrumentId AS nvarchar(64)) AS InstrumentId, CAST(ExternalSymbol AS nvarchar(4000)) AS SymbolValue
            FROM dbo.InstrumentAliases
            WHERE ExternalSymbol IS NOT NULL
            UNION ALL
            SELECT CAST(InstrumentId AS nvarchar(64)) AS InstrumentId, CAST(ExternalInstrumentId AS nvarchar(4000)) AS SymbolValue
            FROM dbo.InstrumentAliases
            WHERE ExternalInstrumentId IS NOT NULL;
            """;
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var values = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetString(0);
            var symbol = reader.GetString(1);
            if (!values.TryGetValue(id, out var list))
            {
                list = [];
                values[id] = list;
            }

            if (!list.Contains(symbol, StringComparer.OrdinalIgnoreCase)) list.Add(symbol);
        }

        return values.ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<SymbolColumnDiagnostic> ReadSymbolsAsync(SqlConnection connection, string schema, string table, string column, IReadOnlyDictionary<string, IReadOnlyList<string>> instrumentSymbolsById, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT TOP (500) CAST({Quote(column)} AS nvarchar(4000)) AS symbol_value, COUNT_BIG(*) AS row_count
            FROM {Quote(schema)}.{Quote(table)}
            WHERE {Quote(column)} IS NOT NULL
            GROUP BY CAST({Quote(column)} AS nvarchar(4000))
            ORDER BY COUNT_BIG(*) DESC, CAST({Quote(column)} AS nvarchar(4000)) ASC;
            """;
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var values = new List<SymbolValueDiagnostic>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var value = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var count = Convert.ToInt64(reader.GetValue(1));
            var resolvedSymbols = instrumentSymbolsById.TryGetValue(value, out var mapped) ? mapped : [];
            var classification = NqSymbolClassifier.Classify(value, resolvedSymbols);
            values.Add(new SymbolValueDiagnostic(value, count, resolvedSymbols, classification.Kind, classification.IsContinuous, classification.Why));
        }

        return new SymbolColumnDiagnostic(
            column,
            values.Count,
            values.Count(x => x.Classification == "NQ"),
            values.Count(x => x.Classification == "MNQ"),
            values.Where(x => x.Classification is "NQ" or "MNQ" || x.Value.Contains("NQ", StringComparison.OrdinalIgnoreCase)).Take(50).ToArray(),
            values.Take(50).ToArray());
    }

    private static string Quote(string identifier)
        => "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";

    private static string? ReadString(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);
}

internal static class SchemaInference
{
    public static TableDiagnostic Infer(string schema, string table, IReadOnlyList<ColumnDiagnostic> columns)
    {
        var names = columns.Select(x => x.Name).ToArray();
        var timestamp = FindTimestamp(columns);
        var symbolColumns = names.Where(IsSymbolColumn).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var tradeColumns = names.Where(x => ContainsAny(x, "price", "trade_price", "last_price")).ToArray();
        var sizeColumns = names.Where(x => ContainsAny(x, "size", "quantity", "volume")).ToArray();
        var bidAskColumns = names.Where(x => ContainsAny(x, "bid", "ask", "best_bid", "best_ask")).ToArray();
        var isBar = table.Contains("Bar", StringComparison.OrdinalIgnoreCase) ||
                    names.Any(x => x.Equals("open", StringComparison.OrdinalIgnoreCase) || x.Equals("high", StringComparison.OrdinalIgnoreCase) || x.Equals("low", StringComparison.OrdinalIgnoreCase) || x.Equals("close", StringComparison.OrdinalIgnoreCase)) ||
                    names.Any(x => x.Contains("timeframe", StringComparison.OrdinalIgnoreCase) || x.Contains("interval", StringComparison.OrdinalIgnoreCase));
        var hasPriceSurface = tradeColumns.Length > 0 || bidAskColumns.Length > 0;
        var status = isBar ? "BAR_ONLY" :
            timestamp is null || symbolColumns.Length == 0 || !hasPriceSurface ? "UNSUPPORTED_SCHEMA" :
            "TICK_CANDIDATE";
        var reasons = new List<string>();
        if (timestamp is null) reasons.Add("no timestamp column");
        if (symbolColumns.Length == 0) reasons.Add("no symbol/root/contract/instrument column");
        if (!hasPriceSurface) reasons.Add("no price/bid/ask columns");
        if (isBar) reasons.Add("table contains bars not ticks");

        return new TableDiagnostic(
            $"{schema}.{table}",
            schema,
            table,
            null,
            columns,
            timestamp is null ? [] : [timestamp],
            symbolColumns,
            tradeColumns.Concat(sizeColumns).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            bidAskColumns,
            null,
            null,
            status,
            reasons.Count == 0 ? ["candidate tick schema"] : reasons,
            []);
    }

    private static bool IsSymbolColumn(string value)
        => value.Equals("symbol", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("ticker", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("contract", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("instrument", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("root", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("symbol", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("ticker", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("contract", StringComparison.OrdinalIgnoreCase) ||
           value.Contains("instrument", StringComparison.OrdinalIgnoreCase);

    private static string? FindTimestamp(IReadOnlyList<ColumnDiagnostic> columns)
    {
        var timestampColumns = columns
            .Where(column => IsTemporalDataType(column.DataType))
            .Select(column => column.Name)
            .Where(name => !name.Equals("Timeframe", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return Find(timestampColumns, "source_timestamp_utc", "sourcetimestamputc", "timestamp_utc", "timestamputc", "timestamp", "datetimeutc", "event_time", "sip_timestamp", "participant_timestamp", "exchange_timestamp", "barstartutc", "barendutc", "receivedatutc", "createdatutc", "startedatutc", "completedatutc", "reportdate", "trade_date", "date");
    }

    private static bool IsTemporalDataType(string value)
        => value.Contains("date", StringComparison.OrdinalIgnoreCase) || value.Contains("time", StringComparison.OrdinalIgnoreCase);

    private static string? Find(IReadOnlyList<string> names, params string[] candidates)
        => candidates.Select(candidate => names.FirstOrDefault(x => x.Equals(candidate, StringComparison.OrdinalIgnoreCase))).FirstOrDefault(x => x is not null) ??
           candidates.Select(candidate => names.FirstOrDefault(x => x.Contains(candidate, StringComparison.OrdinalIgnoreCase))).FirstOrDefault(x => x is not null);

    private static bool ContainsAny(string value, params string[] fragments)
        => fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}

internal static class NqSymbolClassifier
{
    private static readonly Regex NqContract = new(@"^(?:[A-Z_]+:)?[/@]?NQ[FGHJKMNQUVXZ](?:\d{2}|\d{4})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MnqContract = new(@"^(?:[A-Z_]+:)?[/@]?MNQ[FGHJKMNQUVXZ](?:\d{2}|\d{4})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NqContinuous = new(@"^(?:[A-Z_]+:)?[/@]?NQ(?:1!?|_CONT|\.c(?:\.0)?|=F)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MnqContinuous = new(@"^(?:[A-Z_]+:)?[/@]?MNQ(?:1!?|_CONT|\.c(?:\.0)?|=F)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static SymbolClassification Classify(string? raw, IReadOnlyList<string>? resolvedSymbols = null)
    {
        var value = raw?.Trim() ?? string.Empty;
        if (resolvedSymbols is { Count: > 0 })
        {
            foreach (var resolved in resolvedSymbols)
            {
                var resolvedClassification = Classify(resolved);
                if (resolvedClassification.Kind is "NQ" or "MNQ")
                {
                    return resolvedClassification with { Why = $"InstrumentId resolves to {resolved}: {resolvedClassification.Why}" };
                }
            }
        }

        if (value.Length == 0) return new SymbolClassification("UNKNOWN", false, "empty value");
        if (MnqContract.IsMatch(value)) return new SymbolClassification("MNQ", false, "MNQ contract format");
        if (MnqContinuous.IsMatch(value)) return new SymbolClassification("MNQ", true, "MNQ continuous/root format");
        if (NqContract.IsMatch(value)) return new SymbolClassification("NQ", false, "NQ contract format");
        if (NqContinuous.IsMatch(value)) return new SymbolClassification("NQ", IsContinuous(value), "NQ root or continuous format");
        return value.Contains("NQ", StringComparison.OrdinalIgnoreCase)
            ? new SymbolClassification("NEAR_NQ", false, "contains NQ but did not match explicit NQ/MNQ futures formats")
            : new SymbolClassification("OTHER", false, "not NQ-like");
    }

    private static bool IsContinuous(string value)
        => value.Contains('1') || value.Contains("_CONT", StringComparison.OrdinalIgnoreCase) || value.Contains(".c", StringComparison.OrdinalIgnoreCase) || value.Contains("=F", StringComparison.OrdinalIgnoreCase);
}

internal static class DiagnosticBuilder
{
    public static async Task<DiagnosticBundle> BuildAsync(string runKey, string outputRoot, ExistingArtifacts artifacts, SqlDiagnosticSource source, CancellationToken cancellationToken)
    {
        var tables = await source.InspectAsync(cancellationToken);
        var codeFilter = AnalyzeCurrentFilter();
        var rejected = BuildRejectedReasons(tables, codeFilter);
        var tickTablesWithNq = tables.Where(x => x.Status == "TICK_CANDIDATE" && x.SymbolDiscovery.Any(s => s.NqLikeValueCount > 0)).ToArray();
        var barTablesWithNq = tables.Where(x => x.Status == "BAR_ONLY" && x.SymbolDiscovery.Any(s => s.NqLikeValueCount > 0)).ToArray();
        var tablesWithMnq = tables.Where(x => x.SymbolDiscovery.Any(s => s.MnqLikeValueCount > 0)).ToArray();
        var currentFilterMissesNq = tickTablesWithNq.Any(table => table.SymbolDiscovery.SelectMany(s => s.NqLikeExamples).Any(x => !CurrentExporterAccepts(x.Value)));

        var existingStatus = ReadString(artifacts.Metadata, "export_status");
        var existingRows = ReadLong(artifacts.Metadata, "data_file", "row_count") ?? 0;
        var safeToShare = existingRows > 0 && existingStatus is "PASS" or "WARN";
        var failureReason = ClassifyFailureReason(tickTablesWithNq.Length, barTablesWithNq.Length, tablesWithMnq.Length, currentFilterMissesNq, artifacts);
        var dataExists = tickTablesWithNq.Length > 0 ? "YES" : tables.Count == 0 ? "UNKNOWN" : "NO";
        var exportableWithCurrentTool = tickTablesWithNq.Length > 0 && !currentFilterMissesNq ? "YES" : "NO";
        var minimalPatch = failureReason is "SYMBOL_FILTER_TOO_STRICT" or "UNSUPPORTED_TABLE_SCHEMA" ? "YES" : "NO";

        var failure = new FailureDiagnostic(
            runKey,
            DateTimeOffset.UtcNow,
            artifacts.MetadataPath,
            artifacts.ReportJsonPath,
            artifacts.ReportMarkdownPath,
            existingStatus,
            existingRows,
            source.DatabaseDescription,
            tables.Select(x => x.FullName).ToArray(),
            tables.Where(x => x.Status == "TICK_CANDIDATE").Select(x => x.FullName).ToArray(),
            tables.Where(x => x.Status == "BAR_ONLY").Select(x => x.FullName).ToArray(),
            rejected,
            codeFilter,
            failureReason,
            dataExists,
            exportableWithCurrentTool,
            minimalPatch,
            safeToShare ? "YES" : "NO",
            BuildSummary(failureReason, dataExists, exportableWithCurrentTool, minimalPatch, safeToShare));

        var symbol = new SymbolDiscoveryReport(
            runKey,
            DateTimeOffset.UtcNow,
            "NQ = E-mini Nasdaq-100 futures",
            "MNQ = Micro E-mini Nasdaq-100 futures",
            tables,
            tablesWithMnq.Select(x => x.FullName).ToArray(),
            tickTablesWithNq.Select(x => x.FullName).ToArray(),
            barTablesWithNq.Select(x => x.FullName).ToArray());

        return new DiagnosticBundle(failure, symbol);
    }

    private static string ClassifyFailureReason(int tickTablesWithNq, int barTablesWithNq, int tablesWithMnq, bool currentFilterMissesNq, ExistingArtifacts artifacts)
    {
        if (artifacts.Metadata is null) return "UNKNOWN";
        if (tickTablesWithNq > 0 && currentFilterMissesNq) return "SYMBOL_FILTER_TOO_STRICT";
        if (tickTablesWithNq > 0) return "UNKNOWN";
        if (barTablesWithNq > 0) return "ONLY_BARS_FOUND";
        if (tablesWithMnq > 0) return "ONLY_MNQ_FOUND";
        return "NO_NQ_TICKS_FOUND";
    }

    private static IReadOnlyList<RejectedCandidateReason> BuildRejectedReasons(IReadOnlyList<TableDiagnostic> tables, CurrentFilterDiagnostic filter)
    {
        var reasons = new List<RejectedCandidateReason>();
        foreach (var table in tables)
        {
            if (table.RowCount == 0)
            {
                reasons.Add(new RejectedCandidateReason(table.FullName, "empty table", null, "No export patch; populate/source real NQ ticks first."));
                continue;
            }

            if (table.Status == "BAR_ONLY")
            {
                reasons.Add(new RejectedCandidateReason(table.FullName, "table contains bars not ticks", table.Columns.FirstOrDefault(x => x.Name.Contains("close", StringComparison.OrdinalIgnoreCase))?.Name, "Do not export this as ticks; source tick-level data is required."));
            }
            else if (table.Status != "TICK_CANDIDATE")
            {
                var reason = string.Join("; ", table.Reasons);
                reasons.Add(new RejectedCandidateReason(table.FullName, reason, table.Columns.FirstOrDefault()?.Name, "Add explicit source-table/column mapping only if the table really stores NQ ticks."));
            }
            else if (table.SymbolDiscovery.Count > 0 &&
                     table.SymbolDiscovery.All(x => x.NqLikeValueCount == 0) &&
                     table.SymbolDiscovery.All(x => x.MnqLikeValueCount == 0))
            {
                var example = table.SymbolDiscovery.SelectMany(x => x.TopValues).FirstOrDefault();
                var shown = example is null ? null : string.IsNullOrWhiteSpace(string.Join(" | ", example.ResolvedSymbols))
                    ? example.Value
                    : $"{example.Value} -> {string.Join(" | ", example.ResolvedSymbols)}";
                reasons.Add(new RejectedCandidateReason(table.FullName, "no NQ-like symbols found", shown, "Load/source NQ futures tick data or provide an explicit mapping only if this table truly contains NQ."));
            }

            foreach (var symbol in table.SymbolDiscovery.SelectMany(x => x.NqLikeExamples))
            {
                var accepted = filter.Examples.FirstOrDefault(x => string.Equals(x.Value, symbol.Value, StringComparison.OrdinalIgnoreCase))?.AcceptedByCurrentFilter;
                if (accepted == false)
                {
                    reasons.Add(new RejectedCandidateReason(table.FullName, "NQ symbol format not recognized by current filter", symbol.Value, "Add this explicit NQ futures format to the filter without admitting MNQ."));
                }
            }

            if (table.SymbolDiscovery.Count > 0 &&
                table.SymbolDiscovery.All(x => x.NqLikeValueCount == 0) &&
                table.SymbolDiscovery.Any(x => x.MnqLikeValueCount > 0))
            {
                reasons.Add(new RejectedCandidateReason(table.FullName, "only MNQ symbols found", table.SymbolDiscovery.SelectMany(x => x.NqLikeExamples).FirstOrDefault()?.Value, "Do not include MNQ in an NQ export."));
            }
        }

        return reasons;
    }

    private static CurrentFilterDiagnostic AnalyzeCurrentFilter()
    {
        var examples = new[]
        {
            "NQH25", "NQM25", "NQU25", "NQZ25", "NQH2025", "/NQ", "@NQ", "NQ1!", "CME_MINI:NQ1!", "NQ.c.0", "NQ=F", "MNQH25", "/MNQ", "MNQ1!"
        };
        return new CurrentFilterDiagnostic(
            "NqTickExportService.ClassifyInstrument",
            "root must equal NQ/MNQ when root exists; otherwise contract must match ^NQ[FGHJKMNQUVXZ](\\d{2}|\\d{4})(\\b|[^A-Z0-9].*)?$ and MNQ equivalent is excluded.",
            examples.Select(x => new CurrentFilterExample(x, CurrentExporterAccepts(x), NqSymbolClassifier.Classify(x).Kind)).ToArray());
    }

    private static bool CurrentExporterAccepts(string value)
        => Regex.IsMatch(value.Trim().ToUpperInvariant(), @"^NQ[FGHJKMNQUVXZ](\d{2}|\d{4})(\b|[^A-Z0-9].*)?$", RegexOptions.IgnoreCase);

    private static string BuildSummary(string failureReason, string dataExists, string exportable, string minimalPatch, bool safe)
        => $"NQ_TICK_EXPORT_FAILURE_REASON={failureReason}; NQ_TICK_DATA_EXISTS={dataExists}; NQ_TICK_EXPORTABLE_WITH_CURRENT_TOOL={exportable}; MINIMAL_PATCH_REQUIRED={minimalPatch}; SAFE_TO_SHARE_WITH_YANNIK={(safe ? "YES" : "NO")}.";

    private static string? ReadString(JsonElement? element, string property)
        => element is { } json && json.TryGetProperty(property, out var value) ? value.GetString() : null;

    private static long? ReadLong(JsonElement? element, string objectProperty, string nestedProperty)
    {
        if (element is not { } json || !json.TryGetProperty(objectProperty, out var obj) || !obj.TryGetProperty(nestedProperty, out var value)) return null;
        return value.TryGetInt64(out var parsed) ? parsed : null;
    }
}

internal static class DiagnosticWriter
{
    public static async Task WriteAsync(string outputRoot, DiagnosticBundle diagnostic, CancellationToken cancellationToken)
    {
        var validationRoot = Path.Combine(outputRoot, "10_validation");
        Directory.CreateDirectory(validationRoot);
        var failureJson = Path.Combine(validationRoot, "nq_tick_export_failure_diagnostic.json");
        var failureMd = Path.Combine(validationRoot, "nq_tick_export_failure_diagnostic.md");
        var symbolJson = Path.Combine(validationRoot, "nq_tick_symbol_discovery_report.json");
        var symbolMd = Path.Combine(validationRoot, "nq_tick_symbol_discovery_report.md");
        foreach (var path in new[] { failureJson, failureMd, symbolJson, symbolMd })
        {
            if (File.Exists(path)) throw new IOException($"Diagnostic refuses to overwrite existing artifact: {path}");
        }

        await File.WriteAllTextAsync(failureJson, JsonSerializer.Serialize(diagnostic.FailureDiagnostic, JsonOptions()), cancellationToken);
        await File.WriteAllTextAsync(failureMd, RenderFailureMarkdown(diagnostic.FailureDiagnostic), cancellationToken);
        await File.WriteAllTextAsync(symbolJson, JsonSerializer.Serialize(diagnostic.SymbolDiscovery, JsonOptions()), cancellationToken);
        await File.WriteAllTextAsync(symbolMd, RenderSymbolMarkdown(diagnostic.SymbolDiscovery), cancellationToken);
    }

    private static string RenderFailureMarkdown(FailureDiagnostic diagnostic)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# NQ Tick Export Failure Diagnostic");
        builder.AppendLine();
        builder.AppendLine($"- NQ_TICK_EXPORT_FAILURE_REASON = `{diagnostic.NqTickExportFailureReason}`");
        builder.AppendLine($"- NQ_TICK_DATA_EXISTS = `{diagnostic.NqTickDataExists}`");
        builder.AppendLine($"- NQ_TICK_EXPORTABLE_WITH_CURRENT_TOOL = `{diagnostic.NqTickExportableWithCurrentTool}`");
        builder.AppendLine($"- MINIMAL_PATCH_REQUIRED = `{diagnostic.MinimalPatchRequired}`");
        builder.AppendLine($"- SAFE_TO_SHARE_WITH_YANNIK = `{diagnostic.SafeToShareWithYannik}`");
        builder.AppendLine();
        builder.AppendLine("## Existing Export");
        builder.AppendLine($"- status: `{diagnostic.InitialExportStatus}`");
        builder.AppendLine($"- row_count: `{diagnostic.InitialRowCount}`");
        builder.AppendLine($"- metadata: `{diagnostic.MetadataPath}`");
        builder.AppendLine();
        builder.AppendLine("## Tables");
        builder.AppendLine($"- scanned: `{string.Join(", ", diagnostic.TablesScanned)}`");
        builder.AppendLine($"- tick candidates: `{string.Join(", ", diagnostic.TickCandidateTables)}`");
        builder.AppendLine($"- bar-only candidates: `{string.Join(", ", diagnostic.BarOnlyTables)}`");
        builder.AppendLine();
        builder.AppendLine("## Rejected Candidate Reasons");
        foreach (var reason in diagnostic.RejectedCandidateReasons)
        {
            builder.AppendLine($"- `{reason.Table}`: {reason.Reason}; example=`{reason.Example}`; possible_fix={reason.PossibleFix}");
        }
        builder.AppendLine();
        builder.AppendLine("## Current Filter");
        builder.AppendLine($"- method: `{diagnostic.CurrentFilter.Method}`");
        builder.AppendLine($"- logic: `{diagnostic.CurrentFilter.Logic}`");
        foreach (var example in diagnostic.CurrentFilter.Examples)
        {
            builder.AppendLine($"- `{example.Value}`: accepted_by_current_filter=`{example.AcceptedByCurrentFilter}`, diagnostic_classification=`{example.DiagnosticClassification}`");
        }
        builder.AppendLine();
        builder.AppendLine("## Conclusion");
        builder.AppendLine(diagnostic.Conclusion);
        return builder.ToString();
    }

    private static string RenderSymbolMarkdown(SymbolDiscoveryReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# NQ Tick Symbol Discovery Report");
        builder.AppendLine();
        builder.AppendLine($"- {report.NqDefinition}");
        builder.AppendLine($"- {report.MnqDefinition}");
        builder.AppendLine($"- tick tables with NQ-like values: `{string.Join(", ", report.TickTablesWithNqLikeValues)}`");
        builder.AppendLine($"- bar tables with NQ-like values: `{string.Join(", ", report.BarTablesWithNqLikeValues)}`");
        builder.AppendLine($"- tables with MNQ-like values: `{string.Join(", ", report.TablesWithMnqLikeValues)}`");
        builder.AppendLine();
        foreach (var table in report.Tables)
        {
            builder.AppendLine($"## {table.FullName}");
            builder.AppendLine($"- status: `{table.Status}`");
            builder.AppendLine($"- row_count: `{table.RowCount}`");
            builder.AppendLine($"- timestamp_columns: `{string.Join(", ", table.TimestampColumns)}`");
            builder.AppendLine($"- symbol_columns: `{string.Join(", ", table.SymbolColumns)}`");
            builder.AppendLine($"- price_size_columns: `{string.Join(", ", table.PriceSizeColumns)}`");
            builder.AppendLine($"- bid_ask_columns: `{string.Join(", ", table.BidAskColumns)}`");
            builder.AppendLine($"- min/max timestamp: `{table.FirstTimestamp}` to `{table.LastTimestamp}`");
            foreach (var symbolColumn in table.SymbolDiscovery)
            {
                builder.AppendLine($"- symbol column `{symbolColumn.Column}`: nq_like_values=`{symbolColumn.NqLikeValueCount}`, mnq_like_values=`{symbolColumn.MnqLikeValueCount}`");
                foreach (var value in symbolColumn.NqLikeExamples.Take(10))
                {
                    builder.AppendLine($"  - `{value.Value}` resolved=`{string.Join(" | ", value.ResolvedSymbols)}` rows=`{value.RowCount}` classification=`{value.Classification}` continuous=`{value.IsContinuous}`");
                }
            }
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static JsonSerializerOptions JsonOptions()
        => new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, Converters = { new JsonStringEnumConverter() } };
}

internal sealed record DiagnosticBundle(FailureDiagnostic FailureDiagnostic, SymbolDiscoveryReport SymbolDiscovery);
internal sealed record FailureDiagnostic(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    string MetadataPath,
    string ReportJsonPath,
    string ReportMarkdownPath,
    string? InitialExportStatus,
    long InitialRowCount,
    string Database,
    IReadOnlyList<string> TablesScanned,
    IReadOnlyList<string> TickCandidateTables,
    IReadOnlyList<string> BarOnlyTables,
    IReadOnlyList<RejectedCandidateReason> RejectedCandidateReasons,
    CurrentFilterDiagnostic CurrentFilter,
    string NqTickExportFailureReason,
    string NqTickDataExists,
    string NqTickExportableWithCurrentTool,
    string MinimalPatchRequired,
    string SafeToShareWithYannik,
    string Conclusion);
internal sealed record SymbolDiscoveryReport(
    string RunKey,
    DateTimeOffset CreatedAtUtc,
    string NqDefinition,
    string MnqDefinition,
    IReadOnlyList<TableDiagnostic> Tables,
    IReadOnlyList<string> TablesWithMnqLikeValues,
    IReadOnlyList<string> TickTablesWithNqLikeValues,
    IReadOnlyList<string> BarTablesWithNqLikeValues);
internal sealed record RejectedCandidateReason(string Table, string Reason, string? Example, string PossibleFix);
internal sealed record CurrentFilterDiagnostic(string Method, string Logic, IReadOnlyList<CurrentFilterExample> Examples);
internal sealed record CurrentFilterExample(string Value, bool AcceptedByCurrentFilter, string DiagnosticClassification);
internal sealed record ColumnDiagnostic(string Name, string DataType);
internal sealed record TableDiagnostic(
    string FullName,
    string Schema,
    string Table,
    long? RowCount,
    IReadOnlyList<ColumnDiagnostic> Columns,
    IReadOnlyList<string> TimestampColumns,
    IReadOnlyList<string> SymbolColumns,
    IReadOnlyList<string> PriceSizeColumns,
    IReadOnlyList<string> BidAskColumns,
    string? FirstTimestamp,
    string? LastTimestamp,
    string Status,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<SymbolColumnDiagnostic> SymbolDiscovery);
internal sealed record SymbolColumnDiagnostic(
    string Column,
    int DistinctValuesSampled,
    int NqLikeValueCount,
    int MnqLikeValueCount,
    IReadOnlyList<SymbolValueDiagnostic> NqLikeExamples,
    IReadOnlyList<SymbolValueDiagnostic> TopValues);
internal sealed record SymbolValueDiagnostic(string Value, long RowCount, IReadOnlyList<string> ResolvedSymbols, string Classification, bool IsContinuous, string Reason);
internal sealed record SymbolClassification(string Kind, bool IsContinuous, string Why);
internal sealed record TimestampRange(string? First, string? Last);
