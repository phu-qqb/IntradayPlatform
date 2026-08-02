using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class PmsShadowIntradayEconomicContract
{
    public const string Version = "pms_shadow_intraday_economic_projection_v1";
    public const string TargetPositionVersion = "canonical_target_position_calculator_v1";
    public const string PositionSnapshotSelectionVersion =
        "arch7b_position_snapshot_for_slot_selection_v1";
    public const string TestEnvironment = "TEST";
    public const int MaximumUsdLegSkewSeconds = 15;
    public const int MaximumPositionAgeSeconds = 300;
    public const int PostgreSqlPriceScale = 12;

    public static (decimal Bid, decimal Ask, decimal DecisionPrice) ToPostgreSqlMarketPrices(
        decimal bid, decimal ask)
    {
        var storedBid = decimal.Round(bid, PostgreSqlPriceScale, MidpointRounding.AwayFromZero);
        var storedAsk = decimal.Round(ask, PostgreSqlPriceScale, MidpointRounding.AwayFromZero);
        var storedDecisionPrice = decimal.Round((storedBid + storedAsk) / 2m,
            PostgreSqlPriceScale, MidpointRounding.AwayFromZero);
        return (storedBid, storedAsk, storedDecisionPrice);
    }
}

public sealed record PmsShadowRealSlotBbo(string Symbol, string LmaxInstrumentId,
    decimal Bid, decimal Ask, DateTimeOffset SourceTimestampUtc, DateTimeOffset RecordedUtc);

public sealed record PmsShadowRealSlotCapture(string SlotId, DateTimeOffset SlotStartUtc,
    DateTimeOffset SlotEndUtc, string RecorderRunId, string ArtifactPath, string ArtifactSha256,
    IReadOnlyList<PmsShadowRealSlotBbo> Bbo, bool LmaxPrimary, int PolygonCallCount,
    bool Complete, bool NoOrder);

public static class PmsShadowRealSlotCaptureReader
{
    public static PmsShadowRealSlotCapture Read(string manifestPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = document.RootElement;
        var artifactSha = Required(root, "artifact_sha256");
        var artifactPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(manifestPath)!,
            Path.GetFileName(Required(root, "artifact_file"))));
        if (!File.Exists(artifactPath)) throw new InvalidDataException("RAW_SLOT_ARTIFACT_MISSING");
        var actualSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(artifactPath)));
        if (actualSha != artifactSha) throw new InvalidDataException("RAW_SLOT_ARTIFACT_SHA_MISMATCH");
        var slotStartUtc = root.GetProperty("slot_start_utc").GetDateTimeOffset();
        var slotEndUtc = root.GetProperty("slot_end_utc").GetDateTimeOffset();
        if (root.GetProperty("bbo_symbol_count").GetInt32() != 49 ||
            root.GetProperty("missing_required_bbo_symbols").GetArrayLength() != 0)
            throw new InvalidDataException("RAW_SLOT_BBO_COVERAGE_INCOMPLETE");
        if (root.GetProperty("contractually_required_gap_ids").GetArrayLength() != 0)
            throw new InvalidDataException("RAW_SLOT_CONTRACTUAL_GAP_PRESENT");

        var bbo = root.GetProperty("last_bbo_by_symbol").EnumerateObject()
            .OrderBy(value => value.Name, StringComparer.Ordinal)
            .Select(value => new PmsShadowRealSlotBbo(value.Name,
                Required(value.Value, "instrument_id"),
                value.Value.GetProperty("bid_price").GetDecimal(),
                value.Value.GetProperty("ask_price").GetDecimal(),
                value.Value.GetProperty("source_timestamp_utc").GetDateTimeOffset(),
                value.Value.GetProperty("recorded_utc").GetDateTimeOffset())).ToArray();
        if (bbo.Length != 49 || bbo.Select(value => value.Symbol).Distinct(StringComparer.Ordinal).Count() != 49)
            throw new InvalidDataException("RAW_SLOT_BBO_IDENTITY_INCOMPLETE");
        if (bbo.Any(value =>
                value.SourceTimestampUtc < slotStartUtc ||
                value.SourceTimestampUtc > slotEndUtc))
            throw new InvalidDataException("RAW_SLOT_BBO_SOURCE_TIMESTAMP_OUTSIDE_WINDOW");
        if (bbo.Any(value => value.Bid <= 0m || value.Ask < value.Bid))
            throw new InvalidDataException("RAW_SLOT_BBO_PRICE_INVALID");
        if (!root.TryGetProperty("slot_bbo_selection_contract_version", out _))
            throw new InvalidDataException(
                PmsShadowCaptureClockAuthorityContract.Blocker);
        if (root.TryGetProperty("slot_bbo_selection_contract_version", out var selectionVersion))
        {
            if (selectionVersion.GetString() != PmsShadowRealSlotBboSelectionContract.Version)
                throw new InvalidDataException("RAW_SLOT_BBO_SELECTION_VERSION_MISMATCH");
            var clockHost = Required(root, "clock_host_identity");
            var repositoryCommit = Required(root, "repository_commit");
            if (!string.Equals(clockHost, Environment.MachineName,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    PmsShadowCaptureClockAuthorityContract.Blocker);
            var clockSnapshot = ReadClockSnapshot(root, manifestPath,
                "clock_authority_snapshot_file",
                "clock_authority_snapshot_sha256");
            var postCloseSnapshot = ReadClockSnapshot(root, manifestPath,
                "clock_post_close_snapshot_file",
                "clock_post_close_snapshot_sha256");
            var clockAuthority = new PmsShadowCaptureClockAuthorityEvidence(
                clockSnapshot, postCloseSnapshot);
            PmsShadowCaptureClockAuthorityValidator.RequireQualifiedForSlot(
                clockAuthority,
                new PmsShadowIntradaySlotWindow(
                    Required(root, "slot_id"), slotStartUtc, slotEndUtc,
                    DateOnly.Parse(Required(root, "operational_date"),
                        System.Globalization.CultureInfo.InvariantCulture)),
                clockHost, repositoryCommit);
            if (root.GetProperty("clock_authority_contract_version").GetString() !=
                    PmsShadowCaptureClockAuthorityContract.Version ||
                root.GetProperty("clock_preflight_status").GetString() !=
                    PmsShadowCaptureClockAuthorityContract.QualifiedStatus ||
                root.GetProperty("maximum_late_receipt_after_close_ms").GetInt32() !=
                    PmsShadowCaptureClockAuthorityContract
                        .MaximumLateReceiptAfterSlotCloseMilliseconds ||
                root.GetProperty("maximum_cross_clock_lead_ms").GetDecimal() !=
                    clockAuthority.MaximumCrossClockLeadMilliseconds ||
                root.GetProperty("clock_reference_source").GetString() !=
                    clockAuthority.PreCapture.ReferenceClockSource ||
                root.GetProperty("clock_offset_ms").GetDecimal() !=
                    clockAuthority.PreCapture.MeasuredOffsetMilliseconds ||
                root.GetProperty("clock_uncertainty_ms").GetDecimal() !=
                    clockAuthority.MaximumClockUncertaintyMilliseconds ||
                root.GetProperty("clock_snapshot_captured_at_utc").GetDateTimeOffset() !=
                    clockAuthority.PreCapture.CapturedAtUtc ||
                root.GetProperty("cross_clock_comparison").GetString() !=
                    PmsShadowCaptureClockAuthorityContract.CrossClockComparison ||
                clockHost != clockAuthority.PreCapture.HostIdentity ||
                repositoryCommit != clockAuthority.PreCapture.RepositoryCommit)
                throw new InvalidDataException("RAW_SLOT_CLOCK_AUTHORITY_MANIFEST_MISMATCH");
            ValidateSelectedClockFields(root.GetProperty("last_bbo_by_symbol"), clockAuthority);
            if (bbo.Any(value =>
                    !PmsShadowCaptureClockAuthorityValidator.IsCrossClockCausalityValid(
                        value.SourceTimestampUtc, value.RecordedUtc, clockAuthority) ||
                    !PmsShadowCaptureClockAuthorityValidator.IsWithinLateReceiptBound(
                        value.RecordedUtc, slotEndUtc, clockAuthority)))
                throw new InvalidDataException("RAW_SLOT_BBO_CLOCK_ENVELOPE_VIOLATION");
            var selectionSha = Required(root, "selection_sha256");
            PmsShadowIntradayCadenceContract.RequireSha(
                selectionSha, "selection_sha256");
            if (PmsShadowRealSlotBboSelector.SelectionSha256(
                    root.GetProperty("last_bbo_by_symbol")) != selectionSha)
                throw new InvalidDataException("RAW_SLOT_BBO_SELECTION_SHA_MISMATCH");
            if (root.GetProperty("in_slot_bbo_event_count").GetInt32() < 49)
                throw new InvalidDataException("RAW_SLOT_IN_WINDOW_BBO_COVERAGE_INCOMPLETE");
            var excludedPostClose = root.GetProperty("excluded_post_close_by_symbol")
                .EnumerateObject().Sum(value => value.Value.GetInt32());
            if (excludedPostClose != root.GetProperty("post_close_bbo_event_count").GetInt32())
                throw new InvalidDataException("RAW_SLOT_POST_CLOSE_DIAGNOSTIC_MISMATCH");
            if (root.GetProperty("minimum_selected_source_timestamp_utc").GetDateTimeOffset() !=
                    bbo.Min(value => value.SourceTimestampUtc) ||
                root.GetProperty("maximum_selected_source_timestamp_utc").GetDateTimeOffset() !=
                    bbo.Max(value => value.SourceTimestampUtc))
                throw new InvalidDataException("RAW_SLOT_BBO_SELECTED_TIMESTAMP_RANGE_MISMATCH");
        }
        var capture = new PmsShadowRealSlotCapture(Required(root, "slot_id"),
            slotStartUtc, slotEndUtc, Required(root, "recorder_run_id"),
            artifactPath, artifactSha, bbo, root.GetProperty("lmax_primary").GetBoolean(),
            root.GetProperty("polygon_call_count").GetInt32(), root.GetProperty("complete").GetBoolean(),
            root.GetProperty("no_order").GetBoolean());
        var expected = PmsShadowIntradayCadenceContract.WindowEnding(capture.SlotEndUtc);
        if (capture.SlotId != expected.SlotId || capture.SlotStartUtc != expected.SlotStartUtc)
            throw new InvalidDataException("RAW_SLOT_WINDOW_IDENTITY_MISMATCH");
        if (!capture.LmaxPrimary || capture.PolygonCallCount != 0 || !capture.Complete || !capture.NoOrder)
            throw new InvalidDataException("RAW_SLOT_SAFETY_CONTRACT_VIOLATION");
        return capture;
    }

    private static void ValidateSelectedClockFields(
        JsonElement selected,
        PmsShadowCaptureClockAuthorityEvidence clockAuthority)
    {
        foreach (var property in selected.EnumerateObject())
        {
            var value = property.Value;
            var recordedUtc = value.GetProperty("recorded_utc").GetDateTimeOffset();
            if (value.GetProperty("corrected_recorded_utc_for_validation")
                    .GetDateTimeOffset() !=
                    PmsShadowCaptureClockAuthorityValidator
                        .CorrectedRecordedUtcForValidation(recordedUtc, clockAuthority) ||
                value.GetProperty("applied_clock_offset_ms").GetDecimal() !=
                    clockAuthority.PreCapture.MeasuredOffsetMilliseconds ||
                value.GetProperty("clock_uncertainty_ms").GetDecimal() !=
                    clockAuthority.MaximumClockUncertaintyMilliseconds ||
                value.GetProperty("clock_snapshot_sha256").GetString() !=
                    clockAuthority.PreCapture.SnapshotSha256)
                throw new InvalidDataException(
                    "RAW_SLOT_BBO_CLOCK_AUTHORITY_FIELDS_MISMATCH");
        }
    }

    private static PmsShadowCaptureClockAuthoritySnapshot ReadClockSnapshot(
        JsonElement root,
        string manifestPath,
        string fileProperty,
        string shaProperty)
    {
        var fileName = Required(root, fileProperty);
        if (Path.GetFileName(fileName) != fileName)
            throw new InvalidDataException("RAW_SLOT_CLOCK_AUTHORITY_PATH_INVALID");
        var path = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!,
            fileName);
        if (!File.Exists(path))
            throw new InvalidDataException(
                PmsShadowCaptureClockAuthorityContract.Blocker);
        var snapshot = PmsShadowCaptureClockAuthorityStore.Read(path);
        if (snapshot.SnapshotSha256 != Required(root, shaProperty))
            throw new InvalidDataException("RAW_SLOT_CLOCK_AUTHORITY_SHA_MISMATCH");
        return snapshot;
    }

    private static string Required(JsonElement value, string name) =>
        value.GetProperty(name).GetString() ?? throw new InvalidDataException($"RAW_SLOT_FIELD_MISSING:{name}");
}

public sealed record PmsShadowEconomicModel(Guid ModelRunId, Guid QubesInputSnapshotId,
    string StrategyId, DateTimeOffset TargetCloseUtc, DateTimeOffset AsOfUtc, string OutputSha256,
    string CoreCommitId, IReadOnlyList<PmsShadowEconomicWeight> Weights);
public sealed record PmsShadowEconomicWeight(Guid InstrumentId, string SecurityId, decimal Weight);
public sealed record PmsShadowEconomicMapping(Guid InstrumentId, Guid VenueId, Guid VenueInstrumentId,
    string SecurityId, string Symbol, string LmaxInstrumentId, decimal QuantityMultiplier,
    decimal QuantityIncrement, decimal PriceIncrement);
public sealed record PmsShadowEconomicSource(Guid IngestionId, string SourceSessionId,
    Guid AccountSnapshotId, decimal NavUsd, Guid PositionSnapshotId, DateTimeOffset PositionAsOfUtc,
    string PositionAuthority, IReadOnlyDictionary<Guid, decimal> CurrentPositions,
    IReadOnlyList<PmsShadowEconomicMapping> Mappings, IReadOnlyList<PmsShadowEconomicModel> Models);

public static class PmsShadowPositionSnapshotForSlotSelection
{
    public const string Missing = "ARCH7B_POSITION_SNAPSHOT_MISSING_FOR_SLOT";
    public const string AfterSlot = "ARCH7B_POSITION_SNAPSHOT_AFTER_SLOT_START";
    public const string Stale = "ARCH7B_POSITION_SNAPSHOT_STALE_FOR_SLOT";
    public const string Ambiguous = "ARCH7B_POSITION_SNAPSHOT_SELECTION_AMBIGUOUS";

    public static PmsShadowPositionSnapshotRow Select(
        IEnumerable<PmsShadowPositionSnapshotRow> candidates,
        DateTimeOffset slotStartUtc)
    {
        RequireUtc(slotStartUtc);
        var all = candidates.OrderBy(value => value.AsOfUtc)
            .ThenBy(value => value.PositionSnapshotId).ToArray();
        if (all.Length == 0)
            throw new InvalidDataException(Missing);

        var eligible = all.Where(value => value.AsOfUtc <= slotStartUtc).ToArray();
        if (eligible.Length == 0)
            throw new InvalidDataException(AfterSlot);

        var maximumAsOfUtc = eligible.Max(value => value.AsOfUtc);
        var latest = eligible.Where(value => value.AsOfUtc == maximumAsOfUtc).ToArray();
        if (latest.Length != 1)
            throw new InvalidDataException(Ambiguous);

        var age = slotStartUtc - latest[0].AsOfUtc;
        if (age > TimeSpan.FromSeconds(
                PmsShadowIntradayEconomicContract.MaximumPositionAgeSeconds))
            throw new InvalidDataException(Stale);
        return latest[0];
    }

    public static void ValidateSelected(
        PmsShadowPositionSnapshotRow selected,
        PmsShadowIngestionRow ingestion,
        PmsShadowAccountSnapshotRow account,
        IReadOnlyList<PmsShadowPositionSnapshotLineRow> lines,
        IReadOnlyList<PmsShadowSecurityMappingRow> mappings)
    {
        if (selected.IngestionId != ingestion.IngestionId ||
            selected.AccountSnapshotId != account.AccountSnapshotId ||
            !selected.BrokerAuthority ||
            !selected.EmptyStateWasExplicitlyObserved ||
            selected.EmptyStateWasInferred ||
            selected.Classification !=
            Arch7bBracketedGlobalFlatContract.PositionAuthorityCode ||
            !Arch5bHashing.IsSha256(selected.SnapshotSha256))
            throw new InvalidDataException(
                "ARCH7B_POSITION_SNAPSHOT_AUTHORITY_INVALID");

        var mappingByInstrument = mappings.ToDictionary(value => value.InstrumentId);
        if (lines.Count != Arch7bPositionImportContract.RequiredLineCount ||
            lines.Select(value => value.InstrumentId).Distinct().Count() !=
            Arch7bPositionImportContract.RequiredLineCount ||
            lines.Any(value => !mappingByInstrument.TryGetValue(
                value.InstrumentId, out var mapping) ||
                mapping.SecurityId != value.SecurityId ||
                mapping.Symbol != value.Symbol))
            throw new InvalidDataException(
                "SOURCE_POSITION_SNAPSHOT_COVERAGE_INCOMPLETE");
    }

    private static void RequireUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new InvalidDataException(
                "ARCH7B_POSITION_SNAPSHOT_SLOT_NOT_UTC");
    }
}

public sealed record PmsShadowSlotMarketObservation(Guid InstrumentId, string SecurityId,
    string Symbol, string LmaxInstrumentId, decimal Bid, decimal Ask, decimal DecisionPrice,
    DateTimeOffset EventTimeUtc, string ProjectionMethod, IReadOnlyList<string> ProjectionLegSecurityIds);
public sealed record PmsShadowSlotTargetPosition(Guid TargetPositionId, Guid StageId,
    Guid ModelRunId, string StrategyId, Guid InstrumentId, string SecurityId,
    decimal TargetNotionalUsd, decimal TargetBaseQuantity, decimal TargetVenueQuantity,
    decimal DecisionPrice, DateTimeOffset TargetCloseUtc, DateTimeOffset CalculatedAtUtc,
    string InputSha256, string OutputSha256,
    string CoreCommitId);
public sealed record PmsShadowSlotPositionOnlyDrift(Guid DriftId, Guid StageId, Guid ModelRunId,
    string StrategyId, Guid InstrumentId, string SecurityId, decimal CurrentBaseQuantity,
    decimal TargetBaseQuantity, decimal Delta, DateTimeOffset AsOfUtc,
    string InputSha256, string OutputSha256);

public sealed record PmsShadowSelectedModelRun(Guid ModelRunId, Guid QubesInputSnapshotId,
    string StrategyId, DateTimeOffset AsOfUtc, DateTimeOffset TargetCloseUtc, string OutputSha256,
    string CoreCommitId, string Classification);

public sealed record PmsShadowIntradayEconomicProjection(
    Guid ProjectionRevisionId, int RevisionNumber, string SlotId, DateTimeOffset SlotStartUtc,
    DateTimeOffset SlotEndUtc, string RawCaptureSha256, Guid MarketDataSnapshotId,
    string MarketDataSnapshotSha256, Guid SourceIngestionId, string SourceSessionId,
    Guid AccountSnapshotId, Guid PositionSnapshotId, DateTimeOffset PositionSnapshotAsOfUtc,
    string PositionAuthority, IReadOnlyList<Guid> ReusedModelRunIds,
    IReadOnlyList<Guid> ModelInputSnapshotIds, IReadOnlyList<PmsShadowSelectedModelRun> SelectedModelRuns,
    IReadOnlyList<PmsShadowSlotMarketObservation> MarketData,
    IReadOnlyList<PmsShadowSlotTargetPosition> TargetPositions,
    IReadOnlyList<PmsShadowSlotPositionOnlyDrift> PositionOnlyDrifts,
    string InputSha256, string TargetPositionsSha256, string DriftsSha256, string ManifestSha256,
    string? SupersedesSlotManifestSha256, string Status, string ExternalCompletionStatus,
    bool Qualifying, bool NoOrder,
    DateTimeOffset CompletedAtUtc);

public sealed class PmsShadowIntradayEconomicProjectionBuilder
{
    public PmsShadowIntradayEconomicProjection Build(PmsShadowRealSlotCapture capture,
        PmsShadowEconomicSource source, string? supersedesSlotManifestSha256)
    {
        if (supersedesSlotManifestSha256 is not null)
            PmsShadowIntradayCadenceContract.RequireSha(supersedesSlotManifestSha256,
                nameof(supersedesSlotManifestSha256));
        if (source.Models.Count != 4 || source.Models.Sum(value => value.Weights.Count) != 288)
            throw new InvalidDataException("SOURCE_MODEL_WEIGHT_SET_INCOMPLETE");
        var requiredInstrumentIds = source.Models.SelectMany(value => value.Weights)
            .Select(value => value.InstrumentId).Distinct().Order().ToArray();
        if (requiredInstrumentIds.Any(value => !source.CurrentPositions.ContainsKey(value)))
            throw new InvalidDataException("SOURCE_POSITION_SNAPSHOT_COVERAGE_INCOMPLETE");
        var mappings = source.Mappings.ToDictionary(value => value.InstrumentId);
        if (source.Models.SelectMany(value => value.Weights).Any(value => !mappings.ContainsKey(value.InstrumentId)))
            throw new InvalidDataException("SOURCE_SECURITY_MAPPING_INCOMPLETE");

        var quotes = capture.Bbo.Select(value => new Arch6aLmaxFxQuote(value.LmaxInstrumentId,
            value.Symbol, value.Symbol[..3], value.Symbol[3..], value.Bid, value.Ask,
            value.SourceTimestampUtc, value.RecordedUtc, capture.ArtifactSha256)).ToArray();
        var projector = new Arch6aLmaxUsdCrossRateProjector();
        var observations = mappings.Values.OrderBy(value => value.SecurityId, StringComparer.Ordinal)
            .Select(mapping =>
            {
                var pair = Pair(mapping.Symbol);
                var projected = projector.Project(pair.Base, pair.Quote, quotes,
                    TimeSpan.FromSeconds(PmsShadowIntradayEconomicContract.MaximumUsdLegSkewSeconds),
                    TimeSpan.FromSeconds(2));
                return new PmsShadowSlotMarketObservation(mapping.InstrumentId, mapping.SecurityId,
                    mapping.Symbol, mapping.LmaxInstrumentId, projected.Bid, projected.Ask, projected.Mid,
                    projected.AsOfUtc, projected.ProjectionMethod,
                    projected.Provenance.Select(value => value.SecurityId).ToArray());
            }).ToArray();
        var marketSha = Arch5bHashing.HashCanonical(observations);
        var marketId = Arch5bHashing.GuidFromSha256($"arch6f:slot-market:{capture.SlotId}:{marketSha}");
        var revisionNumber = supersedesSlotManifestSha256 is null ? 1 : 2;
        var revisionIdentity =
            $"{PmsShadowIntradayEconomicContract.TestEnvironment}:{capture.SlotId}:{capture.ArtifactSha256}:{PmsShadowIntradayEconomicContract.Version}";
        var revisionId = Arch5bHashing.GuidFromSha256(revisionNumber == 1
            ? revisionIdentity : $"{revisionIdentity}:revision:{revisionNumber}:supersedes:{supersedesSlotManifestSha256}");
        var observationByInstrument = observations.ToDictionary(value => value.InstrumentId);
        var calculator = new TargetPositionCalculator();
        var targets = new List<PmsShadowSlotTargetPosition>(288);
        var drifts = new List<PmsShadowSlotPositionOnlyDrift>(288);

        foreach (var model in source.Models.OrderBy(value => value.StrategyId, StringComparer.Ordinal))
        {
            var domainRun = new ModelRun(new ModelRunId(model.ModelRunId),
                new FundId(Arch5bHashing.GuidFromSha256($"arch6f:fund:{source.IngestionId:D}")),
                model.StrategyId, model.AsOfUtc, model.AsOfUtc, model.TargetCloseUtc, 15, source.NavUsd,
                ModelRunStatus.Processed, model.OutputSha256, "REUSED_FINALIZED_D1_MODEL", true,
                TargetQuantityMode.PortfolioBaseCurrencyNotional);
            var targetStage = Arch5bHashing.GuidFromSha256($"arch6f:target-stage:{revisionId:D}:{model.ModelRunId:D}");
            var driftStage = Arch5bHashing.GuidFromSha256($"arch6f:drift-stage:{revisionId:D}:{model.ModelRunId:D}");
            foreach (var weight in model.Weights.OrderBy(value => value.SecurityId, StringComparer.Ordinal))
            {
                var mapping = mappings[weight.InstrumentId];
                var observation = observationByInstrument[weight.InstrumentId];
                var market = new MarketDataSnapshot(new MarketDataSnapshotId(
                    Arch5bHashing.GuidFromSha256($"arch6f:quote:{marketId:D}:{weight.InstrumentId:D}")),
                    new InstrumentId(weight.InstrumentId), new VenueId(mapping.VenueId), observation.Bid,
                    observation.Ask, observation.DecisionPrice, "LMAX", observation.EventTimeUtc,
                    observation.EventTimeUtc);
                var venueMapping = new VenueInstrumentMapping(new VenueInstrumentId(mapping.VenueInstrumentId),
                    new VenueId(mapping.VenueId), new InstrumentId(mapping.InstrumentId), mapping.Symbol,
                    mapping.LmaxInstrumentId, mapping.QuantityMultiplier, mapping.QuantityIncrement,
                    mapping.QuantityIncrement, mapping.PriceIncrement);
                var inputSha = Arch5bHashing.HashCanonical(new
                {
                    capture.SlotId,
                    capture.ArtifactSha256,
                    MarketDataSnapshotId = marketId,
                    model.ModelRunId,
                    weight.InstrumentId,
                    weight.Weight,
                    observation.Bid,
                    observation.Ask,
                    source.NavUsd,
                    mapping.QuantityMultiplier,
                    mapping.QuantityIncrement,
                    Contract = PmsShadowIntradayEconomicContract.TargetPositionVersion
                });
                var calculated = calculator.Calculate(domainRun,
                    new TargetWeight(new ModelRunId(model.ModelRunId), new InstrumentId(weight.InstrumentId),
                        weight.Weight, weight.SecurityId), market, venueMapping);
                var targetId = Arch5bHashing.GuidFromSha256($"arch6f:target:{revisionId:D}:{marketId:D}:{model.ModelRunId:D}:{weight.InstrumentId:D}:{PmsShadowIntradayEconomicContract.TargetPositionVersion}");
                var targetOutputSha = Arch5bHashing.HashCanonical(new
                {
                    targetId,
                    calculated.TargetNotionalUsd,
                    calculated.TargetBaseQuantity,
                    calculated.TargetVenueQuantity,
                    observation.DecisionPrice
                });
                targets.Add(new(targetId, targetStage, model.ModelRunId, model.StrategyId,
                    weight.InstrumentId, weight.SecurityId, calculated.TargetNotionalUsd,
                    calculated.TargetBaseQuantity, calculated.TargetVenueQuantity, observation.DecisionPrice,
                    model.TargetCloseUtc, capture.SlotEndUtc, inputSha, targetOutputSha, model.CoreCommitId));
                var current = source.CurrentPositions[weight.InstrumentId];
                var delta = calculated.TargetBaseQuantity - current;
                var driftId = Arch5bHashing.GuidFromSha256($"arch6f:drift:{revisionId:D}:{model.ModelRunId:D}:{weight.InstrumentId:D}");
                var driftInput = Arch5bHashing.HashCanonical(new
                {
                    targetOutputSha,
                    source.PositionSnapshotId,
                    CurrentBaseQuantity = current
                });
                var driftOutput = Arch5bHashing.HashCanonical(new
                {
                    driftId,
                    calculated.TargetBaseQuantity,
                    CurrentBaseQuantity = current,
                    Delta = delta
                });
                drifts.Add(new(driftId, driftStage, model.ModelRunId, model.StrategyId,
                    weight.InstrumentId, weight.SecurityId, current, calculated.TargetBaseQuantity, delta,
                    capture.SlotEndUtc, driftInput, driftOutput));
            }
        }
        var targetSha = Arch5bHashing.HashCanonical(targets.OrderBy(value => value.TargetPositionId).ToArray());
        var driftSha = Arch5bHashing.HashCanonical(drifts.OrderBy(value => value.DriftId).ToArray());
        var input = Arch5bHashing.HashCanonical(new
        {
            capture.SlotId,
            capture.ArtifactSha256,
            marketId,
            MarketSha = marketSha,
            Models = source.Models.Select(value => new
            {
                value.ModelRunId,
                value.QubesInputSnapshotId,
                value.OutputSha256
            }),
            source.AccountSnapshotId,
            source.PositionSnapshotId,
            source.PositionAsOfUtc
        });
        var manifest = Arch5bHashing.HashCanonical(new
        {
            RevisionId = revisionId,
            Input = input,
            Targets = targetSha,
            Drifts = driftSha,
            Supersedes = supersedesSlotManifestSha256,
            NoOrder = true,
            Blocker = PmsShadowStateContract.BrokerAdjustedBlocker
        });
        return new(revisionId, revisionNumber, capture.SlotId, capture.SlotStartUtc, capture.SlotEndUtc,
            capture.ArtifactSha256, marketId, marketSha, source.IngestionId, source.SourceSessionId,
            source.AccountSnapshotId, source.PositionSnapshotId, source.PositionAsOfUtc,
            source.PositionAuthority, source.Models.Select(value => value.ModelRunId).ToArray(),
            source.Models.Select(value => value.QubesInputSnapshotId).ToArray(),
            source.Models.Select(value => new PmsShadowSelectedModelRun(value.ModelRunId,
                value.QubesInputSnapshotId, value.StrategyId, value.AsOfUtc, value.TargetCloseUtc,
                value.OutputSha256, value.CoreCommitId, "REUSED_FINALIZED_D1_MODEL")).ToArray(),
            observations, targets, drifts, input, targetSha, driftSha, manifest,
            supersedesSlotManifestSha256, "COMPLETED", PmsShadowStateContract.CompletedNoExternal,
            true, true,
            capture.SlotEndUtc.AddMinutes(1));
    }

    private static (string Base, string Quote) Pair(string symbol)
    {
        var normalized = new string(symbol.ToUpperInvariant().Where(char.IsAsciiLetterUpper).ToArray());
        if (normalized.Length != 6) throw new InvalidDataException($"FX_SYMBOL_INVALID:{symbol}");
        return (normalized[..3], normalized[3..]);
    }
}

public static class PmsShadowEconomicProjectionConflictGuard
{
    public static void RequireIdentical(PmsShadowIntradayEconomicProjection stored,
        PmsShadowIntradayEconomicProjection candidate)
    {
        if (stored.ProjectionRevisionId != candidate.ProjectionRevisionId ||
            stored.InputSha256 != candidate.InputSha256 ||
            stored.MarketDataSnapshotSha256 != candidate.MarketDataSnapshotSha256 ||
            stored.TargetPositionsSha256 != candidate.TargetPositionsSha256 ||
            stored.DriftsSha256 != candidate.DriftsSha256 ||
            stored.ManifestSha256 != candidate.ManifestSha256)
            throw new InvalidDataException("FAILED_CLOSED_CONFLICT");
    }
}

public enum PmsShadowEconomicApplyResult { Completed, AlreadyAppliedIdentical }
public sealed record PmsShadowEconomicApplyOutcome(PmsShadowEconomicApplyResult Result,
    Guid ProjectionRevisionId, string SlotId, int TargetPositionCount, int DriftCount,
    string ManifestSha256);

public interface IPmsShadowIntradayEconomicProjectionStore
{
    Task<string?> LoadSupersededManifestShaAsync(string slotId,
        CancellationToken cancellationToken = default);
    Task<PmsShadowEconomicSource> LoadSourceAsync(
        string sourceSessionId, DateTimeOffset slotStartUtc,
        CancellationToken cancellationToken = default);
    Task<PmsShadowEconomicApplyOutcome> ApplyAsync(PmsShadowIntradayEconomicProjection projection,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PmsShadowIntradayEconomicProjection>> ReadAllAsync(
        CancellationToken cancellationToken = default);
}

public sealed class EfPmsShadowIntradayEconomicProjectionStore(
    IDbContextFactory<PmsShadowDbContext> contextFactory) : IPmsShadowIntradayEconomicProjectionStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<string?> LoadSupersededManifestShaAsync(string slotId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT manifest_sha256 FROM pms_shadow.intraday_slots " +
                "WHERE slot_id=@slot_id AND status='COMPLETED'";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "slot_id";
            parameter.Value = slotId;
            command.Parameters.Add(parameter);
            return ReadOptionalManifestSha(await command.ExecuteScalarAsync(cancellationToken));
        }
        finally { await connection.CloseAsync(); }
    }

    private static string? ReadOptionalManifestSha(object? value) => value switch
    {
        null or DBNull => null,
        string manifestSha => manifestSha,
        _ => throw new InvalidDataException("INVALID_SLOT_MANIFEST_SHA")
    };

    public async Task<PmsShadowEconomicSource> LoadSourceAsync(
        string sourceSessionId, DateTimeOffset slotStartUtc,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var ingestion = await context.Ingestions.AsNoTracking().SingleAsync(value =>
            value.SourceSessionId == sourceSessionId && value.Status == PmsShadowIngestionStatuses.Completed,
            cancellationToken);
        var account = await context.AccountSnapshots.AsNoTracking().SingleAsync(value =>
            value.IngestionId == ingestion.IngestionId, cancellationToken);
        var positionCandidates = await context.PositionSnapshots.AsNoTracking().Where(value =>
            value.IngestionId == ingestion.IngestionId).ToArrayAsync(cancellationToken);
        var position = PmsShadowPositionSnapshotForSlotSelection.Select(
            positionCandidates, slotStartUtc);
        var positionLines = await context.PositionSnapshotLines.AsNoTracking().Where(value =>
            value.PositionSnapshotId == position.PositionSnapshotId)
            .ToArrayAsync(cancellationToken);
        var mappings = await context.SecurityMappings.AsNoTracking().Where(value =>
            value.IngestionId == ingestion.IngestionId).OrderBy(value => value.SecurityId)
            .ToArrayAsync(cancellationToken);
        PmsShadowPositionSnapshotForSlotSelection.ValidateSelected(
            position, ingestion, account, positionLines, mappings);
        var current = positionLines.ToDictionary(
            value => value.InstrumentId, value => value.CurrentBaseQuantity);
        var economicMappings = mappings.Select(value =>
            new PmsShadowEconomicMapping(value.InstrumentId, value.VenueId,
                value.VenueInstrumentId, value.SecurityId, value.Symbol, value.LmaxInstrumentId,
                value.QuantityMultiplier, value.QuantityIncrement, value.PriceIncrement))
            .ToArray();
        var models = await context.ModelRuns.AsNoTracking().Where(value =>
            value.IngestionId == ingestion.IngestionId).OrderBy(value => value.StrategyId)
            .ToArrayAsync(cancellationToken);
        var modelIds = models.Select(value => value.ModelRunId).ToArray();
        var weights = await context.TargetWeights.AsNoTracking().Where(value =>
            modelIds.Contains(value.ModelRunId)).OrderBy(value => value.SecurityId).ToArrayAsync(cancellationToken);
        return new(ingestion.IngestionId, sourceSessionId, account.AccountSnapshotId, account.NavOrEquity,
            position.PositionSnapshotId, position.AsOfUtc, position.Classification, current,
            economicMappings,
            models.Select(model => new PmsShadowEconomicModel(model.ModelRunId, model.QubesInputSnapshotId,
                model.StrategyId, model.TargetCloseUtc, model.AsOfUtc, model.OutputSha256,
                model.CoreMasterCommitId, weights.Where(value => value.ModelRunId == model.ModelRunId)
                    .Select(value => new PmsShadowEconomicWeight(value.InstrumentId, value.SecurityId,
                        value.Weight)).ToArray())).ToArray());
    }

    public async Task<PmsShadowEconomicApplyOutcome> ApplyAsync(
        PmsShadowIntradayEconomicProjection projection, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        var lockKey = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(projection.SlotId)), 0);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await Execute(connection, transaction, "SELECT pg_advisory_xact_lock(@lock_key)", cancellationToken,
                ("lock_key", lockKey));
            var existing = await ScalarString(connection, transaction,
                "SELECT projection_json::text FROM pms_shadow.intraday_projection_revisions " +
                "WHERE projection_revision_id=@id", cancellationToken, ("id", projection.ProjectionRevisionId));
            if (existing is not null)
            {
                var stored = JsonSerializer.Deserialize<PmsShadowIntradayEconomicProjection>(existing, Json)!;
                PmsShadowEconomicProjectionConflictGuard.RequireIdentical(stored, projection);
                await transaction.CommitAsync(cancellationToken);
                return Outcome(PmsShadowEconomicApplyResult.AlreadyAppliedIdentical, projection);
            }

            var projectionJson = JsonSerializer.Serialize(projection, Json);
            await Execute(connection, transaction, """
                INSERT INTO pms_shadow.intraday_projection_revisions
                (projection_revision_id,revision_number,slot_id,raw_capture_sha256,market_data_snapshot_id,
                 market_data_snapshot_sha256,source_ingestion_id,source_session_id,position_snapshot_id,
                 input_sha256,target_positions_sha256,drifts_sha256,manifest_sha256,
                 supersedes_slot_manifest_sha256,status,external_completion_status,qualifying,no_order,completed_at_utc,projection_json)
                VALUES (@id,@revision,@slot,@capture_sha,@market_id,@market_sha,@ingestion_id,@session,
                 @position_id,@input_sha,@targets_sha,@drifts_sha,@manifest_sha,@supersedes,
                 'COMPLETED',@external_completion,TRUE,TRUE,@completed,CAST(@projection_json AS jsonb))
                """, cancellationToken, ("id", projection.ProjectionRevisionId),
                ("revision", projection.RevisionNumber), ("slot", projection.SlotId),
                ("capture_sha", projection.RawCaptureSha256), ("market_id", projection.MarketDataSnapshotId),
                ("market_sha", projection.MarketDataSnapshotSha256), ("ingestion_id", projection.SourceIngestionId),
                ("session", projection.SourceSessionId), ("position_id", projection.PositionSnapshotId),
                ("input_sha", projection.InputSha256), ("targets_sha", projection.TargetPositionsSha256),
                ("drifts_sha", projection.DriftsSha256), ("manifest_sha", projection.ManifestSha256),
                ("supersedes", projection.SupersedesSlotManifestSha256 ?? (object)DBNull.Value),
                ("external_completion", projection.ExternalCompletionStatus),
                ("completed", projection.CompletedAtUtc), ("projection_json", projectionJson));
            foreach (var item in projection.MarketData)
            {
                var prices = PmsShadowIntradayEconomicContract.ToPostgreSqlMarketPrices(item.Bid, item.Ask);
                await Execute(connection, transaction, """
                    INSERT INTO pms_shadow.intraday_market_data_observations
                    (projection_revision_id,instrument_id,security_id,symbol,lmax_instrument_id,bid,ask,
                     decision_price,event_time_utc,projection_method,projection_leg_security_ids_json)
                    VALUES (@revision,@instrument,@security,@symbol,@lmax,@bid,@ask,@price,@event,@method,
                     CAST(@legs AS jsonb))
                    """, cancellationToken, ("revision", projection.ProjectionRevisionId),
                    ("instrument", item.InstrumentId), ("security", item.SecurityId), ("symbol", item.Symbol),
                    ("lmax", item.LmaxInstrumentId), ("bid", prices.Bid), ("ask", prices.Ask),
                    ("price", prices.DecisionPrice), ("event", item.EventTimeUtc),
                    ("method", item.ProjectionMethod), ("legs", JsonSerializer.Serialize(item.ProjectionLegSecurityIds)));
            }
            foreach (var item in projection.TargetPositions)
                await Execute(connection, transaction, """
                    INSERT INTO pms_shadow.intraday_target_positions
                    (target_position_id,projection_revision_id,stage_id,model_run_id,strategy_id,instrument_id,
                     security_id,target_notional_usd,target_base_quantity,target_venue_quantity,decision_price,
                     target_close_utc,calculated_at_utc,input_sha256,output_sha256,no_order)
                    VALUES (@id,@revision,@stage,@model,@strategy,@instrument,@security,@notional,@base,@venue,
                     @price,@close,@calculated,@input,@output,TRUE)
                    """, cancellationToken, ("id", item.TargetPositionId),
                    ("revision", projection.ProjectionRevisionId), ("stage", item.StageId),
                    ("model", item.ModelRunId), ("strategy", item.StrategyId),
                    ("instrument", item.InstrumentId), ("security", item.SecurityId),
                    ("notional", item.TargetNotionalUsd), ("base", item.TargetBaseQuantity),
                    ("venue", item.TargetVenueQuantity), ("price", item.DecisionPrice),
                    ("close", item.TargetCloseUtc), ("calculated", item.CalculatedAtUtc),
                    ("input", item.InputSha256), ("output", item.OutputSha256));
            foreach (var item in projection.PositionOnlyDrifts)
                await Execute(connection, transaction, """
                    INSERT INTO pms_shadow.intraday_position_only_drifts
                    (drift_id,projection_revision_id,stage_id,model_run_id,strategy_id,instrument_id,security_id,
                     current_base_quantity,target_base_quantity,delta,as_of_utc,input_sha256,output_sha256,
                     broker_adjusted_calculated,working_leaves_blocker,no_order)
                    VALUES (@id,@revision,@stage,@model,@strategy,@instrument,@security,@current,@target,@delta,
                     @asof,@input,@output,FALSE,@blocker,TRUE)
                    """, cancellationToken, ("id", item.DriftId),
                    ("revision", projection.ProjectionRevisionId), ("stage", item.StageId),
                    ("model", item.ModelRunId), ("strategy", item.StrategyId),
                    ("instrument", item.InstrumentId), ("security", item.SecurityId),
                    ("current", item.CurrentBaseQuantity), ("target", item.TargetBaseQuantity),
                    ("delta", item.Delta), ("asof", item.AsOfUtc), ("input", item.InputSha256),
                    ("output", item.OutputSha256), ("blocker", PmsShadowStateContract.BrokerAdjustedBlocker));
            await transaction.CommitAsync(cancellationToken);
            return Outcome(PmsShadowEconomicApplyResult.Completed, projection);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally { await connection.CloseAsync(); }
    }

    public async Task<IReadOnlyList<PmsShadowIntradayEconomicProjection>> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT projection_json::text FROM pms_shadow.intraday_projection_revisions " +
                "WHERE status='COMPLETED' AND qualifying ORDER BY completed_at_utc,projection_revision_id";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var result = new List<PmsShadowIntradayEconomicProjection>();
            while (await reader.ReadAsync(cancellationToken))
                result.Add(JsonSerializer.Deserialize<PmsShadowIntradayEconomicProjection>(reader.GetString(0), Json)!);
            return result;
        }
        finally { await connection.CloseAsync(); }
    }

    private static PmsShadowEconomicApplyOutcome Outcome(PmsShadowEconomicApplyResult result,
        PmsShadowIntradayEconomicProjection value) => new(result, value.ProjectionRevisionId,
        value.SlotId, value.TargetPositions.Count, value.PositionOnlyDrifts.Count, value.ManifestSha256);
    private static async Task<string?> ScalarString(DbConnection connection, DbTransaction transaction,
        string sql, CancellationToken token, params (string Name, object Value)[] parameters)
    {
        await using var command = Command(connection, transaction, sql, parameters);
        return await command.ExecuteScalarAsync(token) as string;
    }
    private static async Task Execute(DbConnection connection, DbTransaction transaction, string sql,
        CancellationToken token, params (string Name, object Value)[] parameters)
    {
        await using var command = Command(connection, transaction, sql, parameters);
        await command.ExecuteNonQueryAsync(token);
    }
    private static DbCommand Command(DbConnection connection, DbTransaction transaction, string sql,
        params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter);
        }
        return command;
    }
}

public sealed class PmsShadowIntradayEconomicRefreshPipeline : IPmsShadowIntradaySlotPipeline
{
    private readonly string captureRoot;
    private readonly string sourceSessionId;
    private readonly IPmsShadowIntradayEconomicProjectionStore store;
    private readonly Arch7bPositionMarketImportAuthority? positionMarketAuthority;

    public PmsShadowIntradayEconomicRefreshPipeline(string captureRoot,
        string sourceSessionId, IPmsShadowIntradayEconomicProjectionStore store,
        Arch7bPositionMarketImportAuthority? positionMarketAuthority = null)
    {
        this.captureRoot = captureRoot;
        this.sourceSessionId = sourceSessionId;
        this.store = store;
        this.positionMarketAuthority = positionMarketAuthority;
    }

    public async Task<PmsShadowIntradaySlotManifest> ExecuteAsync(PmsShadowIntradaySlotWindow slot,
        CancellationToken cancellationToken = default)
    {
        var manifestPath = Path.Combine(captureRoot, slot.SlotId,
            "slot_manifest.json");
        var capture = PmsShadowRealSlotCaptureReader.Read(manifestPath);
        var source = await store.LoadSourceAsync(
            sourceSessionId, slot.SlotStartUtc, cancellationToken);
        var lineage = positionMarketAuthority is null ? null :
            Arch7bPositionMarketLiveWiring.RequireImportBinding(
                manifestPath, positionMarketAuthority, source, capture);
        var oldRows = await store.LoadSupersededManifestShaAsync(slot.SlotId, cancellationToken);
        var projection = new PmsShadowIntradayEconomicProjectionBuilder().Build(capture, source, oldRows);
        (Arch7bEconomicRevisionInputBinding Binding,
            Arch7bContentAddressedFile File)? revisionBinding =
            lineage is null ? null :
            Arch7bPositionMarketLiveWiring.BindAndPublishRevision(
                lineage, projection, positionMarketAuthority!.RevisionBindingPath);
        var outcome = await store.ApplyAsync(projection, cancellationToken);
        return new(slot.SlotId, slot.SlotStartUtc, slot.SlotEndUtc, slot.OperationalDate,
            capture.RecorderRunId + "/" + slot.SlotId, capture.ArtifactSha256, 0, [], 0, [], false,
            source.Models.Select(value => value.QubesInputSnapshotId).ToArray(), [],
            source.Models.Select(value => value.ModelRunId).ToArray(),
            source.Models.ToDictionary(value => value.StrategyId, value => value.OutputSha256),
            projection.TargetPositions.Count, projection.PositionOnlyDrifts.Count,
            PmsShadowStateContract.BrokerAdjustedBlocker,
            revisionBinding?.File.Sha256 ?? projection.ManifestSha256, source.SourceSessionId,
            source.IngestionId, outcome.Result == PmsShadowEconomicApplyResult.Completed ? "COMPLETED" :
                "ALREADY_APPLIED_IDENTICAL", new Dictionary<string, int>
                {
                    ["projection_revisions"] = 1,
                    ["market_data_observations"] = projection.MarketData.Count,
                    ["target_positions"] = projection.TargetPositions.Count,
                    ["position_only_drifts"] = projection.PositionOnlyDrifts.Count
                },
            PmsShadowIntradayFreshness.Fresh, PmsShadowIntradayNoOrderCounters.Zero, true,
            projection.CompletedAtUtc);
    }

}
