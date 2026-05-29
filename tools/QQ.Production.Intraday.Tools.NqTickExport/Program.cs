using System.Data;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using QQ.Production.Intraday.Application;

var options = CliOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine("Usage: dotnet run --project tools/QQ.Production.Intraday.Tools.NqTickExport -- --run-key <RunKey> --output-root <path> [--connection-string <sql>] [--from <iso>] [--to <iso>] [--contract <NQH25>] [--max-rows <n>] [--tick-kind <trades|quotes|all>] [--assume-source-timezone <tz>] --no-execution true");
    return 0;
}

if (!options.NoExecution)
{
    Console.Error.WriteLine("NQ tick export is read-only and requires --no-execution true.");
    return 2;
}

var runKey = options.RunKey ?? $"nq-ticks-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
var outputRoot = options.OutputRoot ?? Path.Combine("artifacts", "nq-tick-export", runKey);
var connection = ResolveConnectionString(options.ConnectionString);
var source = new SqlNqTickExportSource(connection.ConnectionString, options.AssumeSourceTimezone);
var result = await NqTickExportService.ExportAsync(source, new NqTickExportRequest(
    runKey,
    outputRoot,
    options.FromUtc,
    options.ToUtc,
    options.Contract,
    options.MaxRows,
    options.TickKind,
    options.AssumeSourceTimezone,
    options.NoExecution), CancellationToken.None);

Console.WriteLine($"export_status={result.Metadata.ExportStatus}");
Console.WriteLine($"row_count={result.Metadata.DataFile.RowCount}");
Console.WriteLine($"metadata={Path.Combine(outputRoot, "share", $"nq_ticks_{runKey}.metadata.json")}");
if (result.DataFilePath is not null) Console.WriteLine($"data={result.DataFilePath}");
return result.Metadata.ExportStatus == "FAIL" ? 2 : 0;

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

internal sealed record CliOptions(
    string? RunKey,
    string? OutputRoot,
    string? ConnectionString,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? Contract,
    int? MaxRows,
    NqTickKind? TickKind,
    string? AssumeSourceTimezone,
    bool NoExecution,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        string? runKey = null;
        string? outputRoot = null;
        string? connectionString = null;
        DateTimeOffset? from = null;
        DateTimeOffset? to = null;
        string? contract = null;
        int? maxRows = null;
        NqTickKind? tickKind = null;
        string? timezone = null;
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
                case "--from": from = DateTimeOffset.Parse(value).ToUniversalTime(); index++; break;
                case "--to": to = DateTimeOffset.Parse(value).ToUniversalTime(); index++; break;
                case "--contract": contract = value; index++; break;
                case "--max-rows": maxRows = int.TryParse(value, out var parsedMaxRows) ? parsedMaxRows : null; index++; break;
                case "--assume-source-timezone": timezone = value; index++; break;
                case "--no-execution": noExecution = bool.TryParse(value, out var parsedNoExecution) && parsedNoExecution; index++; break;
                case "--tick-kind":
                    tickKind = value.Equals("trades", StringComparison.OrdinalIgnoreCase) ? NqTickKind.Trades :
                        value.Equals("quotes", StringComparison.OrdinalIgnoreCase) ? NqTickKind.Quotes :
                        NqTickKind.All;
                    index++;
                    break;
            }
        }

        return new CliOptions(runKey, outputRoot, connectionString, from, to, contract, maxRows, tickKind, timezone, noExecution, showHelp);
    }
}

internal sealed class SqlNqTickExportSource(string connectionString, string? assumedTimezone) : INqTickExportSource
{
    public string DatabaseDescription
    {
        get
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "(configured database)" : builder.InitialCatalog;
        }
    }

    public async Task<IReadOnlyList<NqTickCandidateTable>> DiscoverTablesAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        const string sql = """
            SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION;
            """;
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new List<(string Schema, string Table, string Column)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        return NqTickExportService.InferCandidateTables(columns);
    }

    public async IAsyncEnumerable<NqTickSourceRow> ReadRowsAsync(NqTickExportQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var table in query.Tables)
        {
            var sql = BuildSelect(table, query);
            await using var command = new SqlCommand(sql, connection);
            command.CommandType = CommandType.Text;
            if (query.FromUtc is not null) command.Parameters.Add(new SqlParameter("@from", SqlDbType.DateTimeOffset) { Value = query.FromUtc.Value });
            if (query.ToUtc is not null) command.Parameters.Add(new SqlParameter("@to", SqlDbType.DateTimeOffset) { Value = query.ToUtc.Value });
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                yield return MapRow(reader, table);
            }
        }
    }

    private static string BuildSelect(NqTickCandidateTable table, NqTickExportQuery query)
    {
        var select = new[]
        {
            SelectColumn(table.TimestampColumn, "source_timestamp"),
            SelectColumn(table.RootColumn, "root"),
            SelectColumn(table.ContractColumn, "contract"),
            SelectColumn(table.PriceColumn, "price"),
            SelectColumn(table.SizeColumn, "size"),
            SelectColumn(table.BidPriceColumn, "bid_price"),
            SelectColumn(table.BidSizeColumn, "bid_size"),
            SelectColumn(table.AskPriceColumn, "ask_price"),
            SelectColumn(table.AskSizeColumn, "ask_size"),
            SelectColumn(table.ExchangeColumn, "exchange"),
            SelectColumn(table.SequenceColumn, "sequence"),
            SelectColumn(table.ConditionsColumn, "conditions"),
            SelectColumn(table.SourceColumn, "source"),
            SelectColumn(table.SourceRowIdColumn, "source_row_id")
        };
        var top = query.MaxRows is > 0 ? $"TOP ({query.MaxRows.Value}) " : string.Empty;
        var where = new List<string>();
        if (table.RootColumn is not null) where.Add($"{Quote(table.RootColumn)} IN ('NQ','MNQ')");
        if (table.ContractColumn is not null) where.Add($"({Quote(table.ContractColumn)} LIKE 'NQ%' OR {Quote(table.ContractColumn)} LIKE 'MNQ%')");
        if (query.FromUtc is not null) where.Add($"{Quote(table.TimestampColumn!)} >= @from");
        if (query.ToUtc is not null) where.Add($"{Quote(table.TimestampColumn!)} <= @to");
        var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : string.Empty;
        var order = $"ORDER BY {Quote(table.TimestampColumn!)}, {Quote(table.ContractColumn ?? table.RootColumn ?? table.TimestampColumn!)}, {OrderColumn(table.SequenceColumn)}, {OrderColumn(table.SourceRowIdColumn)}";
        return $"SELECT {top}{string.Join(", ", select)}, '{table.Schema}.{table.Table}' AS source_table FROM {Quote(table.Schema)}.{Quote(table.Table)} {whereSql} {order};";
    }

    private NqTickSourceRow MapRow(SqlDataReader reader, NqTickCandidateTable table)
    {
        var rawTimestamp = reader.GetValue(0);
        var timestamp = ConvertTimestamp(rawTimestamp, assumedTimezone, out var sourceTimezone);
        var sourceTimestamp = rawTimestamp?.ToString() ?? string.Empty;
        var eventType = table.SupportsQuotes && !table.SupportsTrades ? "quote" : table.SupportsQuotes ? "bbo" : "trade";
        return new NqTickSourceRow(
            timestamp,
            sourceTimestamp,
            sourceTimezone,
            ReadString(reader, 1),
            ReadString(reader, 2),
            eventType,
            ReadString(reader, 3),
            ReadString(reader, 4),
            ReadString(reader, 5),
            ReadString(reader, 6),
            ReadString(reader, 7),
            ReadString(reader, 8),
            ReadString(reader, 9),
            ReadString(reader, 10),
            ReadString(reader, 11),
            ReadString(reader, 14) ?? $"{table.Schema}.{table.Table}",
            ReadString(reader, 13));
    }

    private static DateTimeOffset? ConvertTimestamp(object raw, string? assumeTimezone, out string sourceTimezone)
    {
        sourceTimezone = "unknown";
        if (raw is DateTimeOffset dto)
        {
            sourceTimezone = dto.Offset == TimeSpan.Zero ? "UTC" : "source_offset";
            return dto.ToUniversalTime();
        }

        if (raw is DateTime dt)
        {
            if (!string.IsNullOrWhiteSpace(assumeTimezone))
            {
                sourceTimezone = assumeTimezone;
                var zone = TimeZoneInfo.FindSystemTimeZoneById(assumeTimezone);
                return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), zone);
            }

            if (dt.Kind == DateTimeKind.Utc)
            {
                sourceTimezone = "UTC";
                return new DateTimeOffset(dt, TimeSpan.Zero);
            }

            return null;
        }

        if (DateTimeOffset.TryParse(raw?.ToString(), out var parsed))
        {
            sourceTimezone = parsed.Offset == TimeSpan.Zero ? "UTC" : "source_offset";
            return parsed.ToUniversalTime();
        }

        return null;
    }

    private static string SelectColumn(string? column, string alias)
        => column is null ? $"CAST(NULL AS nvarchar(max)) AS {alias}" : $"{Quote(column)} AS {alias}";

    private static string OrderColumn(string? column)
        => column is null ? "(SELECT NULL)" : Quote(column);

    private static string Quote(string identifier)
        => "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";

    private static string? ReadString(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
}
