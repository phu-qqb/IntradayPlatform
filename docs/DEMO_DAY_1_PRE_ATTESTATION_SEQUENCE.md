# Demo Day 1 pre-attestation sequence

This is a concise, decision-time procedure for the four-programme Intraday FX
portfolio. It deliberately ends before any venue-send action.

## 1. Establish the decision-time programme state

For the requested UTC decision timestamp, evaluate the programmes independently:

| Programme | Universe / model | Session | Timeframe | Coefficient |
| --- | --- | --- | --- | --- |
| INFX7 | 54 / 10 | US | 15 minutes | 4.5 |
| INFX8 | 57 / 11 | US | 30 minutes | 2.1 |
| INFX9 | 58 / 12 | EU | 15 minutes | 1.4 |
| INFX10 | 59 / 13 | EU | 60 minutes | 0.6 |

Run only the genuine V1 chain due for each programme. For every programme record
exactly one state:

- `Present`: manager-owned weight is genuine for this decision and its lineage
  files and hashes verify;
- `Absent`: there is no applicable decision or the genuine output has no weight;
  include an explicit reason and contribute zero; or
- `Failed`: an expected programme could not determine its output. Stop.

Never carry a previous weight forward. The manager aggregator uses all four
recorded states, sums only `Present` manager-owned weights, applies no additional
normalisation, and must block a `Failed` state.

## 2. Capture and verify current market data

Immediately before the same decision:

1. Obtain a bounded, read-only LMAX Demo market-data capture through the approved
   operator process.
2. Finalise and replay the canonical capture. Its manifest hash must be recorded
   as the decision lineage.
3. Confirm a valid, non-crossed BBO at or before the decision timestamp for every
   enabled LMAX execution instrument, within the configured maximum source age.
   Missing, stale, malformed, or hash-mismatched market data stops the run.

Massive data may support history through J-1 only. It is not a substitute for the
same-day LMAX capture in this step.

## 3. Run the bounded Worker handoff in safe mode

Use the LocalDB-owning interactive operator context. Configure the Worker for the
single decision with:

- the finalized canonical-capture run root and its expected manifest hash;
- the decision timestamp and a positive maximum source age;
- the four programme states from step 1;
- the existing fake execution gateway and external/live execution disabled.

The Worker must first persist only the verified canonical BBOs, then create the
genuine manager-owned `ModelWeightBatch` and `ModelRun`. Confirm that:

- all four programmes are represented as `Present`, `Absent`, or `Failed`;
- zero contributions are only explicit `Absent` states;
- the current snapshots satisfy the Worker freshness gate; and
- the Worker reports no block or programme failure.

## 4. Stop for Philippe's fresh Demo UI attestation

Before any later venue-send step, Philippe must freshly attest the official LMAX
Demo UI for the current decision. Do not rely on an earlier attestation, replay a
stale target, or submit an order from this sequence.
