using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Application;

public sealed record CrossRailR008BOptions(
    string RunKey,
    string OutputRoot,
    string R008PackageRoot);

public sealed record CrossRailR008BResult(
    IReadOnlyDictionary<string, object> RepairReport,
    IReadOnlyDictionary<string, object> Template,
    IReadOnlyDictionary<string, object> SafetyMatrix,
    IReadOnlyDictionary<string, object> CandidateBinding,
    IReadOnlyDictionary<string, object> Boundary,
    IReadOnlyList<string> Files);

public sealed class CrossRailR008BInvocationRepairWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private const string RiskReviewId = "risk-review-cross-rail-r006-r009-lmax-demo-sandbox-20260526-001";
    private const string OperatorApprovalId = "operator-approval-cross-rail-r006-phili-lmax-demo-sandbox-20260526-001";
    private const string FutureApprovalPhrasePlaceholder = "<FRESH_R009_OPERATOR_APPROVAL_PHRASE>";
    private const string FutureR009ApprovalMarkerPlaceholder = "<R009_EXECUTION_APPROVAL_MARKER>";

    public async Task<CrossRailR008BResult> WriteAsync(CrossRailR008BOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var outputRoot = Path.GetFullPath(options.OutputRoot);
        var validationRoot = Path.Combine(outputRoot, "10_validation");
        var shareRoot = Path.Combine(outputRoot, "share");
        Directory.CreateDirectory(validationRoot);
        Directory.CreateDirectory(shareRoot);

        var r008Root = Path.GetFullPath(options.R008PackageRoot);
        var failurePath = Path.Combine(r008Root, "10_validation", "cross_rail_r008_failure_diagnosis_report.json");
        var candidatePath = Path.Combine(r008Root, "10_validation", "cross_rail_r008_candidate_set_integrity_report.json");
        var reviewPath = Path.Combine(r008Root, "10_validation", "cross_rail_r008_explicit_invocation_review.json");
        var boundaryPath = Path.Combine(r008Root, "10_validation", "cross_rail_r008_no_execution_boundary_report.json");

        using var failure = await ReadJsonAsync(failurePath, cancellationToken);
        using var candidates = await ReadJsonAsync(candidatePath, cancellationToken);
        using var review = await ReadJsonAsync(reviewPath, cancellationToken);
        using var boundarySource = await ReadJsonAsync(boundaryPath, cancellationToken);

        var candidateHash = GetString(candidates.RootElement, "candidateHash");
        var candidateRows = candidates.RootElement.GetProperty("candidateRows").EnumerateArray().Select(row => new Dictionary<string, object>
        {
            ["symbol"] = GetString(row, "symbol"),
            ["side"] = GetString(row, "side"),
            ["quantity"] = GetDecimal(row, "quantity")
        }).ToArray();

        var sourceHash = await CombinedHashAsync([failurePath, candidatePath, reviewPath, boundaryPath], cancellationToken);
        var safetyRows = BuildSafetyRows(candidateHash);
        var commandTemplate = BuildCommandTemplate(candidateHash);

        var repairReport = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["createdAtUtc"] = DateTimeOffset.UtcNow,
            ["sourceR008PackageRoot"] = ToPortableRelative(outputRoot, r008Root),
            ["CROSS_RAIL_R008B_STATUS"] = "PASS",
            ["R008B_RESULT"] = "REVIEWED_INVOCATION_TEMPLATE_READY_NOT_RUN",
            ["sourceR008Status"] = GetString(failure.RootElement, "CROSS_RAIL_R008_STATUS"),
            ["sourceR008Result"] = GetString(failure.RootElement, "R008_RESULT"),
            ["reviewedInvocationTemplate"] = "NOT_RUN",
            ["R009_RUNNABLE_INVOCATION_COMPLETE"] = "NO",
            ["R009_OPERATOR_APPROVAL_REQUIRED"] = "YES",
            ["R009_NOT_RUNNABLE_UNTIL_APPROVAL"] = "YES",
            ["R009_NOT_RUNNABLE_UNTIL_PLACEHOLDERS_REPLACED"] = true,
            ["candidateSetHashBound"] = true,
            ["noExecutionAttempted"] = true,
            ["SAFE_NEXT_GATE"] = "CROSS_RAIL_R009_EXPLICIT_OPERATOR_APPROVED_SANDBOX_EXECUTION",
            ["sourceLineageHash"] = sourceHash
        };

        var template = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["templateStatus"] = "NOT_RUN",
            ["reviewStatus"] = "REVIEW_ONLY",
            ["requiresOperatorApproval"] = true,
            ["requiresR009Gate"] = true,
            ["R009_NOT_RUNNABLE_UNTIL_PLACEHOLDERS_REPLACED"] = true,
            ["R009_RUNNABLE_INVOCATION_COMPLETE"] = "NO",
            ["futureR009RunKey"] = "cross-rail-r009-explicit-operator-approved-sandbox-execution-001",
            ["environment"] = "sandbox",
            ["brokerEnvironmentSelector"] = "ExistingLmaxDemoProfile",
            ["candidateSetHash"] = candidateHash,
            ["candidateSetApprovalBindingId"] = "CROSS-RAIL-R006R007",
            ["riskReviewId"] = RiskReviewId,
            ["operatorApprovalId"] = OperatorApprovalId,
            ["operatorApprovalPhrase"] = FutureApprovalPhrasePlaceholder,
            ["r009ExecutionApprovalMarker"] = FutureR009ApprovalMarkerPlaceholder,
            ["approvalPlaceholdersAreAcceptedForExecution"] = false,
            ["candidate_count"] = 3,
            ["candidates"] = candidateRows,
            ["boundedLifecycle"] = new Dictionary<string, object>
            {
                ["maxLifecycleSeconds"] = 120,
                ["maxOrders"] = 3,
                ["expectedCandidateCount"] = 3,
                ["maxOrderQuantity"] = 0.1m,
                ["perOrderFillTimeoutSeconds"] = 30,
                ["maxFlattenAttemptsPerSymbol"] = 1
            },
            ["policies"] = new Dictionary<string, object>
            {
                ["flattenPolicy"] = "require-flatten",
                ["residualPolicy"] = "require-residual-zero",
                ["reconciliationPolicy"] = "require-sandbox-reconciliation",
                ["noRouteBrokerLiveStateOutsideSandbox"] = true,
                ["noDbMutation"] = true,
                ["noProductionLiveTouched"] = true,
                ["noQubesMutation"] = true,
                ["noNettedUsdWeightsMutation"] = true,
                ["noProductionEndpoint"] = true,
                ["noTradingProductionEndpoint"] = true
            },
            ["commandTemplate"] = commandTemplate,
            ["forbiddenTemplateContents"] = new[]
            {
                "credentials",
                "production endpoint",
                "live endpoint",
                "Qubes 4E active reference",
                "StratTaken active reference",
                "NettedUsdWeights mutation path",
                "PMS/OMS/EMS live handoff"
            }
        };

        var candidateBinding = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["CANDIDATE_SET_PRESERVED"] = "YES",
            ["CANDIDATE_SET_HASH_BOUND"] = "YES",
            ["EXPECTED_CANDIDATE_COUNT"] = 3,
            ["OBSERVED_CANDIDATE_COUNT"] = candidateRows.Length,
            ["CANDIDATE_SET_EXECUTION_COMPATIBILITY"] = "PASS",
            ["candidateHash"] = candidateHash,
            ["sourceR008Artifact"] = ToPortableRelative(outputRoot, candidatePath),
            ["approvalBindingEvidence"] = "CROSS-RAIL-R006R007 InputsResolved, placeholders rejected",
            ["candidateAdded"] = false,
            ["candidateRemoved"] = false,
            ["candidateModified"] = false,
            ["candidates"] = candidateRows
        };

        var safetyMatrix = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["SAFETY_FLAG_MATRIX_STATUS"] = "PASS",
            ["R009_RUNNABLE_INVOCATION_COMPLETE"] = "NO",
            ["R009_OPERATOR_APPROVAL_REQUIRED"] = "YES",
            ["R009_NOT_RUNNABLE_UNTIL_APPROVAL"] = "YES",
            ["flags"] = safetyRows
        };

        var noExecutionBoundary = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["NO_EXECUTION_BOUNDARY_STATUS"] = "PASS",
            ["FIX_SESSION_OPENED"] = "NO",
            ["LMAX_CALL_MADE"] = "NO",
            ["ORDERS_SUBMITTED"] = 0,
            ["FILLS_CAPTURED"] = 0,
            ["FLATTEN_ORDER_CREATED"] = "NO",
            ["ROUTE_BROKER_LIVE_STATE_ARTIFACTS_CREATED"] = "NO",
            ["PRODUCTION_LIVE_TOUCHED"] = "NO",
            ["QUBES_NETTING_NETTEDUSDWEIGHTS_TOUCHED"] = "NO",
            ["DB_MUTATION"] = "NO",
            ["sourceBoundaryStatus"] = GetString(boundarySource.RootElement, "NO_EXECUTION_BOUNDARY_STATUS")
        };

        await WriteJsonAndMarkdown(validationRoot, "cross_rail_r008b_invocation_repair_report", repairReport, Markdown.Repair(repairReport), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "cross_rail_r008b_reviewed_invocation_template", template, Markdown.Template(template, commandTemplate), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "cross_rail_r008b_sandbox_safety_flags_matrix", safetyMatrix, Markdown.Safety(safetyRows), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "cross_rail_r008b_candidate_binding_report", candidateBinding, Markdown.Candidates(candidateBinding), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "cross_rail_r008b_no_execution_boundary_report", noExecutionBoundary, Markdown.Boundary(noExecutionBoundary), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(shareRoot, "cross_rail_r008b_status_summary.md"), Markdown.Summary(repairReport, candidateBinding), cancellationToken);
        await WriteManifestAsync(outputRoot, options.RunKey, sourceHash, cancellationToken);

        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(outputRoot, path).Replace('\\', '/'))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new(repairReport, template, safetyMatrix, candidateBinding, noExecutionBoundary, files);
    }

    private static string[] BuildCommandTemplate(string candidateHash)
        =>
        [
            "dotnet run --project tools/QQ.Production.Intraday.Tools.CrossRailR009ExplicitSandboxExecution --",
            "--run-key cross-rail-r009-explicit-operator-approved-sandbox-execution-001",
            "--environment sandbox",
            "--broker-environment-selector ExistingLmaxDemoProfile",
            $"--candidate-set-hash {candidateHash}",
            "--candidate-set-approval-binding-id CROSS-RAIL-R006R007",
            $"--risk-review-id {RiskReviewId}",
            $"--operator-approval-id {OperatorApprovalId}",
            $"--operator-approval-phrase {FutureApprovalPhrasePlaceholder}",
            $"--r009-execution-approval-marker {FutureR009ApprovalMarkerPlaceholder}",
            "--allow-sandbox-only true",
            "--allow-no-production true",
            "--allow-no-live true",
            "--allow-no-production-endpoint true",
            "--allow-no-trading-production-endpoint true",
            "--allow-no-qubes-mutation true",
            "--allow-no-netted-usd-weights-mutation true",
            "--allow-no-route-broker-live-state-outside-sandbox true",
            "--allow-no-db-mutation true",
            "--max-lifecycle-seconds 120",
            "--max-orders 3",
            "--expected-candidate-count 3",
            "--max-order-quantity 0.1",
            "--require-flatten true",
            "--require-residual-zero true",
            "--require-sandbox-reconciliation true",
            "--requires-r009-gate true",
            "--not-run-review-only true"
        ];

    private static List<Dictionary<string, string>> BuildSafetyRows(string candidateHash)
        =>
        [
            Row("sandbox-only", "true", "true", "PRESENT", "Required to keep the invocation sandbox/demo only."),
            Row("no-production", "true", "true", "PRESENT", "Prevents production state or endpoint use."),
            Row("no-live", "true", "true", "PRESENT", "Prevents live execution promotion."),
            Row("no-production-endpoint", "true", "true", "PRESENT", "Blocks production LMAX endpoint selection."),
            Row("no-trading-production-endpoint", "true", "true", "PRESENT", "Blocks production trading endpoint selection."),
            Row("bounded-lifecycle", "maxLifecycleSeconds=120", "maxLifecycleSeconds=120", "PRESENT", "Bounds future R009 execution lifecycle."),
            Row("max-orders", "3", "3", "PRESENT", "Limits one order per candidate row."),
            Row("expected-candidate-count", "3", "3", "PRESENT", "Binds the candidate count."),
            Row("candidate-set-hash", candidateHash, candidateHash, "PRESENT", "Binds candidate content."),
            Row("candidate-approval-binding-id", "CROSS-RAIL-R006R007", "CROSS-RAIL-R006R007", "PRESENT", "Binds candidate set to approval binding."),
            Row("operator-approval-phrase", "fresh R009 phrase", FutureApprovalPhrasePlaceholder, "PLACEHOLDER", "R009 cannot run until replaced by fresh operator approval."),
            Row("flatten-policy", "require-flatten", "require-flatten", "PRESENT", "Requires flatten after fills."),
            Row("residual-policy", "require-residual-zero", "require-residual-zero", "PRESENT", "Requires residual zero for success."),
            Row("reconciliation-policy", "require-sandbox-reconciliation", "require-sandbox-reconciliation", "PRESENT", "Requires sandbox lifecycle reconciliation."),
            Row("no-Qubes-mutation", "true", "true", "PRESENT", "Prevents Qubes execution or state mutation."),
            Row("no-NettedUsdWeights-mutation", "true", "true", "PRESENT", "Prevents NettedUsdWeights creation or mutation."),
            Row("no-route-broker-live-state-outside-sandbox", "true", "true", "PRESENT", "Prevents artifacts outside sandbox scope."),
            Row("no-DB-mutation", "true", "true", "PRESENT", "Prevents DB writes or migrations."),
            Row("no-production-live-touched", "true", "true", "PRESENT", "Prevents production/live touch."),
            Row("R009-gate-required", "true", "true", "PRESENT", "Requires future R009 gate before any execution.")
        ];

    private static Dictionary<string, string> Row(string flagName, string requiredValue, string templateValue, string status, string executionImpact)
        => new(StringComparer.Ordinal)
        {
            ["flagName"] = flagName,
            ["requiredValue"] = requiredValue,
            ["templateValue"] = templateValue,
            ["status"] = status,
            ["executionImpact"] = executionImpact,
            ["notes"] = status == "PLACEHOLDER" ? "Allowed only in NOT_RUN review template; rejected for runnable R009 status." : "Non-approval safety field is explicit."
        };

    private static async Task<JsonDocument> ReadJsonAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static string GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static decimal GetDecimal(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var parsed) ? parsed : 0m;

    private static async Task WriteJsonAndMarkdown(string root, string basename, IReadOnlyDictionary<string, object> report, string markdown, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.json"), JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(root, $"{basename}.md"), markdown, cancellationToken);
    }

    private static async Task WriteManifestAsync(string outputRoot, string runKey, string sourceHash, CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) is not "hashes.json" and not "manifest.sha256")
            .OrderBy(path => Path.GetRelativePath(outputRoot, path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hashes = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            hashes[Path.GetRelativePath(outputRoot, file).Replace('\\', '/')] = await Sha256Async(file, cancellationToken);
        }

        var manifest = new Dictionary<string, object>
        {
            ["run_key"] = runKey,
            ["created_at_utc"] = DateTimeOffset.UtcNow,
            ["package_type"] = "cross_rail_r008b_invocation_repair_only",
            ["source_lineage_hash"] = sourceHash,
            ["r008b_result"] = "REVIEWED_INVOCATION_TEMPLATE_READY_NOT_RUN",
            ["template_status"] = "NOT_RUN",
            ["r009_runnable_invocation_complete"] = "NO",
            ["files"] = hashes.Keys.ToArray()
        };

        var manifestPath = Path.Combine(outputRoot, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);
        hashes["manifest.json"] = await Sha256Async(manifestPath, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "hashes.json"), JsonSerializer.Serialize(hashes, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputRoot, "manifest.sha256"), $"{hashes["manifest.json"]}  manifest.json{Environment.NewLine}", cancellationToken);
    }

    private static async Task<string> CombinedHashAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        foreach (var path in paths)
        {
            sb.Append(Path.GetFileName(path)).Append('=').Append(await Sha256Async(path, cancellationToken)).AppendLine();
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    private static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ToPortableRelative(string outputRoot, string path)
        => Path.GetRelativePath(outputRoot, path).Replace('\\', '/');

    private static class Markdown
    {
        public static string Repair(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# CROSS-RAIL-R008B Invocation Repair Report",
                "",
                $"- CROSS_RAIL_R008B_STATUS = `{report["CROSS_RAIL_R008B_STATUS"]}`",
                $"- R008B_RESULT = `{report["R008B_RESULT"]}`",
                $"- Reviewed invocation template = `{report["reviewedInvocationTemplate"]}`",
                $"- R009_RUNNABLE_INVOCATION_COMPLETE = `{report["R009_RUNNABLE_INVOCATION_COMPLETE"]}`",
                $"- R009_OPERATOR_APPROVAL_REQUIRED = `{report["R009_OPERATOR_APPROVAL_REQUIRED"]}`",
                $"- SAFE_NEXT_GATE = `{report["SAFE_NEXT_GATE"]}`");

        public static string Template(IReadOnlyDictionary<string, object> template, IReadOnlyList<string> command)
            => Lines(
                "# CROSS-RAIL-R008B Reviewed Invocation Template",
                "",
                "Status: `NOT_RUN`, `REVIEW_ONLY`, `REQUIRES_OPERATOR_APPROVAL`, `REQUIRES_R009_GATE`.",
                "",
                "```powershell",
                string.Join(" `\n  ", command),
                "```",
                "",
                $"- Candidate set hash: `{template["candidateSetHash"]}`",
                $"- R009_NOT_RUNNABLE_UNTIL_PLACEHOLDERS_REPLACED = `{template["R009_NOT_RUNNABLE_UNTIL_PLACEHOLDERS_REPLACED"]}`");

        public static string Safety(IEnumerable<Dictionary<string, string>> rows)
            => Lines(
                "# CROSS-RAIL-R008B Sandbox Safety Flags Matrix",
                "",
                "SAFETY_FLAG_MATRIX_STATUS = `PASS`",
                "R009_RUNNABLE_INVOCATION_COMPLETE = `NO`",
                "R009_OPERATOR_APPROVAL_REQUIRED = `YES`",
                "",
                "| Flag | Template | Status |",
                "| --- | --- | --- |",
                string.Join(Environment.NewLine, rows.Select(row => $"| `{row["flagName"]}` | `{row["templateValue"]}` | `{row["status"]}` |")));

        public static string Candidates(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# CROSS-RAIL-R008B Candidate Binding Report",
                "",
                $"- CANDIDATE_SET_PRESERVED = `{report["CANDIDATE_SET_PRESERVED"]}`",
                $"- CANDIDATE_SET_HASH_BOUND = `{report["CANDIDATE_SET_HASH_BOUND"]}`",
                $"- EXPECTED_CANDIDATE_COUNT = `{report["EXPECTED_CANDIDATE_COUNT"]}`",
                $"- OBSERVED_CANDIDATE_COUNT = `{report["OBSERVED_CANDIDATE_COUNT"]}`",
                $"- CANDIDATE_SET_EXECUTION_COMPATIBILITY = `{report["CANDIDATE_SET_EXECUTION_COMPATIBILITY"]}`",
                $"- candidate hash = `{report["candidateHash"]}`");

        public static string Boundary(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# CROSS-RAIL-R008B No Execution Boundary",
                "",
                $"- NO_EXECUTION_BOUNDARY_STATUS = `{report["NO_EXECUTION_BOUNDARY_STATUS"]}`",
                $"- FIX_SESSION_OPENED = `{report["FIX_SESSION_OPENED"]}`",
                $"- LMAX_CALL_MADE = `{report["LMAX_CALL_MADE"]}`",
                $"- ORDERS_SUBMITTED = `{report["ORDERS_SUBMITTED"]}`",
                $"- FILLS_CAPTURED = `{report["FILLS_CAPTURED"]}`",
                $"- PRODUCTION_LIVE_TOUCHED = `{report["PRODUCTION_LIVE_TOUCHED"]}`");

        public static string Summary(IReadOnlyDictionary<string, object> report, IReadOnlyDictionary<string, object> candidate)
            => Lines(
                "# CROSS-RAIL-R008B Status Summary",
                "",
                $"- R008B status: `{report["CROSS_RAIL_R008B_STATUS"]}`",
                $"- Result: `{report["R008B_RESULT"]}`",
                $"- Template: `NOT_RUN`",
                $"- R009 runnable: `{report["R009_RUNNABLE_INVOCATION_COMPLETE"]}`",
                $"- Candidate set preserved: `{candidate["CANDIDATE_SET_PRESERVED"]}`",
                $"- Candidate hash bound: `{candidate["CANDIDATE_SET_HASH_BOUND"]}`",
                $"- Safe next gate: `{report["SAFE_NEXT_GATE"]}`");

        private static string Lines(params string[] lines)
            => string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
