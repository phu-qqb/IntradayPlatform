using System.Reflection;
using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyExternalSessionSignoffTests
{
    [Fact]
    public void Complete_signoff_metadata_is_not_executable()
    {
        var result = LmaxReadOnlyExternalSessionSignoffValidator.Validate(CompleteEnvelope());

        Assert.Equal(LmaxReadOnlyExternalSessionSignoffStatus.NotExecutable, result.Status);
        Assert.Equal(LmaxReadOnlyExternalSessionSignoffDecision.Signed, result.Decision);
        Assert.False(result.CanAuthorizeExecution);
        Assert.True(result.ExecutionStillBlocked);
        Assert.False(result.SessionStarted);
        Assert.False(result.ExternalConnectionAttempted);
        Assert.False(result.CredentialReadAttempted);
        Assert.False(result.ShadowReplaySubmitAttempted);
        Assert.False(result.TradingMutationAttempted);
        Assert.Contains(result.SafetyGates, x => x.Name == "Phase4ExternalRunImplementationNotStarted" && x.BlocksRun);
        Assert.Contains(result.SafetyGates, x => x.Name == "CredentialResolverDisabled" && x.BlocksRun);
        Assert.Contains(result.SafetyGates, x => x.Name == "GuardedTransportImplementationDisabled" && x.BlocksRun);
    }

    [Theory]
    [InlineData("missing reason", "ReasonRequired")]
    [InlineData("missing signer", "SignedByOperatorIdRequired")]
    [InlineData("missing dry-run summary", "IntentOrDryRunReportRequired")]
    [InlineData("missing attestation", "ConfirmsNoOrderSubmissionRequired")]
    public void Invalid_signoff_inputs_are_rejected(string condition, string expectedCode)
    {
        var envelope = condition switch
        {
            "missing reason" => CompleteEnvelope(reason: ""),
            "missing signer" => CompleteEnvelope(signedBy: ""),
            "missing dry-run summary" => CompleteEnvelope(intentId: Guid.Empty, dryRunReportId: Guid.Empty),
            "missing attestation" => CompleteEnvelope(confirmsNoOrderSubmission: false),
            _ => CompleteEnvelope()
        };

        var result = LmaxReadOnlyExternalSessionSignoffValidator.Validate(envelope);

        Assert.Equal(LmaxReadOnlyExternalSessionSignoffStatus.Invalid, result.Status);
        Assert.False(result.CanAuthorizeExecution);
        Assert.Contains(result.ValidationIssues, x => x.Code == expectedCode);
    }

    [Fact]
    public void Risk_self_signoff_is_blocked()
    {
        var result = LmaxReadOnlyExternalSessionSignoffValidator.Validate(CompleteEnvelope(
            requestedBy: "same-operator",
            signedBy: "same-operator",
            role: LmaxReadOnlyExternalSessionSignoffRole.Risk));

        Assert.Equal(LmaxReadOnlyExternalSessionSignoffStatus.Invalid, result.Status);
        Assert.Contains(result.ValidationIssues, x => x.Code == "MakerCheckerSelfSignoffBlocked");
        Assert.False(result.CanAuthorizeExecution);
    }

    [Fact]
    public void Signoff_does_not_expose_forbidden_fields_or_sensitive_json()
    {
        var envelopeProperties = typeof(LmaxReadOnlyExternalSessionSignoffEnvelope)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(x => x.Name)
            .ToList();
        var resultProperties = typeof(LmaxReadOnlyExternalSessionSignoffResult)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(x => x.Name)
            .ToList();
        var names = envelopeProperties.Concat(resultProperties).ToList();

        foreach (var forbidden in new[] { "Host", "Port", "Username", "Password", "Secret", "Token", "ApiKey", "PrivateKey", "Account", "SenderComp", "TargetComp", "EndpointUrl", "RawFix", "NewOrder", "Cancel", "Replace", "SubmitOrder" })
        {
            Assert.DoesNotContain(names, x => x.Contains(forbidden, StringComparison.OrdinalIgnoreCase)
                                               && !x.Contains("Report", StringComparison.OrdinalIgnoreCase));
        }

        var json = JsonSerializer.Serialize(LmaxReadOnlyExternalSessionSignoffValidator.Validate(CompleteEnvelope()));
        foreach (var forbidden in new[] { "password", "secretValue", "secretMaterial", "token", "apiKey", "privateKey", "authorization", "554=", "endpointUrl", "rawFixText", "Connected=True", "OrderSent=True" })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static LmaxReadOnlyExternalSessionSignoffEnvelope CompleteEnvelope(
        string reason = "Signoff metadata validation only",
        string requestedBy = "requesting-operator",
        string signedBy = "signing-operator",
        Guid? dryRunReportId = null,
        Guid? intentId = null,
        bool confirmsNoOrderSubmission = true,
        LmaxReadOnlyExternalSessionSignoffRole role = LmaxReadOnlyExternalSessionSignoffRole.Approver)
        => new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            dryRunReportId ?? Guid.NewGuid(),
            intentId ?? Guid.NewGuid(),
            requestedBy,
            signedBy,
            role,
            reason,
            ConfirmsReadOnlyIntent: true,
            ConfirmsNoOrderSubmission: confirmsNoOrderSubmission,
            ConfirmsNoTradingMutation: true,
            ConfirmsNoScheduler: true,
            ConfirmsNoShadowReplaySubmit: true,
            ConfirmsNoCredentialExposure: true,
            ConfirmsDemoOnly: true,
            ConfirmsDryRunReportReviewed: true,
            DryRunReportCanStartSession: false,
            [
                "Phase4ExternalRunImplementationNotStarted",
                "CredentialResolverDisabled",
                "GuardedTransportImplementationDisabled",
                "ExternalSessionImplementationStarted"
            ],
            LmaxReadOnlyExternalSessionSignoffDecision.Signed);
}
