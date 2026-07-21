using System.Globalization;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class PmsShadowStateContract
{
    public const string SchemaName = "pms_shadow";
    public const string ContractVersion = "postgresql_pms_shadow_state_contract_v1";
    public const string InitialMigrationId = "20260721152240_InitialPostgreSqlPmsShadowState";
    public const string CorrectiveMigrationId = "20260721175549_CorrectGitCommitIdentityContract";
    public const string MigrationId = InitialMigrationId;
    public static readonly IReadOnlyList<string> MigrationIds = [InitialMigrationId, CorrectiveMigrationId];
    public const string EvidenceClassification = "EVIDENCE_ONLY_NONACCOUNTING";
    public const string NoOrderClassification = "NO_ORDER";
    public const string TestEnvironment = "LMAX_TEST_EOD_ONLY";
    public const string WorkingLeavesUnavailable = "UNAVAILABLE_WITH_CURRENT_LMAX_INTERFACES";
    public const string BrokerAdjustedBlocker = "BROKER_WORKING_LEAVES_UNOBSERVABLE";
    public const string BrokerAdjustedImpact = "BROKER_ADJUSTED_DRIFT_NOT_COMPUTABLE";
    public const string CompletedNoExternal = "CompletedNoExternal";
    public const string DisabledBrokerSend = "DISABLED_NO_ORDER_ENTRY";
}

public static class GitCommitIdentityContract
{
    public const string Version = "git_commit_identity_v1";
    public const string Sha1 = "sha1";
    public const string Sha256 = "sha256";

    public static bool IsValid(string? commitId, string? objectFormat)
    {
        if (string.IsNullOrEmpty(commitId) || string.IsNullOrEmpty(objectFormat) ||
            commitId.Any(character => !char.IsAsciiHexDigit(character) || char.IsUpper(character)))
            return false;

        return objectFormat switch
        {
            Sha1 => commitId.Length == 40,
            Sha256 => commitId.Length == 64,
            _ => false
        };
    }

    public static string DetectObjectFormat(string commitId)
    {
        if (IsValid(commitId, Sha1)) return Sha1;
        if (IsValid(commitId, Sha256)) return Sha256;
        throw new InvalidDataException("GIT_COMMIT_IDENTITY_INVALID");
    }
}

public sealed record Arch6cArtifactReference(
    string ArtifactType,
    string Sha256,
    long SizeBytes,
    string LogicalUri,
    string ContractVersion,
    DateTimeOffset ProducedAtUtc,
    string SourceSystem,
    string Classification);

public sealed record Arch6cQubesInputBinding(
    string StrategyId,
    string SourceSnapshotSha256,
    string OverlaySha256,
    string? GapLedgerSha256,
    string MappingSha256,
    string InputSnapshotSha256,
    int SourceInstrumentCount,
    int GapCount,
    DateTimeOffset TargetCloseUtc,
    string Provenance);

public sealed record Arch6cQualifiedShadowSession(
    string SourceGate,
    string SourceSessionId,
    string SourceEvidenceSha256,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<Arch6cArtifactReference> Artifacts,
    IReadOnlyList<Arch6cQubesInputBinding> QubesInputs,
    Arch6aOperationalPositionShadowResult ShadowResult);

public sealed record PmsShadowIngestionRow(
    Guid IngestionId,
    string SourceGate,
    string SourceSessionId,
    string SourceEvidenceSha256,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string ContractVersion,
    string Environment,
    string Classification,
    string RowsetSha256);

public sealed record PmsShadowSourceArtifactRow(
    Guid ArtifactId,
    Guid IngestionId,
    string ArtifactType,
    string Sha256,
    long SizeBytes,
    string LogicalUri,
    string ContractVersion,
    DateTimeOffset ProducedAtUtc,
    string SourceSystem,
    string Classification);

public sealed record PmsShadowQubesInputSnapshotRow(
    Guid SnapshotId,
    Guid IngestionId,
    Guid InputArtifactId,
    Guid SourceSnapshotArtifactId,
    Guid OverlayArtifactId,
    string StrategyId,
    string SourceSnapshotSha256,
    string OverlaySha256,
    string? GapLedgerSha256,
    string MappingSha256,
    string InputSha256,
    DateTimeOffset TargetCloseUtc,
    int SourceInstrumentCount,
    int GapCount,
    string Provenance,
    string Classification);

public sealed record PmsShadowAccountSnapshotRow(
    Guid AccountSnapshotId,
    Guid IngestionId,
    string AccountId,
    string Scope,
    string BaseCurrency,
    decimal NavOrEquity,
    DateOnly ReportDate,
    DateTimeOffset AsOfUtc,
    string Authority,
    string SourceArtifactSha256,
    string SnapshotSha256,
    string Classification);

public sealed record PmsShadowPositionSnapshotRow(
    Guid PositionSnapshotId,
    Guid IngestionId,
    Guid AccountSnapshotId,
    DateOnly ReportDate,
    DateTimeOffset AsOfUtc,
    string SnapshotSha256,
    bool EmptyStateWasExplicitlyObserved,
    bool EmptyStateWasInferred,
    bool BrokerAuthority,
    string Classification);

public sealed record PmsShadowPositionSnapshotLineRow(
    Guid PositionSnapshotId,
    Guid InstrumentId,
    string SecurityId,
    string Symbol,
    decimal CurrentBaseQuantity);

public sealed record PmsShadowMarketDataSnapshotRow(
    Guid MarketDataSnapshotId,
    Guid IngestionId,
    DateTimeOffset AsOfUtc,
    string SnapshotSha256,
    int ObservationCount,
    string Classification);

public sealed record PmsShadowMarketDataObservationRow(
    Guid MarketDataSnapshotId,
    Guid InstrumentId,
    string SecurityId,
    string LmaxInstrumentId,
    string Symbol,
    decimal Bid,
    decimal Ask,
    DateTimeOffset EventTimeUtc,
    DateTimeOffset ReceivedAtUtc,
    long StalenessMilliseconds,
    string SourceCaptureId,
    string SourceFileSha256,
    string ProjectionMethod,
    string ProjectionLegSecurityIdsJson);

public sealed record PmsShadowSecurityMappingRow(
    Guid IngestionId,
    Guid InstrumentId,
    Guid VenueId,
    Guid VenueInstrumentId,
    string SecurityId,
    string Symbol,
    string LmaxInstrumentId,
    decimal QuantityMultiplier,
    decimal QuantityIncrement,
    decimal PriceIncrement,
    string MappingSha256);

public sealed record PmsShadowWorkingLeavesObservationRow(
    Guid WorkingLeavesObservationId,
    Guid IngestionId,
    string Status,
    string SourceSystem,
    bool ObservationAttempted,
    bool EmptyStateObserved,
    bool EmptyStateInferred,
    bool BrokerAuthority,
    string Reason,
    string Impact,
    DateTimeOffset AsOfUtc,
    string Classification);

public sealed record PmsShadowModelRunRow(
    Guid ModelRunId,
    Guid IngestionId,
    Guid QubesInputSnapshotId,
    Guid OutputArtifactId,
    string ExternalModelRunId,
    string SourceDomainModel,
    string StrategyId,
    decimal BenchmarkParameter,
    DateTimeOffset TargetCloseUtc,
    DateTimeOffset AsOfUtc,
    string CoreMasterCommitId,
    string CoreMasterObjectFormat,
    string PackageSha256,
    string EngineSha256,
    int WrapperExitCode,
    int NativeExitCode,
    string SemanticStatus,
    string R083Status,
    string OutputSha256,
    string ContractVersion,
    string Classification,
    bool AccountingEligible,
    bool ExecutionAllowed,
    bool NotAnOrder);

public sealed record PmsShadowTargetWeightRow(
    Guid ModelRunId,
    Guid InstrumentId,
    string SecurityId,
    decimal Weight,
    DateTimeOffset TargetCloseUtc,
    string SourceRowKey,
    int SourceOrder,
    string OutputSha256,
    string LineageVersion);

public sealed record PmsShadowTargetPositionStageRow(
    Guid StageId,
    Guid ModelRunId,
    Guid AccountSnapshotId,
    Guid MarketDataSnapshotId,
    string Status,
    string Classification,
    bool AccountingEligible,
    bool ExecutionAllowed);

public sealed record PmsShadowTargetPositionRow(
    Guid StageId,
    Guid ModelRunId,
    Guid InstrumentId,
    string SecurityId,
    decimal TargetNotionalUsd,
    decimal TargetBaseQuantity,
    decimal TargetVenueQuantity,
    string SizingPolicy,
    string RoundingPolicy,
    string Status,
    string Classification);

public sealed record PmsShadowPositionOnlyDriftStageRow(
    Guid StageId,
    Guid ModelRunId,
    Guid PositionSnapshotId,
    DateTimeOffset AsOfUtc,
    string Status,
    string Classification);

public sealed record PmsShadowPositionOnlyDriftRow(
    Guid StageId,
    Guid ModelRunId,
    Guid InstrumentId,
    string SecurityId,
    decimal CurrentBaseQuantity,
    decimal TargetBaseQuantity,
    decimal PositionOnlyDeltaBaseQuantity,
    DateTimeOffset AsOfUtc,
    string Status);

public sealed record PmsShadowBrokerAdjustedDriftStageRow(
    Guid StageId,
    Guid ModelRunId,
    Guid WorkingLeavesObservationId,
    bool Calculated,
    string Blocker,
    bool EmptyStateInferred,
    string Status,
    string Classification);

public sealed record PmsShadowCycleResultRow(
    Guid ResultId,
    Guid IngestionId,
    Guid ModelRunId,
    string ManualPaperCycleStatus,
    string R009Status,
    bool ExecutionAllowed,
    bool NotAnOrder,
    bool NoBrokerRoute,
    bool NoFixMessage,
    bool OrderEntryEnabled,
    string BrokerSendStatus,
    int TradeIntentCount,
    DateTimeOffset CompletedAtUtc,
    string Classification);

public sealed record PmsShadowPersistencePlan(
    PmsShadowIngestionRow Ingestion,
    IReadOnlyList<PmsShadowSourceArtifactRow> SourceArtifacts,
    IReadOnlyList<PmsShadowQubesInputSnapshotRow> QubesInputSnapshots,
    PmsShadowAccountSnapshotRow AccountSnapshot,
    PmsShadowPositionSnapshotRow PositionSnapshot,
    IReadOnlyList<PmsShadowPositionSnapshotLineRow> PositionSnapshotLines,
    PmsShadowMarketDataSnapshotRow MarketDataSnapshot,
    IReadOnlyList<PmsShadowMarketDataObservationRow> MarketDataObservations,
    IReadOnlyList<PmsShadowSecurityMappingRow> SecurityMappings,
    PmsShadowWorkingLeavesObservationRow WorkingLeavesObservation,
    IReadOnlyList<PmsShadowModelRunRow> ModelRuns,
    IReadOnlyList<PmsShadowTargetWeightRow> TargetWeights,
    IReadOnlyList<PmsShadowTargetPositionStageRow> TargetPositionStages,
    IReadOnlyList<PmsShadowTargetPositionRow> TargetPositions,
    IReadOnlyList<PmsShadowPositionOnlyDriftStageRow> PositionOnlyDriftStages,
    IReadOnlyList<PmsShadowPositionOnlyDriftRow> PositionOnlyDrifts,
    IReadOnlyList<PmsShadowBrokerAdjustedDriftStageRow> BrokerAdjustedDriftStages,
    IReadOnlyList<PmsShadowCycleResultRow> CycleResults,
    string RowsetSha256);

public sealed record PmsShadowPlanValidation(bool IsValid, IReadOnlyList<string> Issues);

public static class Arch6cPmsShadowPersistencePlanner
{
    public static PmsShadowPersistencePlan Build(Arch6cQualifiedShadowSession input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var result = input.ShadowResult;
        var bundle = result.InputBundle;
        var preview = result.Preview;
        var ingestionId = Id($"ingestion:{input.SourceSessionId}");
        var artifactBySha = input.Artifacts
            .GroupBy(x => x.Sha256, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var values = group.ToArray();
                    if (values.Skip(1).Any(value => value != values[0]))
                    {
                        throw new InvalidDataException("SOURCE_ARTIFACT_SHA_CONTRADICTION");
                    }
                    return values[0];
                },
                StringComparer.Ordinal);
        Guid ArtifactId(string sha) => Id($"artifact:{sha}");
        PmsShadowSourceArtifactRow Artifact(Arch6cArtifactReference value) => new(
            ArtifactId(value.Sha256), ingestionId, value.ArtifactType, value.Sha256, value.SizeBytes,
            value.LogicalUri, value.ContractVersion, value.ProducedAtUtc, value.SourceSystem, value.Classification);
        Arch6cArtifactReference RequiredArtifact(string sha)
            => artifactBySha.GetValueOrDefault(sha) ?? throw new InvalidDataException($"SOURCE_ARTIFACT_MISSING:{sha}");

        var sourceArtifacts = artifactBySha.Values.OrderBy(x => x.Sha256, StringComparer.Ordinal).Select(Artifact).ToArray();
        var bindingByStrategy = input.QubesInputs.ToDictionary(x => x.StrategyId, StringComparer.Ordinal);
        var qubesInputs = preview.Runs.OrderBy(x => x.ModelRun.StrategyId, StringComparer.Ordinal).Select(run =>
        {
            var binding = bindingByStrategy.GetValueOrDefault(run.ModelRun.StrategyId)
                ?? throw new InvalidDataException($"QUBES_INPUT_BINDING_MISSING:{run.ModelRun.StrategyId}");
            RequiredArtifact(binding.InputSnapshotSha256);
            RequiredArtifact(binding.SourceSnapshotSha256);
            RequiredArtifact(binding.OverlaySha256);
            return new PmsShadowQubesInputSnapshotRow(
                Id($"qubes-input:{binding.InputSnapshotSha256}"), ingestionId,
                ArtifactId(binding.InputSnapshotSha256), ArtifactId(binding.SourceSnapshotSha256), ArtifactId(binding.OverlaySha256),
                binding.StrategyId, binding.SourceSnapshotSha256, binding.OverlaySha256, binding.GapLedgerSha256,
                binding.MappingSha256, binding.InputSnapshotSha256, binding.TargetCloseUtc, binding.SourceInstrumentCount,
                binding.GapCount, binding.Provenance, PmsShadowStateContract.EvidenceClassification);
        }).ToArray();
        var qubesInputByStrategy = qubesInputs.ToDictionary(x => x.StrategyId, StringComparer.Ordinal);

        var account = bundle.Account;
        var accountSnapshotId = Id($"account:{account.SnapshotSha256}");
        var accountRow = new PmsShadowAccountSnapshotRow(
            accountSnapshotId, ingestionId, account.AccountId, account.AccountScope, account.BaseCurrency,
            account.NavOrEquity, account.ReportDate, account.AsOfUtc, account.Authority,
            account.SourceFiles[0].Sha256, account.SnapshotSha256, PmsShadowStateContract.EvidenceClassification);
        var positionSnapshotId = Id($"positions:{bundle.Positions.SnapshotSha256}");
        var positionRow = new PmsShadowPositionSnapshotRow(
            positionSnapshotId, ingestionId, accountSnapshotId, bundle.Positions.ReportDate, bundle.Positions.AsOfUtc,
            bundle.Positions.SnapshotSha256, bundle.Positions.EmptyStateWasExplicitlyObserved,
            bundle.Positions.EmptyStateWasInferred, bundle.Positions.BrokerAuthority, PmsShadowStateContract.EvidenceClassification);
        var mappingBySecurity = bundle.SecurityMappings.ToDictionary(x => x.SecurityId, StringComparer.Ordinal);
        var positionLines = bundle.Positions.Positions.OrderBy(x => x.SecurityId, StringComparer.Ordinal).Select(value => new PmsShadowPositionSnapshotLineRow(
            positionSnapshotId, mappingBySecurity[value.SecurityId].InstrumentId, value.SecurityId, value.Symbol, value.CurrentBaseQuantity)).ToArray();

        var marketDataSnapshotId = Id($"market:{bundle.MarketData.SnapshotSha256}");
        var marketRow = new PmsShadowMarketDataSnapshotRow(
            marketDataSnapshotId, ingestionId, bundle.MarketData.AsOfUtc, bundle.MarketData.SnapshotSha256,
            bundle.MarketData.Quotes.Count, PmsShadowStateContract.EvidenceClassification);
        var marketObservations = bundle.MarketData.Quotes.OrderBy(x => x.SecurityId, StringComparer.Ordinal).Select(value => new PmsShadowMarketDataObservationRow(
            marketDataSnapshotId, mappingBySecurity[value.SecurityId].InstrumentId, value.SecurityId, value.LmaxInstrumentId,
            value.Symbol, value.Bid, value.Ask, value.EventTimeUtc, value.ReceivedAtUtc, value.StalenessMilliseconds,
            value.SourceCaptureId, value.SourceFileSha256, value.ProjectionMethod,
            System.Text.Json.JsonSerializer.Serialize(value.ProjectionLegSecurityIds))).ToArray();
        var securityMappings = bundle.SecurityMappings.OrderBy(x => x.SecurityId, StringComparer.Ordinal).Select(value => new PmsShadowSecurityMappingRow(
            ingestionId, value.InstrumentId, value.VenueId, value.VenueInstrumentId, value.SecurityId, value.Symbol,
            value.LmaxInstrumentId, value.QuantityMultiplier, value.QuantityIncrement, value.PriceIncrement,
            bundle.QubesToLmaxMappingSha256)).ToArray();

        var leaves = bundle.BrokerWorkingLeaves;
        var leavesId = Id($"working-leaves:{input.SourceSessionId}:{leaves.Status}:{leaves.Impact}");
        var leavesRow = new PmsShadowWorkingLeavesObservationRow(
            leavesId, ingestionId, leaves.Status, leaves.SourceSystem, leaves.ObservationAttempted, leaves.EmptyStateObserved,
            leaves.EmptyStateInferred, leaves.BrokerAuthority, leaves.Reason, leaves.Impact, bundle.MarketData.AsOfUtc,
            bundle.WorkingLeavesClassification);

        var modelRuns = new List<PmsShadowModelRunRow>();
        var targetWeights = new List<PmsShadowTargetWeightRow>();
        var targetStages = new List<PmsShadowTargetPositionStageRow>();
        var targetPositions = new List<PmsShadowTargetPositionRow>();
        var driftStages = new List<PmsShadowPositionOnlyDriftStageRow>();
        var drifts = new List<PmsShadowPositionOnlyDriftRow>();
        var brokerStages = new List<PmsShadowBrokerAdjustedDriftStageRow>();
        var cycleResults = new List<PmsShadowCycleResultRow>();

        foreach (var run in preview.Runs.OrderBy(x => x.ModelRun.StrategyId, StringComparer.Ordinal))
        {
            var lineage = run.Lineage;
            var modelRunId = Id(run.ModelRun.ModelRunPreviewId);
            RequiredArtifact(lineage.OutputSha256);
            modelRuns.Add(new(
                modelRunId, ingestionId, qubesInputByStrategy[run.ModelRun.StrategyId].SnapshotId,
                ArtifactId(lineage.OutputSha256), run.ModelRun.ModelRunPreviewId, run.ModelRun.SourceDomainModel,
                run.ModelRun.StrategyId, lineage.BenchmarkParameter, lineage.TargetCloseUtc, run.ModelRun.AsOfUtc,
                lineage.SourceMasterSha, GitCommitIdentityContract.DetectObjectFormat(lineage.SourceMasterSha),
                lineage.RunnerPackageSha256, lineage.ExecutableSha256, 0, 0, "SUCCEEDED",
                lineage.R083Status, lineage.OutputSha256, run.ModelRun.ContractVersion,
                PmsShadowStateContract.EvidenceClassification, false, false, true));

            foreach (var weight in run.TargetWeights.OrderBy(x => x.Order))
            {
                var instrumentId = Guid.Parse(weight.InstrumentId ?? throw new InvalidDataException("TARGET_WEIGHT_INSTRUMENT_MISSING"));
                targetWeights.Add(new(modelRunId, instrumentId, weight.SecurityId,
                    decimal.Parse(weight.ExactWeightText, NumberStyles.Float, CultureInfo.InvariantCulture), weight.TargetCloseUtc,
                    weight.SourceRowKey, weight.Order, weight.SourceOutputSha256, lineage.LineageContractVersion));
            }

            var targetStageId = Id($"target-position-stage:{run.ModelRun.ModelRunPreviewId}");
            targetStages.Add(new(targetStageId, modelRunId, accountSnapshotId, marketDataSnapshotId,
                run.TargetPositions.ComputationStatus.ToString(), PmsShadowStateContract.EvidenceClassification, false, false));
            foreach (var position in run.TargetPositions.Positions.OrderBy(x => x.InstrumentId, StringComparer.Ordinal))
            {
                var securityId = run.TargetWeights.Single(x => x.InstrumentId == position.InstrumentId).SecurityId;
                targetPositions.Add(new(targetStageId, modelRunId, Guid.Parse(position.InstrumentId), securityId,
                    position.TargetNotionalUsd, position.TargetBaseQuantity, position.TargetVenueQuantity,
                    TargetQuantityMode.PortfolioBaseCurrencyNotional.ToString(), "VENUE_QUANTITY_INCREMENT",
                    run.TargetPositions.ComputationStatus.ToString(), PmsShadowStateContract.EvidenceClassification));
            }

            var driftStageId = Id($"position-only-drift-stage:{run.ModelRun.ModelRunPreviewId}");
            driftStages.Add(new(driftStageId, modelRunId, positionSnapshotId, bundle.Positions.AsOfUtc,
                run.DriftSnapshot.ComputationStatus.ToString(), PmsShadowStateContract.EvidenceClassification));
            foreach (var drift in (run.DriftSnapshot.PositionOnlyDrifts ?? []).OrderBy(x => x.InstrumentId, StringComparer.Ordinal))
            {
                var securityId = run.TargetWeights.Single(x => x.InstrumentId == drift.InstrumentId).SecurityId;
                drifts.Add(new(driftStageId, modelRunId, Guid.Parse(drift.InstrumentId), securityId,
                    drift.CurrentBaseQuantity, drift.TargetBaseQuantity, drift.PositionOnlyDeltaBaseQuantity,
                    bundle.Positions.AsOfUtc, "COMPUTED_OPERATIONAL_PREVIEW_NO_ORDER"));
            }

            brokerStages.Add(new(Id($"broker-adjusted-drift-stage:{run.ModelRun.ModelRunPreviewId}"), modelRunId, leavesId,
                run.DriftSnapshot.BrokerAdjustedDriftCalculated,
                run.DriftSnapshot.BrokerAdjustedDriftBlocker ?? string.Empty,
                leaves.EmptyStateInferred, run.DriftSnapshot.ComputationStatus.ToString(), PmsShadowStateContract.EvidenceClassification));
            cycleResults.Add(new(Id($"cycle-result:{run.ModelRun.ModelRunPreviewId}"), ingestionId, modelRunId,
                run.ManualPaperCycle.Status.ToString(), run.R009.Status, run.R009.ExecutionAllowed, run.R009.NotAnOrder,
                run.R009.NoBrokerRoute, run.R009.NoFixMessage, run.R009.OrderEntryEnabled, run.R009.BrokerSendStatus,
                run.R009.ExecutionIntentCount, input.CompletedAtUtc, PmsShadowStateContract.EvidenceClassification));
        }

        var ingestionDraft = new PmsShadowIngestionRow(
            ingestionId, input.SourceGate, input.SourceSessionId, input.SourceEvidenceSha256, "COMPLETED",
            input.StartedAtUtc, input.CompletedAtUtc, PmsShadowStateContract.ContractVersion,
            PmsShadowStateContract.TestEnvironment, PmsShadowStateContract.EvidenceClassification, string.Empty);
        var rowsetSha = Arch5bHashing.HashCanonical(new
        {
            ingestionDraft.SourceGate,
            ingestionDraft.SourceSessionId,
            ingestionDraft.SourceEvidenceSha256,
            Artifacts = sourceArtifacts,
            QubesInputs = qubesInputs,
            Account = accountRow,
            Positions = positionRow,
            PositionLines = positionLines,
            Market = marketRow,
            MarketObservations = marketObservations,
            Mappings = securityMappings,
            WorkingLeaves = leavesRow,
            ModelRuns = modelRuns,
            TargetWeights = targetWeights,
            TargetStages = targetStages,
            TargetPositions = targetPositions,
            DriftStages = driftStages,
            Drifts = drifts,
            BrokerStages = brokerStages,
            CycleResults = cycleResults
        });
        var plan = new PmsShadowPersistencePlan(
            ingestionDraft with { RowsetSha256 = rowsetSha }, sourceArtifacts, qubesInputs, accountRow, positionRow,
            positionLines, marketRow, marketObservations, securityMappings, leavesRow, modelRuns, targetWeights,
            targetStages, targetPositions, driftStages, drifts, brokerStages, cycleResults, rowsetSha);
        var validation = Validate(plan);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(string.Join(";", validation.Issues));
        }
        return plan;
    }

    public static PmsShadowPlanValidation Validate(PmsShadowPersistencePlan plan)
    {
        var issues = new List<string>();
        static bool Utc(DateTimeOffset value) => value != default && value.Offset == TimeSpan.Zero;
        static bool Fits(decimal value, decimal exclusiveLimit, int scale) =>
            value > -exclusiveLimit && value < exclusiveLimit && ((decimal.GetBits(value)[3] >> 16) & 0x7f) <= scale;
        const decimal RatioLimit = 10_000_000_000_000_000m;
        const decimal PriceLimit = 10_000_000_000m;
        const decimal QuantityLimit = 100_000_000_000_000_000_000m;
        static void Require(bool condition, string issue, ICollection<string> target) { if (!condition) target.Add(issue); }
        var modelIds = plan.ModelRuns.Select(x => x.ModelRunId).ToHashSet();
        var targetStageById = plan.TargetPositionStages.ToDictionary(x => x.StageId);
        var driftStageIds = plan.PositionOnlyDriftStages.Select(x => x.StageId).ToHashSet();
        var snapshotIds = plan.QubesInputSnapshots.Select(x => x.SnapshotId).ToHashSet();
        var weightKeys = plan.TargetWeights.Select(x => (x.ModelRunId, x.InstrumentId)).ToHashSet();
        var positionKeys = plan.TargetPositions.Select(x => (x.ModelRunId, x.InstrumentId)).ToHashSet();
        var marketKeys = plan.MarketDataObservations.Select(x => x.InstrumentId).ToHashSet();

        Require(plan.Ingestion.ContractVersion == PmsShadowStateContract.ContractVersion, "UNKNOWN_CONTRACT_VERSION", issues);
        Require(Arch5bHashing.IsSha256(plan.Ingestion.SourceEvidenceSha256) && Arch5bHashing.IsSha256(plan.RowsetSha256), "SHA_INVALID", issues);
        Require(Utc(plan.Ingestion.StartedAtUtc) && plan.Ingestion.CompletedAtUtc is { } completed && Utc(completed), "TIMESTAMP_NOT_UTC", issues);
        Require(plan.Ingestion.Environment == PmsShadowStateContract.TestEnvironment && plan.AccountSnapshot.AccountId != Arch5bLineageContractVersions.RealAccountId, "REAL_ACCOUNT_REJECTED", issues);
        Require(plan.ModelRuns.Count == 4 && plan.QubesInputSnapshots.Count == 4, "FOUR_MODEL_RUNS_AND_INPUTS_REQUIRED", issues);
        Require(plan.TargetWeights.Count == 288, "TARGET_WEIGHT_COUNT_INVALID", issues);
        Require(plan.QubesInputSnapshots.All(x => plan.SourceArtifacts.Any(a => a.ArtifactId == x.InputArtifactId && a.Sha256 == x.InputSha256) && plan.SourceArtifacts.Any(a => a.ArtifactId == x.SourceSnapshotArtifactId && a.Sha256 == x.SourceSnapshotSha256) && plan.SourceArtifacts.Any(a => a.ArtifactId == x.OverlayArtifactId && a.Sha256 == x.OverlaySha256)), "QUBES_INPUT_ARTIFACT_LINEAGE_INCOMPLETE", issues);
        Require(plan.TargetPositions.All(x => targetStageById.TryGetValue(x.StageId, out var stage) && stage.ModelRunId == x.ModelRunId), "TARGET_POSITION_STAGE_MISSING", issues);
        Require(plan.TargetPositionStages.All(x => modelIds.Contains(x.ModelRunId) && x.AccountSnapshotId == plan.AccountSnapshot.AccountSnapshotId && x.MarketDataSnapshotId == plan.MarketDataSnapshot.MarketDataSnapshotId), "TARGET_POSITION_STAGE_LINEAGE_INCOMPLETE", issues);
        Require(plan.PositionOnlyDrifts.All(x => driftStageIds.Contains(x.StageId)), "DRIFT_STAGE_MISSING", issues);
        Require(plan.TargetPositionStages.Count == 4 && plan.TargetPositions.Count == 288, "TARGET_POSITION_COUNT_INVALID", issues);
        Require(plan.PositionOnlyDriftStages.Count == 4 && plan.PositionOnlyDrifts.Count == 288, "POSITION_ONLY_DRIFT_COUNT_INVALID", issues);
        Require(plan.BrokerAdjustedDriftStages.Count == 4, "BROKER_ADJUSTED_STAGE_COUNT_INVALID", issues);
        Require(plan.CycleResults.Count == 4, "CYCLE_RESULT_COUNT_INVALID", issues);
        Require(plan.TargetWeights.All(x => modelIds.Contains(x.ModelRunId)), "TARGET_WEIGHT_ORPHAN", issues);
        Require(plan.ModelRuns.All(x => snapshotIds.Contains(x.QubesInputSnapshotId) && plan.SourceArtifacts.Any(a => a.ArtifactId == x.OutputArtifactId && a.Sha256 == x.OutputSha256)), "MODEL_RUN_LINEAGE_INCOMPLETE", issues);
        Require(plan.ModelRuns.All(x => GitCommitIdentityContract.IsValid(x.CoreMasterCommitId, x.CoreMasterObjectFormat)), "GIT_COMMIT_IDENTITY_INVALID", issues);
        Require(plan.ModelRuns.All(x => Arch5bHashing.IsSha256(x.PackageSha256) && Arch5bHashing.IsSha256(x.EngineSha256) && Arch5bHashing.IsSha256(x.OutputSha256)), "MODEL_RUN_ARTIFACT_SHA256_INVALID", issues);
        Require(plan.TargetPositions.All(x => weightKeys.Contains((x.ModelRunId, x.InstrumentId)) && marketKeys.Contains(x.InstrumentId)), "TARGET_POSITION_LINEAGE_INCOMPLETE", issues);
        Require(plan.PositionOnlyDrifts.All(x => positionKeys.Contains((x.ModelRunId, x.InstrumentId))), "DRIFT_TARGET_POSITION_MISSING", issues);
        Require(plan.TargetWeights.All(x => Fits(x.Weight, RatioLimit, 12)), "NUMERIC_ENVELOPE_INVALID", issues);
        Require(plan.MarketDataObservations.All(x => Fits(x.Bid, PriceLimit, 28) && Fits(x.Ask, PriceLimit, 28)), "NUMERIC_ENVELOPE_INVALID", issues);
        Require(plan.TargetPositions.All(x => Fits(x.TargetNotionalUsd, RatioLimit, 12) && Fits(x.TargetBaseQuantity, QuantityLimit, 8) && Fits(x.TargetVenueQuantity, QuantityLimit, 8)), "NUMERIC_ENVELOPE_INVALID", issues);
        Require(plan.PositionOnlyDrifts.All(x => Fits(x.CurrentBaseQuantity, QuantityLimit, 8) && Fits(x.TargetBaseQuantity, QuantityLimit, 8) && Fits(x.PositionOnlyDeltaBaseQuantity, QuantityLimit, 8)), "NUMERIC_ENVELOPE_INVALID", issues);
        Require(Fits(plan.AccountSnapshot.NavOrEquity, QuantityLimit, 8), "NUMERIC_ENVELOPE_INVALID", issues);
        Require(plan.PositionOnlyDriftStages.All(x => x.PositionSnapshotId == plan.PositionSnapshot.PositionSnapshotId), "DRIFT_POSITION_SNAPSHOT_MISSING", issues);
        Require(plan.TargetWeights.GroupBy(x => (x.ModelRunId, x.InstrumentId)).All(x => x.Count() == 1), "DUPLICATE_MODEL_RUN_SECURITY_ID", issues);
        Require(plan.ModelRuns.GroupBy(x => x.ModelRunId).All(x => x.Count() == 1), "DUPLICATE_MODEL_RUN_ID", issues);
        Require(plan.SourceArtifacts.All(x => Arch5bHashing.IsSha256(x.Sha256) && x.SizeBytes >= 0 && !Path.IsPathRooted(x.LogicalUri) && Utc(x.ProducedAtUtc)), "SOURCE_ARTIFACT_INVALID", issues);
        Require(plan.SourceArtifacts.GroupBy(x => x.Sha256, StringComparer.Ordinal).All(x => x.Select(v => (v.ArtifactType, v.SizeBytes, v.LogicalUri)).Distinct().Count() == 1), "SOURCE_ARTIFACT_SHA_CONTRADICTION", issues);
        Require(plan.WorkingLeavesObservation.Status == PmsShadowStateContract.WorkingLeavesUnavailable && !plan.WorkingLeavesObservation.EmptyStateObserved && !plan.WorkingLeavesObservation.EmptyStateInferred && !plan.WorkingLeavesObservation.BrokerAuthority, "WORKING_LEAVES_FALSE_EMPTY_OR_AUTHORITY", issues);
        Require(plan.BrokerAdjustedDriftStages.All(x => !x.Calculated && x.Blocker == PmsShadowStateContract.BrokerAdjustedBlocker && !x.EmptyStateInferred), "BROKER_ADJUSTED_DRIFT_INVALID", issues);
        Require(plan.ModelRuns.All(x => !x.AccountingEligible && !x.ExecutionAllowed && x.NotAnOrder), "MODEL_RUN_NO_ORDER_INVALID", issues);
        Require(plan.CycleResults.All(x => x.ManualPaperCycleStatus == PmsShadowStateContract.CompletedNoExternal && x.R009Status == PmsShadowStateContract.CompletedNoExternal && !x.ExecutionAllowed && x.NotAnOrder && x.NoBrokerRoute && x.NoFixMessage && !x.OrderEntryEnabled && x.BrokerSendStatus == PmsShadowStateContract.DisabledBrokerSend && x.TradeIntentCount == 0), "NO_ORDER_REGRESSION", issues);
        Require(plan.MarketDataObservations.All(x => x.Bid > 0m && x.Ask >= x.Bid && Utc(x.EventTimeUtc) && Utc(x.ReceivedAtUtc)), "MARKET_DATA_INVALID", issues);
        return new(issues.Count == 0, issues.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static Guid Id(string value) => Arch5bHashing.GuidFromSha256($"arch6c:{value}");
}

public enum PmsShadowApplyResult { Applied, AlreadyAppliedIdentical }

public sealed class InMemoryPmsShadowAtomicIngestionRegistry
{
    private readonly object sync = new();
    private readonly Dictionary<string, PmsShadowPersistencePlan> bySession = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, (string OutputSha256, string CoreCommitId, string CoreObjectFormat)> modelIdentities = [];
    private readonly Dictionary<Guid, string> snapshots = [];

    public PmsShadowApplyResult Apply(PmsShadowPersistencePlan plan, bool simulateInterruptionBeforeCommit = false)
    {
        var validation = Arch6cPmsShadowPersistencePlanner.Validate(plan);
        if (!validation.IsValid) throw new InvalidDataException(string.Join(";", validation.Issues));
        lock (sync)
        {
            if (bySession.TryGetValue(plan.Ingestion.SourceSessionId, out var existing))
            {
                if (existing.Ingestion.SourceEvidenceSha256 != plan.Ingestion.SourceEvidenceSha256)
                    throw new InvalidDataException("SOURCE_SESSION_EVIDENCE_SHA_CONFLICT");
                if (existing.RowsetSha256 != plan.RowsetSha256)
                    throw new InvalidDataException("SOURCE_SESSION_ROWSET_CONFLICT");
                foreach (var model in plan.ModelRuns)
                {
                    var stored = existing.ModelRuns.SingleOrDefault(x => x.ModelRunId == model.ModelRunId)
                        ?? throw new InvalidDataException("MODEL_RUN_IDENTITY_CONFLICT");
                    if (stored.OutputSha256 != model.OutputSha256)
                        throw new InvalidDataException("MODEL_RUN_OUTPUT_SHA_CONFLICT");
                    if (stored.CoreMasterCommitId != model.CoreMasterCommitId)
                        throw new InvalidDataException("MODEL_RUN_CORE_COMMIT_ID_CONFLICT");
                    if (stored.CoreMasterObjectFormat != model.CoreMasterObjectFormat)
                        throw new InvalidDataException("MODEL_RUN_CORE_OBJECT_FORMAT_CONFLICT");
                }
                return PmsShadowApplyResult.AlreadyAppliedIdentical;
            }
            var modelAlreadyOwned = false;
            foreach (var model in plan.ModelRuns)
                if (modelIdentities.TryGetValue(model.ModelRunId, out var stored))
                {
                    if (stored.OutputSha256 != model.OutputSha256)
                        throw new InvalidDataException("MODEL_RUN_OUTPUT_SHA_CONFLICT");
                    if (stored.CoreCommitId != model.CoreMasterCommitId)
                        throw new InvalidDataException("MODEL_RUN_CORE_COMMIT_ID_CONFLICT");
                    if (stored.CoreObjectFormat != model.CoreMasterObjectFormat)
                        throw new InvalidDataException("MODEL_RUN_CORE_OBJECT_FORMAT_CONFLICT");
                    modelAlreadyOwned = true;
                }
            foreach (var snapshot in plan.QubesInputSnapshots)
                if (snapshots.TryGetValue(snapshot.SnapshotId, out var sha) && sha != snapshot.InputSha256)
                    throw new InvalidDataException("QUBES_INPUT_SNAPSHOT_CONTENT_CONFLICT");
            if (modelAlreadyOwned) throw new InvalidDataException("MODEL_RUN_ID_ALREADY_OWNED_BY_ANOTHER_SESSION");
            if (simulateInterruptionBeforeCommit) throw new InvalidOperationException("SIMULATED_INTERRUPTION_BEFORE_ATOMIC_COMMIT");
            bySession.Add(plan.Ingestion.SourceSessionId, plan);
            foreach (var model in plan.ModelRuns)
                modelIdentities[model.ModelRunId] = (model.OutputSha256, model.CoreMasterCommitId, model.CoreMasterObjectFormat);
            foreach (var snapshot in plan.QubesInputSnapshots) snapshots[snapshot.SnapshotId] = snapshot.InputSha256;
            return PmsShadowApplyResult.Applied;
        }
    }
}
