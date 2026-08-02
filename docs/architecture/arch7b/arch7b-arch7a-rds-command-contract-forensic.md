# ARCH7B ARCH7A RDS command contract forensic

Verdict: ARCH7A_LEGACY_ARCH6D_RUNTIME_NOT_COMPATIBLE_WITH_ARCH7B_RDS_TEST.

The frozen executable accepted preflight, apply-and-qualify,
resume-and-qualify, and read, but the frozen command manifest selected
qualify-shadow. The executable required
QQ_PMS_SHADOW_ARCH7A_CONNECTION_STRING, qq_pms_shadow_arch6d_test,
loopback, PostgreSQL 16, and automatic migration for its write modes. It
selected the latest qualifying revision rather than the revision named by an
ARCH7B position-market binding.

The corrective path keeps the legacy modes isolated and intercepts only
qualify-shadow. It reuses PmsShadowPostgreSqlTargetContract and
arch7b_postgresql_transport_profile_v2, requires direct RDS TEST,
PostgreSQL 18, VerifyFull, the qualified regional root CA, one physical
connection, eight exact migrations, no pending model changes, an explicit
economic revision and binding, append-only persist/replay, exact readback,
and the existing no-order pipeline.

The historical blocker and evidence remain append-only. No one-shot identity,
secret read, database connection, LMAX session, FIX operation, order, Fill,
or ledger event occurred during the failed frozen invocation.
