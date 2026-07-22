using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class CorrectArch7aSerializationReplayAndChildPriceScale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "simulated_limit_price",
                schema: "pms_shadow",
                table: "shadow_child_orders",
                type: "numeric(28,12)",
                precision: 28,
                scale: 12,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(38,28)",
                oldPrecision: 38,
                oldScale: 28,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "simulated_limit_price",
                schema: "pms_shadow",
                table: "shadow_child_orders",
                type: "numeric(38,28)",
                precision: 38,
                scale: 28,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(28,12)",
                oldPrecision: 28,
                oldScale: 12,
                oldNullable: true);
        }
    }
}
