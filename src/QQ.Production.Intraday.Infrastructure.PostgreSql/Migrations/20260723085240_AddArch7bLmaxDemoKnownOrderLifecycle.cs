using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddArch7bLmaxDemoKnownOrderLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "arch7b_qualification_runs",
                schema: "pms_shadow",
                columns: table => new
                {
                    qualification_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gate = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    scope = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    account_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    security_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    security_id_source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    opening_side = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    venue_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    quantity_increment = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    price_increment = table.Column<decimal>(type: "numeric(38,28)", precision: 38, scale: 28, nullable: false),
                    opening_client_order_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    flatten_client_order_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    cancel_client_order_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    policy_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    authorization_packet_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    owner_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    lease_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    external_or_manual_order_coverage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    registered_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arch7b_qualification_runs", x => x.qualification_run_id);
                    table.CheckConstraint("ck_arch7b_run_hashes", "policy_sha256 ~ '^[0-9a-f]{64}$' AND authorization_packet_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_arch7b_run_quantity", "venue_quantity > 0 AND quantity_increment > 0 AND price_increment > 0");
                    table.CheckConstraint("ck_arch7b_run_scope", "gate = 'ARCH7B_REQUALIFY_EXISTING_LMAX_DEMO_FIX_ORDER_ENTRY_SINGLE_BOUNDED_KNOWN_ORDER_LIFECYCLE_FLATTEN_AND_RECONCILIATION' AND scope = 'DEMO_EXCLUSIVE_KNOWN_ORDER_QUALIFICATION_WINDOW' AND external_or_manual_order_coverage = 'UNPROVEN'");
                    table.CheckConstraint("ck_arch7b_run_test_demo", "environment = 'TEST' AND account_id = '1754288005' AND account_id <> '921640160'");
                    table.ForeignKey(
                        name: "FK_arch7b_qualification_runs_shadow_child_orders_child_order_id",
                        column: x => x.child_order_id,
                        principalSchema: "pms_shadow",
                        principalTable: "shadow_child_orders",
                        principalColumn: "child_order_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "arch7b_execution_reports",
                schema: "pms_shadow",
                columns: table => new
                {
                    execution_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qualification_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    fix_sequence_number = table.Column<long>(type: "bigint", nullable: false),
                    account_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    client_order_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    original_client_order_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    exec_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    exec_type = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    order_status = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    security_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    side = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    order_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    cumulative_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    leaves_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    last_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    last_price = table.Column<decimal>(type: "numeric(38,28)", precision: 38, scale: 28, nullable: false),
                    average_price = table.Column<decimal>(type: "numeric(38,28)", precision: 38, scale: 28, nullable: false),
                    limit_price = table.Column<decimal>(type: "numeric(38,28)", precision: 38, scale: 28, nullable: true),
                    transact_time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    poss_dup = table.Column<bool>(type: "boolean", nullable: false),
                    raw_message_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arch7b_execution_reports", x => x.execution_report_id);
                    table.CheckConstraint("ck_arch7b_execution_report_demo", "account_id = '1754288005' AND account_id <> '921640160'");
                    table.CheckConstraint("ck_arch7b_execution_report_quantities", "fix_sequence_number > 0 AND order_quantity >= 0 AND cumulative_quantity >= 0 AND leaves_quantity >= 0 AND last_quantity >= 0 AND last_price >= 0");
                    table.CheckConstraint("ck_arch7b_execution_report_sha256", "raw_message_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_arch7b_execution_reports_arch7b_qualification_runs_qualific~",
                        column: x => x.qualification_run_id,
                        principalSchema: "pms_shadow",
                        principalTable: "arch7b_qualification_runs",
                        principalColumn: "qualification_run_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "arch7b_final_reconciliations",
                schema: "pms_shadow",
                columns: table => new
                {
                    reconciliation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qualification_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    broker_evidence_authority = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    opening_cumulative_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    opening_fill_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    flatten_cumulative_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    flatten_fill_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    known_working_leaves = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    internal_ledger_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    broker_residual_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    residual_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    critical_break_count = table.Column<int>(type: "integer", nullable: false),
                    breaks_json = table.Column<string>(type: "jsonb", nullable: false),
                    realized_pnl_before_fees = table.Column<decimal>(type: "numeric(38,28)", precision: 38, scale: 28, nullable: true),
                    fee_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    evidence_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arch7b_final_reconciliations", x => x.reconciliation_id);
                    table.CheckConstraint("ck_arch7b_reconciliation_broker_authority", "broker_evidence_authority <> 'INTERNAL_LEDGER_ONLY'");
                    table.CheckConstraint("ck_arch7b_reconciliation_flat", "status = 'FLAT_RECONCILED' AND known_working_leaves = 0 AND internal_ledger_quantity = 0 AND broker_residual_quantity = 0 AND residual_quantity = 0 AND critical_break_count = 0");
                    table.CheckConstraint("ck_arch7b_reconciliation_sha256", "evidence_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_arch7b_final_reconciliations_arch7b_qualification_runs_qual~",
                        column: x => x.qualification_run_id,
                        principalSchema: "pms_shadow",
                        principalTable: "arch7b_qualification_runs",
                        principalColumn: "qualification_run_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "arch7b_fix_session_events",
                schema: "pms_shadow",
                columns: table => new
                {
                    session_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qualification_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fix_sequence_number = table.Column<long>(type: "bigint", nullable: true),
                    event_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arch7b_fix_session_events", x => x.session_event_id);
                    table.CheckConstraint("ck_arch7b_fix_session_event_sha256", "event_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_arch7b_fix_session_events_arch7b_qualification_runs_qualifi~",
                        column: x => x.qualification_run_id,
                        principalSchema: "pms_shadow",
                        principalTable: "arch7b_qualification_runs",
                        principalColumn: "qualification_run_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "arch7b_order_send_ledger",
                schema: "pms_shadow",
                columns: table => new
                {
                    send_ledger_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qualification_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lifecycle_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    message_type = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    client_order_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    original_client_order_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    security_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    side = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    limit_price = table.Column<decimal>(type: "numeric(38,28)", precision: 38, scale: 28, nullable: true),
                    bbo_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    intent_recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arch7b_order_send_ledger", x => x.send_ledger_id);
                    table.CheckConstraint("ck_arch7b_send_hashes", "bbo_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND payload_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_arch7b_send_message_type", "message_type IN ('D', 'F', 'H')");
                    table.CheckConstraint("ck_arch7b_send_quantity", "quantity >= 0");
                    table.ForeignKey(
                        name: "FK_arch7b_order_send_ledger_arch7b_qualification_runs_qualific~",
                        column: x => x.qualification_run_id,
                        principalSchema: "pms_shadow",
                        principalTable: "arch7b_qualification_runs",
                        principalColumn: "qualification_run_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "arch7b_fills",
                schema: "pms_shadow",
                columns: table => new
                {
                    fill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qualification_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    execution_report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exec_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    order_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    client_order_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    security_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    side = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    price = table.Column<decimal>(type: "numeric(38,28)", precision: 38, scale: 28, nullable: false),
                    transact_time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    raw_message_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fee_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    fee_amount = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: true),
                    fee_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arch7b_fills", x => x.fill_id);
                    table.CheckConstraint("ck_arch7b_fill_fees", "(fee_status = 'BROKER_FEES_UNAVAILABLE_NOT_ASSUMED_ZERO' AND fee_amount IS NULL) OR (fee_status = 'BROKER_FEES_REPORTED' AND fee_amount IS NOT NULL)");
                    table.CheckConstraint("ck_arch7b_fill_sha256", "raw_message_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_arch7b_fill_values", "quantity > 0 AND price > 0");
                    table.ForeignKey(
                        name: "FK_arch7b_fills_arch7b_execution_reports_execution_report_id",
                        column: x => x.execution_report_id,
                        principalSchema: "pms_shadow",
                        principalTable: "arch7b_execution_reports",
                        principalColumn: "execution_report_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_arch7b_fills_arch7b_qualification_runs_qualification_run_id",
                        column: x => x.qualification_run_id,
                        principalSchema: "pms_shadow",
                        principalTable: "arch7b_qualification_runs",
                        principalColumn: "qualification_run_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "arch7b_position_ledger_events",
                schema: "pms_shadow",
                columns: table => new
                {
                    position_ledger_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qualification_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exec_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    symbol = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    security_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    instrument_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    settlement_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    signed_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    price = table.Column<decimal>(type: "numeric(38,28)", precision: 38, scale: 28, nullable: false),
                    event_time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source_message_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    event_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arch7b_position_ledger_events", x => x.position_ledger_event_id);
                    table.CheckConstraint("ck_arch7b_position_ledger_hashes", "source_message_sha256 ~ '^[0-9a-f]{64}$' AND event_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_arch7b_position_ledger_events_arch7b_fills_fill_id",
                        column: x => x.fill_id,
                        principalSchema: "pms_shadow",
                        principalTable: "arch7b_fills",
                        principalColumn: "fill_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_arch7b_position_ledger_events_arch7b_qualification_runs_qua~",
                        column: x => x.qualification_run_id,
                        principalSchema: "pms_shadow",
                        principalTable: "arch7b_qualification_runs",
                        principalColumn: "qualification_run_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_execution_reports_account_id_exec_id",
                schema: "pms_shadow",
                table: "arch7b_execution_reports",
                columns: new[] { "account_id", "exec_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_execution_reports_qualification_run_id",
                schema: "pms_shadow",
                table: "arch7b_execution_reports",
                column: "qualification_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_execution_reports_raw_message_sha256",
                schema: "pms_shadow",
                table: "arch7b_execution_reports",
                column: "raw_message_sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_execution_reports_session_id_fix_sequence_number",
                schema: "pms_shadow",
                table: "arch7b_execution_reports",
                columns: new[] { "session_id", "fix_sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_fills_exec_id",
                schema: "pms_shadow",
                table: "arch7b_fills",
                column: "exec_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_fills_execution_report_id",
                schema: "pms_shadow",
                table: "arch7b_fills",
                column: "execution_report_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_fills_qualification_run_id",
                schema: "pms_shadow",
                table: "arch7b_fills",
                column: "qualification_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_fills_raw_message_sha256",
                schema: "pms_shadow",
                table: "arch7b_fills",
                column: "raw_message_sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_final_reconciliations_qualification_run_id",
                schema: "pms_shadow",
                table: "arch7b_final_reconciliations",
                column: "qualification_run_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_fix_session_events_qualification_run_id_session_id_e~",
                schema: "pms_shadow",
                table: "arch7b_fix_session_events",
                columns: new[] { "qualification_run_id", "session_id", "event_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_order_send_ledger_qualification_run_id_payload_sha256",
                schema: "pms_shadow",
                table: "arch7b_order_send_ledger",
                columns: new[] { "qualification_run_id", "payload_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_position_ledger_events_event_sha256",
                schema: "pms_shadow",
                table: "arch7b_position_ledger_events",
                column: "event_sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_position_ledger_events_fill_id",
                schema: "pms_shadow",
                table: "arch7b_position_ledger_events",
                column: "fill_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_position_ledger_events_qualification_run_id",
                schema: "pms_shadow",
                table: "arch7b_position_ledger_events",
                column: "qualification_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_qualification_runs_cancel_client_order_id",
                schema: "pms_shadow",
                table: "arch7b_qualification_runs",
                column: "cancel_client_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_qualification_runs_child_order_id",
                schema: "pms_shadow",
                table: "arch7b_qualification_runs",
                column: "child_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_qualification_runs_flatten_client_order_id",
                schema: "pms_shadow",
                table: "arch7b_qualification_runs",
                column: "flatten_client_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_arch7b_qualification_runs_opening_client_order_id",
                schema: "pms_shadow",
                table: "arch7b_qualification_runs",
                column: "opening_client_order_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "arch7b_final_reconciliations",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "arch7b_fix_session_events",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "arch7b_order_send_ledger",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "arch7b_position_ledger_events",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "arch7b_fills",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "arch7b_execution_reports",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "arch7b_qualification_runs",
                schema: "pms_shadow");
        }
    }
}
