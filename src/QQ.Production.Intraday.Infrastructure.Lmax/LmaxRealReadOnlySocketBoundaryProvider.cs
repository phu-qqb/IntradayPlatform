namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxReadOnlySocketConnectionOptions(
    string EnvironmentLabel,
    string SanitizedEndpointLabel,
    int Port,
    TimeSpan Timeout,
    bool DemoReadOnly,
    bool ExternalConnectionExecutionApproved = false)
{
    public static LmaxReadOnlySocketConnectionOptions DemoReadOnlyDisabled(
        string sanitizedEndpointLabel,
        int port,
        TimeSpan? timeout = null)
        => new(
            "Demo/read-only",
            sanitizedEndpointLabel,
            port,
            timeout ?? TimeSpan.FromSeconds(15),
            DemoReadOnly: true,
            ExternalConnectionExecutionApproved: false);
}

public interface ILmaxReadOnlySocketConnectionClient
{
    LmaxRealReadOnlyDependencyResult OpenTcp(
        LmaxReadOnlySocketConnectionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);

    bool ShutdownRevert();
}

public sealed class LmaxRealReadOnlySocketBoundaryProvider : ILmaxRealReadOnlySocketBoundaryProvider
{
    private readonly LmaxReadOnlySocketConnectionOptions options;
    private readonly ILmaxReadOnlySocketConnectionClient connectionClient;

    public LmaxRealReadOnlySocketBoundaryProvider(
        LmaxReadOnlySocketConnectionOptions options,
        ILmaxReadOnlySocketConnectionClient connectionClient)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.connectionClient = connectionClient ?? throw new ArgumentNullException(nameof(connectionClient));
    }

    public LmaxRealReadOnlyDependencyResult OpenTcp(
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
                "SocketBoundaryProviderBlockedBeforeTcpUse",
                "SafetyConstraintFailed",
                string.Join("; ", scopeIssues.Select(x => x.Code)));
        }

        if (!options.ExternalConnectionExecutionApproved)
        {
            return Blocked(
                "SocketBoundaryProviderExecutionNotApproved",
                "SocketExecutionNotApproved",
                "Socket provider requires a future explicitly approved phase before opening a TCP boundary.");
        }

        return Sanitize(connectionClient.OpenTcp(options, scope, cancellationToken), "TcpBoundary");
    }

    public bool ShutdownRevert() => connectionClient.ShutdownRevert();

    private static LmaxRealReadOnlyDependencyResult? ValidateOptions(LmaxReadOnlySocketConnectionOptions options)
    {
        if (!string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(
                "SocketBoundaryProviderConfigRejected",
                "NonDemoEnvironment",
                "Socket provider environment must be Demo/read-only.");
        }

        if (!options.DemoReadOnly)
        {
            return Blocked(
                "SocketBoundaryProviderConfigRejected",
                "ReadOnlyFlagMissing",
                "Socket provider must be marked Demo/read-only.");
        }

        if (string.IsNullOrWhiteSpace(options.SanitizedEndpointLabel) ||
            options.SanitizedEndpointLabel.Contains("://", StringComparison.Ordinal) ||
            options.SanitizedEndpointLabel.Contains('@', StringComparison.Ordinal) ||
            options.SanitizedEndpointLabel.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            options.SanitizedEndpointLabel.Contains("secret", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(
                "SocketBoundaryProviderConfigRejected",
                "UnsafeEndpointLabel",
                "Socket provider endpoint label must be sanitized.");
        }

        if (options.Port is <= 0 or > 65535)
        {
            return Blocked(
                "SocketBoundaryProviderConfigRejected",
                "InvalidPort",
                "Socket provider port must be a valid TCP port number.");
        }

        if (options.Timeout <= TimeSpan.Zero || options.Timeout > TimeSpan.FromSeconds(60))
        {
            return Blocked(
                "SocketBoundaryProviderConfigRejected",
                "InvalidTimeout",
                "Socket provider timeout must be between zero and sixty seconds.");
        }

        return null;
    }

    private static LmaxRealReadOnlyDependencyResult Sanitize(
        LmaxRealReadOnlyDependencyResult result,
        string fallbackCategory)
        => new(
            result.Status,
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedStatus) ?? "SocketBoundaryProviderStatusSanitized",
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedErrorCategory) ?? fallbackCategory,
            LmaxRealReadOnlyCredentialDependency.Sanitize(result.SanitizedErrorMessage));

    private static LmaxRealReadOnlyDependencyResult Blocked(string status, string category, string message)
        => new(
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            status,
            category,
            LmaxRealReadOnlyCredentialDependency.Sanitize(message));
}
