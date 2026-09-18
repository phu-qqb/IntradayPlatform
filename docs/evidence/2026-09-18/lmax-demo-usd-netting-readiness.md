# LMAX Demo: full Anubis currency netting, 18 September 2026

This is the initial dated inspection. See the later [full USD qualification](lmax-demo-full-usd-qualification.md) for completed configuration/deployment and the remaining account-observation gate.

Philippe requested today's full FX session and clarified that the old PMS already
nets pair signals into XXXUSD / USDXXX legs. Reuse that netting. Raw crosses are
signal inputs, not independent execution orders. This supersedes the EURUSD-only
execution scope; it does not waive account reconciliation or genuine observations.

## Existing components reused

- `QubesFxWeightsFixtureIngestionService.ParseNormalizeAndMap`: adds `+weight`
  to base currency and `-weight` to quote currency, then checks zero-sum exposure.
  The Demo adapter consumes its validated exposures; it never persists the fixture
  batch request returned by that pure service.
- `Arch7aPmsShadowExecutionPipeline.BuildNetting`: retained sizing convention:
  direct USD-quote target `w * NAV / mid`; inverted USD-base target `-w * NAV`.
- Existing four-programme ingestion, provenance hashes, `ProcessModelRunService`,
  risk decisions, physical child/FIX persistence and session coordinator.

The recent Demo path filtered signals against enabled execution instruments before
netting and then applied an EURUSD-only mask. The candidate calls the existing
currency netting on **all** programme rows before selecting native USD legs.
It emits an explicit target for every observed execution leg, including zeros;
rejects omitted currencies; preserves coefficients and absence/no-carry-forward
semantics; and uses a distinct hashed batch identity to prevent old unnetted batch
reuse. All original manager files and their hashes remain the source lineage.

USD-base quantities and risk notionals now use USD units. Gross exposure sums
absolute USD values per instrument rather than summing unrelated base quantities
and multiplying by one instrument's price. Existing risk ceilings are unchanged.
This path is available only through the continuing Demo gateway.

## Actual source scope

Retained genuine 17 September files were inspected as **historical evidence only**:
INFX7 and INFX8 each produce 66 pair rows; INFX9 and INFX10 each produce 78.
The 39 previously enabled instrument records are not the full signal universe.
The combined programme currency set requires these 14 native USD legs:

| Native execution symbol | LMAX SecurityID | LocalDB readiness observed 18 September |
|---|---:|---|
| EURUSD | 4001 | Enabled instrument, mapping and risk rule |
| GBPUSD | 4002 | Mapping disabled; risk rule absent |
| AUDUSD | 4007 | Mapping disabled; risk rule absent |
| NZDUSD | 100613 | Mapping disabled; risk rule absent |
| USDJPY | 4004 | Native instrument and mapping disabled; risk rule absent |
| USDCHF | 4010 | Native instrument and mapping disabled; risk rule absent |
| USDCAD | 4013 | Native instrument and mapping disabled; risk rule absent |
| USDHUF | 100501 | Native execution instrument, mapping and risk rule absent |
| USDMXN | 100507 | Native execution instrument, mapping and risk rule absent |
| USDNOK | 100513 | Native execution instrument, mapping and risk rule absent |
| USDPLN | 100523 | Native execution instrument, mapping and risk rule absent |
| USDRON | 100931 | Native execution instrument, mapping and risk rule absent |
| USDSEK | 100529 | Native execution instrument, mapping and risk rule absent |
| USDZAR | 100547 | Native execution instrument, mapping and risk rule absent |

Identity source: existing approved market-data catalog and
`deploy/aws/anubis-shadow/config/arch6a_qubes_security_id_to_lmax_market_instrument_mapping.v1.json`.
The catalog SHA-256 is `c98176f8aeeac8756049d51468e7367658b94ccccb81e08e314b9e0387e1840d`.
These identities do not by themselves qualify execution contract sizes, quantity
steps/minima or tick sizes. The seven additional execution contracts must be
recovered from authentic reference evidence, not inferred from market-data IDs.
The referenced `LMAX-Instruments-operator-20260528.csv` was not found in the bounded
searches of this host's Intraday deployment and `D:\QQFund`.

## Qualification and operational status

The focused `UsdNetting.Tests.csproj` covers netting, omitted currencies, explicit
zero targets, all 14 currency legs, direct/inverted sizing, USD risk valuation,
cross-instrument gross exposure, existing batch processing and the existing FIX
continuity/parser tests. At 11:02:15Z, **47 tests passed, zero failed** and the full
Worker build completed with zero errors. Existing dependency warnings remain.
TRX SHA-256: `1b213605560b1020e17e6f96c09b54fce49bb171f67c9b4cd266ccc8a1e5fdf4`.
Candidate Worker DLL SHA-256: `a4c546088c1625a1b9f82b47d29d7f40b9e1eca9e89721cde38a9bf21307311a`.
The eight qualified code/project files were read back with hashes matching the
published source candidate. The full Worker builds in staging. Qualification uses
synthetic test transports only; it does not establish a fresh broker logon.

At **2026-09-18T10:51:30Z**, process inspection found **zero Worker processes and
zero `start-worker`/`run-day` launcher processes**. No session or broker order was
started by this work. The installed launcher still pins the earlier EURUSD-only
Worker and is not repointed by this patch.

Startup is not ready. Required next work is concrete:

1. Complete and qualify the 14 native execution bindings, report aliases and risk
   bindings from authentic contract evidence; retain existing risk policy.
2. Bind the launcher observation scope and scheduled final exit to those 14 legs,
   qualify genuine per-leg market data, and pin the resulting Worker deployment.
3. Complete audited retirement of the **actual-send** 17 September session using
   the already audited recovery plus an authentic current account observation.
   The original journal and owner lock remain unchanged. No no-send certificate,
   fabricated FIX acknowledgement, journal deletion or forced lock is permissible.
4. Obtain a fresh official observation of account 1754288005, flat and with no
   working orders, younger than 900 seconds; then use documented `start-worker`
   and `run-day` with a fresh session ID and the next naturally eligible cutoff.
5. Extend the daily recap's current EUR/USD-only trade parser and per-symbol
   quantity reporting before claiming full-universe reporting is supported.

The historical economic recovery remains the one documented in #84 comment
5728601941: four authentic executions recovered, old missing-fill breaks resolved,
zero independent historical FIX execution reports. It is not a current flat/no-order
observation. No Production/PMS runtime access, IAM/OS change, email, Databento request,
contract invention or risk-limit override was performed here.
