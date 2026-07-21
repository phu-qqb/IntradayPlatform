using System.Reflection;
using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch6dPmsShadowSessionImporterTests
{
    [Fact]
    public async Task ImportsCompleteArch6bPlanAndReturnsExpectedRowset()
    {
        var plan = Plan();
        var outcome = await Importer().ImportAsync(plan, Request());

        Assert.Equal(PmsShadowApplyResult.Applied, outcome.Result);
        Assert.Equal(EfPmsShadowSessionImportStore.ExpectedRowCounts(plan).Values.Sum(),
            outcome.RowCounts.Values.Sum());
        Assert.Equal(4, outcome.RowCounts["model_runs"]);
        Assert.Equal(288, outcome.RowCounts["target_weights"]);
        Assert.Equal(288, outcome.RowCounts["target_positions"]);
        Assert.Equal(288, outcome.RowCounts["position_only_drifts"]);
    }

    [Fact]
    public async Task IdenticalReplayIsIdempotent()
    {
        var store = new FakeStore();
        var importer = Importer(store);
        var plan = Plan();

        var first = await importer.ImportAsync(plan, Request());
        var replay = await importer.ImportAsync(plan, Request());

        Assert.Equal(PmsShadowApplyResult.Applied, first.Result);
        Assert.Equal(PmsShadowApplyResult.AlreadyAppliedIdentical, replay.Result);
        Assert.Equal(first.RowCounts, replay.RowCounts);
    }

    [Fact]
    public async Task ConcurrentIdenticalImportsProduceOneApplyAndOneReplay()
    {
        var importer = Importer(new FakeStore());
        var plan = Plan();
        var outcomes = await Task.WhenAll(
            importer.ImportAsync(plan, Request()),
            importer.ImportAsync(plan, Request()));

        Assert.Single(outcomes, x => x.Result == PmsShadowApplyResult.Applied);
        Assert.Single(outcomes, x => x.Result == PmsShadowApplyResult.AlreadyAppliedIdentical);
    }

    [Theory]
    [InlineData("PRODUCTION", true, "postgresql_pms_shadow_state_contract_v1", "PMS_SHADOW_IMPORT_REQUIRES_TEST_ENVIRONMENT")]
    [InlineData("TEST", false, "postgresql_pms_shadow_state_contract_v1", "PMS_SHADOW_IMPORT_REQUIRES_NO_ORDER")]
    [InlineData("TEST", true, "wrong", "PMS_SHADOW_SCHEMA_CONTRACT_VERSION_MISMATCH")]
    public async Task RejectsUnsafeRequest(string environment, bool noOrder, string contract, string issue)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Importer().ImportAsync(Plan(), new(environment, noOrder, contract)));
        Assert.Equal(issue, exception.Message);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Unknown.Provider")]
    public async Task RejectsNonPostgreSqlProvider(string provider)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Importer(new FakeStore(provider)).ImportAsync(Plan(), Request()));
        Assert.Equal("POSTGRESQL_PROVIDER_REQUIRED", exception.Message);
    }

    [Fact]
    public async Task RejectsWhenExactMigrationIsAbsent()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Importer(new FakeStore(migrations: [])).ImportAsync(Plan(), Request()));
        Assert.Equal("EXPECTED_PMS_SHADOW_MIGRATION_NOT_APPLIED", exception.Message);
    }

    [Fact]
    public async Task InterruptedImportLeavesNoSessionAndCanBeRetried()
    {
        var store = new FakeStore { InterruptNext = true };
        var importer = Importer(store);
        await Assert.ThrowsAsync<InvalidOperationException>(() => importer.ImportAsync(Plan(), Request()));

        var outcome = await importer.ImportAsync(Plan(), Request());
        Assert.Equal(PmsShadowApplyResult.Applied, outcome.Result);
    }

    [Theory]
    [InlineData("real_account")]
    [InlineData("execution_allowed")]
    [InlineData("trade_intent")]
    [InlineData("order_entry")]
    [InlineData("timestamp")]
    [InlineData("working_leaves")]
    public async Task RejectsUnsafeOrInvalidPlan(string mutation)
    {
        var plan = Plan();
        plan = mutation switch
        {
            "real_account" => plan with { AccountSnapshot = plan.AccountSnapshot with { AccountId = "921640160" } },
            "execution_allowed" => plan with { ModelRuns = plan.ModelRuns.Select((x, i) => i == 0 ? x with { ExecutionAllowed = true } : x).ToArray() },
            "trade_intent" => plan with { CycleResults = plan.CycleResults.Select((x, i) => i == 0 ? x with { TradeIntentCount = 1 } : x).ToArray() },
            "order_entry" => plan with { CycleResults = plan.CycleResults.Select((x, i) => i == 0 ? x with { OrderEntryEnabled = true } : x).ToArray() },
            "timestamp" => plan with { Ingestion = plan.Ingestion with { StartedAtUtc = plan.Ingestion.StartedAtUtc.ToOffset(TimeSpan.FromHours(2)) } },
            "working_leaves" => plan with { WorkingLeavesObservation = plan.WorkingLeavesObservation with { EmptyStateInferred = true } },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => Importer().ImportAsync(plan, Request()));
    }

    [Fact]
    public void DbContextAllowsOnlyTerminalIngestionTransition()
    {
        using var context = Context();
        var row = Plan().Ingestion with { Status = PmsShadowIngestionStatuses.Applying, CompletedAtUtc = null };
        context.Attach(row);
        context.Entry(row).Property(x => x.Status).CurrentValue = PmsShadowIngestionStatuses.Completed;
        context.Entry(row).Property(x => x.CompletedAtUtc).CurrentValue = Plan().Ingestion.CompletedAtUtc;

        InvokeMutationGuard(context);
    }

    [Fact]
    public void DbContextRejectsFactUpdateAndDelete()
    {
        using var updateContext = Context();
        var model = Plan().ModelRuns[0];
        updateContext.Attach(model);
        updateContext.Entry(model).Property(x => x.OutputSha256).CurrentValue = new string('a', 64);
        AssertGuardRejected(updateContext);

        using var deleteContext = Context();
        deleteContext.Attach(model);
        deleteContext.Remove(model);
        AssertGuardRejected(deleteContext);
    }

    private static Arch6bPmsShadowSessionImporter Importer(FakeStore? store = null) => new(store ?? new());
    private static PmsShadowPersistencePlan Plan() => Arch6cPostgreSqlPmsShadowStateTests.BuildPlan();
    private static PmsShadowImportRequest Request() => new("TEST", true, PmsShadowStateContract.ContractVersion);
    private static PmsShadowDbContext Context() => new PmsShadowDesignTimeDbContextFactory().CreateDbContext([]);

    private static void InvokeMutationGuard(PmsShadowDbContext context) =>
        typeof(PmsShadowDbContext).GetMethod("RejectMutations", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(context, null);

    private static void AssertGuardRejected(PmsShadowDbContext context)
    {
        var exception = Assert.Throws<TargetInvocationException>(() => InvokeMutationGuard(context));
        Assert.Equal("PMS_SHADOW_FACTS_ARE_APPEND_ONLY", exception.InnerException?.Message);
    }

    private sealed class FakeStore(
        string provider = "Npgsql.EntityFrameworkCore.PostgreSQL",
        IReadOnlyList<string>? migrations = null) : IPmsShadowSessionImportStore
    {
        private readonly InMemoryPmsShadowAtomicIngestionRegistry registry = new();
        public bool InterruptNext { get; set; }

        public Task<PmsShadowStorePreflight> InspectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PmsShadowStorePreflight(provider,
                migrations ?? [PmsShadowStateContract.MigrationId]));

        public Task<PmsShadowImportOutcome> ImportAtomicallyAsync(
            PmsShadowPersistencePlan plan,
            CancellationToken cancellationToken = default)
        {
            var interrupt = InterruptNext;
            InterruptNext = false;
            var result = registry.Apply(plan, interrupt);
            return Task.FromResult(new PmsShadowImportOutcome(result, plan.Ingestion.IngestionId,
                plan.Ingestion.SourceSessionId, plan.Ingestion.SourceEvidenceSha256, plan.RowsetSha256,
                EfPmsShadowSessionImportStore.ExpectedRowCounts(plan)));
        }
    }
}
