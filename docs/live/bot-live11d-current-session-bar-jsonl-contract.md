# BOT-LIVE11D Current-Session Strategy Bar JSONL Contract

BOT-LIVE11D defines the bot-side contract for a future NinjaTrader 8 local export. It does not create bars and does not touch NT8 runtime.

## Input Path

The only expected real input path is:

`fixtures/incoming/current-session-bars/nt8_mnq_current_session_bars.jsonl`

No directory scan, globbing, watcher, polling loop, scheduler, service, network connection, HTTP/WebSocket, broker API, Tradovate API, credential access, or market data subscription is part of this phase.

## Why JSONL

JSONL is used because NT8 can append or write one completed bar per line without requiring a file-level header. The bot validates consistency by deriving file-level facts from records: instrument, session date, tick size, bar period type/value, timestamp order, and OHLC/tick validity.

## Required Record Fields

Each line must be one JSON object with:

- `phase` or `exportSchemaVersion`
- `sourceSystem = nt8_local_bar_export`
- `artifactType = current_session_strategy_bar`
- `monitoringOnly = true`
- `nonExecutable = true`
- `instrument`
- `instrumentFullName` if available
- `masterInstrumentName` if available
- `tickSize`
- `sessionDate`
- `sessionTimezone`
- `barPeriodType`
- `barPeriodValue`
- `timestampUtc`
- `open`
- `high`
- `low`
- `close`
- `volume` if available, nullable if not
- `isHistorical`
- `isRealtime`
- `barsInProgress` if available
- `tradingHoursName` if available
- `calculateMode` if available

## Bot Validation

The bot rejects files unless:

- all records have the same instrument
- all records have the same sessionDate
- all records have the same tickSize
- all records have the same barPeriodType and barPeriodValue
- timestamps are UTC and strictly increasing
- OHLC is internally valid
- prices align to tickSize where possible
- volume is not negative when present
- records are explicit local strategy bars
- records contain no signal, order, execution, broker, credential, network, watcher, polling, scheduler, position sizing, quantity, or trading-intent fields

## Non-Acceptable Inputs

EXPORT12 observation snapshots are not accepted because they are monitoring observations, not strategy bars for `StrategyLevelPackService`.

LIVE10A monitoring targets are not accepted because they are target rehearsal objects, not strategy-derived bar input.

LIVE11B synthetic plumbing bars are not accepted as `real_local_current_session_bars`. The only accepted synthetic-like object in BOT-LIVE11D is the parser contract sample labelled `parser_contract_sample_not_market_real`, and that sample is explicitly not real market data.

## Non-Executable Boundary

This phase is contract and parser only. It does not produce a level pack, current-session theoretical target, candidate, signal, order, execution intent, position size, quantity, or trading readiness.

## Next Phase

Recommended next phase: BOT-LIVE11E - Implement NT8 current-session strategy bar exporter, disabled by default, local JSONL only, non-executable.
