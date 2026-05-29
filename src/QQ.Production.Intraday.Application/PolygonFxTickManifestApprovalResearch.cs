using System.Text.Json;
using System.Text.Json.Serialization;

namespace QQ.Production.Intraday.Application;

public enum PolygonFxTickManifestApprovalIssueCode
{
    MissingOperatorApprovalMarker,
    ApproveForResearchSmokeEvaluationFalse,
    MissingApprovedBy,
    InvalidApprovalTimestampUtc,
    WrongDataset,
    WrongSymbols,
    WrongAvailabilityMode,
    WrongAvailabilityDelay,
    MissingTruncatedDatasetAcknowledgment,
    MissingSimulatedAvailabilityAcknowledgment,
    MissingResearchOnlyAcknowledgment,
    HashMismatch,
    MissingDataFile,
    ApprovedManifestNotRequested
}

public sealed record PolygonFxTickOperatorApprovalMarker(
    bool ApproveForResearchSmokeEvaluation,
    string? ApprovedBy,
    DateTimeOffset? ApprovalTimestampUtc,
    string ApprovedDataset,
    IReadOnlyList<string> ApprovedSymbols,
    string ApprovedAvailabilityMode,
    string ApprovedAssumedAvailabilityDelay,
    bool AcknowledgesDatasetIsTruncatedByPageCap,
    bool AcknowledgesAvailabilityIsSimulatedNotProviderReceivedTime,
    bool AcknowledgesResearchOnlyNoProductionUse);

public sealed record PolygonFxTickManifestApprovalIssue(
    PolygonFxTickManifestApprovalIssueCode Code,
    string Message);

public sealed record PolygonFxTickManifestApprovalGateResult(
    bool CanApprove,
    IReadOnlyList<PolygonFxTickManifestApprovalIssue> Issues)
{
    public string Reason => CanApprove ? "Operator approval marker is valid." : Issues.FirstOrDefault()?.Message ?? "Approval blocked.";
}

public sealed class PolygonFxTickManifestApprovalResearch
{
    public static readonly IReadOnlyList<string> RequiredSymbols = ["C:EURUSD", "C:GBPUSD", "C:AUDUSD"];
    public const string RequiredDataset = "PolygonFxTicksR009";
    public const string RequiredAvailabilityMode = "EventTimestampPlusConfiguredDelay";
    public const string RequiredAvailabilityDelay = "00:00:05";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public PolygonFxTickOperatorApprovalMarker CreateTemplate()
        => new(
            ApproveForResearchSmokeEvaluation: false,
            ApprovedBy: null,
            ApprovalTimestampUtc: null,
            ApprovedDataset: RequiredDataset,
            ApprovedSymbols: RequiredSymbols,
            ApprovedAvailabilityMode: RequiredAvailabilityMode,
            ApprovedAssumedAvailabilityDelay: RequiredAvailabilityDelay,
            AcknowledgesDatasetIsTruncatedByPageCap: false,
            AcknowledgesAvailabilityIsSimulatedNotProviderReceivedTime: false,
            AcknowledgesResearchOnlyNoProductionUse: false);

    public string SerializeTemplate()
        => JsonSerializer.Serialize(CreateTemplate(), JsonOptions);

    public PolygonFxTickOperatorApprovalMarker? ReadMarker(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<PolygonFxTickOperatorApprovalMarker>(File.ReadAllText(path), JsonOptions);
    }

    public PolygonFxTickManifestApprovalGateResult ValidateApprovalMarker(PolygonFxTickOperatorApprovalMarker? marker)
    {
        if (marker is null)
        {
            return Blocked(PolygonFxTickManifestApprovalIssueCode.MissingOperatorApprovalMarker, "Operator approval marker is missing.");
        }

        var issues = new List<PolygonFxTickManifestApprovalIssue>();
        if (!marker.ApproveForResearchSmokeEvaluation)
        {
            issues.Add(Issue(PolygonFxTickManifestApprovalIssueCode.ApproveForResearchSmokeEvaluationFalse, "ApproveForResearchSmokeEvaluation is not true."));
        }

        if (string.IsNullOrWhiteSpace(marker.ApprovedBy))
        {
            issues.Add(Issue(PolygonFxTickManifestApprovalIssueCode.MissingApprovedBy, "ApprovedBy is required."));
        }

        if (marker.ApprovalTimestampUtc is null || marker.ApprovalTimestampUtc.Value.Offset != TimeSpan.Zero)
        {
            issues.Add(Issue(PolygonFxTickManifestApprovalIssueCode.InvalidApprovalTimestampUtc, "ApprovalTimestampUtc must be explicit UTC."));
        }

        if (!string.Equals(marker.ApprovedDataset, RequiredDataset, StringComparison.Ordinal))
        {
            issues.Add(Issue(PolygonFxTickManifestApprovalIssueCode.WrongDataset, $"ApprovedDataset must be {RequiredDataset}."));
        }

        var symbols = marker.ApprovedSymbols
            .Select(x => x.Trim().ToUpperInvariant())
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        if (!symbols.SequenceEqual(RequiredSymbols.OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            issues.Add(Issue(PolygonFxTickManifestApprovalIssueCode.WrongSymbols, "ApprovedSymbols must exactly match the fixed R009 symbols."));
        }

        if (!string.Equals(marker.ApprovedAvailabilityMode, RequiredAvailabilityMode, StringComparison.Ordinal))
        {
            issues.Add(Issue(PolygonFxTickManifestApprovalIssueCode.WrongAvailabilityMode, "ApprovedAvailabilityMode must be EventTimestampPlusConfiguredDelay."));
        }

        if (!string.Equals(marker.ApprovedAssumedAvailabilityDelay, RequiredAvailabilityDelay, StringComparison.Ordinal))
        {
            issues.Add(Issue(PolygonFxTickManifestApprovalIssueCode.WrongAvailabilityDelay, "ApprovedAssumedAvailabilityDelay must be 00:00:05."));
        }

        if (!marker.AcknowledgesDatasetIsTruncatedByPageCap)
        {
            issues.Add(Issue(PolygonFxTickManifestApprovalIssueCode.MissingTruncatedDatasetAcknowledgment, "Truncated dataset acknowledgment is required."));
        }

        if (!marker.AcknowledgesAvailabilityIsSimulatedNotProviderReceivedTime)
        {
            issues.Add(Issue(PolygonFxTickManifestApprovalIssueCode.MissingSimulatedAvailabilityAcknowledgment, "Simulated availability acknowledgment is required."));
        }

        if (!marker.AcknowledgesResearchOnlyNoProductionUse)
        {
            issues.Add(Issue(PolygonFxTickManifestApprovalIssueCode.MissingResearchOnlyAcknowledgment, "Research-only acknowledgment is required."));
        }

        return new(issues.Count == 0, issues);
    }

    public FxBboResearchDataAuthorizationManifest CreateApprovedManifest(
        FxBboResearchDataAuthorizationManifest proposal,
        PolygonFxTickOperatorApprovalMarker marker)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(marker);

        var gate = ValidateApprovalMarker(marker);
        if (!gate.CanApprove)
        {
            throw new InvalidOperationException(gate.Reason);
        }

        return proposal with
        {
            AuthorizedForResearch = true,
            AuthorizedBy = marker.ApprovedBy,
            AuthorizationTimestampUtc = marker.ApprovalTimestampUtc,
            Files = proposal.Files.Select(file => file with
            {
                Approved = true,
                AvailabilityMode = FxBboResearchAvailabilityMode.EventTimestampPlusConfiguredDelay,
                AssumedAvailabilityDelay = TimeSpan.FromSeconds(5),
                AvailableAtColumn = null,
                ReceivedAtColumn = null
            }).ToArray()
        };
    }

    private static PolygonFxTickManifestApprovalGateResult Blocked(
        PolygonFxTickManifestApprovalIssueCode code,
        string message)
        => new(false, [Issue(code, message)]);

    private static PolygonFxTickManifestApprovalIssue Issue(
        PolygonFxTickManifestApprovalIssueCode code,
        string message)
        => new(code, message);
}
