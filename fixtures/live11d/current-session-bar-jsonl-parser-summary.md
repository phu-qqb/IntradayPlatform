# BOT-LIVE11D Current-Session Bar JSONL Parser Summary

Decision: `PASS_CURRENT_SESSION_BAR_JSONL_CONTRACT_AND_PARSER_READY_NON_EXECUTABLE`

BOT-LIVE11D adds a bot-side JSONL contract and parser/validator for the explicit local file:

`fixtures/incoming/current-session-bars/nt8_mnq_current_session_bars.jsonl`

The parser is deterministic and explicit-file-only. It does not discover files, watch directories, poll, connect to NT8, connect to a broker, access credentials, subscribe to market data, produce a level pack, produce a theoretical target, produce a candidate, generate a signal, submit an order, or create trading readiness.

The valid sample fixture is labelled `parser_contract_sample_not_market_real`; it proves parser shape only and is not accepted as real market data.

Rejection coverage includes missing required fields, inconsistent file-level metadata, non-increasing timestamps, invalid OHLC, negative volume, unsafe execution/signal fields, EXPORT12 observation snapshots, LIVE10A monitoring targets, and LIVE11B synthetic plumbing bars as real bars.

Build result: PASS.

Test result: PASS.

Next required phase: BOT-LIVE11E - Implement NT8 current-session strategy bar exporter, disabled by default, local JSONL only, non-executable.
