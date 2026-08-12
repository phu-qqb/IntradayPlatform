# ARCH7B Core prequalification output contract

## Direct command

CORE_PREQUALIFICATION is a direct child process. Its executable authority is
node_executable, its working-directory authority is core_node_runtime, and its
only command line is:

    node.exe
    tools/lmax_portal_reports_downloader/src/fast-seal-cli.mjs
    prequalify-bracket-runtime
    --config
    <run-root>/core-prequalification-config.json

The config is created once by SLOT_LOCKED. It binds the qualified Core Git
repository, exact Core commit and tree, a create-new output root, and the
msedge browser channel. The command receives exactly two sealed non-secret
entries: `PATH`, containing only the parent directories of the exact
`git_executable`, `node_executable`, and `taskkill_executable` authorities; and
`ProgramFiles(x86)`, bound to the exact `msedge_executable` authority. PowerShell,
command wrappers, shell execution, and ambient environment resolution are not
command routes.

The Core CLI owns stdout. A successful invocation emits one UTF-8 JSON
document with exactly the top-level properties qualification and manifest.
Wrapper banners, suffixes, multiple documents, a BOM, invalid UTF-8, and native
shape drift are rejected. No generic JSON substring extraction is allowed.
The adapter requires exactly `tests_passed=156` and `tests_total=156` from the
native qualification. The superseded `154/154` count and any other value are
rejected fail-closed.

## Process receipt

After the process exits and both bounded streams complete, the supervisor
writes child-process-output-receipt.json before invoking the adapter. The
receipt contract is arch7b_child_process_output_receipt_v1.

The receipt contains process identity, timestamps, exit status, byte counts,
SHA-256 values, UTF-8 and secret-scan status, adapter identity, native contract,
and materialized-command identity. It never contains raw stdout, raw stderr,
secret values, or environment values.

Adapter rejection writes child-adapter-failure.json under
arch7b_child_adapter_failure_v1. That evidence links the receipt by path and
SHA-256 and records only a classification, exception type, blocker, and
message SHA-256. The first child or adapter blocker remains authoritative.

## Exit status

run-one-shot returns 0 only for passed native evidence with complete cleanup,
2 for a functional NO_GO, and 1 for an unexpected failure. An SSM caller must
require both process exit 0 and evidence.passed=true.

## Taskkill cleanup authority

The Edge-backed prequalification requires the Windows process cleanup utility
after the browser closes. `CORE_PREQUALIFICATION` therefore carries exactly one
sealed `PATH` composed, in order, from the parent directories of these file
authorities:

1. `git_executable`;
2. `node_executable`;
3. `taskkill_executable`.

The taskkill path is derived from
`Environment.SpecialFolder.System/taskkill.exe`. Its file and parent directory
must exist, must not be reparse points, and the file SHA-256 is checked when the
template is produced and immediately before the Node child starts. No ambient
machine, user, or parent `PATH` value is inherited.

The PATH evidence uses the composite authority id
`core_prequalification_executable_search_path`. Its source SHA-256 and evidence
SHA-256 bind the IDs, absolute paths, and byte SHA-256 values of all three
executables. The operational inventory expands that composite source into
three required file-authority references, exclusively for
`CORE_PREQUALIFICATION`.

## Edge channel authority

Playwright 1.62.0 resolves the installed `msedge` channel through
`ProgramFiles(x86)`. `CORE_PREQUALIFICATION` therefore receives exactly one
additional sealed variable with that name. Its value is derived from
`Environment.SpecialFolder.ProgramFilesX86`; it is never inherited from the
machine, user, parent process, or another command.

The composite authority
`core_prequalification_program_files_x86_authority` binds the variable name,
the absolute Program Files (x86) directory, the `msedge_executable` authority
ID, the exact path `Microsoft/Edge/Application/msedge.exe`, the executable byte
SHA-256, and the environment contract version. The executable and every parent
from `ProgramFilesX86` through `Application` must exist without reparse points.
The path and byte SHA-256 are checked again immediately before the Node child
starts. `PROGRAMFILES`, `HOMEDRIVE`, and ambient `ProgramFiles(x86)` values are
not inherited or added to `InheritedSystemVariables`.
