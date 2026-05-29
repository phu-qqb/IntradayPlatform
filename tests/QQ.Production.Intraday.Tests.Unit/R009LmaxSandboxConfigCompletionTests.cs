using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009LmaxSandboxConfigCompletionTests
{
    private readonly R009LmaxSandboxOrderPathSmokeGate _gate = new();

    [Fact]
    public void Missing_sandbox_config_contract_blocks_smoke_order()
    {
        var validation = _gate.ValidateSandboxConfigContract(
            R009LmaxSandboxConfigContract.Missing,
            credentialProfileName: "",
            credentialSourceType: "",
            sandboxCredentialPresent: false);

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, validation.Status);
        Assert.False(validation.SafeForOneBoundedSandboxOrder);
        Assert.Contains("ExplicitSandboxConfigRequired", validation.Reasons);
        Assert.Contains("SandboxCredentialProfileMissing", validation.Reasons);
    }

    [Fact]
    public void Sandbox_credential_profile_validation_redacts_values()
    {
        var validation = _gate.ValidateCredentialProfile(
            credentialProfileName: "lmax-sandbox-smoke-profile",
            credentialSourceType: "UserSecrets",
            sandboxCredentialPresent: true);

        Assert.Equal(R009SandboxOrderPathStatus.Ready, validation.Status);
        Assert.True(validation.CredentialValuesRedacted);
        Assert.True(validation.SandboxCredentialPresent);
        Assert.False(validation.ProductionCredentialDetected);
        Assert.Empty(validation.MissingProfileNames);
    }

    [Fact]
    public void Sandbox_env_var_credentials_are_accepted_as_redacted_presence_metadata()
    {
        var required = new[] { "LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID" };
        var presence = required.ToDictionary(x => x, _ => true, StringComparer.OrdinalIgnoreCase);

        var validation = _gate.ValidateSandboxCredentialEnvironmentVariables(required, presence);

        Assert.Equal(R009SandboxOrderPathStatus.Ready, validation.Status);
        Assert.Equal("EnvVars", validation.CredentialSourceType);
        Assert.True(validation.CredentialValuesRedacted);
        Assert.True(validation.SandboxCredentialPresent);
        Assert.False(validation.ProductionCredentialDetected);
        Assert.All(required, name => Assert.True(validation.CredentialVariablePresence[name]));
        Assert.Empty(validation.MissingProfileNames);
    }

    [Fact]
    public void Sandbox_env_var_validation_reports_missing_variable_names_not_values()
    {
        var required = new[] { "LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD" };
        var presence = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["LMAX_DEMO_FIX_USERNAME"] = true,
            ["LMAX_DEMO_FIX_PASSWORD"] = false
        };

        var validation = _gate.ValidateSandboxCredentialEnvironmentVariables(required, presence);

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, validation.Status);
        Assert.True(validation.CredentialValuesRedacted);
        Assert.Contains("LMAX_DEMO_FIX_PASSWORD", validation.MissingProfileNames);
        Assert.Contains("MissingCredentialVariable:LMAX_DEMO_FIX_PASSWORD", validation.Reasons);
    }

    [Fact]
    public void Production_credential_profile_is_rejected()
    {
        var validation = _gate.ValidateCredentialProfile(
            credentialProfileName: "lmax-production-live-profile",
            credentialSourceType: "UserSecrets",
            sandboxCredentialPresent: true);

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, validation.Status);
        Assert.True(validation.CredentialValuesRedacted);
        Assert.True(validation.ProductionCredentialDetected);
        Assert.Contains("ProductionCredentialDetected", validation.Reasons);
    }

    [Fact]
    public void Complete_sandbox_contract_is_ready_for_one_bounded_order()
    {
        var validation = _gate.ValidateSandboxConfigContract(
            CompleteContract(),
            credentialProfileName: "lmax-sandbox-smoke-profile",
            credentialSourceType: "EnvVars",
            sandboxCredentialPresent: true);

        Assert.Equal(R009SandboxOrderPathStatus.Ready, validation.Status);
        Assert.True(validation.ExplicitSandboxConfig);
        Assert.True(validation.ProductionRouteBlocked);
        Assert.True(validation.ProductionLedgerBlocked);
        Assert.True(validation.SafeForOneBoundedSandboxOrder);
    }

    [Fact]
    public void Contract_requires_open_sandbox_kill_switch()
    {
        var validation = _gate.ValidateSandboxConfigContract(
            CompleteContract(killSwitchOpen: false),
            credentialProfileName: "lmax-sandbox-smoke-profile",
            credentialSourceType: "EnvVars",
            sandboxCredentialPresent: true);

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, validation.Status);
        Assert.Contains("SandboxKillSwitchMustBeOpen", validation.Reasons);
    }

    [Fact]
    public void Contract_enforces_single_order_and_tiny_notional()
    {
        var countValidation = _gate.ValidateSandboxConfigContract(
            CompleteContract(maxOrderCount: 2),
            credentialProfileName: "lmax-sandbox-smoke-profile",
            credentialSourceType: "EnvVars",
            sandboxCredentialPresent: true);
        var notionalValidation = _gate.ValidateSandboxConfigContract(
            CompleteContract(maxNotional: 250m),
            credentialProfileName: "lmax-sandbox-smoke-profile",
            credentialSourceType: "EnvVars",
            sandboxCredentialPresent: true);

        Assert.Contains("MaxSandboxOrderCountMustEqualOne", countValidation.Reasons);
        Assert.Contains("MaxSandboxNotionalMustBeTinyAndPositive", notionalValidation.Reasons);
    }

    [Fact]
    public void Contract_rejects_direct_cross_nonmajor_and_ledger_state_permissions()
    {
        var validation = _gate.ValidateSandboxConfigContract(
            CompleteContract(
                directCrossAllowed: true,
                nonmajorAllowed: true,
                paperLedgerCommitAllowed: true,
                productionLedgerCommitAllowed: true,
                stateMutationAllowed: true),
            credentialProfileName: "lmax-sandbox-smoke-profile",
            credentialSourceType: "EnvVars",
            sandboxCredentialPresent: true);

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, validation.Status);
        Assert.Contains("DirectCrossExecutionAllowedMustBeFalse", validation.Reasons);
        Assert.Contains("NonmajorExecutionAllowedMustBeFalse", validation.Reasons);
        Assert.Contains("PaperLedgerCommitAllowedMustBeFalse", validation.Reasons);
        Assert.Contains("ProductionLedgerCommitAllowedMustBeFalse", validation.Reasons);
        Assert.Contains("StateMutationAllowedMustBeFalse", validation.Reasons);
    }

    [Fact]
    public void Sandbox_submission_is_not_attempted_when_config_validation_blocks()
    {
        var discovery = _gate.DiscoverSandboxConfig(new Dictionary<string, string?>());
        var result = _gate.TryCreateSandboxOrderIntent(ValidIntent(), discovery, R009SandboxGuardrailContract.Default, requestedOrderCount: 1);

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, result.RiskCheck.Status);
        Assert.Null(result.OrderIntent);
        Assert.Null(result.Route);
        Assert.Equal(R009SandboxSubmissionStatus.NotSubmittedBlocked, result.Submission.Status);
        Assert.Equal(0, result.Submission.SubmittedOrderCount);
        Assert.False(result.Reconciliation.ProductionLedgerMutation);
        Assert.False(result.Reconciliation.ProductionStateMutation);
    }

    private static R009LmaxSandboxConfigContract CompleteContract(
        bool killSwitchOpen = true,
        int maxOrderCount = 1,
        decimal maxNotional = 10m,
        bool directCrossAllowed = false,
        bool nonmajorAllowed = false,
        bool paperLedgerCommitAllowed = false,
        bool productionLedgerCommitAllowed = false,
        bool stateMutationAllowed = false)
    {
        return new R009LmaxSandboxConfigContract(
            Environment: "Sandbox",
            BrokerVenue: "LMAXSandbox",
            ProductionVenueAllowed: false,
            ProductionCredentialsAllowed: false,
            SandboxCredentialsRequired: true,
            SandboxOrderSubmissionEnabled: true,
            SandboxKillSwitchOpen: killSwitchOpen,
            MaxSandboxOrderCount: maxOrderCount,
            MaxSandboxNotional: maxNotional,
            AllowedSymbols: R009LmaxSandboxOrderPathSmokeGate.WhitelistedSymbols,
            DirectCrossExecutionAllowed: directCrossAllowed,
            NonmajorExecutionAllowed: nonmajorAllowed,
            PaperLedgerCommitAllowed: paperLedgerCommitAllowed,
            ProductionLedgerCommitAllowed: productionLedgerCommitAllowed,
            StateMutationAllowed: stateMutationAllowed);
    }

    private static R009SandboxExecutionIntent ValidIntent()
    {
        return new R009SandboxExecutionIntent(
            ExecutionIntentId: "r002-eurusd-intent",
            SourceDecisionPreviewId: "r002-r009-decision",
            Symbol: "EURUSD",
            ExecutionTradableSymbol: "EURUSD",
            NormalizedPortfolioSymbol: "EURUSD",
            RequiresInversion: false,
            SecurityID: null,
            SecurityIDSource: null,
            Side: "Buy",
            TargetQuantity: 1m,
            TargetNotional: 10m,
            CanonicalTargetCloseUtc: new DateTimeOffset(2026, 5, 26, 15, 15, 0, TimeSpan.Zero),
            BarRole: "IntradayRebalance",
            ReadinessPresent: true,
            ReadinessWaivedForSandboxSmokeTest: false,
            OperatorSandboxApproval: true,
            KillSwitchOpenForSandboxOnly: true,
            R009DecisionStatus: "PreviewReady",
            SandboxOnly: true,
            ProductionOrder: false,
            IsLiveProduction: false,
            NoProductionLedgerCommit: true);
    }
}
