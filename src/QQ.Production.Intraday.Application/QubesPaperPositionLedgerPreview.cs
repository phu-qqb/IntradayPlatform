using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum PaperPositionLedgerPreviewStatus
{
    PaperLedgerPreviewReady,
    PaperLedgerPreviewApplied,
    PaperLedgerPreviewBlocked,
    PaperLedgerPreviewInconclusiveSafe,
    NoLiveMutation,
    NoBrokerMutation,
    NoTradingStateMutation
}

public sealed record PaperPositionLedgerPreviewId(string Value);

public sealed record PaperPositionLedgerPreviewLineId(string Value);

public sealed record PaperPositionLedgerBaselineLine(
    string CurrencyOrSymbol,
    decimal StartingPaperQuantity,
    string QuantityCurrency,
    string BaselineSource,
    bool NoExternal,
    bool NotBrokerState,
    bool NotProductionLedgerState);

public sealed record PaperPositionLedgerBaselineFixture(
    string BaselineFixtureId,
    string BaselineSource,
    IReadOnlyList<PaperPositionLedgerBaselineLine> Lines,
    bool NoExternal,
    bool NotBrokerState,
    bool NotLiveProductionPositionState,
    bool NotPersistedProductionLedgerState)
{
    public static PaperPositionLedgerBaselineFixture ZeroFor(
        IEnumerable<PaperPositionPreviewLineRecord> previewLines)
    {
        var lines = previewLines
            .Select(x => x.NormalizedSymbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(symbol => new PaperPositionLedgerBaselineLine(
                symbol,
                StartingPaperQuantity: 0m,
                QuantityCurrency: symbol[..3],
                BaselineSource: "NoExternalZeroPaperLedgerBaselineFixture",
                NoExternal: true,
                NotBrokerState: true,
                NotProductionLedgerState: true))
            .ToArray();

        return new PaperPositionLedgerBaselineFixture(
            "paper-ledger-baseline-r022-zero",
            "NoExternalZeroPaperLedgerBaselineFixture",
            lines,
            NoExternal: true,
            NotBrokerState: true,
            NotLiveProductionPositionState: true,
            NotPersistedProductionLedgerState: true);
    }
}

public sealed record PaperPositionLedgerPreviewLine(
    PaperPositionLedgerPreviewLineId PaperPositionLedgerPreviewLineId,
    PaperPositionLedgerPreviewId PaperPositionLedgerPreviewId,
    PaperPositionPreviewId PaperPositionPreviewId,
    PaperSimulationResultId PaperSimulationResultId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    InstrumentId InstrumentId,
    string CurrencyOrSymbol,
    decimal StartingPaperQuantity,
    decimal SimulatedDeltaQuantity,
    decimal PreviewEndingPaperQuantity,
    string QuantityCurrency,
    PaperPositionLedgerPreviewStatus LedgerPreviewStatus,
    string SourceLineageReference,
    bool PaperOnly,
    bool PreviewOnly,
    bool NoExternal,
    bool NoLivePositionMutation,
    bool NoBrokerPositionMutation,
    bool NoProductionLedgerMutation,
    bool NoTradingStateMutation,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool NoOrderCreated,
    bool NoBrokerRoute,
    bool NotSubmitted);

public sealed record PaperPositionLedgerPreviewSummary(
    int PreviewLineCount,
    int AppliedPreviewLineCount,
    int BlockedPreviewLineCount,
    int LivePositionMutationCount,
    int BrokerPositionMutationCount,
    int ProductionLedgerMutationCount,
    int TradingStateMutationCount,
    string SafetyStatus);

public sealed record PaperPositionLedgerPreview(
    PaperPositionLedgerPreviewId PaperPositionLedgerPreviewId,
    PaperPositionPreviewId PaperPositionPreviewId,
    PaperSimulationResultId PaperSimulationResultId,
    PaperSimulationPlanId PaperSimulationPlanId,
    PaperExecutionPlanId PaperExecutionPlanId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    DateTimeOffset CreatedAtUtc,
    PaperPositionLedgerPreviewStatus PreviewStatus,
    PaperPositionLedgerBaselineFixture BaselineFixture,
    IReadOnlyList<PaperPositionLedgerPreviewLine> Lines,
    IReadOnlyList<BlockedPaperReviewLineRecord> BlockedLines,
    PaperPositionLedgerPreviewSummary Summary,
    bool QubesLineagePreserved,
    bool CycleLineagePreserved,
    bool OperatorDecisionLineagePreserved,
    bool PositionPreviewLineagePreserved,
    bool SimulationResultLineagePreserved,
    bool SimulationPlanLineagePreserved,
    bool PaperExecutionPlanLineagePreserved,
    bool PaperCandidateLineagePreserved,
    bool RiskLineagePreserved,
    bool RebalanceIntentLineagePreserved,
    bool LotSizingLineagePreserved,
    bool MissingStaleMarkWarningsPreserved,
    bool DriftAcknowledgementPreserved,
    bool PaperOnly,
    bool PreviewOnly,
    bool NoExternal,
    bool NoLivePositionMutation,
    bool NoBrokerPositionMutation,
    bool NoProductionLedgerMutation,
    bool NoTradingStateMutation,
    bool NoFillCreated,
    bool NoExecutionReportCreated,
    bool NoOrderCreated,
    bool NoBrokerRoute,
    bool NotSubmitted,
    bool LivePositionStateMutated,
    bool BrokerPositionStateMutated,
    bool ProductionLedgerStateMutated,
    bool TradingStateMutated,
    bool FillCreated,
    bool ExecutionReportCreated,
    bool OmsOrderCreated,
    bool BrokerOrderCreated,
    bool OrderStateCreated,
    bool SubmittedOrders,
    bool BrokerRouteCreated);

public sealed record PaperPositionLedgerPreviewResult(
    PaperPositionLedgerPreview Preview,
    bool Persisted,
    bool AlreadyCreated);

public interface IPaperPositionLedgerPreviewRepository
{
    Task<PaperPositionLedgerPreview?> GetByPreviewIdAsync(
        PaperPositionLedgerPreviewId previewId,
        CancellationToken cancellationToken);

    Task AddAsync(PaperPositionLedgerPreview preview, CancellationToken cancellationToken);
}

public sealed class InMemoryPaperPositionLedgerPreviewRepository : IPaperPositionLedgerPreviewRepository
{
    private readonly List<PaperPositionLedgerPreview> previews = [];

    public Task<PaperPositionLedgerPreview?> GetByPreviewIdAsync(
        PaperPositionLedgerPreviewId previewId,
        CancellationToken cancellationToken)
        => Task.FromResult(previews.FirstOrDefault(x => x.PaperPositionLedgerPreviewId == previewId));

    public Task AddAsync(PaperPositionLedgerPreview preview, CancellationToken cancellationToken)
    {
        if (previews.Any(x => x.PaperPositionLedgerPreviewId == preview.PaperPositionLedgerPreviewId))
        {
            return Task.CompletedTask;
        }

        previews.Add(preview);
        return Task.CompletedTask;
    }
}

public sealed class PaperPositionLedgerPreviewService(
    IPaperPositionLedgerPreviewRepository repository,
    IClock clock)
{
    public async Task<PaperPositionLedgerPreviewResult> CreateAsync(
        PaperPositionPreviewRecord archivedPreview,
        PaperPositionLedgerBaselineFixture? baselineFixture,
        CancellationToken cancellationToken)
    {
        var previewId = new PaperPositionLedgerPreviewId($"{archivedPreview.PaperPositionPreviewId.Value}:paper-ledger-preview");
        var existing = await repository.GetByPreviewIdAsync(previewId, cancellationToken);
        if (existing is not null)
        {
            return new PaperPositionLedgerPreviewResult(
                existing,
                Persisted: false,
                AlreadyCreated: true);
        }

        var baseline = baselineFixture ?? PaperPositionLedgerBaselineFixture.ZeroFor(archivedPreview.Lines);
        var preview = CreatePreview(previewId, archivedPreview, baseline, clock.UtcNow);
        await repository.AddAsync(preview, cancellationToken);

        return new PaperPositionLedgerPreviewResult(
            preview,
            Persisted: true,
            AlreadyCreated: false);
    }

    private static PaperPositionLedgerPreview CreatePreview(
        PaperPositionLedgerPreviewId previewId,
        PaperPositionPreviewRecord archivedPreview,
        PaperPositionLedgerBaselineFixture baseline,
        DateTimeOffset createdAtUtc)
    {
        var baselineBySymbol = baseline.Lines.ToDictionary(
            x => x.CurrencyOrSymbol,
            StringComparer.OrdinalIgnoreCase);
        var lines = archivedPreview.Lines
            .OrderBy(x => x.NormalizedSymbol, StringComparer.OrdinalIgnoreCase)
            .Select(x => CreateLine(previewId, archivedPreview, x, baselineBySymbol))
            .ToArray();
        var summary = new PaperPositionLedgerPreviewSummary(
            lines.Length,
            lines.Count(x => x.LedgerPreviewStatus == PaperPositionLedgerPreviewStatus.PaperLedgerPreviewApplied),
            BlockedPreviewLineCount: 0,
            LivePositionMutationCount: 0,
            BrokerPositionMutationCount: 0,
            ProductionLedgerMutationCount: 0,
            TradingStateMutationCount: 0,
            SafetyStatus: "PaperLedgerPreviewOnly");

        return new PaperPositionLedgerPreview(
            previewId,
            archivedPreview.PaperPositionPreviewId,
            archivedPreview.PaperSimulationResultId,
            archivedPreview.PaperSimulationPlanId,
            archivedPreview.PaperExecutionPlanId,
            archivedPreview.CycleRunId,
            archivedPreview.QubesRunId,
            archivedPreview.OperatorDecisionId,
            createdAtUtc,
            PaperPositionLedgerPreviewStatus.PaperLedgerPreviewReady,
            baseline,
            lines,
            archivedPreview.BlockedLines,
            summary,
            archivedPreview.QubesLineagePreserved,
            archivedPreview.CycleLineagePreserved,
            archivedPreview.OperatorDecisionLineagePreserved,
            PositionPreviewLineagePreserved: true,
            archivedPreview.SimulationResultLineagePreserved,
            archivedPreview.SimulationPlanLineagePreserved,
            archivedPreview.PaperExecutionPlanLineagePreserved,
            archivedPreview.PaperCandidateLineagePreserved,
            archivedPreview.RiskLineagePreserved,
            archivedPreview.RebalanceIntentLineagePreserved,
            archivedPreview.LotSizingLineagePreserved,
            archivedPreview.MissingStaleMarkWarningsPreserved,
            archivedPreview.DriftAcknowledgementPreserved,
            PaperOnly: true,
            PreviewOnly: true,
            NoExternal: true,
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoProductionLedgerMutation: true,
            NoTradingStateMutation: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoOrderCreated: true,
            NoBrokerRoute: true,
            NotSubmitted: true,
            LivePositionStateMutated: false,
            BrokerPositionStateMutated: false,
            ProductionLedgerStateMutated: false,
            TradingStateMutated: false,
            FillCreated: false,
            ExecutionReportCreated: false,
            OmsOrderCreated: false,
            BrokerOrderCreated: false,
            OrderStateCreated: false,
            SubmittedOrders: false,
            BrokerRouteCreated: false);
    }

    private static PaperPositionLedgerPreviewLine CreateLine(
        PaperPositionLedgerPreviewId ledgerPreviewId,
        PaperPositionPreviewRecord archivedPreview,
        PaperPositionPreviewLineRecord line,
        IReadOnlyDictionary<string, PaperPositionLedgerBaselineLine> baselineBySymbol)
    {
        var baseline = baselineBySymbol.TryGetValue(line.NormalizedSymbol, out var matched)
            ? matched.StartingPaperQuantity
            : 0m;
        var delta = line.SimulatedPositionDelta ?? 0m;

        return new PaperPositionLedgerPreviewLine(
            new PaperPositionLedgerPreviewLineId($"{ledgerPreviewId.Value}:{line.NormalizedSymbol}:line"),
            ledgerPreviewId,
            archivedPreview.PaperPositionPreviewId,
            archivedPreview.PaperSimulationResultId,
            archivedPreview.CycleRunId,
            archivedPreview.QubesRunId,
            archivedPreview.OperatorDecisionId,
            line.InstrumentId,
            line.NormalizedSymbol,
            baseline,
            delta,
            baseline + delta,
            line.QuantityCurrency,
            PaperPositionLedgerPreviewStatus.PaperLedgerPreviewApplied,
            line.SourceLineageReference,
            PaperOnly: true,
            PreviewOnly: true,
            NoExternal: true,
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoProductionLedgerMutation: true,
            NoTradingStateMutation: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoOrderCreated: true,
            NoBrokerRoute: true,
            NotSubmitted: true);
    }
}
