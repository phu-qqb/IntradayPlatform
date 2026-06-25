using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using QQ.Production.Intraday.Application.R018ImportPlanning;
using QQ.Production.Intraday.Application.R018StageLocalDbReplay;

return await R018StageLocalDbReplayCli.ExecuteAsync(args, Console.Out, Console.Error, CancellationToken.None);

internal static class R018StageLocalDbReplayCli
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    static R018StageLocalDbReplayCli()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static async Task<int> ExecuteAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var options = CliOptions.Parse(args);
        if (options.ShowHelp)
        {
            output.WriteLine(CliOptions.HelpText);
            return 0;
        }

        foreach (var rejection in options.Rejections)
        {
            error.WriteLine(rejection);
        }

        if (options.Rejections.Count > 0)
        {
            return 2;
        }

        var now = DateTimeOffset.UtcNow;
        var outputDir = Path.GetFullPath(options.OutputDir ?? Path.Combine("artifacts", "readiness", "anubis-m1d-stage-only-local-isolated-db-replay", "run"));
        Directory.CreateDirectory(outputDir);

        var loader = new R018StageLocalDbReplayLoader();
        var parity = loader.RecalculateParity(options.PlanDir!, now);
        R018StageReportWriter.WriteJson(Path.Combine(outputDir, "m1d_input_parity_report.json"), parity);
        R018StageReportWriter.WriteParityCsv(Path.Combine(outputDir, "m1d_input_parity_report.csv"), parity.Rows);

        R018StageInputBundle? bundle = null;
        R018StageEntryGateReport gate;
        try
        {
            bundle = loader.Load(options.PlanDir!);
            gate = R018StageOnlyEntryGate.Evaluate(bundle, parity, now);
        }
        catch (Exception ex)
        {
            gate = new R018StageEntryGateReport(
                R018StageLocalDbReplayConstants.ReplaySchemaVersion,
                now,
                "NO_GO_M1D_STAGE_ONLY_LOCAL_ISOLATED_DB_REPLAY",
                false,
                parity.Rows.Concat([new R018StageInputParityRow("bundle_load", options.PlanDir!, "load ok", ex.GetType().Name + ":" + ex.Message, "FAIL", "CRITICAL", "M1D input bundle must load before DB import.")]).ToArray());
        }

        var connectionBuilder = new SqlConnectionStringBuilder(options.Connection!)
        {
            TrustServerCertificate = true
        };
        var connectionGate = R018StageLocalConnectionPolicy.Evaluate(connectionBuilder.DataSource, options.DatabaseName!, options.CreateDisposableDb, options.DropAfterExport);
        R018StageReportWriter.WriteJson(Path.Combine(outputDir, "m1d_connection_gate_report.json"), connectionGate);

        if (!gate.CanImport || connectionGate.Status != "PASS" || bundle is null)
        {
            R018StageReportWriter.WriteGateMarkdown(Path.Combine(outputDir, "m1d_gate_report.md"), gate, connectionGate, "NO_GO_M2");
            error.WriteLine("NO_GO_M2");
            return 2;
        }

        var db = new StageSqlDatabase(connectionBuilder.ConnectionString, options.DatabaseName!);
        StageImportResult? first = null;
        StageImportResult? second = null;
        StageImportResult? recreated = null;
        IReadOnlyDictionary<string, string> firstExportHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        IReadOnlyDictionary<string, string> recreatedExportHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        DbParityReport? dbParity = null;
        TableMutationAudit? mutationAudit = null;
        RollbackProbeReport? rollbackProbe = null;

        try
        {
            await db.DropIfExistsAsync(cancellationToken);
            await db.CreateAsync(cancellationToken);
            await db.InitializeSchemaAsync(cancellationToken);
            first = await db.ImportAsync(bundle, parity, cancellationToken);
            second = await db.ImportAsync(bundle, parity, cancellationToken);
            dbParity = await db.BuildParityReportAsync(bundle, parity, first.ReplayRunId, cancellationToken);
            mutationAudit = await db.BuildTableMutationAuditAsync(cancellationToken);
            rollbackProbe = await db.RunRollbackProbeAsync(parity, first.ReplayRunId, cancellationToken);
            firstExportHashes = await db.ExportAsync(Path.Combine(outputDir, "export_first"), cancellationToken);

            await db.DropIfExistsAsync(cancellationToken);
            await db.CreateAsync(cancellationToken);
            await db.InitializeSchemaAsync(cancellationToken);
            recreated = await db.ImportAsync(bundle, parity, cancellationToken);
            recreatedExportHashes = await db.ExportAsync(Path.Combine(outputDir, "export_recreated"), cancellationToken);
        }
        catch (Exception ex)
        {
            R018StageReportWriter.WriteJson(Path.Combine(outputDir, "m1d_db_import_report.json"), new
            {
                status = "FAILED",
                error = ex.GetType().Name,
                ex.Message,
                db_apply = false,
                canonical_mutation = false
            });
            R018StageReportWriter.WriteGateMarkdown(Path.Combine(outputDir, "m1d_gate_report.md"), gate, connectionGate, "NO_GO_M2");
            error.WriteLine($"NO_GO_M2:{ex.GetType().Name}:{ex.Message}");
            return 1;
        }
        finally
        {
            if (options.DropAfterExport)
            {
                await db.DropIfExistsAsync(CancellationToken.None);
            }
        }

        var idempotence = new
        {
            status = second?.Status == "ALREADY_IMPORTED" && second.RowsInserted == 0 ? "PASS" : "FAIL",
            first_import_status = first?.Status,
            second_import_status = second?.Status,
            first_rows_inserted = first?.RowsInserted,
            second_rows_inserted = second?.RowsInserted,
            conflict_detected = false
        };
        var firstCanonicalExportHash = firstExportHashes.TryGetValue("r018stage_export.json", out var firstPayloadHash) ? firstPayloadHash : null;
        var recreatedCanonicalExportHash = recreatedExportHashes.TryGetValue("r018stage_export.json", out var recreatedPayloadHash) ? recreatedPayloadHash : null;
        var recreate = new
        {
            status = !string.IsNullOrWhiteSpace(firstCanonicalExportHash) &&
                     string.Equals(firstCanonicalExportHash, recreatedCanonicalExportHash, StringComparison.OrdinalIgnoreCase) ? "PASS" : "FAIL",
            canonical_payload = "r018stage_export.json",
            first_canonical_payload_hash = firstCanonicalExportHash,
            recreated_canonical_payload_hash = recreatedCanonicalExportHash,
            first_export_hashes = firstExportHashes,
            recreated_export_hashes = recreatedExportHashes
        };
        var finalStatus = gate.CanImport &&
                          connectionGate.Status == "PASS" &&
                          dbParity?.Status == "PASS" &&
                          mutationAudit?.CanonicalTableCount == 0 &&
                          rollbackProbe?.Status == "PASS" &&
                          (string)idempotence.status == "PASS" &&
                          (string)recreate.status == "PASS"
            ? "GO_M2_RECORDER_SHADOW_CANONICAL"
            : "NO_GO_M2";

        R018StageReportWriter.WriteJson(Path.Combine(outputDir, "m1d_db_import_report.json"), new
        {
            status = "PASS",
            schema = R018StageLocalDbReplayConstants.StageSchemaName,
            database = options.DatabaseName,
            first,
            second,
            recreated,
            db_apply = false,
            canonical_mutation = false
        });
        R018StageReportWriter.WriteJson(Path.Combine(outputDir, "m1d_db_parity_report.json"), dbParity);
        R018StageReportWriter.WriteJson(Path.Combine(outputDir, "m1d_idempotence_report.json"), idempotence);
        R018StageReportWriter.WriteJson(Path.Combine(outputDir, "m1d_recreate_determinism_report.json"), recreate);
        R018StageReportWriter.WriteJson(Path.Combine(outputDir, "m1d_table_mutation_audit.json"), mutationAudit);
        R018StageReportWriter.WriteJson(Path.Combine(outputDir, "m1d_rollback_probe_report.json"), rollbackProbe);
        R018StageReportWriter.WriteGateMarkdown(Path.Combine(outputDir, "m1d_gate_report.md"), gate, connectionGate, finalStatus);

        output.WriteLine(finalStatus);
        output.WriteLine($"output_dir={outputDir}");
        output.WriteLine("NO_LIVE_RUN_EXECUTED");
        output.WriteLine("NO_FIX_LOGON");
        output.WriteLine("NO_BROKER_TRAFFIC");
        output.WriteLine("NO_ACCOUNTAPI");
        output.WriteLine("NO_DATABENTO");
        output.WriteLine("NO_R009");
        output.WriteLine("CANONICAL_DB_APPLY_FALSE");
        return finalStatus == "GO_M2_RECORDER_SHADOW_CANONICAL" ? 0 : 1;
    }

    private static bool DictionariesEqual(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b)
        => a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var value) && string.Equals(kv.Value, value, StringComparison.Ordinal));

    internal sealed record CliOptions(
        string? PlanDir,
        string? Connection,
        string? DatabaseName,
        string? OutputDir,
        bool CreateDisposableDb,
        bool DropAfterExport,
        bool NoNetwork,
        bool StageOnly,
        bool ShowHelp,
        IReadOnlyList<string> Rejections)
    {
        public const string HelpText = """
            Usage:
              replay-r018-stage-local-db --plan-dir <path> --connection <local-sql-connection> --database-name QQIntraday_M1D_StageOnly_<GUID> --create-disposable-db --drop-after-export --no-network --stage-only [--output-dir <path>]
            """;

        private static readonly HashSet<string> ForbiddenFlags = new(StringComparer.OrdinalIgnoreCase)
        {
            "--apply-canonical",
            "--live",
            "--broker",
            "--fill-ledger",
            "--r009",
            "--accountapi",
            "--databento",
            "--fix-logon",
            "--order-send"
        };

        public static CliOptions Parse(string[] args)
        {
            string? planDir = null;
            string? connection = null;
            string? databaseName = null;
            string? outputDir = null;
            var createDisposableDb = false;
            var dropAfterExport = false;
            var noNetwork = false;
            var stageOnly = false;
            var showHelp = false;
            var rejections = new List<string>();

            for (var index = 0; index < args.Length; index++)
            {
                var current = args[index];
                if (current is "--help" or "-h")
                {
                    showHelp = true;
                    continue;
                }

                if (ForbiddenFlags.Contains(current))
                {
                    rejections.Add($"FORBIDDEN_FLAG:{current}");
                    continue;
                }

                switch (current)
                {
                    case "--plan-dir":
                        planDir = Value(args, ref index, current, rejections);
                        break;
                    case "--connection":
                    case "--connection-string":
                        connection = Value(args, ref index, current, rejections);
                        break;
                    case "--database-name":
                        databaseName = Value(args, ref index, current, rejections);
                        break;
                    case "--output-dir":
                        outputDir = Value(args, ref index, current, rejections);
                        break;
                    case "--create-disposable-db":
                        createDisposableDb = true;
                        break;
                    case "--drop-after-export":
                        dropAfterExport = true;
                        break;
                    case "--no-network":
                        noNetwork = true;
                        break;
                    case "--stage-only":
                        stageOnly = true;
                        break;
                    default:
                        rejections.Add($"UNKNOWN_FLAG:{current}");
                        break;
                }
            }

            if (!showHelp)
            {
                if (string.IsNullOrWhiteSpace(planDir)) rejections.Add("MISSING_PLAN_DIR");
                if (string.IsNullOrWhiteSpace(connection)) rejections.Add("MISSING_CONNECTION");
                if (string.IsNullOrWhiteSpace(databaseName)) rejections.Add("MISSING_DATABASE_NAME");
                if (!createDisposableDb) rejections.Add("CREATE_DISPOSABLE_DB_REQUIRED");
                if (!noNetwork) rejections.Add("NO_NETWORK_REQUIRED");
                if (!stageOnly) rejections.Add("STAGE_ONLY_REQUIRED");
            }

            return new CliOptions(planDir, connection, databaseName, outputDir, createDisposableDb, dropAfterExport, noNetwork, stageOnly, showHelp, rejections);
        }

        private static string? Value(string[] args, ref int index, string flag, List<string> rejections)
        {
            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                rejections.Add($"MISSING_VALUE:{flag}");
                return null;
            }

            index++;
            return args[index];
        }
    }
}

internal sealed class StageSqlDatabase(string baseConnectionString, string databaseName)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    static StageSqlDatabase()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    private readonly string _databaseName = R018StageLocalConnectionPolicy.IsSafeDisposableDatabaseName(databaseName)
        ? databaseName
        : throw new InvalidOperationException("UNSAFE_DATABASE_NAME");

    public async Task CreateAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(MasterConnectionString());
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, null, $"CREATE DATABASE [{_databaseName}];", cancellationToken);
    }

    public async Task DropIfExistsAsync(CancellationToken cancellationToken)
    {
        if (!R018StageLocalConnectionPolicy.IsSafeDisposableDatabaseName(_databaseName))
        {
            throw new InvalidOperationException("DROP_REFUSES_UNSAFE_DATABASE_NAME");
        }

        await using var connection = new SqlConnection(MasterConnectionString());
        await connection.OpenAsync(cancellationToken);
        var exists = Convert.ToInt32(await ScalarAsync(connection, null, "SELECT COUNT(1) FROM sys.databases WHERE name = @name;", cancellationToken, ("@name", _databaseName))) > 0;
        if (!exists)
        {
            return;
        }

        await ExecuteAsync(connection, null, $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;", cancellationToken);
        await ExecuteAsync(connection, null, $"DROP DATABASE [{_databaseName}];", cancellationToken);
    }

    public async Task InitializeSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(DatabaseConnectionString());
        await connection.OpenAsync(cancellationToken);
        foreach (var sql in SchemaStatements())
        {
            await ExecuteAsync(connection, null, sql, cancellationToken);
        }
    }

    public async Task<StageImportResult> ImportAsync(R018StageInputBundle bundle, R018StageInputParityReport parity, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(DatabaseConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var payloadJson = Serialize(bundle.Plan);
            var payloadHash = R018ArtifactBundleReader.ComputeSha256(payloadJson);
            var existing = await ExistingReplayRunAsync(connection, transaction, bundle.Plan.SchemaVersion, bundle.Plan.InputBundleHash, bundle.Plan.DeterministicContentHash, cancellationToken);
            if (existing is not null)
            {
                if (!string.Equals(existing.PayloadSha256, payloadHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("CONFLICTING_REPLAY_PAYLOAD");
                }

                var verified = await VerifyExistingRowsAsync(connection, transaction, existing.ReplayRunId, bundle, parity, cancellationToken);
                if (!verified)
                {
                    throw new InvalidOperationException("CONFLICTING_REPLAY_PAYLOAD");
                }

                await transaction.CommitAsync(cancellationToken);
                return new StageImportResult(existing.ReplayRunId, "ALREADY_IMPORTED", 0, 0, 0, 0, 0, 0, 0);
            }

            var replayRunId = Guid.NewGuid();
            var rowsInserted = 0;
            rowsInserted += await InsertReplayRunAsync(connection, transaction, replayRunId, bundle, payloadJson, payloadHash, "IMPORTING", cancellationToken);
            var inputFiles = await InsertInputFilesAsync(connection, transaction, replayRunId, bundle.InputFiles, cancellationToken);
            var normalized = await InsertPayloadRowsAsync(connection, transaction, "NormalizedEvent", replayRunId, bundle.NormalizedEvents.Select(x => new StagePayloadRow(x.StableKey, SemanticHash(x), Serialize(x))), cancellationToken);
            var evidence = await InsertPayloadRowsAsync(connection, transaction, "EvidenceOccurrence", replayRunId, bundle.EvidenceOccurrences.Select(x => new StagePayloadRow(x.OccurrenceId, x.OccurrenceId, Serialize(x))), cancellationToken);
            var business = await InsertPayloadRowsAsync(connection, transaction, "BusinessEvent", replayRunId, bundle.BusinessEvents.Select(x => new StagePayloadRow(x.BusinessEventId, x.SemanticFingerprint, Serialize(x))), cancellationToken);
            var planned = await InsertPayloadRowsAsync(connection, transaction, "PlannedStagingRow", replayRunId, bundle.PlannedStagingRows.Select(x => new StagePayloadRow(x.PlannedRowId, R018ArtifactBundleReader.ComputeSha256(Serialize(x)), Serialize(x))), cancellationToken);
            var parityRows = await InsertPayloadRowsAsync(connection, transaction, "ParityCheck", replayRunId, parity.Rows.Select(x => new StagePayloadRow(x.Check, R018ArtifactBundleReader.ComputeSha256(Serialize(x)), Serialize(x))), cancellationToken);
            rowsInserted += inputFiles + normalized + evidence + business + planned + parityRows;
            await ExecuteAsync(connection, transaction, "UPDATE r018stage.ReplayRun SET State = @state, FinalizedAtUtc = SYSUTCDATETIME() WHERE ReplayRunId = @id;", cancellationToken, ("@state", "IMPORTED"), ("@id", replayRunId));
            await transaction.CommitAsync(cancellationToken);
            return new StageImportResult(replayRunId, "IMPORTED", rowsInserted, inputFiles, normalized, evidence, business, planned, parityRows);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<DbParityReport> BuildParityReportAsync(R018StageInputBundle bundle, R018StageInputParityReport parity, Guid replayRunId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(DatabaseConnectionString());
        await connection.OpenAsync(cancellationToken);
        var counts = new SortedDictionary<string, long>(StringComparer.Ordinal)
        {
            ["ReplayRun"] = await CountReplayRunAsync(connection, replayRunId, cancellationToken),
            ["ReplayInputFile"] = await CountAsync(connection, "ReplayInputFile", replayRunId, cancellationToken),
            ["NormalizedEvent"] = await CountAsync(connection, "NormalizedEvent", replayRunId, cancellationToken),
            ["EvidenceOccurrence"] = await CountAsync(connection, "EvidenceOccurrence", replayRunId, cancellationToken),
            ["BusinessEvent"] = await CountAsync(connection, "BusinessEvent", replayRunId, cancellationToken),
            ["PlannedStagingRow"] = await CountAsync(connection, "PlannedStagingRow", replayRunId, cancellationToken),
            ["ParityCheck"] = await CountAsync(connection, "ParityCheck", replayRunId, cancellationToken)
        };
        var checks = new[]
        {
            Check("ReplayRun", 1, counts["ReplayRun"]),
            Check("ReplayInputFile", bundle.InputFiles.Count, counts["ReplayInputFile"]),
            Check("NormalizedEvent", bundle.NormalizedEvents.Count, counts["NormalizedEvent"]),
            Check("EvidenceOccurrence", bundle.EvidenceOccurrences.Count, counts["EvidenceOccurrence"]),
            Check("BusinessEvent", bundle.BusinessEvents.Count, counts["BusinessEvent"]),
            Check("PlannedStagingRow", bundle.PlannedStagingRows.Count, counts["PlannedStagingRow"]),
            Check("ParityCheck", parity.Rows.Count, counts["ParityCheck"])
        };
        return new DbParityReport(checks.All(x => x.Status == "PASS") ? "PASS" : "FAIL", counts, checks);
    }
    public async Task<RollbackProbeReport> RunRollbackProbeAsync(R018StageInputParityReport parity, Guid replayRunId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(DatabaseConnectionString());
        await connection.OpenAsync(cancellationToken);
        var before = await CountAsync(connection, "ParityCheck", replayRunId, cancellationToken);
        var targetCheck = parity.Rows.FirstOrDefault()?.Check ?? throw new InvalidOperationException("ROLLBACK_PROBE_NO_PARITY_ROW");
        var conflictCode = "NONE";
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await ExecuteAsync(connection, transaction,
                "UPDATE r018stage.ParityCheck SET PayloadSha256 = @payloadSha WHERE ReplayRunId = @id AND CheckName = @key;",
                cancellationToken,
                ("@id", replayRunId),
                ("@key", targetCheck),
                ("@payloadSha", new string('0', 64)));
            if (await VerifyPayloadRowsAsync(connection, transaction, "ParityCheck", "CheckName", replayRunId, parity.Rows.Select(x => new StagePayloadRow(x.Check, R018ArtifactBundleReader.ComputeSha256(Serialize(x)), Serialize(x))), cancellationToken))
            {
                throw new InvalidOperationException("ROLLBACK_PROBE_CONFLICT_NOT_DETECTED");
            }

            conflictCode = "CONFLICTING_REPLAY_PAYLOAD";
            throw new InvalidOperationException(conflictCode);
        }
        catch (InvalidOperationException ex) when (ex.Message == "CONFLICTING_REPLAY_PAYLOAD")
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        var after = await CountAsync(connection, "ParityCheck", replayRunId, cancellationToken);
        var tamperedRows = Convert.ToInt64(await ScalarAsync(connection, null, "SELECT COUNT_BIG(1) FROM r018stage.ParityCheck WHERE ReplayRunId = @id AND CheckName = @key AND PayloadSha256 = @payloadSha;", cancellationToken, ("@id", replayRunId), ("@key", targetCheck), ("@payloadSha", new string('0', 64))), CultureInfo.InvariantCulture);
        return new RollbackProbeReport(before == after && tamperedRows == 0 ? "PASS" : "FAIL", conflictCode, before, after, tamperedRows);
    }
    public async Task<TableMutationAudit> BuildTableMutationAuditAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(DatabaseConnectionString());
        await connection.OpenAsync(cancellationToken);
        var rows = new List<TableAuditRow>();
        await using var command = new SqlCommand("""
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA, TABLE_NAME;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var schema = reader.GetString(0);
            var table = reader.GetString(1);
            rows.Add(new TableAuditRow(schema, table, R018StageLocalDbReplayConstants.ForbiddenCanonicalTables.Contains(table)));
        }

        return new TableMutationAudit(
            rows.Count(x => string.Equals(x.Schema, R018StageLocalDbReplayConstants.StageSchemaName, StringComparison.OrdinalIgnoreCase)),
            rows.Count(x => x.IsForbiddenCanonicalTable),
            0,
            0,
            rows);
    }

    public async Task<IReadOnlyDictionary<string, string>> ExportAsync(string exportDirectory, CancellationToken cancellationToken)
    {
        if (Directory.Exists(exportDirectory))
        {
            Directory.Delete(exportDirectory, recursive: true);
        }

        Directory.CreateDirectory(exportDirectory);
        await using var connection = new SqlConnection(DatabaseConnectionString());
        await connection.OpenAsync(cancellationToken);
        var export = new SortedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["ReplayRun"] = await QueryRowsAsync(connection, "SELECT PlanSchemaVersion, InputBundleHash, DeterministicContentHash, State, SourceRunId, ApprovedCandidateHash, PayloadSha256 FROM r018stage.ReplayRun ORDER BY PlanSchemaVersion, InputBundleHash, DeterministicContentHash;", cancellationToken),
            ["ReplayInputFile"] = await QueryRowsAsync(connection, "SELECT FileName, FileSha256, PayloadSha256 FROM r018stage.ReplayInputFile ORDER BY FileName;", cancellationToken),
            ["NormalizedEvent"] = await QueryRowsAsync(connection, "SELECT StableKey, SemanticPayloadHash, PayloadSha256 FROM r018stage.NormalizedEvent ORDER BY StableKey, SemanticPayloadHash;", cancellationToken),
            ["EvidenceOccurrence"] = await QueryRowsAsync(connection, "SELECT OccurrenceId, PayloadSha256 FROM r018stage.EvidenceOccurrence ORDER BY OccurrenceId;", cancellationToken),
            ["BusinessEvent"] = await QueryRowsAsync(connection, "SELECT BusinessEventId, SemanticPayloadHash, PayloadSha256 FROM r018stage.BusinessEvent ORDER BY BusinessEventId;", cancellationToken),
            ["PlannedStagingRow"] = await QueryRowsAsync(connection, "SELECT PlannedRowId, SemanticPayloadHash, PayloadSha256 FROM r018stage.PlannedStagingRow ORDER BY PlannedRowId;", cancellationToken),
            ["ParityCheck"] = await QueryRowsAsync(connection, "SELECT CheckName, PayloadSha256 FROM r018stage.ParityCheck ORDER BY CheckName;", cancellationToken)
        };
        var exportPath = Path.Combine(exportDirectory, "r018stage_export.json");
        await File.WriteAllTextAsync(exportPath, JsonSerializer.Serialize(export, JsonOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
        var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["r018stage_export.json"] = R018ArtifactBundleReader.ComputeFileSha256(exportPath)
        };
        await File.WriteAllTextAsync(Path.Combine(exportDirectory, "r018stage_export_hashes.json"), JsonSerializer.Serialize(hashes, JsonOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
        return hashes;
    }
    private string MasterConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "master",
            TrustServerCertificate = true
        };
        return builder.ConnectionString;
    }

    private string DatabaseConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = _databaseName,
            TrustServerCertificate = true
        };
        return builder.ConnectionString;
    }

    private static IReadOnlyList<string> SchemaStatements()
        =>
        [
            "IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'r018stage') EXEC('CREATE SCHEMA r018stage');",
            """
            CREATE TABLE r018stage.ReplayRun (
                ReplayRunId uniqueidentifier NOT NULL CONSTRAINT PK_r018stage_ReplayRun PRIMARY KEY,
                SchemaVersion nvarchar(80) NOT NULL,
                PlanSchemaVersion nvarchar(80) NOT NULL,
                InputBundleHash char(64) NOT NULL,
                DeterministicContentHash char(64) NOT NULL,
                State nvarchar(32) NOT NULL,
                SourceRunId nvarchar(200) NOT NULL,
                ApprovedCandidateHash nvarchar(128) NOT NULL,
                ToolCommit nvarchar(128) NOT NULL,
                CreatedAtUtc datetimeoffset NOT NULL CONSTRAINT DF_r018stage_ReplayRun_Created DEFAULT SYSUTCDATETIME(),
                FinalizedAtUtc datetimeoffset NULL,
                PayloadJson nvarchar(max) NOT NULL,
                PayloadSha256 char(64) NOT NULL,
                CONSTRAINT UQ_r018stage_ReplayRun_Identity UNIQUE (PlanSchemaVersion, InputBundleHash, DeterministicContentHash)
            );
            """,
            """
            CREATE TABLE r018stage.ReplayInputFile (
                ReplayRunId uniqueidentifier NOT NULL,
                FileName nvarchar(260) NOT NULL,
                FileSha256 char(64) NOT NULL,
                LengthBytes bigint NOT NULL,
                PayloadJson nvarchar(max) NOT NULL,
                PayloadSha256 char(64) NOT NULL,
                CreatedAtUtc datetimeoffset NOT NULL CONSTRAINT DF_r018stage_ReplayInputFile_Created DEFAULT SYSUTCDATETIME(),
                CONSTRAINT PK_r018stage_ReplayInputFile PRIMARY KEY (ReplayRunId, FileName),
                CONSTRAINT FK_r018stage_ReplayInputFile_ReplayRun FOREIGN KEY (ReplayRunId) REFERENCES r018stage.ReplayRun(ReplayRunId)
            );
            """,
            PayloadTable("NormalizedEvent", "StableKey nvarchar(512) NOT NULL", "SemanticPayloadHash char(64) NOT NULL", "CONSTRAINT PK_r018stage_NormalizedEvent PRIMARY KEY (ReplayRunId, StableKey, SemanticPayloadHash)"),
            PayloadTable("EvidenceOccurrence", "OccurrenceId nvarchar(256) NOT NULL", "SemanticPayloadHash char(64) NOT NULL", "CONSTRAINT PK_r018stage_EvidenceOccurrence PRIMARY KEY (ReplayRunId, OccurrenceId)"),
            PayloadTable("BusinessEvent", "BusinessEventId nvarchar(256) NOT NULL", "SemanticPayloadHash char(64) NOT NULL", "CONSTRAINT PK_r018stage_BusinessEvent PRIMARY KEY (ReplayRunId, BusinessEventId)"),
            PayloadTable("PlannedStagingRow", "PlannedRowId nvarchar(256) NOT NULL", "SemanticPayloadHash char(64) NOT NULL", "CONSTRAINT PK_r018stage_PlannedStagingRow PRIMARY KEY (ReplayRunId, PlannedRowId)"),
            PayloadTable("ParityCheck", "CheckName nvarchar(512) NOT NULL", "SemanticPayloadHash char(64) NOT NULL", "CONSTRAINT PK_r018stage_ParityCheck PRIMARY KEY (ReplayRunId, CheckName)")
        ];

    private static string PayloadTable(string table, string keyColumn, string semanticColumn, string primaryKey)
        => $$"""
            CREATE TABLE r018stage.{{table}} (
                ReplayRunId uniqueidentifier NOT NULL,
                {{keyColumn}},
                {{semanticColumn}},
                PayloadJson nvarchar(max) NOT NULL,
                PayloadSha256 char(64) NOT NULL,
                CreatedAtUtc datetimeoffset NOT NULL CONSTRAINT DF_r018stage_{{table}}_Created DEFAULT SYSUTCDATETIME(),
                {{primaryKey}},
                CONSTRAINT FK_r018stage_{{table}}_ReplayRun FOREIGN KEY (ReplayRunId) REFERENCES r018stage.ReplayRun(ReplayRunId)
            );
            """;

    private static async Task<ExistingReplayRun?> ExistingReplayRunAsync(SqlConnection connection, SqlTransaction transaction, string planSchemaVersion, string inputBundleHash, string deterministicContentHash, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("""
            SELECT ReplayRunId, PayloadSha256
            FROM r018stage.ReplayRun
            WHERE PlanSchemaVersion = @planSchemaVersion
              AND InputBundleHash = @inputBundleHash
              AND DeterministicContentHash = @deterministicContentHash;
            """, connection, transaction);
        command.Parameters.AddWithValue("@planSchemaVersion", planSchemaVersion);
        command.Parameters.AddWithValue("@inputBundleHash", inputBundleHash);
        command.Parameters.AddWithValue("@deterministicContentHash", deterministicContentHash);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new ExistingReplayRun(reader.GetGuid(0), reader.GetString(1));
    }

    private static async Task<bool> VerifyExistingRowsAsync(SqlConnection connection, SqlTransaction transaction, Guid replayRunId, R018StageInputBundle bundle, R018StageInputParityReport parity, CancellationToken cancellationToken)
    {
        return await VerifyInputFilesAsync(connection, transaction, replayRunId, bundle.InputFiles, cancellationToken) &&
               await VerifyPayloadRowsAsync(connection, transaction, "NormalizedEvent", "StableKey", replayRunId, bundle.NormalizedEvents.Select(x => new StagePayloadRow(x.StableKey, SemanticHash(x), Serialize(x))), cancellationToken) &&
               await VerifyPayloadRowsAsync(connection, transaction, "EvidenceOccurrence", "OccurrenceId", replayRunId, bundle.EvidenceOccurrences.Select(x => new StagePayloadRow(x.OccurrenceId, x.OccurrenceId, Serialize(x))), cancellationToken) &&
               await VerifyPayloadRowsAsync(connection, transaction, "BusinessEvent", "BusinessEventId", replayRunId, bundle.BusinessEvents.Select(x => new StagePayloadRow(x.BusinessEventId, x.SemanticFingerprint, Serialize(x))), cancellationToken) &&
               await VerifyPayloadRowsAsync(connection, transaction, "PlannedStagingRow", "PlannedRowId", replayRunId, bundle.PlannedStagingRows.Select(x => new StagePayloadRow(x.PlannedRowId, R018ArtifactBundleReader.ComputeSha256(Serialize(x)), Serialize(x))), cancellationToken) &&
               await VerifyPayloadRowsAsync(connection, transaction, "ParityCheck", "CheckName", replayRunId, parity.Rows.Select(x => new StagePayloadRow(x.Check, R018ArtifactBundleReader.ComputeSha256(Serialize(x)), Serialize(x))), cancellationToken);
    }

    private static async Task<bool> VerifyInputFilesAsync(SqlConnection connection, SqlTransaction transaction, Guid replayRunId, IReadOnlyList<R018StageInputFile> inputFiles, CancellationToken cancellationToken)
    {
        if (await CountAsync(connection, transaction, "ReplayInputFile", replayRunId, cancellationToken) != inputFiles.Count)
        {
            return false;
        }

        foreach (var file in inputFiles)
        {
            var payload = Serialize(new { file.FileName, file.Sha256, file.LengthBytes, file.Content });
            var payloadHash = R018ArtifactBundleReader.ComputeSha256(payload);
            var existing = await ScalarAsync(connection, transaction, "SELECT PayloadSha256 FROM r018stage.ReplayInputFile WHERE ReplayRunId = @id AND FileName = @key AND FileSha256 = @fileSha;", cancellationToken, ("@id", replayRunId), ("@key", file.FileName), ("@fileSha", file.Sha256));
            if (!string.Equals(Convert.ToString(existing, CultureInfo.InvariantCulture), payloadHash, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<bool> VerifyPayloadRowsAsync(SqlConnection connection, SqlTransaction transaction, string table, string keyColumn, Guid replayRunId, IEnumerable<StagePayloadRow> expectedRows, CancellationToken cancellationToken)
    {
        var rows = expectedRows.ToArray();
        if (await CountAsync(connection, transaction, table, replayRunId, cancellationToken) != rows.Length)
        {
            return false;
        }

        foreach (var row in rows)
        {
            var payloadHash = R018ArtifactBundleReader.ComputeSha256(row.PayloadJson);
            var existingPayloadHash = await ScalarAsync(connection, transaction, $"SELECT PayloadSha256 FROM r018stage.{table} WHERE ReplayRunId = @id AND {keyColumn} = @key AND SemanticPayloadHash = @semantic;", cancellationToken, ("@id", replayRunId), ("@key", row.Key), ("@semantic", row.SemanticPayloadHash));
            if (!string.Equals(Convert.ToString(existingPayloadHash, CultureInfo.InvariantCulture), payloadHash, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
    private static async Task<int> InsertReplayRunAsync(SqlConnection connection, SqlTransaction transaction, Guid replayRunId, R018StageInputBundle bundle, string payloadJson, string payloadHash, string state, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, transaction, """
            INSERT INTO r018stage.ReplayRun (ReplayRunId, SchemaVersion, PlanSchemaVersion, InputBundleHash, DeterministicContentHash, State, SourceRunId, ApprovedCandidateHash, ToolCommit, PayloadJson, PayloadSha256)
            VALUES (@id, @schemaVersion, @planSchemaVersion, @inputBundleHash, @deterministicContentHash, @state, @sourceRunId, @approvedCandidateHash, @toolCommit, @payloadJson, @payloadSha256);
            """, cancellationToken,
            ("@id", replayRunId),
            ("@schemaVersion", R018StageLocalDbReplayConstants.ReplaySchemaVersion),
            ("@planSchemaVersion", bundle.Plan.SchemaVersion),
            ("@inputBundleHash", bundle.Plan.InputBundleHash),
            ("@deterministicContentHash", bundle.Plan.DeterministicContentHash),
            ("@state", state),
            ("@sourceRunId", bundle.Plan.SourceRunId),
            ("@approvedCandidateHash", bundle.Plan.ApprovedCandidateHash),
            ("@toolCommit", bundle.Plan.ToolCommit),
            ("@payloadJson", payloadJson),
            ("@payloadSha256", payloadHash));
        return 1;
    }

    private static async Task<int> InsertInputFilesAsync(SqlConnection connection, SqlTransaction transaction, Guid replayRunId, IReadOnlyList<R018StageInputFile> files, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var file in files)
        {
            var payload = Serialize(new { file.FileName, file.Sha256, file.LengthBytes, file.Content });
            await ExecuteAsync(connection, transaction, """
                INSERT INTO r018stage.ReplayInputFile (ReplayRunId, FileName, FileSha256, LengthBytes, PayloadJson, PayloadSha256)
                VALUES (@id, @key, @fileSha, @length, @payload, @payloadSha);
                """, cancellationToken,
                ("@id", replayRunId),
                ("@key", file.FileName),
                ("@fileSha", file.Sha256),
                ("@length", file.LengthBytes),
                ("@payload", payload),
                ("@payloadSha", R018ArtifactBundleReader.ComputeSha256(payload)));
            count++;
        }

        return count;
    }

    private static async Task<int> InsertPayloadRowsAsync(SqlConnection connection, SqlTransaction transaction, string table, Guid replayRunId, IEnumerable<StagePayloadRow> rows, CancellationToken cancellationToken)
    {
        var keyColumn = table switch
        {
            "NormalizedEvent" => "StableKey",
            "EvidenceOccurrence" => "OccurrenceId",
            "BusinessEvent" => "BusinessEventId",
            "PlannedStagingRow" => "PlannedRowId",
            "ParityCheck" => "CheckName",
            _ => throw new InvalidOperationException($"UNKNOWN_STAGE_TABLE:{table}")
        };
        var count = 0;
        foreach (var row in rows)
        {
            var payloadHash = R018ArtifactBundleReader.ComputeSha256(row.PayloadJson);
            var existingPayloadHash = await ScalarAsync(connection, transaction, $"SELECT PayloadSha256 FROM r018stage.{table} WHERE ReplayRunId = @id AND {keyColumn} = @key;", cancellationToken, ("@id", replayRunId), ("@key", row.Key));
            if (existingPayloadHash is not null)
            {
                if (!string.Equals(Convert.ToString(existingPayloadHash, CultureInfo.InvariantCulture), payloadHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("CONFLICTING_REPLAY_PAYLOAD");
                }

                continue;
            }

            await ExecuteAsync(connection, transaction, $"INSERT INTO r018stage.{table} (ReplayRunId, {keyColumn}, SemanticPayloadHash, PayloadJson, PayloadSha256) VALUES (@id, @key, @semantic, @payload, @payloadSha);", cancellationToken,
                ("@id", replayRunId),
                ("@key", row.Key),
                ("@semantic", row.SemanticPayloadHash),
                ("@payload", row.PayloadJson),
                ("@payloadSha", payloadHash));
            count++;
        }

        return count;
    }

    private static string SemanticHash(R018NormalizedEvent ev)
        => R018ArtifactBundleReader.ComputeSha256(string.Join('|',
            ev.Kind,
            ev.StableKey,
            ev.TerminalState ?? "",
            ev.ExecType ?? "",
            ev.OrdStatus ?? "",
            ev.OrderQuantity?.ToString(CultureInfo.InvariantCulture) ?? "",
            ev.LastQuantity?.ToString(CultureInfo.InvariantCulture) ?? "",
            ev.CumQuantity?.ToString(CultureInfo.InvariantCulture) ?? "",
            ev.LeavesQuantity?.ToString(CultureInfo.InvariantCulture) ?? "",
            ev.FillPrice?.ToString(CultureInfo.InvariantCulture) ?? ""));

    private static async Task<long> CountAsync(SqlConnection connection, string table, CancellationToken cancellationToken)
        => Convert.ToInt64(await ScalarAsync(connection, null, $"SELECT COUNT_BIG(1) FROM r018stage.{table};", cancellationToken), CultureInfo.InvariantCulture);

    private static async Task<long> CountAsync(SqlConnection connection, SqlTransaction transaction, string table, Guid replayRunId, CancellationToken cancellationToken)
        => Convert.ToInt64(await ScalarAsync(connection, transaction, $"SELECT COUNT_BIG(1) FROM r018stage.{table} WHERE ReplayRunId = @id;", cancellationToken, ("@id", replayRunId)), CultureInfo.InvariantCulture);

    private static async Task<long> CountAsync(SqlConnection connection, string table, Guid replayRunId, CancellationToken cancellationToken)
        => Convert.ToInt64(await ScalarAsync(connection, null, $"SELECT COUNT_BIG(1) FROM r018stage.{table} WHERE ReplayRunId = @id;", cancellationToken, ("@id", replayRunId)), CultureInfo.InvariantCulture);

    private static async Task<long> CountReplayRunAsync(SqlConnection connection, Guid replayRunId, CancellationToken cancellationToken)
        => Convert.ToInt64(await ScalarAsync(connection, null, "SELECT COUNT_BIG(1) FROM r018stage.ReplayRun WHERE ReplayRunId = @id;", cancellationToken, ("@id", replayRunId)), CultureInfo.InvariantCulture);

    private static DbParityCheck Check(string table, long expected, long actual)
        => new(table, expected, actual, expected == actual ? "PASS" : "FAIL");

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryRowsAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                row[reader.GetName(index)] = await reader.IsDBNullAsync(index, cancellationToken) ? null : reader.GetValue(index);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static async Task ExecuteAsync(SqlConnection connection, SqlTransaction? transaction, string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<object?> ScalarAsync(SqlConnection connection, SqlTransaction? transaction, string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value == DBNull.Value ? null : value;
    }

    private static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, JsonOptions);

    private sealed record ExistingReplayRun(Guid ReplayRunId, string PayloadSha256);
    private sealed record StagePayloadRow(string Key, string SemanticPayloadHash, string PayloadJson);
}

internal sealed record StageImportResult(Guid ReplayRunId, string Status, int RowsInserted, int InputFiles, int NormalizedEvents, int EvidenceOccurrences, int BusinessEvents, int PlannedStagingRows, int ParityChecks);
internal sealed record DbParityReport(string Status, IReadOnlyDictionary<string, long> Counts, IReadOnlyList<DbParityCheck> Checks);
internal sealed record DbParityCheck(string Table, long Expected, long Actual, string Status);
internal sealed record TableMutationAudit(int StageTableCount, int CanonicalTableCount, long CanonicalTablesRowsBefore, long CanonicalTablesRowsAfter, IReadOnlyList<TableAuditRow> Tables);
internal sealed record RollbackProbeReport(string Status, string ConflictCode, long RowsBefore, long RowsAfter, long ProbeRowsAfterRollback);
internal sealed record TableAuditRow(string Schema, string Table, bool IsForbiddenCanonicalTable);


