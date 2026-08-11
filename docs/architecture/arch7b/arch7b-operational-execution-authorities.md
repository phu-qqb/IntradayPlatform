# ARCH7B operational execution authorities

## Scope

The operational authority inventory is derived from the final 40-stage,
13-command live plan template. It is not a manually maintained list. The
derivation closes command executables, working directories, authority
placeholders, sealed non-secret environment bindings, and all static
pre-spawn bindings used by `run-one-shot`.

The source-of-truth types and validators live in
`Arch7bOperationalExecutionAuthorities.cs`. Runtime artifacts use these
versioned contracts:

- `arch7b_operational_execution_authority_reference_v1`
- `arch7b_required_operational_execution_authority_inventory_v1`
- `arch7b_operational_execution_authority_v1`
- `arch7b_operational_execution_authority_manifest_v1`
- `arch7b_operational_execution_authority_directory_inventory_v1`
- `arch7b_operational_execution_authority_validation_v1`

## Materialization

`materialize-operational-execution-authorities` accepts the final static
40-stage, 13-command template, a closed authority-to-path map, and an empty
output root. It writes
the required-reference inventory, one canonical directory inventory for each
directory authority, and the exact authority manifest.

`materialize-operational-live-template` takes the same final template and
requires that manifest. It projects
the complete authority set into `Template.FileAuthorities` and rejects a
missing, unused, duplicated, conflicting, synthetic, or unresolved authority.
The live authority copies that exact set from the validated template.

Directory authority hashes cover the ordinally sorted canonical inventory of
relative paths, entry types, byte lengths, file SHA-256 values, executable
flags, and reparse-point flags. A path string is never accepted as directory
content evidence.

## Pre-slot validation

`run-one-shot --static-preflight-only true --qualification-only true` performs
the native static preflight without loading a live execution authority or an
operator authorization. It requires `--no-order true`, writes only sanitized
validation evidence adjacent to a still-uncreated run root, and reports that
slot selection, slot lock, one-shot identity creation, live access, residual
processes, and residual markers are all zero.

`run-one-shot` parses the raw authority manifest before dictionary
construction and rejects duplicate JSON properties or duplicate AuthorityId
entries. It then validates the manifest, template, and live authority as an
exact bijection before any calendar load, slot selection, identity creation,
secret read, database access, Portal access, LMAX session, FIX logon, or order.

Validation is type-specific:

- files, root CA, and static configs require regular files and exact byte SHA;
- directories require exact readback inventories and no reparse points;
- Git requires the explicit Git executable, exact remote/HEAD/tree, clean
  index/worktree, non-shallow storage, no alternates, and strict fsck;
- Core Node requires exact package files and closure, local AWS SDK and
  Playwright imports, Core module imports, offline tests, and offline audit;
- .NET requires the bound runtime root, executable, version, shared runtime,
  and no system fallback.

The sanitized result is written as
`arch7b-operational-execution-authority-validation-v1.json` in the static
preflight evidence root adjacent to the still-uncreated one-shot run root.

## Safety

This contract performs static filesystem and local process qualification only.
It does not read secrets, connect to PostgreSQL, open Portal or LMAX sessions,
log on to FIX, send orders, create fills, or mutate a ledger.
