CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'pms_shadow') THEN
        CREATE SCHEMA pms_shadow;
    END IF;
END $EF$;

CREATE TABLE pms_shadow.ingestions (
    ingestion_id uuid NOT NULL,
    source_gate character varying(160) NOT NULL,
    source_session_id character varying(200) NOT NULL,
    source_evidence_sha256 character varying(64) NOT NULL,
    status character varying(32) NOT NULL,
    started_at_utc timestamp with time zone NOT NULL,
    completed_at_utc timestamp with time zone,
    contract_version character varying(96) NOT NULL,
    environment character varying(64) NOT NULL,
    classification character varying(64) NOT NULL,
    rowset_sha256 character varying(64) NOT NULL,
    CONSTRAINT "PK_ingestions" PRIMARY KEY (ingestion_id),
    CONSTRAINT ck_ingestions_classification CHECK (classification = 'EVIDENCE_ONLY_NONACCOUNTING'),
    CONSTRAINT ck_ingestions_environment CHECK (environment = 'LMAX_TEST_EOD_ONLY'),
    CONSTRAINT ck_ingestions_rowset_sha256 CHECK (rowset_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_ingestions_source_evidence_sha256 CHECK (source_evidence_sha256 ~ '^[0-9a-f]{64}$')
);

CREATE TABLE pms_shadow.account_snapshots (
    account_snapshot_id uuid NOT NULL,
    ingestion_id uuid NOT NULL,
    account_id character varying(160) NOT NULL,
    scope character varying(96) NOT NULL,
    base_currency character varying(3) NOT NULL,
    nav_or_equity numeric(28,8) NOT NULL,
    report_date date NOT NULL,
    as_of_utc timestamp with time zone NOT NULL,
    authority character varying(96) NOT NULL,
    source_artifact_sha256 character varying(64) NOT NULL,
    snapshot_sha256 character varying(64) NOT NULL,
    classification character varying(64) NOT NULL,
    CONSTRAINT "PK_account_snapshots" PRIMARY KEY (account_snapshot_id),
    CONSTRAINT ck_account_not_real CHECK (account_id <> '921640160'),
    CONSTRAINT ck_account_snapshot_sha256 CHECK (snapshot_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_account_source_sha256 CHECK (source_artifact_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT "FK_account_snapshots_ingestions_ingestion_id" FOREIGN KEY (ingestion_id) REFERENCES pms_shadow.ingestions (ingestion_id) ON DELETE RESTRICT
);

CREATE TABLE pms_shadow.market_data_snapshots (
    market_data_snapshot_id uuid NOT NULL,
    ingestion_id uuid NOT NULL,
    as_of_utc timestamp with time zone NOT NULL,
    snapshot_sha256 character varying(64) NOT NULL,
    observation_count integer NOT NULL,
    classification character varying(64) NOT NULL,
    CONSTRAINT "PK_market_data_snapshots" PRIMARY KEY (market_data_snapshot_id),
    CONSTRAINT ck_market_observation_count CHECK (observation_count > 0),
    CONSTRAINT ck_market_snapshot_sha256 CHECK (snapshot_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT "FK_market_data_snapshots_ingestions_ingestion_id" FOREIGN KEY (ingestion_id) REFERENCES pms_shadow.ingestions (ingestion_id) ON DELETE RESTRICT
);

CREATE TABLE pms_shadow.security_mappings (
    ingestion_id uuid NOT NULL,
    instrument_id uuid NOT NULL,
    venue_id uuid NOT NULL,
    venue_instrument_id uuid NOT NULL,
    security_id character varying(160) NOT NULL,
    symbol character varying(64) NOT NULL,
    lmax_instrument_id character varying(64) NOT NULL,
    quantity_multiplier numeric(28,12) NOT NULL,
    quantity_increment numeric(28,12) NOT NULL,
    price_increment numeric(28,12) NOT NULL,
    mapping_sha256 character varying(64) NOT NULL,
    CONSTRAINT "PK_security_mappings" PRIMARY KEY (ingestion_id, instrument_id),
    CONSTRAINT ck_security_mapping_positive CHECK (quantity_multiplier > 0 AND quantity_increment > 0 AND price_increment > 0),
    CONSTRAINT ck_security_mapping_sha256 CHECK (mapping_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT "FK_security_mappings_ingestions_ingestion_id" FOREIGN KEY (ingestion_id) REFERENCES pms_shadow.ingestions (ingestion_id) ON DELETE RESTRICT
);

CREATE TABLE pms_shadow.source_artifacts (
    artifact_id uuid NOT NULL,
    ingestion_id uuid NOT NULL,
    artifact_type character varying(96) NOT NULL,
    sha256 character varying(64) NOT NULL,
    size_bytes bigint NOT NULL,
    logical_uri character varying(1024) NOT NULL,
    contract_version character varying(96) NOT NULL,
    produced_at_utc timestamp with time zone NOT NULL,
    source_system character varying(96) NOT NULL,
    classification character varying(64) NOT NULL,
    CONSTRAINT "PK_source_artifacts" PRIMARY KEY (artifact_id),
    CONSTRAINT ck_source_artifacts_sha256 CHECK (sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT "FK_source_artifacts_ingestions_ingestion_id" FOREIGN KEY (ingestion_id) REFERENCES pms_shadow.ingestions (ingestion_id) ON DELETE RESTRICT
);

CREATE TABLE pms_shadow.working_leaves_observations (
    working_leaves_observation_id uuid NOT NULL,
    ingestion_id uuid NOT NULL,
    status character varying(96) NOT NULL,
    source_system character varying(96) NOT NULL,
    observation_attempted boolean NOT NULL,
    empty_state_observed boolean NOT NULL,
    empty_state_inferred boolean NOT NULL,
    broker_authority boolean NOT NULL,
    reason character varying(256) NOT NULL,
    impact character varying(160) NOT NULL,
    as_of_utc timestamp with time zone NOT NULL,
    classification character varying(64) NOT NULL,
    CONSTRAINT "PK_working_leaves_observations" PRIMARY KEY (working_leaves_observation_id),
    CONSTRAINT ck_working_leaves_not_empty_not_inferred CHECK (NOT empty_state_observed AND NOT empty_state_inferred AND NOT broker_authority),
    CONSTRAINT ck_working_leaves_unavailable CHECK (status = 'UNAVAILABLE_WITH_CURRENT_LMAX_INTERFACES'),
    CONSTRAINT "FK_working_leaves_observations_ingestions_ingestion_id" FOREIGN KEY (ingestion_id) REFERENCES pms_shadow.ingestions (ingestion_id) ON DELETE RESTRICT
);

CREATE TABLE pms_shadow.position_snapshots (
    position_snapshot_id uuid NOT NULL,
    ingestion_id uuid NOT NULL,
    account_snapshot_id uuid NOT NULL,
    report_date date NOT NULL,
    as_of_utc timestamp with time zone NOT NULL,
    snapshot_sha256 character varying(64) NOT NULL,
    empty_state_was_explicitly_observed boolean NOT NULL,
    empty_state_was_inferred boolean NOT NULL,
    broker_authority boolean NOT NULL,
    classification character varying(64) NOT NULL,
    CONSTRAINT "PK_position_snapshots" PRIMARY KEY (position_snapshot_id),
    CONSTRAINT ck_position_empty_not_inferred CHECK (NOT empty_state_was_inferred),
    CONSTRAINT ck_position_snapshot_sha256 CHECK (snapshot_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT "FK_position_snapshots_account_snapshots_account_snapshot_id" FOREIGN KEY (account_snapshot_id) REFERENCES pms_shadow.account_snapshots (account_snapshot_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_position_snapshots_ingestions_ingestion_id" FOREIGN KEY (ingestion_id) REFERENCES pms_shadow.ingestions (ingestion_id) ON DELETE RESTRICT
);

CREATE TABLE pms_shadow.market_data_observations (
    market_data_snapshot_id uuid NOT NULL,
    instrument_id uuid NOT NULL,
    security_id character varying(160) NOT NULL,
    lmax_instrument_id character varying(64) NOT NULL,
    symbol character varying(64) NOT NULL,
    bid numeric(38,28) NOT NULL,
    ask numeric(38,28) NOT NULL,
    event_time_utc timestamp with time zone NOT NULL,
    received_at_utc timestamp with time zone NOT NULL,
    staleness_milliseconds bigint NOT NULL,
    source_capture_id character varying(160) NOT NULL,
    source_file_sha256 character varying(64) NOT NULL,
    projection_method character varying(96) NOT NULL,
    projection_leg_security_ids_json jsonb NOT NULL,
    CONSTRAINT "PK_market_data_observations" PRIMARY KEY (market_data_snapshot_id, instrument_id),
    CONSTRAINT ck_market_bid_ask CHECK (bid > 0 AND ask >= bid),
    CONSTRAINT ck_market_source_sha256 CHECK (source_file_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_market_staleness CHECK (staleness_milliseconds >= 0),
    CONSTRAINT "FK_market_data_observations_market_data_snapshots_market_data_~" FOREIGN KEY (market_data_snapshot_id) REFERENCES pms_shadow.market_data_snapshots (market_data_snapshot_id) ON DELETE RESTRICT
);

CREATE TABLE pms_shadow.qubes_input_snapshots (
    snapshot_id uuid NOT NULL,
    ingestion_id uuid NOT NULL,
    input_artifact_id uuid NOT NULL,
    source_snapshot_artifact_id uuid NOT NULL,
    overlay_artifact_id uuid NOT NULL,
    strategy_id character varying(160) NOT NULL,
    source_snapshot_sha256 character varying(64) NOT NULL,
    overlay_sha256 character varying(64) NOT NULL,
    gap_ledger_sha256 character varying(64),
    mapping_sha256 character varying(64) NOT NULL,
    input_sha256 character varying(64) NOT NULL,
    target_close_utc timestamp with time zone NOT NULL,
    source_instrument_count integer NOT NULL,
    gap_count integer NOT NULL,
    provenance character varying(512) NOT NULL,
    classification character varying(64) NOT NULL,
    CONSTRAINT "PK_qubes_input_snapshots" PRIMARY KEY (snapshot_id),
    CONSTRAINT ck_qubes_counts CHECK (source_instrument_count > 0 AND gap_count >= 0),
    CONSTRAINT ck_qubes_input_sha256 CHECK (input_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_qubes_overlay_sha256 CHECK (overlay_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_qubes_source_sha256 CHECK (source_snapshot_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT "FK_qubes_input_snapshots_ingestions_ingestion_id" FOREIGN KEY (ingestion_id) REFERENCES pms_shadow.ingestions (ingestion_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_qubes_input_snapshots_source_artifacts_input_artifact_id" FOREIGN KEY (input_artifact_id) REFERENCES pms_shadow.source_artifacts (artifact_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_qubes_input_snapshots_source_artifacts_overlay_artifact_id" FOREIGN KEY (overlay_artifact_id) REFERENCES pms_shadow.source_artifacts (artifact_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_qubes_input_snapshots_source_artifacts_source_snapshot_arti~" FOREIGN KEY (source_snapshot_artifact_id) REFERENCES pms_shadow.source_artifacts (artifact_id) ON DELETE RESTRICT
);

CREATE TABLE pms_shadow.position_snapshot_lines (
    position_snapshot_id uuid NOT NULL,
    instrument_id uuid NOT NULL,
    security_id character varying(160) NOT NULL,
    symbol character varying(64) NOT NULL,
    current_base_quantity numeric(28,8) NOT NULL,
    CONSTRAINT "PK_position_snapshot_lines" PRIMARY KEY (position_snapshot_id, instrument_id),
    CONSTRAINT "FK_position_snapshot_lines_position_snapshots_position_snapsho~" FOREIGN KEY (position_snapshot_id) REFERENCES pms_shadow.position_snapshots (position_snapshot_id) ON DELETE RESTRICT
);

CREATE TABLE pms_shadow.model_runs (
    model_run_id uuid NOT NULL,
    ingestion_id uuid NOT NULL,
    qubes_input_snapshot_id uuid NOT NULL,
    output_artifact_id uuid NOT NULL,
    external_model_run_id character varying(200) NOT NULL,
    source_domain_model character varying(160) NOT NULL,
    strategy_id character varying(160) NOT NULL,
    benchmark_parameter numeric(28,12) NOT NULL,
    target_close_utc timestamp with time zone NOT NULL,
    as_of_utc timestamp with time zone NOT NULL,
    core_master_sha256 character varying(64) NOT NULL,
    package_sha256 character varying(64) NOT NULL,
    engine_sha256 character varying(64) NOT NULL,
    wrapper_exit_code integer NOT NULL,
    native_exit_code integer NOT NULL,
    semantic_status character varying(96) NOT NULL,
    r083_status character varying(96) NOT NULL,
    output_sha256 character varying(64) NOT NULL,
    contract_version character varying(96) NOT NULL,
    classification character varying(64) NOT NULL,
    accounting_eligible boolean NOT NULL,
    execution_allowed boolean NOT NULL,
    not_an_order boolean NOT NULL,
    CONSTRAINT "PK_model_runs" PRIMARY KEY (model_run_id),
    CONSTRAINT ck_model_run_exit_codes CHECK (wrapper_exit_code = 0 AND native_exit_code = 0),
    CONSTRAINT ck_model_run_hashes CHECK (core_master_sha256 ~ '^[0-9a-f]{64}$' AND package_sha256 ~ '^[0-9a-f]{64}$' AND engine_sha256 ~ '^[0-9a-f]{64}$' AND output_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_model_run_no_order CHECK (NOT accounting_eligible AND NOT execution_allowed AND not_an_order),
    CONSTRAINT "FK_model_runs_ingestions_ingestion_id" FOREIGN KEY (ingestion_id) REFERENCES pms_shadow.ingestions (ingestion_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_model_runs_qubes_input_snapshots_qubes_input_snapshot_id" FOREIGN KEY (qubes_input_snapshot_id) REFERENCES pms_shadow.qubes_input_snapshots (snapshot_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_model_runs_source_artifacts_output_artifact_id" FOREIGN KEY (output_artifact_id) REFERENCES pms_shadow.source_artifacts (artifact_id) ON DELETE RESTRICT
);

CREATE TABLE pms_shadow.broker_adjusted_drift_stages (
    stage_id uuid NOT NULL,
    model_run_id uuid NOT NULL,
    working_leaves_observation_id uuid NOT NULL,
    calculated boolean NOT NULL,
    blocker character varying(160) NOT NULL,
    empty_state_inferred boolean NOT NULL,
    status character varying(96) NOT NULL,
    classification character varying(64) NOT NULL,
    CONSTRAINT "PK_broker_adjusted_drift_stages" PRIMARY KEY (stage_id),
    CONSTRAINT ck_broker_adjusted_blocker CHECK (blocker = 'BROKER_WORKING_LEAVES_UNOBSERVABLE'),
    CONSTRAINT ck_broker_adjusted_not_calculated CHECK (NOT calculated AND NOT empty_state_inferred),
    CONSTRAINT "FK_broker_adjusted_drift_stages_model_runs_model_run_id" FOREIGN KEY (model_run_id) REFERENCES pms_shadow.model_runs (model_run_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_broker_adjusted_drift_stages_working_leaves_observations_wo~" FOREIGN KEY (working_leaves_observation_id) REFERENCES pms_shadow.working_leaves_observations (working_leaves_observation_id) ON DELETE RESTRICT
);

CREATE TABLE pms_shadow.cycle_results (
    result_id uuid NOT NULL,
    ingestion_id uuid NOT NULL,
    model_run_id uuid NOT NULL,
    manual_paper_cycle_status character varying(96) NOT NULL,
    r009_status character varying(96) NOT NULL,
    execution_allowed boolean NOT NULL,
    not_an_order boolean NOT NULL,
    no_broker_route boolean NOT NULL,
    no_fix_message boolean NOT NULL,
    order_entry_enabled boolean NOT NULL,
    broker_send_status character varying(96) NOT NULL,
    trade_intent_count integer NOT NULL,
    completed_at_utc timestamp with time zone NOT NULL,
    classification character varying(64) NOT NULL,
    CONSTRAINT "PK_cycle_results" PRIMARY KEY (result_id),
    CONSTRAINT ck_cycle_result_broker_send CHECK (broker_send_status = 'DISABLED_NO_ORDER_ENTRY'),
    CONSTRAINT ck_cycle_result_no_order CHECK (NOT execution_allowed AND not_an_order AND no_broker_route AND no_fix_message AND NOT order_entry_enabled AND trade_intent_count = 0),
    CONSTRAINT "FK_cycle_results_ingestions_ingestion_id" FOREIGN KEY (ingestion_id) REFERENCES pms_shadow.ingestions (ingestion_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_cycle_results_model_runs_model_run_id" FOREIGN KEY (model_run_id) REFERENCES pms_shadow.model_runs (model_run_id) ON DELETE RESTRICT
);

CREATE TABLE pms_shadow.position_only_drift_stages (
    stage_id uuid NOT NULL,
    model_run_id uuid NOT NULL,
    position_snapshot_id uuid NOT NULL,
    as_of_utc timestamp with time zone NOT NULL,
    status character varying(96) NOT NULL,
    classification character varying(64) NOT NULL,
    CONSTRAINT "PK_position_only_drift_stages" PRIMARY KEY (stage_id),
    CONSTRAINT "FK_position_only_drift_stages_model_runs_model_run_id" FOREIGN KEY (model_run_id) REFERENCES pms_shadow.model_runs (model_run_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_position_only_drift_stages_position_snapshots_position_snap~" FOREIGN KEY (position_snapshot_id) REFERENCES pms_shadow.position_snapshots (position_snapshot_id) ON DELETE RESTRICT
);

CREATE TABLE pms_shadow.target_position_stages (
    stage_id uuid NOT NULL,
    model_run_id uuid NOT NULL,
    account_snapshot_id uuid NOT NULL,
    market_data_snapshot_id uuid NOT NULL,
    status character varying(96) NOT NULL,
    classification character varying(64) NOT NULL,
    accounting_eligible boolean NOT NULL,
    execution_allowed boolean NOT NULL,
    CONSTRAINT "PK_target_position_stages" PRIMARY KEY (stage_id),
    CONSTRAINT ck_target_position_stage_no_order CHECK (NOT accounting_eligible AND NOT execution_allowed),
    CONSTRAINT "FK_target_position_stages_account_snapshots_account_snapshot_id" FOREIGN KEY (account_snapshot_id) REFERENCES pms_shadow.account_snapshots (account_snapshot_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_target_position_stages_market_data_snapshots_market_data_sn~" FOREIGN KEY (market_data_snapshot_id) REFERENCES pms_shadow.market_data_snapshots (market_data_snapshot_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_target_position_stages_model_runs_model_run_id" FOREIGN KEY (model_run_id) REFERENCES pms_shadow.model_runs (model_run_id) ON DELETE RESTRICT
);

CREATE TABLE pms_shadow.target_weights (
    model_run_id uuid NOT NULL,
    instrument_id uuid NOT NULL,
    security_id character varying(160) NOT NULL,
    weight numeric(28,12) NOT NULL,
    target_close_utc timestamp with time zone NOT NULL,
    source_row_key character varying(256) NOT NULL,
    source_order integer NOT NULL,
    output_sha256 character varying(64) NOT NULL,
    lineage_version character varying(96) NOT NULL,
    CONSTRAINT "PK_target_weights" PRIMARY KEY (model_run_id, instrument_id),
    CONSTRAINT ck_target_weight_output_sha256 CHECK (output_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT "FK_target_weights_model_runs_model_run_id" FOREIGN KEY (model_run_id) REFERENCES pms_shadow.model_runs (model_run_id) ON DELETE RESTRICT
);

CREATE TABLE pms_shadow.position_only_drifts (
    stage_id uuid NOT NULL,
    instrument_id uuid NOT NULL,
    model_run_id uuid NOT NULL,
    security_id character varying(160) NOT NULL,
    current_base_quantity numeric(28,8) NOT NULL,
    target_base_quantity numeric(28,8) NOT NULL,
    position_only_delta_base_quantity numeric(28,8) NOT NULL,
    as_of_utc timestamp with time zone NOT NULL,
    status character varying(96) NOT NULL,
    CONSTRAINT "PK_position_only_drifts" PRIMARY KEY (stage_id, instrument_id),
    CONSTRAINT "FK_position_only_drifts_position_only_drift_stages_stage_id" FOREIGN KEY (stage_id) REFERENCES pms_shadow.position_only_drift_stages (stage_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_position_only_drifts_target_weights_model_run_id_instrument~" FOREIGN KEY (model_run_id, instrument_id) REFERENCES pms_shadow.target_weights (model_run_id, instrument_id) ON DELETE RESTRICT
);

CREATE TABLE pms_shadow.target_positions (
    stage_id uuid NOT NULL,
    instrument_id uuid NOT NULL,
    model_run_id uuid NOT NULL,
    security_id character varying(160) NOT NULL,
    target_notional_usd numeric(28,12) NOT NULL,
    target_base_quantity numeric(28,8) NOT NULL,
    target_venue_quantity numeric(28,8) NOT NULL,
    sizing_policy character varying(96) NOT NULL,
    rounding_policy character varying(96) NOT NULL,
    status character varying(96) NOT NULL,
    classification character varying(64) NOT NULL,
    CONSTRAINT "PK_target_positions" PRIMARY KEY (stage_id, instrument_id),
    CONSTRAINT "FK_target_positions_target_position_stages_stage_id" FOREIGN KEY (stage_id) REFERENCES pms_shadow.target_position_stages (stage_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_target_positions_target_weights_model_run_id_instrument_id" FOREIGN KEY (model_run_id, instrument_id) REFERENCES pms_shadow.target_weights (model_run_id, instrument_id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_account_snapshots_ingestion_id_snapshot_sha256" ON pms_shadow.account_snapshots (ingestion_id, snapshot_sha256);

CREATE INDEX "IX_account_snapshots_report_date_as_of_utc" ON pms_shadow.account_snapshots (report_date, as_of_utc);

CREATE UNIQUE INDEX "IX_broker_adjusted_drift_stages_model_run_id" ON pms_shadow.broker_adjusted_drift_stages (model_run_id);

CREATE INDEX "IX_broker_adjusted_drift_stages_working_leaves_observation_id" ON pms_shadow.broker_adjusted_drift_stages (working_leaves_observation_id);

CREATE INDEX "IX_cycle_results_completed_at_utc" ON pms_shadow.cycle_results (completed_at_utc);

CREATE UNIQUE INDEX "IX_cycle_results_ingestion_id_model_run_id" ON pms_shadow.cycle_results (ingestion_id, model_run_id);

CREATE INDEX "IX_cycle_results_model_run_id" ON pms_shadow.cycle_results (model_run_id);

CREATE INDEX "IX_ingestions_completed_at_utc" ON pms_shadow.ingestions (completed_at_utc);

CREATE UNIQUE INDEX "IX_ingestions_source_session_id" ON pms_shadow.ingestions (source_session_id);

CREATE UNIQUE INDEX "IX_ingestions_source_session_id_source_evidence_sha256_rowset_~" ON pms_shadow.ingestions (source_session_id, source_evidence_sha256, rowset_sha256);

CREATE INDEX "IX_market_data_snapshots_as_of_utc" ON pms_shadow.market_data_snapshots (as_of_utc);

CREATE UNIQUE INDEX "IX_market_data_snapshots_ingestion_id_snapshot_sha256" ON pms_shadow.market_data_snapshots (ingestion_id, snapshot_sha256);

CREATE UNIQUE INDEX "IX_model_runs_external_model_run_id_output_sha256" ON pms_shadow.model_runs (external_model_run_id, output_sha256);

CREATE UNIQUE INDEX "IX_model_runs_ingestion_id_strategy_id" ON pms_shadow.model_runs (ingestion_id, strategy_id);

CREATE INDEX "IX_model_runs_output_artifact_id" ON pms_shadow.model_runs (output_artifact_id);

CREATE INDEX "IX_model_runs_qubes_input_snapshot_id" ON pms_shadow.model_runs (qubes_input_snapshot_id);

CREATE INDEX "IX_model_runs_target_close_utc" ON pms_shadow.model_runs (target_close_utc);

CREATE INDEX "IX_position_only_drift_stages_as_of_utc" ON pms_shadow.position_only_drift_stages (as_of_utc);

CREATE UNIQUE INDEX "IX_position_only_drift_stages_model_run_id" ON pms_shadow.position_only_drift_stages (model_run_id);

CREATE INDEX "IX_position_only_drift_stages_position_snapshot_id" ON pms_shadow.position_only_drift_stages (position_snapshot_id);

CREATE UNIQUE INDEX "IX_position_only_drifts_model_run_id_instrument_id" ON pms_shadow.position_only_drifts (model_run_id, instrument_id);

CREATE INDEX "IX_position_snapshots_account_snapshot_id" ON pms_shadow.position_snapshots (account_snapshot_id);

CREATE UNIQUE INDEX "IX_position_snapshots_ingestion_id_snapshot_sha256" ON pms_shadow.position_snapshots (ingestion_id, snapshot_sha256);

CREATE INDEX "IX_position_snapshots_report_date_as_of_utc" ON pms_shadow.position_snapshots (report_date, as_of_utc);

CREATE UNIQUE INDEX "IX_qubes_input_snapshots_ingestion_id_strategy_id" ON pms_shadow.qubes_input_snapshots (ingestion_id, strategy_id);

CREATE INDEX "IX_qubes_input_snapshots_input_artifact_id" ON pms_shadow.qubes_input_snapshots (input_artifact_id);

CREATE UNIQUE INDEX "IX_qubes_input_snapshots_input_sha256" ON pms_shadow.qubes_input_snapshots (input_sha256);

CREATE INDEX "IX_qubes_input_snapshots_overlay_artifact_id" ON pms_shadow.qubes_input_snapshots (overlay_artifact_id);

CREATE INDEX "IX_qubes_input_snapshots_source_snapshot_artifact_id" ON pms_shadow.qubes_input_snapshots (source_snapshot_artifact_id);

CREATE INDEX "IX_qubes_input_snapshots_target_close_utc" ON pms_shadow.qubes_input_snapshots (target_close_utc);

CREATE UNIQUE INDEX "IX_security_mappings_ingestion_id_lmax_instrument_id" ON pms_shadow.security_mappings (ingestion_id, lmax_instrument_id);

CREATE UNIQUE INDEX "IX_security_mappings_ingestion_id_security_id" ON pms_shadow.security_mappings (ingestion_id, security_id);

CREATE UNIQUE INDEX "IX_source_artifacts_ingestion_id_sha256" ON pms_shadow.source_artifacts (ingestion_id, sha256);

CREATE UNIQUE INDEX "IX_source_artifacts_sha256_artifact_type_size_bytes_logical_uri" ON pms_shadow.source_artifacts (sha256, artifact_type, size_bytes, logical_uri);

CREATE INDEX "IX_target_position_stages_account_snapshot_id" ON pms_shadow.target_position_stages (account_snapshot_id);

CREATE INDEX "IX_target_position_stages_market_data_snapshot_id" ON pms_shadow.target_position_stages (market_data_snapshot_id);

CREATE UNIQUE INDEX "IX_target_position_stages_model_run_id" ON pms_shadow.target_position_stages (model_run_id);

CREATE UNIQUE INDEX "IX_target_positions_model_run_id_instrument_id" ON pms_shadow.target_positions (model_run_id, instrument_id);

CREATE UNIQUE INDEX "IX_target_weights_model_run_id_source_order" ON pms_shadow.target_weights (model_run_id, source_order);

CREATE UNIQUE INDEX "IX_target_weights_model_run_id_source_row_key" ON pms_shadow.target_weights (model_run_id, source_row_key);

CREATE INDEX "IX_target_weights_target_close_utc" ON pms_shadow.target_weights (target_close_utc);

CREATE UNIQUE INDEX "IX_working_leaves_observations_ingestion_id" ON pms_shadow.working_leaves_observations (ingestion_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260721152240_InitialPostgreSqlPmsShadowState', '10.0.0');

COMMIT;
