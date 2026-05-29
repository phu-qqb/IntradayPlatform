using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Application;

public sealed record MarketDataDbPersistencePreflightOptions(
    string RunKey,
    string RepoRoot,
    string OutputRoot,
    string SourceRunRoot,
    string FreezeRoot,
    string ConnectionStringEnvVarName,
    bool ConnectionStringPresent,
    bool NoExternal,
    bool NoExecution,
    bool NoDbWrite);

public sealed record MarketDataDbPersistencePreflightResult(
    IReadOnlyDictionary<string, object> PreflightReport,
    IReadOnlyDictionary<string, object> SchemaInventory,
    IReadOnlyDictionary<string, object> EventMappingReport,
    IReadOnlyDictionary<string, object> RowCountsReport,
    IReadOnlyDictionary<string, object> BlockersReport,
    IReadOnlyDictionary<string, object> BoundaryReport,
    IReadOnlyList<string> Files);

public sealed record MarketDataDbTableInventory(
    string SchemaName,
    string TableName,
    string StorageType,
    IReadOnlyList<string> Columns,
    IReadOnlyList<string> TimestampColumns,
    IReadOnlyList<string> InstrumentColumns,
    IReadOnlyList<string> BidAskColumns,
    IReadOnlyList<string> OhlcColumns,
    IReadOnlyList<string> VolumeColumns,
    IReadOnlyList<string> PrimaryKeyColumns,
    IReadOnlyList<string> NullableColumns,
    long? RowCount,
    string? MinTimestampUtc,
    string? MaxTimestampUtc,
    string EvidenceStatus,
    string EvidenceSource);

public sealed record MarketDataDbInventoryResult(
    string ConnectionStatus,
    string SchemaInventoryStatus,
    string RowCountsStatus,
    bool ConnectionStringPresent,
    string ConnectionStringEnvVarName,
    IReadOnlyList<MarketDataDbTableInventory> Tables,
    IReadOnlyList<string> ReadOnlyQueries,
    IReadOnlyList<string> Findings);

public interface IMarketDataDbInventoryProvider
{
    Task<MarketDataDbInventoryResult> InspectAsync(CancellationToken cancellationToken);
}

public sealed class MissingConnectionStringMarketDataDbInventoryProvider(string connectionStringEnvVarName) : IMarketDataDbInventoryProvider
{
    public Task<MarketDataDbInventoryResult> InspectAsync(CancellationToken cancellationToken)
        => Task.FromResult(new MarketDataDbInventoryResult(
            "MISSING",
            "PARTIAL",
            "MISSING_CONNECTION_STRING",
            false,
            connectionStringEnvVarName,
            MarketDataDbPersistencePreflightWriter.StaticCandidateTables("repo-code-evidence", rowCountsAvailable: false),
            Array.Empty<string>(),
            [$"Connection string env var {connectionStringEnvVarName} is missing; live DB schema and row counts were not inspected."]));
}

public sealed class MarketDataDbPersistencePreflightWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<MarketDataDbPersistencePreflightResult> WriteAsync(
        MarketDataDbPersistencePreflightOptions options,
        IMarketDataDbInventoryProvider dbInventoryProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(dbInventoryProvider);

        var outputRoot = Path.GetFullPath(options.OutputRoot);
        var validationRoot = Path.Combine(outputRoot, "10_validation");
        var shareRoot = Path.Combine(outputRoot, "share");
        Directory.CreateDirectory(validationRoot);
        Directory.CreateDirectory(shareRoot);

        var source = await InspectSourceArtifactsAsync(options, cancellationToken);
        var dbInventory = await dbInventoryProvider.InspectAsync(cancellationToken);
        var mapping = BuildEventMappingReport(options, source, dbInventory);
        var rowCounts = BuildRowCountsReport(options, dbInventory);
        var blockers = BuildBlockersReport(options, source, dbInventory, mapping);
        var boundary = BuildBoundaryReport(options);
        var schema = BuildSchemaInventory(options, dbInventory);
        var preflight = BuildPreflightReport(options, source, dbInventory, mapping, blockers, boundary);

        var isRowCountEvidenceGate = IsRowCountEvidenceGate(options.RunKey);
        var mainReportName = isRowCountEvidenceGate ? "marketdata_db_row_count_evidence_report" : "marketdata_db_persistence_preflight_report";
        var summaryName = isRowCountEvidenceGate ? "marketdata_db_row_count_evidence_summary.md" : "marketdata_db_persistence_preflight_summary.md";

        await WriteJsonAndMarkdownAsync(validationRoot, mainReportName, preflight, Markdown.Preflight(preflight), cancellationToken);
        await WriteJsonAndMarkdownAsync(validationRoot, "marketdata_db_schema_inventory", schema, Markdown.Schema(schema), cancellationToken);
        await WriteJsonAndMarkdownAsync(validationRoot, "marketdata_lmax_event_to_db_mapping_report", mapping, Markdown.Mapping(mapping), cancellationToken);
        await WriteJsonAndMarkdownAsync(validationRoot, "marketdata_db_row_counts_report", rowCounts, Markdown.RowCounts(rowCounts), cancellationToken);
        if (!isRowCountEvidenceGate)
        {
            await WriteJsonAndMarkdownAsync(validationRoot, "marketdata_db_persistence_blockers_report", blockers, Markdown.Blockers(blockers), cancellationToken);
        }

        await WriteJsonAndMarkdownAsync(validationRoot, "marketdata_db_no_mutation_boundary_report", boundary, Markdown.Boundary(boundary), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(shareRoot, summaryName), Markdown.Summary(preflight), cancellationToken);
        await WriteManifestAsync(outputRoot, options.RunKey, preflight, cancellationToken);

        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(outputRoot, path).Replace('\\', '/'))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new(preflight, schema, mapping, rowCounts, blockers, boundary, files);
    }

    public static IReadOnlyList<MarketDataDbTableInventory> StaticCandidateTables(string evidenceSource, bool rowCountsAvailable)
        =>
        [
            new("dbo", "MarketDataSnapshots", "quote", ["Id", "InstrumentId", "VenueId", "Bid", "Ask", "BidSize", "AskSize", "SourceTimestampUtc", "ReceivedAtUtc", "Source"], ["SourceTimestampUtc", "ReceivedAtUtc"], ["InstrumentId"], ["Bid", "Ask", "BidSize", "AskSize"], [], [], ["Id"], [], null, null, null, rowCountsAvailable ? "PRESENT" : "PARTIAL", evidenceSource),
            new("dbo", "MarketDataBars", "bar", ["Id", "InstrumentId", "VenueId", "Timeframe", "BarStartUtc", "BarEndUtc", "Open", "High", "Low", "Close", "BidClose", "AskClose", "MidClose", "ObservationCount"], ["BarStartUtc", "BarEndUtc"], ["InstrumentId"], ["BidClose", "AskClose"], ["Open", "High", "Low", "Close", "MidClose"], ["ObservationCount"], ["Id"], [], null, null, null, rowCountsAvailable ? "PRESENT" : "PARTIAL", evidenceSource),
            new("dbo", "LmaxIndividualTrades", "tick", ["Id", "ExecutionId", "TransactionTimeUtc", "Price", "Quantity", "SecurityId", "LmaxSymbol"], ["TransactionTimeUtc"], ["SecurityId", "LmaxSymbol"], [], [], ["Quantity"], ["Id"], [], null, null, null, rowCountsAvailable ? "PRESENT" : "PARTIAL", evidenceSource),
            new("dbo", "LmaxTradeSummaries", "summary", ["Id", "ReportDate", "SecurityId", "LmaxSymbol", "GrossQuantity", "NetQuantity"], ["ReportDate"], ["SecurityId", "LmaxSymbol"], [], [], ["GrossQuantity", "NetQuantity"], ["Id"], [], null, null, null, rowCountsAvailable ? "PRESENT" : "PARTIAL", evidenceSource)
        ];

    private static async Task<IReadOnlyDictionary<string, object>> InspectSourceArtifactsAsync(MarketDataDbPersistencePreflightOptions options, CancellationToken cancellationToken)
    {
        var sourceRoot = Path.GetFullPath(options.SourceRunRoot);
        var freezeRoot = Path.GetFullPath(options.FreezeRoot);
        var summaryPath = Path.Combine(sourceRoot, "marketdata", "lmax_marketdata_summary.json");
        var eventsPath = Path.Combine(sourceRoot, "marketdata", "lmax_marketdata_events.jsonl");
        var freezeReportPath = Path.Combine(freezeRoot, "10_validation", "lmax_bounded_loop_demo_freeze_report.json");
        var freezeEvidencePath = Path.Combine(freezeRoot, "10_validation", "lmax_bounded_loop_demo_evidence_matrix.json");
        var freezeBoundaryPath = Path.Combine(freezeRoot, "10_validation", "lmax_bounded_loop_demo_boundary_verification.json");

        var events = new List<Dictionary<string, object?>>();
        if (File.Exists(eventsPath))
        {
            foreach (var line in await File.ReadAllLinesAsync(eventsPath, cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                using var doc = JsonDocument.Parse(line);
                events.Add(doc.RootElement.EnumerateObject().ToDictionary(
                    property => property.Name,
                    property => JsonValue(property.Value),
                    StringComparer.Ordinal));
            }
        }

        using var summaryDoc = File.Exists(summaryPath)
            ? JsonDocument.Parse(await File.ReadAllTextAsync(summaryPath, cancellationToken))
            : null;
        using var freezeDoc = File.Exists(freezeReportPath)
            ? JsonDocument.Parse(await File.ReadAllTextAsync(freezeReportPath, cancellationToken))
            : null;
        using var boundaryDoc = File.Exists(freezeBoundaryPath)
            ? JsonDocument.Parse(await File.ReadAllTextAsync(freezeBoundaryPath, cancellationToken))
            : null;

        var classifications = events.Select(GetEventClassification).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var instruments = events.Select(row => GetNullableString(row, "instrument")).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        var snapshots = CountClassification(events, "Snapshot");
        var incrementals = CountClassification(events, "Incremental");
        var marketDataObserved = snapshots > 0 || incrementals > 0 ||
                                 string.Equals(GetString(summaryDoc?.RootElement, "final_fix_session_state"), "MarketDataObserved", StringComparison.Ordinal);
        var schemaPass = File.Exists(eventsPath) && events.Count > 0 && events.All(EventHasMinimumFields) && marketDataObserved;

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["sourceRunRoot"] = sourceRoot,
            ["freezeRoot"] = freezeRoot,
            ["sourceSummaryPath"] = summaryPath,
            ["sourceEventsPath"] = eventsPath,
            ["freezeReportPath"] = freezeReportPath,
            ["freezeEvidencePath"] = freezeEvidencePath,
            ["freezeBoundaryPath"] = freezeBoundaryPath,
            ["sourceSummaryExists"] = File.Exists(summaryPath),
            ["sourceEventsExists"] = File.Exists(eventsPath),
            ["freezeReportExists"] = File.Exists(freezeReportPath),
            ["freezeEvidenceExists"] = File.Exists(freezeEvidencePath),
            ["freezeBoundaryExists"] = File.Exists(freezeBoundaryPath),
            ["LMAX_SOURCE_EVENTS_PRESENT"] = File.Exists(eventsPath) && events.Count > 0 ? "YES" : "NO",
            ["LMAX_MARKETDATA_OBSERVED"] = marketDataObserved ? "YES" : "NO",
            ["LMAX_EVENTS_COUNT"] = events.Count,
            ["LMAX_SNAPSHOTS_COUNT"] = snapshots,
            ["LMAX_INCREMENTALS_COUNT"] = incrementals,
            ["LMAX_EVENT_SCHEMA_STATUS"] = schemaPass ? "PASS" : events.Count > 0 ? "WARN" : "FAIL",
            ["observedInstruments"] = instruments,
            ["observedClassifications"] = classifications,
            ["observedSecurityIds"] = events.Select(row => GetNullableString(row, "security_id")).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            ["observedSecurityIdSources"] = events.Select(row => GetNullableString(row, "security_id_source")).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            ["observedFixMsgTypes"] = events.Select(row => GetNullableString(row, "fix_msg_type")).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            ["hasBidAsk"] = events.Any(row => GetNullableString(row, "bid") is not null && GetNullableString(row, "ask") is not null),
            ["hasRawRedactedFix"] = events.Any(row => GetNullableString(row, "raw_redacted_fix") is not null),
            ["freezeStatus"] = GetString(freezeDoc?.RootElement, "LMAX_BOUNDED_LOOP_DEMO_FREEZE_STATUS"),
            ["freezeLoopStatus"] = GetString(freezeDoc?.RootElement, "FIRST_STEP_FETCH_LIVE_MARKET_DATA_LOOP_STATUS"),
            ["freezePrimaryFailureReason"] = GetString(freezeDoc?.RootElement, "PRIMARY_FAILURE_REASON"),
            ["freezeBoundaryStatus"] = GetString(boundaryDoc?.RootElement, "BOUNDARY_STATUS")
        };
    }

    private static IReadOnlyDictionary<string, object> BuildSchemaInventory(MarketDataDbPersistencePreflightOptions options, MarketDataDbInventoryResult db)
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["connectionStringEnvVarName"] = db.ConnectionStringEnvVarName,
            ["connectionStringPresent"] = db.ConnectionStringPresent,
            ["credentialValuesRedacted"] = true,
            ["DB_CONNECTION_STATUS"] = db.ConnectionStatus,
            ["DB_SCHEMA_INVENTORY_STATUS"] = db.SchemaInventoryStatus,
            ["DB_ROW_COUNTS_STATUS"] = db.RowCountsStatus,
            ["readOnlyQueriesOnly"] = true,
            ["candidateTables"] = db.Tables.Select(TableDictionary).ToArray(),
            ["findings"] = db.Findings
        };

    private static IReadOnlyDictionary<string, object> BuildEventMappingReport(
        MarketDataDbPersistencePreflightOptions options,
        IReadOnlyDictionary<string, object> source,
        MarketDataDbInventoryResult db)
    {
        var rows = db.Tables.Select(table => BuildMappingRow(table)).ToArray();
        var snapshotCandidate = rows.FirstOrDefault(row => string.Equals(row["tableName"].ToString(), "MarketDataSnapshots", StringComparison.OrdinalIgnoreCase));
        var mappingStatus = GetString(source, "LMAX_EVENT_SCHEMA_STATUS") == "FAIL"
            ? "FAIL"
            : db.ConnectionStatus == "PRESENT" && snapshotCandidate is not null && snapshotCandidate["can_store_quote"].ToString() == "YES"
                ? "PASS"
                : "WARN";

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["LMAX_EVENT_TO_DB_MAPPING_STATUS"] = mappingStatus,
            ["LMAX_SOURCE_EVENTS_PRESENT"] = GetString(source, "LMAX_SOURCE_EVENTS_PRESENT") ?? "NO",
            ["LMAX_MARKETDATA_OBSERVED"] = GetString(source, "LMAX_MARKETDATA_OBSERVED") ?? "NO",
            ["LMAX_EVENTS_COUNT"] = GetInt(source, "LMAX_EVENTS_COUNT"),
            ["LMAX_SNAPSHOTS_COUNT"] = GetInt(source, "LMAX_SNAPSHOTS_COUNT"),
            ["LMAX_INCREMENTALS_COUNT"] = GetInt(source, "LMAX_INCREMENTALS_COUNT"),
            ["observedFields"] = new[] { "received_at_utc", "instrument", "security_id", "security_id_source", "bid", "ask", "bid_size", "ask_size", "fix_msg_type", "classification", "raw_redacted_fix", "source" },
            ["tableMappings"] = rows,
            ["targetStorageTableRecommendation"] = mappingStatus == "PASS" ? "MarketDataSnapshots" : null!,
            ["targetStorageTableConfidence"] = mappingStatus == "PASS" ? "MEDIUM" : "NONE",
            ["recommendationNotes"] = mappingStatus == "PASS"
                ? "MarketDataSnapshots appears to support normalized bid/ask quote snapshots. Raw FIX and lineage policy still require a future write gate."
                : "No canonical target is selected in this preflight because live DB row counts/schema or write policy evidence is incomplete."
        };
    }

    private static IReadOnlyDictionary<string, object> BuildRowCountsReport(MarketDataDbPersistencePreflightOptions options, MarketDataDbInventoryResult db)
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["connectionStringEnvVarName"] = db.ConnectionStringEnvVarName,
            ["connectionStringPresent"] = db.ConnectionStringPresent,
            ["credentialValuesRedacted"] = true,
            ["DB_CONNECTION_STATUS"] = db.ConnectionStatus,
            ["DB_ROW_COUNTS_STATUS"] = db.RowCountsStatus,
            ["rowCounts"] = db.Tables.Select(table => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["schemaName"] = table.SchemaName,
                ["tableName"] = table.TableName,
                ["storageType"] = table.StorageType,
                ["rowCount"] = table.RowCount,
                ["rowCountQueryRedacted"] = table.RowCount is null ? null : $"SELECT COUNT_BIG(*) FROM [{table.SchemaName}].[{table.TableName}]",
                ["minTimestampUtc"] = table.MinTimestampUtc,
                ["maxTimestampUtc"] = table.MaxTimestampUtc,
                ["evidenceStatus"] = table.EvidenceStatus
            }).ToArray(),
            ["readOnlyQueries"] = db.ReadOnlyQueries
        };

    private static IReadOnlyDictionary<string, object> BuildBlockersReport(
        MarketDataDbPersistencePreflightOptions options,
        IReadOnlyDictionary<string, object> source,
        MarketDataDbInventoryResult db,
        IReadOnlyDictionary<string, object> mapping)
    {
        var blockers = new List<Dictionary<string, object>>();
        if (db.ConnectionStatus == "MISSING")
        {
            blockers.Add(Blocker("DB_CONNECTION_STRING_MISSING", "WARN", $"Env var {db.ConnectionStringEnvVarName} is not present.", "Provide the local connection string via the approved environment only.", "CLOSE_DB_ROW_COUNT_EVIDENCE"));
            blockers.Add(Blocker("ROW_COUNTS_UNAVAILABLE", "WARN", "Row counts are unavailable without a DB connection.", "Run this same preflight with the connection string env var present.", "CLOSE_DB_ROW_COUNT_EVIDENCE"));
        }

        if (GetString(mapping, "targetStorageTableRecommendation") is null)
        {
            blockers.Add(Blocker("TARGET_TABLE_NOT_SELECTED", "BLOCKER", "No definitive target storage table is selected by this read-only preflight.", "Select a target table in a future write-gate design after schema/count evidence is complete.", "DESIGN_PERSISTENCE_WRITE_GATE"));
        }

        if (GetString(source, "LMAX_SOURCE_EVENTS_PRESENT") != "YES")
        {
            blockers.Add(Blocker("SOURCE_EVENTS_MISSING", "BLOCKER", "The LMAX source events artifact is missing or empty.", "Restore the bounded loop source artifacts before designing persistence.", "STATUS_ONLY"));
        }

        blockers.Add(Blocker("WRITE_GATE_NOT_APPROVED", "BLOCKER", "No DB write gate has been approved for this increment.", "Create and approve a separate persistence write gate before any INSERT/UPDATE path.", "DESIGN_PERSISTENCE_WRITE_GATE"));
        blockers.Add(Blocker("IDEMPOTENCY_KEY_UNDEFINED", "BLOCKER", "Idempotency key for quote/snapshot persistence is not defined.", "Define run/instrument/timestamp/security lineage keys before writing.", "DESIGN_PERSISTENCE_WRITE_GATE"));
        blockers.Add(Blocker("LINEAGE_HASH_STRATEGY_UNDEFINED", "WARN", "Lineage/hash strategy for persisted market data is not finalized.", "Specify source event hash and storage lineage columns or sidecar policy.", "DESIGN_PERSISTENCE_WRITE_GATE"));
        blockers.Add(Blocker("RAW_FIX_STORAGE_POLICY_UNDEFINED", "WARN", "Raw redacted FIX storage is not selected or explicitly excluded.", "Decide whether raw redacted FIX is persisted, sidecar-only, or discarded.", "DESIGN_PERSISTENCE_WRITE_GATE"));
        blockers.Add(Blocker("RETENTION_POLICY_UNDEFINED", "WARN", "Retention and replay policy are not defined.", "Define retention, replay, and deduplication policy before writes.", "DESIGN_PERSISTENCE_WRITE_GATE"));
        blockers.Add(Blocker("MARKETDATA_LMAX_DB_ADOPTED_WITH_WARNINGS", "WARN", "MarketData-LMAX-DB remains AdoptedWithWarnings.", "Keep status unchanged until DB evidence/write gate is complete.", "STATUS_ONLY"));

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["blockerCount"] = blockers.Count,
            ["blockers"] = blockers
        };
    }

    private static IReadOnlyDictionary<string, object> BuildBoundaryReport(MarketDataDbPersistencePreflightOptions options)
        => new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["DB_WRITE_ATTEMPTED"] = "NO",
            ["DB_MUTATION_ATTEMPTED"] = "NO",
            ["MIGRATION_ATTEMPTED"] = "NO",
            ["ENSURE_CREATED_CALLED"] = "NO",
            ["MIGRATE_CALLED"] = "NO",
            ["INSERT_UPDATE_DELETE_MERGE_TRUNCATE_DROP_ALTER_USED"] = "NO",
            ["LMAX_EXTERNAL_CALLS_ATTEMPTED"] = "NO",
            ["PRODUCTION_ENDPOINT_USED"] = "NO",
            ["TRADING_ENDPOINT_USED"] = "NO",
            ["ORDER_MESSAGES_SENT"] = "NO",
            ["FILL_ARTIFACTS_CREATED"] = "NO",
            ["ROUTE_BROKER_LIVE_STATE_ARTIFACTS_CREATED"] = "NO",
            ["QUBES_EXECUTED"] = "NO",
            ["PMS_OMS_EMS_TOUCHED"] = "NO",
            ["A_H_I_CREATED"] = "NO",
            ["CREDENTIAL_VALUES_PERSISTED"] = "NO",
            ["BOUNDARY_STATUS"] = options.NoExternal && options.NoExecution && options.NoDbWrite ? "PASS" : "FAIL"
        };

    private static IReadOnlyDictionary<string, object> BuildPreflightReport(
        MarketDataDbPersistencePreflightOptions options,
        IReadOnlyDictionary<string, object> source,
        MarketDataDbInventoryResult db,
        IReadOnlyDictionary<string, object> mapping,
        IReadOnlyDictionary<string, object> blockers,
        IReadOnlyDictionary<string, object> boundary)
    {
        var sourceOk = GetString(source, "LMAX_SOURCE_EVENTS_PRESENT") == "YES" &&
                       GetString(source, "LMAX_MARKETDATA_OBSERVED") == "YES";
        var boundaryOk = GetString(boundary, "BOUNDARY_STATUS") == "PASS";
        var mappingStatus = GetString(mapping, "LMAX_EVENT_TO_DB_MAPPING_STATUS") ?? "FAIL";
        var preflightStatus = !sourceOk || !boundaryOk || mappingStatus == "FAIL"
            ? "FAIL"
            : db.ConnectionStatus == "PRESENT" &&
              db.SchemaInventoryStatus == "PRESENT" &&
              db.RowCountsStatus == "PRESENT" &&
              mappingStatus == "PASS" &&
              GetString(mapping, "targetStorageTableRecommendation") is not null
                ? "PASS"
                : "WARN";

        var isRowCountEvidenceGate = IsRowCountEvidenceGate(options.RunKey);
        var rowCountEvidenceStatus = !sourceOk || !boundaryOk || mappingStatus == "FAIL"
            ? "FAIL"
            : db.ConnectionStatus == "MISSING"
                ? "NOT_ATTEMPTED"
                : db.ConnectionStatus == "FAILED"
                    ? "FAIL"
                    : db.SchemaInventoryStatus == "PRESENT" &&
                      db.RowCountsStatus == "PRESENT" &&
                      mappingStatus is "PASS" or "WARN"
                        ? "PASS"
                        : "WARN";

        var safeNextPhase = isRowCountEvidenceGate
            ? rowCountEvidenceStatus switch
            {
                "FAIL" => "BLOCKED",
                "NOT_ATTEMPTED" => "CLOSE_DB_CONNECTION_SECRET_ACCESS",
                _ => "DESIGN_PERSISTENCE_WRITE_GATE"
            }
            : preflightStatus == "FAIL"
            ? "BLOCKED"
            : db.RowCountsStatus == "MISSING_CONNECTION_STRING"
                ? "CLOSE_DB_ROW_COUNT_EVIDENCE"
                : "DESIGN_PERSISTENCE_WRITE_GATE";

        var report = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["createdAtUtc"] = DateTimeOffset.UtcNow,
            ["MARKETDATA_DB_PERSISTENCE_PREFLIGHT_STATUS"] = preflightStatus,
            ["DB_CONNECTION_STATUS"] = db.ConnectionStatus,
            ["DB_SCHEMA_INVENTORY_STATUS"] = db.SchemaInventoryStatus,
            ["DB_ROW_COUNTS_STATUS"] = db.RowCountsStatus,
            ["LMAX_EVENT_TO_DB_MAPPING_STATUS"] = mappingStatus,
            ["TARGET_STORAGE_TABLE_RECOMMENDATION"] = GetString(mapping, "targetStorageTableRecommendation"),
            ["TARGET_STORAGE_TABLE_CONFIDENCE"] = GetString(mapping, "targetStorageTableConfidence") ?? "NONE",
            ["DB_WRITE_GATE_STATUS"] = "BLOCKED",
            ["DB_MUTATION_ATTEMPTED"] = "NO",
            ["MARKETDATA_LMAX_DB_STATUS"] = "AdoptedWithWarnings",
            ["PRODUCTION_STATUS"] = "BLOCKED",
            ["EXECUTION_STATUS"] = "BLOCKED",
            ["SAFE_NEXT_PHASE"] = safeNextPhase,
            ["LMAX_SOURCE_EVENTS_PRESENT"] = GetString(source, "LMAX_SOURCE_EVENTS_PRESENT") ?? "NO",
            ["LMAX_MARKETDATA_OBSERVED"] = GetString(source, "LMAX_MARKETDATA_OBSERVED") ?? "NO",
            ["LMAX_EVENTS_COUNT"] = GetInt(source, "LMAX_EVENTS_COUNT"),
            ["LMAX_SNAPSHOTS_COUNT"] = GetInt(source, "LMAX_SNAPSHOTS_COUNT"),
            ["LMAX_INCREMENTALS_COUNT"] = GetInt(source, "LMAX_INCREMENTALS_COUNT"),
            ["LMAX_EVENT_SCHEMA_STATUS"] = GetString(source, "LMAX_EVENT_SCHEMA_STATUS") ?? "FAIL",
            ["blockerCount"] = blockers.TryGetValue("blockerCount", out var count) ? count : 0,
            ["connectionStringEnvVarName"] = options.ConnectionStringEnvVarName,
            ["connectionStringPresent"] = options.ConnectionStringPresent,
            ["credentialValuesRedacted"] = true
        };

        if (isRowCountEvidenceGate)
        {
            report["MARKETDATA_DB_ROW_COUNT_EVIDENCE_STATUS"] = rowCountEvidenceStatus;
        }

        return report;
    }

    private static Dictionary<string, object> BuildMappingRow(MarketDataDbTableInventory table)
    {
        var hasBidAsk = table.BidAskColumns.Any(column => column.Contains("Bid", StringComparison.OrdinalIgnoreCase)) &&
                        table.BidAskColumns.Any(column => column.Contains("Ask", StringComparison.OrdinalIgnoreCase));
        var hasTimestamp = table.TimestampColumns.Count > 0;
        var hasInstrument = table.InstrumentColumns.Count > 0;
        var rawFix = table.Columns.Any(column => column.Contains("Raw", StringComparison.OrdinalIgnoreCase) || column.Contains("Fix", StringComparison.OrdinalIgnoreCase));
        var lineage = table.Columns.Any(column => column.Contains("Source", StringComparison.OrdinalIgnoreCase) || column.Contains("Hash", StringComparison.OrdinalIgnoreCase) || column.Contains("Lineage", StringComparison.OrdinalIgnoreCase));

        var canStoreSnapshot = table.StorageType is "quote" or "snapshot" && hasBidAsk && hasTimestamp && hasInstrument
            ? "YES"
            : hasBidAsk || hasTimestamp ? "PARTIAL" : "NO";
        var canStoreQuote = table.StorageType is "quote" or "snapshot" && hasBidAsk && hasTimestamp
            ? "YES"
            : hasBidAsk ? "PARTIAL" : "NO";

        string recommendedUsage = table.TableName switch
        {
            "MarketDataSnapshots" => "Candidate for normalized bid/ask quote snapshots; raw FIX and lineage policy need future write-gate design.",
            "MarketDataBars" => "OHLC/bar storage only; do not use for raw LMAX quote snapshots.",
            "LmaxIndividualTrades" => "Trade/tick execution evidence table shape; do not use for bid/ask quotes without explicit schema support.",
            "LmaxTradeSummaries" => "Trade summary table shape; not suitable for quote snapshots.",
            _ => "Candidate requires manual schema review."
        };

        var missing = new List<string>();
        if (!hasTimestamp) missing.Add("timestamp");
        if (!hasInstrument) missing.Add("instrument/security");
        if (!hasBidAsk) missing.Add("bid/ask");
        if (!rawFix) missing.Add("raw_redacted_fix");
        if (!lineage) missing.Add("lineage/hash");

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["schemaName"] = table.SchemaName,
            ["tableName"] = table.TableName,
            ["storageType"] = table.StorageType,
            ["can_store_snapshot"] = canStoreSnapshot,
            ["can_store_quote"] = canStoreQuote,
            ["can_store_raw_fix"] = rawFix ? "YES" : "NO",
            ["can_store_lineage"] = lineage ? "PARTIAL" : "NO",
            ["missing_columns"] = missing,
            ["incompatible_columns"] = table.StorageType is "bar" or "summary" or "tick" && !hasBidAsk ? new[] { "not a bid/ask quote snapshot table" } : Array.Empty<string>(),
            ["recommended_usage"] = recommendedUsage,
            ["risk"] = canStoreQuote == "YES" ? "MEDIUM: requires idempotency and lineage policy before writes." : "HIGH: not sufficient for LMAX quote persistence."
        };
    }

    private static Dictionary<string, object?> TableDictionary(MarketDataDbTableInventory table)
        => new(StringComparer.Ordinal)
        {
            ["schemaName"] = table.SchemaName,
            ["tableName"] = table.TableName,
            ["storageType"] = table.StorageType,
            ["columns"] = table.Columns,
            ["timestampColumns"] = table.TimestampColumns,
            ["instrumentSecurityColumns"] = table.InstrumentColumns,
            ["bidAskColumns"] = table.BidAskColumns,
            ["OHLCColumns"] = table.OhlcColumns,
            ["volumeColumns"] = table.VolumeColumns,
            ["primaryKeyColumns"] = table.PrimaryKeyColumns,
            ["nullableColumns"] = table.NullableColumns,
            ["rowCount"] = table.RowCount,
            ["rowCountQueryRedacted"] = table.RowCount is null ? null : $"SELECT COUNT_BIG(*) FROM [{table.SchemaName}].[{table.TableName}]",
            ["minTimestampUtc"] = table.MinTimestampUtc,
            ["maxTimestampUtc"] = table.MaxTimestampUtc,
            ["evidenceStatus"] = table.EvidenceStatus,
            ["evidenceSource"] = table.EvidenceSource
        };

    private static Dictionary<string, object> Blocker(string blockerId, string severity, string description, string requiredResolution, string safeNextAction)
        => new(StringComparer.Ordinal)
        {
            ["blockerId"] = blockerId,
            ["severity"] = severity,
            ["description"] = description,
            ["requiredResolution"] = requiredResolution,
            ["safeNextAction"] = safeNextAction
        };

    private static bool EventHasMinimumFields(IReadOnlyDictionary<string, object?> row)
    {
        var classification = GetEventClassification(row);
        if (classification is "TerminalTimeout" or "CleanClose")
        {
            return GetNullableString(row, "received_at_utc") is not null && GetNullableString(row, "classification") is not null;
        }

        return GetNullableString(row, "received_at_utc") is not null &&
               GetNullableString(row, "fix_msg_type") is not null &&
               GetNullableString(row, "classification") is not null;
    }

    private static int CountClassification(IReadOnlyList<Dictionary<string, object?>> events, string classification)
        => events.Count(row => string.Equals(GetEventClassification(row), classification, StringComparison.OrdinalIgnoreCase));

    private static string? GetEventClassification(IReadOnlyDictionary<string, object?> row)
        => GetNullableString(row, "classification");

    private static object? JsonValue(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };

    private static string? GetString(IReadOnlyDictionary<string, object> dictionary, string property)
        => dictionary.TryGetValue(property, out var value) ? value?.ToString() : null;

    private static string? GetNullableString(IReadOnlyDictionary<string, object?> dictionary, string property)
        => dictionary.TryGetValue(property, out var value) ? value?.ToString() : null;

    private static int GetInt(IReadOnlyDictionary<string, object> dictionary, string property)
        => dictionary.TryGetValue(property, out var value) && int.TryParse(value?.ToString(), out var result) ? result : 0;

    private static string? GetString(JsonElement? element, string property)
        => element is { } value &&
           value.ValueKind == JsonValueKind.Object &&
           value.TryGetProperty(property, out var propertyValue)
            ? propertyValue.ValueKind == JsonValueKind.String ? propertyValue.GetString() : propertyValue.ToString()
            : null;

    private static async Task WriteJsonAndMarkdownAsync<T>(string root, string basename, T json, string markdown, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.json"), JsonSerializer.Serialize(json, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.md"), markdown, cancellationToken);
    }

    private static async Task WriteManifestAsync(
        string outputRoot,
        string runKey,
        IReadOnlyDictionary<string, object> preflightReport,
        CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) is not "hashes.json" and not "manifest.sha256")
            .OrderBy(path => Path.GetRelativePath(outputRoot, path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hashes = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            await using var stream = File.OpenRead(file);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            hashes[Path.GetRelativePath(outputRoot, file).Replace('\\', '/')] = Convert.ToHexString(hash).ToLowerInvariant();
        }

        var manifest = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["run_key"] = runKey,
            ["created_at_utc"] = DateTimeOffset.UtcNow,
            ["package_type"] = IsRowCountEvidenceGate(runKey) ? "marketdata_db_row_count_evidence_status_only" : "marketdata_db_persistence_preflight_status_only",
            ["preflight_status"] = preflightReport["MARKETDATA_DB_PERSISTENCE_PREFLIGHT_STATUS"],
            ["row_count_evidence_status"] = preflightReport.TryGetValue("MARKETDATA_DB_ROW_COUNT_EVIDENCE_STATUS", out var rowCountEvidenceStatus) ? rowCountEvidenceStatus : null!,
            ["marketdata_lmax_db_status"] = "AdoptedWithWarnings",
            ["db_write_gate_status"] = "BLOCKED",
            ["safe_next_phase"] = preflightReport["SAFE_NEXT_PHASE"],
            ["files"] = hashes.Keys.ToArray()
        };

        var manifestPath = Path.Combine(outputRoot, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);
        await using var manifestStream = File.OpenRead(manifestPath);
        var manifestHash = Convert.ToHexString(await SHA256.HashDataAsync(manifestStream, cancellationToken)).ToLowerInvariant();
        hashes["manifest.json"] = manifestHash;
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "hashes.json"), JsonSerializer.Serialize(hashes, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "manifest.sha256"), $"{manifestHash}  manifest.json{Environment.NewLine}", Encoding.ASCII, cancellationToken);
    }

    private static bool IsRowCountEvidenceGate(string runKey)
        => runKey.Contains("row-count-evidence", StringComparison.OrdinalIgnoreCase);

    private static class Markdown
    {
        public static string Preflight(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# MarketData DB Persistence Preflight R001",
                "",
                $"- MARKETDATA_DB_PERSISTENCE_PREFLIGHT_STATUS = `{report["MARKETDATA_DB_PERSISTENCE_PREFLIGHT_STATUS"]}`",
                $"- DB_CONNECTION_STATUS = `{report["DB_CONNECTION_STATUS"]}`",
                $"- DB_SCHEMA_INVENTORY_STATUS = `{report["DB_SCHEMA_INVENTORY_STATUS"]}`",
                $"- DB_ROW_COUNTS_STATUS = `{report["DB_ROW_COUNTS_STATUS"]}`",
                $"- LMAX_EVENT_TO_DB_MAPPING_STATUS = `{report["LMAX_EVENT_TO_DB_MAPPING_STATUS"]}`",
                $"- TARGET_STORAGE_TABLE_RECOMMENDATION = `{report["TARGET_STORAGE_TABLE_RECOMMENDATION"] ?? "null"}`",
                $"- TARGET_STORAGE_TABLE_CONFIDENCE = `{report["TARGET_STORAGE_TABLE_CONFIDENCE"]}`",
                $"- DB_WRITE_GATE_STATUS = `{report["DB_WRITE_GATE_STATUS"]}`",
                $"- MARKETDATA_LMAX_DB_STATUS = `{report["MARKETDATA_LMAX_DB_STATUS"]}`",
                $"- SAFE_NEXT_PHASE = `{report["SAFE_NEXT_PHASE"]}`",
                "",
                "This is a read-only/status-only preflight. It does not rerun LMAX, write DB data, migrate schema, or enable production/live execution.");

        public static string Schema(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# MarketData DB Schema Inventory",
                "",
                $"- DB_CONNECTION_STATUS = `{report["DB_CONNECTION_STATUS"]}`",
                $"- DB_SCHEMA_INVENTORY_STATUS = `{report["DB_SCHEMA_INVENTORY_STATUS"]}`",
                $"- DB_ROW_COUNTS_STATUS = `{report["DB_ROW_COUNTS_STATUS"]}`",
                "- Candidate tables are listed in the JSON artifact.");

        public static string Mapping(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# LMAX Event To DB Mapping",
                "",
                $"- LMAX_EVENT_TO_DB_MAPPING_STATUS = `{report["LMAX_EVENT_TO_DB_MAPPING_STATUS"]}`",
                $"- LMAX_SOURCE_EVENTS_PRESENT = `{report["LMAX_SOURCE_EVENTS_PRESENT"]}`",
                $"- LMAX_MARKETDATA_OBSERVED = `{report["LMAX_MARKETDATA_OBSERVED"]}`",
                $"- TARGET_STORAGE_TABLE_RECOMMENDATION = `{report["targetStorageTableRecommendation"] ?? "null"}`");

        public static string RowCounts(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# MarketData DB Row Counts",
                "",
                $"- DB_CONNECTION_STATUS = `{report["DB_CONNECTION_STATUS"]}`",
                $"- DB_ROW_COUNTS_STATUS = `{report["DB_ROW_COUNTS_STATUS"]}`",
                $"- connectionStringEnvVarName = `{report["connectionStringEnvVarName"]}`",
                $"- credentialValuesRedacted = `{report["credentialValuesRedacted"]}`");

        public static string Blockers(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# MarketData DB Persistence Blockers",
                "",
                $"- blockerCount = `{report["blockerCount"]}`",
                "- See JSON for blocker severity, required resolution, and safe next action.");

        public static string Boundary(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# MarketData DB No Mutation Boundary",
                "",
                $"- BOUNDARY_STATUS = `{report["BOUNDARY_STATUS"]}`",
                $"- DB_WRITE_ATTEMPTED = `{report["DB_WRITE_ATTEMPTED"]}`",
                $"- DB_MUTATION_ATTEMPTED = `{report["DB_MUTATION_ATTEMPTED"]}`",
                $"- MIGRATION_ATTEMPTED = `{report["MIGRATION_ATTEMPTED"]}`",
                $"- LMAX_EXTERNAL_CALLS_ATTEMPTED = `{report["LMAX_EXTERNAL_CALLS_ATTEMPTED"]}`",
                $"- PRODUCTION_ENDPOINT_USED = `{report["PRODUCTION_ENDPOINT_USED"]}`",
                $"- TRADING_ENDPOINT_USED = `{report["TRADING_ENDPOINT_USED"]}`",
                $"- CREDENTIAL_VALUES_PERSISTED = `{report["CREDENTIAL_VALUES_PERSISTED"]}`");

        public static string Summary(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# MarketData DB Persistence Preflight Summary",
                "",
                $"- Preflight status: `{report["MARKETDATA_DB_PERSISTENCE_PREFLIGHT_STATUS"]}`",
                $"- DB connection: `{report["DB_CONNECTION_STATUS"]}`",
                $"- DB schema inventory: `{report["DB_SCHEMA_INVENTORY_STATUS"]}`",
                $"- DB row counts: `{report["DB_ROW_COUNTS_STATUS"]}`",
                $"- LMAX event mapping: `{report["LMAX_EVENT_TO_DB_MAPPING_STATUS"]}`",
                $"- Target storage recommendation: `{report["TARGET_STORAGE_TABLE_RECOMMENDATION"] ?? "null"}`",
                $"- DB write gate: `{report["DB_WRITE_GATE_STATUS"]}`",
                $"- MarketData-LMAX-DB: `{report["MARKETDATA_LMAX_DB_STATUS"]}`",
                $"- Safe next phase: `{report["SAFE_NEXT_PHASE"]}`");

        private static string Lines(params string[] lines)
            => string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
