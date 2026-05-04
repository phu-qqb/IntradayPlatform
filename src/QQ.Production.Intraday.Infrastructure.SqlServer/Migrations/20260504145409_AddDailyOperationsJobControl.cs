using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyOperationsJobControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OperationalJobDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobType = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsRerunnable = table.Column<bool>(type: "bit", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalJobDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationalJobRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JobType = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TriggerType = table.Column<int>(type: "int", nullable: false),
                    TriggeredByActorType = table.Column<int>(type: "int", nullable: false),
                    TriggeredByOperatorId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TriggeredByDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RequestId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    InputJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutputJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ExceptionCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuditEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetryOfJobRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CanRetry = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalJobRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationalJobRuns_ExceptionCases_ExceptionCaseId",
                        column: x => x.ExceptionCaseId,
                        principalTable: "ExceptionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalJobRuns_OperationalJobDefinitions_JobDefinitionId",
                        column: x => x.JobDefinitionId,
                        principalTable: "OperationalJobDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalJobRuns_OperationalJobRuns_RetryOfJobRunId",
                        column: x => x.RetryOfJobRunId,
                        principalTable: "OperationalJobRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperationalJobRuns_OperatorAuditEvents_AuditEventId",
                        column: x => x.AuditEventId,
                        principalTable: "OperatorAuditEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationalJobRunEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalJobRunEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationalJobRunEvents_OperationalJobRuns_JobRunId",
                        column: x => x.JobRunId,
                        principalTable: "OperationalJobRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationalJobSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InputJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutputJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationalJobSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationalJobSteps_OperationalJobRuns_JobRunId",
                        column: x => x.JobRunId,
                        principalTable: "OperationalJobRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalJobRunEvents_JobRunId",
                table: "OperationalJobRunEvents",
                column: "JobRunId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalJobRuns_AuditEventId",
                table: "OperationalJobRuns",
                column: "AuditEventId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalJobRuns_CorrelationId",
                table: "OperationalJobRuns",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalJobRuns_ExceptionCaseId",
                table: "OperationalJobRuns",
                column: "ExceptionCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalJobRuns_JobDefinitionId",
                table: "OperationalJobRuns",
                column: "JobDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalJobRuns_JobType_StartedAtUtc",
                table: "OperationalJobRuns",
                columns: new[] { "JobType", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationalJobRuns_RetryOfJobRunId",
                table: "OperationalJobRuns",
                column: "RetryOfJobRunId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalJobRuns_Status",
                table: "OperationalJobRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalJobRuns_TriggeredByOperatorId",
                table: "OperationalJobRuns",
                column: "TriggeredByOperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationalJobSteps_JobRunId",
                table: "OperationalJobSteps",
                column: "JobRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationalJobRunEvents");

            migrationBuilder.DropTable(
                name: "OperationalJobSteps");

            migrationBuilder.DropTable(
                name: "OperationalJobRuns");

            migrationBuilder.DropTable(
                name: "OperationalJobDefinitions");
        }
    }
}
