using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class InstitutionalMetricCatalog
{
    public static IReadOnlyList<InstitutionalMetricDefinition> Build()
    {
        var result = new List<InstitutionalMetricDefinition>
        {
            Source("TARGET_NOTIONAL", "PORTFOLIO", "Signed target notional from TargetPositions.", "economic_revision/target", "USD", InstitutionalMetricContract.ExposureFormula, ["TargetPositions"]),
            Source("GROSS_TARGET_EXPOSURE", "RISK", "Sum of absolute target notionals.", "economic_revision", "USD", InstitutionalMetricContract.ExposureFormula, ["TargetPositions"]),
            Source("NET_TARGET_EXPOSURE", "RISK", "Sum of signed target notionals.", "economic_revision", "USD", InstitutionalMetricContract.ExposureFormula, ["TargetPositions"]),
            Source("LONG_TARGET_NOTIONAL", "RISK", "Sum of positive target notionals.", "economic_revision", "USD", InstitutionalMetricContract.ExposureFormula, ["TargetPositions"]),
            Source("SHORT_TARGET_NOTIONAL", "RISK", "Absolute sum of negative target notionals.", "economic_revision", "USD", InstitutionalMetricContract.ExposureFormula, ["TargetPositions"]),
            Source("TARGET_CURRENCY_EXPOSURE", "RISK", "Canonical base and quote leg target exposure.", "economic_revision/currency", "USD", InstitutionalMetricContract.CurrencyFormula, ["TargetPositions", "SecurityMappings"]),
            Derivable("PAIR_CONCENTRATION", "RISK", "Absolute pair target notional divided by gross target exposure.", "economic_revision/pair", "RATIO", InstitutionalMetricContract.ConcentrationFormula, ["TargetPositions"]),
            Derivable("STRATEGY_CONCENTRATION", "RISK", "Absolute strategy target notional divided by gross target exposure.", "economic_revision/strategy", "RATIO", InstitutionalMetricContract.ConcentrationFormula, ["TargetPositions"]),
            Derivable("TOP_N_CONCENTRATION", "RISK", "Sum of the N largest absolute concentration shares.", "economic_revision/dimension_type/N", "RATIO", InstitutionalMetricContract.ConcentrationFormula, ["TargetPositions"]),
            Derivable("TARGET_HHI", "RISK", "Sum of squared concentration shares when gross is non-zero.", "economic_revision/dimension_type", "RATIO", InstitutionalMetricContract.ConcentrationFormula, ["TargetPositions"]),
            Derivable("GROSS_NET_RATIO", "RISK", "Gross target exposure divided by absolute net target exposure when net is non-zero.", "economic_revision", "RATIO", InstitutionalMetricContract.ConcentrationFormula, ["TargetPositions"]),
            Source("POSITION_ONLY_DRIFT", "PORTFOLIO", "Signed source PositionOnlyDrift delta.", "economic_revision/drift", "BASE_QUANTITY", InstitutionalMetricContract.DriftFormula, ["PositionOnlyDrifts"]),
            Derivable("ABSOLUTE_POSITION_ONLY_DRIFT", "RISK", "Sum of absolute source PositionOnlyDrift deltas.", "economic_revision/dimension", "BASE_QUANTITY", InstitutionalMetricContract.DriftFormula, ["PositionOnlyDrifts"]),
            Derivable("TARGET_TURNOVER", "PORTFOLIO", "Gross target change between successive qualifying revisions.", "previous_revision/revision/dimension", "USD", InstitutionalMetricContract.TurnoverFormula, ["TargetPositions", "EconomicRevisions"])
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
            ["sum_across_as_of", "sum_ratio", "sum_concentration"],
            "RPT2", category is "TCA" ? "TCA" : "PMS/portfolio and risk");
}

public static class InstitutionalMetricProjector
{
    public static InstitutionalMetricReportSet Build(
        OperationalReportingSnapshot snapshot,
        string roadmapSha256)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Require(IsSha(roadmapSha256), "RPT2_ROADMAP_SHA_INVALID");
        var revisions = snapshot.EconomicProjectionSources
            .Where(value => value.Qualifying && value.NoOrder)
            .OrderBy(value => value.CompletedAtUtc)
            .ThenBy(value => value.ProjectionRevisionId)
            .ToArray();
        var mappings = snapshot.SecurityMappingSources.ToDictionary(
            value => (value.IngestionId, value.InstrumentId));
        var sources = revisions.SelectMany(revision => revision.TargetPositions.Select(target =>
        {
            Require(mappings.TryGetValue((revision.SourceIngestionId, target.InstrumentId),
                out var mapping), "RPT2_SECURITY_MAPPING_MISSING");
            var symbol = NormalizeSymbol(mapping!.Symbol);
            Require(symbol.Length == 6, "RPT2_CANONICAL_SYMBOL_INVALID");
            return new InstitutionalTargetSource(revision, target, mapping!, symbol);
        })).ToArray();

        var byRevision = BuildExposure(sources, "REVISION", value => "ALL");
        var byStrategy = BuildExposure(sources, "STRATEGY", value => value.Target.StrategyId);
        var byModel = BuildExposure(sources, "MODEL", value => value.Target.ModelRunId.ToString("D"));
        var byPair = BuildExposure(sources, "PAIR", value => value.CanonicalSymbol);
        var byCurrency = BuildCurrency(sources);
        var concentrations = BuildConcentrations(byRevision, byStrategy, byPair);
        var turnover = BuildTurnover(revisions, mappings);
        var driftByStrategy = BuildDrift(revisions, "STRATEGY", value => value.Drift.StrategyId);
        var driftByModel = BuildDrift(revisions, "MODEL", value => value.Drift.ModelRunId.ToString("D"));
        var driftByPair = BuildDrift(revisions, "PAIR", value =>
            NormalizeSymbol(mappings[(value.Revision.SourceIngestionId, value.Drift.InstrumentId)].Symbol));
        var operational = OperationalReportProjector.Build(snapshot);
        var activeBreaks = operational.Breaks.Where(value =>
            value.Status is OperationalBreakStatus.Active or OperationalBreakStatus.Unknown)
            .OrderBy(value => value.BreakId, StringComparer.Ordinal).ToArray();
        var quality = BuildQuality(snapshot, revisions, mappings, activeBreaks);
        var risk = BuildRisk(snapshot.AsOfUtc, revisions, byRevision, concentrations, turnover,
            driftByStrategy);
        var catalog = InstitutionalMetricCatalog.Build();
        var availability = BuildAvailability(catalog, revisions, quality);
        return new(snapshot.AsOfUtc, snapshot.RepositoryCommit, snapshot.Database, roadmapSha256,
            catalog, availability, byRevision, byStrategy, byModel, byPair, byCurrency,
            concentrations, turnover, driftByStrategy, driftByModel, driftByPair, risk, quality,
            activeBreaks, PowerBiContracts());
    }

    private static IReadOnlyList<TargetExposureRow> BuildExposure(
        IEnumerable<InstitutionalTargetSource> sources,
        string dimensionType,
        Func<InstitutionalTargetSource, string> key)
    {
        return sources.GroupBy(value => new
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
                    first.Revision.ProjectionRevisionId.ToString("D"), dimensionType,
                    group.Key.DimensionId, gross, net,
                    string.Join(',', ordered.Select(value => value.Target.TargetPositionId)));
                return new TargetExposureRow(
                    first.Revision.ProjectionRevisionId,
                    first.Revision.RevisionNumber,
                    first.Revision.SlotId,
                    first.Revision.CompletedAtUtc,
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
                    0m,
                    InstitutionalMetricContract.ExposureFormula,
                    ReportingAuthority.Proven,
                    evidence);
            })
            .GroupBy(value => value.EconomicRevisionId)
            .SelectMany(group =>
            {
                var totalGross = group.FirstOrDefault(value => value.DimensionType == "REVISION")
                    ?.GrossTargetNotionalUsd ?? sources.Where(value =>
                        value.Revision.ProjectionRevisionId == group.Key)
                        .Sum(value => Math.Abs(value.Target.TargetNotionalUsd));
                return group.Select(value => value with
                {
                    AbsoluteWeight = totalGross == 0m ? 0m :
                        Math.Abs(value.NetTargetNotionalUsd) / totalGross
                });
            })
            .OrderBy(value => value.AsOfUtc)
            .ThenBy(value => value.DimensionId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<TargetCurrencyExposureRow> BuildCurrency(
        IReadOnlyList<InstitutionalTargetSource> sources)
    {
        return sources.SelectMany(value => new[]
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
                var first = group.First().Source.Revision;
                var signed = group.Sum(value => value.Amount);
                return new TargetCurrencyExposureRow(first.ProjectionRevisionId,
                    first.RevisionNumber, first.SlotId, first.CompletedAtUtc,
                    group.Key.Currency, signed, group.Sum(value => Math.Abs(value.Amount)),
                    group.Count(), InstitutionalMetricContract.CurrencyFormula,
                    ReportingAuthority.Proven,
                    Evidence(first.ProjectionRevisionId.ToString("D"), group.Key.Currency,
                        signed, group.Count()));
            })
            .OrderBy(value => value.AsOfUtc)
            .ThenBy(value => value.Currency, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<TargetConcentrationRow> BuildConcentrations(
        IReadOnlyList<TargetExposureRow> revisions,
        IReadOnlyList<TargetExposureRow> strategies,
        IReadOnlyList<TargetExposureRow> pairs)
    {
        var result = new List<TargetConcentrationRow>();
        foreach (var revision in revisions)
        {
            AddConcentrations(result, revision, "PAIR",
                pairs.Where(value => value.EconomicRevisionId == revision.EconomicRevisionId));
            AddConcentrations(result, revision, "STRATEGY",
                strategies.Where(value => value.EconomicRevisionId == revision.EconomicRevisionId));
        }
        return result.OrderBy(value => value.EconomicRevisionId)
            .ThenBy(value => value.DimensionType, StringComparer.Ordinal)
            .ThenBy(value => value.Rank).ToArray();
    }

    private static void AddConcentrations(
        ICollection<TargetConcentrationRow> result,
        TargetExposureRow revision,
        string type,
        IEnumerable<TargetExposureRow> rows)
    {
        var ordered = rows.OrderByDescending(value => Math.Abs(value.NetTargetNotionalUsd))
            .ThenBy(value => value.DimensionId, StringComparer.Ordinal).ToArray();
        var gross = revision.GrossTargetNotionalUsd;
        var concentrations = ordered.Select(value =>
            gross == 0m ? (decimal?)null : Math.Abs(value.NetTargetNotionalUsd) / gross).ToArray();
        var topN = concentrations.Take(3).Where(value => value.HasValue).Sum(value => value!.Value);
        var hhi = concentrations.All(value => value.HasValue)
            ? concentrations.Sum(value => value!.Value * value.Value) : (decimal?)null;
        decimal? ratio = revision.NetTargetNotionalUsd == 0m ? null :
            gross / Math.Abs(revision.NetTargetNotionalUsd);
        for (var index = 0; index < ordered.Length; index++)
            result.Add(new(revision.EconomicRevisionId, type, ordered[index].DimensionId,
                concentrations[index], index + 1, topN, hhi, ratio,
                InstitutionalMetricContract.ConcentrationFormula,
                gross == 0m ? "UNDEFINED_ZERO_GROSS" : "PROVEN",
                ratio.HasValue ? string.Empty : "Gross/net ratio is NULL because net is zero."));
    }

    private static IReadOnlyList<TargetTurnoverRow> BuildTurnover(
        IReadOnlyList<QQ.Production.Intraday.Infrastructure.PostgreSql.PmsShadowIntradayEconomicProjection> revisions,
        IReadOnlyDictionary<(Guid, Guid), QQ.Production.Intraday.Infrastructure.PostgreSql.PmsShadowSecurityMappingRow> mappings)
    {
        var result = new List<TargetTurnoverRow>();
        for (var index = 1; index < revisions.Count; index++)
        {
            var previous = revisions[index - 1];
            var current = revisions[index];
            AddTurnover(result, previous, current, mappings, "TOTAL", _ => "ALL");
            AddTurnover(result, previous, current, mappings, "STRATEGY",
                value => value.StrategyId);
            AddTurnover(result, previous, current, mappings, "PAIR",
                value => NormalizeSymbol(mappings[(current.SourceIngestionId, value.InstrumentId)].Symbol));
        }
        return result.OrderBy(value => value.PeriodEndUtc)
            .ThenBy(value => value.DimensionType, StringComparer.Ordinal)
            .ThenBy(value => value.DimensionId, StringComparer.Ordinal).ToArray();
    }

    private static void AddTurnover(
        ICollection<TargetTurnoverRow> result,
        QQ.Production.Intraday.Infrastructure.PostgreSql.PmsShadowIntradayEconomicProjection previous,
        QQ.Production.Intraday.Infrastructure.PostgreSql.PmsShadowIntradayEconomicProjection current,
        IReadOnlyDictionary<(Guid, Guid), QQ.Production.Intraday.Infrastructure.PostgreSql.PmsShadowSecurityMappingRow> mappings,
        string dimensionType,
        Func<QQ.Production.Intraday.Infrastructure.PostgreSql.PmsShadowSlotTargetPosition, string> dimension)
    {
        var previousByDimension = previous.TargetPositions.GroupBy(dimension)
            .ToDictionary(group => group.Key,
                group => group.GroupBy(value => (value.StrategyId, value.InstrumentId))
                    .ToDictionary(items => items.Key, items => items.Sum(value => value.TargetNotionalUsd)),
                StringComparer.Ordinal);
        var currentByDimension = current.TargetPositions.GroupBy(dimension)
            .ToDictionary(group => group.Key,
                group => group.GroupBy(value => (value.StrategyId, value.InstrumentId))
                    .ToDictionary(items => items.Key, items => items.Sum(value => value.TargetNotionalUsd)),
                StringComparer.Ordinal);
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
            result.Add(new(previous.ProjectionRevisionId, current.ProjectionRevisionId,
                previous.CompletedAtUtc, current.CompletedAtUtc, dimensionType, id,
                changes.Sum(value => value.Delta),
                changes.Count(value => value.Old == 0m && value.New != 0m),
                changes.Count(value => value.Old != 0m && value.New == 0m),
                changes.Count(value => SameSign(value.Old, value.New) &&
                                       Math.Abs(value.New) > Math.Abs(value.Old)),
                changes.Count(value => SameSign(value.Old, value.New) &&
                                       value.New != 0m && Math.Abs(value.New) < Math.Abs(value.Old)),
                changes.Count(value => value.Old * value.New < 0m),
                "TARGET_TURNOVER", InstitutionalMetricContract.TurnoverFormula,
                MetricAvailabilityStatus.DerivableProven,
                Evidence(previous.ProjectionRevisionId.ToString("D"),
                    current.ProjectionRevisionId.ToString("D"), dimensionType, id,
                    changes.Sum(value => value.Delta))));
        }
    }

    private static IReadOnlyList<DriftSummaryRow> BuildDrift(
        IReadOnlyList<QQ.Production.Intraday.Infrastructure.PostgreSql.PmsShadowIntradayEconomicProjection> revisions,
        string dimensionType,
        Func<(QQ.Production.Intraday.Infrastructure.PostgreSql.PmsShadowIntradayEconomicProjection Revision,
            QQ.Production.Intraday.Infrastructure.PostgreSql.PmsShadowSlotPositionOnlyDrift Drift), string> dimension)
    {
        return revisions.SelectMany(revision => revision.PositionOnlyDrifts.Select(drift =>
                (Revision: revision, Drift: drift)))
            .GroupBy(value => new
            {
                value.Revision.ProjectionRevisionId,
                DimensionId = dimension(value)
            })
            .Select(group =>
            {
                var first = group.First();
                var signed = group.Sum(value => value.Drift.Delta);
                var absolute = group.Sum(value => Math.Abs(value.Drift.Delta));
                var positionAuthority = string.IsNullOrWhiteSpace(first.Revision.PositionAuthority)
                    ? ReportingAuthority.Absent : ReportingAuthority.Proven;
                return new DriftSummaryRow(first.Revision.ProjectionRevisionId,
                    dimensionType, group.Key.DimensionId, signed, absolute, group.Count(),
                    positionAuthority,
                    positionAuthority == ReportingAuthority.Proven
                        ? MetricAvailabilityStatus.SourceProven
                        : MetricAvailabilityStatus.BlockedAuthorityUnproven,
                    InstitutionalMetricContract.DriftFormula,
                    Evidence(first.Revision.ProjectionRevisionId.ToString("D"),
                        dimensionType, group.Key.DimensionId, signed, absolute));
            })
            .OrderBy(value => value.EconomicRevisionId)
            .ThenBy(value => value.DimensionId, StringComparer.Ordinal).ToArray();
    }

    private static InstitutionalDataQuality BuildQuality(
        OperationalReportingSnapshot snapshot,
        IReadOnlyList<QQ.Production.Intraday.Infrastructure.PostgreSql.PmsShadowIntradayEconomicProjection> revisions,
        IReadOnlyDictionary<(Guid, Guid), QQ.Production.Intraday.Infrastructure.PostgreSql.PmsShadowSecurityMappingRow> mappings,
        IReadOnlyList<OperationalBreak> breaks)
    {
        var latest = revisions.LastOrDefault();
        var counts = latest?.TargetPositions.GroupBy(value => value.StrategyId)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)
            ?? new Dictionary<string, int>(StringComparer.Ordinal);
        var complete = OperationalReportingContract.ExpectedPerModelCounts.All(pair =>
            counts.GetValueOrDefault(pair.Key) == pair.Value);
        var mappingComplete = latest is not null && latest.TargetPositions.All(value =>
            mappings.ContainsKey((latest.SourceIngestionId, value.InstrumentId)));
        var lineage = latest is not null && IsSha(latest.ManifestSha256) &&
                      IsSha(latest.TargetPositionsSha256) && IsSha(latest.DriftsSha256) &&
                      latest.SelectedModelRuns.All(value =>
                          IsSha(value.OutputSha256) && IsGitCommit(value.CoreCommitId));
        var freshness = latest is null ? ReportingAuthority.Absent :
            snapshot.AsOfUtc - latest.CompletedAtUtc >
            TimeSpan.FromMinutes(QQ.Production.Intraday.Infrastructure.PostgreSql
                .PmsShadowIntradayCadenceContract.StaleMinutes)
                ? ReportingAuthority.Stale : ReportingAuthority.Proven;
        var arch7a = latest is not null && snapshot.Arch7a.Any(value =>
            value.EconomicRevisionId == latest.ProjectionRevisionId &&
            value.IsAuthoritativeForEconomicRevision)
            ? ReportingAuthority.Proven : ReportingAuthority.Absent;
        var arch7b = snapshot.Arch7b.Any(value => value.AuthorityStatus == ReportingAuthority.Proven)
            ? ReportingAuthority.Proven : ReportingAuthority.Absent;
        var fill = snapshot.Arch7b.Sum(value => value.FillCount) > 0
            ? ReportingAuthority.Proven : ReportingAuthority.Absent;
        var ledger = snapshot.Arch7b.Sum(value => value.PositionLedgerEventCount) > 0
            ? ReportingAuthority.Proven : ReportingAuthority.Absent;
        var position = latest is null || string.IsNullOrWhiteSpace(latest.PositionAuthority)
            ? ReportingAuthority.Absent : ReportingAuthority.Proven;
        var overall = latest is not null && complete && mappingComplete && lineage
            ? "PROVEN_WITH_EXPLICIT_AUTHORITY_GAPS" : "INCOMPLETE";
        return new(snapshot.AsOfUtc, overall, latest?.ProjectionRevisionId,
            latest?.MarketData.Count ?? 0, latest?.TargetPositions.Count ?? 0,
            latest?.PositionOnlyDrifts.Count ?? 0, counts, complete, mappingComplete,
            lineage, freshness,
            breaks.Count(value => value.Status == OperationalBreakStatus.Active),
            breaks.Count(value => value.Status == OperationalBreakStatus.Unknown),
            arch7a, arch7b, fill, ledger, position, ReportingAuthority.Absent,
            ReportingAuthority.Absent,
            ["AUM/NAV authority is absent.", "Cost authority is absent.",
             "Unavailable performance and TCA metrics remain NULL."]);
    }

    private static PmsRiskSummary BuildRisk(
        DateTimeOffset asOfUtc,
        IReadOnlyList<QQ.Production.Intraday.Infrastructure.PostgreSql.PmsShadowIntradayEconomicProjection> revisions,
        IReadOnlyList<TargetExposureRow> exposures,
        IReadOnlyList<TargetConcentrationRow> concentrations,
        IReadOnlyList<TargetTurnoverRow> turnover,
        IReadOnlyList<DriftSummaryRow> drift)
    {
        var latest = revisions.LastOrDefault();
        if (latest is null)
            return new(asOfUtc, null, null, null, null, null, null, null, null, null,
                null, null, null, MetricAvailabilityStatus.BlockedMissingSource,
                "Leverage is unavailable without authoritative AUM/NAV.",
                ReportingAuthority.Absent);
        var exposure = exposures.Single(value =>
            value.EconomicRevisionId == latest.ProjectionRevisionId);
        var pair = concentrations.Where(value =>
            value.EconomicRevisionId == latest.ProjectionRevisionId &&
            value.DimensionType == "PAIR").ToArray();
        var strategy = concentrations.Where(value =>
            value.EconomicRevisionId == latest.ProjectionRevisionId &&
            value.DimensionType == "STRATEGY").ToArray();
        return new(asOfUtc, latest.ProjectionRevisionId, exposure.GrossTargetNotionalUsd,
            exposure.NetTargetNotionalUsd, exposure.LongTargetNotionalUsd,
            exposure.ShortTargetNotionalUsd, pair.Max(value => value.Concentration),
            strategy.Max(value => value.Concentration), pair.FirstOrDefault()?.Hhi,
            strategy.FirstOrDefault()?.Hhi, pair.FirstOrDefault()?.GrossNetRatio,
            drift.Where(value => value.EconomicRevisionId == latest.ProjectionRevisionId)
                .Sum(value => value.AbsoluteDrift),
            turnover.Where(value => value.EconomicRevisionId == latest.ProjectionRevisionId &&
                                    value.DimensionType == "TOTAL")
                .Select(value => (decimal?)value.TargetTurnoverUsd).SingleOrDefault(),
            MetricAvailabilityStatus.BlockedMissingSource,
            "Leverage is unavailable without authoritative AUM/NAV.",
            ReportingAuthority.Proven);
    }

    private static IReadOnlyList<InstitutionalMetricAvailability> BuildAvailability(
        IReadOnlyList<InstitutionalMetricDefinition> catalog,
        IReadOnlyList<QQ.Production.Intraday.Infrastructure.PostgreSql.PmsShadowIntradayEconomicProjection> revisions,
        InstitutionalDataQuality quality)
    {
        return catalog.Select(definition =>
        {
            var status = definition.CurrentAvailability;
            var missing = Array.Empty<string>();
            var activation = "Current qualifying PMS economic revision.";
            var caveat = "Target-only metric; it is not an executed or fund-accounting metric.";
            var authority = ReportingAuthority.Proven;
            if (definition.MetricCode == "TARGET_TURNOVER" && revisions.Count < 2)
            {
                status = MetricAvailabilityStatus.BlockedMissingSource;
                missing = ["TwoSuccessiveQualifyingEconomicRevisions"];
                activation = "At least two successive qualifying economic revisions.";
                caveat = "TARGET_TURNOVER is not executed turnover.";
                authority = ReportingAuthority.Absent;
            }
            else if (status == MetricAvailabilityStatus.BlockedMissingSource)
            {
                missing = definition.RequiredFacts.ToArray();
                activation = $"Provide versioned authoritative facts: {string.Join(", ", missing)}.";
                caveat = "No numeric value is emitted until all required authorities exist.";
                authority = ReportingAuthority.Absent;
            }
            return new InstitutionalMetricAvailability(definition.MetricCode, status, null,
                definition.Unit, definition.Unit == "USD" ? "USD" : null, missing,
                activation, caveat, authority, quality.OverallStatus);
        }).OrderBy(value => value.MetricCode, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<PowerBiCsvContract> PowerBiContracts() =>
    [
        PowerBi("metric-availability.csv", "metric", ["MetricCode"],
            ["metric", "availability", "authority"], "NON_ADDITIVE", null, null),
        PowerBi("target-exposure-by-revision.csv", "economic_revision",
            ["EconomicRevisionId"], ["economic_revision", "slot"], "ADDITIVE_USD_COMPONENTS", "USD", "USD"),
        PowerBi("target-exposure-by-strategy.csv", "economic_revision/strategy",
            ["EconomicRevisionId", "StrategyId"], ["economic_revision", "strategy"], "ADDITIVE_WITHIN_REVISION", "USD", "USD"),
        PowerBi("target-exposure-by-model.csv", "economic_revision/model_run",
            ["EconomicRevisionId", "ModelRunId"], ["economic_revision", "strategy", "model_run"], "ADDITIVE_WITHIN_REVISION", "USD", "USD"),
        PowerBi("target-exposure-by-pair.csv", "economic_revision/canonical_pair",
            ["EconomicRevisionId", "CanonicalSymbol"], ["economic_revision", "instrument", "pair"], "ADDITIVE_WITHIN_REVISION", "USD", "USD"),
        PowerBi("target-exposure-by-currency.csv", "economic_revision/currency",
            ["EconomicRevisionId", "Currency"], ["economic_revision", "currency"], "ADDITIVE_WITHIN_REVISION", "USD", "USD"),
        PowerBi("target-gross-net.csv", "economic_revision",
            ["EconomicRevisionId"], ["economic_revision", "slot"], "NON_ADDITIVE_ACROSS_AS_OF", "USD", "USD"),
        PowerBi("target-concentration.csv", "economic_revision/dimension_type/dimension",
            ["EconomicRevisionId", "DimensionType", "DimensionId"], ["economic_revision", "strategy", "pair"], "NON_ADDITIVE", "RATIO", null),
        PowerBi("target-turnover.csv", "previous_revision/revision/dimension",
            ["PreviousEconomicRevisionId", "EconomicRevisionId", "DimensionType", "DimensionId"], ["economic_revision", "strategy", "pair"], "ADDITIVE_ONLY_FOR_DISJOINT_DIMENSIONS", "USD", "USD"),
        PowerBi("drift-by-strategy.csv", "economic_revision/strategy",
            ["EconomicRevisionId", "DimensionId"], ["economic_revision", "strategy"], "ADDITIVE_WITHIN_REVISION", "BASE_QUANTITY", null),
        PowerBi("drift-by-model.csv", "economic_revision/model_run",
            ["EconomicRevisionId", "DimensionId"], ["economic_revision", "model_run"], "ADDITIVE_WITHIN_REVISION", "BASE_QUANTITY", null),
        PowerBi("drift-by-pair.csv", "economic_revision/canonical_pair",
            ["EconomicRevisionId", "DimensionId"], ["economic_revision", "pair"], "ADDITIVE_WITHIN_REVISION", "BASE_QUANTITY", null),
        PowerBi("active-breaks.csv", "break", ["BreakId"], ["break", "severity", "status"], "NON_ADDITIVE", null, null)
    ];

    private static PowerBiCsvContract PowerBi(string file, string grain, string[] key,
        string[] dimensions, string additive, string? unit, string? currency) =>
        new(file, grain, key, dimensions, ["EconomicRevisionId -> economic revision dimension"],
            additive, unit, currency, "Explicit NULL literal; missing is never zero.",
            "Rows are valid at the injected AsOfUtc and are not additive across as-of.");

    private static string Evidence(params object?[] values) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n',
            values.Select(value => value switch
            {
                null => InstitutionalMetricContract.NullCsvValue,
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            })))));

    private static bool SameSign(decimal left, decimal right) =>
        left != 0m && right != 0m && Math.Sign(left) == Math.Sign(right);

    private static string NormalizeSymbol(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static bool IsSha(string value) =>
        value.Length == 64 && value.All(char.IsAsciiHexDigit);

    private static bool IsGitCommit(string value) =>
        (value.Length == 40 || value.Length == 64) && value.All(char.IsAsciiHexDigit);

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}
