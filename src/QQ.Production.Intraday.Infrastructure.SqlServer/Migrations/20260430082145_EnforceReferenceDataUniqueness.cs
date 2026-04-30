using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class EnforceReferenceDataUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VenueInstrumentMappings_VenueId",
                table: "VenueInstrumentMappings");

            migrationBuilder.DropIndex(
                name: "IX_TradingWindows_FundId",
                table: "TradingWindows");

            migrationBuilder.DropIndex(
                name: "IX_RiskLimitSets_FundId",
                table: "RiskLimitSets");

            migrationBuilder.DropIndex(
                name: "IX_BrokerAccounts_FundId",
                table: "BrokerAccounts");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Venues",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "VenueSymbol",
                table: "VenueInstrumentMappings",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ModelName",
                table: "TradingWindows",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "RiskLimits",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Symbol",
                table: "Instruments",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Funds",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "AccountCode",
                table: "BrokerAccounts",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Venues_Name",
                table: "Venues",
                column: "Name",
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_VenueRiskLimits_RiskLimitSetId_VenueId",
                table: "VenueRiskLimits",
                columns: new[] { "RiskLimitSetId", "VenueId" },
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_VenueInstrumentMappings_VenueId_InstrumentId",
                table: "VenueInstrumentMappings",
                columns: new[] { "VenueId", "InstrumentId" },
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_VenueInstrumentMappings_VenueId_VenueSymbol",
                table: "VenueInstrumentMappings",
                columns: new[] { "VenueId", "VenueSymbol" },
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TradingWindows_FundId_ModelName_DayOfWeek",
                table: "TradingWindows",
                columns: new[] { "FundId", "ModelName", "DayOfWeek" },
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_RiskLimitSets_FundId",
                table: "RiskLimitSets",
                column: "FundId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskLimits_RiskLimitSetId_Name",
                table: "RiskLimits",
                columns: new[] { "RiskLimitSetId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_Symbol_AssetClass",
                table: "Instruments",
                columns: new[] { "Symbol", "AssetClass" },
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentRiskLimits_RiskLimitSetId_InstrumentId",
                table: "InstrumentRiskLimits",
                columns: new[] { "RiskLimitSetId", "InstrumentId" },
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Funds_Name",
                table: "Funds",
                column: "Name",
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_BrokerAccounts_FundId_AccountCode",
                table: "BrokerAccounts",
                columns: new[] { "FundId", "AccountCode" },
                unique: true,
                filter: "[IsEnabled] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Venues_Name",
                table: "Venues");

            migrationBuilder.DropIndex(
                name: "IX_VenueRiskLimits_RiskLimitSetId_VenueId",
                table: "VenueRiskLimits");

            migrationBuilder.DropIndex(
                name: "IX_VenueInstrumentMappings_VenueId_InstrumentId",
                table: "VenueInstrumentMappings");

            migrationBuilder.DropIndex(
                name: "IX_VenueInstrumentMappings_VenueId_VenueSymbol",
                table: "VenueInstrumentMappings");

            migrationBuilder.DropIndex(
                name: "IX_TradingWindows_FundId_ModelName_DayOfWeek",
                table: "TradingWindows");

            migrationBuilder.DropIndex(
                name: "IX_RiskLimitSets_FundId",
                table: "RiskLimitSets");

            migrationBuilder.DropIndex(
                name: "IX_RiskLimits_RiskLimitSetId_Name",
                table: "RiskLimits");

            migrationBuilder.DropIndex(
                name: "IX_Instruments_Symbol_AssetClass",
                table: "Instruments");

            migrationBuilder.DropIndex(
                name: "IX_InstrumentRiskLimits_RiskLimitSetId_InstrumentId",
                table: "InstrumentRiskLimits");

            migrationBuilder.DropIndex(
                name: "IX_Funds_Name",
                table: "Funds");

            migrationBuilder.DropIndex(
                name: "IX_BrokerAccounts_FundId_AccountCode",
                table: "BrokerAccounts");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Venues",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "VenueSymbol",
                table: "VenueInstrumentMappings",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "ModelName",
                table: "TradingWindows",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "RiskLimits",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Symbol",
                table: "Instruments",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Funds",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "AccountCode",
                table: "BrokerAccounts",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_VenueInstrumentMappings_VenueId",
                table: "VenueInstrumentMappings",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_TradingWindows_FundId",
                table: "TradingWindows",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskLimitSets_FundId",
                table: "RiskLimitSets",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_BrokerAccounts_FundId",
                table: "BrokerAccounts",
                column: "FundId");
        }
    }
}
