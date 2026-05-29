using System.Security.Cryptography;
using System.Text;

namespace QQ.Production.Intraday.Application;

public enum LedgerStateR002PreviewDecision
{
    PaperLedgerPreviewMapperReadyWithEconomicFieldGaps,
    PaperLedgerPreviewMapperBlockedMissingCoreReportFields,
    InconclusiveSafe
}

public enum LedgerStateR002CashImpactStatus
{
    Ready,
    Incomplete
}

public sealed record LedgerStateR002SandboxFillEvidence(
    string SourcePhase,
    string SourceFillId,
    string? SourceExecutionReportId,
    string SourceSandboxOrderId,
    string ClOrdId,
    string Symbol,
    string ExecutionTradableSymbol,
    string NormalizedPortfolioSymbol,
    bool RequiresInversion,
    string SecurityID,
    string SecurityIDSource,
    string Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset? TimestampUtc,
    bool SandboxOnly,
    bool ProductionOrder,
    bool ProductionFill,
    string SideEvidenceSource);

public sealed record LedgerStateR002SandboxReconciliationEvidence(
    string SourcePhase,
    decimal ExpectedResidualQuantity,
    bool FlatByFillReportDerivedAudit,
    bool ProductionMutationDetected,
    bool SandboxOnly);

public sealed record LedgerStateR002MapperRequest(
    string RequestId,
    IReadOnlyList<LedgerStateR002SandboxFillEvidence> OpenFills,
    IReadOnlyList<LedgerStateR002SandboxFillEvidence> FlattenFills,
    LedgerStateR002SandboxReconciliationEvidence Reconciliation,
    DateTimeOffset? CanonicalTargetCloseUtc,
    bool PreviewOnly,
    bool CommitAllowed,
    bool LedgerMutationAllowed,
    bool TradingStateMutationAllowed);

public sealed record LedgerStateR002PaperLedgerPreviewLine(
    string LineId,
    string SourcePhase,
    string SourceExecutionReportId,
    string SourceFillId,
    string SourceSandboxOrderId,
    string ClOrdId,
    string Symbol,
    string ExecutionTradableSymbol,
    string NormalizedPortfolioSymbol,
    bool RequiresInversion,
    string SecurityID,
    string SecurityIDSource,
    string Side,
    decimal Quantity,
    decimal Price,
    DateTimeOffset? TimestampUtc,
    DateTimeOffset? CanonicalTargetCloseUtc,
    bool SandboxOnly,
    bool ProductionOrder,
    bool PreviewOnly,
    bool CommitAllowed,
    bool NoLedgerCommit,
    bool LedgerMutation,
    bool TradingStateMutation,
    IReadOnlyList<string> MissingFields);

public sealed record LedgerStateR002HypotheticalPositionDelta(
    string LineId,
    string Symbol,
    string NormalizedPortfolioSymbol,
    decimal QuantityDelta,
    decimal NotionalDelta,
    bool HypotheticalOnly,
    bool LedgerMutation,
    bool TradingStateMutation);

public sealed record LedgerStateR002HypotheticalExposurePreview(
    decimal GrossNotionalDelta,
    decimal NetNotionalDelta,
    IReadOnlyDictionary<string, decimal> NotionalBySymbol,
    bool FlattenedPairNetsToZero,
    bool HypotheticalOnly,
    bool LedgerMutation,
    bool TradingStateMutation);

public sealed record LedgerStateR002HypotheticalCashImpact(
    LedgerStateR002CashImpactStatus Status,
    string Currency,
    decimal? GrossCashDelta,
    decimal? NetCashDelta,
    IReadOnlyList<string> MissingReasons,
    bool HypotheticalOnly,
    bool LedgerMutation,
    bool TradingStateMutation);

public sealed record LedgerStateR002IdempotencyResult(
    string PreviewId,
    string InputHash,
    string PreviewHash,
    bool SameSourceSameInputSamePreviewHash,
    bool SameSourceDifferentInputConflict,
    bool DuplicateCommitCandidateCreated);

public sealed record LedgerStateR002PreviewReconciliation(
    int OpenFillCount,
    int FlattenFillCount,
    decimal OpenQuantity,
    decimal FlattenQuantity,
    decimal ExpectedResidualQuantity,
    decimal PreviewResidualQuantity,
    bool ResidualMatchesEvidence,
    bool LedgerMutation,
    bool TradingStateMutation);

public sealed record LedgerStateR002MapperResult(
    LedgerStateR002PreviewDecision Decision,
    IReadOnlyList<LedgerStateR002PaperLedgerPreviewLine> PreviewLines,
    IReadOnlyList<LedgerStateR002HypotheticalPositionDelta> PositionDeltas,
    LedgerStateR002HypotheticalExposurePreview ExposurePreview,
    LedgerStateR002HypotheticalCashImpact CashImpact,
    IReadOnlyList<string> CommitBlockers,
    IReadOnlyList<string> MappingDiagnostics,
    LedgerStateR002IdempotencyResult Idempotency,
    LedgerStateR002PreviewReconciliation Reconciliation,
    bool PreviewOnly,
    bool CommitAllowed,
    bool LedgerMutation,
    bool TradingStateMutation);

public sealed class LedgerStateR002SandboxReportPreviewMapper
{
    private static readonly string[] SupportedSandboxExecutionSymbols =
    [
        "EURUSD",
        "USDJPY",
        "AUDUSD",
        "GBPUSD",
        "NZDUSD",
        "USDCAD",
        "USDCHF"
    ];

    private static readonly string[] CommitBlockers =
    [
        "MissingAccountId",
        "MissingPortfolioId",
        "MissingStrategyId",
        "MissingPmsCycleId",
        "MissingQubesRunId",
        "MissingRiskReviewId",
        "MissingOperatorApprovalId",
        "MissingCommissionFeeModel",
        "MissingFxConversionModel",
        "MissingCanonicalCloseBinding",
        "CommitSafeIdempotencyPolicyIncomplete"
    ];

    public LedgerStateR002MapperResult Map(LedgerStateR002MapperRequest request)
    {
        var allFills = request.OpenFills.Concat(request.FlattenFills).ToArray();
        var diagnostics = ValidateRequest(request, allFills).ToList();
        var blocked = diagnostics.Any(x => x.StartsWith("MissingCore", StringComparison.Ordinal) || x.Contains("Forbidden", StringComparison.Ordinal));

        var lines = blocked
            ? Array.Empty<LedgerStateR002PaperLedgerPreviewLine>()
            : allFills.Select((fill, index) => ToLine(fill, index, request.CanonicalTargetCloseUtc)).ToArray();

        var deltas = lines.Select(ToPositionDelta).ToArray();
        var exposure = BuildExposure(deltas, request.Reconciliation.ExpectedResidualQuantity);
        var cashImpact = BuildCashImpact(lines);
        var reconciliation = BuildReconciliation(request, lines);
        var inputHash = Hash(string.Join("|", allFills.Select(FillKey).OrderBy(x => x, StringComparer.Ordinal)));
        var previewHash = Hash(string.Join("|", request.RequestId, inputHash, string.Join("|", lines.Select(x => $"{x.LineId}:{x.Quantity}:{x.Price}:{x.Side}"))));

        var idempotency = new LedgerStateR002IdempotencyResult(
            PreviewId: $"{request.RequestId}:paper-ledger-preview",
            InputHash: inputHash,
            PreviewHash: previewHash,
            SameSourceSameInputSamePreviewHash: true,
            SameSourceDifferentInputConflict: true,
            DuplicateCommitCandidateCreated: false);

        var decision = blocked
            ? LedgerStateR002PreviewDecision.PaperLedgerPreviewMapperBlockedMissingCoreReportFields
            : LedgerStateR002PreviewDecision.PaperLedgerPreviewMapperReadyWithEconomicFieldGaps;

        return new LedgerStateR002MapperResult(
            decision,
            lines,
            deltas,
            exposure,
            cashImpact,
            CommitBlockers,
            diagnostics,
            idempotency,
            reconciliation,
            PreviewOnly: true,
            CommitAllowed: false,
            LedgerMutation: false,
            TradingStateMutation: false);
    }

    private static IEnumerable<string> ValidateRequest(LedgerStateR002MapperRequest request, IReadOnlyList<LedgerStateR002SandboxFillEvidence> fills)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId)) yield return "MissingCoreRequestId";
        if (!request.PreviewOnly) yield return "PreviewOnlyRequiredForbidden";
        if (request.CommitAllowed) yield return "CommitAllowedForbidden";
        if (request.LedgerMutationAllowed) yield return "LedgerMutationForbidden";
        if (request.TradingStateMutationAllowed) yield return "TradingStateMutationForbidden";
        if (!request.Reconciliation.SandboxOnly || request.Reconciliation.ProductionMutationDetected) yield return "UnsafeReconciliationEvidenceForbidden";
        if (fills.Count == 0) yield return "MissingCoreFillEvidence";

        foreach (var fill in fills)
        {
            if (!fill.SandboxOnly || fill.ProductionOrder || fill.ProductionFill) yield return $"UnsafeFillEvidenceForbidden:{fill.ClOrdId}";
            if (string.IsNullOrWhiteSpace(fill.ClOrdId)) yield return "MissingCoreClOrdId";
            if (string.IsNullOrWhiteSpace(fill.Symbol)) yield return $"MissingCoreSymbol:{fill.ClOrdId}";
            if (string.IsNullOrWhiteSpace(fill.ExecutionTradableSymbol)) yield return $"MissingCoreExecutionTradableSymbol:{fill.ClOrdId}";
            if (string.IsNullOrWhiteSpace(fill.NormalizedPortfolioSymbol)) yield return $"MissingCoreNormalizedPortfolioSymbol:{fill.ClOrdId}";
            if (string.IsNullOrWhiteSpace(fill.SecurityID)) yield return $"MissingCoreSecurityID:{fill.ClOrdId}";
            if (string.IsNullOrWhiteSpace(fill.SecurityIDSource)) yield return $"MissingCoreSecurityIDSource:{fill.ClOrdId}";
            if (string.IsNullOrWhiteSpace(fill.Side)) yield return $"MissingCoreSide:{fill.ClOrdId}";
            if (fill.Quantity <= 0m) yield return $"MissingCoreQuantity:{fill.ClOrdId}";
            if (fill.Price <= 0m) yield return $"MissingCorePrice:{fill.ClOrdId}";
            if (IsDirectCross(fill.ExecutionTradableSymbol)) yield return $"DirectCrossExecutionForbidden:{fill.ExecutionTradableSymbol}";
            if (fill.ExecutionTradableSymbol.Equals("USDJPY", StringComparison.OrdinalIgnoreCase) &&
                (!fill.NormalizedPortfolioSymbol.Equals("JPYUSD", StringComparison.OrdinalIgnoreCase) || !fill.RequiresInversion || fill.SecurityID != "4004" || fill.SecurityIDSource != "8"))
            {
                yield return "USDJPYCaveatWeakenedForbidden";
            }
        }
    }

    private static LedgerStateR002PaperLedgerPreviewLine ToLine(LedgerStateR002SandboxFillEvidence fill, int index, DateTimeOffset? canonicalTargetCloseUtc)
    {
        var sourceExecutionReportId = string.IsNullOrWhiteSpace(fill.SourceExecutionReportId)
            ? $"{fill.SourcePhase}:{fill.ClOrdId}:execution-report"
            : fill.SourceExecutionReportId;

        var missing = new List<string>
        {
            "AccountId",
            "PortfolioId",
            "StrategyId",
            "PmsCycleId",
            "QubesRunId",
            "RiskReviewId",
            "OperatorApprovalId",
            "CommissionFees",
            "FxConversion"
        };
        if (canonicalTargetCloseUtc is null)
        {
            missing.Add("CanonicalTargetCloseUtc");
        }

        return new LedgerStateR002PaperLedgerPreviewLine(
            LineId: $"{fill.SourcePhase}:{fill.ClOrdId}:{index + 1}:paper-ledger-preview-line",
            fill.SourcePhase,
            sourceExecutionReportId,
            fill.SourceFillId,
            fill.SourceSandboxOrderId,
            fill.ClOrdId,
            fill.Symbol,
            fill.ExecutionTradableSymbol,
            fill.NormalizedPortfolioSymbol,
            fill.RequiresInversion,
            fill.SecurityID,
            fill.SecurityIDSource,
            fill.Side,
            fill.Quantity,
            fill.Price,
            fill.TimestampUtc,
            canonicalTargetCloseUtc,
            fill.SandboxOnly,
            fill.ProductionOrder,
            PreviewOnly: true,
            CommitAllowed: false,
            NoLedgerCommit: true,
            LedgerMutation: false,
            TradingStateMutation: false,
            MissingFields: missing);
    }

    private static LedgerStateR002HypotheticalPositionDelta ToPositionDelta(LedgerStateR002PaperLedgerPreviewLine line)
    {
        var signed = line.Side.Equals("Sell", StringComparison.OrdinalIgnoreCase) ? -line.Quantity : line.Quantity;
        var notional = signed * line.Price;

        return new LedgerStateR002HypotheticalPositionDelta(
            line.LineId,
            line.NormalizedPortfolioSymbol,
            line.NormalizedPortfolioSymbol,
            signed,
            notional,
            HypotheticalOnly: true,
            LedgerMutation: false,
            TradingStateMutation: false);
    }

    private static LedgerStateR002HypotheticalExposurePreview BuildExposure(IReadOnlyList<LedgerStateR002HypotheticalPositionDelta> deltas, decimal expectedResidual)
    {
        var bySymbol = deltas
            .GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.NotionalDelta), StringComparer.OrdinalIgnoreCase);

        return new LedgerStateR002HypotheticalExposurePreview(
            GrossNotionalDelta: deltas.Sum(x => Math.Abs(x.NotionalDelta)),
            NetNotionalDelta: deltas.Sum(x => x.NotionalDelta),
            NotionalBySymbol: bySymbol,
            FlattenedPairNetsToZero: expectedResidual == 0m,
            HypotheticalOnly: true,
            LedgerMutation: false,
            TradingStateMutation: false);
    }

    private static LedgerStateR002HypotheticalCashImpact BuildCashImpact(IReadOnlyList<LedgerStateR002PaperLedgerPreviewLine> lines)
    {
        var missing = new[]
        {
            "MissingAccountId",
            "MissingCommissionFeeModel",
            "MissingFxConversionModel",
            "MissingSettlementCurrencyPolicy"
        };

        return new LedgerStateR002HypotheticalCashImpact(
            LedgerStateR002CashImpactStatus.Incomplete,
            Currency: "USD",
            GrossCashDelta: lines.Count == 0 ? null : lines.Sum(x => Math.Abs(x.Quantity * x.Price)),
            NetCashDelta: null,
            MissingReasons: missing,
            HypotheticalOnly: true,
            LedgerMutation: false,
            TradingStateMutation: false);
    }

    private static LedgerStateR002PreviewReconciliation BuildReconciliation(LedgerStateR002MapperRequest request, IReadOnlyList<LedgerStateR002PaperLedgerPreviewLine> lines)
    {
        var openQuantity = request.OpenFills.Sum(x => x.Quantity);
        var flattenQuantity = request.FlattenFills.Sum(x => x.Quantity);
        var previewResidual = Math.Abs(openQuantity - flattenQuantity);

        return new LedgerStateR002PreviewReconciliation(
            OpenFillCount: request.OpenFills.Count,
            FlattenFillCount: request.FlattenFills.Count,
            OpenQuantity: openQuantity,
            FlattenQuantity: flattenQuantity,
            ExpectedResidualQuantity: request.Reconciliation.ExpectedResidualQuantity,
            PreviewResidualQuantity: previewResidual,
            ResidualMatchesEvidence: previewResidual == request.Reconciliation.ExpectedResidualQuantity,
            LedgerMutation: false,
            TradingStateMutation: false);
    }

    private static string FillKey(LedgerStateR002SandboxFillEvidence fill)
        => string.Join("|", fill.SourcePhase, fill.SourceFillId, fill.SourceExecutionReportId, fill.SourceSandboxOrderId, fill.ClOrdId, fill.Symbol, fill.Side, fill.Quantity, fill.Price, fill.SecurityID, fill.SecurityIDSource);

    private static bool IsDirectCross(string symbol)
        => !SupportedSandboxExecutionSymbols.Contains(symbol, StringComparer.OrdinalIgnoreCase);

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
