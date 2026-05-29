using Microsoft.Data.SqlClient;
using QQ.Production.Intraday.Application;

var cli = CliOptions.Parse(args);
if (cli.ShowHelp)
{
    Console.WriteLine("Usage: dotnet run --project tools/QQ.Production.Intraday.Tools.MarketDataDbPersistencePreflight -- --run-key marketdata-db-persistence-preflight-r001 --repo-root . --output-root artifacts/lmax-sandbox-marketdata/marketdata-db-persistence-preflight-r001 --source-run-root artifacts/lmax-sandbox-marketdata/lmax-bounded-md-loop-demo-002 --freeze-root artifacts/lmax-sandbox-marketdata/lmax-bounded-md-loop-demo-002-freeze-001 --connection-string-env QQPRODUCTIONINTRADAY_CONNECTION_STRING --no-external true --no-execution true --no-db-write true");
    return 0;
}

var runKey = cli.RunKey ?? "marketdata-db-persistence-preflight-r001";
var repoRoot = Path.GetFullPath(cli.RepoRoot ?? ".");
var outputRoot = Path.GetFullPath(cli.OutputRoot ?? Path.Combine("artifacts", "lmax-sandbox-marketdata", runKey));
var sourceRunRoot = Path.GetFullPath(cli.SourceRunRoot ?? Path.Combine("artifacts", "lmax-sandbox-marketdata", "lmax-bounded-md-loop-demo-002"));
var freezeRoot = Path.GetFullPath(cli.FreezeRoot ?? Path.Combine("artifacts", "lmax-sandbox-marketdata", "lmax-bounded-md-loop-demo-002-freeze-001"));
var connectionStringEnv = cli.ConnectionStringEnv ?? "QQPRODUCTIONINTRADAY_CONNECTION_STRING";
var connectionString = Environment.GetEnvironmentVariable(connectionStringEnv);
var connectionStringPresent = !string.IsNullOrWhiteSpace(connectionString);

RefuseOverwrite(outputRoot);

IMarketDataDbInventoryProvider provider = connectionStringPresent
    ? new SqlServerMarketDataDbInventoryProvider(connectionString!, connectionStringEnv)
    : new MissingConnectionStringMarketDataDbInventoryProvider(connectionStringEnv);

var result = await new MarketDataDbPersistencePreflightWriter().WriteAsync(
    new MarketDataDbPersistencePreflightOptions(
        runKey,
        repoRoot,
        outputRoot,
        sourceRunRoot,
        freezeRoot,
        connectionStringEnv,
        connectionStringPresent,
        cli.NoExternal,
        cli.NoExecution,
        cli.NoDbWrite),
    provider,
    CancellationToken.None);

Console.WriteLine($"MARKETDATA_DB_PERSISTENCE_PREFLIGHT_STATUS={result.PreflightReport["MARKETDATA_DB_PERSISTENCE_PREFLIGHT_STATUS"]}");
if (result.PreflightReport.TryGetValue("MARKETDATA_DB_ROW_COUNT_EVIDENCE_STATUS", out var rowCountEvidenceStatus))
{
    Console.WriteLine($"MARKETDATA_DB_ROW_COUNT_EVIDENCE_STATUS={rowCountEvidenceStatus}");
}

Console.WriteLine($"DB_CONNECTION_STATUS={result.PreflightReport["DB_CONNECTION_STATUS"]}");
Console.WriteLine($"DB_SCHEMA_INVENTORY_STATUS={result.PreflightReport["DB_SCHEMA_INVENTORY_STATUS"]}");
Console.WriteLine($"DB_ROW_COUNTS_STATUS={result.PreflightReport["DB_ROW_COUNTS_STATUS"]}");
Console.WriteLine($"LMAX_EVENT_TO_DB_MAPPING_STATUS={result.PreflightReport["LMAX_EVENT_TO_DB_MAPPING_STATUS"]}");
Console.WriteLine($"TARGET_STORAGE_TABLE_RECOMMENDATION={result.PreflightReport["TARGET_STORAGE_TABLE_RECOMMENDATION"] ?? "null"}");
Console.WriteLine($"DB_WRITE_GATE_STATUS={result.PreflightReport["DB_WRITE_GATE_STATUS"]}");
Console.WriteLine($"MARKETDATA_LMAX_DB_STATUS={result.PreflightReport["MARKETDATA_LMAX_DB_STATUS"]}");
Console.WriteLine($"SAFE_NEXT_PHASE={result.PreflightReport["SAFE_NEXT_PHASE"]}");
var summaryName = runKey.Contains("row-count-evidence", StringComparison.OrdinalIgnoreCase)
    ? "marketdata_db_row_count_evidence_summary.md"
    : "marketdata_db_persistence_preflight_summary.md";
Console.WriteLine($"summary={Path.Combine(outputRoot, "share", summaryName)}");

return string.Equals(result.PreflightReport["MARKETDATA_DB_PERSISTENCE_PREFLIGHT_STATUS"].ToString(), "FAIL", StringComparison.Ordinal) ? 2 : 0;

static void RefuseOverwrite(string outputRoot)
{
    var protectedFiles = new[]
    {
        Path.Combine(outputRoot, "10_validation", "marketdata_db_persistence_preflight_report.json"),
        Path.Combine(outputRoot, "manifest.json"),
        Path.Combine(outputRoot, "hashes.json"),
        Path.Combine(outputRoot, "manifest.sha256")
    };

    var existing = protectedFiles.FirstOrDefault(File.Exists);
    if (existing is not null)
    {
        throw new IOException($"MarketData DB persistence preflight refuses to overwrite existing artifact: {existing}");
    }
}

internal sealed class SqlServerMarketDataDbInventoryProvider(string connectionString, string connectionStringEnvVarName) : IMarketDataDbInventoryProvider
{
    private static readonly string[] NameFragments = ["Tick", "Quote", "Snapshot", "Bar", "Candle", "MarketData", "Lmax"];

    public async Task<MarketDataDbInventoryResult> InspectAsync(CancellationToken cancellationToken)
    {
        var tables = new List<MarketDataDbTableInventory>();
        var queries = new List<string>();
        var findings = new List<string>();

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var tableRows = await QueryTablesAsync(connection, queries, cancellationToken);
            foreach (var (schema, table) in tableRows)
            {
                var columns = await QueryColumnsAsync(connection, schema, table, queries, cancellationToken);
                var pk = await QueryPrimaryKeyAsync(connection, schema, table, queries, cancellationToken);
                var rowCount = await QueryRowCountAsync(connection, schema, table, queries, cancellationToken);
                var timestampColumns = columns.Keys.Where(IsTimestampColumn).ToArray();
                var minMax = timestampColumns.Length > 0
                    ? await QueryMinMaxTimestampAsync(connection, schema, table, timestampColumns[0], queries, cancellationToken)
                    : (null, null);

                tables.Add(new MarketDataDbTableInventory(
                    schema,
                    table,
                    InferStorageType(table, columns.Keys),
                    columns.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                    timestampColumns,
                    columns.Keys.Where(IsInstrumentColumn).ToArray(),
                    columns.Keys.Where(IsBidAskColumn).ToArray(),
                    columns.Keys.Where(IsOhlcColumn).ToArray(),
                    columns.Keys.Where(IsVolumeColumn).ToArray(),
                    pk,
                    columns.Where(pair => pair.Value.Nullable).Select(pair => pair.Key).ToArray(),
                    rowCount,
                    minMax.Item1,
                    minMax.Item2,
                    rowCount > 0 ? "PRESENT" : "PRESENT_EMPTY",
                    "db-select-only"));
            }

            if (tables.Count == 0)
            {
                findings.Add("No live DB candidate tables were discovered by SELECT-only schema inspection; repo-code evidence is included as fallback candidates.");
                tables.AddRange(MarketDataDbPersistencePreflightWriter.StaticCandidateTables("repo-code-evidence", rowCountsAvailable: false));
                return new MarketDataDbInventoryResult("PRESENT", "PARTIAL", "PARTIAL", true, connectionStringEnvVarName, tables, queries, findings);
            }

            return new MarketDataDbInventoryResult("PRESENT", "PRESENT", "PRESENT", true, connectionStringEnvVarName, tables, queries, findings);
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException or TimeoutException)
        {
            findings.Add($"DB SELECT-only inspection failed without mutation: {exception.GetType().Name}");
            tables.AddRange(MarketDataDbPersistencePreflightWriter.StaticCandidateTables("repo-code-evidence", rowCountsAvailable: false));
            return new MarketDataDbInventoryResult("FAILED", "PARTIAL", "FAILED", true, connectionStringEnvVarName, tables, queries, findings);
        }
    }

    private static async Task<IReadOnlyList<(string Schema, string Table)>> QueryTablesAsync(SqlConnection connection, List<string> queries, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
              AND (
                    TABLE_NAME IN ('MarketDataSnapshots', 'MarketDataBars', 'LmaxIndividualTrades', 'LmaxTradeSummaries')
                    OR TABLE_NAME LIKE '%Tick%'
                    OR TABLE_NAME LIKE '%Quote%'
                    OR TABLE_NAME LIKE '%Snapshot%'
                    OR TABLE_NAME LIKE '%Bar%'
                    OR TABLE_NAME LIKE '%Candle%'
                    OR TABLE_NAME LIKE '%MarketData%'
                    OR TABLE_NAME LIKE '%Lmax%'
                  )
            ORDER BY TABLE_SCHEMA, TABLE_NAME
            """;
        queries.Add("SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND candidate market-data name filters");
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<(string, string)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((reader.GetString(0), reader.GetString(1)));
        }

        return rows;
    }

    private static async Task<IReadOnlyDictionary<string, (string DataType, bool Nullable)>> QueryColumnsAsync(SqlConnection connection, string schema, string table, List<string> queries, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
            ORDER BY ORDINAL_POSITION
            """;
        queries.Add($"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table -- [{schema}].[{table}]");
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@table", table);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new Dictionary<string, (string, bool)>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns[reader.GetString(0)] = (reader.GetString(1), string.Equals(reader.GetString(2), "YES", StringComparison.OrdinalIgnoreCase));
        }

        return columns;
    }

    private static async Task<IReadOnlyList<string>> QueryPrimaryKeyAsync(SqlConnection connection, string schema, string table, List<string> queries, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT kcu.COLUMN_NAME
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
              ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
             AND tc.TABLE_SCHEMA = kcu.TABLE_SCHEMA
             AND tc.TABLE_NAME = kcu.TABLE_NAME
            WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
              AND tc.TABLE_SCHEMA = @schema
              AND tc.TABLE_NAME = @table
            ORDER BY kcu.ORDINAL_POSITION
            """;
        queries.Add($"SELECT primary key columns from INFORMATION_SCHEMA for [{schema}].[{table}]");
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@table", table);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    private static async Task<long> QueryRowCountAsync(SqlConnection connection, string schema, string table, List<string> queries, CancellationToken cancellationToken)
    {
        var sql = $"SELECT COUNT_BIG(*) FROM {Quote(schema)}.{Quote(table)}";
        queries.Add($"SELECT COUNT_BIG(*) FROM [{schema}].[{table}]");
        await using var command = new SqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    private static async Task<(string?, string?)> QueryMinMaxTimestampAsync(SqlConnection connection, string schema, string table, string column, List<string> queries, CancellationToken cancellationToken)
    {
        var sql = $"SELECT MIN({Quote(column)}), MAX({Quote(column)}) FROM {Quote(schema)}.{Quote(table)}";
        queries.Add($"SELECT MIN([{column}]), MAX([{column}]) FROM [{schema}].[{table}]");
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return (null, null);
        }

        static string? Value(SqlDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal).ToString();

        return (Value(reader, 0), Value(reader, 1));
    }

    private static string Quote(string identifier)
    {
        if (!identifier.All(ch => char.IsLetterOrDigit(ch) || ch == '_'))
        {
            throw new InvalidOperationException("Unsafe SQL identifier rejected before SELECT-only query construction.");
        }

        return $"[{identifier}]";
    }

    private static string InferStorageType(string tableName, IEnumerable<string> columns)
    {
        if (tableName.Contains("Snapshot", StringComparison.OrdinalIgnoreCase)) return "snapshot";
        if (tableName.Contains("Quote", StringComparison.OrdinalIgnoreCase)) return "quote";
        if (tableName.Contains("Tick", StringComparison.OrdinalIgnoreCase) || tableName.Contains("IndividualTrade", StringComparison.OrdinalIgnoreCase)) return "tick";
        if (tableName.Contains("Bar", StringComparison.OrdinalIgnoreCase) || tableName.Contains("Candle", StringComparison.OrdinalIgnoreCase)) return "bar";
        if (tableName.Contains("Summary", StringComparison.OrdinalIgnoreCase)) return "summary";
        var columnArray = columns.ToArray();
        if (columnArray.Any(IsOhlcColumn)) return "bar";
        if (columnArray.Any(IsBidAskColumn)) return "quote";
        return "unknown";
    }

    private static bool IsTimestampColumn(string column)
        => column.Contains("Timestamp", StringComparison.OrdinalIgnoreCase) ||
           column.EndsWith("AtUtc", StringComparison.OrdinalIgnoreCase) ||
           column.EndsWith("TimeUtc", StringComparison.OrdinalIgnoreCase) ||
           column.EndsWith("Date", StringComparison.OrdinalIgnoreCase);

    private static bool IsInstrumentColumn(string column)
        => column.Contains("Instrument", StringComparison.OrdinalIgnoreCase) ||
           column.Contains("Security", StringComparison.OrdinalIgnoreCase) ||
           column.Contains("Symbol", StringComparison.OrdinalIgnoreCase);

    private static bool IsBidAskColumn(string column)
        => column.Contains("Bid", StringComparison.OrdinalIgnoreCase) ||
           column.Contains("Ask", StringComparison.OrdinalIgnoreCase);

    private static bool IsOhlcColumn(string column)
        => column.Equals("Open", StringComparison.OrdinalIgnoreCase) ||
           column.Equals("High", StringComparison.OrdinalIgnoreCase) ||
           column.Equals("Low", StringComparison.OrdinalIgnoreCase) ||
           column.Equals("Close", StringComparison.OrdinalIgnoreCase) ||
           column.Contains("MidClose", StringComparison.OrdinalIgnoreCase);

    private static bool IsVolumeColumn(string column)
        => column.Contains("Volume", StringComparison.OrdinalIgnoreCase) ||
           column.Contains("Quantity", StringComparison.OrdinalIgnoreCase) ||
           column.Contains("ObservationCount", StringComparison.OrdinalIgnoreCase);
}

internal sealed record CliOptions(
    string? RunKey,
    string? RepoRoot,
    string? OutputRoot,
    string? SourceRunRoot,
    string? FreezeRoot,
    string? ConnectionStringEnv,
    bool NoExternal,
    bool NoExecution,
    bool NoDbWrite,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        string? runKey = null;
        string? repoRoot = null;
        string? outputRoot = null;
        string? sourceRunRoot = null;
        string? freezeRoot = null;
        string? connectionStringEnv = null;
        var noExternal = false;
        var noExecution = false;
        var noDbWrite = false;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "-h" or "--help")
            {
                showHelp = true;
                continue;
            }

            string? Next() => i + 1 < args.Length ? args[++i] : null;
            switch (arg)
            {
                case "--run-key": runKey = Next(); break;
                case "--repo-root": repoRoot = Next(); break;
                case "--output-root": outputRoot = Next(); break;
                case "--source-run-root": sourceRunRoot = Next(); break;
                case "--freeze-root": freezeRoot = Next(); break;
                case "--connection-string-env": connectionStringEnv = Next(); break;
                case "--no-external": noExternal = bool.Parse(Next() ?? "false"); break;
                case "--no-execution": noExecution = bool.Parse(Next() ?? "false"); break;
                case "--no-db-write": noDbWrite = bool.Parse(Next() ?? "false"); break;
            }
        }

        return new(runKey, repoRoot, outputRoot, sourceRunRoot, freezeRoot, connectionStringEnv, noExternal, noExecution, noDbWrite, showHelp);
    }
}
