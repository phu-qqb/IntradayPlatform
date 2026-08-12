# ARCH7B Core Broker to Intraday Supervisor Binding Candidate

## Verdict

`ARCH7B_CORE_BROKER_AND_INTRADAY_SUPERVISOR_CROSS_REPO_BINDING_READY`

The Intraday functional implementation candidate is the commit containing this
packet; its parent authority is
`eb01fedcd323e2181382d8d320de55f7c936d95b` with tree
`71830df92b90a74c9771954e1ae11065f964ee74`. The Core implementation is
`24992b452a1a3d99318c137413a5e6a4a55512d3` with tree
`d9ff920ea5d514190375c689b6d795f1a9a57f37`. The Core repository authority
on Primary is `d4a3c264d2dca0983ac375a39fd8dbc788daf16f329e1afa24ac8afb7a3ae7e9`,
with tracked inventory SHA-256
`786c7b52353a061ec4f1d56c0b238f61809ecadd951a245ae54944d0d5eaeb91`.
Repository materialization and authority computation were executed by SSM commands
`d4117e94-72fb-4c2a-844b-6dbd6278001f` and
`5a5a5cd2-a0d5-43ef-be04-cedeab5882d5`; the probe root was removed.
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
`5d65927118bb3cc24fc4884dbab1dd82ab579cff41ba1f4683378d88d9aa6613`
and module SHA-256
`277ed26a04bf705d05d04f70be96d838fb8326e70c9e38d72dd4f475adb8aea2`.
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
770/770, the obsolete 154/154 count is rejected, and the Release build has zero
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
