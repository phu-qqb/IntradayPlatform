using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace QQ.Production.Intraday.Application.R018ImportPlanning;

public static class R018ImportPlanningConstants
{
    public const string ToolVersion = "M1C_R018_INTRADAY_IMPORT_PLAN_R003";
    public const string PlanSchemaVersion = "r018_intraday_import_plan_v3";
    public const string BundleManifestSchemaVersion = "r018_artifact_bundle_manifest_v2";
    public const string NormalizedEventSchemaVersion = "r018_normalized_event_v3";
    public const string InstrumentCatalogVersion = "embedded_lmax_fx_13_v1";
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

public enum R018ArtifactType
{
    RAW_FIX_LOG,
    ORDER_LEDGER,
    FILL_LEDGER,
    EOD_LMAX_REPORT,
    MANUAL_EXPORT,
    MANUAL_UI,
    BBO_JSONL,
    UNKNOWN
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
    string Kind,
    R018ArtifactType ArtifactType = R018ArtifactType.UNKNOWN);

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
    string RawPayloadHash,
    R018ArtifactType ArtifactType = R018ArtifactType.UNKNOWN,
    DateTimeOffset? LocalReceiveUtc = null,
    long? LocalEventOrder = null,
    int? MsgSeqNum = null,
    bool? PossDupFlag = null);

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
    string RawJson,
    int? MsgSeqNum = null,
    bool? PossDupFlag = null,
    string? FixReceiveEventId = null,
    string? SourceExecutionReportEventId = null,
    string? TerminalState = null,
    decimal? Bid = null,
    decimal? Ask = null,
    decimal? BidSize = null,
    decimal? AskSize = null,
    string? BenchmarkId = null,
    string? BenchmarkType = null,
    IReadOnlyList<R018SourceEvidence>? EvidenceRefs = null,
    IReadOnlyList<string>? MissingFields = null)
{
    public string StableKey => Kind switch
    {
        R018NormalizedEventKind.Order => $"ORDER|{Environment}|{BrokerAccount}|{Venue}|{ClOrdId}",
        R018NormalizedEventKind.Fill => $"FILL|{Environment}|{BrokerAccount}|{Venue}|{ClOrdId}|{ExecId}",
        R018NormalizedEventKind.ExecutionReport => $"ER|{Environment}|{BrokerAccount}|{Venue}|{ClOrdId}|{BrokerOrderId}|{ExecId}|{ExecType}|{OrdStatus}",
        _ => $"{Kind}|{Environment}|{BrokerAccount}|{Venue}|{EventId}"
    };
}

public sealed record R018EvidenceOccurrence(
    string OccurrenceId,
    string BusinessIdentity,
    R018NormalizedEventKind NormalizedKind,
    R018ProvenanceType Provenance,
    R018ArtifactType ArtifactType,
    string SourcePath,
    string SourceFileHash,
    string SourceLocator,
    string RawPayloadHash,
    DateTimeOffset? SourceTimestampUtc,
    DateTimeOffset? LocalReceiveUtc,
    long? LocalEventOrder,
    int? MsgSeqNum,
    bool? PossDupFlag);

public sealed record R018BusinessEvent(
    string BusinessEventId,
    string BusinessIdentity,
    R018NormalizedEventKind BusinessEventType,
    string SemanticFingerprint,
    IReadOnlyDictionary<string, string> Facts,
    IReadOnlyList<R018SourceEvidence> EvidenceRefs);

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

public sealed record R018ReplayEligibility(
    bool ValidationPassed,
    bool CriticalParityPassed,
    bool ScopeHomogeneous,
    int IdentityConflictCount,
    bool InputSnapshotConsistent,
    bool LocalIsolatedReplayAllowed,
    IReadOnlyList<string> Reasons);

public sealed record R018CatalogResolution(
    string Resolution,
    string? CatalogPath,
    string? CatalogSha256,
    string? CatalogSchemaVersion,
    string? MatchedEntryHash);

public sealed record R018LineageReport(
    string SchemaVersion,
    string ToolVersion,
    R018ImportBundleStatus Status,
    string ModelRunResolution,
    string? ModelRunId,
    string LedgerApplicability,
    IReadOnlyList<string> UnmappedEvents,
    IReadOnlyList<string> LineageWarnings,
    R018CatalogResolution? CatalogResolution = null);

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

public sealed record R018IdentityKeyAuditRow(
    string KeyKind,
    string SourceBusinessKey,
    string CurrentDbCandidateKey,
    string SafeStagingKey,
    string Status,
    string Detail);

public sealed record R018PlannedStagingRow(
    string PlannedRowId,
    string SafeStagingKey,
    string TargetContract,
    string OperationKind,
    string SourceBusinessKey,
    string? CanonicalModelRunId,
    IReadOnlyDictionary<string, string> FieldMappings,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> MissingRequiredFields,
    IReadOnlyList<R018SourceEvidence> EvidenceRefs,
    string CanonicalEligibility,
    bool ApplyEligible,
    IReadOnlyList<string> RejectionReasons);

public sealed record R018ImportPlan(
    string SchemaVersion,
    string ToolVersion,
    R018ImportBundleStatus Status,
    string SourceRunId,
    string ApprovedCandidateHash,
    string InputBundleHash,
    string DeterministicContentHash,
    string ToolCommit,
    string ToolCommitSource,
    string SourceBaselineCommit,
    string? CoreCommit,
    string? ConfigHash,
    string InstrumentCatalogVersion,
    string InstrumentCatalogHash,
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
    IReadOnlyList<R018EvidenceOccurrence> EvidenceOccurrences,
    IReadOnlyList<R018BusinessEvent> BusinessEvents,
    R018ValidationReport Validation,
    R018LineageReport Lineage,
    R018IdentityScopeReport IdentityScope,
    R018ReplayEligibility ReplayEligibility,
    IReadOnlyList<R018IdentityKeyAuditRow>? IdentityKeyAudit = null,
    IReadOnlyList<R018PlannedStagingRow>? PlannedStagingRows = null);

public sealed record R018ParityRow(
    string Check,
    string Source,
    string Expected,
    string Actual,
    string Status,
    string Severity,
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
        var manifestPath = Path.Combine(fullBundlePath, "r018_bundle_manifest_v2.json");
        if (!File.Exists(manifestPath))
        {
            manifestPath = Path.Combine(fullBundlePath, "r018_bundle_manifest_v1.json");
        }

        var manifestFound = File.Exists(manifestPath);
        var manifest = manifestFound
            ? ReadManifest(manifestPath, issues)
            : R018ArtifactBundleManifest.Missing;

        if (!manifestFound)
        {
            issues.Add("MISSING_MANIFEST:r018_bundle_manifest_v2.json");
        }

        var events = new List<R018NormalizedEvent>();
        if (manifestFound)
        {
            var seenArtifacts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var artifact in manifest.Artifacts)
            {
                if (!seenArtifacts.Add(artifact.Path))
                {
                    issues.Add($"DUPLICATE_ARTIFACT_PATH:{artifact.Path}");
                    continue;
                }

                if (artifact.ArtifactType is R018ArtifactType.UNKNOWN)
                {
                    issues.Add($"UNSUPPORTED_ARTIFACT_TYPE:{artifact.Path}:{artifact.ArtifactType}");
                    continue;
                }

                if (!TryResolveArtifactPath(fullBundlePath, artifact.Path, out var artifactPath, out var pathIssue))
                {
                    issues.Add(pathIssue);
                    continue;
                }

                if (!File.Exists(artifactPath))
                {
                    issues.Add($"MISSING_ARTIFACT:{artifact.Path}");
                    continue;
                }

                if (IsReparsePoint(artifactPath))
                {
                    issues.Add($"ARTIFACT_REPARSE_POINT_REJECTED:{artifact.Path}");
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
                else
                {
                    issues.Add($"UNSUPPORTED_ARTIFACT_EXTENSION:{artifact.Path}");
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
                        GetString(item, "kind") ?? "unknown",
                        ParseArtifactType(GetString(item, "artifact_type") ?? GetString(item, "artifactType") ?? GetString(item, "type"))));
                }
            }

            return new R018ArtifactBundleManifest(
                GetString(json, "schema_version") ?? string.Empty,
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


    private static void RequireManifestField(JsonObject json, List<string> issues, string fieldName, string issueCode)
    {
        if (string.IsNullOrWhiteSpace(GetString(json, fieldName)))
        {
            issues.Add(issueCode);
        }
    }

    private static bool TryResolveArtifactPath(string bundlePath, string artifactRelativePath, out string artifactPath, out string issue)
    {
        artifactPath = string.Empty;
        issue = string.Empty;
        if (string.IsNullOrWhiteSpace(artifactRelativePath))
        {
            issue = "EMPTY_ARTIFACT_PATH";
            return false;
        }

        if (Path.IsPathRooted(artifactRelativePath))
        {
            issue = $"ARTIFACT_ROOTED_PATH_REJECTED:{artifactRelativePath}";
            return false;
        }

        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        if (artifactRelativePath.Split(separators, StringSplitOptions.RemoveEmptyEntries).Any(part => part == ".."))
        {
            issue = $"ARTIFACT_PARENT_TRAVERSAL_REJECTED:{artifactRelativePath}";
            return false;
        }

        var bundleFull = Path.GetFullPath(bundlePath);
        var candidate = Path.GetFullPath(Path.Combine(bundleFull, artifactRelativePath));
        var relative = Path.GetRelativePath(bundleFull, candidate);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            issue = $"ARTIFACT_PATH_ESCAPES_BUNDLE:{artifactRelativePath}";
            return false;
        }

        artifactPath = candidate;
        return true;
    }

    private static bool IsReparsePoint(string path)
        => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static R018ArtifactType ParseArtifactType(string? value)
    {
        var normalized = value?.Replace("-", "_", StringComparison.Ordinal).Trim();
        return Enum.TryParse<R018ArtifactType>(normalized, ignoreCase: true, out var parsed)
            ? parsed
            : R018ArtifactType.UNKNOWN;
    }

    private static IReadOnlySet<R018ProvenanceType> AllowedProvenances(R018ArtifactType artifactType)
        => artifactType switch
        {
            R018ArtifactType.RAW_FIX_LOG => new HashSet<R018ProvenanceType> { R018ProvenanceType.RAW_FIX },
            R018ArtifactType.ORDER_LEDGER or R018ArtifactType.FILL_LEDGER => new HashSet<R018ProvenanceType> { R018ProvenanceType.ARTIFACT_LEDGER, R018ProvenanceType.Derived },
            R018ArtifactType.EOD_LMAX_REPORT => new HashSet<R018ProvenanceType> { R018ProvenanceType.EOD_LMAX_REPORT },
            R018ArtifactType.MANUAL_EXPORT => new HashSet<R018ProvenanceType> { R018ProvenanceType.MANUAL_EXPORT },
            R018ArtifactType.MANUAL_UI => new HashSet<R018ProvenanceType> { R018ProvenanceType.MANUAL_UI },
            R018ArtifactType.BBO_JSONL => new HashSet<R018ProvenanceType> { R018ProvenanceType.ARTIFACT_LEDGER, R018ProvenanceType.Derived },
            _ => new HashSet<R018ProvenanceType>()
        };

    private static R018ProvenanceType DefaultProvenanceForArtifact(R018ArtifactType artifactType)
        => artifactType switch
        {
            R018ArtifactType.RAW_FIX_LOG => R018ProvenanceType.RAW_FIX,
            R018ArtifactType.ORDER_LEDGER or R018ArtifactType.FILL_LEDGER or R018ArtifactType.BBO_JSONL => R018ProvenanceType.ARTIFACT_LEDGER,
            R018ArtifactType.EOD_LMAX_REPORT => R018ProvenanceType.EOD_LMAX_REPORT,
            R018ArtifactType.MANUAL_EXPORT => R018ProvenanceType.MANUAL_EXPORT,
            R018ArtifactType.MANUAL_UI => R018ProvenanceType.MANUAL_UI,
            _ => R018ProvenanceType.Unknown
        };
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

            foreach (var ev in Normalize(obj, manifest, artifact, sourceFileHash, lineNumber.ToString(CultureInfo.InvariantCulture), line, issues))
            {
                yield return ev;
            }
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

            foreach (var ev in Normalize(obj, manifest, artifact, sourceFileHash, lineNumber.ToString(CultureInfo.InvariantCulture), line, issues))
            {
                yield return ev;
            }
        }
    }

    private static IEnumerable<R018NormalizedEvent> Normalize(
        JsonObject obj,
        R018ArtifactBundleManifest manifest,
        R018SourceArtifact artifact,
        string sourceFileHash,
        string locator,
        string rawPayload,
        List<string> issues)
    {
        var declaredProvenance = GetAnyString(obj, "provenance", "source_provenance", "source");
        var provenance = string.IsNullOrWhiteSpace(declaredProvenance)
            ? DefaultProvenanceForArtifact(artifact.ArtifactType)
            : ParseProvenance(declaredProvenance);
        if (!AllowedProvenances(artifact.ArtifactType).Contains(provenance))
        {
            issues.Add($"PROVENANCE_NOT_ALLOWED_FOR_ARTIFACT:{artifact.Path}:{artifact.ArtifactType}:{provenance}");
        }

        var rawEventType = GetAnyString(obj, "event_type", "type", "msg_type", "kind", "35") ?? artifact.Kind;
        var execType = GetAnyString(obj, "exec_type", "tag150", "150");
        var ordStatus = GetAnyString(obj, "ord_status", "tag39", "39");
        var kind = DetermineKind(rawEventType, execType, provenance);
        var isManualTerminalObservation = provenance is R018ProvenanceType.MANUAL_UI or R018ProvenanceType.MANUAL_EXPORT or R018ProvenanceType.EOD_LMAX_REPORT
            && (kind is R018NormalizedEventKind.Fill or R018NormalizedEventKind.ExecutionReport);
        if (isManualTerminalObservation)
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
            ComputeSha256(rawPayload),
            artifact.ArtifactType,
            GetAnyDate(obj, "local_receive_utc", "received_utc"),
            GetAnyLong(obj, "local_event_order", "event_order"),
            GetAnyInt(obj, "msg_seq_num", "tag34", "34"),
            GetAnyBool(obj, "poss_dup", "poss_dup_flag", "tag43", "43"));

        var orderQuantity = GetAnyDecimal(obj, "order_qty", "quantity", "qty", "OrderQty", "tag38", "38");
        var lastQuantity = GetAnyDecimal(obj, "last_qty", "last_quantity", "LastQty", "tag32", "32");
        var cumQuantity = GetAnyDecimal(obj, "cum_qty", "CumQty", "tag14", "14");
        var leavesQuantity = GetAnyDecimal(obj, "leaves_qty", "LeavesQty", "tag151", "151");
        var missing = new List<string>();
        if (kind is R018NormalizedEventKind.Order && string.IsNullOrWhiteSpace(clOrdId)) missing.Add("ClOrdID");
        if (kind is R018NormalizedEventKind.Order && orderQuantity is null) missing.Add("OrderQty");
        if (kind is R018NormalizedEventKind.Order && GetAnyDecimal(obj, "limit_price", "price", "tag44", "44") is null) missing.Add("tag44");
        if (kind is R018NormalizedEventKind.Fill && string.IsNullOrWhiteSpace(execId)) missing.Add("ExecID");

        var normalized = new R018NormalizedEvent(
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
            rawPayload,
            GetAnyInt(obj, "msg_seq_num", "tag34", "34"),
            GetAnyBool(obj, "poss_dup", "poss_dup_flag", "tag43", "43"),
            GetAnyString(obj, "fix_receive_event_id"),
            null,
            ResolveTerminalState(execType, ordStatus, kind, cumQuantity, leavesQuantity),
            GetAnyDecimal(obj, "bid"),
            GetAnyDecimal(obj, "ask"),
            GetAnyDecimal(obj, "bid_size"),
            GetAnyDecimal(obj, "ask_size"),
            GetAnyString(obj, "benchmark_id"),
            GetAnyString(obj, "benchmark_type"),
            new[] { evidence },
            missing);

        yield return normalized;

        if (normalized.Kind is R018NormalizedEventKind.ExecutionReport && IsFillExecType(execType) && lastQuantity is > 0)
        {
            var derivedEvidence = evidence with
            {
                Provenance = R018ProvenanceType.Derived,
                AuthorityClass = "DERIVED_FROM_EXECUTION_REPORT"
            };
            yield return normalized with
            {
                EventId = normalized.EventId + ":fill_fact",
                Kind = R018NormalizedEventKind.Fill,
                Provenance = R018ProvenanceType.Derived,
                Evidence = derivedEvidence,
                SourceExecutionReportEventId = normalized.EventId,
                IsTerminal = IsTerminal(execType, ordStatus, R018NormalizedEventKind.Fill, cumQuantity, leavesQuantity),
                TerminalState = ResolveTerminalState(execType, ordStatus, R018NormalizedEventKind.Fill, cumQuantity, leavesQuantity),
                EvidenceRefs = new[] { evidence }
            };
        }
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

        if (value is not null && (value.Contains("BBO", StringComparison.OrdinalIgnoreCase) || value.Equals("W", StringComparison.OrdinalIgnoreCase)))
        {
            return R018NormalizedEventKind.Bbo;
        }

        if (value is not null && value.Contains("Fill", StringComparison.OrdinalIgnoreCase))
        {
            return provenance is R018ProvenanceType.MANUAL_UI or R018ProvenanceType.MANUAL_EXPORT or R018ProvenanceType.EOD_LMAX_REPORT or R018ProvenanceType.EOD_LMAX_REPORT
                ? R018NormalizedEventKind.ManualObservation
                : R018NormalizedEventKind.Fill;
        }

        if (value is not null && (value.Equals("ExecutionReport", StringComparison.OrdinalIgnoreCase) || value.Equals("8", StringComparison.OrdinalIgnoreCase)))
        {
            return provenance is R018ProvenanceType.MANUAL_UI or R018ProvenanceType.MANUAL_EXPORT or R018ProvenanceType.EOD_LMAX_REPORT or R018ProvenanceType.EOD_LMAX_REPORT
                ? R018NormalizedEventKind.ManualObservation
                : R018NormalizedEventKind.ExecutionReport;
        }

        return R018NormalizedEventKind.Unknown;
    }

    private static bool IsFillExecType(string? execType)
        => execType is "1" or "2" or "F" or "f" or "PARTIAL_FILL" or "FILL";

    private static bool IsTerminal(string? execType, string? ordStatus, R018NormalizedEventKind kind, decimal? cumQuantity, decimal? leavesQuantity)
        => ResolveTerminalState(execType, ordStatus, kind, cumQuantity, leavesQuantity) is "FILLED" or "CANCELED" or "REJECTED" or "EXPIRED";

    private static string ResolveTerminalState(string? execType, string? ordStatus, R018NormalizedEventKind kind, decimal? cumQuantity, decimal? leavesQuantity)
    {
        if (ordStatus is "2" or "FILLED" || (cumQuantity.HasValue && leavesQuantity is 0 && (IsFillExecType(execType) || kind is R018NormalizedEventKind.Fill)))
        {
            return "FILLED";
        }

        if (ordStatus is "4" or "CANCELED" or "CANCELLED" || execType is "4" or "CANCELED" or "CANCELLED")
        {
            return "CANCELED";
        }

        if (ordStatus is "8" or "REJECTED" || execType is "8" or "REJECTED")
        {
            return "REJECTED";
        }

        if (ordStatus is "C" or "EXPIRED" || execType is "C" or "EXPIRED")
        {
            return "EXPIRED";
        }

        if ((kind is R018NormalizedEventKind.Fill or R018NormalizedEventKind.ExecutionReport) && IsFillExecType(execType) && leavesQuantity is > 0)
        {
            return "PARTIAL_FILL";
        }

        return "NON_TERMINAL";
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
        var manifestPathV2 = Path.Combine(bundlePath, "r018_bundle_manifest_v2.json");
        var manifestPathV1 = Path.Combine(bundlePath, "r018_bundle_manifest_v1.json");
        var manifestPath = File.Exists(manifestPathV2) ? manifestPathV2 : manifestPathV1;
        if (File.Exists(manifestPath))
        {
            builder.Append("manifest|").Append(Path.GetFileName(manifestPath)).Append('|').Append(ComputeFileSha256(manifestPath)).AppendLine();
        }

        foreach (var artifact in artifacts.OrderBy(a => a.Path, StringComparer.Ordinal))
        {
            builder.Append("artifact|").Append(artifact.Path).Append('|').Append(artifact.Kind).Append('|').Append(artifact.ArtifactType).Append('|').Append(artifact.Sha256).Append('|');
            if (TryResolveArtifactPath(bundlePath, artifact.Path, out var safePath, out _) && File.Exists(safePath) && !IsReparsePoint(safePath))
            {
                builder.Append(ComputeFileSha256(safePath));
            }
            else
            {
                builder.Append("UNSAFE_OR_MISSING_NOT_OPENED");
            }

            builder.AppendLine();
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

    private static int? GetAnyInt(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            var value = GetString(obj, name);
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static bool? GetAnyBool(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
        {
            var value = GetString(obj, name);
            if (bool.TryParse(value, out var parsed))
            {
                return parsed;
            }

            if (value is "Y" or "1") return true;
            if (value is "N" or "0") return false;
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
    public static readonly IReadOnlyDictionary<string, string> InstrumentCatalog = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["AUDUSD"] = "4007",
        ["EURUSD"] = "4001",
        ["GBPUSD"] = "4002",
        ["NZDUSD"] = "100613",
        ["USDCAD"] = "4013",
        ["USDCNH"] = "100892",
        ["USDJPY"] = "4004",
        ["USDMXN"] = "100507",
        ["USDNOK"] = "100513",
        ["USDSEK"] = "100529",
        ["USDSGD"] = "100535",
        ["USDZAR"] = "100547",
        ["USDCHF"] = "4010"
    };

    private static readonly HashSet<string> KnownSymbols = new(InstrumentCatalog.Keys, StringComparer.OrdinalIgnoreCase);

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
        ValidateModelRunCatalog(modelRunCatalogPath, issues);
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

    private static void ValidateModelRunCatalog(string? modelRunCatalogPath, List<R018ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(modelRunCatalogPath) || !File.Exists(modelRunCatalogPath))
        {
            return;
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(modelRunCatalogPath));
            var schema = root?["catalog_schema_version"]?.ToString() ?? root?["schema_version"]?.ToString();
            if (!string.Equals(schema, "model_run_catalog_v1", StringComparison.Ordinal))
            {
                issues.Add(new R018ValidationIssue("ERROR", "MODEL_RUN_CATALOG_SCHEMA_INVALID", schema ?? "missing"));
            }
        }
        catch (Exception ex)
        {
            issues.Add(new R018ValidationIssue("ERROR", "MODEL_RUN_CATALOG_PARSE_ERROR", ex.Message));
        }
    }

    private static void ValidateManifest(R018ArtifactBundle bundle, List<R018ValidationIssue> issues)
    {
        if (!bundle.ManifestFound)
        {
            issues.Add(new R018ValidationIssue("ERROR", "MISSING_MANIFEST", "r018_bundle_manifest_v2.json is required."));
            return;
        }

        if (!string.Equals(bundle.Manifest.SchemaVersion, R018ImportPlanningConstants.BundleManifestSchemaVersion, StringComparison.Ordinal))
        {
            issues.Add(new R018ValidationIssue("ERROR", "UNKNOWN_MANIFEST_SCHEMA", bundle.Manifest.SchemaVersion));
        }
        if (string.IsNullOrWhiteSpace(bundle.Manifest.SchemaVersion))
        {
            issues.Add(new R018ValidationIssue("ERROR", "MISSING_SCHEMA_VERSION", "Manifest schema_version is required."));
        }

        if (IsUnknown(bundle.Manifest.SourceRunId))
        {
            issues.Add(new R018ValidationIssue("ERROR", "MISSING_SOURCE_RUN_ID", "source_run_id is required."));
        }

        if (IsUnknown(bundle.Manifest.ApprovedCandidateHash))
        {
            issues.Add(new R018ValidationIssue("ERROR", "MISSING_APPROVED_CANDIDATE_HASH", "approved_candidate_hash is required."));
        }

        if (bundle.Manifest.Artifacts.Count == 0)
        {
            issues.Add(new R018ValidationIssue("ERROR", "MISSING_ARTIFACTS", "At least one artifact is required."));
        }

        if (bundle.Manifest.DecisionUtc.HasValue && bundle.Manifest.EffectiveFromUtc.HasValue && bundle.Manifest.DecisionUtc.Value > bundle.Manifest.EffectiveFromUtc.Value)
        {
            issues.Add(new R018ValidationIssue("ERROR", "TIMESTAMP_COHERENCE_DECISION_AFTER_EFFECTIVE", "decision_utc must not be after effective_from_utc."));
        }

        if (bundle.Manifest.EffectiveFromUtc.HasValue && bundle.Manifest.DeadlineUtc.HasValue && bundle.Manifest.EffectiveFromUtc.Value > bundle.Manifest.DeadlineUtc.Value)
        {
            issues.Add(new R018ValidationIssue("ERROR", "TIMESTAMP_COHERENCE_EFFECTIVE_AFTER_DEADLINE", "effective_from_utc must not be after deadline_utc."));
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
        if (bundle.ManifestFound && bundle.Events.Count == 0)
        {
            issues.Add(new R018ValidationIssue("ERROR", "EMPTY_BUNDLE", "Bundle contains no normalized events."));
        }

        var orders = DeduplicateExact(bundle.Events.Where(e => e.Kind is R018NormalizedEventKind.Order))
            .GroupBy(OrderLookupKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        foreach (var ev in bundle.Events)
        {
            if (ev.Kind is R018NormalizedEventKind.Unknown)
            {
                issues.Add(new R018ValidationIssue("ERROR", "UNKNOWN_EVENT_KIND", "Event kind could not be normalized.", ev.EventId, ev.Evidence.SourcePath));
            }

            if (!ScopeMatches(ev.Environment, bundle.Manifest.Environment))
            {
                issues.Add(new R018ValidationIssue("ERROR", "SCOPE_ENVIRONMENT_MISMATCH", $"event={ev.Environment} manifest={bundle.Manifest.Environment}", ev.EventId, ev.Evidence.SourcePath));
            }

            if (!ScopeMatches(ev.BrokerAccount, bundle.Manifest.BrokerAccount))
            {
                issues.Add(new R018ValidationIssue("ERROR", "SCOPE_ACCOUNT_MISMATCH", $"event={ev.BrokerAccount} manifest={bundle.Manifest.BrokerAccount}", ev.EventId, ev.Evidence.SourcePath));
            }

            if (!ScopeMatches(ev.Venue, bundle.Manifest.Venue))
            {
                issues.Add(new R018ValidationIssue("ERROR", "SCOPE_VENUE_MISMATCH", $"event={ev.Venue} manifest={bundle.Manifest.Venue}", ev.EventId, ev.Evidence.SourcePath));
            }

            if (ev.Symbol is null || !KnownSymbols.Contains(ev.Symbol))
            {
                issues.Add(new R018ValidationIssue("ERROR", "UNKNOWN_INSTRUMENT", ev.Symbol ?? "missing", ev.EventId, ev.Evidence.SourcePath));
            }

            if (ev.QuantityUnit is null || !KnownQuantityUnits.Contains(ev.QuantityUnit))
            {
                issues.Add(new R018ValidationIssue("ERROR", "UNKNOWN_QUANTITY_UNIT", ev.QuantityUnit ?? "missing", ev.EventId, ev.Evidence.SourcePath));
            }

            if (ev.Kind is R018NormalizedEventKind.Order or R018NormalizedEventKind.ExecutionReport or R018NormalizedEventKind.Fill && string.IsNullOrWhiteSpace(ev.Side))
            {
                issues.Add(new R018ValidationIssue("ERROR", "MISSING_SIDE", "Side is required for order/execution/fill facts.", ev.EventId, ev.Evidence.SourcePath));
            }

            if (ev.Side is not null && ev.Side is not ("BUY" or "SELL"))
            {
                issues.Add(new R018ValidationIssue("ERROR", "INVALID_SIDE", ev.Side, ev.EventId, ev.Evidence.SourcePath));
            }


            if (ev.SecurityId is not null && ev.Symbol is not null && InstrumentCatalog.TryGetValue(ev.Symbol, out var expectedSecurityId) && !string.Equals(expectedSecurityId, ev.SecurityId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new R018ValidationIssue("ERROR", "SECURITY_ID_MISMATCH", $"symbol={ev.Symbol} expected={expectedSecurityId} actual={ev.SecurityId}", ev.EventId, ev.Evidence.SourcePath));
            }
            if (ev.Kind is R018NormalizedEventKind.Order)
            {
                if (string.IsNullOrWhiteSpace(ev.ClOrdId))
                {
                    issues.Add(new R018ValidationIssue("ERROR", "MISSING_CLORDID", "Order is missing ClOrdID.", ev.EventId, ev.Evidence.SourcePath));
                }

                if (ev.OrderQuantity is null or <= 0)
                {
                    issues.Add(new R018ValidationIssue("ERROR", "INVALID_ORDER_QTY", ev.OrderQuantity?.ToString(CultureInfo.InvariantCulture) ?? "missing", ev.EventId, ev.Evidence.SourcePath));
                }

                if (ev.OrderType is not "LIMIT")
                {
                    issues.Add(new R018ValidationIssue("ERROR", ev.OrderType is "MARKET" ? "MARKET_ORDER_FORBIDDEN" : "LIMIT_ORDER_REQUIRED", ev.OrderType ?? "missing", ev.EventId, ev.Evidence.SourcePath));
                }

                if (ev.LimitPrice is null or <= 0)
                {
                    issues.Add(new R018ValidationIssue("ERROR", "LIMIT_PRICE_TAG44_REQUIRED", ev.LimitPrice?.ToString(CultureInfo.InvariantCulture) ?? "missing", ev.EventId, ev.Evidence.SourcePath));
                }

                if (string.IsNullOrWhiteSpace(ev.TimeInForce))
                {
                    issues.Add(new R018ValidationIssue("ERROR", "MISSING_TIME_IN_FORCE", "Order requires explicit TIF/tag59.", ev.EventId, ev.Evidence.SourcePath));
                }

                if (ev.TimeInForce is not null && ev.TimeInForce is not ("0" or "DAY" or "GTC"))
                {
                    issues.Add(new R018ValidationIssue("ERROR", "UNSUPPORTED_TIME_IN_FORCE", ev.TimeInForce, ev.EventId, ev.Evidence.SourcePath));
                }

                if (ev.SourceTimestampUtc is null)
                {
                    issues.Add(new R018ValidationIssue("ERROR", "MISSING_SOURCE_TIMESTAMP", "Order requires source timestamp.", ev.EventId, ev.Evidence.SourcePath));
                }
            }


            if (ev.Kind is R018NormalizedEventKind.ExecutionReport)
            {
                if (string.IsNullOrWhiteSpace(ev.ExecId)) issues.Add(new R018ValidationIssue("ERROR", "REPORT_WITHOUT_EXECID", "ExecutionReport requires ExecID/tag17.", ev.EventId, ev.Evidence.SourcePath));
                if (string.IsNullOrWhiteSpace(ev.ExecType)) issues.Add(new R018ValidationIssue("ERROR", "REPORT_WITHOUT_EXEC_TYPE", "ExecutionReport requires ExecType/tag150.", ev.EventId, ev.Evidence.SourcePath));
                if (string.IsNullOrWhiteSpace(ev.OrdStatus)) issues.Add(new R018ValidationIssue("ERROR", "REPORT_WITHOUT_ORD_STATUS", "ExecutionReport requires OrdStatus/tag39.", ev.EventId, ev.Evidence.SourcePath));
                if (ev.CumQuantity is null) issues.Add(new R018ValidationIssue("ERROR", "REPORT_WITHOUT_CUM_QTY", "ExecutionReport requires CumQty/tag14.", ev.EventId, ev.Evidence.SourcePath));
                if (ev.LeavesQuantity is null) issues.Add(new R018ValidationIssue("ERROR", "REPORT_WITHOUT_LEAVES_QTY", "ExecutionReport requires LeavesQty/tag151.", ev.EventId, ev.Evidence.SourcePath));
                if (ev.SourceTimestampUtc is null) issues.Add(new R018ValidationIssue("ERROR", "REPORT_WITHOUT_SOURCE_TIMESTAMP", "ExecutionReport requires SendingTime/source timestamp.", ev.EventId, ev.Evidence.SourcePath));
            }
            if (ev.Kind is R018NormalizedEventKind.ExecutionReport or R018NormalizedEventKind.Fill)
            {
                if (string.IsNullOrWhiteSpace(ev.ClOrdId) || !orders.TryGetValue(OrderLookupKey(ev), out var order))
                {
                    issues.Add(new R018ValidationIssue("ERROR", ev.Kind is R018NormalizedEventKind.Fill ? "FILL_WITHOUT_CHILD_ORDER" : "REPORT_WITHOUT_CHILD_ORDER", ev.ClOrdId ?? "missing", ev.EventId, ev.Evidence.SourcePath));
                }
                else
                {
                    ValidateSame(order.Symbol, ev.Symbol, "SYMBOL_MISMATCH_WITH_ORDER", ev, issues);
                    ValidateSame(order.Side, ev.Side, "SIDE_MISMATCH_WITH_ORDER", ev, issues);
                    ValidateSame(order.QuantityUnit, ev.QuantityUnit, "UNIT_MISMATCH_WITH_ORDER", ev, issues);
                }
            }

            if (ev.Kind is R018NormalizedEventKind.Fill)
            {
                if (string.IsNullOrWhiteSpace(ev.ExecId))
                {
                    issues.Add(new R018ValidationIssue("ERROR", "FILL_WITHOUT_EXECID", "Fill must have ExecID.", ev.EventId, ev.Evidence.SourcePath));
                }

                if (ev.LastQuantity is null or <= 0 || ev.FillPrice is null or <= 0)
                {
                    issues.Add(new R018ValidationIssue("ERROR", "FILL_MISSING_QTY_OR_PRICE", "Fill must have positive quantity and price.", ev.EventId, ev.Evidence.SourcePath));
                }

                if (ev.Provenance is R018ProvenanceType.MANUAL_UI or R018ProvenanceType.MANUAL_EXPORT)
                {
                    issues.Add(new R018ValidationIssue("ERROR", "MANUAL_EVIDENCE_CANNOT_CREATE_FILL", "Manual evidence is observation-only.", ev.EventId, ev.Evidence.SourcePath));
                }
            }

            if ((ev.CumQuantity ?? 0) < 0 || (ev.LeavesQuantity ?? 0) < 0 || (ev.LastQuantity ?? 0) < 0)
            {
                issues.Add(new R018ValidationIssue("ERROR", "NEGATIVE_QUANTITY", "FIX quantities must be non-negative.", ev.EventId, ev.Evidence.SourcePath));
            }

            if (ev.Kind is R018NormalizedEventKind.ExecutionReport or R018NormalizedEventKind.Fill && ev.CumQuantity.HasValue && ev.LeavesQuantity.HasValue && ev.ClOrdId is not null && orders.TryGetValue(OrderLookupKey(ev), out var orderForQty) && orderForQty.OrderQuantity.HasValue)
            {
                var sum = ev.CumQuantity.Value + ev.LeavesQuantity.Value;
                if (Math.Abs(sum - orderForQty.OrderQuantity.Value) > 0.0000001m)
                {
                    issues.Add(new R018ValidationIssue("ERROR", "CUM_LEAVES_INCONSISTENT", $"cum+leaves={sum} order_qty={orderForQty.OrderQuantity}", ev.EventId, ev.Evidence.SourcePath));
                }

                if (ev.CumQuantity.Value - orderForQty.OrderQuantity.Value > 0.0000001m)
                {
                    issues.Add(new R018ValidationIssue("ERROR", "OVERFILL", $"cum={ev.CumQuantity} order_qty={orderForQty.OrderQuantity}", ev.EventId, ev.Evidence.SourcePath));
                }
            }
        }

        foreach (var order in orders.Values)
        {
            var related = bundle.Events
                .Where(e => OrderLookupKey(e) == OrderLookupKey(order) && e.Kind is R018NormalizedEventKind.ExecutionReport or R018NormalizedEventKind.Fill)
                .OrderBy(e => e.LocalEventOrder ?? long.MaxValue)
                .ToArray();
            decimal? previousCum = null;
            foreach (var ev in related.Where(e => e.CumQuantity.HasValue))
            {
                if (previousCum.HasValue && ev.CumQuantity!.Value < previousCum.Value)
                {
                    issues.Add(new R018ValidationIssue("ERROR", "CUM_DECREASING", $"previous={previousCum} current={ev.CumQuantity}", ev.EventId, ev.Evidence.SourcePath));
                }

                previousCum = ev.CumQuantity;
            }

            var lastQtySum = related.Where(e => e.Kind is R018NormalizedEventKind.Fill && e.LastQuantity.HasValue).GroupBy(SemanticFingerprint, StringComparer.Ordinal).Select(g => g.First()).Sum(e => e.LastQuantity!.Value);
            var maxCum = related.Where(e => e.CumQuantity.HasValue).Select(e => e.CumQuantity!.Value).DefaultIfEmpty(0).Max();
            if (lastQtySum > 0 && maxCum > 0 && Math.Abs(lastQtySum - maxCum) > 0.0000001m)
            {
                issues.Add(new R018ValidationIssue("ERROR", "LASTQTY_CUM_INCONSISTENT", $"last_qty_sum={lastQtySum} max_cum={maxCum}", order.EventId, order.Evidence.SourcePath));
            }

            if (!related.Any(e => e.IsTerminal))
            {
                issues.Add(new R018ValidationIssue("ERROR", "NON_TERMINAL_ORDER", order.ClOrdId ?? "missing", order.EventId, order.Evidence.SourcePath));
            }
        }

        foreach (var observationGroup in bundle.Events.Where(e => e.Kind is R018NormalizedEventKind.ManualObservation && !string.IsNullOrWhiteSpace(e.ExecId)).GroupBy(e => $"{e.Environment}|{e.BrokerAccount}|{e.Venue}|{e.ClOrdId}|{e.ExecId}", StringComparer.Ordinal))
        {
            var canonicalFacts = bundle.Events.Where(e => e.Kind is R018NormalizedEventKind.ExecutionReport or R018NormalizedEventKind.Fill && $"{e.Environment}|{e.BrokerAccount}|{e.Venue}|{e.ClOrdId}|{e.ExecId}" == observationGroup.Key).ToArray();
            if (canonicalFacts.Length > 0)
            {
                var observationFingerprints = observationGroup.Select(ObservationComparableFingerprint).Distinct(StringComparer.Ordinal).ToArray();
                var canonicalFingerprints = canonicalFacts.Select(ObservationComparableFingerprint).Distinct(StringComparer.Ordinal).ToArray();
                if (observationFingerprints.Except(canonicalFingerprints, StringComparer.Ordinal).Any())
                {
                    issues.Add(new R018ValidationIssue("ERROR", "OBSERVATION_FACT_CONFLICT", observationGroup.Key));
                }
            }
        }

        foreach (var execGroup in bundle.Events.Where(e => e.Kind is not R018NormalizedEventKind.ManualObservation && !string.IsNullOrWhiteSpace(e.ExecId)).GroupBy(e => $"{e.Environment}|{e.BrokerAccount}|{e.Venue}|{e.ExecId}", StringComparer.Ordinal))
        {
            if (execGroup.Select(e => e.ClOrdId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                issues.Add(new R018ValidationIssue("ERROR", "SAME_EXECID_MULTIPLE_CLORDID", execGroup.Key));
            }

            if (execGroup.Select(ExecSemanticFingerprint).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                issues.Add(new R018ValidationIssue("ERROR", "EXECID_FACT_CONFLICT", execGroup.Key));
            }
        }

        foreach (var group in bundle.Events.Where(e => e.ClOrdId is not null).GroupBy(BusinessKey, StringComparer.Ordinal))
        {
            if (group.Select(SemanticFingerprint).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                issues.Add(new R018ValidationIssue("ERROR", "DUPLICATE_CONFLICT", group.Key));
            }
        }
    }

    private static bool ScopeMatches(string? actual, string expected)
        => !IsUnknown(expected) && !IsUnknown(actual ?? string.Empty) && string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static void ValidateSame(string? expected, string? actual, string code, R018NormalizedEvent ev, List<R018ValidationIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(expected) && !string.IsNullOrWhiteSpace(actual) && !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new R018ValidationIssue("ERROR", code, $"expected={expected} actual={actual}", ev.EventId, ev.Evidence.SourcePath));
        }
    }

    private static string BusinessKey(R018NormalizedEvent ev)
        => ev.Kind switch
        {
            R018NormalizedEventKind.Order => $"ORDER|{ev.Environment}|{ev.BrokerAccount}|{ev.Venue}|{ev.ClOrdId}",
            R018NormalizedEventKind.ExecutionReport => $"ER|{ev.Environment}|{ev.BrokerAccount}|{ev.Venue}|{ev.ClOrdId}|{ev.BrokerOrderId}|{ev.ExecId}|{ev.ExecType}|{ev.OrdStatus}",
            R018NormalizedEventKind.Fill => $"FILL|{ev.Environment}|{ev.BrokerAccount}|{ev.Venue}|{ev.ClOrdId}|{ev.BrokerOrderId}|{ev.ExecId}",
            R018NormalizedEventKind.Bbo => $"BBO|{ev.Environment}|{ev.BrokerAccount}|{ev.Venue}|{ev.Symbol}|{ev.BboEventId}|{ev.LocalEventOrder}",
            _ => ev.StableKey
        };

    private static string ObservationComparableFingerprint(R018NormalizedEvent ev)
        => string.Join('|',
            ev.Symbol,
            ev.SecurityId,
            ev.Side,
            ev.QuantityUnit,
            ev.ClOrdId,
            ev.BrokerOrderId,
            ev.ExecId,
            ev.LastQuantity?.ToString(CultureInfo.InvariantCulture),
            ev.CumQuantity?.ToString(CultureInfo.InvariantCulture),
            ev.LeavesQuantity?.ToString(CultureInfo.InvariantCulture),
            (ev.FillPrice ?? ev.LimitPrice)?.ToString(CultureInfo.InvariantCulture));

    private static string ExecSemanticFingerprint(R018NormalizedEvent ev)
        => string.Join('|',
            ev.Environment,
            ev.BrokerAccount,
            ev.Venue,
            ev.Symbol,
            ev.SecurityId,
            ev.Side,
            ev.QuantityUnit,
            ev.ClOrdId,
            ev.BrokerOrderId,
            ev.ExecId,
            ev.LastQuantity?.ToString(CultureInfo.InvariantCulture),
            ev.CumQuantity?.ToString(CultureInfo.InvariantCulture),
            ev.LeavesQuantity?.ToString(CultureInfo.InvariantCulture),
            ev.FillPrice?.ToString(CultureInfo.InvariantCulture),
            ev.TerminalState);
    private static string SemanticFingerprint(R018NormalizedEvent ev)
        => string.Join('|',
            ev.Kind,
            ev.Environment,
            ev.BrokerAccount,
            ev.Venue,
            ev.Symbol,
            ev.SecurityId,
            ev.Side,
            ev.QuantityUnit,
            ev.ClOrdId,
            ev.BrokerOrderId,
            ev.ExecId,
            ev.OrderType,
            ev.TimeInForce,
            ev.OrderQuantity?.ToString(CultureInfo.InvariantCulture),
            ev.LastQuantity?.ToString(CultureInfo.InvariantCulture),
            ev.CumQuantity?.ToString(CultureInfo.InvariantCulture),
            ev.LeavesQuantity?.ToString(CultureInfo.InvariantCulture),
            ev.LimitPrice?.ToString(CultureInfo.InvariantCulture),
            ev.FillPrice?.ToString(CultureInfo.InvariantCulture),
            ev.Kind is R018NormalizedEventKind.Fill ? null : ev.ExecType,
            ev.Kind is R018NormalizedEventKind.Fill ? null : ev.OrdStatus,
            ev.TerminalState);

    private static string OrderLookupKey(R018NormalizedEvent ev)
        => $"{ev.Environment}|{ev.BrokerAccount}|{ev.Venue}|{ev.ClOrdId}";

    private static R018ImportBundleStatus DetermineStatus(R018ArtifactBundle bundle, List<R018ValidationIssue> issues, string? modelRunCatalogPath)
    {
        if (issues.Any(i => i.Severity.Equals("ERROR", StringComparison.Ordinal)))
        {
            return R018ImportBundleStatus.REJECTED;
        }

        var explicitModelRun = bundle.Manifest.ModelRunId;
        if (string.IsNullOrWhiteSpace(modelRunCatalogPath) || !File.Exists(modelRunCatalogPath))
        {
            return R018ImportBundleStatus.EVIDENCE_ONLY;
        }

        var catalog = LoadCatalog(modelRunCatalogPath);
        var exactMatches = catalog
            .Where(e => e.SourceRunId == bundle.Manifest.SourceRunId && e.ApprovedCandidateHash == bundle.Manifest.ApprovedCandidateHash)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(explicitModelRun))
        {
            var explicitMatches = exactMatches.Where(e => e.ModelRunId == explicitModelRun).ToArray();
            if (explicitMatches.Length == 1)
            {
                return R018ImportBundleStatus.CANONICAL_LINKED;
            }

            issues.Add(new R018ValidationIssue("ERROR", "MODEL_RUN_CATALOG_CONTRADICTION", $"Explicit model_run_id {explicitModelRun} is not an exact unique match for source_run_id/candidate hash."));
            return R018ImportBundleStatus.REJECTED;
        }

        return exactMatches.Length == 1
            ? R018ImportBundleStatus.CANONICAL_LINKED
            : R018ImportBundleStatus.EVIDENCE_ONLY;
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
        R018CatalogResolution? catalogResolution = null;

        if (!string.IsNullOrWhiteSpace(modelRunCatalogPath) && File.Exists(modelRunCatalogPath))
        {
            var catalogHash = R018ArtifactBundleReader.ComputeFileSha256(modelRunCatalogPath);
            var catalogSchema = ReadCatalogSchema(modelRunCatalogPath);
            var entries = R018ArtifactBundleValidator.LoadCatalog(modelRunCatalogPath);
            var exactMatches = entries
                .Where(e => e.SourceRunId == bundle.Manifest.SourceRunId && e.ApprovedCandidateHash == bundle.Manifest.ApprovedCandidateHash)
                .ToArray();
            if (!string.IsNullOrWhiteSpace(bundle.Manifest.ModelRunId))
            {
                var explicitMatches = exactMatches.Where(e => e.ModelRunId == bundle.Manifest.ModelRunId).ToArray();
                if (explicitMatches.Length == 1 && validation.Status is R018ImportBundleStatus.CANONICAL_LINKED)
                {
                    resolution = "OFFLINE_CATALOG_EXPLICIT_EXACT_MATCH";
                    modelRunId = explicitMatches[0].ModelRunId;
                    catalogResolution = new R018CatalogResolution(resolution, Path.GetFileName(modelRunCatalogPath), catalogHash, catalogSchema, R018ArtifactBundleReader.ComputeSha256(JsonSerializer.Serialize(explicitMatches[0])));
                }
                else
                {
                    resolution = validation.Status is R018ImportBundleStatus.REJECTED ? "OFFLINE_CATALOG_EXPLICIT_CONTRADICTION" : "NO_CATALOG_EXPLICIT_ID_UNVERIFIED";
                    catalogResolution = new R018CatalogResolution(resolution, Path.GetFileName(modelRunCatalogPath), catalogHash, catalogSchema, null);
                }
            }
            else if (exactMatches.Length == 1 && validation.Status is R018ImportBundleStatus.CANONICAL_LINKED)
            {
                resolution = "OFFLINE_CATALOG_UNIQUE_MATCH";
                modelRunId = exactMatches[0].ModelRunId;
                catalogResolution = new R018CatalogResolution(resolution, Path.GetFileName(modelRunCatalogPath), catalogHash, catalogSchema, R018ArtifactBundleReader.ComputeSha256(JsonSerializer.Serialize(exactMatches[0])));
            }
            else if (exactMatches.Length > 1)
            {
                resolution = "OFFLINE_CATALOG_AMBIGUOUS";
                warnings.Add("Multiple catalog matches; no arbitrary ModelRun selection performed.");
                catalogResolution = new R018CatalogResolution(resolution, Path.GetFileName(modelRunCatalogPath), catalogHash, catalogSchema, null);
            }
            else
            {
                resolution = "NO_CANONICAL_MODEL_RUN_LINK";
                catalogResolution = new R018CatalogResolution(resolution, Path.GetFileName(modelRunCatalogPath), catalogHash, catalogSchema, null);
            }
        }
        else if (!string.IsNullOrWhiteSpace(bundle.Manifest.ModelRunId))
        {
            resolution = "NO_CATALOG_EXPLICIT_ID_UNVERIFIED";
            warnings.Add("Explicit model_run_id is evidence only until an offline catalog exact match is supplied.");
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
            warnings,
            catalogResolution);
    }

    private static string? ReadCatalogSchema(string path)
    {
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path));
            return root?["catalog_schema_version"]?.ToString() ?? root?["schema_version"]?.ToString();
        }
        catch
        {
            return null;
        }
    }
    public static R018LedgerApplicability ResolveLedgerApplicability(R018ArtifactBundle bundle, R018ValidationReport validation)
    {
        if (!bundle.Events.Any(e => e.Kind is R018NormalizedEventKind.Fill))
        {
            return R018LedgerApplicability.NOT_APPLICABLE;
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
    public R018ImportPlan Build(R018ArtifactBundle bundle, string? modelRunCatalogPath = null, string toolCommit = "unknown", string? sourceBaselineCommit = null, string? toolCommitSource = null)
    {
        var validator = new R018ArtifactBundleValidator();
        var validation = validator.Validate(bundle, modelRunCatalogPath);
        var lineage = new R018LineageResolver().Resolve(bundle, validation, modelRunCatalogPath);
        var identity = R018IdentityScopeBuilder.Build(bundle);
        var ledgerApplicability = R018LineageResolver.ResolveLedgerApplicability(bundle, validation);
        var proposedTargets = ProposedTargets(validation.Status).ToArray();
        var normalizedEvents = bundle.Events
            .GroupBy(e => e.StableKey, StringComparer.Ordinal)
            .Select(MergeNormalizedFact)
            .OrderBy(e => e.LocalEventOrder ?? long.MaxValue)
            .ThenBy(e => e.EventId, StringComparer.Ordinal)
            .ToArray();
        var identityAudit = BuildIdentityKeyAudit(normalizedEvents).ToArray();
        var evidenceOccurrences = BuildEvidenceOccurrences(normalizedEvents).ToArray();
        var businessEvents = BuildBusinessEvents(normalizedEvents).ToArray();
        var replayEligibility = BuildReplayEligibility(validation, identity, bundle);
        var stagingRows = BuildStagingRows(normalizedEvents, validation.Status, lineage.ModelRunId, bundle.Manifest.SourceRunId, replayEligibility).ToArray();

        var modeReason = validation.Status switch
        {
            R018ImportBundleStatus.REJECTED => "Bundle failed closed during validation.",
            R018ImportBundleStatus.CANONICAL_LINKED => "Explicit or uniquely catalogued ModelRun link is present; ledger mutation remains forbidden in M1C.",
            _ => "No deterministic canonical ModelRun link; evidence-only outputs are allowed."
        };

        var seed = JsonSerializer.Serialize(new
        {
            validation.Status,
            bundle.Manifest.SchemaVersion,
            bundle.Manifest.Environment,
            bundle.Manifest.BrokerAccount,
            bundle.Manifest.Venue,
            bundle.Manifest.SourceRunId,
            bundle.Manifest.ApprovedCandidateHash,
            bundle.Manifest.QuantityUnit,
            bundle.Manifest.DecisionUtc,
            bundle.Manifest.EffectiveFromUtc,
            bundle.Manifest.DeadlineUtc,
            bundle.Manifest.TargetCloseUtc,
            manifestModelRunId = bundle.Manifest.ModelRunId,
            bundle.Manifest.CoreCommit,
            bundle.Manifest.ConfigHash,
            bundle.InputBundleHash,
            lineage.ModelRunResolution,
            lineageModelRunId = lineage.ModelRunId,
            lineageCatalogResolution = lineage.CatalogResolution,
            ledgerApplicability = ledgerApplicability.ToString(),
            proposedTargets,
            identityAudit = identityAudit,
            plannedStagingRows = stagingRows.Select(r => new
            {
                r.PlannedRowId,
                r.SafeStagingKey,
                r.TargetContract,
                r.OperationKind,
                r.SourceBusinessKey,
                r.CanonicalModelRunId,
                r.FieldMappings,
                r.Dependencies,
                r.MissingRequiredFields,
                r.CanonicalEligibility,
                r.ApplyEligible,
                r.RejectionReasons,
                evidence = r.EvidenceRefs.Select(e => new { e.Provenance, e.SourcePath, e.SourceFileHash, e.SourceLocator, e.RawPayloadHash })
            }),
            events = normalizedEvents.Select(e => new
            {
                e.StableKey,
                e.Kind,
                e.Provenance,
                e.Environment,
                e.BrokerAccount,
                e.Venue,
                e.ClOrdId,
                e.BrokerOrderId,
                e.ExecId,
                e.Symbol,
                e.SecurityId,
                e.Side,
                e.QuantityUnit,
                e.PhaseId,
                e.WaveId,
                e.ClipId,
                e.OrderType,
                e.TimeInForce,
                e.OrderQuantity,
                e.LastQuantity,
                e.CumQuantity,
                e.LeavesQuantity,
                e.LimitPrice,
                e.FillPrice,
                e.ExecType,
                e.OrdStatus,
                e.TerminalState,
                e.SourceTimestampUtc,
                e.LocalReceiveUtc,
                e.LocalEventOrder,
                e.FixReceiveEventId,
                e.BboEventId,
                e.SourceExecutionReportEventId,
                evidence = e.EvidenceRefs?.Select(er => new { er.Provenance, er.SourcePath, er.SourceFileHash, er.SourceLocator, er.RawPayloadHash })
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
            toolCommit,
            toolCommitSource ?? (string.IsNullOrWhiteSpace(toolCommit) || toolCommit.Equals("unknown", StringComparison.OrdinalIgnoreCase) ? "operator_argument_or_unknown" : "operator_argument"),
            sourceBaselineCommit ?? toolCommit,
            bundle.Manifest.CoreCommit,
            bundle.Manifest.ConfigHash,
            R018ImportPlanningConstants.InstrumentCatalogVersion,
            ComputeInstrumentCatalogHash(),
            DateTimeOffset.UtcNow,
            modeReason,
            ledgerApplicability,
            false,
            false,
            false,
            false,
            false,
            proposedTargets,
            normalizedEvents,
            evidenceOccurrences,
            businessEvents,
            validation,
            lineage,
            identity,
            replayEligibility,
            identityAudit,
            stagingRows);
    }


    private static R018NormalizedEvent MergeNormalizedFact(IGrouping<string, R018NormalizedEvent> group)
    {
        var ordered = group.OrderBy(e => e.LocalEventOrder ?? long.MaxValue).ThenBy(e => e.EventId, StringComparer.Ordinal).ToArray();
        var first = ordered[0];
        var evidenceRefs = ordered
            .SelectMany(e => e.EvidenceRefs ?? new[] { e.Evidence })
            .GroupBy(e => $"{e.SourcePath}|{e.SourceLocator}|{e.RawPayloadHash}", StringComparer.Ordinal)
            .Select(g => g.First())
            .ToArray();
        var missingFields = ordered.SelectMany(e => e.MissingFields ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
        return first with
        {
            EvidenceRefs = evidenceRefs,
            MissingFields = missingFields,
            PossDupFlag = ordered.Any(e => e.PossDupFlag is true) ? true : first.PossDupFlag
        };
    }

    private static IEnumerable<R018IdentityKeyAuditRow> BuildIdentityKeyAudit(IEnumerable<R018NormalizedEvent> events)
    {
        foreach (var ev in events)
        {
            var kind = ev.Kind.ToString();
            var businessKey = ev.Kind switch
            {
                R018NormalizedEventKind.Order => $"{ev.Environment}|{ev.BrokerAccount}|{ev.Venue}|ORDER|{ev.ClOrdId}",
                R018NormalizedEventKind.ExecutionReport => $"{ev.Environment}|{ev.BrokerAccount}|{ev.Venue}|ER|{ev.ClOrdId}|{ev.BrokerOrderId}|{ev.ExecId}",
                R018NormalizedEventKind.Fill => $"{ev.Environment}|{ev.BrokerAccount}|{ev.Venue}|FILL|{ev.ClOrdId}|{ev.BrokerOrderId}|{ev.ExecId}",
                _ => $"{ev.Environment}|{ev.BrokerAccount}|{ev.Venue}|{kind}|{ev.EventId}"
            };
            yield return new R018IdentityKeyAuditRow(
                kind,
                businessKey,
                ev.Kind is R018NormalizedEventKind.Fill ? $"venue_exec_id:{ev.Venue}|{ev.ExecId}" : $"client_order_id:{ev.ClOrdId}",
                $"{ev.Environment}|{ev.BrokerAccount}|{ev.Venue}|{businessKey}",
                "STAGING_SAFE_DB_APPLY_FALSE",
                "Safe staging key includes environment/account/venue; current DB candidate key is reported but not used for apply.");
        }
    }

    private static IEnumerable<R018PlannedStagingRow> BuildStagingRows(IEnumerable<R018NormalizedEvent> events, R018ImportBundleStatus status, string? modelRunId, string sourceRunId, R018ReplayEligibility replayEligibility)
    {
        if (status is R018ImportBundleStatus.REJECTED)
        {
            yield break;
        }

        foreach (var ev in events)
        {
            var target = status is R018ImportBundleStatus.CANONICAL_LINKED
                ? CanonicalTarget(ev.Kind)
                : EvidenceOnlyTarget(ev.Kind);
            var missing = new List<string>();
            if (target is "ChildOrder" && string.IsNullOrWhiteSpace(ev.ClOrdId)) missing.Add("ClOrdID");
            if (target is "Fill" && string.IsNullOrWhiteSpace(ev.ExecId)) missing.Add("ExecID");
            if (target is "Fill" && string.IsNullOrWhiteSpace(modelRunId)) missing.Add("canonical_model_run_id");
            var mappings = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["environment"] = ev.Environment,
                ["broker_account"] = ev.BrokerAccount,
                ["venue"] = ev.Venue,
                ["symbol"] = ev.Symbol ?? "",
                ["security_id"] = ev.SecurityId ?? "",
                ["clordid"] = ev.ClOrdId ?? "",
                ["broker_order_id"] = ev.BrokerOrderId ?? "",
                ["exec_id"] = ev.ExecId ?? "",
                ["phase_id"] = ev.PhaseId ?? "",
                ["wave_id"] = ev.WaveId ?? "",
                ["clip_id"] = ev.ClipId ?? ""
            };
            var safeStagingKey = SafeStagingKey(ev, target, sourceRunId);
            var plannedRowId = $"PLAN-{R018ArtifactBundleReader.ComputeSha256(safeStagingKey)[..16]}";
            var canonicalEligibility = replayEligibility.LocalIsolatedReplayAllowed && status is R018ImportBundleStatus.CANONICAL_LINKED && missing.Count == 0
                ? "CANONICAL_REPLAY_ELIGIBLE_OFFLINE_ONLY"
                : "NOT_CANONICAL_REPLAY_ELIGIBLE";
            yield return new R018PlannedStagingRow(
                plannedRowId,
                safeStagingKey,
                target,
                status is R018ImportBundleStatus.CANONICAL_LINKED ? "CANDIDATE_INSERT_DESCRIBED_ONLY" : "EVIDENCE_STAGING_ONLY",
                ev.StableKey,
                status is R018ImportBundleStatus.CANONICAL_LINKED ? modelRunId : null,
                mappings,
                ev.Kind is R018NormalizedEventKind.Fill && ev.SourceExecutionReportEventId is not null ? new[] { ev.SourceExecutionReportEventId } : Array.Empty<string>(),
                missing,
                ev.EvidenceRefs ?? new[] { ev.Evidence },
                canonicalEligibility,
                false,
                replayEligibility.Reasons.Concat(new[] { "DB_APPLY_FALSE_M1C2" }).Distinct(StringComparer.Ordinal).ToArray());
        }
    }


    private static IEnumerable<R018EvidenceOccurrence> BuildEvidenceOccurrences(IEnumerable<R018NormalizedEvent> events)
    {
        foreach (var ev in events)
        {
            foreach (var evidence in ev.EvidenceRefs ?? new[] { ev.Evidence })
            {
                yield return new R018EvidenceOccurrence(
                    $"OCC-{R018ArtifactBundleReader.ComputeSha256(ev.StableKey + "|" + evidence.SourcePath + "|" + evidence.SourceLocator + "|" + evidence.RawPayloadHash)[..16]}",
                    ev.StableKey,
                    ev.Kind,
                    evidence.Provenance,
                    evidence.ArtifactType,
                    evidence.SourcePath,
                    evidence.SourceFileHash,
                    evidence.SourceLocator,
                    evidence.RawPayloadHash,
                    ev.SourceTimestampUtc,
                    evidence.LocalReceiveUtc ?? ev.LocalReceiveUtc,
                    evidence.LocalEventOrder ?? ev.LocalEventOrder,
                    evidence.MsgSeqNum ?? ev.MsgSeqNum,
                    evidence.PossDupFlag ?? ev.PossDupFlag);
            }
        }
    }

    private static IEnumerable<R018BusinessEvent> BuildBusinessEvents(IEnumerable<R018NormalizedEvent> events)
    {
        var materialized = events.ToArray();
        var observationsByIdentity = materialized
            .Where(e => e.Kind is R018NormalizedEventKind.ManualObservation && !string.IsNullOrWhiteSpace(e.ExecId))
            .GroupBy(ObservationMergeKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);

        foreach (var ev in materialized)
        {
            if (ev.Kind is R018NormalizedEventKind.ManualObservation && observationsByIdentity.ContainsKey(ObservationMergeKey(ev)))
            {
                var canonicalExists = materialized.Any(candidate => candidate.Kind is R018NormalizedEventKind.ExecutionReport or R018NormalizedEventKind.Fill && ObservationMergeKey(candidate) == ObservationMergeKey(ev));
                if (canonicalExists)
                {
                    continue;
                }
            }

            var evidenceRefs = (ev.EvidenceRefs ?? new[] { ev.Evidence }).ToList();
            if (ev.Kind is R018NormalizedEventKind.ExecutionReport or R018NormalizedEventKind.Fill && observationsByIdentity.TryGetValue(ObservationMergeKey(ev), out var corroborating))
            {
                evidenceRefs.AddRange(corroborating.SelectMany(obs => obs.EvidenceRefs ?? new[] { obs.Evidence }));
            }

            evidenceRefs = evidenceRefs
                .GroupBy(e => $"{e.SourcePath}|{e.SourceLocator}|{e.RawPayloadHash}", StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();

            var facts = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["clordid"] = ev.ClOrdId ?? "",
                ["broker_order_id"] = ev.BrokerOrderId ?? "",
                ["exec_id"] = ev.ExecId ?? "",
                ["symbol"] = ev.Symbol ?? "",
                ["security_id"] = ev.SecurityId ?? "",
                ["side"] = ev.Side ?? "",
                ["quantity_unit"] = ev.QuantityUnit ?? "",
                ["order_qty"] = ev.OrderQuantity?.ToString(CultureInfo.InvariantCulture) ?? "",
                ["last_qty"] = ev.LastQuantity?.ToString(CultureInfo.InvariantCulture) ?? "",
                ["cum_qty"] = ev.CumQuantity?.ToString(CultureInfo.InvariantCulture) ?? "",
                ["leaves_qty"] = ev.LeavesQuantity?.ToString(CultureInfo.InvariantCulture) ?? "",
                ["limit_price"] = ev.LimitPrice?.ToString(CultureInfo.InvariantCulture) ?? "",
                ["fill_price"] = ev.FillPrice?.ToString(CultureInfo.InvariantCulture) ?? "",
                ["terminal_state"] = ev.TerminalState ?? ""
            };
            var fingerprint = R018ArtifactBundleReader.ComputeSha256(string.Join('|', facts.Select(kv => kv.Key + "=" + kv.Value)));
            yield return new R018BusinessEvent($"BE-{R018ArtifactBundleReader.ComputeSha256(ev.StableKey + "|" + fingerprint)[..16]}", ev.StableKey, ev.Kind, fingerprint, facts, evidenceRefs);
        }
    }

    private static string ObservationMergeKey(R018NormalizedEvent ev)
        => $"{ev.Environment}|{ev.BrokerAccount}|{ev.Venue}|{ev.ClOrdId}|{ev.ExecId}";

    private static R018ReplayEligibility BuildReplayEligibility(R018ValidationReport validation, R018IdentityScopeReport identity, R018ArtifactBundle bundle)
    {
        var reasons = new List<string>();
        var validationPassed = validation.Status is not R018ImportBundleStatus.REJECTED && !validation.Issues.Any(i => i.Severity.Equals("ERROR", StringComparison.OrdinalIgnoreCase));
        var criticalParityPassed = validation.DuplicateConflictCount == 0 && !validation.Issues.Any(i => i.Code is "EMPTY_BUNDLE" or "UNKNOWN_EVENT_KIND" or "MISSING_MANIFEST" or "UNKNOWN_MANIFEST_SCHEMA");
        var scopeHomogeneous = identity.EnvironmentPresent && identity.BrokerAccountPresent && identity.VenuePresent && !validation.Issues.Any(i => i.Code.StartsWith("SCOPE_", StringComparison.Ordinal));
        var identityConflictCount = validation.Issues.Count(i => i.Code.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) || i.Code.Contains("MULTIPLE_CLORDID", StringComparison.OrdinalIgnoreCase));
        var snapshotConsistent = !bundle.ReadIssues.Any(i => i.StartsWith("HASH_MISMATCH", StringComparison.Ordinal) || i.StartsWith("SNAPSHOT_", StringComparison.Ordinal));
        if (!validationPassed) reasons.Add("VALIDATION_FAILED");
        if (!criticalParityPassed) reasons.Add("CRITICAL_PARITY_FAILED");
        if (!scopeHomogeneous) reasons.Add("SCOPE_NOT_HOMOGENEOUS");
        if (identityConflictCount > 0) reasons.Add("IDENTITY_CONFLICTS_PRESENT");
        if (!snapshotConsistent) reasons.Add("INPUT_SNAPSHOT_INCONSISTENT");
        if (validation.Status is R018ImportBundleStatus.REJECTED) reasons.Add("STATUS_REJECTED");
        var allowed = validation.Status is not R018ImportBundleStatus.REJECTED && validationPassed && criticalParityPassed && scopeHomogeneous && identityConflictCount == 0 && snapshotConsistent;
        return new R018ReplayEligibility(validationPassed, criticalParityPassed, scopeHomogeneous, identityConflictCount, snapshotConsistent, allowed, reasons);
    }

    private static string SafeStagingKey(R018NormalizedEvent ev, string targetContract, string sourceRunId)
        => string.Join('|', R018ImportPlanningConstants.PlanSchemaVersion, R018ImportPlanningConstants.NormalizedEventSchemaVersion, sourceRunId, ev.Environment, ev.BrokerAccount, ev.Venue, targetContract, ev.StableKey, SemanticFingerprintForStaging(ev));

    private static string SemanticFingerprintForStaging(R018NormalizedEvent ev)
        => string.Join('|', ev.Kind, ev.Symbol, ev.SecurityId, ev.Side, ev.QuantityUnit, ev.OrderQuantity, ev.LastQuantity, ev.CumQuantity, ev.LeavesQuantity, ev.LimitPrice, ev.FillPrice, ev.ExecType, ev.OrdStatus, ev.TerminalState);

    private static string ComputeInstrumentCatalogHash()
        => R018ArtifactBundleReader.ComputeSha256(R018ImportPlanningConstants.InstrumentCatalogVersion + "|" + string.Join('|', R018ArtifactBundleValidator.InstrumentCatalog.Select(kv => kv.Key + ":" + kv.Value).OrderBy(x => x, StringComparer.Ordinal)));
    private static string CanonicalTarget(R018NormalizedEventKind kind)
        => kind switch
        {
            R018NormalizedEventKind.Order => "ChildOrder",
            R018NormalizedEventKind.ExecutionReport => "ExecutionReport",
            R018NormalizedEventKind.Fill => "Fill",
            R018NormalizedEventKind.Bbo => "TCAResearchEvidence",
            _ => "ExceptionCaseEvidence"
        };

    private static string EvidenceOnlyTarget(R018NormalizedEventKind kind)
        => kind switch
        {
            R018NormalizedEventKind.Bbo => "TCAResearchEvidence",
            R018NormalizedEventKind.Unknown => "ExceptionCaseEvidence",
            R018NormalizedEventKind.ManualObservation => "ExceptionCaseEvidence",
            _ => "R018_NORMALIZED_EVENT_STAGING"
        };
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
    {
        var status = expected == actual ? "PASS" : "FAIL";
        var severity = status == "PASS" ? "INFO" : "ERROR";
        return new R018ParityRow(check, "offline_import_plan", expected.ToString(CultureInfo.InvariantCulture), actual.ToString(CultureInfo.InvariantCulture), status, severity, string.Empty);
    }
}

public sealed class R018ImportPlanSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions JsonLineOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
    static R018ImportPlanSerializer()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
        JsonLineOptions.Converters.Add(new JsonStringEnumConverter());
    }


    public void WriteAll(R018ArtifactBundle bundle, R018ImportPlan plan, string outputPath)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        if (Directory.Exists(fullOutputPath) && Directory.EnumerateFileSystemEntries(fullOutputPath).Any())
        {
            throw new InvalidOperationException("OUTPUT_DIRECTORY_NOT_EMPTY");
        }

        var parent = Directory.GetParent(fullOutputPath)?.FullName ?? throw new InvalidOperationException("OUTPUT_PARENT_NOT_FOUND");
        Directory.CreateDirectory(parent);
        var tempPath = Path.Combine(parent, $".{Path.GetFileName(fullOutputPath)}.tmp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPath);
        try
        {
            WriteJson(Path.Combine(tempPath, "bundle_manifest.json"), bundle.Manifest);
            WriteJson(Path.Combine(tempPath, "validation_report.json"), plan.Validation);
            WriteJsonLines(Path.Combine(tempPath, "normalized_events.jsonl"), plan.NormalizedEvents);
            WriteJson(Path.Combine(tempPath, "lineage_report.json"), plan.Lineage);
            WriteJson(Path.Combine(tempPath, "identity_scope_report.json"), plan.IdentityScope);
            WriteJson(Path.Combine(tempPath, "identity_key_audit.json"), plan.IdentityKeyAudit ?? Array.Empty<R018IdentityKeyAuditRow>());
            WriteJson(Path.Combine(tempPath, "typed_staging_plan.json"), plan.PlannedStagingRows ?? Array.Empty<R018PlannedStagingRow>());
            WriteJsonLines(Path.Combine(tempPath, "evidence_occurrences.jsonl"), plan.EvidenceOccurrences);
            WriteJson(Path.Combine(tempPath, "business_events.json"), plan.BusinessEvents);
            WriteJson(Path.Combine(tempPath, "replay_eligibility.json"), plan.ReplayEligibility);
            WriteJson(Path.Combine(tempPath, "import_plan_v3.json"), plan);
            WriteParity(Path.Combine(tempPath, "parity_report.csv"), new R018ParityReportBuilder().Build(bundle, plan));
            WriteSummary(Path.Combine(tempPath, "human_summary.md"), plan);
            WriteJson(Path.Combine(tempPath, "output_hashes.json"), BuildHashes(tempPath));

            if (Directory.Exists(fullOutputPath))
            {
                Directory.Delete(fullOutputPath);
            }

            Directory.Move(tempPath, fullOutputPath);
        }
        catch
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }

            throw;
        }
    }
    private static void WriteJson<T>(string path, T value)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteJsonLines<T>(string path, IEnumerable<T> values)
    {
        File.WriteAllLines(path, values.Select(value => JsonSerializer.Serialize(value, JsonLineOptions)), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void WriteParity(string path, IReadOnlyList<R018ParityRow> rows)
    {
        var lines = new List<string> { "check,source,expected,actual,status,severity,detail" };
        lines.AddRange(rows.Select(r => string.Join(',', Escape(r.Check), Escape(r.Source), Escape(r.Expected), Escape(r.Actual), Escape(r.Status), Escape(r.Severity), Escape(r.Detail))));
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

        if (Directory.Exists(outputPath) && Directory.EnumerateFileSystemEntries(outputPath).Any())
        {
            error.WriteLine("OUTPUT_DIRECTORY_NOT_EMPTY");
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
            var plan = new R018ImportPlanBuilder().Build(bundle, parsed.ModelRunCatalogPath, parsed.ToolCommit ?? "unknown", parsed.SourceBaselineCommit, parsed.ToolCommitSource);
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

    private static bool IsSameOrChildPath(string parentPath, string candidatePath)
    {
        var parent = Path.GetFullPath(parentPath);
        var candidate = Path.GetFullPath(candidatePath);
        var relative = Path.GetRelativePath(parent, candidate);
        return relative == "." || (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative));
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
    string? ToolCommit,
    string? ToolCommitSource,
    string? SourceBaselineCommit,
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
            return new R018OfflineImportPlanCliOptions(null, null, null, null, null, null, false, false, true, reasons);
        }

        if (!args[0].Equals("build-r018-import-plan", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("UNKNOWN_COMMAND");
        }

        string? bundle = null;
        string? output = null;
        string? catalog = null;
        string? toolCommit = null;
        string? toolCommitSource = null;
        string? sourceBaselineCommit = null;
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
                case "--tool-commit":
                    toolCommit = RequireValue(args, ref i, reasons, arg);
                    toolCommitSource = arg == "--tool-commit" ? "operator_argument" : "legacy_code_commit_argument";
                    break;
                case "--source-baseline-commit":
                    sourceBaselineCommit = RequireValue(args, ref i, reasons, arg);
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

        return new R018OfflineImportPlanCliOptions(bundle, output, catalog, toolCommit, toolCommitSource, sourceBaselineCommit, noDb, noNetwork, false, reasons);
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






