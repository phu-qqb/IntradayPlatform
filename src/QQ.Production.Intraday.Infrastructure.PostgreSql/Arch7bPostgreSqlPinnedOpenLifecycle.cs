using System.Data;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class Arch7bPostgreSqlPinnedOpenLifecycleContract
{
    public const string Version =
        "arch7b_postgresql_pinned_open_lifecycle_v1";
    public const string PrimaryOpenFailure =
        "ARCH7B_PINNED_POSTGRESQL_PRIMARY_OPEN_FAILURE";
    public const string OpenAsyncTimeout =
        "ARCH7B_PINNED_POSTGRESQL_OPENASYNC_TIMEOUT";
    public const string HardDeadlineExceeded =
        "ARCH7B_PINNED_POSTGRESQL_OPEN_HARD_DEADLINE_EXCEEDED";
    public const string OpenTaskNotSettled =
        "ARCH7B_PINNED_POSTGRESQL_OPEN_TASK_NOT_SETTLED";
    public const string ConnectingCleanupDeferred =
        "ARCH7B_PINNED_POSTGRESQL_CONNECTING_CLEANUP_DEFERRED_TO_PROCESS_EXIT";
    public const string CleanupFailedWithoutPrimary =
        "ARCH7B_PINNED_POSTGRESQL_CLEANUP_FAILED_WITHOUT_PRIMARY_FAILURE";
    public const string PrimaryFailurePreservationFailed =
        "ARCH7B_PINNED_POSTGRESQL_PRIMARY_FAILURE_PRESERVATION_FAILED";
    public static readonly TimeSpan HardDeadline = TimeSpan.FromSeconds(25);
    public static readonly TimeSpan CancellationGrace = TimeSpan.FromSeconds(2);
}

public sealed record Arch7bPostgreSqlPinnedCleanupResult(
    string Result,
    ConnectionState StateBeforeCleanup,
    ConnectionState StateAfterCleanup,
    bool CleanupAttempted,
    string? CleanupFailureType,
    bool CleanupFailureSuppressedByPrimary,
    bool ProcessExitRequiredForHandleCleanup);

public sealed record Arch7bPostgreSqlPinnedOpenLifecycleEvidence(
    string ContractVersion,
    int ProcessId,
    string Mode,
    DateTimeOffset OpenStartedAtDiagnosticUtc,
    DateTimeOffset? OpenCompletedAtDiagnosticUtc,
    double OpenElapsedMilliseconds,
    string OpenTaskStatus,
    string ConnectionStateBeforeOpen,
    string? ConnectionStateAtPrimaryFailure,
    string ConnectionStateBeforeCleanup,
    string ConnectionStateAfterCleanup,
    string? PrimaryFailureType,
    string? PrimaryFailureCode,
    string? PrimarySqlState,
    string? PrimaryFailureMessageSanitized,
    IReadOnlyList<string> PrimaryInnerFailureTypes,
    IReadOnlyList<string> PrimaryStackTraceSanitized,
    IReadOnlyList<string> SecondaryFailureTypes,
    bool PrimaryFailurePreserved,
    bool CleanupAttempted,
    string CleanupResult,
    string? CleanupFailureType,
    bool CleanupFailureSuppressedByPrimary,
    bool ProcessExitRequiredForHandleCleanup,
    IReadOnlyList<string> StateTransitions,
    string EvidenceSha256);

public interface IArch7bPostgreSqlPinnedOpenRuntime
{
    ConnectionState ConnectionState { get; }
    Task<Arch7bPostgreSqlPinnedSessionEvidence> OpenAsync(
        CancellationToken cancellationToken = default);
    Task<Arch7bPostgreSqlPinnedCleanupResult> CompleteAsync(
        Task openTask,
        CancellationTokenSource openCancellation,
        Exception? primaryFailure,
        TimeSpan cancellationGrace);
}

public sealed class Arch7bPostgreSqlPinnedOpenSupervisor
{
    private readonly object sync = new();
    private readonly IArch7bPostgreSqlPinnedOpenRuntime runtime;
    private readonly string mode;
    private readonly string evidenceDirectory;
    private readonly TimeSpan hardDeadline;
    private readonly TimeSpan cancellationGrace;
    private readonly CancellationTokenSource openCancellation = new();
    private readonly List<string> transitions = ["CREATED"];
    private readonly List<string> secondaryFailureTypes = [];
    private Task<Arch7bPostgreSqlPinnedSessionEvidence>? openTask;
    private ExceptionDispatchInfo? primaryFailure;
    private long primaryFailureTimestamp = long.MaxValue;
    private DateTimeOffset openStartedAtDiagnosticUtc;
    private DateTimeOffset? openCompletedAtDiagnosticUtc;
    private ConnectionState connectionStateBeforeOpen;
    private ConnectionState? connectionStateAtPrimaryFailure;
    private int completed;
    private readonly TaskCompletionSource<
        Arch7bPostgreSqlPinnedOpenLifecycleEvidence> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Arch7bPostgreSqlPinnedOpenSupervisor(
        IArch7bPostgreSqlPinnedOpenRuntime runtime,
        string mode,
        string evidenceDirectory,
        TimeSpan? hardDeadline = null,
        TimeSpan? cancellationGrace = null)
    {
        this.runtime = runtime;
        this.mode = mode;
        this.evidenceDirectory = Path.GetFullPath(evidenceDirectory);
        this.hardDeadline = hardDeadline ??
            Arch7bPostgreSqlPinnedOpenLifecycleContract.HardDeadline;
        this.cancellationGrace = cancellationGrace ??
            Arch7bPostgreSqlPinnedOpenLifecycleContract.CancellationGrace;
    }

    public Task<Arch7bPostgreSqlPinnedSessionEvidence> StartOpen()
    {
        lock (sync)
        {
            if (openTask is not null)
                throw new InvalidOperationException(
                    Arch7bPostgreSqlPinnedSessionAuthority.SecondOpen);
            connectionStateBeforeOpen = runtime.ConnectionState;
            openStartedAtDiagnosticUtc = DateTimeOffset.UtcNow;
            transitions.Add("OPENING");
            openTask = ObserveOpenAsync();
            return openTask;
        }
    }

    public async Task<Arch7bPostgreSqlPinnedSessionEvidence>
        WaitForOpenAsync()
    {
        var task = openTask ?? StartOpen();
        if (await Task.WhenAny(task, Task.Delay(hardDeadline)) != task)
        {
            var failure = new TimeoutException(
                Arch7bPostgreSqlPinnedOpenLifecycleContract
                    .HardDeadlineExceeded);
            CapturePrimary(failure);
            openCancellation.Cancel();
            throw failure;
        }
        return await task;
    }

    public async Task<(
        Arch7bPostgreSqlPinnedSessionEvidence OpenEvidence,
        T PeerResult)> WaitForOpenAndPeerAsync<T>(Task<T> peerTask)
    {
        var observedPeer = ObservePeerAsync(peerTask);
        var observedOpen = WaitForOpenAsync();
        try
        {
            await Task.WhenAll(observedOpen, observedPeer);
            return (await observedOpen, await observedPeer);
        }
        catch
        {
            openCancellation.Cancel();
            await ObserveSettlementAsync(observedOpen);
            await ObserveSettlementAsync(observedPeer);
            RethrowPrimary();
            throw new InvalidOperationException(
                Arch7bPostgreSqlPinnedOpenLifecycleContract
                    .PrimaryFailurePreservationFailed);
        }
    }

    public void CapturePrimary(Exception exception)
    {
        var observedAt = Stopwatch.GetTimestamp();
        lock (sync)
        {
            if (primaryFailure is not null)
            {
                if (ReferenceEquals(
                        primaryFailure.SourceException, exception))
                    return;
                if (observedAt < primaryFailureTimestamp)
                    secondaryFailureTypes.Add(
                        primaryFailure.SourceException.GetType().FullName ??
                        primaryFailure.SourceException.GetType().Name);
                else
                {
                    secondaryFailureTypes.Add(
                        exception.GetType().FullName ??
                        exception.GetType().Name);
                    return;
                }
            }
            primaryFailure = ExceptionDispatchInfo.Capture(exception);
            primaryFailureTimestamp = observedAt;
            connectionStateAtPrimaryFailure = runtime.ConnectionState;
        }
    }

    public async Task<Arch7bPostgreSqlPinnedOpenLifecycleEvidence>
        CompleteAsync(Exception? callerPrimaryFailure = null)
    {
        if (callerPrimaryFailure is not null)
            CapturePrimary(callerPrimaryFailure);
        if (Interlocked.Exchange(ref completed, 1) != 0)
            return await completion.Task;

        var task = openTask ?? Task.FromException(
            new InvalidOperationException(
                Arch7bPostgreSqlPinnedOpenLifecycleContract
                    .OpenTaskNotSettled));
        lock (sync) transitions.Add("CLEANUP");
        var beforeCleanup = runtime.ConnectionState;
        Arch7bPostgreSqlPinnedCleanupResult cleanup;
        try
        {
            cleanup = await runtime.CompleteAsync(
                task, openCancellation,
                PrimaryException, cancellationGrace);
        }
        catch (Exception cleanupFailure)
        {
            var primaryExists = PrimaryException is not null;
            cleanup = new(
                primaryExists ? "CLEANUP_FAILURE_SUPPRESSED" :
                    "CLEANUP_FAILED_WITHOUT_PRIMARY",
                beforeCleanup,
                runtime.ConnectionState,
                true,
                cleanupFailure.GetType().FullName,
                primaryExists,
                runtime.ConnectionState == ConnectionState.Connecting);
        }

        lock (sync) transitions.Add("COMPLETED");
        var evidence = BuildEvidence(beforeCleanup, cleanup);
        PublishEvidence(evidence);
        completion.TrySetResult(evidence);
        if (PrimaryException is null &&
            cleanup.CleanupFailureType is not null)
            throw new InvalidDataException(
                Arch7bPostgreSqlPinnedOpenLifecycleContract
                    .CleanupFailedWithoutPrimary);
        return evidence;
    }

    public Exception? PrimaryException
    {
        get
        {
            lock (sync) return primaryFailure?.SourceException;
        }
    }

    public void RethrowPrimary()
    {
        ExceptionDispatchInfo? failure;
        lock (sync) failure = primaryFailure;
        failure?.Throw();
    }

    private async Task<Arch7bPostgreSqlPinnedSessionEvidence>
        ObserveOpenAsync()
    {
        try
        {
            var evidence = await runtime.OpenAsync(openCancellation.Token);
            openCompletedAtDiagnosticUtc = DateTimeOffset.UtcNow;
            lock (sync) transitions.Add("OPEN");
            return evidence;
        }
        catch (Exception exception)
        {
            openCompletedAtDiagnosticUtc = DateTimeOffset.UtcNow;
            CapturePrimary(exception);
            lock (sync) transitions.Add("OPEN_FAILED");
            throw;
        }
    }

    private async Task<T> ObservePeerAsync<T>(Task<T> task)
    {
        try
        {
            return await task;
        }
        catch (Exception exception)
        {
            CapturePrimary(exception);
            throw;
        }
    }

    private Arch7bPostgreSqlPinnedOpenLifecycleEvidence BuildEvidence(
        ConnectionState beforeCleanup,
        Arch7bPostgreSqlPinnedCleanupResult cleanup)
    {
        var primary = PrimaryException;
        var elapsed = openStartedAtDiagnosticUtc == default
            ? TimeSpan.Zero
            : (openCompletedAtDiagnosticUtc ?? DateTimeOffset.UtcNow) -
              openStartedAtDiagnosticUtc;
        var material = new
        {
            contract_version =
                Arch7bPostgreSqlPinnedOpenLifecycleContract.Version,
            process_id = Environment.ProcessId,
            mode,
            open_started_at_diagnostic_utc = openStartedAtDiagnosticUtc,
            open_completed_at_diagnostic_utc = openCompletedAtDiagnosticUtc,
            open_elapsed_milliseconds = elapsed.TotalMilliseconds,
            open_task_status = openTask?.Status.ToString() ?? "NotStarted",
            connection_state_before_open = connectionStateBeforeOpen.ToString(),
            connection_state_at_primary_failure =
                connectionStateAtPrimaryFailure?.ToString(),
            connection_state_before_cleanup = beforeCleanup.ToString(),
            connection_state_after_cleanup =
                cleanup.StateAfterCleanup.ToString(),
            primary_failure_type = primary?.GetType().FullName,
            primary_failure_code = FailureCode(primary),
            primary_sql_state = SqlState(primary),
            primary_failure_message_sanitized = FailureCode(primary),
            primary_inner_failure_types = InnerFailureTypes(primary),
            primary_stack_trace_sanitized =
                SanitizedStackTrace(primary),
            secondary_failure_types =
                secondaryFailureTypes.ToArray(),
            primary_failure_preserved = primary is not null,
            cleanup_attempted = cleanup.CleanupAttempted,
            cleanup_result = cleanup.Result,
            cleanup_failure_type = cleanup.CleanupFailureType,
            cleanup_failure_suppressed_by_primary =
                cleanup.CleanupFailureSuppressedByPrimary,
            process_exit_required_for_handle_cleanup =
                cleanup.ProcessExitRequiredForHandleCleanup,
            state_transitions = transitions.ToArray()
        };
        var sha = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(material))));
        return new(
            Arch7bPostgreSqlPinnedOpenLifecycleContract.Version,
            Environment.ProcessId,
            mode,
            openStartedAtDiagnosticUtc,
            openCompletedAtDiagnosticUtc,
            elapsed.TotalMilliseconds,
            openTask?.Status.ToString() ?? "NotStarted",
            connectionStateBeforeOpen.ToString(),
            connectionStateAtPrimaryFailure?.ToString(),
            beforeCleanup.ToString(),
            cleanup.StateAfterCleanup.ToString(),
            primary?.GetType().FullName,
            FailureCode(primary),
            SqlState(primary),
            FailureCode(primary),
            InnerFailureTypes(primary),
            SanitizedStackTrace(primary),
            secondaryFailureTypes.ToArray(),
            primary is not null,
            cleanup.CleanupAttempted,
            cleanup.Result,
            cleanup.CleanupFailureType,
            cleanup.CleanupFailureSuppressedByPrimary,
            cleanup.ProcessExitRequiredForHandleCleanup,
            transitions.ToArray(),
            sha);
    }

    private void PublishEvidence(
        Arch7bPostgreSqlPinnedOpenLifecycleEvidence evidence)
    {
        Directory.CreateDirectory(evidenceDirectory);
        var fileName = PrimaryException is not null ||
                       evidence.CleanupFailureType is not null
            ? "pinned-open-lifecycle-failure.json"
            : "pinned-open-lifecycle.json";
        var path = Path.Combine(evidenceDirectory, fileName);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            evidence, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true
            });
        using var stream = new FileStream(
            path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            4096, FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(true);
    }

    private async Task ObserveSettlementAsync(Task task)
    {
        try
        {
            await task.WaitAsync(cancellationGrace);
        }
        catch
        {
            // Each observed task already records its own failure.
        }
    }

    private static string? FailureCode(Exception? exception)
    {
        if (exception is null) return null;
        if (exception.Message.StartsWith("ARCH7B_", StringComparison.Ordinal))
            return exception.Message;
        return exception switch
        {
            TimeoutException => Arch7bPostgreSqlPinnedOpenLifecycleContract
                .OpenAsyncTimeout,
            OperationCanceledException => "OPENASYNC_CANCELLATION",
            NpgsqlException => Arch7bPostgreSqlPinnedOpenLifecycleContract
                .PrimaryOpenFailure,
            _ => exception.GetType().Name
        };
    }

    private static IReadOnlyList<string> InnerFailureTypes(
        Exception? exception)
    {
        var result = new List<string>();
        for (var current = exception?.InnerException;
             current is not null;
             current = current.InnerException)
            result.Add(current.GetType().FullName ??
                       current.GetType().Name);
        return result;
    }

    private static IReadOnlyList<string> SanitizedStackTrace(
        Exception? exception)
    {
        if (exception is null) return [];
        return new StackTrace(exception).GetFrames()
            .Select(frame =>
            {
                var method = frame.GetMethod();
                var type = method?.DeclaringType?.FullName ?? "UNKNOWN_TYPE";
                return $"{type}.{method?.Name ?? "UNKNOWN_METHOD"}";
            })
            .ToArray();
    }

    private static string? SqlState(Exception? exception)
    {
        for (var current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is PostgresException postgres)
                return postgres.SqlState;
        }
        return null;
    }
}
