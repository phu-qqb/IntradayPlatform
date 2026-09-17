namespace QQ.Production.Intraday.Application;

/// <summary>The real account has one fixed journal directory. A new filename
/// cannot evade an unclosed or damaged prior journal.</summary>
public sealed class LmaxDemoSessionOwnership : IDisposable
{
    public const string RealAccountRoot = @"D:\data\lmax-demo-session\1754288005";
    private readonly FileStream ownerLock;
    public LmaxDemoSessionJournal Journal { get; }
    public LmaxDemoControlledSession Session { get; }
    private LmaxDemoSessionOwnership(FileStream ownerLock, LmaxDemoSessionJournal journal, LmaxDemoControlledSession session)
        => (this.ownerLock, Journal, Session) = (ownerLock, journal, session);

    public static LmaxDemoSessionOwnership Begin(LmaxDemoSessionStart start, DateTimeOffset now, string? simulatedRoot = null)
    {
        if (start.Simulated != (simulatedRoot is not null)) throw new InvalidOperationException("DEMO_SESSION_STORAGE_MODE_MISMATCH");
        if (string.IsNullOrWhiteSpace(start.SessionId) || start.SessionId.Length > 100
            || start.SessionId.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
            throw new InvalidOperationException("DEMO_SESSION_ID_INVALID");
        var root = simulatedRoot ?? RealAccountRoot;
        Directory.CreateDirectory(root);
        var lease = new FileStream(Path.Combine(root, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        LmaxDemoSessionJournal? journal = null;
        try
        {
            foreach (var path in Directory.EnumerateFiles(root, "*.journal.jsonl", SearchOption.TopDirectoryOnly))
            {
                using var prior = LmaxDemoSessionJournal.OpenForInspection(path);
                var priorSession = LmaxDemoControlledSession.Inspect(prior);
                if (priorSession.Start.AccountId != start.AccountId
                    || !priorSession.IsClosed && !LmaxDemoNoSendRetirement.IsValidated(path, prior, now, start.Simulated)
                        && !LmaxDemoUnsentParentRetirement.IsValidated(path, prior, now, start.Simulated))
                    throw new InvalidOperationException("DEMO_SESSION_PRIOR_ACCOUNT_OWNER_UNRESOLVED");
            }
            journal = LmaxDemoSessionJournal.CreateNew(Path.Combine(root, start.SessionId + ".journal.jsonl"));
            var session = LmaxDemoControlledSession.Begin(journal, start, now);
            return new(lease, journal, session);
        }
        catch { journal?.Dispose(); lease.Dispose(); throw; }
    }
    public void Dispose() { Journal.Dispose(); ownerLock.Dispose(); }
}
