DO $arch7a$
DECLARE
    qualifier_oid oid;
BEGIN
    IF current_database() <> 'qq_pms_shadow_arch7b_test' THEN
        RAISE EXCEPTION 'ARCH7A_PRIVILEGE_TARGET_DATABASE_MISMATCH';
    END IF;
    IF current_user <> 'qqpgadmin' THEN
        RAISE EXCEPTION 'ARCH7A_PRIVILEGE_ADMIN_ROLE_MISMATCH';
    END IF;
    IF current_setting('server_version_num')::integer / 10000 <> 18 THEN
        RAISE EXCEPTION 'ARCH7A_PRIVILEGE_POSTGRESQL_MAJOR_MISMATCH';
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_catalog.pg_roles
        WHERE rolname = 'qq_arch7b_position_importer' AND rolcanlogin
    ) THEN
        RAISE EXCEPTION 'ARCH7A_PRIVILEGE_LOGIN_ROLE_MISSING';
    END IF;

    SELECT oid INTO qualifier_oid
    FROM pg_catalog.pg_roles
    WHERE rolname = 'qq_arch7a_shadow_qualifier';

    IF qualifier_oid IS NULL THEN
        CREATE ROLE qq_arch7a_shadow_qualifier
            NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE
            NOINHERIT NOREPLICATION NOBYPASSRLS;
    ELSIF NOT EXISTS (
        SELECT 1 FROM pg_catalog.pg_roles
        WHERE oid = qualifier_oid
          AND NOT rolcanlogin
          AND NOT rolsuper
          AND NOT rolcreatedb
          AND NOT rolcreaterole
          AND NOT rolinherit
          AND NOT rolreplication
          AND NOT rolbypassrls
    ) THEN
        RAISE EXCEPTION 'ARCH7A_PRIVILEGE_EXISTING_ROLE_ATTRIBUTES_MISMATCH';
    END IF;
END
$arch7a$;

GRANT USAGE ON SCHEMA pms_shadow
TO qq_arch7a_shadow_qualifier;

GRANT SELECT ON TABLE
    public."__EFMigrationsHistory",
    pms_shadow.intraday_projection_revisions,
    pms_shadow.intraday_slots,
    pms_shadow.account_snapshots,
    pms_shadow.position_snapshots,
    pms_shadow.position_snapshot_lines,
    pms_shadow.working_leaves_observations,
    pms_shadow.model_runs,
    pms_shadow.security_mappings,
    pms_shadow.shadow_trade_intents,
    pms_shadow.shadow_risk_decisions,
    pms_shadow.shadow_parent_orders,
    pms_shadow.shadow_child_orders,
    pms_shadow.shadow_execution_qualification_runs
TO qq_arch7a_shadow_qualifier;

GRANT INSERT ON TABLE
    pms_shadow.shadow_trade_intents,
    pms_shadow.shadow_risk_decisions,
    pms_shadow.shadow_parent_orders,
    pms_shadow.shadow_child_orders,
    pms_shadow.shadow_execution_qualification_runs
TO qq_arch7a_shadow_qualifier;

GRANT qq_arch7a_shadow_qualifier
TO qq_arch7b_position_importer
WITH INHERIT FALSE, SET TRUE, ADMIN FALSE;
