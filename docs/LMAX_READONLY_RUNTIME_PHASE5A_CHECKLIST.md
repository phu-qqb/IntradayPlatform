# LMAX Read-Only Runtime - Phase 5A Checklist

| Gate | Required status | Evidence/check | Pass/Fail |
| --- | --- | --- | --- |
| Phase 4P | Closed | `scripts/run-lmax-readonly-runtime-no-socket-release-gate.ps1` passes |  |
| No-socket release gate | PASS or accepted warning state | Gate report under `artifacts/readiness/` |  |
| Operational readiness | PASS or accepted warning state | `scripts/run-operational-readiness-gate.ps1 -SkipBuild -SkipFrontend` |  |
| Health | FakeLmax-only when API is available | `/health` reports `FakeLmaxGateway`, live/external false |  |
| Runtime defaults | Disabled/design-only | `appsettings.json` safe defaults |  |
| Generated evidence | Not tracked | Git status for generated evidence paths |  |
| Forbidden fields | Absent | No host/user/password/port/account/session/endpoint request DTO fields |  |
| Forbidden implementation surface | Absent | No socket/network/order implementation text in runtime Phase 4/5A files |  |
| Gateway registration | Fake only | API/Worker register `FakeLmaxGateway` only |  |
| Hosted service | None | No LMAX hosted service registration |  |
| Kill/rollback plan | Reviewed | `LMAX_READONLY_RUNTIME_FIRST_TRANSPORT_PREFLIGHT.md` section 7 |  |
| Abort conditions | Reviewed | `LMAX_READONLY_RUNTIME_FIRST_TRANSPORT_PREFLIGHT.md` section 8 |  |
| Phase 5B prompt | Required | Separate explicit prompt before any socket-boundary work |  |

## Phase 5B Result

Phase 5B added the dedicated manual prototype boundary and script. Phase 5C added credential availability/redaction. Phase 5D adds the first isolated manual Demo EURUSD market-data socket prototype behind those gates. Phase 5E adds failure/retry hardening. Phase 5F adds sanitized result capture for an operator-approved manual Demo snapshot attempt. Phase 5G adds sanitized timeout/reject diagnostics after the first run logged on but received no snapshot. Phase 5H adds MarketDataRequest compatibility profiles and blocks known rejected shapes by default. Phase 5J adds sanitized MarketData logon/session diagnostics and profile-label comparison after observed Logout before logon confirmation. Phase 5L validates the first successful sanitized snapshot artifact and closes that milestone without adding runtime behavior. Use `scripts/check-lmax-readonly-runtime-phase5b-prototype-gate.ps1`, `scripts/check-lmax-readonly-runtime-phase5c-credential-gate.ps1`, `scripts/check-lmax-readonly-runtime-phase5d-demo-snapshot-gate.ps1`, `scripts/check-lmax-readonly-runtime-phase5e-failure-hardening-gate.ps1`, `scripts/check-lmax-readonly-runtime-phase5f-manual-snapshot-gate.ps1`, `scripts/check-lmax-readonly-runtime-phase5g-snapshot-diagnostics-gate.ps1`, `scripts/check-lmax-readonly-runtime-phase5h-marketdata-compatibility-gate.ps1`, `scripts/check-lmax-readonly-runtime-phase5j-logon-diagnostics-gate.ps1`, and `scripts/check-lmax-readonly-runtime-phase5l-successful-snapshot-closure-gate.ps1` to verify the prototype stays isolated from API/Worker gateway registration, order submission, scheduler, shadow replay submit, and trading mutation.
