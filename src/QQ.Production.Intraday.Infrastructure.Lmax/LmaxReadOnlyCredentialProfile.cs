namespace QQ.Production.Intraday.Infrastructure.Lmax;

public interface ILmaxReadOnlyCredentialProfileResolver
{
    Task<LmaxReadOnlyCredentialProfileStatus> GetStatusAsync(
        LmaxReadOnlyCredentialProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<LmaxReadOnlyCredentialProfileResult> ResolveAsync(
        LmaxReadOnlyCredentialProfileRequest request,
        CancellationToken cancellationToken = default);
}

public enum LmaxReadOnlyCredentialProfileResolverMode
{
    Disabled,
    EnvironmentAvailability
}

public enum LmaxReadOnlyCredentialProfileSourceKind
{
    None,
    UserSecrets,
    Environment,
    FutureVault
}

public enum LmaxReadOnlyCredentialProfileRedactionStatus
{
    NotApplicable,
    Redacted
}

public sealed record LmaxReadOnlyCredentialProfileRequest(
    string? CredentialProfileName,
    string? EnvironmentName,
    string? VenueProfileName,
    string? Reason = null);

public static class LmaxReadOnlyCredentialRequiredKeyLabels
{
    public static readonly IReadOnlyList<string> DemoReadOnlyEnvironmentLabels =
    [
        "LMAX_DEMO_FIX_USERNAME",
        "LMAX_DEMO_FIX_PASSWORD",
        "LMAX_DEMO_SENDER_COMP_ID",
        "LMAX_DEMO_TARGET_COMP_ID"
    ];
}

public sealed record LmaxReadOnlyCredentialAvailabilityKeyStatus(
    string KeyLabel,
    bool IsPresent,
    LmaxReadOnlyCredentialProfileRedactionStatus RedactionStatus);

public sealed record LmaxReadOnlyCredentialAvailabilityResult(
    string CredentialProfileName,
    LmaxReadOnlyCredentialProfileSourceKind SourceKind,
    bool IsConfigured,
    IReadOnlyList<string> MissingKeyLabels,
    IReadOnlyList<LmaxReadOnlyCredentialAvailabilityKeyStatus> KeyStatuses,
    LmaxReadOnlyCredentialProfileRedactionStatus RedactionStatus,
    bool SensitiveMaterialReturned,
    bool CredentialReadAttempted,
    bool CredentialValuesReturned,
    string Message);

public interface ILmaxReadOnlyCredentialAvailabilityResolver
{
    LmaxReadOnlyCredentialAvailabilityResult CheckAvailability(LmaxReadOnlyCredentialProfileRequest request);
}

public static class LmaxReadOnlyCredentialRedactionPolicy
{
    private static readonly string[] SecretShapedKeys =
    [
        "password",
        "secret",
        "token",
        "apiKey",
        "privateKey",
        "authorization"
    ];

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var redacted = value;
        foreach (var key in SecretShapedKeys)
        {
            redacted = System.Text.RegularExpressions.Regex.Replace(
                redacted,
                $"(?i)(\"?{System.Text.RegularExpressions.Regex.Escape(key)}\"?\\s*[:=]\\s*)\"?[^,;\\r\\n\"}}]+\"?",
                "$1[REDACTED]");
        }

        foreach (var label in LmaxReadOnlyCredentialRequiredKeyLabels.DemoReadOnlyEnvironmentLabels)
        {
            var raw = Environment.GetEnvironmentVariable(label);
            if (!string.IsNullOrEmpty(raw))
            {
                redacted = redacted.Replace(raw, "[REDACTED]", StringComparison.Ordinal);
            }
        }

        return redacted;
    }
}

public sealed class LmaxReadOnlyCredentialProfileResolverEnvironment : ILmaxReadOnlyCredentialAvailabilityResolver
{
    private readonly Func<string, string?> _read;

    public LmaxReadOnlyCredentialProfileResolverEnvironment(Func<string, string?>? read = null)
    {
        _read = read ?? Environment.GetEnvironmentVariable;
    }

    public LmaxReadOnlyCredentialAvailabilityResult CheckAvailability(LmaxReadOnlyCredentialProfileRequest request)
    {
        var profileName = string.IsNullOrWhiteSpace(request.CredentialProfileName)
            ? "(missing)"
            : request.CredentialProfileName;
        var keyStatuses = LmaxReadOnlyCredentialRequiredKeyLabels.DemoReadOnlyEnvironmentLabels
            .Select(label => new LmaxReadOnlyCredentialAvailabilityKeyStatus(
                label,
                !string.IsNullOrWhiteSpace(_read(label)),
                LmaxReadOnlyCredentialProfileRedactionStatus.Redacted))
            .ToList();
        var missing = keyStatuses.Where(x => !x.IsPresent).Select(x => x.KeyLabel).ToList();
        var configured = missing.Count == 0;

        return new LmaxReadOnlyCredentialAvailabilityResult(
            profileName,
            LmaxReadOnlyCredentialProfileSourceKind.Environment,
            configured,
            missing,
            keyStatuses,
            LmaxReadOnlyCredentialProfileRedactionStatus.Redacted,
            SensitiveMaterialReturned: false,
            CredentialReadAttempted: true,
            CredentialValuesReturned: false,
            configured
                ? "Credential availability check passed using environment key labels only. Values were read for presence only and were not returned."
                : "Credential availability check found missing environment key labels. Values were not returned.");
    }
}

public sealed record LmaxReadOnlyCredentialProfileDescriptor(
    string CredentialProfileName,
    string EnvironmentName,
    string VenueProfileName,
    bool IsConfigured,
    LmaxReadOnlyCredentialProfileSourceKind SourceKind,
    LmaxReadOnlyCredentialProfileRedactionStatus RedactionStatus);

public sealed record LmaxReadOnlyCredentialProfileSafetyReport(
    LmaxReadOnlyRuntimeRunStatus RunStatus,
    string BlockedReason,
    bool CredentialProfileBoundaryPresent,
    bool CredentialResolverDisabled,
    bool CredentialReadImplemented,
    bool CredentialUseImplemented,
    bool SensitiveMaterialReturned,
    bool RedactionRequired,
    LmaxReadOnlyCredentialProfileResolverMode ResolverMode,
    IReadOnlyList<LmaxReadOnlyRuntimeSafetyGateResult> Gates)
{
    public bool Passed => !Gates.Any(x => x.BlocksRun);
    public IReadOnlyList<string> FailedGateNames => Gates.Where(x => x.BlocksRun).Select(x => x.Name).ToList();
}

public sealed record LmaxReadOnlyCredentialProfileStatus(
    LmaxReadOnlyRuntimeRunStatus Status,
    LmaxReadOnlyCredentialProfileDescriptor Descriptor,
    LmaxReadOnlyCredentialProfileSafetyReport Safety,
    string Message);

public sealed record LmaxReadOnlyCredentialProfileResult(
    LmaxReadOnlyRuntimeRunStatus Status,
    LmaxReadOnlyCredentialProfileDescriptor Descriptor,
    bool CredentialReadImplemented,
    bool CredentialUseImplemented,
    bool SensitiveMaterialReturned,
    bool RedactionRequired,
    string Message,
    LmaxReadOnlyCredentialProfileSafetyReport Safety);

public sealed class LmaxReadOnlyCredentialProfileResolverDisabled : ILmaxReadOnlyCredentialProfileResolver
{
    public Task<LmaxReadOnlyCredentialProfileStatus> GetStatusAsync(
        LmaxReadOnlyCredentialProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var safety = Evaluate(request);
        return Task.FromResult(new LmaxReadOnlyCredentialProfileStatus(
            safety.RunStatus,
            CreateDescriptor(request),
            safety,
            "LMAX read-only credential profile resolver is disabled. ResolverMode=Disabled; CredentialReadImplemented=false; CredentialUseImplemented=false; SensitiveMaterialReturned=false; RedactionRequired=true."));
    }

    public Task<LmaxReadOnlyCredentialProfileResult> ResolveAsync(
        LmaxReadOnlyCredentialProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var safety = Evaluate(request);
        return Task.FromResult(new LmaxReadOnlyCredentialProfileResult(
            safety.RunStatus,
            CreateDescriptor(request),
            CredentialReadImplemented: false,
            CredentialUseImplemented: false,
            SensitiveMaterialReturned: false,
            RedactionRequired: true,
            "LMAX read-only credential profile resolver is disabled/not available for Phase 4H. No profile stores, process variables, application settings, vaults, or credential material were read, logged, stored, used, or returned. " + safety.BlockedReason,
            safety));
    }

    private static LmaxReadOnlyCredentialProfileSafetyReport Evaluate(LmaxReadOnlyCredentialProfileRequest request)
    {
        var gates = new List<LmaxReadOnlyRuntimeSafetyGateResult>
        {
            Gate("CredentialProfileBoundaryPresent", true, "true", "true - credential profile boundary exists."),
            Gate("CredentialResolverDisabled", false, "false until a separate credential resolver gate", "true - disabled resolver is the only implementation."),
            Gate("CredentialReadImplemented", true, "false in Phase 4H", "false - no credential value read implementation exists."),
            Gate("CredentialUseImplemented", true, "false in Phase 4H", "false - no credential value use implementation exists."),
            Gate("SensitiveMaterialReturned", true, "false", "false - no sensitive material is returned."),
            Gate("RedactionRequired", true, "true", "true - all future resolver results must be redacted."),
            Gate("CredentialProfileName", !string.IsNullOrWhiteSpace(request.CredentialProfileName), "non-empty profile label for future activation", string.IsNullOrWhiteSpace(request.CredentialProfileName) ? "missing" : "present as label only")
        };
        var failed = gates.Where(x => x.BlocksRun).Select(x => x.Name).ToList();
        var status = failed.Count > 0 ? LmaxReadOnlyRuntimeRunStatus.Blocked : LmaxReadOnlyRuntimeRunStatus.DryRun;
        var reason = "Blocked by credential profile safety gates: " + string.Join(", ", failed);
        return new LmaxReadOnlyCredentialProfileSafetyReport(
            status,
            reason,
            CredentialProfileBoundaryPresent: true,
            CredentialResolverDisabled: true,
            CredentialReadImplemented: false,
            CredentialUseImplemented: false,
            SensitiveMaterialReturned: false,
            RedactionRequired: true,
            LmaxReadOnlyCredentialProfileResolverMode.Disabled,
            gates);
    }

    private static LmaxReadOnlyCredentialProfileDescriptor CreateDescriptor(LmaxReadOnlyCredentialProfileRequest request)
        => new(
            string.IsNullOrWhiteSpace(request.CredentialProfileName) ? "(missing)" : request.CredentialProfileName,
            string.IsNullOrWhiteSpace(request.EnvironmentName) ? "Demo" : request.EnvironmentName,
            string.IsNullOrWhiteSpace(request.VenueProfileName) ? "LmaxDemoReadOnly" : request.VenueProfileName,
            IsConfigured: false,
            LmaxReadOnlyCredentialProfileSourceKind.None,
            LmaxReadOnlyCredentialProfileRedactionStatus.NotApplicable);

    private static LmaxReadOnlyRuntimeSafetyGateResult Gate(string name, bool passed, string expected, string observed)
        => new(name, passed ? LmaxReadOnlyRuntimeSafetyGateStatus.Passed : LmaxReadOnlyRuntimeSafetyGateStatus.Failed, observed, expected, observed);
}
