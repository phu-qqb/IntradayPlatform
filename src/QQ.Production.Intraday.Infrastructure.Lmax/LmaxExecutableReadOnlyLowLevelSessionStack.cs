namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxReadOnlyCredentialAccessPolicy(
    bool FutureApprovedRuntimeAttemptRequired = true,
    bool RealSecretMaterialAllowedNow = false,
    bool RedactSensitiveFields = true,
    string Environment = "Demo/read-only");

public sealed record LmaxReadOnlyCredentialSanitizationRecord(
    bool AccessPolicyAccepted,
    bool RealSecretMaterialLoaded,
    bool SensitiveMaterialReturned,
    bool SensitiveMaterialPrinted,
    bool SensitiveMaterialStored,
    string SanitizedStatus,
    string? SanitizedErrorCategory = null,
    string? SanitizedErrorMessage = null);

public interface ILmaxReadOnlyCredentialBoundary
{
    LmaxReadOnlyCredentialSanitizationRecord ValidatePolicy(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialAccessPolicy policy);
}

public interface ILmaxReadOnlySocketBoundaryTransport
{
    LmaxReadOnlyBoundaryStepResult OpenTcp(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);

    LmaxReadOnlyBoundaryStepResult OpenTls(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);

    bool ShutdownRevert();
}

public interface ILmaxReadOnlyFixFrameBoundary
{
    LmaxReadOnlyBoundaryStepResult OpenLogon(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord credentialRecord,
        CancellationToken cancellationToken = default);
}

public interface ILmaxReadOnlyMarketDataFrameCodec
{
    LmaxReadOnlyMarketDataSessionClientResult ReadMarketData(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default);
}

public sealed class LmaxExecutableReadOnlyCredentialBoundary : ILmaxReadOnlyCredentialBoundary
{
    public LmaxReadOnlyCredentialSanitizationRecord ValidatePolicy(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialAccessPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(policy);

        var issues = ValidateScope(scope);
        if (issues.Count > 0)
        {
            return new LmaxReadOnlyCredentialSanitizationRecord(
                AccessPolicyAccepted: false,
                RealSecretMaterialLoaded: false,
                SensitiveMaterialReturned: false,
                SensitiveMaterialPrinted: false,
                SensitiveMaterialStored: false,
                "CredentialBoundaryBlockedBeforeSecretUse",
                "SafetyConstraintFailed",
                SanitizeText(string.Join("; ", issues.Select(x => x.Code))));
        }

        if (!policy.FutureApprovedRuntimeAttemptRequired ||
            policy.RealSecretMaterialAllowedNow ||
            !policy.RedactSensitiveFields ||
            !string.Equals(policy.Environment, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return new LmaxReadOnlyCredentialSanitizationRecord(
                AccessPolicyAccepted: false,
                RealSecretMaterialLoaded: false,
                SensitiveMaterialReturned: false,
                SensitiveMaterialPrinted: false,
                SensitiveMaterialStored: false,
                "CredentialBoundaryPolicyRejected",
                "CredentialPolicyNotSafeForR18",
                "Credential boundary policy must remain future-approved, Demo/read-only, redacted, and non-loading in R18.");
        }

        return new LmaxReadOnlyCredentialSanitizationRecord(
            AccessPolicyAccepted: true,
            RealSecretMaterialLoaded: false,
            SensitiveMaterialReturned: false,
            SensitiveMaterialPrinted: false,
            SensitiveMaterialStored: false,
            "CredentialBoundaryPolicyAcceptedNoSecretMaterialLoaded");
    }

    internal static IReadOnlyList<LmaxReadOnlyRuntimePreflightIssue> ValidateScope(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope)
    {
        var issues = new List<LmaxReadOnlyRuntimePreflightIssue>(
            LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(scope).Issues);

        foreach (var instrument in scope.Instruments)
        {
            var approved = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Find(instrument.Symbol);
            if (approved is null)
            {
                Add(issues, "NonApprovedInstrument", "$.instruments", $"Instrument '{instrument.Symbol}' is not approved for read-only low-level session use.");
                continue;
            }

            if (!string.Equals(approved.SecurityId, instrument.SecurityId, StringComparison.Ordinal) ||
                !string.Equals(approved.SecurityIdSource, instrument.SecurityIdSource, StringComparison.Ordinal))
            {
                Add(issues, "InstrumentSecurityIdMismatch", "$.instruments", $"Instrument '{instrument.Symbol}' does not match the approved SecurityID evidence.");
            }

            if (string.Equals(instrument.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(instrument.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal))
            {
                Add(issues, "UsdJpyCaveatMissing", "$.instruments[USDJPY].caveat", "USDJPY caveat must be preserved exactly.");
            }
        }

        return issues;
    }

    internal static string? SanitizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value
            .Replace("password", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("secret", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("credential", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("554=", "[redacted-fix-tag]", StringComparison.OrdinalIgnoreCase);
    }

    private static void Add(List<LmaxReadOnlyRuntimePreflightIssue> issues, string code, string path, string message)
        => issues.Add(new LmaxReadOnlyRuntimePreflightIssue(code, path, message));
}

public sealed class LmaxExecutableReadOnlySocketSessionBoundary : ILmaxReadOnlySocketSessionBoundary
{
    private readonly ILmaxReadOnlySocketBoundaryTransport transport;
    private readonly ILmaxReadOnlyCredentialBoundary credentialBoundary;
    private readonly LmaxReadOnlyCredentialAccessPolicy credentialPolicy;

    public LmaxExecutableReadOnlySocketSessionBoundary(
        ILmaxReadOnlySocketBoundaryTransport transport,
        ILmaxReadOnlyCredentialBoundary credentialBoundary,
        LmaxReadOnlyCredentialAccessPolicy? credentialPolicy = null)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.credentialBoundary = credentialBoundary ?? throw new ArgumentNullException(nameof(credentialBoundary));
        this.credentialPolicy = credentialPolicy ?? new LmaxReadOnlyCredentialAccessPolicy();
    }

    public LmaxReadOnlyBoundaryStepResult OpenTcpBoundary(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var blocked = ValidateBeforeTransportUse(scope);
        if (blocked is not null)
        {
            return blocked;
        }

        return Sanitize(transport.OpenTcp(scope, cancellationToken), "TcpBoundary");
    }

    public LmaxReadOnlyBoundaryStepResult OpenTlsBoundary(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var blocked = ValidateBeforeTransportUse(scope);
        if (blocked is not null)
        {
            return blocked;
        }

        return Sanitize(transport.OpenTls(scope, cancellationToken), "TlsBoundary");
    }

    public bool ShutdownRevert() => transport.ShutdownRevert();

    private LmaxReadOnlyBoundaryStepResult? ValidateBeforeTransportUse(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope)
    {
        var credential = credentialBoundary.ValidatePolicy(scope, credentialPolicy);
        if (credential.AccessPolicyAccepted)
        {
            return null;
        }

        return new LmaxReadOnlyBoundaryStepResult(
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            "ReadOnlySocketBoundaryBlockedBeforeTransportUse",
            credential.SanitizedErrorCategory ?? "CredentialBoundaryRejected",
            credential.SanitizedErrorMessage);
    }

    private static LmaxReadOnlyBoundaryStepResult Sanitize(
        LmaxReadOnlyBoundaryStepResult result,
        string fallbackCategory)
        => new(
            result.Status,
            LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.SanitizedStatus) ?? "ReadOnlyBoundaryStatusSanitized",
            LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.SanitizedErrorCategory) ?? fallbackCategory,
            LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.SanitizedErrorMessage));
}

public sealed class LmaxExecutableReadOnlyFixSessionBoundary : ILmaxReadOnlyFixSessionBoundary
{
    private readonly ILmaxReadOnlyFixFrameBoundary fixBoundary;
    private readonly ILmaxReadOnlyCredentialBoundary credentialBoundary;
    private readonly LmaxReadOnlyCredentialAccessPolicy credentialPolicy;

    public LmaxExecutableReadOnlyFixSessionBoundary(
        ILmaxReadOnlyFixFrameBoundary fixBoundary,
        ILmaxReadOnlyCredentialBoundary credentialBoundary,
        LmaxReadOnlyCredentialAccessPolicy? credentialPolicy = null)
    {
        this.fixBoundary = fixBoundary ?? throw new ArgumentNullException(nameof(fixBoundary));
        this.credentialBoundary = credentialBoundary ?? throw new ArgumentNullException(nameof(credentialBoundary));
        this.credentialPolicy = credentialPolicy ?? new LmaxReadOnlyCredentialAccessPolicy();
    }

    public LmaxReadOnlyBoundaryStepResult OpenFixLogonBoundary(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var credential = credentialBoundary.ValidatePolicy(scope, credentialPolicy);
        if (!credential.AccessPolicyAccepted)
        {
            return new LmaxReadOnlyBoundaryStepResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
                "ReadOnlyFixBoundaryBlockedBeforeFrameUse",
                credential.SanitizedErrorCategory ?? "CredentialBoundaryRejected",
                credential.SanitizedErrorMessage);
        }

        var result = fixBoundary.OpenLogon(scope, credential, cancellationToken);
        return new LmaxReadOnlyBoundaryStepResult(
            result.Status,
            LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.SanitizedStatus) ?? "ReadOnlyFixBoundaryStatusSanitized",
            LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.SanitizedErrorCategory) ?? "FixLogonBoundary",
            LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.SanitizedErrorMessage));
    }
}

public sealed class LmaxExecutableReadOnlyMarketDataRequestCodec : ILmaxReadOnlyMarketDataRequestCodec
{
    private readonly ILmaxReadOnlyMarketDataFrameCodec codec;

    public LmaxExecutableReadOnlyMarketDataRequestCodec(ILmaxReadOnlyMarketDataFrameCodec codec)
    {
        this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    public LmaxReadOnlyMarketDataSessionClientResult RequestReadOnlyMarketData(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var issues = LmaxExecutableReadOnlyCredentialBoundary.ValidateScope(scope);
        if (issues.Count > 0)
        {
            return new LmaxReadOnlyMarketDataSessionClientResult(
                [],
                "ReadOnlyMarketDataCodecBlockedBeforeFrameUse",
                "SafetyConstraintFailed",
                LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(string.Join("; ", issues.Select(x => x.Code))));
        }

        var result = codec.ReadMarketData(scope, cancellationToken);
        return new LmaxReadOnlyMarketDataSessionClientResult(
            SanitizeStatuses(scope, result.InstrumentStatuses),
            LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.SanitizedStatus) ?? "ReadOnlyMarketDataStatusSanitized",
            LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.SanitizedErrorCategory),
            LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.SanitizedErrorMessage))
        {
            MarketDataRequestWriteAttempted = result.MarketDataRequestWriteAttempted,
            MarketDataRequestWriteSucceeded = result.MarketDataRequestWriteSucceeded,
            MarketDataRequestResponseReadAttempted = result.MarketDataRequestResponseReadAttempted,
            MarketDataRequestReachedBoundedResponseClassification = result.MarketDataRequestReachedBoundedResponseClassification,
            MarketDataRejectSanitizedSubcategory = LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.MarketDataRejectSanitizedSubcategory),
            SessionRejectSanitizedSubcategory = LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.SessionRejectSanitizedSubcategory),
            RejectReasonExtractionSource = LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.RejectReasonExtractionSource),
            SessionRejectRefTagIdSanitizedCategory = LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.SessionRejectRefTagIdSanitizedCategory),
            SessionRejectReasonSanitizedCategory = LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.SessionRejectReasonSanitizedCategory),
            SessionRejectRefMsgTypeSanitizedCategory = LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.SessionRejectRefMsgTypeSanitizedCategory),
            MarketDataEntriesObserved = result.MarketDataEntriesObserved,
            MarketDataSanitizedEntryCount = result.MarketDataSanitizedEntryCount,
            MarketDataEntriesEvidenceCategory = LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.MarketDataEntriesEvidenceCategory),
            MarketDataEntriesReportingSource = LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.MarketDataEntriesReportingSource),
            MarketDataEntriesNotAvailableReason = LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.MarketDataEntriesNotAvailableReason),
            LogoutObserved = result.LogoutObserved,
            LogoutSourceCategory = LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.LogoutSourceCategory),
            LogoutReasonSanitizedCategory = LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.LogoutReasonSanitizedCategory),
            LogoutTextPresentSanitized = result.LogoutTextPresentSanitized,
            LogoutAfterInstrument = LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.LogoutAfterInstrument),
            LogoutAfterSecurityIdSanitized = LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.LogoutAfterSecurityIdSanitized),
            LogoutTimingCategory = LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.LogoutTimingCategory),
            LogoutReasonExtractionSource = LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(result.LogoutReasonExtractionSource)
        };
    }

    private static IReadOnlyList<LmaxTemporaryReadOnlyInstrumentMarketDataStatus> SanitizeStatuses(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        IReadOnlyList<LmaxTemporaryReadOnlyInstrumentMarketDataStatus> statuses)
    {
        var statusBySymbol = statuses.ToDictionary(x => x.Symbol, StringComparer.OrdinalIgnoreCase);

        return scope.Instruments.Select(instrument =>
        {
            if (!statusBySymbol.TryGetValue(instrument.Symbol, out var status))
            {
                return new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                    instrument.Symbol,
                    instrument.SecurityId,
                    instrument.SecurityIdSource,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.FakeFailed,
                    MarketDataSnapshotCount: 0,
                    MarketDataRequestRejectCount: 0,
                    BusinessMessageRejectCount: 0,
                    SessionRejectCount: 0,
                    "ReadOnlyMarketDataStatusMissingSanitized",
                    "InstrumentStatusMissing",
                    "Approved instrument did not return sanitized read-only market-data evidence.",
                    instrument.Caveat);
            }

            return new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                instrument.Symbol,
                instrument.SecurityId,
                instrument.SecurityIdSource,
                status.MarketDataBoundary,
                status.MarketDataSnapshotCount,
                status.MarketDataRequestRejectCount,
                status.BusinessMessageRejectCount,
                status.SessionRejectCount,
                LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(status.SanitizedStatus) ?? "ReadOnlyInstrumentStatusSanitized",
                LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(status.SanitizedErrorCategory),
                LmaxExecutableReadOnlyCredentialBoundary.SanitizeText(status.SanitizedErrorMessage),
                instrument.Caveat);
        }).ToList();
    }
}

public sealed class LmaxExecutableReadOnlySessionStackFactory
{
    private readonly ILmaxReadOnlySocketBoundaryTransport socketTransport;
    private readonly ILmaxReadOnlyFixFrameBoundary fixBoundary;
    private readonly ILmaxReadOnlyMarketDataFrameCodec marketDataCodec;
    private readonly ILmaxReadOnlyCredentialBoundary credentialBoundary;
    private readonly LmaxReadOnlyCredentialAccessPolicy credentialPolicy;

    public LmaxExecutableReadOnlySessionStackFactory(
        ILmaxReadOnlySocketBoundaryTransport socketTransport,
        ILmaxReadOnlyFixFrameBoundary fixBoundary,
        ILmaxReadOnlyMarketDataFrameCodec marketDataCodec,
        ILmaxReadOnlyCredentialBoundary credentialBoundary,
        LmaxReadOnlyCredentialAccessPolicy? credentialPolicy = null)
    {
        this.socketTransport = socketTransport ?? throw new ArgumentNullException(nameof(socketTransport));
        this.fixBoundary = fixBoundary ?? throw new ArgumentNullException(nameof(fixBoundary));
        this.marketDataCodec = marketDataCodec ?? throw new ArgumentNullException(nameof(marketDataCodec));
        this.credentialBoundary = credentialBoundary ?? throw new ArgumentNullException(nameof(credentialBoundary));
        this.credentialPolicy = credentialPolicy ?? new LmaxReadOnlyCredentialAccessPolicy();
    }

    public LmaxExecutableReadOnlyMarketDataSessionClient CreateSessionClient()
        => new(
            new LmaxExecutableReadOnlySocketSessionBoundary(socketTransport, credentialBoundary, credentialPolicy),
            new LmaxExecutableReadOnlyFixSessionBoundary(fixBoundary, credentialBoundary, credentialPolicy),
            new LmaxExecutableReadOnlyMarketDataRequestCodec(marketDataCodec));
}
