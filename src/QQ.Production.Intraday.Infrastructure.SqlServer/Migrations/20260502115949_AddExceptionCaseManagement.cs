using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddExceptionCaseManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExceptionCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Symbol = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    AssignedTo = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ResolvedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResolutionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WaiverReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExceptionCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExceptionCases_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExceptionCaseActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    ActorId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: true),
                    ToStatus = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExceptionCaseActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExceptionCaseActions_ExceptionCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "ExceptionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExceptionCaseLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceEntityType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SourceEntityId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExceptionCaseLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExceptionCaseLinks_ExceptionCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "ExceptionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExceptionCaseNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExceptionCaseNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExceptionCaseNotes_ExceptionCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "ExceptionCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionCaseActions_CaseId_OccurredAtUtc",
                table: "ExceptionCaseActions",
                columns: new[] { "CaseId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionCaseLinks_CaseId",
                table: "ExceptionCaseLinks",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionCaseLinks_SourceEntityType_SourceEntityId",
                table: "ExceptionCaseLinks",
                columns: new[] { "SourceEntityType", "SourceEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionCaseNotes_CaseId_CreatedAtUtc",
                table: "ExceptionCaseNotes",
                columns: new[] { "CaseId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionCases_AssignedTo",
                table: "ExceptionCases",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionCases_CorrelationId",
                table: "ExceptionCases",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionCases_CreatedAtUtc",
                table: "ExceptionCases",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionCases_EntityType_EntityId",
                table: "ExceptionCases",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionCases_InstrumentId",
                table: "ExceptionCases",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionCases_Severity",
                table: "ExceptionCases",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionCases_Source",
                table: "ExceptionCases",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionCases_Status",
                table: "ExceptionCases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ExceptionCases_Type",
                table: "ExceptionCases",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExceptionCaseActions");

            migrationBuilder.DropTable(
                name: "ExceptionCaseLinks");

            migrationBuilder.DropTable(
                name: "ExceptionCaseNotes");

            migrationBuilder.DropTable(
                name: "ExceptionCases");
        }
    }
}
