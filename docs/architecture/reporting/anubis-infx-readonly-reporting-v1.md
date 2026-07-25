# Anubis / INFX read-only operational reporting v2

## Purpose

`QQ.Production.Intraday.Tools.OperationalReporting` projects existing
`pms_shadow` append-only facts into a deterministic operator bundle. It does
not persist reporting state and is not a second business authority.

Authority order:

1. PostgreSQL append-only facts.
2. Content-addressed evidence referenced by those facts.
3. Versioned status and blocker codes.
4. In-memory reporting projections.
5. JSON, CSV and local HTML presentation.

Unknown, absent and stale values remain `INCONNU`, `ABSENT` and `OBSOLÈTE`.
They are never converted to zero, flat, fresh or successful states.

## Safety

The CLI accepts only the `ARCH7B_RDS_TEST` profile, database
`qq_pms_shadow_arch7b_test`, schema `pms_shadow`, PostgreSQL major 18 and the
expected target fingerprint. Remote targets require TLS `VerifyFull`.

Every connection includes `default_transaction_read_only=on`. The reader
opens a `REPEATABLE READ` transaction, executes `SET TRANSACTION READ ONLY`,
and requires `SHOW transaction_read_only` to return `on` before reading.
The CLI rejects pending EF model changes. It never invokes `SaveChanges`,
migrations, LMAX, Polygon, FIX or order code.

## Calendar And Schedule

Slot freshness reuses `PmsShadowIntradayCadenceContract`, including
`IsOperational`. Saturday and Sunday are
`OUTSIDE_OPERATIONAL_CALENDAR`: an old Friday slot is not falsely reported
stale while FX markets are closed. A persisted `MISSED` fact remains active
until an authoritative later fact resolves it.

The typed operational expectation reports the latest expected closed slot,
the next expected slot and one of:

`CURRENT`, `DUE`, `NOT_DUE`, `OUTSIDE_OPERATIONAL_CALENDAR`, `MISSED`,
`STALE_AFTER_DUE_TIME`, or `UNKNOWN`.

INFX model reporting is sourced exclusively from `SelectedModelRuns` in the
latest qualifying economic revision. The exact selected set is INFX7 through
INFX10. Schedule status distinguishes fresh selection, scheduled reuse,
not-due, due-missing and stale-after-due. A selected finalized model reused
according to its daily schedule is not stale.

Per-model completeness is contractual:

| Strategy | Weights | Targets | Drifts |
|---|---:|---:|---:|
| INFX7 | 66 | 66 | 66 |
| INFX8 | 66 | 66 | 66 |
| INFX9 | 78 | 78 | 78 |
| INFX10 | 78 | 78 | 78 |

The global total of 288 cannot hide a per-model mismatch.

## Manifest And Ready Marker

The manifest reader supports legacy fields and the exact PR38 typed
interface. A legacy omission remains absent or unknown. A future typed
manifest is proven only when its artifact and selection SHA-256 values,
49-symbol coverage, event counts, post-close exclusions, clock snapshots,
clock source, offset, uncertainty and cutoff controls are complete and valid.

Ready-marker evidence is projected separately. It is never inferred from the
slot manifest. When no database or evidence source exists, its state is
`ABSENT` or `INCONNU`.

## Provenance And Break State

Observed codes retain their exact source code, fact kind, source component
and table, scope identities, timestamps, SHA evidence and blocking flag.
`ReasonCodesJson` is evidence but does not automatically create a break.
`BlockingBreaksJson` and reconciliation breaks may create scoped breaks.
An unknown source code maps to `REPORTING_UNCATALOGUED_SOURCE_CODE` while
preserving the original `SourceExactCode`.

Read-only break states are `ACTIVE`, `HISTORICAL`,
`RESOLVED_BY_LATER_FACT` and `UNKNOWN`. Historical and resolved facts do not
affect active counts or readiness. A later fact resolves an earlier fact only
when the append-only source explicitly provides that authority.


## ARCH7A Temporal Authority

An ARCH7A risk fact is joined read-only to its TradeIntent, economic revision
and completed qualification run. `SourceStatus` remains the raw persisted risk
outcome; it is never interpreted as temporal authority. The separate
`DerivedOperationalStatus` is `ACTIVE` only for the latest qualifying economic
revision selected by `CompletedAtUtc` and its authoritative completed ARCH7A
qualification selected by `CompletedAtUtc` with matching `PlanSha256`.

Older qualifying revisions are `HISTORICAL`. A missing TradeIntent, missing
economic revision, missing matching qualification, or exact timestamp tie
between candidate qualifications is fail-closed `UNKNOWN`; GUID ordering never
breaks an authority tie. `observed-code-facts.csv` retains the revision time,
latest-revision flag, qualification-run identity and authority flag.
`operational-summary.json` exposes the latest economic revision ID separately
from the true latest ARCH7A `QualificationRunId`, completion time and status.
`BreakId` is a lowercase SHA-256 of the versioned canonical identity fields.
No random GUID participates in break identity.

## FX Views

`fx-net-lines.csv` contains exactly seven persisted ARCH7A net execution
facts. `CurrentQuantity`, `TargetQuantity` and `SignedDesiredDelta` occur once
per pair. PMS security IDs and LMAX instrument IDs remain distinct; GBPUSD is
PMS `68`, LMAX `4002`, source `8`.

`fx-strategy-contributions.csv` contains exactly 28 derived attribution
rows. It retains source TargetPosition IDs and counts and uses
`PROPORTIONAL_NET_ATTRIBUTION_V1`. Derived allocated execution quantities are
`PROBABLE`; unprovable values remain `INCONNU`/`NULL`. Allocations sum exactly
to each persisted net target and never duplicate current account quantity.

## Determinism

Rows and files use stable ordinal ordering. JSON uses a fixed snake-case
contract. CSV is UTF-8 without BOM, uses invariant decimals, UTC ISO-8601
timestamps and the literal `NULL`. The HTML has no script, CDN or external
resource.

For a fixed database snapshot, repository commit and `--as-of-utc`, two runs
produce identical bytes, file SHA-256 values, BreakIds and bundle SHA-256.

## Outputs

- `operational-summary.json`
- `operational-calendar.json`
- `breaks.json`
- `breaks.csv`
- `status-code-catalog.json`
- `source-code-inventory.json`
- `observed-code-facts.csv`
- `infx-model-runs.csv`
- `slots.csv`
- `ready-markers.csv`
- `economic-revisions.csv`
- `fx-net-lines.csv`
- `fx-strategy-contributions.csv`
- `arch7a.csv`
- `arch7b-lifecycle.csv`
- `reconciliation.json`
- `report.html`
- `manifest.json`

The ARCH7B tables may be empty. In that state lifecycle and reconciliation
are `ABSENT`, never successful or flat. A registered lifecycle without final
reconciliation is blocking `UNKNOWN`; proven non-flat state is critical.

## CLI

```powershell
dotnet run --project tools/QQ.Production.Intraday.Tools.OperationalReporting -- `
  report-operational-state `
  --connection-secret-reference env:QQ_ARCH7B_REPORTING_PASSWORD `
  --host <RDS endpoint> `
  --port 5432 `
  --connect-host 127.0.0.1 `
  --connect-port 15432 `
  --root-certificate <AWS RDS CA bundle> `
  --include-history 64 `
  --expected-environment TEST `
  --expected-database qq_pms_shadow_arch7b_test `
  --expected-schema pms_shadow `
  --expected-postgresql-major 18 `
  --role qq_arch6d_pms_reporting `
  --target-profile ARCH7B_RDS_TEST `
  --expected-target-fingerprint 72fa569ee28e4dec6272db0d69c7594b2be8853e9607dff3e78066378a0b5ee4 `
  --repository-commit <40-or-64-character-git-commit> `
  --as-of-utc <UTC timestamp> `
  --output-directory <empty directory> `
  --no-order
```

The secret value is read only from the named environment variable. Neither
the reference nor the value is written to the reporting bundle.
