using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

/// <summary>
/// Executes one already-materialized, Demo-only Intraday FX cycle.  The caller owns
/// capture, transfer, V1 and the static ExecDesk writer; this service deliberately
/// consumes their immutable outputs and never scans or promotes a shared queue.
/// </summary>
public sealed record LmaxDemoCycleCoordinatorRequest(
    string CycleId,
    LmaxCanonicalSnapshotIngestionRequest CanonicalSnapshot,
    LegacyAnubisPortfolioWeightIngestionRequest PortfolioWeights);

public sealed record LmaxDemoCycleCoordinatorResult(
    string CycleId,
    LmaxCanonicalSnapshotIngestionResult CanonicalSnapshot,
    LegacyAnubisPortfolioWeightIngestionResult PortfolioWeights,
    ModelWeightPromotionResult Validation,
    ModelWeightPromotionResult? Promotion,
    ProcessModelRunResult? Processing);

public interface ILmaxDemoCycleCoordinator
{
    Task<LmaxDemoCycleCoordinatorResult> RunAsync(
        LmaxDemoCycleCoordinatorRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// The missing caller between the validated LMAX capture / V1 artifacts and the
/// existing PR #91 ingestion, targeted promotion and targeted Worker processing.
/// Bulk ready-batch promotion is intentionally not used here.
/// </summary>
public sealed class LmaxDemoCycleCoordinator(
    ILmaxCanonicalSnapshotIngestionService canonicalSnapshotIngestion,
    ILegacyAnubisPortfolioWeightIngestionService portfolioWeightIngestion,
    IModelWeightPromotionService modelWeightPromotion,
    ProcessModelRunService processModelRunService) : ILmaxDemoCycleCoordinator
{
    public async Task<LmaxDemoCycleCoordinatorResult> RunAsync(
        LmaxDemoCycleCoordinatorRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var canonicalSnapshot = await canonicalSnapshotIngestion.IngestAsync(
            request.CanonicalSnapshot,
            cancellationToken);
        var portfolioWeights = await portfolioWeightIngestion.IngestAsync(
            request.PortfolioWeights,
            cancellationToken);

        // Keep the exact batch identity returned by PR #91.  Do not inspect or
        // promote any other ready batch.
        var validation = await modelWeightPromotion.ValidateBatchAsync(
            portfolioWeights.Batch.Id,
            cancellationToken);
        if (!validation.Succeeded)
        {
            return new LmaxDemoCycleCoordinatorResult(
                request.CycleId,
                canonicalSnapshot,
                portfolioWeights,
                validation,
                null,
                null);
        }

        var promotion = await modelWeightPromotion.PromoteBatchAsync(
            portfolioWeights.Batch.Id,
            cancellationToken);
        if (!promotion.Succeeded || promotion.ModelRunId is null)
        {
            return new LmaxDemoCycleCoordinatorResult(
                request.CycleId,
                canonicalSnapshot,
                portfolioWeights,
                validation,
                promotion,
                null);
        }

        // Process only the ModelRun that was created by the exact targeted batch.
        // ProcessNextAsync would be able to consume an unrelated historical run.
        var processing = await processModelRunService.ProcessAsync(
            promotion.ModelRunId.Value,
            cancellationToken);
        return new LmaxDemoCycleCoordinatorResult(
            request.CycleId,
            canonicalSnapshot,
            portfolioWeights,
            validation,
            promotion,
            processing);
    }

    private static void Validate(LmaxDemoCycleCoordinatorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CycleId))
        {
            throw new DomainRuleViolationException("LMAX Demo cycle id is required.");
        }

        if (request.CanonicalSnapshot.DecisionAtUtc != request.PortfolioWeights.DecisionAtUtc)
        {
            throw new DomainRuleViolationException(
                "LMAX Demo cycle capture and portfolio decision timestamps must be identical.");
        }
    }
}
