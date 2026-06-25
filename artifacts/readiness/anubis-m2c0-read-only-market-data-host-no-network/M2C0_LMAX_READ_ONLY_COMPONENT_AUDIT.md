# M2C0 LMAX Read-Only Component Audit

Status: PASS for offline M2C0. No existing live-capable LMAX component is referenced by the new host runtime path.

| Component | Classification | Rationale |
|---|---|---|
| QQ.Production.Intraday.Tools.LmaxReadOnlyActivation | LEGACY_REFERENCE | Tool contains bounded read-only activation concepts but also explicit TCP/FIX/logon/market-data boundary surfaces. M2C0 audited it only; not referenced by the new offline host. |
| QQ.Production.Intraday.Lmax.ConnectivityLab | LEGACY_REFERENCE | Lab includes AccountApi, FIX recovery, raw session client and connectivity surfaces. Evidence/reference only; not safe as M2C0 dependency. |
| QQ.Production.Intraday.Infrastructure.Lmax.LmaxRealReadOnlyMarketDataTransport | SPLIT_REQUIRED | Market-data oriented but still composes TCP/FIX/logon and credential boundaries. Candidate for future M2C1 under lead approval, not M2C0. |
| QQ.Production.Intraday.Infrastructure.Lmax.LmaxReadOnlyExternalSessionFakeTransport | FIXTURE_ONLY | Useful fake transport concept; M2C0 uses a narrower local playback source instead. |
| QQ.Production.Intraday.Infrastructure.Lmax.LmaxAdapterDesign / ILmaxFixOrderGateway | ORDER_CAPABLE_DO_NOT_REFERENCE | Contains trading/order abstractions and explicit order submission safety gates. |
| R220 scripts/evidence | LEGACY_REFERENCE | Useful BBO capture evidence, not imported or run by M2C0. |
| New CanonicalReadOnlyMarketDataHost | REUSE_READ_ONLY | Narrow interface with Start, Subscribe, Read market-data events, Health, Stop only. No order/cancel/account/network implementation. |

Transitive risk conclusion: the new M2C0 host is placed in Application/CanonicalRecorder and references no LMAX infrastructure assembly or order-capable gateway.
