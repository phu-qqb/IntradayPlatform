# ARCH7B PR #53 live runtime integration gap forensic

The inspected authority is IntradayPlatform `5db7bfc769e0ab77cfdc89319a4986658d4f8069`, tree `fcc53d5a1bdbad46f54b07eb6599b16a4a3a5a19`, based on master `3ed8c928eb33063a900ac0d8fa4262c1fe349546`.

## Gaps A-H

| ID | Finding | V2 closure |
|---|---|---|
| A | The freeze contains a fully materialized live plan. | Static template plus append-only typed live fact store. |
| B | Every stage is modeled as a short child process. | Eight execution kinds and a long-lived process registry. |
| C | Declared secret variables are not injected. | Command-scoped lease injection with immediate reference release. |
| D | Native tools must emit one generic envelope. | Fourteen strict native adapters normalize tool-specific output. |
| E | Authority and CLI path bindings are incomplete. | Full V2 authority and exact path/SHA binding. |
| F | Freeze manifest SHA and plan SHA are conflated. | Five independent expected SHA inputs. |
| G | Output limits are checked after `ReadToEndAsync`. | Incremental bounded UTF-8 read, hash, scan, and fail-fast kill-tree. |
| H | Secret scanning recognizes only a sentinel. | Exact leased values and forbidden signatures are scanned in memory. |

## Secret ownership

The Core v6r1 call graph at `9ba391dd197d51d1f44dc8c0d86ac1653f36a042` proves classification **B**: `CORE_LEASE_PROCESS_OWNS_SECRET_AND_SPAWNS_SECRET_CHILDREN`. Core owns and releases the second credential. The Intraday supervisor controls the versioned Core broker contract and never asks for or receives the value. No Core contract change is required.

## Forensic verdict

`ARCH7B_PR53_GENERIC_PROCESS_RUNTIME_NOT_YET_BOUND_TO_REAL_LIVE_COMMAND_GRAPH`

This verdict describes the old PR head and is superseded by `arch7b_one_shot_live_execution_runtime_v2` once the corrected head is qualified.
