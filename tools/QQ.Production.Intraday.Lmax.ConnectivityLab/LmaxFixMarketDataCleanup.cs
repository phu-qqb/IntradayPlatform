namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

using System.Net.Sockets;
using System.Security.Authentication;

public static class LmaxFixMarketDataCleanup
{
    public static async Task<LmaxFixMarketDataCleanupSnapshot> RunAsync(
        DateTimeOffset lifecycleDeadlineUtc,
        TimeSpan maximumDuration,
        string? mdReqId,
        Func<CancellationToken, Task<bool>>? unsubscribeAsync,
        Func<CancellationToken, Task<bool>>? logoutAsync,
        Action? forceClose,
        Action? disposeStream,
        Action? disposeSocket,
        CancellationToken cancellationToken)
    {
        if (maximumDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(maximumDuration),
                "The cleanup duration must be positive.");

        var startedAtUtc = DateTimeOffset.UtcNow;
        var cleanupDeadlineUtc = Min(
            lifecycleDeadlineUtc,
            startedAtUtc + maximumDuration);
        var diagnostics = new List<string>();
        var unsubscribeAttempted = false;
        var unsubscribeSent = false;
        var logoutAttempted = false;
        var logoutSent = false;
        var forceCloseAttempted = false;
        var forceCloseSucceeded = false;
        var streamDisposeAttempted = false;
        var streamDisposeSucceeded = false;
        var socketDisposeAttempted = false;
        var socketDisposeSucceeded = false;

        if (cleanupDeadlineUtc <= startedAtUtc)
        {
            diagnostics.Add("ARCH7B_MARKET_DATA_CLEANUP_DEADLINE_EXHAUSTED");
        }
        else
        {
            using var cleanupBudget =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cleanupBudget.CancelAfter(cleanupDeadlineUtc - startedAtUtc);

            if (unsubscribeAsync is not null)
            {
                var unsubscribeSlice = logoutAsync is null
                    ? (TimeSpan?)null
                    : TimeSpan.FromTicks(
                        Math.Max(1, (cleanupDeadlineUtc - startedAtUtc).Ticks / 2));
                unsubscribeAttempted = true;
                unsubscribeSent = await TryOperationAsync(
                    "UNSUBSCRIBE",
                    unsubscribeAsync,
                    unsubscribeSlice,
                    cleanupBudget.Token,
                    diagnostics);
            }

            if (logoutAsync is not null)
            {
                logoutAttempted = true;
                logoutSent = await TryOperationAsync(
                    "LOGOUT",
                    logoutAsync,
                    null,
                    cleanupBudget.Token,
                    diagnostics);
            }
        }

        if (forceClose is not null)
        {
            forceCloseAttempted = true;
            try
            {
                forceClose();
                forceCloseSucceeded = true;
            }
            catch (Exception exception)
            {
                diagnostics.Add(
                    $"ARCH7B_MARKET_DATA_FORCE_CLOSE_FAILURE:{SanitizedType(exception)}");
            }
        }

        if (disposeStream is not null)
        {
            streamDisposeAttempted = true;
            try
            {
                disposeStream();
                streamDisposeSucceeded = true;
            }
            catch (Exception exception)
            {
                diagnostics.Add(
                    $"ARCH7B_MARKET_DATA_STREAM_DISPOSE_FAILURE:{SanitizedType(exception)}");
            }
        }

        if (disposeSocket is not null)
        {
            socketDisposeAttempted = true;
            try
            {
                disposeSocket();
                socketDisposeSucceeded = true;
            }
            catch (Exception exception)
            {
                diagnostics.Add(
                    $"ARCH7B_MARKET_DATA_SOCKET_DISPOSE_FAILURE:{SanitizedType(exception)}");
            }
        }

        return new LmaxFixMarketDataCleanupSnapshot(
            unsubscribeAttempted,
            unsubscribeSent,
            unsubscribeAttempted ? mdReqId : null,
            logoutAttempted,
            logoutSent,
            streamDisposeAttempted,
            streamDisposeSucceeded,
            socketDisposeAttempted,
            socketDisposeSucceeded,
            forceCloseAttempted,
            forceCloseSucceeded,
            startedAtUtc,
            DateTimeOffset.UtcNow,
            cleanupDeadlineUtc,
            diagnostics);
    }

    private static async Task<bool> TryOperationAsync(
        string operation,
        Func<CancellationToken, Task<bool>> action,
        TimeSpan? maximumOperationDuration,
        CancellationToken sharedCleanupToken,
        ICollection<string> diagnostics)
    {
        using var operationBudget =
            CancellationTokenSource.CreateLinkedTokenSource(sharedCleanupToken);
        if (maximumOperationDuration is { } duration)
        {
            operationBudget.CancelAfter(duration);
        }

        var operationToken = operationBudget.Token;
        if (operationToken.IsCancellationRequested)
        {
            diagnostics.Add(
                $"ARCH7B_MARKET_DATA_{operation}_TIMEOUT:OperationCanceledException");
            return false;
        }

        try
        {
            var succeeded = await action(operationToken);
            if (!succeeded)
            {
                diagnostics.Add(
                    $"ARCH7B_MARKET_DATA_{operation}_FAILURE:SANITIZED");
            }
            return succeeded;
        }
        catch (OperationCanceledException)
        {
            diagnostics.Add(
                $"ARCH7B_MARKET_DATA_{operation}_TIMEOUT:OperationCanceledException");
            return false;
        }
        catch (TimeoutException)
        {
            diagnostics.Add(
                $"ARCH7B_MARKET_DATA_{operation}_TIMEOUT:TimeoutException");
            return false;
        }
        catch (SocketException)
        {
            diagnostics.Add(
                $"ARCH7B_MARKET_DATA_{operation}_FAILURE:SocketException");
            return false;
        }
        catch (IOException)
        {
            diagnostics.Add(
                $"ARCH7B_MARKET_DATA_{operation}_FAILURE:IOException");
            return false;
        }
        catch (AuthenticationException)
        {
            diagnostics.Add(
                $"ARCH7B_MARKET_DATA_{operation}_FAILURE:AuthenticationException");
            return false;
        }
        catch (ObjectDisposedException)
        {
            diagnostics.Add(
                $"ARCH7B_MARKET_DATA_{operation}_FAILURE:ObjectDisposedException");
            return false;
        }
        catch (Exception)
        {
            diagnostics.Add(
                $"ARCH7B_MARKET_DATA_{operation}_FAILURE:SANITIZED");
            return false;
        }
    }

    private static string SanitizedType(Exception exception)
        => exception switch
        {
            OperationCanceledException => nameof(OperationCanceledException),
            TimeoutException => nameof(TimeoutException),
            SocketException => nameof(SocketException),
            IOException => nameof(IOException),
            AuthenticationException => nameof(AuthenticationException),
            ObjectDisposedException => nameof(ObjectDisposedException),
            _ => "SANITIZED"
        };

    private static DateTimeOffset Min(
        DateTimeOffset left,
        DateTimeOffset right)
        => left <= right ? left : right;
}
