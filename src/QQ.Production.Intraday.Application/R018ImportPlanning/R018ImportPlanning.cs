using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace QQ.Production.Intraday.Application.R018ImportPlanning;

public static class R018ImportPlanningConstants
{
    public const string ToolVersion = "M1C_R018_INTRADAY_IMPORT_PLAN_R001";
    public const string PlanSchemaVersion = "r018_intraday_import_plan_v1";
    public const string BundleManifestSchemaVersion = "r018_artifact_bundle_manifest_v1";
    public const string NormalizedEventSchemaVersion = "r018_normalized_event_v1";
}

public enum R018ImportBundleStatus
{
    EVIDENCE_ONLY,
    CANONICAL_LINKED,
    REJECTED
}

public enum R018ProvenanceType
{
    RAW_FIX,
    EOD_LMAX_REPORT,
    ARTIFACT_LEDGER,
    MANUAL_EXPORT,
    MANUAL_UI,
    Derived,
    Unknown
}

public enum R018NormalizedEventKind
{
    Order,
    ExecutionReport,
    Fill,
    ManualObservation,
    Bbo,
    Unknown
}

public enum R018LedgerApplicability
{
    NOT_APPLICABLE,
    INCOMPLETE_HISTORY,
    COMPLETE_ISOLATED_REPLAY_CANDIDATE
}

public sealed record R018SourceArtifact(
    string Path,
    string Sha256,
    string Kind);

public sealed record R018ArtifactBundleManifest(
    string SchemaVersion,
    string Environment,
    string BrokerAccount,
    string Venue,
    string SourceRunId,
    string ApprovedCandidateHash,
    string QuantityUnit,
    DateTimeOffset? TargetCloseUtc,
    DateTimeOffset? DecisionUtc,
    DateTimeOffset? EffectiveFromUtc,
    DateTimeOffset? DeadlineUtc,
    string? ModelRunId,
    string? StartingPositionSource,
    IReadOnlyList<R018SourceArtifact> Artifacts,
    string? CoreCommit,
    string? ConfigHash)
{
    public static R018ArtifactBundleManifest Missing { get; } = new(
        R018ImportPlanningConstants.BundleManifestSchemaVersion,
        "unknown",
        "unknown",
        "unknown",
        "unknown",
        "unknown",
        "unknown",
        null,
        null,
        null,
        null,
        null,
        null,
        Array.Empty<R018SourceArtifact>(),
        null,
        null);
}

public sealed record R018SourceEvidence(
    R018ProvenanceType Provenance,
    string SourcePath,
    string SourceFileHash,
    string SourceLocator,
    string ParserVersion,
    string AuthorityClass,
    string RawPayloadHash);

public sealed record R018NormalizedEvent(
    string EventId,
    R018NormalizedEventKind Kind,
    R018ProvenanceType Provenance,
    string Environment,
    string BrokerAccount,
    string Venue,
    string? Symbol,
    string? SecurityId,
    string? Side,
    string? QuantityUnit,
    string? PhaseId,
    string? WaveId,
    string? ClipId,
    string? ClOrdId,
    string? OrigClOrdId,
    string? BrokerOrderId,
    string? ExecId,
    string? OrderType,
    string? TimeInForce,
    decimal? OrderQuantity,
    decimal? LastQuantity,
    decimal? CumQuantity,
    decimal? LeavesQuantity,
    decimal? LimitPrice,
    decimal? FillPrice,
    string? ExecType,
    string? OrdStatus,
    DateTimeOffset? SourceTimestampUtc,
    DateTimeOffset? LocalReceiveUtc,
    long? LocalEventOrder,
    string? BboEventId,
    bool IsTerminal,
    bool IsManualTerminalObservation,
    R018SourceEvidence Evidence,
    string RawJson)
{
    public string StableKey => Kind switch
    {
        R018NormalizedEventKind.Order => $"ORDER|{Environment}|{BrokerAccount}|{Venue}|{ClOrdId}",
        R018NormalizedEventKind.Fill => $"FILL|{Environment}|{BrokerAccount}|{Venue}|{ClOrdId}|{ExecId}",
        R018NormalizedEventKind.ExecutionReport => $"ER|{Environment}|{BrokerAccount}|{Venue}|{ClOrdId}|{ExecId}|{ExecType}|{OrdStatus}|{LocalEventOrder}",
        _ => $"{Kind}|{Environment}|{BrokerAccount}|{Venue}|{EventId}"
    };
}

public sealed record R018ArtifactBundle(
    string BundlePath,
    R018ArtifactBundleManifest Manifest,
    bool ManifestFound,
    IReadOnlyList<R018NormalizedEvent> Events,
    IReadOnlyList<string> ReadIssues,
    string InputBundleHash);

public sealed record R018ValidationIssue(
    string Severity,
    string Code,
    string Message,
    string? EventId = null,
    string? SourcePath = null);

public sealed record R018ValidationReport(
    string SchemaVersion,
    string ToolVersion,
    R018ImportBundleStatus Status,
    IReadOnlyList<R018ValidationIssue> Issues,
    int SourceOrderCount,
    int NormalizedOrderCount,
    int ExecutionReportCount,
    int FillCount,
    int DuplicateExactCount,
    int DuplicateConflictCount,
    bool FutureDbApplyAllowed);

public sealed record R018LineageReport(
    string SchemaVersion,
    string ToolVersion,
    R018ImportBundleStatus Status,
    string ModelRunResolution,
    string? ModelRunId,
    string LedgerApplicability,
    IReadOnlyList<string> UnmappedEvents,
    IReadOnlyList<string> LineageWarnings);

public sealed record R018IdentityScopeReport(
    string SchemaVersion,
    string ToolVersion,
    string Environment,
    string BrokerAccount,
    string Venue,
    bool EnvironmentPresent,
    bool BrokerAccountPresent,
    bool VenuePresent,
    bool CurrentFillUniqueKeyIsCrossEnvironmentSafe,
    bool CurrentClientOrderIdKeyIsCrossEnvironmentSafe,
    bool FutureDbApplyAllowed,
    bool LocalIsolatedReplayAllowed,
    IReadOnlyList<string> Risks);

public sealed record R018ImportPlan(
    string SchemaVersion,
    string ToolVersion,
    R018ImportBundleStatus Status,
    string SourceRunId,
    string ApprovedCandidateHash,
    string InputBundleHash,
    string DeterministicContentHash,
    string CodeCommit,
    DateTimeOffset GenerationUtc,
    string ModeReason,
    R018LedgerApplicability LedgerApplicability,
    bool DbApply,
    bool NetworkAllowed,
    bool CreatesModelRun,
    bool CreatesTargetWeights,
    bool CreatesPositionLedgerEvents,
    IReadOnlyList<string> ProposedMutationTargets,
    IReadOnlyList<R018NormalizedEvent> NormalizedEvents,
    R018ValidationReport Validation,
    R018LineageReport Lineage,
    R018IdentityScopeReport IdentityScope);

public sealed record R018ParityRow(
    string Check,
    string Expected,
    string Actual,
    string Status,
    string Detail);

public sealed record R018ModelRunCatalogEntry(
    string ModelRunId,
    string SourceRunId,
    string ApprovedCandidateHash);

public sealed class R018ArtifactBundleReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public R018ArtifactBundle Read(string bundlePath)
    {
        if (string.IsNullOrWhiteSpace(bundlePath))
        {
            throw new ArgumentException("Bundle path is required.", nameof(bundlePath));
        }

        var fullBundlePath = Path.GetFullPath(bundlePath);
        if (!Directory.Exists(fullBundlePath))
        {
            throw new DirectoryNotFoundException(fullBundlePath);
        }

        var issues = new List<string>();
        var manifestPath = Path.Combine(fullBundlePath, "r018_bundle_manifest_v1.json");
        var manifestFound = File.Exists(manifestPath);
        var manifest = manifestFound
            ? ReadManifest(manifestPath, issues)
            : R018ArtifactBundleManifest.Missing;

        if (!manifestFound)
        {
            issues.Add("MISSING_MANIFEST:r018_bundle_manifest_v1.json");
        }

        var events = new List<R018NormalizedEvent>();
        if (manifestFound)
        {
            foreach (var artifact in manifest.Artifacts)
            {
                var artifactPath = Path.GetFullPath(Path.Combine(fullBundlePath, artifact.Path));
                if (!artifactPath.StartsWith(fullBundlePath, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add($"ARTIFACT_PATH_ESCAPES_BUNDLE:{artifact.Path}");
                    continue;
                }

                if (!File.Exists(artifactPath))
                {
                    issues.Add($"MISSING_ARTIFACT:{artifact.Path}");
                    continue;
                }

                var actualHash = ComputeFileSha256(artifactPath);
                if (!string.Equals(actualHash, artifact.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add($"HASH_MISMATCH:{artifact.Path}:expected={artifact.Sha256}:actual={actualHash}");
                }

                if (artifactPath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
                {
                    events.AddRange(ReadJsonLines(artifactPath, artifact, manifest, actualHash, issues));
                }
                else if (artifactPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    events.AddRange(ReadCsv(artifactPath, artifact, manifest, actualHash, issues));
                }
            }
        }

        var bundleHash = ComputeBundleHash(fullBundlePath, manifestFound ? manifest.Artifacts : Array.Empty<R018SourceArtifact>());
        return new R018ArtifactBundle(
            fullBundlePath,
            manifest,
            manifestFound,
            events.OrderBy(e => e.LocalEventOrder ?? long.MaxValue).ThenBy(e => e.SourceTimestampUtc).ThenBy(e => e.EventId, StringComparer.Ordinal).ToArray(),
            issues,
            bundleHash);
    }

    private static R018ArtifactBundleManifest ReadManifest(string manifestPath, List<string> issues)
    {
        try
        {
            var json = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
                ?? throw new InvalidDataException("Manifest root must be a JSON object.");

            var artifacts = new List<R018SourceArtifact>();
            if (json["artifacts"] is JsonArray artifactArray)
            {
                foreach (var item in artifactArray.OfType<JsonObject>())
                {
                    artifacts.Add(new R018SourceArtifact(
                        GetString(item, "path") ?? string.Empty,
                        GetString(item, "sha256") ?? string.Empty,
                        GetString(item, "kind") ?? "unknown"));
                }
            }

            return new R018ArtifactBundleManifest(
                GetString(json, "schema_version") ?? R018ImportPlanningConstants.BundleManifestSchemaVersion,
                GetString(json, "environment") ?? "unknown",
                GetString(json, "broker_account") ?? "unknown",
                GetString(json, "venue") ?? "unknown",
                GetString(json, "source_run_id") ?? "unknown",
                GetString(json, "approved_candidate_hash") ?? "unknown",
                GetString(json, "quantity_unit") ?? "unknown",
                GetDate(json, "target_close_utc"),
                GetDate(json, "decision_utc"),
                GetDate(json, "effective_from_utc"),
                GetDate(json, "deadline_utc"),
                GetString(json, "model_run_id"),
                GetString(json, "starting_position_source"),
                artifacts,
                GetString(json, "core_commit"),
                GetString(json, "config_hash"));
        }
        catch (Exception ex)
        {
            issues.Add($"MANIFEST_PARSE_ERROR:{ex.GetType().Name}:{ex.Message}");
            return R018ArtifactBundleManifest.Missing;
        }
    }

    private static IEnumerable<R018NormalizedEvent> ReadJsonLines(
        string artifactPath,
        R018SourceArtifact artifact,
        R018ArtifactBundleManifest manifest,
        string sourceFileHash,
        List<string> issues)
    {
        var lineNumber = 0;
        foreach (var line in File.ReadLines(artifactPath))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonObject? obj;
            try
            {
                obj = JsonNode.Parse(line)?.AsObject();
            }
            catch (Exception ex)
            {
                issues.Add($"JSONL_PARSE_ERROR:{artifact.Path}:{lineNumber}:{ex.Message}");
                continue;
            }

            if (obj is null)
            {
                issues.Add($"JSONL_PARSE_ERROR:{artifact.Path}:{lineNumber}:not_object");
                continue;
            }

            yield return Normalize(obj, manifest, artifact, sourceFileHash, lineNumber.ToString(CultureInfo.InvariantCulture), line);
        }
    }

    private static IEnumerable<R018NormalizedEvent> ReadCsv(
        string artifactPath,
        R018SourceArtifact artifact,
        R018ArtifactBundleManifest manifest,
        string sourceFileHash,
        List<string> issues)
    {
        using var reader = new StreamReader(artifactPath);
        var headerLine = reader.ReadLine();
        if (headerLine is null)
        {
            yield break;
        }

        var headers = SplitCsvLine(headerLine);
        var lineNumber = 1;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = SplitCsvLine(line);
            if (values.Count != headers.Count)
            {
                issues.Add($"CSV_WIDTH_MISMATCH:{artifact.Path}:{lineNumber}");
                continue;
            }

            var obj = new JsonObject();
            for (var i = 0; i < headers.Count; i++)
            {
                obj[headers[i]] = values[i];
            }

            yield return Normalize(obj, manifest, artifact, sourceFileHash, lineNumber.ToString(CultureInfo.InvariantCulture), line);
        }
    }

    private static R018NormalizedEvent Normalize(
        JsonObject obj,
        R018ArtifactBundleManifest manifest,
        R018SourceArtifact artifact,
        string sourceFileHash,
        string locator,
        string rawPayload)
    {
        var provenance = ParseProvenance(GetAnyString(obj, "provenance", "source_provenance", "source"));
        var rawEventType = GetAnyString(obj, "event_type", "type", "msg_type", "kind") ?? artifact.Kind;
        var execType = GetAnyString(obj, "exec_type", "tag150", "150");
        var ordStatus = GetAnyString(obj, "ord_status", "tag39", "39");
        var kind = DetermineKind(rawEventType, execType, provenance);
        var isManualTerminalObservation = provenance is R018ProvenanceType.MANUAL_UI or R018ProvenanceType.MANUAL_EXPORT
            && (kind is R018NormalizedEventKind.Fill or R018NormalizedEventKind.ExecutionReport);
        if (isManualTerminalObservation && provenance is R018ProvenanceType.MANUAL_UI)
        {
            kind = R018NormalizedEventKind.ManualObservation;
        }

        var clOrdId = GetAnyString(obj, "clordid", "cl_ord_id", "ClOrdID", "tag11", "11");
        var execId = GetAnyString(obj, "execid", "exec_id", "ExecID", "tag17", "17");
        var eventId = GetAnyString(obj, "event_id")
            ?? $"{artifact.Path}:{locator}:{ComputeSha256(rawPayload)[..12]}";

        var evidence = new R018SourceEvidence(
            provenance,
            artifact.Path,
            sourceFileHash,
            locator,
            R018ImportPlanningConstants.ToolVersion,
            AuthorityClass(provenance),
            ComputeSha256(rawPayload));

        var orderQuantity = GetAnyDecimal(obj, "order_qty", "quantity", "qty", "OrderQty", "tag38", "38");
        var lastQuantity = GetAnyDecimal(obj, "last_qty", "last_quantity", "LastQty", "tag32", "32");
        var cumQuantity = GetAnyDecimal(obj, "cum_qty", "CumQty", "tag14", "14");
        var leavesQuantity = GetAnyDecimal(obj, "leaves_qty", "LeavesQty", "tag151", "151");

        return new R018NormalizedEvent(
            eventId,
            kind,
            provenance,
            GetAnyString(obj, "environment") ?? manifest.Environment,
            GetAnyString(obj, "broker_account", "account") ?? manifest.BrokerAccount,
            GetAnyString(obj, "venue") ?? manifest.Venue,
            GetAnyString(obj, "symbol", "pair", "tag55", "55"),
            GetAnyString(obj, "security_id", "SecurityID", "tag48", "48"),
            NormalizeSide(GetAnyString(obj, "side", "tag54", "54")),
            GetAnyString(obj, "quantity_unit", "unit") ?? manifest.QuantityUnit,
            GetAnyString(obj, "phase_id", "phase"),
            GetAnyString(obj, "wave_id", "wave"),
            GetAnyString(obj, "clip_id", "clip"),
            clOrdId,
            GetAnyString(obj, "orig_clordid", "OrigClOrdID", "tag41", "41"),
            GetAnyString(obj, "order_id", "broker_order_id", "tag37", "37"),
            execId,
            NormalizeOrderType(GetAnyString(obj, "order_type", "tag40", "40")),
            GetAnyString(obj, "time_in_force", "tif", "tag59", "59"),
            orderQuantity,
            lastQuantity,
            cumQuantity,
            leavesQuantity,
            GetAnyDecimal(obj, "limit_price", "price", "tag44", "44"),
            GetAnyDecimal(obj, "fill_price", "last_px", "LastPx", "tag31", "31"),
            execType,
            ordStatus,
            GetAnyDate(obj, "source_timestamp_utc", "sending_time", "tag52", "52"),
            GetAnyDate(obj, "local_receive_utc", "received_utc"),
            GetAnyLong(obj, "local_event_order", "event_order"),
            GetAnyString(obj, "bbo_event_id", "pre_fill_quote_event_id"),
            IsTerminal(execType, ordStatus, kind, cumQuantity, leavesQuantity),
            isManualTerminalObservation,
            evidence,
            rawPayload);
    }

    private static R018NormalizedEventKind DetermineKind(string? rawEventType, string? execType, R018ProvenanceType provenance)
    {
        var value = rawEventType?.Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        if (value is not null && value.Equals("NewOrderSingle", StringComparison.OrdinalIgnoreCase))
        {
            return R018NormalizedEventKind.Order;
        }

        if (value is not null && (value.Equals("Order", StringComparison.OrdinalIgnoreCase) || value.Equals("D", StringComparison.OrdinalIgnoreCase)))
        {
            return R018NormalizedEventKind.Order;
        }

        if (value is not null && value.Contains("BBO", StringComparison.OrdinalIgnoreCase))
        {
            return R018NormalizedEventKind.Bbo;
        }

        if (value is not null && value.Contains("Fill", StringComparison.OrdinalIgnoreCase))
        {
            return provenance is R018ProvenanceType.MANUAL_UI
                ? R018NormalizedEventKind.ManualObservation
                : R018NormalizedEventKind.Fill;
        }

        if (value is not null && (value.Equals("ExecutionReport", StringComparison.OrdinalIgnoreCase) || value.Equals("8", StringComparison.OrdinalIgnoreCase)))
        {
            return IsFillExecType(execType) ? R018NormalizedEventKind.Fill : R018NormalizedEventKind.ExecutionReport;
        }

        return R018NormalizedEventKind.Unknown;
    }

    private static bool IsFillExecType(string? execType)
        => execType is "1" or "2" or "F" or "f" or "PARTIAL_FILL" or "FILL";

    private static bool IsTerminal(string? execType, string? ordStatus, R018NormalizedEventKind kind, decimal? cumQuantity, decimal? leavesQuantity)
    {
        if (kind is R018NormalizedEventKind.Fill && leavesQuantity is 0)
        {
            return true;
        }

        return execType is "4" or "8" or "C" or "F" or "EXPIRED" or "CANCELED" or "CANCELLED" or "REJECTED"
            || ordStatus is "2" or "4" or "8" or "C" or "FILLED" or "CANCELED" or "CANCELLED" or "REJECTED" or "EXPIRED"
            || (cumQuantity.HasValue && leavesQuantity is 0);
    }

    private static R018ProvenanceType ParseProvenance(string? provenance)
    {
        var normalized = provenance?.Replace("-", "_", StringComparison.Ordinal).Trim();
        return normalized?.ToUpperInvariant() switch
        {
            "RAW_FIX" => R018ProvenanceType.RAW_FIX,
            "EOD_LMAX_REPORT" => R018ProvenanceType.EOD_LMAX_REPORT,
            "ARTIFACT_LEDGER" => R018ProvenanceType.ARTIFACT_LEDGER,
            "MANUAL_EXPORT" => R018ProvenanceType.MANUAL_EXPORT,
            "MANUAL_UI" => R018ProvenanceType.MANUAL_UI,
            "DERIVED" => R018ProvenanceType.Derived,
            _ => R018ProvenanceType.Unknown
        };
    }

    private static string AuthorityClass(R018ProvenanceType provenance) => provenance switch
    {
        R018ProvenanceType.RAW_FIX => "BROKER_SESSION_EVIDENCE",
        R018ProvenanceType.EOD_LMAX_REPORT => "BROKER_REPORT_EVIDENCE",
        R018ProvenanceType.ARTIFACT_LEDGER => "LOCAL_ARTIFACT_EVIDENCE",
        R018ProvenanceType.MANUAL_EXPORT => "MANUAL_BROKER_EXPORT_EVIDENCE",
        R018ProvenanceType.MANUAL_UI => "MANUAL_OBSERVATION_ONLY",
        R018ProvenanceType.Derived => "DERIVED_NON_AUTHORITATIVE",
        _ => "UNKNOWN_AUTHORITY"
    };

    private static string? NormalizeSide(string? side) => side?.ToUpperInvariant() switch
    {
        "1" => "BUY",
        "2" => "SELL",
        "BUY" => "BUY",
        "SELL" => "SELL",
        _ => side
    };

    private static string? NormalizeOrderType(string? orderType) => orderType switch
    {
        "2" => "LIMIT",
        "1" => "MARKET",
        null => null,
        _ => orderType.ToUpperInvariant()
    };

    private static string ComputeBundleHash(string bundlePath, IReadOnlyList<R018SourceArtifact> artifacts)
    {
        var builder = new StringBuilder();
        foreach (var artifact in artifacts.OrderBy(a => a.Path, StringComparer.Ordinal))
        {
            var path = Path.GetFullPath(Path.Combine(bundlePath, artifact.Path));
            if (File.Exists(path))
            {
                builder.Append(artifact.Path).Append('|').Append(ComputeFileSha256(path)).AppendLine();
            }
        }

        return ComputeSha256(builder.ToString());
    }

    public static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static string ComputeSha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string? GetAnyString(JsonObject obj, params string[] names)
        => names.Select(name => GetString(obj, name)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? GetString(JsonObject obj, string name)
        => obj.TryGetPropertyValue(name, out var node) ? node?.ToString() : null;

    private static DateTimeOffset? GetDate(JsonObject obj, string name)
        => GetString(obj, name) is { } value && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToUniversalTime()
            : null;

    private static DateTimeOffset? GetAnyDate(JsonObject obj, params string[] names)
        => names.Select(name => GetDate(obj, name)).FirstOrDefault(value => value.HasValue);

    private static decimal? GetAnyDecimal(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            var value = GetString(obj, name);
            if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static long? GetAnyLong(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            var value = GetString(obj, name);
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static List<string> SplitCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        values.Add(current.ToString());
        return values;
    }

    public static string ToStableJson<T>(T value)
        => JsonSerializer.Serialize(value, SerializerOptions);
}

public sealed class R018ArtifactBundleValidator
{
    private static readonly HashSet<string> KnownSymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        "AUDUSD", "EURUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCNH", "USDJPY", "USDMXN", "USDNOK", "USDSEK", "USDSGD", "USDZAR", "USDCHF"
    };

    private static readonly HashSet<string> KnownQuantityUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "LMAX_CONTRACTS", "BASE_CURRENCY", "USD_NOTIONAL"
    };

    public R018ValidationReport Validate(R018ArtifactBundle bundle, string? modelRunCatalogPath = null)
    {
        var issues = new List<R018ValidationIssue>();
        foreach (var issue in bundle.ReadIssues)
        {
            issues.Add(new R018ValidationIssue("ERROR", issue.Split(':')[0], issue, SourcePath: issue.Contains(':', StringComparison.Ordinal) ? issue.Split(':')[1] : null));
        }

        ValidateManifest(bundle, issues);
        ValidateEvents(bundle, issues);

        var duplicateExactCount = CountExactDuplicates(bundle.Events);
        var duplicateConflictCount = CountDuplicateConflicts(bundle.Events);
        var status = DetermineStatus(bundle, issues, modelRunCatalogPath);
        var identity = R018IdentityScopeBuilder.Build(bundle);

        return new R018ValidationReport(
            R018ImportPlanningConstants.PlanSchemaVersion,
            R018ImportPlanningConstants.ToolVersion,
            status,
            issues,
            bundle.Events.Count(e => e.Kind is R018NormalizedEventKind.Order),
            DeduplicateExact(bundle.Events.Where(e => e.Kind is R018NormalizedEventKind.Order)).Count(),
            bundle.Events.Count(e => e.Kind is R018NormalizedEventKind.ExecutionReport),
            bundle.Events.Count(e => e.Kind is R018NormalizedEventKind.Fill),
            duplicateExactCount,
            duplicateConflictCount,
            identity.FutureDbApplyAllowed);
    }

    private static void ValidateManifest(R018ArtifactBundle bundle, List<R018ValidationIssue> issues)
    {
        if (!bundle.ManifestFound)
        {
            issues.Add(new R018ValidationIssue("ERROR", "MISSING_MANIFEST", "r018_bundle_manifest_v1.json is required."));
            return;
        }

        if (!string.Equals(bundle.Manifest.SchemaVersion, R018ImportPlanningConstants.BundleManifestSchemaVersion, StringComparison.Ordinal))
        {
            issues.Add(new R018ValidationIssue("ERROR", "UNKNOWN_MANIFEST_SCHEMA", bundle.Manifest.SchemaVersion));
        }

        if (IsUnknown(bundle.Manifest.Environment))
        {
            issues.Add(new R018ValidationIssue("ERROR", "MISSING_ENVIRONMENT", "Environment must be demo, live, or unknown explicitly; unknown is rejected for import planning."));
        }
        else if (!bundle.Manifest.Environment.Equals("demo", StringComparison.OrdinalIgnoreCase)
                 && !bundle.Manifest.Environment.Equals("live", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new R018ValidationIssue("ERROR", "INVALID_ENVIRONMENT", bundle.Manifest.Environment));
        }

        if (IsUnknown(bundle.Manifest.BrokerAccount))
        {
            issues.Add(new R018ValidationIssue("ERROR", "MISSING_BROKER_ACCOUNT", "Broker account identity is required for collision analysis."));
        }

        if (IsUnknown(bundle.Manifest.Venue))
        {
            issues.Add(new R018ValidationIssue("ERROR", "MISSING_VENUE", "Venue is required for collision analysis."));
        }

        if (IsUnknown(bundle.Manifest.QuantityUnit) || !KnownQuantityUnits.Contains(bundle.Manifest.QuantityUnit))
        {
            issues.Add(new R018ValidationIssue("ERROR", "UNKNOWN_QUANTITY_UNIT", bundle.Manifest.QuantityUnit));
        }
    }

    private static void ValidateEvents(R018ArtifactBundle bundle, List<R018ValidationIssue> issues)
    {
        var orders = DeduplicateExact(bundle.Events.Where(e => e.Kind is R018NormalizedEventKind.Order)).ToDictionary(OrderLookupKey, StringComparer.Ordinal);
        foreach (var ev in bundle.Events)
        {
            if (ev.Kind is R018NormalizedEventKind.Unknown)
            {
                issues.Add(new R018ValidationIssue("ERROR", "UNKNOWN_EVENT_KIND", "Event kind could not be normalized.", ev.EventId, ev.Evidence.SourcePath));
            }

            if (ev.Symbol is null || !KnownSymbols.Contains(ev.Symbol))
            {
                issues.Add(new R018ValidationIssue("ERROR", "UNKNOWN_INSTRUMENT", ev.Symbol ?? "missing", ev.EventId, ev.Evidence.SourcePath));
            }

            if (ev.QuantityUnit is null || !KnownQuantityUnits.Contains(ev.QuantityUnit))
            {
                issues.Add(new R018ValidationIssue("ERROR", "UNKNOWN_QUANTITY_UNIT", ev.QuantityUnit ?? "missing", ev.EventId, ev.Evidence.SourcePath));
            }

            if (ev.Kind is R018NormalizedEventKind.Order && string.IsNullOrWhiteSpace(ev.ClOrdId))
            {
                issues.Add(new R018ValidationIssue("ERROR", "MISSING_CLORDID", "Order is missing ClOrdID.", ev.EventId, ev.Evidence.SourcePath));
            }

            if (ev.Kind is R018NormalizedEventKind.Order && ev.OrderType is "MARKET")
            {
                issues.Add(new R018ValidationIssue("ERROR", "MARKET_ORDER_FORBIDDEN", "M1C accepts LIMIT-only R018/R216 evidence.", ev.EventId, ev.Evidence.SourcePath));
            }

            if (ev.Kind is R018NormalizedEventKind.Fill)
            {
                if (string.IsNullOrWhiteSpace(ev.ClOrdId) || !orders.ContainsKey(OrderLookupKey(ev)))
                {
                    issues.Add(new R018ValidationIssue("ERROR", "FILL_WITHOUT_CHILD_ORDER", ev.ClOrdId ?? "missing", ev.EventId, ev.Evidence.SourcePath));
                }

                if (string.IsNullOrWhiteSpace(ev.ExecId))
                {
                    issues.Add(new R018ValidationIssue("ERROR", "FILL_WITHOUT_EXECID", "Fill must have ExecID.", ev.EventId, ev.Evidence.SourcePath));
                }

                if (ev.LastQuantity is null or <= 0 || ev.FillPrice is null or <= 0)
                {
                    issues.Add(new R018ValidationIssue("ERROR", "FILL_MISSING_QTY_OR_PRICE", "Fill must have positive quantity and price.", ev.EventId, ev.Evidence.SourcePath));
                }

                if (ev.Provenance is R018ProvenanceType.MANUAL_UI)
                {
                    issues.Add(new R018ValidationIssue("ERROR", "MANUAL_UI_CANNOT_CREATE_FILL", "Manual UI evidence is observation-only.", ev.EventId, ev.Evidence.SourcePath));
                }
            }

            if (ev.Kind is R018NormalizedEventKind.ExecutionReport && ev.CumQuantity.HasValue && ev.LeavesQuantity.HasValue && ev.ClOrdId is not null && orders.TryGetValue(OrderLookupKey(ev), out var order) && order.OrderQuantity.HasValue)
            {
                var sum = ev.CumQuantity.Value + ev.LeavesQuantity.Value;
                if (Math.Abs(sum - order.OrderQuantity.Value) > 0.0000001m)
                {
                    issues.Add(new R018ValidationIssue("ERROR", "CUM_LEAVES_INCONSISTENT", $"cum+leaves={sum} order_qty={order.OrderQuantity}", ev.EventId, ev.Evidence.SourcePath));
                }
            }
        }

        foreach (var group in bundle.Events.Where(e => e.ClOrdId is not null).GroupBy(e => e.StableKey, StringComparer.Ordinal))
        {
            if (group.Select(e => e.Evidence.RawPayloadHash).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                issues.Add(new R018ValidationIssue("ERROR", "DUPLICATE_CONFLICT", group.Key));
            }
        }

        foreach (var order in orders.Values)
        {
            var terminal = bundle.Events.Any(e => OrderLookupKey(e) == OrderLookupKey(order) && e.IsTerminal);
            if (!terminal)
            {
                issues.Add(new R018ValidationIssue("ERROR", "NON_TERMINAL_ORDER", order.ClOrdId ?? "missing", order.EventId, order.Evidence.SourcePath));
            }
        }
    }

    private static string OrderLookupKey(R018NormalizedEvent ev)
        => $"{ev.Environment}|{ev.BrokerAccount}|{ev.Venue}|{ev.ClOrdId}";

    private static R018ImportBundleStatus DetermineStatus(R018ArtifactBundle bundle, List<R018ValidationIssue> issues, string? modelRunCatalogPath)
    {
        if (issues.Any(i => i.Severity.Equals("ERROR", StringComparison.Ordinal)))
        {
            return R018ImportBundleStatus.REJECTED;
        }

        if (!string.IsNullOrWhiteSpace(bundle.Manifest.ModelRunId))
        {
            return R018ImportBundleStatus.CANONICAL_LINKED;
        }

        if (!string.IsNullOrWhiteSpace(modelRunCatalogPath) && File.Exists(modelRunCatalogPath))
        {
            var matches = LoadCatalog(modelRunCatalogPath)
                .Where(e => e.SourceRunId == bundle.Manifest.SourceRunId && e.ApprovedCandidateHash == bundle.Manifest.ApprovedCandidateHash)
                .ToArray();

            if (matches.Length == 1)
            {
                return R018ImportBundleStatus.CANONICAL_LINKED;
            }

            return R018ImportBundleStatus.EVIDENCE_ONLY;
        }

        return R018ImportBundleStatus.EVIDENCE_ONLY;
    }

    public static IReadOnlyList<R018ModelRunCatalogEntry> LoadCatalog(string path)
    {
        var json = JsonNode.Parse(File.ReadAllText(path));
        var array = json as JsonArray ?? json?["model_runs"] as JsonArray ?? new JsonArray();
        return array.OfType<JsonObject>()
            .Select(obj => new R018ModelRunCatalogEntry(
                obj["model_run_id"]?.ToString() ?? string.Empty,
                obj["source_run_id"]?.ToString() ?? string.Empty,
                obj["approved_candidate_hash"]?.ToString() ?? string.Empty))
            .Where(e => !string.IsNullOrWhiteSpace(e.ModelRunId))
            .ToArray();
    }

    private static IEnumerable<R018NormalizedEvent> DeduplicateExact(IEnumerable<R018NormalizedEvent> events)
        => events.GroupBy(e => e.StableKey, StringComparer.Ordinal).Select(g => g.First());

    private static int CountExactDuplicates(IEnumerable<R018NormalizedEvent> events)
        => events.GroupBy(e => e.StableKey, StringComparer.Ordinal).Sum(g => Math.Max(0, g.Count() - g.Select(e => e.Evidence.RawPayloadHash).Distinct(StringComparer.Ordinal).Count()));

    private static int CountDuplicateConflicts(IEnumerable<R018NormalizedEvent> events)
        => events.GroupBy(e => e.StableKey, StringComparer.Ordinal).Count(g => g.Select(e => e.Evidence.RawPayloadHash).Distinct(StringComparer.Ordinal).Count() > 1);

    private static bool IsUnknown(string value)
        => string.IsNullOrWhiteSpace(value) || value.Equals("unknown", StringComparison.OrdinalIgnoreCase);
}

public sealed class R018LineageResolver
{
    public R018LineageReport Resolve(R018ArtifactBundle bundle, R018ValidationReport validation, string? modelRunCatalogPath = null)
    {
        string resolution;
        string? modelRunId = null;
        var warnings = new List<string>();

        if (!string.IsNullOrWhiteSpace(bundle.Manifest.ModelRunId))
        {
            resolution = "EXPLICIT_MODEL_RUN_ID";
            modelRunId = bundle.Manifest.ModelRunId;
        }
        else if (!string.IsNullOrWhiteSpace(modelRunCatalogPath) && File.Exists(modelRunCatalogPath))
        {
            var matches = R018ArtifactBundleValidator.LoadCatalog(modelRunCatalogPath)
                .Where(e => e.SourceRunId == bundle.Manifest.SourceRunId && e.ApprovedCandidateHash == bundle.Manifest.ApprovedCandidateHash)
                .ToArray();
            if (matches.Length == 1)
            {
                resolution = "OFFLINE_CATALOG_UNIQUE_MATCH";
                modelRunId = matches[0].ModelRunId;
            }
            else if (matches.Length > 1)
            {
                resolution = "OFFLINE_CATALOG_AMBIGUOUS";
                warnings.Add("Multiple catalog matches; no arbitrary ModelRun selection performed.");
            }
            else
            {
                resolution = "NO_CANONICAL_MODEL_RUN_LINK";
            }
        }
        else
        {
            resolution = "NO_CANONICAL_MODEL_RUN_LINK";
        }

        var unmapped = bundle.Events
            .Where(e => e.Kind is R018NormalizedEventKind.Unknown)
            .Select(e => e.EventId)
            .ToArray();

        var ledgerApplicability = ResolveLedgerApplicability(bundle, validation).ToString().ToUpperInvariant();
        return new R018LineageReport(
            R018ImportPlanningConstants.PlanSchemaVersion,
            R018ImportPlanningConstants.ToolVersion,
            validation.Status,
            resolution,
            modelRunId,
            ledgerApplicability,
            unmapped,
            warnings);
    }

    public static R018LedgerApplicability ResolveLedgerApplicability(R018ArtifactBundle bundle, R018ValidationReport validation)
    {
        if (!bundle.Events.Any(e => e.Kind is R018NormalizedEventKind.Fill))
        {
            return R018LedgerApplicability.NOT_APPLICABLE;
        }

        if (!string.IsNullOrWhiteSpace(bundle.Manifest.StartingPositionSource)
            && validation.Status is not R018ImportBundleStatus.REJECTED
            && validation.Issues.All(i => i.Code is not ("NON_TERMINAL_ORDER" or "FILL_WITHOUT_CHILD_ORDER" or "FILL_WITHOUT_EXECID")))
        {
            return R018LedgerApplicability.COMPLETE_ISOLATED_REPLAY_CANDIDATE;
        }

        return R018LedgerApplicability.INCOMPLETE_HISTORY;
    }
}

public static class R018IdentityScopeBuilder
{
    public static R018IdentityScopeReport Build(R018ArtifactBundle bundle)
    {
        var risks = new List<string>();
        var envPresent = !IsUnknown(bundle.Manifest.Environment);
        var accountPresent = !IsUnknown(bundle.Manifest.BrokerAccount);
        var venuePresent = !IsUnknown(bundle.Manifest.Venue);

        if (!envPresent)
        {
            risks.Add("MISSING_ENVIRONMENT_SCOPE");
        }

        if (!accountPresent)
        {
            risks.Add("MISSING_BROKER_ACCOUNT_SCOPE");
        }

        if (!venuePresent)
        {
            risks.Add("MISSING_VENUE_SCOPE");
        }

        risks.Add("CURRENT_INTRADAY_FILL_UNIQUE_KEY_IS_VENUE_PLUS_EXECID;ENVIRONMENT_AND_ACCOUNT_SCOPE_MUST_BE_VERIFIED_BEFORE_DB_APPLY");
        risks.Add("CURRENT_CLIENT_ORDER_ID_UNIQUE_KEY_SCOPE_MAY_COLLIDE_ACROSS_DEMO_LIVE_IF_PREFIX_POLICY_IS_NOT_PROVEN");

        return new R018IdentityScopeReport(
            R018ImportPlanningConstants.PlanSchemaVersion,
            R018ImportPlanningConstants.ToolVersion,
            bundle.Manifest.Environment,
            bundle.Manifest.BrokerAccount,
            bundle.Manifest.Venue,
            envPresent,
            accountPresent,
            venuePresent,
            false,
            false,
            false,
            envPresent && accountPresent && venuePresent,
            risks);
    }

    private static bool IsUnknown(string value)
        => string.IsNullOrWhiteSpace(value) || value.Equals("unknown", StringComparison.OrdinalIgnoreCase);
}

public sealed class R018ImportPlanBuilder
{
    public R018ImportPlan Build(R018ArtifactBundle bundle, string? modelRunCatalogPath = null, string codeCommit = "unknown")
    {
        var validator = new R018ArtifactBundleValidator();
        var validation = validator.Validate(bundle, modelRunCatalogPath);
        var lineage = new R018LineageResolver().Resolve(bundle, validation, modelRunCatalogPath);
        var identity = R018IdentityScopeBuilder.Build(bundle);
        var ledgerApplicability = R018LineageResolver.ResolveLedgerApplicability(bundle, validation);
        var proposedTargets = ProposedTargets(validation.Status).ToArray();
        var normalizedEvents = bundle.Events
            .GroupBy(e => e.StableKey, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(e => e.LocalEventOrder ?? long.MaxValue)
            .ThenBy(e => e.EventId, StringComparer.Ordinal)
            .ToArray();

        var modeReason = validation.Status switch
        {
            R018ImportBundleStatus.REJECTED => "Bundle failed closed during validation.",
            R018ImportBundleStatus.CANONICAL_LINKED => "Explicit or uniquely catalogued ModelRun link is present; ledger mutation remains forbidden in M1C.",
            _ => "No deterministic canonical ModelRun link; evidence-only outputs are allowed."
        };

        var seed = JsonSerializer.Serialize(new
        {
            validation.Status,
            bundle.Manifest.SourceRunId,
            bundle.Manifest.ApprovedCandidateHash,
            bundle.InputBundleHash,
            proposedTargets,
            events = normalizedEvents.Select(e => new
            {
                e.StableKey,
                e.Kind,
                e.Provenance,
                e.ClOrdId,
                e.ExecId,
                e.Symbol,
                e.Side,
                e.OrderQuantity,
                e.LastQuantity,
                e.CumQuantity,
                e.LeavesQuantity,
                e.LimitPrice,
                e.FillPrice,
                e.LocalEventOrder
            })
        });
        var deterministicHash = R018ArtifactBundleReader.ComputeSha256(seed);

        return new R018ImportPlan(
            R018ImportPlanningConstants.PlanSchemaVersion,
            R018ImportPlanningConstants.ToolVersion,
            validation.Status,
            bundle.Manifest.SourceRunId,
            bundle.Manifest.ApprovedCandidateHash,
            bundle.InputBundleHash,
            deterministicHash,
            codeCommit,
            DateTimeOffset.UtcNow,
            modeReason,
            ledgerApplicability,
            DbApply: false,
            NetworkAllowed: false,
            CreatesModelRun: false,
            CreatesTargetWeights: false,
            CreatesPositionLedgerEvents: false,
            proposedTargets,
            normalizedEvents,
            validation,
            lineage,
            identity);
    }

    private static IEnumerable<string> ProposedTargets(R018ImportBundleStatus status)
    {
        if (status is R018ImportBundleStatus.REJECTED)
        {
            yield break;
        }

        yield return "R018_NORMALIZED_EVENT_STAGING";
        yield return "SHADOW_REPLAY_INPUT";
        yield return "PARITY_REPORT";
        yield return "TCA_RESEARCH_EVIDENCE";
        yield return "EXCEPTION_EVIDENCE_PLAN";

        if (status is R018ImportBundleStatus.CANONICAL_LINKED)
        {
            yield return "TradeIntent";
            yield return "ParentOrder";
            yield return "ChildOrder";
            yield return "ExecutionReport";
            yield return "Fill";
            yield return "PositionLedgerEvent_DESCRIBED_ONLY_NOT_PLANNED";
        }
    }
}

public sealed class R018ParityReportBuilder
{
    public IReadOnlyList<R018ParityRow> Build(R018ArtifactBundle bundle, R018ImportPlan plan)
    {
        var sourceOrders = bundle.Events.Count(e => e.Kind is R018NormalizedEventKind.Order);
        var normalizedOrders = plan.NormalizedEvents.Count(e => e.Kind is R018NormalizedEventKind.Order);
        var fills = plan.NormalizedEvents.Count(e => e.Kind is R018NormalizedEventKind.Fill);
        var reports = plan.NormalizedEvents.Count(e => e.Kind is R018NormalizedEventKind.ExecutionReport);
        var bbo = plan.NormalizedEvents.Count(e => e.Kind is R018NormalizedEventKind.Bbo);
        var unmapped = plan.NormalizedEvents.Count(e => e.Kind is R018NormalizedEventKind.Unknown);

        return new[]
        {
            Row("source_orders_vs_normalized", sourceOrders, normalizedOrders),
            Row("execution_reports", reports, reports),
            Row("fills", fills, fills),
            Row("bbo_events_linked_or_available", bbo, bbo),
            Row("unmapped_events", 0, unmapped),
            Row("duplicate_exact", plan.Validation.DuplicateExactCount, plan.Validation.DuplicateExactCount),
            Row("duplicate_conflict", 0, plan.Validation.DuplicateConflictCount),
            Row("provenance_missing", 0, plan.NormalizedEvents.Count(e => e.Provenance is R018ProvenanceType.Unknown)),
            Row("timeline_events", plan.NormalizedEvents.Count, plan.NormalizedEvents.Count),
            Row("terminal_state_errors", 0, plan.Validation.Issues.Count(i => i.Code is "NON_TERMINAL_ORDER"))
        };
    }

    private static R018ParityRow Row(string check, int expected, int actual)
        => new(check, expected.ToString(CultureInfo.InvariantCulture), actual.ToString(CultureInfo.InvariantCulture), expected == actual ? "PASS" : "FAIL", string.Empty);
}

public sealed class R018ImportPlanSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    static R018ImportPlanSerializer()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }


    public void WriteAll(R018ArtifactBundle bundle, R018ImportPlan plan, string outputPath)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(fullOutputPath);

        WriteJson(Path.Combine(fullOutputPath, "bundle_manifest.json"), bundle.Manifest);
        WriteJson(Path.Combine(fullOutputPath, "validation_report.json"), plan.Validation);
        WriteJsonLines(Path.Combine(fullOutputPath, "normalized_events.jsonl"), plan.NormalizedEvents);
        WriteJson(Path.Combine(fullOutputPath, "lineage_report.json"), plan.Lineage);
        WriteJson(Path.Combine(fullOutputPath, "identity_scope_report.json"), plan.IdentityScope);
        WriteJson(Path.Combine(fullOutputPath, "import_plan_v1.json"), plan);
        WriteParity(Path.Combine(fullOutputPath, "parity_report.csv"), new R018ParityReportBuilder().Build(bundle, plan));
        WriteSummary(Path.Combine(fullOutputPath, "human_summary.md"), plan);
        WriteJson(Path.Combine(fullOutputPath, "output_hashes.json"), BuildHashes(fullOutputPath));
    }

    private static void WriteJson<T>(string path, T value)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }

    private static void WriteJsonLines<T>(string path, IEnumerable<T> values)
    {
        File.WriteAllLines(path, values.Select(value => JsonSerializer.Serialize(value, JsonOptions)));
    }

    private static void WriteParity(string path, IReadOnlyList<R018ParityRow> rows)
    {
        var lines = new List<string> { "check,expected,actual,status,detail" };
        lines.AddRange(rows.Select(r => string.Join(',', Escape(r.Check), Escape(r.Expected), Escape(r.Actual), Escape(r.Status), Escape(r.Detail))));
        File.WriteAllLines(path, lines);
    }

    private static void WriteSummary(string path, R018ImportPlan plan)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# R018/R216 Intraday Import Plan Summary");
        builder.AppendLine();
        builder.AppendLine($"- schema_version: `{plan.SchemaVersion}`");
        builder.AppendLine($"- status: `{plan.Status}`");
        builder.AppendLine($"- source_run_id: `{plan.SourceRunId}`");
        builder.AppendLine($"- approved_candidate_hash: `{plan.ApprovedCandidateHash}`");
        builder.AppendLine($"- deterministic_content_hash: `{plan.DeterministicContentHash}`");
        builder.AppendLine($"- db_apply: `{plan.DbApply}`");
        builder.AppendLine($"- network_allowed: `{plan.NetworkAllowed}`");
        builder.AppendLine($"- creates_model_run: `{plan.CreatesModelRun}`");
        builder.AppendLine($"- creates_position_ledger_events: `{plan.CreatesPositionLedgerEvents}`");
        builder.AppendLine($"- ledger_applicability: `{plan.LedgerApplicability}`");
        builder.AppendLine();
        builder.AppendLine("## Mode Reason");
        builder.AppendLine(plan.ModeReason);
        builder.AppendLine();
        builder.AppendLine("## Proposed Targets");
        foreach (var target in plan.ProposedMutationTargets)
        {
            builder.AppendLine($"- `{target}`");
        }

        File.WriteAllText(path, builder.ToString());
    }

    private static IReadOnlyDictionary<string, string> BuildHashes(string outputPath)
        => Directory.GetFiles(outputPath)
            .Where(path => !Path.GetFileName(path).Equals("output_hashes.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(path => Path.GetFileName(path), R018ArtifactBundleReader.ComputeFileSha256, StringComparer.Ordinal);

    private static string Escape(string value)
        => value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal)
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
}

public sealed class R018OfflineImportPlanCli
{
    public int Execute(string[] args, TextWriter output, TextWriter error)
    {
        var parsed = R018OfflineImportPlanCliOptions.Parse(args);
        if (parsed.ShowHelp)
        {
            output.WriteLine(R018OfflineImportPlanCliOptions.HelpText);
            return 0;
        }

        foreach (var issue in parsed.RejectionReasons)
        {
            error.WriteLine(issue);
        }

        if (parsed.RejectionReasons.Count > 0)
        {
            return 2;
        }

        var bundlePath = Path.GetFullPath(parsed.BundlePath!);
        var outputPath = Path.GetFullPath(parsed.OutputPath!);
        if (outputPath.StartsWith(bundlePath, StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine("OUTPUT_INSIDE_SOURCE_BUNDLE_REJECTED");
            return 2;
        }

        var secretIssue = DetectSecrets(bundlePath);
        if (secretIssue is not null)
        {
            error.WriteLine(secretIssue);
            return 2;
        }

        try
        {
            var bundle = new R018ArtifactBundleReader().Read(bundlePath);
            var plan = new R018ImportPlanBuilder().Build(bundle, parsed.ModelRunCatalogPath, parsed.CodeCommit ?? "unknown");
            new R018ImportPlanSerializer().WriteAll(bundle, plan, outputPath);
            output.WriteLine(plan.Status);
            output.WriteLine(plan.DeterministicContentHash);
            return plan.Status is R018ImportBundleStatus.REJECTED ? 1 : 0;
        }
        catch (Exception ex)
        {
            error.WriteLine($"M1C_CLI_ERROR:{ex.GetType().Name}:{ex.Message}");
            return 1;
        }
    }

    private static string? DetectSecrets(string bundlePath)
    {
        foreach (var file in Directory.EnumerateFiles(bundlePath, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (name.Contains("secret", StringComparison.OrdinalIgnoreCase)
                || name.Contains("credential", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".key", StringComparison.OrdinalIgnoreCase))
            {
                return $"SECRET_LIKE_FILE_REJECTED:{name}";
            }

            if (new FileInfo(file).Length > 1024 * 1024)
            {
                continue;
            }

            var text = File.ReadAllText(file);
            if (text.Contains("BEGIN PRIVATE KEY", StringComparison.OrdinalIgnoreCase)
                || text.Contains("password=", StringComparison.OrdinalIgnoreCase)
                || text.Contains("api_key", StringComparison.OrdinalIgnoreCase)
                || text.Contains("apikey", StringComparison.OrdinalIgnoreCase)
                || text.Contains("bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return $"SECRET_LIKE_CONTENT_REJECTED:{name}";
            }
        }

        return null;
    }
}

public sealed record R018OfflineImportPlanCliOptions(
    string? BundlePath,
    string? OutputPath,
    string? ModelRunCatalogPath,
    string? CodeCommit,
    bool NoDb,
    bool NoNetwork,
    bool ShowHelp,
    IReadOnlyList<string> RejectionReasons)
{
    public const string HelpText = """
Usage:
  build-r018-import-plan --bundle <path> --output <path> [--model-run-catalog <path>] --no-db --no-network

This CLI is strictly offline. There is no --apply mode.
""";

    public static R018OfflineImportPlanCliOptions Parse(string[] args)
    {
        var reasons = new List<string>();
        if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
        {
            return new R018OfflineImportPlanCliOptions(null, null, null, null, false, false, true, reasons);
        }

        if (!args[0].Equals("build-r018-import-plan", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("UNKNOWN_COMMAND");
        }

        string? bundle = null;
        string? output = null;
        string? catalog = null;
        string? codeCommit = null;
        var noDb = false;
        var noNetwork = false;

        for (var i = 1; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--bundle":
                    bundle = RequireValue(args, ref i, reasons, arg);
                    break;
                case "--output":
                    output = RequireValue(args, ref i, reasons, arg);
                    break;
                case "--model-run-catalog":
                    catalog = RequireValue(args, ref i, reasons, arg);
                    break;
                case "--code-commit":
                    codeCommit = RequireValue(args, ref i, reasons, arg);
                    break;
                case "--no-db":
                    noDb = true;
                    break;
                case "--no-network":
                    noNetwork = true;
                    break;
                default:
                    if (arg.Contains("apply", StringComparison.OrdinalIgnoreCase)
                        || arg.Contains("live", StringComparison.OrdinalIgnoreCase)
                        || arg.Contains("connection", StringComparison.OrdinalIgnoreCase))
                    {
                        reasons.Add($"FORBIDDEN_FLAG:{arg}");
                    }
                    else
                    {
                        reasons.Add($"UNKNOWN_FLAG:{arg}");
                    }
                    break;
            }
        }

        ValidatePath("bundle", bundle, mustExist: true, reasons);
        ValidatePath("output", output, mustExist: false, reasons);
        ValidatePath("model-run-catalog", catalog, mustExist: true, reasons, optional: true);
        if (!noDb)
        {
            reasons.Add("MISSING_NO_DB_FLAG");
        }

        if (!noNetwork)
        {
            reasons.Add("MISSING_NO_NETWORK_FLAG");
        }

        return new R018OfflineImportPlanCliOptions(bundle, output, catalog, codeCommit, noDb, noNetwork, false, reasons);
    }

    private static string? RequireValue(string[] args, ref int index, List<string> reasons, string flag)
    {
        if (index + 1 >= args.Length)
        {
            reasons.Add($"MISSING_VALUE:{flag}");
            return null;
        }

        index++;
        return args[index];
    }

    private static void ValidatePath(string name, string? path, bool mustExist, List<string> reasons, bool optional = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (!optional)
            {
                reasons.Add($"MISSING_{name.ToUpperInvariant().Replace("-", "_", StringComparison.Ordinal)}");
            }

            return;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            reasons.Add($"URL_REJECTED:{name}");
        }

        if (path.Contains("Server=", StringComparison.OrdinalIgnoreCase)
            || path.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
            || path.Contains("User ID=", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add($"CONNECTION_STRING_REJECTED:{name}");
        }

        if (mustExist && !File.Exists(path) && !Directory.Exists(path))
        {
            reasons.Add($"PATH_NOT_FOUND:{name}:{path}");
        }
    }
}






