using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QQ.Production.Intraday.Application;

var options = CliOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine("Usage: dotnet run --project tools/QQ.Production.Intraday.Tools.M15DbCoverageAudit -- --run-key <RunKey> --output-root <package-root> [--connection-string <sql>] [--appsettings <path>]");
    return 0;
}

var producedAtUtc = DateTimeOffset.UtcNow;
var runKey = string.IsNullOrWhiteSpace(options.RunKey)
    ? $"m15-db-coverage-{producedAtUtc:yyyyMMddHHmmss}"
    : options.RunKey;
var outputRoot = string.IsNullOrWhiteSpace(options.OutputRoot)
    ? Path.Combine("artifacts", "qubes-intraday", runKey)
    : options.OutputRoot;
var validationDirectory = Path.Combine(outputRoot, "10_validation");

var connection = ResolveConnectionString(options);
var readOnlyConnectionString = NormalizeReadOnlyConnectionString(connection.ConnectionString);
await using var sql = new SqlConnection(readOnlyConnectionString);
await sql.OpenAsync();

var candidateTables = await LoadCandidateTablesAsync(sql);
var availableInstruments = await LoadAvailableInstrumentsAsync(sql);
var rows = candidateTables.Any(x => x.SupportedByAudit && x.Table.Equals("MarketDataBars", StringComparison.OrdinalIgnoreCase))
    ? await LoadMarketDataBarsM15RowsAsync(sql)
    : [];

var package = M15DbCoverageAudit.Audit(new M15DbCoverageAuditRequest(
    runKey,
    producedAtUtc,
    candidateTables,
    rows,
    availableInstruments,
    connection.Source,
    RedactConnectionString(readOnlyConnectionString)));

await M15DbCoverageAudit.WritePackageAsync(validationDirectory, package, CancellationToken.None);
Console.WriteLine($"M15_DB_COVERAGE_GATE={package.Report.M15_DB_COVERAGE_GATE}");
Console.WriteLine($"Validation package: {Path.GetFullPath(validationDirectory)}");
return package.Report.M15_DB_COVERAGE_GATE == M15DbCoverageGate.FAIL.ToString() ? 2 : 0;

static async Task<IReadOnlyList<M15DbCandidateTable>> LoadCandidateTablesAsync(SqlConnection connection)
{
    const string sql = """
        SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME
        FROM INFORMATION_SCHEMA.COLUMNS
        ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION;
        """;
    await using var command = new SqlCommand(sql, connection);
    command.CommandType = CommandType.Text;
    await using var reader = await command.ExecuteReaderAsync();
    var columns = new List<(string Schema, string Table, string Column)>();
    while (await reader.ReadAsync())
    {
        columns.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
    }

    return M15DbCoverageAudit.InferCandidateTables(columns);
}

static async Task<IReadOnlyList<string>> LoadAvailableInstrumentsAsync(SqlConnection connection)
{
    const string sql = """
        IF OBJECT_ID(N'dbo.Instruments', N'U') IS NOT NULL
            SELECT Symbol FROM dbo.Instruments WHERE IsEnabled = 1 AND IsMarketDataEnabled = 1 ORDER BY Symbol;
        """;
    await using var command = new SqlCommand(sql, connection);
    command.CommandType = CommandType.Text;
    await using var reader = await command.ExecuteReaderAsync();
    var instruments = new List<string>();
    while (await reader.ReadAsync())
    {
        if (!reader.IsDBNull(0))
        {
            instruments.Add(reader.GetString(0));
        }
    }

    return instruments;
}

static async Task<IReadOnlyList<M15DbCandleRow>> LoadMarketDataBarsM15RowsAsync(SqlConnection connection)
{
    const int fifteenMinutes = 1;
    const string sql = """
        SELECT
            COALESCE(i.Symbol, CONVERT(nvarchar(36), b.InstrumentId)) AS Instrument,
            v.Name AS Venue,
            b.BarEndUtc AS TimestampUtc,
            TRY_CONVERT(decimal(38, 10), b.MidClose) AS [Close]
        FROM dbo.MarketDataBars b
        LEFT JOIN dbo.Instruments i ON i.Id = b.InstrumentId
        LEFT JOIN dbo.Venues v ON v.Id = b.VenueId
        WHERE b.Timeframe = @timeframe
        ORDER BY COALESCE(i.Symbol, CONVERT(nvarchar(36), b.InstrumentId)), v.Name, b.BarEndUtc, b.Id;
        """;
    await using var command = new SqlCommand(sql, connection);
    command.CommandType = CommandType.Text;
    command.Parameters.Add(new SqlParameter("@timeframe", SqlDbType.Int) { Value = fifteenMinutes });
    await using var reader = await command.ExecuteReaderAsync();
    var rows = new List<M15DbCandleRow>();
    var ordinal = 0;
    while (await reader.ReadAsync())
    {
        rows.Add(new M15DbCandleRow(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetFieldValue<DateTimeOffset>(2).ToUniversalTime(),
            M15TimestampRole.BarCloseUtc,
            reader.IsDBNull(3) ? null : reader.GetDecimal(3),
            ordinal++));
    }

    return rows;
}

static ResolvedConnectionString ResolveConnectionString(CliOptions options)
{
    if (!string.IsNullOrWhiteSpace(options.ConnectionString))
    {
        return new ResolvedConnectionString(options.ConnectionString, "cli:--connection-string");
    }

    var env = Environment.GetEnvironmentVariable("QQ_INTRADAY_SQLSERVER_CONNECTIONSTRING");
    if (!string.IsNullOrWhiteSpace(env))
    {
        return new ResolvedConnectionString(env, "env:QQ_INTRADAY_SQLSERVER_CONNECTIONSTRING");
    }

    var appsettingsPath = string.IsNullOrWhiteSpace(options.AppsettingsPath)
        ? Path.Combine("src", "QQ.Production.Intraday.Api", "appsettings.json")
        : options.AppsettingsPath;
    var discovered = TryReadConnectionString(appsettingsPath);
    if (!string.IsNullOrWhiteSpace(discovered))
    {
        return new ResolvedConnectionString(discovered, appsettingsPath);
    }

    throw new InvalidOperationException("No DB connection string found. Provide --connection-string, QQ_INTRADAY_SQLSERVER_CONNECTIONSTRING, or appsettings.json ConnectionStrings:IntradaySqlServer.");
}

static string? TryReadConnectionString(string path)
{
    if (!File.Exists(path))
    {
        return null;
    }

    using var doc = JsonDocument.Parse(File.ReadAllText(path));
    if (!doc.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings))
    {
        return null;
    }

    return connectionStrings.TryGetProperty("IntradaySqlServer", out var intraday)
        ? intraday.GetString()
        : null;
}

static string RedactConnectionString(string connectionString)
{
    var builder = new SqlConnectionStringBuilder(connectionString);
    if (builder.ContainsKey("Password")) builder.Password = "***";
    if (builder.ContainsKey("User ID")) builder.UserID = "***";
    return builder.ConnectionString;
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

internal sealed record CliOptions(
    string? RunKey,
    string? OutputRoot,
    string? ConnectionString,
    string? AppsettingsPath,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        string? runKey = null;
        string? outputRoot = null;
        string? connectionString = null;
        string? appsettingsPath = null;
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
            if (value is null || value.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            switch (current)
            {
                case "--run-key":
                    runKey = value;
                    index++;
                    break;
                case "--output-root":
                    outputRoot = value;
                    index++;
                    break;
                case "--connection-string":
                    connectionString = value;
                    index++;
                    break;
                case "--appsettings":
                    appsettingsPath = value;
                    index++;
                    break;
            }
        }

        return new CliOptions(runKey, outputRoot, connectionString, appsettingsPath, showHelp);
    }
}
