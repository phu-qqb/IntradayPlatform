using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bPersistenceContractTests
{
    [Theory]
    [InlineData(typeof(PmsArch7bQualificationRunRow), "arch7b_qualification_runs")]
    [InlineData(typeof(PmsArch7bFixSessionEventRow), "arch7b_fix_session_events")]
    [InlineData(typeof(PmsArch7bOrderSendLedgerRow), "arch7b_order_send_ledger")]
    [InlineData(typeof(PmsArch7bExecutionReportRow), "arch7b_execution_reports")]
    [InlineData(typeof(PmsArch7bFillRow), "arch7b_fills")]
    [InlineData(typeof(PmsArch7bPositionLedgerEventRow), "arch7b_position_ledger_events")]
    [InlineData(typeof(PmsArch7bFinalReconciliationRow), "arch7b_final_reconciliations")]
    public void Arch7b_entities_map_only_to_the_existing_pms_shadow_schema(Type type, string table)
    {
        using var context = Context();
        var entity = context.Model.FindEntityType(type);

        Assert.NotNull(entity);
        Assert.Equal(PmsShadowStateContract.SchemaName, entity.GetSchema());
        Assert.Equal(table, entity.GetTableName());
    }

    [Fact]
    public void Qualification_run_is_restricted_to_one_existing_arch7a_child_order()
    {
        using var context = Context();
        var entity = context.Model.FindEntityType(typeof(PmsArch7bQualificationRunRow))!;
        var childForeignKey = Assert.Single(entity.GetForeignKeys());

        Assert.Equal(typeof(PmsShadowChildOrderRow), childForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, childForeignKey.DeleteBehavior);
        Assert.True(entity.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(PmsArch7bQualificationRunRow.ChildOrderId)])).IsUnique);
    }

    [Fact]
    public void Every_arch7b_relationship_is_restrict_and_no_existing_entity_is_retargeted()
    {
        using var context = Context();
        var arch7bTypes = new HashSet<Type>
        {
            typeof(PmsArch7bQualificationRunRow),
            typeof(PmsArch7bFixSessionEventRow),
            typeof(PmsArch7bOrderSendLedgerRow),
            typeof(PmsArch7bExecutionReportRow),
            typeof(PmsArch7bFillRow),
            typeof(PmsArch7bPositionLedgerEventRow),
            typeof(PmsArch7bFinalReconciliationRow)
        };
        var foreignKeys = context.Model.GetEntityTypes()
            .Where(entity => arch7bTypes.Contains(entity.ClrType))
            .SelectMany(entity => entity.GetForeignKeys())
            .ToArray();

        Assert.Equal(9, foreignKeys.Length);
        Assert.All(foreignKeys, foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        Assert.All(foreignKeys, foreignKey => Assert.Contains(
            foreignKey.PrincipalEntityType.ClrType,
            arch7bTypes.Append(typeof(PmsShadowChildOrderRow))));
    }

    [Fact]
    public void Execution_report_identity_is_unique_by_raw_message_exec_id_and_fix_sequence()
    {
        using var context = Context();
        var entity = context.Model.FindEntityType(typeof(PmsArch7bExecutionReportRow))!;
        var uniqueIndexes = entity.GetIndexes().Where(index => index.IsUnique)
            .Select(index => string.Join("|", index.Properties.Select(property => property.Name)))
            .ToArray();

        Assert.Contains(nameof(PmsArch7bExecutionReportRow.RawMessageSha256), uniqueIndexes);
        Assert.Contains(
            $"{nameof(PmsArch7bExecutionReportRow.AccountId)}|{nameof(PmsArch7bExecutionReportRow.ExecId)}",
            uniqueIndexes);
        Assert.Contains(
            $"{nameof(PmsArch7bExecutionReportRow.SessionId)}|{nameof(PmsArch7bExecutionReportRow.FixSequenceNumber)}",
            uniqueIndexes);
    }

    [Fact]
    public void Fill_and_position_ledger_are_one_to_one_with_validated_sources()
    {
        using var context = Context();
        var fill = context.Model.FindEntityType(typeof(PmsArch7bFillRow))!;
        var ledger = context.Model.FindEntityType(typeof(PmsArch7bPositionLedgerEventRow))!;

        Assert.True(fill.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(PmsArch7bFillRow.ExecutionReportId)])).IsUnique);
        Assert.True(ledger.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(PmsArch7bPositionLedgerEventRow.FillId)])).IsUnique);
    }

    [Fact]
    public void Demo_account_no_real_account_and_final_flat_checks_are_in_the_model()
    {
        using var context = Context();
        var model = context.GetService<IDesignTimeModel>().Model;
        var runChecks = model.FindEntityType(typeof(PmsArch7bQualificationRunRow))!
            .GetCheckConstraints().Select(check => check.Sql).ToArray();
        var reportChecks = model.FindEntityType(typeof(PmsArch7bExecutionReportRow))!
            .GetCheckConstraints().Select(check => check.Sql).ToArray();
        var reconciliationChecks = model.FindEntityType(typeof(PmsArch7bFinalReconciliationRow))!
            .GetCheckConstraints().Select(check => check.Sql).ToArray();

        Assert.Contains(runChecks, sql =>
            sql.Contains(Arch7bKnownOrderQualificationPolicy.DemoAccountId, StringComparison.Ordinal) &&
            sql.Contains(Arch7bKnownOrderQualificationPolicy.ForbiddenRealAccountId, StringComparison.Ordinal));
        Assert.Contains(reportChecks, sql =>
            sql.Contains(Arch7bKnownOrderQualificationPolicy.DemoAccountId, StringComparison.Ordinal) &&
            sql.Contains(Arch7bKnownOrderQualificationPolicy.ForbiddenRealAccountId, StringComparison.Ordinal));
        Assert.Contains(reconciliationChecks, sql =>
            sql.Contains("internal_ledger_quantity = 0", StringComparison.Ordinal) &&
            sql.Contains("broker_residual_quantity = 0", StringComparison.Ordinal) &&
            sql.Contains("critical_break_count = 0", StringComparison.Ordinal));
    }

    [Fact]
    public void Quantity_and_price_columns_use_explicit_contract_precision()
    {
        using var context = Context();
        var report = context.Model.FindEntityType(typeof(PmsArch7bExecutionReportRow))!;

        AssertPrecision(report, nameof(PmsArch7bExecutionReportRow.OrderQuantity), 28, 8);
        AssertPrecision(report, nameof(PmsArch7bExecutionReportRow.LastQuantity), 28, 8);
        AssertPrecision(report, nameof(PmsArch7bExecutionReportRow.LastPrice), 38, 28);
        AssertPrecision(report, nameof(PmsArch7bExecutionReportRow.AveragePrice), 38, 28);
        AssertPrecision(report, nameof(PmsArch7bExecutionReportRow.LimitPrice), 38, 28);
    }

    [Fact]
    public void Arch7b_rows_are_covered_by_the_existing_append_only_guard()
    {
        using var context = Context();
        var row = new PmsArch7bFixSessionEventRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "session",
            "LOGON",
            1,
            new string('a', 64),
            DateTimeOffset.UtcNow);
        context.Attach(row);
        context.Entry(row).State = EntityState.Modified;

        var error = Assert.Throws<InvalidOperationException>(() => context.SaveChanges());

        Assert.Equal("PMS_SHADOW_FACTS_ARE_APPEND_ONLY", error.Message);
    }

    private static void AssertPrecision(
        IEntityType entity,
        string propertyName,
        int precision,
        int scale)
    {
        var property = entity.FindProperty(propertyName)!;
        Assert.Equal(precision, property.GetPrecision());
        Assert.Equal(scale, property.GetScale());
    }

    private static PmsShadowDbContext Context()
        => new PmsShadowDesignTimeDbContextFactory().CreateDbContext([]);
}
