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
    public DbSet<QubesWeightAuditBatch> QubesWeightAuditBatches => Set<QubesWeightAuditBatch>();
    public DbSet<QubesRawWeightAuditRow> QubesRawWeightAuditRows => Set<QubesRawWeightAuditRow>();
    public DbSet<QubesNormalizedWeightAuditRow> QubesNormalizedWeightAuditRows => Set<QubesNormalizedWeightAuditRow>();
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
    public DbSet<RiskDecisionDetail> RiskDecisionDetails => Set<RiskDecisionDetail>();
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
    public DbSet<ExceptionCase> ExceptionCases => Set<ExceptionCase>();
    public DbSet<ExceptionCaseAction> ExceptionCaseActions => Set<ExceptionCaseAction>();
    public DbSet<ExceptionCaseNote> ExceptionCaseNotes => Set<ExceptionCaseNote>();
    public DbSet<ExceptionCaseLink> ExceptionCaseLinks => Set<ExceptionCaseLink>();
    public DbSet<OperatorAuditEvent> OperatorAuditEvents => Set<OperatorAuditEvent>();
    public DbSet<OperatorUser> OperatorUsers => Set<OperatorUser>();
    public DbSet<OperatorUserRole> OperatorUserRoles => Set<OperatorUserRole>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();
    public DbSet<OperationalJobDefinition> OperationalJobDefinitions => Set<OperationalJobDefinition>();
    public DbSet<OperationalJobRun> OperationalJobRuns => Set<OperationalJobRun>();
    public DbSet<OperationalJobStep> OperationalJobSteps => Set<OperationalJobStep>();
    public DbSet<OperationalJobRunEvent> OperationalJobRunEvents => Set<OperationalJobRunEvent>();
    public DbSet<OperationalRunbookDefinition> OperationalRunbookDefinitions => Set<OperationalRunbookDefinition>();
    public DbSet<OperationalRunbookStepDefinition> OperationalRunbookStepDefinitions => Set<OperationalRunbookStepDefinition>();
    public DbSet<OperationalRunbookRun> OperationalRunbookRuns => Set<OperationalRunbookRun>();
    public DbSet<OperationalRunbookStepRun> OperationalRunbookStepRuns => Set<OperationalRunbookStepRun>();
    public DbSet<OperationalScheduleDefinition> OperationalScheduleDefinitions => Set<OperationalScheduleDefinition>();
    public DbSet<LmaxShadowReplayRun> LmaxShadowReplayRuns => Set<LmaxShadowReplayRun>();
    public DbSet<LmaxShadowObservation> LmaxShadowObservations => Set<LmaxShadowObservation>();

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
        modelBuilder.Entity<QubesWeightAuditBatch>().HasKey(x => x.Id);
        modelBuilder.Entity<QubesRawWeightAuditRow>().HasKey(x => x.Id);
        modelBuilder.Entity<QubesNormalizedWeightAuditRow>().HasKey(x => x.Id);
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
        modelBuilder.Entity<RiskDecisionDetail>().HasKey(x => x.Id);
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
        modelBuilder.Entity<ExceptionCase>().HasKey(x => x.Id);
        modelBuilder.Entity<ExceptionCaseAction>().HasKey(x => x.Id);
        modelBuilder.Entity<ExceptionCaseNote>().HasKey(x => x.Id);
        modelBuilder.Entity<ExceptionCaseLink>().HasKey(x => x.Id);
        modelBuilder.Entity<OperatorAuditEvent>().HasKey(x => x.Id);
        modelBuilder.Entity<OperatorUser>().HasKey(x => x.Id);
        modelBuilder.Entity<OperatorUserRole>().HasKey(x => x.Id);
        modelBuilder.Entity<ApprovalRequest>().HasKey(x => x.Id);
        modelBuilder.Entity<ApprovalDecision>().HasKey(x => x.Id);
        modelBuilder.Entity<OperationalJobDefinition>().HasKey(x => x.Id);
        modelBuilder.Entity<OperationalJobRun>().HasKey(x => x.Id);
        modelBuilder.Entity<OperationalJobStep>().HasKey(x => x.Id);
        modelBuilder.Entity<OperationalJobRunEvent>().HasKey(x => x.Id);
        modelBuilder.Entity<OperationalRunbookDefinition>().HasKey(x => x.Id);
        modelBuilder.Entity<OperationalRunbookStepDefinition>().HasKey(x => x.Id);
        modelBuilder.Entity<OperationalRunbookRun>().HasKey(x => x.Id);
        modelBuilder.Entity<OperationalRunbookStepRun>().HasKey(x => x.Id);
        modelBuilder.Entity<OperationalScheduleDefinition>().HasKey(x => x.Id);
        modelBuilder.Entity<LmaxShadowReplayRun>().HasKey(x => x.Id);
        modelBuilder.Entity<LmaxShadowObservation>().HasKey(x => x.Id);

        modelBuilder.Entity<ModelRun>().HasIndex(x => x.Id).IsUnique();
        modelBuilder.Entity<Fund>().HasIndex(x => x.Name).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<BrokerAccount>().HasIndex(x => new { x.FundId, x.AccountCode }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<Instrument>().HasIndex(x => new { x.Symbol, x.AssetClass }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<InstrumentAlias>().HasIndex(x => new { x.Source, x.ExternalSymbol }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<InstrumentAlias>().HasIndex(x => new { x.Source, x.ExternalInstrumentId }).IsUnique().HasFilter("[IsEnabled] = 1 AND [ExternalInstrumentId] IS NOT NULL");
        modelBuilder.Entity<Venue>().HasIndex(x => x.Name).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<VenueInstrumentMapping>().HasIndex(x => new { x.VenueId, x.InstrumentId }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<VenueInstrumentMapping>().HasIndex(x => new { x.VenueId, x.VenueSymbol }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<RiskLimitSet>().HasIndex(x => new { x.FundId, x.ModelName, x.Status });
        modelBuilder.Entity<RiskLimitSet>().HasIndex(x => new { x.FundId, x.ModelName, x.IsActive }).IsUnique().HasFilter("[IsActive] = 1");
        modelBuilder.Entity<RiskLimit>().HasIndex(x => new { x.RiskLimitSetId, x.Name }).IsUnique();
        modelBuilder.Entity<InstrumentRiskLimit>().HasIndex(x => new { x.RiskLimitSetId, x.InstrumentId }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<VenueRiskLimit>().HasIndex(x => new { x.RiskLimitSetId, x.VenueId }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<TradingWindow>().HasIndex(x => new { x.FundId, x.ModelName, x.DayOfWeek }).IsUnique().HasFilter("[IsEnabled] = 1");
        modelBuilder.Entity<RiskDecision>().HasIndex(x => x.RiskLimitSetId);
        modelBuilder.Entity<RiskDecision>().HasIndex(x => x.TradeIntentId);
        modelBuilder.Entity<RiskDecision>().HasIndex(x => x.CreatedAtUtc);
        modelBuilder.Entity<RiskDecisionDetail>().HasIndex(x => new { x.RiskDecisionId, x.CheckName });
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
        modelBuilder.Entity<QubesWeightAuditBatch>().HasIndex(x => x.QubesRunId).IsUnique();
        modelBuilder.Entity<QubesWeightAuditBatch>().HasIndex(x => new { x.SourceSystem, x.ProducedAtUtc });
        modelBuilder.Entity<QubesRawWeightAuditRow>().HasIndex(x => new { x.AuditBatchId, x.RowNumber }).IsUnique();
        modelBuilder.Entity<QubesNormalizedWeightAuditRow>().HasIndex(x => new { x.AuditBatchId, x.Symbol }).IsUnique();
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
        modelBuilder.Entity<ExceptionCase>().HasIndex(x => x.Status);
        modelBuilder.Entity<ExceptionCase>().HasIndex(x => x.Severity);
        modelBuilder.Entity<ExceptionCase>().HasIndex(x => x.Type);
        modelBuilder.Entity<ExceptionCase>().HasIndex(x => x.Source);
        modelBuilder.Entity<ExceptionCase>().HasIndex(x => x.CreatedAtUtc);
        modelBuilder.Entity<ExceptionCase>().HasIndex(x => new { x.EntityType, x.EntityId });
        modelBuilder.Entity<ExceptionCase>().HasIndex(x => x.InstrumentId);
        modelBuilder.Entity<ExceptionCase>().HasIndex(x => x.CorrelationId);
        modelBuilder.Entity<ExceptionCase>().HasIndex(x => x.AssignedTo);
        modelBuilder.Entity<ExceptionCaseAction>().HasIndex(x => new { x.CaseId, x.OccurredAtUtc });
        modelBuilder.Entity<ExceptionCaseNote>().HasIndex(x => new { x.CaseId, x.CreatedAtUtc });
        modelBuilder.Entity<ExceptionCaseLink>().HasIndex(x => new { x.SourceEntityType, x.SourceEntityId }).IsUnique();
        modelBuilder.Entity<OperatorUser>().HasIndex(x => x.OperatorId).IsUnique();
        modelBuilder.Entity<OperatorUserRole>().HasIndex(x => new { x.OperatorUserId, x.Role }).IsUnique();
        modelBuilder.Entity<ApprovalRequest>().HasIndex(x => x.Status);
        modelBuilder.Entity<ApprovalRequest>().HasIndex(x => x.Type);
        modelBuilder.Entity<ApprovalRequest>().HasIndex(x => x.RequestedByOperatorId);
        modelBuilder.Entity<ApprovalRequest>().HasIndex(x => new { x.EntityType, x.EntityId });
        modelBuilder.Entity<ApprovalRequest>().HasIndex(x => x.CorrelationId);
        modelBuilder.Entity<ApprovalRequest>().HasIndex(x => x.CreatedAtUtc);
        modelBuilder.Entity<ApprovalDecision>().HasIndex(x => new { x.ApprovalRequestId, x.DecidedAtUtc });
        modelBuilder.Entity<OperationalJobRun>().HasIndex(x => new { x.JobType, x.StartedAtUtc });
        modelBuilder.Entity<OperationalJobRun>().HasIndex(x => x.Status);
        modelBuilder.Entity<OperationalJobRun>().HasIndex(x => x.CorrelationId);
        modelBuilder.Entity<OperationalJobRun>().HasIndex(x => x.TriggeredByOperatorId);
        modelBuilder.Entity<OperationalJobRun>().HasIndex(x => x.RetryOfJobRunId);
        modelBuilder.Entity<OperationalJobStep>().HasIndex(x => x.JobRunId);
        modelBuilder.Entity<OperationalRunbookDefinition>().HasIndex(x => new { x.RunbookType, x.IsEnabled });
        modelBuilder.Entity<OperationalRunbookRun>().HasIndex(x => new { x.RunbookType, x.StartedAtUtc });
        modelBuilder.Entity<OperationalRunbookRun>().HasIndex(x => x.Status);
        modelBuilder.Entity<OperationalRunbookRun>().HasIndex(x => x.CorrelationId);
        modelBuilder.Entity<OperationalRunbookRun>().HasIndex(x => x.RetryOfRunbookRunId);
        modelBuilder.Entity<OperationalRunbookStepRun>().HasIndex(x => x.RunbookRunId);
        modelBuilder.Entity<OperationalScheduleDefinition>().HasIndex(x => new { x.IsEnabled, x.NextRunAtUtc });
        modelBuilder.Entity<LmaxShadowObservation>().HasIndex(x => x.Type);
        modelBuilder.Entity<LmaxShadowObservation>().HasIndex(x => x.Severity);
        modelBuilder.Entity<LmaxShadowObservation>().HasIndex(x => x.Status);
        modelBuilder.Entity<LmaxShadowObservation>().HasIndex(x => x.BrokerExecutionId);
        modelBuilder.Entity<LmaxShadowObservation>().HasIndex(x => x.ClientOrderId);
        modelBuilder.Entity<LmaxShadowObservation>().HasIndex(x => x.BrokerOrderId);
        modelBuilder.Entity<LmaxShadowObservation>().HasIndex(x => x.InstrumentId);
        modelBuilder.Entity<LmaxShadowObservation>().HasIndex(x => x.ReplayRunId);
        modelBuilder.Entity<LmaxShadowObservation>().HasIndex(x => x.Fingerprint);
        modelBuilder.Entity<LmaxShadowObservation>().HasIndex(x => new { x.ReplayRunId, x.Fingerprint });
        modelBuilder.Entity<LmaxShadowObservation>().HasIndex(x => x.CreatedAtUtc);
        modelBuilder.Entity<LmaxShadowReplayRun>().HasIndex(x => x.Status);
        modelBuilder.Entity<LmaxShadowReplayRun>().HasIndex(x => x.InputSource);
        modelBuilder.Entity<LmaxShadowReplayRun>().HasIndex(x => x.CorrelationId);
        modelBuilder.Entity<LmaxShadowReplayRun>().HasIndex(x => x.CreatedAtUtc);

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
        modelBuilder.Entity<QubesWeightAuditBatch>().HasOne<ModelWeightBatch>().WithMany().HasForeignKey(x => x.ModelWeightBatchId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<QubesWeightAuditBatch>().HasOne<ModelRun>().WithMany().HasForeignKey(x => x.PromotedModelRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<QubesRawWeightAuditRow>().HasOne<QubesWeightAuditBatch>().WithMany().HasForeignKey(x => x.AuditBatchId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<QubesNormalizedWeightAuditRow>().HasOne<QubesWeightAuditBatch>().WithMany().HasForeignKey(x => x.AuditBatchId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<QubesNormalizedWeightAuditRow>().HasOne<ModelWeightBatch>().WithMany().HasForeignKey(x => x.ModelWeightBatchId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<QubesNormalizedWeightAuditRow>().HasOne<ModelRun>().WithMany().HasForeignKey(x => x.ModelRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<QubesNormalizedWeightAuditRow>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.TargetWeightInstrumentId).OnDelete(DeleteBehavior.Restrict);
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
        modelBuilder.Entity<RiskDecisionDetail>().HasOne<RiskDecision>().WithMany().HasForeignKey(x => x.RiskDecisionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ParentOrder>().HasOne<TradeIntent>().WithMany().HasForeignKey(x => x.TradeIntentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChildOrder>().HasOne<ParentOrder>().WithMany().HasForeignKey(x => x.ParentOrderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ChildOrder>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ExecutionReport>().HasOne<ChildOrder>().WithMany().HasForeignKey(x => x.ChildOrderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ExecutionReport>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Fill>().HasOne<ChildOrder>().WithMany().HasForeignKey(x => x.ChildOrderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Fill>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Fill>().HasOne<Venue>().WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RiskLimitSet>().HasOne<Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RiskDecision>().HasOne<RiskLimitSet>().WithMany().HasForeignKey(x => x.RiskLimitSetId).OnDelete(DeleteBehavior.Restrict);
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
        modelBuilder.Entity<ExceptionCase>().HasOne<Instrument>().WithMany().HasForeignKey(x => x.InstrumentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ExceptionCaseAction>().HasOne<ExceptionCase>().WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ExceptionCaseNote>().HasOne<ExceptionCase>().WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ExceptionCaseLink>().HasOne<ExceptionCase>().WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OperatorUserRole>().HasOne<OperatorUser>().WithMany().HasForeignKey(x => x.OperatorUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ApprovalDecision>().HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OperationalJobRun>().HasOne<OperationalJobDefinition>().WithMany().HasForeignKey(x => x.JobDefinitionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OperationalJobRun>().HasOne<ExceptionCase>().WithMany().HasForeignKey(x => x.ExceptionCaseId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OperationalJobRun>().HasOne<OperatorAuditEvent>().WithMany().HasForeignKey(x => x.AuditEventId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OperationalJobRun>().HasOne<OperationalJobRun>().WithMany().HasForeignKey(x => x.RetryOfJobRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OperationalJobStep>().HasOne<OperationalJobRun>().WithMany().HasForeignKey(x => x.JobRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OperationalJobRunEvent>().HasOne<OperationalJobRun>().WithMany().HasForeignKey(x => x.JobRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OperationalRunbookStepDefinition>().HasOne<OperationalRunbookDefinition>().WithMany().HasForeignKey(x => x.RunbookDefinitionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OperationalRunbookRun>().HasOne<OperationalRunbookDefinition>().WithMany().HasForeignKey(x => x.RunbookDefinitionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OperationalRunbookRun>().HasOne<OperationalRunbookRun>().WithMany().HasForeignKey(x => x.RetryOfRunbookRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OperationalRunbookStepRun>().HasOne<OperationalRunbookRun>().WithMany().HasForeignKey(x => x.RunbookRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OperationalRunbookStepRun>().HasOne<OperationalRunbookStepDefinition>().WithMany().HasForeignKey(x => x.StepDefinitionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OperationalRunbookStepRun>().HasOne<OperationalJobRun>().WithMany().HasForeignKey(x => x.JobRunId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OperationalScheduleDefinition>().HasOne<OperationalRunbookDefinition>().WithMany().HasForeignKey(x => x.RunbookDefinitionId).OnDelete(DeleteBehavior.Restrict);

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
        modelBuilder.Entity<OperationalJobDefinition>().Property(x => x.Name).HasMaxLength(160);
        modelBuilder.Entity<OperationalJobDefinition>().Property(x => x.Description).HasMaxLength(1000);
        modelBuilder.Entity<OperationalJobRun>().Property(x => x.Name).HasMaxLength(160);
        modelBuilder.Entity<OperationalJobRun>().Property(x => x.TriggeredByOperatorId).HasMaxLength(128);
        modelBuilder.Entity<OperationalJobRun>().Property(x => x.TriggeredByDisplayName).HasMaxLength(256);
        modelBuilder.Entity<OperationalJobRun>().Property(x => x.CorrelationId).HasMaxLength(128);
        modelBuilder.Entity<OperationalJobRun>().Property(x => x.RequestId).HasMaxLength(128);
        modelBuilder.Entity<OperationalJobRun>().Property(x => x.ErrorMessage).HasMaxLength(4000);
        modelBuilder.Entity<OperationalJobStep>().Property(x => x.StepName).HasMaxLength(160);
        modelBuilder.Entity<OperationalJobStep>().Property(x => x.Message).HasMaxLength(1000);
        modelBuilder.Entity<OperationalJobStep>().Property(x => x.ErrorMessage).HasMaxLength(4000);
        modelBuilder.Entity<OperationalJobRunEvent>().Property(x => x.Message).HasMaxLength(2000);
        modelBuilder.Entity<OperationalRunbookDefinition>().Property(x => x.Name).HasMaxLength(160);
        modelBuilder.Entity<OperationalRunbookDefinition>().Property(x => x.Description).HasMaxLength(1000);
        modelBuilder.Entity<OperationalRunbookStepDefinition>().Property(x => x.Name).HasMaxLength(160);
        modelBuilder.Entity<OperationalRunbookStepDefinition>().Property(x => x.Description).HasMaxLength(1000);
        modelBuilder.Entity<OperationalRunbookRun>().Property(x => x.Name).HasMaxLength(160);
        modelBuilder.Entity<OperationalRunbookRun>().Property(x => x.TriggeredByOperatorId).HasMaxLength(128);
        modelBuilder.Entity<OperationalRunbookRun>().Property(x => x.TriggeredByDisplayName).HasMaxLength(256);
        modelBuilder.Entity<OperationalRunbookRun>().Property(x => x.CorrelationId).HasMaxLength(128);
        modelBuilder.Entity<OperationalRunbookRun>().Property(x => x.Reason).HasMaxLength(1000);
        modelBuilder.Entity<OperationalRunbookRun>().Property(x => x.ErrorMessage).HasMaxLength(4000);
        modelBuilder.Entity<OperationalRunbookStepRun>().Property(x => x.Name).HasMaxLength(160);
        modelBuilder.Entity<OperationalRunbookStepRun>().Property(x => x.Message).HasMaxLength(1000);
        modelBuilder.Entity<OperationalRunbookStepRun>().Property(x => x.ErrorMessage).HasMaxLength(4000);
        modelBuilder.Entity<OperationalScheduleDefinition>().Property(x => x.Name).HasMaxLength(160);
        modelBuilder.Entity<OperationalScheduleDefinition>().Property(x => x.CronExpression).HasMaxLength(160);
        modelBuilder.Entity<OperationalScheduleDefinition>().Property(x => x.TimeZoneId).HasMaxLength(128);

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
        modelBuilder.Entity<QubesWeightAuditBatch>().Property(x => x.Id).HasConversion(x => x.Value, x => new QubesWeightAuditBatchId(x));
        modelBuilder.Entity<QubesWeightAuditBatch>().Property(x => x.ModelWeightBatchId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new ModelWeightBatchId(x.Value) : null);
        modelBuilder.Entity<QubesWeightAuditBatch>().Property(x => x.PromotedModelRunId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new ModelRunId(x.Value) : null);
        modelBuilder.Entity<QubesRawWeightAuditRow>().Property(x => x.Id).HasConversion(x => x.Value, x => new QubesRawWeightAuditRowId(x));
        modelBuilder.Entity<QubesRawWeightAuditRow>().Property(x => x.AuditBatchId).HasConversion(x => x.Value, x => new QubesWeightAuditBatchId(x));
        modelBuilder.Entity<QubesNormalizedWeightAuditRow>().Property(x => x.Id).HasConversion(x => x.Value, x => new QubesNormalizedWeightAuditRowId(x));
        modelBuilder.Entity<QubesNormalizedWeightAuditRow>().Property(x => x.AuditBatchId).HasConversion(x => x.Value, x => new QubesWeightAuditBatchId(x));
        modelBuilder.Entity<QubesNormalizedWeightAuditRow>().Property(x => x.ModelWeightBatchId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new ModelWeightBatchId(x.Value) : null);
        modelBuilder.Entity<QubesNormalizedWeightAuditRow>().Property(x => x.ModelRunId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new ModelRunId(x.Value) : null);
        modelBuilder.Entity<QubesNormalizedWeightAuditRow>().Property(x => x.TargetWeightInstrumentId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new InstrumentId(x.Value) : null);
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
        modelBuilder.Entity<RiskDecision>().Property(x => x.ModelRunId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new ModelRunId(x.Value) : null);
        modelBuilder.Entity<RiskDecision>().Property(x => x.InstrumentId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new InstrumentId(x.Value) : null);
        modelBuilder.Entity<RiskDecision>().Property(x => x.VenueId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new VenueId(x.Value) : null);
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
        modelBuilder.Entity<ExceptionCase>().Property(x => x.Id).HasConversion(x => x.Value, x => new ExceptionCaseId(x));
        modelBuilder.Entity<ExceptionCase>().Property(x => x.InstrumentId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new InstrumentId(x.Value) : null);
        modelBuilder.Entity<ExceptionCaseAction>().Property(x => x.Id).HasConversion(x => x.Value, x => new ExceptionCaseActionId(x));
        modelBuilder.Entity<ExceptionCaseAction>().Property(x => x.CaseId).HasConversion(x => x.Value, x => new ExceptionCaseId(x));
        modelBuilder.Entity<ExceptionCaseNote>().Property(x => x.Id).HasConversion(x => x.Value, x => new ExceptionCaseNoteId(x));
        modelBuilder.Entity<ExceptionCaseNote>().Property(x => x.CaseId).HasConversion(x => x.Value, x => new ExceptionCaseId(x));
        modelBuilder.Entity<ExceptionCaseLink>().Property(x => x.CaseId).HasConversion(x => x.Value, x => new ExceptionCaseId(x));
        modelBuilder.Entity<OperatorAuditEvent>().Property(x => x.Id).HasConversion(x => x.Value, x => new OperatorAuditEventId(x));
        modelBuilder.Entity<OperatorUser>().Property(x => x.Id).HasConversion(x => x.Value, x => new OperatorUserId(x));
        modelBuilder.Entity<OperatorUserRole>().Property(x => x.Id).HasConversion(x => x.Value, x => new OperatorUserRoleId(x));
        modelBuilder.Entity<OperatorUserRole>().Property(x => x.OperatorUserId).HasConversion(x => x.Value, x => new OperatorUserId(x));
        modelBuilder.Entity<ApprovalRequest>().Property(x => x.Id).HasConversion(x => x.Value, x => new ApprovalRequestId(x));
        modelBuilder.Entity<ApprovalDecision>().Property(x => x.Id).HasConversion(x => x.Value, x => new ApprovalDecisionId(x));
        modelBuilder.Entity<ApprovalDecision>().Property(x => x.ApprovalRequestId).HasConversion(x => x.Value, x => new ApprovalRequestId(x));
        modelBuilder.Entity<OperationalJobDefinition>().Property(x => x.Id).HasConversion(x => x.Value, x => new OperationalJobDefinitionId(x));
        modelBuilder.Entity<OperationalJobRun>().Property(x => x.Id).HasConversion(x => x.Value, x => new OperationalJobRunId(x));
        modelBuilder.Entity<OperationalJobRun>().Property(x => x.JobDefinitionId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new OperationalJobDefinitionId(x.Value) : null);
        modelBuilder.Entity<OperationalJobRun>().Property(x => x.ExceptionCaseId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new ExceptionCaseId(x.Value) : null);
        modelBuilder.Entity<OperationalJobRun>().Property(x => x.AuditEventId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new OperatorAuditEventId(x.Value) : null);
        modelBuilder.Entity<OperationalJobRun>().Property(x => x.RetryOfJobRunId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new OperationalJobRunId(x.Value) : null);
        modelBuilder.Entity<OperationalJobStep>().Property(x => x.Id).HasConversion(x => x.Value, x => new OperationalJobStepId(x));
        modelBuilder.Entity<OperationalJobStep>().Property(x => x.JobRunId).HasConversion(x => x.Value, x => new OperationalJobRunId(x));
        modelBuilder.Entity<OperationalJobRunEvent>().Property(x => x.JobRunId).HasConversion(x => x.Value, x => new OperationalJobRunId(x));
        modelBuilder.Entity<OperationalRunbookDefinition>().Property(x => x.Id).HasConversion(x => x.Value, x => new OperationalRunbookDefinitionId(x));
        modelBuilder.Entity<OperationalRunbookStepDefinition>().Property(x => x.Id).HasConversion(x => x.Value, x => new OperationalRunbookStepDefinitionId(x));
        modelBuilder.Entity<OperationalRunbookStepDefinition>().Property(x => x.RunbookDefinitionId).HasConversion(x => x.Value, x => new OperationalRunbookDefinitionId(x));
        modelBuilder.Entity<OperationalRunbookRun>().Property(x => x.Id).HasConversion(x => x.Value, x => new OperationalRunbookRunId(x));
        modelBuilder.Entity<OperationalRunbookRun>().Property(x => x.RunbookDefinitionId).HasConversion(x => x.Value, x => new OperationalRunbookDefinitionId(x));
        modelBuilder.Entity<OperationalRunbookRun>().Property(x => x.RetryOfRunbookRunId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new OperationalRunbookRunId(x.Value) : null);
        modelBuilder.Entity<OperationalRunbookStepRun>().Property(x => x.Id).HasConversion(x => x.Value, x => new OperationalRunbookStepRunId(x));
        modelBuilder.Entity<OperationalRunbookStepRun>().Property(x => x.RunbookRunId).HasConversion(x => x.Value, x => new OperationalRunbookRunId(x));
        modelBuilder.Entity<OperationalRunbookStepRun>().Property(x => x.StepDefinitionId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new OperationalRunbookStepDefinitionId(x.Value) : null);
        modelBuilder.Entity<OperationalRunbookStepRun>().Property(x => x.JobRunId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new OperationalJobRunId(x.Value) : null);
        modelBuilder.Entity<OperationalScheduleDefinition>().Property(x => x.Id).HasConversion(x => x.Value, x => new OperationalScheduleDefinitionId(x));
        modelBuilder.Entity<OperationalScheduleDefinition>().Property(x => x.RunbookDefinitionId).HasConversion(x => x.Value, x => new OperationalRunbookDefinitionId(x));
        modelBuilder.Entity<LmaxShadowReplayRun>().Property(x => x.Id).HasConversion(x => x.Value, x => new LmaxShadowReplayRunId(x));
        modelBuilder.Entity<LmaxShadowObservation>().Property(x => x.Id).HasConversion(x => x.Value, x => new LmaxShadowObservationId(x));
        modelBuilder.Entity<LmaxShadowObservation>().Property(x => x.ReplayRunId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new LmaxShadowReplayRunId(x.Value) : null);
        modelBuilder.Entity<LmaxShadowObservation>().Property(x => x.InstrumentId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new InstrumentId(x.Value) : null);
        modelBuilder.Entity<LmaxShadowObservation>().Property(x => x.InternalFillId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new FillId(x.Value) : null);
        modelBuilder.Entity<LmaxShadowObservation>().Property(x => x.InternalOrderId).HasConversion(x => x.HasValue ? x.Value.Value : (Guid?)null, x => x.HasValue ? new ChildOrderId(x.Value) : null);
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

public sealed class SqlServerExceptionCaseRepository(IntradayDbContext dbContext) : IExceptionCaseRepository
{
    public async Task AddCaseAsync(ExceptionCase exceptionCase, ExceptionCaseAction action, ExceptionCaseLink? link, CancellationToken cancellationToken)
    {
        dbContext.ExceptionCases.Add(exceptionCase);
        dbContext.ExceptionCaseActions.Add(action);
        if (link is not null)
        {
            dbContext.ExceptionCaseLinks.Add(link);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateCaseAsync(ExceptionCase exceptionCase, ExceptionCaseAction action, CancellationToken cancellationToken)
    {
        var existing = await dbContext.ExceptionCases.FirstAsync(x => x.Id == exceptionCase.Id, cancellationToken);
        dbContext.Entry(existing).CurrentValues.SetValues(exceptionCase);
        dbContext.ExceptionCaseActions.Add(action);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddNoteAsync(ExceptionCaseNote note, ExceptionCaseAction action, CancellationToken cancellationToken)
    {
        dbContext.ExceptionCaseNotes.Add(note);
        dbContext.ExceptionCaseActions.Add(action);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<ExceptionCase?> GetCaseAsync(ExceptionCaseId id, CancellationToken cancellationToken)
        => dbContext.ExceptionCases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<ExceptionCaseLink?> GetLinkAsync(string sourceEntityType, string sourceEntityId, CancellationToken cancellationToken)
        => dbContext.ExceptionCaseLinks.AsNoTracking().FirstOrDefaultAsync(x => x.SourceEntityType == sourceEntityType && x.SourceEntityId == sourceEntityId, cancellationToken);

    public async Task<IReadOnlyList<ExceptionCase>> GetCasesAsync(ExceptionCaseFilter filter, CancellationToken cancellationToken)
    {
        var query = dbContext.ExceptionCases.AsNoTracking().AsQueryable();
        if (filter.Status is not null) query = query.Where(x => x.Status == filter.Status.Value);
        if (filter.Severity is not null) query = query.Where(x => x.Severity == filter.Severity.Value);
        if (filter.Type is not null) query = query.Where(x => x.Type == filter.Type.Value);
        if (filter.Source is not null) query = query.Where(x => x.Source == filter.Source.Value);
        if (!string.IsNullOrWhiteSpace(filter.AssignedTo)) query = query.Where(x => x.AssignedTo == filter.AssignedTo);
        if (!string.IsNullOrWhiteSpace(filter.Instrument))
        {
            if (Guid.TryParse(filter.Instrument, out var instrumentId))
            {
                var typedInstrumentId = new InstrumentId(instrumentId);
                query = query.Where(x => x.Symbol == filter.Instrument || x.InstrumentId == typedInstrumentId);
            }
            else
            {
                query = query.Where(x => x.Symbol == filter.Instrument);
            }
        }
        if (!string.IsNullOrWhiteSpace(filter.EntityType)) query = query.Where(x => x.EntityType == filter.EntityType);
        if (!string.IsNullOrWhiteSpace(filter.EntityId)) query = query.Where(x => x.EntityId == filter.EntityId);
        if (!string.IsNullOrWhiteSpace(filter.CorrelationId)) query = query.Where(x => x.CorrelationId == filter.CorrelationId);
        if (filter.FromUtc is not null) query = query.Where(x => x.CreatedAtUtc >= filter.FromUtc);
        if (filter.ToUtc is not null) query = query.Where(x => x.CreatedAtUtc <= filter.ToUtc);
        return await query.OrderByDescending(x => x.UpdatedAtUtc).Take(Math.Clamp(filter.Limit, 1, 500)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExceptionCaseAction>> GetActionsAsync(ExceptionCaseId id, CancellationToken cancellationToken)
        => await dbContext.ExceptionCaseActions.AsNoTracking().Where(x => x.CaseId == id).OrderBy(x => x.OccurredAtUtc).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExceptionCaseNote>> GetNotesAsync(ExceptionCaseId id, CancellationToken cancellationToken)
        => await dbContext.ExceptionCaseNotes.AsNoTracking().Where(x => x.CaseId == id).OrderBy(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
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
        state.RiskDecisionDetails.AddRange(await dbContext.RiskDecisionDetails.AsNoTracking().ToListAsync(cancellationToken));
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
        state.ExceptionCases.AddRange(await dbContext.ExceptionCases.AsNoTracking().ToListAsync(cancellationToken));
        state.ExceptionCaseActions.AddRange(await dbContext.ExceptionCaseActions.AsNoTracking().ToListAsync(cancellationToken));
        state.ExceptionCaseNotes.AddRange(await dbContext.ExceptionCaseNotes.AsNoTracking().ToListAsync(cancellationToken));
        state.ExceptionCaseLinks.AddRange(await dbContext.ExceptionCaseLinks.AsNoTracking().ToListAsync(cancellationToken));
        state.OperatorAuditEvents.AddRange(await dbContext.OperatorAuditEvents.AsNoTracking().ToListAsync(cancellationToken));
        state.OperatorUsers.AddRange(await dbContext.OperatorUsers.AsNoTracking().ToListAsync(cancellationToken));
        state.OperatorUserRoles.AddRange(await dbContext.OperatorUserRoles.AsNoTracking().ToListAsync(cancellationToken));
        state.ApprovalRequests.AddRange(await dbContext.ApprovalRequests.AsNoTracking().ToListAsync(cancellationToken));
        state.ApprovalDecisions.AddRange(await dbContext.ApprovalDecisions.AsNoTracking().ToListAsync(cancellationToken));
        state.OperationalJobDefinitions.AddRange(await dbContext.OperationalJobDefinitions.AsNoTracking().ToListAsync(cancellationToken));
        state.OperationalJobRuns.AddRange(await dbContext.OperationalJobRuns.AsNoTracking().ToListAsync(cancellationToken));
        state.OperationalJobSteps.AddRange(await dbContext.OperationalJobSteps.AsNoTracking().ToListAsync(cancellationToken));
        state.OperationalJobRunEvents.AddRange(await dbContext.OperationalJobRunEvents.AsNoTracking().ToListAsync(cancellationToken));
        state.OperationalRunbookDefinitions.AddRange(await dbContext.OperationalRunbookDefinitions.AsNoTracking().ToListAsync(cancellationToken));
        state.OperationalRunbookStepDefinitions.AddRange(await dbContext.OperationalRunbookStepDefinitions.AsNoTracking().ToListAsync(cancellationToken));
        state.OperationalRunbookRuns.AddRange(await dbContext.OperationalRunbookRuns.AsNoTracking().ToListAsync(cancellationToken));
        state.OperationalRunbookStepRuns.AddRange(await dbContext.OperationalRunbookStepRuns.AsNoTracking().ToListAsync(cancellationToken));
        state.OperationalScheduleDefinitions.AddRange(await dbContext.OperationalScheduleDefinitions.AsNoTracking().ToListAsync(cancellationToken));
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

    public async Task AddRiskDecisionAsync(RiskDecision decision, IReadOnlyList<RiskDecisionDetail>? details, CancellationToken cancellationToken)
    {
        if (await dbContext.RiskDecisions.AnyAsync(x => x.TradeIntentId == decision.TradeIntentId, cancellationToken))
        {
            return;
        }

        dbContext.RiskDecisions.Add(decision);
        if (details is not null)
        {
            dbContext.RiskDecisionDetails.AddRange(details);
        }
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

    public Task UpsertRiskLimitSetAsync(RiskLimitSet riskLimitSet, CancellationToken cancellationToken)
        => UpsertAsync(dbContext.RiskLimitSets, riskLimitSet, cancellationToken);

    public Task UpsertRiskLimitAsync(RiskLimit riskLimit, CancellationToken cancellationToken)
        => UpsertAsync(dbContext.RiskLimits, riskLimit, cancellationToken);

    public Task UpsertInstrumentRiskLimitAsync(InstrumentRiskLimit instrumentRiskLimit, CancellationToken cancellationToken)
        => UpsertAsync(dbContext.InstrumentRiskLimits, instrumentRiskLimit, cancellationToken);

    public Task UpsertVenueRiskLimitAsync(VenueRiskLimit venueRiskLimit, CancellationToken cancellationToken)
        => UpsertAsync(dbContext.VenueRiskLimits, venueRiskLimit, cancellationToken);

    public Task UpsertTradingWindowAsync(TradingWindow tradingWindow, CancellationToken cancellationToken)
        => UpsertAsync(dbContext.TradingWindows, tradingWindow, cancellationToken);

    public Task UpsertInstrumentAsync(Instrument instrument, CancellationToken cancellationToken)
        => UpsertAsync(dbContext.Instruments, instrument, cancellationToken);

    public Task UpsertVenueAsync(Venue venue, CancellationToken cancellationToken)
        => UpsertAsync(dbContext.Venues, venue, cancellationToken);

    private async Task UpsertAsync<TEntity>(DbSet<TEntity> set, TEntity entity, CancellationToken cancellationToken)
        where TEntity : class
    {
        var entry = dbContext.Entry(entity);
        var key = dbContext.Model.FindEntityType(typeof(TEntity))?.FindPrimaryKey() ?? throw new InvalidOperationException($"No key configured for {typeof(TEntity).Name}.");
        var keyValues = key.Properties.Select(x => entry.Property(x.Name).CurrentValue).ToArray();
        var existing = await set.FindAsync(keyValues, cancellationToken);
        if (existing is null)
        {
            set.Add(entity);
        }
        else
        {
            dbContext.Entry(existing).CurrentValues.SetValues(entity);
        }

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

public sealed class SqlServerQubesWeightAuditRepository(IntradayDbContext dbContext) : IQubesWeightAuditRepository
{
    public Task<QubesWeightAuditBatch?> GetByRunIdAsync(string qubesRunId, CancellationToken cancellationToken)
        => dbContext.QubesWeightAuditBatches.AsNoTracking().FirstOrDefaultAsync(x => x.QubesRunId == qubesRunId, cancellationToken);

    public async Task<IReadOnlyList<QubesRawWeightAuditRow>> GetRawRowsAsync(QubesWeightAuditBatchId auditBatchId, CancellationToken cancellationToken)
        => await dbContext.QubesRawWeightAuditRows.AsNoTracking()
            .Where(x => x.AuditBatchId == auditBatchId)
            .OrderBy(x => x.RowNumber)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<QubesNormalizedWeightAuditRow>> GetNormalizedRowsAsync(QubesWeightAuditBatchId auditBatchId, CancellationToken cancellationToken)
        => await dbContext.QubesNormalizedWeightAuditRows.AsNoTracking()
            .Where(x => x.AuditBatchId == auditBatchId)
            .OrderBy(x => x.Symbol)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(QubesWeightAuditBatch batch, IReadOnlyList<QubesRawWeightAuditRow> rawRows, IReadOnlyList<QubesNormalizedWeightAuditRow> normalizedRows, CancellationToken cancellationToken)
    {
        if (await dbContext.QubesWeightAuditBatches.AnyAsync(x => x.QubesRunId == batch.QubesRunId, cancellationToken))
        {
            return;
        }

        dbContext.QubesWeightAuditBatches.Add(batch);
        dbContext.QubesRawWeightAuditRows.AddRange(rawRows);
        dbContext.QubesNormalizedWeightAuditRows.AddRange(normalizedRows);
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

public sealed class SqlServerOperationalJobRepository(IntradayDbContext dbContext) : IOperationalJobRepository
{
    public async Task AddDefinitionAsync(OperationalJobDefinition definition, CancellationToken cancellationToken)
    {
        if (!await dbContext.OperationalJobDefinitions.AnyAsync(x => x.Id == definition.Id, cancellationToken))
        {
            dbContext.OperationalJobDefinitions.Add(definition);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<OperationalJobDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
        => await dbContext.OperationalJobDefinitions.AsNoTracking().OrderBy(x => x.JobType).ToListAsync(cancellationToken);

    public Task<OperationalJobDefinition?> GetDefinitionAsync(OperationalJobDefinitionId id, CancellationToken cancellationToken)
        => dbContext.OperationalJobDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<OperationalJobDefinition?> GetDefinitionByTypeAsync(OperationalJobType jobType, CancellationToken cancellationToken)
        => dbContext.OperationalJobDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.JobType == jobType, cancellationToken);

    public async Task AddRunAsync(OperationalJobRun run, CancellationToken cancellationToken)
    {
        dbContext.OperationalJobRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRunAsync(OperationalJobRun run, CancellationToken cancellationToken)
    {
        var existing = await dbContext.OperationalJobRuns.FirstOrDefaultAsync(x => x.Id == run.Id, cancellationToken);
        if (existing is null) return;
        dbContext.Entry(existing).CurrentValues.SetValues(run);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<OperationalJobRun?> GetRunAsync(OperationalJobRunId id, CancellationToken cancellationToken)
        => dbContext.OperationalJobRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<OperationalJobRun>> GetRunsAsync(OperationalJobRunFilter filter, CancellationToken cancellationToken)
    {
        var query = dbContext.OperationalJobRuns.AsNoTracking().AsQueryable();
        if (filter.Status is not null) query = query.Where(x => x.Status == filter.Status);
        if (filter.JobType is not null) query = query.Where(x => x.JobType == filter.JobType);
        if (filter.FromUtc is not null) query = query.Where(x => x.StartedAtUtc >= filter.FromUtc);
        if (filter.ToUtc is not null) query = query.Where(x => x.StartedAtUtc <= filter.ToUtc);
        return await query.OrderByDescending(x => x.StartedAtUtc).Take(Math.Clamp(filter.Limit, 1, 500)).ToListAsync(cancellationToken);
    }

    public async Task AddStepAsync(OperationalJobStep step, CancellationToken cancellationToken)
    {
        dbContext.OperationalJobSteps.Add(step);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateStepAsync(OperationalJobStep step, CancellationToken cancellationToken)
    {
        var existing = await dbContext.OperationalJobSteps.FirstOrDefaultAsync(x => x.Id == step.Id, cancellationToken);
        if (existing is null) return;
        dbContext.Entry(existing).CurrentValues.SetValues(step);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OperationalJobStep>> GetStepsAsync(OperationalJobRunId jobRunId, CancellationToken cancellationToken)
        => await dbContext.OperationalJobSteps.AsNoTracking()
            .Where(x => x.JobRunId == jobRunId)
            .OrderBy(x => x.StartedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task AddEventAsync(OperationalJobRunEvent jobEvent, CancellationToken cancellationToken)
    {
        dbContext.OperationalJobRunEvents.Add(jobEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OperationalJobRunEvent>> GetEventsAsync(OperationalJobRunId jobRunId, CancellationToken cancellationToken)
        => await dbContext.OperationalJobRunEvents.AsNoTracking()
            .Where(x => x.JobRunId == jobRunId)
            .OrderBy(x => x.OccurredAtUtc)
            .ToListAsync(cancellationToken);
}

public sealed class SqlServerOperationalRunbookRepository(IntradayDbContext dbContext) : IOperationalRunbookRepository
{
    public async Task AddDefinitionAsync(OperationalRunbookDefinition definition, IReadOnlyList<OperationalRunbookStepDefinition> steps, CancellationToken cancellationToken)
    {
        if (!await dbContext.OperationalRunbookDefinitions.AnyAsync(x => x.Id == definition.Id, cancellationToken))
        {
            dbContext.OperationalRunbookDefinitions.Add(definition);
        }

        foreach (var step in steps)
        {
            if (!await dbContext.OperationalRunbookStepDefinitions.AnyAsync(x => x.Id == step.Id, cancellationToken))
            {
                dbContext.OperationalRunbookStepDefinitions.Add(step);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OperationalRunbookDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken)
        => await dbContext.OperationalRunbookDefinitions.AsNoTracking().OrderBy(x => x.RunbookType).ToListAsync(cancellationToken);

    public Task<OperationalRunbookDefinition?> GetDefinitionAsync(OperationalRunbookDefinitionId id, CancellationToken cancellationToken)
        => dbContext.OperationalRunbookDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<OperationalRunbookDefinition?> GetDefinitionByTypeAsync(OperationalRunbookType runbookType, CancellationToken cancellationToken)
        => dbContext.OperationalRunbookDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.RunbookType == runbookType, cancellationToken);

    public async Task<IReadOnlyList<OperationalRunbookStepDefinition>> GetStepDefinitionsAsync(OperationalRunbookDefinitionId definitionId, CancellationToken cancellationToken)
        => await dbContext.OperationalRunbookStepDefinitions.AsNoTracking()
            .Where(x => x.RunbookDefinitionId == definitionId)
            .OrderBy(x => x.StepOrder)
            .ToListAsync(cancellationToken);

    public async Task AddRunAsync(OperationalRunbookRun run, IReadOnlyList<OperationalRunbookStepRun> steps, CancellationToken cancellationToken)
    {
        dbContext.OperationalRunbookRuns.Add(run);
        dbContext.OperationalRunbookStepRuns.AddRange(steps);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRunAsync(OperationalRunbookRun run, CancellationToken cancellationToken)
    {
        var existing = await dbContext.OperationalRunbookRuns.FirstOrDefaultAsync(x => x.Id == run.Id, cancellationToken);
        if (existing is null) return;
        dbContext.Entry(existing).CurrentValues.SetValues(run);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<OperationalRunbookRun?> GetRunAsync(OperationalRunbookRunId id, CancellationToken cancellationToken)
        => dbContext.OperationalRunbookRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<OperationalRunbookRun>> GetRunsAsync(OperationalRunbookRunFilter filter, CancellationToken cancellationToken)
    {
        var query = dbContext.OperationalRunbookRuns.AsNoTracking().AsQueryable();
        if (filter.RunbookType is not null) query = query.Where(x => x.RunbookType == filter.RunbookType);
        if (filter.Status is not null) query = query.Where(x => x.Status == filter.Status);
        if (filter.FromUtc is not null) query = query.Where(x => x.StartedAtUtc >= filter.FromUtc);
        if (filter.ToUtc is not null) query = query.Where(x => x.StartedAtUtc <= filter.ToUtc);
        return await query.OrderByDescending(x => x.StartedAtUtc).Take(Math.Clamp(filter.Limit, 1, 500)).ToListAsync(cancellationToken);
    }

    public async Task UpdateStepRunAsync(OperationalRunbookStepRun step, CancellationToken cancellationToken)
    {
        var existing = await dbContext.OperationalRunbookStepRuns.FirstOrDefaultAsync(x => x.Id == step.Id, cancellationToken);
        if (existing is null) return;
        dbContext.Entry(existing).CurrentValues.SetValues(step);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OperationalRunbookStepRun>> GetStepRunsAsync(OperationalRunbookRunId runId, CancellationToken cancellationToken)
        => await dbContext.OperationalRunbookStepRuns.AsNoTracking()
            .Where(x => x.RunbookRunId == runId)
            .OrderBy(x => x.StepOrder)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OperationalScheduleDefinition>> GetSchedulesAsync(CancellationToken cancellationToken)
        => await dbContext.OperationalScheduleDefinitions.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public async Task UpsertScheduleAsync(OperationalScheduleDefinition schedule, CancellationToken cancellationToken)
    {
        var existing = await dbContext.OperationalScheduleDefinitions.FirstOrDefaultAsync(x => x.Id == schedule.Id, cancellationToken);
        if (existing is null) dbContext.OperationalScheduleDefinitions.Add(schedule);
        else dbContext.Entry(existing).CurrentValues.SetValues(schedule);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SqlServerLmaxShadowRepository(IntradayDbContext dbContext) : ILmaxShadowRepository
{
    public async Task AddReplayRunAsync(LmaxShadowReplayRun run, CancellationToken cancellationToken)
    {
        dbContext.LmaxShadowReplayRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateReplayRunAsync(LmaxShadowReplayRun run, CancellationToken cancellationToken)
    {
        var existing = await dbContext.LmaxShadowReplayRuns.FirstAsync(x => x.Id == run.Id, cancellationToken);
        dbContext.Entry(existing).CurrentValues.SetValues(run);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<LmaxShadowReplayRun?> GetReplayRunAsync(LmaxShadowReplayRunId id, CancellationToken cancellationToken)
        => dbContext.LmaxShadowReplayRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<LmaxShadowReplayRun>> GetReplayRunsAsync(LmaxShadowReplayRunFilter filter, CancellationToken cancellationToken)
    {
        var query = dbContext.LmaxShadowReplayRuns.AsNoTracking().AsQueryable();
        if (filter.Status is not null) query = query.Where(x => x.Status == filter.Status);
        if (filter.InputSource is not null) query = query.Where(x => x.InputSource == filter.InputSource);
        if (filter.FromUtc is not null) query = query.Where(x => x.StartedAtUtc >= filter.FromUtc);
        if (filter.ToUtc is not null) query = query.Where(x => x.StartedAtUtc <= filter.ToUtc);
        return await query.OrderByDescending(x => x.StartedAtUtc).Take(Math.Clamp(filter.Limit, 1, 500)).ToListAsync(cancellationToken);
    }

    public async Task AddObservationAsync(LmaxShadowObservation observation, CancellationToken cancellationToken)
    {
        dbContext.LmaxShadowObservations.Add(observation);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateObservationAsync(LmaxShadowObservation observation, CancellationToken cancellationToken)
    {
        var existing = await dbContext.LmaxShadowObservations.FirstAsync(x => x.Id == observation.Id, cancellationToken);
        dbContext.Entry(existing).CurrentValues.SetValues(observation);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<LmaxShadowObservation?> GetObservationAsync(LmaxShadowObservationId id, CancellationToken cancellationToken)
        => dbContext.LmaxShadowObservations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<LmaxShadowObservation>> GetObservationsAsync(LmaxShadowObservationFilter filter, CancellationToken cancellationToken)
    {
        var query = dbContext.LmaxShadowObservations.AsNoTracking().AsQueryable();
        if (filter.ReplayRunId is not null) query = query.Where(x => x.ReplayRunId == filter.ReplayRunId);
        if (filter.Severity is not null) query = query.Where(x => x.Severity == filter.Severity);
        if (filter.Status is not null) query = query.Where(x => x.Status == filter.Status);
        if (filter.Type is not null) query = query.Where(x => x.Type == filter.Type);
        if (!string.IsNullOrWhiteSpace(filter.Symbol)) query = query.Where(x => x.Symbol == filter.Symbol);
        if (!string.IsNullOrWhiteSpace(filter.BrokerExecutionId)) query = query.Where(x => x.BrokerExecutionId == filter.BrokerExecutionId);
        if (!string.IsNullOrWhiteSpace(filter.BrokerOrderId)) query = query.Where(x => x.BrokerOrderId == filter.BrokerOrderId);
        if (!string.IsNullOrWhiteSpace(filter.ClientOrderId)) query = query.Where(x => x.ClientOrderId == filter.ClientOrderId);
        if (!string.IsNullOrWhiteSpace(filter.Fingerprint)) query = query.Where(x => x.Fingerprint == filter.Fingerprint);
        return await query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(filter.Limit, 1, 500)).ToListAsync(cancellationToken);
    }
}
