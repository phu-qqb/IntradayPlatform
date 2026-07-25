# Anubis / INFX read-only operational reporting v1

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

Unknown, absent and stale values remain `INCONNU`, `ABSENT` and `OBSOLETE`.
They are never converted to zero, flat, fresh or successful states.

## Safety

The CLI accepts only the `ARCH7B_RDS_TEST` profile, database
`qq_pms_shadow_arch7b_test`, schema `pms_shadow`, PostgreSQL major 18 and the
expected target fingerprint. Remote targets require TLS `VerifyFull`.

Every connection includes:

```text
options=-c default_transaction_read_only=on
```

The reader also opens a `REPEATABLE READ` transaction, executes
`SET TRANSACTION READ ONLY`, and requires:

```sql
SHOW transaction_read_only;
```

to return `on` before reading facts. The CLI rejects pending EF model changes.
It never invokes `SaveChanges`, migrations, LMAX, Polygon, FIX or order code.

## Break identity

`BreakId` is the lowercase SHA-256 of canonical newline-delimited fields:

```text
contract
exact code
component
scope type
scope ID
slot ID
economic revision ID
trade intent ID
qualification run ID
order ID
evidence SHA-256
```

No random GUID participates in break identity.

Read-only break states are `ACTIVE`, `HISTORICAL`,
`RESOLVED_BY_LATER_FACT` and `UNKNOWN`. There is no mutable acknowledgement
workflow and no `MANUALLY_RESOLVED` state.

## Determinism

Rows and files use stable ordinal ordering. JSON uses a fixed snake-case
contract. CSV is UTF-8 without BOM, uses invariant decimals, UTC ISO-8601
timestamps and the literal `NULL`. The HTML has no script, CDN or external
resource.

For a fixed database snapshot, repository commit and `--as-of-utc`, two runs
produce identical bytes, file SHA-256 values, BreakIds and bundle SHA-256.

The bundle SHA is calculated over sorted:

```text
relative path<TAB>size<TAB>file SHA-256
```

for every payload file before `manifest.json`.

## Outputs

- `operational-summary.json`
- `breaks.json`
- `breaks.csv`
- `status-code-catalog.json`
- `infx-model-runs.csv`
- `slots.csv`
- `economic-revisions.csv`
- `fx-lines.csv`
- `arch7a.csv`
- `arch7b-lifecycle.csv`
- `reconciliation.json`
- `report.html`
- `manifest.json`

The seven ARCH7B tables may be empty. In that state the lifecycle and
reconciliation projections are `ABSENT`, never successful or flat.

The FX projection uses persisted ARCH7A TradeIntents as the authority for the
seven execution symbols and joins their LMAX SecurityId to the PMS
`LmaxInstrumentId`. Per-INFX target quantities are allocated from the
persisted TargetPosition contribution IDs and sum exactly to each intent's
target quantity. `CurrentBaseQuantity` remains the line-level persisted
authority. When it is non-zero, no per-INFX current-position allocation is
invented and per-INFX `PositionOnlyDrift` is `NULL`; line-level `NetDrift`
remains available.

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

`--include-history` bounds slot and economic-revision history to 1 through
1000 slots and defaults to 64. `--connect-host` and `--connect-port` are
optional transport endpoints for an SSM tunnel. Target validation and the
fingerprint always use `--host` and `--port`; TLS `TargetHost` also remains
the logical RDS DNS name, so `VerifyFull` certificate validation is preserved
through the tunnel.
