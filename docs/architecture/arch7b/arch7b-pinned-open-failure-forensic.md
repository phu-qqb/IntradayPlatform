# ARCH7B pinned PostgreSQL open failure forensic

## Preserved verdict

`NO_GO_ARCH7B_PINNED_POSTGRESQL_SESSION_OPEN_FAILED_CONNECTION_STUCK_CONNECTING`

The selected historical run remains append-only and is classified:

`ABORTED_PINNED_POSTGRESQL_OPEN_FAILURE_MASKED_BY_CONNECTING_CLEANUP`

## Honest primary classification

Category **G**:

`PRIMARY_EXCEPTION_NOT_RECOVERABLE_FROM_SEALED_EVIDENCE`

The sealed stdout, stderr, and run artifacts contain no retained first
exception, first-exception timestamp, SQLSTATE, or primary stack. A timeout
must not be inferred from the later cleanup exception.

The selected SSM command was
`ff94ca98-c8b9-4795-96b4-54e4030ceb84`. Its recorded execution interval was
2026-07-29 16:53:56.800Z through 16:53:58.800Z, its terminal elapsed duration
was 2.709 seconds, and its exit code was 1. The process creation and internal
milestone timestamps were not present in the sealed evidence.

## Recovered cleanup exception

Type: `System.InvalidOperationException`

Sanitized message:

`Can't close, connection is in state Connecting`

Sanitized stack sequence:

1. `Npgsql.NpgsqlConnection.Close`
2. `Npgsql.NpgsqlConnection.CloseAsync`
3. `Npgsql.NpgsqlConnection.DisposeAsync`
4. `Arch7bPostgreSqlPinnedSession.DisposeAsync`
5. `Arch7bPositionSnapshotImport.Program`

No SQLSTATE was present.

## Masking sequence

1. A primary failure escaped the unsupervised `OpenAsync` task.
2. Compiler-generated top-level `await using` cleanup invoked `DisposeAsync`.
3. Npgsql disposal ran while the connection state was `Connecting`.
4. The cleanup `InvalidOperationException` became the terminal stderr error.
5. The primary exception was not retained and cannot be reconstructed.

The target, DbContext factory, store, repository authority, build identity,
secret target, root CA, and Npgsql open paths were considered. Sealed evidence
does not identify one of them as the primary source. An earlier root-CA
precondition failure was a distinct attempt and is not attributed to this run.

No historical artifact was modified. The run produced no armed state, owner
lock, database write, LMAX acquisition, FIX session, or order.
