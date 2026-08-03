# ARCH7B one-shot live execution candidate v2

Verdict: `ARCH7B_PR53_UPDATED_REAL_PLAN_MATERIALIZATION_AND_COMMAND_ADAPTERS_READY`

This packet binds the corrected runtime to commit
`ab0ae068353a649bfe2ce4cccd1f991f2457b042` and tree
`338ab43a9a66c847aa5eb47e4453970527502e8c`. The packet files are added by a
later documentation-only commit; their byte hashes and that final PR HEAD/tree
are external bindings recorded in PR #53, avoiding a recursive commit identity.

## Forensic closure

The original gaps A-H are recorded in
`arch7b-pr53-live-runtime-integration-gap-forensic.json` and `.md`. They are
closed by a static template plus append-only typed fact store, progressive typed
command materialization, runtime slot selection and lock, explicit process
execution kinds, long-lived process ownership, bounded streaming output,
exact-value secret scanning, strict native adapters, and complete authority
binding.

Artifact identities are reconciled in
`arch7b-pr53-artifact-identity-reconciliation.json`. The historical
`87f149...` value names canonical packet evidence while `a2d258...` names JSON
file bytes; neither is reused as an arbitrary current v2 identity.

## Runtime contracts

- Plan template: `arch7b_one_shot_live_plan_template_v1`
- Fact store: `arch7b_one_shot_live_fact_store_v1`
- Command template: `arch7b_one_shot_command_template_v1`
- Materialized command: `arch7b_one_shot_materialized_command_v1`
- Long-lived registry: `arch7b_one_shot_long_lived_process_registry_v1`
- Secret environment injection: `arch7b_one_shot_secret_environment_injection_v1`
- Operator authorization: `arch7b_one_shot_operator_authorization_v2`
- Live execution authority: `arch7b_one_shot_live_execution_authority_v2`

The chronology remains 40 stages. Fifteen command templates use fourteen
strict adapters; internal and filesystem stages do not spawn artificial child
processes. Every child invocation receives a content-addressed
`stage-command-authority.json`.

## Secret ownership

Classification is **B**:
`CORE_LEASE_PROCESS_OWNS_SECRET_AND_SPAWNS_SECRET_CHILDREN`.

The supervisor never asks for or receives the Core-owned secret. Its separate
secret lease contract still enforces command allowlists, child-only environment
injection, exact-value streaming scans, two reads maximum, and zero reads after
the bracket. Qualification used zero real secret reads.

## Process lifecycle

The preloaded RDS lease and market recorder are explicitly long-lived. READY
and COMPLETE markers are published atomically, state transitions are recorded,
and terminal cleanup runs in reverse order. A Primary-only timing race in
`HANDOFF_V3` was reproduced and corrected by writing the completion signal to a
temporary path before atomic rename. Twenty repeated local lifecycles pass with
zero residual processes and markers.

## Qualification

- V2 tests: 20/20 PASS
- Independent real-adapter rehearsals: 20/20 PASS
- Campaigns: 10/10 PASS, three runs each
- Extended negative matrix: 31 catalogued cases
- Historical runtime/security: 23/23 PASS
- Historical supervisor: 38/38 PASS
- Relevant regressions: 993/993 PASS
- Release build: 0 errors
- Pending model changes: false
- PostgreSQL migrations: 8

Primary `i-05535ebe6ce80a57b`, runtime root
`D:\QQFund\ARCH7B\runtime\supervisor-candidate-ab0ae068-r3`:

- Static template/authority: 3/3 PASS
- Materialized plan: 3/3 PASS
- Real adapters: 3/3 PASS
- Long-lived lifecycle: 3/3 PASS
- Residual processes/markers: 0/0
- Evidence file SHA-256:
  `dc3598ab8b5b4d88aac99d1852b0e0db7d1d4770673ff375b6ca766238165076`

Current binary identities:

- Supervisor assembly: `7357c1e2f1185b8b2f451361bdbc63ee31a8211b783432772c46c4d08cd08bd6`
- Supervisor apphost: `268feb5923d8e4c9fbe4e7fa5af007a622e214382d2e88adb1313b865222e06e`
- Published executable: `268feb5923d8e4c9fbe4e7fa5af007a622e214382d2e88adb1313b865222e06e`
- Runtime inventory: `3c179c9560d716bd5d35fc81386527acc1e85df06e5eb49a1f2df65ee48bbe6c`
- Primary source archive: `29b8491a201e24ed69c0fb2d9b978ef7ecb7f45909e4937f797070e09e1fa36f`
- Candidate evidence: `d982344edcc457499df618a2493a5e1b3d9ce03931dbf656501265b2578e4f5f`

Dependency closure is complete: unresolved references 0, historical prompt
dependency false, unversioned sources 0, ambiguous artifact identities 0.

## Safety

Secret reads, DB connections/writes, live slots, Portal HTTP, live Market Data,
FIX logons, orders, Fills, ledger events, Account API, Polygon, Databento,
AWS/S3 configuration mutations, and operational one-shot state are all zero.
The expected terminal blocker remains
`ARCH7B_WORKING_ORDER_AUTHORITY_MISSING`. PR #53 remains draft and is not
authorized for merge by this packet.
