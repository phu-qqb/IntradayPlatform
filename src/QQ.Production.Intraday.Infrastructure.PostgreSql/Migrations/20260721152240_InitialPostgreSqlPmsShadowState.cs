using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSqlPmsShadowState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pms_shadow");

            migrationBuilder.CreateTable(
                name: "ingestions",
                schema: "pms_shadow",
                columns: table => new
                {
                    ingestion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_gate = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    source_session_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_evidence_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    contract_version = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    environment = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    classification = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    rowset_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestions", x => x.ingestion_id);
                    table.CheckConstraint("ck_ingestions_classification", "classification = 'EVIDENCE_ONLY_NONACCOUNTING'");
                    table.CheckConstraint("ck_ingestions_environment", "environment = 'LMAX_TEST_EOD_ONLY'");
                    table.CheckConstraint("ck_ingestions_rowset_sha256", "rowset_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_ingestions_source_evidence_sha256", "source_evidence_sha256 ~ '^[0-9a-f]{64}$'");
                });

            migrationBuilder.CreateTable(
                name: "account_snapshots",
                schema: "pms_shadow",
                columns: table => new
                {
                    account_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingestion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    scope = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    base_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    nav_or_equity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    report_date = table.Column<DateOnly>(type: "date", nullable: false),
                    as_of_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    authority = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    source_artifact_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    classification = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_snapshots", x => x.account_snapshot_id);
                    table.CheckConstraint("ck_account_not_real", "account_id <> '921640160'");
                    table.CheckConstraint("ck_account_snapshot_sha256", "snapshot_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_account_source_sha256", "source_artifact_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_account_snapshots_ingestions_ingestion_id",
                        column: x => x.ingestion_id,
                        principalSchema: "pms_shadow",
                        principalTable: "ingestions",
                        principalColumn: "ingestion_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "market_data_snapshots",
                schema: "pms_shadow",
                columns: table => new
                {
                    market_data_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingestion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    as_of_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    observation_count = table.Column<int>(type: "integer", nullable: false),
                    classification = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_data_snapshots", x => x.market_data_snapshot_id);
                    table.CheckConstraint("ck_market_observation_count", "observation_count > 0");
                    table.CheckConstraint("ck_market_snapshot_sha256", "snapshot_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_market_data_snapshots_ingestions_ingestion_id",
                        column: x => x.ingestion_id,
                        principalSchema: "pms_shadow",
                        principalTable: "ingestions",
                        principalColumn: "ingestion_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "security_mappings",
                schema: "pms_shadow",
                columns: table => new
                {
                    ingestion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    venue_instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    security_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    symbol = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    lmax_instrument_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    quantity_multiplier = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    quantity_increment = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    price_increment = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    mapping_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_mappings", x => new { x.ingestion_id, x.instrument_id });
                    table.CheckConstraint("ck_security_mapping_positive", "quantity_multiplier > 0 AND quantity_increment > 0 AND price_increment > 0");
                    table.CheckConstraint("ck_security_mapping_sha256", "mapping_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_security_mappings_ingestions_ingestion_id",
                        column: x => x.ingestion_id,
                        principalSchema: "pms_shadow",
                        principalTable: "ingestions",
                        principalColumn: "ingestion_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "source_artifacts",
                schema: "pms_shadow",
                columns: table => new
                {
                    artifact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingestion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    artifact_type = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    logical_uri = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    contract_version = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    produced_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source_system = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    classification = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_artifacts", x => x.artifact_id);
                    table.CheckConstraint("ck_source_artifacts_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_source_artifacts_ingestions_ingestion_id",
                        column: x => x.ingestion_id,
                        principalSchema: "pms_shadow",
                        principalTable: "ingestions",
                        principalColumn: "ingestion_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "working_leaves_observations",
                schema: "pms_shadow",
                columns: table => new
                {
                    working_leaves_observation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingestion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    source_system = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    observation_attempted = table.Column<bool>(type: "boolean", nullable: false),
                    empty_state_observed = table.Column<bool>(type: "boolean", nullable: false),
                    empty_state_inferred = table.Column<bool>(type: "boolean", nullable: false),
                    broker_authority = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    impact = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    as_of_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    classification = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_working_leaves_observations", x => x.working_leaves_observation_id);
                    table.CheckConstraint("ck_working_leaves_not_empty_not_inferred", "NOT empty_state_observed AND NOT empty_state_inferred AND NOT broker_authority");
                    table.CheckConstraint("ck_working_leaves_unavailable", "status = 'UNAVAILABLE_WITH_CURRENT_LMAX_INTERFACES'");
                    table.ForeignKey(
                        name: "FK_working_leaves_observations_ingestions_ingestion_id",
                        column: x => x.ingestion_id,
                        principalSchema: "pms_shadow",
                        principalTable: "ingestions",
                        principalColumn: "ingestion_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "position_snapshots",
                schema: "pms_shadow",
                columns: table => new
                {
                    position_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingestion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_date = table.Column<DateOnly>(type: "date", nullable: false),
                    as_of_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    empty_state_was_explicitly_observed = table.Column<bool>(type: "boolean", nullable: false),
                    empty_state_was_inferred = table.Column<bool>(type: "boolean", nullable: false),
                    broker_authority = table.Column<bool>(type: "boolean", nullable: false),
                    classification = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_position_snapshots", x => x.position_snapshot_id);
                    table.CheckConstraint("ck_position_empty_not_inferred", "NOT empty_state_was_inferred");
                    table.CheckConstraint("ck_position_snapshot_sha256", "snapshot_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_position_snapshots_account_snapshots_account_snapshot_id",
                        column: x => x.account_snapshot_id,
                        principalSchema: "pms_shadow",
                        principalTable: "account_snapshots",
                        principalColumn: "account_snapshot_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_position_snapshots_ingestions_ingestion_id",
                        column: x => x.ingestion_id,
                        principalSchema: "pms_shadow",
                        principalTable: "ingestions",
                        principalColumn: "ingestion_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "market_data_observations",
                schema: "pms_shadow",
                columns: table => new
                {
                    market_data_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    security_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    lmax_instrument_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    symbol = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    bid = table.Column<decimal>(type: "numeric(38,28)", precision: 38, scale: 28, nullable: false),
                    ask = table.Column<decimal>(type: "numeric(38,28)", precision: 38, scale: 28, nullable: false),
                    event_time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    staleness_milliseconds = table.Column<long>(type: "bigint", nullable: false),
                    source_capture_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    source_file_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    projection_method = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    projection_leg_security_ids_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_data_observations", x => new { x.market_data_snapshot_id, x.instrument_id });
                    table.CheckConstraint("ck_market_bid_ask", "bid > 0 AND ask >= bid");
                    table.CheckConstraint("ck_market_source_sha256", "source_file_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_market_staleness", "staleness_milliseconds >= 0");
                    table.ForeignKey(
                        name: "FK_market_data_observations_market_data_snapshots_market_data_~",
                        column: x => x.market_data_snapshot_id,
                        principalSchema: "pms_shadow",
                        principalTable: "market_data_snapshots",
                        principalColumn: "market_data_snapshot_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "qubes_input_snapshots",
                schema: "pms_shadow",
                columns: table => new
                {
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingestion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    input_artifact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_snapshot_artifact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    overlay_artifact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    source_snapshot_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    overlay_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    gap_ledger_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    mapping_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    input_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_close_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source_instrument_count = table.Column<int>(type: "integer", nullable: false),
                    gap_count = table.Column<int>(type: "integer", nullable: false),
                    provenance = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    classification = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qubes_input_snapshots", x => x.snapshot_id);
                    table.CheckConstraint("ck_qubes_counts", "source_instrument_count > 0 AND gap_count >= 0");
                    table.CheckConstraint("ck_qubes_input_sha256", "input_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_qubes_overlay_sha256", "overlay_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_qubes_source_sha256", "source_snapshot_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_qubes_input_snapshots_ingestions_ingestion_id",
                        column: x => x.ingestion_id,
                        principalSchema: "pms_shadow",
                        principalTable: "ingestions",
                        principalColumn: "ingestion_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_qubes_input_snapshots_source_artifacts_input_artifact_id",
                        column: x => x.input_artifact_id,
                        principalSchema: "pms_shadow",
                        principalTable: "source_artifacts",
                        principalColumn: "artifact_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_qubes_input_snapshots_source_artifacts_overlay_artifact_id",
                        column: x => x.overlay_artifact_id,
                        principalSchema: "pms_shadow",
                        principalTable: "source_artifacts",
                        principalColumn: "artifact_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_qubes_input_snapshots_source_artifacts_source_snapshot_arti~",
                        column: x => x.source_snapshot_artifact_id,
                        principalSchema: "pms_shadow",
                        principalTable: "source_artifacts",
                        principalColumn: "artifact_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "position_snapshot_lines",
                schema: "pms_shadow",
                columns: table => new
                {
                    position_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    security_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    symbol = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    current_base_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_position_snapshot_lines", x => new { x.position_snapshot_id, x.instrument_id });
                    table.ForeignKey(
                        name: "FK_position_snapshot_lines_position_snapshots_position_snapsho~",
                        column: x => x.position_snapshot_id,
                        principalSchema: "pms_shadow",
                        principalTable: "position_snapshots",
                        principalColumn: "position_snapshot_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "model_runs",
                schema: "pms_shadow",
                columns: table => new
                {
                    model_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingestion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qubes_input_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    output_artifact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_model_run_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_domain_model = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    strategy_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    benchmark_parameter = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    target_close_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    as_of_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    core_master_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    package_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    engine_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    wrapper_exit_code = table.Column<int>(type: "integer", nullable: false),
                    native_exit_code = table.Column<int>(type: "integer", nullable: false),
                    semantic_status = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    r083_status = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    output_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    contract_version = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    classification = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    accounting_eligible = table.Column<bool>(type: "boolean", nullable: false),
                    execution_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    not_an_order = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_runs", x => x.model_run_id);
                    table.CheckConstraint("ck_model_run_exit_codes", "wrapper_exit_code = 0 AND native_exit_code = 0");
                    table.CheckConstraint("ck_model_run_hashes", "core_master_sha256 ~ '^[0-9a-f]{64}$' AND package_sha256 ~ '^[0-9a-f]{64}$' AND engine_sha256 ~ '^[0-9a-f]{64}$' AND output_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_model_run_no_order", "NOT accounting_eligible AND NOT execution_allowed AND not_an_order");
                    table.ForeignKey(
                        name: "FK_model_runs_ingestions_ingestion_id",
                        column: x => x.ingestion_id,
                        principalSchema: "pms_shadow",
                        principalTable: "ingestions",
                        principalColumn: "ingestion_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_model_runs_qubes_input_snapshots_qubes_input_snapshot_id",
                        column: x => x.qubes_input_snapshot_id,
                        principalSchema: "pms_shadow",
                        principalTable: "qubes_input_snapshots",
                        principalColumn: "snapshot_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_model_runs_source_artifacts_output_artifact_id",
                        column: x => x.output_artifact_id,
                        principalSchema: "pms_shadow",
                        principalTable: "source_artifacts",
                        principalColumn: "artifact_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "broker_adjusted_drift_stages",
                schema: "pms_shadow",
                columns: table => new
                {
                    stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    working_leaves_observation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    calculated = table.Column<bool>(type: "boolean", nullable: false),
                    blocker = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    empty_state_inferred = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    classification = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_broker_adjusted_drift_stages", x => x.stage_id);
                    table.CheckConstraint("ck_broker_adjusted_blocker", "blocker = 'BROKER_WORKING_LEAVES_UNOBSERVABLE'");
                    table.CheckConstraint("ck_broker_adjusted_not_calculated", "NOT calculated AND NOT empty_state_inferred");
                    table.ForeignKey(
                        name: "FK_broker_adjusted_drift_stages_model_runs_model_run_id",
                        column: x => x.model_run_id,
                        principalSchema: "pms_shadow",
                        principalTable: "model_runs",
                        principalColumn: "model_run_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_broker_adjusted_drift_stages_working_leaves_observations_wo~",
                        column: x => x.working_leaves_observation_id,
                        principalSchema: "pms_shadow",
                        principalTable: "working_leaves_observations",
                        principalColumn: "working_leaves_observation_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cycle_results",
                schema: "pms_shadow",
                columns: table => new
                {
                    result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingestion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    manual_paper_cycle_status = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    r009_status = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    execution_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    not_an_order = table.Column<bool>(type: "boolean", nullable: false),
                    no_broker_route = table.Column<bool>(type: "boolean", nullable: false),
                    no_fix_message = table.Column<bool>(type: "boolean", nullable: false),
                    order_entry_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    broker_send_status = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    trade_intent_count = table.Column<int>(type: "integer", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    classification = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cycle_results", x => x.result_id);
                    table.CheckConstraint("ck_cycle_result_broker_send", "broker_send_status = 'DISABLED_NO_ORDER_ENTRY'");
                    table.CheckConstraint("ck_cycle_result_no_order", "NOT execution_allowed AND not_an_order AND no_broker_route AND no_fix_message AND NOT order_entry_enabled AND trade_intent_count = 0");
                    table.ForeignKey(
                        name: "FK_cycle_results_ingestions_ingestion_id",
                        column: x => x.ingestion_id,
                        principalSchema: "pms_shadow",
                        principalTable: "ingestions",
                        principalColumn: "ingestion_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_cycle_results_model_runs_model_run_id",
                        column: x => x.model_run_id,
                        principalSchema: "pms_shadow",
                        principalTable: "model_runs",
                        principalColumn: "model_run_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "position_only_drift_stages",
                schema: "pms_shadow",
                columns: table => new
                {
                    stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    as_of_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    classification = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_position_only_drift_stages", x => x.stage_id);
                    table.ForeignKey(
                        name: "FK_position_only_drift_stages_model_runs_model_run_id",
                        column: x => x.model_run_id,
                        principalSchema: "pms_shadow",
                        principalTable: "model_runs",
                        principalColumn: "model_run_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_position_only_drift_stages_position_snapshots_position_snap~",
                        column: x => x.position_snapshot_id,
                        principalSchema: "pms_shadow",
                        principalTable: "position_snapshots",
                        principalColumn: "position_snapshot_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "target_position_stages",
                schema: "pms_shadow",
                columns: table => new
                {
                    stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    market_data_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    classification = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    accounting_eligible = table.Column<bool>(type: "boolean", nullable: false),
                    execution_allowed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_target_position_stages", x => x.stage_id);
                    table.CheckConstraint("ck_target_position_stage_no_order", "NOT accounting_eligible AND NOT execution_allowed");
                    table.ForeignKey(
                        name: "FK_target_position_stages_account_snapshots_account_snapshot_id",
                        column: x => x.account_snapshot_id,
                        principalSchema: "pms_shadow",
                        principalTable: "account_snapshots",
                        principalColumn: "account_snapshot_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_target_position_stages_market_data_snapshots_market_data_sn~",
                        column: x => x.market_data_snapshot_id,
                        principalSchema: "pms_shadow",
                        principalTable: "market_data_snapshots",
                        principalColumn: "market_data_snapshot_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_target_position_stages_model_runs_model_run_id",
                        column: x => x.model_run_id,
                        principalSchema: "pms_shadow",
                        principalTable: "model_runs",
                        principalColumn: "model_run_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "target_weights",
                schema: "pms_shadow",
                columns: table => new
                {
                    model_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    security_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    weight = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    target_close_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source_row_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    source_order = table.Column<int>(type: "integer", nullable: false),
                    output_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    lineage_version = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_target_weights", x => new { x.model_run_id, x.instrument_id });
                    table.CheckConstraint("ck_target_weight_output_sha256", "output_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_target_weights_model_runs_model_run_id",
                        column: x => x.model_run_id,
                        principalSchema: "pms_shadow",
                        principalTable: "model_runs",
                        principalColumn: "model_run_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "position_only_drifts",
                schema: "pms_shadow",
                columns: table => new
                {
                    stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    security_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    current_base_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    target_base_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    position_only_delta_base_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    as_of_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_position_only_drifts", x => new { x.stage_id, x.instrument_id });
                    table.ForeignKey(
                        name: "FK_position_only_drifts_position_only_drift_stages_stage_id",
                        column: x => x.stage_id,
                        principalSchema: "pms_shadow",
                        principalTable: "position_only_drift_stages",
                        principalColumn: "stage_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_position_only_drifts_target_weights_model_run_id_instrument~",
                        columns: x => new { x.model_run_id, x.instrument_id },
                        principalSchema: "pms_shadow",
                        principalTable: "target_weights",
                        principalColumns: new[] { "model_run_id", "instrument_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "target_positions",
                schema: "pms_shadow",
                columns: table => new
                {
                    stage_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instrument_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    security_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    target_notional_usd = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    target_base_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    target_venue_quantity = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    sizing_policy = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    rounding_policy = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    status = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    classification = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_target_positions", x => new { x.stage_id, x.instrument_id });
                    table.ForeignKey(
                        name: "FK_target_positions_target_position_stages_stage_id",
                        column: x => x.stage_id,
                        principalSchema: "pms_shadow",
                        principalTable: "target_position_stages",
                        principalColumn: "stage_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_target_positions_target_weights_model_run_id_instrument_id",
                        columns: x => new { x.model_run_id, x.instrument_id },
                        principalSchema: "pms_shadow",
                        principalTable: "target_weights",
                        principalColumns: new[] { "model_run_id", "instrument_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_snapshots_ingestion_id_snapshot_sha256",
                schema: "pms_shadow",
                table: "account_snapshots",
                columns: new[] { "ingestion_id", "snapshot_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_snapshots_report_date_as_of_utc",
                schema: "pms_shadow",
                table: "account_snapshots",
                columns: new[] { "report_date", "as_of_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_broker_adjusted_drift_stages_model_run_id",
                schema: "pms_shadow",
                table: "broker_adjusted_drift_stages",
                column: "model_run_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_broker_adjusted_drift_stages_working_leaves_observation_id",
                schema: "pms_shadow",
                table: "broker_adjusted_drift_stages",
                column: "working_leaves_observation_id");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_results_completed_at_utc",
                schema: "pms_shadow",
                table: "cycle_results",
                column: "completed_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_cycle_results_ingestion_id_model_run_id",
                schema: "pms_shadow",
                table: "cycle_results",
                columns: new[] { "ingestion_id", "model_run_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cycle_results_model_run_id",
                schema: "pms_shadow",
                table: "cycle_results",
                column: "model_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_ingestions_completed_at_utc",
                schema: "pms_shadow",
                table: "ingestions",
                column: "completed_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_ingestions_source_session_id",
                schema: "pms_shadow",
                table: "ingestions",
                column: "source_session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ingestions_source_session_id_source_evidence_sha256_rowset_~",
                schema: "pms_shadow",
                table: "ingestions",
                columns: new[] { "source_session_id", "source_evidence_sha256", "rowset_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_market_data_snapshots_as_of_utc",
                schema: "pms_shadow",
                table: "market_data_snapshots",
                column: "as_of_utc");

            migrationBuilder.CreateIndex(
                name: "IX_market_data_snapshots_ingestion_id_snapshot_sha256",
                schema: "pms_shadow",
                table: "market_data_snapshots",
                columns: new[] { "ingestion_id", "snapshot_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_model_runs_external_model_run_id_output_sha256",
                schema: "pms_shadow",
                table: "model_runs",
                columns: new[] { "external_model_run_id", "output_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_model_runs_ingestion_id_strategy_id",
                schema: "pms_shadow",
                table: "model_runs",
                columns: new[] { "ingestion_id", "strategy_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_model_runs_output_artifact_id",
                schema: "pms_shadow",
                table: "model_runs",
                column: "output_artifact_id");

            migrationBuilder.CreateIndex(
                name: "IX_model_runs_qubes_input_snapshot_id",
                schema: "pms_shadow",
                table: "model_runs",
                column: "qubes_input_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_model_runs_target_close_utc",
                schema: "pms_shadow",
                table: "model_runs",
                column: "target_close_utc");

            migrationBuilder.CreateIndex(
                name: "IX_position_only_drift_stages_as_of_utc",
                schema: "pms_shadow",
                table: "position_only_drift_stages",
                column: "as_of_utc");

            migrationBuilder.CreateIndex(
                name: "IX_position_only_drift_stages_model_run_id",
                schema: "pms_shadow",
                table: "position_only_drift_stages",
                column: "model_run_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_position_only_drift_stages_position_snapshot_id",
                schema: "pms_shadow",
                table: "position_only_drift_stages",
                column: "position_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_position_only_drifts_model_run_id_instrument_id",
                schema: "pms_shadow",
                table: "position_only_drifts",
                columns: new[] { "model_run_id", "instrument_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_position_snapshots_account_snapshot_id",
                schema: "pms_shadow",
                table: "position_snapshots",
                column: "account_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_position_snapshots_ingestion_id_snapshot_sha256",
                schema: "pms_shadow",
                table: "position_snapshots",
                columns: new[] { "ingestion_id", "snapshot_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_position_snapshots_report_date_as_of_utc",
                schema: "pms_shadow",
                table: "position_snapshots",
                columns: new[] { "report_date", "as_of_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_qubes_input_snapshots_ingestion_id_strategy_id",
                schema: "pms_shadow",
                table: "qubes_input_snapshots",
                columns: new[] { "ingestion_id", "strategy_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_qubes_input_snapshots_input_artifact_id",
                schema: "pms_shadow",
                table: "qubes_input_snapshots",
                column: "input_artifact_id");

            migrationBuilder.CreateIndex(
                name: "IX_qubes_input_snapshots_input_sha256",
                schema: "pms_shadow",
                table: "qubes_input_snapshots",
                column: "input_sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_qubes_input_snapshots_overlay_artifact_id",
                schema: "pms_shadow",
                table: "qubes_input_snapshots",
                column: "overlay_artifact_id");

            migrationBuilder.CreateIndex(
                name: "IX_qubes_input_snapshots_source_snapshot_artifact_id",
                schema: "pms_shadow",
                table: "qubes_input_snapshots",
                column: "source_snapshot_artifact_id");

            migrationBuilder.CreateIndex(
                name: "IX_qubes_input_snapshots_target_close_utc",
                schema: "pms_shadow",
                table: "qubes_input_snapshots",
                column: "target_close_utc");

            migrationBuilder.CreateIndex(
                name: "IX_security_mappings_ingestion_id_lmax_instrument_id",
                schema: "pms_shadow",
                table: "security_mappings",
                columns: new[] { "ingestion_id", "lmax_instrument_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_security_mappings_ingestion_id_security_id",
                schema: "pms_shadow",
                table: "security_mappings",
                columns: new[] { "ingestion_id", "security_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_source_artifacts_ingestion_id_sha256",
                schema: "pms_shadow",
                table: "source_artifacts",
                columns: new[] { "ingestion_id", "sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_source_artifacts_sha256_artifact_type_size_bytes_logical_uri",
                schema: "pms_shadow",
                table: "source_artifacts",
                columns: new[] { "sha256", "artifact_type", "size_bytes", "logical_uri" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_target_position_stages_account_snapshot_id",
                schema: "pms_shadow",
                table: "target_position_stages",
                column: "account_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_target_position_stages_market_data_snapshot_id",
                schema: "pms_shadow",
                table: "target_position_stages",
                column: "market_data_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "IX_target_position_stages_model_run_id",
                schema: "pms_shadow",
                table: "target_position_stages",
                column: "model_run_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_target_positions_model_run_id_instrument_id",
                schema: "pms_shadow",
                table: "target_positions",
                columns: new[] { "model_run_id", "instrument_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_target_weights_model_run_id_source_order",
                schema: "pms_shadow",
                table: "target_weights",
                columns: new[] { "model_run_id", "source_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_target_weights_model_run_id_source_row_key",
                schema: "pms_shadow",
                table: "target_weights",
                columns: new[] { "model_run_id", "source_row_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_target_weights_target_close_utc",
                schema: "pms_shadow",
                table: "target_weights",
                column: "target_close_utc");

            migrationBuilder.CreateIndex(
                name: "IX_working_leaves_observations_ingestion_id",
                schema: "pms_shadow",
                table: "working_leaves_observations",
                column: "ingestion_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "broker_adjusted_drift_stages",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "cycle_results",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "market_data_observations",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "position_only_drifts",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "position_snapshot_lines",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "security_mappings",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "target_positions",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "working_leaves_observations",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "position_only_drift_stages",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "target_position_stages",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "target_weights",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "position_snapshots",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "market_data_snapshots",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "model_runs",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "account_snapshots",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "qubes_input_snapshots",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "source_artifacts",
                schema: "pms_shadow");

            migrationBuilder.DropTable(
                name: "ingestions",
                schema: "pms_shadow");
        }
    }
}
