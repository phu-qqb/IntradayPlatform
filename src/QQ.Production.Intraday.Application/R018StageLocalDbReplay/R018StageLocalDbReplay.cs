using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using QQ.Production.Intraday.Application.R018ImportPlanning;

namespace QQ.Production.Intraday.Application.R018StageLocalDbReplay;

public static class R018StageLocalDbReplayConstants
{
    public const string ReplaySchemaVersion = "r018_stage_local_isolated_db_replay_v1";
    public const string StageSchemaName = "r018stage";
    public const string DisposableDatabasePrefix = "QQIntraday_M1D_StageOnly_";

    public static readonly IReadOnlyList<string> RequiredPlanFiles =
    [
        "bundle_manifest.json",
        "validation_report.json",
        "replay_eligibility.json",
        "normalized_events.jsonl",
        "evidence_occurrences.jsonl",
        "business_events.json",
        "typed_staging_plan.json",
        "import_plan_v3.json",
        "output_hashes.json"
    ];

    public static readonly IReadOnlySet<string> ForbiddenCanonicalTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ModelRuns",
        "TargetWeights",
        "TargetPositions",
        "DriftSnapshots",
        "TradeIntents",
        "RiskDecisions",
        "ParentOrders",
        "ChildOrders",
        "ExecutionReports",
        "Fills",
        "PositionLedgerEvents",
        "ReconciliationRuns",
        "ReconciliationBreaks",
        "EodReconciliationRuns",
        "EodReconciliationBreaks",
        "ExceptionCases"
    };
}

public sealed record R018StageInputFile(
    string FileName,
    string FullPath,
    string Sha256,
    long LengthBytes,
    string Content);

public sealed record R018StageInputBundle(
    string PlanDirectory,
    R018ImportPlan Plan,
    R018ArtifactBundleManifest BundleManifest,
    R018ValidationReport ValidationReport,
    R018ReplayEligibility ReplayEligibility,
    IReadOnlyList<R018NormalizedEvent> NormalizedEvents,
    IReadOnlyList<R018EvidenceOccurrence> EvidenceOccurrences,
    IReadOnlyList<R018BusinessEvent> BusinessEvents,
    IReadOnlyList<R018PlannedStagingRow> PlannedStagingRows,
    IReadOnlyDictionary<string, string> OutputHashes,
    IReadOnlyList<R018StageInputFile> InputFiles);

public sealed record R018StageInputParityRow(
    string Check,
    string Source,
    string Expected,
    string Actual,
    string Status,
    string Severity,
    string Detail);

public sealed record R018StageInputParityReport(
    string SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string PlanDirectory,
    int CheckCount,
    int CriticalFailureCount,
    IReadOnlyList<R018StageInputParityRow> Rows)
{
    public bool CriticalPassed => CriticalFailureCount == 0;
}

public sealed record R018StageEntryGateReport(
    string SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string Status,
    bool CanImport,
    IReadOnlyList<R018StageInputParityRow> Checks)
{
    public IReadOnlyList<string> BlockReasons =>
        Checks.Where(x => x.Status == "FAIL" && x.Severity == "CRITICAL")
            .Select(x => x.Check)
            .ToArray();
}

public sealed record R018StageConnectionGateReport(
    string Status,
    bool IsLocalOnly,
    bool DatabaseNameHasRequiredPrefix,
    bool CreateDisposableDbRequired,
    bool DropAfterExportRefusesUnsafeName,
    string DataSource,
    string DatabaseName,
    IReadOnlyList<string> RejectionReasons);

public sealed class R018StageLocalDbReplayLoader
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public R018StageInputBundle Load(string planDirectory)
    {
        var fullPlanDirectory = Path.GetFullPath(planDirectory);
        if (!Directory.Exists(fullPlanDirectory))
        {
            throw new DirectoryNotFoundException(fullPlanDirectory);
        }

        var plan = ReadJson<R018ImportPlan>(Path.Combine(fullPlanDirectory, "import_plan_v3.json"));
        var manifest = ReadJson<R018ArtifactBundleManifest>(Path.Combine(fullPlanDirectory, "bundle_manifest.json"));
        var validation = ReadJson<R018ValidationReport>(Path.Combine(fullPlanDirectory, "validation_report.json"));
        var eligibility = ReadJson<R018ReplayEligibility>(Path.Combine(fullPlanDirectory, "replay_eligibility.json"));
        var normalizedEvents = ReadJsonLines<R018NormalizedEvent>(Path.Combine(fullPlanDirectory, "normalized_events.jsonl"));
        var evidenceOccurrences = ReadJsonLines<R018EvidenceOccurrence>(Path.Combine(fullPlanDirectory, "evidence_occurrences.jsonl"));
        var businessEvents = ReadJson<IReadOnlyList<R018BusinessEvent>>(Path.Combine(fullPlanDirectory, "business_events.json"));
        var stagingRows = ReadJson<IReadOnlyList<R018PlannedStagingRow>>(Path.Combine(fullPlanDirectory, "typed_staging_plan.json"));
        var outputHashes = ReadJson<IReadOnlyDictionary<string, string>>(Path.Combine(fullPlanDirectory, "output_hashes.json"));
        var files = R018StageLocalDbReplayConstants.RequiredPlanFiles
            .Select(fileName =>
            {
                var path = Path.Combine(fullPlanDirectory, fileName);
                return new R018StageInputFile(
                    fileName,
                    path,
                    File.Exists(path) ? R018ArtifactBundleReader.ComputeFileSha256(path) : "",
                    File.Exists(path) ? new FileInfo(path).Length : -1,
                    File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "");
            })
            .ToArray();

        return new R018StageInputBundle(
            fullPlanDirectory,
            plan,
            manifest,
            validation,
            eligibility,
            normalizedEvents,
            evidenceOccurrences,
            businessEvents,
            stagingRows,
            outputHashes,
            files);
    }

    public R018StageInputParityReport RecalculateParity(string planDirectory, DateTimeOffset generatedAtUtc)
    {
        var rows = new List<R018StageInputParityRow>();
        var fullPlanDirectory = Path.GetFullPath(planDirectory);

        foreach (var fileName in R018StageLocalDbReplayConstants.RequiredPlanFiles)
        {
            var path = Path.Combine(fullPlanDirectory, fileName);
            rows.Add(Row(
                $"required_file_present:{fileName}",
                fileName,
                "exists",
                File.Exists(path) ? "exists" : "missing",
                File.Exists(path),
                "CRITICAL",
                "M1D requires this M1C.2 output file and does not trust prior parity CSV."));
        }

        R018StageInputBundle? bundle = null;
        try
        {
            bundle = Load(fullPlanDirectory);
        }
        catch (Exception ex)
        {
            rows.Add(Row("plan_parse", fullPlanDirectory, "parse ok", ex.GetType().Name + ":" + ex.Message, false, "CRITICAL", "All required M1C.2 outputs must parse before staging import."));
        }

        if (bundle is not null)
        {
            foreach (var inputFile in bundle.InputFiles)
            {
                if (inputFile.FileName.Equals("output_hashes.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!bundle.OutputHashes.TryGetValue(inputFile.FileName, out var expectedHash))
                {
                    rows.Add(Row($"output_hash_present:{inputFile.FileName}", "output_hashes.json", "hash present", "missing", false, "CRITICAL", "Independent parity recalculates output_hashes membership."));
                    continue;
                }

                rows.Add(Row(
                    $"output_hash_match:{inputFile.FileName}",
                    inputFile.FileName,
                    expectedHash,
                    inputFile.Sha256,
                    string.Equals(expectedHash, inputFile.Sha256, StringComparison.OrdinalIgnoreCase),
                    "CRITICAL",
                    "Independent SHA-256 recomputation, not M1C.2 CSV trust."));
            }

            rows.Add(Row("normalized_event_count_matches_plan", "normalized_events.jsonl/import_plan_v3.json", bundle.Plan.NormalizedEvents.Count.ToString(CultureInfo.InvariantCulture), bundle.NormalizedEvents.Count.ToString(CultureInfo.InvariantCulture), bundle.Plan.NormalizedEvents.Count == bundle.NormalizedEvents.Count, "CRITICAL", "Standalone JSONL and embedded plan must agree."));
            rows.Add(Row("evidence_occurrence_count_matches_plan", "evidence_occurrences.jsonl/import_plan_v3.json", bundle.Plan.EvidenceOccurrences.Count.ToString(CultureInfo.InvariantCulture), bundle.EvidenceOccurrences.Count.ToString(CultureInfo.InvariantCulture), bundle.Plan.EvidenceOccurrences.Count == bundle.EvidenceOccurrences.Count, "CRITICAL", "Standalone JSONL and embedded plan must agree."));
            rows.Add(Row("business_event_count_matches_plan", "business_events.json/import_plan_v3.json", bundle.Plan.BusinessEvents.Count.ToString(CultureInfo.InvariantCulture), bundle.BusinessEvents.Count.ToString(CultureInfo.InvariantCulture), bundle.Plan.BusinessEvents.Count == bundle.BusinessEvents.Count, "CRITICAL", "Standalone JSON and embedded plan must agree."));
            rows.Add(Row("planned_staging_count_matches_plan", "typed_staging_plan.json/import_plan_v3.json", (bundle.Plan.PlannedStagingRows?.Count ?? 0).ToString(CultureInfo.InvariantCulture), bundle.PlannedStagingRows.Count.ToString(CultureInfo.InvariantCulture), (bundle.Plan.PlannedStagingRows?.Count ?? 0) == bundle.PlannedStagingRows.Count, "CRITICAL", "Typed staging plan must match import plan."));
            rows.Add(Row("validation_report_status_matches_plan", "validation_report.json/import_plan_v3.json", bundle.Plan.Validation.Status.ToString(), bundle.ValidationReport.Status.ToString(), bundle.Plan.Validation.Status == bundle.ValidationReport.Status, "CRITICAL", "Validation status must not diverge between files."));
            rows.Add(Row("replay_eligibility_matches_plan", "replay_eligibility.json/import_plan_v3.json", bundle.Plan.ReplayEligibility.LocalIsolatedReplayAllowed.ToString(), bundle.ReplayEligibility.LocalIsolatedReplayAllowed.ToString(), bundle.Plan.ReplayEligibility.LocalIsolatedReplayAllowed == bundle.ReplayEligibility.LocalIsolatedReplayAllowed, "CRITICAL", "M1D gate uses ReplayEligibility only."));
        }

        var criticalFailures = rows.Count(x => x.Status == "FAIL" && x.Severity == "CRITICAL");
        return new R018StageInputParityReport(
            R018StageLocalDbReplayConstants.ReplaySchemaVersion,
            generatedAtUtc,
            fullPlanDirectory,
            rows.Count,
            criticalFailures,
            rows);
    }

    internal static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static T ReadJson<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(path);
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8), JsonOptions)
            ?? throw new InvalidOperationException($"JSON_DESERIALIZE_NULL:{path}");
    }

    private static IReadOnlyList<T> ReadJsonLines<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(path);
        }

        var rows = new List<T>();
        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            rows.Add(JsonSerializer.Deserialize<T>(line, JsonOptions) ?? throw new InvalidOperationException($"JSONL_DESERIALIZE_NULL:{path}"));
        }

        return rows;
    }

    private static R018StageInputParityRow Row(string check, string source, string expected, string actual, bool passed, string severity, string detail)
        => new(check, source, expected, actual, passed ? "PASS" : "FAIL", severity, detail);
}

public static class R018StageOnlyEntryGate
{
    public static R018StageEntryGateReport Evaluate(R018StageInputBundle bundle, R018StageInputParityReport parity, DateTimeOffset generatedAtUtc)
    {
        var rows = new List<R018StageInputParityRow>(parity.Rows);
        rows.Add(Row("input_parity_critical_passed", "m1d_input_parity_report", "true", parity.CriticalPassed.ToString(), parity.CriticalPassed, "CRITICAL", "Critical input parity must pass before DB staging."));
        rows.Add(Row("plan_status_not_rejected", "import_plan_v3.json", "not REJECTED", bundle.Plan.Status.ToString(), bundle.Plan.Status is not R018ImportBundleStatus.REJECTED, "CRITICAL", "Rejected plans never enter staging import."));
        rows.Add(Row("replay_eligibility_local_isolated_allowed", "import_plan_v3.json:ReplayEligibility", "true", bundle.Plan.ReplayEligibility.LocalIsolatedReplayAllowed.ToString(), bundle.Plan.ReplayEligibility.LocalIsolatedReplayAllowed, "CRITICAL", "M1D must use ReplayEligibility.LocalIsolatedReplayAllowed, never IdentityScope.LocalIsolatedReplayAllowed."));
        rows.Add(Row("validation_has_no_error", "import_plan_v3.json:Validation", "0 ERROR", bundle.Plan.Validation.Issues.Count(x => string.Equals(x.Severity, "ERROR", StringComparison.OrdinalIgnoreCase)).ToString(CultureInfo.InvariantCulture), !bundle.Plan.Validation.Issues.Any(x => string.Equals(x.Severity, "ERROR", StringComparison.OrdinalIgnoreCase)), "CRITICAL", "Validation ERROR blocks transaction."));
        rows.Add(Row("db_apply_false", "import_plan_v3.json", "false", bundle.Plan.DbApply.ToString(), !bundle.Plan.DbApply, "CRITICAL", "M1D is staging evidence only, not canonical DB apply."));
        rows.Add(Row("network_allowed_false", "import_plan_v3.json", "false", bundle.Plan.NetworkAllowed.ToString(), !bundle.Plan.NetworkAllowed, "CRITICAL", "M1D is local only and no network."));
        rows.Add(Row("creates_model_run_false", "import_plan_v3.json", "false", bundle.Plan.CreatesModelRun.ToString(), !bundle.Plan.CreatesModelRun, "CRITICAL", "Canonical ModelRun creation is forbidden."));
        rows.Add(Row("creates_target_weights_false", "import_plan_v3.json", "false", bundle.Plan.CreatesTargetWeights.ToString(), !bundle.Plan.CreatesTargetWeights, "CRITICAL", "Canonical TargetWeights creation is forbidden."));
        rows.Add(Row("creates_position_ledger_events_false", "import_plan_v3.json", "false", bundle.Plan.CreatesPositionLedgerEvents.ToString(), !bundle.Plan.CreatesPositionLedgerEvents, "CRITICAL", "Canonical PositionLedgerEvents creation is forbidden."));
        rows.Add(Row("planned_rows_apply_eligible_false", "typed_staging_plan.json", "0 apply eligible", bundle.PlannedStagingRows.Count(x => x.ApplyEligible).ToString(CultureInfo.InvariantCulture), !bundle.PlannedStagingRows.Any(x => x.ApplyEligible), "CRITICAL", "Typed rows are persisted as plans/evidence only."));

        var forbiddenTargets = bundle.Plan.ProposedMutationTargets.Where(R018StageLocalDbReplayConstants.ForbiddenCanonicalTables.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        rows.Add(Row("proposed_mutation_targets_are_descriptive_only", "import_plan_v3.json", "canonical descriptive targets allowed only with DbApply=false", string.Join("|", forbiddenTargets), !bundle.Plan.DbApply, "CRITICAL", "Canonical target names may appear in a stage-only plan but no canonical apply is allowed."));

        var canImport = rows.All(x => x.Status == "PASS" || x.Severity != "CRITICAL");
        return new R018StageEntryGateReport(
            R018StageLocalDbReplayConstants.ReplaySchemaVersion,
            generatedAtUtc,
            canImport ? "GO_M1D_STAGE_ONLY_LOCAL_ISOLATED_DB_REPLAY" : "NO_GO_M1D_STAGE_ONLY_LOCAL_ISOLATED_DB_REPLAY",
            canImport,
            rows);
    }

    private static R018StageInputParityRow Row(string check, string source, string expected, string actual, bool passed, string severity, string detail)
        => new(check, source, expected, actual, passed ? "PASS" : "FAIL", severity, detail);
}

public static class R018StageLocalConnectionPolicy
{
    private static readonly Regex SafeDbName = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    public static R018StageConnectionGateReport Evaluate(string dataSource, string databaseName, bool createDisposableDb, bool dropAfterExport)
    {
        var reasons = new List<string>();
        var local = IsLocalDataSource(dataSource);
        var prefixed = IsSafeDisposableDatabaseName(databaseName);
        if (!local) reasons.Add("BLOCKED_NON_LOCAL_SQL_DATASOURCE");
        if (!prefixed) reasons.Add("BLOCKED_DATABASE_NAME_PREFIX_OR_CHARSET");
        if (!createDisposableDb) reasons.Add("BLOCKED_CREATE_DISPOSABLE_DB_REQUIRED");
        if (dropAfterExport && !prefixed) reasons.Add("BLOCKED_DROP_REFUSES_NON_M1D_PREFIX");

        return new R018StageConnectionGateReport(
            reasons.Count == 0 ? "PASS" : "FAIL",
            local,
            prefixed,
            createDisposableDb,
            !dropAfterExport || prefixed,
            dataSource,
            databaseName,
            reasons);
    }

    public static bool IsSafeDisposableDatabaseName(string databaseName)
        => !string.IsNullOrWhiteSpace(databaseName) &&
           databaseName.Length > R018StageLocalDbReplayConstants.DisposableDatabasePrefix.Length &&
           databaseName.StartsWith(R018StageLocalDbReplayConstants.DisposableDatabasePrefix, StringComparison.Ordinal) &&
           SafeDbName.IsMatch(databaseName);

    public static bool IsLocalDataSource(string dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource)) return false;
        var value = dataSource.Trim();
        var lower = value.ToLowerInvariant();
        if (lower.Contains("amazonaws", StringComparison.Ordinal) || lower.Contains("rds", StringComparison.Ordinal)) return false;
        if (lower.StartsWith("(localdb)\\", StringComparison.Ordinal)) return true;
        if (lower is "." or "(local)" or "localhost" or "127.0.0.1") return true;
        if (lower.StartsWith(".\\", StringComparison.Ordinal)) return true;
        if (lower.StartsWith("(local)\\", StringComparison.Ordinal)) return true;
        if (lower.StartsWith("localhost\\", StringComparison.Ordinal)) return true;
        if (lower.StartsWith("127.0.0.1\\", StringComparison.Ordinal)) return true;
        if (lower.StartsWith("localhost,", StringComparison.Ordinal)) return true;
        if (lower.StartsWith("127.0.0.1,", StringComparison.Ordinal)) return true;
        return false;
    }
}

public static class R018StageReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = R018StageLocalDbReplayLoader.CreateJsonOptions();

    public static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static void WriteParityCsv(string path, IEnumerable<R018StageInputParityRow> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var lines = new List<string> { "check,source,expected,actual,status,severity,detail" };
        lines.AddRange(rows.Select(x => string.Join(',', Escape(x.Check), Escape(x.Source), Escape(x.Expected), Escape(x.Actual), Escape(x.Status), Escape(x.Severity), Escape(x.Detail))));
        File.WriteAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static void WriteGateMarkdown(string path, R018StageEntryGateReport gate, R018StageConnectionGateReport? connection, string finalStatus)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var builder = new StringBuilder();
        builder.AppendLine("# M1D Stage-Only Local Isolated DB Replay Gate");
        builder.AppendLine();
        builder.AppendLine($"- final_status: `{finalStatus}`");
        builder.AppendLine($"- entry_gate_status: `{gate.Status}`");
        builder.AppendLine($"- can_import: `{gate.CanImport}`");
        builder.AppendLine("- live_run: `false`");
        builder.AppendLine("- fix_logon: `false`");
        builder.AppendLine("- broker_traffic: `false`");
        builder.AppendLine("- account_api: `false`");
        builder.AppendLine("- databento_api: `false`");
        builder.AppendLine("- r009_used: `false`");
        builder.AppendLine("- canonical_db_apply: `false`");
        if (connection is not null)
        {
            builder.AppendLine($"- sql_datasource: `{connection.DataSource}`");
            builder.AppendLine($"- database_name: `{connection.DatabaseName}`");
            builder.AppendLine($"- connection_gate: `{connection.Status}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Critical Failures");
        var failures = gate.Checks.Where(x => x.Status == "FAIL" && x.Severity == "CRITICAL").ToArray();
        if (failures.Length == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var failure in failures)
            {
                builder.AppendLine($"- `{failure.Check}`: expected `{failure.Expected}`, actual `{failure.Actual}`");
            }
        }

        File.WriteAllText(path, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string Escape(string value)
        => value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal)
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
}
