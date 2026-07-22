using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.PostgreSql.Migrations;

[DbContext(typeof(PmsShadowDbContext))]
[Migration("20260722231500_AddIntradayEconomicProjectionRevisions")]
public sealed class AddIntradayEconomicProjectionRevisions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE pms_shadow.intraday_projection_revisions (
                projection_revision_id uuid PRIMARY KEY,
                revision_number integer NOT NULL,
                slot_id character varying(64) NOT NULL
                    REFERENCES pms_shadow.intraday_slots (slot_id),
                raw_capture_sha256 character varying(64) NOT NULL,
                market_data_snapshot_id uuid NOT NULL,
                market_data_snapshot_sha256 character varying(64) NOT NULL,
                source_ingestion_id uuid NOT NULL
                    REFERENCES pms_shadow.ingestions (ingestion_id),
                source_session_id character varying(200) NOT NULL,
                position_snapshot_id uuid NOT NULL
                    REFERENCES pms_shadow.position_snapshots (position_snapshot_id),
                input_sha256 character varying(64) NOT NULL,
                target_positions_sha256 character varying(64) NOT NULL,
                drifts_sha256 character varying(64) NOT NULL,
                manifest_sha256 character varying(64) NOT NULL,
                supersedes_slot_manifest_sha256 character varying(64),
                status character varying(32) NOT NULL,
                external_completion_status character varying(32) NOT NULL,
                qualifying boolean NOT NULL,
                no_order boolean NOT NULL,
                completed_at_utc timestamp with time zone NOT NULL,
                projection_json jsonb NOT NULL,
                CONSTRAINT uq_intraday_projection_revision UNIQUE (slot_id, revision_number),
                CONSTRAINT uq_intraday_projection_manifest UNIQUE (manifest_sha256),
                CONSTRAINT ck_intraday_projection_revision_number CHECK (revision_number >= 1),
                CONSTRAINT ck_intraday_projection_status CHECK (status = 'COMPLETED' AND
                    external_completion_status = 'CompletedNoExternal'),
                CONSTRAINT ck_intraday_projection_qualifying CHECK (qualifying),
                CONSTRAINT ck_intraday_projection_no_order CHECK (no_order),
                CONSTRAINT ck_intraday_projection_hashes CHECK (
                    raw_capture_sha256 ~ '^[0-9a-f]{64}$' AND
                    market_data_snapshot_sha256 ~ '^[0-9a-f]{64}$' AND
                    input_sha256 ~ '^[0-9a-f]{64}$' AND
                    target_positions_sha256 ~ '^[0-9a-f]{64}$' AND
                    drifts_sha256 ~ '^[0-9a-f]{64}$' AND
                    manifest_sha256 ~ '^[0-9a-f]{64}$' AND
                    (supersedes_slot_manifest_sha256 IS NULL OR
                        supersedes_slot_manifest_sha256 ~ '^[0-9a-f]{64}$'))
            );
            CREATE INDEX ix_intraday_projection_latest
                ON pms_shadow.intraday_projection_revisions (completed_at_utc DESC, projection_revision_id);
            CREATE INDEX ix_intraday_projection_history
                ON pms_shadow.intraday_projection_revisions (slot_id, revision_number DESC);

            CREATE TABLE pms_shadow.intraday_market_data_observations (
                projection_revision_id uuid NOT NULL
                    REFERENCES pms_shadow.intraday_projection_revisions (projection_revision_id),
                instrument_id uuid NOT NULL,
                security_id character varying(64) NOT NULL,
                symbol character varying(32) NOT NULL,
                lmax_instrument_id character varying(128) NOT NULL,
                bid numeric(28,12) NOT NULL,
                ask numeric(28,12) NOT NULL,
                decision_price numeric(28,12) NOT NULL,
                event_time_utc timestamp with time zone NOT NULL,
                projection_method character varying(64) NOT NULL,
                projection_leg_security_ids_json jsonb NOT NULL,
                PRIMARY KEY (projection_revision_id, instrument_id),
                CONSTRAINT ck_intraday_market_prices CHECK (
                    bid > 0 AND ask > 0 AND ask >= bid AND decision_price = (bid + ask) / 2)
            );

            CREATE TABLE pms_shadow.intraday_target_positions (
                target_position_id uuid PRIMARY KEY,
                projection_revision_id uuid NOT NULL
                    REFERENCES pms_shadow.intraday_projection_revisions (projection_revision_id),
                stage_id uuid NOT NULL,
                model_run_id uuid NOT NULL REFERENCES pms_shadow.model_runs (model_run_id),
                strategy_id character varying(64) NOT NULL,
                instrument_id uuid NOT NULL,
                security_id character varying(64) NOT NULL,
                target_notional_usd numeric(28,12) NOT NULL,
                target_base_quantity numeric(28,12) NOT NULL,
                target_venue_quantity numeric(28,12) NOT NULL,
                decision_price numeric(28,12) NOT NULL,
                target_close_utc timestamp with time zone NOT NULL,
                calculated_at_utc timestamp with time zone NOT NULL,
                input_sha256 character varying(64) NOT NULL,
                output_sha256 character varying(64) NOT NULL,
                no_order boolean NOT NULL,
                CONSTRAINT uq_intraday_target_fact UNIQUE
                    (projection_revision_id, model_run_id, instrument_id),
                CONSTRAINT ck_intraday_target_hashes CHECK (
                    input_sha256 ~ '^[0-9a-f]{64}$' AND output_sha256 ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_intraday_target_price CHECK (decision_price > 0),
                CONSTRAINT ck_intraday_target_no_order CHECK (no_order)
            );
            CREATE INDEX ix_intraday_target_latest
                ON pms_shadow.intraday_target_positions (strategy_id, security_id, projection_revision_id);

            CREATE TABLE pms_shadow.intraday_position_only_drifts (
                drift_id uuid PRIMARY KEY,
                projection_revision_id uuid NOT NULL
                    REFERENCES pms_shadow.intraday_projection_revisions (projection_revision_id),
                stage_id uuid NOT NULL,
                model_run_id uuid NOT NULL REFERENCES pms_shadow.model_runs (model_run_id),
                strategy_id character varying(64) NOT NULL,
                instrument_id uuid NOT NULL,
                security_id character varying(64) NOT NULL,
                current_base_quantity numeric(28,12) NOT NULL,
                target_base_quantity numeric(28,12) NOT NULL,
                delta numeric(28,12) NOT NULL,
                as_of_utc timestamp with time zone NOT NULL,
                input_sha256 character varying(64) NOT NULL,
                output_sha256 character varying(64) NOT NULL,
                broker_adjusted_calculated boolean NOT NULL,
                working_leaves_blocker character varying(96) NOT NULL,
                no_order boolean NOT NULL,
                CONSTRAINT uq_intraday_drift_fact UNIQUE
                    (projection_revision_id, model_run_id, instrument_id),
                CONSTRAINT ck_intraday_drift_delta CHECK (
                    delta = target_base_quantity - current_base_quantity),
                CONSTRAINT ck_intraday_drift_hashes CHECK (
                    input_sha256 ~ '^[0-9a-f]{64}$' AND output_sha256 ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_intraday_drift_broker_blocker CHECK (
                    NOT broker_adjusted_calculated AND
                    working_leaves_blocker = 'BROKER_WORKING_LEAVES_UNOBSERVABLE'),
                CONSTRAINT ck_intraday_drift_no_order CHECK (no_order)
            );
            CREATE INDEX ix_intraday_drift_latest
                ON pms_shadow.intraday_position_only_drifts (strategy_id, security_id, projection_revision_id);

            DO $grant$
            BEGIN
                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'qq_arch6d_pms_runtime') THEN
                    GRANT SELECT, INSERT ON
                        pms_shadow.intraday_projection_revisions,
                        pms_shadow.intraday_market_data_observations,
                        pms_shadow.intraday_target_positions,
                        pms_shadow.intraday_position_only_drifts
                    TO qq_arch6d_pms_runtime;
                END IF;
                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'qq_arch6d_pms_reporting') THEN
                    GRANT SELECT ON
                        pms_shadow.intraday_projection_revisions,
                        pms_shadow.intraday_market_data_observations,
                        pms_shadow.intraday_target_positions,
                        pms_shadow.intraday_position_only_drifts
                    TO qq_arch6d_pms_reporting;
                END IF;
            END
            $grant$;
            """.ReplaceLineEndings("\n"));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS pms_shadow.intraday_position_only_drifts;
            DROP TABLE IF EXISTS pms_shadow.intraday_target_positions;
            DROP TABLE IF EXISTS pms_shadow.intraday_market_data_observations;
            DROP TABLE IF EXISTS pms_shadow.intraday_projection_revisions;
            """.ReplaceLineEndings("\n"));
    }
}
