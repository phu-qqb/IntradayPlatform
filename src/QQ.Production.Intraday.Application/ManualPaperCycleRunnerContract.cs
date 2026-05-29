using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum PaperCycleRunMode
{
    ManualNoExternal,
    SchedulerRequested,
    ServiceRequested,
    PollingRequested,
    BackgroundJobRequested,
    LiveRequested
}

public enum PaperCyclePreflightStatus
{
    ReadyNoExternal,
    HeldMissingPriorBaseline,
    HeldMissingContinuityGate,
    HeldInvalidCadence,
    HeldDuplicateCycleRunId,
    HeldMissingQubesInput,
    RejectedUnsafeExternalActionRisk,
    InconclusiveSafe
}

public enum PaperCyclePreconditionStatus
{
    Satisfied,
    Missing,
    Failed,
    InconclusiveSafe
}

public enum PaperCycleIdempotencyStatus
{
    NewAccepted,
    DuplicateReturned,
    DuplicateRejected
}

public enum ManualPaperCycleFailureCategory
{
    MissingPriorBaseline,
    MissingContinuityGate,
    InvalidCadence,
    DuplicateCycleRunId,
    MissingQubesInput,
    UnsafeExternalActionRisk,
    InconclusiveSafe
}

public sealed record ManualPaperCycleRunRequest(
    string RequestedCycleRunId,
    QubesRunId? QubesRunId,
    string RequestedBy,
    DateTimeOffset RequestedAtUtc,
    int ExpectedCadenceMinutes,
    string? QubesFixturePath,
    string? QubesBatchReference,
    PaperNextCycleBaselineReference? BaselineReference,
    PaperLedgerStateId? PriorPaperLedgerStateId,
    string? PriorContinuityGateId,
    PaperCycleRunMode RunMode,
    bool QubesInputIsFixtureNoExternal,
    bool SchedulerOrServiceRequested,
    bool LiveBoundaryRequested,
    bool ExecuteNowRequested);

public sealed record ManualPaperCyclePrecondition(
    string Name,
    PaperCyclePreconditionStatus Status,
    ManualPaperCycleFailureCategory? FailureCategory,
    string SafeSummary);

public sealed record ManualPaperCyclePreflightResult(
    string PreflightId,
    string RequestedCycleRunId,
    PaperCyclePreflightStatus PreflightStatus,
    bool PreconditionsSatisfied,
    IReadOnlyList<ManualPaperCyclePrecondition> Preconditions,
    IReadOnlyList<ManualPaperCycleFailureCategory> MissingPreconditions,
    string SafetyStatus,
    string BaselineStatus,
    string QubesInputStatus,
    string CadenceStatus,
    PaperCycleIdempotencyStatus IdempotencyStatus,
    bool ExecutesCycle,
    bool IngestsNewQubesBatch,
    bool MutatesPaperLedgerState,
    bool StartsSchedulerOrService,
    bool CallsBrokerOrLiveMarketData,
    bool CreatesOrders,
    bool CreatesFills,
    bool CreatesExecutionReports,
    bool CreatesRoutes,
    bool SubmitsOrders);

public sealed record ManualPaperCycleExpectedOutput(
    string ExpectedOutputShapeId,
    bool ShapeOnly,
    bool CycleRunShapeIncluded,
    bool QubesLineageShapeIncluded,
    bool PaperBaselineInputShapeIncluded,
    bool TargetPortfolioShapeIncluded,
    bool TargetVsCurrentDiffShapeIncluded,
    bool TheoreticalPnlShapeIncluded,
    bool ReconciliationShapeIncluded,
    bool TheoreticalVsRealShapeIncluded,
    bool NonExecutableRebalanceIntentShapeIncluded,
    bool OperatorReportShapeIncluded,
    bool RebalanceIntentsMustRemainNonExecutable,
    bool CreatesNoOrderCandidates,
    bool CreatesNoExecutionPlans,
    bool ExecutesNoCycle);

public sealed record ManualPaperCycleSafetyGate(
    string SafetyGateId,
    bool NoExternal,
    bool NoBroker,
    bool NoLiveMarketData,
    bool NoSchedulerServicePolling,
    bool NoCycleExecution,
    bool NoQubesIngest,
    bool NoPaperLedgerMutation,
    bool NoLivePositionMutation,
    bool NoBrokerPositionMutation,
    bool NoProductionLedgerMutation,
    bool NoTradingStateMutation,
    bool NoOrder,
    bool NoFill,
    bool NoExecutionReport,
    bool NoRoute,
    bool NoSubmission,
    bool NoReplayOrShadowReplay);

public sealed record ManualPaperCycleRunnerContract(
    string ContractId,
    ManualPaperCycleRunRequest Request,
    ManualPaperCyclePreflightResult Preflight,
    ManualPaperCycleExpectedOutput ExpectedOutput,
    ManualPaperCycleSafetyGate SafetyGate,
    PaperCycleContinuityStatus PriorContinuityStatus,
    bool R027ContinuityDecisionPreserved,
    bool R025PaperLedgerBaselineLineagePreserved,
    bool QubesLineagePreserved,
    bool AudUsdTlsBoundaryInconclusiveNotFailed,
    bool UsdJpyCaveatPreserved,
    bool NoExternal,
    DateTimeOffset CreatedAtUtc);

public sealed record ManualPaperCycleRunnerContractResult(
    ManualPaperCycleRunnerContract Contract,
    bool Persisted,
    bool AlreadyDefined);

public interface IManualPaperCycleRunnerContractRepository
{
    Task<ManualPaperCycleRunnerContract?> GetByRequestedCycleRunIdAsync(
        string requestedCycleRunId,
        CancellationToken cancellationToken);

    Task AddAsync(ManualPaperCycleRunnerContract contract, CancellationToken cancellationToken);
}

public sealed class InMemoryManualPaperCycleRunnerContractRepository : IManualPaperCycleRunnerContractRepository
{
    private readonly List<ManualPaperCycleRunnerContract> contracts = [];

    public Task<ManualPaperCycleRunnerContract?> GetByRequestedCycleRunIdAsync(
        string requestedCycleRunId,
        CancellationToken cancellationToken)
        => Task.FromResult(contracts.FirstOrDefault(x =>
            x.Request.RequestedCycleRunId.Equals(requestedCycleRunId, StringComparison.OrdinalIgnoreCase)));

    public Task AddAsync(ManualPaperCycleRunnerContract contract, CancellationToken cancellationToken)
    {
        if (contracts.Any(x => x.Request.RequestedCycleRunId.Equals(
                contract.Request.RequestedCycleRunId,
                StringComparison.OrdinalIgnoreCase)))
        {
            return Task.CompletedTask;
        }

        contracts.Add(contract);
        return Task.CompletedTask;
    }
}

public sealed class ManualPaperCycleRunnerContractService(
    IManualPaperCycleRunnerContractRepository repository,
    IClock clock)
{
    public async Task<ManualPaperCycleRunnerContractResult> DefineAsync(
        ManualPaperCycleRunRequest request,
        PaperCycleContinuityDecisionGate? priorContinuityGate,
        CancellationToken cancellationToken)
    {
        var existing = await repository.GetByRequestedCycleRunIdAsync(request.RequestedCycleRunId, cancellationToken);
        if (existing is not null)
        {
            var duplicate = existing with
            {
                Preflight = existing.Preflight with
                {
                    PreflightStatus = PaperCyclePreflightStatus.HeldDuplicateCycleRunId,
                    IdempotencyStatus = PaperCycleIdempotencyStatus.DuplicateReturned
                }
            };

            return new ManualPaperCycleRunnerContractResult(duplicate, Persisted: false, AlreadyDefined: true);
        }

        var contract = CreateContract(request, priorContinuityGate, clock.UtcNow);
        await repository.AddAsync(contract, cancellationToken);
        return new ManualPaperCycleRunnerContractResult(contract, Persisted: true, AlreadyDefined: false);
    }

    private static ManualPaperCycleRunnerContract CreateContract(
        ManualPaperCycleRunRequest request,
        PaperCycleContinuityDecisionGate? priorContinuityGate,
        DateTimeOffset createdAtUtc)
    {
        var preflight = CreatePreflight(request, priorContinuityGate);
        var contractId = $"{request.RequestedCycleRunId}:manual-paper-cycle-runner-contract";

        return new ManualPaperCycleRunnerContract(
            contractId,
            request,
            preflight,
            CreateExpectedOutput($"{contractId}:expected-output-shape"),
            CreateSafetyGate($"{contractId}:safety-gate"),
            priorContinuityGate?.ContinuityStatus ?? PaperCycleContinuityStatus.InconclusiveSafe,
            R027ContinuityDecisionPreserved: priorContinuityGate?.ContinuityStatus is PaperCycleContinuityStatus.PaperContinuityReadyNoExternal,
            R025PaperLedgerBaselineLineagePreserved: request.BaselineReference is
            {
                NextCycleBaselineType: "PaperLedgerFixture",
                PaperOnly: true,
                NoExternal: true,
                FixtureState: true,
                BaselineIsProduction: false,
                BaselineIsBroker: false,
                BaselineIsLiveTrading: false
            },
            QubesLineagePreserved: request.QubesRunId is not null && request.QubesInputIsFixtureNoExternal,
            AudUsdTlsBoundaryInconclusiveNotFailed: true,
            UsdJpyCaveatPreserved: true,
            NoExternal: true,
            createdAtUtc);
    }

    private static ManualPaperCyclePreflightResult CreatePreflight(
        ManualPaperCycleRunRequest request,
        PaperCycleContinuityDecisionGate? priorContinuityGate)
    {
        var preconditions = new List<ManualPaperCyclePrecondition>
        {
            CheckPriorContinuityGate(priorContinuityGate),
            CheckBaseline(request),
            CheckQubesRunId(request),
            CheckQubesInput(request),
            CheckCadence(request),
            CheckManualMode(request),
            CheckNoSchedulerServicePolling(request),
            CheckNoLiveBoundary(request)
        };
        var missing = preconditions
            .Where(x => x.Status is not PaperCyclePreconditionStatus.Satisfied)
            .Select(x => x.FailureCategory ?? ManualPaperCycleFailureCategory.InconclusiveSafe)
            .Distinct()
            .ToArray();
        var preconditionsSatisfied = missing.Length == 0;
        var status = ResolveStatus(missing);

        return new ManualPaperCyclePreflightResult(
            $"{request.RequestedCycleRunId}:manual-paper-cycle-preflight",
            request.RequestedCycleRunId,
            status,
            preconditionsSatisfied,
            preconditions,
            missing,
            preconditionsSatisfied ? "ManualRunnerShapeReadyNoExternal" : "ManualRunnerShapeHeldNoExternal",
            request.BaselineReference is not null ? "PriorPaperLedgerBaselineReady" : "MissingPriorBaseline",
            HasQubesInput(request) && request.QubesInputIsFixtureNoExternal ? "FixtureNoExternal" : "MissingOrUnsafeQubesInput",
            request.ExpectedCadenceMinutes == 15 ? "FifteenMinuteCadence" : "InvalidCadence",
            PaperCycleIdempotencyStatus.NewAccepted,
            ExecutesCycle: false,
            IngestsNewQubesBatch: false,
            MutatesPaperLedgerState: false,
            StartsSchedulerOrService: false,
            CallsBrokerOrLiveMarketData: false,
            CreatesOrders: false,
            CreatesFills: false,
            CreatesExecutionReports: false,
            CreatesRoutes: false,
            SubmitsOrders: false);
    }

    private static PaperCyclePreflightStatus ResolveStatus(IReadOnlyCollection<ManualPaperCycleFailureCategory> failures)
    {
        if (failures.Count == 0)
        {
            return PaperCyclePreflightStatus.ReadyNoExternal;
        }

        if (failures.Contains(ManualPaperCycleFailureCategory.UnsafeExternalActionRisk))
        {
            return PaperCyclePreflightStatus.RejectedUnsafeExternalActionRisk;
        }

        if (failures.Contains(ManualPaperCycleFailureCategory.MissingContinuityGate))
        {
            return PaperCyclePreflightStatus.HeldMissingContinuityGate;
        }

        if (failures.Contains(ManualPaperCycleFailureCategory.MissingPriorBaseline))
        {
            return PaperCyclePreflightStatus.HeldMissingPriorBaseline;
        }

        if (failures.Contains(ManualPaperCycleFailureCategory.InvalidCadence))
        {
            return PaperCyclePreflightStatus.HeldInvalidCadence;
        }

        return failures.Contains(ManualPaperCycleFailureCategory.MissingQubesInput)
            ? PaperCyclePreflightStatus.HeldMissingQubesInput
            : PaperCyclePreflightStatus.InconclusiveSafe;
    }

    private static ManualPaperCyclePrecondition CheckPriorContinuityGate(PaperCycleContinuityDecisionGate? priorContinuityGate)
        => priorContinuityGate?.ContinuityStatus is PaperCycleContinuityStatus.PaperContinuityReadyNoExternal
            ? Satisfied("PriorContinuityGate", "R027 paper continuity gate is PaperContinuityReadyNoExternal.")
            : Missing("PriorContinuityGate", ManualPaperCycleFailureCategory.MissingContinuityGate, "Prior paper continuity gate is missing or not ready.");

    private static ManualPaperCyclePrecondition CheckBaseline(ManualPaperCycleRunRequest request)
        => request.BaselineReference is
        {
            NextCycleBaselineType: "PaperLedgerFixture",
            PaperOnly: true,
            NoExternal: true,
            FixtureState: true,
            BaselineIsProduction: false,
            BaselineIsBroker: false,
            BaselineIsLiveTrading: false
        } && request.PriorPaperLedgerStateId is not null
            ? Satisfied("PriorPaperLedgerBaseline", "R025 paper ledger fixture baseline is present and not live/broker/production.")
            : Missing("PriorPaperLedgerBaseline", ManualPaperCycleFailureCategory.MissingPriorBaseline, "Prior paper ledger baseline is missing or unsafe.");

    private static ManualPaperCyclePrecondition CheckQubesRunId(ManualPaperCycleRunRequest request)
        => request.QubesRunId is not null
            ? Satisfied("QubesRunId", "QubesRunId is present for fixture lineage.")
            : Missing("QubesRunId", ManualPaperCycleFailureCategory.MissingQubesInput, "QubesRunId is required.");

    private static ManualPaperCyclePrecondition CheckQubesInput(ManualPaperCycleRunRequest request)
        => HasQubesInput(request) && request.QubesInputIsFixtureNoExternal
            ? Satisfied("QubesFixtureInput", "Qubes input is fixture/no-external.")
            : Missing("QubesFixtureInput", ManualPaperCycleFailureCategory.MissingQubesInput, "Qubes input fixture or batch reference is required.");

    private static ManualPaperCyclePrecondition CheckCadence(ManualPaperCycleRunRequest request)
        => request.ExpectedCadenceMinutes == 15
            ? Satisfied("Cadence", "Expected cadence is 15 minutes.")
            : Failed("Cadence", ManualPaperCycleFailureCategory.InvalidCadence, "Expected cadence must be 15 minutes.");

    private static ManualPaperCyclePrecondition CheckManualMode(ManualPaperCycleRunRequest request)
        => request.RunMode is PaperCycleRunMode.ManualNoExternal && !request.ExecuteNowRequested
            ? Satisfied("RunMode", "Run mode is ManualNoExternal shape-only and execution is not requested.")
            : Failed("RunMode", ManualPaperCycleFailureCategory.UnsafeExternalActionRisk, "R028 may define only a manual no-external shape and must not execute.");

    private static ManualPaperCyclePrecondition CheckNoSchedulerServicePolling(ManualPaperCycleRunRequest request)
        => !request.SchedulerOrServiceRequested
            ? Satisfied("NoSchedulerServicePolling", "No scheduler, service, polling, timer, or background job is involved.")
            : Failed("NoSchedulerServicePolling", ManualPaperCycleFailureCategory.UnsafeExternalActionRisk, "Scheduler/service/polling mode is not allowed.");

    private static ManualPaperCyclePrecondition CheckNoLiveBoundary(ManualPaperCycleRunRequest request)
        => !request.LiveBoundaryRequested
            ? Satisfied("NoLiveBoundary", "No broker or live market-data boundary is involved.")
            : Failed("NoLiveBoundary", ManualPaperCycleFailureCategory.UnsafeExternalActionRisk, "Live broker or market-data boundary is not allowed.");

    private static bool HasQubesInput(ManualPaperCycleRunRequest request)
        => !string.IsNullOrWhiteSpace(request.QubesFixturePath) ||
           !string.IsNullOrWhiteSpace(request.QubesBatchReference);

    private static ManualPaperCyclePrecondition Satisfied(string name, string summary)
        => new(name, PaperCyclePreconditionStatus.Satisfied, null, summary);

    private static ManualPaperCyclePrecondition Missing(
        string name,
        ManualPaperCycleFailureCategory category,
        string summary)
        => new(name, PaperCyclePreconditionStatus.Missing, category, summary);

    private static ManualPaperCyclePrecondition Failed(
        string name,
        ManualPaperCycleFailureCategory category,
        string summary)
        => new(name, PaperCyclePreconditionStatus.Failed, category, summary);

    private static ManualPaperCycleExpectedOutput CreateExpectedOutput(string expectedOutputShapeId)
        => new(
            expectedOutputShapeId,
            ShapeOnly: true,
            CycleRunShapeIncluded: true,
            QubesLineageShapeIncluded: true,
            PaperBaselineInputShapeIncluded: true,
            TargetPortfolioShapeIncluded: true,
            TargetVsCurrentDiffShapeIncluded: true,
            TheoreticalPnlShapeIncluded: true,
            ReconciliationShapeIncluded: true,
            TheoreticalVsRealShapeIncluded: true,
            NonExecutableRebalanceIntentShapeIncluded: true,
            OperatorReportShapeIncluded: true,
            RebalanceIntentsMustRemainNonExecutable: true,
            CreatesNoOrderCandidates: true,
            CreatesNoExecutionPlans: true,
            ExecutesNoCycle: true);

    private static ManualPaperCycleSafetyGate CreateSafetyGate(string safetyGateId)
        => new(
            safetyGateId,
            NoExternal: true,
            NoBroker: true,
            NoLiveMarketData: true,
            NoSchedulerServicePolling: true,
            NoCycleExecution: true,
            NoQubesIngest: true,
            NoPaperLedgerMutation: true,
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoProductionLedgerMutation: true,
            NoTradingStateMutation: true,
            NoOrder: true,
            NoFill: true,
            NoExecutionReport: true,
            NoRoute: true,
            NoSubmission: true,
            NoReplayOrShadowReplay: true);
}
