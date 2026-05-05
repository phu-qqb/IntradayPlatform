using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.SqlServer.Migrations;

public partial class AddOperationalRunbooksAndLocalScheduler : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OperationalRunbookDefinitions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                RunbookType = table.Column<int>(type: "int", nullable: false),
                Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                IsRerunnable = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_OperationalRunbookDefinitions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "OperationalRunbookRuns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RunbookDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RunbookType = table.Column<int>(type: "int", nullable: false),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                TriggerType = table.Column<int>(type: "int", nullable: false),
                TriggeredByOperatorId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                TriggeredByDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                DurationMs = table.Column<long>(type: "bigint", nullable: true),
                CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                InputJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                OutputJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ErrorMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                RetryOfRunbookRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RetryCount = table.Column<int>(type: "int", nullable: false),
                CanRetry = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OperationalRunbookRuns", x => x.Id);
                table.ForeignKey("FK_OperationalRunbookRuns_OperationalRunbookDefinitions_RunbookDefinitionId", x => x.RunbookDefinitionId, "OperationalRunbookDefinitions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_OperationalRunbookRuns_OperationalRunbookRuns_RetryOfRunbookRunId", x => x.RetryOfRunbookRunId, "OperationalRunbookRuns", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "OperationalRunbookStepDefinitions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RunbookDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StepOrder = table.Column<int>(type: "int", nullable: false),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                JobType = table.Column<int>(type: "int", nullable: true),
                GateType = table.Column<int>(type: "int", nullable: false),
                IsRequired = table.Column<bool>(type: "bit", nullable: false),
                ContinueOnFailure = table.Column<bool>(type: "bit", nullable: false),
                InputTemplateJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OperationalRunbookStepDefinitions", x => x.Id);
                table.ForeignKey("FK_OperationalRunbookStepDefinitions_OperationalRunbookDefinitions_RunbookDefinitionId", x => x.RunbookDefinitionId, "OperationalRunbookDefinitions", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "OperationalScheduleDefinitions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                RunbookDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                CronExpression = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                FixedIntervalMinutes = table.Column<int>(type: "int", nullable: true),
                TimeZoneId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                NextRunAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LastRunAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OperationalScheduleDefinitions", x => x.Id);
                table.ForeignKey("FK_OperationalScheduleDefinitions_OperationalRunbookDefinitions_RunbookDefinitionId", x => x.RunbookDefinitionId, "OperationalRunbookDefinitions", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "OperationalRunbookStepRuns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RunbookRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StepDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                StepOrder = table.Column<int>(type: "int", nullable: false),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                JobRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                DurationMs = table.Column<long>(type: "bigint", nullable: true),
                Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                InputJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                OutputJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ErrorMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OperationalRunbookStepRuns", x => x.Id);
                table.ForeignKey("FK_OperationalRunbookStepRuns_OperationalJobRuns_JobRunId", x => x.JobRunId, "OperationalJobRuns", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_OperationalRunbookStepRuns_OperationalRunbookRuns_RunbookRunId", x => x.RunbookRunId, "OperationalRunbookRuns", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_OperationalRunbookStepRuns_OperationalRunbookStepDefinitions_StepDefinitionId", x => x.StepDefinitionId, "OperationalRunbookStepDefinitions", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_OperationalRunbookDefinitions_RunbookType_IsEnabled", "OperationalRunbookDefinitions", new[] { "RunbookType", "IsEnabled" });
        migrationBuilder.CreateIndex("IX_OperationalRunbookRuns_CorrelationId", "OperationalRunbookRuns", "CorrelationId");
        migrationBuilder.CreateIndex("IX_OperationalRunbookRuns_RetryOfRunbookRunId", "OperationalRunbookRuns", "RetryOfRunbookRunId");
        migrationBuilder.CreateIndex("IX_OperationalRunbookRuns_RunbookDefinitionId", "OperationalRunbookRuns", "RunbookDefinitionId");
        migrationBuilder.CreateIndex("IX_OperationalRunbookRuns_RunbookType_StartedAtUtc", "OperationalRunbookRuns", new[] { "RunbookType", "StartedAtUtc" });
        migrationBuilder.CreateIndex("IX_OperationalRunbookRuns_Status", "OperationalRunbookRuns", "Status");
        migrationBuilder.CreateIndex("IX_OperationalRunbookStepDefinitions_RunbookDefinitionId", "OperationalRunbookStepDefinitions", "RunbookDefinitionId");
        migrationBuilder.CreateIndex("IX_OperationalRunbookStepRuns_JobRunId", "OperationalRunbookStepRuns", "JobRunId");
        migrationBuilder.CreateIndex("IX_OperationalRunbookStepRuns_RunbookRunId", "OperationalRunbookStepRuns", "RunbookRunId");
        migrationBuilder.CreateIndex("IX_OperationalRunbookStepRuns_StepDefinitionId", "OperationalRunbookStepRuns", "StepDefinitionId");
        migrationBuilder.CreateIndex("IX_OperationalScheduleDefinitions_IsEnabled_NextRunAtUtc", "OperationalScheduleDefinitions", new[] { "IsEnabled", "NextRunAtUtc" });
        migrationBuilder.CreateIndex("IX_OperationalScheduleDefinitions_RunbookDefinitionId", "OperationalScheduleDefinitions", "RunbookDefinitionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("OperationalRunbookStepRuns");
        migrationBuilder.DropTable("OperationalScheduleDefinitions");
        migrationBuilder.DropTable("OperationalRunbookRuns");
        migrationBuilder.DropTable("OperationalRunbookStepDefinitions");
        migrationBuilder.DropTable("OperationalRunbookDefinitions");
    }
}
