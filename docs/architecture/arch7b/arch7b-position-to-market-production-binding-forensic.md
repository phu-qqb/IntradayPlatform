# ARCH7B position-to-market production binding forensic

## Verdict

`FORTY_NINE_MARKET_SOURCES_PROJECT_TO_NINETY_NINE_PMS_INSTRUMENTS`

The cardinalities describe different layers. The slot finalizer selects 49 real LMAX BBO source pairs. `Arch6aLmaxUsdCrossRateProjector` then resolves every one of the 99 persisted PMS mappings with a direct quote, an inverted quote, or a two-leg USD cross. The economic builder produces 99 market observations and applies four 72-weight model runs, yielding 288 targets and 288 position-only drifts.

## Exact binding

The position and market sessions are not required to have the same session ID. The position side is identified by `SourceIngestionId`, `SourceSessionId`, `PositionSnapshotId`, its 99-line SHA and broker authority. The market side has its own `MarketCaptureSessionId`, exact slot, 49-symbol set, selected-event SHA, qualified clock SHAs and manifest SHA.

They become one economic input only through the explicit revision binding:

- exact slot ID and window;
- raw capture artifact SHA;
- 99-mapping set SHA and cross-rate contract;
- market snapshot ID and 99-observation SHA;
- PMS source ingestion/session;
- account and position snapshot IDs;
- position snapshot `AsOfUtc`;
- model run, Qubes input and output identities;
- revision input, targets, drifts and manifest SHAs.

## Freshness

Current production selection requires one unambiguous snapshot with `AsOfUtc <= SlotStartUtc` and an age of at most 300 seconds. The snapshot must have explicit broker authority, an explicitly observed global-flat state, no inference, 99 unique instruments and exact symbol/security mapping agreement.

## Historical result

The bounded local inventory contains seven distinct complete 49-symbol market slots and four distinct position snapshot IDs. None is `MATCHED_COMPLETE` under the current contract:

- slots from 23-24 July predate the 27 July fresh-position selection contract and do not carry the v2 market selection SHA or a position-binding draft;
- the 24 July 10:30 slot has a real 99/288/288 revision, but its position selection predates the 300-second rule and its evidence has no v1 E-to-F binding;
- the 27 July position snapshot at 11:23:45Z has no market slot within five minutes;
- the 28 July snapshot at 10:53:49Z is position-only;
- the best candidate, snapshot `2a8d16ee-e5bb-89eb-5bc6-eec4409db391` at 13:12:15Z for the 13:15Z slot, has no market capture artifact or economic revision.

No RDS secret read was needed. A read could reveal additional database rows, but it cannot manufacture the missing market manifest or v1 binding evidence.

## Live contract

`arch7b_position_market_slot_lineage_v1` establishes:

1. build a draft from the runtime-selected 99-line snapshot before capture;
2. bind the exact 49-symbol set, mapping set, commits, account, target and capture session;
3. finalize with both clock evidences, selection SHA, market manifest SHA and source timestamp range;
4. reject PMS revision input unless slot, ingestion, session and snapshot match;
5. reject ARCH7A unless it consumes the exact bound revision.

The contract is no-order and contains no secret or connection string. Its 17 synthetic tests qualify only fail-closed identity behavior; they are not economic market evidence.
