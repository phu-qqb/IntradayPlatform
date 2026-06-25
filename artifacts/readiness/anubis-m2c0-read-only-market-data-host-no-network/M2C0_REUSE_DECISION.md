# M2C0 Reuse Decision

Decision: reuse existing CanonicalRecorderV2 and CanonicalShadowOffline contracts; create only a narrow local read-only market-data host abstraction for M2C0.

Rationale:

- Existing LMAX read-only components are useful evidence, but several include TCP/FIX/logon/credential or lab surfaces. M2C0 forbids external sockets and any endpoint access.
- Creating the narrow IReadOnlyMarketDataSource avoids importing order-capable transitive dependencies.
- Existing recorder and shadow mappings are reused and completed rather than duplicated.
- M2C1 can later bind a real read-only source only after operator/lead approval and with this same narrow contract.
