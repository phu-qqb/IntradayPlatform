using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Infrastructure.SqlServer;

public sealed class IntradayDbContext(DbContextOptions<IntradayDbContext> options) : DbContext(options)
{
    public DbSet<Fund> Funds => Set<Fund>();
    public DbSet<BrokerAccount> BrokerAccounts => Set<BrokerAccount>();
    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<InstrumentAlias> InstrumentAliases => Set<InstrumentAlias>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<VenueInstrumentMapping> VenueInstrumentMappings => Set<VenueInstrumentMapping>();
    public DbSet<NavSnapshot> NavSnapshots => Set<NavSnapshot>();
    public DbSet<ModelRun> ModelRuns => Set<ModelRun>();
    public DbSet<TargetWeight> TargetWeights => Set<TargetWeight>();
    public DbSet<ModelWeightBatch> ModelWeightBatches => Set<ModelWeightBatch>();
    public DbSet<ModelWeightRow> ModelWeightRows => Set<ModelWeightRow>();
    public DbSet<ModelWeightValidationIssue> ModelWeightValidationIssues => Set<ModelWeightValidationIssue>();
    public DbSet<TargetPosition> TargetPositions => Set<TargetPosition>();
    public DbSet<DriftSnapshot> DriftSnapshots => Set<DriftSnapshot>();
    public DbSet<MarketDataSnapshot> MarketDataSnapshots => Set<MarketDataSnapshot>();
    public DbSet<MarketDataBar> MarketDataBars => Set<MarketDataBar>();
    public DbSet<BarBuildRun> BarBuildRuns => Set<BarBuildRun>();
    public DbSet<PositionLedgerEvent> PositionLedgerEvents => Set<PositionLedgerEvent>();
    public DbSet<ReconciliationRun> ReconciliationRuns => Set<ReconciliationRun>();
    public DbSet<ReconciliationBreak> ReconciliationBreaks => Set<ReconciliationBreak>();
    public DbSet<TradeIntent> TradeIntents => Set<TradeIntent>();
    public DbSet<RiskDecision> RiskDecisions => Set<RiskDecision>();
    public DbSet<ParentOrder> ParentOrders => Set<ParentOrder>();
    public DbSet<ChildOrder> ChildOrders => Set<ChildOrder>();
    public DbSet<ExecutionReport> ExecutionReports => Set<ExecutionReport>();
    public DbSet<Fill> Fills => Set<Fill>();
    public DbSet<RiskLimitSet> RiskLimitSets => Set<RiskLimitSet>();
    public DbSet<RiskLimit> RiskLimits => Set<RiskLimit>();
    public DbSet<InstrumentRiskLimit> InstrumentRiskLimits => Set<InstrumentRiskLimit>();
    public DbSet<VenueRiskLimit> VenueRiskLimits => Set<VenueRiskLimit>();
    public DbSet<TradingWindow> TradingWindows => Set<TradingWindow>();
    public DbSet<KillSwitchState> KillSwitchStates => Set<KillSwitchState>();
    public DbSet<LmaxReportImportRun> LmaxReportImportRuns => Set<LmaxReportImportRun>();
    public DbSet<LmaxReportValidationIssue> LmaxReportValidationIssues => Set<LmaxReportValidationIssue>();
    public DbSet<LmaxIndividualTrade> LmaxIndividualTrades => Set<LmaxIndividualTrade>();
    public DbSet<LmaxTradeSummary> LmaxTradeSummaries => Set<LmaxTradeSummary>();
    public DbSet<LmaxCurrencyWallet> LmaxCurrencyWallets => Set<LmaxCurrencyWallet>();
    public DbSet<EodReconciliationRun> EodReconciliationRuns => Set<EodReconciliationRun>();
    public DbSet<EodReconciliationBreak> EodReconciliationBreaks => Set<EodReconciliationBreak>();
    public DbSet<OperatorAuditEvent> OperatorAuditEvents => Set<OperatorAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureStronglyTypedIds(modelBuilder);

        modelBuilder.Entity<Fund>().HasKey(x => x.Id);
        modelBuilder.Entity<BrokerAccount>().HasKey(x => x.Id);
        modelBuilder.Entity<Instrument>().HasKey(x => x.Id);
        modelBuilder.Entity<InstrumentAlias>().HasKey(x => x.Id);
        modelBuilder.Entity<Venue>().HasKey(x => x.Id);
        modelBuilder.Entity<VenueInstrumentMapping>().HasKey(x => x.Id);
        modelBuilder.Entity<NavSnapshot>().HasKey(nameof(NavSnapshot.FundId), nameof(NavSnapshot.AsOfUtc));
        modelBuilder.Entity<ModelRun>().HasKey(x => x.Id);
        modelBuilder.Entity<TargetWeight>().HasKey(nameof(TargetWeight.ModelRunId), nameof(TargetWeight.InstrumentId));
        modelBuilder.Entity<ModelWeightBatch>().HasKey(x => x.Id);
        modelBuilder.Entity<ModelWeightRow>().HasKey(x => x.Id);
        modelBuilder.Entity<ModelWeightValidationIssue>().HasKey(x => x.Id);
        modelBuilder.Entity<TargetPosition>().HasKey(nameof(TargetPosition.ModelRunId), nameof(TargetPosition.InstrumentId));
        modelBuilder.Entity<DriftSnapshot>().HasKey(nameof(DriftSnapshot.ModelRunId), nameof(DriftSnapshot.InstrumentId));
        modelBuilder.Entity<MarketDataSnapshot>().HasKey(x => x.Id);
        modelBuilder.Entity<MarketDataBar>().HasKey(x => x.Id);
        modelBuilder.Entity<BarBuildRun>().HasKey(x => x.Id);
        modelBuilder.Entity<PositionLedgerEvent>().HasKey(x => x.Id);
        modelBuilder.Entity<ReconciliationRun>().HasKey(x => x.Id);
        modelBuilder.Entity<ReconciliationBreak>().HasKey(x => x.Id);
        modelBuilder.Entity<TradeIntent>().HasKey(x => x.Id);
        modelBuilder.Entity<RiskDecision>().HasKey(x => x.Id);
        modelBuilder.Entity<ParentOrder>().HasKey(x => x.Id);
        modelBuilder.Entity<ChildOrder>().HasKey(x => x.Id);
        modelBuilder.Entity<ExecutionReport>().HasKey(x => x.Id);
        modelBuilder.Entity<Fill>().HasKey(x => x.Id);
        modelBuilder.Entity<RiskLimitSet>().HasKey(x => x.Id);
        modelBuilder.Entity<RiskLimit>().HasKey(x => x.Id);
        modelBuilder.Entity<InstrumentRiskLimit>().HasKey(x => x.Id);
        modelBuilder.Entity<VenueRiskLimit>().HasKey(x => x.Id);
        modelBuilder.Entity<TradingWindow>().HasKey(x => x.Id);
        modelBuilder.Entity<KillSwitchState>().HasKey(x => x.Id);
        modelBuilder.Entity<LmaxReportImportRun>().HasKey(x => x.Id);
        modelBuilder.Entity<LmaxReportValidationIssue>().HasKey(x => x.Id);
        modelBuilder.Entity<LmaxIndividualTrade>().HasKey(x => x.Id);
        modelBuilder.Entity<LmaxTradeSummary>().HasKey(x => x.Id);
        modelBuilder.Entity<LmaxCurrencyWallet>().HasKey(x => x.Id);
        modelBuilder.Entity<EodReconciliationRun>().HasKey(x => x.Id);
        modelBuilder.Entity<EodReconciliationBreak>().HasKey(x => x.Id);
        modelBuilder.Entity<OperatorAuditEvent>().HasKey(x => x.Id);

        modelBuilder.Entity<ModelRun>().HasIndex(x => x.Id).IsUnique();
        modelBuilder.Entity<Fund>().HasIndex(x => x.Name).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<BrokerAccount>().HasIndex(x => new { x.FundId, x.AccountCode }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<Instrument>().HasIndex(x => new { x.Symbol, x.AssetClass }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<InstrumentAlias>().HasIndex(x => new { x.Source, x.ExternalSymbol }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<InstrumentAlias>().HasIndex(x => new { x.Source, x.ExternalInstrumentId }).IsUnique().HasFilter("[IsEnabled] = 1 AND [ExternalInstrumentId] IS NOT NULL");
        modelBuilder.Entity<Venue>().HasIndex(x => x.Name).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<VenueInstrumentMapping>().HasIndex(x => new { x.VenueId, x.InstrumentId }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<VenueInstrumentMapping>().HasIndex(x => new { x.VenueId, x.VenueSymbol }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<RiskLimitSet>().HasIndex(x => x.FundId).IsUnique();
        modelBuilder.Entity<RiskLimit>().HasIndex(x => new { x.RiskLimitSetId, x.Name }).IsUnique();
        modelBuilder.Entity<InstrumentRiskLimit>().HasIndex(x => new { x.RiskLimitSetId, x.InstrumentId }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<VenueRiskLimit>().HasIndex(x => new { x.RiskLimitSetId, x.VenueId }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<TradingWindow>().HasIndex(x => new { x.FundId, x.ModelName, x.DayOfWeek }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<Fill>().HasIndex(x => new { x.VenueId, x.BrokerExecutionId }).IsUnique();
        modelBuilder.Entity<ParentOrder>().HasIndex(x => x.ClientOrderId).IsUnique();
        modelBuilder.Entity<ChildOrder>().HasIndex(x => x.ClientOrderId).IsUnique();
        modelBuilder.Entity<MarketDataSnapshot>().HasIndex(x => new { x.InstrumentId, x.VenueId, x.ReceivedAtUtc });
        modelBuilder.Entity<MarketDataBar>().HasIndex(x => new { x.InstrumentId, x.VenueId, x.Timeframe, x.BarStartUtc }).IsUnique();
        modelBuilder.Entity<ModelRun>().HasIndex(x => new { x.FundId, x.IsProcessed, x.ReceivedAtUtc });
        modelBuilder.Entity<PositionLedgerEvent>().HasIndex(x => new { x.FundId, x.InstrumentId, x.CreatedAtUtc });
        modelBuilder.Entity<ReconciliationBreak>().HasIndex(x => new { x.ReconciliationRunId, x.Severity, x.Status });
        modelBuilder.Entity<TradeIntent>().HasIndex(x => new { x.ModelRunId, x.InstrumentId });
        modelBuilder.Entity<ExecutionReport>().HasIndex(x => new { x.ChildOrderId, x.ReceivedAtUtc });
        modelBuilder.Entity<ModelWeightBatch>().HasIndex(x => new { x.SourceSystem, x.ExternalBatchId }).IsUnique();
        modelBuilder.Entity<ModelWeightBatch>().HasIndex(x => new { x.Status, x.AsOfUtc, x.ModelName });
        modelBuilder.Entity<ModelWeightBatch>().HasIndex(x => x.PromotedModelRunId);
        modelBuilder.Entity<ModelWeightRow>().HasIndex(x => x.BatchId);
        modelBuilder.Entity<ModelWeightRow>().HasIndex(x => new { x.BatchId, x.Symbol }).IsUnique();
        modelBuilder.Entity<ModelWeightRow>().HasIndex(x => new { x.BatchId, x.RawSecurityId }).IsUnique();
        modelBuilder.Entity<ModelWeightRow>().HasIndex(x => new { x.BatchId, x.InstrumentId }).IsUnique().HasFilter("[InstrumentId] IS NOT NULL");
        modelBuilder.Entity<ModelWeightValidationIssue>().HasIndex(x => new { x.BatchId, x.Severity, x.IssueType });
        modelBuilder.Entity<LmaxReportImportRun>().HasIndex(x => new { x.ReportDate, x.ReportType, x.VenueId, x.BrokerAccountId });
        modelBuilder.Entity<LmaxIndividualTrade>().HasIndex(x => new { x.VenueId, x.AccountId, x.ExecutionId }).IsUnique();
        modelBuilder.Entity<LmaxIndividualTrade>().HasIndex(x => new { x.VenueId, x.AccountId, x.TradeUti }).IsUnique();
        modelBuilder.Entity<LmaxIndividualTrade>().HasIndex(x => x.OrderId);
        modelBuilder.Entity<LmaxIndividualTrade>().HasIndex(x => x.InstructionId);
        modelBuilder.Entity<LmaxIndividualTrade>().HasIndex(x => new { x.InstrumentId, x.ReportDate });
        modelBuilder.Entity<LmaxTradeSummary>().HasIndex(x => new { x.InstrumentId, x.ReportDate });
        modelBuilder.Entity<LmaxCurrencyWallet>().HasIndex(x => new { x.ReportDate, x.BrokerAccountId });
        modelBuilder.Entity<LmaxCurrencyWallet>().HasIndex(x => new { x.ReportDate, x.VenueId, x.BrokerAccountId, x.Currency }).IsUnique();
        modelBuilder.Entity<OperatorAuditEvent>().HasIndex(x => x.OccurredAtUtc);
        modelBuilder.Entity<OperatorAuditEvent>().HasIndex(x => x.EventType);
        modelBuilder.Entity<OperatorAuditEvent>().HasIndex(x => new { x.EntityType, x.EntityId });
        modelBuilder.Entity<OperatorAuditEvent>().HasIndex(x => x.CorrelationId);
        modelBuilder.Entity<OperatorAuditEvent>().HasIndex(x => x.Severity);

        modelBuilder.Entity<BrokerAccount>().HasOne<Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<NavSnapshot>().HasOne<Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ModelRun>().HasOne<Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InstrumentAlias>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TargetWeight>().HasOne<ModelRun>().WithMany().HasForeignKey(x => x.ModelRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TargetWeight>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ModelWeightBatch>().HasOne<Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ModelWeightBatch>().HasOne<ModelRun>().WithMany().HasForeignKey(x => x.PromotedModelRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ModelWeightRow>().HasOne<ModelWeightBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ModelWeightRow>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ModelWeightValidationIssue>().HasOne<ModelWeightBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ModelWeightValidationIssue>().HasOne<ModelWeightRow>().WithMany().HasForeignKey(x => x.RowId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TargetPosition>().HasOne<ModelRun>().WithMany().HasForeignKey(x => x.ModelRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TargetPosition>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DriftSnapshot>().HasOne<ModelRun>().WithMany().HasForeignKey(x => x.ModelRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DriftSnapshot>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VenueInstrumentMapping>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VenueInstrumentMapping>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MarketDataSnapshot>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MarketDataSnapshot>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MarketDataBar>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MarketDataBar>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PositionLedgerEvent>().HasOne<Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PositionLedgerEvent>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ReconciliationRun>().HasOne<ModelRun>().WithMany().HasForeignKey(x => x.ModelRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ReconciliationBreak>().HasOne<ReconciliationRun>().WithMany().HasForeignKey(x => x.ReconciliationRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TradeIntent>().HasOne<ModelRun>().WithMany().HasForeignKey(x => x.ModelRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TradeIntent>().HasOne<Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TradeIntent>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RiskDecision>().HasOne<TradeIntent>().WithMany().HasForeignKey(x => x.TradeIntentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ParentOrder>().HasOne<TradeIntent>().WithMany().HasForeignKey(x => x.TradeIntentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChildOrder>().HasOne<ParentOrder>().WithMany().HasForeignKey(x => x.ParentOrderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChildOrder>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ExecutionReport>().HasOne<ChildOrder>().WithMany().HasForeignKey(x => x.ChildOrderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ExecutionReport>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Fill>().HasOne<ChildOrder>().WithMany().HasForeignKey(x => x.ChildOrderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Fill>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Fill>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RiskLimitSet>().HasOne<Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InstrumentRiskLimit>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VenueRiskLimit>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TradingWindow>().HasOne<Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LmaxReportImportRun>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LmaxReportImportRun>().HasOne<BrokerAccount>().WithMany().HasForeignKey(x => x.BrokerAccountId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LmaxReportValidationIssue>().HasOne<LmaxReportImportRun>().WithMany().HasForeignKey(x => x.ImportRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LmaxIndividualTrade>().HasOne<LmaxReportImportRun>().WithMany().HasForeignKey(x => x.ImportRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LmaxIndividualTrade>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LmaxIndividualTrade>().HasOne<BrokerAccount>().WithMany().HasForeignKey(x => x.BrokerAccountId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LmaxIndividualTrade>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LmaxTradeSummary>().HasOne<LmaxReportImportRun>().WithMany().HasForeignKey(x => x.ImportRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LmaxTradeSummary>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LmaxTradeSummary>().HasOne<BrokerAccount>().WithMany().HasForeignKey(x => x.BrokerAccountId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LmaxTradeSummary>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LmaxCurrencyWallet>().HasOne<LmaxReportImportRun>().WithMany().HasForeignKey(x => x.ImportRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LmaxCurrencyWallet>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<LmaxCurrencyWallet>().HasOne<BrokerAccount>().WithMany().HasForeignKey(x => x.BrokerAccountId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EodReconciliationRun>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EodReconciliationRun>().HasOne<BrokerAccount>().WithMany().HasForeignKey(x => x.BrokerAccountId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EodReconciliationBreak>().HasOne<EodReconciliationRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<EodReconciliationBreak>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OperatorAuditEvent>().Property(x => x.ActorId).HasMaxLength(128);
        modelBuilder.Entity<OperatorAuditEvent>().Property(x => x.ActorDisplayName).HasMaxLength(256);
        modelBuilder.Entity<OperatorAuditEvent>().Property(x => x.EntityType).HasMaxLength(128);
        modelBuilder.Entity<OperatorAuditEvent>().Property(x => x.EntityId).HasMaxLength(128);
        modelBuilder.Entity<OperatorAuditEvent>().Property(x => x.CorrelationId).HasMaxLength(128);
        modelBuilder.Entity<OperatorAuditEvent>().Property(x => x.CausationId).HasMaxLength(128);
        modelBuilder.Entity<OperatorAuditEvent>().Property(x => x.RequestId).HasMaxLength(128);
        modelBuilder.Entity<OperatorAuditEvent>().Property(x => x.Source).HasMaxLength(128);
        modelBuilder.Entity<OperatorAuditEvent>().Property(x => x.Description).HasMaxLength(1000);
        modelBuilder.Entity<OperatorAuditEvent>().Property(x => x.Reason).HasMaxLength(1000);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties().Where(x => x.ClrType == typeof(decimal) || x.ClrType == typeof(decimal?)))
            {
                property.SetPrecision(28);
                property.SetScale(10);
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }

        modelBuilder.Entity<Fund>().Property(x => x.BaseCurrency).HasConversion(x => x.Code, x => new Currency(x)).HasMaxLength(3);
        modelBuilder.Entity<Instrument>().Property(x => x.BaseCurrency).HasConversion(x => x.Code, x => new Currency(x)).HasMaxLength(3);
        modelBuilder.Entity<Instrument>().Property(x => x.QuoteCurrency).HasConversion(x => x.Code, x => new Currency(x)).HasMaxLength(3);
    }

    private static void ConfigureStronglyTypedIds(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Fund>().Property(x => x.Id).HasConversion(x => x.Value, x => new FundId(x));
        modelBuilder.Entity<BrokerAccount>().Property(x => x.Id).HasConversion(x => x.Value, x => new BrokerAccountId(x));
        modelBuilder.Entity<BrokerAccount>().Property(x => x.FundId).HasConversion(x => x.Value, x => new FundId(x));
        modelBuilder.Entity<Instrument>().Property(x => x.Id).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<InstrumentAlias>().Property(x => x.Id).HasConversion(x => x.Value, x => new InstrumentAliasId(x));
        modelBuilder.Entity<InstrumentAlias>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<Venue>().Property(x => x.Id).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<VenueInstrumentMapping>().Property(x => x.Id).HasConversion(x => x.Value, x => new VenueInstrumentId(x));
        modelBuilder.Entity<VenueInstrumentMapping>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<VenueInstrumentMapping>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<NavSnapshot>().Property(x => x.FundId).HasConversion(x => x.Value, x => new FundId(x));
        modelBuilder.Entity<ModelRun>().Property(x => x.Id).HasConversion(x => x.Value, x => new ModelRunId(x));
        modelBuilder.Entity<ModelRun>().Property(x => x.FundId).HasConversion(x => x.Value, x => new FundId(x));
        modelBuilder.Entity<TargetWeight>().Property(x => x.ModelRunId).HasConversion(x => x.Value, x => new ModelRunId(x));
        modelBuilder.Entity<TargetWeight>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<ModelWeightBatch>().Property(x => x.Id).HasConversion(x => x.Value, x => new ModelWeightBatchId(x));
        modelBuilder.Entity<ModelWeightBatch>().Property(x => x.FundId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new FundId(x.Value) : null);
        modelBuilder.Entity<ModelWeightBatch>().Property(x => x.PromotedModelRunId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new ModelRunId(x.Value) : null);
        modelBuilder.Entity<ModelWeightRow>().Property(x => x.Id).HasConversion(x => x.Value, x => new ModelWeightRowId(x));
        modelBuilder.Entity<ModelWeightRow>().Property(x => x.BatchId).HasConversion(x => x.Value, x => new ModelWeightBatchId(x));
        modelBuilder.Entity<ModelWeightRow>().Property(x => x.InstrumentId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new InstrumentId(x.Value) : null);
        modelBuilder.Entity<ModelWeightValidationIssue>().Property(x => x.BatchId).HasConversion(x => x.Value, x => new ModelWeightBatchId(x));
        modelBuilder.Entity<ModelWeightValidationIssue>().Property(x => x.RowId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new ModelWeightRowId(x.Value) : null);
        modelBuilder.Entity<TargetPosition>().Property(x => x.ModelRunId).HasConversion(x => x.Value, x => new ModelRunId(x));
        modelBuilder.Entity<TargetPosition>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<DriftSnapshot>().Property(x => x.ModelRunId).HasConversion(x => x.Value, x => new ModelRunId(x));
        modelBuilder.Entity<DriftSnapshot>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<MarketDataSnapshot>().Property(x => x.Id).HasConversion(x => x.Value, x => new MarketDataSnapshotId(x));
        modelBuilder.Entity<MarketDataSnapshot>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<MarketDataSnapshot>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<MarketDataBar>().Property(x => x.Id).HasConversion(x => x.Value, x => new MarketDataBarId(x));
        modelBuilder.Entity<MarketDataBar>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<MarketDataBar>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<MarketDataBar>().Property(x => x.BuildRunId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new BarBuildRunId(x.Value) : null);
        modelBuilder.Entity<BarBuildRun>().Property(x => x.Id).HasConversion(x => x.Value, x => new BarBuildRunId(x));
        modelBuilder.Entity<PositionLedgerEvent>().Property(x => x.FundId).HasConversion(x => x.Value, x => new FundId(x));
        modelBuilder.Entity<PositionLedgerEvent>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<ReconciliationRun>().Property(x => x.ModelRunId).HasConversion(x => x.Value, x => new ModelRunId(x));
        modelBuilder.Entity<ReconciliationBreak>().Property(x => x.InstrumentId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new InstrumentId(x.Value) : null);
        modelBuilder.Entity<TradeIntent>().Property(x => x.Id).HasConversion(x => x.Value, x => new TradeIntentId(x));
        modelBuilder.Entity<TradeIntent>().Property(x => x.ModelRunId).HasConversion(x => x.Value, x => new ModelRunId(x));
        modelBuilder.Entity<TradeIntent>().Property(x => x.FundId).HasConversion(x => x.Value, x => new FundId(x));
        modelBuilder.Entity<TradeIntent>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<RiskDecision>().Property(x => x.TradeIntentId).HasConversion(x => x.Value, x => new TradeIntentId(x));
        modelBuilder.Entity<ParentOrder>().Property(x => x.Id).HasConversion(x => x.Value, x => new ParentOrderId(x));
        modelBuilder.Entity<ParentOrder>().Property(x => x.TradeIntentId).HasConversion(x => x.Value, x => new TradeIntentId(x));
        modelBuilder.Entity<ParentOrder>().Property(x => x.ClientOrderId).HasConversion(x => x.Value, x => new ClientOrderId(x));
        modelBuilder.Entity<ChildOrder>().Property(x => x.Id).HasConversion(x => x.Value, x => new ChildOrderId(x));
        modelBuilder.Entity<ChildOrder>().Property(x => x.ParentOrderId).HasConversion(x => x.Value, x => new ParentOrderId(x));
        modelBuilder.Entity<ChildOrder>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<ChildOrder>().Property(x => x.ClientOrderId).HasConversion(x => x.Value, x => new ClientOrderId(x));
        modelBuilder.Entity<ExecutionReport>().Property(x => x.Id).HasConversion(x => x.Value, x => new ExecutionReportId(x));
        modelBuilder.Entity<ExecutionReport>().Property(x => x.ChildOrderId).HasConversion(x => x.Value, x => new ChildOrderId(x));
        modelBuilder.Entity<ExecutionReport>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<ExecutionReport>().Property(x => x.ClientOrderId).HasConversion(x => x.Value, x => new ClientOrderId(x));
        modelBuilder.Entity<Fill>().Property(x => x.Id).HasConversion(x => x.Value, x => new FillId(x));
        modelBuilder.Entity<Fill>().Property(x => x.ChildOrderId).HasConversion(x => x.Value, x => new ChildOrderId(x));
        modelBuilder.Entity<Fill>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<Fill>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<RiskLimitSet>().Property(x => x.FundId).HasConversion(x => x.Value, x => new FundId(x));
        modelBuilder.Entity<RiskLimitSet>().Property(x => x.MaxModelRunAge).HasConversion(x => x.Ticks, x => TimeSpan.FromTicks(x));
        modelBuilder.Entity<RiskLimitSet>().Property(x => x.MaxMarketDataAge).HasConversion(x => x.Ticks, x => TimeSpan.FromTicks(x));
        modelBuilder.Entity<InstrumentRiskLimit>().Property(x => x.InstrumentId).HasConversion(x => x.Value, x => new InstrumentId(x));
        modelBuilder.Entity<VenueRiskLimit>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<TradingWindow>().Property(x => x.FundId).HasConversion(x => x.Value, x => new FundId(x));
        modelBuilder.Entity<LmaxReportImportRun>().Property(x => x.Id).HasConversion(x => x.Value, x => new LmaxReportImportRunId(x));
        modelBuilder.Entity<LmaxReportImportRun>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<LmaxReportImportRun>().Property(x => x.BrokerAccountId).HasConversion(x => x.Value, x => new BrokerAccountId(x));
        modelBuilder.Entity<LmaxReportValidationIssue>().Property(x => x.ImportRunId).HasConversion(x => x.Value, x => new LmaxReportImportRunId(x));
        modelBuilder.Entity<LmaxIndividualTrade>().Property(x => x.Id).HasConversion(x => x.Value, x => new LmaxIndividualTradeId(x));
        modelBuilder.Entity<LmaxIndividualTrade>().Property(x => x.ImportRunId).HasConversion(x => x.Value, x => new LmaxReportImportRunId(x));
        modelBuilder.Entity<LmaxIndividualTrade>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<LmaxIndividualTrade>().Property(x => x.BrokerAccountId).HasConversion(x => x.Value, x => new BrokerAccountId(x));
        modelBuilder.Entity<LmaxIndividualTrade>().Property(x => x.InstrumentId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new InstrumentId(x.Value) : null);
        modelBuilder.Entity<LmaxTradeSummary>().Property(x => x.Id).HasConversion(x => x.Value, x => new LmaxTradeSummaryId(x));
        modelBuilder.Entity<LmaxTradeSummary>().Property(x => x.ImportRunId).HasConversion(x => x.Value, x => new LmaxReportImportRunId(x));
        modelBuilder.Entity<LmaxTradeSummary>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<LmaxTradeSummary>().Property(x => x.BrokerAccountId).HasConversion(x => x.Value, x => new BrokerAccountId(x));
        modelBuilder.Entity<LmaxTradeSummary>().Property(x => x.InstrumentId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new InstrumentId(x.Value) : null);
        modelBuilder.Entity<LmaxCurrencyWallet>().Property(x => x.Id).HasConversion(x => x.Value, x => new LmaxCurrencyWalletId(x));
        modelBuilder.Entity<LmaxCurrencyWallet>().Property(x => x.ImportRunId).HasConversion(x => x.Value, x => new LmaxReportImportRunId(x));
        modelBuilder.Entity<LmaxCurrencyWallet>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<LmaxCurrencyWallet>().Property(x => x.BrokerAccountId).HasConversion(x => x.Value, x => new BrokerAccountId(x));
        modelBuilder.Entity<EodReconciliationRun>().Property(x => x.VenueId).HasConversion(x => x.Value, x => new VenueId(x));
        modelBuilder.Entity<EodReconciliationRun>().Property(x => x.BrokerAccountId).HasConversion(x => x.Value, x => new BrokerAccountId(x));
        modelBuilder.Entity<EodReconciliationBreak>().Property(x => x.InstrumentId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new InstrumentId(x.Value) : null);
        modelBuilder.Entity<OperatorAuditEvent>().Property(x => x.Id).HasConversion(x => x.Value, x => new OperatorAuditEventId(x));
    }
}

public sealed class SqlServerOperatorAuditRepository(IntradayDbContext dbContext) : IOperatorAuditRepository
{
    public async Task AddAsync(OperatorAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        dbContext.OperatorAuditEvents.Add(auditEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<OperatorAuditEvent?> GetAsync(OperatorAuditEventId id, CancellationToken cancellationToken)
        => dbContext.OperatorAuditEvents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<OperatorAuditEvent>> GetRecentAsync(OperatorAuditEventFilter filter, CancellationToken cancellationToken)
    {
        var query = dbContext.OperatorAuditEvents.AsNoTracking().AsQueryable();
        if (filter.Severity is not null) query = query.Where(x => x.Severity == filter.Severity.Value);
        if (filter.EventType is not null) query = query.Where(x => x.EventType == filter.EventType.Value);
        if (!string.IsNullOrWhiteSpace(filter.EntityType)) query = query.Where(x => x.EntityType == filter.EntityType);
        if (!string.IsNullOrWhiteSpace(filter.EntityId)) query = query.Where(x => x.EntityId == filter.EntityId);
        if (!string.IsNullOrWhiteSpace(filter.CorrelationId)) query = query.Where(x => x.CorrelationId == filter.CorrelationId);
        if (filter.FromUtc is not null) query = query.Where(x => x.OccurredAtUtc >= filter.FromUtc);
        if (filter.ToUtc is not null) query = query.Where(x => x.OccurredAtUtc <= filter.ToUtc);
        return await query.OrderByDescending(x => x.OccurredAtUtc).Take(Math.Clamp(filter.Limit, 1, 500)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OperatorAuditEvent>> GetByEntityAsync(string entityType, string entityId, int limit, CancellationToken cancellationToken)
        => await dbContext.OperatorAuditEvents.AsNoTracking()
            .Where(x => x.EntityType == entityType && x.EntityId == entityId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OperatorAuditEvent>> GetByCorrelationIdAsync(string correlationId, int limit, CancellationToken cancellationToken)
        => await dbContext.OperatorAuditEvents.AsNoTracking()
            .Where(x => x.CorrelationId == correlationId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
}

public sealed class SqlServerIntradayRepository(IntradayDbContext dbContext) : IIntradayRepository
{
    public async Task<PlatformState> LoadStateAsync(CancellationToken cancellationToken)
    {
        var state = new PlatformState();
        state.Funds.AddRange(await dbContext.Funds.AsNoTracking().ToListAsync(cancellationToken));
        state.BrokerAccounts.AddRange(await dbContext.BrokerAccounts.AsNoTracking().ToListAsync(cancellationToken));
        state.Instruments.AddRange(await dbContext.Instruments.AsNoTracking().ToListAsync(cancellationToken));
        state.InstrumentAliases.AddRange(await dbContext.InstrumentAliases.AsNoTracking().ToListAsync(cancellationToken));
        state.Venues.AddRange(await dbContext.Venues.AsNoTracking().ToListAsync(cancellationToken));
        state.VenueInstrumentMappings.AddRange(await dbContext.VenueInstrumentMappings.AsNoTracking().ToListAsync(cancellationToken));
        state.NavSnapshots.AddRange(await dbContext.Set<NavSnapshot>().AsNoTracking().ToListAsync(cancellationToken));
        state.ModelRuns.AddRange(await dbContext.ModelRuns.AsNoTracking().ToListAsync(cancellationToken));
        state.TargetWeights.AddRange(await dbContext.TargetWeights.AsNoTracking().ToListAsync(cancellationToken));
        state.ModelWeightBatches.AddRange(await dbContext.ModelWeightBatches.AsNoTracking().ToListAsync(cancellationToken));
        state.ModelWeightRows.AddRange(await dbContext.ModelWeightRows.AsNoTracking().ToListAsync(cancellationToken));
        state.ModelWeightValidationIssues.AddRange(await dbContext.ModelWeightValidationIssues.AsNoTracking().ToListAsync(cancellationToken));
        state.TargetPositions.AddRange(await dbContext.TargetPositions.AsNoTracking().ToListAsync(cancellationToken));
        state.DriftSnapshots.AddRange(await dbContext.DriftSnapshots.AsNoTracking().ToListAsync(cancellationToken));
        state.MarketData.AddRange(await dbContext.MarketDataSnapshots.AsNoTracking().ToListAsync(cancellationToken));
        state.MarketDataBars.AddRange(await dbContext.MarketDataBars.AsNoTracking().ToListAsync(cancellationToken));
        state.BarBuildRuns.AddRange(await dbContext.BarBuildRuns.AsNoTracking().ToListAsync(cancellationToken));
        state.PositionLedger.AddRange(await dbContext.PositionLedgerEvents.AsNoTracking().ToListAsync(cancellationToken));
        state.ReconciliationRuns.AddRange(await dbContext.ReconciliationRuns.AsNoTracking().ToListAsync(cancellationToken));
        state.ReconciliationBreaks.AddRange(await dbContext.ReconciliationBreaks.AsNoTracking().ToListAsync(cancellationToken));
        state.TradeIntents.AddRange(await dbContext.TradeIntents.AsNoTracking().ToListAsync(cancellationToken));
        state.RiskDecisions.AddRange(await dbContext.RiskDecisions.AsNoTracking().ToListAsync(cancellationToken));
        state.ParentOrders.AddRange(await dbContext.ParentOrders.AsNoTracking().ToListAsync(cancellationToken));
        state.ChildOrders.AddRange(await dbContext.ChildOrders.AsNoTracking().ToListAsync(cancellationToken));
        state.ExecutionReports.AddRange(await dbContext.ExecutionReports.AsNoTracking().ToListAsync(cancellationToken));
        state.Fills.AddRange(await dbContext.Fills.AsNoTracking().ToListAsync(cancellationToken));
        state.RiskLimitSets.AddRange(await dbContext.RiskLimitSets.AsNoTracking().ToListAsync(cancellationToken));
        state.RiskLimits.AddRange(await dbContext.RiskLimits.AsNoTracking().ToListAsync(cancellationToken));
        state.InstrumentRiskLimits.AddRange(await dbContext.InstrumentRiskLimits.AsNoTracking().ToListAsync(cancellationToken));
        state.VenueRiskLimits.AddRange(await dbContext.VenueRiskLimits.AsNoTracking().ToListAsync(cancellationToken));
        state.TradingWindows.AddRange(await dbContext.TradingWindows.AsNoTracking().ToListAsync(cancellationToken));
        state.KillSwitchStates.AddRange(await dbContext.KillSwitchStates.AsNoTracking().ToListAsync(cancellationToken));
        state.LmaxReportImportRuns.AddRange(await dbContext.LmaxReportImportRuns.AsNoTracking().ToListAsync(cancellationToken));
        state.LmaxReportValidationIssues.AddRange(await dbContext.LmaxReportValidationIssues.AsNoTracking().ToListAsync(cancellationToken));
        state.LmaxIndividualTrades.AddRange(await dbContext.LmaxIndividualTrades.AsNoTracking().ToListAsync(cancellationToken));
        state.LmaxTradeSummaries.AddRange(await dbContext.LmaxTradeSummaries.AsNoTracking().ToListAsync(cancellationToken));
        state.LmaxCurrencyWallets.AddRange(await dbContext.LmaxCurrencyWallets.AsNoTracking().ToListAsync(cancellationToken));
        state.EodReconciliationRuns.AddRange(await dbContext.EodReconciliationRuns.AsNoTracking().ToListAsync(cancellationToken));
        state.EodReconciliationBreaks.AddRange(await dbContext.EodReconciliationBreaks.AsNoTracking().ToListAsync(cancellationToken));
        state.OperatorAuditEvents.AddRange(await dbContext.OperatorAuditEvents.AsNoTracking().ToListAsync(cancellationToken));
        state.KillSwitch = state.KillSwitchStates.OrderByDescending(x => x.UpdatedAtUtc).FirstOrDefault() ?? state.KillSwitch;
        return state;
    }

    public Task<ModelRun?> GetNextUnprocessedModelRunAsync(CancellationToken cancellationToken)
        => dbContext.ModelRuns.OrderBy(x => x.ReceivedAtUtc).FirstOrDefaultAsync(x => !x.IsProcessed, cancellationToken);

    public Task<ModelRun?> GetModelRunAsync(ModelRunId modelRunId, CancellationToken cancellationToken)
        => dbContext.ModelRuns.FirstOrDefaultAsync(x => x.Id == modelRunId, cancellationToken);

    public async Task AddModelRunAsync(ModelRun modelRun, IReadOnlyList<TargetWeight> weights, CancellationToken cancellationToken)
    {
        if (await dbContext.ModelRuns.AnyAsync(x => x.Id == modelRun.Id, cancellationToken))
        {
            return;
        }

        dbContext.ModelRuns.Add(modelRun);
        dbContext.TargetWeights.AddRange(weights);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkModelRunProcessedAsync(ModelRunId modelRunId, ModelRunStatus status, CancellationToken cancellationToken)
    {
        var run = await dbContext.ModelRuns.FirstAsync(x => x.Id == modelRunId, cancellationToken);
        dbContext.Entry(run).CurrentValues.SetValues(run with { IsProcessed = true, Status = status });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveReconciliationAsync(ReconciliationRun run, IReadOnlyList<ReconciliationBreak> breaks, CancellationToken cancellationToken)
    {
        if (await dbContext.ReconciliationRuns.AnyAsync(x => x.ModelRunId == run.ModelRunId && x.Phase == run.Phase, cancellationToken))
        {
            return;
        }

        dbContext.ReconciliationRuns.Add(run);
        dbContext.ReconciliationBreaks.AddRange(breaks);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveTargetAndDriftAsync(TargetPosition targetPosition, DriftSnapshot driftSnapshot, CancellationToken cancellationToken)
    {
        if (!await dbContext.TargetPositions.AnyAsync(x => x.ModelRunId == targetPosition.ModelRunId && x.InstrumentId == targetPosition.InstrumentId, cancellationToken))
        {
            dbContext.TargetPositions.Add(targetPosition);
        }

        if (!await dbContext.DriftSnapshots.AnyAsync(x => x.ModelRunId == driftSnapshot.ModelRunId && x.InstrumentId == driftSnapshot.InstrumentId, cancellationToken))
        {
            dbContext.DriftSnapshots.Add(driftSnapshot);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddTradeIntentAsync(TradeIntent intent, CancellationToken cancellationToken)
    {
        dbContext.TradeIntents.Add(intent);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRiskDecisionAsync(RiskDecision decision, CancellationToken cancellationToken)
    {
        if (await dbContext.RiskDecisions.AnyAsync(x => x.TradeIntentId == decision.TradeIntentId, cancellationToken))
        {
            return;
        }

        dbContext.RiskDecisions.Add(decision);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddOrdersAsync(ParentOrder parentOrder, ChildOrder childOrder, CancellationToken cancellationToken)
    {
        dbContext.ParentOrders.Add(parentOrder);
        dbContext.ChildOrders.Add(childOrder);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddExecutionReportAsync(ExecutionReport report, CancellationToken cancellationToken)
    {
        if (await dbContext.ExecutionReports.AnyAsync(x => x.Id == report.Id, cancellationToken))
        {
            return;
        }

        dbContext.ExecutionReports.Add(report);
        var child = await dbContext.ChildOrders.FirstOrDefaultAsync(x => x.Id == report.ChildOrderId, cancellationToken);
        if (child is not null)
        {
            var childStatus = new OrderStateMachine().Transition(child.Status, report.ExecutionReportType);
            dbContext.Entry(child).CurrentValues.SetValues(child with { Status = childStatus });

            var parent = await dbContext.ParentOrders.FirstOrDefaultAsync(x => x.Id == child.ParentOrderId, cancellationToken);
            if (parent is not null)
            {
                var parentStatus = report.ExecutionReportType switch
                {
                    ExecutionReportType.OrderReject => OrderStatus.Rejected,
                    ExecutionReportType.Fill => OrderStatus.Filled,
                    ExecutionReportType.PartialFill => OrderStatus.PartiallyFilled,
                    ExecutionReportType.Expired when parent.Status == OrderStatus.PartiallyFilled => OrderStatus.PartiallyFilled,
                    ExecutionReportType.Expired => OrderStatus.Expired,
                    _ => parent.Status
                };
                dbContext.Entry(parent).CurrentValues.SetValues(parent with { Status = parentStatus });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryAddFillAsync(Fill fill, CancellationToken cancellationToken)
    {
        if (await dbContext.Fills.AnyAsync(x => x.VenueId == fill.VenueId && x.BrokerExecutionId == fill.BrokerExecutionId, cancellationToken))
        {
            return false;
        }

        dbContext.Fills.Add(fill);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task AddPositionLedgerEventAsync(PositionLedgerEvent ledgerEvent, CancellationToken cancellationToken)
    {
        dbContext.PositionLedgerEvents.Add(ledgerEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetKillSwitchAsync(bool isActive, string? reason, CancellationToken cancellationToken)
    {
        dbContext.KillSwitchStates.Add(new KillSwitchState(Guid.NewGuid(), isActive, reason, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SqlServerMarketDataSnapshotRepository(IntradayDbContext dbContext) : IMarketDataSnapshotRepository
{
    public async Task AddAsync(MarketDataSnapshot snapshot, CancellationToken cancellationToken)
    {
        snapshot.Validate();
        dbContext.MarketDataSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IReadOnlyList<MarketDataSnapshot> snapshots, CancellationToken cancellationToken)
    {
        foreach (var snapshot in snapshots)
        {
            snapshot.Validate();
        }

        dbContext.MarketDataSnapshots.AddRange(snapshots);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<MarketDataSnapshot?> GetLatestAsync(InstrumentId instrumentId, VenueId venueId, CancellationToken cancellationToken)
        => dbContext.MarketDataSnapshots.AsNoTracking().Where(x => x.InstrumentId == instrumentId && x.VenueId == venueId).OrderByDescending(x => x.SourceTimestampUtc).ThenByDescending(x => x.ReceivedAtUtc).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<MarketDataSnapshot>> GetRangeAsync(InstrumentId instrumentId, VenueId venueId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
        => await dbContext.MarketDataSnapshots.AsNoTracking()
            .Where(x => x.InstrumentId == instrumentId && x.VenueId == venueId && x.SourceTimestampUtc >= startUtc && x.SourceTimestampUtc < endUtc)
            .OrderBy(x => x.SourceTimestampUtc)
            .ToListAsync(cancellationToken);
}

public sealed class SqlServerMarketDataBarRepository(IntradayDbContext dbContext) : IMarketDataBarRepository
{
    public async Task<BarUpsertResult> UpsertAsync(MarketDataBar bar, CancellationToken cancellationToken)
    {
        bar.Validate();
        var existing = await dbContext.MarketDataBars.FirstOrDefaultAsync(x => x.InstrumentId == bar.InstrumentId && x.VenueId == bar.VenueId && x.Timeframe == bar.Timeframe && x.BarStartUtc == bar.BarStartUtc, cancellationToken);
        if (existing is null)
        {
            dbContext.MarketDataBars.Add(bar);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new BarUpsertResult(true);
        }

        dbContext.Entry(existing).CurrentValues.SetValues(bar with { Id = existing.Id });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new BarUpsertResult(false);
    }

    public Task<MarketDataBar?> GetAsync(InstrumentId instrumentId, VenueId venueId, BarTimeframe timeframe, DateTimeOffset barStartUtc, CancellationToken cancellationToken)
        => dbContext.MarketDataBars.AsNoTracking().FirstOrDefaultAsync(x => x.InstrumentId == instrumentId && x.VenueId == venueId && x.Timeframe == timeframe && x.BarStartUtc == barStartUtc, cancellationToken);

    public async Task<IReadOnlyList<MarketDataBar>> GetRangeAsync(InstrumentId instrumentId, VenueId venueId, BarTimeframe timeframe, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
        => await dbContext.MarketDataBars.AsNoTracking()
            .Where(x => x.InstrumentId == instrumentId && x.VenueId == venueId && x.Timeframe == timeframe && x.BarStartUtc >= startUtc && x.BarStartUtc < endUtc)
            .OrderBy(x => x.BarStartUtc)
            .ToListAsync(cancellationToken);
}

public sealed class SqlServerBarBuildRunRepository(IntradayDbContext dbContext, IClock clock) : IBarBuildRunRepository
{
    public async Task AddAsync(BarBuildRun run, CancellationToken cancellationToken)
    {
        dbContext.BarBuildRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkCompletedAsync(BarBuildRunId runId, int barsCreated, int barsUpdated, CancellationToken cancellationToken)
    {
        var run = await dbContext.BarBuildRuns.FirstAsync(x => x.Id == runId, cancellationToken);
        dbContext.Entry(run).CurrentValues.SetValues(run with { Status = BarBuildRunStatus.Completed, CompletedAtUtc = clock.UtcNow, BarsCreated = barsCreated, BarsUpdated = barsUpdated });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(BarBuildRunId runId, string errorMessage, CancellationToken cancellationToken)
    {
        var run = await dbContext.BarBuildRuns.FirstAsync(x => x.Id == runId, cancellationToken);
        dbContext.Entry(run).CurrentValues.SetValues(run with { Status = BarBuildRunStatus.Failed, CompletedAtUtc = clock.UtcNow, ErrorMessage = errorMessage });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SqlServerModelWeightBatchRepository(IntradayDbContext dbContext) : IModelWeightBatchRepository
{
    public Task<ModelWeightBatch?> GetBatchAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken)
        => dbContext.ModelWeightBatches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == batchId, cancellationToken);

    public Task<ModelWeightBatch?> GetBatchByExternalIdAsync(ModelWeightSourceSystem sourceSystem, string externalBatchId, CancellationToken cancellationToken)
        => dbContext.ModelWeightBatches.AsNoTracking().FirstOrDefaultAsync(x => x.SourceSystem == sourceSystem && x.ExternalBatchId == externalBatchId, cancellationToken);

    public async Task<IReadOnlyList<ModelWeightBatch>> GetRecentBatchesAsync(int limit, ModelWeightBatchStatus? status, ModelWeightSourceSystem? sourceSystem, string? modelName, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken cancellationToken)
    {
        var query = dbContext.ModelWeightBatches.AsNoTracking().AsQueryable();
        if (status is not null) query = query.Where(x => x.Status == status);
        if (sourceSystem is not null) query = query.Where(x => x.SourceSystem == sourceSystem);
        if (!string.IsNullOrWhiteSpace(modelName)) query = query.Where(x => x.ModelName == modelName);
        if (fromUtc is not null) query = query.Where(x => x.AsOfUtc >= fromUtc.Value);
        if (toUtc is not null) query = query.Where(x => x.AsOfUtc < toUtc.Value);
        return await query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(limit, 1, 500)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ModelWeightBatch>> GetReadyBatchesAsync(int limit, CancellationToken cancellationToken)
        => await dbContext.ModelWeightBatches.AsNoTracking()
            .Where(x => x.Status == ModelWeightBatchStatus.Ready || x.Status == ModelWeightBatchStatus.Accepted)
            .OrderBy(x => x.AsOfUtc)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ModelWeightRow>> GetRowsAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken)
        => await dbContext.ModelWeightRows.AsNoTracking().Where(x => x.BatchId == batchId).OrderBy(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ModelWeightValidationIssue>> GetValidationIssuesAsync(ModelWeightBatchId batchId, CancellationToken cancellationToken)
        => await dbContext.ModelWeightValidationIssues.AsNoTracking().Where(x => x.BatchId == batchId).OrderBy(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public async Task AddBatchAsync(ModelWeightBatch batch, IReadOnlyList<ModelWeightRow> rows, CancellationToken cancellationToken)
    {
        if (await dbContext.ModelWeightBatches.AnyAsync(x => x.SourceSystem == batch.SourceSystem && x.ExternalBatchId == batch.ExternalBatchId, cancellationToken))
        {
            return;
        }

        dbContext.ModelWeightBatches.Add(batch);
        dbContext.ModelWeightRows.AddRange(rows);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateBatchAsync(ModelWeightBatch batch, CancellationToken cancellationToken)
    {
        var existing = await dbContext.ModelWeightBatches.FirstOrDefaultAsync(x => x.Id == batch.Id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        dbContext.Entry(existing).CurrentValues.SetValues(batch);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddValidationIssuesAsync(ModelWeightBatchId batchId, IReadOnlyList<ModelWeightValidationIssue> issues, bool replaceExisting, CancellationToken cancellationToken)
    {
        if (replaceExisting)
        {
            dbContext.ModelWeightValidationIssues.RemoveRange(dbContext.ModelWeightValidationIssues.Where(x => x.BatchId == batchId));
        }

        dbContext.ModelWeightValidationIssues.AddRange(issues);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkPromotedAsync(ModelWeightBatchId batchId, ModelRunId modelRunId, DateTimeOffset promotedAtUtc, CancellationToken cancellationToken)
    {
        var batch = await dbContext.ModelWeightBatches.FirstAsync(x => x.Id == batchId, cancellationToken);
        dbContext.Entry(batch).CurrentValues.SetValues(batch with { Status = ModelWeightBatchStatus.Promoted, PromotedAtUtc = promotedAtUtc, PromotedModelRunId = modelRunId, Message = "Promoted to model run." });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SqlServerLmaxEodReportRepository(IntradayDbContext dbContext) : ILmaxEodReportRepository
{
    public async Task AddImportRunAsync(LmaxReportImportRun run, CancellationToken cancellationToken)
    {
        dbContext.LmaxReportImportRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateImportRunAsync(LmaxReportImportRun run, CancellationToken cancellationToken)
    {
        var existing = await dbContext.LmaxReportImportRuns.FirstOrDefaultAsync(x => x.Id == run.Id, cancellationToken);
        if (existing is null) return;
        dbContext.Entry(existing).CurrentValues.SetValues(run);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddValidationIssuesAsync(IReadOnlyList<LmaxReportValidationIssue> issues, CancellationToken cancellationToken)
    {
        dbContext.LmaxReportValidationIssues.AddRange(issues);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddIndividualTradesAsync(IReadOnlyList<LmaxIndividualTrade> trades, CancellationToken cancellationToken)
    {
        foreach (var trade in trades)
        {
            if (!await dbContext.LmaxIndividualTrades.AnyAsync(x => x.VenueId == trade.VenueId && x.AccountId == trade.AccountId && (x.ExecutionId == trade.ExecutionId || x.TradeUti == trade.TradeUti), cancellationToken))
            {
                dbContext.LmaxIndividualTrades.Add(trade);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddTradeSummariesAsync(IReadOnlyList<LmaxTradeSummary> summaries, CancellationToken cancellationToken)
    {
        dbContext.LmaxTradeSummaries.AddRange(summaries);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddCurrencyWalletsAsync(IReadOnlyList<LmaxCurrencyWallet> wallets, CancellationToken cancellationToken)
    {
        foreach (var wallet in wallets)
        {
            var existing = await dbContext.LmaxCurrencyWallets.FirstOrDefaultAsync(x => x.ReportDate == wallet.ReportDate && x.VenueId == wallet.VenueId && x.BrokerAccountId == wallet.BrokerAccountId && x.Currency == wallet.Currency, cancellationToken);
            if (existing is null) dbContext.LmaxCurrencyWallets.Add(wallet);
            else dbContext.Entry(existing).CurrentValues.SetValues(wallet with { Id = existing.Id });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LmaxReportImportRun>> GetImportRunsAsync(int limit, DateOnly? reportDate, LmaxReportType? reportType, CancellationToken cancellationToken)
    {
        var query = dbContext.LmaxReportImportRuns.AsNoTracking().AsQueryable();
        if (reportDate is not null) query = query.Where(x => x.ReportDate == reportDate);
        if (reportType is not null) query = query.Where(x => x.ReportType == reportType);
        return await query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(limit, 1, 500)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LmaxReportValidationIssue>> GetValidationIssuesAsync(int limit, LmaxReportImportRunId? importRunId, CancellationToken cancellationToken)
    {
        var query = dbContext.LmaxReportValidationIssues.AsNoTracking().AsQueryable();
        if (importRunId is not null) query = query.Where(x => x.ImportRunId == importRunId);
        return await query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(limit, 1, 500)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LmaxIndividualTrade>> GetIndividualTradesAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken)
    {
        var query = dbContext.LmaxIndividualTrades.AsNoTracking().AsQueryable();
        if (reportDate is not null) query = query.Where(x => x.ReportDate == reportDate);
        return await query.OrderByDescending(x => x.TimestampUtc).Take(Math.Clamp(limit, 1, 500)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LmaxTradeSummary>> GetTradeSummariesAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken)
    {
        var query = dbContext.LmaxTradeSummaries.AsNoTracking().AsQueryable();
        if (reportDate is not null) query = query.Where(x => x.ReportDate == reportDate);
        return await query.OrderByDescending(x => x.DateTimeUtc).Take(Math.Clamp(limit, 1, 500)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LmaxCurrencyWallet>> GetCurrencyWalletsAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken)
    {
        var query = dbContext.LmaxCurrencyWallets.AsNoTracking().AsQueryable();
        if (reportDate is not null) query = query.Where(x => x.ReportDate == reportDate);
        return await query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(limit, 1, 500)).ToListAsync(cancellationToken);
    }

    public async Task AddEodReconciliationAsync(EodReconciliationRun run, IReadOnlyList<EodReconciliationBreak> breaks, CancellationToken cancellationToken)
    {
        dbContext.EodReconciliationRuns.Add(run);
        dbContext.EodReconciliationBreaks.AddRange(breaks);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EodReconciliationRun>> GetEodReconciliationRunsAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken)
    {
        var query = dbContext.EodReconciliationRuns.AsNoTracking().AsQueryable();
        if (reportDate is not null) query = query.Where(x => x.ReportDate == reportDate);
        return await query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(limit, 1, 500)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EodReconciliationBreak>> GetEodReconciliationBreaksAsync(DateOnly? reportDate, int limit, CancellationToken cancellationToken)
    {
        var query = dbContext.EodReconciliationBreaks.AsNoTracking().AsQueryable();
        if (reportDate is not null)
        {
            var runIds = dbContext.EodReconciliationRuns.Where(x => x.ReportDate == reportDate).Select(x => x.Id);
            query = query.Where(x => runIds.Contains(x.RunId));
        }

        return await query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(limit, 1, 500)).ToListAsync(cancellationToken);
    }
}

public sealed class SqlServerFakeBrokerPositionProvider(IntradayDbContext dbContext, IClock clock) : IBrokerPositionProvider
{
    public async Task<IReadOnlyList<BrokerPositionSnapshot>> GetPositionsAsync(BrokerAccountId brokerAccountId, CancellationToken cancellationToken)
    {
        var account = await dbContext.BrokerAccounts.AsNoTracking().SingleAsync(x => x.Id == brokerAccountId, cancellationToken);
        return await dbContext.PositionLedgerEvents.AsNoTracking()
            .Where(x => x.FundId == account.FundId)
            .GroupBy(x => x.InstrumentId)
            .Select(x => new BrokerPositionSnapshot(brokerAccountId, x.Key, x.Sum(y => y.BaseQuantityDelta), clock.UtcNow))
            .ToListAsync(cancellationToken);
    }
}
