# Controlled LMAX Demo day launcher

This operator tool joins the existing collector, fixed Anubis V1 SSM document,
static ExecDesk writer and continuing Worker. It does not change target weights,
NAV, execution masks, quantity conventions or risk limits. Default command: `plan`.

## Modes and effects

| Command | Effects |
|---|---|
| `inspect-reference` | Read-only verification of all 14 native execution contracts, report aliases and instrument risk bindings. |
| `plan` | Prints the next eligible same-day cutoff; no external calls. |
| `self-test` | Simulated contract and boundary checks; local temporary files only. |
| `normalize --capture-root ROOT --cutoff UTC --output DIR` | Validates retained canonical capture and writes normalized files locally. |
| `verify-runtime` | Checks pinned files, current instance role, exact GPU/document and SSM status access. |
| `verify-capture-credentials` | Verifies role and the four market-data secret bindings in memory; prints only a success marker. No FIX or database call. |
| `inspect-worker` | Reads the existing Demo secret into child-process memory and invokes the pinned Worker's inspection-only early return. No broker or database access. |
| `prepare-cycle --cutoff UTC` | Captures genuine Demo market data, uploads two immutable S3 inputs, dispatches the fixed Anubis document, downloads current outputs and invokes the existing ExecDesk adapter. Writes a prepared manifest without sending it to the Worker. |
| `start-worker --observation FILE` | Starts one continuing Demo Worker from a genuine fresh observation; retains output supervision until the Worker exits. Does not schedule cycles. |
| `run-day --session-id ID` | Requires an existing account owner and current FIX reception; prepares and atomically delivers fresh cycles through the final scheduled reduction. |

`prepare-cycle`, `start-worker` and `run-day` have external effects and must not
be described as dry runs. The owner explicitly approved the full Demo chain on
2026-09-17 after the earlier automatic-review blocks. That approval does not
establish successful end-to-end execution; retain actual stage receipts.

## Fixed boundaries

- Demo host `EC2AMAZ-1QPHTD8`, Administrator, account `1754288005`.
- Existing role `qq-role-ec2-intraday` on `i-05626133ca7892fb8`.
- Anubis instance `i-019ec3c94b9d234f6`, eu-west-2, must already be running.
- Document `QQ-LMAX-Demo-Anubis-Cycle-v1`, version 1, pinned content hash.
- S3 transport uses only the established per-cycle input and result paths.
- Existing collector, catalog, template, ExecDesk binary and mapping are hashed.
- All programme signals enter the existing currency netting before selecting the
  14 native XXXUSD/USDXXX legs. EURUSD-only fallback and raw-cross execution are rejected.
  Contract facts are bound in `LmaxDemoUsdExecutionUniverse` to the official LD4
  reference CSV SHA-256. Quantity step is QQ's conservative one-minimum-lot quantum.
  The owner removed the separate Demo caps; ordinary risk controls remain active.
  NAV USD 1000000, PortfolioBaseCurrencyNotional, USD-base sizing uses USD units.
- Full Worker dependency closure is hash-pinned, including corrected FIX parsing.
  Preflight queries each native leg through the real market-data-only adapter.
- Existing approved Demo TLS/revocation and post-logon sender continuity are
  retained. Credentials never enter source, files, logs or command arguments.
- No IAM administration, generic SSM document, Production, PMS, Databento or
  alternate data-provider route is implemented.

## Start and recovery

Build with the installed SDK 10.0.400. Run `self-test`, `inspect-reference`, `verify-runtime`,
`verify-capture-credentials` and `inspect-worker` before initial activation.
The owner-approved regional status-read amendment was applied separately by the
existing IAM operator on 2026-09-17; `verify-runtime` then passed. Runtime code
still performs no IAM administration.

AWS child output is explicitly UTF-8. The market-data JSON parser accepts a
single leading native BOM, without changing the secret or accepting arbitrary
prefixes. The launcher uses an exclusive file lease that survives async thread
changes and is released on process exit. This does not replace the Worker's
separate account ownership lock. The blocked 09:15 UTC attempt is retained;
recovery must use a new admissible cutoff and the existing continuing owner.

The starting observation must be produced from the official LMAX UI, show the
correct account flat with no working orders, declare exclusive order activity,
and be no older than 900 seconds. The launcher does not create or refresh that
observation. The fixed session journal prevents restarting an unresolved owner.
The observed prior fake Worker must be reconciled and stopped separately before
`start-worker`; this tool does not terminate any existing process.

Run `start-worker` under a supervised process, then `run-day` after genuine FIX
logon. A session beginning intraday starts at the next admissible cutoff; there
is no replay of a qualification cycle. The collector starts two minutes before
cutoff. Every stage retains the original cutoff and its next-M15-close deadline.
Off-cadence programme contributions are absent, with no prior-result carry.

On any ambiguous dispatch, missing data, stale result, failed previous cycle or
lost account owner, planning stops. Do not retry that cycle or delete its evidence.
The Worker and FIX receiver remain running for reconciliation. A genuine final
UI observation is still required; a report download is not a flatness attestation.
The existing report acquisition path is unchanged.

The 17 September actual-send incident has a separate `retire-session` command in
the recovery tool. It requires verified recovered accounting/audit, the exact
unchanged journal and a retained authentic official UI observation under 900 seconds.
`inspect-retirement` only checks the database; it cannot issue that certificate.
The certificate preserves the failed journal and does not prove a later account state.

## Qualification limits

The checked-in receipt distinguishes source/build/simulated/historical checks
from real external execution. Passing those checks does not establish S3/SSM
round-trip operation, broker acceptance, fills, TCA or a completed Demo day.

## Dated owner confirmation — 18 September 2026 only

Philippe supplied current flat/no-working-orders confirmation and requested today's
start in #84 comment 5730268654. The explicit `OWNER_CONFIRMED_ACCOUNT_STATE` source
binds that exact quote, account, reference and fixed declaration recording time
12:52:03.0232487Z to the authentic retained opening report receipt. It is not an
automated UI/broker observation. Existing UI evidence remains a separate path.

The recording time is never refreshed: the same 900-second age check applies at
retirement and startup. Report dates stay unchanged. Fresh recovery/readback,
exclusive ownership, exact journal hash, no internal orders/positions/breaks and
all normal trading controls remain mandatory. This dated owner source is not a
new daily unattended attestation or a generic way to manufacture broker evidence.
The startup journal retains observation source, evidence path and SHA256.
