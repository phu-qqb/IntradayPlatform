using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class InstitutionalMetricCatalog
{
    public static IReadOnlyList<InstitutionalMetricDefinition> Build()
    {
        var result = new List<InstitutionalMetricDefinition>
        {
            Source("TARGET_POSITION_NOTIONAL", "PORTFOLIO",
                "Persisted TargetPosition notional at its source grain.",
                "economic_revision/target_position", "USD",
                InstitutionalMetricContract.ExposureFormula, ["TargetPositions"]),
            Source("POSITION_ONLY_DRIFT_SOURCE", "PORTFOLIO",
                "Persisted PositionOnlyDrift delta at its source grain.",
                "economic_revision/position_only_drift", "BASE_CURRENCY",
                InstitutionalMetricContract.DriftFormula, ["PositionOnlyDrifts"]),
            Derivable("GROSS_TARGET_EXPOSURE", "RISK",
                "Sum of absolute target notionals.", "economic_revision", "USD",
                InstitutionalMetricContract.ExposureFormula, ["TargetPositions"]),
            Derivable("NET_TARGET_EXPOSURE", "RISK",
                "Sum of signed target notionals.", "economic_revision", "USD",
                InstitutionalMetricContract.ExposureFormula, ["TargetPositions"]),
            Derivable("LONG_TARGET_NOTIONAL", "RISK",
                "Sum of positive target notionals.", "economic_revision", "USD",
                InstitutionalMetricContract.ExposureFormula, ["TargetPositions"]),
            Derivable("SHORT_TARGET_NOTIONAL", "RISK",
                "Absolute sum of negative target notionals.", "economic_revision", "USD",
                InstitutionalMetricContract.ExposureFormula, ["TargetPositions"]),
            Derivable("TARGET_CURRENCY_EXPOSURE", "RISK",
                "Canonical base and quote leg target exposure.",
                "economic_revision/currency", "USD",
                InstitutionalMetricContract.CurrencyFormula,
                ["TargetPositions", "SecurityMappings"]),
            Derivable("ABSOLUTE_POSITION_ONLY_DRIFT", "RISK",
                "Absolute PositionOnlyDrift at compatible pair grains only.",
                "economic_revision/canonical_pair", "BASE_CURRENCY",
                InstitutionalMetricContract.DriftFormula,
                ["PositionOnlyDrifts", "SecurityMappings"]),
            Derivable("PAIR_GROSS_CONCENTRATION", "RISK",
                "Pair gross notional divided by portfolio gross notional.",
                "economic_revision/pair", "RATIO",
                InstitutionalMetricContract.GrossConcentrationFormula, ["TargetPositions"]),
            Derivable("PAIR_NET_CONCENTRATION", "RISK",
                "Absolute pair net notional divided by the sum of absolute pair net notionals.",
                "economic_revision/pair", "RATIO",
                InstitutionalMetricContract.NetConcentrationFormula, ["TargetPositions"]),
            Derivable("STRATEGY_GROSS_CONCENTRATION", "RISK",
                "Strategy gross notional divided by portfolio gross notional.",
                "economic_revision/strategy", "RATIO",
                InstitutionalMetricContract.GrossConcentrationFormula, ["TargetPositions"]),
            Derivable("STRATEGY_NET_CONCENTRATION", "RISK",
                "Absolute strategy net notional divided by the sum of absolute strategy net notionals.",
                "economic_revision/strategy", "RATIO",
                InstitutionalMetricContract.NetConcentrationFormula, ["TargetPositions"]),
            Derivable("TOP_N_CONCENTRATION", "RISK",
                "Separate gross and net top-N concentration with explicit N.",
                "economic_revision/dimension_type/family/N", "RATIO",
                InstitutionalMetricContract.GrossConcentrationFormula, ["TargetPositions"]),
            Derivable("TARGET_HHI", "RISK",
                "Separate gross and net HHI over normalized share sets.",
                "economic_revision/dimension_type/family", "RATIO",
                InstitutionalMetricContract.GrossConcentrationFormula, ["TargetPositions"]),
            Derivable("GROSS_NET_RATIO", "RISK",
                "Portfolio gross divided by absolute portfolio net when net is non-zero.",
                "economic_revision", "RATIO",
                InstitutionalMetricContract.GrossConcentrationFormula, ["TargetPositions"]),
            Derivable("TARGET_TURNOVER", "PORTFOLIO",
                "Gross target change between successive authoritative slots.",
                "previous_slot/current_slot/dimension", "USD",
                InstitutionalMetricContract.TurnoverFormula,
                ["TargetPositions", "EconomicRevisions", "SecurityMappings"])
        };

        foreach (var blocked in BlockedMetrics())
            result.Add(Blocked(blocked.Code, blocked.Category, blocked.Description,
                blocked.Required, blocked.Authority));
        return result.OrderBy(value => value.MetricCode, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<(string Code, string Category, string Description,
        string[] Required, string Authority)> BlockedMetrics() =>
    [
        ("ACTUAL_EXECUTION_COST", "COST", "Actual authoritative execution cost.", ["RealFills", "CostContract"], "Execution and cost authority"),
        ("ACTUAL_EXECUTION_VS_COST_MODEL", "COST", "Actual execution compared with a versioned cost model.", ["RealFills", "CostModel"], "Execution and cost-model authority"),
        ("BROKER_POSITION", "PORTFOLIO", "Current authoritative broker position.", ["BrokerPositionFacts"], "Broker position authority"),
        ("CASH", "PORTFOLIO", "Authoritative cash balance.", ["CashFacts"], "Fund accounting authority"),
        ("CUMULATIVE_PNL", "PERFORMANCE", "Cumulative authoritative PnL.", ["RealizedPnl", "UnrealizedPnl"], "Fund accounting authority"),
        ("DAILY_PNL", "PERFORMANCE", "Daily authoritative PnL.", ["RealizedPnl", "UnrealizedPnl"], "Fund accounting authority"),
        ("DRIFT_USD_BY_MODEL", "RISK", "Cross-pair model drift converted to USD.", ["QualifiedMarks", "VersionedFxConversionContract"], "Position and mark authority"),
        ("DRIFT_USD_BY_STRATEGY", "RISK", "Cross-pair strategy drift converted to USD.", ["QualifiedMarks", "VersionedFxConversionContract"], "Position and mark authority"),
        ("DRAWDOWN", "PERFORMANCE", "Drawdown from an authoritative return series.", ["NavSeries"], "Fund accounting authority"),
        ("EXECUTED_TURNOVER", "TCA", "Turnover from authoritative executions.", ["RealFills"], "Broker execution authority"),
        ("GROSS_PERFORMANCE", "PERFORMANCE", "Gross fund performance.", ["NavSeries", "CashFlows"], "Fund accounting authority"),
        ("HIT_RATE", "PERFORMANCE", "Hit rate from authoritative closed outcomes.", ["RealizedPnl"], "Fund accounting authority"),
        ("IMPLEMENTATION_SHORTFALL", "TCA", "Execution shortfall against a versioned benchmark.", ["RealFills", "BenchmarkMarks"], "Broker execution authority"),
        ("LEVERAGE", "RISK", "Gross exposure divided by authoritative NAV or AUM.", ["NavOrAum"], "Fund accounting authority"),
        ("LIVE_CAPACITY_USED", "RISK", "Live AUM relative to a versioned capacity contract.", ["Aum", "CapacityContract"], "Capacity authority"),
        ("LIVE_TCA", "TCA", "Live transaction-cost analysis.", ["RealFills", "BenchmarkMarks"], "Broker execution authority"),
        ("LIVE_VERSUS_BACKTEST", "PERFORMANCE", "Live performance compared with a versioned backtest.", ["LivePerformance", "BacktestComparisonContract"], "Performance authority"),
        ("LIVE_VERSUS_EXPECTATION", "PERFORMANCE", "Live performance compared with versioned expectation.", ["LivePerformance", "ExpectationContract"], "Performance authority"),
        ("MARKOUTS", "TCA", "Post-fill markouts at versioned horizons.", ["RealFills", "BenchmarkMarks"], "Broker execution authority"),
        ("NET_PERFORMANCE", "PERFORMANCE", "Net fund performance after authoritative fees.", ["NavSeries", "Fees", "CashFlows"], "Fund accounting authority"),
        ("PROFIT_FACTOR", "PERFORMANCE", "Gross gains divided by gross losses.", ["RealizedPnl"], "Fund accounting authority"),
        ("REALIZED_PNL", "PERFORMANCE", "Realized PnL from authoritative fills and accounting ledger.", ["RealFills", "AccountingLedger"], "Execution and accounting authority"),
        ("RECOVERY", "PERFORMANCE", "Recovery duration from an authoritative return series.", ["NavSeries"], "Fund accounting authority"),
        ("SHARPE", "PERFORMANCE", "Sharpe ratio from an authoritative return series.", ["NavSeries", "RiskFreeRateContract"], "Fund accounting authority"),
        ("SLIPPAGE", "TCA", "Execution slippage against a versioned benchmark.", ["RealFills", "BenchmarkMarks"], "Broker execution authority"),
        ("SORTINO", "PERFORMANCE", "Sortino ratio from an authoritative return series.", ["NavSeries", "MinimumAcceptableReturnContract"], "Fund accounting authority"),
        ("UNREALIZED_PNL", "PERFORMANCE", "Unrealized PnL from authoritative positions and qualified marks.", ["AuthoritativePositions", "QualifiedMarks"], "Position and mark authority"),
        ("VOLATILITY", "PERFORMANCE", "Volatility from an authoritative return series.", ["NavSeries"], "Fund accounting authority")
    ];

    private static InstitutionalMetricDefinition Source(string code, string category,
        string description, string grain, string unit, string formula, string[] facts) =>
        Definition(code, category, description, grain, unit, formula, facts,
            MetricAvailabilityStatus.SourceProven, "PMS economic revision");

    private static InstitutionalMetricDefinition Derivable(string code, string category,
        string description, string grain, string unit, string formula, string[] facts) =>
        Definition(code, category, description, grain, unit, formula, facts,
            MetricAvailabilityStatus.DerivableProven, "PMS economic revision");

    private static InstitutionalMetricDefinition Blocked(string code, string category,
        string description, string[] facts, string authority) =>
        Definition(code, category, description, "NOT_AVAILABLE", null, "not_available_v1",
            facts, MetricAvailabilityStatus.BlockedMissingSource, authority);

    private static InstitutionalMetricDefinition Definition(string code, string category,
        string description, string grain, string? unit, string formula, string[] facts,
        string availability, string authority) =>
        new(code, category, description, grain, unit, formula, facts, availability, authority,
            new[] { "economic_revision", "strategy", "model", "pair", "currency" }.Where(value =>
                grain.Contains(value, StringComparison.OrdinalIgnoreCase)).ToArray(),
            ["sum_across_as_of", "sum_ratio", "sum_concentration", "sum_mixed_base_units"],
            "RPT2", category is "TCA" ? "TCA" : "PMS/portfolio and risk");
}

public static class InstitutionalAuthoritativeRevisionResolver
{
    public const string AmbiguousCode = "RPT2_AUTHORITATIVE_ECONOMIC_REVISION_AMBIGUOUS";
    public const string ModelSetIncomplete = "RPT2_SELECTED_MODEL_SET_INCOMPLETE";
    public const string ModelSetDuplicated = "RPT2_SELECTED_MODEL_SET_DUPLICATED";
    public const string ModelSetUnexpected = "RPT2_SELECTED_MODEL_SET_UNEXPECTED";
    public const string ModelLineageMismatch = "RPT2_SELECTED_MODEL_LINEAGE_MISMATCH";
    public const string ModelCountsMismatch = "RPT2_SELECTED_MODEL_COUNTS_MISMATCH";

    public static IReadOnlyList<PmsShadowIntradayEconomicProjection> Resolve(
        IEnumerable<PmsShadowIntradayEconomicProjection> source,
        IReadOnlyDictionary<string, string?> slotManifestSha256BySlotId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(slotManifestSha256BySlotId);
        var all = source.ToArray();
        if (all.GroupBy(value => value.ProjectionRevisionId).Any(group => group.Count() > 1))
            throw new InvalidDataException(AmbiguousCode);

        var result = new List<PmsShadowIntradayEconomicProjection>();
        foreach (var slot in all.GroupBy(value => value.SlotId, StringComparer.Ordinal))
        {
            var candidates = slot.Where(IsCandidate).ToArray();
            if (candidates.Length == 0)
                continue;
            var maxRevision = candidates.Max(value => value.RevisionNumber);
            var top = candidates.Where(value => value.RevisionNumber == maxRevision).ToArray();
            if (top.Length != 1)
                throw new InvalidDataException(AmbiguousCode);
            var selected = top[0];
            if (selected.RevisionNumber == 1 &&
                selected.SupersedesSlotManifestSha256 is not null)
                throw new InvalidDataException("RPT2_SUPERSESSION_LINEAGE_INCOHERENT");
            if (selected.RevisionNumber > 1 &&
                (!slotManifestSha256BySlotId.TryGetValue(slot.Key, out var slotManifestSha256) ||
                 slotManifestSha256 is null ||
                 !IsSha(slotManifestSha256) ||
                 !string.Equals(selected.SupersedesSlotManifestSha256,
                     slotManifestSha256, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("RPT2_SUPERSESSION_LINEAGE_INCOHERENT");
            result.Add(selected);
        }

        return result.OrderBy(value => value.SlotEndUtc)
            .ThenBy(value => value.SlotStartUtc)
            .ThenBy(value => value.CompletedAtUtc)
            .ThenBy(value => value.SlotId, StringComparer.Ordinal)
            .ThenBy(value => value.ProjectionRevisionId)
            .ToArray();
    }

    internal static bool IsCandidate(PmsShadowIntradayEconomicProjection value) =>
        CandidateBlocker(value) is null;

    internal static string? CandidateBlocker(PmsShadowIntradayEconomicProjection value)
    {
        if (!string.Equals(value.Status, "COMPLETED", StringComparison.Ordinal) ||
            !value.Qualifying || !value.NoOrder ||
            !IsSha(value.ManifestSha256) ||
            !IsSha(value.TargetPositionsSha256) ||
            !IsSha(value.DriftsSha256) ||
            !IsSha(value.MarketDataSnapshotSha256))
            return "RPT2_REVISION_NOT_CANDIDATE";
        if (value.SelectedModelRuns.Count != OperationalReportingContract.ExpectedModelRunCount)
            return ModelSetIncomplete;
        if (value.SelectedModelRuns.GroupBy(model => model.StrategyId, StringComparer.Ordinal)
                .Any(group => group.Count() != 1) ||
            value.SelectedModelRuns.GroupBy(model => model.ModelRunId)
                .Any(group => group.Count() != 1) ||
            value.SelectedModelRuns.GroupBy(model => model.QubesInputSnapshotId)
                .Any(group => group.Count() != 1))
            return ModelSetDuplicated;
        var expected = OperationalReportingContract.ExpectedPerModelCounts;
        if (value.SelectedModelRuns.Any(model => !expected.ContainsKey(model.StrategyId)))
            return ModelSetUnexpected;
        if (value.SelectedModelRuns.Any(model =>
                model.ModelRunId == Guid.Empty ||
                model.QubesInputSnapshotId == Guid.Empty ||
                model.TargetCloseUtc.Offset != TimeSpan.Zero ||
                !IsSha(model.OutputSha256) ||
                !IsGitCommit(model.CoreCommitId)))
            return ModelLineageMismatch;
        if (value.MarketData.Count != OperationalReportingContract.ExpectedMarketObservationCount ||
            value.TargetPositions.Count != OperationalReportingContract.ExpectedTargetPositionCount ||
            value.PositionOnlyDrifts.Count !=
            OperationalReportingContract.ExpectedPositionOnlyDriftCount)
            return ModelCountsMismatch;
        var selectedModelIds = value.SelectedModelRuns.Select(model => model.ModelRunId)
            .Order().ToArray();
        var selectedInputIds = value.SelectedModelRuns
            .Select(model => model.QubesInputSnapshotId).Order().ToArray();
        if (!selectedModelIds.SequenceEqual(value.ReusedModelRunIds.Order()) ||
            !selectedInputIds.SequenceEqual(value.ModelInputSnapshotIds.Order()))
            return ModelLineageMismatch;
        var byId = value.SelectedModelRuns.ToDictionary(model => model.ModelRunId);
        if (value.TargetPositions.Any(target =>
                !byId.TryGetValue(target.ModelRunId, out var model) ||
                !string.Equals(target.StrategyId, model.StrategyId, StringComparison.Ordinal) ||
                target.TargetCloseUtc != model.TargetCloseUtc ||
                target.CalculatedAtUtc.Offset != TimeSpan.Zero ||
                target.DecisionPrice <= 0m ||
                !string.Equals(target.CoreCommitId, model.CoreCommitId,
                    StringComparison.OrdinalIgnoreCase) ||
                !IsSha(target.InputSha256) || !IsSha(target.OutputSha256)) ||
            value.PositionOnlyDrifts.Any(drift =>
                !byId.TryGetValue(drift.ModelRunId, out var model) ||
                !string.Equals(drift.StrategyId, model.StrategyId, StringComparison.Ordinal) ||
                drift.AsOfUtc.Offset != TimeSpan.Zero ||
                !IsSha(drift.InputSha256) || !IsSha(drift.OutputSha256)))
            return ModelLineageMismatch;
        foreach (var model in value.SelectedModelRuns)
        {
            var targetCount = value.TargetPositions.Count(target =>
                target.ModelRunId == model.ModelRunId);
            var driftCount = value.PositionOnlyDrifts.Count(drift =>
                drift.ModelRunId == model.ModelRunId);
            if (targetCount != expected[model.StrategyId] ||
                driftCount != expected[model.StrategyId] ||
                targetCount != driftCount)
                return ModelCountsMismatch;
        }
        return null;
    }

    internal static bool IsSha(string value) =>
        value.Length == 64 && value.All(char.IsAsciiHexDigit);

    internal static bool IsGitCommit(string value) =>
        (value.Length == 40 || value.Length == 64) && value.All(char.IsAsciiHexDigit);
}

public static class InstitutionalMetricProjector
{
    public static InstitutionalMetricReportSet Build(
        OperationalReportingSnapshot snapshot,
        string roadmapSha256)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Require(InstitutionalAuthoritativeRevisionResolver.IsSha(roadmapSha256),
            "RPT2_ROADMAP_SHA_INVALID");
        var revisions = InstitutionalAuthoritativeRevisionResolver.Resolve(
            snapshot.EconomicProjectionSources,
            snapshot.SlotManifestSha256BySlotId);
        var mappings = snapshot.SecurityMappingSources.ToDictionary(
            value => (value.IngestionId, value.InstrumentId));
        var targets = BuildTargetSources(revisions, mappings);
        var drifts = BuildDriftSources(revisions, mappings);
        var targetFacts = BuildTargetFacts(targets);
        var driftFacts = BuildDriftFacts(drifts);
        var byRevision = BuildExposure(targets, "REVISION", _ => "ALL");
        var byStrategy = BuildExposure(targets, "STRATEGY", value => value.Target.StrategyId);
        var byModel = BuildExposure(targets, "MODEL",
            value => value.Target.ModelRunId.ToString("D"));
        var byPair = BuildExposure(targets, "PAIR", value => value.CanonicalSymbol);
        var byCurrency = BuildCurrency(targets);
        var (grossConcentrations, netConcentrations, concentrationSummaries) =
            BuildConcentrations(byRevision, byStrategy, byPair);
        var turnover = BuildTurnover(revisions, mappings);
        var driftByPair = BuildDrift(drifts, "PAIR",
            value => value.CanonicalSymbol);
        var driftByStrategyPair = BuildDrift(drifts, "STRATEGY_PAIR",
            value => $"{value.Drift.StrategyId}|{value.CanonicalSymbol}");
        var driftByModelPair = BuildDrift(drifts, "MODEL_PAIR",
            value => $"{value.Drift.ModelRunId:D}|{value.CanonicalSymbol}");
        var operational = OperationalReportProjector.Build(snapshot);
        var activeBreaks = operational.Breaks.Where(value =>
            value.Status is OperationalBreakStatus.Active or OperationalBreakStatus.Unknown)
            .OrderBy(value => value.BreakId, StringComparer.Ordinal).ToArray();
        var quality = BuildQuality(snapshot, revisions, mappings, activeBreaks);
        var risk = BuildRisk(snapshot.AsOfUtc, revisions, byRevision, grossConcentrations,
            netConcentrations, concentrationSummaries, turnover, driftByPair);
        var catalog = InstitutionalMetricCatalog.Build();
        var availability = BuildAvailability(catalog, revisions, targetFacts, driftFacts,
            byCurrency, grossConcentrations, netConcentrations, concentrationSummaries,
            turnover, driftByPair, risk, quality);
        var sourceSnapshot = BuildSourceSnapshot(snapshot, roadmapSha256, revisions,
            mappings, activeBreaks, quality);
        var sourceSnapshotSha = InstitutionalCanonicalJson.FileSha256(sourceSnapshot);
        return new(snapshot.AsOfUtc, snapshot.RepositoryCommit, snapshot.Database, roadmapSha256,
            catalog, availability, targetFacts, driftFacts, byRevision, byStrategy, byModel,
            byPair, byCurrency, grossConcentrations, netConcentrations,
            concentrationSummaries, turnover, driftByStrategyPair, driftByModelPair,
            driftByPair, risk, quality, activeBreaks, PowerBiContracts(), sourceSnapshot,
            sourceSnapshotSha);
    }

    private static IReadOnlyList<InstitutionalTargetSource> BuildTargetSources(
        IReadOnlyList<PmsShadowIntradayEconomicProjection> revisions,
        IReadOnlyDictionary<(Guid, Guid), PmsShadowSecurityMappingRow> mappings) =>
        revisions.SelectMany(revision => revision.TargetPositions.Select(target =>
        {
            Require(mappings.TryGetValue((revision.SourceIngestionId, target.InstrumentId),
                out var mapping), "RPT2_SECURITY_MAPPING_MISSING");
            var symbol = NormalizeSymbol(mapping!.Symbol);
            Require(symbol.Length == 6, "RPT2_CANONICAL_SYMBOL_INVALID");
            return new InstitutionalTargetSource(revision, target, mapping, symbol);
        })).ToArray();

    private static IReadOnlyList<InstitutionalDriftSource> BuildDriftSources(
        IReadOnlyList<PmsShadowIntradayEconomicProjection> revisions,
        IReadOnlyDictionary<(Guid, Guid), PmsShadowSecurityMappingRow> mappings) =>
        revisions.SelectMany(revision =>
        {
            var modelCloses = revision.SelectedModelRuns.ToDictionary(
                value => value.ModelRunId, value => value.TargetCloseUtc);
            return revision.PositionOnlyDrifts.Select(drift =>
            {
                Require(mappings.TryGetValue((revision.SourceIngestionId, drift.InstrumentId),
                    out var mapping), "RPT2_SECURITY_MAPPING_MISSING");
                Require(modelCloses.TryGetValue(drift.ModelRunId, out var targetClose),
                    "RPT2_DRIFT_MODEL_RUN_LINEAGE_MISSING");
                var symbol = NormalizeSymbol(mapping!.Symbol);
                Require(symbol.Length == 6, "RPT2_CANONICAL_SYMBOL_INVALID");
                return new InstitutionalDriftSource(revision, drift, mapping, symbol, targetClose);
            });
        }).ToArray();

    private static IReadOnlyList<TargetPositionFact> BuildTargetFacts(
        IReadOnlyList<InstitutionalTargetSource> sources) =>
        sources.Select(value => new TargetPositionFact(
                value.Revision.ProjectionRevisionId,
                value.Revision.RevisionNumber,
                value.Revision.SlotId,
                value.Target.TargetPositionId,
                value.Target.StrategyId,
                value.Target.ModelRunId,
                value.Target.TargetCloseUtc,
                value.Target.InstrumentId,
                value.Mapping.SecurityId,
                value.Mapping.LmaxInstrumentId,
                value.CanonicalSymbol,
                value.Target.TargetNotionalUsd,
                value.Target.TargetBaseQuantity,
                value.Target.TargetVenueQuantity,
                value.Target.CalculatedAtUtc,
                value.Revision.CompletedAtUtc,
                value.Target.DecisionPrice,
                value.Target.CoreCommitId,
                value.Target.InputSha256,
                value.Target.OutputSha256,
                Evidence("TARGET_POSITION_SOURCE_V1", value.Revision.ProjectionRevisionId,
                    value.Revision.ManifestSha256, value.Revision.TargetPositionsSha256,
                    value.Target.TargetPositionId, value.Target.InputSha256,
                    value.Target.OutputSha256),
                ReportingAuthority.Proven))
            .OrderBy(value => value.EconomicRevisionId)
            .ThenBy(value => value.TargetPositionId).ToArray();

    private static IReadOnlyList<PositionOnlyDriftFact> BuildDriftFacts(
        IReadOnlyList<InstitutionalDriftSource> sources) =>
        sources.Select(value =>
        {
            Require(value.Drift.Delta ==
                    value.Drift.TargetBaseQuantity - value.Drift.CurrentBaseQuantity,
                "RPT2_POSITION_ONLY_DRIFT_ARITHMETIC_MISMATCH");
            var authority = InstitutionalPositionAuthorityPolicy.Evaluate(value.Revision);
            return new PositionOnlyDriftFact(
                value.Revision.ProjectionRevisionId,
                value.Drift.DriftId,
                value.Drift.StrategyId,
                value.Drift.ModelRunId,
                value.TargetCloseUtc,
                value.Drift.InstrumentId,
                value.Mapping.SecurityId,
                value.Mapping.LmaxInstrumentId,
                value.CanonicalSymbol,
                value.Drift.CurrentBaseQuantity,
                value.Drift.TargetBaseQuantity,
                value.Drift.Delta,
                value.Drift.AsOfUtc,
                value.Revision.CompletedAtUtc,
                value.Revision.AccountSnapshotId,
                value.Revision.PositionSnapshotId,
                value.Revision.PositionSnapshotAsOfUtc,
                value.Revision.PositionAuthority,
                value.Drift.InputSha256,
                value.Drift.OutputSha256,
                value.CanonicalSymbol[..3],
                Evidence("POSITION_ONLY_DRIFT_SOURCE_V1",
                    value.Revision.ProjectionRevisionId, value.Revision.ManifestSha256,
                    value.Revision.DriftsSha256, value.Drift.DriftId,
                    value.Drift.InputSha256, value.Drift.OutputSha256),
                authority.AuthorityStatus);
        }).OrderBy(value => value.EconomicRevisionId)
            .ThenBy(value => value.PositionOnlyDriftId).ToArray();

    private static IReadOnlyList<TargetExposureRow> BuildExposure(
        IEnumerable<InstitutionalTargetSource> sources,
        string dimensionType,
        Func<InstitutionalTargetSource, string> key)
    {
        var sourceArray = sources.ToArray();
        var rows = sourceArray.GroupBy(value => new
            {
                value.Revision.ProjectionRevisionId,
                DimensionId = key(value)
            })
            .Select(group =>
            {
                var ordered = group.OrderBy(value => value.Target.TargetPositionId).ToArray();
                var first = ordered[0];
                var notionals = ordered.Select(value => value.Target.TargetNotionalUsd).ToArray();
                var gross = notionals.Sum(Math.Abs);
                var net = notionals.Sum();
                var model = dimensionType == "MODEL" ? first.Target.ModelRunId : (Guid?)null;
                var strategy = dimensionType is "STRATEGY" or "MODEL"
                    ? first.Target.StrategyId : null;
                var pair = dimensionType == "PAIR" ? first : null;
                var evidence = Evidence(
                    InstitutionalMetricContract.ExposureFormula,
                    first.Revision.ProjectionRevisionId,
                    first.Revision.TargetPositionsSha256,
                    first.Revision.ManifestSha256,
                    dimensionType,
                    group.Key.DimensionId,
                    gross,
                    net,
                    string.Join(',', ordered.Select(value => value.Target.TargetPositionId)));
                return new TargetExposureRow(
                    first.Revision.ProjectionRevisionId,
                    first.Revision.RevisionNumber,
                    first.Revision.SlotId,
                    first.Revision.SlotEndUtc,
                    dimensionType,
                    group.Key.DimensionId,
                    strategy,
                    model,
                    dimensionType is "STRATEGY" or "MODEL"
                        ? ordered.Select(value => value.Target.TargetCloseUtc).Distinct().Single()
                        : null,
                    pair?.Mapping.InstrumentId,
                    pair?.Mapping.SecurityId,
                    pair?.Mapping.LmaxInstrumentId,
                    pair?.CanonicalSymbol,
                    gross,
                    net,
                    notionals.Where(value => value > 0m).Sum(),
                    Math.Abs(notionals.Where(value => value < 0m).Sum()),
                    null,
                    "PENDING_GROSS_WEIGHT",
                    string.Empty,
                    InstitutionalMetricContract.ExposureFormula,
                    ReportingAuthority.Proven,
                    evidence);
            }).ToArray();
        return rows.Select(value =>
            {
                var totalGross = sourceArray.Where(source =>
                        source.Revision.ProjectionRevisionId == value.EconomicRevisionId)
                    .Sum(source => Math.Abs(source.Target.TargetNotionalUsd));
                return value with
                {
                    GrossWeight = totalGross == 0m
                        ? null
                        : value.GrossTargetNotionalUsd / totalGross,
                    DataQualityStatus = totalGross == 0m
                        ? "UNDEFINED_ZERO_GROSS" : "PROVEN",
                    Caveat = totalGross == 0m
                        ? "GrossWeight is NULL because portfolio gross target notional is zero."
                        : string.Empty
                };
            })
            .OrderBy(value => value.AsOfUtc)
            .ThenBy(value => value.DimensionId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<TargetCurrencyExposureRow> BuildCurrency(
        IReadOnlyList<InstitutionalTargetSource> sources) =>
        sources.SelectMany(value => new[]
            {
                new { Source = value, Currency = value.CanonicalSymbol[..3],
                    Amount = value.Target.TargetNotionalUsd },
                new { Source = value, Currency = value.CanonicalSymbol[3..],
                    Amount = -value.Target.TargetNotionalUsd }
            })
            .GroupBy(value => new
            {
                value.Source.Revision.ProjectionRevisionId,
                value.Currency
            })
            .Select(group =>
            {
                var ordered = group.OrderBy(value => value.Source.Target.TargetPositionId).ToArray();
                var first = ordered[0].Source.Revision;
                var signed = ordered.Sum(value => value.Amount);
                return new TargetCurrencyExposureRow(first.ProjectionRevisionId,
                    first.RevisionNumber, first.SlotId, first.SlotEndUtc,
                    group.Key.Currency, signed, ordered.Sum(value => Math.Abs(value.Amount)),
                    ordered.Length, InstitutionalMetricContract.CurrencyFormula,
                    ReportingAuthority.Proven,
                    Evidence(InstitutionalMetricContract.CurrencyFormula,
                        first.ProjectionRevisionId, first.TargetPositionsSha256,
                        first.ManifestSha256, group.Key.Currency, signed,
                        string.Join(',', ordered.Select(value =>
                            value.Source.Target.TargetPositionId))));
            })
            .OrderBy(value => value.AsOfUtc)
            .ThenBy(value => value.Currency, StringComparer.Ordinal)
            .ToArray();

    private static (
        IReadOnlyList<TargetConcentrationRow> Gross,
        IReadOnlyList<TargetConcentrationRow> Net,
        IReadOnlyList<TargetConcentrationSummaryRow> Summaries) BuildConcentrations(
        IReadOnlyList<TargetExposureRow> revisions,
        IReadOnlyList<TargetExposureRow> strategies,
        IReadOnlyList<TargetExposureRow> pairs)
    {
        var gross = new List<TargetConcentrationRow>();
        var net = new List<TargetConcentrationRow>();
        var summaries = new List<TargetConcentrationSummaryRow>();
        foreach (var revision in revisions)
        {
            AddConcentrationFamily(gross, net, summaries, revision, "PAIR",
                pairs.Where(value => value.EconomicRevisionId == revision.EconomicRevisionId));
            AddConcentrationFamily(gross, net, summaries, revision, "STRATEGY",
                strategies.Where(value => value.EconomicRevisionId == revision.EconomicRevisionId));
            var ratio = revision.NetTargetNotionalUsd == 0m
                ? (decimal?)null
                : revision.GrossTargetNotionalUsd / Math.Abs(revision.NetTargetNotionalUsd);
            summaries.Add(new(revision.EconomicRevisionId, "PORTFOLIO", "GROSS_NET_RATIO",
                Math.Abs(revision.NetTargetNotionalUsd), null, null, null, ratio,
                InstitutionalMetricContract.GrossConcentrationFormula,
                ratio.HasValue ? "PROVEN" : "UNDEFINED_ZERO_PORTFOLIO_NET",
                Evidence(InstitutionalMetricContract.GrossConcentrationFormula,
                    revision.EvidenceSha256, revision.GrossTargetNotionalUsd,
                    revision.NetTargetNotionalUsd, ratio),
                ratio.HasValue ? string.Empty :
                    "Gross/net ratio is NULL because portfolio net is zero."));
        }
        return (
            gross.OrderBy(value => value.EconomicRevisionId)
                .ThenBy(value => value.DimensionType, StringComparer.Ordinal)
                .ThenBy(value => value.Rank).ToArray(),
            net.OrderBy(value => value.EconomicRevisionId)
                .ThenBy(value => value.DimensionType, StringComparer.Ordinal)
                .ThenBy(value => value.Rank).ToArray(),
            summaries.OrderBy(value => value.EconomicRevisionId)
                .ThenBy(value => value.DimensionType, StringComparer.Ordinal)
                .ThenBy(value => value.Family, StringComparer.Ordinal).ToArray());
    }

    private static void AddConcentrationFamily(
        ICollection<TargetConcentrationRow> grossResult,
        ICollection<TargetConcentrationRow> netResult,
        ICollection<TargetConcentrationSummaryRow> summaries,
        TargetExposureRow revision,
        string type,
        IEnumerable<TargetExposureRow> sourceRows)
    {
        var rows = sourceRows.OrderBy(value => value.DimensionId, StringComparer.Ordinal).ToArray();
        var grossDenominator = revision.GrossTargetNotionalUsd;
        var netDenominator = rows.Sum(value => Math.Abs(value.NetTargetNotionalUsd));
        AddNormalizedRows(grossResult, summaries, revision, type, "GROSS", rows,
            grossDenominator, value => value.GrossTargetNotionalUsd,
            InstitutionalMetricContract.GrossConcentrationFormula, "UNDEFINED_ZERO_GROSS");
        AddNormalizedRows(netResult, summaries, revision, type, "NET", rows,
            netDenominator, value => Math.Abs(value.NetTargetNotionalUsd),
            InstitutionalMetricContract.NetConcentrationFormula,
            "UNDEFINED_ZERO_NET_ABSOLUTE");
    }

    private static void AddNormalizedRows(
        ICollection<TargetConcentrationRow> result,
        ICollection<TargetConcentrationSummaryRow> summaries,
        TargetExposureRow revision,
        string type,
        string family,
        IReadOnlyList<TargetExposureRow> rows,
        decimal denominator,
        Func<TargetExposureRow, decimal> numerator,
        string formula,
        string undefinedStatus)
    {
        var normalized = rows.Select(row => new
            {
                Row = row,
                Numerator = numerator(row),
                Share = denominator == 0m ? (decimal?)null : numerator(row) / denominator
            })
            .OrderByDescending(value => value.Share)
            .ThenBy(value => value.Row.DimensionId, StringComparer.Ordinal)
            .ToArray();
        var shareEvidence = string.Join('|', normalized.Select(value =>
            $"{value.Row.DimensionId}:{Canonical(value.Share)}"));
        for (var index = 0; index < normalized.Length; index++)
        {
            var item = normalized[index];
            result.Add(new(revision.EconomicRevisionId, type, item.Row.DimensionId, family,
                item.Row.GrossTargetNotionalUsd, item.Row.NetTargetNotionalUsd,
                denominator == 0m ? null : denominator, item.Share, index + 1, formula,
                denominator == 0m ? undefinedStatus : "PROVEN",
                Evidence(formula, item.Row.EvidenceSha256, denominator, shareEvidence,
                    item.Row.DimensionId, item.Share),
                denominator == 0m
                    ? "Concentration is NULL because its denominator is zero."
                    : string.Empty));
        }
        var defined = normalized.Where(value => value.Share.HasValue).ToArray();
        var topN = defined.Length == 0
            ? (decimal?)null
            : defined.Take(InstitutionalMetricContract.ConcentrationTopN)
                .Sum(value => value.Share!.Value);
        var hhi = defined.Length == 0
            ? (decimal?)null
            : defined.Sum(value => value.Share!.Value * value.Share.Value);
        summaries.Add(new(revision.EconomicRevisionId, type, family,
            denominator == 0m ? null : denominator,
            InstitutionalMetricContract.ConcentrationTopN, topN, hhi, null, formula,
            denominator == 0m ? undefinedStatus : "PROVEN",
            Evidence(formula, revision.EvidenceSha256, denominator, shareEvidence, topN, hhi),
            denominator == 0m
                ? "Top-N and HHI are NULL because their denominator is zero."
                : string.Empty));
    }

    private sealed record CanonicalTurnoverTarget(string StrategyId, string CanonicalSymbol,
        decimal TargetNotionalUsd);

    private static IReadOnlyList<TargetTurnoverRow> BuildTurnover(
        IReadOnlyList<PmsShadowIntradayEconomicProjection> revisions,
        IReadOnlyDictionary<(Guid, Guid), PmsShadowSecurityMappingRow> mappings)
    {
        var result = new List<TargetTurnoverRow>();
        for (var index = 1; index < revisions.Count; index++)
        {
            var previous = revisions[index - 1];
            var current = revisions[index];
            if (string.Equals(previous.SlotId, current.SlotId, StringComparison.Ordinal))
                continue;
            var previousMappings = MappingsFor(previous.SourceIngestionId, mappings);
            var currentMappings = MappingsFor(current.SourceIngestionId, mappings);
            foreach (var instrumentId in previousMappings.Keys.Intersect(currentMappings.Keys))
                Require(NormalizeSymbol(previousMappings[instrumentId].Symbol) ==
                        NormalizeSymbol(currentMappings[instrumentId].Symbol),
                    "RPT2_SECURITY_MAPPING_CONTRADICTORY");
            var previousTargets = CanonicalTurnoverTargets(previous, previousMappings);
            var currentTargets = CanonicalTurnoverTargets(current, currentMappings);
            var previousMappingSha = MappingSetSha(previousMappings.Values);
            var currentMappingSha = MappingSetSha(currentMappings.Values);
            var gapCount = OperationalGapCount(previous.SlotEndUtc, current.SlotEndUtc);
            var continuity = gapCount == 0
                ? "CONSECUTIVE_OPERATIONAL_SLOTS"
                : "GAP_SPANNING_AUTHORITATIVE_SNAPSHOTS";
            AddTurnover(result, previous, current, previousTargets, currentTargets,
                previousMappingSha, currentMappingSha, gapCount, continuity,
                "TOTAL", _ => "ALL");
            AddTurnover(result, previous, current, previousTargets, currentTargets,
                previousMappingSha, currentMappingSha, gapCount, continuity,
                "STRATEGY", value => value.StrategyId);
            AddTurnover(result, previous, current, previousTargets, currentTargets,
                previousMappingSha, currentMappingSha, gapCount, continuity,
                "PAIR", value => value.CanonicalSymbol);
        }
        return result.OrderBy(value => value.PeriodEndUtc)
            .ThenBy(value => value.DimensionType, StringComparer.Ordinal)
            .ThenBy(value => value.DimensionId, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyDictionary<Guid, PmsShadowSecurityMappingRow> MappingsFor(
        Guid ingestionId,
        IReadOnlyDictionary<(Guid, Guid), PmsShadowSecurityMappingRow> mappings) =>
        mappings.Where(value => value.Key.Item1 == ingestionId)
            .ToDictionary(value => value.Key.Item2, value => value.Value);

    private static IReadOnlyList<CanonicalTurnoverTarget> CanonicalTurnoverTargets(
        PmsShadowIntradayEconomicProjection revision,
        IReadOnlyDictionary<Guid, PmsShadowSecurityMappingRow> mappings) =>
        revision.TargetPositions.Select(target =>
        {
            Require(mappings.TryGetValue(target.InstrumentId, out var mapping),
                "RPT2_SECURITY_MAPPING_MISSING");
            var symbol = NormalizeSymbol(mapping!.Symbol);
            Require(symbol.Length == 6, "RPT2_CANONICAL_SYMBOL_INVALID");
            return new CanonicalTurnoverTarget(target.StrategyId, symbol,
                target.TargetNotionalUsd);
        }).ToArray();

    private static void AddTurnover(
        ICollection<TargetTurnoverRow> result,
        PmsShadowIntradayEconomicProjection previous,
        PmsShadowIntradayEconomicProjection current,
        IReadOnlyList<CanonicalTurnoverTarget> previousTargets,
        IReadOnlyList<CanonicalTurnoverTarget> currentTargets,
        string previousMappingSha,
        string currentMappingSha,
        int operationalSlotGapCount,
        string periodContinuityStatus,
        string dimensionType,
        Func<CanonicalTurnoverTarget, string> dimension)
    {
        static Dictionary<(string StrategyId, string CanonicalSymbol), decimal> Values(
            IEnumerable<CanonicalTurnoverTarget> values) =>
            values.GroupBy(value => (value.StrategyId, value.CanonicalSymbol))
                .ToDictionary(group => group.Key,
                    group => group.Sum(value => value.TargetNotionalUsd));
        var previousByDimension = previousTargets.GroupBy(dimension)
            .ToDictionary(group => group.Key, Values, StringComparer.Ordinal);
        var currentByDimension = currentTargets.GroupBy(dimension)
            .ToDictionary(group => group.Key, Values, StringComparer.Ordinal);
        foreach (var id in previousByDimension.Keys.Concat(currentByDimension.Keys)
                     .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var oldValues = previousByDimension.GetValueOrDefault(id) ?? [];
            var newValues = currentByDimension.GetValueOrDefault(id) ?? [];
            var keys = oldValues.Keys.Concat(newValues.Keys).Distinct().ToArray();
            var changes = keys.Select(key =>
            {
                var oldValue = oldValues.GetValueOrDefault(key);
                var newValue = newValues.GetValueOrDefault(key);
                return (Old: oldValue, New: newValue, Delta: Math.Abs(newValue - oldValue));
            }).ToArray();
            var turnover = changes.Sum(value => value.Delta);
            result.Add(new(previous.ProjectionRevisionId, current.ProjectionRevisionId,
                previous.SlotId, current.SlotId,
                previous.SlotEndUtc, current.SlotEndUtc,
                previous.SlotEndUtc, current.SlotEndUtc,
                operationalSlotGapCount, periodContinuityStatus, dimensionType, id,
                turnover,
                changes.Count(value => value.Old == 0m && value.New != 0m),
                changes.Count(value => value.Old != 0m && value.New == 0m),
                changes.Count(value => SameSign(value.Old, value.New) &&
                                       Math.Abs(value.New) > Math.Abs(value.Old)),
                changes.Count(value => SameSign(value.Old, value.New) &&
                                       value.New != 0m && Math.Abs(value.New) < Math.Abs(value.Old)),
                changes.Count(value => value.Old * value.New < 0m),
                previousMappingSha, currentMappingSha,
                "TARGET_TURNOVER", InstitutionalMetricContract.TurnoverFormula,
                MetricAvailabilityStatus.DerivableProven,
                Evidence(InstitutionalMetricContract.TurnoverFormula,
                    previous.ProjectionRevisionId, current.ProjectionRevisionId,
                    previous.TargetPositionsSha256, current.TargetPositionsSha256,
                    previousMappingSha, currentMappingSha,
                    previous.SlotId, current.SlotId, previous.SlotEndUtc,
                    current.SlotEndUtc, operationalSlotGapCount,
                    periodContinuityStatus, dimensionType, id, turnover)));
        }
    }

    private static int OperationalGapCount(
        DateTimeOffset previousSlotEndUtc,
        DateTimeOffset currentSlotEndUtc)
    {
        var count = 0;
        for (var end = previousSlotEndUtc.AddMinutes(
                 PmsShadowIntradayCadenceContract.SlotMinutes);
             end < currentSlotEndUtc;
             end = end.AddMinutes(PmsShadowIntradayCadenceContract.SlotMinutes))
            if (PmsShadowIntradayCadenceContract.IsOperational(
                    PmsShadowIntradayCadenceContract.WindowEnding(end)))
                count++;
        return count;
    }

    private static IReadOnlyList<DriftSummaryRow> BuildDrift(
        IReadOnlyList<InstitutionalDriftSource> sources,
        string dimensionType,
        Func<InstitutionalDriftSource, string> dimension) =>
        sources.GroupBy(value => new
            {
                value.Revision.ProjectionRevisionId,
                value.CanonicalSymbol,
                DimensionId = dimension(value)
            })
            .Select(group =>
            {
                var ordered = group.OrderBy(value => value.Drift.DriftId).ToArray();
                var first = ordered[0];
                var signed = ordered.Sum(value => value.Drift.Delta);
                var absolute = ordered.Sum(value => Math.Abs(value.Drift.Delta));
                var positionAuthority =
                    InstitutionalPositionAuthorityPolicy.Evaluate(first.Revision);
                return new DriftSummaryRow(first.Revision.ProjectionRevisionId,
                    dimensionType, group.Key.DimensionId, group.Key.CanonicalSymbol,
                    group.Key.CanonicalSymbol[..3], signed, absolute, ordered.Length,
                    positionAuthority.AuthorityStatus,
                    positionAuthority.AuthorityStatus == ReportingAuthority.Proven
                        ? MetricAvailabilityStatus.DerivableProven
                        : MetricAvailabilityStatus.BlockedAuthorityUnproven,
                    InstitutionalMetricContract.DriftFormula,
                    Evidence(InstitutionalMetricContract.DriftFormula,
                        first.Revision.ProjectionRevisionId, first.Revision.DriftsSha256,
                        first.Revision.ManifestSha256, dimensionType, group.Key.DimensionId,
                        group.Key.CanonicalSymbol, signed, absolute,
                        string.Join(',', ordered.Select(value => value.Drift.DriftId))));
            })
            .OrderBy(value => value.EconomicRevisionId)
            .ThenBy(value => value.DimensionId, StringComparer.Ordinal).ToArray();

    private static InstitutionalDataQuality BuildQuality(
        OperationalReportingSnapshot snapshot,
        IReadOnlyList<PmsShadowIntradayEconomicProjection> revisions,
        IReadOnlyDictionary<(Guid, Guid), PmsShadowSecurityMappingRow> mappings,
        IReadOnlyList<OperationalBreak> breaks)
    {
        var currentness = InstitutionalMetricCurrentnessPolicy.Evaluate(snapshot, revisions);
        var latest = revisions.LastOrDefault();
        var counts = latest?.TargetPositions.GroupBy(value => value.StrategyId)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)
            ?? new Dictionary<string, int>(StringComparer.Ordinal);
        var complete = OperationalReportingContract.ExpectedPerModelCounts.All(pair =>
            counts.GetValueOrDefault(pair.Key) == pair.Value);
        var mappingComplete = latest is not null && latest.TargetPositions.All(value =>
            mappings.ContainsKey((latest.SourceIngestionId, value.InstrumentId)));
        var lineage = latest is not null &&
                      InstitutionalAuthoritativeRevisionResolver.CandidateBlocker(latest) is null;
        var freshness = latest is null ? ReportingAuthority.Absent :
            currentness.MetricCurrentnessStatus.StartsWith("OBSOLÈTE",
                StringComparison.Ordinal) ||
            currentness.MetricCurrentnessStatus ==
            InstitutionalCurrentnessStatuses.StaleAfterDueTime
                ? ReportingAuthority.Stale : ReportingAuthority.Proven;
        var arch7a = latest is not null && snapshot.Arch7a.Any(value =>
            value.EconomicRevisionId == latest.ProjectionRevisionId &&
            value.IsAuthoritativeForEconomicRevision)
            ? ReportingAuthority.Proven : ReportingAuthority.Absent;
        var arch7b = snapshot.Arch7b.Any(value => value.AuthorityStatus == ReportingAuthority.Proven)
            ? ReportingAuthority.Proven : ReportingAuthority.Absent;
        var fill = InstitutionalExecutionAuthorityPolicy.FillAuthority(
            snapshot.Arch7b.Sum(value => value.FillCount));
        var ledger = InstitutionalExecutionAuthorityPolicy.LedgerAuthority(
            snapshot.Arch7b.Sum(value => value.PositionLedgerEventCount), fill);
        var position = latest is null
            ? ReportingAuthority.Absent
            : InstitutionalPositionAuthorityPolicy.Evaluate(latest).AuthorityStatus;
        var overall = latest is not null && complete && mappingComplete && lineage
            ? "PROVEN_WITH_EXPLICIT_AUTHORITY_GAPS" : "INCOMPLETE";
        return new(snapshot.AsOfUtc, overall, latest?.ProjectionRevisionId,
            latest?.MarketData.Count ?? 0, latest?.TargetPositions.Count ?? 0,
            latest?.PositionOnlyDrifts.Count ?? 0, counts, complete, mappingComplete,
            lineage, freshness,
            currentness.MarketCalendarStatus, currentness.SlotDueStatus,
            currentness.LatestExpectedClosedSlotId,
            currentness.LatestExpectedClosedSlotEndUtc,
            currentness.LatestPersistedSlotId, currentness.LatestPersistedSlotStatus,
            currentness.LatestQualifyingRevisionSlotId,
            currentness.MetricCurrentnessStatus, currentness.CurrentnessReason,
            currentness.ContractVersion,
            breaks.Count(value => value.Status == OperationalBreakStatus.Active),
            breaks.Count(value => value.Status == OperationalBreakStatus.Unknown),
            arch7a, arch7b, fill, ledger, position, ReportingAuthority.Absent,
            ReportingAuthority.Absent,
            ["AUM/NAV authority is absent.", "Cost authority is absent.",
             "Cross-pair base-quantity drift totals are forbidden.",
             "Unavailable performance and TCA metrics remain NULL."]);
    }

    private static PmsRiskSummary BuildRisk(
        DateTimeOffset asOfUtc,
        IReadOnlyList<PmsShadowIntradayEconomicProjection> revisions,
        IReadOnlyList<TargetExposureRow> exposures,
        IReadOnlyList<TargetConcentrationRow> grossConcentrations,
        IReadOnlyList<TargetConcentrationRow> netConcentrations,
        IReadOnlyList<TargetConcentrationSummaryRow> summaries,
        IReadOnlyList<TargetTurnoverRow> turnover,
        IReadOnlyList<DriftSummaryRow> driftByPair)
    {
        var latest = revisions.LastOrDefault();
        if (latest is null)
            return new(asOfUtc, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null,
                MetricAvailabilityStatus.BlockedMissingSource,
                "Leverage is unavailable without authoritative AUM/NAV.",
                ReportingAuthority.Absent);
        var exposure = exposures.Single(value =>
            value.EconomicRevisionId == latest.ProjectionRevisionId);
        decimal? MaxShare(IEnumerable<TargetConcentrationRow> rows, string type) =>
            rows.Where(value => value.EconomicRevisionId == latest.ProjectionRevisionId &&
                                value.DimensionType == type)
                .Select(value => value.Share).Where(value => value.HasValue)
                .DefaultIfEmpty().Max();
        decimal? Hhi(string type, string family) => summaries.SingleOrDefault(value =>
            value.EconomicRevisionId == latest.ProjectionRevisionId &&
            value.DimensionType == type && value.Family == family)?.Hhi;
        var ratio = summaries.Single(value =>
            value.EconomicRevisionId == latest.ProjectionRevisionId &&
            value.DimensionType == "PORTFOLIO").GrossNetRatio;
        var pairDrift = driftByPair.Where(value =>
                value.EconomicRevisionId == latest.ProjectionRevisionId)
            .ToDictionary(value => value.CanonicalSymbol, value => value.AbsoluteDrift,
                StringComparer.Ordinal);
        return new(asOfUtc, latest.ProjectionRevisionId, exposure.GrossTargetNotionalUsd,
            exposure.NetTargetNotionalUsd, exposure.LongTargetNotionalUsd,
            exposure.ShortTargetNotionalUsd,
            MaxShare(grossConcentrations, "PAIR"), MaxShare(netConcentrations, "PAIR"),
            MaxShare(grossConcentrations, "STRATEGY"),
            MaxShare(netConcentrations, "STRATEGY"),
            Hhi("PAIR", "GROSS"), Hhi("PAIR", "NET"),
            Hhi("STRATEGY", "GROSS"), Hhi("STRATEGY", "NET"), ratio, pairDrift,
            turnover.Where(value => value.EconomicRevisionId == latest.ProjectionRevisionId &&
                                    value.DimensionType == "TOTAL")
                .Select(value => (decimal?)value.TargetTurnoverUsd).SingleOrDefault(),
            MetricAvailabilityStatus.BlockedMissingSource,
            "Leverage is unavailable without authoritative AUM/NAV.",
            ReportingAuthority.Proven);
    }

    private static IReadOnlyList<InstitutionalMetricAvailability> BuildAvailability(
        IReadOnlyList<InstitutionalMetricDefinition> catalog,
        IReadOnlyList<PmsShadowIntradayEconomicProjection> revisions,
        IReadOnlyList<TargetPositionFact> targetFacts,
        IReadOnlyList<PositionOnlyDriftFact> driftFacts,
        IReadOnlyList<TargetCurrencyExposureRow> currencies,
        IReadOnlyList<TargetConcentrationRow> grossConcentrations,
        IReadOnlyList<TargetConcentrationRow> netConcentrations,
        IReadOnlyList<TargetConcentrationSummaryRow> concentrationSummaries,
        IReadOnlyList<TargetTurnoverRow> turnover,
        IReadOnlyList<DriftSummaryRow> driftByPair,
        PmsRiskSummary risk,
        InstitutionalDataQuality quality)
    {
        var hasRevision = revisions.Count > 0;
        var historicalAuthority = quality.Freshness == ReportingAuthority.Stale
            ? ReportingAuthority.Stale : ReportingAuthority.Proven;
        return catalog.Select(definition =>
        {
            if (definition.CurrentAvailability == MetricAvailabilityStatus.BlockedMissingSource)
                return Availability(definition, MetricAvailabilityStatus.BlockedMissingSource,
                    null, definition.RequiredFacts, $"Provide versioned authoritative facts: {string.Join(", ", definition.RequiredFacts)}.",
                    "No numeric value is emitted until all required authorities exist.",
                    ReportingAuthority.Absent, null, null, 0, false);
            if (!hasRevision)
                return Availability(definition, MetricAvailabilityStatus.BlockedMissingSource,
                    null, definition.RequiredFacts, "Provide an authoritative economic revision.",
                    "No authoritative economic revision is available.",
                    ReportingAuthority.Absent, null, null, 0, false);

            return definition.MetricCode switch
            {
                "TARGET_POSITION_NOTIONAL" => Availability(definition,
                    MetricAvailabilityStatus.SourceProven, null, [],
                    "Authoritative TargetPositions are present.",
                    "VALUE_AVAILABLE_IN_FACT_FILE_NOT_SCALAR", historicalAuthority,
                    "target-position-facts.csv", "target-position-facts.csv",
                    targetFacts.Count, false),
                "POSITION_ONLY_DRIFT_SOURCE" when quality.PositionAuthority != ReportingAuthority.Proven =>
                    Availability(definition, MetricAvailabilityStatus.BlockedAuthorityUnproven,
                        null, ["PositionAuthority"], "Prove position authority.",
                        "PositionOnlyDrift rows exist but their position authority is unproven.",
                        ReportingAuthority.Absent, "position-only-drift-facts.csv",
                        "position-only-drift-facts.csv", driftFacts.Count, false),
                "POSITION_ONLY_DRIFT_SOURCE" => Availability(definition,
                    MetricAvailabilityStatus.SourceProven, null, [],
                    "Authoritative PositionOnlyDrifts are present.",
                    "VALUE_AVAILABLE_IN_FACT_FILE_NOT_SCALAR", historicalAuthority,
                    "position-only-drift-facts.csv", "position-only-drift-facts.csv",
                    driftFacts.Count, false),
                "GROSS_TARGET_EXPOSURE" => Scalar(definition, risk.GrossTargetExposureUsd,
                    "pms-risk-summary.json", historicalAuthority, quality),
                "NET_TARGET_EXPOSURE" => Scalar(definition, risk.NetTargetExposureUsd,
                    "pms-risk-summary.json", historicalAuthority, quality),
                "LONG_TARGET_NOTIONAL" => Scalar(definition, risk.LongTargetNotionalUsd,
                    "pms-risk-summary.json", historicalAuthority, quality),
                "SHORT_TARGET_NOTIONAL" => Scalar(definition, risk.ShortTargetNotionalUsd,
                    "pms-risk-summary.json", historicalAuthority, quality),
                "GROSS_NET_RATIO" => Scalar(definition, risk.GrossNetRatio,
                    "target-concentration-summary.csv", historicalAuthority, quality),
                "TARGET_CURRENCY_EXPOSURE" => Multi(definition,
                    "target-exposure-by-currency.csv", currencies.Count, historicalAuthority,
                    quality),
                "PAIR_GROSS_CONCENTRATION" => Multi(definition,
                    "target-concentration-gross.csv",
                    grossConcentrations.Count(value => value.DimensionType == "PAIR"),
                    historicalAuthority, quality),
                "PAIR_NET_CONCENTRATION" => Multi(definition,
                    "target-concentration-net.csv",
                    netConcentrations.Count(value => value.DimensionType == "PAIR"),
                    historicalAuthority, quality),
                "STRATEGY_GROSS_CONCENTRATION" => Multi(definition,
                    "target-concentration-gross.csv",
                    grossConcentrations.Count(value => value.DimensionType == "STRATEGY"),
                    historicalAuthority, quality),
                "STRATEGY_NET_CONCENTRATION" => Multi(definition,
                    "target-concentration-net.csv",
                    netConcentrations.Count(value => value.DimensionType == "STRATEGY"),
                    historicalAuthority, quality),
                "TOP_N_CONCENTRATION" or "TARGET_HHI" => Multi(definition,
                    "target-concentration-summary.csv", concentrationSummaries.Count,
                    historicalAuthority, quality),
                "TARGET_TURNOVER" when revisions.Count < 2 => Availability(definition,
                    MetricAvailabilityStatus.BlockedMissingSource, null,
                    ["TwoSuccessiveAuthoritativeSlots"],
                    "Provide two successive authoritative slots.",
                    "Two revisions of the same slot never form a turnover period.",
                    ReportingAuthority.Absent, "target-turnover.csv",
                    "target-turnover.csv", 0, false),
                "TARGET_TURNOVER" => Multi(definition, "target-turnover.csv",
                    turnover.Count, historicalAuthority, quality),
                "ABSOLUTE_POSITION_ONLY_DRIFT" when quality.PositionAuthority != ReportingAuthority.Proven =>
                    Availability(definition, MetricAvailabilityStatus.BlockedAuthorityUnproven,
                        null, ["PositionAuthority"], "Prove position authority.",
                        "USD cross-pair drift remains blocked without qualified marks.",
                        ReportingAuthority.Absent, "drift-by-pair.csv",
                        "drift-by-pair.csv", driftByPair.Count, false),
                "ABSOLUTE_POSITION_ONLY_DRIFT" => Multi(definition, "drift-by-pair.csv",
                    driftByPair.Count, historicalAuthority, quality),
                _ => Availability(definition, MetricAvailabilityStatus.BlockedMissingSource,
                    null, definition.RequiredFacts, "Provide required authoritative facts.",
                    "Metric has no dynamic projection.", ReportingAuthority.Absent,
                    null, null, 0, false)
            };
        }).OrderBy(value => value.MetricCode, StringComparer.Ordinal).ToArray();
    }

    private static InstitutionalMetricAvailability Scalar(
        InstitutionalMetricDefinition definition,
        decimal? value,
        string location,
        string authority,
        InstitutionalDataQuality quality) =>
        Availability(definition,
            value.HasValue ? MetricAvailabilityStatus.DerivableProven :
                MetricAvailabilityStatus.BlockedMissingSource,
            value, value.HasValue ? [] : definition.RequiredFacts,
            value.HasValue ? "Required authoritative facts and formula are present." :
                "Provide a non-zero formula denominator.",
            value.HasValue ? "Target-only metric; not an executed or accounting metric." :
                "Value is NULL because the formula is undefined.",
            value.HasValue ? authority : ReportingAuthority.Absent,
            location, null, value.HasValue ? 1 : 0, true, quality);

    private static InstitutionalMetricAvailability Multi(
        InstitutionalMetricDefinition definition,
        string file,
        int rows,
        string authority,
        InstitutionalDataQuality quality) =>
        Availability(definition,
            rows > 0 ? MetricAvailabilityStatus.DerivableProven :
                MetricAvailabilityStatus.BlockedMissingSource,
            null, rows > 0 ? [] : definition.RequiredFacts,
            rows > 0 ? "Required authoritative facts and formula are present." :
                "Provide required authoritative facts.",
            rows > 0 ? "VALUE_AVAILABLE_IN_FACT_FILE_NOT_SCALAR" :
                "No derived rows are available.",
            rows > 0 ? authority : ReportingAuthority.Absent,
            file, file, rows, false, quality);

    private static InstitutionalMetricAvailability Availability(
        InstitutionalMetricDefinition definition,
        string status,
        decimal? value,
        IReadOnlyList<string> missing,
        string activation,
        string caveat,
        string authority,
        string? location,
        string? factFile,
        int factRows,
        bool scalar,
        InstitutionalDataQuality? quality = null) =>
        new(definition.MetricCode, status, value, definition.Unit,
            definition.Unit == "USD" ? "USD" : null, missing, activation, caveat,
            authority, quality?.OverallStatus ?? "INCOMPLETE", location, factFile,
            factRows, scalar, definition.Grain);

    private static InstitutionalSourceSnapshot BuildSourceSnapshot(
        OperationalReportingSnapshot snapshot,
        string roadmapSha,
        IReadOnlyList<PmsShadowIntradayEconomicProjection> revisions,
        IReadOnlyDictionary<(Guid, Guid), PmsShadowSecurityMappingRow> mappings,
        IReadOnlyList<OperationalBreak> breaks,
        InstitutionalDataQuality quality)
    {
        var includedIngestions = revisions.Select(value => value.SourceIngestionId).ToHashSet();
        var includedMappings = mappings.Values.Where(value =>
            includedIngestions.Contains(value.IngestionId)).ToArray();
        var sourceRevisions = revisions.Select(value => new InstitutionalSourceRevision(
                value.ProjectionRevisionId, value.SlotId, value.RevisionNumber,
                value.SlotEndUtc, value.CompletedAtUtc, value.SourceIngestionId,
                value.SourceSessionId, value.MarketDataSnapshotSha256, value.ManifestSha256,
                value.TargetPositionsSha256, value.DriftsSha256,
                value.TargetPositions.Min(target => target.CalculatedAtUtc),
                value.TargetPositions.Max(target => target.CalculatedAtUtc),
                value.PositionOnlyDrifts.Min(drift => drift.AsOfUtc),
                value.PositionOnlyDrifts.Max(drift => drift.AsOfUtc),
                value.AccountSnapshotId, value.PositionSnapshotId,
                value.PositionSnapshotAsOfUtc, value.PositionAuthority,
                value.SelectedModelRuns.Select(model => model.ModelRunId)
                    .Order().ToArray(),
                value.SelectedModelRuns.Select(model => model.OutputSha256)
                    .Order(StringComparer.Ordinal).ToArray(),
                value.SelectedModelRuns.Select(model => model.CoreCommitId)
                    .Order(StringComparer.Ordinal).ToArray()))
            .ToArray();
        var currentness = new InstitutionalMetricCurrentness(
            quality.MarketCalendarStatus, quality.SlotDueStatus,
            quality.LatestExpectedClosedSlotId, quality.LatestExpectedClosedSlotEndUtc,
            quality.LatestPersistedSlotId, quality.LatestPersistedSlotStatus,
            quality.LatestQualifyingRevisionSlotId,
            quality.MetricCurrentnessStatus, quality.CurrentnessReason,
            quality.CurrentnessContractVersion);
        var breakFacts = breaks.Select(value => new InstitutionalSourceBreakFact(
                value.BreakId, value.ExactCode, value.SourceExactCode,
                value.Status.ToString().ToUpperInvariant(),
                value.Severity.ToString().ToUpperInvariant(),
                value.AuthorityStatus, value.Component, value.ScopeType, value.ScopeId,
                value.SlotId, value.EconomicRevisionId, value.TradeIntentId,
                value.QualificationRunId, value.FirstObservedAtUtc,
                value.LastObservedAtUtc, value.EvidenceSha256,
                value.BlocksTrading, value.BlocksAccounting))
            .OrderBy(value => value.BreakId, StringComparer.Ordinal)
            .ThenBy(value => value.ScopeType, StringComparer.Ordinal)
            .ThenBy(value => value.ScopeId, StringComparer.Ordinal)
            .ToArray();
        return new(InstitutionalMetricContract.SourceSnapshotVersion,
            snapshot.RepositoryCommit, roadmapSha, snapshot.Database.TargetProfileId,
            snapshot.Database.TargetFingerprint, snapshot.Database, snapshot.AsOfUtc,
            sourceRevisions, MappingSetSha(includedMappings), currentness, breakFacts,
            InstitutionalPositionAuthorityPolicy.ContractVersion,
            InstitutionalMetricContract.EconomicTimelineContractVersion,
            InstitutionalRoadmapAuthority.ContractVersion,
            OperationalReportingContract.Version, null);
    }

    private static string MappingSetSha(IEnumerable<PmsShadowSecurityMappingRow> mappings) =>
        Evidence("RPT2_MAPPING_SET_V1", string.Join('\n', mappings
            .OrderBy(value => value.IngestionId)
            .ThenBy(value => value.InstrumentId)
            .Select(value => string.Join('|', value.IngestionId, value.InstrumentId,
                value.SecurityId, NormalizeSymbol(value.Symbol), value.LmaxInstrumentId,
                value.MappingSha256))));

    private static IReadOnlyList<PowerBiCsvContract> PowerBiContracts() =>
    [
        PowerBi("metric-availability.csv", "metric", ["MetricCode"],
            ["metric", "availability", "authority"], "NON_ADDITIVE", null, null),
        PowerBi("target-position-facts.csv", "economic_revision/target_position",
            ["EconomicRevisionId", "TargetPositionId"],
            ["economic_revision", "strategy", "model_run", "instrument"],
            "SOURCE_FACT", "USD", "USD"),
        PowerBi("position-only-drift-facts.csv", "economic_revision/position_only_drift",
            ["EconomicRevisionId", "PositionOnlyDriftId"],
            ["economic_revision", "strategy", "model_run", "pair"],
            "SOURCE_FACT_NON_ADDITIVE_ACROSS_PAIRS", "BASE_CURRENCY", null),
        PowerBi("target-exposure-by-revision.csv", "economic_revision",
            ["EconomicRevisionId"], ["economic_revision", "slot"],
            "ADDITIVE_USD_COMPONENTS", "USD", "USD"),
        PowerBi("target-exposure-by-strategy.csv", "economic_revision/strategy",
            ["EconomicRevisionId", "StrategyId"], ["economic_revision", "strategy"],
            "ADDITIVE_WITHIN_REVISION", "USD", "USD"),
        PowerBi("target-exposure-by-model.csv", "economic_revision/model_run",
            ["EconomicRevisionId", "ModelRunId"],
            ["economic_revision", "strategy", "model_run"],
            "ADDITIVE_WITHIN_REVISION", "USD", "USD"),
        PowerBi("target-exposure-by-pair.csv", "economic_revision/canonical_pair",
            ["EconomicRevisionId", "CanonicalSymbol"],
            ["economic_revision", "instrument", "pair"],
            "ADDITIVE_WITHIN_REVISION", "USD", "USD"),
        PowerBi("target-exposure-by-currency.csv", "economic_revision/currency",
            ["EconomicRevisionId", "Currency"], ["economic_revision", "currency"],
            "ADDITIVE_WITHIN_REVISION", "USD", "USD"),
        PowerBi("target-concentration-gross.csv",
            "economic_revision/dimension_type/dimension",
            ["EconomicRevisionId", "DimensionType", "DimensionId"],
            ["economic_revision", "strategy", "pair"], "NON_ADDITIVE", "RATIO", null),
        PowerBi("target-concentration-net.csv",
            "economic_revision/dimension_type/dimension",
            ["EconomicRevisionId", "DimensionType", "DimensionId"],
            ["economic_revision", "strategy", "pair"], "NON_ADDITIVE", "RATIO", null),
        PowerBi("target-concentration-summary.csv",
            "economic_revision/dimension_type/family",
            ["EconomicRevisionId", "DimensionType", "Family"],
            ["economic_revision", "strategy", "pair"], "NON_ADDITIVE", "RATIO", null),
        PowerBi("target-turnover.csv", "previous_slot/current_slot/dimension",
            ["PreviousEconomicRevisionId", "EconomicRevisionId", "DimensionType", "DimensionId"],
            ["economic_revision", "strategy", "pair"],
            "ADDITIVE_ONLY_FOR_DISJOINT_DIMENSIONS", "USD", "USD"),
        PowerBi("drift-by-strategy-pair.csv", "economic_revision/strategy/canonical_pair",
            ["EconomicRevisionId", "DimensionId"],
            ["economic_revision", "strategy", "pair"],
            "ADDITIVE_ONLY_WITHIN_SAME_PAIR", "BASE_CURRENCY", null),
        PowerBi("drift-by-model-pair.csv", "economic_revision/model_run/canonical_pair",
            ["EconomicRevisionId", "DimensionId"],
            ["economic_revision", "model_run", "pair"],
            "ADDITIVE_ONLY_WITHIN_SAME_PAIR", "BASE_CURRENCY", null),
        PowerBi("drift-by-pair.csv", "economic_revision/canonical_pair",
            ["EconomicRevisionId", "DimensionId"], ["economic_revision", "pair"],
            "ADDITIVE_ONLY_WITHIN_SAME_PAIR", "BASE_CURRENCY", null),
        PowerBi("active-breaks.csv", "break", ["BreakId"],
            ["break", "severity", "status"], "NON_ADDITIVE", null, null)
    ];

    private static PowerBiCsvContract PowerBi(string file, string grain, string[] key,
        string[] dimensions, string additive, string? unit, string? currency) =>
        new(file, grain, key, dimensions, ["EconomicRevisionId -> economic revision dimension"],
            additive, unit, currency, "Explicit NULL literal; missing is never zero.",
            "Rows are valid at the injected AsOfUtc and are not additive across as-of.");

    private static string Evidence(params object?[] values) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n',
            values.Select(Canonical)))));

    private static string Canonical(object? value) => value switch
    {
        null => InstitutionalMetricContract.NullCsvValue,
        DateTimeOffset date => date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        decimal number => number.ToString("0.############################",
            CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? InstitutionalMetricContract.NullCsvValue
    };

    private static bool SameSign(decimal left, decimal right) =>
        left != 0m && right != 0m && Math.Sign(left) == Math.Sign(right);

    private static string NormalizeSymbol(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
