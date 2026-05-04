using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskControlCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RiskLimitSets_FundId",
                table: "RiskLimitSets");

            migrationBuilder.AddColumn<bool>(
                name: "IsMarketDataEnabled",
                table: "Venues",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReportImportEnabled",
                table: "Venues",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTradingEnabled",
                table: "Venues",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVenueEnabled",
                table: "VenueRiskLimits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxDailyTurnoverUsd",
                table: "VenueRiskLimits",
                type: "decimal(28,10)",
                precision: 28,
                scale: 10,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MaxOrdersPerMinute",
                table: "VenueRiskLimits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "TradingWindows",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScheduleName",
                table: "TradingWindows",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "TradingWindows",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "TradingWindows",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActivatedAtUtc",
                table: "RiskLimitSets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivatedBy",
                table: "RiskLimitSets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "RiskLimitSets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "RiskLimitSets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "RiskLimitSets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveFromUtc",
                table: "RiskLimitSets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EffectiveToUtc",
                table: "RiskLimitSets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "RiskLimitSets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                table: "RiskLimitSets",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "RiskLimitSets",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetiredAtUtc",
                table: "RiskLimitSets",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetiredBy",
                table: "RiskLimitSets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "RiskLimitSets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "RiskLimitSets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "RiskLimits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "RiskLimits",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "RiskLimits",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "InstrumentId",
                table: "RiskDecisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModelRunId",
                table: "RiskDecisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RiskLimitSetId",
                table: "RiskDecisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VenueId",
                table: "RiskDecisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMarketDataEnabled",
                table: "Instruments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsReportImportEnabled",
                table: "Instruments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTradingEnabled",
                table: "Instruments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTradingEnabled",
                table: "InstrumentRiskLimits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxOrdersPerDay",
                table: "InstrumentRiskLimits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "MinTradeQuantity",
                table: "InstrumentRiskLimits",
                type: "decimal(28,10)",
                precision: 28,
                scale: 10,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "RiskDecisionDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RejectReason = table.Column<int>(type: "int", nullable: true),
                    ObservedValue = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    LimitValue = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskDecisionDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskDecisionDetails_RiskDecisions_RiskDecisionId",
                        column: x => x.RiskDecisionId,
                        principalTable: "RiskDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RiskLimitSets_FundId_ModelName_IsActive",
                table: "RiskLimitSets",
                columns: new[] { "FundId", "ModelName", "IsActive" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RiskLimitSets_FundId_ModelName_Status",
                table: "RiskLimitSets",
                columns: new[] { "FundId", "ModelName", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RiskDecisions_CreatedAtUtc",
                table: "RiskDecisions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RiskDecisions_RiskLimitSetId",
                table: "RiskDecisions",
                column: "RiskLimitSetId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskDecisionDetails_RiskDecisionId_CheckName",
                table: "RiskDecisionDetails",
                columns: new[] { "RiskDecisionId", "CheckName" });

            migrationBuilder.AddForeignKey(
                name: "FK_RiskDecisions_RiskLimitSets_RiskLimitSetId",
                table: "RiskDecisions",
                column: "RiskLimitSetId",
                principalTable: "RiskLimitSets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RiskDecisions_RiskLimitSets_RiskLimitSetId",
                table: "RiskDecisions");

            migrationBuilder.DropTable(
                name: "RiskDecisionDetails");

            migrationBuilder.DropIndex(
                name: "IX_RiskLimitSets_FundId_ModelName_IsActive",
                table: "RiskLimitSets");

            migrationBuilder.DropIndex(
                name: "IX_RiskLimitSets_FundId_ModelName_Status",
                table: "RiskLimitSets");

            migrationBuilder.DropIndex(
                name: "IX_RiskDecisions_CreatedAtUtc",
                table: "RiskDecisions");

            migrationBuilder.DropIndex(
                name: "IX_RiskDecisions_RiskLimitSetId",
                table: "RiskDecisions");

            migrationBuilder.DropColumn(
                name: "IsMarketDataEnabled",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "IsReportImportEnabled",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "IsTradingEnabled",
                table: "Venues");

            migrationBuilder.DropColumn(
                name: "IsVenueEnabled",
                table: "VenueRiskLimits");

            migrationBuilder.DropColumn(
                name: "MaxDailyTurnoverUsd",
                table: "VenueRiskLimits");

            migrationBuilder.DropColumn(
                name: "MaxOrdersPerMinute",
                table: "VenueRiskLimits");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "TradingWindows");

            migrationBuilder.DropColumn(
                name: "ScheduleName",
                table: "TradingWindows");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "TradingWindows");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "TradingWindows");

            migrationBuilder.DropColumn(
                name: "ActivatedAtUtc",
                table: "RiskLimitSets");

            migrationBuilder.DropColumn(
                name: "ActivatedBy",
                table: "RiskLimitSets");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "RiskLimitSets");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "RiskLimitSets");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "RiskLimitSets");

            migrationBuilder.DropColumn(
                name: "EffectiveFromUtc",
                table: "RiskLimitSets");

            migrationBuilder.DropColumn(
                name: "EffectiveToUtc",
                table: "RiskLimitSets");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "RiskLimitSets");

            migrationBuilder.DropColumn(
                name: "ModelName",
                table: "RiskLimitSets");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "RiskLimitSets");

            migrationBuilder.DropColumn(
                name: "RetiredAtUtc",
                table: "RiskLimitSets");

            migrationBuilder.DropColumn(
                name: "RetiredBy",
                table: "RiskLimitSets");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "RiskLimitSets");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "RiskLimitSets");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "RiskLimits");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "RiskLimits");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "RiskLimits");

            migrationBuilder.DropColumn(
                name: "InstrumentId",
                table: "RiskDecisions");

            migrationBuilder.DropColumn(
                name: "ModelRunId",
                table: "RiskDecisions");

            migrationBuilder.DropColumn(
                name: "RiskLimitSetId",
                table: "RiskDecisions");

            migrationBuilder.DropColumn(
                name: "VenueId",
                table: "RiskDecisions");

            migrationBuilder.DropColumn(
                name: "IsMarketDataEnabled",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "IsReportImportEnabled",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "IsTradingEnabled",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "IsTradingEnabled",
                table: "InstrumentRiskLimits");

            migrationBuilder.DropColumn(
                name: "MaxOrdersPerDay",
                table: "InstrumentRiskLimits");

            migrationBuilder.DropColumn(
                name: "MinTradeQuantity",
                table: "InstrumentRiskLimits");

            migrationBuilder.CreateIndex(
                name: "IX_RiskLimitSets_FundId",
                table: "RiskLimitSets",
                column: "FundId",
                unique: true);
        }
    }
}
