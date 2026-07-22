using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddArch7aShadowExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shadow_trade_intents",
                schema: "pms_shadow",
                columns: table => new
                {
                    trade_intent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingestion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_session_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slot_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    operational_date = table.Column<DateOnly>(type: "date", nullable: false),
                    target_close_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_from_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deadline_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    model_run_ids_json = table.Column<string>(type: "jsonb", nullable: false),
                    target_position_ids_json = table.Column<string>(type: "jsonb", nullable: false),
                    drift_ids_json = table.Column<string>(type: "jsonb", nullable: false),
                    security_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    security_id_source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    normalized_portfolio_symbol = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    execution_tradable_symbol = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    requires_inversion = table.Column<bool>(type: "boolean", nullable: false),
                    side = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    signed_desired_delta = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    target_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    current_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    account_scope = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    environment = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    classification = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    actionable = table.Column<bool>(type: "boolean", nullable: false),
                    execution_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    broker_route_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    blocking_reason = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    lineage_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    plan_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shadow_trade_intents", x => x.trade_intent_id);
                    table.CheckConstraint("ck_shadow_trade_intent_no_route", "NOT actionable AND NOT execution_allowed AND NOT broker_route_allowed");
                    table.CheckConstraint("ck_shadow_trade_intent_sha256", "idempotency_key ~ '^[0-9a-f]{64}$' AND lineage_sha256 ~ '^[0-9a-f]{64}$' AND plan_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_shadow_trade_intent_test_only", "environment = 'TEST' AND classification = 'SHADOW_ONLY'");
                    table.ForeignKey(
                        name: "FK_shadow_trade_intents_ingestions_ingestion_id",
                        column: x => x.ingestion_id,
                        principalSchema: "pms_shadow",
                        principalTable: "ingestions",
                        principalColumn: "ingestion_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shadow_risk_decisions",
                schema: "pms_shadow",
                columns: table => new
                {
                    risk_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trade_intent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason_codes_json = table.Column<string>(type: "jsonb", nullable: false),
                    blocking_breaks_json = table.Column<string>(type: "jsonb", nullable: false),
                    source_complete = table.Column<bool>(type: "boolean", nullable: false),
                    position_authority = table.Column<bool>(type: "boolean", nullable: false),
                    working_order_authority = table.Column<bool>(type: "boolean", nullable: false),
                    freshness = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    limits_evaluated_json = table.Column<string>(type: "jsonb", nullable: false),
                    no_order_invariant = table.Column<bool>(type: "boolean", nullable: false),
                    broker_send_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    plan_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shadow_risk_decisions", x => x.risk_decision_id);
                    table.CheckConstraint("ck_shadow_risk_decision_no_send", "no_order_invariant AND NOT broker_send_allowed");
                    table.CheckConstraint("ck_shadow_risk_decision_plan_sha256", "plan_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_shadow_risk_decisions_shadow_trade_intents_trade_intent_id",
                        column: x => x.trade_intent_id,
                        principalSchema: "pms_shadow",
                        principalTable: "shadow_trade_intents",
                        principalColumn: "trade_intent_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shadow_parent_orders",
                schema: "pms_shadow",
                columns: table => new
                {
                    parent_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trade_intent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    risk_decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    symbol = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    side = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    total_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    target_close_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    execution_algo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    route_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    deterministic_identity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    plan_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shadow_parent_orders", x => x.parent_order_id);
                    table.CheckConstraint("ck_shadow_parent_no_route", "NOT route_allowed");
                    table.CheckConstraint("ck_shadow_parent_plan_sha256", "plan_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_shadow_parent_orders_shadow_risk_decisions_risk_decision_id",
                        column: x => x.risk_decision_id,
                        principalSchema: "pms_shadow",
                        principalTable: "shadow_risk_decisions",
                        principalColumn: "risk_decision_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shadow_parent_orders_shadow_trade_intents_trade_intent_id",
                        column: x => x.trade_intent_id,
                        principalSchema: "pms_shadow",
                        principalTable: "shadow_trade_intents",
                        principalColumn: "trade_intent_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shadow_child_orders",
                schema: "pms_shadow",
                columns: table => new
                {
                    child_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    venue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tranche = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    side = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    simulated_limit_price = table.Column<decimal>(type: "numeric(38,28)", precision: 38, scale: 28, nullable: true),
                    effective_time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deadline_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    algo_phase = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    broker_send_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    deterministic_identity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    plan_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shadow_child_orders", x => x.child_order_id);
                    table.CheckConstraint("ck_shadow_child_no_send", "NOT broker_send_allowed");
                    table.CheckConstraint("ck_shadow_child_plan_sha256", "plan_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_shadow_child_orders_shadow_parent_orders_parent_order_id",
                        column: x => x.parent_order_id,
                        principalSchema: "pms_shadow",
                        principalTable: "shadow_parent_orders",
                        principalColumn: "parent_order_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shadow_child_orders_client_order_id",
                schema: "pms_shadow",
                table: "shadow_child_orders",
                column: "client_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shadow_child_orders_parent_order_id",
                schema: "pms_shadow",
                table: "shadow_child_orders",
                column: "parent_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shadow_parent_orders_client_order_id",
                schema: "pms_shadow",
                table: "shadow_parent_orders",
                column: "client_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shadow_parent_orders_risk_decision_id",
                schema: "pms_shadow",
                table: "shadow_parent_orders",
                column: "risk_decision_id");

            migrationBuilder.CreateIndex(
                name: "IX_shadow_parent_orders_trade_intent_id",
                schema: "pms_shadow",
                table: "shadow_parent_orders",
                column: "trade_intent_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shadow_risk_decisions_trade_intent_id",
                schema: "pms_shadow",
                table: "shadow_risk_decisions",
                column: "trade_intent_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shadow_trade_intents_ingestion_id",
                schema: "pms_shadow",
                table: "shadow_trade_intents",
                column: "ingestion_id");

            migrationBuilder.CreateIndex(
                name: "IX_shadow_trade_intents_plan_sha256",
                schema: "pms_shadow",
                table: "shadow_trade_intents",
                column: "plan_sha256");

            migrationBuilder.CreateIndex(
                name: "IX_shadow_trade_intents_source_session_id_slot_id_execution_tr~",
                schema: "pms_shadow",
                table: "shadow_trade_intents",
                columns: new[] { "source_session_id", "slot_id", "execution_tradable_symbol" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shadow_child_orders",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "shadow_parent_orders",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "shadow_risk_decisions",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "shadow_trade_intents",
                schema: "pms_shadow");
        }
    }
}
