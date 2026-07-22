using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.PostgreSql.Migrations;

[DbContext(typeof(PmsShadowDbContext))]
[Migration("20260722234500_CorrectIntradayMarketPriceScaleInvariant")]
public sealed class CorrectIntradayMarketPriceScaleInvariant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE pms_shadow.intraday_market_data_observations
                DROP CONSTRAINT ck_intraday_market_prices;
            ALTER TABLE pms_shadow.intraday_market_data_observations
                ADD CONSTRAINT ck_intraday_market_prices CHECK (
                    bid > 0 AND ask > 0 AND ask >= bid AND
                    decision_price = round((bid + ask) / 2, 12));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE pms_shadow.intraday_market_data_observations
                DROP CONSTRAINT ck_intraday_market_prices;
            ALTER TABLE pms_shadow.intraday_market_data_observations
                ADD CONSTRAINT ck_intraday_market_prices CHECK (
                    bid > 0 AND ask > 0 AND ask >= bid AND
                    decision_price = (bid + ask) / 2);
            """);
    }
}
