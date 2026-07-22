using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch6cPostgreSqlPmsShadowStateTests
{
    private static readonly DateTimeOffset AsOfUtc = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Planner_projects_the_qualified_arch6b_rowset()
    {
        var plan = BuildPlan();

        Assert.Equal(4, plan.QubesInputSnapshots.Count);
        Assert.Equal(4, plan.ModelRuns.Count);
        Assert.Equal(288, plan.TargetWeights.Count);
        Assert.Equal(4, plan.TargetPositionStages.Count);
        Assert.Equal(288, plan.TargetPositions.Count);
        Assert.Equal(4, plan.PositionOnlyDriftStages.Count);
        Assert.Equal(288, plan.PositionOnlyDrifts.Count);
        Assert.Equal(4, plan.BrokerAdjustedDriftStages.Count);
        Assert.Equal(4, plan.CycleResults.Count);
        Assert.True(Arch6cPmsShadowPersistencePlanner.Validate(plan).IsValid);
    }

    [Fact]
    public void Planner_is_deterministic_and_content_addressed()
    {
        var first = BuildPlan();
        var second = BuildPlan();

        Assert.Equal(first.ModelRuns.ToArray(), second.ModelRuns.ToArray());
        Assert.Equal(first.TargetWeights.ToArray(), second.TargetWeights.ToArray());
        Assert.Equal(first.RowsetSha256, first.Ingestion.RowsetSha256);
        Assert.True(Arch5bHashing.IsSha256(first.RowsetSha256));
    }

    [Fact]
    public void Planner_preserves_relational_lineage()
    {
        var plan = BuildPlan();
        var runs = plan.ModelRuns.ToDictionary(x => x.ModelRunId);
        var inputs = plan.QubesInputSnapshots.Select(x => x.SnapshotId).ToHashSet();
        var artifacts = plan.SourceArtifacts.Select(x => x.ArtifactId).ToHashSet();
        var weights = plan.TargetWeights.Select(x => (x.ModelRunId, x.InstrumentId)).ToHashSet();

        Assert.All(runs.Values, run =>
        {
            Assert.Contains(run.QubesInputSnapshotId, inputs);
            Assert.Contains(run.OutputArtifactId, artifacts);
        });
        Assert.All(plan.TargetPositions, row => Assert.Contains((row.ModelRunId, row.InstrumentId), weights));
        Assert.All(plan.PositionOnlyDrifts, row => Assert.Contains((row.ModelRunId, row.InstrumentId), weights));
    }

    [Fact]
    public void Planner_preserves_working_leaves_as_unavailable_not_empty_and_non_authoritative()
    {
        var plan = BuildPlan();
        var leaves = plan.WorkingLeavesObservation;

        Assert.Equal(PmsShadowStateContract.WorkingLeavesUnavailable, leaves.Status);
        Assert.False(leaves.EmptyStateObserved);
        Assert.False(leaves.EmptyStateInferred);
        Assert.False(leaves.BrokerAuthority);
        Assert.All(plan.BrokerAdjustedDriftStages, stage =>
        {
            Assert.False(stage.Calculated);
            Assert.False(stage.EmptyStateInferred);
            Assert.Equal(PmsShadowStateContract.BrokerAdjustedBlocker, stage.Blocker);
        });
    }

    [Fact]
    public void Planner_preserves_the_no_order_boundary()
    {
        var plan = BuildPlan();

        Assert.All(plan.ModelRuns, run =>
        {
            Assert.False(run.AccountingEligible);
            Assert.False(run.ExecutionAllowed);
            Assert.True(run.NotAnOrder);
        });
        Assert.All(plan.CycleResults, result =>
        {
            Assert.False(result.ExecutionAllowed);
            Assert.True(result.NotAnOrder);
            Assert.True(result.NoBrokerRoute);
            Assert.True(result.NoFixMessage);
            Assert.False(result.OrderEntryEnabled);
            Assert.Equal(0, result.TradeIntentCount);
        });
    }

    [Theory]
    [InlineData("ingestions")]
    [InlineData("source_artifacts")]
    [InlineData("qubes_input_snapshots")]
    [InlineData("account_snapshots")]
    [InlineData("position_snapshots")]
    [InlineData("position_snapshot_lines")]
    [InlineData("market_data_snapshots")]
    [InlineData("market_data_observations")]
    [InlineData("security_mappings")]
    [InlineData("working_leaves_observations")]
    [InlineData("model_runs")]
    [InlineData("target_weights")]
    [InlineData("target_position_stages")]
    [InlineData("target_positions")]
    [InlineData("position_only_drift_stages")]
    [InlineData("position_only_drifts")]
    [InlineData("broker_adjusted_drift_stages")]
    [InlineData("cycle_results")]
    public void Ef_model_contains_each_canonical_table_in_the_dedicated_schema(string table)
    {
        using var context = NewContext();
        Assert.Contains(context.Model.GetEntityTypes(), entity =>
            entity.GetSchema() == PmsShadowStateContract.SchemaName && entity.GetTableName() == table);
    }

    [Fact]
    public void Ef_model_contains_only_explicit_arch7a_shadow_execution_entities_and_no_accounting_entity()
    {
        using var context = NewContext();
        var names = context.Model.GetEntityTypes().Select(x => x.ClrType.Name).ToArray();
        var executionNames = names.Where(name =>
                name.Contains("TradeIntent", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Order", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("RiskDecision", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal([
            nameof(PmsShadowChildOrderRow),
            nameof(PmsShadowParentOrderRow),
            nameof(PmsShadowRiskDecisionRow),
            nameof(PmsShadowTradeIntentRow)
        ], executionNames);
        Assert.DoesNotContain(names, name => name.Contains("Fill", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Ledger", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("ExecutionReport", StringComparison.OrdinalIgnoreCase));
    }
    [Fact]
    public void Ef_model_uses_restrict_for_every_foreign_key()
    {
        using var context = NewContext();
        var foreignKeys = context.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()).ToArray();

        Assert.NotEmpty(foreignKeys);
        Assert.All(foreignKeys, foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    [Theory]
    [InlineData(typeof(PmsShadowTargetWeightRow), nameof(PmsShadowTargetWeightRow.Weight), 28, 12)]
    [InlineData(typeof(PmsShadowMarketDataObservationRow), nameof(PmsShadowMarketDataObservationRow.Bid), 38, 28)]
    [InlineData(typeof(PmsShadowMarketDataObservationRow), nameof(PmsShadowMarketDataObservationRow.Ask), 38, 28)]
    [InlineData(typeof(PmsShadowTargetPositionRow), nameof(PmsShadowTargetPositionRow.TargetNotionalUsd), 28, 12)]
    [InlineData(typeof(PmsShadowTargetPositionRow), nameof(PmsShadowTargetPositionRow.TargetBaseQuantity), 28, 8)]
    [InlineData(typeof(PmsShadowPositionOnlyDriftRow), nameof(PmsShadowPositionOnlyDriftRow.PositionOnlyDeltaBaseQuantity), 28, 8)]
    public void Ef_model_has_explicit_decimal_precision(Type entityType, string propertyName, int precision, int scale)
    {
        using var context = NewContext();
        var property = context.Model.FindEntityType(entityType)!.FindProperty(propertyName)!;

        Assert.Equal(precision, property.GetPrecision());
        Assert.Equal(scale, property.GetScale());
    }

    [Fact]
    public void Design_time_factory_has_no_connection_string()
    {
        using var context = new PmsShadowDesignTimeDbContextFactory().CreateDbContext([]);

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        Assert.Null(context.Database.GetConnectionString());
    }

    [Fact]
    public void Context_rejects_updates_before_any_database_operation()
    {
        using var context = NewContext();
        var row = BuildPlan().Ingestion;
        context.Attach(row);
        context.Entry(row).Property(x => x.Status).CurrentValue = "MUTATED";

        Assert.Equal("PMS_SHADOW_FACTS_ARE_APPEND_ONLY", Assert.Throws<InvalidOperationException>(() => context.SaveChanges()).Message);
    }

    [Fact]
    public void Registry_accepts_an_identical_retry_without_duplicate_apply()
    {
        var registry = new InMemoryPmsShadowAtomicIngestionRegistry();
        var plan = BuildPlan();

        Assert.Equal(PmsShadowApplyResult.Applied, registry.Apply(plan));
        Assert.Equal(PmsShadowApplyResult.AlreadyAppliedIdentical, registry.Apply(plan));
    }

    [Fact]
    public void Registry_rejects_same_session_with_different_evidence()
    {
        var registry = new InMemoryPmsShadowAtomicIngestionRegistry();
        var plan = BuildPlan();
        registry.Apply(plan);

        var conflicting = plan with
        {
            Ingestion = plan.Ingestion with { SourceEvidenceSha256 = Sha('0') }
        };
        Assert.Contains("SOURCE_SESSION_EVIDENCE_SHA_CONFLICT", Assert.Throws<InvalidDataException>(() => registry.Apply(conflicting)).Message);
    }
    [Fact]
    public void Registry_rejects_same_model_run_id_with_different_output_sha()
    {
        var registry = new InMemoryPmsShadowAtomicIngestionRegistry();
        var plan = BuildPlan();
        registry.Apply(plan);
        var model = plan.ModelRuns[0];
        var changedSha = Sha('6');
        var artifacts = plan.SourceArtifacts.Select(x =>
            x.ArtifactId == model.OutputArtifactId ? x with { Sha256 = changedSha } : x).ToArray();
        PmsShadowModelRunRow[] models = [model with { OutputSha256 = changedSha }, .. plan.ModelRuns.Skip(1)];
        var conflicting = DifferentSession(plan) with { SourceArtifacts = artifacts, ModelRuns = models };

        Assert.Contains("MODEL_RUN_OUTPUT_SHA_CONFLICT",
            Assert.Throws<InvalidDataException>(() => registry.Apply(conflicting)).Message);
    }

    [Fact]
    public void Registry_rejects_same_snapshot_id_with_different_content()
    {
        var registry = new InMemoryPmsShadowAtomicIngestionRegistry();
        var plan = BuildPlan();
        registry.Apply(plan);
        var snapshot = plan.QubesInputSnapshots[0];
        var changedSha = Sha('5');
        var artifacts = plan.SourceArtifacts.Select(x =>
            x.ArtifactId == snapshot.InputArtifactId ? x with { Sha256 = changedSha } : x).ToArray();
        PmsShadowQubesInputSnapshotRow[] snapshots = [snapshot with { InputSha256 = changedSha }, .. plan.QubesInputSnapshots.Skip(1)];
        var conflicting = DifferentSession(plan) with { SourceArtifacts = artifacts, QubesInputSnapshots = snapshots };

        Assert.Contains("QUBES_INPUT_SNAPSHOT_CONTENT_CONFLICT",
            Assert.Throws<InvalidDataException>(() => registry.Apply(conflicting)).Message);
    }

    private static PmsShadowPersistencePlan DifferentSession(PmsShadowPersistencePlan plan)
    {
        var rowsetSha = Sha('7');
        return plan with
        {
            Ingestion = plan.Ingestion with
            {
                SourceSessionId = plan.Ingestion.SourceSessionId + "-different",
                SourceEvidenceSha256 = Sha('4'),
                RowsetSha256 = rowsetSha
            },
            RowsetSha256 = rowsetSha
        };
    }

    [Fact]
    public void Registry_interruption_is_atomic_and_retryable()
    {
        var registry = new InMemoryPmsShadowAtomicIngestionRegistry();
        var plan = BuildPlan();

        Assert.Throws<InvalidOperationException>(() => registry.Apply(plan, simulateInterruptionBeforeCommit: true));
        Assert.Equal(PmsShadowApplyResult.Applied, registry.Apply(plan));
    }

    [Fact]
    public async Task Registry_serializes_concurrent_identical_ingestions()
    {
        var registry = new InMemoryPmsShadowAtomicIngestionRegistry();
        var plan = BuildPlan();
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() => registry.Apply(plan))).ToArray();

        var results = await Task.WhenAll(tasks);
        Assert.Single(results, result => result == PmsShadowApplyResult.Applied);
        Assert.Equal(7, results.Count(result => result == PmsShadowApplyResult.AlreadyAppliedIdentical));
    }

    [Theory]
    [InlineData("wrong-contract", "UNKNOWN_CONTRACT_VERSION")]
    [InlineData("bad-count", "TARGET_WEIGHT_COUNT_INVALID")]
    [InlineData("working-leaves-empty", "WORKING_LEAVES_FALSE_EMPTY_OR_AUTHORITY")]
    [InlineData("order-enabled", "NO_ORDER_REGRESSION")]
    [InlineData("bad-market", "MARKET_DATA_INVALID")]
    [InlineData("orphan-weight", "TARGET_WEIGHT_ORPHAN")]
    [InlineData("target-position-no-price", "TARGET_POSITION_LINEAGE_INCOMPLETE")]
    [InlineData("target-stage-no-account", "TARGET_POSITION_STAGE_LINEAGE_INCOMPLETE")]
    [InlineData("drift-no-snapshot", "DRIFT_POSITION_SNAPSHOT_MISSING")]
    [InlineData("broker-calculated", "BROKER_ADJUSTED_DRIFT_INVALID")]
    [InlineData("duplicate-weight", "DUPLICATE_MODEL_RUN_SECURITY_ID")]
    [InlineData("bad-sha", "SHA_INVALID")]
    [InlineData("non-utc", "TIMESTAMP_NOT_UTC")]
    [InlineData("numeric-overflow", "NUMERIC_ENVELOPE_INVALID")]
    [InlineData("real-account", "REAL_ACCOUNT_REJECTED")]
    [InlineData("execution-allowed", "MODEL_RUN_NO_ORDER_INVALID")]
    [InlineData("trade-intent", "NO_ORDER_REGRESSION")]
    [InlineData("orphan-drift-stage", "DRIFT_STAGE_MISSING")]
    public void Validator_rejects_contract_and_lineage_regressions(string mutation, string issue)
    {
        var plan = BuildPlan();
        plan = mutation switch
        {
            "wrong-contract" => plan with { Ingestion = plan.Ingestion with { ContractVersion = "unknown" } },
            "bad-count" => plan with { TargetWeights = plan.TargetWeights.Skip(1).ToArray() },
            "working-leaves-empty" => plan with { WorkingLeavesObservation = plan.WorkingLeavesObservation with { EmptyStateObserved = true } },
            "order-enabled" => plan with { CycleResults = [plan.CycleResults[0] with { OrderEntryEnabled = true }, .. plan.CycleResults.Skip(1)] },
            "bad-market" => plan with { MarketDataObservations = [plan.MarketDataObservations[0] with { Ask = 0m }, .. plan.MarketDataObservations.Skip(1)] },
            "orphan-weight" => plan with { TargetWeights = [plan.TargetWeights[0] with { ModelRunId = Guid.NewGuid() }, .. plan.TargetWeights.Skip(1)] },
            "target-position-no-price" => plan with { MarketDataObservations = plan.MarketDataObservations.Skip(1).ToArray() },
            "target-stage-no-account" => plan with { TargetPositionStages = [plan.TargetPositionStages[0] with { AccountSnapshotId = Guid.NewGuid() }, .. plan.TargetPositionStages.Skip(1)] },
            "drift-no-snapshot" => plan with { PositionOnlyDriftStages = [plan.PositionOnlyDriftStages[0] with { PositionSnapshotId = Guid.NewGuid() }, .. plan.PositionOnlyDriftStages.Skip(1)] },
            "broker-calculated" => plan with { BrokerAdjustedDriftStages = [plan.BrokerAdjustedDriftStages[0] with { Calculated = true }, .. plan.BrokerAdjustedDriftStages.Skip(1)] },
            "duplicate-weight" => plan with { TargetWeights = [plan.TargetWeights[0], plan.TargetWeights[0], .. plan.TargetWeights.Skip(2)] },
            "bad-sha" => plan with { Ingestion = plan.Ingestion with { SourceEvidenceSha256 = "bad" } },
            "non-utc" => plan with { Ingestion = plan.Ingestion with { StartedAtUtc = plan.Ingestion.StartedAtUtc.ToOffset(TimeSpan.FromHours(2)) } },
            "numeric-overflow" => plan with { TargetWeights = [plan.TargetWeights[0] with { Weight = decimal.MaxValue }, .. plan.TargetWeights.Skip(1)] },
            "real-account" => plan with { AccountSnapshot = plan.AccountSnapshot with { AccountId = Arch5bLineageContractVersions.RealAccountId } },
            "execution-allowed" => plan with { ModelRuns = [plan.ModelRuns[0] with { ExecutionAllowed = true }, .. plan.ModelRuns.Skip(1)] },
            "trade-intent" => plan with { CycleResults = [plan.CycleResults[0] with { TradeIntentCount = 1 }, .. plan.CycleResults.Skip(1)] },
            "orphan-drift-stage" => plan with { PositionOnlyDrifts = [plan.PositionOnlyDrifts[0] with { StageId = Guid.NewGuid() }, .. plan.PositionOnlyDrifts.Skip(1)] },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };

        Assert.Contains(issue, Arch6cPmsShadowPersistencePlanner.Validate(plan).Issues);
    }

    [Theory]
    [InlineData("CREATE TABLE pms_shadow.ingestions")]
    [InlineData("CREATE TABLE pms_shadow.qubes_input_snapshots")]
    [InlineData("CREATE TABLE pms_shadow.model_runs")]
    [InlineData("CREATE TABLE pms_shadow.target_weights")]
    [InlineData("CREATE TABLE pms_shadow.target_positions")]
    [InlineData("CREATE TABLE pms_shadow.position_only_drifts")]
    [InlineData("CREATE TABLE pms_shadow.broker_adjusted_drift_stages")]
    [InlineData("CREATE TABLE pms_shadow.cycle_results")]
    [InlineData("ON DELETE RESTRICT")]
    [InlineData("ck_cycle_result_no_order")]
    [InlineData("ck_working_leaves_not_empty_not_inferred")]
    public void Up_sql_contains_required_schema_elements(string fragment)
        => Assert.Contains(fragment, ReadSql("20260721152240_up.sql"), StringComparison.Ordinal);

    [Theory]
    [InlineData("CREATE TABLE pms_shadow.trade_intents")]
    [InlineData("CREATE TABLE pms_shadow.orders")]
    [InlineData("CREATE TABLE pms_shadow.fills")]
    [InlineData("CREATE TABLE pms_shadow.ledger")]
    [InlineData("UPDATE pms_shadow")]
    [InlineData("DELETE FROM pms_shadow")]
    [InlineData("INSERT INTO pms_shadow")]
    public void Up_sql_contains_no_execution_accounting_or_data_mutation_path(string fragment)
        => Assert.DoesNotContain(fragment, ReadSql("20260721152240_up.sql"), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Idempotent_sql_is_guarded_by_the_migration_history()
    {
        var sql = ReadSql("20260721152240_idempotent.sql");
        Assert.Contains("IF NOT EXISTS", sql, StringComparison.Ordinal);
        Assert.Contains("__EFMigrationsHistory", sql, StringComparison.Ordinal);
        Assert.Contains("20260721152240_InitialPostgreSqlPmsShadowState", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Down_sql_removes_the_dedicated_schema_after_its_tables_and_migration_history_row()
    {
        var sql = ReadSql("20260721152240_down.sql");
        Assert.Contains("DROP TABLE pms_shadow.ingestions", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE public.", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP DATABASE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DROP SCHEMA pms_shadow", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static PmsShadowDbContext NewContext()
        => new PmsShadowDesignTimeDbContextFactory().CreateDbContext([]);

    internal static PmsShadowPersistencePlan BuildPlan()
    {
        var bundle = ValidBundle();
        var result = new Arch6aOperationalPositionShadowService().Build(bundle);
        var bindings = result.Preview.Runs.OrderBy(x => x.ModelRun.StrategyId, StringComparer.Ordinal).Select(run =>
        {
            var strategy = run.ModelRun.StrategyId;
            return new Arch6cQubesInputBinding(strategy, Hash($"source:{strategy}"), Hash($"overlay:{strategy}"), null,
                bundle.QubesToLmaxMappingSha256, Hash($"input:{strategy}"), 72, 0, AsOfUtc, "ARCH6B_QUALIFIED_INPUT");
        }).ToArray();
        var artifacts = new List<Arch6cArtifactReference>();
        foreach (var binding in bindings)
        {
            artifacts.Add(Artifact("QUBES_SOURCE_SNAPSHOT", binding.SourceSnapshotSha256, $"inputs/{binding.StrategyId}/source.json"));
            artifacts.Add(Artifact("CONTENT_ADDRESSED_OVERLAY", binding.OverlaySha256, $"inputs/{binding.StrategyId}/overlay.json"));
            artifacts.Add(Artifact("QUBES_INPUT_SNAPSHOT", binding.InputSnapshotSha256, $"inputs/{binding.StrategyId}/input.json"));
        }
        foreach (var run in result.Preview.Runs)
            artifacts.Add(Artifact("QUBES_WEIGHTS_OUTPUT", run.Lineage.OutputSha256, run.Lineage.OutputRelativePath));

        return Arch6cPmsShadowPersistencePlanner.Build(new(
            "GO_ARCH6B_BIND_LMAX_OPERATIONAL_MARKET_DATA_TO_QUBES_INPUT_AND_QUALIFY_FRESH_DAILY_MODEL_POSITION_SHADOW_NO_ORDER",
            "arch6b-daily-tier1-20260721T130346Z-422530a8", Sha('8'), AsOfUtc.AddMinutes(-3), AsOfUtc,
            artifacts, bindings, result));
    }

    private static Arch6cArtifactReference Artifact(string type, string sha, string uri) => new(
        type, sha, 100, uri.Replace('\\', '/'), "v1", AsOfUtc, "ARCH6B_EVIDENCE", PmsShadowStateContract.EvidenceClassification);

    private static OperationalPositionShadowInputBundleV1 ValidBundle()
    {
        var lineage = ValidLineage();
        var securityIds = lineage.Runs.SelectMany(run => run.TargetCloseWeights).Select(x => x.SecurityId)
            .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var sources = new[] { new Arch6aSourceFileEvidence("lmax/eod/account.csv", Sha('a'), AsOfUtc) };
        var account = new OperationalAccountSnapshotV1(
            Arch6aOperationalPositionShadowContracts.AccountV1, Arch5bLineageContractVersions.TestAccountId,
            Arch5bLineageContractVersions.TestAccountScope, "USD", 1_000_000m, DateOnly.FromDateTime(AsOfUtc.UtcDateTime),
            AsOfUtc, sources, Sha('b'), "BROKER_PORTAL_EOD", "HISTORICAL");
        var positions = new OperationalPositionSnapshotV1(
            Arch6aOperationalPositionShadowContracts.PositionV1, Arch5bLineageContractVersions.TestAccountId,
            account.ReportDate, AsOfUtc, [], true, false, true,
            [new Arch6aSourceFileEvidence("lmax/eod/open-positions.csv", Sha('c'), AsOfUtc)], Sha('d'));
        var quotes = securityIds.Select((securityId, index) => new OperationalMarketDataQuoteV1(
            securityId, $"lmax-{securityId}", $"FX{int.Parse(securityId):000}", 1m + index / 10_000m,
            1.0001m + index / 10_000m, AsOfUtc.AddMilliseconds(-10), AsOfUtc, 10,
            "lmax-capture-20260701", Sha('e'), "LMAX", "LMAX_DIRECT", [securityId])).ToArray();
        var market = new OperationalMarketDataSnapshotV1(
            Arch6aOperationalPositionShadowContracts.MarketDataV1, AsOfUtc, quotes, Sha('f'), 0, 0, 0);
        var mappings = securityIds.Select(securityId => new OperationalSecurityMappingV1(
            securityId, Arch5bHashing.GuidFromSha256($"instrument:{securityId}"), Arch5bHashing.GuidFromSha256("venue:lmax"),
            Arch5bHashing.GuidFromSha256($"venue-instrument:{securityId}"), $"FX{int.Parse(securityId):000}",
            $"lmax-{securityId}", 1m, 1m, 0.00001m)).ToArray();
        var leaves = new BrokerWorkingLeavesObservationV1(
            Arch6aOperationalPositionShadowContracts.WorkingLeavesV1,
            Arch6aOperationalPositionShadowContracts.WorkingLeavesUnavailable, "LMAX", false, false, false, false,
            Arch6aOperationalPositionShadowContracts.WorkingLeavesReason,
            Arch6aOperationalPositionShadowContracts.WorkingLeavesImpact);
        var temporal = new Arch6aTemporalPolicyV1(
            Arch6aOperationalShadowMode.HISTORICAL_LMAX_OPERATIONAL_POSITION_SHADOW, AsOfUtc, AsOfUtc, false, false,
            Arch6aOperationalPositionShadowContracts.WorkingLeavesUnavailable);
        var safety = new Arch6aNoOrderSafetyV1(
            false, false, true, true, true, false, 0, PmsShadowStateContract.DisabledBrokerSend,
            0, 0, 0, 0, 0, 0, 0, 0);
        var draft = new OperationalPositionShadowInputBundleV1(
            Arch6aOperationalPositionShadowContracts.BundleV1, string.Empty,
            Arch6aOperationalPositionShadowContracts.Classification,
            Arch6aOperationalPositionShadowContracts.WorkingLeavesClassification,
            Arch6aOperationalPositionShadowContracts.EvidenceClassification,
            Arch6aOperationalPositionShadowContracts.NoOrderClassification, lineage, 288,
            Arch6aOperationalPositionShadowContracts.QubesToLmaxMappingV1, Sha('9'), account, positions, market,
            leaves, mappings, temporal, safety);
        return draft with { BundleSha256 = Arch6aOperationalPositionShadowValidator.ComputeBundleSha256(draft) };
    }

    private static Arch5bSessionLineageContractV1 ValidLineage()
    {
        var strategies = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["INFX7"] = 4.5m, ["INFX8"] = 2.1m, ["INFX9"] = 1.4m, ["INFX10"] = 0.6m
        };
        var runs = strategies.OrderBy(x => x.Key, StringComparer.Ordinal).Select(entry =>
        {
            var weights = Enumerable.Range(1, 72).Select(index => new Arch5bTargetCloseWeightV1(
                index.ToString(), "0.001", 0.001d, index - 1, $"202607011200:{index}", Hash($"{entry.Key}:{index}"))).ToArray();
            return new Arch5bRunLineageContractV1(
                Arch5bLineageContractVersions.LineageV1, Arch5bLineageContractVersions.SourceQubesWeightsOutputV1,
                "arch6b-session", $"arch6b-{entry.Key}", $"arch6b-{entry.Key}", entry.Key, entry.Value,
                new string('a', 40), Sha('1'), Sha('2'), "arch6b-bundle", Sha('3'), Hash($"output:{entry.Key}"), 100,
                $"outputs/{entry.Key}/AggregatedWeights.txt", Arch5bLineageContractVersions.OutputQubesWeightsOutputV1,
                AsOfUtc, AsOfUtc, AsOfUtc, "202607011200", "PRODMANAGERV4_LAST_CHRONOLOGICAL_DATA_ROW", "PASS",
                0, 0, true, null, null, Arch5bLineageContractVersions.MissingMarketDataSnapshot,
                Arch5bLineageContractVersions.EvidenceOnlyClassification, true, false, false, weights);
        }).ToArray();
        return new Arch5bSessionLineageContractV1(
            Arch5bLineageContractVersions.LineageV1, Arch5bLineageContractVersions.SourceQubesWeightsOutputV1,
            "arch6b-session", Arch5bLineageContractVersions.TestAccountId, Arch5bLineageContractVersions.TestAccountScope,
            new string('a', 40), Sha('1'), Sha('2'), "arch6b-bundle", AsOfUtc,
            Arch5bLineageContractVersions.EvidenceOnlyClassification, true, false, false, runs);
    }

    private static string ReadSql(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory.FullName, "src", "QQ.Production.Intraday.Infrastructure.PostgreSql", "Sql", name));
    }

    private static string Hash(string value) => Arch5bHashing.Sha256Hex(value);
    private static string Sha(char value) => new(value, 64);
}
