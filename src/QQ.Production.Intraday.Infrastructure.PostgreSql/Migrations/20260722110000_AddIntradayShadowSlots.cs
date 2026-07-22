using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.PostgreSql.Migrations;

[DbContext(typeof(PmsShadowDbContext))]
[Migration("20260722110000_AddIntradayShadowSlots")]
public sealed class AddIntradayShadowSlots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE pms_shadow.intraday_slots (
                slot_id character varying(64) PRIMARY KEY,
                slot_start_utc timestamp with time zone NOT NULL,
                slot_end_utc timestamp with time zone NOT NULL,
                operational_date date NOT NULL,
                status character varying(32) NOT NULL,
                contract_version character varying(96) NOT NULL,
                cadence_mode character varying(96) NOT NULL,
                coordinator_id character varying(160) NOT NULL,
                claimed_at_utc timestamp with time zone NOT NULL,
                completed_at_utc timestamp with time zone NULL,
                manifest_json jsonb NULL,
                manifest_sha256 character varying(64) NULL,
                ingestion_id uuid NULL,
                source_session_id character varying(200) NULL,
                failure_code character varying(160) NULL,
                no_order boolean NOT NULL,
                CONSTRAINT fk_intraday_slots_ingestions_ingestion_id
                    FOREIGN KEY (ingestion_id) REFERENCES pms_shadow.ingestions (ingestion_id) ON DELETE RESTRICT,
                CONSTRAINT ck_intraday_slots_status
                    CHECK (status IN ('MISSED','RUNNING','COMPLETED','FAILED_CLOSED')),
                CONSTRAINT ck_intraday_slots_window
                    CHECK (slot_end_utc = slot_start_utc + interval '15 minutes'
                        AND extract(second from slot_start_utc) = 0
                        AND mod(extract(minute from slot_start_utc)::integer, 15) = 0),
                CONSTRAINT ck_intraday_slots_contract
                    CHECK (contract_version = 'pms_shadow_intraday_15m_cadence_v1'
                        AND cadence_mode = 'FRESH_DRIFT_EVERY_15_MINUTES_WITH_MODEL_SCHEDULE'),
                CONSTRAINT ck_intraday_slots_manifest_sha256
                    CHECK (manifest_sha256 IS NULL OR manifest_sha256 ~ '^[0-9a-f]{64}$'),
                CONSTRAINT ck_intraday_slots_no_order CHECK (no_order),
                CONSTRAINT ck_intraday_slots_completion
                    CHECK ((status = 'RUNNING' AND completed_at_utc IS NULL AND manifest_json IS NULL)
                        OR (status = 'COMPLETED' AND completed_at_utc IS NOT NULL
                            AND manifest_json IS NOT NULL AND manifest_sha256 IS NOT NULL
                            AND ingestion_id IS NOT NULL AND source_session_id IS NOT NULL)
                        OR (status IN ('MISSED','FAILED_CLOSED') AND completed_at_utc IS NOT NULL))
            );
            CREATE UNIQUE INDEX ix_intraday_slots_window
                ON pms_shadow.intraday_slots (slot_start_utc, slot_end_utc);
            CREATE INDEX ix_intraday_slots_latest
                ON pms_shadow.intraday_slots (status, slot_end_utc DESC);
            CREATE INDEX ix_intraday_slots_operational_date
                ON pms_shadow.intraday_slots (operational_date, slot_start_utc);
            CREATE UNIQUE INDEX ix_intraday_slots_manifest_sha256
                ON pms_shadow.intraday_slots (manifest_sha256) WHERE manifest_sha256 IS NOT NULL;
            """.ReplaceLineEndings("\n"));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS pms_shadow.intraday_slots;");
    }
}
