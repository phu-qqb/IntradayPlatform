# ARCH7B Core Broker to Intraday Supervisor Binding Candidate

## Verdict

`ARCH7B_CORE_BROKER_AND_INTRADAY_SUPERVISOR_CROSS_REPO_BINDING_READY`

The Intraday functional implementation candidate is
`83f07be60e335c47dd086f82f6469f1694f5b3af` with tree
`fae87d0951ed6b7492546ea2834f58ddb73e4264`; the final authority-only rebind is
the commit containing this packet. The Core implementation is
`97e15383fc4af3d9c2ef19f3804219e793bf29db` with tree
`755139be1ce22338f9b17dbd40c78cc4c46b24e8`. The Core repository authority
on Primary is `e7c8eb41601f55107ba6367bbc72ef8be5581509c042088ce6727c5fd0869da2`,
with tracked inventory SHA-256
`25b72115e399294c761c393ea8bef4028b6f1b21ae5385a5085406369ed1ea7c`.
The Portal wrapper remains
`tools/lmax_portal_reports_downloader/src/downloader.mjs`; its Git source and
Windows runtime SHA-256 values are respectively
`c732eaff2912e09f0cb31d4143bd9a28f648428e195dd32937c525bd1fd56fab` and
`7525a08daea3f830e705b253d522320d09ed7eff287d20bc4ff767ca99004f3b`.
It exposes the exact `lmax_portal_demo_session_proof_v1` contract.
The broker module and CLI SHA-256 remain respectively
`2ba086323683524fc018937e88a0adbd4723d8ed201efa93293b98eb81f587f2` and
`e0bfb03b75af841a8a808b8efb0f734b128756d5c7fdc3e10e9d13a19fe886c3`:
the broker functional bytes have not changed. Core prequalification is now
bound to source-set SHA-256
`1a3cd25c03fab0ee2933e6e2f86ca384e427ac7502669aefd9f21fc0a44a2709`
and module SHA-256
`e4c87cbd179e83454852e3f88cd721a824c28d9e167dbdab083ab2ab7241e5eb`.
It requires exactly 156/156 tests. The final Intraday PR #58 HEAD is the commit
containing this packet.

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

Local output, receipt, and exit tests pass 28/28. ARCH7B regressions pass
757/757, the obsolete 154/154 count is rejected, and the Release build has zero
errors. Pending model changes remain false, the migration count remains eight,
and the warning differential is zero.

The authority-only rebind must be committed before the Primary runtime can be
content-addressed. Three Primary A-J campaigns are therefore required against
that commit, with zero residual processes and markers, before merge.

## Safety

Real secret reads, DB connections and writes, live slots, Portal HTTP, Market
Data, FIX logons, orders, Fills, ledger events, Account API, Polygon,
Databento, AWS/S3 mutations, and operational one-shot state are all zero. PR
#58 remains open, draft, and unmerged.
