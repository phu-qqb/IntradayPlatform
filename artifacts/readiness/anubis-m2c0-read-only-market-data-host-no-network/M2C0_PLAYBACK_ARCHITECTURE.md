# M2C0 Playback Architecture

M2C0 uses local synthetic playback only:

`	ext
fixture EURUSD/AUDUSD observations
-> PlaybackReadOnlyMarketDataSource
-> ReadOnlyMarketDataFeedStateMachine
-> Canonical recorder events
-> ReplaySnapshotAsync
-> BuildParity independent hashes
`

Fixture coverage:

- EURUSD and AUDUSD
- bid/ask prices and sizes
- contiguous source sequences
- PossDup duplicate evidence
- gap detection
- stale quote detection
- invalid book fail-closed
- recovery transition
- multiple fast updates

No socket, endpoint, FIX logon, AccountAPI, Databento API or DB apply is used.
