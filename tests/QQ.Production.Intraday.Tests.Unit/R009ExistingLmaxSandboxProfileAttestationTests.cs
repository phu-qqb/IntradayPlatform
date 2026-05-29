using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009ExistingLmaxSandboxProfileAttestationTests
{
    private readonly R009LmaxSandboxOrderPathSmokeGate _gate = new();

    [Fact]
    public void Operator_attested_demo_profile_is_accepted_without_literal_lmaxsandbox_section()
    {
        var credential = ReadyDemoEnvCredentialProfile();
        var classification = _gate.ClassifyOperatorAttestedExistingLmaxProfile(
            currentLmaxSetupOperatorAttestedSandbox: true,
            localConfigValues: DemoConnectivityLabValues(),
            credentialProfile: credential);

        Assert.Equal(R009SandboxOrderPathStatus.Ready, classification.Status);
        Assert.True(classification.Attestation.CurrentLmaxSetupOperatorAttestedSandbox);
        Assert.True(classification.ExistingLmaxProfileIsSandbox);
        Assert.Equal("ExistingLmaxDemoProfile", classification.BrokerVenue);
        Assert.Equal("OperatorAttestationAndDemoCredentialProfile", classification.Attestation.SandboxClassificationSource);
        Assert.True(classification.EndpointValuesRedacted);
        Assert.False(classification.ProductionEndpointDetected);
        Assert.False(classification.ProductionCredentialsDetected);
        Assert.Empty(classification.MissingNonSecretConfigurationNames);
    }

    [Fact]
    public void Demo_env_vars_are_presence_metadata_only()
    {
        var required = new[]
        {
            "LMAX_DEMO_FIX_USERNAME",
            "LMAX_DEMO_FIX_PASSWORD",
            "LMAX_DEMO_SENDER_COMP_ID",
            "LMAX_DEMO_TARGET_COMP_ID"
        };
        var validation = _gate.ValidateSandboxCredentialEnvironmentVariables(
            required,
            required.ToDictionary(x => x, _ => true, StringComparer.OrdinalIgnoreCase));

        Assert.Equal("LMAX_DEMO_ENV_VARS", validation.CredentialProfileName);
        Assert.Equal("EnvVars", validation.CredentialSourceType);
        Assert.True(validation.CredentialValuesRedacted);
        Assert.True(validation.SandboxCredentialPresent);
        Assert.False(validation.ProductionCredentialDetected);
        Assert.Empty(validation.MissingProfileNames);
    }

    [Fact]
    public void Production_labeled_endpoint_blocks_existing_profile_classification()
    {
        var values = DemoConnectivityLabValues();
        values["LmaxConnectivityLab:FixOrderHost"] = "fix-order.production.lmax.com";

        var classification = _gate.ClassifyOperatorAttestedExistingLmaxProfile(
            currentLmaxSetupOperatorAttestedSandbox: true,
            localConfigValues: values,
            credentialProfile: ReadyDemoEnvCredentialProfile());

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, classification.Status);
        Assert.True(classification.ProductionEndpointDetected);
        Assert.False(classification.ExistingLmaxProfileIsSandbox);
        Assert.Contains("ProductionEndpointDetected", classification.Reasons);
    }

    [Fact]
    public void Missing_non_secret_session_fields_block_before_submission()
    {
        var values = DemoConnectivityLabValues();
        values.Remove("LmaxConnectivityLab:FixOrderTargetCompId");

        var classification = _gate.ClassifyOperatorAttestedExistingLmaxProfile(
            currentLmaxSetupOperatorAttestedSandbox: true,
            localConfigValues: values,
            credentialProfile: ReadyDemoEnvCredentialProfile());

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, classification.Status);
        Assert.Contains("LmaxConnectivityLab:FixOrderTargetCompId", classification.MissingNonSecretConfigurationNames);
        Assert.Contains("MissingNonSecretConfig:LmaxConnectivityLab:FixOrderTargetCompId", classification.Reasons);
    }

    private static R009SandboxCredentialProfileValidation ReadyDemoEnvCredentialProfile()
    {
        var required = new[]
        {
            "LMAX_DEMO_FIX_USERNAME",
            "LMAX_DEMO_FIX_PASSWORD",
            "LMAX_DEMO_SENDER_COMP_ID",
            "LMAX_DEMO_TARGET_COMP_ID"
        };

        return new R009SandboxCredentialProfileValidation(
            Status: R009SandboxOrderPathStatus.Ready,
            CredentialProfileName: "LMAX_DEMO_ENV_VARS",
            CredentialSourceType: "EnvVars",
            CredentialValuesRedacted: true,
            ProductionCredentialDetected: false,
            SandboxCredentialPresent: true,
            CredentialVariablePresence: required.ToDictionary(x => x, _ => true, StringComparer.OrdinalIgnoreCase),
            MissingProfileNames: Array.Empty<string>(),
            Reasons: Array.Empty<string>());
    }

    private static Dictionary<string, string?> DemoConnectivityLabValues() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["LmaxConnectivityLab:EnvironmentName"] = "Demo",
            ["LmaxConnectivityLab:FixOrderHost"] = "fix-order.london-demo.lmax.com",
            ["LmaxConnectivityLab:FixOrderPort"] = "443",
            ["LmaxConnectivityLab:FixOrderTargetCompId"] = "LMXBD",
            ["LmaxConnectivityLab:AccountCode"] = "LMAX_DEMO_LOCAL"
        };
}
