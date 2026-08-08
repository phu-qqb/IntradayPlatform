# ARCH7B Core Broker to Intraday Supervisor Binding Candidate

## Verdict

`ARCH7B_CORE_BROKER_AND_INTRADAY_SUPERVISOR_CROSS_REPO_BINDING_READY`

The Intraday implementation candidate is
`7986a0a1d79f7d84bd2d1ab3e4958cc863c16c44` with tree
`0e44081e064622c697a78472a98ee0edd5001d67`. The Core implementation is
`be5e969fbeae56cf8de673023a36062a26f52e64` with tree
`03229eb69a859927bfcd27ff2796fe3051df33c3`. The Core repository authority
on Primary is `d58a6bf3e6b7c62c68d8a3df0924ae8f7bfa3965ea5f5a6553735a785b66be89`,
with tracked inventory SHA-256
`532a8774c00717bf67fa0a7e44e8eb1fa6f44a4b4135121dcbebc46985ed408d`.
The broker module and CLI SHA-256 remain respectively
`2ba086323683524fc018937e88a0adbd4723d8ed201efa93293b98eb81f587f2` and
`e0bfb03b75af841a8a808b8efb0f734b128756d5c7fdc3e10e9d13a19fe886c3`:
the functional bytes have not changed. The final Intraday PR HEAD is
the commit containing this packet.

## Binding

Intraday materializes the Core config and four-command plan after slot lock,
one-shot identity creation, RDS read one, and runtime inventory validation. It
starts one long-lived Core broker at RDS_READ_2, verifies READY and VersionId,
marks BRACKET_T0, routes four strict consumers, marks TERMINAL_READONLY after
ARCH7A, executes reporting, then verifies terminal cleanup.

Portable .NET runtimes are supported through an optional absolute
`DOTNET_ROOT` that is validated against the SHA-256 of its `dotnet` executable
and sealed into each command's non-secret environment. Ambient unbound
environment inheritance is not used.

## Consumers And Payloads

Position import, PMS replay, ARCH7A shadow qualification, and operational
reporting consume the password-only broker variable. PMS combines that value
with the versioned non-secret PostgreSQL target contract and rejects the old
full-connection-string fallback. Runtime selection and working-order preflight
remain internal.

Native payloads exist only in memory between the broker client and one strict
adapter. Persistable responses, the fact journal, and terminal evidence exclude
them. Sequence 1-4, phase, command/stage identity, previous-response SHA, and
normalized output evidence are checked on every response.

## Qualification

Local qualification passes 20/20 independent runs and 10/10 campaigns of
three runs: 50/50 complete sequences, adapters, and terminal cleanups. ARCH7B
regressions pass 637/637 and the Release build has zero errors. Pending model
changes remain false and the migration count remains eight.

Primary command `0eadeb4b-f3f1-49c2-bf39-b51c3cfac90e` passes 3/3 complete
rehearsals. The installed supervisor DLL SHA-256 is
`78070ee73463ead2fa267af5fc79f74b7822e1379415b596a05b905585f20513`.
Evidence SHA-256 is
`cee11a6d8665fae6703a6fa87572529352e92671a35d99e79b0c13ddc3c7be8e`.
Residual processes and transfer files are zero.

## Safety

Real secret reads, DB connections and writes, live slots, Portal HTTP, Market
Data, FIX logons, orders, Fills, ledger events, Account API, Polygon,
Databento, AWS/S3 mutations, and operational one-shot state are all zero. PR
#53 remains open, draft, and unmerged.
