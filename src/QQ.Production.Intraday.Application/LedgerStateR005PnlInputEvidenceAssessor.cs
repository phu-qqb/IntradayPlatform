namespace QQ.Production.Intraday.Application;

public enum LedgerStateR005EvidenceClassification
{
    Present,
    PresentWithWarnings,
    Missing,
    BlockedByMarketData,
    BlockedByPmsQubes,
    RequiresOperatorPolicy,
    NotApplicable
}

public enum LedgerStateR005TheoreticalPnlReadinessStatus
{
    SandboxPriceDeltaOnlyReady,
    SandboxTheoreticalPnlReadyWithWarnings,
    SandboxTheoreticalPnlBlockedMissingEconomicInputs,
    InconclusiveSafe
}

public enum LedgerStateR005Decision
{
    SandboxTheoreticalPnlInputEvidenceReady,
    SandboxTheoreticalPnlInputEvidencePartial,
    SandboxTheoreticalPnlBlockedMissingInputs,
    InconclusiveSafe
}

public sealed record LedgerStateR005EvidenceItem(
    string EvidenceName,
    LedgerStateR005EvidenceClassification Classification,
    string Source,
    string StatusReason,
    string? Blocker);

public sealed record LedgerStateR005AssessmentRequest(
    string RequestId,
    IReadOnlyList<LedgerStateR005EvidenceItem> EvidenceItems,
    bool SandboxPriceDeltaOnlyReady,
    bool ProductionPnlAllowed,
    bool AccountingPnlAllowed,
    bool RealPnlComputed,
    bool LedgerCommitAllowed,
    bool LedgerMutationAllowed,
    bool TradingStateMutationAllowed,
    bool MarkPricesInvented,
    bool CostModelInvented,
    bool FxConversionInvented,
    bool AccountCurrencyInvented,
    bool AttributionInvented,
    bool MissingPmsQubesFieldsInvented,
    bool MarketDataDbReadinessClaimedComplete);

public sealed record LedgerStateR005AssessmentResult(
    LedgerStateR005TheoreticalPnlReadinessStatus ReadinessStatus,
    LedgerStateR005Decision Decision,
    IReadOnlyList<string> RemainingBlockers,
    IReadOnlyList<string> Diagnostics,
    bool SandboxPriceDeltaOnlyReady,
    bool FullTheoreticalPnlReady,
    bool ProductionPnlReady,
    bool AccountingPnlReady,
    bool CommitAllowed,
    bool LedgerMutation,
    bool TradingStateMutation);

public sealed class LedgerStateR005PnlInputEvidenceAssessor
{
    public LedgerStateR005AssessmentResult Assess(LedgerStateR005AssessmentRequest request)
    {
        var diagnostics = ValidateRequest(request).ToArray();
        var forbidden = diagnostics.Any(x => x.EndsWith("Forbidden", StringComparison.Ordinal));
        var blockers = request.EvidenceItems
            .Select(x => x.Blocker)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var hasBlockingEvidence = request.EvidenceItems.Any(IsBlocking);
        var readiness = forbidden
            ? LedgerStateR005TheoreticalPnlReadinessStatus.InconclusiveSafe
            : hasBlockingEvidence
                ? LedgerStateR005TheoreticalPnlReadinessStatus.SandboxTheoreticalPnlBlockedMissingEconomicInputs
                : LedgerStateR005TheoreticalPnlReadinessStatus.SandboxTheoreticalPnlReadyWithWarnings;

        var decision = readiness switch
        {
            LedgerStateR005TheoreticalPnlReadinessStatus.SandboxTheoreticalPnlReadyWithWarnings => LedgerStateR005Decision.SandboxTheoreticalPnlInputEvidencePartial,
            LedgerStateR005TheoreticalPnlReadinessStatus.SandboxTheoreticalPnlBlockedMissingEconomicInputs => LedgerStateR005Decision.SandboxTheoreticalPnlBlockedMissingInputs,
            LedgerStateR005TheoreticalPnlReadinessStatus.InconclusiveSafe => LedgerStateR005Decision.InconclusiveSafe,
            _ => LedgerStateR005Decision.SandboxTheoreticalPnlInputEvidenceReady
        };

        return new LedgerStateR005AssessmentResult(
            readiness,
            decision,
            blockers,
            diagnostics,
            SandboxPriceDeltaOnlyReady: request.SandboxPriceDeltaOnlyReady,
            FullTheoreticalPnlReady: readiness == LedgerStateR005TheoreticalPnlReadinessStatus.SandboxTheoreticalPnlReadyWithWarnings && blockers.Length == 0,
            ProductionPnlReady: false,
            AccountingPnlReady: false,
            CommitAllowed: false,
            LedgerMutation: false,
            TradingStateMutation: false);
    }

    private static bool IsBlocking(LedgerStateR005EvidenceItem item)
        => item.Classification is LedgerStateR005EvidenceClassification.Missing
            or LedgerStateR005EvidenceClassification.BlockedByMarketData
            or LedgerStateR005EvidenceClassification.BlockedByPmsQubes
            or LedgerStateR005EvidenceClassification.RequiresOperatorPolicy;

    private static IEnumerable<string> ValidateRequest(LedgerStateR005AssessmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId)) yield return "MissingRequestId";
        if (!request.SandboxPriceDeltaOnlyReady) yield return "SandboxPriceDeltaBoundaryMissing";
        if (request.ProductionPnlAllowed) yield return "ProductionPnlAllowedForbidden";
        if (request.AccountingPnlAllowed) yield return "AccountingPnlAllowedForbidden";
        if (request.RealPnlComputed) yield return "RealPnlComputedForbidden";
        if (request.LedgerCommitAllowed) yield return "LedgerCommitAllowedForbidden";
        if (request.LedgerMutationAllowed) yield return "LedgerMutationForbidden";
        if (request.TradingStateMutationAllowed) yield return "TradingStateMutationForbidden";
        if (request.MarkPricesInvented) yield return "MarkPricesInventedForbidden";
        if (request.CostModelInvented) yield return "CostModelInventedForbidden";
        if (request.FxConversionInvented) yield return "FxConversionInventedForbidden";
        if (request.AccountCurrencyInvented) yield return "AccountCurrencyInventedForbidden";
        if (request.AttributionInvented) yield return "AttributionInventedForbidden";
        if (request.MissingPmsQubesFieldsInvented) yield return "MissingPmsQubesFieldsInventedForbidden";
        if (request.MarketDataDbReadinessClaimedComplete) yield return "MarketDataDbReadinessClaimedCompleteForbidden";
    }
}
