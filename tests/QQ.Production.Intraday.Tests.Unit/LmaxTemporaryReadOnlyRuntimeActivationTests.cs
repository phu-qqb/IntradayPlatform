using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxTemporaryReadOnlyRuntimeActivationTests
{
    [Fact]
    public void Approved_instrument_allowlist_preserves_usdjpy_caveat()
    {
        var instruments = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments;

        Assert.Contains(instruments, x => x.Symbol == "GBPUSD" && x.SecurityId == "4002" && x.SecurityIdSource == "8");
        Assert.Contains(instruments, x => x.Symbol == "EURGBP" && x.SecurityId == "4003" && x.SecurityIdSource == "8");
        Assert.Contains(instruments, x => x.Symbol == "AUDUSD" && x.SecurityId == "4007" && x.SecurityIdSource == "8");
        var usdJpy = Assert.Single(instruments, x => x.Symbol == "USDJPY");
        Assert.Equal("4004", usdJpy.SecurityId);
        Assert.Equal("8", usdJpy.SecurityIdSource);
        Assert.Equal(LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, usdJpy.Caveat);
    }

    [Fact]
    public void Valid_inert_readonly_scope_passes()
    {
        var gate = LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(ValidScope());

        Assert.True(gate.Passed);
        Assert.Empty(gate.Issues);
    }

    [Theory]
    [InlineData("ProductionAccount")]
    [InlineData("OrdersEnabled")]
    [InlineData("LiveTradingEnabled")]
    [InlineData("TradingEnabled")]
    [InlineData("SchedulerEnabled")]
    [InlineData("PollingEnabled")]
    [InlineData("ReplayEnabled")]
    [InlineData("ShadowReplayEnabled")]
    [InlineData("MutationEnabled")]
    [InlineData("NonApprovedInstrument")]
    [InlineData("UsdJpyWithoutCaveat")]
    [InlineData("PersistentRuntimeEnablement")]
    [InlineData("DefaultGatewayRegistrationChange")]
    [InlineData("MissingOperatorApproval")]
    [InlineData("MissingShutdownRevertPlan")]
    [InlineData("SanitizationDisabled")]
    public void Unsafe_or_incomplete_scope_fails(string condition)
    {
        var scope = condition switch
        {
            "ProductionAccount" => ValidScope(safetyFlags: ValidSafetyFlags() with { ProductionAccountRequested = true }),
            "OrdersEnabled" => ValidScope(safetyFlags: ValidSafetyFlags() with { AllowOrderSubmission = true }),
            "LiveTradingEnabled" => ValidScope(safetyFlags: ValidSafetyFlags() with { AllowLiveTrading = true }),
            "TradingEnabled" => ValidScope(safetyFlags: ValidSafetyFlags() with { IsTradingEnabled = true }),
            "SchedulerEnabled" => ValidScope(safetyFlags: ValidSafetyFlags() with { SchedulerEnabled = true }),
            "PollingEnabled" => ValidScope(safetyFlags: ValidSafetyFlags() with { PollingEnabled = true }),
            "ReplayEnabled" => ValidScope(safetyFlags: ValidSafetyFlags() with { ReplayEnabled = true }),
            "ShadowReplayEnabled" => ValidScope(safetyFlags: ValidSafetyFlags() with { ShadowReplayEnabled = true }),
            "MutationEnabled" => ValidScope(safetyFlags: ValidSafetyFlags() with { TradingMutationEnabled = true }),
            "NonApprovedInstrument" => ValidScope(instruments: [.. LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments, new("EURUSD", "4001", "8", "prior_workflow_closed", false, null)]),
            "UsdJpyWithoutCaveat" => ValidScope(instruments: LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol == "USDJPY" ? x with { Caveat = null } : x).ToList()),
            "PersistentRuntimeEnablement" => ValidScope(safetyFlags: ValidSafetyFlags() with { PersistentRuntimeEnablementRequested = true }),
            "DefaultGatewayRegistrationChange" => ValidScope(safetyFlags: ValidSafetyFlags() with { DefaultGatewayRegistrationChangeRequested = true }),
            "MissingOperatorApproval" => ValidScope(includeOperatorApproval: false),
            "MissingShutdownRevertPlan" => ValidScope(includeShutdownRevert: false),
            "SanitizationDisabled" => ValidScope(safetyFlags: ValidSafetyFlags() with { OutputSanitizationEnabled = false }),
            _ => throw new InvalidOperationException(condition)
        };

        var gate = LmaxTemporaryReadOnlyRuntimeActivationValidator.Validate(scope);

        Assert.False(gate.Passed);
        Assert.NotEmpty(gate.Issues);
    }

    [Fact]
    public void Sanitized_status_contract_contains_boundary_fields_without_credentials()
    {
        var status = new LmaxReadOnlyRuntimeSanitizedInstrumentStatus(
            "GBPUSD",
            "4002",
            "8",
            "Demo/read-only",
            LmaxTemporaryReadOnlyRuntimeBoundaryStatus.NotAttempted,
            "NotAttemptedR5InertValidationOnly",
            null,
            DateTimeOffset.UtcNow,
            null);

        Assert.Equal("GBPUSD", status.Symbol);
        Assert.Equal("4002", status.SecurityId);
        Assert.Equal(LmaxTemporaryReadOnlyRuntimeBoundaryStatus.NotAttempted, status.BoundaryStatus);
    }

    private static LmaxTemporaryReadOnlyRuntimeActivationScope ValidScope(
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? instruments = null,
        LmaxReadOnlyRuntimeSafetyFlags? safetyFlags = null,
        LmaxReadOnlyRuntimeOperatorApproval? operatorApproval = null,
        LmaxReadOnlyRuntimeShutdownRevertRecord? shutdownRevert = null,
        bool includeOperatorApproval = true,
        bool includeShutdownRevert = true)
    {
        var selectedInstruments = instruments ?? LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments;

        return new LmaxTemporaryReadOnlyRuntimeActivationScope(
            "LMAX-R5",
            "Demo",
            DemoReadOnly: true,
            Temporary: true,
            InertValidatorOnly: true,
            selectedInstruments,
            safetyFlags ?? ValidSafetyFlags(),
            includeOperatorApproval ? operatorApproval ?? new LmaxReadOnlyRuntimeOperatorApproval(
                "Philippe",
                new DateTimeOffset(2026, 05, 12, 15, 50, 00, TimeSpan.Zero),
                "R5 inert validator-only approval model",
                "LMAX-R5",
                "Demo/read-only",
                selectedInstruments.Select(x => x.Symbol).ToList()) : null,
            includeShutdownRevert ? shutdownRevert ?? new LmaxReadOnlyRuntimeShutdownRevertRecord(
                PlanPresent: true,
                ShutdownRequiredAfterAttempt: true,
                RevertRequiredAfterAttempt: true,
                "artifacts/readiness/lmax-runtime-enablement/future-shutdown-revert-record.json") : null,
            MaxRuntimeSeconds: 30,
            OutputRoot: "artifacts/readiness/lmax-runtime-enablement");
    }

    private static LmaxReadOnlyRuntimeSafetyFlags ValidSafetyFlags()
        => new();
}
