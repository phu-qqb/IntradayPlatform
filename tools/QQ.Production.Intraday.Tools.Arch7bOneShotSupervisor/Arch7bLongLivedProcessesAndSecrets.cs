using System.Diagnostics;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public enum Arch7bLongLivedProcessState
{
    Registered,
    Starting,
    Running,
    Ready,
    Signalled,
    Completed,
    Terminated,
    Cleaned
}

public sealed record Arch7bLongLivedProcessEvidence(
    string ContractVersion,
    string ProcessKey,
    string CommandId,
    string OwnerStage,
    int ProcessId,
    string ExecutableSha256,
    DateTimeOffset StartedAtUtc,
    string ExpectedReadyEvidence,
    string? ReadyEvidenceSha256,
    IReadOnlyList<string> AllowedSignals,
    string TerminalStage,
    string CleanupResourceId,
    Arch7bLongLivedProcessState State,
    string EvidenceSha256);

public sealed class Arch7bOneShotLongLivedProcessRegistry
{
    private sealed class Entry(Arch7bLongLivedProcessEvidence evidence, Process process)
    {
        public Arch7bLongLivedProcessEvidence Evidence { get; set; } = evidence;
        public Process Process { get; } = process;
    }

    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    public IReadOnlyList<Arch7bLongLivedProcessEvidence> Evidence =>
        entries.Values.Select(value => value.Evidence).OrderBy(value => value.ProcessKey, StringComparer.Ordinal).ToArray();

    public void Register(string processKey, Arch7bOneShotMaterializedCommand command, Process process,
        string expectedReadyEvidence, IReadOnlyList<string> allowedSignals, string terminalStage,
        string cleanupResourceId, DateTimeOffset startedAtUtc)
    {
        if (entries.ContainsKey(processKey))
            throw new Arch7bQualificationException(Arch7bV2Blockers.DuplicateProcessKey, processKey);
        if (process.HasExited)
            throw new Arch7bQualificationException(Arch7bV2Blockers.LongLivedProcessExited, processKey);
        var value = Build(processKey, command, process, expectedReadyEvidence, null, allowedSignals,
            terminalStage, cleanupResourceId, Arch7bLongLivedProcessState.Running, startedAtUtc);
        entries.Add(processKey, new Entry(value, process));
    }

    public Arch7bLongLivedProcessEvidence MarkReady(string processKey, string readyEvidenceSha256)
    {
        var entry = Require(processKey);
        if (entry.Evidence.State != Arch7bLongLivedProcessState.Running || entry.Process.HasExited ||
            !Arch7bOneShotContracts.IsSha256(readyEvidenceSha256))
            throw new Arch7bQualificationException(Arch7bV2Blockers.LongLivedProcessStateInvalid, processKey);
        entry.Evidence = Rehash(entry.Evidence with
        {
            ReadyEvidenceSha256 = readyEvidenceSha256,
            State = Arch7bLongLivedProcessState.Ready
        });
        return entry.Evidence;
    }

    public void AssertReadyAndAlive(string processKey)
    {
        var entry = Require(processKey);
        if (entry.Evidence.State is not (Arch7bLongLivedProcessState.Ready or Arch7bLongLivedProcessState.Signalled) ||
            entry.Process.HasExited)
            throw new Arch7bQualificationException(Arch7bV2Blockers.LongLivedProcessExited, processKey);
    }

    public void Signal(string processKey, string signal)
    {
        var entry = Require(processKey);
        AssertReadyAndAlive(processKey);
        if (!entry.Evidence.AllowedSignals.Contains(signal, StringComparer.Ordinal))
            throw new Arch7bQualificationException(Arch7bV2Blockers.ProcessSignalForbidden, signal);
        entry.Evidence = Rehash(entry.Evidence with { State = Arch7bLongLivedProcessState.Signalled });
    }

    public async Task StopAsync(string processKey, CancellationToken cancellationToken = default)
    {
        var entry = Require(processKey);
        if (!entry.Process.HasExited)
        {
            entry.Process.Kill(true);
            await entry.Process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            entry.Evidence = Rehash(entry.Evidence with { State = Arch7bLongLivedProcessState.Terminated });
        }
        else
        {
            entry.Evidence = Rehash(entry.Evidence with { State = Arch7bLongLivedProcessState.Completed });
        }
        entry.Evidence = Rehash(entry.Evidence with { State = Arch7bLongLivedProcessState.Cleaned });
    }

    public int ResidualCount => entries.Values.Count(value =>
        value.Evidence.State != Arch7bLongLivedProcessState.Cleaned && !value.Process.HasExited);

    private Entry Require(string processKey) => entries.TryGetValue(processKey, out var entry)
        ? entry : throw new Arch7bQualificationException(Arch7bV2Blockers.LongLivedProcessStateInvalid, processKey);

    private static Arch7bLongLivedProcessEvidence Build(string processKey,
        Arch7bOneShotMaterializedCommand command, Process process, string expectedReadyEvidence,
        string? readyEvidenceSha256, IReadOnlyList<string> allowedSignals, string terminalStage,
        string cleanupResourceId, Arch7bLongLivedProcessState state, DateTimeOffset startedAtUtc) =>
        Rehash(new(Arch7bV2Contracts.LongLivedProcessRegistryVersion, processKey, command.CommandId,
            command.StageId, process.Id, command.ExecutableSha256, startedAtUtc, expectedReadyEvidence,
            readyEvidenceSha256, allowedSignals, terminalStage, cleanupResourceId, state, string.Empty));

    private static Arch7bLongLivedProcessEvidence Rehash(Arch7bLongLivedProcessEvidence value)
    {
        var canonical = string.Join('\n', value.ContractVersion, value.ProcessKey, value.CommandId,
            value.OwnerStage, value.ProcessId, value.ExecutableSha256, value.StartedAtUtc.ToString("O"),
            value.ExpectedReadyEvidence, value.ReadyEvidenceSha256 ?? string.Empty,
            string.Join('|', value.AllowedSignals), value.TerminalStage, value.CleanupResourceId, value.State);
        return value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(canonical) };
    }
}

public sealed record Arch7bSecretEnvironmentLease(
    string ContractVersion,
    string CommandId,
    IReadOnlyDictionary<string, string> Values,
    int SecretValueCount,
    bool Released);

public interface IArch7bOneShotSecretLease
{
    Arch7bSecretEnvironmentLease Acquire(string commandId, IReadOnlyList<string> variableNames,
        bool bracketStarted);
    void Release(Arch7bSecretEnvironmentLease lease);
    int ReadCount { get; }
}

public sealed class Arch7bCoreOwnedSecretLease : IArch7bOneShotSecretLease
{
    public int ReadCount => 0;

    public Arch7bSecretEnvironmentLease Acquire(string commandId, IReadOnlyList<string> variableNames,
        bool bracketStarted)
    {
        if (variableNames.Count != 0)
            throw new Arch7bQualificationException(Arch7bV2Blockers.SecretCommandScopeMismatch, commandId);
        return new(Arch7bV2Contracts.SecretEnvironmentInjectionVersion, commandId,
            new Dictionary<string, string>(), 0, false);
    }

    public void Release(Arch7bSecretEnvironmentLease lease) { }
}

public sealed class Arch7bScopedSecretLease : IArch7bOneShotSecretLease, IDisposable
{
    private readonly Dictionary<string, Dictionary<string, string>> commandValues;
    private bool disposed;

    public Arch7bScopedSecretLease(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> values)
    {
        commandValues = values.ToDictionary(pair => pair.Key,
            pair => pair.Value.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    public int ReadCount { get; private set; }

    public Arch7bSecretEnvironmentLease Acquire(string commandId, IReadOnlyList<string> variableNames,
        bool bracketStarted)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (bracketStarted) throw new Arch7bQualificationException(Arch7bBlockers.SecretReadAfterBracket);
        if (!commandValues.TryGetValue(commandId, out var values))
            throw new Arch7bQualificationException(Arch7bV2Blockers.SecretLeaseMissing, commandId);
        if (!variableNames.Order(StringComparer.Ordinal).SequenceEqual(values.Keys.Order(StringComparer.Ordinal),
                StringComparer.Ordinal))
            throw new Arch7bQualificationException(Arch7bV2Blockers.SecretCommandScopeMismatch, commandId);
        ReadCount++;
        if (ReadCount > Arch7bOneShotContracts.MaximumRdsReads)
            throw new Arch7bQualificationException(Arch7bBlockers.RdsReadLimitExceeded);
        return new(Arch7bV2Contracts.SecretEnvironmentInjectionVersion, commandId,
            new Dictionary<string, string>(values, StringComparer.Ordinal), values.Count, false);
    }

    public void Release(Arch7bSecretEnvironmentLease lease)
    {
        if (lease.Values is Dictionary<string, string> values) values.Clear();
    }

    public void Dispose()
    {
        foreach (var values in commandValues.Values) values.Clear();
        commandValues.Clear();
        disposed = true;
    }
}
