using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Application;

public enum ManualPaperCycleCliStatus
{
    CompletedNoExternal,
    DuplicateReturned,
    RejectedInvalidArguments,
    RejectedPreflightFailed,
    RejectedUnsafeMode,
    InconclusiveSafe
}

public enum ManualPaperCycleCliRejectedReason
{
    None,
    MissingRequiredArgument,
    InvalidMode,
    InvalidCadence,
    MissingExplicitSafetyFlag,
    UnsafeSchedulerServicePolling,
    UnsafeLiveBoundary,
    UnsafeOrderOrTradingMode,
    UnsafePaperLedgerCommit,
    PreflightFailed,
    InconclusiveSafe
}

public sealed record ManualPaperCycleCliSafetyFlags(
    bool SchedulerRequested,
    bool ServiceRequested,
    bool PollingRequested,
    bool LiveBrokerRequested,
    bool LiveMarketInputRequested,
    bool BrokerRequested,
    bool FixRequested,
    bool RouteRequested,
    bool ExecutableScheduleRequested,
    bool LiveStateMutationRequested,
    bool TradingRequested,
    bool OrdersRequested,
    bool FillsRequested,
    bool ReportsRequested,
    bool SyntheticPmsFixtureAllowed,
    bool NotQubesEconomicOutputFixtureAllowed,
    bool NoOrderExplicit,
    bool NoRouteExplicit,
    bool NoFillExplicit,
    bool NoBrokerExplicit,
    bool NoFixExplicit,
    bool NoExecutableScheduleExplicit,
    bool NoLiveStateMutationExplicit,
    bool NoLedgerCommitExplicit,
    bool PaperLedgerCommitRequested);

public sealed record ManualPaperCycleCliParsedRequest(
    ManualPaperCycleRunRequest Request,
    string OutputArtifactsDirectory,
    ManualPaperCycleCliSafetyFlags SafetyFlags);

public sealed record ManualPaperCycleCliExecutionContext(
    PaperCycleContinuityDecisionGate PriorContinuityGate,
    PaperNextCycleBaselineReference BaselineReference,
    PaperLedgerStateArchiveRecord PaperLedgerStateArchive,
    DateTimeOffset ProducedAtUtc,
    DateTimeOffset EffectiveAtUtc,
    DateTimeOffset CycleStartedAtUtc,
    DateTimeOffset CycleCompletedAtUtc,
    string FundCode,
    string ModelName,
    decimal PortfolioNotional,
    IReadOnlyList<string> RawQubesLines,
    IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol,
    decimal WeightTolerance,
    decimal NotionalTolerance,
    decimal PnlTolerance,
    TimeSpan MaxMarkAge,
    int Version);

public sealed record ManualPaperCycleCliContract(
    string CommandName,
    bool ExplicitManualInvocationRequired,
    bool RequiresManualNoExternalMode,
    bool RunsAtMostOneCyclePerInvocation,
    bool CallsPreflightBeforeCycle,
    bool EmitsSafeArtifactsOnly,
    bool NoSchedulerServicePolling,
    bool NoAutomaticExecution,
    bool NoPaperLedgerCommit,
    bool NoBroker,
    bool NoLiveMarketInput,
    bool NoOrders,
    bool NoFills,
    bool NoReports,
    bool NoRoutes,
    bool NoSubmissions);

public sealed record ManualPaperCycleCliArgumentsContract(
    IReadOnlyList<string> RequiredArguments,
    IReadOnlyList<string> OptionalArguments,
    IReadOnlyList<string> ForbiddenArguments,
    string RequiredMode,
    int ExpectedCadenceMinutes);

public sealed record ManualPaperCycleCliPreflightContract(
    bool RequiresPriorPaperContinuityReadyNoExternal,
    bool RequiresPaperBaseline,
    bool RequiresQubesRunId,
    bool RequiresQubesFixtureInput,
    bool RequiresFifteenMinuteCadence,
    bool RejectsSchedulerServicePolling,
    bool RejectsLiveBoundary,
    bool RejectsOrderTradingMode,
    bool RejectsPaperLedgerCommit);

public sealed record ManualPaperCycleCliOutputContract(
    bool IncludesManualRunRequest,
    bool IncludesPreflightResult,
    bool IncludesCycleResultWhenExecuted,
    bool IncludesPaperBaselineInput,
    bool IncludesQubesLineage,
    bool IncludesTargetPortfolio,
    bool IncludesTargetVsCurrentDiff,
    bool IncludesTheoreticalPnl,
    bool IncludesReconciliation,
    bool IncludesTheoreticalVsReal,
    bool IncludesNonExecutableRebalanceIntents,
    bool IncludesOperatorCycleReport,
    bool IncludesNoExternalAudits);

public sealed record ManualPaperCycleCliResult(
    ManualPaperCycleCliStatus CliStatus,
    ManualPaperCycleCliRejectedReason RejectedReason,
    ManualPaperCycleCliParsedRequest? ParsedRequest,
    ManualPaperCyclePreflightResult? PreflightResult,
    ManualPaperCycleRunResult? CycleRunResult,
    ManualPaperCycleOperatorReport? OperatorReport,
    bool CycleExecuted,
    int CycleExecutionCount,
    bool MoreThanOneCycleAllowed,
    bool PaperLedgerCommitted,
    bool CreatedOrder,
    bool CreatedFill,
    bool CreatedExecutionReport,
    bool CreatedRoute,
    bool SubmittedOrder,
    bool StartedSchedulerServicePolling,
    bool AutomaticExecutionIntroduced,
    bool UsedBrokerOrLiveInput,
    bool MutatedLivePositionState,
    bool MutatedBrokerPositionState,
    bool MutatedProductionLedgerState,
    bool MutatedTradingState,
    bool AudUsdTlsBoundaryInconclusiveNotFailed,
    bool UsdJpyCaveatPreserved,
    bool NoExternal);

public sealed class ManualPaperCycleCliSurface(
    ManualPaperCycleRunnerContractService preflightService,
    ManualPaperCycleFixtureRunner runner)
{
    public static ManualPaperCycleCliContract Contract { get; } = new(
        "run-manual-paper-cycle",
        ExplicitManualInvocationRequired: true,
        RequiresManualNoExternalMode: true,
        RunsAtMostOneCyclePerInvocation: true,
        CallsPreflightBeforeCycle: true,
        EmitsSafeArtifactsOnly: true,
        NoSchedulerServicePolling: true,
        NoAutomaticExecution: true,
        NoPaperLedgerCommit: true,
        NoBroker: true,
        NoLiveMarketInput: true,
        NoOrders: true,
        NoFills: true,
        NoReports: true,
        NoRoutes: true,
        NoSubmissions: true);

    public static ManualPaperCycleCliArgumentsContract ArgumentsContract { get; } = new(
        RequiredArguments:
        [
            "--mode",
            "--requested-cycle-run-id",
            "--qubes-run-id",
            "--qubes-fixture-path or --qubes-batch-reference",
            "--prior-paper-ledger-state-id",
            "--prior-continuity-gate-id",
            "--requested-by",
            "--expected-cadence-minutes",
            "--output-artifacts-dir",
            "--qubes-fixture-path, --qubes-batch-reference, or --pms-synthetic-fixture-path",
            "--allow-synthetic-pms-fixture true",
            "--allow-not-qubes-economic-output-fixture true",
            "--no-order true",
            "--no-route true",
            "--no-fill true",
            "--no-broker true",
            "--no-fix true",
            "--no-executable-schedule true",
            "--no-live-state-mutation true",
            "--no-ledger-commit true"
        ],
        OptionalArguments:
        [
            "--no-paper-ledger-commit true"
        ],
        ForbiddenArguments:
        [
            "--scheduler",
            "--service",
            "--polling",
            "--live-broker",
            "--live-market-input",
            "--trading",
            "--orders",
            "--fills",
            "--reports",
            "--broker",
            "--fix",
            "--route",
            "--executable-schedule",
            "--live-state-mutation",
            "--paper-ledger-commit"
        ],
        RequiredMode: nameof(PaperCycleRunMode.ManualNoExternal),
        ExpectedCadenceMinutes: 15);

    public static ManualPaperCycleCliPreflightContract PreflightContract { get; } = new(
        RequiresPriorPaperContinuityReadyNoExternal: true,
        RequiresPaperBaseline: true,
        RequiresQubesRunId: true,
        RequiresQubesFixtureInput: true,
        RequiresFifteenMinuteCadence: true,
        RejectsSchedulerServicePolling: true,
        RejectsLiveBoundary: true,
        RejectsOrderTradingMode: true,
        RejectsPaperLedgerCommit: true);

    public static ManualPaperCycleCliOutputContract OutputContract { get; } = new(
        IncludesManualRunRequest: true,
        IncludesPreflightResult: true,
        IncludesCycleResultWhenExecuted: true,
        IncludesPaperBaselineInput: true,
        IncludesQubesLineage: true,
        IncludesTargetPortfolio: true,
        IncludesTargetVsCurrentDiff: true,
        IncludesTheoreticalPnl: true,
        IncludesReconciliation: true,
        IncludesTheoreticalVsReal: true,
        IncludesNonExecutableRebalanceIntents: true,
        IncludesOperatorCycleReport: true,
        IncludesNoExternalAudits: true);

    public async Task<ManualPaperCycleCliResult> RunAsync(
        string[] args,
        ManualPaperCycleCliExecutionContext context,
        CancellationToken cancellationToken)
    {
        var parse = TryParse(args, context.BaselineReference);
        if (parse.Result is null)
        {
            return Rejected(parse.Reason, null, null);
        }

        var parsed = parse.Result;
        var unsafeReason = ResolveUnsafeReason(parsed);
        if (unsafeReason is not ManualPaperCycleCliRejectedReason.None)
        {
            var unsafePreflight = await preflightService.DefineAsync(
                parsed.Request,
                context.PriorContinuityGate,
                cancellationToken);
            return Rejected(unsafeReason, parsed, unsafePreflight.Contract.Preflight);
        }

        var preflight = await preflightService.DefineAsync(
            parsed.Request,
            context.PriorContinuityGate,
            cancellationToken);
        var canExecute = preflight.Contract.Preflight.PreflightStatus is PaperCyclePreflightStatus.ReadyNoExternal ||
                         preflight.Contract.Preflight.IdempotencyStatus is PaperCycleIdempotencyStatus.DuplicateReturned;
        if (!canExecute)
        {
            return Rejected(
                ManualPaperCycleCliRejectedReason.PreflightFailed,
                parsed,
                preflight.Contract.Preflight);
        }

        var execution = await runner.ExecuteOneAsync(
            parsed.Request,
            context.PriorContinuityGate,
            context.BaselineReference,
            context.PaperLedgerStateArchive,
            context.ProducedAtUtc,
            context.EffectiveAtUtc,
            context.CycleStartedAtUtc,
            context.CycleCompletedAtUtc,
            context.FundCode,
            context.ModelName,
            context.PortfolioNotional,
            context.RawQubesLines,
            context.InstrumentIdsBySymbol,
            context.WeightTolerance,
            context.NotionalTolerance,
            context.PnlTolerance,
            context.MaxMarkAge,
            context.Version,
            cancellationToken);

        return new ManualPaperCycleCliResult(
            execution.AlreadyExecuted ? ManualPaperCycleCliStatus.DuplicateReturned : ManualPaperCycleCliStatus.CompletedNoExternal,
            ManualPaperCycleCliRejectedReason.None,
            parsed,
            execution.RunResult.Preflight,
            execution.RunResult,
            execution.OperatorReport,
            CycleExecuted: !execution.AlreadyExecuted,
            CycleExecutionCount: execution.RunResult.ManualCycleExecutionCount,
            MoreThanOneCycleAllowed: false,
            PaperLedgerCommitted: execution.RunResult.PaperLedgerStateCommittedOrMutated,
            CreatedOrder: execution.RunResult.CreatedOrder,
            CreatedFill: execution.RunResult.CreatedFill,
            CreatedExecutionReport: execution.RunResult.CreatedExecutionReport,
            CreatedRoute: execution.RunResult.CreatedBrokerRoute,
            SubmittedOrder: execution.RunResult.SubmittedOrder,
            StartedSchedulerServicePolling: execution.RunResult.StartedSchedulerServicePolling,
            AutomaticExecutionIntroduced: false,
            UsedBrokerOrLiveInput: execution.RunResult.UsedBrokerOrLiveMarketData,
            MutatedLivePositionState: execution.RunResult.MutatedLivePositionState,
            MutatedBrokerPositionState: execution.RunResult.MutatedBrokerPositionState,
            MutatedProductionLedgerState: execution.RunResult.MutatedProductionLedgerState,
            MutatedTradingState: execution.RunResult.MutatedTradingState,
            AudUsdTlsBoundaryInconclusiveNotFailed: execution.RunResult.AudUsdTlsBoundaryInconclusiveNotFailed,
            UsdJpyCaveatPreserved: execution.RunResult.UsdJpyCaveatPreserved,
            NoExternal: true);
    }

    private static (ManualPaperCycleCliParsedRequest? Result, ManualPaperCycleCliRejectedReason Reason) TryParse(
        string[] args,
        PaperNextCycleBaselineReference baselineReference)
    {
        var options = ParseOptions(args);
        var hasRequired =
            HasValue(options, "--mode") &&
            HasValue(options, "--requested-cycle-run-id") &&
            HasValue(options, "--qubes-run-id") &&
            (HasValue(options, "--qubes-fixture-path") || HasValue(options, "--qubes-batch-reference") || HasValue(options, "--pms-synthetic-fixture-path")) &&
            HasValue(options, "--prior-paper-ledger-state-id") &&
            HasValue(options, "--prior-continuity-gate-id") &&
            HasValue(options, "--requested-by") &&
            HasValue(options, "--expected-cadence-minutes") &&
            HasValue(options, "--output-artifacts-dir");

        if (!hasRequired)
        {
            return (null, ManualPaperCycleCliRejectedReason.MissingRequiredArgument);
        }

        if (!Enum.TryParse<PaperCycleRunMode>(options["--mode"], ignoreCase: true, out var runMode))
        {
            return (null, ManualPaperCycleCliRejectedReason.InvalidMode);
        }

        if (!int.TryParse(options["--expected-cadence-minutes"], out var cadence))
        {
            return (null, ManualPaperCycleCliRejectedReason.InvalidCadence);
        }

        var safety = new ManualPaperCycleCliSafetyFlags(
            SchedulerRequested: Flag(options, "--scheduler"),
            ServiceRequested: Flag(options, "--service"),
            PollingRequested: Flag(options, "--polling"),
            LiveBrokerRequested: Flag(options, "--live-broker"),
            LiveMarketInputRequested: Flag(options, "--live-market-input"),
            BrokerRequested: Flag(options, "--broker"),
            FixRequested: Flag(options, "--fix"),
            RouteRequested: Flag(options, "--route"),
            ExecutableScheduleRequested: Flag(options, "--executable-schedule"),
            LiveStateMutationRequested: Flag(options, "--live-state-mutation"),
            TradingRequested: Flag(options, "--trading"),
            OrdersRequested: Flag(options, "--orders"),
            FillsRequested: Flag(options, "--fills"),
            ReportsRequested: Flag(options, "--reports"),
            SyntheticPmsFixtureAllowed: Flag(options, "--allow-synthetic-pms-fixture"),
            NotQubesEconomicOutputFixtureAllowed: Flag(options, "--allow-not-qubes-economic-output-fixture"),
            NoOrderExplicit: Flag(options, "--no-order"),
            NoRouteExplicit: Flag(options, "--no-route"),
            NoFillExplicit: Flag(options, "--no-fill"),
            NoBrokerExplicit: Flag(options, "--no-broker"),
            NoFixExplicit: Flag(options, "--no-fix"),
            NoExecutableScheduleExplicit: Flag(options, "--no-executable-schedule"),
            NoLiveStateMutationExplicit: Flag(options, "--no-live-state-mutation"),
            NoLedgerCommitExplicit: Flag(options, "--no-ledger-commit"),
            PaperLedgerCommitRequested: Flag(options, "--paper-ledger-commit"));

        var request = new ManualPaperCycleRunRequest(
            options["--requested-cycle-run-id"],
            new QubesRunId(options["--qubes-run-id"]),
            options["--requested-by"],
            DateTimeOffset.UtcNow,
            cadence,
            options.GetValueOrDefault("--qubes-fixture-path") ?? options.GetValueOrDefault("--pms-synthetic-fixture-path"),
            options.GetValueOrDefault("--qubes-batch-reference"),
            baselineReference,
            new PaperLedgerStateId(options["--prior-paper-ledger-state-id"]),
            options["--prior-continuity-gate-id"],
            runMode,
            QubesInputIsFixtureNoExternal: true,
            safety.SchedulerRequested || safety.ServiceRequested || safety.PollingRequested,
            safety.LiveBrokerRequested || safety.LiveMarketInputRequested,
            ExecuteNowRequested: false);

        return (new ManualPaperCycleCliParsedRequest(
            request,
            options["--output-artifacts-dir"],
            safety), ManualPaperCycleCliRejectedReason.None);
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                result[arg] = args[index + 1];
                index++;
            }
            else
            {
                result[arg] = "true";
            }
        }

        return result;
    }

    private static bool HasValue(IReadOnlyDictionary<string, string> options, string name)
        => options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value);

    private static bool Flag(IReadOnlyDictionary<string, string> options, string name)
        => options.TryGetValue(name, out var value) &&
           bool.TryParse(value, out var parsed) &&
           parsed;

    private static ManualPaperCycleCliRejectedReason ResolveUnsafeReason(ManualPaperCycleCliParsedRequest parsed)
    {
        if (parsed.Request.RunMode is not PaperCycleRunMode.ManualNoExternal)
        {
            return ManualPaperCycleCliRejectedReason.InvalidMode;
        }

        if (parsed.Request.ExpectedCadenceMinutes != 15)
        {
            return ManualPaperCycleCliRejectedReason.InvalidCadence;
        }

        if (!parsed.SafetyFlags.SyntheticPmsFixtureAllowed ||
            !parsed.SafetyFlags.NotQubesEconomicOutputFixtureAllowed ||
            !parsed.SafetyFlags.NoOrderExplicit ||
            !parsed.SafetyFlags.NoRouteExplicit ||
            !parsed.SafetyFlags.NoFillExplicit ||
            !parsed.SafetyFlags.NoBrokerExplicit ||
            !parsed.SafetyFlags.NoFixExplicit ||
            !parsed.SafetyFlags.NoExecutableScheduleExplicit ||
            !parsed.SafetyFlags.NoLiveStateMutationExplicit ||
            !parsed.SafetyFlags.NoLedgerCommitExplicit)
        {
            return ManualPaperCycleCliRejectedReason.MissingExplicitSafetyFlag;
        }

        if (parsed.SafetyFlags.SchedulerRequested || parsed.SafetyFlags.ServiceRequested || parsed.SafetyFlags.PollingRequested)
        {
            return ManualPaperCycleCliRejectedReason.UnsafeSchedulerServicePolling;
        }

        if (parsed.SafetyFlags.LiveBrokerRequested ||
            parsed.SafetyFlags.LiveMarketInputRequested ||
            parsed.SafetyFlags.BrokerRequested ||
            parsed.SafetyFlags.FixRequested ||
            parsed.SafetyFlags.RouteRequested ||
            parsed.SafetyFlags.LiveStateMutationRequested)
        {
            return ManualPaperCycleCliRejectedReason.UnsafeLiveBoundary;
        }

        if (parsed.SafetyFlags.ExecutableScheduleRequested)
        {
            return ManualPaperCycleCliRejectedReason.UnsafeSchedulerServicePolling;
        }

        if (parsed.SafetyFlags.TradingRequested || parsed.SafetyFlags.OrdersRequested || parsed.SafetyFlags.FillsRequested || parsed.SafetyFlags.ReportsRequested)
        {
            return ManualPaperCycleCliRejectedReason.UnsafeOrderOrTradingMode;
        }

        return parsed.SafetyFlags.PaperLedgerCommitRequested
            ? ManualPaperCycleCliRejectedReason.UnsafePaperLedgerCommit
            : ManualPaperCycleCliRejectedReason.None;
    }

    private static ManualPaperCycleCliResult Rejected(
        ManualPaperCycleCliRejectedReason reason,
        ManualPaperCycleCliParsedRequest? parsed,
        ManualPaperCyclePreflightResult? preflight)
        => new(
            reason is ManualPaperCycleCliRejectedReason.MissingRequiredArgument
                ? ManualPaperCycleCliStatus.RejectedInvalidArguments
                : reason is ManualPaperCycleCliRejectedReason.PreflightFailed
                    ? ManualPaperCycleCliStatus.RejectedPreflightFailed
                    : ManualPaperCycleCliStatus.RejectedUnsafeMode,
            reason,
            parsed,
            preflight,
            CycleRunResult: null,
            OperatorReport: null,
            CycleExecuted: false,
            CycleExecutionCount: 0,
            MoreThanOneCycleAllowed: false,
            PaperLedgerCommitted: false,
            CreatedOrder: false,
            CreatedFill: false,
            CreatedExecutionReport: false,
            CreatedRoute: false,
            SubmittedOrder: false,
            StartedSchedulerServicePolling: false,
            AutomaticExecutionIntroduced: false,
            UsedBrokerOrLiveInput: false,
            MutatedLivePositionState: false,
            MutatedBrokerPositionState: false,
            MutatedProductionLedgerState: false,
            MutatedTradingState: false,
            AudUsdTlsBoundaryInconclusiveNotFailed: true,
            UsdJpyCaveatPreserved: true,
            NoExternal: true);
}
