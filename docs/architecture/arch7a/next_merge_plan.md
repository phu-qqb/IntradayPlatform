# ARCH7A next merge plan

1. Finish offline regressions and Release build on the isolated Intraday branch.
2. Commit and publish one draft PR only after the branch-specific GitHub export authorization is confirmed.
3. Do not merge, apply the PostgreSQL migration, or run a live FIX session while ARCH6F is active.
4. After the ARCH6F Final Gate, rebase onto the post-ARCH6F master and replay all ARCH6C/D/E/F and ARCH7A tests.
5. Request a coordinated merge and PostgreSQL shadow qualification authorization. Any later FIX qualification is status-only, Demo, bounded, and separately explicit.
