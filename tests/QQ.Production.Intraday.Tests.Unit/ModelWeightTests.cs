using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ModelWeightTests
{
    private static readonly DateTimeOffset Now = new(2026, 04, 30, 10, 00, 00, TimeSpan.Zero);

    [Fact]
    public async Task Valid_fake_batch_validates()
    {
        var services = CreateServices();
        var batch = await services.Generator.CreateFakeBatchAsync(DefaultRequest("batch-valid"), CancellationToken.None);

        var result = await services.Promotion.ValidateBatchAsync(batch.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(ModelWeightBatchStatus.Accepted, result.Status);
        Assert.Equal(0, result.ValidationIssueCount);
    }

    [Fact]
    public async Task Missing_rows_rejects()
    {
        var services = CreateServices();
        var batch = await services.Generator.CreateFakeBatchAsync(DefaultRequest("batch-empty") with { Weights = [] }, CancellationToken.None);

        var result = await services.Promotion.ValidateBatchAsync(batch.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, x => x.IssueType == ModelWeightValidationIssueType.MissingRows);
    }

    [Fact]
    public async Task Invalid_nav_rejects()
    {
        var services = CreateServices();
        var batch = await services.Generator.CreateFakeBatchAsync(DefaultRequest("batch-bad-nav") with { NavUsd = -1m }, CancellationToken.None);

        var result = await services.Promotion.ValidateBatchAsync(batch.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, x => x.IssueType == ModelWeightValidationIssueType.InvalidNav);
    }

    [Fact]
    public async Task Unknown_symbol_rejects()
    {
        var services = CreateServices();
        var batch = await services.Generator.CreateFakeBatchAsync(DefaultRequest("batch-unknown") with { Weights = [new("GBPUSD", "GBPUSD", 0.1m)] }, CancellationToken.None);

        var result = await services.Promotion.ValidateBatchAsync(batch.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, x => x.IssueType == ModelWeightValidationIssueType.UnknownInstrument);
    }

    [Fact]
    public async Task Duplicate_symbol_rejects()
    {
        var services = CreateServices();
        var batch = await services.Generator.CreateFakeBatchAsync(DefaultRequest("batch-duplicate-symbol") with
        {
            Weights =
            [
                new("EURUSD_A", "EURUSD", -0.1m),
                new("EURUSD_B", "EURUSD", -0.2m)
            ]
        }, CancellationToken.None);

        var result = await services.Promotion.ValidateBatchAsync(batch.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, x => x.IssueType == ModelWeightValidationIssueType.DuplicateSecurity);
    }

    [Fact]
    public async Task Reference_integrity_blocking_issue_rejects()
    {
        var services = CreateServices();
        var mapping = services.State.VenueInstrumentMappings.Single();
        services.State.VenueInstrumentMappings.Add(mapping with { Id = VenueInstrumentId.New() });
        var batch = await services.Generator.CreateFakeBatchAsync(DefaultRequest("batch-bad-ref"), CancellationToken.None);

        var result = await services.Promotion.ValidateBatchAsync(batch.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Issues, x => x.IssueType == ModelWeightValidationIssueType.ReferenceDataInvalid);
    }

    [Fact]
    public async Task Promotion_creates_unprocessed_model_run_and_target_weight()
    {
        var services = CreateServices();
        var batch = await services.Generator.CreateFakeBatchAsync(DefaultRequest("batch-promote"), CancellationToken.None);

        var result = await services.Promotion.PromoteBatchAsync(batch.Id, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.ModelRunId);
        var run = services.State.ModelRuns.Single(x => x.Id == result.ModelRunId);
        Assert.False(run.IsProcessed);
        Assert.Equal("db-weight-source", run.SourceFileName);
        Assert.Contains(services.State.TargetWeights, x => x.ModelRunId == run.Id && x.RawSecurityId == "EURUSD");
        Assert.DoesNotContain(services.State.ParentOrders, x => services.State.TradeIntents.Any(t => t.Id == x.TradeIntentId && t.ModelRunId == run.Id));
    }

    [Fact]
    public async Task Promoted_batch_is_idempotent()
    {
        var services = CreateServices();
        var batch = await services.Generator.CreateFakeBatchAsync(DefaultRequest("batch-idempotent"), CancellationToken.None);
        var first = await services.Promotion.PromoteBatchAsync(batch.Id, CancellationToken.None);

        var second = await services.Promotion.PromoteBatchAsync(batch.Id, CancellationToken.None);

        Assert.True(second.Succeeded);
        Assert.True(second.AlreadyPromoted);
        Assert.Equal(first.ModelRunId, second.ModelRunId);
        Assert.Equal(1, services.State.ModelRuns.Count(x => x.Id == first.ModelRunId));
    }

    [Fact]
    public async Task Same_external_batch_id_with_same_payload_is_idempotent()
    {
        var services = CreateServices();
        var first = await services.Generator.CreateFakeBatchAsync(DefaultRequest("batch-same"), CancellationToken.None);

        var second = await services.Generator.CreateFakeBatchAsync(DefaultRequest("batch-same"), CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(services.State.ModelWeightBatches, x => x.ExternalBatchId == "batch-same");
    }

    private static CreateFakeModelWeightBatchRequest DefaultRequest(string externalBatchId)
        => new(
            externalBatchId,
            ModelWeightSourceSystem.Fake,
            "QQ_MASTER",
            "IntradayFxModel",
            Now,
            Now,
            15,
            1_000_000m,
            TargetQuantityMode.PortfolioBaseCurrencyNotional,
            ModelWeightBatchStatus.Ready,
            [new("EURUSD", "EURUSD", -0.10m)]);

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(Now);
        var clock = new FixedClock(Now);
        var intradayRepository = new InMemoryIntradayRepository(state);
        var batchRepository = new InMemoryModelWeightBatchRepository(state);
        var integrity = new ReferenceDataIntegrityService(intradayRepository, clock);
        return new TestServices(state, new FakeModelWeightGenerator(batchRepository, clock), new ModelWeightPromotionService(batchRepository, intradayRepository, integrity, clock));
    }

    private sealed record TestServices(PlatformState State, IFakeModelWeightGenerator Generator, IModelWeightPromotionService Promotion);
}
