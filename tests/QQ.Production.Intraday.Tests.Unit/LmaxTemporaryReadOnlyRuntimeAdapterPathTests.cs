using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxTemporaryReadOnlyRuntimeAdapterPathTests
{
    [Fact]
    public void Dry_run_adapter_accepts_valid_r7_harness_output()
    {
        var adapter = new LmaxDryRunTemporaryReadOnlyRuntimeActivationAdapter();
        var request = ValidAdapterRequest();

        var result = adapter.ValidateAsync(request);

        Assert.True(result.Passed);
        Assert.Equal(LmaxTemporaryReadOnlyRuntimeActivationOutcome.DryRunAccepted, result.Outcome);
        Assert.True(result.HarnessOutputConsumed);
        Assert.True(result.HarnessPreflightPassed);
        Assert.True(result.DryRunOnly);
        Assert.True(result.FutureR10ApprovalRequired);
        Assert.True(result.ApprovedInstrumentsOnly);
        Assert.False(result.SafetySnapshot.ExternalRunExecuted);
        Assert.False(result.SafetySnapshot.RealSocketOpened);
        Assert.False(result.SafetySnapshot.CredentialsLoaded);
    }

    [Fact]
    public void Dry_run_adapter_preserves_approved_instruments_and_usdjpy_caveat()
    {
        var adapter = new LmaxDryRunTemporaryReadOnlyRuntimeActivationAdapter();

        var result = adapter.ValidateAsync(ValidAdapterRequest());

        Assert.Contains(result.InstrumentStatuses, x => x.Symbol == "GBPUSD" && x.SecurityId == "4002" && x.SecurityIdSource == "8");
        Assert.Contains(result.InstrumentStatuses, x => x.Symbol == "EURGBP" && x.SecurityId == "4003" && x.SecurityIdSource == "8");
        Assert.Contains(result.InstrumentStatuses, x => x.Symbol == "AUDUSD" && x.SecurityId == "4007" && x.SecurityIdSource == "8");
        var usdJpy = Assert.Single(result.InstrumentStatuses, x => x.Symbol == "USDJPY");
        Assert.Equal("4004", usdJpy.SecurityId);
        Assert.Equal("8", usdJpy.SecurityIdSource);
        Assert.Equal(LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, usdJpy.Caveat);
    }

    [Theory]
    [InlineData("MissingHarnessValidation")]
    [InlineData("NonApprovedInstrument")]
    [InlineData("UsdJpyWithoutCaveat")]
    [InlineData("ProductionAccount")]
    [InlineData("OrdersEnabled")]
    [InlineData("LiveTradingEnabled")]
    [InlineData("SchedulerEnabled")]
    [InlineData("PollingEnabled")]
    [InlineData("ReplayEnabled")]
    [InlineData("ShadowReplayEnabled")]
    [InlineData("TradingMutationEnabled")]
    [InlineData("PersistentRuntimeEnablement")]
    [InlineData("NonDryRunAdapterMode")]
    public void Dry_run_adapter_rejects_unsafe_or_non_harness_ready_requests(string condition)
    {
        var adapter = new LmaxDryRunTemporaryReadOnlyRuntimeActivationAdapter();
        var request = condition switch
        {
            "MissingHarnessValidation" => ValidAdapterRequest(approvalPhrase: "wrong template"),
            "NonApprovedInstrument" => ValidAdapterRequest(instruments: [.. LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments, new("EURUSD", "4001", "8", "prior_workflow_closed", false, null)]),
            "UsdJpyWithoutCaveat" => ValidAdapterRequest(instruments: LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol == "USDJPY" ? x with { Caveat = null } : x).ToList()),
            "ProductionAccount" => ValidAdapterRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(ProductionAccountRequested: true)),
            "OrdersEnabled" => ValidAdapterRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(AllowOrderSubmission: true)),
            "LiveTradingEnabled" => ValidAdapterRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(AllowLiveTrading: true)),
            "SchedulerEnabled" => ValidAdapterRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(SchedulerEnabled: true)),
            "PollingEnabled" => ValidAdapterRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(PollingEnabled: true)),
            "ReplayEnabled" => ValidAdapterRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(ReplayEnabled: true)),
            "ShadowReplayEnabled" => ValidAdapterRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(ShadowReplayEnabled: true)),
            "TradingMutationEnabled" => ValidAdapterRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(TradingMutationEnabled: true)),
            "PersistentRuntimeEnablement" => ValidAdapterRequest(safetyFlags: new LmaxReadOnlyRuntimeSafetyFlags(PersistentRuntimeEnablementRequested: true)),
            "NonDryRunAdapterMode" => ValidAdapterRequest(adapterMode: LmaxTemporaryReadOnlyRuntimeAdapterMode.RealActivationSkeleton),
            _ => throw new InvalidOperationException(condition)
        };

        var result = adapter.ValidateAsync(request);

        Assert.False(result.Passed);
        Assert.NotEmpty(result.Issues);
        Assert.NotEqual(LmaxTemporaryReadOnlyRuntimeActivationOutcome.DryRunAccepted, result.Outcome);
        Assert.False(result.SafetySnapshot.ExternalRunExecuted);
        Assert.False(result.SafetySnapshot.RealSocketOpened);
        Assert.False(result.SafetySnapshot.TcpConnectionAttempted);
        Assert.False(result.SafetySnapshot.TlsHandshakeAttempted);
        Assert.False(result.SafetySnapshot.FixLogonAttempted);
        Assert.False(result.SafetySnapshot.MarketDataRequestSent);
    }

    [Fact]
    public void Real_adapter_skeleton_cannot_execute()
    {
        var adapter = new LmaxRealTemporaryReadOnlyRuntimeActivationAdapterSkeleton();

        var result = adapter.ValidateAsync(ValidAdapterRequest(adapterMode: LmaxTemporaryReadOnlyRuntimeAdapterMode.RealActivationSkeleton));

        Assert.False(result.Passed);
        Assert.Equal(LmaxTemporaryReadOnlyRuntimeActivationOutcome.RealActivationNotAuthorized, result.Outcome);
        Assert.True(result.FutureR10ApprovalRequired);
        Assert.False(result.SafetySnapshot.ExternalRunExecuted);
        Assert.False(result.SafetySnapshot.RealSocketOpened);
        Assert.False(result.SafetySnapshot.CredentialsLoaded);
    }

    [Fact]
    public void Adapter_path_source_has_no_network_credential_or_api_worker_dependency()
    {
        var root = FindRepoRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Infrastructure.Lmax", "LmaxTemporaryReadOnlyRuntimeAdapterPath.cs"));
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var appsettings = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "appsettings.json"));

        Assert.DoesNotContain("TcpClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Net.Sockets", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new Socket", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SslStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("NetworkStream", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("QuickFix", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CredentialProfileResolver", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SessionPassword", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxDryRunTemporaryReadOnlyRuntimeActivationAdapter", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Enabled\": true", appsettings, StringComparison.Ordinal);
    }

    private static LmaxTemporaryReadOnlyRuntimeActivationRequest ValidAdapterRequest(
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags = null,
        string? approvalPhrase = null,
        LmaxTemporaryReadOnlyRuntimeAdapterMode adapterMode = LmaxTemporaryReadOnlyRuntimeAdapterMode.DryRunOnly)
    {
        var harness = LmaxReadOnlyRuntimeActivationGateHarness.BuildDryRunPreflight(new LmaxReadOnlyRuntimeActivationGateHarnessRequest(
            "LMAX-R7",
            "Philippe",
            new DateTimeOffset(2026, 05, 12, 17, 00, 00, TimeSpan.Zero),
            approvalPhrase ?? LmaxReadOnlyRuntimeActivationGateHarness.ExpectedR8ApprovalPhraseTemplate,
            instruments,
            safetyFlags));

        return LmaxTemporaryReadOnlyRuntimeActivationRequest.FromHarnessResult(
            harness,
            new DateTimeOffset(2026, 05, 12, 18, 20, 00, TimeSpan.Zero),
            adapterMode);
    }

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
