using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed class LmaxReadOnlyRuntimeRunStoreInMemory : ILmaxReadOnlyRuntimeRunStore
{
    private readonly List<LmaxReadOnlyRuntimeRunResult> _runs = [];

    public Task RecordRunAttemptAsync(LmaxReadOnlyRuntimeRunResult result, CancellationToken cancellationToken = default)
    {
        _runs.Insert(0, result);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LmaxReadOnlyRuntimeRunResult>> GetRecentRunsAsync(int limit = 20, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<LmaxReadOnlyRuntimeRunResult>>(_runs.Take(Math.Max(0, limit)).ToList());
}

public sealed class LmaxReadOnlyRuntimeAdapterFakeInMemory(
    LmaxReadOnlyRuntimeAdapterOptions? options = null,
    ILmaxReadOnlyRuntimeSafetyGate? safetyGate = null,
    ILmaxReadOnlyRuntimeRunStore? runStore = null) : ILmaxReadOnlyRuntimeAdapter
{
    public const string DefaultFixtureRelativePath = "tests/fixtures/lmax-shadow/lmax-mixed-readonly-evidence-v1.json";

    private readonly LmaxReadOnlyRuntimeAdapterOptions _options = options ?? new LmaxReadOnlyRuntimeAdapterOptions();
    private readonly ILmaxReadOnlyRuntimeSafetyGate _safetyGate = safetyGate ?? new LmaxReadOnlyRuntimeSafetyGateEvaluator();
    private readonly ILmaxReadOnlyRuntimeRunStore _runStore = runStore ?? new LmaxReadOnlyRuntimeRunStoreInMemory();

    public Task<LmaxReadOnlyRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var safety = EvaluateSafety(null);
        var status = new LmaxReadOnlyRuntimeStatus(
            _options.ImplementationMode,
            _options.RequestedActivationLevel,
            safety.RunStatus,
            _options.Enabled,
            _options.ReadOnly,
            _options.AllowExternalConnections,
            _options.AllowCredentialUse,
            _options.AllowOrderSubmission,
            _options.PersistRawFixMessages,
            _options.PersistToTradingTables,
            _options.SubmitToShadowReplay,
            _options.SchedulerEnabled,
            "LMAX read-only runtime Phase 2 fake adapter uses local evidence fixtures only; no sockets, credentials, scheduler, order submission, or trading-state mutation.",
            safety.Gates);

        return Task.FromResult(status);
    }

    public Task<LmaxReadOnlyRuntimeSafetyEvaluation> EvaluateSafetyAsync(LmaxReadOnlyRuntimeRunRequest? request = null, CancellationToken cancellationToken = default)
        => Task.FromResult(EvaluateSafety(request));

    public async Task<LmaxReadOnlyRuntimeRunResult> RunAsync(LmaxReadOnlyRuntimeRunRequest request, CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid().ToString("D");
        var safety = EvaluateSafety(request);
        if (!safety.Passed)
        {
            var blocked = new LmaxReadOnlyRuntimeRunResult(
                safety.RunStatus == LmaxReadOnlyRuntimeRunStatus.Disabled ? LmaxReadOnlyRuntimeRunStatus.Disabled : LmaxReadOnlyRuntimeRunStatus.Blocked,
                "LMAX read-only runtime fake adapter run blocked. " + safety.BlockedReason,
                safety,
                null)
            {
                RunId = runId,
                RunMode = LmaxReadOnlyRuntimeRunMode.FakeInMemoryFixtureOnly,
                FixtureEvidenceFile = _options.FixtureEvidenceFile
            };
            await _runStore.RecordRunAttemptAsync(blocked, cancellationToken);
            return blocked;
        }

        var fixturePath = ResolveFixturePath(_options.FixtureEvidenceFile);
        if (fixturePath is null || !File.Exists(fixturePath))
        {
            var missing = new LmaxReadOnlyRuntimeRunResult(
                LmaxReadOnlyRuntimeRunStatus.Blocked,
                $"Fixture evidence file was not found. Configure {nameof(LmaxReadOnlyRuntimeAdapterOptions.FixtureEvidenceFile)} or provide {DefaultFixtureRelativePath}.",
                safety,
                null)
            {
                RunId = runId,
                RunMode = LmaxReadOnlyRuntimeRunMode.FakeInMemoryFixtureOnly,
                FixtureEvidenceFile = _options.FixtureEvidenceFile,
                ValidationErrorCount = 1
            };
            await _runStore.RecordRunAttemptAsync(missing, cancellationToken);
            return missing;
        }

        var preview = PreviewFixtureEvidence(fixturePath);
        if (preview.ErrorCount > 0)
        {
            var invalid = new LmaxReadOnlyRuntimeRunResult(
                LmaxReadOnlyRuntimeRunStatus.Blocked,
                "Fixture evidence validation failed: " + string.Join("; ", preview.Errors),
                safety,
                null)
            {
                RunId = runId,
                RunMode = LmaxReadOnlyRuntimeRunMode.FakeInMemoryFixtureOnly,
                FixtureEvidenceFile = fixturePath,
                EvidenceMode = preview.Batch.EvidenceMode,
                ExecutionReportCount = preview.Batch.ExecutionReportCount,
                OrderStatusCount = preview.Batch.OrderStatusCount,
                TradeCaptureReportCount = preview.Batch.TradeCaptureReportCount,
                ProtocolRejectCount = preview.Batch.ProtocolRejectCount,
                MarketDataSnapshotCount = preview.Batch.MarketDataSnapshotCount,
                InputEventCount = preview.InputEventCount,
                ValidationErrorCount = preview.ErrorCount,
                ValidationWarningCount = preview.WarningCount,
                ValidationInfoCount = 1
            };
            await _runStore.RecordRunAttemptAsync(invalid, cancellationToken);
            return invalid;
        }

        var completedAt = DateTimeOffset.UtcNow;
        var summary = new LmaxReadOnlyRuntimeEvidenceBatchSummary(
            preview.Batch.BatchId,
            preview.Batch.EvidenceMode,
            preview.Batch.CreatedAtUtc,
            completedAt,
            preview.InputEventCount,
            preview.InputEventCount,
            0,
            SubmittedToShadowReplay: false,
            preview.Batch.Warnings);

        var completed = new LmaxReadOnlyRuntimeRunResult(
            LmaxReadOnlyRuntimeRunStatus.Completed,
            "Fake/in-memory fixture evidence validated successfully. No external connection was made, no shadow replay was submitted, and no trading state was mutated in Phase 3.5 preview mode.",
            safety,
            summary)
        {
            RunId = runId,
            RunMode = LmaxReadOnlyRuntimeRunMode.FakeInMemoryFixtureOnly,
            FixtureEvidenceFile = fixturePath,
            EvidenceMode = preview.Batch.EvidenceMode,
            ExecutionReportCount = preview.Batch.ExecutionReportCount,
            OrderStatusCount = preview.Batch.OrderStatusCount,
            TradeCaptureReportCount = preview.Batch.TradeCaptureReportCount,
            ProtocolRejectCount = preview.Batch.ProtocolRejectCount,
            MarketDataSnapshotCount = preview.Batch.MarketDataSnapshotCount,
            InputEventCount = preview.InputEventCount,
            ValidationErrorCount = preview.ErrorCount,
            ValidationWarningCount = preview.WarningCount,
            ValidationInfoCount = 1,
            ObservationCount = 0,
            BlockingObservationCount = 0,
            WarningObservationCount = 0,
            ReplayRunId = null
        };

        await _runStore.RecordRunAttemptAsync(completed, cancellationToken);
        return completed;
    }

    private LmaxReadOnlyRuntimeSafetyEvaluation EvaluateSafety(LmaxReadOnlyRuntimeRunRequest? request)
    {
        var baseEvaluation = _safetyGate.Evaluate(_options, request);
        var gates = baseEvaluation.Gates.ToList();
        var requestedLevel = request?.RequestedActivationLevel ?? _options.RequestedActivationLevel;

        gates.Add(Gate("FakeInMemoryImplementationMode", _options.ImplementationMode == LmaxReadOnlyRuntimeImplementationMode.FakeInMemory, "FakeInMemory", _options.ImplementationMode.ToString()));
        gates.Add(Gate("Phase2ActivationLevel", requestedLevel <= LmaxReadOnlyRuntimeActivationLevel.Level2LocalManualNoExternal, "<= Level2LocalManualNoExternal", requestedLevel.ToString()));
        gates.Add(Gate("FixtureOnlyExternalConnections", !_options.AllowExternalConnections, "false", _options.AllowExternalConnections.ToString(CultureInfo.InvariantCulture)));
        gates.Add(Gate("FixtureOnlyCredentialUse", !_options.AllowCredentialUse, "false", _options.AllowCredentialUse.ToString(CultureInfo.InvariantCulture)));
        gates.Add(Gate("FixtureOnlyScheduler", !_options.SchedulerEnabled, "false", _options.SchedulerEnabled.ToString(CultureInfo.InvariantCulture)));

        var failed = gates.Where(x => x.BlocksRun).Select(x => x.Name).ToList();
        var status = !_options.Enabled
            ? LmaxReadOnlyRuntimeRunStatus.Disabled
            : failed.Count > 0
                ? LmaxReadOnlyRuntimeRunStatus.Blocked
                : LmaxReadOnlyRuntimeRunStatus.DryRun;
        var reason = failed.Count == 0
            ? "Phase 2 fake/in-memory fixture-only safety gates passed."
            : "Blocked by safety gates: " + string.Join(", ", failed);

        return new LmaxReadOnlyRuntimeSafetyEvaluation(status, reason, gates);
    }

    private static LmaxReadOnlyRuntimeSafetyGateResult Gate(string name, bool passed, string expected, string observed)
        => new(
            name,
            passed ? LmaxReadOnlyRuntimeSafetyGateStatus.Passed : LmaxReadOnlyRuntimeSafetyGateStatus.Failed,
            observed,
            expected,
            passed ? $"{name} is within Phase 2 fixture-only policy." : $"{name} violates Phase 2 fixture-only policy.");

    private static string? ResolveFixturePath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, DefaultFixtureRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    public static LmaxReadOnlyRuntimeFixturePreview PreviewFixtureEvidence(string path)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var json = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            errors.Add("Evidence JSON is empty or missing.");
            return Empty(path, errors, warnings);
        }

        if (ContainsSensitiveEvidence(json))
        {
            errors.Add("Evidence JSON contains credential-like sensitive content.");
        }

        JsonObject? root = null;
        try
        {
            root = JsonNode.Parse(json)?.AsObject();
        }
        catch (JsonException ex)
        {
            errors.Add("Evidence JSON is not valid JSON: " + ex.Message);
        }

        if (root is null)
        {
            return Empty(path, errors, warnings);
        }

        var schemaVersion = root["schemaVersion"]?.GetValue<string>() ?? string.Empty;
        if (!string.Equals(schemaVersion, "lmax-fix-lifecycle-evidence-v1", StringComparison.Ordinal))
        {
            errors.Add("schemaVersion must be lmax-fix-lifecycle-evidence-v1.");
        }

        var executionReports = CountArray(root, "executionReports", errors);
        var orderStatuses = CountArray(root, "orderStatuses", errors);
        var tradeCaptureReports = CountArray(root, "tradeCaptureReports", errors);
        var protocolRejects = CountArray(root, "protocolRejects", errors);
        var marketData = CountMarketData(root, errors);
        var evidenceMode = root["evidenceMode"]?.GetValue<string>() ?? InferEvidenceMode(executionReports, orderStatuses, tradeCaptureReports, protocolRejects, marketData);
        var eventCount = executionReports + orderStatuses + tradeCaptureReports + protocolRejects + marketData;

        if (root["orderStatusReports"] is not null)
        {
            errors.Add("Evidence must use orderStatuses, not legacy orderStatusReports, in Phase 2 fixture preview.");
        }

        var batch = new LmaxReadOnlyRuntimeEvidenceBatchPreview(
            Path.GetFileNameWithoutExtension(path),
            schemaVersion,
            evidenceMode,
            DateTimeOffset.UtcNow,
            executionReports,
            orderStatuses,
            tradeCaptureReports,
            protocolRejects,
            marketData,
            Sanitized: errors.All(x => !x.Contains("sensitive", StringComparison.OrdinalIgnoreCase)),
            ContainsRawFix: json.Contains("35=", StringComparison.Ordinal) || json.Contains("\u0001", StringComparison.Ordinal),
            warnings);

        return new LmaxReadOnlyRuntimeFixturePreview(path, batch, eventCount, errors.Count, warnings.Count, errors, warnings);
    }

    private static int CountArray(JsonObject root, string propertyName, List<string> errors, bool required = true)
    {
        if (root[propertyName] is null)
        {
            if (required) errors.Add($"{propertyName} array is required.");
            return 0;
        }

        if (root[propertyName] is JsonArray array)
        {
            return array.Count;
        }

        errors.Add($"{propertyName} must be an array.");
        return 0;
    }

    private static int CountMarketData(JsonObject root, List<string> errors)
    {
        if (root["marketData"] is null)
        {
            return 0;
        }

        return root["marketData"] switch
        {
            JsonArray array => array.Count,
            JsonObject => 1,
            _ => ErrorMarketData(errors)
        };
    }

    private static int ErrorMarketData(List<string> errors)
    {
        errors.Add("marketData must be an object or array when present.");
        return 0;
    }

    private static string InferEvidenceMode(int executionReports, int orderStatuses, int tradeCaptureReports, int protocolRejects, int marketData)
    {
        if (executionReports == 0 && orderStatuses == 0 && tradeCaptureReports == 0 && protocolRejects == 0)
        {
            return marketData > 0 ? "MarketDataOnly" : "EmptyReadOnly";
        }

        var populated = new[] { executionReports > 0, orderStatuses > 0, tradeCaptureReports > 0, protocolRejects > 0 }.Count(x => x);
        if (executionReports > 0 && tradeCaptureReports > 0) return "SyntheticLifecycle";
        if (populated > 1 || marketData > 0 && populated > 0) return "MixedReadOnly";
        if (protocolRejects > 0) return "ProtocolRejectOnly";
        if (tradeCaptureReports > 0) return "TradeCaptureOnly";
        if (orderStatuses > 0) return "OrderStatusOnly";
        return "MixedReadOnly";
    }

    private static bool ContainsSensitiveEvidence(string json)
    {
        var sensitiveTerms = new[]
        {
            "password",
            "passwd",
            "secret",
            "apiKey",
            "api_key",
            "authorization",
            "bearer ",
            "554="
        };

        return sensitiveTerms.Any(term => json.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static LmaxReadOnlyRuntimeFixturePreview Empty(string path, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
        => new(
            path,
            new LmaxReadOnlyRuntimeEvidenceBatchPreview(Path.GetFileNameWithoutExtension(path), string.Empty, "Unknown", DateTimeOffset.UtcNow, 0, 0, 0, 0, 0, Sanitized: false, ContainsRawFix: false, warnings),
            0,
            errors.Count,
            warnings.Count,
            errors,
            warnings);
}

public sealed record LmaxReadOnlyRuntimeFixturePreview(
    string FixturePath,
    LmaxReadOnlyRuntimeEvidenceBatchPreview Batch,
    int InputEventCount,
    int ErrorCount,
    int WarningCount,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
