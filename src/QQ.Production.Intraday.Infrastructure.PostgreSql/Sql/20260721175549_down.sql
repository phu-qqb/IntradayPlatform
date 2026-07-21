ALTER TABLE pms_shadow.model_runs DROP CONSTRAINT ck_model_run_artifact_hashes;

ALTER TABLE pms_shadow.model_runs DROP CONSTRAINT ck_model_run_core_master_commit_identity;

ALTER TABLE pms_shadow.model_runs DROP COLUMN core_master_object_format;

ALTER TABLE pms_shadow.model_runs RENAME COLUMN core_master_commit_id TO core_master_sha256;

ALTER TABLE pms_shadow.model_runs ADD CONSTRAINT ck_model_run_hashes CHECK (core_master_sha256 ~ '^[0-9a-f]{64}$' AND package_sha256 ~ '^[0-9a-f]{64}$' AND engine_sha256 ~ '^[0-9a-f]{64}$' AND output_sha256 ~ '^[0-9a-f]{64}$');

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20260721175549_CorrectGitCommitIdentityContract';

DROP TABLE pms_shadow.broker_adjusted_drift_stages;

DROP TABLE pms_shadow.cycle_results;

DROP TABLE pms_shadow.market_data_observations;

DROP TABLE pms_shadow.position_only_drifts;

DROP TABLE pms_shadow.position_snapshot_lines;

DROP TABLE pms_shadow.security_mappings;

DROP TABLE pms_shadow.target_positions;

DROP TABLE pms_shadow.working_leaves_observations;

DROP TABLE pms_shadow.position_only_drift_stages;

DROP TABLE pms_shadow.target_position_stages;

DROP TABLE pms_shadow.target_weights;

DROP TABLE pms_shadow.position_snapshots;

DROP TABLE pms_shadow.market_data_snapshots;

DROP TABLE pms_shadow.model_runs;

DROP TABLE pms_shadow.account_snapshots;

DROP TABLE pms_shadow.qubes_input_snapshots;

DROP TABLE pms_shadow.source_artifacts;

DROP TABLE pms_shadow.ingestions;

DROP SCHEMA pms_shadow;

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20260721152240_InitialPostgreSqlPmsShadowState';
