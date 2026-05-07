using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(IntradayDbContext))]
    [Migration("20260506113000_AddLmaxShadowObservationFingerprint")]
    public partial class AddLmaxShadowObservationFingerprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Fingerprint",
                table: "LmaxShadowObservations",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "legacy");

            migrationBuilder.AddColumn<int>(
                name: "DuplicateEventCount",
                table: "LmaxShadowReplayRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "InputEventCount",
                table: "LmaxShadowReplayRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UniqueEventCount",
                table: "LmaxShadowReplayRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_LmaxShadowObservations_Fingerprint",
                table: "LmaxShadowObservations",
                column: "Fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxShadowObservations_ReplayRunId_Fingerprint",
                table: "LmaxShadowObservations",
                columns: new[] { "ReplayRunId", "Fingerprint" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LmaxShadowObservations_Fingerprint",
                table: "LmaxShadowObservations");

            migrationBuilder.DropIndex(
                name: "IX_LmaxShadowObservations_ReplayRunId_Fingerprint",
                table: "LmaxShadowObservations");

            migrationBuilder.DropColumn(
                name: "Fingerprint",
                table: "LmaxShadowObservations");

            migrationBuilder.DropColumn(
                name: "DuplicateEventCount",
                table: "LmaxShadowReplayRuns");

            migrationBuilder.DropColumn(
                name: "InputEventCount",
                table: "LmaxShadowReplayRuns");

            migrationBuilder.DropColumn(
                name: "UniqueEventCount",
                table: "LmaxShadowReplayRuns");
        }
    }
}
