using System.Data.Common;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class PmsShadowDailyIngestionContract
{
    public const string Version = "pms_shadow_daily_ingestion_request_v1";

    public static string CreateIdempotencyKey(string sourceSessionId, string evidenceZipSha256,
        string contractVersion = Version)
    {
        var canonical = $"{contractVersion}\n{sourceSessionId}\n{evidenceZipSha256}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

public enum PmsShadowDailyIngestionStatus
{
    Discovered,
    Validating,
    ReadyToImport,
    Importing,
    Completed,
    AlreadyAppliedIdentical,
    FailedClosed,
    BlockedIncompleteSource
}

public sealed record PmsShadowDailyIngestionRequest(
    string ContractVersion,
    string SourceGate,
    string SourceDecision,
    string SourceSessionId,
    DateOnly OperationalDate,
    string EvidenceManifestSha256,
    string EvidenceZipSha256,
    string RowsetManifestSha256,
    string CoreMasterCommitId,
    string CoreMasterObjectFormat,
    string IntradayMasterCommitId,
    string IntradayMasterObjectFormat,
    IReadOnlyList<Guid> QubesInputSnapshotIds,
    IReadOnlyList<Guid> ModelRunIds,
    IReadOnlyDictionary<string, int> ExpectedRowCounts,
    bool CalculationSessionCompleted,
    bool FourRequiredRunsFinalized,
    bool OutputsTransferred,
    bool DownstreamShadowFinalized,
    bool EvidenceManifestFinalized,
    bool NoOrderManifestValid,
    string Environment,
    string Classification,
    bool NoOrder,
    DateTimeOffset CreatedAtUtc,
    string IdempotencyKey);

public sealed record PmsShadowDailyHandoffValidation(bool IsValid,
    PmsShadowDailyIngestionStatus Status, IReadOnlyList<string> Issues);

public sealed record PmsShadowIngestionTransition(PmsShadowDailyIngestionStatus From,
    PmsShadowDailyIngestionStatus To, string Reason);

public sealed record PmsShadowOperationalAlert(string Code, string Severity, string SourceSessionId,
    DateOnly OperationalDate, DateTimeOffset CreatedAtUtc, string EvidenceReference, string ActionableReason);

public sealed record PmsShadowDailyIngestionOutcome(PmsShadowDailyIngestionStatus Status,
    string IdempotencyKey, PmsShadowImportOutcome? Import,
    IReadOnlyList<PmsShadowIngestionTransition> Transitions,
    IReadOnlyList<PmsShadowOperationalAlert> Alerts);

public sealed record PmsShadowDailyEvidencePackage(Arch6dEvidencePackage Arch6dPackage,
    string EvidenceManifestSha256);

public static class PmsShadowDailyEvidencePackageReader
{
    public static PmsShadowDailyEvidencePackage Read(string arch6cEvidenceZip,
        string expectedArch6cSha256, string arch6bEvidenceZip, string expectedArch6bSha256)
    {
        var package = Arch6dPmsShadowEvidencePackageReader.Read(arch6cEvidenceZip,
            expectedArch6cSha256, arch6bEvidenceZip, expectedArch6bSha256);
        using var archive = ZipFile.OpenRead(arch6bEvidenceZip);
        var entry = archive.GetEntry("evidence_manifest.json")
            ?? throw new InvalidDataException("ARCH6B_ENTRY_MISSING:evidence_manifest.json");
        using var stream = entry.Open();
        return new(package, Convert.ToHexStringLower(SHA256.HashData(stream)));
    }
}

public static class PmsShadowDailyHandoffSerializer
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static PmsShadowDailyIngestionRequest Read(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<PmsShadowDailyIngestionRequest>(stream, Json)
            ?? throw new InvalidDataException("DAILY_HANDOFF_INVALID_JSON");
    }

    public static string Serialize(PmsShadowDailyIngestionRequest request) =>
        JsonSerializer.Serialize(request, Json);
}

public static class PmsShadowDailyHandoffValidator
{
    public static PmsShadowDailyHandoffValidation Validate(PmsShadowDailyIngestionRequest request,
        PmsShadowDailyEvidencePackage package)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(package);
        var issues = new List<string>();
        var plan = package.Arch6dPackage.Plan;

        Require(request.ContractVersion == PmsShadowDailyIngestionContract.Version,
            "DAILY_HANDOFF_CONTRACT_VERSION_MISMATCH", issues);
        Require(request.SourceGate.Contains("ARCH6B", StringComparison.Ordinal),
            "SOURCE_GATE_NOT_ARCH6B", issues);
        Require(request.SourceDecision.StartsWith("GO_ARCH6B_", StringComparison.Ordinal) &&
            request.SourceDecision.EndsWith("_NO_ORDER", StringComparison.Ordinal), "SOURCE_SESSION_NO_GO", issues);
        Require(request.SourceSessionId == plan.Ingestion.SourceSessionId, "SOURCE_SESSION_ID_MISMATCH", issues);
        Require(request.OperationalDate == plan.AccountSnapshot.ReportDate, "OPERATIONAL_DATE_MISMATCH", issues);
        Require(IsSha256(request.EvidenceManifestSha256), "EVIDENCE_MANIFEST_SHA_INVALID", issues);
        Require(request.EvidenceManifestSha256 == package.EvidenceManifestSha256,
            "EVIDENCE_HASH_MISMATCH", issues);
        Require(request.EvidenceZipSha256 == package.Arch6dPackage.Verification.Arch6bEvidenceSha256,
            "EVIDENCE_ZIP_SHA_MISMATCH", issues);
        Require(request.RowsetManifestSha256 == plan.RowsetSha256, "ROWSET_MANIFEST_SHA_MISMATCH", issues);

        Require(GitCommitIdentityContract.IsValid(request.CoreMasterCommitId, request.CoreMasterObjectFormat),
            "CORE_MASTER_COMMIT_IDENTITY_INVALID", issues);
        Require(plan.ModelRuns.All(run => run.CoreMasterCommitId == request.CoreMasterCommitId &&
            run.CoreMasterObjectFormat == request.CoreMasterObjectFormat), "CORE_MASTER_COMMIT_IDENTITY_MISMATCH", issues);
        Require(GitCommitIdentityContract.IsValid(request.IntradayMasterCommitId, request.IntradayMasterObjectFormat),
            "INTRADAY_MASTER_COMMIT_IDENTITY_INVALID", issues);
        Require(SetEquals(request.QubesInputSnapshotIds, plan.QubesInputSnapshots.Select(value => value.SnapshotId)),
            "QUBES_INPUT_SNAPSHOT_SET_MISMATCH", issues);
        Require(SetEquals(request.ModelRunIds, plan.ModelRuns.Select(value => value.ModelRunId)),
            "MODEL_RUN_SET_MISMATCH", issues);
        Require(request.ModelRunIds.Count == 4 && request.ModelRunIds.Distinct().Count() == 4,
            "FOUR_REQUIRED_MODEL_RUNS_MISSING", issues);
        Require(DictionaryEquals(request.ExpectedRowCounts, EfPmsShadowSessionImportStore.ExpectedRowCounts(plan)),
            "ROW_COUNT_MISMATCH", issues);

        Require(request.CalculationSessionCompleted, "CALCULATION_SESSION_NOT_COMPLETED", issues);
        Require(request.FourRequiredRunsFinalized, "REQUIRED_RUNS_NOT_FINALIZED", issues);
        Require(request.OutputsTransferred, "OUTPUTS_NOT_TRANSFERRED", issues);
        Require(request.DownstreamShadowFinalized, "DOWNSTREAM_SHADOW_NOT_FINALIZED", issues);
        Require(request.EvidenceManifestFinalized, "EVIDENCE_MANIFEST_NOT_FINALIZED", issues);
        Require(request.NoOrderManifestValid, "NO_ORDER_MANIFEST_INVALID", issues);
        Require(request.Environment == "TEST", "PMS_SHADOW_IMPORT_REQUIRES_TEST_ENVIRONMENT", issues);
        Require(request.Classification == PmsShadowStateContract.EvidenceClassification,
            "PMS_SHADOW_CLASSIFICATION_MISMATCH", issues);
        Require(request.NoOrder, "PMS_SHADOW_IMPORT_REQUIRES_NO_ORDER", issues);
        Require(request.CreatedAtUtc.Offset == TimeSpan.Zero, "CREATED_AT_UTC_REQUIRED", issues);
        Require(request.IdempotencyKey == PmsShadowDailyIngestionContract.CreateIdempotencyKey(
            request.SourceSessionId, request.EvidenceZipSha256, request.ContractVersion),
            "IDEMPOTENCY_KEY_MISMATCH", issues);

        var planValidation = Arch6cPmsShadowPersistencePlanner.Validate(plan);
        issues.AddRange(planValidation.Issues.Select(issue => $"PLAN:{issue}"));
        var incomplete = issues.Any(IsIncompleteSourceIssue);
        return new(issues.Count == 0,
            issues.Count == 0 ? PmsShadowDailyIngestionStatus.ReadyToImport :
            incomplete ? PmsShadowDailyIngestionStatus.BlockedIncompleteSource : PmsShadowDailyIngestionStatus.FailedClosed,
            issues.Order(StringComparer.Ordinal).ToArray());
    }

    private static bool IsIncompleteSourceIssue(string issue) => issue is
        "SOURCE_SESSION_NO_GO" or "CALCULATION_SESSION_NOT_COMPLETED" or "REQUIRED_RUNS_NOT_FINALIZED" or
        "OUTPUTS_NOT_TRANSFERRED" or "DOWNSTREAM_SHADOW_NOT_FINALIZED" or "EVIDENCE_MANIFEST_NOT_FINALIZED" or
        "NO_ORDER_MANIFEST_INVALID" or "FOUR_REQUIRED_MODEL_RUNS_MISSING";

    private static bool SetEquals(IEnumerable<Guid> actual, IEnumerable<Guid> expected) =>
        actual.Order().SequenceEqual(expected.Order());

    private static bool DictionaryEquals(IReadOnlyDictionary<string, int> actual,
        IReadOnlyDictionary<string, int> expected) => actual.Count == expected.Count &&
        expected.All(pair => actual.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static bool IsSha256(string? value) => value is not null && value.Length == 64 &&
        value.All(character => char.IsAsciiHexDigit(character) && !char.IsUpper(character));

    private static void Require(bool condition, string issue, ICollection<string> issues)
    {
        if (!condition) issues.Add(issue);
    }
}

public sealed class PmsShadowDailyIngestionCoordinator(Arch6bPmsShadowSessionImporter importer)
{
    public async Task<PmsShadowDailyIngestionOutcome> CoordinateAsync(PmsShadowDailyIngestionRequest request,
        PmsShadowDailyEvidencePackage package, CancellationToken cancellationToken = default)
    {
        var transitions = new List<PmsShadowIngestionTransition>
        {
            new(PmsShadowDailyIngestionStatus.Discovered, PmsShadowDailyIngestionStatus.Validating,
                "Finalized daily handoff discovered.")
        };
        var validation = PmsShadowDailyHandoffValidator.Validate(request, package);
        if (!validation.IsValid)
        {
            transitions.Add(new(PmsShadowDailyIngestionStatus.Validating, validation.Status,
                string.Join(';', validation.Issues)));
            return new(validation.Status, request.IdempotencyKey, null, transitions,
                AlertsFor(validation.Issues, request));
        }

        transitions.Add(new(PmsShadowDailyIngestionStatus.Validating,
            PmsShadowDailyIngestionStatus.ReadyToImport, "Handoff and evidence hashes validated."));
        transitions.Add(new(PmsShadowDailyIngestionStatus.ReadyToImport,
            PmsShadowDailyIngestionStatus.Importing, "Delegated to the ARCH6D atomic importer."));
        try
        {
            var outcome = await importer.ImportAsync(package.Arch6dPackage.Plan,
                new("TEST", true, PmsShadowStateContract.ContractVersion), cancellationToken);
            var status = outcome.Result == PmsShadowApplyResult.Applied
                ? PmsShadowDailyIngestionStatus.Completed
                : PmsShadowDailyIngestionStatus.AlreadyAppliedIdentical;
            transitions.Add(new(PmsShadowDailyIngestionStatus.Importing, status, outcome.Result.ToString()));
            return new(status, request.IdempotencyKey, outcome, transitions, []);
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or DbException)
        {
            transitions.Add(new(PmsShadowDailyIngestionStatus.Importing,
                PmsShadowDailyIngestionStatus.FailedClosed, exception.Message));
            return new(PmsShadowDailyIngestionStatus.FailedClosed, request.IdempotencyKey, null, transitions,
                [Alert("INGESTION_FAILED_CLOSED", "ERROR", request, exception.Message)]);
        }
    }

    private static IReadOnlyList<PmsShadowOperationalAlert> AlertsFor(IReadOnlyList<string> issues,
        PmsShadowDailyIngestionRequest request) => issues.Select(issue => issue switch
    {
        "EVIDENCE_HASH_MISMATCH" or "EVIDENCE_ZIP_SHA_MISMATCH" =>
            Alert("EVIDENCE_HASH_MISMATCH", "ERROR", request, issue),
        "ROW_COUNT_MISMATCH" => Alert("ROW_COUNT_MISMATCH", "ERROR", request, issue),
        "FOUR_REQUIRED_MODEL_RUNS_MISSING" or "QUBES_INPUT_SNAPSHOT_SET_MISMATCH" or "MODEL_RUN_SET_MISMATCH" =>
            Alert("LINEAGE_INCOMPLETE", "ERROR", request, issue),
        "PMS_SHADOW_IMPORT_REQUIRES_NO_ORDER" or "NO_ORDER_MANIFEST_INVALID" =>
            Alert("NO_ORDER_INVARIANT_VIOLATION", "CRITICAL", request, issue),
        _ => Alert("INGESTION_FAILED_CLOSED", "ERROR", request, issue)
    }).ToArray();

    private static PmsShadowOperationalAlert Alert(string code, string severity,
        PmsShadowDailyIngestionRequest request, string reason) => new(code, severity,
            request.SourceSessionId, request.OperationalDate, request.CreatedAtUtc,
            request.EvidenceZipSha256, reason);
}
