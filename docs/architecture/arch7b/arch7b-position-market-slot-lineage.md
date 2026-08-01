# ARCH7B position-market-slot lineage

`arch7b_position_market_slot_lineage_v1` is the mandatory E-to-F identity contract for the next operational run. It does not contain market prices and is not a historical economic fixture.

## Required order

1. Import the bracketed 99-line position snapshot append-only.
2. Select that exact snapshot for the planned slot.
3. Build the binding draft before Market Data is armed.
4. Bind account, target profile, Core and Intraday commits, source ingestion/session, position IDs and SHAs, the 99-mapping set, the exact slot, capture session and 49-symbol set.
5. Arm the market-data-only capture with the draft evidence SHA.
6. Finalize the 49/49 selection with both clock evidences, source timestamp range, selection SHA and market manifest SHA.
7. Reject PMS economic refresh unless the final binding matches the selected source and slot byte-for-byte.
8. Bind the resulting projection revision ID, input SHA and manifest SHA.
9. Reject ARCH7A unless it consumes that exact projection revision.

## Fail-closed blockers

- `ARCH7B_POSITION_MARKET_SLOT_BINDING_REQUIRED`
- `ARCH7B_POSITION_MARKET_POSITION_SNAPSHOT_MISMATCH`
- `ARCH7B_POSITION_MARKET_SOURCE_INGESTION_MISMATCH`
- `ARCH7B_POSITION_MARKET_REQUIRED_UNIVERSE_MISMATCH`
- `ARCH7B_POSITION_MARKET_SLOT_MISMATCH`
- `ARCH7B_POSITION_MARKET_MAPPING_AUTHORITY_MISMATCH`
- `ARCH7B_POSITION_MARKET_MANIFEST_BINDING_MISMATCH`
- `ARCH7B_POSITION_MARKET_REVISION_INPUT_MISMATCH`

## Cardinality authority

- position lines: exactly 99;
- persisted security mappings: exactly 99;
- selected LMAX BBO sources: exactly 49;
- projected observations: exactly 99;
- targets and drifts: exactly 288/288 for four 72-weight model runs.

The 49→99 edge is governed by the versioned mapping-set SHA and `arch6a_lmax_usd_cross_rate_projection_v1`. No numeric cardinality is treated as an identity.

## Temporal authority

The selected snapshot must be unique, not future-dated, and no more than 300000 milliseconds old at slot start. Every selected market source timestamp must be inside the slot. Late receipt is governed separately by the qualified capture clocks.

## Regenerate immediately before live

The following values must never be reused from a prior run:

- run and owner IDs;
- selected slot;
- Core prequalification and exact commits;
- position snapshot ID, line-set SHA and age;
- required PMS universe and mapping-set SHAs;
- market capture session and required-symbol-set SHA;
- clock evidences;
- selection and manifest SHAs;
- armed state, lease and lease marker;
- secret reads and LMAX session proof.

## Safety

The contract requires `NoOrder=true`. Contract construction and validation do not connect to PostgreSQL, LMAX, FIX, AWS, S3, Polygon, Databento or Account API. They create no Fill or PositionLedgerEvent.

The synthetic tests cover the 17 required positive/negative identity cases. Passing those tests qualifies the contract implementation only. A successful E-to-F economic lineage still requires one operational market session.
