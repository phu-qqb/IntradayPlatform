using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using QQ.Production.Intraday.Application;

var options = CliOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine("Usage: dotnet run --project tools/QQ.Production.Intraday.Tools.NqM1StorageAudit -- --run-key <RunKey> --repo-root <path> --output-root <path> [--connection-string <sql>] [--include-file-scan true] [--include-db-scan true] --no-execution true");
    return 0;
}

if (!options.NoExecution)
{
    Console.Error.WriteLine("NQ M1 storage audit is read-only and requires --no-execution true.");
    return 2;
}

var repoRoot = Path.GetFullPath(options.RepoRoot ?? ".");
var runKey = options.RunKey ?? $"nq-m1-storage-audit-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
var outputRoot = Path.GetFullPath(options.OutputRoot ?? Path.Combine("artifacts", "nq-m1-storage-audit", runKey));
var validationRoot = Path.Combine(outputRoot, "10_validation");
var shareRoot = Path.Combine(outputRoot, "share");
Directory.CreateDirectory(validationRoot);
Directory.CreateDirectory(shareRoot);
EnsureOutputsDoNotExist(outputRoot);

var config = ConfigScanner.Scan(repoRoot);
var dbReport = options.IncludeDbScan
    ? await DbScanner.ScanAsync(runKey, ResolveConnectionString(options.ConnectionString), CancellationToken.None)
    : new NqM1DbDiscoveryReport(runKey, DateTimeOffset.UtcNow, [], []);
var fileReport = options.IncludeFileScan
    ? FileScanner.Scan(runKey, repoRoot, config)
    : new NqM1FileDiscoveryReport(runKey, DateTimeOffset.UtcNow, repoRoot, [], []);
var tickStatus = ReadPreviousTickAvailability(repoRoot);
var inventory = NqM1StorageAuditService.BuildInventory(runKey, config, dbReport, fileReport, tickStatus);
var duplicate = NqM1StorageAuditService.BuildDuplicateReport(runKey, inventory.Locations);

await ReportWriter.WriteAsync(outputRoot, inventory, dbReport, fileReport, duplicate, CancellationToken.None);

Console.WriteLine($"NQ_M1_DATA_EXISTS={inventory.NqM1DataExists}");
Console.WriteLine($"NQ_M1_DATA_LOCATION_COUNT={inventory.NqM1DataLocationCount}");
Console.WriteLine($"NQ_M1_CANONICAL_LOCATION={inventory.NqM1CanonicalLocation}");
Console.WriteLine($"NQ_M1_DUPLICATE_STORAGE_STATUS={inventory.NqM1DuplicateStorageStatus}");
Console.WriteLine($"SAFE_TO_EXPORT_M1_FOR_YANNIK={inventory.SafeToExportM1ForYannik}");
Console.WriteLine($"summary={Path.Combine(shareRoot, "nq_m1_data_location_summary.md")}");
return 0;

static void EnsureOutputsDoNotExist(string outputRoot)
{
    var files = new[]
    {
        Path.Combine(outputRoot, "10_validation", "nq_m1_storage_inventory_report.json"),
        Path.Combine(outputRoot, "10_validation", "nq_m1_storage_inventory_report.md"),
        Path.Combine(outputRoot, "10_validation", "nq_m1_db_discovery_report.json"),
        Path.Combine(outputRoot, "10_validation", "nq_m1_db_discovery_report.md"),
        Path.Combine(outputRoot, "10_validation", "nq_m1_file_discovery_report.json"),
        Path.Combine(outputRoot, "10_validation", "nq_m1_file_discovery_report.md"),
        Path.Combine(outputRoot, "10_validation", "nq_m1_duplicate_storage_report.json"),
        Path.Combine(outputRoot, "10_validation", "nq_m1_duplicate_storage_report.md"),
        Path.Combine(outputRoot, "share", "nq_m1_data_location_summary.md"),
        Path.Combine(outputRoot, "manifest.json"),
        Path.Combine(outputRoot, "manifest.sha256"),
        Path.Combine(outputRoot, "hashes.json")
    };
    var existing = files.FirstOrDefault(File.Exists);
    if (existing is not null) throw new IOException($"NQ M1 storage audit refuses to overwrite existing artifact: {existing}");
}

static string? ResolveConnectionString(string? explicitConnectionString)
{
    if (!string.IsNullOrWhiteSpace(explicitConnectionString)) return NormalizeReadOnlyConnectionString(explicitConnectionString);
    var env = Environment.GetEnvironmentVariable("QQ_INTRADAY_SQLSERVER_CONNECTIONSTRING");
    if (!string.IsNullOrWhiteSpace(env)) return NormalizeReadOnlyConnectionString(env);
    var appsettingsPath = Path.Combine("src", "QQ.Production.Intraday.Api", "appsettings.json");
    if (!File.Exists(appsettingsPath)) return null;
    using var doc = JsonDocument.Parse(File.ReadAllText(appsettingsPath));
    if (!doc.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings)) return null;
    return connectionStrings.TryGetProperty("IntradaySqlServer", out var intraday) && !string.IsNullOrWhiteSpace(intraday.GetString())
        ? NormalizeReadOnlyConnectionString(intraday.GetString()!)
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

static string ReadPreviousTickAvailability(string repoRoot)
{
    var path = Path.Combine(repoRoot, "artifacts", "nq-tick-export", "nq-ticks-yannik-001", "10_validation", "nq_tick_export_failure_diagnostic.json");
    if (!File.Exists(path)) return "UNKNOWN";
    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    return doc.RootElement.TryGetProperty("nq_tick_data_exists", out var value) ? value.GetString() ?? "UNKNOWN" : "UNKNOWN";
}

internal sealed record CliOptions(
    string? RunKey,
    string? RepoRoot,
    string? OutputRoot,
    string? ConnectionString,
    bool IncludeFileScan,
    bool IncludeDbScan,
    bool NoExecution,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        string? runKey = null;
        string? repoRoot = null;
        string? outputRoot = null;
        string? connectionString = null;
        var includeFileScan = true;
        var includeDbScan = true;
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
                case "--repo-root": repoRoot = value; index++; break;
                case "--output-root": outputRoot = value; index++; break;
                case "--connection-string": connectionString = value; index++; break;
                case "--include-file-scan": includeFileScan = bool.Parse(value); index++; break;
                case "--include-db-scan": includeDbScan = bool.Parse(value); index++; break;
                case "--no-execution": noExecution = bool.Parse(value); index++; break;
            }
        }

        return new CliOptions(runKey, repoRoot, outputRoot, connectionString, includeFileScan, includeDbScan, noExecution, showHelp);
    }
}

internal static class ConfigScanner
{
    private static readonly string[] Patterns =
    [
        "QQProductionIntraday", "MarketDataBars", "Bars", "Candles", "Minute", "M1", "1m",
        "OneMinute", "Timeframe", "Polygon", "Massive", "NQ", "MNQ", "Nasdaq", "futures",
        "storage", "artifacts", "cache", "parquet", "csv", "sqlite", "duckdb", "mdf",
        "database", "connection string"
    ];

    public static NqM1ConfigDiscovery Scan(string repoRoot)
    {
        var roots = new[] { repoRoot };
        var files = roots.SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(IsConfigFile)
            .Take(10_000)).ToArray();
        var dbConfigured = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var dbAlternatives = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var dataDirs = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var artifacts = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidateFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidateTables = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            string text;
            try { text = File.ReadAllText(file); }
            catch { continue; }
            if (!Patterns.Any(pattern => text.Contains(pattern, StringComparison.OrdinalIgnoreCase))) continue;
            var relative = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            candidateFiles.Add(relative);
            foreach (var match in Regex.Matches(text, @"(?:Initial Catalog|Database)\s*=\s*([^;""\r\n]+)", RegexOptions.IgnoreCase).Cast<Match>())
            {
                dbConfigured.Add(match.Groups[1].Value.Trim());
            }

            if (text.Contains("QQProductionIntraday", StringComparison.OrdinalIgnoreCase)) dbConfigured.Add("QQProductionIntraday");
            foreach (var table in new[] { "MarketDataBars", "MarketDataSnapshots", "LmaxIndividualTrades", "LmaxTradeSummaries" })
            {
                if (text.Contains(table, StringComparison.OrdinalIgnoreCase)) candidateTables.Add(table);
            }

            foreach (var path in Regex.Matches(text, @"(?:artifacts|data|datasets|downloads|historical|marketdata|cache|polygon|massive|storage|packages)[\\/][A-Za-z0-9_\-./\\]+", RegexOptions.IgnoreCase).Cast<Match>().Select(m => m.Value))
            {
                if (path.StartsWith("artifacts", StringComparison.OrdinalIgnoreCase)) artifacts.Add(path);
                else dataDirs.Add(path);
            }
        }

        return new NqM1ConfigDiscovery(dbConfigured.ToArray(), dbAlternatives.ToArray(), dataDirs.ToArray(), artifacts.ToArray(), candidateFiles.ToArray(), candidateTables.ToArray());
    }

    private static bool IsConfigFile(string path)
    {
        var name = Path.GetFileName(path);
        var extension = Path.GetExtension(path);
        if (path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
            path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
            path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
        {
            return false;
        }

        return name.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) && extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("launchSettings.json", StringComparison.OrdinalIgnoreCase) ||
               name.Equals(".env", StringComparison.OrdinalIgnoreCase) ||
               extension is ".csproj" or ".sln" or ".ps1" or ".sh" or ".md" or ".json" or ".config";
    }
}

internal static class DbScanner
{
    private static readonly string[] CandidateFragments = ["Bar", "Bars", "Candle", "Candles", "Minute", "M1", "MarketData", "Historical", "Polygon", "Massive", "Futures", "Price", "OHLC", "Raw"];

    public static async Task<NqM1DbDiscoveryReport> ScanAsync(string runKey, string? connectionString, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return new NqM1DbDiscoveryReport(runKey, DateTimeOffset.UtcNow, [], []);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var database = connection.Database;
        var instrumentMap = await ReadInstrumentMapAsync(connection, cancellationToken);
        var columnRows = await ReadCandidateColumnsAsync(connection, cancellationToken);
        var tables = new List<NqM1DbTableReport>();
        foreach (var group in columnRows.GroupBy(x => (x.Schema, x.Table)).OrderBy(x => x.Key.Schema).ThenBy(x => x.Key.Table))
        {
            var columns = group.Select(x => x.Column).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var timestampColumns = columns.Where(IsTimestampColumn).ToArray();
            var symbolColumns = columns.Where(IsSymbolColumn).ToArray();
            var timeframeColumns = columns.Where(IsTimeframeColumn).ToArray();
            var ohlcColumns = columns.Where(IsOhlcColumn).ToArray();
            var volumeColumns = columns.Where(x => x.Contains("volume", StringComparison.OrdinalIgnoreCase)).ToArray();
            var rowCount = await CountRowsAsync(connection, group.Key.Schema, group.Key.Table, cancellationToken);
            var firstLast = timestampColumns.Length > 0 ? await FirstLastAsync(connection, group.Key.Schema, group.Key.Table, timestampColumns[0], cancellationToken) : (null, null);
            var samples = new List<NqM1SymbolSample>();
            foreach (var column in symbolColumns)
            {
                samples.AddRange(await ReadSymbolSamplesAsync(connection, group.Key.Schema, group.Key.Table, column, instrumentMap, cancellationToken));
            }

            var timeframeValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in timeframeColumns)
            {
                timeframeValues[column] = await ReadTopValueAsync(connection, group.Key.Schema, group.Key.Table, column, cancellationToken);
            }

            var timestampSample = timestampColumns.Length > 0 ? await ReadTimestampSampleAsync(connection, group.Key.Schema, group.Key.Table, timestampColumns[0], cancellationToken) : [];
            var m1 = NqM1StorageAuditService.InferM1(group.Key.Table, timeframeValues, timestampSample);
            var hasNq = samples.Any(x => x.Classification == "NQ");
            var hasMnq = samples.Any(x => x.Classification == "MNQ");
            var isTickLike = group.Key.Table.Contains("Tick", StringComparison.OrdinalIgnoreCase) || group.Key.Table.Contains("Trade", StringComparison.OrdinalIgnoreCase) || group.Key.Table.Contains("Quote", StringComparison.OrdinalIgnoreCase);
            var status = NqM1StorageAuditService.DetermineTableStatus(hasNq, hasMnq, m1.Confidence is "HIGH" or "MEDIUM", ohlcColumns.Length >= 4, rowCount == 0, isTickLike);
            var reasons = new List<string>();
            if (rowCount == 0) reasons.Add("empty table");
            if (!hasNq) reasons.Add(hasMnq ? "MNQ found but NQ not found" : "no NQ-like symbols found");
            if (ohlcColumns.Length < 4) reasons.Add("OHLC columns not present");
            if (m1.Confidence == "NONE") reasons.Add("M1 timeframe not proven");

            tables.Add(new NqM1DbTableReport(database, $"{group.Key.Schema}.{group.Key.Table}", rowCount, columns, timestampColumns, symbolColumns, timeframeColumns, ohlcColumns, volumeColumns, firstLast.Item1, firstLast.Item2, samples, m1.Confidence, m1.Evidence, status, reasons));
        }

        return new NqM1DbDiscoveryReport(runKey, DateTimeOffset.UtcNow, [database], tables);
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ReadInstrumentMapAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string existsSql = "SELECT COUNT_BIG(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Instruments';";
        await using (var command = new SqlCommand(existsSql, connection))
        {
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) == 0) return new Dictionary<string, IReadOnlyList<string>>();
        }

        const string sql = """
            SELECT CAST(Id AS nvarchar(64)), CAST(Symbol AS nvarchar(4000)) FROM dbo.Instruments WHERE Symbol IS NOT NULL
            UNION ALL SELECT CAST(InstrumentId AS nvarchar(64)), CAST(ExternalSymbol AS nvarchar(4000)) FROM dbo.InstrumentAliases WHERE ExternalSymbol IS NOT NULL
            UNION ALL SELECT CAST(InstrumentId AS nvarchar(64)), CAST(ExternalInstrumentId AS nvarchar(4000)) FROM dbo.InstrumentAliases WHERE ExternalInstrumentId IS NOT NULL;
            """;
        await using var read = new SqlCommand(sql, connection);
        await using var reader = await read.ExecuteReaderAsync(cancellationToken);
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetString(0);
            var symbol = reader.GetString(1);
            if (!map.TryGetValue(id, out var values))
            {
                values = [];
                map[id] = values;
            }

            if (!values.Contains(symbol, StringComparer.OrdinalIgnoreCase)) values.Add(symbol);
        }

        return map.ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<IReadOnlyList<(string Schema, string Table, string Column)>> ReadCandidateColumnsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var where = string.Join(" OR ", CandidateFragments.Select((_, i) => $"TABLE_NAME LIKE @p{i}"));
        var sql = $"SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE {where} ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION;";
        await using var command = new SqlCommand(sql, connection);
        for (var i = 0; i < CandidateFragments.Length; i++) command.Parameters.Add(new SqlParameter($"@p{i}", SqlDbType.NVarChar, 128) { Value = $"%{CandidateFragments[i]}%" });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<(string Schema, string Table, string Column)>();
        while (await reader.ReadAsync(cancellationToken)) rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return rows;
    }

    private static async Task<long?> CountRowsAsync(SqlConnection connection, string schema, string table, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand($"SELECT COUNT_BIG(*) FROM {Quote(schema)}.{Quote(table)};", connection) { CommandTimeout = 120 };
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<(string?, string?)> FirstLastAsync(SqlConnection connection, string schema, string table, string column, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand($"SELECT MIN({Quote(column)}), MAX({Quote(column)}) FROM {Quote(schema)}.{Quote(table)};", connection) { CommandTimeout = 120 };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return (null, null);
        return (ReadString(reader, 0), ReadString(reader, 1));
    }

    private static async Task<IReadOnlyList<NqM1SymbolSample>> ReadSymbolSamplesAsync(SqlConnection connection, string schema, string table, string column, IReadOnlyDictionary<string, IReadOnlyList<string>> instrumentMap, CancellationToken cancellationToken)
    {
        var sql = $"SELECT TOP (200) CAST({Quote(column)} AS nvarchar(4000)), COUNT_BIG(*) FROM {Quote(schema)}.{Quote(table)} WHERE {Quote(column)} IS NOT NULL GROUP BY CAST({Quote(column)} AS nvarchar(4000)) ORDER BY COUNT_BIG(*) DESC;";
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<NqM1SymbolSample>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var value = reader.GetString(0);
            var count = Convert.ToInt64(reader.GetValue(1));
            var resolved = instrumentMap.TryGetValue(value, out var symbols) ? symbols : [];
            var classification = NqM1SymbolClassifier.Classify(value, resolved);
            rows.Add(new NqM1SymbolSample(column, value, count, resolved, classification.Kind, classification.IsContinuous, classification.Reason));
        }

        return rows;
    }

    private static async Task<string?> ReadTopValueAsync(SqlConnection connection, string schema, string table, string column, CancellationToken cancellationToken)
    {
        var sql = $"SELECT TOP (1) CAST({Quote(column)} AS nvarchar(4000)) FROM {Quote(schema)}.{Quote(table)} WHERE {Quote(column)} IS NOT NULL GROUP BY CAST({Quote(column)} AS nvarchar(4000)) ORDER BY COUNT_BIG(*) DESC;";
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<DateTimeOffset>> ReadTimestampSampleAsync(SqlConnection connection, string schema, string table, string column, CancellationToken cancellationToken)
    {
        var sql = $"SELECT TOP (500) {Quote(column)} FROM {Quote(schema)}.{Quote(table)} WHERE {Quote(column)} IS NOT NULL ORDER BY {Quote(column)} ASC;";
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var timestamps = new List<DateTimeOffset>();
        while (await reader.ReadAsync(cancellationToken) && timestamps.Count < 500)
        {
            if (DateTimeOffset.TryParse(ReadString(reader, 0), out var parsed)) timestamps.Add(parsed.ToUniversalTime());
        }

        return timestamps;
    }

    private static bool IsTimestampColumn(string name)
        => !name.Equals("Timeframe", StringComparison.OrdinalIgnoreCase) &&
           !name.EndsWith("Updated", StringComparison.OrdinalIgnoreCase) &&
           (name.Contains("time", StringComparison.OrdinalIgnoreCase) || name.Contains("date", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Utc", StringComparison.OrdinalIgnoreCase));

    private static bool IsSymbolColumn(string name)
        => name.Equals("symbol", StringComparison.OrdinalIgnoreCase) || name.Equals("ticker", StringComparison.OrdinalIgnoreCase) || name.Equals("contract", StringComparison.OrdinalIgnoreCase) || name.Equals("root", StringComparison.OrdinalIgnoreCase) || name.Contains("instrument", StringComparison.OrdinalIgnoreCase);

    private static bool IsTimeframeColumn(string name)
        => name.Contains("timeframe", StringComparison.OrdinalIgnoreCase) || name.Contains("granularity", StringComparison.OrdinalIgnoreCase) || name.Contains("interval", StringComparison.OrdinalIgnoreCase);

    private static bool IsOhlcColumn(string name)
        => name.Equals("open", StringComparison.OrdinalIgnoreCase) || name.Equals("high", StringComparison.OrdinalIgnoreCase) || name.Equals("low", StringComparison.OrdinalIgnoreCase) || name.Equals("close", StringComparison.OrdinalIgnoreCase) ||
           name.EndsWith("Open", StringComparison.OrdinalIgnoreCase) || name.EndsWith("High", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Low", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Close", StringComparison.OrdinalIgnoreCase);

    private static string Quote(string identifier) => "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
    private static string? ReadString(SqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
}

internal static class FileScanner
{
    private static readonly string[] CandidateDirs = ["artifacts", "data", "datasets", "downloads", "historical", "marketdata", "cache", "polygon", "massive", "storage", "tmp", "out", "validation", "packages", "fixtures"];
    private static readonly string[] Extensions = [".csv", ".gz", ".json", ".jsonl", ".parquet", ".sqlite", ".sqlite3", ".db", ".duckdb", ".bin", ".txt"];

    public static NqM1FileDiscoveryReport Scan(string runKey, string repoRoot, NqM1ConfigDiscovery config)
    {
        var directories = CandidateDirs.Select(x => Path.Combine(repoRoot, x)).Where(Directory.Exists).ToArray();
        var files = new List<NqM1FileReport>();
        foreach (var directory in directories)
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Where(IsCandidateFile).Take(20_000))
            {
                files.Add(InspectFile(repoRoot, path));
            }
        }

        return new NqM1FileDiscoveryReport(runKey, DateTimeOffset.UtcNow, repoRoot, directories.Select(x => Path.GetRelativePath(repoRoot, x).Replace('\\', '/')).ToArray(), files);
    }

    private static bool IsCandidateFile(string path)
    {
        var extension = Path.GetExtension(path);
        var name = Path.GetFileName(path);
        return Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase) &&
               (name.Contains("NQ", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("MNQ", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("M1", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("1m", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("minute", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("bar", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("candle", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("polygon", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("massive", StringComparison.OrdinalIgnoreCase));
    }

    private static NqM1FileReport InspectFile(string repoRoot, string path)
    {
        var relative = Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
        var info = new FileInfo(path);
        if (info.Length == 0) return new NqM1FileReport(relative, 0, Path.GetExtension(path), [], null, null, 0, null, null, "NONE", "UNKNOWN", "EMPTY_FILE", null, []);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is ".parquet" or ".sqlite" or ".sqlite3" or ".db" or ".duckdb" or ".bin")
        {
            var fileClass = NqM1SymbolClassifier.Classify(Path.GetFileNameWithoutExtension(path));
            var status = fileClass.Kind == "NQ" ? "POSSIBLE_NQ_BUT_AMBIGUOUS" : fileClass.Kind == "MNQ" ? "MNQ_FILE_FOUND" : "UNSUPPORTED_FILE";
            return new NqM1FileReport(relative, info.Length, extension.TrimStart('.'), [], fileClass.Kind == "NQ" ? Path.GetFileNameWithoutExtension(path) : null, null, null, null, null, "NONE", "UNKNOWN", status, null, ["Binary/container file was not opened; metadata-only audit."]);
        }

        try
        {
            return InspectDelimited(repoRoot, path, relative, info.Length);
        }
        catch (Exception ex)
        {
            return new NqM1FileReport(relative, info.Length, extension.TrimStart('.'), [], null, null, null, null, null, "NONE", "UNKNOWN", "UNSUPPORTED_FILE", null, [ex.Message]);
        }
    }

    private static NqM1FileReport InspectDelimited(string repoRoot, string path, string relative, long size)
    {
        using var stream = File.OpenRead(path);
        using Stream input = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? new GZipStream(stream, CompressionMode.Decompress) : stream;
        using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        var first = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(first)) return new NqM1FileReport(relative, size, "text", [], null, null, 0, null, null, "NONE", "UNKNOWN", "EMPTY_FILE", null, []);
        var columns = SplitCsv(first);
        var symbolIndex = FindIndex(columns, "symbol", "ticker", "contract", "root", "instrument");
        var timestampIndex = FindIndex(columns, "timestamp", "time", "datetime", "bar_start", "barstart", "date");
        var timeframeIndex = FindIndex(columns, "timeframe", "granularity", "interval");
        var ohlc = new[] { FindIndex(columns, "open"), FindIndex(columns, "high"), FindIndex(columns, "low"), FindIndex(columns, "close") }.Count(x => x >= 0);
        var timestamps = new List<DateTimeOffset>();
        var symbols = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var sampleLines = new List<string> { first };
        long rows = 0;
        string? timeframe = null;
        while (reader.ReadLine() is { } line)
        {
            rows++;
            if (sampleLines.Count < 32) sampleLines.Add(line);
            if (rows > 50_000) break;
            var parts = SplitCsv(line);
            if (symbolIndex >= 0 && symbolIndex < parts.Length)
            {
                var symbol = parts[symbolIndex];
                symbols[symbol] = symbols.GetValueOrDefault(symbol) + 1;
            }

            if (timestampIndex >= 0 && timestampIndex < parts.Length && DateTimeOffset.TryParse(parts[timestampIndex], out var parsed)) timestamps.Add(parsed.ToUniversalTime());
            if (timeframe is null && timeframeIndex >= 0 && timeframeIndex < parts.Length) timeframe = parts[timeframeIndex];
        }

        var fileNameClass = NqM1SymbolClassifier.Classify(Path.GetFileNameWithoutExtension(relative));
        var bestSymbol = symbols.OrderByDescending(x => x.Value).FirstOrDefault().Key;
        var symbolClass = NqM1SymbolClassifier.Classify(bestSymbol);
        if (symbolClass.Kind is "UNKNOWN" or "OTHER") symbolClass = fileNameClass;
        var m1 = NqM1StorageAuditService.InferM1(relative, new Dictionary<string, string?> { ["timeframe"] = timeframe }, timestamps);
        var hasNq = symbolClass.Kind == "NQ";
        var hasMnq = symbolClass.Kind == "MNQ";
        var status = hasNq && (m1.Confidence is "HIGH" or "MEDIUM") && ohlc >= 4 ? "NQ_M1_FILE_FOUND" :
            hasMnq ? "MNQ_FILE_FOUND" :
            hasNq ? "POSSIBLE_NQ_BUT_AMBIGUOUS" :
            "NON_NQ_FILE";
        var ordered = timestamps.OrderBy(x => x).ToArray();
        return new NqM1FileReport(relative, size, relative.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? "csv.gz" : Path.GetExtension(relative).TrimStart('.'), columns, hasNq || hasMnq ? bestSymbol ?? Path.GetFileNameWithoutExtension(relative) : null, timeframe, rows, ordered.FirstOrDefault() == default ? null : ordered.First().ToString("O"), ordered.LastOrDefault() == default ? null : ordered.Last().ToString("O"), m1.Confidence, m1.Evidence, status, NqM1StorageAuditService.HashSample(sampleLines), rows >= 50_000 ? ["Row count capped at 50000 during read-only file audit."] : []);
    }

    private static int FindIndex(IReadOnlyList<string> columns, params string[] names)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (names.Any(name => columns[index].Contains(name, StringComparison.OrdinalIgnoreCase))) return index;
        }

        return -1;
    }

    private static string[] SplitCsv(string line)
        => line.Split(',').Select(x => x.Trim().Trim('"')).ToArray();
}

internal static class ReportWriter
{
    public static async Task WriteAsync(string outputRoot, NqM1StorageInventoryReport inventory, NqM1DbDiscoveryReport db, NqM1FileDiscoveryReport file, NqM1DuplicateStorageReport duplicate, CancellationToken cancellationToken)
    {
        var validation = Path.Combine(outputRoot, "10_validation");
        var share = Path.Combine(outputRoot, "share");
        await WritePairAsync(Path.Combine(validation, "nq_m1_storage_inventory_report"), inventory, RenderInventory(inventory), cancellationToken);
        await WritePairAsync(Path.Combine(validation, "nq_m1_db_discovery_report"), db, RenderDb(db), cancellationToken);
        await WritePairAsync(Path.Combine(validation, "nq_m1_file_discovery_report"), file, RenderFile(file), cancellationToken);
        await WritePairAsync(Path.Combine(validation, "nq_m1_duplicate_storage_report"), duplicate, RenderDuplicate(duplicate), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(share, "nq_m1_data_location_summary.md"), RenderShareSummary(inventory), cancellationToken);
        await WriteManifestAsync(outputRoot, cancellationToken);
    }

    private static async Task WritePairAsync<T>(string basePath, T value, string markdown, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(basePath + ".json", JsonSerializer.Serialize(value, JsonOptions()), cancellationToken);
        await File.WriteAllTextAsync(basePath + ".md", markdown, cancellationToken);
    }

    private static string RenderInventory(NqM1StorageInventoryReport report)
    {
        var b = new StringBuilder();
        b.AppendLine("# NQ M1 Storage Inventory");
        b.AppendLine($"- NQ_M1_DATA_EXISTS = `{report.NqM1DataExists}`");
        b.AppendLine($"- NQ_M1_DATA_LOCATION_COUNT = `{report.NqM1DataLocationCount}`");
        b.AppendLine($"- NQ_M1_CANONICAL_LOCATION = `{report.NqM1CanonicalLocation}`");
        b.AppendLine($"- NQ_M1_DUPLICATE_STORAGE_STATUS = `{report.NqM1DuplicateStorageStatus}`");
        b.AppendLine($"- NQ_M1_EXPORTABLE = `{report.NqM1Exportable}`");
        b.AppendLine($"- NQ_TICKS_AVAILABLE = `{report.NqTicksAvailable}`");
        b.AppendLine($"- M1_IS_NOT_TICK_LEVEL = `{report.M1IsNotTickLevel}`");
        b.AppendLine($"- SAFE_TO_EXPORT_M1_FOR_YANNIK = `{report.SafeToExportM1ForYannik}`");
        b.AppendLine();
        foreach (var location in report.Locations) b.AppendLine($"- `{location.Location}` rows=`{location.RowCount}` period=`{location.FirstTimestamp}` to `{location.LastTimestamp}` contracts=`{string.Join(", ", location.Contracts)}`");
        return b.ToString();
    }

    private static string RenderDb(NqM1DbDiscoveryReport report)
    {
        var b = new StringBuilder();
        b.AppendLine("# NQ M1 DB Discovery");
        foreach (var table in report.Tables)
        {
            b.AppendLine($"## {table.Database}:{table.TableName}");
            b.AppendLine($"- status: `{table.Status}`");
            b.AppendLine($"- rows: `{table.RowCount}`");
            b.AppendLine($"- timestamp: `{string.Join(", ", table.TimestampColumns)}`");
            b.AppendLine($"- symbol: `{string.Join(", ", table.SymbolColumns)}`");
            b.AppendLine($"- timeframe: `{string.Join(", ", table.TimeframeColumns)}`");
            b.AppendLine($"- OHLC: `{string.Join(", ", table.OhlcColumns)}`");
            b.AppendLine($"- M1: `{table.M1Confidence}` via `{table.M1Evidence}`");
            b.AppendLine($"- period: `{table.FirstTimestamp}` to `{table.LastTimestamp}`");
            foreach (var sample in table.SymbolSamples.Where(x => x.Classification is "NQ" or "MNQ").Take(20)) b.AppendLine($"- symbol `{sample.Value}` resolved=`{string.Join(" | ", sample.ResolvedSymbols)}` class=`{sample.Classification}` rows=`{sample.RowCount}`");
        }
        return b.ToString();
    }

    private static string RenderFile(NqM1FileDiscoveryReport report)
    {
        var b = new StringBuilder();
        b.AppendLine("# NQ M1 File Discovery");
        b.AppendLine($"- repo_root: `{report.RepoRoot}`");
        b.AppendLine($"- directories_scanned: `{string.Join(", ", report.DirectoriesScanned)}`");
        foreach (var file in report.Files)
        {
            b.AppendLine($"- `{file.Path}` status=`{file.Status}` rows=`{file.RowCount}` symbol=`{file.InferredSymbolOrContract}` M1=`{file.M1Confidence}` via `{file.M1Evidence}` period=`{file.FirstTimestamp}` to `{file.LastTimestamp}`");
        }
        return b.ToString();
    }

    private static string RenderDuplicate(NqM1DuplicateStorageReport report)
    {
        var b = new StringBuilder();
        b.AppendLine("# NQ M1 Duplicate Storage");
        b.AppendLine($"- DUPLICATE_STORAGE_STATUS = `{report.DuplicateStorageStatus}`");
        b.AppendLine($"- canonical_location = `{report.CanonicalLocation}`");
        foreach (var candidate in report.Candidates) b.AppendLine($"- `{candidate.Left}` vs `{candidate.Right}`: `{candidate.Assessment}` ({candidate.Reason})");
        foreach (var recommendation in report.Recommendations) b.AppendLine($"- recommendation: {recommendation}");
        return b.ToString();
    }

    private static string RenderShareSummary(NqM1StorageInventoryReport report)
        => $"""
           # NQ M1 Data Location Summary

           NQ M1 exists: `{report.NqM1DataExists}`
           Location count: `{report.NqM1DataLocationCount}`
           Canonical location: `{report.NqM1CanonicalLocation}`
           Duplicate storage: `{report.NqM1DuplicateStorageStatus}`
           Safe to export M1 for Yannik: `{report.SafeToExportM1ForYannik}`
           NQ ticks available: `{report.NqTicksAvailable}`

           M1 bars are not tick-level data. The prior tick export can fail even if M1 bars exist because ticks and minute bars are different storage grains.
           """;

    private static async Task WriteManifestAsync(string outputRoot, CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) is not "manifest.json" and not "manifest.sha256" and not "hashes.json")
            .Select(path => Path.GetRelativePath(outputRoot, path).Replace('\\', '/'))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hashes = files.Select(path => new { path, sha256 = Sha256File(Path.Combine(outputRoot, path.Replace('/', Path.DirectorySeparatorChar))) }).ToArray();
        var manifest = new { packageKind = "NqM1StorageAudit", readOnly = true, files };
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "manifest.json"), JsonSerializer.Serialize(manifest, JsonOptions()), cancellationToken);
        var manifestHash = Sha256File(Path.Combine(outputRoot, "manifest.json"));
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "hashes.json"), JsonSerializer.Serialize(hashes.Append(new { path = "manifest.json", sha256 = manifestHash }), JsonOptions()), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "manifest.sha256"), manifestHash + "  manifest.json" + Environment.NewLine, Encoding.ASCII, cancellationToken);
    }

    private static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static JsonSerializerOptions JsonOptions()
        => new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, Converters = { new JsonStringEnumConverter() } };
}
