using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddQubesWeightAuditPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QubesWeightAuditBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QubesRunId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SourceSystem = table.Column<int>(type: "int", nullable: false),
                    ProducedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CadenceMinutes = table.Column<int>(type: "int", nullable: false),
                    RawRowCount = table.Column<int>(type: "int", nullable: false),
                    NormalizedRowCount = table.Column<int>(type: "int", nullable: false),
                    ModelWeightBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PromotedModelRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QubesWeightAuditBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QubesWeightAuditBatches_ModelRuns_PromotedModelRunId",
                        column: x => x.PromotedModelRunId,
                        principalTable: "ModelRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QubesWeightAuditBatches_ModelWeightBatches_ModelWeightBatchId",
                        column: x => x.ModelWeightBatchId,
                        principalTable: "ModelWeightBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QubesRawWeightAuditRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    BloombergTicker = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Pair = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BaseCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    QuoteCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QubesRawWeightAuditRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QubesRawWeightAuditRows_QubesWeightAuditBatches_AuditBatchId",
                        column: x => x.AuditBatchId,
                        principalTable: "QubesWeightAuditBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QubesNormalizedWeightAuditRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NormalizedTicker = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    ModelWeightBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModelRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetWeightInstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PromotionStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QubesNormalizedWeightAuditRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QubesNormalizedWeightAuditRows_Instruments_TargetWeightInstrumentId",
                        column: x => x.TargetWeightInstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QubesNormalizedWeightAuditRows_ModelRuns_ModelRunId",
                        column: x => x.ModelRunId,
                        principalTable: "ModelRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QubesNormalizedWeightAuditRows_ModelWeightBatches_ModelWeightBatchId",
                        column: x => x.ModelWeightBatchId,
                        principalTable: "ModelWeightBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QubesNormalizedWeightAuditRows_QubesWeightAuditBatches_AuditBatchId",
                        column: x => x.AuditBatchId,
                        principalTable: "QubesWeightAuditBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QubesWeightAuditBatches_ModelWeightBatchId",
                table: "QubesWeightAuditBatches",
                column: "ModelWeightBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_QubesWeightAuditBatches_PromotedModelRunId",
                table: "QubesWeightAuditBatches",
                column: "PromotedModelRunId");

            migrationBuilder.CreateIndex(
                name: "IX_QubesWeightAuditBatches_QubesRunId",
                table: "QubesWeightAuditBatches",
                column: "QubesRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QubesWeightAuditBatches_SourceSystem_ProducedAtUtc",
                table: "QubesWeightAuditBatches",
                columns: new[] { "SourceSystem", "ProducedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_QubesRawWeightAuditRows_AuditBatchId_RowNumber",
                table: "QubesRawWeightAuditRows",
                columns: new[] { "AuditBatchId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QubesNormalizedWeightAuditRows_AuditBatchId_Symbol",
                table: "QubesNormalizedWeightAuditRows",
                columns: new[] { "AuditBatchId", "Symbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QubesNormalizedWeightAuditRows_ModelRunId",
                table: "QubesNormalizedWeightAuditRows",
                column: "ModelRunId");

            migrationBuilder.CreateIndex(
                name: "IX_QubesNormalizedWeightAuditRows_ModelWeightBatchId",
                table: "QubesNormalizedWeightAuditRows",
                column: "ModelWeightBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_QubesNormalizedWeightAuditRows_TargetWeightInstrumentId",
                table: "QubesNormalizedWeightAuditRows",
                column: "TargetWeightInstrumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "QubesNormalizedWeightAuditRows");
            migrationBuilder.DropTable(name: "QubesRawWeightAuditRows");
            migrationBuilder.DropTable(name: "QubesWeightAuditBatches");
        }
    }
}
