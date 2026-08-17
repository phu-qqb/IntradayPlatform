# ARCH7B V7R9 live CLI authority binding forensic

This append-only record preserves the V7R9 failure. V7R9 and its evidence are
not modified.

- Historical blocker: `ARCH7B_ONE_SHOT_AUTHORITY_BINDING_MISMATCH: core_repository`
- SSM command: `161ff0dd-6ae6-4ead-b81b-d1f00a196e61`
- Operator authorization: `arch7b-v7r9-42a8a52149fb453b96c958f5232f2c63`
- Evidence SHA-256: `bee4b754af53e51d1f69009775742ca31c13d76d3d892683598236685dbb2ef8`
- Slot, retry, RDS, capture, and bracket budgets consumed: `0/0/0/0/0`

## Target-bound findings

| Authority | Kind | CLI and target path | Target SHA-256 semantics | Target SHA-256 | Legacy static field | Legacy SHA-256 | Classification |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `core_repository` | Git repository directory | `D:\QQFund\ARCH7B\runtime\core-operational-24992b45-dde797bc` | directory inventory | `724103e4223f5243f59d0270b996c7769f0ed4e66913d3cddfa6fc7f861c631a` | `CoreRepositoryAuthoritySha256` | `d4a3c264d2dca0983ac375a39fd8dbc788daf16f329e1afa24ac8afb7a3ae7e9` | `DIFFERENT_SEMANTICS` |
| `intraday_runtime` | directory inventory | `D:\QQFund\ARCH7B\runtime\intraday-9142b11-source-provenance\runtime` | directory inventory | `f0ab18b0c8f6df4f3c8acaf1b8a5259d378da88ea26d359b93cad554a17f83ef` | `RuntimeInventorySha256` | `2be6c532796298823bb7b70912f23c3412aa4887effcf7e171ad2f281e3ddcee` | `DIFFERENT_SEMANTICS` |
| `git_executable` | file | `D:\QQFund\ARCH7B\runtime\mingit\cmd\git.exe` | file content | `7b7971dd13f0c3a284e538601f2f9770b3a87dfaccb5fb52d68141c67ed22364` | none | none | `SAME_SEMANTICS` |
| `root_certificate` | file | `D:\QQFund\ARCH7B\runtime\intraday-0dd8be3a-v7r7\static\amazon-rds-eu-west-2-root-ca-rsa2048-g1.pem` | file content | `17976078e32d253e3d77a464933d96804357a7d61206e0ecdd38145a64f67527` | `RootCaAuthoritySha256` | `61ded1572899c07ad188ef3ae7e3529049cf999c366c66f2ee290d793f33cf8e` | `DIFFERENT_SEMANTICS` |

For `core_repository`, the operational manifest binds the same target path and
directory inventory SHA-256. It also binds repository
`https://github.com/phu-qqb/QQ.Production.Core.git`, commit
`24992b452a1a3d99318c137413a5e6a4a55512d3`, tree
`d9ff920ea5d514190375c689b6d795f1a9a57f37`, inventory manifest SHA-256
`7c10f61b4ff480019548978aa99afde136d52ba0852674fa8ccb43fe4a64c72e`.

The original CLI path and target authority path were equal. The native failure
is therefore classified as:

`CORE_REPOSITORY_CLI_BINDER_COMPARED_TARGET_DIRECTORY_INVENTORY_SHA_TO_LEGACY_STATIC_CORE_AUTHORITY_SHA`

The same latent semantic mismatch existed for `intraday_runtime`:

`INTRADAY_RUNTIME_CLI_BINDER_COMPARED_TARGET_DIRECTORY_AUTHORITY_TO_PORTABLE_RUNTIME_INVENTORY_SHA`

Directory and Git content remain owned by
`Arch7bOperationalExecutionAuthorityValidator.ValidateStatic`. The shared CLI
binder verifies exact target paths for directory authorities and recomputes
content SHA-256 only for file authorities.
