using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

public sealed class DeferredArch7bPostgreSqlFixLifecycleObserver(
    Func<Arch7bPostgreSqlFixLifecycleObserver> observerFactory) :
    ILmaxFixArch7bLifecycleObserver,
    ILmaxFixArch7bQualificationSession
{
    private readonly Lazy<Arch7bPostgreSqlFixLifecycleObserver> inner =
        new(observerFactory, LazyThreadSafetyMode.ExecutionAndPublication);

    public bool IsDurable => true;

    public Task InitializeAsync(
        LmaxFixArch7bKnownOrderRequest request,
        CancellationToken cancellationToken)
        => inner.Value.InitializeAsync(request, cancellationToken);

    public Task<LmaxFixArch7bRecoveryState> LoadRecoveryStateAsync(
        LmaxFixArch7bKnownOrderRequest request,
        CancellationToken cancellationToken)
        => inner.Value.LoadRecoveryStateAsync(request, cancellationToken);

    public Task RecordSessionEventAsync(
        LmaxFixArch7bKnownOrderRequest request,
        string eventType,
        long? fixSequenceNumber,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
        => inner.Value.RecordSessionEventAsync(
            request,
            eventType,
            fixSequenceNumber,
            occurredAtUtc,
            cancellationToken);

    public Task RecordSendIntentAsync(
        LmaxFixArch7bKnownOrderRequest request,
        LmaxFixArch7bOutboundIntent intent,
        CancellationToken cancellationToken)
        => inner.Value.RecordSendIntentAsync(
            request,
            intent,
            cancellationToken);

    public Task RecordExecutionReportAsync(
        LmaxFixArch7bKnownOrderRequest request,
        LmaxFixExecutionReport report,
        CancellationToken cancellationToken)
        => inner.Value.RecordExecutionReportAsync(
            request,
            report,
            cancellationToken);

    public Task<decimal> ReadValidatedFillQuantityAsync(
        LmaxFixArch7bKnownOrderRequest request,
        string clientOrderId,
        CancellationToken cancellationToken)
        => inner.Value.ReadValidatedFillQuantityAsync(
            request,
            clientOrderId,
            cancellationToken);

    public Task<Arch7bLifecycleEvaluation> FinalizeReconciliationAsync(
        LmaxFixArch7bKnownOrderRequest request,
        CancellationToken cancellationToken)
        => inner.Value.FinalizeReconciliationAsync(request, cancellationToken);

    public ValueTask CompleteAsync(
        LmaxFixArch7bKnownOrderRequest request,
        CancellationToken cancellationToken)
        => inner.IsValueCreated
            ? inner.Value.CompleteAsync(request, cancellationToken)
            : ValueTask.CompletedTask;
}

public sealed class Arch7bPostgreSqlFixLifecycleObserver(
    IDbContextFactory<PmsShadowDbContext> contextFactory,
    EfArch7bKnownOrderLifecycleStore store) :
    ILmaxFixArch7bLifecycleObserver,
    ILmaxFixArch7bQualificationSession
{
    private Arch7bPostgreSqlExclusiveLease? lease;
    private string? activeFixSessionId;

    public bool IsDurable => true;

    public async Task InitializeAsync(
        LmaxFixArch7bKnownOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (lease is not null)
            throw new InvalidOperationException("ARCH7B_QUALIFICATION_SESSION_ALREADY_INITIALIZED");
        var now = DateTimeOffset.UtcNow;
        activeFixSessionId =
            $"A7B-{request.QualificationRunId:N}-{now.ToUnixTimeMilliseconds()}";
        var duration = request.DeadlineUtc - now;
        lease = await Arch7bPostgreSqlExclusiveLease.AcquireAsync(
            contextFactory,
            request.OwnerId,
            now,
            duration,
            cancellationToken);
        var exclusivity = new Arch7bExclusivityDeclaration(
            request.OwnerId,
            request.RegisteredAtUtc,
            request.DeadlineUtc,
            AdvisoryLeaseHeld: true,
            NoManualOrdersDeclared: request.ExclusivityDeclared,
            NoOtherBotDeclared: request.ExclusivityDeclared,
            NoOtherGatewayDeclared: request.ExclusivityDeclared,
            NoOtherUserDeclared: request.ExclusivityDeclared,
            NoOtherTestDeclared: request.ExclusivityDeclared,
            NoConcurrentFixOrderEntrySessionDeclared: request.ExclusivityDeclared,
            OneRunOnly: request.ExclusivityDeclared,
            OneDemoAccountOnly: request.ExclusivityDeclared);
        try
        {
            var snapshot = await new EfArch7bPostgreSqlPreflightReader(contextFactory)
                .ReadAsync(
                    request.ChildOrderId,
                    request.QualificationRunId,
                    now,
                    cancellationToken);
            if (snapshot.PlatformKnownWorkingOrderCount != 0)
                throw new InvalidOperationException("ARCH7B_PLATFORM_KNOWN_WORKING_ORDER_PRESENT");

            var preflight = snapshot.ExistingRun is not null &&
                            snapshot.OpeningSendIntentExists
                ? RecoveryPreflight(snapshot.ExistingRun, request)
                : Arch7bKnownOrderQualification.EvaluatePreflight(new(
                    snapshot.ChildOrder,
                    new(
                        Arch7bKnownOrderQualificationPolicy.Symbol,
                        Arch7bKnownOrderQualificationPolicy.SecurityId,
                        request.BboBid,
                        request.BboAsk,
                        request.BboObservedAtUtc,
                        request.BboSource,
                        request.BboSnapshotSha256,
                        request.BboAcquisitionStartedAtUtc,
                        request.BboSequenceIntegrityProven,
                        request.BboPolygonUsed),
                    exclusivity,
                    Arch7bKnownOrderQualificationPolicy.Environment,
                    request.AccountId,
                    snapshot.CurrentKnownPosition,
                    snapshot.PlatformKnownWorkingOrderCount,
                    request.ExactOperatorAuthorizationPresent,
                    request.KillSwitchArmed,
                    now));
            ValidatePreflightBinding(preflight, request);
            await store.RegisterRunAsync(
                new(
                    request.QualificationRunId,
                    request.ChildOrderId,
                    preflight,
                    request.AuthorizationPacketSha256,
                    exclusivity,
                    request.RegisteredAtUtc),
                cancellationToken);
        }
        catch
        {
            await lease.DisposeAsync();
            lease = null;
            throw;
        }
    }

    public async Task<LmaxFixArch7bRecoveryState> LoadRecoveryStateAsync(
        LmaxFixArch7bKnownOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (lease is null)
            throw new InvalidOperationException("ARCH7B_QUALIFICATION_SESSION_NOT_INITIALIZED");
        var state = await store.ReadRecoveryStateAsync(
            request.QualificationRunId,
            cancellationToken);
        return new(
            state.OpeningSendIntentExists,
            state.CancelSendIntentExists,
            state.FlattenSendIntentExists,
            state.OrderStatusRequestCount,
            state.OpeningCumulativeQuantity,
            state.OpeningLeavesQuantity,
            state.OpeningTerminal,
            state.FlattenCumulativeQuantity,
            state.FlattenLeavesQuantity,
            state.FlattenTerminal,
            state.OpeningMarketObservationId,
            state.FlattenMarketObservationId);
    }

    public Task RecordSessionEventAsync(
        LmaxFixArch7bKnownOrderRequest request,
        string eventType,
        long? fixSequenceNumber,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var eventSha = Sha256(string.Join("|",
            request.QualificationRunId.ToString("D"),
            ActiveFixSessionId(),
            eventType,
            fixSequenceNumber?.ToString() ?? string.Empty,
            occurredAtUtc.ToUniversalTime().ToString("O")));
        return Persist(store.RecordFixSessionEventAsync(
            new(
                DeterministicGuid($"arch7b-fix-session-event|{eventSha}"),
                request.QualificationRunId,
                ActiveFixSessionId(),
                eventType,
                fixSequenceNumber,
                eventSha,
                occurredAtUtc.ToUniversalTime()),
            cancellationToken));
    }

    public Task RecordSendIntentAsync(
        LmaxFixArch7bKnownOrderRequest request,
        LmaxFixArch7bOutboundIntent intent,
        CancellationToken cancellationToken)
    {
        if (intent.QualificationRunId != request.QualificationRunId)
            throw new InvalidOperationException("ARCH7B_SEND_INTENT_RUN_ID_MISMATCH");
        var identity = string.Join("|",
            intent.QualificationRunId.ToString("D"),
            intent.LifecycleRole,
            intent.MessageType,
            intent.ClientOrderId,
            intent.OriginalClientOrderId ?? string.Empty,
            intent.PayloadSha256);
        return Persist(store.RecordSendIntentAsync(
            new(
                DeterministicGuid($"arch7b-send-intent|{identity}"),
                intent.QualificationRunId,
                intent.LifecycleRole,
                intent.MessageType,
                intent.ClientOrderId,
                intent.OriginalClientOrderId,
                intent.Side,
                intent.Quantity,
                intent.LimitPrice,
                intent.BboSnapshotSha256,
                intent.PayloadSha256,
                intent.IntentRecordedAtUtc.ToUniversalTime()),
            cancellationToken));
    }

    public Task RecordExecutionReportAsync(
        LmaxFixArch7bKnownOrderRequest request,
        LmaxFixExecutionReport report,
        CancellationToken cancellationToken)
        => Persist(store.PersistExecutionReportsAsync(
            request.QualificationRunId,
            [LmaxFixArch7bReportMapper.Map(
                request, report, ActiveFixSessionId())],
            cancellationToken));

    public Task<decimal> ReadValidatedFillQuantityAsync(
        LmaxFixArch7bKnownOrderRequest request,
        string clientOrderId,
        CancellationToken cancellationToken)
        => store.ReadValidatedFillQuantityAsync(
            request.QualificationRunId,
            clientOrderId,
            cancellationToken);

    public async Task<Arch7bLifecycleEvaluation> FinalizeReconciliationAsync(
        LmaxFixArch7bKnownOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (lease is null)
            throw new InvalidOperationException("ARCH7B_QUALIFICATION_SESSION_NOT_INITIALIZED");
        var lifecycle = await store.ReadLifecycleEvaluationAsync(
            request.QualificationRunId,
            cancellationToken);
        if (!lifecycle.Qualified || !lifecycle.Flat)
            throw new InvalidOperationException(
                lifecycle.Issues.FirstOrDefault() ?? "ARCH7B_FINAL_RECONCILIATION_NOT_FLAT");
        const string authority = "LMAX_FIX_EXECUTION_REPORTS_KNOWN_ORDERS";
        var completedAtUtc = lifecycle.AcceptedExecutionReports
            .Max(value => value.TransactTimeUtc);
        var evidenceSha = Sha256(string.Join("|",
            request.QualificationRunId.ToString("D"),
            lifecycle.EvaluationSha256,
            authority,
            "0"));
        await store.PersistFinalReconciliationAsync(
            new(
                DeterministicGuid($"arch7b-final-reconciliation|{evidenceSha}"),
                request.QualificationRunId,
                lifecycle,
                authority,
                0m,
                evidenceSha,
                completedAtUtc),
            cancellationToken);
        return lifecycle;
    }

    public async ValueTask CompleteAsync(
        LmaxFixArch7bKnownOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (lease is null)
            return;
        var activeLease = lease;
        lease = null;
        try
        {
            await activeLease.DisposeAsync();
        }
        catch when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static Arch7bPreflightDecision RecoveryPreflight(
        PmsArch7bQualificationRunRow existing,
        LmaxFixArch7bKnownOrderRequest request)
    {
        if (existing.ChildOrderId != request.ChildOrderId ||
            existing.AuthorizationPacketSha256 != request.AuthorizationPacketSha256 ||
            existing.AccountId != request.AccountId ||
            existing.OpeningClientOrderId != request.OpeningClientOrderId ||
            existing.CancelClientOrderId != request.CancelClientOrderId ||
            existing.FlattenClientOrderId != request.FlattenClientOrderId ||
            existing.PolicySha256 != request.PolicySha256)
        {
            throw new InvalidOperationException("ARCH7B_RECOVERY_PACKET_BINDING_CONFLICT");
        }

        return new(
            true,
            [],
            existing.OpeningClientOrderId,
            existing.FlattenClientOrderId,
            existing.CancelClientOrderId,
            request.OpeningLimitPrice,
            request.MaximumOpeningPrice,
            request.MinimumOpeningPrice,
            existing.PolicySha256);
    }

    private static void ValidatePreflightBinding(
        Arch7bPreflightDecision preflight,
        LmaxFixArch7bKnownOrderRequest request)
    {
        if (!preflight.Allowed || preflight.Blockers.Count != 0)
            throw new InvalidOperationException(
                preflight.Blockers.FirstOrDefault() ?? "ARCH7B_PREFLIGHT_NOT_ALLOWED");
        if (preflight.OpeningClientOrderId != request.OpeningClientOrderId ||
            preflight.CancelClientOrderId != request.CancelClientOrderId ||
            preflight.FlattenClientOrderId != request.FlattenClientOrderId ||
            preflight.OpeningLimitPrice != request.OpeningLimitPrice ||
            preflight.MinimumOpeningPrice != request.MinimumOpeningPrice ||
            preflight.MaximumOpeningPrice != request.MaximumOpeningPrice ||
            preflight.PolicySha256 != request.PolicySha256 ||
            request.OpeningLimitPrice != request.BboAsk)
        {
            throw new InvalidOperationException("ARCH7B_AUTHORIZED_ECONOMICS_BINDING_CONFLICT");
        }
    }

    private static async Task Persist(Task<Arch7bPostgreSqlWriteResult> persistence)
        => _ = await persistence;

    private string ActiveFixSessionId()
        => activeFixSessionId ??
           throw new InvalidOperationException("ARCH7B_FIX_SESSION_INSTANCE_NOT_INITIALIZED");

    private static Guid DeterministicGuid(string identity)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes(identity))[..16]);

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
