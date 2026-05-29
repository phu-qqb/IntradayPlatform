using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009SandboxLifecycleRepeatabilityTests
{
    private readonly R009LmaxSandboxOrderPathSmokeGate _gate = new();

    [Fact]
    public void Sandbox_oms_state_model_forbids_production_order_and_ledger_states()
    {
        var model = _gate.BuildSandboxOmsStateModel();

        Assert.True(model.ProductionOrderStateForbidden);
        Assert.True(model.ProductionLedgerStateForbidden);
        Assert.True(model.SupportsReconciliationState);
        Assert.True(model.SupportsIdempotencyState);
        Assert.Contains(model.Transitions, x => x.To == R009SandboxOmsState.SandboxFilled);
        Assert.Contains(model.Transitions, x => x.To == R009SandboxOmsState.SandboxFlatConfirmed);
        Assert.All(model.Transitions, transition =>
        {
            Assert.True(transition.ProductionOrderStateForbidden);
            Assert.True(transition.LedgerStateForbidden);
        });
    }

    [Fact]
    public void Idempotency_contract_rejects_duplicate_clordid_and_production_fallback()
    {
        var contract = _gate.BuildSandboxIdempotencyContract(
            "r009-repeat-open-intent",
            "r009-repeat-open-route",
            "r009-repeat-open-submission",
            "R009OEURUSD260526");
        var result = _gate.ValidateDuplicatePrevention(
            contract,
            duplicateClOrdIdAttempted: true,
            sameIntentReplay: true,
            sameIntentDifferentQuantity: true,
            alreadyFlattenedReplayAttempted: true,
            explicitNewSandboxApprovalForSecondFlatten: false,
            productionOrderFallback: false);

        Assert.Equal(R009SandboxOrderPathStatus.Ready, result.Status);
        Assert.True(result.DuplicateClOrdIdRejected);
        Assert.True(result.SameIntentReplaySafe);
        Assert.True(result.SameIntentDifferentQuantityConflict);
        Assert.True(result.AlreadyFlattenedReplayBlocked);
        Assert.True(result.NoProductionOrderFallback);
    }

    [Fact]
    public void Duplicate_prevention_blocks_already_flattened_replay_without_approval()
    {
        var contract = _gate.BuildSandboxIdempotencyContract(
            "flatten-intent",
            "flatten-route",
            "flatten-submission",
            "R009FEURUSD260526");
        var result = _gate.ValidateDuplicatePrevention(
            contract,
            duplicateClOrdIdAttempted: false,
            sameIntentReplay: false,
            sameIntentDifferentQuantity: false,
            alreadyFlattenedReplayAttempted: true,
            explicitNewSandboxApprovalForSecondFlatten: false,
            productionOrderFallback: false);

        Assert.Equal(R009SandboxOrderPathStatus.Ready, result.Status);
        Assert.True(result.AlreadyFlattenedReplayBlocked);
    }

    [Fact]
    public void Duplicate_prevention_rejects_production_order_fallback()
    {
        var contract = _gate.BuildSandboxIdempotencyContract(
            "open-intent",
            "open-route",
            "open-submission",
            "R009OEURUSD260526");
        var result = _gate.ValidateDuplicatePrevention(
            contract,
            duplicateClOrdIdAttempted: false,
            sameIntentReplay: false,
            sameIntentDifferentQuantity: false,
            alreadyFlattenedReplayAttempted: false,
            explicitNewSandboxApprovalForSecondFlatten: false,
            productionOrderFallback: true);

        Assert.Equal(R009SandboxOrderPathStatus.Blocked, result.Status);
        Assert.False(result.NoProductionOrderFallback);
        Assert.Contains("ProductionOrderFallbackAllowed", result.Reasons);
    }
}
