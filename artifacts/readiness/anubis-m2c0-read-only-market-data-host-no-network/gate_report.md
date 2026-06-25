# M2C0 Gate Report

Final gate: GO_M2C1_OPERATOR_READ_ONLY_MARKET_DATA_CAPTURE

Checks:

- Provenance resolved: PASS (551dd0bae4ff1133f51eb8580ed9062797791c03)
- M2B zip hash verified: PASS (5c25de6dfec5b58c89d75ff52ea4b5a7279c73ad196ff2c99dfb690aa9291dbf)
- M2B semantic closeout: PASS
- Recorder closeout: PASS
- Read-only host structural safety: PASS
- Playback/replay/parity deterministic: PASS
- Targeted M2A/M2B/M2C0 tests: PASS 103/103
- Build: PASS
- No live run: PASS
- No FIX logon: PASS
- No socket external: PASS
- No order/cancel/replace: PASS
- No AccountAPI: PASS
- No Databento API/download: PASS
- No DB apply/write: PASS
- No R009/R018/R216 run: PASS
- No push/merge: PASS at artifact generation time

Non-gate note: full unit suite failed because historical artifact fixtures unrelated to M2C0 are absent in the isolated worktree. See validation_commands.log.
