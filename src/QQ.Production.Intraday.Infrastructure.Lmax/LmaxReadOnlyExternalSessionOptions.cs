namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyExternalSessionConfigIssueSeverity
{
    Error,
    Warning,
    Info
}

public sealed record LmaxReadOnlyExternalSessionOptions
{
    public const int SafeMaxEventsPerRun = 1_000;
    public const int SafeMaxRuntimeSeconds = 300;

    public bool Enabled { get; init; }
    public LmaxReadOnlyRuntimeImplementationMode ImplementationMode { get; init; } = LmaxReadOnlyRuntimeImplementationMode.DesignOnly;
    public LmaxReadOnlyRuntimeActivationLevel ActivationLevel { get; init; } = LmaxReadOnlyRuntimeActivationLevel.Level1DisabledSkeleton;
    public string EnvironmentName { get; init; } = "Demo";
    public string VenueProfileName { get; init; } = "DemoLondon";
    public string CredentialProfileName { get; init; } = "LmaxDemoReadOnlyProfile";
    public bool AllowExternalConnections { get; init; }
    public bool AllowCredentialUse { get; init; }
    public bool AllowOrderSubmission { get; init; }
    public bool PersistRawFixMessages { get; init; }
    public bool PersistToTradingTables { get; init; }
    public bool SchedulerEnabled { get; init; }
    public bool SubmitToShadowReplay { get; init; }
    public bool DryRun { get; init; } = true;
    public bool RequireReason { get; init; } = true;
    public bool RequireOperationalReadinessPass { get; init; } = true;
    public bool OperationalReadinessPassed { get; init; }
    public bool RequireGovernanceApproval { get; init; } = true;
    public bool GovernanceApproved { get; init; }
    public int MaxRuntimeSeconds { get; init; } = 30;
    public int MaxEventsPerRun { get; init; } = 100;
}

public sealed record LmaxReadOnlyExternalSessionEnvironmentOptions(
    string EnvironmentName = "Demo",
    string VenueProfileName = "DemoLondon");

public sealed record LmaxReadOnlyExternalSessionLimitsOptions(
    int MaxRuntimeSeconds = 30,
    int MaxEventsPerRun = 100);

public sealed record LmaxReadOnlyExternalSessionCredentialProfileOptions(
    string CredentialProfileName = "LmaxDemoReadOnlyProfile",
    bool AllowCredentialUse = false);

public sealed record LmaxReadOnlyExternalSessionConfigIssue(
    LmaxReadOnlyExternalSessionConfigIssueSeverity Severity,
    string Code,
    string Path,
    string Message);

public sealed record LmaxReadOnlyExternalSessionOptionsValidationResult(
    bool IsSafeDisabled,
    bool HasErrors,
    IReadOnlyList<LmaxReadOnlyExternalSessionConfigIssue> Issues)
{
    public int ErrorCount => Issues.Count(x => x.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Error);
    public int WarningCount => Issues.Count(x => x.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Warning);
    public int InfoCount => Issues.Count(x => x.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Info);
}

public static class LmaxReadOnlyExternalSessionOptionsValidator
{
    private static readonly string[] SecretShapedNames =
    [
        "password",
        "secret",
        "token",
        "apiKey",
        "privateKey",
        "credentialValue",
        "authorization"
    ];

    public static LmaxReadOnlyExternalSessionOptionsValidationResult Validate(
        LmaxReadOnlyExternalSessionOptions options,
        string? reason = null)
    {
        var issues = new List<LmaxReadOnlyExternalSessionConfigIssue>();
        var venueValidation = new LmaxReadOnlyVenueProfileRegistryDisabled().Validate(options.VenueProfileName, options.EnvironmentName);
        issues.AddRange(venueValidation.Issues);

        if (!options.Enabled && options.ImplementationMode == LmaxReadOnlyRuntimeImplementationMode.DesignOnly)
        {
            issues.Add(Info("SafeDisabled", "$", "External read-only session configuration is disabled/design-only."));
        }

        if (options.Enabled)
        {
            issues.Add(Error("ExternalSessionImplementationNotStarted", "$.enabled", "Enabled=true is blocked in Phase 4H because no external read-only implementation exists."));
        }

        if (options.ImplementationMode != LmaxReadOnlyRuntimeImplementationMode.DesignOnly)
        {
            issues.Add(Error("ImplementationModeBlocked", "$.implementationMode", "Only DesignOnly is safe for the Phase 4H configuration envelope."));
        }

        if (options.ActivationLevel >= LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit)
        {
            issues.Add(Error("ActivationLevelBlocked", "$.activationLevel", "Runtime external activation levels remain blocked in Phase 4H."));
        }

        if (!string.Equals(options.EnvironmentName, "Demo", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("EnvironmentMustBeDemo", "$.environmentName", "Initial external read-only prototype configuration must use EnvironmentName=Demo."));
        }

        if (options.AllowOrderSubmission)
        {
            issues.Add(Error("OrderSubmissionForbidden", "$.allowOrderSubmission", "AllowOrderSubmission must always be false."));
        }

        if (options.PersistToTradingTables)
        {
            issues.Add(Error("TradingTablePersistenceForbidden", "$.persistToTradingTables", "PersistToTradingTables must always be false."));
        }

        if (options.SchedulerEnabled)
        {
            issues.Add(Error("SchedulerForbidden", "$.schedulerEnabled", "SchedulerEnabled must remain false."));
        }

        if (options.SubmitToShadowReplay)
        {
            issues.Add(Error("ShadowReplaySubmitDeferred", "$.submitToShadowReplay", "SubmitToShadowReplay must remain false for initial Phase 4 external configuration."));
        }

        if (options.PersistRawFixMessages)
        {
            issues.Add(Error("RawFixPersistenceForbidden", "$.persistRawFixMessages", "PersistRawFixMessages must remain false until a separate sanitized-retention gate."));
        }

        if (options.AllowExternalConnections)
        {
            issues.Add(Error("ExternalConnectionBlocked", "$.allowExternalConnections", "AllowExternalConnections=true is blocked in Phase 4H; no network transport exists."));
        }

        if (options.AllowCredentialUse)
        {
            issues.Add(Error("CredentialUseBlocked", "$.allowCredentialUse", "AllowCredentialUse=true is blocked in Phase 4H; the credential profile resolver is disabled and reads no credential values."));
            issues.Add(Error("CredentialResolverDisabled", "$.credentialProfileName", "Credential use remains blocked in Phase 4H because only the disabled/no-op credential profile resolver exists."));
        }

        if ((options.Enabled || options.AllowCredentialUse)
            && string.IsNullOrWhiteSpace(options.CredentialProfileName))
        {
            issues.Add(Error("CredentialProfileNameRequired", "$.credentialProfileName", "A non-empty CredentialProfileName label is required before any future external activation can be considered."));
        }

        issues.Add(Info("CredentialProfileBoundaryPresent", "$.credentialProfileName", "CredentialProfileName is a non-secret label only; Phase 4H resolver remains disabled/no-op."));
        issues.Add(Info("VenueProfileBoundaryPresent", "$.venueProfileName", "VenueProfileName is a non-secret label only; Phase 4I venue profile registry remains inactive/disabled."));

        if (!options.DryRun)
        {
            issues.Add(Error("DryRunRequired", "$.dryRun", "DryRun must remain true."));
        }

        if (options.MaxRuntimeSeconds <= 0 || options.MaxRuntimeSeconds > LmaxReadOnlyExternalSessionOptions.SafeMaxRuntimeSeconds)
        {
            issues.Add(Error("MaxRuntimeSecondsOutOfRange", "$.maxRuntimeSeconds", $"MaxRuntimeSeconds must be within 1..{LmaxReadOnlyExternalSessionOptions.SafeMaxRuntimeSeconds}."));
        }

        if (options.MaxEventsPerRun <= 0 || options.MaxEventsPerRun > LmaxReadOnlyExternalSessionOptions.SafeMaxEventsPerRun)
        {
            issues.Add(Error("MaxEventsPerRunOutOfRange", "$.maxEventsPerRun", $"MaxEventsPerRun must be within 1..{LmaxReadOnlyExternalSessionOptions.SafeMaxEventsPerRun}."));
        }

        if (options.Enabled && options.RequireReason && string.IsNullOrWhiteSpace(reason))
        {
            issues.Add(Error("ReasonRequired", "$.reason", "A non-empty reason is required for any future external session run request."));
        }

        foreach (var property in typeof(LmaxReadOnlyExternalSessionOptions).GetProperties())
        {
            foreach (var forbidden in SecretShapedNames)
            {
                if (property.Name.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(Error("SecretShapedOptionName", "$." + property.Name, $"Option property name '{property.Name}' is not allowed because it looks like a credential value field."));
                }
            }
        }

        var safeDisabled = !options.Enabled
                           && options.ImplementationMode == LmaxReadOnlyRuntimeImplementationMode.DesignOnly
                           && !options.AllowExternalConnections
                           && !options.AllowCredentialUse
                           && !options.AllowOrderSubmission
                           && !options.PersistRawFixMessages
                           && !options.PersistToTradingTables
                           && !options.SchedulerEnabled
                           && !options.SubmitToShadowReplay
                           && options.DryRun;
        return new LmaxReadOnlyExternalSessionOptionsValidationResult(
            safeDisabled,
            issues.Any(x => x.Severity == LmaxReadOnlyExternalSessionConfigIssueSeverity.Error),
            issues);
    }

    private static LmaxReadOnlyExternalSessionConfigIssue Error(string code, string path, string message)
        => new(LmaxReadOnlyExternalSessionConfigIssueSeverity.Error, code, path, message);

    private static LmaxReadOnlyExternalSessionConfigIssue Info(string code, string path, string message)
        => new(LmaxReadOnlyExternalSessionConfigIssueSeverity.Info, code, path, message);
}
