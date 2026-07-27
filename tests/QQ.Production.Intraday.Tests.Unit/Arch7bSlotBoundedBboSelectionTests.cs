using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bSlotBoundedBboSelectionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),
        $"arch7b-slot-bbo-{Guid.NewGuid():N}");
    private const string Commit = "e74f984bf3320142617b9016fcb91610d36b5741";
    private static readonly string Host = Environment.MachineName;
    private static readonly DateTimeOffset Close = new(2026, 7, 24, 10, 45, 0, TimeSpan.Zero);
    private static readonly PmsShadowIntradaySlotWindow Slot =
        PmsShadowIntradayCadenceContract.WindowEnding(Close);
    private static readonly IReadOnlyDictionary<string, string> Required =
        Enumerable.Range(0, 49).ToDictionary(
            value => value == 0 ? "GBPUSD" : $"X{value:00000}",
            value => (4002 + value).ToString(),
            StringComparer.Ordinal);

    [Fact]
    public void Last_source_timestamp_in_slot_wins_over_post_close_book_update()
    {
        var inSlot = Event("GBPUSD", Close.AddMilliseconds(-100), Close.AddMilliseconds(-50), 100);
        var postClose = Event("GBPUSD", Close.AddMilliseconds(125), Close.AddMilliseconds(150), 101);

        var result = Select(CompleteEvents()
            .Where(value => value.Symbol != "GBPUSD")
            .Append(inSlot)
            .Append(postClose));

        Assert.True(result.Qualifying);
        Assert.Equal(inSlot.EventId, result.SelectedBySymbol["GBPUSD"].EventId);
        Assert.Equal(1, result.PostCloseBboEventCount);
        Assert.Equal(1, result.ExcludedPostCloseBySymbol["GBPUSD"]);
    }

    [Fact]
    public void Source_timestamp_exactly_at_close_is_accepted()
    {
        var atClose = Event("GBPUSD", Close, Close.AddMilliseconds(1), 100);

        var result = Select(CompleteEvents()
            .Where(value => value.Symbol != "GBPUSD")
            .Append(atClose));

        Assert.Equal(atClose.EventId, result.SelectedBySymbol["GBPUSD"].EventId);
    }

    [Fact]
    public void Source_timestamp_one_tick_after_close_is_excluded()
    {
        var baseline = Event("GBPUSD", Close.AddSeconds(-1), Close.AddMilliseconds(-900), 100);
        var after = Event("GBPUSD", Close.AddTicks(1), Close.AddMilliseconds(1), 101);

        var result = Select(CompleteEvents()
            .Where(value => value.Symbol != "GBPUSD")
            .Append(baseline)
            .Append(after));

        Assert.Equal(baseline.EventId, result.SelectedBySymbol["GBPUSD"].EventId);
        Assert.Equal(1, result.PostCloseBboEventCount);
    }

    [Fact]
    public void All_events_post_close_for_required_symbol_fail_coverage()
    {
        var result = Select(CompleteEvents()
            .Where(value => value.Symbol != "GBPUSD")
            .Append(Event("GBPUSD", Close.AddTicks(1), Close.AddMilliseconds(1), 100)));

        Assert.False(result.Qualifying);
        Assert.Equal(["GBPUSD"], result.MissingRequiredSymbols);
    }

    [Fact]
    public void Late_arrival_with_in_slot_source_is_accepted_within_existing_finalization_bound()
    {
        var late = Event("GBPUSD", Close.AddMilliseconds(-50), Close.AddSeconds(1), 100);

        var result = Select(CompleteEvents()
            .Where(value => value.Symbol != "GBPUSD")
            .Append(late));

        Assert.Equal(late.EventId, result.SelectedBySymbol["GBPUSD"].EventId);
        Assert.Equal(0, result.RecordedAfterFinalizationEventCount);
    }

    [Fact]
    public void Arrival_after_short_late_receipt_bound_is_rejected()
    {
        var tooLate = Event("GBPUSD", Close.AddMilliseconds(-50),
            Close + PmsShadowRealSlotBboSelectionContract.MaximumLateReceiptAfterSlotClose +
            TimeSpan.FromTicks(1), 100);

        var result = Select(CompleteEvents()
            .Where(value => value.Symbol != "GBPUSD")
            .Append(tooLate));

        Assert.False(result.Qualifying);
        Assert.Equal(1, result.RecordedAfterFinalizationEventCount);
        Assert.Equal(["GBPUSD"], result.MissingRequiredSymbols);
    }

    [Fact]
    public void Source_ahead_beyond_measured_clock_envelope_is_rejected_without_retimestamping()
    {
        var invalid = Event("GBPUSD", Close.AddMilliseconds(-50), Close.AddMilliseconds(-100), 100);

        var result = Select(CompleteEvents()
            .Where(value => value.Symbol != "GBPUSD")
            .Append(invalid));

        Assert.False(result.Qualifying);
        Assert.Equal(1, result.SourceAfterRecordedEventCount);
        Assert.Equal(1, result.CrossClockLeadExceededEventCount);
        Assert.Equal(["GBPUSD"], result.MissingRequiredSymbols);
    }

    [Fact]
    public void Source_slightly_ahead_is_accepted_by_measured_envelope_with_raw_times_preserved()
    {
        var source = Close.AddMilliseconds(-50);
        var recorded = source.AddMilliseconds(-25);
        var valid = Event("GBPUSD", source, recorded, 100);

        var result = Select(CompleteEvents()
            .Where(value => value.Symbol != "GBPUSD")
            .Append(valid));

        Assert.True(result.Qualifying);
        Assert.Equal(source, result.SelectedBySymbol["GBPUSD"].SourceTimestampUtc);
        Assert.Equal(recorded, result.SelectedBySymbol["GBPUSD"].RecordedUtc);
        Assert.Equal(1, result.SourceAfterRecordedEventCount);
        Assert.Equal(0, result.CrossClockLeadExceededEventCount);
        Assert.Empty(result.MissingRequiredSymbols);
    }

    [Fact]
    public void Equal_source_timestamps_use_recorded_sequence_and_identity_deterministically()
    {
        var source = Close.AddSeconds(-1);
        var first = Event("GBPUSD", source, source.AddMilliseconds(1), 100);
        var second = Event("GBPUSD", source, source.AddMilliseconds(2), 101);
        var values = CompleteEvents().Where(value => value.Symbol != "GBPUSD")
            .Concat([first, second]).ToArray();

        var forward = Select(values);
        var reverse = Select(values.Reverse());

        Assert.Equal(second.EventId, forward.SelectedBySymbol["GBPUSD"].EventId);
        Assert.Equal(forward.SelectedBySymbol["GBPUSD"], reverse.SelectedBySymbol["GBPUSD"]);
        Assert.Equal(forward.SelectionSha256, reverse.SelectionSha256);
    }

    [Fact]
    public void Forty_nine_symbols_qualify_and_forty_eight_fail_closed()
    {
        Assert.True(Select(CompleteEvents()).Qualifying);

        var incomplete = Select(CompleteEvents().Where(value => value.Symbol != "GBPUSD"));

        Assert.False(incomplete.Qualifying);
        Assert.Equal(["GBPUSD"], incomplete.MissingRequiredSymbols);
    }

    [Fact]
    public void Finalizer_writes_diagnostics_and_is_byte_for_byte_idempotent()
    {
        var fixture = WriteFixture(CompleteEvents());

        var selection = Finalize(fixture);
        var first = File.ReadAllBytes(fixture.ManifestPath);
        var secondSelection = Finalize(fixture);
        var second = File.ReadAllBytes(fixture.ManifestPath);
        using var document = JsonDocument.Parse(second);
        var manifest = document.RootElement;

        Assert.True(selection.Qualifying);
        Assert.Equal(selection.SelectionSha256, secondSelection.SelectionSha256);
        Assert.Equal(first, second);
        Assert.Equal(PmsShadowRealSlotBboSelectionContract.Version,
            manifest.GetProperty("slot_bbo_selection_contract_version").GetString());
        Assert.Equal(49, manifest.GetProperty("in_slot_bbo_event_count").GetInt32());
        Assert.Equal(0, manifest.GetProperty("post_close_bbo_event_count").GetInt32());
        Assert.Equal(49, manifest.GetProperty("bbo_symbol_count").GetInt32());
        Assert.Equal(64, manifest.GetProperty("selection_sha256").GetString()!.Length);
        Assert.Equal(0, manifest.GetProperty("missing_required_bbo_symbols").GetArrayLength());
        Assert.True(manifest.GetProperty("complete").GetBoolean());
        Assert.Empty(manifest.GetProperty("excluded_post_close_by_symbol").EnumerateObject());
        Assert.Equal(PmsShadowCaptureClockAuthorityContract.Version,
            manifest.GetProperty("clock_authority_contract_version").GetString());
        Assert.Equal(Clock.PreCapture.SnapshotSha256,
            manifest.GetProperty("clock_authority_snapshot_sha256").GetString());
        Assert.Equal(Clock.PostClose.SnapshotSha256,
            manifest.GetProperty("clock_post_close_snapshot_sha256").GetString());
        Assert.Equal(31m,
            manifest.GetProperty("maximum_cross_clock_lead_ms").GetDecimal());
        Assert.Equal(2_000,
            manifest.GetProperty("maximum_late_receipt_after_close_ms").GetInt32());
        Assert.NotEqual(PmsShadowFreshSlotHandoffContract.AbsoluteStartDeadlineSeconds * 1000,
            manifest.GetProperty("maximum_late_receipt_after_close_ms").GetInt32());
        Assert.Equal(49, PmsShadowRealSlotCaptureReader.Read(fixture.ManifestPath).Bbo.Count);
    }

    [Fact]
    public void Reader_rejects_post_close_manifest_without_filtering_or_clamping()
    {
        var fixture = WriteFixture(CompleteEvents());
        Finalize(fixture);
        var manifest = JsonNode.Parse(File.ReadAllText(fixture.ManifestPath))!.AsObject();
        manifest["last_bbo_by_symbol"]!["GBPUSD"]!["source_timestamp_utc"] =
            Close.AddTicks(1).ToString("O");
        File.WriteAllText(fixture.ManifestPath,
            manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var error = Assert.Throws<InvalidDataException>(
            () => PmsShadowRealSlotCaptureReader.Read(fixture.ManifestPath));

        Assert.Equal("RAW_SLOT_BBO_SOURCE_TIMESTAMP_OUTSIDE_WINDOW", error.Message);
    }

    [Fact]
    public void Finalizer_does_not_publish_a_qualifying_manifest_with_forty_eight_symbols()
    {
        var fixture = WriteFixture(CompleteEvents().Where(value => value.Symbol != "GBPUSD"));
        var before = File.ReadAllBytes(fixture.ManifestPath);

        var error = Assert.Throws<InvalidDataException>(() =>
            Finalize(fixture));

        Assert.StartsWith("RAW_SLOT_IN_WINDOW_BBO_COVERAGE_INCOMPLETE:GBPUSD", error.Message);
        Assert.Equal(before, File.ReadAllBytes(fixture.ManifestPath));
    }

    private static PmsShadowSlotBboSelection Select(
        IEnumerable<PmsShadowRawSlotBboEvent> values) =>
        PmsShadowRealSlotBboSelector.Select(Slot, Required, values, Clock);

    private static PmsShadowRawSlotBboEvent[] CompleteEvents() =>
        Required.Select((value, index) => Event(
            value.Key, Close.AddSeconds(-2), Close.AddSeconds(-1), index + 1)).ToArray();

    private static PmsShadowRawSlotBboEvent Event(
        string symbol, DateTimeOffset source, DateTimeOffset recorded, long sequence)
    {
        var instrument = Required[symbol];
        return new(
            $"evt-{sequence:000000}",
            sequence,
            symbol,
            instrument,
            source,
            recorded,
            sequence,
            sequence,
            $"quote-{sequence:000000}",
            1.20m + sequence / 100000m,
            50m,
            1.21m + sequence / 100000m,
            50m,
            "M2C1B_TEST",
            "LMAX_MARKET_DATA_CAPTURE_ONLY",
            "LMAX_DEMO_READ_ONLY");
    }

    private static PmsShadowCaptureClockAuthorityEvidence Clock => new(
        Snapshot(Slot.SlotStartUtc.AddSeconds(-1), 20m),
        Snapshot(Slot.SlotEndUtc.AddSeconds(1), 18m));

    private static PmsShadowCaptureClockAuthoritySnapshot Snapshot(
        DateTimeOffset capturedAtUtc, decimal offset) =>
        PmsShadowCaptureClockAuthoritySnapshot.Create(
            capturedAtUtc,
            "Windows Time",
            "time.windows.com",
            offset,
            10m,
            20m,
            5,
            "PASS",
            Host,
            1234,
            Commit,
            true,
            0,
            capturedAtUtc.AddMinutes(-1));

    private static PmsShadowSlotBboSelection Finalize(
        (string ManifestPath, string ArtifactPath) fixture) =>
        PmsShadowRealSlotManifestFinalizer.Finalize(
            fixture.ManifestPath, fixture.ArtifactPath, Slot,
            Clock, Host, Commit);

    private (string ManifestPath, string ArtifactPath) WriteFixture(
        IEnumerable<PmsShadowRawSlotBboEvent> values)
    {
        Directory.CreateDirectory(root);
        var artifact = Path.Combine(root, "slot.jsonl");
        File.WriteAllLines(artifact, values.Select(value => JsonSerializer.Serialize(new
        {
            event_type = "BBO_UPDATED",
            event_id = value.EventId,
            process_event_sequence = value.ProcessEventSequence,
            symbol = value.Symbol,
            instrument_id = value.InstrumentId,
            source_timestamp_utc = value.SourceTimestampUtc,
            recorded_utc = value.RecordedUtc,
            fix_msg_seq_num = value.FixMsgSeqNum,
            source_receive_sequence = value.SourceReceiveSequence,
            quote_event_id = value.QuoteEventId,
            bid_price = value.BidPrice,
            bid_quantity = value.BidQuantity,
            ask_price = value.AskPrice,
            ask_quantity = value.AskQuantity,
            recorder_run_id = value.RecorderRunId,
            source_component = value.SourceComponent,
            venue = value.Venue
        })));
        var artifactSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(artifact)));
        var lastBbo = new JsonObject(Required.OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => KeyValuePair.Create<string, JsonNode?>(
                value.Key, new JsonObject { ["instrument_id"] = value.Value })));
        var manifest = new JsonObject
        {
            ["version"] = "arch7b-fresh-source-slot-artifact-v1",
            ["slot_id"] = Slot.SlotId,
            ["slot_start_utc"] = Slot.SlotStartUtc.ToString("O"),
            ["slot_end_utc"] = Slot.SlotEndUtc.ToString("O"),
            ["operational_date"] = Slot.OperationalDate.ToString("yyyy-MM-dd"),
            ["recorder_run_id"] = "ARCH7B_TEST",
            ["artifact_file"] = Path.GetFileName(artifact),
            ["artifact_sha256"] = artifactSha,
            ["bbo_symbol_count"] = 49,
            ["missing_required_bbo_symbols"] = new JsonArray(),
            ["contractually_required_gap_ids"] = new JsonArray(),
            ["polygon_call_count"] = 0,
            ["polygon_replaced_valid_lmax_observation"] = false,
            ["lmax_primary"] = true,
            ["complete"] = true,
            ["no_order"] = true,
            ["inbound_execution_report_count"] = 0,
            ["last_bbo_by_symbol"] = lastBbo
        };
        var manifestPath = Path.Combine(root, "slot_manifest.json");
        File.WriteAllText(manifestPath,
            manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return (manifestPath, artifact);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
