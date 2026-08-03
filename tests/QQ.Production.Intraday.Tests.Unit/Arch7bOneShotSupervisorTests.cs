using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bOneShotSupervisorTests
{
    [Fact]
    public void Contracts_are_versioned_and_no_order()
    {
        Assert.Equal("arch7b_operational_slot_selection_policy_v1", Arch7bOneShotContracts.OperationalSlotSelectionPolicyVersion);
        Assert.Equal("arch7b_global_slo_registry_v1", Arch7bOneShotContracts.GlobalSloRegistryVersion);
        Assert.Equal("arch7b_one_shot_cross_repository_chronology_v1", Arch7bOneShotContracts.CrossRepositoryChronologyVersion);
        Assert.Equal("arch7b_terminal_cleanup_supervisor_v1", Arch7bOneShotContracts.TerminalCleanupSupervisorVersion);
        Assert.Equal("arch7b_one_shot_live_supervisor_v1", Arch7bOneShotContracts.LiveSupervisorVersion);
        Assert.Equal(0, Arch7bOneShotContracts.MaximumRetries);
    }

    [Fact]
    public void Calendar_selects_first_eligible_canonical_weekday_slot_and_locks_once()
    {
        var observed = new DateTimeOffset(2026, 8, 3, 10, 2, 0, TimeSpan.Zero);
        var selector = new Arch7bOperationalSlotSelector();
        var selected = selector.SelectAndLock(observed, 400);

        Assert.Equal(new DateTimeOffset(2026, 8, 3, 10, 15, 0, TimeSpan.Zero), selected.SlotStartUtc);
        Assert.Equal(600, selected.RequiredPreparationMarginSeconds);
        Assert.True(Arch7bOneShotContracts.IsSha256(selected.LockSha256));
        Assert.Equal(Arch7bBlockers.SlotLockAlreadyPublished,
            Assert.Throws<Arch7bQualificationException>(() => selector.SelectAndLock(observed, 400)).BlockerCode);
    }

    [Fact]
    public void Slo_registry_aggregates_sourced_values_and_four_global_values_without_contradictions()
    {
        var registry = Arch7bGlobalSloRegistry.CreateDefault();

        Assert.Equal(4, registry.Entries.Count(value => value.SloId.StartsWith("GLOBAL_", StringComparison.Ordinal)));
        Assert.True(registry.Entries.Count > 20);
        Assert.All(registry.Entries, value =>
        {
            Assert.False(string.IsNullOrWhiteSpace(value.SourceFile));
            Assert.False(string.IsNullOrWhiteSpace(value.SourceSymbol));
            Assert.True(Arch7bOneShotContracts.IsSha256(value.SourceFileSha256));
        });
    }

    [Fact]
    public void Chronology_is_closed_acyclic_and_calculates_critical_path_from_slos()
    {
        var registry = Arch7bGlobalSloRegistry.CreateDefault();
        var validation = Arch7bCrossRepositoryChronology.Validate(Arch7bCrossRepositoryChronology.CreateDefault(), registry);

        Assert.True(validation.IsValid, string.Join(',', validation.Blockers));
        Assert.Equal(40, validation.StageCount);
        Assert.Equal(39, validation.EdgeCount);
        Assert.Equal(40, validation.TopologicalOrder.Count);
        Assert.True(validation.PreSlotCriticalPathSloSeconds > 0);
        Assert.True(Arch7bOneShotContracts.IsSha256(validation.EvidenceSha256));
    }

    [Fact]
    public async Task Cleanup_is_reverse_order_exactly_once_and_preserves_primary_failure()
    {
        var order = new List<string>();
        var root = Path.Combine(Path.GetTempPath(), "arch7b-cleanup-test");
        var cleanup = new Arch7bTerminalCleanupSupervisor(root);
        foreach (var id in new[] { "first", "second", "third" })
        {
            cleanup.Register(Resource(id), _ => { order.Add(id); return Task.FromResult(id); });
            cleanup.MarkCreated(id);
        }

        var report = await cleanup.CleanupAllAsync("primary-blocker");

        Assert.Equal(["third", "second", "first"], order);
        Assert.True(report.Complete);
        Assert.Equal("primary-blocker", report.PrimaryBlocker);
        Assert.Null(report.CleanupBlocker);
        Assert.All(report.Resources, value => Assert.Equal(Arch7bCleanupState.Cleaned, value.CleanupState));
    }

    [Fact]
    public async Task Core_static_authority_binding_reads_exact_commit_tree_parsers_and_tests_only()
    {
        var binding = await Arch7bCoreStaticAuthorityQualifier.QualifyAsync(new FakeCoreReader());

        Assert.Equal(3, binding.Commands.Count);
        Assert.All(binding.Commands, command =>
        {
            Assert.True(command.NoOrder);
            Assert.Contains(command.Sources, source => source.Role == "PARSER");
            Assert.Contains(command.Sources, source => source.Role == "TEST");
            Assert.True(Arch7bOneShotContracts.IsSha256(command.ExecutableSha256));
        });
        Assert.Equal(0, binding.SecretReads);
        Assert.Equal(0, binding.DatabaseConnections);
        Assert.Equal(0, binding.PortalHttpRequests);
    }

    [Fact]
    public async Task Fifty_independent_success_simulations_pass_without_residue()
    {
        var result = await Arch7bSimulationQualifier.RunAsync(50, 0, 3);

        Assert.Equal(50, result.IndependentRunCount);
        Assert.Equal(50, result.IndependentPassCount);
        Assert.Equal(0, result.ResidualResourceCount);
    }

    [Fact]
    public async Task Ten_campaigns_of_three_sequential_runs_pass_without_cross_run_contamination()
    {
        var result = await Arch7bSimulationQualifier.RunAsync(1, 10, 3);

        Assert.Equal(10, result.SequentialCampaignCount);
        Assert.Equal(10, result.SequentialCampaignPassCount);
        Assert.Equal(3, result.RunsPerSequentialCampaign);
        Assert.Equal(0, result.ResidualResourceCount);
    }

    public static TheoryData<int, string> NegativeCases => new()
    {
        { 1, Arch7bBlockers.SlotAlreadyStarted },
        { 2, Arch7bBlockers.PreparationMarginInsufficient },
        { 3, Arch7bBlockers.SlotOutsideOperationalSession },
        { 4, Arch7bBlockers.CalendarAmbiguous },
        { 5, Arch7bBlockers.SlotLockAlreadyPublished },
        { 6, Arch7bBlockers.SloContradiction },
        { 7, Arch7bBlockers.CriticalPathSloMissing },
        { 8, Arch7bBlockers.SchedulerWakeLatenessExceeded },
        { 9, Arch7bBlockers.CleanupDeadlineExceeded },
        { 10, Arch7bBlockers.ChronologyCycle },
        { 11, Arch7bBlockers.ChronologyUnknownStage },
        { 12, Arch7bBlockers.ChronologyEvidenceMissing },
        { 13, Arch7bBlockers.RdsRead2AfterBracket },
        { 14, Arch7bBlockers.MarketPrearmAfterSlotStart },
        { 15, Arch7bBlockers.Arch7aBeforeRevisionBinding },
        { 16, Arch7bBlockers.ResourceNotRegistered },
        { 17, Arch7bBlockers.ResourceDoubleCleanup },
        { 18, Arch7bBlockers.ChildProcessResidual },
        { 19, Arch7bBlockers.MarkerResidual },
        { 20, Arch7bBlockers.PrimaryBlockerMasked },
        { 21, Arch7bBlockers.CleanupPathOutsideRunRoot },
        { 22, Arch7bBlockers.CoreParserAuthorityMissing },
        { 23, Arch7bBlockers.ExecutableShaMismatch },
        { 24, Arch7bBlockers.SupervisorModeUnknown },
        { 25, Arch7bBlockers.CorePlaceholderUnresolved },
        { 26, Arch7bBlockers.RetryForbidden },
        { 27, Arch7bBlockers.RdsReadLimitExceeded },
        { 28, Arch7bBlockers.CaptureLimitExceeded },
        { 29, Arch7bBlockers.SlotLimitExceeded },
        { 30, Arch7bBlockers.IdentityReused }
    };

    [Theory]
    [MemberData(nameof(NegativeCases))]
    public async Task Negative_matrix_returns_exact_catalogued_blocker(int caseNumber, string expected)
    {
        var exception = await Assert.ThrowsAsync<Arch7bQualificationException>(() => TriggerNegativeCaseAsync(caseNumber));
        Assert.Equal(expected, exception.BlockerCode);
    }

    private static async Task TriggerNegativeCaseAsync(int caseNumber)
    {
        var observed = new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);
        switch (caseNumber)
        {
            case 1: Arch7bOneShotInvariantGuard.ValidateSlotCandidate(observed, observed, 600); break;
            case 2: Arch7bOneShotInvariantGuard.ValidateSlotCandidate(observed, observed.AddSeconds(599), 600); break;
            case 3: Arch7bOneShotInvariantGuard.ValidateSlotCandidate(observed, observed.AddSeconds(600), 600, operational: false); break;
            case 4: Arch7bOneShotInvariantGuard.ValidateSlotCandidate(observed, observed.AddSeconds(600), 600, ambiguous: true); break;
            case 5:
                {
                    var selector = new Arch7bOperationalSlotSelector();
                    selector.SelectAndLock(observed, 1);
                    selector.SelectAndLock(observed, 1);
                    break;
                }
            case 6:
                {
                    var entries = Arch7bGlobalSloRegistry.CreateDefault().Entries.ToList();
                    entries.Add(entries[0] with { Threshold = entries[0].Threshold + 1 });
                    _ = new Arch7bGlobalSloRegistry(entries);
                    break;
                }
            case 7:
                {
                    var edges = Arch7bCrossRepositoryChronology.CreateDefault().ToArray();
                    edges[0] = edges[0] with { SloId = "missing-slo" };
                    var result = Arch7bCrossRepositoryChronology.Validate(edges, Arch7bGlobalSloRegistry.CreateDefault());
                    throw new Arch7bQualificationException(result.Blockers.Single(value => value == Arch7bBlockers.CriticalPathSloMissing));
                }
            case 8: Arch7bOneShotInvariantGuard.ValidateWakeLateness(TimeSpan.FromMilliseconds(1001)); break;
            case 9: Arch7bOneShotInvariantGuard.ValidateCleanupDeadline(TimeSpan.FromSeconds(61)); break;
            case 10:
                {
                    var edges = Arch7bCrossRepositoryChronology.CreateDefault().ToList();
                    edges.Add(new("TERMINAL_CLEANUP", "STATIC_AUTHORITY_VALIDATION", "cycle", null, "cycle", []));
                    var result = Arch7bCrossRepositoryChronology.Validate(edges, Arch7bGlobalSloRegistry.CreateDefault());
                    throw new Arch7bQualificationException(result.Blockers.Single(value => value == Arch7bBlockers.ChronologyCycle));
                }
            case 11:
                {
                    var edges = Arch7bCrossRepositoryChronology.CreateDefault().ToArray();
                    edges[0] = edges[0] with { From = "unknown-stage" };
                    var result = Arch7bCrossRepositoryChronology.Validate(edges, Arch7bGlobalSloRegistry.CreateDefault());
                    throw new Arch7bQualificationException(result.Blockers.First(value => value == Arch7bBlockers.ChronologyUnknownStage));
                }
            case 12:
                {
                    var edges = Arch7bCrossRepositoryChronology.CreateDefault().ToArray();
                    edges[0] = edges[0] with { RequiredEvidence = string.Empty };
                    var result = Arch7bCrossRepositoryChronology.Validate(edges, Arch7bGlobalSloRegistry.CreateDefault());
                    throw new Arch7bQualificationException(result.Blockers.First(value => value == Arch7bBlockers.ChronologyEvidenceMissing));
                }
            case 13: Arch7bOneShotInvariantGuard.ValidateStageOrder("RDS_READ_2", "BRACKET_T0", ["BRACKET_T0", "RDS_READ_2"], Arch7bBlockers.RdsRead2AfterBracket); break;
            case 14: Arch7bCrossRepositoryChronology.ValidatePrearm(observed, observed); break;
            case 15: Arch7bOneShotInvariantGuard.ValidateStageOrder("REVISION_BINDING", "ARCH7A_QUALIFY_SHADOW", ["ARCH7A_QUALIFY_SHADOW", "REVISION_BINDING"], Arch7bBlockers.Arch7aBeforeRevisionBinding); break;
            case 16: new Arch7bTerminalCleanupSupervisor(Path.GetTempPath()).MarkCreated("missing"); break;
            case 17:
                {
                    var cleanup = CleanupWithCreatedResource("double", "process");
                    await cleanup.CleanupOneAsync("double");
                    await cleanup.CleanupOneAsync("double");
                    break;
                }
            case 18: CleanupWithCreatedResource("process", "child-process").ValidateNoResidue(value => value.ResourceId == "process"); break;
            case 19: CleanupWithCreatedResource("marker", "lease-marker").ValidateNoResidue(value => value.ResourceId == "marker"); break;
            case 20: Arch7bOneShotInvariantGuard.ValidatePrimaryFailurePreserved("PRIMARY", "CLEANUP"); break;
            case 21:
                {
                    var root = Path.Combine(Path.GetTempPath(), "arch7b-root");
                    new Arch7bTerminalCleanupSupervisor(root).Register(Resource("outside") with { OwnedPath = Path.GetPathRoot(root) }, _ => Task.FromResult("done"));
                    break;
                }
            case 22: await Arch7bCoreStaticAuthorityQualifier.QualifyAsync(new FakeCoreReader(missingParser: true)); break;
            case 23: Arch7bCoreStaticAuthorityQualifier.ValidateExecutableSha(new string('a', 64), new string('b', 64)); break;
            case 24: Arch7bOneShotInvariantGuard.ValidateKnownMode("live"); break;
            case 25: Arch7bCoreStaticAuthorityQualifier.ValidateDeclaredPlaceholders(["<UNDECLARED>"], []); break;
            case 26: new Arch7bOneShotBudget().RecordRetry(); break;
            case 27:
                {
                    var budget = new Arch7bOneShotBudget(); budget.RecordRdsRead(); budget.RecordRdsRead(); budget.RecordRdsRead(); break;
                }
            case 28:
                {
                    var budget = new Arch7bOneShotBudget(); budget.RecordCapture(); budget.RecordCapture(); break;
                }
            case 29:
                {
                    var budget = new Arch7bOneShotBudget(); budget.RecordSlot(); budget.RecordSlot(); break;
                }
            case 30:
                {
                    var identities = new Arch7bIdentityRegistry(); identities.Add("same"); identities.Add("same"); break;
                }
            default: throw new ArgumentOutOfRangeException(nameof(caseNumber));
        }
    }

    private static Arch7bTerminalCleanupSupervisor CleanupWithCreatedResource(string id, string type)
    {
        var cleanup = new Arch7bTerminalCleanupSupervisor(Path.GetTempPath());
        cleanup.Register(Resource(id) with { ResourceType = type }, _ => Task.FromResult("done"));
        cleanup.MarkCreated(id);
        return cleanup;
    }

    private static Arch7bResourceRegistration Resource(string id) => new(id, "process", "TEST", "test",
        false, "TERMINAL_ALWAYS", "STOP", TimeSpan.FromSeconds(1), false, true, true,
        Arch7bCleanupState.Registered, null);

    private sealed class FakeCoreReader(bool missingParser = false) : IArch7bCoreRepositoryReader
    {
        public Task<string> HeadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Arch7bOneShotContracts.CoreCommit);

        public Task<string> TreeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Arch7bOneShotContracts.CoreTree);

        public Task<string> ReadTextAsync(string commit, string path, CancellationToken cancellationToken = default)
        {
            var content = "export\nRDS_SECRET_DEADLINE_MS\nPREQUALIFICATION_MAX_AGE_SECONDS\nprocess.argv\ntest\nqualify-arm-import-operational-orchestrator\nrun-bracket-fast-seal-and-hand-off\n";
            if (missingParser && path.EndsWith("policy.mjs", StringComparison.Ordinal)) content = "missing";
            return Task.FromResult(content);
        }
    }
}
