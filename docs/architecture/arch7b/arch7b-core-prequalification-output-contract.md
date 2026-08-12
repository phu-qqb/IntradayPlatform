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
msedge browser channel. The command receives one sealed non-secret `PATH` entry
containing only the parent directories of the exact `git_executable` and
`node_executable` authorities. This lets npm child scripts resolve Node while
preserving MiniGit resolution. PowerShell, command wrappers, shell execution,
and ambient PATH resolution are not command routes.

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
