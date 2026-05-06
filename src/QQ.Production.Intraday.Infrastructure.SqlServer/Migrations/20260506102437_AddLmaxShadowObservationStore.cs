using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddLmaxShadowObservationStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LmaxShadowObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReplayRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Symbol = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BrokerExecutionId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    BrokerOrderId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ClientOrderId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    InternalFillId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InternalOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LmaxPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InternalPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DifferenceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LmaxShadowObservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LmaxShadowReplayRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InputSource = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    InputJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OutputJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ObservationCount = table.Column<int>(type: "int", nullable: false),
                    BlockingObservationCount = table.Column<int>(type: "int", nullable: false),
                    WarningObservationCount = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LmaxShadowReplayRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LmaxShadowObservations_BrokerExecutionId",
                table: "LmaxShadowObservations",
                column: "BrokerExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxShadowObservations_BrokerOrderId",
                table: "LmaxShadowObservations",
                column: "BrokerOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxShadowObservations_ClientOrderId",
                table: "LmaxShadowObservations",
                column: "ClientOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxShadowObservations_CreatedAtUtc",
                table: "LmaxShadowObservations",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxShadowObservations_InstrumentId",
                table: "LmaxShadowObservations",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxShadowObservations_ReplayRunId",
                table: "LmaxShadowObservations",
                column: "ReplayRunId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxShadowObservations_Severity",
                table: "LmaxShadowObservations",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxShadowObservations_Status",
                table: "LmaxShadowObservations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxShadowObservations_Type",
                table: "LmaxShadowObservations",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxShadowReplayRuns_CorrelationId",
                table: "LmaxShadowReplayRuns",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxShadowReplayRuns_CreatedAtUtc",
                table: "LmaxShadowReplayRuns",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxShadowReplayRuns_InputSource",
                table: "LmaxShadowReplayRuns",
                column: "InputSource");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxShadowReplayRuns_Status",
                table: "LmaxShadowReplayRuns",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LmaxShadowObservations");

            migrationBuilder.DropTable(
                name: "LmaxShadowReplayRuns");
        }
    }
}
