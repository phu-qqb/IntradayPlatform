using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bClockAuthorityFact(
    string Path,
    string FileSha256,
    string SnapshotSha256,
    DateTimeOffset CapturedAtUtc,
    decimal MeasuredOffsetMilliseconds,
    decimal MeasurementUncertaintyMilliseconds,
    int SampleCount,
    string Status);

public sealed partial class Arch7bOneShotLiveExecutionRuntimeV2
{
    private async Task ExecuteClockAuthorityStageAsync(
        Arch7bOneShotStageContract stage,
        Arch7bOneShotLivePlanTemplate template,
        Arch7bOneShotLiveFactStore facts,
        string runRoot,
        Arch7bSlotLock? selectedSlot,
        TimeProvider timeProvider,
        ICollection<string> produced,
        CancellationToken cancellationToken)
    {
        var contract = Arch7bClockFactContracts.RequireProducer(stage.StageId);

        var hostIdentity = Environment.MachineName;
        var production = await clockAuthorityProducer.ProduceAsync(
            runRoot, contract.FileName, hostIdentity, template.IntradayCommit,
            cancellationToken).ConfigureAwait(false);
        var snapshot = production.Snapshot;
        PmsShadowCaptureClockAuthorityValidator.RequireQualified(
            snapshot, timeProvider.GetUtcNow(), hostIdentity, template.IntradayCommit);

        if (stage.StageId == "CLOCK_CAPTURE_START")
        {
            if (selectedSlot is null)
                throw new Arch7bQualificationException(
                    Arch7bV2Blockers.RequiredFactMissing, "selected_slot");
            var preflight = PmsShadowCaptureClockAuthorityStore.Read(Path.Combine(
                runRoot, Arch7bClockFactContracts.RequireProducer("CLOCK_PREFLIGHT").FileName));
            RequireIndependentSameSource(preflight, snapshot);
            if (snapshot.CapturedAtUtc > selectedSlot.SlotStartUtc)
                throw new InvalidDataException(
                    PmsShadowCaptureClockAuthorityContract.Blocker);
        }
        else if (stage.StageId == "CLOCK_POST_CLOSE")
        {
            if (selectedSlot is null)
                throw new Arch7bQualificationException(
                    Arch7bV2Blockers.RequiredFactMissing, "selected_slot");
            var capture = PmsShadowCaptureClockAuthorityStore.Read(Path.Combine(
                runRoot, Arch7bClockFactContracts.RequireProducer("CLOCK_CAPTURE_START").FileName));
            RequireIndependentSameSource(capture, snapshot);
            PmsShadowCaptureClockAuthorityValidator.RequireQualifiedForSlot(
                new(capture, snapshot),
                PmsShadowIntradayCadenceContract.WindowEnding(selectedSlot.SlotEndUtc),
                hostIdentity,
                template.IntradayCommit);
        }

        var fact = new Arch7bClockAuthorityFact(
            production.AbsolutePath,
            production.FileSha256,
            snapshot.SnapshotSha256,
            snapshot.CapturedAtUtc,
            snapshot.MeasuredOffsetMilliseconds,
            snapshot.MeasurementUncertaintyMilliseconds,
            snapshot.SampleCount,
            snapshot.Status);
        produced.Add(facts.Append(contract.FactType, stage.StageId, fact,
            snapshot.SnapshotSha256, snapshot.CapturedAtUtc).FactSha256);
    }

    private static void RequireIndependentSameSource(
        PmsShadowCaptureClockAuthoritySnapshot earlier,
        PmsShadowCaptureClockAuthoritySnapshot later)
    {
        if (later.SnapshotSha256 == earlier.SnapshotSha256 ||
            later.CapturedAtUtc <= earlier.CapturedAtUtc ||
            later.HostClockSource != earlier.HostClockSource ||
            later.ReferenceClockSource != earlier.ReferenceClockSource)
            throw new InvalidDataException(
                PmsShadowCaptureClockAuthorityContract.Blocker);
    }
}
