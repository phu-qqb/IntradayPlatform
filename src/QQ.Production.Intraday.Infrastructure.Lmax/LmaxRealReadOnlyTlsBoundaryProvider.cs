namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxReadOnlyTlsConnectionOptions(
    string EnvironmentLabel,
    string SanitizedEndpointLabel,
    string SanitizedTargetHostLabel,
    TimeSpan Timeout,
    bool DemoReadOnly,
    string CertificateValidationPolicyLabel,
    bool ExternalTlsHandshakeExecutionApproved = false)
{
    public static LmaxReadOnlyTlsConnectionOptions DemoReadOnlyDisabled(
        string sanitizedEndpointLabel,
        string sanitizedTargetHostLabel,
        TimeSpan? timeout = null,
        string certificateValidationPolicyLabel = "SystemDefaultValidation")
        => new(
            "Demo/read-only",
            sanitizedEndpointLabel,
            sanitizedTargetHostLabel,
            timeout ?? TimeSpan.FromSeconds(15),
            DemoReadOnly: true,
            certificateValidationPolicyLabel,
            ExternalTlsHandshakeExecutionApproved: false);
}

public interface ILmaxReadOnlyTlsHandshakeClient
{
    LmaxRealReadOnlyDependencyResult OpenTls(
        LmaxReadOnlyTlsConnectionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);

    bool ShutdownRevert();
}

public sealed class LmaxRealReadOnlyTlsBoundaryProvider : ILmaxRealReadOnlyTlsBoundaryProvider
{
    private readonly LmaxReadOnlyTlsConnectionOptions options;
    private readonly ILmaxReadOnlyTlsHandshakeClient handshakeClient;

    public LmaxRealReadOnlyTlsBoundaryProvider(
        LmaxReadOnlyTlsConnectionOptions options,
        ILmaxReadOnlyTlsHandshakeClient handshakeClient)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.handshakeClient = handshakeClient ?? throw new ArgumentNullException(nameof(handshakeClient));
    }

    public LmaxRealReadOnlyDependencyResult OpenTls(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
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
                "TlsBoundaryProviderBlockedBeforeHandshakeUse",
                "SafetyConstraintFailed",
                string.Join("; ", scopeIssues.Select(x => x.Code)));
        }

        if (!options.ExternalTlsHandshakeExecutionApproved)
        {
            return Blocked(
                "TlsBoundaryProviderExecutionNotApproved",
                "TlsExecutionNotApproved",
                "TLS provider requires a future explicitly approved phase before opening a TLS boundary.");
        }

        return Sanitize(handshakeClient.OpenTls(options, scope, cancellationToken), "TlsBoundary");
    }

    public bool ShutdownRevert() => handshakeClient.ShutdownRevert();

    private static LmaxRealReadOnlyDependencyResult? ValidateOptions(LmaxReadOnlyTlsConnectionOptions options)
    {
        if (!string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(
                "TlsBoundaryProviderConfigRejected",
                "NonDemoEnvironment",
                "TLS provider environment must be Demo/read-only.");
        }

        if (!options.DemoReadOnly)
        {
            return Blocked(
                "TlsBoundaryProviderConfigRejected",
                "ReadOnlyFlagMissing",
                "TLS provider must be marked Demo/read-only.");
        }

        if (IsUnsafeLabel(options.SanitizedEndpointLabel) ||
            IsUnsafeLabel(options.SanitizedTargetHostLabel))
        {
            return Blocked(
                "TlsBoundaryProviderConfigRejected",
                "UnsafeTlsEndpointOrServerName",
                "TLS provider endpoint and target host labels must be sanitized.");
        }

        if (options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromSeconds(60))
        {
            return Blocked(
                "TlsBoundaryProviderConfigRejected",
                "InvalidTimeout",
                "TLS provider timeout must be between zero and sixty seconds.");
        }

        if (!string.Equals(options.CertificateValidationPolicyLabel, "SystemDefaultValidation", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(options.CertificateValidationPolicyLabel, "PinnedPublicCertificateMetadataOnly", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(
                "TlsBoundaryProviderConfigRejected",
                "UnsafeCertificateValidationPolicy",
                "TLS certificate validation policy must be an approved sanitized label.");
        }

        return null;
    }

    private static bool IsUnsafeLabel(string label)
        => string.IsNullOrWhiteSpace(label) ||
           label.Contains("://", StringComparison.Ordinal) ||
           label.Contains('@', StringComparison.Ordinal) ||
           label.Contains("password", StringComparison.OrdinalIgnoreCase) ||
           label.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
           label.Contains("private", StringComparison.OrdinalIgnoreCase) ||
           label.Contains("-----BEGIN", StringComparison.OrdinalIgnoreCase);

    private static LmaxRealReadOnlyDependencyResult Sanitize(
        LmaxRealReadOnlyDependencyResult result,
        string fallbackCategory)
        => new(
            result.Status,
            SanitizeTlsMaterial(result.SanitizedStatus) ?? "TlsBoundaryProviderStatusSanitized",
            SanitizeTlsMaterial(result.SanitizedErrorCategory) ?? fallbackCategory,
            SanitizeTlsMaterial(result.SanitizedErrorMessage));

    private static LmaxRealReadOnlyDependencyResult Blocked(string status, string category, string message)
        => new(
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            status,
            category,
            SanitizeTlsMaterial(message));

    private static string? SanitizeTlsMaterial(string? value)
    {
        var sanitized = LmaxRealReadOnlyCredentialDependency.Sanitize(value);
        if (sanitized is null)
        {
            return null;
        }

        return sanitized
            .Replace("private", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("-----BEGIN", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("certificate", "[redacted]", StringComparison.OrdinalIgnoreCase);
    }
}
