using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace QQ.Production.Intraday.Infrastructure.SqlServer.Migrations;
[DbContext(typeof(IntradayDbContext))]
[Migration("20260918073000_AllowMissingLmaxTradeUti")]
public sealed class AllowMissingLmaxTradeUti : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_LmaxIndividualTrades_VenueId_AccountId_TradeUti", "LmaxIndividualTrades");
        migrationBuilder.CreateIndex("IX_LmaxIndividualTrades_VenueId_AccountId_TradeUti", "LmaxIndividualTrades",
            new[] { "VenueId", "AccountId", "TradeUti" }, unique: true, filter: "[TradeUti] <> N''");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // SQL Server will reject rollback if multiple absent UTIs now exist. Never delete report evidence.
        migrationBuilder.DropIndex("IX_LmaxIndividualTrades_VenueId_AccountId_TradeUti", "LmaxIndividualTrades");
        migrationBuilder.CreateIndex("IX_LmaxIndividualTrades_VenueId_AccountId_TradeUti", "LmaxIndividualTrades",
            new[] { "VenueId", "AccountId", "TradeUti" }, unique: true);
    }
}
