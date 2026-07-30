# ARCH7B explicit MiniGit repository authority

## Scope

This corrective contract removes the ambient `PATH` dependency from the
ARCH7B position import repository prevalidation. It does not change
PostgreSQL, migrations, IAM, networking, TLS, Core, LMAX, FIX, or order
behavior.

The historical run remains append-only classified as:

`ABORTED_REPOSITORY_PREVALIDATION_GIT_EXECUTABLE_UNRESOLVED`

with verdict:

`NO_GO_ARCH7B_ARM_IMPORT_REPOSITORY_PREVALIDATION_GIT_NOT_ON_SSM_PATH`

## Primary forensic

The non-interactive SSM process runs as `NT AUTHORITY\SYSTEM`, under
`C:\Program Files\Amazon\SSM\ssm-document-worker.exe`. Its ambient PATH
SHA-256 is:

`030dc50e0c9516b39b86aeb722d4ac5f9ad99039f0e9309b393ab36004530204`

Both `where.exe git` and `Get-Command git -All` found no executable. Six
historical Git candidates were identified; the run-scoped MiniGit authority
is:

- path: `D:\QQFund\ARCH7B\runtime\mingit\cmd\git.exe`
- SHA-256: `7b7971dd13f0c3a284e538601f2f9770b3a87dfaccb5fb52d68141c67ed22364`
- version: `git version 2.55.0.windows.3`
- architecture: `x64`
- Authenticode: `Valid`
- reparse point: `false`

The forensic evidence is:

`D:\QQFund\ARCH7B\git-executable-forensic-d427-20260730T083136Z\arch7b-git-executable-forensic.json`

Its SHA-256 is:

`d8774de2c28c941f08cd25481f968452326f1dba66817e13a7221c46f2274819`

## Runtime contract

Every repository-bound mode requires:

```text
--git-executable <absolute-path>
--expected-git-sha256 <sha256>
--expected-git-version <exact-version-output>
--expected-repository-head <full-sha1>
```

The executable is invoked directly with `UseShellExecute=false`,
`CreateNoWindow=true`, redirected output, no shell, no PATH lookup, and a
10-second timeout per command. The authority validates the executable path,
regular-file identity, reparse chain, SHA-256, version, x64 architecture,
Authenticode status, Primary host, repository root, origin remote, HEAD,
worktree, and index.

The dedicated `qualify-repository-authority` mode returns before
`BuildRuntime`, Secrets Manager access, pinned-session construction, or
`OpenAsync`. Its evidence explicitly records zero secret reads, zero runtime
builds, zero DB opens, zero armed states, and zero owner locks.

## Orchestration

Operational orchestration must:

1. run `qualify-repository-authority`;
2. verify `ARCH7B_REPOSITORY_AUTHORITY_QUALIFIED` and its evidence SHA;
3. only then read the PostgreSQL secret;
4. pass the same Git path, Git SHA, Git version, repository HEAD, and build
   commit to `arm-import` and later repository-bound modes.

## Core handoff

No Core code is changed in this packet. The existing Core handoff accepts
additional safe Intraday arguments, but does not yet make these fields
authoritative:

- `git_executable_path`
- `git_executable_sha256`
- `git_authority_evidence_sha256`

A separate Core packet is required to make those handoff fields mandatory.
Until then, operational resume must bind the prepared Intraday command to the
qualified profile and verify the evidence SHA before any secret read.
