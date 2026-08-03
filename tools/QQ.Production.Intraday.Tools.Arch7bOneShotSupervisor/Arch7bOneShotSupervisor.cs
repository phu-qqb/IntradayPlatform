namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed class Arch7bOneShotBudget
{
    public int Slots { get; private set; }
    public int Captures { get; private set; }
    public int RdsReads { get; private set; }
    public int Retries { get; private set; }

    public void RecordSlot()
    {
        if (++Slots > Arch7bOneShotContracts.MaximumSlots)
            throw new Arch7bQualificationException(Arch7bBlockers.SlotLimitExceeded);
    }

    public void RecordCapture()
    {
        if (++Captures > Arch7bOneShotContracts.MaximumCaptures)
            throw new Arch7bQualificationException(Arch7bBlockers.CaptureLimitExceeded);
    }

    public void RecordRdsRead()
    {
        if (++RdsReads > Arch7bOneShotContracts.MaximumRdsReads)
            throw new Arch7bQualificationException(Arch7bBlockers.RdsReadLimitExceeded);
    }

    public void RecordRetry()
    {
        if (++Retries > Arch7bOneShotContracts.MaximumRetries)
            throw new Arch7bQualificationException(Arch7bBlockers.RetryForbidden);
    }
}

public sealed class Arch7bIdentityRegistry
{
    private readonly HashSet<string> identities = new(StringComparer.Ordinal);

    public void Add(string identity)
    {
        if (!identities.Add(identity))
            throw new Arch7bQualificationException(Arch7bBlockers.IdentityReused, identity);
    }
}

public sealed record Arch7bNoLiveSafetyCounters(
    int SecretReads,
    int DatabaseConnections,
    int DatabaseWrites,
    int LiveSlots,
    int PortalHttpRequests,
    int MarketDataLiveConnections,
    int FixLogons,
    int Orders,
    int Fills,
    int LedgerEvents,
    int AccountApiCalls,
    int PolygonCalls,
    int DatabentoCalls,
    int AwsMutations,
    int S3Objects,
    int OperationalOneShotStates)
{
    public static Arch7bNoLiveSafetyCounters Zero { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}

public sealed record Arch7bOneShotSimulationEvidence(
    string ContractVersion,
    string RunId,
    Arch7bSlotLock SlotLock,
    IReadOnlyList<Arch7bSupervisorState> States,
    IReadOnlyDictionary<string, string> StageEvidenceSha256,
    int SlotCount,
    int CaptureCount,
    int RdsReadCount,
    int RetryCount,
    int BracketCount,
    string FinalBlocker,
    Arch7bCleanupReport Cleanup,
    Arch7bNoLiveSafetyCounters Safety,
    bool Passed,
    string EvidenceSha256);

public sealed class Arch7bOneShotLiveSupervisor
{
    private readonly Arch7bIdentityRegistry identityRegistry;

    public Arch7bOneShotLiveSupervisor(Arch7bIdentityRegistry? identityRegistry = null)
    {
        this.identityRegistry = identityRegistry ?? new Arch7bIdentityRegistry();
    }

    public async Task<Arch7bOneShotSimulationEvidence> SimulateAsync(int seed,
        CancellationToken cancellationToken = default)
    {
        var states = new List<Arch7bSupervisorState> { Arch7bSupervisorState.Created };
        var registry = Arch7bGlobalSloRegistry.CreateDefault();
        var chronology = Arch7bCrossRepositoryChronology.Validate(Arch7bCrossRepositoryChronology.CreateDefault(), registry);
        if (!chronology.IsValid) throw new InvalidDataException(string.Join(',', chronology.Blockers));
        states.Add(Arch7bSupervisorState.StaticValidated);
        states.Add(Arch7bSupervisorState.CalendarReady);

        var observed = new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero).AddSeconds(seed % 120);
        var selector = new Arch7bOperationalSlotSelector();
        var slot = selector.SelectAndLock(observed, chronology.PreSlotCriticalPathSloSeconds);
        var budget = new Arch7bOneShotBudget();
        budget.RecordSlot();
        states.Add(Arch7bSupervisorState.SlotLocked);

        var runId = $"arch7b-one-shot-sim-{seed:D8}-{slot.LockSha256[..12]}";
        identityRegistry.Add(runId);
        identityRegistry.Add(slot.SlotId + ":" + runId);
        var runRoot = Path.Combine(Path.GetTempPath(), "qq-arch7b-qualification", runId);
        var cleanup = new Arch7bTerminalCleanupSupervisor(runRoot);
        foreach (var resourceType in Arch7bTerminalCleanupSupervisor.RequiredResourceTypes)
        {
            var resourceId = $"{runId}:{resourceType}";
            var ownedPath = resourceType == "transient-output-roots" ? runRoot : null;
            cleanup.Register(new(resourceId, resourceType, "SYNTHETIC", "synthetic-runner", false,
                "TERMINAL_ALWAYS", "SYNTHETIC_RELEASE", TimeSpan.FromSeconds(2),
                resourceType.Contains("evidence", StringComparison.Ordinal), true, true,
                Arch7bCleanupState.Registered, null, ownedPath),
                _ => Task.FromResult(Arch7bOneShotContracts.Sha256(resourceId + ":cleaned")));
            cleanup.MarkCreated(resourceId);
        }

        var stageEvidence = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var stage in Arch7bStages.All)
            stageEvidence[stage] = Arch7bOneShotContracts.Sha256($"{runId}:{stage}:synthetic");
        states.Add(Arch7bSupervisorState.Prepared);
        budget.RecordRdsRead();
        states.Add(Arch7bSupervisorState.Armed);
        budget.RecordRdsRead();
        states.Add(Arch7bSupervisorState.LeaseReady);
        const int bracketCount = 1;
        states.Add(Arch7bSupervisorState.BracketCompleted);
        states.Add(Arch7bSupervisorState.PositionReady);
        Arch7bCrossRepositoryChronology.ValidatePrearm(observed, slot.SlotStartUtc);
        states.Add(Arch7bSupervisorState.MarketPrearmed);
        budget.RecordCapture();
        states.Add(Arch7bSupervisorState.MarketCompleted);
        states.Add(Arch7bSupervisorState.PmsCompleted);
        states.Add(Arch7bSupervisorState.Arch7aCompleted);
        states.Add(Arch7bSupervisorState.Reported);
        states.Add(Arch7bSupervisorState.ExpectedFinalBlocker);
        states.Add(Arch7bSupervisorState.Cleaning);
        var cleanupReport = await cleanup.CleanupAllAsync(Arch7bOneShotContracts.ExpectedFinalBlocker,
            cancellationToken).ConfigureAwait(false);
        cleanup.ValidateNoResidue(value => value.Created && value.CleanupState != Arch7bCleanupState.Cleaned);
        states.Add(cleanupReport.Complete ? Arch7bSupervisorState.TerminalSuccess : Arch7bSupervisorState.TerminalFailed);
        var passed = cleanupReport.Complete && budget.Slots == 1 && budget.Captures == 1 &&
            budget.RdsReads == 2 && budget.Retries == 0 && bracketCount == 1 &&
            stageEvidence.Count == Arch7bStages.All.Count && states[^1] == Arch7bSupervisorState.TerminalSuccess;
        var canonical = string.Join('\n', Arch7bOneShotContracts.SupervisorEvidenceVersion, runId,
            slot.LockSha256, string.Join(',', states), string.Join(',', stageEvidence.OrderBy(value => value.Key)
                .Select(value => $"{value.Key}:{value.Value}")), cleanupReport.EvidenceSha256, passed);
        return new(Arch7bOneShotContracts.SupervisorEvidenceVersion, runId, slot, states, stageEvidence,
            budget.Slots, budget.Captures, budget.RdsReads, budget.Retries, bracketCount,
            Arch7bOneShotContracts.ExpectedFinalBlocker, cleanupReport, Arch7bNoLiveSafetyCounters.Zero,
            passed, Arch7bOneShotContracts.Sha256(canonical));
    }
}

public sealed record Arch7bSimulationQualification(
    int IndependentRunCount,
    int IndependentPassCount,
    int SequentialCampaignCount,
    int SequentialCampaignPassCount,
    int RunsPerSequentialCampaign,
    int ResidualResourceCount,
    string EvidenceSha256);

public static class Arch7bSimulationQualifier
{
    public static async Task<Arch7bSimulationQualification> RunAsync(int independentRuns = 50,
        int campaigns = 10, int runsPerCampaign = 3, int seedOffset = 0,
        CancellationToken cancellationToken = default)
    {
        var independent = new List<Arch7bOneShotSimulationEvidence>();
        for (var index = 0; index < independentRuns; index++)
            independent.Add(await new Arch7bOneShotLiveSupervisor().SimulateAsync(seedOffset + index + 1, cancellationToken)
                .ConfigureAwait(false));
        var campaignPasses = 0;
        var campaignEvidence = new List<string>();
        for (var campaign = 0; campaign < campaigns; campaign++)
        {
            var identities = new Arch7bIdentityRegistry();
            var supervisor = new Arch7bOneShotLiveSupervisor(identities);
            var runs = new List<Arch7bOneShotSimulationEvidence>();
            for (var run = 0; run < runsPerCampaign; run++)
                runs.Add(await supervisor.SimulateAsync(seedOffset + 1000 + campaign * 100 + run, cancellationToken)
                    .ConfigureAwait(false));
            if (runs.All(value => value.Passed) && runs.Select(value => value.RunId).Distinct().Count() == runs.Count &&
                runs.Select(value => value.SlotLock.LockSha256).Distinct().Count() == runs.Count)
                campaignPasses++;
            campaignEvidence.AddRange(runs.Select(value => value.EvidenceSha256));
        }
        var canonical = string.Join('\n', independent.Select(value => value.EvidenceSha256)
            .Concat(campaignEvidence));
        return new(independentRuns, independent.Count(value => value.Passed), campaigns, campaignPasses,
            runsPerCampaign, 0, Arch7bOneShotContracts.Sha256(canonical));
    }
}
