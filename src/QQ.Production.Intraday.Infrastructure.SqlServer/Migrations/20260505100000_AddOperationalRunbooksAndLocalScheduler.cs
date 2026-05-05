using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalRunbooksAndLocalScheduler : Migration
    {
        /// <inheritdoc />
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
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalRunbookDefinitions", x => x.Id);
                });

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
                    table.ForeignKey(
                        name: "FK_OperationalRunbookRuns_OperationalRunbookDefinitions_RunbookDefinitionId",
                        column: x => x.RunbookDefinitionId,
                        principalTable: "OperationalRunbookDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalRunbookRuns_OperationalRunbookRuns_RetryOfRunbookRunId",
                        column: x => x.RetryOfRunbookRunId,
                        principalTable: "OperationalRunbookRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "FK_OperationalRunbookStepDefinitions_OperationalRunbookDefinitions_RunbookDefinitionId",
                        column: x => x.RunbookDefinitionId,
                        principalTable: "OperationalRunbookDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "FK_OperationalScheduleDefinitions_OperationalRunbookDefinitions_RunbookDefinitionId",
                        column: x => x.RunbookDefinitionId,
                        principalTable: "OperationalRunbookDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "FK_OperationalRunbookStepRuns_OperationalJobRuns_JobRunId",
                        column: x => x.JobRunId,
                        principalTable: "OperationalJobRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalRunbookStepRuns_OperationalRunbookRuns_RunbookRunId",
                        column: x => x.RunbookRunId,
                        principalTable: "OperationalRunbookRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalRunbookStepRuns_OperationalRunbookStepDefinitions_StepDefinitionId",
                        column: x => x.StepDefinitionId,
                        principalTable: "OperationalRunbookStepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalRunbookDefinitions_RunbookType_IsEnabled",
                table: "OperationalRunbookDefinitions",
                columns: new[] { "RunbookType", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalRunbookRuns_CorrelationId",
                table: "OperationalRunbookRuns",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalRunbookRuns_RetryOfRunbookRunId",
                table: "OperationalRunbookRuns",
                column: "RetryOfRunbookRunId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalRunbookRuns_RunbookDefinitionId",
                table: "OperationalRunbookRuns",
                column: "RunbookDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalRunbookRuns_RunbookType_StartedAtUtc",
                table: "OperationalRunbookRuns",
                columns: new[] { "RunbookType", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalRunbookRuns_Status",
                table: "OperationalRunbookRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalRunbookStepDefinitions_RunbookDefinitionId",
                table: "OperationalRunbookStepDefinitions",
                column: "RunbookDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalRunbookStepRuns_JobRunId",
                table: "OperationalRunbookStepRuns",
                column: "JobRunId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalRunbookStepRuns_RunbookRunId",
                table: "OperationalRunbookStepRuns",
                column: "RunbookRunId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalRunbookStepRuns_StepDefinitionId",
                table: "OperationalRunbookStepRuns",
                column: "StepDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalScheduleDefinitions_IsEnabled_NextRunAtUtc",
                table: "OperationalScheduleDefinitions",
                columns: new[] { "IsEnabled", "NextRunAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalScheduleDefinitions_RunbookDefinitionId",
                table: "OperationalScheduleDefinitions",
                column: "RunbookDefinitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationalRunbookStepRuns");

            migrationBuilder.DropTable(
                name: "OperationalScheduleDefinitions");

            migrationBuilder.DropTable(
                name: "OperationalRunbookRuns");

            migrationBuilder.DropTable(
                name: "OperationalRunbookStepDefinitions");

            migrationBuilder.DropTable(
                name: "OperationalRunbookDefinitions");
        }
    }
}
