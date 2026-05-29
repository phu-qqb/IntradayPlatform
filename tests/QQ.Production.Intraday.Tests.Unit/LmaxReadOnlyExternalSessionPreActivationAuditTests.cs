using System.Reflection;
using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyExternalSessionPreActivationAuditTests
{
    [Fact]
    public void Complete_pre_activation_audit_metadata_is_not_executable()
    {
        var result = LmaxReadOnlyExternalSessionPreActivationAuditValidator.Validate(CompleteEnvelope());

        Assert.Equal(LmaxReadOnlyExternalSessionPreActivationAuditStatus.NotExecutable, result.Status);
        Assert.Equal(LmaxReadOnlyExternalSessionPreActivationAuditOutcome.NotExecutable, result.FinalOutcome);
        Assert.False(result.CanAuthorizeExecution);
        Assert.True(result.ExecutionStillBlocked);
        Assert.False(result.SessionStarted);
        Assert.False(result.ExternalConnectionAttempted);
        Assert.False(result.CredentialReadAttempted);
        Assert.False(result.ShadowReplaySubmitAttempted);
        Assert.False(result.TradingMutationAttempted);
        Assert.True(result.NoSensitiveContent);
        Assert.Contains(result.SafetyGates, x => x.Name == "Phase4ExternalRunImplementationNotStarted" && x.BlocksRun);
        Assert.Contains(result.SafetyGates, x => x.Name == "CredentialResolverDisabled" && x.BlocksRun);
        Assert.Contains(result.SafetyGates, x => x.Name == "GuardedTransportImplementationDisabled" && x.BlocksRun);
    }

    [Theory]
    [InlineData("missing reason", "ReasonRequired")]
    [InlineData("missing intent", "IntentSummaryRequired")]
    [InlineData("missing dry-run report", "DryRunReportSummaryRequired")]
    [InlineData("missing signoff", "SignoffSummaryRequired")]
    [InlineData("missing blocker", "CredentialResolverDisabled")]
    [InlineData("dry-run can start", "DryRunReportMustRemainBlocked")]
    [InlineData("signoff can authorize", "SignoffCannotAuthorizeExecution")]
    [InlineData("execution unblocked", "SignoffExecutionStillBlockedRequired")]
    [InlineData("session started", "SessionStartedMustRemainFalse")]
    [InlineData("external connection attempted", "ExternalConnectionAttemptedMustRemainFalse")]
    [InlineData("credential read attempted", "CredentialReadAttemptedMustRemainFalse")]
    [InlineData("shadow replay submit attempted", "ShadowReplaySubmitAttemptedMustRemainFalse")]
    [InlineData("trading mutation attempted", "TradingMutationAttemptedMustRemainFalse")]
    public void Invalid_pre_activation_audit_inputs_are_rejected(string condition, string expectedCode)
    {
        var envelope = condition switch
        {
            "missing reason" => CompleteEnvelope(reason: ""),
            "missing intent" => CompleteEnvelope(intentId: Guid.Empty),
            "missing dry-run report" => CompleteEnvelope(dryRunReportId: Guid.Empty),
            "missing signoff" => CompleteEnvelope(signoffId: Guid.Empty),
            "missing blocker" => CompleteEnvelope(stableBlockers: ["Phase4ExternalRunImplementationNotStarted", "GuardedTransportImplementationDisabled"]),
            "dry-run can start" => CompleteEnvelope(dryRunCanStart: true),
            "signoff can authorize" => CompleteEnvelope(signoffCanAuthorize: true),
            "execution unblocked" => CompleteEnvelope(signoffExecutionStillBlocked: false),
            "session started" => CompleteEnvelope(sessionStarted: true),
            "external connection attempted" => CompleteEnvelope(externalConnectionAttempted: true),
            "credential read attempted" => CompleteEnvelope(credentialReadAttempted: true),
            "shadow replay submit attempted" => CompleteEnvelope(shadowReplaySubmitAttempted: true),
            "trading mutation attempted" => CompleteEnvelope(tradingMutationAttempted: true),
            _ => CompleteEnvelope()
        };

        var result = LmaxReadOnlyExternalSessionPreActivationAuditValidator.Validate(envelope);

        Assert.Equal(LmaxReadOnlyExternalSessionPreActivationAuditStatus.Invalid, result.Status);
        Assert.False(result.CanAuthorizeExecution);
        Assert.True(result.ExecutionStillBlocked);
        Assert.Contains(result.ValidationIssues, x => x.Code == expectedCode);
    }

    [Fact]
    public void Pre_activation_audit_does_not_expose_forbidden_fields_or_sensitive_json()
    {
        var envelopeProperties = typeof(LmaxReadOnlyExternalSessionPreActivationAuditEnvelope)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(x => x.Name)
            .ToList();
        var resultProperties = typeof(LmaxReadOnlyExternalSessionPreActivationAuditResult)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(x => x.Name)
            .ToList();
        var names = envelopeProperties.Concat(resultProperties).ToList();

        foreach (var forbidden in new[] { "Host", "Port", "Username", "Password", "Secret", "Token", "ApiKey", "PrivateKey", "Account", "SenderComp", "TargetComp", "EndpointUrl", "RawFix", "NewOrder", "Cancel", "Replace", "SubmitOrder" })
        {
            Assert.DoesNotContain(names, x => x.Contains(forbidden, StringComparison.OrdinalIgnoreCase)
                                               && !x.Contains("Report", StringComparison.OrdinalIgnoreCase));
        }

        var json = JsonSerializer.Serialize(LmaxReadOnlyExternalSessionPreActivationAuditValidator.Validate(CompleteEnvelope()));
        foreach (var forbidden in new[] { "password", "secretValue", "secretMaterial", "token", "apiKey", "privateKey", "authorization", "554=", "endpointUrl", "rawFixText", "Connected=True", "OrderSent=True" })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static LmaxReadOnlyExternalSessionPreActivationAuditEnvelope CompleteEnvelope(
        string reason = "Pre-activation audit validation only",
        Guid? intentId = null,
        Guid? dryRunReportId = null,
        Guid? signoffId = null,
        bool dryRunCanStart = false,
        bool signoffCanAuthorize = false,
        bool signoffExecutionStillBlocked = true,
        bool sessionStarted = false,
        bool externalConnectionAttempted = false,
        bool credentialReadAttempted = false,
        bool shadowReplaySubmitAttempted = false,
        bool tradingMutationAttempted = false,
        IReadOnlyList<string>? stableBlockers = null)
        => new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "requesting-operator",
            "reviewing-operator",
            "signing-operator",
            reason,
            intentId ?? Guid.NewGuid(),
            dryRunReportId ?? Guid.NewGuid(),
            signoffId ?? Guid.NewGuid(),
            dryRunCanStart,
            signoffCanAuthorize,
            signoffExecutionStillBlocked,
            sessionStarted,
            externalConnectionAttempted,
            credentialReadAttempted,
            shadowReplaySubmitAttempted,
            tradingMutationAttempted,
            stableBlockers ?? [
                "Phase4ExternalRunImplementationNotStarted",
                "CredentialResolverDisabled",
                "GuardedTransportImplementationDisabled"
            ],
            DryRunReportReviewed: true,
            SignoffReviewed: true);
}
