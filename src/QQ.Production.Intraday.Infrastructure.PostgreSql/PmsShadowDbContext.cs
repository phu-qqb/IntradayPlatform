using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public sealed class PmsShadowDbContext(DbContextOptions<PmsShadowDbContext> options) : DbContext(options)
{
    public DbSet<PmsShadowIngestionRow> Ingestions => Set<PmsShadowIngestionRow>();
    public DbSet<PmsShadowSourceArtifactRow> SourceArtifacts => Set<PmsShadowSourceArtifactRow>();
    public DbSet<PmsShadowQubesInputSnapshotRow> QubesInputSnapshots => Set<PmsShadowQubesInputSnapshotRow>();
    public DbSet<PmsShadowAccountSnapshotRow> AccountSnapshots => Set<PmsShadowAccountSnapshotRow>();
    public DbSet<PmsShadowPositionSnapshotRow> PositionSnapshots => Set<PmsShadowPositionSnapshotRow>();
    public DbSet<PmsShadowPositionSnapshotLineRow> PositionSnapshotLines => Set<PmsShadowPositionSnapshotLineRow>();
    public DbSet<PmsShadowMarketDataSnapshotRow> MarketDataSnapshots => Set<PmsShadowMarketDataSnapshotRow>();
    public DbSet<PmsShadowMarketDataObservationRow> MarketDataObservations => Set<PmsShadowMarketDataObservationRow>();
    public DbSet<PmsShadowSecurityMappingRow> SecurityMappings => Set<PmsShadowSecurityMappingRow>();
    public DbSet<PmsShadowWorkingLeavesObservationRow> WorkingLeavesObservations => Set<PmsShadowWorkingLeavesObservationRow>();
    public DbSet<PmsShadowModelRunRow> ModelRuns => Set<PmsShadowModelRunRow>();
    public DbSet<PmsShadowTargetWeightRow> TargetWeights => Set<PmsShadowTargetWeightRow>();
    public DbSet<PmsShadowTargetPositionStageRow> TargetPositionStages => Set<PmsShadowTargetPositionStageRow>();
    public DbSet<PmsShadowTargetPositionRow> TargetPositions => Set<PmsShadowTargetPositionRow>();
    public DbSet<PmsShadowPositionOnlyDriftStageRow> PositionOnlyDriftStages => Set<PmsShadowPositionOnlyDriftStageRow>();
    public DbSet<PmsShadowPositionOnlyDriftRow> PositionOnlyDrifts => Set<PmsShadowPositionOnlyDriftRow>();
    public DbSet<PmsShadowBrokerAdjustedDriftStageRow> BrokerAdjustedDriftStages => Set<PmsShadowBrokerAdjustedDriftStageRow>();
    public DbSet<PmsShadowCycleResultRow> CycleResults => Set<PmsShadowCycleResultRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(PmsShadowStateContract.SchemaName);
        ConfigureIngestion(modelBuilder.Entity<PmsShadowIngestionRow>());
        ConfigureSourceArtifact(modelBuilder.Entity<PmsShadowSourceArtifactRow>());
        ConfigureQubesInput(modelBuilder.Entity<PmsShadowQubesInputSnapshotRow>());
        ConfigureAccount(modelBuilder.Entity<PmsShadowAccountSnapshotRow>());
        ConfigurePositionSnapshot(modelBuilder.Entity<PmsShadowPositionSnapshotRow>());
        ConfigurePositionLine(modelBuilder.Entity<PmsShadowPositionSnapshotLineRow>());
        ConfigureMarketSnapshot(modelBuilder.Entity<PmsShadowMarketDataSnapshotRow>());
        ConfigureMarketObservation(modelBuilder.Entity<PmsShadowMarketDataObservationRow>());
        ConfigureSecurityMapping(modelBuilder.Entity<PmsShadowSecurityMappingRow>());
        ConfigureWorkingLeaves(modelBuilder.Entity<PmsShadowWorkingLeavesObservationRow>());
        ConfigureModelRun(modelBuilder.Entity<PmsShadowModelRunRow>());
        ConfigureTargetWeight(modelBuilder.Entity<PmsShadowTargetWeightRow>());
        ConfigureTargetPositionStage(modelBuilder.Entity<PmsShadowTargetPositionStageRow>());
        ConfigureTargetPosition(modelBuilder.Entity<PmsShadowTargetPositionRow>());
        ConfigureDriftStage(modelBuilder.Entity<PmsShadowPositionOnlyDriftStageRow>());
        ConfigureDrift(modelBuilder.Entity<PmsShadowPositionOnlyDriftRow>());
        ConfigureBrokerStage(modelBuilder.Entity<PmsShadowBrokerAdjustedDriftStageRow>());
        ConfigureCycleResult(modelBuilder.Entity<PmsShadowCycleResultRow>());
        ApplySnakeCaseColumns(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectMutations();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RejectMutations();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RejectMutations()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("PMS_SHADOW_FACTS_ARE_APPEND_ONLY");
            if (entry.State != EntityState.Modified)
                continue;

            if (entry.Entity is not PmsShadowIngestionRow ||
                entry.Properties.Where(x => x.IsModified).Any(x =>
                    x.Metadata.Name is not nameof(PmsShadowIngestionRow.Status) and
                    not nameof(PmsShadowIngestionRow.CompletedAtUtc)) ||
                !string.Equals(entry.OriginalValues.GetValue<string>(nameof(PmsShadowIngestionRow.Status)),
                    PmsShadowIngestionStatuses.Applying, StringComparison.Ordinal) ||
                !string.Equals(entry.CurrentValues.GetValue<string>(nameof(PmsShadowIngestionRow.Status)),
                    PmsShadowIngestionStatuses.Completed, StringComparison.Ordinal) ||
                entry.OriginalValues.GetValue<DateTimeOffset?>(nameof(PmsShadowIngestionRow.CompletedAtUtc)) is not null ||
                entry.CurrentValues.GetValue<DateTimeOffset?>(nameof(PmsShadowIngestionRow.CompletedAtUtc)) is not { } completedAtUtc ||
                completedAtUtc.Offset != TimeSpan.Zero)
                throw new InvalidOperationException("PMS_SHADOW_FACTS_ARE_APPEND_ONLY");
        }
    }

    private static void ConfigureIngestion(EntityTypeBuilder<PmsShadowIngestionRow> entity)
    {
        entity.ToTable("ingestions", table =>
        {
            table.HasCheckConstraint("ck_ingestions_source_evidence_sha256", ShaCheck("source_evidence_sha256"));
            table.HasCheckConstraint("ck_ingestions_rowset_sha256", ShaCheck("rowset_sha256"));
            table.HasCheckConstraint("ck_ingestions_environment", $"environment = '{PmsShadowStateContract.TestEnvironment}'");
            table.HasCheckConstraint("ck_ingestions_classification", $"classification = '{PmsShadowStateContract.EvidenceClassification}'");
        });
        entity.HasKey(x => x.IngestionId);
        entity.HasIndex(x => x.CompletedAtUtc);
        entity.HasIndex(x => x.SourceSessionId).IsUnique();
        entity.HasIndex(x => new { x.SourceSessionId, x.SourceEvidenceSha256, x.RowsetSha256 }).IsUnique();
        Text(entity.Property(x => x.SourceGate), 160);
        Text(entity.Property(x => x.SourceSessionId), 200);
        Hash(entity.Property(x => x.SourceEvidenceSha256));
        Text(entity.Property(x => x.Status), 32);
        Text(entity.Property(x => x.ContractVersion), 96);
        Text(entity.Property(x => x.Environment), 64);
        Text(entity.Property(x => x.Classification), 64);
        Hash(entity.Property(x => x.RowsetSha256));
    }

    private static void ConfigureSourceArtifact(EntityTypeBuilder<PmsShadowSourceArtifactRow> entity)
    {
        entity.ToTable("source_artifacts", table => table.HasCheckConstraint("ck_source_artifacts_sha256", ShaCheck("sha256")));
        entity.HasKey(x => x.ArtifactId);
        entity.HasIndex(x => new { x.IngestionId, x.Sha256 }).IsUnique();
        entity.HasIndex(x => new { x.Sha256, x.ArtifactType, x.SizeBytes, x.LogicalUri }).IsUnique();
        Restrict<PmsShadowSourceArtifactRow, PmsShadowIngestionRow>(entity, x => x.IngestionId);
        Text(entity.Property(x => x.ArtifactType), 96);
        Hash(entity.Property(x => x.Sha256));
        Text(entity.Property(x => x.LogicalUri), 1024);
        Text(entity.Property(x => x.ContractVersion), 96);
        Text(entity.Property(x => x.SourceSystem), 96);
        Text(entity.Property(x => x.Classification), 64);
    }

    private static void ConfigureQubesInput(EntityTypeBuilder<PmsShadowQubesInputSnapshotRow> entity)
    {
        entity.ToTable("qubes_input_snapshots", table =>
        {
            table.HasCheckConstraint("ck_qubes_source_sha256", ShaCheck("source_snapshot_sha256"));
            table.HasCheckConstraint("ck_qubes_overlay_sha256", ShaCheck("overlay_sha256"));
            table.HasCheckConstraint("ck_qubes_input_sha256", ShaCheck("input_sha256"));
            table.HasCheckConstraint("ck_qubes_counts", "source_instrument_count > 0 AND gap_count >= 0");
        });
        entity.HasKey(x => x.SnapshotId);
        entity.HasIndex(x => x.TargetCloseUtc);
        entity.HasIndex(x => new { x.IngestionId, x.StrategyId }).IsUnique();
        entity.HasIndex(x => x.InputSha256).IsUnique();
        Restrict<PmsShadowQubesInputSnapshotRow, PmsShadowIngestionRow>(entity, x => x.IngestionId);
        Restrict<PmsShadowQubesInputSnapshotRow, PmsShadowSourceArtifactRow>(entity, x => x.InputArtifactId);
        Restrict<PmsShadowQubesInputSnapshotRow, PmsShadowSourceArtifactRow>(entity, x => x.SourceSnapshotArtifactId);
        Restrict<PmsShadowQubesInputSnapshotRow, PmsShadowSourceArtifactRow>(entity, x => x.OverlayArtifactId);
        Text(entity.Property(x => x.StrategyId), 160);
        Hash(entity.Property(x => x.SourceSnapshotSha256));
        Hash(entity.Property(x => x.OverlaySha256));
        OptionalHash(entity.Property(x => x.GapLedgerSha256));
        Hash(entity.Property(x => x.MappingSha256));
        Hash(entity.Property(x => x.InputSha256));
        Text(entity.Property(x => x.Provenance), 512);
        Text(entity.Property(x => x.Classification), 64);
    }

    private static void ConfigureAccount(EntityTypeBuilder<PmsShadowAccountSnapshotRow> entity)
    {
        entity.ToTable("account_snapshots", table =>
        {
            table.HasCheckConstraint("ck_account_snapshot_sha256", ShaCheck("snapshot_sha256"));
            table.HasCheckConstraint("ck_account_source_sha256", ShaCheck("source_artifact_sha256"));
            table.HasCheckConstraint("ck_account_not_real", $"account_id <> '{Arch5bLineageContractVersions.RealAccountId}'");
        });
        entity.HasKey(x => x.AccountSnapshotId);
        entity.HasIndex(x => new { x.ReportDate, x.AsOfUtc });
        entity.HasIndex(x => new { x.IngestionId, x.SnapshotSha256 }).IsUnique();
        Restrict<PmsShadowAccountSnapshotRow, PmsShadowIngestionRow>(entity, x => x.IngestionId);
        Text(entity.Property(x => x.AccountId), 160);
        Text(entity.Property(x => x.Scope), 96);
        Text(entity.Property(x => x.BaseCurrency), 3);
        Money(entity.Property(x => x.NavOrEquity));
        Text(entity.Property(x => x.Authority), 96);
        Hash(entity.Property(x => x.SourceArtifactSha256));
        Hash(entity.Property(x => x.SnapshotSha256));
        Text(entity.Property(x => x.Classification), 64);
    }

    private static void ConfigurePositionSnapshot(EntityTypeBuilder<PmsShadowPositionSnapshotRow> entity)
    {
        entity.ToTable("position_snapshots", table =>
        {
            table.HasCheckConstraint("ck_position_snapshot_sha256", ShaCheck("snapshot_sha256"));
            table.HasCheckConstraint("ck_position_empty_not_inferred", "NOT empty_state_was_inferred");
        });
        entity.HasKey(x => x.PositionSnapshotId);
        entity.HasIndex(x => new { x.ReportDate, x.AsOfUtc });
        entity.HasIndex(x => new { x.IngestionId, x.SnapshotSha256 }).IsUnique();
        Restrict<PmsShadowPositionSnapshotRow, PmsShadowIngestionRow>(entity, x => x.IngestionId);
        Restrict<PmsShadowPositionSnapshotRow, PmsShadowAccountSnapshotRow>(entity, x => x.AccountSnapshotId);
        Hash(entity.Property(x => x.SnapshotSha256));
        Text(entity.Property(x => x.Classification), 64);
    }

    private static void ConfigurePositionLine(EntityTypeBuilder<PmsShadowPositionSnapshotLineRow> entity)
    {
        entity.ToTable("position_snapshot_lines");
        entity.HasKey(x => new { x.PositionSnapshotId, x.InstrumentId });
        Restrict<PmsShadowPositionSnapshotLineRow, PmsShadowPositionSnapshotRow>(entity, x => x.PositionSnapshotId);
        Text(entity.Property(x => x.SecurityId), 160);
        Text(entity.Property(x => x.Symbol), 64);
        Quantity(entity.Property(x => x.CurrentBaseQuantity));
    }

    private static void ConfigureMarketSnapshot(EntityTypeBuilder<PmsShadowMarketDataSnapshotRow> entity)
    {
        entity.ToTable("market_data_snapshots", table =>
        {
            table.HasCheckConstraint("ck_market_snapshot_sha256", ShaCheck("snapshot_sha256"));
            table.HasCheckConstraint("ck_market_observation_count", "observation_count > 0");
        });
        entity.HasKey(x => x.MarketDataSnapshotId);
        entity.HasIndex(x => x.AsOfUtc);
        entity.HasIndex(x => new { x.IngestionId, x.SnapshotSha256 }).IsUnique();
        Restrict<PmsShadowMarketDataSnapshotRow, PmsShadowIngestionRow>(entity, x => x.IngestionId);
        Hash(entity.Property(x => x.SnapshotSha256));
        Text(entity.Property(x => x.Classification), 64);
    }

    private static void ConfigureMarketObservation(EntityTypeBuilder<PmsShadowMarketDataObservationRow> entity)
    {
        entity.ToTable("market_data_observations", table =>
        {
            table.HasCheckConstraint("ck_market_bid_ask", "bid > 0 AND ask >= bid");
            table.HasCheckConstraint("ck_market_source_sha256", ShaCheck("source_file_sha256"));
            table.HasCheckConstraint("ck_market_staleness", "staleness_milliseconds >= 0");
        });
        entity.HasKey(x => new { x.MarketDataSnapshotId, x.InstrumentId });
        Restrict<PmsShadowMarketDataObservationRow, PmsShadowMarketDataSnapshotRow>(entity, x => x.MarketDataSnapshotId);
        Text(entity.Property(x => x.SecurityId), 160);
        Text(entity.Property(x => x.LmaxInstrumentId), 64);
        Text(entity.Property(x => x.Symbol), 64);
        Price(entity.Property(x => x.Bid));
        Price(entity.Property(x => x.Ask));
        Text(entity.Property(x => x.SourceCaptureId), 160);
        Hash(entity.Property(x => x.SourceFileSha256));
        Text(entity.Property(x => x.ProjectionMethod), 96);
        entity.Property(x => x.ProjectionLegSecurityIdsJson).HasColumnType("jsonb");
    }

    private static void ConfigureSecurityMapping(EntityTypeBuilder<PmsShadowSecurityMappingRow> entity)
    {
        entity.ToTable("security_mappings", table =>
        {
            table.HasCheckConstraint("ck_security_mapping_sha256", ShaCheck("mapping_sha256"));
            table.HasCheckConstraint("ck_security_mapping_positive", "quantity_multiplier > 0 AND quantity_increment > 0 AND price_increment > 0");
        });
        entity.HasKey(x => new { x.IngestionId, x.InstrumentId });
        entity.HasIndex(x => new { x.IngestionId, x.SecurityId }).IsUnique();
        entity.HasIndex(x => new { x.IngestionId, x.LmaxInstrumentId }).IsUnique();
        Restrict<PmsShadowSecurityMappingRow, PmsShadowIngestionRow>(entity, x => x.IngestionId);
        Text(entity.Property(x => x.SecurityId), 160);
        Text(entity.Property(x => x.Symbol), 64);
        Text(entity.Property(x => x.LmaxInstrumentId), 64);
        Ratio(entity.Property(x => x.QuantityMultiplier));
        Ratio(entity.Property(x => x.QuantityIncrement));
        Ratio(entity.Property(x => x.PriceIncrement));
        Hash(entity.Property(x => x.MappingSha256));
    }

    private static void ConfigureWorkingLeaves(EntityTypeBuilder<PmsShadowWorkingLeavesObservationRow> entity)
    {
        entity.ToTable("working_leaves_observations", table =>
        {
            table.HasCheckConstraint("ck_working_leaves_unavailable", $"status = '{PmsShadowStateContract.WorkingLeavesUnavailable}'");
            table.HasCheckConstraint("ck_working_leaves_not_empty_not_inferred", "NOT empty_state_observed AND NOT empty_state_inferred AND NOT broker_authority");
        });
        entity.HasKey(x => x.WorkingLeavesObservationId);
        entity.HasIndex(x => x.IngestionId).IsUnique();
        Restrict<PmsShadowWorkingLeavesObservationRow, PmsShadowIngestionRow>(entity, x => x.IngestionId);
        Text(entity.Property(x => x.Status), 96);
        Text(entity.Property(x => x.SourceSystem), 96);
        Text(entity.Property(x => x.Reason), 256);
        Text(entity.Property(x => x.Impact), 160);
        Text(entity.Property(x => x.Classification), 64);
    }

    private static void ConfigureModelRun(EntityTypeBuilder<PmsShadowModelRunRow> entity)
    {
        entity.ToTable("model_runs", table =>
        {
            table.HasCheckConstraint("ck_model_run_artifact_hashes", $"{ShaCheck("package_sha256")} AND {ShaCheck("engine_sha256")} AND {ShaCheck("output_sha256")}");
            table.HasCheckConstraint("ck_model_run_core_master_commit_identity",
                GitCommitCheck("core_master_commit_id", "core_master_object_format"));
            table.HasCheckConstraint("ck_model_run_no_order", "NOT accounting_eligible AND NOT execution_allowed AND not_an_order");
            table.HasCheckConstraint("ck_model_run_exit_codes", "wrapper_exit_code = 0 AND native_exit_code = 0");
        });
        entity.HasKey(x => x.ModelRunId);
        entity.HasIndex(x => x.TargetCloseUtc);
        entity.HasIndex(x => new { x.IngestionId, x.StrategyId }).IsUnique();
        entity.HasIndex(x => new { x.ExternalModelRunId, x.OutputSha256 }).IsUnique();
        Restrict<PmsShadowModelRunRow, PmsShadowIngestionRow>(entity, x => x.IngestionId);
        Restrict<PmsShadowModelRunRow, PmsShadowQubesInputSnapshotRow>(entity, x => x.QubesInputSnapshotId);
        Restrict<PmsShadowModelRunRow, PmsShadowSourceArtifactRow>(entity, x => x.OutputArtifactId);
        Text(entity.Property(x => x.ExternalModelRunId), 200);
        Text(entity.Property(x => x.SourceDomainModel), 160);
        Text(entity.Property(x => x.StrategyId), 160);
        Ratio(entity.Property(x => x.BenchmarkParameter));
        Text(entity.Property(x => x.CoreMasterCommitId), 64);
        entity.Property(x => x.CoreMasterObjectFormat)
            .HasMaxLength(6)
            .HasComputedColumnSql(
                "CASE WHEN length(core_master_commit_id) = 40 THEN 'sha1' WHEN length(core_master_commit_id) = 64 THEN 'sha256' ELSE 'invalid' END", stored: true);
        Hash(entity.Property(x => x.PackageSha256));
        Hash(entity.Property(x => x.EngineSha256));
        Text(entity.Property(x => x.SemanticStatus), 96);
        Text(entity.Property(x => x.R083Status), 96);
        Hash(entity.Property(x => x.OutputSha256));
        Text(entity.Property(x => x.ContractVersion), 96);
        Text(entity.Property(x => x.Classification), 64);
    }

    private static void ConfigureTargetWeight(EntityTypeBuilder<PmsShadowTargetWeightRow> entity)
    {
        entity.ToTable("target_weights", table => table.HasCheckConstraint("ck_target_weight_output_sha256", ShaCheck("output_sha256")));
        entity.HasKey(x => new { x.ModelRunId, x.InstrumentId });
        entity.HasIndex(x => x.TargetCloseUtc);
        entity.HasIndex(x => new { x.ModelRunId, x.SourceRowKey }).IsUnique();
        entity.HasIndex(x => new { x.ModelRunId, x.SourceOrder }).IsUnique();
        Restrict<PmsShadowTargetWeightRow, PmsShadowModelRunRow>(entity, x => x.ModelRunId);
        Text(entity.Property(x => x.SecurityId), 160);
        Ratio(entity.Property(x => x.Weight));
        Text(entity.Property(x => x.SourceRowKey), 256);
        Hash(entity.Property(x => x.OutputSha256));
        Text(entity.Property(x => x.LineageVersion), 96);
    }

    private static void ConfigureTargetPositionStage(EntityTypeBuilder<PmsShadowTargetPositionStageRow> entity)
    {
        entity.ToTable("target_position_stages", table => table.HasCheckConstraint("ck_target_position_stage_no_order", "NOT accounting_eligible AND NOT execution_allowed"));
        entity.HasKey(x => x.StageId);
        entity.HasIndex(x => x.ModelRunId).IsUnique();
        Restrict<PmsShadowTargetPositionStageRow, PmsShadowModelRunRow>(entity, x => x.ModelRunId);
        Restrict<PmsShadowTargetPositionStageRow, PmsShadowAccountSnapshotRow>(entity, x => x.AccountSnapshotId);
        Restrict<PmsShadowTargetPositionStageRow, PmsShadowMarketDataSnapshotRow>(entity, x => x.MarketDataSnapshotId);
        Text(entity.Property(x => x.Status), 96);
        Text(entity.Property(x => x.Classification), 64);
    }

    private static void ConfigureTargetPosition(EntityTypeBuilder<PmsShadowTargetPositionRow> entity)
    {
        entity.ToTable("target_positions");
        entity.HasKey(x => new { x.StageId, x.InstrumentId });
        entity.HasIndex(x => new { x.ModelRunId, x.InstrumentId }).IsUnique();
        Restrict<PmsShadowTargetPositionRow, PmsShadowTargetPositionStageRow>(entity, x => x.StageId);
        entity.HasOne<PmsShadowTargetWeightRow>().WithMany()
            .HasForeignKey(x => new { x.ModelRunId, x.InstrumentId })
            .OnDelete(DeleteBehavior.Restrict);
        Text(entity.Property(x => x.SecurityId), 160);
        Notional(entity.Property(x => x.TargetNotionalUsd));
        Quantity(entity.Property(x => x.TargetBaseQuantity));
        Quantity(entity.Property(x => x.TargetVenueQuantity));
        Text(entity.Property(x => x.SizingPolicy), 96);
        Text(entity.Property(x => x.RoundingPolicy), 96);
        Text(entity.Property(x => x.Status), 96);
        Text(entity.Property(x => x.Classification), 64);
    }

    private static void ConfigureDriftStage(EntityTypeBuilder<PmsShadowPositionOnlyDriftStageRow> entity)
    {
        entity.ToTable("position_only_drift_stages");
        entity.HasKey(x => x.StageId);
        entity.HasIndex(x => x.AsOfUtc);
        entity.HasIndex(x => x.ModelRunId).IsUnique();
        Restrict<PmsShadowPositionOnlyDriftStageRow, PmsShadowModelRunRow>(entity, x => x.ModelRunId);
        Restrict<PmsShadowPositionOnlyDriftStageRow, PmsShadowPositionSnapshotRow>(entity, x => x.PositionSnapshotId);
        Text(entity.Property(x => x.Status), 96);
        Text(entity.Property(x => x.Classification), 64);
    }

    private static void ConfigureDrift(EntityTypeBuilder<PmsShadowPositionOnlyDriftRow> entity)
    {
        entity.ToTable("position_only_drifts");
        entity.HasKey(x => new { x.StageId, x.InstrumentId });
        entity.HasIndex(x => new { x.ModelRunId, x.InstrumentId }).IsUnique();
        Restrict<PmsShadowPositionOnlyDriftRow, PmsShadowPositionOnlyDriftStageRow>(entity, x => x.StageId);
        entity.HasOne<PmsShadowTargetWeightRow>().WithMany()
            .HasForeignKey(x => new { x.ModelRunId, x.InstrumentId })
            .OnDelete(DeleteBehavior.Restrict);
        Text(entity.Property(x => x.SecurityId), 160);
        Quantity(entity.Property(x => x.CurrentBaseQuantity));
        Quantity(entity.Property(x => x.TargetBaseQuantity));
        Quantity(entity.Property(x => x.PositionOnlyDeltaBaseQuantity));
        Text(entity.Property(x => x.Status), 96);
    }

    private static void ConfigureBrokerStage(EntityTypeBuilder<PmsShadowBrokerAdjustedDriftStageRow> entity)
    {
        entity.ToTable("broker_adjusted_drift_stages", table =>
        {
            table.HasCheckConstraint("ck_broker_adjusted_not_calculated", "NOT calculated AND NOT empty_state_inferred");
            table.HasCheckConstraint("ck_broker_adjusted_blocker", $"blocker = '{PmsShadowStateContract.BrokerAdjustedBlocker}'");
        });
        entity.HasKey(x => x.StageId);
        entity.HasIndex(x => x.ModelRunId).IsUnique();
        Restrict<PmsShadowBrokerAdjustedDriftStageRow, PmsShadowModelRunRow>(entity, x => x.ModelRunId);
        Restrict<PmsShadowBrokerAdjustedDriftStageRow, PmsShadowWorkingLeavesObservationRow>(entity, x => x.WorkingLeavesObservationId);
        Text(entity.Property(x => x.Blocker), 160);
        Text(entity.Property(x => x.Status), 96);
        Text(entity.Property(x => x.Classification), 64);
    }

    private static void ConfigureCycleResult(EntityTypeBuilder<PmsShadowCycleResultRow> entity)
    {
        entity.ToTable("cycle_results", table =>
        {
            table.HasCheckConstraint("ck_cycle_result_no_order", "NOT execution_allowed AND not_an_order AND no_broker_route AND no_fix_message AND NOT order_entry_enabled AND trade_intent_count = 0");
            table.HasCheckConstraint("ck_cycle_result_broker_send", $"broker_send_status = '{PmsShadowStateContract.DisabledBrokerSend}'");
        });
        entity.HasKey(x => x.ResultId);
        entity.HasIndex(x => x.CompletedAtUtc);
        entity.HasIndex(x => new { x.IngestionId, x.ModelRunId }).IsUnique();
        Restrict<PmsShadowCycleResultRow, PmsShadowIngestionRow>(entity, x => x.IngestionId);
        Restrict<PmsShadowCycleResultRow, PmsShadowModelRunRow>(entity, x => x.ModelRunId);
        Text(entity.Property(x => x.ManualPaperCycleStatus), 96);
        Text(entity.Property(x => x.R009Status), 96);
        Text(entity.Property(x => x.BrokerSendStatus), 96);
        Text(entity.Property(x => x.Classification), 64);
    }

    private static void Restrict<TEntity, TPrincipal>(EntityTypeBuilder<TEntity> entity, System.Linq.Expressions.Expression<Func<TEntity, object?>> foreignKey)
        where TEntity : class where TPrincipal : class =>
        entity.HasOne<TPrincipal>().WithMany().HasForeignKey(foreignKey).OnDelete(DeleteBehavior.Restrict);

    private static void Text(PropertyBuilder<string> property, int maxLength) => property.HasMaxLength(maxLength).IsRequired();
    private static void Hash(PropertyBuilder<string> property, bool required = true)
    {
        property.HasMaxLength(64);
        if (required) property.IsRequired();
    }
    private static void OptionalHash(PropertyBuilder<string?> property) => property.HasMaxLength(64);
    private static void Ratio(PropertyBuilder<decimal> property) => property.HasPrecision(28, 12);
    private static void Price(PropertyBuilder<decimal> property) => property.HasPrecision(38, 28);
    private static void Notional(PropertyBuilder<decimal> property) => property.HasPrecision(28, 12);
    private static void Quantity(PropertyBuilder<decimal> property) => property.HasPrecision(28, 8);
    private static void Money(PropertyBuilder<decimal> property) => property.HasPrecision(28, 8);
    private static string ShaCheck(string column) => $"{column} ~ '^[0-9a-f]{{64}}$'";
    private static string GitCommitCheck(string commitColumn, string formatColumn) =>
        $"{formatColumn} IN ('{GitCommitIdentityContract.Sha1}', '{GitCommitIdentityContract.Sha256}') AND " +
        $"(({formatColumn} = '{GitCommitIdentityContract.Sha1}' AND {commitColumn} ~ '^[0-9a-f]{{40}}$') OR " +
        $"({formatColumn} = '{GitCommitIdentityContract.Sha256}' AND {commitColumn} ~ '^[0-9a-f]{{64}}$'))";

    private static void ApplySnakeCaseColumns(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.Name));
    }

    private static string ToSnakeCase(string value)
    {
        var result = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (char.IsUpper(current) && index > 0 &&
                (char.IsLower(value[index - 1]) || (index + 1 < value.Length && char.IsLower(value[index + 1]))))
                result.Append('_');
            result.Append(char.ToLowerInvariant(current));
        }
        return result.ToString();
    }
}

public sealed class PmsShadowDesignTimeDbContextFactory : IDesignTimeDbContextFactory<PmsShadowDbContext>
{
    public PmsShadowDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PmsShadowDbContext>()
            .UseNpgsql(npgsql => npgsql.SetPostgresVersion(16, 0))
            .Options;
        return new PmsShadowDbContext(options);
    }
}
