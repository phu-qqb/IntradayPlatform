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
exact `browserExecutablePath` and `expectedBrowserExecutableSha256` from the
`chrome_executable` authority. `browserChannel` is absent. The command receives
exactly one sealed non-secret entry: `PATH`, containing only the parent
directories of the exact `git_executable`, `node_executable`, and
`taskkill_executable` authorities. PowerShell, command wrappers, shell
execution, ambient browser discovery, Playwright downloads, and browser
fallbacks are not command routes.

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

The Chrome-backed prequalification retains the Windows process cleanup utility
after the browser closes. `CORE_PREQUALIFICATION` carries exactly one sealed
`PATH` composed, in order, from the parent directories of these file
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

## Chrome executable authority

All selected ARCH7B Playwright stages use the same `chrome_executable` FILE
authority. On the Primary it binds Chrome 151.0.7922.110 at
`C:\Program Files\Google\Chrome\Application\chrome.exe` with SHA-256
`1c8a72b0e6b5a4dd1de5ce42a7b11460753d8941baebda208360475f31eb17d2`.
Playwright 1.62.0 launches it through `chromium.launch` with an explicit
`executablePath`, `headless=true`, and no channel.

The authority requires an absolute regular file named `chrome.exe`,
`MustExist=true`, `MustBeInsideRunRoot=false`, and no reparse point on the file
or any parent. The path and byte SHA-256 are checked when the operational
template is materialized and again immediately before every browser spawn.
The native `browser_runtime` output must report source
`EXPLICIT_EXECUTABLE`, basename `chrome.exe`, the same SHA-256, a non-empty
version, `headless=true`, and a null channel.

The selected operational template contains
`selected_browser=CHROME_EXPLICIT_EXECUTABLE`. Portal proof, auth-only recovery
when separately authorized, bracketed snapshot, and Core prequalification all
bind this exact executable. `msedge_executable`, `browserChannel`,
`ProgramFiles(x86)` Edge resolution, browser downloads, PATH discovery, and
fallback browsers are forbidden selected dependencies.

## Preserved Edge forensic

The append-only verdict
`NO_GO_ARCH7B_EDGE_CANNOT_RUN_UNDER_PRIMARY_SSM_PROCESS_CONTEXT` remains
authoritative with classification
`EDGE_SYSTEM_SESSION_ZERO_OR_PROCESS_TOKEN_INCOMPATIBLE`. Its historical
forensic files are retained; they are evidence, not selectable runtime
authorities.
