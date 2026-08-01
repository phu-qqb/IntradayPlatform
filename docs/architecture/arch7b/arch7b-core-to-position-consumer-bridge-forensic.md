# ARCH7B Core-to-position consumer bridge forensic

## Verdict

`COMPATIBLE`

The Core qualification fixture uses the production bracket capture, contract finalizer, and fast-seal writer. The Intraday bridge then invokes the production package reader, required-universe builder, global-flat snapshot builder, package reader, import planner, and runtime selector. No economic field is rewritten between the Core output and the reader.

## Core authority

- Base: `1385705a3aa4ef4d5ac22f1c89bf49da36e070f1`, tree `210ad9933abc2d9b0e6ad202253212996136ba75`.
- Contract: `lmax_portal_bracketed_current_position_snapshot_v2`.
- Account/environment: `1754288005` / `LMAX_LONDON_DEMO`.
- Broker sequence: T0 `08:00:00Z`, P1 `08:00:01Z`, T1 `08:00:02Z`, P2 `08:00:03Z`, T2 `08:00:04Z` on 2026-07-28.
- Executions/positions: `0/0`; both semantic SHAs are `4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945`.
- Source session ID is not a Core contract field. It is not inferred or substituted.

## Consumer authority

The historical universe is extracted from the existing ARCH6C and ARCH7B unit fixtures under contract `arch7b_historical_pms_required_universe_fixture_v1`. Its source session is `arch6b-daily-tier1-20260721T130346Z-422530a8`, source ingestion is `6cf3cd6d-8e63-a3b5-1a0c-45fdec54e1a2`, and required-universe SHA is `f538619ed5c5f49155aa23ab240f26c9cbf4dbe10352cd7ba68aea7cd783d5b4`.

The four model runs preserve counts `66/66/78/78`; all 99 mappings are inherited from the versioned fixture. The Core P2 remains unchanged and is later than the historical PMS source, so the global-flat projection is temporally admissible as a qualification-only historical projection.

## Integrated result

The base-bound process produced `ARCH7B_CORE_TO_POSITION_CONSUMER_OFFLINE_BRIDGE_QUALIFIED`: `99` normalized, `99` derived zero, `0` unknown, import plan `+1/+99`, and runtime-selected snapshot `c5f4d0d9-1912-c4ef-c8c9-61d0401caef7`.

Bridge evidence SHA: `5ba1232c93c40aed7b2bb10e22b9e0e2a3bba687a6f80cb954d37bda601fb7c1`.

## Safety

The fixture and bridge perform zero secret reads, zero database connections, zero network or LMAX calls, zero FIX logons, and zero orders.
