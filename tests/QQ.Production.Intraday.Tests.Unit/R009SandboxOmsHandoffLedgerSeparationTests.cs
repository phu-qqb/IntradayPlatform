using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class R009SandboxOmsHandoffLedgerSeparationTests
{
    private readonly R009LmaxSandboxOrderPathSmokeGate _gate = new();

    [Fact]
    public void Sandbox_oms_handoff_accepts_lifecycle_without_production_or_ledger_state()
    {
        var model = _gate.BuildSandboxOmsStateModel();
        var handoff = _gate.BuildSandboxOmsHandoffContract(model, sandboxLifecycleAccepted: true);

        Assert.Equal(R009SandboxOrderPathStatus.Ready, handoff.Status);
        Assert.True(handoff.SandboxLifecycleAccepted);
        Assert.False(handoff.ProductionOmsStateMutationAllowed);
        Assert.False(handoff.PaperLedgerCommitAllowed);
        Assert.False(handoff.ProductionLedgerCommitAllowed);
        Assert.False(handoff.TradingStateMutationAllowed);
        Assert.Contains("SandboxFillToPaperLedgerCommit", handoff.ForbiddenTransitions);
        Assert.Contains(handoff.AllowedTransitions, x => x.To == R009SandboxOmsState.SandboxFlatConfirmed);
    }

    [Fact]
    public void State_transition_map_links_r007_r008_r009_evidence_to_sandbox_states_only()
    {
        var map = _gate.BuildSandboxStateTransitionMap(_gate.BuildSandboxOmsStateModel());

        Assert.True(map.ProductionStateForbidden);
        Assert.True(map.LedgerStateForbidden);
        Assert.Contains("EXEC-SANDBOX-R007", map.EvidencePhases);
        Assert.Contains("EXEC-SANDBOX-R008", map.EvidencePhases);
        Assert.Contains("EXEC-SANDBOX-R009", map.EvidencePhases);
        Assert.Contains(R009SandboxOmsState.SandboxFilled, map.EvidenceByState.Keys);
        Assert.Contains(R009SandboxOmsState.SandboxFlatConfirmed, map.EvidenceByState.Keys);
        Assert.Contains("SandboxTerminalToLiveProductionPromotion", map.ForbiddenTransitions);
    }

    [Fact]
    public void Paper_ledger_separation_contract_allows_review_reference_but_blocks_mutation()
    {
        var contract = _gate.BuildSandboxPaperLedgerSeparationContract();

        Assert.False(contract.PaperLedgerCommitAllowed);
        Assert.False(contract.ProductionLedgerCommitAllowed);
        Assert.False(contract.TradingStateMutationAllowed);
        Assert.True(contract.SandboxFillCanBeReferencedForReview);
        Assert.False(contract.SandboxFillCanMutateLedger);
        Assert.False(contract.SandboxFillCanMutateProductionState);
        Assert.True(contract.PaperLedgerPreviewOnlyPreserved);
    }

    [Fact]
    public void Duplicate_prevention_handoff_preserves_already_flattened_and_no_fallback_guards()
    {
        var contract = _gate.BuildSandboxIdempotencyContract(
            "r009-repeat-open-intent",
            "r009-repeat-open-route",
            "r009-repeat-open-submission",
            "R009OEURUSD260526");
        var duplicate = _gate.ValidateDuplicatePrevention(
            contract,
            duplicateClOrdIdAttempted: true,
            sameIntentReplay: true,
            sameIntentDifferentQuantity: true,
            alreadyFlattenedReplayAttempted: true,
            explicitNewSandboxApprovalForSecondFlatten: false,
            productionOrderFallback: false);

        var handoff = _gate.BuildDuplicatePreventionHandoff(duplicate);

        Assert.Equal(R009SandboxOrderPathStatus.Ready, handoff.Status);
        Assert.True(handoff.DuplicateClOrdIdPreventionPreserved);
        Assert.True(handoff.SameIntentReplaySafe);
        Assert.True(handoff.SameIntentDifferentQuantityConflict);
        Assert.True(handoff.AlreadyFlattenedProtectionPreserved);
        Assert.True(handoff.NoDuplicateSubmissionForSameIdempotencyKey);
        Assert.True(handoff.NoProductionOrderFallback);
    }
}
