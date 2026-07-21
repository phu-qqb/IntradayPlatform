ALTER TABLE pms_shadow.model_runs DROP CONSTRAINT ck_model_run_hashes;

ALTER TABLE pms_shadow.model_runs RENAME COLUMN core_master_sha256 TO core_master_commit_id;

ALTER TABLE pms_shadow.model_runs ADD core_master_object_format character varying(6) GENERATED ALWAYS AS (CASE WHEN length(core_master_commit_id) = 40 THEN 'sha1' WHEN length(core_master_commit_id) = 64 THEN 'sha256' ELSE 'invalid' END) STORED NOT NULL;

ALTER TABLE pms_shadow.model_runs ADD CONSTRAINT ck_model_run_artifact_hashes CHECK (package_sha256 ~ '^[0-9a-f]{64}$' AND engine_sha256 ~ '^[0-9a-f]{64}$' AND output_sha256 ~ '^[0-9a-f]{64}$');

ALTER TABLE pms_shadow.model_runs ADD CONSTRAINT ck_model_run_core_master_commit_identity CHECK (core_master_object_format IN ('sha1', 'sha256') AND ((core_master_object_format = 'sha1' AND core_master_commit_id ~ '^[0-9a-f]{40}$') OR (core_master_object_format = 'sha256' AND core_master_commit_id ~ '^[0-9a-f]{64}$')));

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260721175549_CorrectGitCommitIdentityContract', '10.0.0');
