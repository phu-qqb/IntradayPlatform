# ARCH7B Core Broker to Intraday Supervisor Binding Candidate

## Verdict

`ARCH7B_CORE_BROKER_AND_INTRADAY_SUPERVISOR_CROSS_REPO_BINDING_READY`

The Intraday functional implementation candidate is
`08d7c7195939d7e7ee48b4567ee97e45bba417d6` with tree
`6d339ca73c8f5e8cb957ac5d6cc08a48b6da4941`; the final authority-only rebind is
the commit containing this packet. The Core implementation is
`43a848ec0c609d6257a3020cb7cbe1f10443b5e6` with tree
`f4fe2265c80288e6133f2484da3ed8819aa6c92b`. The Core repository authority
on Primary is `44cb8bc70ac9f488bb819e24590fee3d96b1dc3d73b07ce99135a42350a8ce42`,
with tracked inventory SHA-256
`f5dbca8c496456580c7d63e498866333bd0afbc68057440587c0826261709991`.
The Portal wrapper remains
`tools/lmax_portal_reports_downloader/src/downloader.mjs`; its Git source and
Windows runtime SHA-256 values are respectively
`c732eaff2912e09f0cb31d4143bd9a28f648428e195dd32937c525bd1fd56fab` and
`7525a08daea3f830e705b253d522320d09ed7eff287d20bc4ff767ca99004f3b`.
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
