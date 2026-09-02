using QQ.Production.Intraday.Application.CanonicalRecorder;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

/// <summary>
/// Persists the latest valid read-only LMAX BBO for each enabled execution instrument
/// from one finalized canonical recorder run. This is deliberately a bounded handoff,
/// not a market-data subscription, scheduler, or order-entry component.
/// </summary>
public sealed record LmaxCanonicalSnapshotIngestionRequest(
    string CaptureRunRoot,
    string ExpectedFinalManifestSha256,
    DateTimeOffset DecisionAtUtc,
    TimeSpan MaximumSourceAge);

public sealed record LmaxCanonicalSnapshotIngestionResult(
    string RecorderRunId,
    string FinalManifestSha256,
    int ImportedSnapshotCount,
    int AlreadyPersistedSnapshotCount,
    IReadOnlyList<string> Symbols);

public interface ILmaxCanonicalSnapshotIngestionService
{
    Task<LmaxCanonicalSnapshotIngestionResult> IngestAsync(
        LmaxCanonicalSnapshotIngestionRequest request,
        CancellationToken cancellationToken);
}

public sealed class LmaxCanonicalSnapshotIngestionService(
    IIntradayRepository intradayRepository,
    IMarketDataSnapshotRepository snapshotRepository,
    IClock clock) : ILmaxCanonicalSnapshotIngestionService
{
    private const string CaptureComponent = "LMAX_MARKET_DATA_CAPTURE_ONLY";
    private const string CaptureVenue = "LMAX_DEMO_READ_ONLY";

    public async Task<LmaxCanonicalSnapshotIngestionResult> IngestAsync(
        LmaxCanonicalSnapshotIngestionRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var root = Path.GetFullPath(request.CaptureRunRoot);
        var finalManifestPath = Path.Combine(root, "final_manifest.json");
        if (!File.Exists(finalManifestPath))
            throw new DomainRuleViolationException("LMAX canonical capture final manifest is missing.");

        var manifestSha256 = CanonicalRecorderV2.Sha256File(finalManifestPath);
        if (!string.Equals(manifestSha256, request.ExpectedFinalManifestSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleViolationException("LMAX canonical capture final manifest hash does not match governed lineage.");

        var replay = await new CanonicalRecorderV2Replayer()
            .ReplaySnapshotAsync(root, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(replay.ReplayReport.Status, "PASS", StringComparison.Ordinal))
            throw new DomainRuleViolationException(
                $"LMAX canonical capture replay failed: {replay.ReplayReport.FailureReason}");

        var state = await intradayRepository.LoadStateAsync(cancellationToken).ConfigureAwait(false);
        var venue = state.Venues.SingleOrDefault(x => x.Name == "LMAX" && x.IsEnabled && x.IsTradingEnabled)
            ?? throw new DomainRuleViolationException("Enabled LMAX execution venue is required for canonical snapshot ingestion.");
        var instruments = state.Instruments
            .Where(x => x.IsEnabled && x.IsTradingEnabled)
            .Where(instrument => state.VenueInstrumentMappings.Any(mapping =>
                mapping.VenueId == venue.Id && mapping.InstrumentId == instrument.Id && mapping.IsEnabled))
            .ToList();
        if (instruments.Count == 0)
            throw new DomainRuleViolationException("No enabled LMAX execution instruments are configured for canonical snapshot ingestion.");
        if (instruments.GroupBy(x => x.Symbol, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() != 1))
            throw new DomainRuleViolationException("Enabled LMAX execution instrument symbols are ambiguous.");

        var expectedSymbols = instruments.Select(x => x.Symbol).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var latestBySymbol = replay.Events
            .Where(x => x.EventType == "BBO_UPDATED")
            .Where(x => string.Equals(x.SourceComponent, CaptureComponent, StringComparison.Ordinal))
            .Where(x => string.Equals(x.Venue, CaptureVenue, StringComparison.Ordinal))
            .Where(x => x.BookValid == true && x.BidPrice is > 0 && x.AskPrice is > 0 && x.BidPrice < x.AskPrice)
            .Where(x => x.Symbol is not null && expectedSymbols.Contains(x.Symbol))
            .Where(x => x.SourceTimestampUtc.HasValue && x.SourceTimestampUtc.Value <= request.DecisionAtUtc)
            .Where(x => request.DecisionAtUtc - x.SourceTimestampUtc!.Value <= request.MaximumSourceAge)
            .GroupBy(x => x.Symbol!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(y => y.SourceTimestampUtc)
                    .ThenByDescending(y => y.ProcessEventSequence)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        var missing = expectedSymbols.Where(x => !latestBySymbol.ContainsKey(x)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (missing.Length > 0)
            throw new DomainRuleViolationException(
                $"LMAX canonical capture has no valid fresh BBO at or before the decision for enabled execution instrument(s): {string.Join(", ", missing)}.");

        var source = $"LMAX_CANONICAL:{manifestSha256}";
        var toPersist = new List<MarketDataSnapshot>();
        var alreadyPersisted = 0;
        foreach (var instrument in instruments.OrderBy(x => x.Symbol, StringComparer.Ordinal))
        {
            var observation = latestBySymbol[instrument.Symbol];
            var sourceTimestamp = observation.SourceTimestampUtc!.Value;
            var existing = await snapshotRepository.GetRangeAsync(
                    instrument.Id,
                    venue.Id,
                    sourceTimestamp,
                    sourceTimestamp.AddTicks(1),
                    cancellationToken)
                .ConfigureAwait(false);
            var matching = existing.FirstOrDefault(x => string.Equals(x.Source, source, StringComparison.Ordinal));
            if (matching is not null)
            {
                if (matching.Bid != observation.BidPrice || matching.Ask != observation.AskPrice)
                    throw new DomainRuleViolationException(
                        $"LMAX canonical snapshot lineage is ambiguous for {instrument.Symbol} at {sourceTimestamp:O}.");
                alreadyPersisted++;
                continue;
            }

            var receivedAt = clock.UtcNow;
            toPersist.Add(new MarketDataSnapshot(
                MarketDataSnapshotId.New(),
                instrument.Id,
                venue.Id,
                observation.BidPrice!.Value,
                observation.AskPrice!.Value,
                null,
                source,
                sourceTimestamp,
                receivedAt)
            {
                SequenceNumber = observation.FixMsgSeqNum,
                IsSynthetic = false,
                CreatedAtUtc = receivedAt
            });
        }

        if (toPersist.Count > 0)
            await snapshotRepository.AddRangeAsync(toPersist, cancellationToken).ConfigureAwait(false);

        return new LmaxCanonicalSnapshotIngestionResult(
            replay.ReplayReport.RecorderRunId,
            manifestSha256,
            toPersist.Count,
            alreadyPersisted,
            instruments.Select(x => x.Symbol).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static void Validate(LmaxCanonicalSnapshotIngestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CaptureRunRoot))
            throw new DomainRuleViolationException("LMAX canonical capture run root is required.");
        if (request.DecisionAtUtc.Offset != TimeSpan.Zero)
            throw new DomainRuleViolationException("LMAX canonical snapshot decision timestamp must be UTC.");
        if (request.MaximumSourceAge <= TimeSpan.Zero)
            throw new DomainRuleViolationException("LMAX canonical snapshot maximum source age must be positive.");
        if (request.ExpectedFinalManifestSha256.Length != 64 ||
            !request.ExpectedFinalManifestSha256.All(Uri.IsHexDigit))
            throw new DomainRuleViolationException("Expected LMAX canonical final manifest SHA-256 is invalid.");
    }
}
