using System.Diagnostics;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public enum Arch7bCleanupState
{
    Registered,
    Created,
    Cleaning,
    Cleaned,
    Failed
}

public sealed record Arch7bResourceRegistration(
    string ResourceId,
    string ResourceType,
    string OwnerStage,
    string OwnerProcess,
    bool Created,
    string TerminalPolicy,
    string CleanupAction,
    TimeSpan CleanupDeadline,
    bool EvidenceRetention,
    bool Idempotent,
    bool Critical,
    Arch7bCleanupState CleanupState,
    string? CleanupEvidenceSha256,
    string? OwnedPath = null);

public sealed record Arch7bCleanupReport(
    string ContractVersion,
    string? PrimaryBlocker,
    string? CleanupBlocker,
    IReadOnlyList<Arch7bResourceRegistration> Resources,
    IReadOnlyList<string> CleanupOrder,
    bool Complete,
    TimeSpan Elapsed,
    string EvidenceSha256);

public sealed class Arch7bTerminalCleanupSupervisor
{
    public static IReadOnlyList<string> RequiredResourceTypes { get; } =
    [
        "core-prequalification-process-env", "portal-browser-context", "secret-clients", "secret-references",
        "arm-import-child", "armed-state", "owner-lock", "preloaded-lease-process", "lease-marker",
        "bracket-downloader-process", "fast-seal-process", "handoff-child", "position-importer-process",
        "market-data-recorder", "market-data-subscriptions", "pms-importer", "arch7a-child", "set-role-state",
        "reporting-process", "transient-output-roots"
    ];

    private readonly string runRoot;
    private readonly List<ResourceEntry> resources = [];

    public Arch7bTerminalCleanupSupervisor(string runRoot)
    {
        this.runRoot = Path.GetFullPath(runRoot);
    }

    public IReadOnlyList<Arch7bResourceRegistration> Resources => resources.Select(value => value.Value).ToArray();

    public void Register(Arch7bResourceRegistration registration,
        Func<CancellationToken, Task<string>> cleanup)
    {
        if (resources.Any(value => value.Value.ResourceId == registration.ResourceId))
            throw new Arch7bQualificationException(Arch7bBlockers.ResourceNotRegistered,
                $"duplicate resource id {registration.ResourceId}");
        if (registration.Created || registration.CleanupState != Arch7bCleanupState.Registered)
            throw new Arch7bQualificationException(Arch7bBlockers.ResourceNotRegistered,
                "resource must be registered before creation");
        if (registration.OwnedPath is not null) RequireInsideRunRoot(registration.OwnedPath);
        resources.Add(new(registration, cleanup));
    }

    public void MarkCreated(string resourceId)
    {
        var entry = Required(resourceId);
        if (entry.Value.CleanupState != Arch7bCleanupState.Registered)
            throw new Arch7bQualificationException(Arch7bBlockers.ResourceNotRegistered, resourceId);
        entry.Value = entry.Value with { Created = true, CleanupState = Arch7bCleanupState.Created };
    }

    public async Task<string> CleanupOneAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        var entry = Required(resourceId);
        if (entry.Value.CleanupState == Arch7bCleanupState.Cleaned)
            throw new Arch7bQualificationException(Arch7bBlockers.ResourceDoubleCleanup, resourceId);
        return await CleanupEntryAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Arch7bCleanupReport> CleanupAllAsync(string? primaryBlocker = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var order = new List<string>();
        var failures = new List<string>();
        using var global = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        global.CancelAfter(TimeSpan.FromSeconds(Arch7bGlobalSloRegistry.GlobalTerminalCleanupDeadlineSeconds));
        foreach (var entry in resources.AsEnumerable().Reverse())
        {
            if (!entry.Value.Created || entry.Value.CleanupState == Arch7bCleanupState.Cleaned) continue;
            order.Add(entry.Value.ResourceId);
            try
            {
                await CleanupEntryAsync(entry, global.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add($"{entry.Value.ResourceId}:{exception.GetType().Name}");
            }
        }
        stopwatch.Stop();
        if (stopwatch.Elapsed > TimeSpan.FromSeconds(Arch7bGlobalSloRegistry.GlobalTerminalCleanupDeadlineSeconds))
            failures.Add(Arch7bBlockers.CleanupDeadlineExceeded);
        var incomplete = resources.Any(value => value.Value.Created && value.Value.CleanupState != Arch7bCleanupState.Cleaned);
        var cleanupBlocker = failures.Count == 0 && !incomplete ? null : Arch7bBlockers.TerminalCleanupIncomplete;
        var canonical = string.Join('\n', Arch7bOneShotContracts.TerminalCleanupSupervisorVersion,
            primaryBlocker ?? string.Empty, cleanupBlocker ?? string.Empty, string.Join(',', order),
            string.Join(',', resources.Select(value => $"{value.Value.ResourceId}:{value.Value.CleanupState}:{value.Value.CleanupEvidenceSha256}")));
        return new(Arch7bOneShotContracts.TerminalCleanupSupervisorVersion, primaryBlocker, cleanupBlocker,
            Resources, order, cleanupBlocker is null, stopwatch.Elapsed, Arch7bOneShotContracts.Sha256(canonical));
    }

    public void ValidateNoResidue(Func<Arch7bResourceRegistration, bool> residuePredicate)
    {
        foreach (var resource in Resources.Where(value => residuePredicate(value)))
        {
            var blocker = resource.ResourceType.Contains("marker", StringComparison.OrdinalIgnoreCase)
                ? Arch7bBlockers.MarkerResidual : Arch7bBlockers.ChildProcessResidual;
            throw new Arch7bQualificationException(blocker, resource.ResourceId);
        }
    }

    public void RequireInsideRunRoot(string path)
    {
        var candidate = Path.GetFullPath(path);
        var prefix = runRoot.EndsWith(Path.DirectorySeparatorChar) ? runRoot : runRoot + Path.DirectorySeparatorChar;
        if (!candidate.Equals(runRoot, StringComparison.OrdinalIgnoreCase) &&
            !candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new Arch7bQualificationException(Arch7bBlockers.CleanupPathOutsideRunRoot, candidate);
    }

    private async Task<string> CleanupEntryAsync(ResourceEntry entry, CancellationToken cancellationToken)
    {
        if (!entry.Value.Created)
            throw new Arch7bQualificationException(Arch7bBlockers.ResourceNotRegistered, entry.Value.ResourceId);
        entry.Value = entry.Value with { CleanupState = Arch7bCleanupState.Cleaning };
        try
        {
            using var local = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            local.CancelAfter(entry.Value.CleanupDeadline);
            var evidence = await entry.Cleanup(local.Token).ConfigureAwait(false);
            var evidenceSha = Arch7bOneShotContracts.IsSha256(evidence)
                ? evidence : Arch7bOneShotContracts.Sha256(evidence);
            entry.Value = entry.Value with { CleanupState = Arch7bCleanupState.Cleaned, CleanupEvidenceSha256 = evidenceSha };
            return evidenceSha;
        }
        catch
        {
            entry.Value = entry.Value with { CleanupState = Arch7bCleanupState.Failed };
            throw;
        }
    }

    private ResourceEntry Required(string resourceId) => resources.SingleOrDefault(value => value.Value.ResourceId == resourceId)
        ?? throw new Arch7bQualificationException(Arch7bBlockers.ResourceNotRegistered, resourceId);

    private sealed class ResourceEntry(Arch7bResourceRegistration value,
        Func<CancellationToken, Task<string>> cleanup)
    {
        public Arch7bResourceRegistration Value { get; set; } = value;
        public Func<CancellationToken, Task<string>> Cleanup { get; } = cleanup;
    }
}
