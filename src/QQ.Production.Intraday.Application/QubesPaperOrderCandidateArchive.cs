using System.Globalization;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum PaperOrderCandidateArchiveBatchStatus
{
    ArchivedNoExternal,
    ArchivedWithBlockedLines,
    DuplicateReturned,
    RejectedInvalidCandidate,
    InconclusiveSafe
}

public enum PaperOrderCandidateBlotterStatus
{
    PaperReady,
    RequiresLotSizing,
    BlockedByRisk,
    BlockedMissingMark,
    BlockedStaleMark,
    NonExecutable,
    InconclusiveSafe
}

public sealed record PaperOrderCandidateBatchId(string Value);

public sealed record PaperOrderCandidateLineRecord(
    PaperOrderCandidateId PaperOrderCandidateId,
    string SourceRebalanceIntentId,
    InstrumentId InstrumentId,
    string NormalizedSymbol,
    IntentSide Side,
    decimal TargetWeight,
    decimal CurrentWeight,
    decimal DeltaWeight,
    decimal? TargetNotional,
    decimal? CurrentNotional,
    decimal? DeltaNotional,
    PaperQuantityShapeCategory QuantityShapeCategory,
    PaperOrderTypeShapeCategory OrderTypeShapeCategory,
    PaperTimeInForceShapeCategory TimeInForceShapeCategory,
    PaperOrderCandidateStatus CandidateStatus,
    string NonExecutableReason,
    PaperOrderCandidateRiskReference RiskReviewReference,
    bool PaperOnly,
    bool NonExecutable,
    bool NotAnOrder,
    bool NotSubmitted,
    bool NoBrokerRoute);

public sealed record BlockedPaperReviewLineRecord(
    string SourceRiskReviewLineId,
    InstrumentId InstrumentId,
    string Symbol,
    PaperPreTradeRiskResultCategory Result,
    PaperOmsReviewStatus Status,
    string Reason);

public sealed record PaperOrderCandidateBatchRecord(
    PaperOrderCandidateBatchId PaperOrderCandidateBatchId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    string PaperRiskReviewId,
    DateTimeOffset CreatedAtUtc,
    int CandidateCount,
    int ReadyCandidateCount,
    int BlockedCandidateCount,
    PaperOrderCandidateArchiveBatchStatus BatchStatus,
    IReadOnlyList<PaperOrderCandidateLineRecord> CandidateLines,
    IReadOnlyList<BlockedPaperReviewLineRecord> BlockedLines,
    bool QubesLineagePreserved,
    bool OperatorDecisionLineagePreserved,
    bool RiskLineagePreserved,
    bool RebalanceIntentLineagePreserved,
    bool MissingStaleMarkWarningsPreserved,
    bool DriftAcknowledgementPreserved,
    bool NoExternal,
    bool CreatedOmsOrder,
    bool CreatedParentOrder,
    bool CreatedChildOrder,
    bool CreatedBrokerOrder,
    bool CreatedFill,
    bool CreatedExecutionReport,
    bool SubmittedOrders,
    bool CalledBrokerGateway,
    bool RequestedLiveMarketData,
    bool StartedApiOrWorker,
    bool StartedSchedulerOrBackgroundJob,
    bool MutatedLiveTradingState);

public sealed record PaperOrderCandidateBlotterLine(
    PaperOrderCandidateId CandidateId,
    string CycleRunId,
    string QubesRunId,
    string Instrument,
    IntentSide Side,
    decimal DeltaWeight,
    decimal? DeltaNotional,
    PaperOrderCandidateBlotterStatus Status,
    string NonExecutableReason,
    PaperPreTradeRiskResultCategory RiskReason,
    string MissingStaleMarkWarning,
    string LineageReference,
    bool PaperOnly,
    bool NoOrderCreated,
    bool NoBrokerRoute,
    bool NonExecutable,
    bool NotSubmitted);

public sealed record PaperOrderCandidateBlotter(
    PaperOrderCandidateBatchId BatchId,
    string CycleRunId,
    string QubesRunId,
    OperatorDecisionId OperatorDecisionId,
    IReadOnlyList<PaperOrderCandidateBlotterLine> Lines,
    IReadOnlyList<string> BlockedLineSummaries,
    IReadOnlyList<string> Disclaimers,
    string NextActionRecommendation);

public sealed record PaperOrderCandidateArchiveResult(
    PaperOrderCandidateBatchRecord ArchiveRecord,
    PaperOrderCandidateBlotter Blotter,
    string BlotterMarkdown,
    bool Persisted,
    bool AlreadyArchived);

public interface IPaperOrderCandidateArchiveRepository
{
    Task<PaperOrderCandidateBatchRecord?> GetByBatchIdAsync(
        PaperOrderCandidateBatchId batchId,
        CancellationToken cancellationToken);

    Task AddAsync(PaperOrderCandidateBatchRecord record, CancellationToken cancellationToken);
}

public sealed class InMemoryPaperOrderCandidateArchiveRepository : IPaperOrderCandidateArchiveRepository
{
    private readonly List<PaperOrderCandidateBatchRecord> records = [];

    public Task<PaperOrderCandidateBatchRecord?> GetByBatchIdAsync(
        PaperOrderCandidateBatchId batchId,
        CancellationToken cancellationToken)
        => Task.FromResult(records.FirstOrDefault(x => x.PaperOrderCandidateBatchId == batchId));

    public Task AddAsync(PaperOrderCandidateBatchRecord record, CancellationToken cancellationToken)
    {
        if (records.Any(x => x.PaperOrderCandidateBatchId == record.PaperOrderCandidateBatchId))
        {
            return Task.CompletedTask;
        }

        records.Add(record);
        return Task.CompletedTask;
    }
}

public sealed class PaperOrderCandidateArchiveService(
    IPaperOrderCandidateArchiveRepository repository,
    IClock clock)
{
    public async Task<PaperOrderCandidateArchiveResult> ArchiveAsync(
        PaperOrderCandidateBatch candidateBatch,
        CancellationToken cancellationToken)
    {
        var batchId = CreateBatchId(candidateBatch);
        var existing = await repository.GetByBatchIdAsync(batchId, cancellationToken);
        if (existing is not null)
        {
            var duplicate = existing with { BatchStatus = PaperOrderCandidateArchiveBatchStatus.DuplicateReturned };
            var existingBlotter = PaperOrderCandidateBlotterRenderer.CreateBlotter(duplicate);
            return new PaperOrderCandidateArchiveResult(
                duplicate,
                existingBlotter,
                PaperOrderCandidateBlotterRenderer.RenderMarkdown(existingBlotter),
                Persisted: false,
                AlreadyArchived: true);
        }

        var record = CreateRecord(batchId, candidateBatch, clock.UtcNow);
        if (record.BatchStatus is not PaperOrderCandidateArchiveBatchStatus.RejectedInvalidCandidate)
        {
            await repository.AddAsync(record, cancellationToken);
        }

        var blotter = PaperOrderCandidateBlotterRenderer.CreateBlotter(record);
        return new PaperOrderCandidateArchiveResult(
            record,
            blotter,
            PaperOrderCandidateBlotterRenderer.RenderMarkdown(blotter),
            Persisted: record.BatchStatus is not PaperOrderCandidateArchiveBatchStatus.RejectedInvalidCandidate,
            AlreadyArchived: false);
    }

    private static PaperOrderCandidateBatchId CreateBatchId(PaperOrderCandidateBatch candidateBatch)
        => new($"{candidateBatch.CycleRunId}:{candidateBatch.OperatorDecisionId.Value}:paper-candidate-batch");

    private static PaperOrderCandidateBatchRecord CreateRecord(
        PaperOrderCandidateBatchId batchId,
        PaperOrderCandidateBatch candidateBatch,
        DateTimeOffset createdAtUtc)
    {
        var lines = candidateBatch.Candidates.Select(x => new PaperOrderCandidateLineRecord(
                x.PaperOrderCandidateId,
                x.SourceRebalanceIntentId,
                x.InstrumentId,
                x.NormalizedSymbol,
                x.Side,
                x.TargetWeight,
                x.CurrentWeight,
                x.DeltaWeight,
                x.TargetNotional,
                x.CurrentNotional,
                x.DeltaNotional,
                x.QuantityShapeCategory,
                x.OrderTypeShapeCategory,
                x.TimeInForceShapeCategory,
                x.CandidateStatus,
                x.NonExecutableReason,
                x.RiskReviewReference,
                x.PaperOnly,
                x.NonExecutable,
                x.NotAnOrder,
                x.NotSubmitted,
                x.NoBrokerRoute))
            .ToArray();
        var blocked = candidateBatch.BlockedLines.Select(x => new BlockedPaperReviewLineRecord(
                x.SourceRiskReviewLineId,
                x.InstrumentId,
                x.Symbol,
                x.Result,
                x.Status,
                x.Reason))
            .ToArray();
        var invalidCandidate = candidateBatch.Candidates.Any(x =>
            !x.PaperOnly ||
            !x.NonExecutable ||
            !x.NotAnOrder ||
            !x.NotSubmitted ||
            !x.NoBrokerRoute ||
            x.CreatesOmsOrder ||
            x.CreatesParentOrder ||
            x.CreatesChildOrder ||
            x.CreatesBrokerOrder ||
            x.CreatesFill ||
            x.CreatesExecutionReport);
        var status = invalidCandidate
            ? PaperOrderCandidateArchiveBatchStatus.RejectedInvalidCandidate
            : blocked.Length > 0
                ? PaperOrderCandidateArchiveBatchStatus.ArchivedWithBlockedLines
                : PaperOrderCandidateArchiveBatchStatus.ArchivedNoExternal;

        return new PaperOrderCandidateBatchRecord(
            batchId,
            candidateBatch.CycleRunId,
            candidateBatch.QubesRunId,
            candidateBatch.OperatorDecisionId,
            $"{candidateBatch.CycleRunId}:{candidateBatch.OperatorDecisionId.Value}:paper-risk-review",
            createdAtUtc,
            lines.Length,
            lines.Count(x => x.CandidateStatus is PaperOrderCandidateStatus.PaperCandidateReady or PaperOrderCandidateStatus.PaperCandidateRequiresLotSizing),
            blocked.Length,
            status,
            lines,
            blocked,
            candidateBatch.QubesLineagePreserved,
            candidateBatch.OperatorDecisionLineagePreserved,
            candidateBatch.RiskLineagePreserved,
            candidateBatch.RebalanceIntentLineagePreserved,
            candidateBatch.MissingStaleMarkWarningsPreserved,
            candidateBatch.DriftAcknowledgementPreserved,
            candidateBatch.CandidatesAreNonExecutable &&
                candidateBatch.CandidatesAreNotOrders &&
                candidateBatch.CandidatesAreNotSubmitted &&
                candidateBatch.CandidatesHaveNoBrokerRoute &&
                !candidateBatch.CreatedOmsOrder &&
                !candidateBatch.CreatedParentOrder &&
                !candidateBatch.CreatedChildOrder &&
                !candidateBatch.CreatedBrokerOrder &&
                !candidateBatch.CreatedFill &&
                !candidateBatch.CreatedExecutionReport &&
                !candidateBatch.SubmittedOrders &&
                !candidateBatch.CalledBrokerGateway &&
                !candidateBatch.RequestedLiveMarketData &&
                !candidateBatch.StartedApiOrWorker &&
                !candidateBatch.StartedSchedulerOrBackgroundJob &&
                !candidateBatch.MutatedLiveTradingState,
            CreatedOmsOrder: false,
            CreatedParentOrder: false,
            CreatedChildOrder: false,
            CreatedBrokerOrder: false,
            CreatedFill: false,
            CreatedExecutionReport: false,
            SubmittedOrders: false,
            CalledBrokerGateway: false,
            RequestedLiveMarketData: false,
            StartedApiOrWorker: false,
            StartedSchedulerOrBackgroundJob: false,
            MutatedLiveTradingState: false);
    }
}

public static class PaperOrderCandidateBlotterRenderer
{
    public static PaperOrderCandidateBlotter CreateBlotter(PaperOrderCandidateBatchRecord record)
    {
        var lines = record.CandidateLines
            .OrderBy(x => x.NormalizedSymbol, StringComparer.OrdinalIgnoreCase)
            .Select(x => new PaperOrderCandidateBlotterLine(
                x.PaperOrderCandidateId,
                record.CycleRunId,
                record.QubesRunId,
                x.NormalizedSymbol,
                x.Side,
                x.DeltaWeight,
                x.DeltaNotional,
                ToBlotterStatus(x),
                x.NonExecutableReason,
                x.RiskReviewReference.RiskResult,
                MissingStaleWarningFor(x),
                $"intent={x.SourceRebalanceIntentId}; risk={x.RiskReviewReference.SourceRiskReviewLineId}",
                x.PaperOnly,
                x.NotAnOrder,
                x.NoBrokerRoute,
                x.NonExecutable,
                x.NotSubmitted))
            .ToArray();

        return new PaperOrderCandidateBlotter(
            record.PaperOrderCandidateBatchId,
            record.CycleRunId,
            record.QubesRunId,
            record.OperatorDecisionId,
            lines,
            record.BlockedLines
                .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
                .Select(x => $"{x.Symbol}: {x.Result} ({x.Reason})")
                .ToArray(),
            [
                "Paper-only candidate archive.",
                "No order created.",
                "No broker route exists.",
                "Candidates are not executable.",
                "Candidates were not submitted."
            ],
            "Proceed to R012 paper lot-sizing and instrument convention work using no-external fixture conventions only.");
    }

    public static string RenderMarkdown(PaperOrderCandidateBlotter blotter)
    {
        var rows = blotter.Lines.Count == 0
            ? "| None | | | | | | | | | | | | | | |"
            : string.Join(Environment.NewLine, blotter.Lines.Select(x => string.Join(" | ", [
                $"| {x.CandidateId.Value}",
                x.CycleRunId,
                x.QubesRunId,
                x.Instrument,
                x.Side.ToString(),
                FormatDecimal(x.DeltaWeight),
                FormatNullable(x.DeltaNotional),
                x.Status.ToString(),
                x.RiskReason.ToString(),
                x.MissingStaleMarkWarning,
                x.PaperOnly.ToString(CultureInfo.InvariantCulture),
                x.NoOrderCreated.ToString(CultureInfo.InvariantCulture),
                x.NoBrokerRoute.ToString(CultureInfo.InvariantCulture),
                x.NonExecutable.ToString(CultureInfo.InvariantCulture),
                $"{x.NotSubmitted.ToString(CultureInfo.InvariantCulture)} |"
            ])));
        var blocked = blotter.BlockedLineSummaries.Count == 0
            ? "- None."
            : string.Join(Environment.NewLine, blotter.BlockedLineSummaries.Select(x => $"- {x}"));
        var disclaimers = string.Join(Environment.NewLine, blotter.Disclaimers.Select(x => $"- {x}"));

        return string.Join(Environment.NewLine, [
            "# Paper Order Candidate Blotter",
            "",
            $"BatchId: {blotter.BatchId.Value}",
            $"CycleRunId: {blotter.CycleRunId}",
            $"QubesRunId: {blotter.QubesRunId}",
            $"OperatorDecisionId: {blotter.OperatorDecisionId.Value}",
            "",
            "## Candidate Lines",
            "| CandidateId | Cycle | Qubes | Instrument | Side | DeltaWeight | DeltaNotional | Status | RiskReason | Missing/Stale Warning | PaperOnly | NoOrderCreated | NoBrokerRoute | NonExecutable | NotSubmitted |",
            "| --- | --- | --- | --- | --- | ---: | ---: | --- | --- | --- | --- | --- | --- | --- | --- |",
            rows,
            "",
            "## Blocked R009 Lines",
            blocked,
            "",
            "## No-External / No-Order Disclaimer",
            disclaimers,
            "",
            "## Next Action",
            blotter.NextActionRecommendation,
            ""
        ]);
    }

    private static PaperOrderCandidateBlotterStatus ToBlotterStatus(PaperOrderCandidateLineRecord line)
        => line.CandidateStatus switch
        {
            PaperOrderCandidateStatus.PaperCandidateReady => PaperOrderCandidateBlotterStatus.PaperReady,
            PaperOrderCandidateStatus.PaperCandidateRequiresLotSizing => PaperOrderCandidateBlotterStatus.RequiresLotSizing,
            PaperOrderCandidateStatus.PaperCandidateRequiresMark => PaperOrderCandidateBlotterStatus.BlockedMissingMark,
            PaperOrderCandidateStatus.PaperCandidateBlocked => PaperOrderCandidateBlotterStatus.BlockedByRisk,
            PaperOrderCandidateStatus.PaperCandidateNotExecutable => PaperOrderCandidateBlotterStatus.NonExecutable,
            _ => PaperOrderCandidateBlotterStatus.InconclusiveSafe
        };

    private static string MissingStaleWarningFor(PaperOrderCandidateLineRecord line)
        => line.CandidateStatus is PaperOrderCandidateStatus.PaperCandidateRequiresMark
            ? "Missing or stale mark prevents sizing; preserved from paper risk review."
            : "None on accepted candidate; blocked R009 missing/stale rows are preserved separately.";

    private static string FormatDecimal(decimal value)
        => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string FormatNullable(decimal? value)
        => value.HasValue ? value.Value.ToString("0.##", CultureInfo.InvariantCulture) : "";
}
