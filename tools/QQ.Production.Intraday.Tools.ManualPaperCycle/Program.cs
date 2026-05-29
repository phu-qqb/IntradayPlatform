using System.Globalization;
using System.Text.Json;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

var producedAt = new DateTimeOffset(2026, 05, 20, 09, 00, 00, TimeSpan.Zero);
var context = CreateContext(producedAt, args);
var surface = new ManualPaperCycleCliSurface(
    new ManualPaperCycleRunnerContractService(new InMemoryManualPaperCycleRunnerContractRepository(), new FixedClock(producedAt)),
    CreateRunner(producedAt));
var result = await surface.RunAsync(args, context, CancellationToken.None);

await WriteSafeOutputAsync(result, CancellationToken.None);
Console.WriteLine(result.CliStatus);
return result.CliStatus is ManualPaperCycleCliStatus.CompletedNoExternal or ManualPaperCycleCliStatus.DuplicateReturned ? 0 : 1;

static ManualPaperCycleFixtureRunner CreateRunner(DateTimeOffset producedAt)
{
    var state = SeedData.Create(producedAt);
    EnsureInstruments(state, ["AUDUSD", "CADUSD", "CHFUSD", "EURUSD", "GBPUSD", "JPYUSD", "NZDUSD"]);
    var clock = new FixedClock(producedAt);
    var intradayRepository = new InMemoryIntradayRepository(state);
    var batchRepository = new InMemoryModelWeightBatchRepository(state);
    var integrity = new ReferenceDataIntegrityService(intradayRepository, clock);
    var cycleService = new PaperBaselineSecondCycleService(
        new FakeModelWeightGenerator(batchRepository, clock),
        new ModelWeightPromotionService(batchRepository, intradayRepository, integrity, clock),
        new QubesWeightPersistenceService(new InMemoryQubesWeightAuditRepository(), clock),
        new InMemoryPaperBaselineSecondCycleRepository());

    return new ManualPaperCycleFixtureRunner(
        new ManualPaperCycleRunnerContractService(new InMemoryManualPaperCycleRunnerContractRepository(), clock),
        cycleService,
        new InMemoryManualPaperCycleRunResultRepository());
}

static ManualPaperCycleCliExecutionContext CreateContext(DateTimeOffset producedAt, string[] args)
{
    var state = SeedData.Create(producedAt);
    var rawQubesLines = ReadRawQubesLines(args);
    var normalizedSymbols = ResolveNormalizedSymbols(args, rawQubesLines, producedAt);
    EnsureInstruments(state, normalizedSymbols.Concat(["AUDUSD", "EURUSD", "GBPUSD"]));
    var idsBySymbol = state.Instruments
        .Where(x => normalizedSymbols.Contains(x.Symbol, StringComparer.OrdinalIgnoreCase) ||
                    x.Symbol is "AUDUSD" or "EURUSD" or "GBPUSD")
        .ToDictionary(x => x.Symbol, x => x.Id, StringComparer.OrdinalIgnoreCase);
    var archive = CreateArchive(producedAt);
    var effectiveAt = producedAt.AddMinutes(15);

    return new ManualPaperCycleCliExecutionContext(
        CreateContinuityGate(),
        CreateBaselineReference(archive),
        archive,
        producedAt,
        effectiveAt,
        producedAt,
        effectiveAt,
        "QQ_MASTER",
        "IntradayFxModel",
        1_000_000m,
        rawQubesLines,
        idsBySymbol,
        0.0000000001m,
        1m,
        1m,
        TimeSpan.FromMinutes(30),
        1);
}

static IReadOnlyList<string> ReadRawQubesLines(string[] args)
{
    var syntheticPmsFixturePath = GetOption(args, "--pms-synthetic-fixture-path");
    if (!string.IsNullOrWhiteSpace(syntheticPmsFixturePath) && File.Exists(syntheticPmsFixturePath))
    {
        var syntheticLines = File.ReadAllLines(syntheticPmsFixturePath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        var adapter = new SyntheticPmsFixtureAdapter().Adapt(new SyntheticPmsFixtureAdapterRequest(
            syntheticLines,
            Flag(args, "--allow-synthetic-pms-fixture"),
            Flag(args, "--allow-not-qubes-economic-output-fixture")));

        if (!adapter.Succeeded)
        {
            var messages = string.Join("; ", adapter.Issues.Select(x => $"row={x.RowNumber?.ToString(CultureInfo.InvariantCulture) ?? "n/a"} code={x.Code} {x.Message}"));
            throw new InvalidOperationException($"Synthetic PMS fixture adapter rejected input: {messages}");
        }

        return adapter.InternalPaperInputLines;
    }

    var fixturePath = GetOption(args, "--qubes-fixture-path");
    if (!string.IsNullOrWhiteSpace(fixturePath) && File.Exists(fixturePath))
    {
        var fixtureLines = File.ReadAllLines(fixturePath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        var qubesRunId = GetOption(args, "--qubes-run-id") ?? "qubes-r031-cli-fixture";
        var producedAt = new DateTimeOffset(2026, 05, 20, 09, 00, 00, TimeSpan.Zero);
        var netted = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId(qubesRunId),
            producedAt,
            producedAt.AddMinutes(15),
            15,
            "QQ_MASTER",
            "IntradayFxModel",
            1_000_000m,
            TargetQuantityMode.PortfolioBaseCurrencyNotional,
            fixtureLines));

        if (netted.Succeeded)
        {
            var supported = netted.NormalizedWeights
                .Where(x => IsCoreExecutionNormalizedSymbol(x.Symbol))
                .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
                .Select(x => $"{x.BloombergTicker};{x.Weight.ToString("0.######", CultureInfo.InvariantCulture)}")
                .ToArray();
            if (supported.Length > 0)
            {
                return supported;
            }
        }

        return fixtureLines;
    }

    return ["AUDUSD Curncy;0.150000", "EURUSD Curncy;0.150000", "GBPUSD Curncy;-0.260000"];
}

static bool IsCoreExecutionNormalizedSymbol(string symbol)
    => symbol.Equals("EURUSD", StringComparison.OrdinalIgnoreCase) ||
       symbol.Equals("JPYUSD", StringComparison.OrdinalIgnoreCase) ||
       symbol.Equals("AUDUSD", StringComparison.OrdinalIgnoreCase) ||
       symbol.Equals("GBPUSD", StringComparison.OrdinalIgnoreCase) ||
       symbol.Equals("NZDUSD", StringComparison.OrdinalIgnoreCase) ||
       symbol.Equals("CADUSD", StringComparison.OrdinalIgnoreCase) ||
       symbol.Equals("CHFUSD", StringComparison.OrdinalIgnoreCase);

static IReadOnlyList<string> ResolveNormalizedSymbols(
    string[] args,
    IReadOnlyList<string> rawQubesLines,
    DateTimeOffset producedAt)
{
    var qubesRunId = GetOption(args, "--qubes-run-id") ?? "qubes-r031-cli-fixture";
    var ingestion = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
        new QubesRunId(qubesRunId),
        producedAt,
        producedAt.AddMinutes(15),
        15,
        "QQ_MASTER",
        "IntradayFxModel",
        1_000_000m,
        TargetQuantityMode.PortfolioBaseCurrencyNotional,
        rawQubesLines));

    return ingestion.Succeeded
        ? ingestion.NormalizedWeights.Select(x => x.Symbol).ToArray()
        : ["AUDUSD", "EURUSD", "GBPUSD"];
}

static string? GetOption(string[] args, string option)
{
    for (var index = 0; index < args.Length - 1; index++)
    {
        if (args[index].Equals(option, StringComparison.OrdinalIgnoreCase))
        {
            return args[index + 1];
        }
    }

    return null;
}

static bool Flag(string[] args, string option)
{
    var value = GetOption(args, option);
    return !string.IsNullOrWhiteSpace(value) &&
           bool.TryParse(value, out var parsed) &&
           parsed;
}

static async Task WriteSafeOutputAsync(ManualPaperCycleCliResult result, CancellationToken cancellationToken)
{
    var outputDirectory = result.ParsedRequest?.OutputArtifactsDirectory;
    if (string.IsNullOrWhiteSpace(outputDirectory))
    {
        return;
    }

    Directory.CreateDirectory(outputDirectory);
    var payload = new
    {
        result.CliStatus,
        result.RejectedReason,
        requestedCycleRunId = result.ParsedRequest?.Request.RequestedCycleRunId,
        qubesRunId = result.ParsedRequest?.Request.QubesRunId?.Value,
        result.PreflightResult?.PreflightStatus,
        cycleRunStatus = result.CycleRunResult?.RunStatus,
        result.CycleExecuted,
        result.CycleExecutionCount,
        rawRowCount = result.CycleRunResult?.CycleResult.QubesWeights.RawInputRowCount,
        normalizedRowCount = result.CycleRunResult?.CycleResult.QubesWeights.NormalizedOutputRowCount,
        noExternal = result.NoExternal,
        noPaperLedgerCommit = !result.PaperLedgerCommitted,
        noOrder = !result.CreatedOrder,
        noFill = !result.CreatedFill,
        noReport = !result.CreatedExecutionReport,
        noRoute = !result.CreatedRoute,
        noSubmission = !result.SubmittedOrder
    };
    var path = Path.Combine(outputDirectory, "phase-pms-ems-oms-r031-cli-manual-run-output.json");
    await File.WriteAllTextAsync(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);

    if (result.CycleRunResult is not null)
    {
        await WriteLineLevelArtifactsAsync(outputDirectory, result, cancellationToken);
    }
}

static async Task WriteLineLevelArtifactsAsync(
    string outputDirectory,
    ManualPaperCycleCliResult result,
    CancellationToken cancellationToken)
{
    var cycle = result.CycleRunResult!;
    var sourceFixturePath = result.ParsedRequest?.Request.QubesFixturePath;
    var rawBySymbol = cycle.CycleResult.QubesWeights.RawRows
        .GroupBy(x => x.Pair, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
    var normalizedBySymbol = cycle.CycleResult.QubesWeights.NormalizedWeights
        .ToDictionary(x => x.Symbol, StringComparer.OrdinalIgnoreCase);
    var linePayloads = cycle.CycleResult.RebalanceIntents
        .OrderBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase)
        .Select(intent =>
        {
            normalizedBySymbol.TryGetValue(intent.Symbol, out var normalized);
            var mapping = MapExecutionSymbol(intent.Symbol);
            var missing = new List<string>
            {
                "MissingCanonicalTargetClose",
                "MissingQuoteWindowReadinessBinding",
                "MissingCloseBenchmarkReadinessBinding",
                "MissingFeedQualityReadinessBinding",
                "MissingRiskReview",
                "MissingOperatorApproval"
            };

            return new
            {
                PaperExecutionPlanLineId = $"{cycle.Request.RequestedCycleRunId}:paper-execution-plan:{intent.Symbol}:line",
                PaperExecutionPlanId = $"{cycle.Request.RequestedCycleRunId}:paper-execution-plan",
                CycleRunId = cycle.Request.RequestedCycleRunId,
                QubesRunId = cycle.Request.QubesRunId?.Value,
                SourceQubesFixturePath = sourceFixturePath,
                SourceBloombergTicker = normalized?.BloombergTicker,
                SourceBloombergTickerWasRawFixtureRow = normalized is not null && rawBySymbol.ContainsKey(normalized.Symbol),
                RawWeight = normalized?.Weight,
                NetCurrencyExposure = normalized?.Weight,
                Symbol = intent.Symbol,
                TargetWeight = intent.TargetWeight,
                CurrentWeight = intent.CurrentWeight,
                DeltaWeight = intent.DeltaWeight,
                SourceClassification = normalized is not null
                    ? "SyntheticTarget"
                    : Math.Abs(intent.TargetWeight) == 0m && Math.Abs(intent.CurrentWeight) > 0m
                        ? "PriorPaperBaseline"
                        : "Other",
                PreviewLineReason = normalized is not null
                    ? "SyntheticTarget"
                    : Math.Abs(intent.TargetWeight) == 0m && Math.Abs(intent.CurrentWeight) > 0m
                        ? "PriorBaselineZeroTarget"
                        : "Other",
                ExecutionTradableSymbol = mapping.ExecutionTradableSymbol,
                NormalizedPortfolioSymbol = mapping.NormalizedPortfolioSymbol,
                RequiresInversion = mapping.RequiresInversion,
                SecurityID = mapping.SecurityId,
                SecurityIDSource = mapping.SecurityIdSource,
                Side = intent.IntentSide.ToString(),
                TargetQuantity = (decimal?)null,
                CurrentNotional = intent.CurrentNotional,
                TargetNotional = intent.TargetNotional,
                DeltaNotional = intent.DeltaNotional,
                ArithmeticValid = intent.DeltaWeight == intent.TargetWeight - intent.CurrentWeight &&
                                  intent.DeltaNotional == intent.TargetNotional - intent.CurrentNotional,
                CanonicalTargetCloseTimestamp = (DateTimeOffset?)null,
                CanonicalSession = (object?)null,
                CanonicalQuarterHourTimestampConfirmed = false,
                RiskReviewId = (string?)null,
                LotSizingId = (string?)null,
                RebalanceIntentId = $"{cycle.Request.RequestedCycleRunId}:rebalance-intent:{intent.Symbol}",
                PaperCandidateId = (string?)null,
                NonExecutable = true,
                ExecutionAllowed = false,
                NotAnOrder = true,
                NotSubmitted = true,
                NoBrokerRoute = true,
                NoFixMessage = true,
                NoChildSlices = true,
                NoExecutableSchedule = true,
                NoFill = true,
                NoExecutionReport = true,
                NoRoute = true,
                NoSubmission = true,
                NoPaperLedgerCommit = true,
                MissingEvidence = missing,
                HoldReason = "Line emitted from ManualNoExternal no-external pipeline; canonical target close, readiness bindings, risk review, and operator approval are not present in the current CLI inputs."
            };
        })
        .ToArray();

    var planPayload = new
    {
        PaperExecutionPlanId = $"{cycle.Request.RequestedCycleRunId}:paper-execution-plan",
        CycleRunId = cycle.Request.RequestedCycleRunId,
        QubesRunId = cycle.Request.QubesRunId?.Value,
        SourceQubesFixturePath = sourceFixturePath,
        LineCount = linePayloads.Length,
        PlanStatus = "PaperPlanPartiallyReadyMissingR009ReadinessBindings",
        DerivedFromQubesFixtureAndManualNoExternalPipeline = true,
        DirectCrossExecutionDisabled = true,
        USDPairNormalizedOnly = true,
        NonExecutable = true,
        NotAnOrder = true,
        NotSubmitted = true,
        NoBrokerRoute = true,
        NoFixMessage = true,
        NoChildSlices = true,
        NoExecutableSchedule = true,
        NoFill = true,
        NoExecutionReport = true,
        NoRoute = true,
        NoSubmission = true,
        NoPaperLedgerCommit = true,
        ExecutionAllowed = false,
        MissingEvidence = new[]
        {
            "MissingCanonicalTargetClose",
            "MissingQuoteWindowReadinessBinding",
            "MissingCloseBenchmarkReadinessBinding",
            "MissingFeedQualityReadinessBinding",
            "MissingRiskReview",
            "MissingOperatorApproval"
        }
    };

    var options = new JsonSerializerOptions { WriteIndented = true };
    await File.WriteAllTextAsync(
        Path.Combine(outputDirectory, "phase-pms-ems-oms-manual-noexternal-paper-execution-plan.json"),
        JsonSerializer.Serialize(planPayload, options),
        cancellationToken);
    await File.WriteAllTextAsync(
        Path.Combine(outputDirectory, "phase-pms-ems-oms-manual-noexternal-paper-execution-plan-lines.json"),
        JsonSerializer.Serialize(new { Lines = linePayloads }, options),
        cancellationToken);
}

static (string ExecutionTradableSymbol, string NormalizedPortfolioSymbol, bool RequiresInversion, string? SecurityId, string? SecurityIdSource) MapExecutionSymbol(string normalizedSymbol)
    => normalizedSymbol.ToUpperInvariant() switch
    {
        "JPYUSD" => ("USDJPY", "JPYUSD", true, "4004", "8"),
        "CADUSD" => ("USDCAD", "CADUSD", true, null, null),
        "CHFUSD" => ("USDCHF", "CHFUSD", true, null, null),
        _ => (normalizedSymbol.ToUpperInvariant(), normalizedSymbol.ToUpperInvariant(), false, null, null)
    };

static void EnsureInstruments(PlatformState state, IEnumerable<string> symbols)
{
    foreach (var symbol in symbols.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        if (state.Instruments.Any(x => x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
        {
            continue;
        }

        state.Instruments.Add(new Instrument(
            InstrumentId.New(),
            symbol,
            AssetClass.FxSpot,
            new Currency(symbol[..3]),
            Currency.Usd,
            5,
            1));
    }
}

static PaperCycleContinuityDecisionGate CreateContinuityGate()
    => new(
        "cycle-r026-second-paper-baseline:paper-continuity-archive:paper-continuity-gate",
        new SecondCyclePaperContinuityArchiveId("cycle-r026-second-paper-baseline:paper-continuity-archive"),
        PaperCycleContinuityStatus.PaperContinuityReadyNoExternal,
        FutureManualPaperCyclesMayUseLatestPaperLedgerFixtureBaseline: true,
        StartsSchedulerOrService: false,
        RunsAnotherCycle: false,
        IngestsNewQubesBatch: false,
        MutatesPaperLedgerState: false,
        MutatesLivePositionState: false,
        MutatesBrokerPositionState: false,
        MutatesProductionLedgerState: false,
        MutatesTradingState: false,
        CreatesOrderCandidates: false,
        CreatesExecutionPlans: false,
        CreatesOrders: false,
        CreatesFills: false,
        CreatesExecutionReports: false,
        CreatesRoutes: false,
        SubmitsOrders: false,
        NoExternal: true,
        PreservesNonExecutableRebalanceIntents: true);

static PaperLedgerStateArchiveRecord CreateArchive(DateTimeOffset producedAt)
{
    var stateArchiveId = new PaperLedgerStateArchiveId("paper-ledger-commit-r025-sample:paper-ledger-state:archive");
    var stateId = new PaperLedgerStateId("paper-ledger-commit-r025-sample:paper-ledger-state");
    var commitId = new PaperLedgerCommitId("paper-ledger-commit-r025-sample");
    var previewId = new PaperPositionLedgerPreviewId("paper-ledger-preview-r025-sample");
    var decisionId = new OperatorDecisionId("decision-r025-approve-ledger-commit-readiness");
    var lines = new[]
    {
        ArchiveLine(stateId, commitId, previewId, decisionId, "AUDUSD", "AUD", 131000m),
        ArchiveLine(stateId, commitId, previewId, decisionId, "EURUSD", "EUR", 124000m),
        ArchiveLine(stateId, commitId, previewId, decisionId, "GBPUSD", "GBP", -368000m)
    };

    return new PaperLedgerStateArchiveRecord(
        stateArchiveId,
        stateId,
        commitId,
        previewId,
        new PaperPositionPreviewId("paper-position-preview-r025-sample"),
        new PaperSimulationResultId("paper-simulation-result-r025-sample"),
        new PaperSimulationPlanId("paper-simulation-plan-r025-sample"),
        new PaperExecutionPlanId("paper-execution-plan-r025-sample"),
        "cycle-r025-sample",
        "qubes-r025-sample",
        decisionId,
        producedAt,
        PaperLedgerCommitStatus.PaperLedgerCommittedNoExternal,
        "PaperLedgerFixtureStateOnly",
        3,
        10,
        PaperLedgerStateArchiveStatus.ArchivedPaperFixtureState,
        lines,
        Array.Empty<BlockedPaperReviewLineRecord>(),
        QubesLineagePreserved: true,
        CycleLineagePreserved: true,
        OperatorDecisionLineagePreserved: true,
        LedgerCommitLineagePreserved: true,
        LedgerPreviewLineagePreserved: true,
        PositionPreviewLineagePreserved: true,
        SimulationResultLineagePreserved: true,
        SimulationPlanLineagePreserved: true,
        PaperExecutionPlanLineagePreserved: true,
        PaperCandidateLineagePreserved: true,
        RiskLineagePreserved: true,
        RebalanceIntentLineagePreserved: true,
        LotSizingLineagePreserved: true,
        MissingStaleMarkWarningsPreserved: true,
        DriftAcknowledgementPreserved: true,
        PaperOnly: true,
        NoExternal: true,
        FixtureState: true,
        NotProductionLedger: true,
        NotBrokerPosition: true,
        NotTradingState: true,
        NoProductionLedgerMutation: true,
        NoLivePositionMutation: true,
        NoBrokerPositionMutation: true,
        NoTradingStateMutation: true,
        NoFillCreated: true,
        NoExecutionReportCreated: true,
        NoOrderCreated: true,
        NoBrokerRoute: true,
        NotSubmitted: true,
        PaperLedgerMutatedAgain: false,
        NewCycleRan: false,
        NewQubesBatchIngested: false,
        LivePositionStateMutated: false,
        BrokerPositionStateMutated: false,
        ProductionLedgerStateMutated: false,
        TradingStateMutated: false,
        FillCreated: false,
        ExecutionReportCreated: false,
        OmsOrderCreated: false,
        ParentOrderCreated: false,
        ChildOrderCreated: false,
        BrokerOrderCreated: false,
        OrderStateCreated: false,
        SubmittedOrders: false,
        BrokerRouteCreated: false);
}

static PaperLedgerStateLineArchiveRecord ArchiveLine(
    PaperLedgerStateId stateId,
    PaperLedgerCommitId commitId,
    PaperPositionLedgerPreviewId previewId,
    OperatorDecisionId decisionId,
    string symbol,
    string currency,
    decimal quantity)
    => new(
        new PaperLedgerStateLineId($"{stateId.Value}:{symbol}:line"),
        stateId,
        commitId,
        previewId,
        "cycle-r025-sample",
        "qubes-r025-sample",
        decisionId,
        InstrumentId.New(),
        symbol,
        currency,
        quantity,
        "PaperLedgerFixtureStateOnly",
        $"paperLedgerStateLine={symbol}",
        PaperOnly: true,
        NoExternal: true,
        FixtureState: true,
        NotProductionLedger: true,
        NotBrokerPosition: true,
        NotTradingState: true,
        NoLivePositionMutation: true,
        NoBrokerPositionMutation: true,
        NoProductionLedgerMutation: true,
        NoTradingStateMutation: true,
        NoFillCreated: true,
        NoExecutionReportCreated: true,
        NoOrderCreated: true,
        NoBrokerRoute: true,
        NotSubmitted: true);

static PaperNextCycleBaselineReference CreateBaselineReference(PaperLedgerStateArchiveRecord archive)
    => new(
        "paper-next-cycle-baseline-r025",
        archive.PaperLedgerStateArchiveId,
        archive.PaperLedgerStateId,
        archive.PaperLedgerCommitId,
        archive.CycleRunId,
        archive.QubesRunId,
        "PaperLedgerFixture",
        "R024 committed paper ledger state",
        BaselineIsProduction: false,
        BaselineIsBroker: false,
        BaselineIsLiveTrading: false,
        PaperOnly: true,
        NoExternal: true,
        FixtureState: true,
        NewCycleRan: false,
        NewQubesBatchIngested: false,
        PaperLedgerMutatedAgain: false);
