START TRANSACTION;
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

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20260721152240_InitialPostgreSqlPmsShadowState';

COMMIT;
