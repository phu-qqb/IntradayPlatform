# ARCH6D Down schema contract decision

The repository is pre-production and the initial migration has no production deployment. ARCH6D therefore changes only the versioned `Down` method of `20260721152240_InitialPostgreSqlPmsShadowState`; its `Up` method and already-applied database state are unchanged.

After all 18 `pms_shadow` tables are dropped in dependency order, the migration calls `DropSchema("pms_shadow")`. No `CASCADE` is used. The corrective migration rolls back to the exact initial model before the initial migration removes the tables and schema.

The operational rollback command requires the exact two-migration chain and migrates to EF's initial database target. The final scratch qualification must prove zero tables, zero relevant migration-history rows, and `schema_exists_after_full_down = false` before deleting the scratch database.
