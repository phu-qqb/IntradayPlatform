using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyRuntimeActivationGateHarnessTests
{
    [Fact]
    public void Valid_dry_run_scope_passes_and_does_not_authorize_r8()
    {
        var result = LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(ValidRequest());

        Assert.True(result.Passed);
        Assert.True(result.PreflightGate.Passed);
        Assert.False(result.R8Authorized);
        Assert.False(result.ExternalRunExecuted);
        Assert.False(result.RuntimeActivationExecuted);
        Assert.False(result.CredentialLoadingAdded);
        Assert.Equal("LMAX_R7_LOCAL_RUNTIME_GATE_HARNESS_READY_NO_ACTIVATION", result.FinalDecision);
    }

    [Fact]
    public void R8_approval_phrase_template_is_recognized_as_template_only()
    {
        var validation = LmaxReadOnlyRuntimeActivationGateHarness.ValidateApprovalTemplate(
            LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate);

        Assert.True(validation.TemplatePresent);
        Assert.True(validation.TemplateMatchesExpected);
        Assert.False(validation.ActiveAuthorization);
    }

    [Theory]
    [InlineData("UsdJpyWithoutCaveat")]
    [InlineData("NonApprovedInstrument")]
    [InlineData("ProductionAccount")]
    [InlineData("OrdersEnabled")]
    [InlineData("LiveTradingEnabled")]
    [InlineData("SchedulerEnabled")]
    [InlineData("PollingEnabled")]
    [InlineData("ReplayEnabled")]
    [InlineData("ShadowReplayEnabled")]
    [InlineData("TradingMutationEnabled")]
    [InlineData("PersistentRuntimeEnablement")]
    [InlineData("DefaultGatewayRegistrationChange")]
    [InlineData("MissingShutdownRevertPlan")]
    [InlineData("SanitizationDisabled")]
    public void Unsafe_dry_run_scope_fails(string condition)
    {
        var request = condition switch
        {
            "UsdJpyWithoutCaveat" => ValidRequest(instruments: LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol == "USDJPY" ? x with { Caveat = null } : x).ToList()),
            "NonApprovedInstrument" => ValidRequest(instruments: [.. LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments, new("EURUSD", "4001", "8", "prior_workflow_closed", false, null)]),
            "ProductionAccount" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(ProductionAccountRequested: true)),
            "OrdersEnabled" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(AllowOrderSubmission: true)),
            "LiveTradingEnabled" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(AllowLiveTrading: true)),
            "SchedulerEnabled" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(SchedulerEnabled: true)),
            "PollingEnabled" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(PollingEnabled: true)),
            "ReplayEnabled" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(ReplayEnabled: true)),
            "ShadowReplayEnabled" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(ShadowReplayEnabled: true)),
            "TradingMutationEnabled" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(TradingMutationEnabled: true)),
            "PersistentRuntimeEnablement" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(PersistentRuntimeEnablementRequested: true)),
            "DefaultGatewayRegistrationChange" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(DefaultGatewayRegistrationChangeRequested: true)),
            "MissingShutdownRevertPlan" => ValidRequest(shutdownRevert: new LmaxReadOnlyRuntimeShutdownRevertRecord(false, true, true, "artifacts/readiness/lmax-runtime-enablement/missing.json")),
            "SanitizationDisabled" => ValidRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(OutputSanitizationEnabled: false)),
            _ => throw new InvalidOperationException(condition)
        };

        var result = LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(request);

        Assert.False(result.Passed);
        Assert.NotEmpty(result.PreflightGate.Issues);
        Assert.False(result.R8Authorized);
        Assert.False(result.ExternalRunExecuted);
        Assert.False(result.RuntimeActivationExecuted);
    }

    [Fact]
    public void Harness_source_has_no_network_or_credential_dependency()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxReadOnlyRuntimeActivationGateHarness.cs"));

        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Socket", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NetworkStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("QuickFix", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CredentialProfileResolver", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SessionPassword", source, StringComparison.Ordinal);
    }

    private static LmaxReadOnlyRuntimeActivationGateHarnessRequest ValidRequest(
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags = null,
        LmaxReadOnlyRuntimeShutdownRevertRecord? shutdownRevert = null)
        => new(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 12, 17, 00, 00, TimeSpan.Zero),
            LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate,
            instruments,
            safetyFlags,
            shutdownRevert);

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "QQ.Production.Intraday.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
