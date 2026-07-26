using System.Security.Cryptography;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class InstitutionalCurrentnessStatuses
{
    public const string CurrentAtLatestRequiredSlot = "CURRENT_AT_LATEST_REQUIRED_SLOT";
    public const string OutsideCalendarCurrent =
        "OUTSIDE_OPERATIONAL_CALENDAR_CURRENT_AT_LAST_REQUIRED_SLOT";
    public const string LastRequiredSlotMissed = "OBSOL\u00c8TE_LAST_REQUIRED_SLOT_MISSED";
    public const string LatestRequiredRevisionAbsent =
        "OBSOL\u00c8TE_LATEST_REQUIRED_REVISION_ABSENT";
    public const string DueNotYetLate = "DUE_NOT_YET_LATE";
    public const string StaleAfterDueTime = "STALE_AFTER_DUE_TIME";
    public const string Unknown = "UNKNOWN";
}

public sealed record InstitutionalMetricCurrentness(
    string MarketCalendarStatus,
    string SlotDueStatus,
    string? LatestExpectedClosedSlotId,
    DateTimeOffset? LatestExpectedClosedSlotEndUtc,
    string? LatestPersistedSlotId,
    string? LatestPersistedSlotStatus,
    string? LatestQualifyingRevisionSlotId,
    string MetricCurrentnessStatus,
    string CurrentnessReason,
    string ContractVersion);

public static class InstitutionalMetricCurrentnessPolicy
{
    public const string ContractVersion = "institutional_metric_currentness_calendar_v1";

    public static InstitutionalMetricCurrentness Evaluate(
        OperationalReportingSnapshot snapshot,
        IReadOnlyList<PmsShadowIntradayEconomicProjection> revisions)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(revisions);
        var expectation = ReportingOperationalCalendar.Project(snapshot.AsOfUtc, snapshot.Slots);
        var latestPersisted = snapshot.Slots
            .Where(value => value.SlotEndUtc <= snapshot.AsOfUtc)
            .OrderBy(value => value.SlotEndUtc)
            .ThenBy(value => value.SlotId, StringComparer.Ordinal)
            .LastOrDefault();
        var latestRevision = revisions.LastOrDefault();
        var outside = expectation.MarketCalendarStatus ==
                      ReportingSlotDueStatuses.OutsideOperationalCalendar;
        var requiredId = outside
            ? latestPersisted?.SlotId
            : expectation.LatestExpectedClosedSlotId;
        var requiredEnd = outside
            ? latestPersisted?.SlotEndUtc
            : expectation.LatestExpectedClosedSlotEndUtc;
        var requiredSlot = requiredId is null
            ? null
            : snapshot.Slots.SingleOrDefault(value =>
                string.Equals(value.SlotId, requiredId, StringComparison.Ordinal));

        string status;
        string reason;
        if (requiredSlot?.Status == "MISSED" ||
            expectation.SlotDueStatus == ReportingSlotDueStatuses.Missed)
        {
            status = InstitutionalCurrentnessStatuses.LastRequiredSlotMissed;
            reason = "The latest required operational slot is absent or explicitly MISSED.";
        }
        else if (expectation.SlotDueStatus == ReportingSlotDueStatuses.Due)
        {
            status = InstitutionalCurrentnessStatuses.DueNotYetLate;
            reason = "The latest required slot remains inside its contractual grace period.";
        }
        else if (expectation.SlotDueStatus == ReportingSlotDueStatuses.StaleAfterDueTime)
        {
            status = InstitutionalCurrentnessStatuses.StaleAfterDueTime;
            reason = "The latest required slot did not complete inside its grace period.";
        }
        else if (requiredSlot?.Status == "COMPLETED" &&
                 !string.Equals(latestRevision?.SlotId, requiredSlot.SlotId,
                     StringComparison.Ordinal))
        {
            status = InstitutionalCurrentnessStatuses.LatestRequiredRevisionAbsent;
            reason = "The latest required completed slot has no authoritative economic revision.";
        }
        else if (requiredSlot?.Status == "COMPLETED" &&
                 string.Equals(latestRevision?.SlotId, requiredSlot.SlotId,
                     StringComparison.Ordinal))
        {
            status = outside
                ? InstitutionalCurrentnessStatuses.OutsideCalendarCurrent
                : InstitutionalCurrentnessStatuses.CurrentAtLatestRequiredSlot;
            reason = outside
                ? "The market is outside the operational calendar and the last required slot is complete."
                : "The latest required slot and its authoritative revision are complete.";
        }
        else
        {
            status = InstitutionalCurrentnessStatuses.Unknown;
            reason = "Currentness cannot be proven from the persisted operational slots.";
        }

        return new(expectation.MarketCalendarStatus, expectation.SlotDueStatus,
            requiredId, requiredEnd, latestPersisted?.SlotId, latestPersisted?.Status,
            latestRevision?.SlotId, status, reason, ContractVersion);
    }
}

public sealed record InstitutionalPositionAuthorityDecision(
    string AuthorityStatus,
    string AvailabilityStatus,
    string Reason,
    string ContractVersion);

public sealed record InstitutionalPositionSnapshotCoverageDecision(
    Guid EconomicRevisionId,
    Guid PositionSnapshotId,
    string ContractVersion,
    string CoverageMode,
    int RequiredInstrumentCount,
    int CoveredInstrumentCount,
    int MissingCount,
    int DuplicateCount,
    int ExtraCount,
    int MismatchCount,
    IReadOnlyList<Guid> MissingInstrumentIds,
    IReadOnlyList<Guid> DuplicateInstrumentIds,
    IReadOnlyList<Guid> MismatchInstrumentIds,
    string PositionSnapshotLineSetSha256,
    string CurrentPositionAuthorityDecision,
    string AvailabilityStatus,
    string Reason,
    string EvidenceSha256);

public static class InstitutionalPositionSnapshotCoveragePolicy
{
    public const string ContractVersion = "institutional_position_snapshot_coverage_v1";
    public const string FullUniverseExplicitZero = "FULL_UNIVERSE_EXPLICIT_ZERO";
    public const string SparseNonzero = "SPARSE_NONZERO";
    public const string Unknown = "UNKNOWN";

    public static InstitutionalPositionSnapshotCoverageDecision Evaluate(
        PmsShadowIntradayEconomicProjection revision,
        IEnumerable<ReportingPositionSnapshotLineFact> source)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(source);
        var required = revision.TargetPositions.Select(value => value.InstrumentId)
            .Concat(revision.PositionOnlyDrifts.Select(value => value.InstrumentId))
            .Distinct().Order().ToArray();
        var lines = source.Where(value =>
                value.PositionSnapshotId == revision.PositionSnapshotId)
            .OrderBy(value => value.InstrumentId)
            .ThenBy(value => value.RowIdentity, StringComparer.Ordinal)
            .ToArray();
        var byInstrument = lines.GroupBy(value => value.InstrumentId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var missing = required.Where(value => !byInstrument.ContainsKey(value)).ToArray();
        var duplicates = byInstrument.Where(value => value.Value.Length != 1)
            .Select(value => value.Key).Order().ToArray();
        var requiredSet = required.ToHashSet();
        var extras = byInstrument.Keys.Count(value => !requiredSet.Contains(value));
        var mismatch = revision.PositionOnlyDrifts
            .GroupBy(value => value.InstrumentId)
            .Where(group => byInstrument.TryGetValue(group.Key, out var matches) &&
                            matches.Length == 1 &&
                            group.Any(value =>
                                value.CurrentBaseQuantity != matches[0].CurrentBaseQuantity))
            .Select(group => group.Key).Order().ToArray();
        var metadataComplete = lines.All(value =>
            value.SourceIngestionId == revision.SourceIngestionId &&
            value.SourceAsOfUtc.Offset == TimeSpan.Zero &&
            value.SourceAsOfUtc == revision.PositionSnapshotAsOfUtc &&
            value.SourceAsOfUtc <= revision.SlotEndUtc);
        var lineSetSha = Arch5bHashing.HashCanonical(lines.Select(value => new
        {
            value.PositionSnapshotId,
            value.InstrumentId,
            value.SecurityId,
            value.Symbol,
            value.CurrentBaseQuantity,
            value.SourceIngestionId,
            value.RowIdentity,
            value.SourceAsOfUtc,
            value.EvidenceSha256
        }).ToArray());
        var covered = required.Length - missing.Length;
        var complete = lines.Length > 0 &&
                       missing.Length == 0 &&
                       duplicates.Length == 0 &&
                       mismatch.Length == 0 &&
                       metadataComplete;
        var coverageMode = complete
            ? FullUniverseExplicitZero
            : lines.Length > 0 && missing.Length > 0 &&
              lines.All(value => value.CurrentBaseQuantity != 0m)
                ? SparseNonzero
                : Unknown;
        var authority = lines.Length == 0
            ? ReportingAuthority.Absent
            : complete &&
              string.Equals(revision.PositionAuthority,
                  InstitutionalPositionAuthorityPolicy.CanonicalAuthorityCode,
                  StringComparison.Ordinal) &&
              revision.AccountSnapshotId != Guid.Empty &&
              revision.PositionSnapshotId != Guid.Empty
                ? ReportingAuthority.Proven
                : ReportingAuthority.Unknown;
        var availability = authority == ReportingAuthority.Proven
            ? MetricAvailabilityStatus.SourceProven
            : MetricAvailabilityStatus.BlockedAuthorityUnproven;
        var reason = authority switch
        {
            ReportingAuthority.Proven =>
                "Every required instrument has one explicit, matching position line.",
            ReportingAuthority.Absent =>
                "No position snapshot line exists for the authoritative revision.",
            _ when coverageMode == SparseNonzero =>
                "The persisted position snapshot is sparse; missing instruments are not zero.",
            _ => "Position snapshot coverage or lineage is not proven."
        };
        var evidenceSha = Arch5bHashing.HashCanonical(new
        {
            ContractVersion,
            revision.ProjectionRevisionId,
            revision.PositionSnapshotId,
            CoverageMode = coverageMode,
            RequiredInstrumentCount = required.Length,
            CoveredInstrumentCount = covered,
            MissingInstrumentIds = missing,
            DuplicateInstrumentIds = duplicates,
            ExtraCount = extras,
            MismatchInstrumentIds = mismatch,
            PositionSnapshotLineSetSha256 = lineSetSha,
            CurrentPositionAuthorityDecision = authority,
            AvailabilityStatus = availability,
            MetadataComplete = metadataComplete
        });
        return new(
            revision.ProjectionRevisionId,
            revision.PositionSnapshotId,
            ContractVersion,
            coverageMode,
            required.Length,
            covered,
            missing.Length,
            duplicates.Length,
            extras,
            mismatch.Length,
            missing,
            duplicates,
            mismatch,
            lineSetSha,
            authority,
            availability,
            reason,
            evidenceSha);
    }
}

public static class InstitutionalPositionAuthorityPolicy
{
    public const string ContractVersion = "institutional_position_authority_v2";
    public const string CanonicalAuthorityCode = "BROKER_PORTAL_EOD";

    public static InstitutionalPositionAuthorityDecision Evaluate(
        PmsShadowIntradayEconomicProjection revision,
        InstitutionalPositionSnapshotCoverageDecision coverage)
    {
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(coverage);
        if (coverage.EconomicRevisionId != revision.ProjectionRevisionId ||
            coverage.PositionSnapshotId != revision.PositionSnapshotId)
            return Decision(ReportingAuthority.Unknown,
                "Position coverage evidence is bound to a different revision.");
        return new(
            coverage.CurrentPositionAuthorityDecision,
            coverage.AvailabilityStatus,
            coverage.Reason,
            ContractVersion);
    }

    private static InstitutionalPositionAuthorityDecision Decision(
        string authority, string reason) =>
        new(authority, MetricAvailabilityStatus.BlockedAuthorityUnproven,
            reason, ContractVersion);
}

public static class InstitutionalExecutionAuthorityPolicy
{
    public const string ContractVersion = "institutional_execution_authority_v1";

    public static string FillAuthority(int rowCount) =>
        rowCount == 0 ? ReportingAuthority.Absent : ReportingAuthority.Unknown;

    public static string LedgerAuthority(int rowCount, string _) =>
        rowCount == 0 ? ReportingAuthority.Absent : ReportingAuthority.Unknown;
}

public sealed record InstitutionalRoadmapAuthorityResult(
    string RepositoryRoot,
    string RoadmapPath,
    string Sha256,
    string ContractVersion);

public static class InstitutionalRoadmapAuthority
{
    public const string ContractVersion = "institutional_roadmap_path_authority_v1";
    public const string RelativePath =
        "docs/architecture/reporting/hedge-fund-institutional-reporting-roadmap-v1.md";

    public static InstitutionalRoadmapAuthorityResult Resolve(
        string repositoryRoot,
        string? requestedRoadmapPath = null)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new InvalidDataException("RPT2_REPOSITORY_ROOT_REQUIRED");
        var root = Path.GetFullPath(repositoryRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var canonical = Path.GetFullPath(Path.Combine(root,
            RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!canonical.StartsWith(root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("RPT2_ROADMAP_AUTHORITY_PATH_MISMATCH");
        if (requestedRoadmapPath is not null &&
            !string.Equals(Path.GetFullPath(requestedRoadmapPath), canonical,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("RPT2_ROADMAP_AUTHORITY_PATH_MISMATCH");
        RequireNoReparsePoint(root, canonical);
        if (!File.Exists(canonical))
            throw new InvalidDataException("RPT2_ROADMAP_MANIFEST_MISSING");
        var text = File.ReadAllText(canonical);
        Require(text.Contains(
            "ManifestId | `hedge_fund_institutional_reporting_roadmap`",
            StringComparison.Ordinal), "RPT2_ROADMAP_MANIFEST_ID_MISMATCH");
        Require(text.Contains("ManifestVersion | `v1`", StringComparison.Ordinal),
            "RPT2_ROADMAP_MANIFEST_VERSION_MISMATCH");
        Require(text.Contains("Status | `AUTHORITATIVE_REPORTING_ROADMAP`",
            StringComparison.Ordinal), "RPT2_ROADMAP_STATUS_MISMATCH");
        Require(text.Contains("CurrentMasterAtCreation | `", StringComparison.Ordinal),
            "RPT2_ROADMAP_MASTER_IDENTITY_MISSING");
        foreach (var value in new[]
                 {
                     "reporting_source", "reporting_mart", "reporting_control",
                     "reporting_publication", "RPT1", "RPT2", "RPT3", "RPT4"
                 })
            Require(text.Contains(value, StringComparison.Ordinal),
                "RPT2_ROADMAP_CONTENT_INCOMPLETE");
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(canonical)));
        return new(root, canonical, sha, ContractVersion);
    }

    private static void RequireNoReparsePoint(string root, string canonical)
    {
        for (var current = new DirectoryInfo(Path.GetDirectoryName(canonical)!);
             current is not null &&
             current.FullName.StartsWith(root, StringComparison.OrdinalIgnoreCase);
             current = current.Parent)
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("RPT2_ROADMAP_AUTHORITY_REPARSE_POINT");
        if (File.Exists(canonical) &&
            (File.GetAttributes(canonical) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("RPT2_ROADMAP_AUTHORITY_REPARSE_POINT");
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
