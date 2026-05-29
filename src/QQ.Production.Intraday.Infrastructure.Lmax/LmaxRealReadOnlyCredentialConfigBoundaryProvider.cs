namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxReadOnlyCredentialConfigOptions(
    string EnvironmentLabel,
    bool DemoReadOnly,
    string SanitizedConfigSourceLabel,
    bool ExternalCredentialAccessApproved = false);

public interface ILmaxReadOnlyCredentialConfigClient
{
    LmaxRealReadOnlySecretAccessResult AccessDemoReadOnlyConfig(
        LmaxReadOnlyCredentialConfigOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialAccessPolicy policy,
        CancellationToken cancellationToken = default);
}

public sealed class LmaxRealReadOnlyCredentialConfigBoundaryProvider : ILmaxRealReadOnlyCredentialConfigBoundaryProvider
{
    private readonly LmaxReadOnlyCredentialConfigOptions options;
    private readonly ILmaxReadOnlyCredentialConfigClient configClient;

    public LmaxRealReadOnlyCredentialConfigBoundaryProvider(
        LmaxReadOnlyCredentialConfigOptions options,
        ILmaxReadOnlyCredentialConfigClient configClient)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.configClient = configClient ?? throw new ArgumentNullException(nameof(configClient));
    }

    public LmaxRealReadOnlySecretAccessResult AccessDemoReadOnlyConfig(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialAccessPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();

        var optionIssue = ValidateOptions(options);
        if (optionIssue is not null)
        {
            return optionIssue;
        }

        var scopeIssues = LmaxExecutableReadOnlyCredentialBoundary.ValidateScope(scope);
        if (scopeIssues.Count > 0)
        {
            return Blocked(
                "CredentialConfigBoundaryProviderBlockedBeforeSecretUse",
                "SafetyConstraintFailed",
                string.Join("; ", scopeIssues.Select(x => x.Code)));
        }

        var policyIssue = ValidatePolicy(policy);
        if (policyIssue is not null)
        {
            return policyIssue;
        }

        if (!options.ExternalCredentialAccessApproved || !policy.RealSecretMaterialAllowedNow)
        {
            return new LmaxRealReadOnlySecretAccessResult(
                AccessAllowed: true,
                RealSecretMaterialLoaded: false,
                SensitiveMaterialReturned: false,
                SensitiveMaterialPrinted: false,
                SensitiveMaterialStored: false,
                "CredentialConfigBoundaryAcceptedNoSecretMaterialLoaded",
                "CredentialAccessNotExecutedInCurrentPhase",
                "Credential/config provider is present, but real secret access requires a future explicitly approved phase.");
        }

        return Sanitize(configClient.AccessDemoReadOnlyConfig(options, scope, policy, cancellationToken));
    }

    private static LmaxRealReadOnlySecretAccessResult? ValidateOptions(
        LmaxReadOnlyCredentialConfigOptions options)
    {
        if (!string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(
                "CredentialConfigBoundaryProviderConfigRejected",
                "NonDemoEnvironment",
                "Credential/config provider environment must be Demo/read-only.");
        }

        if (!options.DemoReadOnly)
        {
            return Blocked(
                "CredentialConfigBoundaryProviderConfigRejected",
                "ReadOnlyFlagMissing",
                "Credential/config provider must be marked Demo/read-only.");
        }

        if (string.IsNullOrWhiteSpace(options.SanitizedConfigSourceLabel) ||
            options.SanitizedConfigSourceLabel.Contains("://", StringComparison.Ordinal) ||
            options.SanitizedConfigSourceLabel.Contains('@', StringComparison.Ordinal) ||
            options.SanitizedConfigSourceLabel.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            options.SanitizedConfigSourceLabel.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            options.SanitizedConfigSourceLabel.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
            options.SanitizedConfigSourceLabel.Contains("554=", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(
                "CredentialConfigBoundaryProviderConfigRejected",
                "UnsafeConfigSourceLabel",
                "Credential/config source label must be sanitized.");
        }

        return null;
    }

    private static LmaxRealReadOnlySecretAccessResult? ValidatePolicy(
        LmaxReadOnlyCredentialAccessPolicy policy)
    {
        if (!policy.FutureApprovedRuntimeAttemptRequired ||
            !policy.RedactSensitiveFields ||
            !string.Equals(policy.Environment, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(
                "CredentialConfigBoundaryProviderPolicyRejected",
                "CredentialPolicyNotSafe",
                "Credential/config policy must remain Demo/read-only, explicitly approved, and redacted.");
        }

        return null;
    }

    private static LmaxRealReadOnlySecretAccessResult Sanitize(LmaxRealReadOnlySecretAccessResult result)
        => new(
            result.AccessAllowed,
            result.RealSecretMaterialLoaded,
            result.SensitiveMaterialReturned,
            result.SensitiveMaterialPrinted,
            result.SensitiveMaterialStored,
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedStatus) ?? "CredentialConfigBoundaryProviderAccessSanitized",
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedErrorCategory),
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedErrorMessage));

    private static LmaxRealReadOnlySecretAccessResult Blocked(string status, string category, string message)
        => new(
            AccessAllowed: false,
            RealSecretMaterialLoaded: false,
            SensitiveMaterialReturned: false,
            SensitiveMaterialPrinted: false,
            SensitiveMaterialStored: false,
            status,
            category,
            LmaxRealReadOnlyCredentialDependency.Sanitize(message));
}
