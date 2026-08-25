using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

/// <summary>Read-only consumer boundary for canonical contract v1 at QQ.Investment.Platform@47d5d10cf4ee914621526e687c707657324730a1.</summary>
public sealed record CanonicalPostRiskInput(
    string AdapterInputId, long Revision, string? SupersedesAdapterInputId, long? SupersedesRevision,
    string MandateId, string InstrumentId, decimal RiskApprovedTargetWeight, string Fingerprint, string CanonicalJson);

public static class CanonicalPostRiskInputParser
{
    private static readonly string[] Root = ["contractVersion", "adapterInputId", "revision", "supersedes", "mandateId", "instrumentId", "modelTargetId", "adjustmentState", "overrideRevisionId", "riskApprovedTargetWeight", "riskDecisionId", "policyRevisionId", "riskInputSnapshot", "riskEvaluatedAt", "riskRecordedAt", "riskRuleEvaluations", "participants", "effectiveAt", "recordedAt", "knowledgeCutoff", "provenance", "decision", "fingerprint"];

    public static CanonicalPostRiskInput Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Object(root, "root", Root); Text(root, "contractVersion");
        if (root.GetProperty("contractVersion").GetString() != "v1") Fail("contractVersion is invalid.");
        Text(root, "adapterInputId"); Positive(root, "revision"); Text(root, "mandateId"); Text(root, "instrumentId"); Text(root, "modelTargetId");
        OptionalReference(root.GetProperty("supersedes"));
        var suppliedSupersedes = root.GetProperty("supersedes");
        if (suppliedSupersedes.ValueKind != JsonValueKind.Null && (suppliedSupersedes.GetProperty("adapterInputId").GetString() != root.GetProperty("adapterInputId").GetString() || suppliedSupersedes.GetProperty("revision").GetInt64() >= root.GetProperty("revision").GetInt64())) Fail("supersedes must reference the same identity at a lower revision.");
        Enum(root, "adjustmentState", ["NoOverride", "Overridden"]); OptionalText(root.GetProperty("overrideRevisionId"));
        if ((root.GetProperty("adjustmentState").GetString() == "NoOverride" && root.GetProperty("overrideRevisionId").ValueKind != JsonValueKind.Null) || (root.GetProperty("adjustmentState").GetString() == "Overridden" && root.GetProperty("overrideRevisionId").ValueKind != JsonValueKind.String)) Fail("adjustment state and override revision are inconsistent.");
        var weight = Decimal(root, "riskApprovedTargetWeight"); Text(root, "riskDecisionId"); Text(root, "policyRevisionId"); RiskInput(root.GetProperty("riskInputSnapshot")); Utc(root, "riskEvaluatedAt"); Utc(root, "riskRecordedAt");
        Array(root, "riskRuleEvaluations", RiskRule); Array(root, "participants", Participant); Utc(root, "effectiveAt"); Utc(root, "recordedAt"); Utc(root, "knowledgeCutoff"); Text(root, "provenance"); Text(root, "decision"); Hash(root, "fingerprint");
        var fingerprint = root.GetProperty("fingerprint").GetString()!;
        var expected = CanonicalFingerprint(json);
        if (fingerprint != expected) Fail("fingerprint does not bind canonical material wire content.");
        var supersedes = root.GetProperty("supersedes");
        return new(root.GetProperty("adapterInputId").GetString()!, root.GetProperty("revision").GetInt64(), supersedes.ValueKind == JsonValueKind.Null ? null : supersedes.GetProperty("adapterInputId").GetString(), supersedes.ValueKind == JsonValueKind.Null ? null : supersedes.GetProperty("revision").GetInt64(), root.GetProperty("mandateId").GetString()!, root.GetProperty("instrumentId").GetString()!, weight, fingerprint, root.GetRawText());
    }

    public static string CanonicalFingerprint(string json) { using var document = JsonDocument.Parse(json); return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Material(document.RootElement)))).ToLowerInvariant(); }

    private static string Material(JsonElement root)
    {
        using var stream = new MemoryStream(); using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        foreach (var name in new[] { "contractVersion", "adapterInputId" }) S(writer, root, name); N(writer, root, "revision");
        writer.WritePropertyName("supersedes"); var s = root.GetProperty("supersedes"); if (s.ValueKind == JsonValueKind.Null) writer.WriteNullValue(); else { writer.WriteStartObject(); S(writer, s, "adapterInputId"); N(writer, s, "revision"); writer.WriteEndObject(); }
        foreach (var name in new[] { "mandateId", "instrumentId", "modelTargetId", "adjustmentState" }) S(writer, root, name);
        writer.WritePropertyName("overrideRevisionId"); var o = root.GetProperty("overrideRevisionId"); if (o.ValueKind == JsonValueKind.Null) writer.WriteNullValue(); else writer.WriteStringValue(o.GetString());
        writer.WriteString("riskApprovedTargetWeight", root.GetProperty("riskApprovedTargetWeight").GetString()); foreach (var name in new[] { "riskDecisionId", "policyRevisionId" }) S(writer, root, name);
        writer.WritePropertyName("riskInputSnapshot"); writer.WriteStartObject(); var ri = root.GetProperty("riskInputSnapshot"); S(writer, ri, "snapshotId"); T(writer, ri, "effectiveAt"); T(writer, ri, "recordedAt"); S(writer, ri, "provenance"); writer.WriteEndObject();
        T(writer, root, "riskEvaluatedAt"); T(writer, root, "riskRecordedAt");
        writer.WritePropertyName("riskRuleEvaluations"); writer.WriteStartArray(); foreach (var x in root.GetProperty("riskRuleEvaluations").EnumerateArray().OrderBy(x => x.GetProperty("ruleId").GetString(), StringComparer.Ordinal).ThenBy(x => x.GetProperty("ruleVersion").GetString(), StringComparer.Ordinal)) { writer.WriteStartObject(); foreach (var name in new[] { "ruleId", "ruleVersion", "outcome", "explanation" }) S(writer, x, name); writer.WriteEndObject(); } writer.WriteEndArray();
        writer.WritePropertyName("participants"); writer.WriteStartArray(); foreach (var x in root.GetProperty("participants").EnumerateArray().OrderBy(x => x.GetProperty("strategyRunId").GetString(), StringComparer.Ordinal)) { writer.WriteStartObject(); foreach (var name in new[] { "strategyId", "strategyVersion", "strategyRunId", "snapshotId" }) S(writer, x, name); N(writer, x, "snapshotRevision"); foreach (var name in new[] { "snapshotFingerprint", "resultingRunInput", "mappingSetId" }) S(writer, x, name); N(writer, x, "mappingRevision"); writer.WriteEndObject(); } writer.WriteEndArray();
        T(writer, root, "effectiveAt"); T(writer, root, "recordedAt"); T(writer, root, "knowledgeCutoff"); S(writer, root, "provenance"); S(writer, root, "decision"); writer.WriteEndObject(); writer.Flush(); return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Object(JsonElement x, string name, string[] fields) { if (x.ValueKind != JsonValueKind.Object || x.EnumerateObject().Count() != fields.Length || fields.Any(f => !x.TryGetProperty(f, out _)) || x.EnumerateObject().Any(p => !fields.Contains(p.Name, StringComparer.Ordinal))) Fail(name + " has unknown or missing fields."); }
    private static void OptionalReference(JsonElement x) { if (x.ValueKind == JsonValueKind.Null) return; Object(x, "supersedes", ["adapterInputId", "revision"]); Text(x, "adapterInputId"); Positive(x, "revision"); }
    private static void RiskInput(JsonElement x) { Object(x, "riskInputSnapshot", ["snapshotId", "effectiveAt", "recordedAt", "provenance"]); Text(x, "snapshotId"); Utc(x, "effectiveAt"); Utc(x, "recordedAt"); Text(x, "provenance"); }
    private static void RiskRule(JsonElement x) { Object(x, "riskRule", ["ruleId", "ruleVersion", "outcome", "explanation"]); Text(x, "ruleId"); Text(x, "ruleVersion"); Enum(x, "outcome", ["Pass", "Block", "Indeterminate"]); Text(x, "explanation"); }
    private static void Participant(JsonElement x) { Object(x, "participant", ["strategyId", "strategyVersion", "strategyRunId", "snapshotId", "snapshotRevision", "snapshotFingerprint", "resultingRunInput", "mappingSetId", "mappingRevision"]); foreach (var n in new[] { "strategyId", "strategyVersion", "strategyRunId", "snapshotId", "resultingRunInput", "mappingSetId" }) Text(x, n); Positive(x, "snapshotRevision"); Positive(x, "mappingRevision"); Hash(x, "snapshotFingerprint"); }
    private static void Array(JsonElement x, string n, Action<JsonElement> item) { var a = x.GetProperty(n); if (a.ValueKind != JsonValueKind.Array || a.GetArrayLength() == 0) Fail(n + " must be a non-empty array."); foreach (var i in a.EnumerateArray()) item(i); }
    private static decimal Decimal(JsonElement x, string n) { Text(x, n); var v = x.GetProperty(n).GetString()!; if (!decimal.TryParse(v, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d) || d.ToString("0.############################", CultureInfo.InvariantCulture) != v) Fail(n + " must be canonical base-10 decimal."); return d; }
    private static void Utc(JsonElement x, string n) { Text(x, n); if (!DateTimeOffset.TryParse(x.GetProperty(n).GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var t) || t.Offset != TimeSpan.Zero) Fail(n + " must be UTC."); }
    private static void Hash(JsonElement x, string n) { Text(x, n); var v = x.GetProperty(n).GetString()!; if (v.Length != 64 || v.Any(c => !((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))) Fail(n + " must be lower-case SHA-256 hex."); }
    private static void Text(JsonElement x, string n) { if (x.GetProperty(n).ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(x.GetProperty(n).GetString())) Fail(n + " must be a non-empty string."); }
    private static void OptionalText(JsonElement x) { if (x.ValueKind != JsonValueKind.Null && (x.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(x.GetString()))) Fail("optional text is invalid."); }
    private static void Positive(JsonElement x, string n) { if (x.GetProperty(n).ValueKind != JsonValueKind.Number || !x.GetProperty(n).TryGetInt64(out var v) || v <= 0) Fail(n + " must be a positive integer."); }
    private static void Enum(JsonElement x, string n, string[] values) { Text(x, n); if (!values.Contains(x.GetProperty(n).GetString(), StringComparer.Ordinal)) Fail(n + " is invalid."); }
    private static void S(Utf8JsonWriter w, JsonElement x, string n) => w.WriteString(n, x.GetProperty(n).GetString()); private static void T(Utf8JsonWriter w, JsonElement x, string n) => w.WriteString(n, DateTimeOffset.Parse(x.GetProperty(n).GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)); private static void N(Utf8JsonWriter w, JsonElement x, string n) => w.WriteNumber(n, x.GetProperty(n).GetInt64()); private static void Fail(string m) => throw new ArgumentException(m, "json");
}

public enum CanonicalInputReceiptResult { Accepted, Duplicate }
public interface ICanonicalInputReceiptStore { CanonicalInputReceiptResult Record(CanonicalPostRiskInput input); }
public sealed class InMemoryCanonicalInputReceiptStore : ICanonicalInputReceiptStore
{
    private readonly Dictionary<(string Id, long Revision), string> receipts = [];
    public CanonicalInputReceiptResult Record(CanonicalPostRiskInput input)
    {
        var key = (input.AdapterInputId, input.Revision);
        if (receipts.TryGetValue(key, out var fingerprint)) { if (fingerprint != input.Fingerprint) throw new InvalidOperationException("Conflicting canonical input fingerprint."); return CanonicalInputReceiptResult.Duplicate; }
        receipts.Add(key, input.Fingerprint); return CanonicalInputReceiptResult.Accepted;
    }
}

public sealed record MandateFundMapping(string MandateId, FundId FundId, string MappingId, long Revision, DateTimeOffset EffectiveFromUtc, DateTimeOffset? EffectiveToUtc, string Provenance);
public sealed record InstrumentExecutionMapping(string CanonicalInstrumentId, InstrumentId InstrumentId, VenueId VenueId, VenueInstrumentId VenueInstrumentId, string MappingId, long Revision, DateTimeOffset EffectiveFromUtc, DateTimeOffset? EffectiveToUtc, string Provenance);
public sealed record RetainedExecutionContext(FundId FundId, VenueId VenueId, decimal NavUsd, int FrequencyMinutes, TargetQuantityMode TargetQuantityMode, BrokerAccountId BrokerAccountId, string RoutingContext, string ContextId, long Revision, DateTimeOffset EffectiveFromUtc, DateTimeOffset? EffectiveToUtc, string Provenance);
public sealed record ResolvedCanonicalExecutionContext(MandateFundMapping MandateFund, InstrumentExecutionMapping Instrument, RetainedExecutionContext Execution);
public interface ICanonicalExecutionContextResolver { ResolvedCanonicalExecutionContext Resolve(CanonicalPostRiskInput input, DateTimeOffset asOfUtc); }
public sealed class InMemoryCanonicalExecutionContextResolver(IEnumerable<MandateFundMapping> mandateFunds, IEnumerable<InstrumentExecutionMapping> instruments, IEnumerable<RetainedExecutionContext> contexts) : ICanonicalExecutionContextResolver
{
    public ResolvedCanonicalExecutionContext Resolve(CanonicalPostRiskInput input, DateTimeOffset asOfUtc)
    {
        if (asOfUtc.Offset != TimeSpan.Zero) throw new ArgumentException("An explicit UTC as-of time is required.", nameof(asOfUtc));
        var mandate = One(mandateFunds.Where(x => x.MandateId == input.MandateId && Active(x.EffectiveFromUtc, x.EffectiveToUtc, asOfUtc)), "Mandate-to-Fund mapping");
        var instrument = One(instruments.Where(x => x.CanonicalInstrumentId == input.InstrumentId && Active(x.EffectiveFromUtc, x.EffectiveToUtc, asOfUtc)), "Instrument execution mapping");
        var context = One(contexts.Where(x => x.FundId == mandate.FundId && x.VenueId == instrument.VenueId && Active(x.EffectiveFromUtc, x.EffectiveToUtc, asOfUtc)), "execution context");
        if (!Lineage(mandate.MappingId, mandate.Revision, mandate.EffectiveFromUtc, mandate.EffectiveToUtc, mandate.Provenance) || !Lineage(instrument.MappingId, instrument.Revision, instrument.EffectiveFromUtc, instrument.EffectiveToUtc, instrument.Provenance) || !Lineage(context.ContextId, context.Revision, context.EffectiveFromUtc, context.EffectiveToUtc, context.Provenance)) throw new InvalidOperationException("Mapping or execution-context lineage is invalid.");
        if (context.NavUsd <= 0 || context.FrequencyMinutes <= 0 || !Enum.IsDefined(context.TargetQuantityMode) || string.IsNullOrWhiteSpace(context.RoutingContext)) throw new InvalidOperationException("Execution context is invalid.");
        return new(mandate, instrument, context);
    }
    private static bool Active(DateTimeOffset from, DateTimeOffset? to, DateTimeOffset at) => from.Offset == TimeSpan.Zero && (to is null || to.Value.Offset == TimeSpan.Zero) && from <= at && (to is null || at < to.Value);
    private static bool Lineage(string id, long revision, DateTimeOffset from, DateTimeOffset? to, string provenance) => !string.IsNullOrWhiteSpace(id) && revision > 0 && !string.IsNullOrWhiteSpace(provenance) && from.Offset == TimeSpan.Zero && (to is null || (to.Value.Offset == TimeSpan.Zero && to.Value > from));
    private static T One<T>(IEnumerable<T> values, string name) { var all = values.ToList(); return all.Count == 1 ? all[0] : throw new InvalidOperationException(name + " is missing or ambiguous."); }
}
