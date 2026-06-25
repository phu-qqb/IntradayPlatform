# M2C0 Read-Only Dependency Graph

`mermaid
graph TD
  A[Local playback fixture] --> B[IReadOnlyMarketDataSource]
  B --> C[ReadOnlyMarketDataFeedStateMachine]
  C --> D[CanonicalDomainEventMapper MapMarketData]
  C --> E[MapSizingMarketData]
  C --> F[MapExecutionBbo]
  D --> G[CanonicalRecorderV2]
  E --> G
  F --> G
  H[CanonicalShadowOffline MapIntraday] --> G
  G --> I[Final manifest and chunks]
  I --> J[ReplaySnapshotAsync]
  J --> K[BuildParity source vs recorded vs replayed]
`

Forbidden edges for M2C0: no LMAX endpoint, no FIX logon, no socket, no AccountAPI, no Databento, no DB apply, no order/cancel/replace path.
