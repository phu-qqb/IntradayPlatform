using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009SandboxFlattenReconciliationTests
{
    private readonly R009LmaxSandboxOrderPathSmokeGate _gate = new();

    [Fact]
    public void R007_fill_review_requires_seven_sandbox_whitelisted_fills()
    {
        var review = _gate.ReviewR007SandboxFills(SevenR007Results());

        Assert.Equal(R009SandboxOrderPathStatus.Ready, review.Status);
        Assert.Equal(7, review.FillCount);
        Assert.Equal(0.7m, review.TotalFilledQuantity);
        Assert.True(review.SevenWhitelistedSymbolsFilled);
        Assert.True(review.QuantityPointOnePerSymbol);
        Assert.True(review.SandboxOnly);
    }

    [Fact]
    public void Fill_report_derived_reconciliation_builds_open_positions()
    {
        var reconciliation = _gate.BuildFillReportDerivedPositionReconciliation(SevenR007Results());

        Assert.Equal(R009SandboxOrderPathStatus.Ready, reconciliation.Status);
        Assert.Equal("FillReportDerived", reconciliation.PositionSource);
        Assert.False(reconciliation.ProductionPositionQueryUsed);
        Assert.Equal(7, reconciliation.Lines.Count);
        Assert.Equal(0.7m, reconciliation.GrossOpenQuantity);
    }

    [Fact]
    public void Flatten_plan_uses_opposite_side_for_r007_buy_positions()
    {
        var positions = _gate.BuildFillReportDerivedPositionReconciliation(SevenR007Results());
        var plan = _gate.PlanSandboxFlattenOrders(positions.Lines, Metadata());

        Assert.Equal(R009SandboxOrderPathStatus.Ready, plan.Status);
        Assert.Equal(7, plan.PlannedOrderCount);
        Assert.Equal(0.7m, plan.PlannedTotalQuantity);
        Assert.All(plan.Lines, line =>
        {
            Assert.Equal("Sell", line.FlattenSide);
            Assert.Equal(0.1m, line.FlattenQuantity);
            Assert.True(line.SandboxOnly);
            Assert.False(line.ProductionOrder);
        });
    }

    [Fact]
    public void Flatten_guardrails_reject_quantity_above_original_fill()
    {
        var badPlan = new R009SandboxFlattenOrderPlan(
            R009SandboxOrderPathStatus.Ready,
            [
                new R009SandboxFlattenOrderPlanLine("EURUSD", "Sell", 0.2m, "4001", "8", false, "EURUSD", true, false)
            ],
            PlannedOrderCount: 1,
            PlannedTotalQuantity: 0.2m,
            OneFlattenOrderPerOpenPosition: true,
            DirectCrossExecutionAllowed: false,
            NonWhitelistedSymbolAllowed: false,
            Reasons: []);

        var guardrail = _gate.ValidateSandboxFlattenGuardrails(
            badPlan,
            maxSandboxFlattenOrderCount: 7,
            maxFlattenQuantityPerSymbol: 0.1m,
            maxTotalFlattenQuantity: 0.7m,
            sandboxProfileConfirmed: true,
            credentialValuesRedacted: true,
            productionRouteBlocked: true,
            productionLedgerBlocked: true,
            schedulerBlocked: true,
            canonicalTargetCloseUtc: new DateTimeOffset(2026, 5, 26, 15, 15, 0, TimeSpan.Zero));

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, guardrail.Status);
        Assert.Contains("MaxFlattenQuantityPerSymbolExceeded", guardrail.Reasons);
    }

    [Fact]
    public void Post_flatten_reconciliation_marks_flat_when_all_positions_filled_opposite()
    {
        var pre = _gate.BuildFillReportDerivedPositionReconciliation(SevenR007Results());
        var post = _gate.ReconcilePostFlatten(pre, SevenR008FlattenResults());

        Assert.Equal(R009SandboxOrderPathStatus.Ready, post.Status);
        Assert.Equal(7, post.FlattenSubmittedCount);
        Assert.Equal(7, post.FlattenFilledCount);
        Assert.Equal(0m, post.ExpectedResidualQuantity);
        Assert.True(post.FlatByFillReportDerivedAudit);
    }

    private static IReadOnlyDictionary<string, (string SecurityId, bool RequiresInversion, string NormalizedPortfolioSymbol)> Metadata()
        => new Dictionary<string, (string, bool, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["EURUSD"] = ("4001", false, "EURUSD"),
            ["AUDUSD"] = ("4007", false, "AUDUSD"),
            ["GBPUSD"] = ("4002", false, "GBPUSD"),
            ["NZDUSD"] = ("100613", false, "NZDUSD"),
            ["USDJPY"] = ("4004", true, "JPYUSD"),
            ["USDCAD"] = ("4013", true, "CADUSD"),
            ["USDCHF"] = ("4010", true, "CHFUSD")
        };

    private static R009SandboxPerSymbolQuantityCalibrationResult[] SevenR007Results()
        => new[]
        {
            Result("EURUSD", "RuleValidatedLocal", "4001"),
            Result("AUDUSD", "RuleValidatedSandboxAccepted", "4007"),
            Result("GBPUSD", "RuleValidatedSandboxAccepted", "4002"),
            Result("NZDUSD", "RuleValidatedSandboxAccepted", "100613"),
            Result("USDJPY", "RuleValidatedSandboxAccepted", "4004"),
            Result("USDCAD", "RuleValidatedSandboxAccepted", "4013"),
            Result("USDCHF", "RuleValidatedSandboxAccepted", "4010")
        };

    private static R009SandboxPerSymbolQuantityCalibrationResult[] SevenR008FlattenResults()
        => SevenR007Results()
            .Select(x => x with { QuantityRuleStatus = "FlattenFilled" })
            .ToArray();

    private static R009SandboxPerSymbolQuantityCalibrationResult Result(string symbol, string status, string securityId)
        => new(
            Symbol: symbol,
            QuantityRuleStatus: status,
            CandidateQuantity: 0.1m,
            Attempted: true,
            Submitted: true,
            AcceptedOrAcked: true,
            Rejected: false,
            RejectReason: null,
            FillCount: 1,
            SecurityID: securityId,
            SecurityIDSource: "8",
            SandboxOnly: true,
            ProductionOrderCreated: false,
            ProductionRouteCreated: false,
            ProductionLedgerMutation: false,
            SourceEvidencePaths: ["test"]);
}
