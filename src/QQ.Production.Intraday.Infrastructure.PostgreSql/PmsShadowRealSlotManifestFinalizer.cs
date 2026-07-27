using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class PmsShadowRealSlotBboSelectionContract
{
    public const string Version = "slot_bbo_selection_source_timestamp_clock_authority_v2";
    public const int RequiredSymbolCount = 49;
    public static TimeSpan MaximumLateReceiptAfterSlotClose =>
        PmsShadowCaptureClockAuthorityContract.MaximumLateReceiptAfterSlotClose;
}

public sealed record PmsShadowRawSlotBboEvent(
    string EventId,
    long ProcessEventSequence,
    string Symbol,
    string InstrumentId,
    DateTimeOffset SourceTimestampUtc,
    DateTimeOffset RecordedUtc,
    long FixMsgSeqNum,
    long SourceReceiveSequence,
    string QuoteEventId,
    decimal BidPrice,
    decimal BidQuantity,
    decimal AskPrice,
    decimal AskQuantity,
    string RecorderRunId,
    string SourceComponent,
    string Venue);

public sealed record PmsShadowSlotBboSelection(
    IReadOnlyDictionary<string, PmsShadowRawSlotBboEvent> SelectedBySymbol,
    int InSlotBboEventCount,
    int PostCloseBboEventCount,
    IReadOnlyDictionary<string, int> ExcludedPostCloseBySymbol,
    int SourceAfterRecordedEventCount,
    int CrossClockLeadExceededEventCount,
    int RecordedAfterFinalizationEventCount,
    IReadOnlyList<string> MissingRequiredSymbols,
    DateTimeOffset? MinimumSelectedSourceTimestampUtc,
    DateTimeOffset? MaximumSelectedSourceTimestampUtc,
    string? SelectionSha256)
{
    public bool Qualifying =>
        MissingRequiredSymbols.Count == 0 &&
        SelectedBySymbol.Count == PmsShadowRealSlotBboSelectionContract.RequiredSymbolCount;
}

public static class PmsShadowRealSlotBboSelector
{
    public static PmsShadowSlotBboSelection Select(
        PmsShadowIntradaySlotWindow slot,
        IReadOnlyDictionary<string, string> requiredInstrumentBySymbol,
        IEnumerable<PmsShadowRawSlotBboEvent> events,
        PmsShadowCaptureClockAuthorityEvidence clockAuthority)
    {
        if (requiredInstrumentBySymbol.Count !=
            PmsShadowRealSlotBboSelectionContract.RequiredSymbolCount)
            throw new InvalidDataException("RAW_SLOT_REQUIRED_INSTRUMENT_SET_INCOMPLETE");

        var candidates = new Dictionary<string, List<PmsShadowRawSlotBboEvent>>(
            StringComparer.Ordinal);
        var postClose = new Dictionary<string, int>(StringComparer.Ordinal);
        var inSlotCount = 0;
        var postCloseCount = 0;
        var sourceAfterRecordedCount = 0;
        var crossClockLeadExceededCount = 0;
        var afterFinalizationCount = 0;

        foreach (var value in events)
        {
            if (!requiredInstrumentBySymbol.TryGetValue(value.Symbol, out var expectedInstrumentId))
                continue;
            if (!string.Equals(value.InstrumentId, expectedInstrumentId, StringComparison.Ordinal))
                throw new InvalidDataException("RAW_SLOT_BBO_INSTRUMENT_IDENTITY_MISMATCH");
            if (!string.Equals(value.SourceComponent, "LMAX_MARKET_DATA_CAPTURE_ONLY",
                    StringComparison.Ordinal) ||
                !value.Venue.StartsWith("LMAX_", StringComparison.Ordinal))
                throw new InvalidDataException("RAW_SLOT_NON_LMAX_BBO_CONTAMINATION");

            if (value.SourceTimestampUtc > slot.SlotEndUtc)
            {
                postCloseCount++;
                postClose[value.Symbol] = postClose.GetValueOrDefault(value.Symbol) + 1;
                continue;
            }
            if (value.SourceTimestampUtc < slot.SlotStartUtc)
                continue;
            if (value.SourceTimestampUtc > value.RecordedUtc)
                sourceAfterRecordedCount++;
            if (!PmsShadowCaptureClockAuthorityValidator.IsCrossClockCausalityValid(
                    value.SourceTimestampUtc, value.RecordedUtc, clockAuthority))
            {
                crossClockLeadExceededCount++;
                continue;
            }
            if (!PmsShadowCaptureClockAuthorityValidator.IsWithinLateReceiptBound(
                    value.RecordedUtc, slot.SlotEndUtc, clockAuthority))
            {
                afterFinalizationCount++;
                continue;
            }
            if (value.BidPrice <= 0m || value.AskPrice < value.BidPrice ||
                value.FixMsgSeqNum <= 0 || value.SourceReceiveSequence <= 0 ||
                value.ProcessEventSequence <= 0 ||
                string.IsNullOrWhiteSpace(value.EventId) ||
                string.IsNullOrWhiteSpace(value.QuoteEventId))
                continue;

            inSlotCount++;
            if (!candidates.TryGetValue(value.Symbol, out var values))
                candidates[value.Symbol] = values = [];
            values.Add(value);
        }

        var selected = candidates.ToDictionary(
            pair => pair.Key,
            pair => pair.Value
                .OrderBy(value => value.SourceTimestampUtc)
                .ThenBy(value => value.RecordedUtc)
                .ThenBy(value => value.FixMsgSeqNum)
                .ThenBy(value => value.SourceReceiveSequence)
                .ThenBy(value => value.ProcessEventSequence)
                .ThenBy(value => value.EventId, StringComparer.Ordinal)
                .Last(),
            StringComparer.Ordinal);
        var missing = requiredInstrumentBySymbol.Keys
            .Where(symbol => !selected.ContainsKey(symbol))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var ordered = selected.Values
            .OrderBy(value => value.Symbol, StringComparer.Ordinal)
            .ToArray();

        return new(
            selected,
            inSlotCount,
            postCloseCount,
            postClose.OrderBy(value => value.Key, StringComparer.Ordinal)
                .ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal),
            sourceAfterRecordedCount,
            crossClockLeadExceededCount,
            afterFinalizationCount,
            missing,
            ordered.Length == 0 ? null : ordered.Min(value => value.SourceTimestampUtc),
            ordered.Length == 0 ? null : ordered.Max(value => value.SourceTimestampUtc),
            ordered.Length == 0 ? null : SelectionSha256(ordered));
    }

    public static string SelectionSha256(IEnumerable<PmsShadowRawSlotBboEvent> values)
    {
        var canonical = string.Join("\n", values
            .OrderBy(value => value.Symbol, StringComparer.Ordinal)
            .Select(value => string.Join("|",
                value.Symbol,
                value.InstrumentId,
                value.SourceTimestampUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                value.RecordedUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                value.FixMsgSeqNum.ToString(CultureInfo.InvariantCulture),
                value.SourceReceiveSequence.ToString(CultureInfo.InvariantCulture),
                value.ProcessEventSequence.ToString(CultureInfo.InvariantCulture),
                value.EventId,
                value.QuoteEventId,
                value.BidPrice.ToString(CultureInfo.InvariantCulture),
                value.BidQuantity.ToString(CultureInfo.InvariantCulture),
                value.AskPrice.ToString(CultureInfo.InvariantCulture),
                value.AskQuantity.ToString(CultureInfo.InvariantCulture),
                value.RecorderRunId)));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static string SelectionSha256(JsonElement values) =>
        SelectionSha256(values.EnumerateObject().Select(value =>
        {
            var bbo = value.Value;
            return new PmsShadowRawSlotBboEvent(
                RequiredString(bbo, "event_id"),
                bbo.GetProperty("process_event_sequence").GetInt64(),
                value.Name,
                RequiredString(bbo, "instrument_id"),
                bbo.GetProperty("source_timestamp_utc").GetDateTimeOffset(),
                bbo.GetProperty("recorded_utc").GetDateTimeOffset(),
                bbo.GetProperty("fix_msg_seq_num").GetInt64(),
                bbo.GetProperty("source_receive_sequence").GetInt64(),
                RequiredString(bbo, "quote_event_id"),
                bbo.GetProperty("bid_price").GetDecimal(),
                bbo.GetProperty("bid_quantity").GetDecimal(),
                bbo.GetProperty("ask_price").GetDecimal(),
                bbo.GetProperty("ask_quantity").GetDecimal(),
                RequiredString(bbo, "recorder_run_id"),
                "LMAX_MARKET_DATA_CAPTURE_ONLY",
                "LMAX_MANIFEST");
        }));

    private static string RequiredString(JsonElement value, string name) =>
        value.GetProperty(name).GetString() ??
        throw new InvalidDataException($"RAW_SLOT_FIELD_MISSING:{name}");
}

public static class PmsShadowRealSlotManifestFinalizer
{
    public static PmsShadowSlotBboSelection Finalize(
        string manifestPath,
        string artifactPath,
        PmsShadowIntradaySlotWindow expectedSlot,
        PmsShadowCaptureClockAuthorityEvidence clockAuthority,
        string expectedHostIdentity,
        string expectedRepositoryCommit)
    {
        manifestPath = Path.GetFullPath(manifestPath);
        artifactPath = Path.GetFullPath(artifactPath);
        PmsShadowCaptureClockAuthorityValidator.RequireQualifiedForSlot(
            clockAuthority, expectedSlot, expectedHostIdentity, expectedRepositoryCommit);
        var root = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
            ?? throw new InvalidDataException("RAW_SLOT_MANIFEST_INVALID");
        var slot = ReadSlot(root);
        if (slot != expectedSlot)
            throw new InvalidDataException("RAW_SLOT_WINDOW_IDENTITY_MISMATCH");

        var expectedArtifactPath = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(manifestPath)!,
            Path.GetFileName(RequiredString(root, "artifact_file"))));
        if (!string.Equals(expectedArtifactPath, artifactPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("RAW_SLOT_ARTIFACT_MANIFEST_ROOT_MISMATCH");
        var expectedArtifactSha = RequiredString(root, "artifact_sha256");
        using (var stream = File.OpenRead(artifactPath))
        {
            var actualArtifactSha = Convert.ToHexStringLower(SHA256.HashData(stream));
            if (!string.Equals(actualArtifactSha, expectedArtifactSha, StringComparison.Ordinal))
                throw new InvalidDataException("RAW_SLOT_ARTIFACT_SHA_MISMATCH");
        }
        if (root["lmax_primary"]?.GetValue<bool>() != true ||
            root["polygon_call_count"]?.GetValue<int>() != 0 ||
            root["no_order"]?.GetValue<bool>() != true ||
            root["inbound_execution_report_count"]?.GetValue<int>() != 0)
            throw new InvalidDataException("RAW_SLOT_SAFETY_CONTRACT_VIOLATION");

        var required = RequiredObject(root, "last_bbo_by_symbol")
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToDictionary(
                value => value.Key,
                value => RequiredString(value.Value?.AsObject()
                    ?? throw new InvalidDataException("RAW_SLOT_BBO_INVALID"), "instrument_id"),
                StringComparer.Ordinal);
        var selection = PmsShadowRealSlotBboSelector.Select(
            slot, required, ReadBboEvents(artifactPath), clockAuthority);
        if (!selection.Qualifying)
            throw new InvalidDataException(
                $"RAW_SLOT_IN_WINDOW_BBO_COVERAGE_INCOMPLETE:{string.Join(",", selection.MissingRequiredSymbols)}");

        root["slot_bbo_selection_contract_version"] =
            PmsShadowRealSlotBboSelectionContract.Version;
        root["in_slot_bbo_event_count"] = selection.InSlotBboEventCount;
        root["post_close_bbo_event_count"] = selection.PostCloseBboEventCount;
        root["source_after_recorded_bbo_event_count"] = selection.SourceAfterRecordedEventCount;
        root["cross_clock_lead_exceeded_bbo_event_count"] =
            selection.CrossClockLeadExceededEventCount;
        root["recorded_after_finalization_bbo_event_count"] =
            selection.RecordedAfterFinalizationEventCount;
        root["finalization_deadline_utc"] =
            (slot.SlotEndUtc +
             PmsShadowRealSlotBboSelectionContract.MaximumLateReceiptAfterSlotClose)
            .ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        root["clock_authority_contract_version"] =
            PmsShadowCaptureClockAuthorityContract.Version;
        root["clock_authority_snapshot_file"] = "clock_authority_capture.json";
        root["clock_authority_snapshot_sha256"] =
            clockAuthority.PreCapture.SnapshotSha256;
        root["clock_post_close_snapshot_file"] =
            "clock_authority_post_close.json";
        root["clock_post_close_snapshot_sha256"] =
            clockAuthority.PostClose.SnapshotSha256;
        root["clock_reference_source"] =
            clockAuthority.PreCapture.ReferenceClockSource;
        root["clock_offset_ms"] =
            clockAuthority.PreCapture.MeasuredOffsetMilliseconds;
        root["clock_uncertainty_ms"] =
            clockAuthority.MaximumClockUncertaintyMilliseconds;
        root["clock_snapshot_captured_at_utc"] =
            clockAuthority.PreCapture.CapturedAtUtc.ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture);
        root["clock_preflight_status"] =
            PmsShadowCaptureClockAuthorityContract.QualifiedStatus;
        root["clock_host_identity"] = expectedHostIdentity;
        root["repository_commit"] = expectedRepositoryCommit;
        root["maximum_late_receipt_after_close_ms"] =
            PmsShadowCaptureClockAuthorityContract
                .MaximumLateReceiptAfterSlotCloseMilliseconds;
        root["maximum_cross_clock_lead_ms"] =
            clockAuthority.MaximumCrossClockLeadMilliseconds;
        root["cross_clock_comparison"] =
            PmsShadowCaptureClockAuthorityContract.CrossClockComparison;
        root["minimum_selected_source_timestamp_utc"] =
            selection.MinimumSelectedSourceTimestampUtc!.Value.ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture);
        root["maximum_selected_source_timestamp_utc"] =
            selection.MaximumSelectedSourceTimestampUtc!.Value.ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture);
        root["selection_sha256"] = selection.SelectionSha256;
        root["excluded_post_close_by_symbol"] = new JsonObject(
            selection.ExcludedPostCloseBySymbol.Select(value =>
                KeyValuePair.Create<string, JsonNode?>(value.Key, JsonValue.Create(value.Value))));
        root["last_bbo_by_symbol"] = new JsonObject(selection.SelectedBySymbol
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => KeyValuePair.Create<string, JsonNode?>(
                value.Key, ToJson(value.Value, clockAuthority))));
        root["bbo_symbol_count"] = selection.SelectedBySymbol.Count;
        root["missing_required_bbo_symbols"] = new JsonArray();
        root["complete"] = true;

        var manifestRoot = Path.GetDirectoryName(manifestPath)!;
        PmsShadowCaptureClockAuthorityStore.WriteAtomic(
            Path.Combine(manifestRoot, "clock_authority_capture.json"),
            clockAuthority.PreCapture);
        PmsShadowCaptureClockAuthorityStore.WriteAtomic(
            Path.Combine(manifestRoot, "clock_authority_post_close.json"),
            clockAuthority.PostClose);

        var temporary = manifestPath + $".{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            var bytes = Encoding.UTF8.GetBytes(root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            }) + Environment.NewLine);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temporary, manifestPath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return selection;
    }

    private static IEnumerable<PmsShadowRawSlotBboEvent> ReadBboEvents(string artifactPath)
    {
        foreach (var line in File.ReadLines(artifactPath))
        {
            using var document = JsonDocument.Parse(line);
            var value = document.RootElement;
            if (RequiredString(value, "event_type") != "BBO_UPDATED")
                continue;
            yield return new(
                RequiredString(value, "event_id"),
                value.GetProperty("process_event_sequence").GetInt64(),
                RequiredString(value, "symbol"),
                RequiredString(value, "instrument_id"),
                value.GetProperty("source_timestamp_utc").GetDateTimeOffset(),
                value.GetProperty("recorded_utc").GetDateTimeOffset(),
                value.GetProperty("fix_msg_seq_num").GetInt64(),
                value.GetProperty("source_receive_sequence").GetInt64(),
                RequiredString(value, "quote_event_id"),
                value.GetProperty("bid_price").GetDecimal(),
                value.GetProperty("bid_quantity").GetDecimal(),
                value.GetProperty("ask_price").GetDecimal(),
                value.GetProperty("ask_quantity").GetDecimal(),
                RequiredString(value, "recorder_run_id"),
                RequiredString(value, "source_component"),
                RequiredString(value, "venue"));
        }
    }

    private static JsonObject ToJson(PmsShadowRawSlotBboEvent value,
        PmsShadowCaptureClockAuthorityEvidence clockAuthority) => new()
    {
        ["event_id"] = value.EventId,
        ["symbol"] = value.Symbol,
        ["instrument_id"] = value.InstrumentId,
        ["recorded_utc"] = value.RecordedUtc.ToUniversalTime()
            .ToString("O", CultureInfo.InvariantCulture),
        ["source_timestamp_utc"] = value.SourceTimestampUtc.ToUniversalTime()
            .ToString("O", CultureInfo.InvariantCulture),
        ["corrected_recorded_utc_for_validation"] =
            PmsShadowCaptureClockAuthorityValidator.CorrectedRecordedUtcForValidation(
                value.RecordedUtc, clockAuthority).ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture),
        ["applied_clock_offset_ms"] =
            clockAuthority.PreCapture.MeasuredOffsetMilliseconds,
        ["clock_uncertainty_ms"] = clockAuthority.MaximumClockUncertaintyMilliseconds,
        ["clock_snapshot_sha256"] = clockAuthority.PreCapture.SnapshotSha256,
        ["fix_msg_seq_num"] = value.FixMsgSeqNum,
        ["source_receive_sequence"] = value.SourceReceiveSequence,
        ["process_event_sequence"] = value.ProcessEventSequence,
        ["quote_event_id"] = value.QuoteEventId,
        ["bid_price"] = value.BidPrice,
        ["bid_quantity"] = value.BidQuantity,
        ["ask_price"] = value.AskPrice,
        ["ask_quantity"] = value.AskQuantity,
        ["recorder_run_id"] = value.RecorderRunId
    };

    private static PmsShadowIntradaySlotWindow ReadSlot(JsonObject root) => new(
        RequiredString(root, "slot_id"),
        DateTimeOffset.Parse(RequiredString(root, "slot_start_utc"),
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
        DateTimeOffset.Parse(RequiredString(root, "slot_end_utc"),
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
        DateOnly.Parse(RequiredString(root, "operational_date"), CultureInfo.InvariantCulture));

    private static JsonObject RequiredObject(JsonObject value, string name) =>
        value[name]?.AsObject() ?? throw new InvalidDataException($"RAW_SLOT_FIELD_MISSING:{name}");

    private static string RequiredString(JsonObject value, string name) =>
        value[name]?.GetValue<string>() ??
        throw new InvalidDataException($"RAW_SLOT_FIELD_MISSING:{name}");

    private static string RequiredString(JsonElement value, string name) =>
        value.GetProperty(name).GetString() ??
        throw new InvalidDataException($"RAW_SLOT_FIELD_MISSING:{name}");
}
