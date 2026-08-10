using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bProductionClockQualificationRun(
    int RunNumber,
    string RunRoot,
    string PreflightPath,
    string PreflightFileSha256,
    string PreflightSnapshotSha256,
    string CapturePath,
    string CaptureFileSha256,
    string CaptureSnapshotSha256,
    string PostClosePath,
    string PostCloseFileSha256,
    string PostCloseSnapshotSha256,
    DateTimeOffset SlotStartUtc,
    DateTimeOffset SlotEndUtc,
    decimal CaptureOffsetMilliseconds,
    decimal PostCloseOffsetMilliseconds,
    decimal MaximumUncertaintyMilliseconds,
    int IndividualValidationCount,
    int PairValidationCount,
    string EvidenceSha256);

public sealed record Arch7bProductionClockQualification(
    string ContractVersion,
    string Status,
    bool QualificationOnly,
    string OutputRoot,
    string HostIdentity,
    string RepositoryCommit,
    string HostClockSource,
    string ReferenceClockSource,
    int RunCount,
    int PreflightValidationCount,
    int CaptureValidationCount,
    int PostCloseValidationCount,
    int PairValidationCount,
    IReadOnlyList<Arch7bProductionClockQualificationRun> Runs,
    string EvidenceSha256);

public sealed class Arch7bProductionClockAuthorityQualifier(
    IPmsShadowCaptureClockAuthorityProducer producer,
    TimeProvider timeProvider)
{
    public const string Version = "arch7b_production_clock_authority_qualification_v1";
    public const int RequiredRunCount = 3;

    public Arch7bProductionClockAuthorityQualifier()
        : this(new PmsShadowCaptureClockAuthorityProducer(), TimeProvider.System)
    {
    }

    public async Task<Arch7bProductionClockQualification> RunAsync(
        string outputRoot,
        string repositoryCommit,
        CancellationToken cancellationToken = default)
    {
        outputRoot = Path.GetFullPath(outputRoot);
        if (Directory.Exists(outputRoot) &&
            Directory.EnumerateFileSystemEntries(outputRoot).Any())
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.AuthorityBindingMismatch, "clock-output-root-not-empty");
        Directory.CreateDirectory(outputRoot);

        var hostIdentity = Environment.MachineName;
        var runs = new List<Arch7bProductionClockQualificationRun>(RequiredRunCount);
        for (var runNumber = 1; runNumber <= RequiredRunCount; runNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var runRoot = Path.Combine(outputRoot, $"run-{runNumber:D3}");
            Directory.CreateDirectory(runRoot);
            var preflight = await producer.ProduceAsync(runRoot,
                "clock_authority_preflight.json", hostIdentity, repositoryCommit,
                cancellationToken).ConfigureAwait(false);
            var capture = await producer.ProduceAsync(runRoot,
                "clock_authority_capture.json", hostIdentity, repositoryCommit,
                cancellationToken).ConfigureAwait(false);

            var slotStartUtc = capture.Snapshot.CapturedAtUtc;
            var slotEndUtc = Later(slotStartUtc, timeProvider.GetUtcNow());
            var postClose = await producer.ProduceAsync(runRoot,
                "clock_authority_post_close.json", hostIdentity, repositoryCommit,
                cancellationToken).ConfigureAwait(false);

            RequireIndividual(preflight.Snapshot, timeProvider.GetUtcNow(), hostIdentity,
                repositoryCommit);
            RequireIndividual(capture.Snapshot, slotStartUtc, hostIdentity,
                repositoryCommit);
            RequireIndividual(postClose.Snapshot, postClose.Snapshot.CapturedAtUtc,
                hostIdentity, repositoryCommit);
            RequirePreflightCapturePair(preflight.Snapshot, capture.Snapshot);
            var slot = new PmsShadowIntradaySlotWindow(
                $"arch7b-clock-qualification-{runNumber:D3}", slotStartUtc, slotEndUtc,
                DateOnly.FromDateTime(slotStartUtc.UtcDateTime));
            PmsShadowCaptureClockAuthorityValidator.RequireQualifiedForSlot(
                new(capture.Snapshot, postClose.Snapshot), slot, hostIdentity,
                repositoryCommit);

            var provisional = new Arch7bProductionClockQualificationRun(
                runNumber, runRoot,
                preflight.AbsolutePath, preflight.FileSha256,
                preflight.Snapshot.SnapshotSha256,
                capture.AbsolutePath, capture.FileSha256,
                capture.Snapshot.SnapshotSha256,
                postClose.AbsolutePath, postClose.FileSha256,
                postClose.Snapshot.SnapshotSha256,
                slotStartUtc, slotEndUtc,
                capture.Snapshot.MeasuredOffsetMilliseconds,
                postClose.Snapshot.MeasuredOffsetMilliseconds,
                Math.Max(capture.Snapshot.MeasurementUncertaintyMilliseconds,
                    postClose.Snapshot.MeasurementUncertaintyMilliseconds),
                3, 1, string.Empty);
            runs.Add(provisional with
            {
                EvidenceSha256 = Arch7bOneShotContracts.Sha256(Canonical(provisional))
            });
        }

        var evidence = Arch7bOneShotContracts.Sha256(string.Join('\n', Version,
            outputRoot, hostIdentity, repositoryCommit,
            PmsShadowCaptureClockAuthorityMeasurementContract.HostClockSource,
            PmsShadowCaptureClockAuthorityMeasurementContract.ReferenceClockSource,
            string.Join('|', runs.Select(value => value.EvidenceSha256))));
        return new(Version, "PASS", true, outputRoot, hostIdentity, repositoryCommit,
            PmsShadowCaptureClockAuthorityMeasurementContract.HostClockSource,
            PmsShadowCaptureClockAuthorityMeasurementContract.ReferenceClockSource,
            runs.Count, runs.Count, runs.Count, runs.Count, runs.Count, runs, evidence);
    }

    private static DateTimeOffset Later(DateTimeOffset first, DateTimeOffset second) =>
        second > first ? second : first;

    private static void RequireIndividual(PmsShadowCaptureClockAuthoritySnapshot snapshot,
        DateTimeOffset observedAtUtc, string hostIdentity, string repositoryCommit) =>
        PmsShadowCaptureClockAuthorityValidator.RequireQualified(
            snapshot, observedAtUtc, hostIdentity, repositoryCommit);

    private static void RequirePreflightCapturePair(
        PmsShadowCaptureClockAuthoritySnapshot preflight,
        PmsShadowCaptureClockAuthoritySnapshot capture)
    {
        if (preflight.SnapshotSha256 == capture.SnapshotSha256 ||
            preflight.HostClockSource != capture.HostClockSource ||
            preflight.ReferenceClockSource != capture.ReferenceClockSource ||
            capture.CapturedAtUtc < preflight.CapturedAtUtc)
            throw new Arch7bQualificationException(
                PmsShadowCaptureClockAuthorityContract.Blocker,
                "preflight-capture-pair");
    }

    private static string Canonical(Arch7bProductionClockQualificationRun value) =>
        string.Join('\n', Version, value.RunNumber, value.RunRoot,
            value.PreflightPath, value.PreflightFileSha256,
            value.PreflightSnapshotSha256, value.CapturePath,
            value.CaptureFileSha256, value.CaptureSnapshotSha256,
            value.PostClosePath, value.PostCloseFileSha256,
            value.PostCloseSnapshotSha256, value.SlotStartUtc.ToString("O"),
            value.SlotEndUtc.ToString("O"), value.CaptureOffsetMilliseconds,
            value.PostCloseOffsetMilliseconds, value.MaximumUncertaintyMilliseconds,
            value.IndividualValidationCount, value.PairValidationCount);
}
