using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddRealLmaxEodReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalAccountId",
                table: "BrokerAccounts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EodReconciliationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HasBlockingBreaks = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EodReconciliationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EodReconciliationRuns_BrokerAccounts_BrokerAccountId",
                        column: x => x.BrokerAccountId,
                        principalTable: "BrokerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EodReconciliationRuns_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstrumentAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExternalSymbol = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExternalInstrumentId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstrumentAliases_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LmaxReportImportRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportType = table.Column<int>(type: "int", nullable: false),
                    ReportDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowCount = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ArchivedPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RejectedPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LmaxReportImportRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LmaxReportImportRuns_BrokerAccounts_BrokerAccountId",
                        column: x => x.BrokerAccountId,
                        principalTable: "BrokerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LmaxReportImportRuns_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EodReconciliationBreaks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BrokerExecutionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InternalFillId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EodReconciliationBreaks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EodReconciliationBreaks_EodReconciliationRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "EodReconciliationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EodReconciliationBreaks_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LmaxCurrencyWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BalanceNetDeposits = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    Adjustments = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    InterAccountTransfers = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    ProfitLoss = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    Commission = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    Dividends = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    Financing = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    WalletBalance = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    RateToBaseCcy = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    BaseCurrency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BalanceNetDepositsBaseUsd = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    AdjustmentsBaseUsd = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    InterAccountTransfersBaseUsd = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    ProfitLossBaseUsd = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    CommissionBaseUsd = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    DividendsBaseUsd = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    FinancingBaseUsd = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    WalletBalanceBaseUsd = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RawLine = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LmaxCurrencyWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LmaxCurrencyWallets_BrokerAccounts_BrokerAccountId",
                        column: x => x.BrokerAccountId,
                        principalTable: "BrokerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LmaxCurrencyWallets_LmaxReportImportRuns_ImportRunId",
                        column: x => x.ImportRunId,
                        principalTable: "LmaxReportImportRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LmaxCurrencyWallets_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LmaxIndividualTrades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExecutionId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MtfExecutionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TradeQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    TradePrice = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    TradeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LmaxInstrumentId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LmaxSymbol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InstructionId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    OrderId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    StopPrice = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    LimitPrice = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    OrderPlacementTimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OrderType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RemoteVenue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserPlacingOrder = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalProfitLoss = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    TotalCommission = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UnitsBoughtSold = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    NotionalValue = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    TradeUti = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RawLine = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LmaxIndividualTrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LmaxIndividualTrades_BrokerAccounts_BrokerAccountId",
                        column: x => x.BrokerAccountId,
                        principalTable: "BrokerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LmaxIndividualTrades_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LmaxIndividualTrades_LmaxReportImportRuns_ImportRunId",
                        column: x => x.ImportRunId,
                        principalTable: "LmaxReportImportRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LmaxIndividualTrades_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LmaxReportValidationIssues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssueType = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: true),
                    RawLine = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LmaxReportValidationIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LmaxReportValidationIssues_LmaxReportImportRuns_ImportRunId",
                        column: x => x.ImportRunId,
                        principalTable: "LmaxReportImportRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LmaxTradeSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateTimeUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Instrument = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Contracts = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    AveragePrice = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    CommissionRounded = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    NotionalValue = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    LmaxSymbol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserPlacingOrder = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommissionFullPrecision = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RawLine = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LmaxTradeSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LmaxTradeSummaries_BrokerAccounts_BrokerAccountId",
                        column: x => x.BrokerAccountId,
                        principalTable: "BrokerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LmaxTradeSummaries_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LmaxTradeSummaries_LmaxReportImportRuns_ImportRunId",
                        column: x => x.ImportRunId,
                        principalTable: "LmaxReportImportRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LmaxTradeSummaries_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EodReconciliationBreaks_InstrumentId",
                table: "EodReconciliationBreaks",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_EodReconciliationBreaks_RunId",
                table: "EodReconciliationBreaks",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_EodReconciliationRuns_BrokerAccountId",
                table: "EodReconciliationRuns",
                column: "BrokerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_EodReconciliationRuns_VenueId",
                table: "EodReconciliationRuns",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentAliases_InstrumentId",
                table: "InstrumentAliases",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentAliases_Source_ExternalInstrumentId",
                table: "InstrumentAliases",
                columns: new[] { "Source", "ExternalInstrumentId" },
                unique: true,
                filter: "[IsEnabled] = 1 AND [ExternalInstrumentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentAliases_Source_ExternalSymbol",
                table: "InstrumentAliases",
                columns: new[] { "Source", "ExternalSymbol" },
                unique: true,
                filter: "[IsEnabled] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxCurrencyWallets_BrokerAccountId",
                table: "LmaxCurrencyWallets",
                column: "BrokerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxCurrencyWallets_ImportRunId",
                table: "LmaxCurrencyWallets",
                column: "ImportRunId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxCurrencyWallets_ReportDate_BrokerAccountId",
                table: "LmaxCurrencyWallets",
                columns: new[] { "ReportDate", "BrokerAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_LmaxCurrencyWallets_ReportDate_VenueId_BrokerAccountId_Currency",
                table: "LmaxCurrencyWallets",
                columns: new[] { "ReportDate", "VenueId", "BrokerAccountId", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LmaxCurrencyWallets_VenueId",
                table: "LmaxCurrencyWallets",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxIndividualTrades_BrokerAccountId",
                table: "LmaxIndividualTrades",
                column: "BrokerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxIndividualTrades_ImportRunId",
                table: "LmaxIndividualTrades",
                column: "ImportRunId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxIndividualTrades_InstructionId",
                table: "LmaxIndividualTrades",
                column: "InstructionId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxIndividualTrades_InstrumentId_ReportDate",
                table: "LmaxIndividualTrades",
                columns: new[] { "InstrumentId", "ReportDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LmaxIndividualTrades_OrderId",
                table: "LmaxIndividualTrades",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxIndividualTrades_VenueId_AccountId_ExecutionId",
                table: "LmaxIndividualTrades",
                columns: new[] { "VenueId", "AccountId", "ExecutionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LmaxIndividualTrades_VenueId_AccountId_TradeUti",
                table: "LmaxIndividualTrades",
                columns: new[] { "VenueId", "AccountId", "TradeUti" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LmaxReportImportRuns_BrokerAccountId",
                table: "LmaxReportImportRuns",
                column: "BrokerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxReportImportRuns_ReportDate_ReportType_VenueId_BrokerAccountId",
                table: "LmaxReportImportRuns",
                columns: new[] { "ReportDate", "ReportType", "VenueId", "BrokerAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_LmaxReportImportRuns_VenueId",
                table: "LmaxReportImportRuns",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxReportValidationIssues_ImportRunId",
                table: "LmaxReportValidationIssues",
                column: "ImportRunId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxTradeSummaries_BrokerAccountId",
                table: "LmaxTradeSummaries",
                column: "BrokerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxTradeSummaries_ImportRunId",
                table: "LmaxTradeSummaries",
                column: "ImportRunId");

            migrationBuilder.CreateIndex(
                name: "IX_LmaxTradeSummaries_InstrumentId_ReportDate",
                table: "LmaxTradeSummaries",
                columns: new[] { "InstrumentId", "ReportDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LmaxTradeSummaries_VenueId",
                table: "LmaxTradeSummaries",
                column: "VenueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EodReconciliationBreaks");

            migrationBuilder.DropTable(
                name: "InstrumentAliases");

            migrationBuilder.DropTable(
                name: "LmaxCurrencyWallets");

            migrationBuilder.DropTable(
                name: "LmaxIndividualTrades");

            migrationBuilder.DropTable(
                name: "LmaxReportValidationIssues");

            migrationBuilder.DropTable(
                name: "LmaxTradeSummaries");

            migrationBuilder.DropTable(
                name: "EodReconciliationRuns");

            migrationBuilder.DropTable(
                name: "LmaxReportImportRuns");

            migrationBuilder.DropColumn(
                name: "ExternalAccountId",
                table: "BrokerAccounts");
        }
    }
}
