using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public interface IQubesWeightAuditRepository
{
    Task<QubesWeightAuditBatch?> GetByRunIdAsync(string qubesRunId, CancellationToken cancellationToken);
    Task<IReadOnlyList<QubesRawWeightAuditRow>> GetRawRowsAsync(QubesWeightAuditBatchId auditBatchId, CancellationToken cancellationToken);
    Task<IReadOnlyList<QubesNormalizedWeightAuditRow>> GetNormalizedRowsAsync(QubesWeightAuditBatchId auditBatchId, CancellationToken cancellationToken);
    Task AddAsync(QubesWeightAuditBatch batch, IReadOnlyList<QubesRawWeightAuditRow> rawRows, IReadOnlyList<QubesNormalizedWeightAuditRow> normalizedRows, CancellationToken cancellationToken);
}

public sealed record PersistQubesWeightsRequest(
    QubesFxWeightsIngestionResult Ingestion,
    ModelWeightBatch? ModelWeightBatch,
    ModelWeightPromotionResult? Promotion,
    IReadOnlyList<TargetWeight> TargetWeights);

public sealed record PersistQubesWeightsResult(
    QubesWeightAuditBatch AuditBatch,
    IReadOnlyList<QubesRawWeightAuditRow> RawRows,
    IReadOnlyList<QubesNormalizedWeightAuditRow> NormalizedRows,
    bool Persisted,
    bool AlreadyPersisted);

public sealed class QubesWeightPersistenceService(IQubesWeightAuditRepository repository, IClock clock)
{
    public async Task<PersistQubesWeightsResult?> PersistAsync(PersistQubesWeightsRequest request, CancellationToken cancellationToken)
    {
        var ingestion = request.Ingestion;
        QubesProductionBoundaryGuard.EnsureFixtureIngestionBlockedForProductionAccounting(ingestion);

        if (!ingestion.Succeeded || ingestion.ModelWeightBatchRequest is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(ingestion.QubesRunId.Value))
        {
            return null;
        }

        var existing = await repository.GetByRunIdAsync(ingestion.QubesRunId.Value, cancellationToken);
        if (existing is not null)
        {
            return new PersistQubesWeightsResult(
                existing,
                await repository.GetRawRowsAsync(existing.Id, cancellationToken),
                await repository.GetNormalizedRowsAsync(existing.Id, cancellationToken),
                Persisted: false,
                AlreadyPersisted: true);
        }

        var now = clock.UtcNow;
        var auditBatch = new QubesWeightAuditBatch(
            QubesWeightAuditBatchId.New(),
            ingestion.QubesRunId.Value,
            ingestion.SourceSystem,
            ingestion.ProducedAtUtc,
            ingestion.EffectiveAtUtc,
            ingestion.CadenceMinutes,
            ingestion.RawInputRowCount,
            ingestion.NormalizedOutputRowCount,
            request.ModelWeightBatch?.Id,
            request.Promotion?.ModelRunId,
            now);
        var rawRows = ingestion.RawRows
            .Select(row => new QubesRawWeightAuditRow(
                QubesRawWeightAuditRowId.New(),
                auditBatch.Id,
                row.RowNumber,
                row.BloombergTicker,
                row.Pair,
                row.BaseCurrency,
                row.QuoteCurrency,
                row.Weight,
                now))
            .ToArray();
        var targetByInstrument = request.TargetWeights.ToDictionary(x => x.InstrumentId);
        var normalizedRows = ingestion.NormalizedWeights
            .Select(weight =>
            {
                var target = request.TargetWeights.FirstOrDefault(x => x.RawSecurityId.Equals(weight.BloombergTicker, StringComparison.OrdinalIgnoreCase));
                var targetInstrumentId = target == default ? (InstrumentId?)null : target.InstrumentId;
                return new QubesNormalizedWeightAuditRow(
                    QubesNormalizedWeightAuditRowId.New(),
                    auditBatch.Id,
                    weight.BloombergTicker,
                    weight.Symbol,
                    weight.Currency,
                    weight.Weight,
                    request.ModelWeightBatch?.Id,
                    request.Promotion?.ModelRunId,
                    targetInstrumentId is not null && targetByInstrument.ContainsKey(targetInstrumentId.Value) ? targetInstrumentId : null,
                    request.Promotion?.Succeeded == true ? "Promoted" : request.ModelWeightBatch?.Status.ToString() ?? "Mapped",
                    now);
            })
            .ToArray();

        await repository.AddAsync(auditBatch, rawRows, normalizedRows, cancellationToken);
        return new PersistQubesWeightsResult(auditBatch, rawRows, normalizedRows, Persisted: true, AlreadyPersisted: false);
    }
}

public sealed class InMemoryQubesWeightAuditRepository : IQubesWeightAuditRepository
{
    private readonly List<QubesWeightAuditBatch> batches = [];
    private readonly List<QubesRawWeightAuditRow> rawRows = [];
    private readonly List<QubesNormalizedWeightAuditRow> normalizedRows = [];

    public Task<QubesWeightAuditBatch?> GetByRunIdAsync(string qubesRunId, CancellationToken cancellationToken)
        => Task.FromResult(batches.FirstOrDefault(x => x.QubesRunId.Equals(qubesRunId, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<QubesRawWeightAuditRow>> GetRawRowsAsync(QubesWeightAuditBatchId auditBatchId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<QubesRawWeightAuditRow>>(rawRows.Where(x => x.AuditBatchId == auditBatchId).OrderBy(x => x.RowNumber).ToList());

    public Task<IReadOnlyList<QubesNormalizedWeightAuditRow>> GetNormalizedRowsAsync(QubesWeightAuditBatchId auditBatchId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<QubesNormalizedWeightAuditRow>>(normalizedRows.Where(x => x.AuditBatchId == auditBatchId).OrderBy(x => x.Symbol).ToList());

    public Task AddAsync(QubesWeightAuditBatch batch, IReadOnlyList<QubesRawWeightAuditRow> rows, IReadOnlyList<QubesNormalizedWeightAuditRow> normalized, CancellationToken cancellationToken)
    {
        if (batches.Any(x => x.QubesRunId.Equals(batch.QubesRunId, StringComparison.OrdinalIgnoreCase)))
        {
            return Task.CompletedTask;
        }

        batches.Add(batch);
        rawRows.AddRange(rows);
        normalizedRows.AddRange(normalized);
        return Task.CompletedTask;
    }
}
