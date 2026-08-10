# ARCH7B Core Broker to Intraday Supervisor Binding Candidate

## Verdict

`ARCH7B_CORE_BROKER_AND_INTRADAY_SUPERVISOR_CROSS_REPO_BINDING_READY`

The Intraday functional implementation candidate is
`c11e1fea47831a0f338daaaee540e62bcf2914df` with tree
`da29040372964526e5701349b43e49ead1eb87ed`; the final authority-only rebind is
the commit containing this packet. The Core implementation is
`cb4486c38d8b57addef34218449c17cc04bdd40d` with tree
`a65831ee7fb0e58ac85a95a8ee59d87ee9a97600`. The Core repository authority
on Primary is `d20f5a97876ca1267850d027b48d78d72c9e1248c414cba21410ad5f7f540ce2`,
with tracked inventory SHA-256
`62d876179e7178df70e084a5d55181cd90c0fb50505af2562d1ff2a3f4af1b60`.
The Portal wrapper remains
`tools/lmax_portal_reports_downloader/src/downloader.mjs`; its Git source and
Windows runtime SHA-256 values are respectively
`929962b7cf40b04700929a4c199ee932bd99faa79a04afeec02b783ff896c0c3` and
`9ca645c8333db35bedd5c4f86985c4e6781ca5e059770b29924e01fcc25e2b72`.
It exposes the exact `lmax_portal_demo_session_proof_v1` contract.
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
regressions pass 698/698 and the Release build has zero errors. Pending model
changes remain false and the migration count remains eight.

The authority-only rebind must be committed before the Primary runtime can be
content-addressed. Three Primary A-H campaigns are therefore required against
that commit, with zero residual processes and markers, before merge.

## Safety

Real secret reads, DB connections and writes, live slots, Portal HTTP, Market
Data, FIX logons, orders, Fills, ledger events, Account API, Polygon,
Databento, AWS/S3 mutations, and operational one-shot state are all zero. PR
#56 remains open, draft, and unmerged.
