# M3A Source Gap Report

## Fills

Status: available as a model and normalization path via ExecutionReports, plus recovery evidence contracts. M3A can reconcile fills offline when the evidence is supplied.

Gap for M3B: connect a read-only broker execution source or replay source that provides scoped broker execution events with durable ids, sequence information, timestamps, quantities, and prices.

## Open orders

Status: reconstructable from a proven complete execution session and order-status lifecycle. Not yet proven as an authoritative broker snapshot source.

Gap for M3B: implement/read-only integrate a broker open-order source or prove complete session-state reconstruction across restart. Until then, unknown open-order authority is NO_GO for new order entry.

## Positions

Status: internal position lineage exists. Broker-authoritative runtime position source is missing under current bans. AccountAPI remains forbidden unless lead explicitly changes policy.

Gap for M3B: define and wire a broker position snapshot/report source that is read-only, scoped by environment/account/venue, and can be freshness-checked. If unavailable, readiness remains NO_GO instead of assuming flat.

## M3B adapter interface requirement

A future read-only adapter must provide:

- broker execution events;
- broker position snapshots;
- broker open-order snapshots or a completeness proof for reconstructed state;
- source quality, source hash, as-of time, sequence health, and account scope.

No AccountAPI or Order Entry is introduced by M3A.
