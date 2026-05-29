namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyVenueProfilePurpose
{
    Disabled,
    LabOnly,
    ReadOnlyPrototypeFuture
}

public enum LmaxReadOnlyVenueProfileSafetyStatus
{
    Disabled,
    Blocked,
    FuturePrototypeLabelOnly
}

public enum LmaxReadOnlyVenueProfileRedactionStatus
{
    NotApplicable,
    LabelOnly
}

public sealed record LmaxReadOnlyVenueProfileName(string Value)
{
    public static readonly LmaxReadOnlyVenueProfileName DemoLondon = new("DemoLondon");
    public static readonly LmaxReadOnlyVenueProfileName Uat = new("Uat");
    public static readonly LmaxReadOnlyVenueProfileName Production = new("Production");
    public static readonly LmaxReadOnlyVenueProfileName LegacyDemoReadOnly = new("LmaxDemoReadOnly");
}

public sealed record LmaxReadOnlyVenueProfileDescriptor(
    string VenueProfileName,
    string EnvironmentName,
    bool IsActive,
    bool IsExternalConnectionAllowed,
    bool IsCredentialUseAllowed,
    string Description,
    IReadOnlyList<LmaxReadOnlyVenueProfilePurpose> SupportedPurposes,
    LmaxReadOnlyVenueProfileSafetyStatus SafetyStatus,
    LmaxReadOnlyVenueProfileRedactionStatus RedactionStatus);

public sealed record LmaxReadOnlyVenueProfileValidationResult(
    bool IsKnown,
    bool IsAllowedForPhase4,
    LmaxReadOnlyVenueProfileDescriptor? Descriptor,
    IReadOnlyList<LmaxReadOnlyExternalSessionConfigIssue> Issues)
{
    public bool HasErrors => Issues.Any(x => x.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Error);
    public int ErrorCount => Issues.Count(x => x.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Error);
}

public interface ILmaxReadOnlyVenueProfileRegistry
{
    IReadOnlyList<LmaxReadOnlyVenueProfileDescriptor> ListProfiles();
    LmaxReadOnlyVenueProfileValidationResult Validate(string? venueProfileName, string? environmentName);
}

public sealed class LmaxReadOnlyVenueProfileRegistryDisabled : ILmaxReadOnlyVenueProfileRegistry
{
    private static readonly IReadOnlyList<LmaxReadOnlyVenueProfileDescriptor> Profiles =
    [
        new(
            LmaxReadOnlyVenueProfileName.DemoLondon.Value,
            "Demo",
            IsActive: false,
            IsExternalConnectionAllowed: false,
            IsCredentialUseAllowed: false,
            "Demo read-only prototype label for a future external session. It contains no endpoint, account, session, or credential values.",
            [LmaxReadOnlyVenueProfilePurpose.ReadOnlyPrototypeFuture, LmaxReadOnlyVenueProfilePurpose.Disabled],
            LmaxReadOnlyVenueProfileSafetyStatus.FuturePrototypeLabelOnly,
            LmaxReadOnlyVenueProfileRedactionStatus.LabelOnly),
        new(
            LmaxReadOnlyVenueProfileName.LegacyDemoReadOnly.Value,
            "Demo",
            IsActive: false,
            IsExternalConnectionAllowed: false,
            IsCredentialUseAllowed: false,
            "Legacy local demo label retained for compatibility with earlier safe-disabled configuration examples. It contains no endpoint, account, session, or credential values.",
            [LmaxReadOnlyVenueProfilePurpose.ReadOnlyPrototypeFuture, LmaxReadOnlyVenueProfilePurpose.Disabled],
            LmaxReadOnlyVenueProfileSafetyStatus.FuturePrototypeLabelOnly,
            LmaxReadOnlyVenueProfileRedactionStatus.LabelOnly),
        new(
            LmaxReadOnlyVenueProfileName.Uat.Value,
            "UAT",
            IsActive: false,
            IsExternalConnectionAllowed: false,
            IsCredentialUseAllowed: false,
            "UAT label reserved for a future governance gate. It is blocked for the current Phase 4 path.",
            [LmaxReadOnlyVenueProfilePurpose.Disabled],
            LmaxReadOnlyVenueProfileSafetyStatus.Blocked,
            LmaxReadOnlyVenueProfileRedactionStatus.LabelOnly),
        new(
            LmaxReadOnlyVenueProfileName.Production.Value,
            "Production",
            IsActive: false,
            IsExternalConnectionAllowed: false,
            IsCredentialUseAllowed: false,
            "Production label reserved for a separate future program. It is blocked for the current Phase 4 path.",
            [LmaxReadOnlyVenueProfilePurpose.Disabled],
            LmaxReadOnlyVenueProfileSafetyStatus.Blocked,
            LmaxReadOnlyVenueProfileRedactionStatus.LabelOnly)
    ];

    public IReadOnlyList<LmaxReadOnlyVenueProfileDescriptor> ListProfiles()
        => Profiles;

    public LmaxReadOnlyVenueProfileValidationResult Validate(string? venueProfileName, string? environmentName)
    {
        var issues = new List<LmaxReadOnlyExternalSessionConfigIssue>();

        if (string.IsNullOrWhiteSpace(venueProfileName))
        {
            issues.Add(Error("VenueProfileNameRequired", "$.venueProfileName", "VenueProfileName label is required."));
            return new LmaxReadOnlyVenueProfileValidationResult(false, false, null, issues);
        }

        var descriptor = Profiles.FirstOrDefault(x => string.Equals(x.VenueProfileName, venueProfileName, StringComparison.OrdinalIgnoreCase));
        if (descriptor is null)
        {
            issues.Add(Error("VenueProfileUnknown", "$.venueProfileName", $"VenueProfileName '{venueProfileName}' is not a known non-secret label."));
            return new LmaxReadOnlyVenueProfileValidationResult(false, false, null, issues);
        }

        issues.Add(Info("VenueProfileBoundaryPresent", "$.venueProfileName", "VenueProfileName is a non-secret label only; venue profiles expose no endpoint, account, session, or credential values."));

        if (!string.Equals(descriptor.EnvironmentName, environmentName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("VenueProfileEnvironmentMismatch", "$.venueProfileName", $"VenueProfileName '{descriptor.VenueProfileName}' requires EnvironmentName={descriptor.EnvironmentName}."));
        }

        if (descriptor.VenueProfileName.Equals(LmaxReadOnlyVenueProfileName.Uat.Value, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("VenueProfileUatBlocked", "$.venueProfileName", "UAT venue profile is blocked for the current Phase 4 path."));
        }

        if (descriptor.VenueProfileName.Equals(LmaxReadOnlyVenueProfileName.Production.Value, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("VenueProfileProductionBlocked", "$.venueProfileName", "Production venue profile is blocked for the current Phase 4 path."));
        }

        if (descriptor.IsActive)
        {
            issues.Add(Error("VenueProfileActiveForbidden", "$.venueProfileName", "Venue profiles must remain inactive in Phase 4I."));
        }

        if (descriptor.IsExternalConnectionAllowed)
        {
            issues.Add(Error("VenueProfileExternalConnectionForbidden", "$.venueProfileName", "Venue profiles must not allow external connections in Phase 4I."));
        }

        if (descriptor.IsCredentialUseAllowed)
        {
            issues.Add(Error("VenueProfileCredentialUseForbidden", "$.venueProfileName", "Venue profiles must not allow credential use in Phase 4I."));
        }

        var allowedForPhase4 = descriptor.VenueProfileName.Equals(LmaxReadOnlyVenueProfileName.DemoLondon.Value, StringComparison.OrdinalIgnoreCase)
                               || descriptor.VenueProfileName.Equals(LmaxReadOnlyVenueProfileName.LegacyDemoReadOnly.Value, StringComparison.OrdinalIgnoreCase);
        if (!allowedForPhase4)
        {
            issues.Add(Error("VenueProfileNotAllowedForPhase4", "$.venueProfileName", $"VenueProfileName '{descriptor.VenueProfileName}' is not allowed for the current Phase 4 path."));
        }

        return new LmaxReadOnlyVenueProfileValidationResult(
            true,
            allowedForPhase4 && !issues.Any(x => x.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Error),
            descriptor,
            issues);
    }

    private static LmaxReadOnlyExternalSessionConfigIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyExternalSessionConfigIssueSeverity.Error, code, path, message);

    private static LmaxReadOnlyExternalSessionConfigIssue Info(string code, string path, string message)
        => new(LmaxReadOnlyExternalSessionConfigIssueSeverity.Info, code, path, message);
}
