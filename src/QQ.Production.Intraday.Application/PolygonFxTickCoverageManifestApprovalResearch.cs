using System.Text.Json;
using System.Text.Json.Serialization;

namespace QQ.Production.Intraday.Application;

public enum PolygonFxTickCoverageManifestApprovalIssueCode
{
    MissingOperatorApprovalMarker,
    ApproveForResearchSmokeEvaluationFalse,
    MissingApprovedBy,
    InvalidApprovalTimestampUtc,
    WrongDataset,
    WrongSymbols,
    WrongDateRange,
    WrongAvailabilityMode,
    WrongAvailabilityDelay,
    MissingResearchOnlyAcknowledgment,
    MissingSimulatedAvailabilityAcknowledgment,
    MissingNoProductionUseAcknowledgment,
    MissingNoExecutionOrOrdersAcknowledgment,
    MissingDiagnosticEvaluationOnlyAcknowledgment,
    HashMismatch,
    MissingDataFile,
    WrongManifestApprovalState,
    WrongFileApprovalState,
    WrongManifestAvailabilityMode
}

public sealed record PolygonFxTickCoverageOperatorApprovalMarker(
    bool ApproveForResearchSmokeEvaluation,
    string? ApprovedBy,
    DateTimeOffset? ApprovalTimestampUtc,
    string ApprovedDataset,
    IReadOnlyList<string> ApprovedSymbols,
    DateTimeOffset? ApprovedDateRangeStartUtc,
    DateTimeOffset? ApprovedDateRangeEndUtc,
    string ApprovedAvailabilityMode,
    string ApprovedAssumedAvailabilityDelay,
    bool AcknowledgesDatasetIsResearchOnly,
    bool AcknowledgesAvailabilityIsSimulatedNotProviderReceivedTime,
    bool AcknowledgesNoProductionUse,
    bool AcknowledgesNoExecutionOrOrders,
    bool AcknowledgesNextStepIsDiagnosticEvaluationOnly);

public sealed record PolygonFxTickCoverageManifestApprovalIssue(
    PolygonFxTickCoverageManifestApprovalIssueCode Code,
    string Message);

public sealed record PolygonFxTickCoverageManifestApprovalGateResult(
    bool CanApprove,
    IReadOnlyList<PolygonFxTickCoverageManifestApprovalIssue> Issues)
{
    public string Reason => CanApprove ? "Operator approval marker is valid." : Issues.FirstOrDefault()?.Message ?? "Approval blocked.";
}

public sealed class PolygonFxTickCoverageManifestApprovalResearch
{
    public static readonly IReadOnlyList<string> RequiredSymbols = ["C:EURUSD", "C:GBPUSD", "C:AUDUSD"];
    public static readonly DateTimeOffset RequiredStartUtc = new(2025, 07, 01, 00, 00, 00, TimeSpan.Zero);
    public static readonly DateTimeOffset RequiredEndUtc = new(2025, 07, 03, 23, 59, 59, TimeSpan.Zero);
    public const string RequiredDataset = "PolygonFxTicksR012";
    public const string RequiredAvailabilityMode = "EventTimestampPlusConfiguredDelay";
    public const string RequiredAvailabilityDelay = "00:00:05";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public PolygonFxTickCoverageOperatorApprovalMarker CreateTemplate()
        => new(
            ApproveForResearchSmokeEvaluation: false,
            ApprovedBy: null,
            ApprovalTimestampUtc: null,
            ApprovedDataset: RequiredDataset,
            ApprovedSymbols: RequiredSymbols,
            ApprovedDateRangeStartUtc: RequiredStartUtc,
            ApprovedDateRangeEndUtc: RequiredEndUtc,
            ApprovedAvailabilityMode: RequiredAvailabilityMode,
            ApprovedAssumedAvailabilityDelay: RequiredAvailabilityDelay,
            AcknowledgesDatasetIsResearchOnly: false,
            AcknowledgesAvailabilityIsSimulatedNotProviderReceivedTime: false,
            AcknowledgesNoProductionUse: false,
            AcknowledgesNoExecutionOrOrders: false,
            AcknowledgesNextStepIsDiagnosticEvaluationOnly: false);

    public string SerializeTemplate()
        => JsonSerializer.Serialize(CreateTemplate(), JsonOptions);

    public PolygonFxTickCoverageOperatorApprovalMarker? ReadMarker(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<PolygonFxTickCoverageOperatorApprovalMarker>(File.ReadAllText(path), JsonOptions);
    }

    public PolygonFxTickCoverageManifestApprovalGateResult ValidateApprovalMarker(
        PolygonFxTickCoverageOperatorApprovalMarker? marker)
    {
        if (marker is null)
        {
            return Blocked(PolygonFxTickCoverageManifestApprovalIssueCode.MissingOperatorApprovalMarker, "Operator approval marker is missing.");
        }

        var issues = new List<PolygonFxTickCoverageManifestApprovalIssue>();
        if (!marker.ApproveForResearchSmokeEvaluation)
        {
            issues.Add(Issue(PolygonFxTickCoverageManifestApprovalIssueCode.ApproveForResearchSmokeEvaluationFalse, "ApproveForResearchSmokeEvaluation is not true."));
        }

        if (string.IsNullOrWhiteSpace(marker.ApprovedBy))
        {
            issues.Add(Issue(PolygonFxTickCoverageManifestApprovalIssueCode.MissingApprovedBy, "ApprovedBy is required."));
        }

        if (marker.ApprovalTimestampUtc is null || marker.ApprovalTimestampUtc.Value.Offset != TimeSpan.Zero)
        {
            issues.Add(Issue(PolygonFxTickCoverageManifestApprovalIssueCode.InvalidApprovalTimestampUtc, "ApprovalTimestampUtc must be explicit UTC."));
        }

        if (!string.Equals(marker.ApprovedDataset, RequiredDataset, StringComparison.Ordinal))
        {
            issues.Add(Issue(PolygonFxTickCoverageManifestApprovalIssueCode.WrongDataset, $"ApprovedDataset must be {RequiredDataset}."));
        }

        var symbols = marker.ApprovedSymbols
            .Select(x => x.Trim().ToUpperInvariant())
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        if (!symbols.SequenceEqual(RequiredSymbols.OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            issues.Add(Issue(PolygonFxTickCoverageManifestApprovalIssueCode.WrongSymbols, "ApprovedSymbols must exactly match the fixed R012 symbols."));
        }

        if (marker.ApprovedDateRangeStartUtc != RequiredStartUtc ||
            marker.ApprovedDateRangeEndUtc != RequiredEndUtc)
        {
            issues.Add(Issue(PolygonFxTickCoverageManifestApprovalIssueCode.WrongDateRange, "Approved date range must exactly match the fixed R012 date range."));
        }

        if (!string.Equals(marker.ApprovedAvailabilityMode, RequiredAvailabilityMode, StringComparison.Ordinal))
        {
            issues.Add(Issue(PolygonFxTickCoverageManifestApprovalIssueCode.WrongAvailabilityMode, "ApprovedAvailabilityMode must be EventTimestampPlusConfiguredDelay."));
        }

        if (!string.Equals(marker.ApprovedAssumedAvailabilityDelay, RequiredAvailabilityDelay, StringComparison.Ordinal))
        {
            issues.Add(Issue(PolygonFxTickCoverageManifestApprovalIssueCode.WrongAvailabilityDelay, "ApprovedAssumedAvailabilityDelay must be 00:00:05."));
        }

        if (!marker.AcknowledgesDatasetIsResearchOnly)
        {
            issues.Add(Issue(PolygonFxTickCoverageManifestApprovalIssueCode.MissingResearchOnlyAcknowledgment, "Research-only acknowledgment is required."));
        }

        if (!marker.AcknowledgesAvailabilityIsSimulatedNotProviderReceivedTime)
        {
            issues.Add(Issue(PolygonFxTickCoverageManifestApprovalIssueCode.MissingSimulatedAvailabilityAcknowledgment, "Simulated availability acknowledgment is required."));
        }

        if (!marker.AcknowledgesNoProductionUse)
        {
            issues.Add(Issue(PolygonFxTickCoverageManifestApprovalIssueCode.MissingNoProductionUseAcknowledgment, "No-production-use acknowledgment is required."));
        }

        if (!marker.AcknowledgesNoExecutionOrOrders)
        {
            issues.Add(Issue(PolygonFxTickCoverageManifestApprovalIssueCode.MissingNoExecutionOrOrdersAcknowledgment, "No-execution-or-orders acknowledgment is required."));
        }

        if (!marker.AcknowledgesNextStepIsDiagnosticEvaluationOnly)
        {
            issues.Add(Issue(PolygonFxTickCoverageManifestApprovalIssueCode.MissingDiagnosticEvaluationOnlyAcknowledgment, "Diagnostic-evaluation-only next step acknowledgment is required."));
        }

        return new(issues.Count == 0, issues);
    }

    public IReadOnlyList<PolygonFxTickCoverageManifestApprovalIssue> ValidateProposal(
        FxBboResearchDataAuthorizationManifest proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        var issues = new List<PolygonFxTickCoverageManifestApprovalIssue>();
        if (proposal.AuthorizedForResearch)
        {
            issues.Add(Issue(PolygonFxTickCoverageManifestApprovalIssueCode.WrongManifestApprovalState, "R012 proposal must not already be AuthorizedForResearch."));
        }

        if (proposal.Files.Any(x => x.Approved))
        {
            issues.Add(Issue(PolygonFxTickCoverageManifestApprovalIssueCode.WrongFileApprovalState, "R012 proposal file entries must not already be Approved."));
        }

        foreach (var file in proposal.Files)
        {
            if (file.AvailabilityMode != FxBboResearchAvailabilityMode.EventTimestampPlusConfiguredDelay ||
                file.AssumedAvailabilityDelay != TimeSpan.FromSeconds(5) ||
                string.Equals(file.AvailableAtColumn, "download_observed_at_utc", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(file.ReceivedAtColumn, "download_observed_at_utc", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Issue(PolygonFxTickCoverageManifestApprovalIssueCode.WrongManifestAvailabilityMode, "R012 proposal must use EventTimestampPlusConfiguredDelay 00:00:05 and must not use download_observed_at_utc as availability."));
            }
        }

        return issues;
    }

    public FxBboResearchDataAuthorizationManifest CreateApprovedManifest(
        FxBboResearchDataAuthorizationManifest proposal,
        PolygonFxTickCoverageOperatorApprovalMarker marker,
        bool sequenceIdIsMeaningful)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(marker);

        var gate = ValidateApprovalMarker(marker);
        if (!gate.CanApprove)
        {
            throw new InvalidOperationException(gate.Reason);
        }

        var proposalIssues = ValidateProposal(proposal);
        if (proposalIssues.Count > 0)
        {
            throw new InvalidOperationException(proposalIssues[0].Message);
        }

        return proposal with
        {
            DatasetName = "Polygon FX BBO Coverage Backfill R012 Approved Local Smoke Dataset",
            AuthorizedForResearch = true,
            AuthorizedBy = marker.ApprovedBy,
            AuthorizationTimestampUtc = marker.ApprovalTimestampUtc,
            Files = proposal.Files.Select(file => file with
            {
                Approved = true,
                SequenceIdColumn = sequenceIdIsMeaningful ? file.SequenceIdColumn : null,
                AvailabilityMode = FxBboResearchAvailabilityMode.EventTimestampPlusConfiguredDelay,
                AssumedAvailabilityDelay = TimeSpan.FromSeconds(5),
                AvailableAtColumn = null,
                ReceivedAtColumn = null,
                AvailabilityJustification = "Dataset approved only for residual-divergence smoke diagnostic evaluation. No production/execution use."
            }).ToArray()
        };
    }

    private static PolygonFxTickCoverageManifestApprovalGateResult Blocked(
        PolygonFxTickCoverageManifestApprovalIssueCode code,
        string message)
        => new(false, [Issue(code, message)]);

    private static PolygonFxTickCoverageManifestApprovalIssue Issue(
        PolygonFxTickCoverageManifestApprovalIssueCode code,
        string message)
        => new(code, message);
}
