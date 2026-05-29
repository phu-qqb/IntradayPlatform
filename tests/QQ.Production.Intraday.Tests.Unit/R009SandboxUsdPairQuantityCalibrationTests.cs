using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009SandboxUsdPairQuantityCalibrationTests
{
    private readonly R009LmaxSandboxOrderPathSmokeGate _gate = new();

    [Fact]
    public void Quantity_inventory_preserves_all_supported_usd_pairs()
    {
        var inventory = _gate.BuildUsdPairQuantityRuleInventory(
        [
            Result("EURUSD", "RuleValidatedLocal", submitted: true, accepted: true, securityId: "4001"),
            Result("AUDUSD", "RuleValidatedSandboxAccepted", submitted: true, accepted: true, securityId: "4007")
        ]);

        Assert.Equal(7, inventory.Results.Count);
        Assert.Equal(1, inventory.LocallyValidatedCount);
        Assert.Equal(1, inventory.SandboxValidatedCount);
        Assert.Equal(5, inventory.MissingSkippedCount);
        Assert.False(inventory.QuantityRulesInvented);
        Assert.Contains("USDJPY", inventory.SupportedSymbols);
    }

    [Fact]
    public void Calibration_plan_enforces_one_order_per_symbol_and_total_cap()
    {
        var validation = _gate.ValidateUsdPairQuantityCalibrationPlan(
        [
            Result("EURUSD", "RuleValidatedLocal", submitted: true, accepted: true, securityId: "4001"),
            Result("EURUSD", "RuleValidatedSandboxAccepted", submitted: true, accepted: true, securityId: "4001")
        ],
        maxSandboxOrderCount: 7,
        maxOrderQuantityPerSymbol: 0.1m,
        maxTotalSandboxQuantity: 0.7m,
        canonicalTargetCloseUtc: new DateTimeOffset(2026, 5, 26, 15, 15, 0, TimeSpan.Zero));

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, validation.Status);
        Assert.False(validation.OneOrderPerSymbol);
        Assert.Contains("MoreThanOneOrderPerSymbol", validation.Reasons);
    }

    [Fact]
    public void Calibration_plan_rejects_legacy_noncanonical_target_close()
    {
        var validation = _gate.ValidateUsdPairQuantityCalibrationPlan(
        [
            Result("EURUSD", "RuleValidatedLocal", submitted: true, accepted: true, securityId: "4001")
        ],
        maxSandboxOrderCount: 7,
        maxOrderQuantityPerSymbol: 0.1m,
        maxTotalSandboxQuantity: 0.7m,
        canonicalTargetCloseUtc: new DateTimeOffset(2026, 5, 26, 15, 6, 0, TimeSpan.Zero));

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, validation.Status);
        Assert.True(validation.Legacy06AcceptedAsFutureCanonical);
        Assert.Contains("CanonicalQuarterHourTargetCloseRequired", validation.Reasons);
    }

    [Fact]
    public void Calibration_plan_preserves_usdjpy_security_id_caveat()
    {
        var validation = _gate.ValidateUsdPairQuantityCalibrationPlan(
        [
            Result("USDJPY", "RuleValidatedSandboxAccepted", submitted: true, accepted: true, securityId: "4004")
        ],
        maxSandboxOrderCount: 7,
        maxOrderQuantityPerSymbol: 0.1m,
        maxTotalSandboxQuantity: 0.7m,
        canonicalTargetCloseUtc: new DateTimeOffset(2026, 5, 26, 15, 15, 0, TimeSpan.Zero));

        Assert.Equal(R009SandboxOrderPathStatus.Ready, validation.Status);
        Assert.True(validation.UsdjpyCaveatPreserved);
    }

    [Fact]
    public void Calibration_plan_rejects_direct_cross_submission()
    {
        var validation = _gate.ValidateUsdPairQuantityCalibrationPlan(
        [
            Result("EURGBP", "RuleValidatedSandboxAccepted", submitted: true, accepted: true, securityId: "4003")
        ],
        maxSandboxOrderCount: 7,
        maxOrderQuantityPerSymbol: 0.1m,
        maxTotalSandboxQuantity: 0.7m,
        canonicalTargetCloseUtc: new DateTimeOffset(2026, 5, 26, 15, 15, 0, TimeSpan.Zero));

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, validation.Status);
        Assert.True(validation.DirectCrossExecutionAllowed);
        Assert.True(validation.NonWhitelistedSymbolAllowed);
        Assert.Contains("UnsupportedSymbolSubmitted", validation.Reasons);
    }

    private static R009SandboxPerSymbolQuantityCalibrationResult Result(
        string symbol,
        string status,
        bool submitted,
        bool accepted,
        string securityId)
        => new(
            Symbol: symbol,
            QuantityRuleStatus: status,
            CandidateQuantity: 0.1m,
            Attempted: submitted,
            Submitted: submitted,
            AcceptedOrAcked: accepted,
            Rejected: !accepted && submitted,
            RejectReason: accepted ? null : "QUANTITY_NOT_VALID",
            FillCount: accepted ? 1 : 0,
            SecurityID: securityId,
            SecurityIDSource: "8",
            SandboxOnly: true,
            ProductionOrderCreated: false,
            ProductionRouteCreated: false,
            ProductionLedgerMutation: false,
            SourceEvidencePaths: ["test"]);
}
