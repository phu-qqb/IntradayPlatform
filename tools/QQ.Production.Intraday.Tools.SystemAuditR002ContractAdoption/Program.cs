using QQ.Production.Intraday.Application;
using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;

var cli = CliOptions.Parse(args);
if (cli.ShowHelp)
{
    Console.WriteLine("Usage: dotnet run --project tools/QQ.Production.Intraday.Tools.SystemAuditR002ContractAdoption -- --run-key <RunKey> --repo-root . --output-root <path> --no-external true --no-execution true");
    return 0;
}

var runKey = cli.RunKey ?? "system-audit-r002-adoption-001";
var outputRoot = Path.GetFullPath(cli.OutputRoot ?? Path.Combine("artifacts", "system-audit-r002-adoption", runKey));
RefuseOverwrite(outputRoot);
var connectionString = string.IsNullOrWhiteSpace(cli.ConnectionStringEnv) ? null : Environment.GetEnvironmentVariable(cli.ConnectionStringEnv);
IReadOnlyList<SystemAuditR002DbTableEvidence> dbTables = string.IsNullOrWhiteSpace(connectionString)
    ? Array.Empty<SystemAuditR002DbTableEvidence>()
    : await LoadDbTablesAsync(connectionString, CancellationToken.None);

var request = new SystemAuditR002ContractAdoptionRequest(
    runKey,
    Path.GetFullPath(cli.RepoRoot ?? "."),
    outputRoot,
    cli.NoExternal,
    cli.NoExecution,
    cli.ConnectionStringEnv,
    !string.IsNullOrWhiteSpace(connectionString),
    string.IsNullOrWhiteSpace(connectionString) ? null : Sha256(connectionString)[..12],
    dbTables,
    cli.CanonicalTargetCloseUtc,
    cli.WindowStartUtc,
    cli.WindowEndUtc,
    cli.QuoteWindowReadinessId,
    cli.CloseBenchmarkReadinessId,
    cli.FeedQualityReadinessId);

var result = await SystemAuditR002ContractAdoption.RunAsync(request, CancellationToken.None);

Console.WriteLine($"SYSTEM_AUDIT_R002_ADOPTION_STATUS={result.SystemAuditR002AdoptionStatus}");
Console.WriteLine($"LMAX_MARKETDATA_DB_V1_ADOPTED={result.LmaxMarketdataDbV1Adopted}");
Console.WriteLine($"MARKETDATA_READINESS_V1_ADOPTED={result.MarketdataReadinessV1Adopted}");
Console.WriteLine($"CANONICAL_TIMING_V1_ADOPTED={result.CanonicalTimingV1Adopted}");
Console.WriteLine($"ENVIRONMENT_SECRET_V1_ADOPTED={result.EnvironmentSecretV1Adopted}");
if (runKey.Contains("gap-closure", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine($"SYSTEM_AUDIT_R002_GAP_CLOSURE_STATUS={result.SystemAuditR002AdoptionStatus}");
}

Console.WriteLine($"connection_string_present={!string.IsNullOrWhiteSpace(connectionString)}");
Console.WriteLine($"adoption_report={result.AdoptionReportPath}");
Console.WriteLine($"evidence_matrix={result.EvidenceMatrixPath}");
Console.WriteLine($"summary={result.SummaryPath}");
Console.WriteLine($"manifest={result.ManifestPath}");

return result.SystemAuditR002AdoptionStatus == "FAIL" ? 2 : 0;

static void RefuseOverwrite(string outputRoot)
{
    var protectedFiles = new[]
    {
        Path.Combine(outputRoot, "10_validation", "system_audit_r002_contract_adoption_report.json"),
        Path.Combine(outputRoot, "10_validation", "system_audit_r002_evidence_matrix.json"),
        Path.Combine(outputRoot, "manifest.json"),
        Path.Combine(outputRoot, "hashes.json"),
        Path.Combine(outputRoot, "manifest.sha256")
    };

    var existing = protectedFiles.FirstOrDefault(File.Exists);
    if (existing is not null)
    {
        throw new IOException($"SYSTEM-AUDIT-R002 adoption refuses to overwrite existing artifact: {existing}");
    }
}

static async Task<IReadOnlyList<SystemAuditR002DbTableEvidence>> LoadDbTablesAsync(string connectionString, CancellationToken cancellationToken)
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync(cancellationToken);
    var tables = new List<SystemAuditR002DbTableEvidence>();
    await using var tableCommand = connection.CreateCommand();
    tableCommand.CommandText = """
        SELECT s.name AS SchemaName, t.name AS TableName
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE t.name IN ('MarketDataSnapshots','MarketDataBars','LmaxIndividualTrades','LmaxTradeSummaries')
           OR t.name LIKE '%Tick%' OR t.name LIKE '%Trade%' OR t.name LIKE '%Quote%' OR t.name LIKE '%Bar%' OR t.name LIKE '%MarketData%'
        ORDER BY s.name, t.name
        """;
    await using var reader = await tableCommand.ExecuteReaderAsync(cancellationToken);
    var tableNames = new List<(string Schema, string Table)>();
    while (await reader.ReadAsync(cancellationToken))
    {
        tableNames.Add((reader.GetString(0), reader.GetString(1)));
    }

    await reader.CloseAsync();
    foreach (var (schema, table) in tableNames)
    {
        var columns = await LoadColumnsAsync(connection, schema, table, cancellationToken);
        var timestampColumns = columns.Where(IsTimestampColumn).ToArray();
        var instrumentColumns = columns.Where(IsInstrumentColumn).ToArray();
        var bidAskColumns = columns.Where(IsBidAskColumn).ToArray();
        var ohlcColumns = columns.Where(IsOhlcColumn).ToArray();
        var volumeColumns = columns.Where(IsVolumeColumn).ToArray();
        var rowCount = await CountRowsAsync(connection, schema, table, cancellationToken);
        var (minTimestamp, maxTimestamp) = timestampColumns.Length == 0
            ? (null, null)
            : await TimestampRangeAsync(connection, schema, table, timestampColumns[0], cancellationToken);

        tables.Add(new SystemAuditR002DbTableEvidence(
            schema,
            table,
            InferStorageType(table, columns),
            columns,
            timestampColumns,
            instrumentColumns,
            bidAskColumns,
            ohlcColumns,
            volumeColumns,
            rowCount,
            minTimestamp,
            maxTimestamp,
            rowCount == 0 ? "PRESENT_EMPTY" : "PRESENT"));
    }

    return tables;
}

static async Task<IReadOnlyList<string>> LoadColumnsAsync(SqlConnection connection, string schema, string table, CancellationToken cancellationToken)
{
    await using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT c.name
        FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = @schema AND t.name = @table
        ORDER BY c.column_id
        """;
    command.Parameters.AddWithValue("@schema", schema);
    command.Parameters.AddWithValue("@table", table);
    var columns = new List<string>();
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
    {
        columns.Add(reader.GetString(0));
    }

    return columns;
}

static async Task<long> CountRowsAsync(SqlConnection connection, string schema, string table, CancellationToken cancellationToken)
{
    await using var command = connection.CreateCommand();
    command.CommandText = $"SELECT COUNT_BIG(*) FROM {Quote(schema)}.{Quote(table)}";
    return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
}

static async Task<(string? Min, string? Max)> TimestampRangeAsync(SqlConnection connection, string schema, string table, string column, CancellationToken cancellationToken)
{
    await using var command = connection.CreateCommand();
    command.CommandText = $"SELECT MIN({Quote(column)}), MAX({Quote(column)}) FROM {Quote(schema)}.{Quote(table)}";
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    if (!await reader.ReadAsync(cancellationToken))
    {
        return (null, null);
    }

    return (reader.IsDBNull(0) ? null : Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture),
        reader.IsDBNull(1) ? null : Convert.ToString(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture));
}

static string Quote(string identifier) => "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

static string InferStorageType(string table, IReadOnlyList<string> columns)
{
    if (table.Contains("Bar", StringComparison.OrdinalIgnoreCase)) return "bar";
    if (table.Contains("Snapshot", StringComparison.OrdinalIgnoreCase)) return "quote";
    if (table.Contains("Trade", StringComparison.OrdinalIgnoreCase) || table.Contains("Tick", StringComparison.OrdinalIgnoreCase)) return "tick";
    if (columns.Any(IsOhlcColumn)) return "bar";
    if (columns.Any(IsBidAskColumn)) return "quote";
    return "unknown";
}

static bool IsTimestampColumn(string name) => name.Contains("Time", StringComparison.OrdinalIgnoreCase) || name.Contains("Utc", StringComparison.OrdinalIgnoreCase) || name.Contains("Date", StringComparison.OrdinalIgnoreCase);
static bool IsInstrumentColumn(string name) => name.Contains("Instrument", StringComparison.OrdinalIgnoreCase) || name.Contains("Symbol", StringComparison.OrdinalIgnoreCase) || name.Contains("Security", StringComparison.OrdinalIgnoreCase);
static bool IsBidAskColumn(string name) => name.Contains("Bid", StringComparison.OrdinalIgnoreCase) || name.Contains("Ask", StringComparison.OrdinalIgnoreCase);
static bool IsOhlcColumn(string name) => name.Contains("Open", StringComparison.OrdinalIgnoreCase) || name.Contains("High", StringComparison.OrdinalIgnoreCase) || name.Contains("Low", StringComparison.OrdinalIgnoreCase) || name.Contains("Close", StringComparison.OrdinalIgnoreCase);
static bool IsVolumeColumn(string name) => name.Contains("Volume", StringComparison.OrdinalIgnoreCase) || name.Contains("Quantity", StringComparison.OrdinalIgnoreCase) || name.Contains("Count", StringComparison.OrdinalIgnoreCase);

internal sealed record CliOptions(
    string? RunKey,
    string? RepoRoot,
    string? OutputRoot,
    string? ConnectionStringEnv,
    bool NoExternal,
    bool NoExecution,
    string? CanonicalTargetCloseUtc,
    string? WindowStartUtc,
    string? WindowEndUtc,
    string? QuoteWindowReadinessId,
    string? CloseBenchmarkReadinessId,
    string? FeedQualityReadinessId,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        string? runKey = null;
        string? repoRoot = null;
        string? outputRoot = null;
        string? connectionStringEnv = null;
        var noExternal = false;
        var noExecution = false;
        string? canonicalTargetCloseUtc = null;
        string? windowStartUtc = null;
        string? windowEndUtc = null;
        string? quoteWindowReadinessId = null;
        string? closeBenchmarkReadinessId = null;
        string? feedQualityReadinessId = null;
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
                case "--connection-string-env": connectionStringEnv = Next(); break;
                case "--no-external": noExternal = ParseBool(Next()); break;
                case "--no-execution": noExecution = ParseBool(Next()); break;
                case "--connection-string": _ = Next(); break;
                case "--canonical-target-close-utc": canonicalTargetCloseUtc = Next(); break;
                case "--window-start-utc": windowStartUtc = Next(); break;
                case "--window-end-utc": windowEndUtc = Next(); break;
                case "--quote-window-readiness-id": quoteWindowReadinessId = Next(); break;
                case "--close-benchmark-readiness-id": closeBenchmarkReadinessId = Next(); break;
                case "--feed-quality-readiness-id": feedQualityReadinessId = Next(); break;
            }
        }

        return new CliOptions(runKey, repoRoot, outputRoot, connectionStringEnv, noExternal, noExecution, canonicalTargetCloseUtc, windowStartUtc, windowEndUtc, quoteWindowReadinessId, closeBenchmarkReadinessId, feedQualityReadinessId, showHelp);
    }

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out var parsed) && parsed;
}
