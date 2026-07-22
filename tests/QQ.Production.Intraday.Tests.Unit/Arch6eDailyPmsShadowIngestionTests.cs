using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch6eDailyPmsShadowIngestionTests
{
    [Fact]
    public void CompleteHandoffIsReadyToImport()
    {
        var validation = Validate();
        Assert.True(validation.IsValid);
        Assert.Equal(PmsShadowDailyIngestionStatus.ReadyToImport, validation.Status);
    }

    [Theory]
    [InlineData("evidence", "EVIDENCE_MANIFEST_NOT_FINALIZED")]
    [InlineData("runs", "REQUIRED_RUNS_NOT_FINALIZED")]
    [InlineData("outputs", "OUTPUTS_NOT_TRANSFERRED")]
    [InlineData("downstream", "DOWNSTREAM_SHADOW_NOT_FINALIZED")]
    [InlineData("calculation", "CALCULATION_SESSION_NOT_COMPLETED")]
    public void IncompleteSourceIsBlocked(string mutation, string issue)
    {
        var request = Request();
        request = mutation switch
        {
            "evidence" => request with { EvidenceManifestFinalized = false },
            "runs" => request with { FourRequiredRunsFinalized = false },
            "outputs" => request with { OutputsTransferred = false },
            "downstream" => request with { DownstreamShadowFinalized = false },
            "calculation" => request with { CalculationSessionCompleted = false },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        var validation = Validate(request);
        Assert.Equal(PmsShadowDailyIngestionStatus.BlockedIncompleteSource, validation.Status);
        Assert.Contains(issue, validation.Issues);
    }

    [Fact]
    public void EvidenceHashMismatchFailsClosed()
    {
        var validation = Validate(Request() with { EvidenceManifestSha256 = Hash('f') });
        Assert.Equal(PmsShadowDailyIngestionStatus.FailedClosed, validation.Status);
        Assert.Contains("EVIDENCE_HASH_MISMATCH", validation.Issues);
    }

    [Fact]
    public void NoGoSourceIsBlocked()
    {
        var validation = Validate(Request() with { SourceDecision = "NO_GO_ARCH6B_FAILURE" });
        Assert.Equal(PmsShadowDailyIngestionStatus.BlockedIncompleteSource, validation.Status);
        Assert.Contains("SOURCE_SESSION_NO_GO", validation.Issues);
    }

    [Fact]
    public void MissingStrategyIsBlocked()
    {
        var request = Request();
        request = request with { ModelRunIds = request.ModelRunIds.Take(3).ToArray() };
        var validation = Validate(request);
        Assert.Contains("FOUR_REQUIRED_MODEL_RUNS_MISSING", validation.Issues);
        Assert.Contains("MODEL_RUN_SET_MISMATCH", validation.Issues);
    }

    [Fact]
    public void IdempotencyKeyIsDeterministicAndPathIndependent()
    {
        var request = Request();
        var first = PmsShadowDailyIngestionContract.CreateIdempotencyKey(request.SourceSessionId,
            request.EvidenceZipSha256);
        var second = PmsShadowDailyIngestionContract.CreateIdempotencyKey(request.SourceSessionId,
            request.EvidenceZipSha256);
        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public async Task DoubleTriggerReturnsCompletedThenAlreadyApplied()
    {
        var coordinator = Coordinator(new FakeStore());
        var first = await coordinator.CoordinateAsync(Request(), Package());
        var second = await coordinator.CoordinateAsync(Request(), Package());
        Assert.Equal(PmsShadowDailyIngestionStatus.Completed, first.Status);
        Assert.Equal(PmsShadowDailyIngestionStatus.AlreadyAppliedIdentical, second.Status);
    }

    [Fact]
    public async Task RestartBeforeImportRetriesNormally()
    {
        var store = new FakeStore { InterruptBeforeCommit = true };
        var coordinator = Coordinator(store);
        Assert.Equal(PmsShadowDailyIngestionStatus.FailedClosed,
            (await coordinator.CoordinateAsync(Request(), Package())).Status);
        Assert.Equal(PmsShadowDailyIngestionStatus.Completed,
            (await coordinator.CoordinateAsync(Request(), Package())).Status);
    }

    [Fact]
    public async Task RestartAfterCommitReturnsAlreadyApplied()
    {
        var store = new FakeStore { InterruptAfterCommit = true };
        var coordinator = Coordinator(store);
        Assert.Equal(PmsShadowDailyIngestionStatus.FailedClosed,
            (await coordinator.CoordinateAsync(Request(), Package())).Status);
        Assert.Equal(PmsShadowDailyIngestionStatus.AlreadyAppliedIdentical,
            (await coordinator.CoordinateAsync(Request(), Package())).Status);
    }

    [Fact]
    public async Task ConcurrentCoordinatorsProduceOneImport()
    {
        var store = new FakeStore();
        var outcomes = await Task.WhenAll(Coordinator(store).CoordinateAsync(Request(), Package()),
            Coordinator(store).CoordinateAsync(Request(), Package()));
        Assert.Single(outcomes, value => value.Status == PmsShadowDailyIngestionStatus.Completed);
        Assert.Single(outcomes, value => value.Status == PmsShadowDailyIngestionStatus.AlreadyAppliedIdentical);
    }

    [Fact]
    public async Task ConflictingSessionFailsClosed()
    {
        var outcome = await Coordinator(new FakeStore { Conflict = true })
            .CoordinateAsync(Request(), Package());
        Assert.Equal(PmsShadowDailyIngestionStatus.FailedClosed, outcome.Status);
        Assert.Contains(outcome.Alerts, value => value.Code == "INGESTION_FAILED_CLOSED");
    }

    [Fact]
    public void LatestExcludesIncompleteIngestion()
    {
        var completed = Plan();
        var incomplete = completed with
        {
            Ingestion = completed.Ingestion with
            {
                SourceSessionId = "z-incomplete",
                Status = PmsShadowIngestionStatuses.Applying,
                CompletedAtUtc = null
            }
        };
        var result = PmsShadowOperationalProjection.Latest([incomplete, completed], Policy(completed), Now(completed));
        Assert.Equal(completed.Ingestion.SourceSessionId, result!.LatestSession.SourceSessionId);
    }

    [Fact]
    public void LatestSelectionIsDeterministic()
    {
        var first = Plan();
        var second = first with { Ingestion = first.Ingestion with { SourceSessionId = "z-session" } };
        var result = PmsShadowOperationalProjection.Latest([first, second], Policy(first), Now(first));
        Assert.Equal("z-session", result!.LatestSession.SourceSessionId);
    }

    [Fact]
    public void FreshAndStaleStatusesUseExplicitPolicy()
    {
        var plan = Plan();
        var fresh = PmsShadowOperationalProjection.Build(plan, Policy(plan, 2), Now(plan, 1));
        var stale = PmsShadowOperationalProjection.Build(plan, Policy(plan, 1), Now(plan, 2));
        Assert.Equal(PmsShadowFreshnessStatus.Fresh, fresh.Freshness.Status);
        Assert.Equal(PmsShadowFreshnessStatus.Stale, stale.Freshness.Status);
        Assert.Contains(stale.Alerts, value => value.Code == "SHADOW_DATA_STALE");
    }

    [Fact]
    public void MissingTodayIsReported()
    {
        var plan = Plan();
        var policy = new PmsShadowFreshnessPolicy(plan.AccountSnapshot.ReportDate.AddDays(1), TimeSpan.FromDays(2));
        var result = PmsShadowOperationalProjection.Build(plan, policy, Now(plan));
        Assert.Equal(PmsShadowFreshnessStatus.MissingToday, result.Freshness.Status);
        Assert.Contains(result.Alerts, value => value.Code == "DAILY_SESSION_MISSING");
    }

    [Fact]
    public void ReadModelsExposeExpectedRowCountsInStableOrder()
    {
        var result = Projection();
        Assert.Equal(4, result.ModelRuns.Count);
        Assert.Equal(288, result.TargetPositions.Count);
        Assert.Equal(288, result.PositionOnlyDrifts.Count);
        Assert.Equal(4, result.BrokerAdjustedDrifts.Count);
        Assert.Equal(result.TargetPositions.OrderBy(value => value.StrategyId).ThenBy(value => value.SecurityId),
            result.TargetPositions);
    }

    [Fact]
    public void ReadModelLineageLinksInputsModelsOutputsAndFacts()
    {
        var result = Projection();
        Assert.Equal(4, result.Lineage.Entries.Count);
        Assert.All(result.Lineage.Entries, value =>
        {
            Assert.Equal(72, value.TargetWeightCount);
            Assert.Equal(72, value.TargetPositionCount);
            Assert.Equal(72, value.DriftCount);
            Assert.Equal(64, value.InputSha256.Length);
            Assert.Equal(64, value.OutputSha256.Length);
            Assert.Equal(40, value.CoreCommitId.Length);
        });
    }

    [Fact]
    public void WorkingLeavesBlockerIsNeverConvertedToEmptyState()
    {
        var result = Projection();
        Assert.All(result.BrokerAdjustedDrifts, value =>
        {
            Assert.False(value.Calculated);
            Assert.Equal(PmsShadowStateContract.BrokerAdjustedBlocker, value.Blocker);
            Assert.False(value.EmptyStateObserved);
            Assert.False(value.EmptyStateInferred);
            Assert.False(value.BrokerAuthority);
        });
    }

    [Fact]
    public void ReadOnlyInterfaceContainsNoMutationMethod()
    {
        var methods = typeof(IPmsShadowOperationalReadService).GetMethods();
        Assert.All(methods, method => Assert.StartsWith("Get", method.Name));
        Assert.DoesNotContain(methods, method => method.Name.Contains("Save", StringComparison.Ordinal) ||
            method.Name.Contains("Import", StringComparison.Ordinal) ||
            method.Name.Contains("Update", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("real_account")]
    [InlineData("execution")]
    [InlineData("trade_intent")]
    [InlineData("order_entry")]
    public void UnsafeSourcePlansFailClosed(string mutation)
    {
        var plan = Plan();
        plan = mutation switch
        {
            "real_account" => plan with { AccountSnapshot = plan.AccountSnapshot with { AccountId = "921640160" } },
            "execution" => plan with { ModelRuns = plan.ModelRuns.Select((value, index) =>
                index == 0 ? value with { ExecutionAllowed = true } : value).ToArray() },
            "trade_intent" => plan with { CycleResults = plan.CycleResults.Select((value, index) =>
                index == 0 ? value with { TradeIntentCount = 1 } : value).ToArray() },
            "order_entry" => plan with { CycleResults = plan.CycleResults.Select((value, index) =>
                index == 0 ? value with { OrderEntryEnabled = true } : value).ToArray() },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        var package = Package(plan);
        var validation = PmsShadowDailyHandoffValidator.Validate(Request(plan), package);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, value => value.StartsWith("PLAN:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NonPostgreSqlProviderFailsClosed()
    {
        var outcome = await Coordinator(new FakeStore(provider: "Microsoft.EntityFrameworkCore.SqlServer"))
            .CoordinateAsync(Request(), Package());
        Assert.Equal(PmsShadowDailyIngestionStatus.FailedClosed, outcome.Status);
    }

    [Fact]
    public async Task IncorrectMigrationBaselineFailsClosed()
    {
        var outcome = await Coordinator(new FakeStore(migrations: []))
            .CoordinateAsync(Request(), Package());
        Assert.Equal(PmsShadowDailyIngestionStatus.FailedClosed, outcome.Status);
    }

    [Fact]
    public void IncompleteReadModelIsNotPresentedAsFresh()
    {
        var plan = Plan();
        plan = plan with { TargetWeights = plan.TargetWeights.Skip(1).ToArray() };
        var result = PmsShadowOperationalProjection.Build(plan, Policy(plan), Now(plan));
        Assert.Equal(PmsShadowFreshnessStatus.Incomplete, result.Freshness.Status);
        Assert.Contains("EXPECTED_TARGET_WEIGHTS_MISMATCH", result.Freshness.Blockers);
    }

    private static PmsShadowDailyHandoffValidation Validate(PmsShadowDailyIngestionRequest? request = null) =>
        PmsShadowDailyHandoffValidator.Validate(request ?? Request(), Package());

    private static PmsShadowDailyIngestionCoordinator Coordinator(FakeStore store) =>
        new(new Arch6bPmsShadowSessionImporter(store));

    private static PmsShadowPersistencePlan Plan() => Arch6cPostgreSqlPmsShadowStateTests.BuildPlan();

    private static PmsShadowDailyEvidencePackage Package(PmsShadowPersistencePlan? plan = null)
    {
        plan ??= Plan();
        var counts = EfPmsShadowSessionImportStore.ExpectedRowCounts(plan);
        var verification = new Arch6dEvidenceVerification(Hash('c'), plan.Ingestion.SourceEvidenceSha256,
            plan.Ingestion.SourceSessionId, plan.RowsetSha256, plan.SourceArtifacts.Count, 0, counts);
        return new(new Arch6dEvidencePackage(plan, verification), Hash('e'));
    }

    private static PmsShadowDailyIngestionRequest Request(PmsShadowPersistencePlan? plan = null)
    {
        plan ??= Plan();
        var evidenceSha = plan.Ingestion.SourceEvidenceSha256;
        return new(PmsShadowDailyIngestionContract.Version, "ARCH6B_DAILY_MODEL_POSITION_SHADOW",
            "GO_ARCH6B_COMPLETE_NO_ORDER", plan.Ingestion.SourceSessionId, plan.AccountSnapshot.ReportDate,
            Hash('e'), evidenceSha, plan.RowsetSha256, plan.ModelRuns[0].CoreMasterCommitId,
            plan.ModelRuns[0].CoreMasterObjectFormat, "f349001c0589a34d7e28f40c3e531475a4eb0c37", "sha1",
            plan.QubesInputSnapshots.Select(value => value.SnapshotId).ToArray(),
            plan.ModelRuns.Select(value => value.ModelRunId).ToArray(),
            EfPmsShadowSessionImportStore.ExpectedRowCounts(plan), true, true, true, true, true, true,
            "TEST", PmsShadowStateContract.EvidenceClassification, true,
            plan.Ingestion.CompletedAtUtc!.Value.ToUniversalTime(),
            PmsShadowDailyIngestionContract.CreateIdempotencyKey(plan.Ingestion.SourceSessionId, evidenceSha));
    }

    private static PmsShadowOperationalReadSnapshot Projection()
    {
        var plan = Plan();
        return PmsShadowOperationalProjection.Build(plan, Policy(plan), Now(plan));
    }

    private static PmsShadowFreshnessPolicy Policy(PmsShadowPersistencePlan plan, int hours = 24) =>
        new(plan.AccountSnapshot.ReportDate, TimeSpan.FromHours(hours));

    private static DateTimeOffset Now(PmsShadowPersistencePlan plan, int hours = 1) =>
        plan.Ingestion.CompletedAtUtc!.Value.AddHours(hours);

    private static string Hash(char value) => new(value, 64);

    private sealed class FakeStore(string provider = "Npgsql.EntityFrameworkCore.PostgreSQL",
        IReadOnlyList<string>? migrations = null) : IPmsShadowSessionImportStore
    {
        private readonly InMemoryPmsShadowAtomicIngestionRegistry registry = new();
        public bool InterruptBeforeCommit { get; set; }
        public bool InterruptAfterCommit { get; set; }
        public bool Conflict { get; set; }

        public Task<PmsShadowStorePreflight> InspectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PmsShadowStorePreflight(provider,
                migrations ?? PmsShadowStateContract.MigrationIds));

        public Task<PmsShadowImportOutcome> ImportAtomicallyAsync(PmsShadowPersistencePlan plan,
            CancellationToken cancellationToken = default)
        {
            if (Conflict) throw new InvalidDataException("SOURCE_SESSION_ROWSET_SHA_CONFLICT");
            if (InterruptBeforeCommit)
            {
                InterruptBeforeCommit = false;
                registry.Apply(plan, true);
            }
            var result = registry.Apply(plan, false);
            if (InterruptAfterCommit)
            {
                InterruptAfterCommit = false;
                throw new InvalidOperationException("CONTROLLER_ACK_INTERRUPTED");
            }
            return Task.FromResult(new PmsShadowImportOutcome(result, plan.Ingestion.IngestionId,
                plan.Ingestion.SourceSessionId, plan.Ingestion.SourceEvidenceSha256, plan.RowsetSha256,
                EfPmsShadowSessionImportStore.ExpectedRowCounts(plan)));
        }
    }
}
