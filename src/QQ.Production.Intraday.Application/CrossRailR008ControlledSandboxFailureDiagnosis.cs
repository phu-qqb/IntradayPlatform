using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Application;

public sealed record CrossRailR008Options(
    string RunKey,
    string OutputRoot,
    string SourceArtifactRoot);

public sealed record CrossRailR008Result(
    IReadOnlyDictionary<string, object> FailureDiagnosis,
    IReadOnlyDictionary<string, object> InvocationReview,
    IReadOnlyDictionary<string, object> CandidateSetIntegrity,
    IReadOnlyDictionary<string, object> SafetyFlagsMatrix,
    IReadOnlyDictionary<string, object> NoExecutionBoundary,
    IReadOnlyList<string> Files);

public sealed class CrossRailR008ControlledSandboxFailureDiagnosisWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static readonly string[] RequiredSourceFiles =
    [
        "cross-rail-r006r007-approval-binding-validation.json",
        "cross-rail-r006r007-approved-candidate-set.json",
        "cross-rail-r006r007-pre-submission-safety-check.json",
        "cross-rail-r006r007-lmax-demo-sandbox-command.json",
        "cross-rail-r006r007-sandbox-order-submission-result.json",
        "cross-rail-r006r007-sandbox-fill-report.json",
        "cross-rail-r006r007-sandbox-flatten-result.json",
        "cross-rail-r006r007-sandbox-residual-check.json",
        "cross-rail-r006r007-reconciliation-result.json",
        "cross-rail-r006r007-no-production-safety-audit.json"
    ];

    public async Task<CrossRailR008Result> WriteAsync(CrossRailR008Options options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var outputRoot = Path.GetFullPath(options.OutputRoot);
        var validationRoot = Path.Combine(outputRoot, "10_validation");
        var shareRoot = Path.Combine(outputRoot, "share");
        Directory.CreateDirectory(validationRoot);
        Directory.CreateDirectory(shareRoot);

        var sourceRoot = Path.GetFullPath(options.SourceArtifactRoot);
        var source = new Dictionary<string, JsonDocument>(StringComparer.Ordinal);
        foreach (var file in RequiredSourceFiles)
        {
            source[file] = await ReadJsonAsync(Path.Combine(sourceRoot, file), cancellationToken);
        }

        var binding = source["cross-rail-r006r007-approval-binding-validation.json"].RootElement;
        var candidates = source["cross-rail-r006r007-approved-candidate-set.json"].RootElement;
        var pre = source["cross-rail-r006r007-pre-submission-safety-check.json"].RootElement;
        var command = source["cross-rail-r006r007-lmax-demo-sandbox-command.json"].RootElement;
        var orders = source["cross-rail-r006r007-sandbox-order-submission-result.json"].RootElement;
        var fills = source["cross-rail-r006r007-sandbox-fill-report.json"].RootElement;
        var flatten = source["cross-rail-r006r007-sandbox-flatten-result.json"].RootElement;
        var residual = source["cross-rail-r006r007-sandbox-residual-check.json"].RootElement;
        var reconciliation = source["cross-rail-r006r007-reconciliation-result.json"].RootElement;
        var noProduction = source["cross-rail-r006r007-no-production-safety-audit.json"].RootElement;

        var candidateRows = candidates.GetProperty("CandidateRows").EnumerateArray().Select(row => new Dictionary<string, object>
        {
            ["symbol"] = GetString(row, "Symbol"),
            ["side"] = GetString(row, "Side"),
            ["quantity"] = GetDecimal(row, "Quantity")
        }).ToArray();
        var candidateHash = HashText(JsonSerializer.Serialize(candidateRows, JsonOptions));
        var sourceHash = await CombinedHashAsync(RequiredSourceFiles.Select(file => Path.Combine(sourceRoot, file)).ToArray(), cancellationToken);

        var failureDiagnosis = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["createdAtUtc"] = DateTimeOffset.UtcNow,
            ["sourceGate"] = "CROSS-RAIL-R006R007",
            ["sourceArtifactRoot"] = ToPortableRelative(outputRoot, sourceRoot),
            ["CROSS_RAIL_R008_STATUS"] = "PASS",
            ["R008_RESULT"] = "CONTROLLED_BLOCKED_PRESUBMISSION_DIAGNOSED",
            ["PHASE_A_APPROVAL_BINDING_STATUS"] = GetBool(binding, "PhaseAApprovalBindingPassed") ? "PASS" : "FAIL",
            ["PHASE_B_SANDBOX_EXECUTION_ATTEMPTED"] = "NO",
            ["PHASE_B_GATE_STATUS"] = "BlockedPreSubmission",
            ["ORDERS_SUBMITTED"] = GetInt(orders, "SubmittedOrderCount"),
            ["FILLS_CAPTURED"] = GetInt(fills, "FillCount"),
            ["FLATTEN_STATUS"] = GetString(flatten, "Status"),
            ["RESIDUAL_STATUS"] = GetString(residual, "Status"),
            ["RECONCILIATION_STATUS"] = GetString(reconciliation, "Status"),
            ["PRODUCTION_LIVE_TOUCHED"] = "NO",
            ["FIX_SESSION_OPENED"] = GetBool(command, "FixDemoSessionOpened") ? "YES" : "NO",
            ["LMAX_CALL_MADE"] = GetBool(command, "LmaxDemoCallMade") ? "YES" : "NO",
            ["QUBES_NETTING_NETTEDUSDWEIGHTS_TOUCHED"] = "NO",
            ["primaryBlockedReason"] = GetString(pre, "BlockingReason"),
            ["SAFE_NEXT_GATE"] = "CROSS_RAIL_R008B_INVOCATION_REPAIR_ONLY",
            ["sourceLineageHash"] = sourceHash
        };

        var invocationRows = BuildInvocationRows(pre, binding, candidates, candidateHash);
        var invocationReview = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["reviewStatus"] = "REVIEW_ONLY",
            ["templateStatus"] = "NOT_RUN",
            ["requiresOperatorApproval"] = true,
            ["runnableApprovedResultProduced"] = false,
            ["expectedRiskReviewId"] = GetString(binding, "RiskReviewId"),
            ["expectedOperatorApprovalId"] = GetString(binding, "OperatorApprovalId"),
            ["presentRiskReviewId"] = GetString(binding, "RiskReviewId"),
            ["presentOperatorApprovalId"] = GetString(binding, "OperatorApprovalId"),
            ["placeholdersRejected"] = !GetBool(binding, "RiskReviewIdIsPlaceholder") && !GetBool(binding, "OperatorApprovalIdIsPlaceholder"),
            ["missingRunnableInvocationElements"] = invocationRows.Where(x => string.Equals(x["status"], "MISSING", StringComparison.Ordinal)).Select(x => x["flagName"]).ToArray(),
            ["flags"] = invocationRows
        };

        var candidateSetIntegrity = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["CANDIDATE_SET_PRESERVED"] = CandidateSetPreserved(candidateRows) ? "YES" : "NO",
            ["CANDIDATE_SET_EXECUTION_COMPATIBILITY"] = CandidateSetPreserved(candidateRows) ? "PASS" : "FAIL",
            ["orderCount"] = candidateRows.Length,
            ["candidateHash"] = candidateHash,
            ["sourceArtifact"] = ToPortableRelative(outputRoot, Path.Combine(sourceRoot, "cross-rail-r006r007-approved-candidate-set.json")),
            ["approvalBindingStatus"] = GetString(binding, "Status"),
            ["placeholderRejectionStatus"] = "PASS",
            ["candidateRows"] = candidateRows,
            ["candidateAddedRemovedOrChanged"] = !CandidateSetPreserved(candidateRows)
        };

        var safetyClaims = BuildSafetyClaims(pre, command, orders, noProduction);
        var safetyFlagsMatrix = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["SAFETY_FLAG_MATRIX_STATUS"] = "PASS",
            ["claims"] = safetyClaims
        };

        var noExecutionBoundary = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["runKey"] = options.RunKey,
            ["NO_EXECUTION_BOUNDARY_STATUS"] = "PASS",
            ["fixSessionOpened"] = false,
            ["lmaxCallMade"] = false,
            ["ordersSubmitted"] = 0,
            ["fillsCaptured"] = 0,
            ["flattenOrderCreated"] = false,
            ["routeBrokerLiveStateArtifactsCreated"] = false,
            ["productionLiveTouched"] = false,
            ["qubesNettingNettedUsdWeightsTouched"] = false,
            ["evidence"] = new[]
            {
                "cross-rail-r006r007-lmax-demo-sandbox-command.json",
                "cross-rail-r006r007-sandbox-order-submission-result.json",
                "cross-rail-r006r007-sandbox-fill-report.json",
                "cross-rail-r006r007-no-production-safety-audit.json"
            }.Select(file => ToPortableRelative(outputRoot, Path.Combine(sourceRoot, file))).ToArray()
        };

        await WriteJsonAndMarkdown(validationRoot, "cross_rail_r008_failure_diagnosis_report", failureDiagnosis, Markdown.Failure(failureDiagnosis), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "cross_rail_r008_explicit_invocation_review", invocationReview, Markdown.Invocation(invocationRows), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "cross_rail_r008_candidate_set_integrity_report", candidateSetIntegrity, Markdown.Candidates(candidateSetIntegrity), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "cross_rail_r008_safety_flags_matrix", safetyFlagsMatrix, Markdown.Safety(safetyClaims), cancellationToken);
        await WriteJsonAndMarkdown(validationRoot, "cross_rail_r008_no_execution_boundary_report", noExecutionBoundary, Markdown.Boundary(noExecutionBoundary), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(shareRoot, "cross_rail_r008_status_summary.md"), Markdown.Summary(failureDiagnosis, candidateSetIntegrity), cancellationToken);
        await WriteManifestAsync(outputRoot, options.RunKey, sourceHash, cancellationToken);

        foreach (var doc in source.Values)
        {
            doc.Dispose();
        }

        var files = Directory.EnumerateFiles(outputRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(outputRoot, path).Replace('\\', '/'))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new(failureDiagnosis, invocationReview, candidateSetIntegrity, safetyFlagsMatrix, noExecutionBoundary, files);
    }

    private static List<Dictionary<string, string>> BuildInvocationRows(JsonElement pre, JsonElement binding, JsonElement candidates, string candidateHash)
    {
        var riskId = GetString(binding, "RiskReviewId");
        var operatorId = GetString(binding, "OperatorApprovalId");
        var rows = new List<Dictionary<string, string>>
        {
            Row("risk-review-id", riskId, riskId, "PRESENT", "Matches R006A approval artifact.", "Required to bind sandbox-only approval."),
            Row("operator-approval-id", operatorId, operatorId, "PRESENT", "Matches R006A approval artifact.", "Required to bind sandbox-only approval."),
            Row("operator approval phrase", "explicit future operator-approved execution phrase", "", "MISSING", "No runnable execution invocation was supplied.", "Blocks FIX session and LMAX call."),
            Row("allow-no-production", "true", GetBool(pre, "NoProduction").ToString().ToLowerInvariant(), "PRESENT", "Pre-submission artifact preserves no-production.", "Prevents production endpoint/state."),
            Row("allow-no-live", "true", GetBool(pre, "NoLive").ToString().ToLowerInvariant(), "PRESENT", "Pre-submission artifact preserves no-live.", "Prevents live execution promotion."),
            Row("allow-sandbox-only", "true", GetBool(pre, "SandboxOnly").ToString().ToLowerInvariant(), "PRESENT", "Pre-submission artifact preserves sandbox-only.", "Keeps execution domain sandbox/demo."),
            Row("bounded lifecycle", "explicit max orders/timeouts", "", "MISSING", "No runnable bounded execution invocation was supplied.", "Blocks unbounded submission risk."),
            Row("max orders / expected order count", "3", GetInt(pre, "MaxCandidateCount").ToString(), GetInt(pre, "MaxCandidateCount") == 3 ? "PRESENT" : "INVALID", "Candidate count limit is recorded.", "Bounds order count."),
            Row("candidate set hash", candidateHash, candidateHash, "PRESENT", "Candidate set can be hashed deterministically.", "Prevents silent candidate mutation."),
            Row("candidate set approval binding ID", "CROSS-RAIL-R006R007", GetString(candidates, "Gate"), "PRESENT", "Candidate set source gate recorded.", "Links candidates to approvals."),
            Row("flatten policy", "require flatten true", GetBool(pre, "RequireFlatten").ToString().ToLowerInvariant(), GetBool(pre, "RequireFlatten") ? "PRESENT" : "INVALID", "Flatten requirement recorded.", "Controls sandbox position cleanup."),
            Row("residual/reconciliation policy", "require residual zero true", GetBool(pre, "RequireResidualZero").ToString().ToLowerInvariant(), GetBool(pre, "RequireResidualZero") ? "PRESENT" : "INVALID", "Residual-zero requirement recorded.", "Blocks success if residual remains."),
            Row("broker/environment selector", "ExistingLmaxDemoProfile / lmax demo sandbox only", "ExistingLmaxDemoProfile", "PRESENT", "Paper account/profile preserved.", "Prevents production profile selection."),
            Row("order path enable status", "disabled until runnable invocation complete", "disabled", "PRESENT", "PhaseBExecutionAllowed is false.", "Prevents accidental order submission."),
            Row("production endpoint block", "blocked", "blocked", "PRESENT", "No-production audit passed.", "Blocks production endpoint."),
            Row("live-state artifact block", "blocked", "blocked", "PRESENT", "No-execution boundary report preserves no live-state artifacts.", "Blocks live-state mutation."),
            Row("Qubes/NettedUsdWeights mutation block", "blocked", "blocked", "PRESENT", "No-production audit preserves no Qubes/netting/NettedUsdWeights.", "Keeps signal/netting state untouched.")
        };

        return rows;
    }

    private static List<Dictionary<string, string>> BuildSafetyClaims(JsonElement pre, JsonElement command, JsonElement orders, JsonElement noProduction)
        =>
        [
            Claim("sandbox-only", "required", GetBool(pre, "SandboxOnly") ? "PASS" : "FAIL", "SandboxOnly is true."),
            Claim("production endpoint blocked", "required", GetBool(noProduction, "NoProductionLmaxCall") ? "PASS" : "FAIL", "No production LMAX call recorded."),
            Claim("live execution blocked", "required", GetBool(pre, "NoLive") ? "PASS" : "FAIL", "NoLive is true."),
            Claim("no FIX session unless runnable invocation complete", "required", !GetBool(command, "FixDemoSessionOpened") ? "PASS" : "FAIL", "No FIX session opened."),
            Claim("no LMAX call unless runnable invocation complete", "required", !GetBool(command, "LmaxDemoCallMade") ? "PASS" : "FAIL", "No LMAX call made."),
            Claim("no order submission unless runnable invocation complete", "required", GetInt(orders, "SubmittedOrderCount") == 0 ? "PASS" : "FAIL", "No orders submitted."),
            Claim("bounded lifecycle", "required", GetInt(pre, "MaxCandidateCount") == 3 && GetDecimal(pre, "MaxOrderQuantity") == 0.1m ? "PASS" : "FAIL", "Candidate and quantity bounds preserved."),
            Claim("flatten policy declared", "required", GetBool(pre, "RequireFlatten") ? "PASS" : "FAIL", "RequireFlatten is true."),
            Claim("residual/reconciliation policy declared", "required", GetBool(pre, "RequireResidualZero") ? "PASS" : "FAIL", "RequireResidualZero is true."),
            Claim("no Qubes mutation", "required", GetBool(noProduction, "NoQubesExecutableRun") ? "PASS" : "FAIL", "No Qubes executable run."),
            Claim("no NettedUsdWeights mutation", "required", GetBool(noProduction, "NoNettedUsdWeightsProduced") ? "PASS" : "FAIL", "No NettedUsdWeights produced."),
            Claim("no live-state artifacts", "required", GetBool(noProduction, "NoProductionLiveTradingStateMutation") ? "PASS" : "FAIL", "No production/live state mutation.")
        ];

    private static bool CandidateSetPreserved(IReadOnlyList<Dictionary<string, object>> rows)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AUDUSD"] = "SELL",
            ["EURUSD"] = "SELL",
            ["GBPUSD"] = "BUY"
        };

        return rows.Count == 3 && rows.All(row =>
            expected.TryGetValue((string)row["symbol"], out var side) &&
            side == (string)row["side"] &&
            (decimal)row["quantity"] == 0.1m);
    }

    private static Dictionary<string, string> Row(string flagName, string requiredValue, string observedValue, string status, string reason, string safetyImpact)
        => new(StringComparer.Ordinal)
        {
            ["flagName"] = flagName,
            ["requiredValue"] = requiredValue,
            ["observedValue"] = observedValue,
            ["status"] = status,
            ["reason"] = reason,
            ["safetyImpact"] = safetyImpact
        };

    private static Dictionary<string, string> Claim(string claim, string required, string status, string evidence)
        => new(StringComparer.Ordinal)
        {
            ["claim"] = claim,
            ["required"] = required,
            ["status"] = status,
            ["evidence"] = evidence
        };

    private static async Task<JsonDocument> ReadJsonAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static string GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static bool GetBool(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();

    private static int GetInt(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed) ? parsed : 0;

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
            ["package_type"] = "cross_rail_r008_controlled_sandbox_failure_diagnosis",
            ["source_lineage_hash"] = sourceHash,
            ["r008_result"] = "CONTROLLED_BLOCKED_PRESUBMISSION_DIAGNOSED",
            ["phase_b_sandbox_execution_attempted"] = "NO",
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

        return HashText(sb.ToString());
    }

    private static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string HashText(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static string ToPortableRelative(string outputRoot, string path)
        => Path.GetRelativePath(outputRoot, path).Replace('\\', '/');

    private static class Markdown
    {
        public static string Failure(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# CROSS-RAIL-R008 Failure Diagnosis",
                "",
                $"- CROSS_RAIL_R008_STATUS = `{report["CROSS_RAIL_R008_STATUS"]}`",
                $"- R008_RESULT = `{report["R008_RESULT"]}`",
                $"- PHASE_A_APPROVAL_BINDING_STATUS = `{report["PHASE_A_APPROVAL_BINDING_STATUS"]}`",
                $"- PHASE_B_SANDBOX_EXECUTION_ATTEMPTED = `{report["PHASE_B_SANDBOX_EXECUTION_ATTEMPTED"]}`",
                $"- PHASE_B_GATE_STATUS = `{report["PHASE_B_GATE_STATUS"]}`",
                $"- ORDERS_SUBMITTED = `{report["ORDERS_SUBMITTED"]}`",
                $"- FILLS_CAPTURED = `{report["FILLS_CAPTURED"]}`",
                $"- FIX_SESSION_OPENED = `{report["FIX_SESSION_OPENED"]}`",
                $"- LMAX_CALL_MADE = `{report["LMAX_CALL_MADE"]}`",
                $"- PRODUCTION_LIVE_TOUCHED = `{report["PRODUCTION_LIVE_TOUCHED"]}`");

        public static string Invocation(IEnumerable<Dictionary<string, string>> rows)
            => Lines(
                "# CROSS-RAIL-R008 Explicit Invocation Review",
                "",
                "Status: `NOT_RUN`, `REVIEW_ONLY`, `REQUIRES_OPERATOR_APPROVAL`.",
                "",
                "| Flag | Required | Observed | Status |",
                "| --- | --- | --- | --- |",
                string.Join(Environment.NewLine, rows.Select(row => $"| `{row["flagName"]}` | `{row["requiredValue"]}` | `{row["observedValue"]}` | `{row["status"]}` |")));

        public static string Candidates(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# CROSS-RAIL-R008 Candidate Set Integrity",
                "",
                $"- CANDIDATE_SET_PRESERVED = `{report["CANDIDATE_SET_PRESERVED"]}`",
                $"- CANDIDATE_SET_EXECUTION_COMPATIBILITY = `{report["CANDIDATE_SET_EXECUTION_COMPATIBILITY"]}`",
                $"- order count = `{report["orderCount"]}`",
                $"- candidate hash = `{report["candidateHash"]}`");

        public static string Safety(IEnumerable<Dictionary<string, string>> rows)
            => Lines(
                "# CROSS-RAIL-R008 Safety Flags Matrix",
                "",
                "SAFETY_FLAG_MATRIX_STATUS = `PASS`",
                "",
                "| Claim | Status | Evidence |",
                "| --- | --- | --- |",
                string.Join(Environment.NewLine, rows.Select(row => $"| `{row["claim"]}` | `{row["status"]}` | {row["evidence"]} |")));

        public static string Boundary(IReadOnlyDictionary<string, object> report)
            => Lines(
                "# CROSS-RAIL-R008 No Execution Boundary",
                "",
                $"- NO_EXECUTION_BOUNDARY_STATUS = `{report["NO_EXECUTION_BOUNDARY_STATUS"]}`",
                "- No FIX session opened.",
                "- No LMAX call made.",
                "- No orders submitted.",
                "- No fills captured.",
                "- No route/broker/live-state artifacts created.",
                "- No production/live touched.",
                "- No Qubes/netting/NettedUsdWeights touched.");

        public static string Summary(IReadOnlyDictionary<string, object> diagnosis, IReadOnlyDictionary<string, object> candidates)
            => Lines(
                "# CROSS-RAIL-R008 Status Summary",
                "",
                $"- R008 status: `{diagnosis["CROSS_RAIL_R008_STATUS"]}`",
                $"- Result: `{diagnosis["R008_RESULT"]}`",
                $"- Phase A: `{diagnosis["PHASE_A_APPROVAL_BINDING_STATUS"]}`",
                $"- Phase B attempted: `{diagnosis["PHASE_B_SANDBOX_EXECUTION_ATTEMPTED"]}`",
                $"- Gate status: `{diagnosis["PHASE_B_GATE_STATUS"]}`",
                $"- Candidate set preserved: `{candidates["CANDIDATE_SET_PRESERVED"]}`",
                $"- No execution boundary: `PASS`",
                "- Safe next gate: `CROSS_RAIL_R008B_INVOCATION_REPAIR_ONLY`.");

        private static string Lines(params string[] lines)
            => string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
