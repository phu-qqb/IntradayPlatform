using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddModelWeightSourceTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelWeightBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalBatchId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SourceSystem = table.Column<int>(type: "int", nullable: false),
                    FundCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AsOfUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FrequencyMinutes = table.Column<int>(type: "int", nullable: false),
                    NavUsd = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    TargetQuantityMode = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExpectedRowCount = table.Column<int>(type: "int", nullable: true),
                    ContentHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadyAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PromotedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RejectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PromotedModelRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelWeightBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelWeightBatches_Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelWeightBatches_ModelRuns_PromotedModelRunId",
                        column: x => x.PromotedModelRunId,
                        principalTable: "ModelRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModelWeightRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RawSecurityId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelWeightRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelWeightRows_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelWeightRows_ModelWeightBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "ModelWeightBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModelWeightValidationIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssueType = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowNumber = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelWeightValidationIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelWeightValidationIssues_ModelWeightBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "ModelWeightBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelWeightValidationIssues_ModelWeightRows_RowId",
                        column: x => x.RowId,
                        principalTable: "ModelWeightRows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelWeightBatches_FundId",
                table: "ModelWeightBatches",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelWeightBatches_PromotedModelRunId",
                table: "ModelWeightBatches",
                column: "PromotedModelRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelWeightBatches_SourceSystem_ExternalBatchId",
                table: "ModelWeightBatches",
                columns: new[] { "SourceSystem", "ExternalBatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelWeightBatches_Status_AsOfUtc_ModelName",
                table: "ModelWeightBatches",
                columns: new[] { "Status", "AsOfUtc", "ModelName" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelWeightRows_BatchId",
                table: "ModelWeightRows",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelWeightRows_BatchId_InstrumentId",
                table: "ModelWeightRows",
                columns: new[] { "BatchId", "InstrumentId" },
                unique: true,
                filter: "[InstrumentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ModelWeightRows_BatchId_RawSecurityId",
                table: "ModelWeightRows",
                columns: new[] { "BatchId", "RawSecurityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelWeightRows_BatchId_Symbol",
                table: "ModelWeightRows",
                columns: new[] { "BatchId", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelWeightRows_InstrumentId",
                table: "ModelWeightRows",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelWeightValidationIssues_BatchId_Severity_IssueType",
                table: "ModelWeightValidationIssues",
                columns: new[] { "BatchId", "Severity", "IssueType" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelWeightValidationIssues_RowId",
                table: "ModelWeightValidationIssues",
                column: "RowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelWeightValidationIssues");

            migrationBuilder.DropTable(
                name: "ModelWeightRows");

            migrationBuilder.DropTable(
                name: "ModelWeightBatches");
        }
    }
}
