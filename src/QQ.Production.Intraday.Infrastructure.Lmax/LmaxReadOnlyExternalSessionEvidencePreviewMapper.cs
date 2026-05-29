using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxReadOnlyExternalSessionEvidencePreviewIssue(
    string Severity,
    string Path,
    string Code,
    string Message);

public sealed record LmaxReadOnlyExternalSessionEvidencePreviewResult(
    string SchemaVersion,
    string EvidenceMode,
    string NormalizedEvidenceJson,
    LmaxReadOnlyRuntimeEvidenceBatchPreview Batch,
    int InputEventCount,
    int ValidationErrorCount,
    int ValidationWarningCount,
    int ValidationInfoCount,
    bool NoSensitiveContent,
    string Message,
    IReadOnlyList<LmaxReadOnlyExternalSessionEvidencePreviewIssue> Issues);

public sealed class LmaxReadOnlyExternalSessionEvidencePreviewMapper
{
    public const string SchemaVersion = "lmax-fix-lifecycle-evidence-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public LmaxReadOnlyExternalSessionEvidencePreviewResult Map(
        IReadOnlyList<LmaxReadOnlyExternalSessionEvent> events,
        string reason = "Preview LMAX read-only runtime fake transport evidence")
    {
        var now = events.Count == 0 ? DateTimeOffset.UtcNow : events.Min(x => x.ObservedAtUtc);
        var completedAt = events.Count == 0 ? now : events.Max(x => x.ObservedAtUtc);
        var warnings = new JsonArray();
        var issues = new List<LmaxReadOnlyExternalSessionEvidencePreviewIssue>();
        var marketData = BuildMarketData(events.Where(x => x.EventType == LmaxReadOnlyExternalSessionEventType.MarketDataSnapshot).ToList());
        var orderStatuses = new JsonArray();
        var tradeCaptureReports = new JsonArray();
        var protocolRejects = new JsonArray();

        foreach (var item in events)
        {
            switch (item.EventType)
            {
                case LmaxReadOnlyExternalSessionEventType.TradeCaptureReport:
                    tradeCaptureReports.Add(BuildTradeCapture(item));
                    break;
                case LmaxReadOnlyExternalSessionEventType.OrderStatusReport:
                    orderStatuses.Add(BuildOrderStatus(item));
                    break;
                case LmaxReadOnlyExternalSessionEventType.ProtocolReject:
                    protocolRejects.Add(BuildProtocolReject(item));
                    break;
                case LmaxReadOnlyExternalSessionEventType.SessionWarning:
                    warnings.Add($"Session warning from fake transport: {ExtractString(item, "message") ?? item.EventId}");
                    break;
                case LmaxReadOnlyExternalSessionEventType.SessionError:
                    warnings.Add($"Session error from fake transport: {ExtractString(item, "message") ?? item.EventId}");
                    issues.Add(new LmaxReadOnlyExternalSessionEvidencePreviewIssue("Warning", "$.warnings", "SessionErrorCapturedAsWarning", $"Session error event {item.EventId} was captured as preview warning only."));
                    break;
                case LmaxReadOnlyExternalSessionEventType.MarketDataSnapshot:
                    break;
                default:
                    warnings.Add($"Unknown fake transport event ignored: {item.EventId}");
                    issues.Add(new LmaxReadOnlyExternalSessionEvidencePreviewIssue("Warning", "$.events", "UnknownEventIgnored", $"Unknown event {item.EventId} was not mapped to replay arrays."));
                    break;
            }
        }

        var evidenceMode = InferEvidenceMode(marketData is not null ? 1 : 0, orderStatuses.Count, tradeCaptureReports.Count, protocolRejects.Count);
        var root = new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["createdAtUtc"] = FormatTimestamp(now),
            ["capturedAtUtc"] = FormatTimestamp(now),
            ["source"] = "RuntimeFakeTransport",
            ["inputSource"] = "LabEvidenceFile",
            ["reason"] = reason,
            ["environment"] = "LocalFake",
            ["captureMode"] = "FakeRuntimePreview",
            ["redaction"] = "SanitizedNoCredentialsNoRawLogon",
            ["dryRun"] = true,
            ["instrumentSymbol"] = FirstNonEmpty(events.Select(x => x.Symbol)) ?? "EURUSD",
            ["securityId"] = FirstNonEmpty(events.Select(x => x.InstrumentId)) ?? "4001",
            ["slashSymbol"] = "EUR/USD",
            ["startedAtUtc"] = FormatTimestamp(now),
            ["completedAtUtc"] = FormatTimestamp(completedAt),
            ["evidenceMode"] = evidenceMode,
            ["executionReports"] = new JsonArray(),
            ["orderStatuses"] = orderStatuses,
            ["tradeCaptureReports"] = tradeCaptureReports,
            ["protocolRejects"] = protocolRejects,
            ["warnings"] = warnings
        };

        if (marketData is not null)
        {
            root["marketData"] = marketData;
        }

        var json = root.ToJsonString(JsonOptions);
        var sensitive = ContainsSensitiveEvidence(json);
        if (sensitive)
        {
            issues.Add(new LmaxReadOnlyExternalSessionEvidencePreviewIssue("Error", "$", "SensitiveContentDetected", "Mapped evidence contains credential-shaped content."));
        }

        var batchWarnings = warnings.Select(x => x?.GetValue<string>() ?? string.Empty).Where(x => x.Length > 0).ToList();
        var batch = new LmaxReadOnlyRuntimeEvidenceBatchPreview(
            "runtime-fake-preview-" + Guid.NewGuid().ToString("N"),
            SchemaVersion,
            evidenceMode,
            now,
            ExecutionReportCount: 0,
            OrderStatusCount: orderStatuses.Count,
            TradeCaptureReportCount: tradeCaptureReports.Count,
            ProtocolRejectCount: protocolRejects.Count,
            MarketDataSnapshotCount: marketData is null ? 0 : 1,
            Sanitized: !sensitive,
            ContainsRawFix: false,
            batchWarnings);

        return new LmaxReadOnlyExternalSessionEvidencePreviewResult(
            SchemaVersion,
            evidenceMode,
            json,
            batch,
            events.Count,
            issues.Count(x => string.Equals(x.Severity, "Error", StringComparison.OrdinalIgnoreCase)),
            issues.Count(x => string.Equals(x.Severity, "Warning", StringComparison.OrdinalIgnoreCase)) + batchWarnings.Count,
            1,
            !sensitive,
            sensitive ? "Fake transport evidence preview failed sensitive-content validation." : "Fake transport events mapped to sanitized evidence preview. No shadow replay submit occurred.",
            issues);
    }

    public LmaxReadOnlyExternalSessionEvidencePreviewResult Map(
        LmaxReadOnlyExternalSessionFakeTransportResult transportResult,
        string reason = "Preview LMAX read-only runtime fake transport evidence")
        => Map(transportResult.Events, reason);

    private static JsonObject? BuildMarketData(IReadOnlyList<LmaxReadOnlyExternalSessionEvent> events)
    {
        if (events.Count == 0)
        {
            return null;
        }

        var first = events[0];
        var bestBid = ExtractDecimal(first, "bestBid");
        var bestAsk = ExtractDecimal(first, "bestAsk");
        var mid = bestBid is not null && bestAsk is not null ? decimal.Round((bestBid.Value + bestAsk.Value) / 2m, 5) : ExtractDecimal(first, "mid");
        var entries = new JsonArray();
        if (bestBid is not null)
        {
            entries.Add(new JsonObject
            {
                ["symbol"] = first.Symbol ?? "EUR/USD",
                ["securityId"] = first.InstrumentId ?? "4001",
                ["entryType"] = "0",
                ["price"] = bestBid.Value,
                ["size"] = ExtractDecimal(first, "bidSize") ?? 0m
            });
        }

        if (bestAsk is not null)
        {
            entries.Add(new JsonObject
            {
                ["symbol"] = first.Symbol ?? "EUR/USD",
                ["securityId"] = first.InstrumentId ?? "4001",
                ["entryType"] = "1",
                ["price"] = bestAsk.Value,
                ["size"] = ExtractDecimal(first, "askSize") ?? 0m
            });
        }

        return new JsonObject
        {
            ["status"] = "Ok",
            ["snapshotReceived"] = true,
            ["bestBid"] = bestBid,
            ["bestAsk"] = bestAsk,
            ["mid"] = mid,
            ["entryCount"] = entries.Count,
            ["entries"] = entries
        };
    }

    private static JsonObject BuildTradeCapture(LmaxReadOnlyExternalSessionEvent item)
        => new()
        {
            ["execId"] = item.BrokerExecutionId ?? ExtractString(item, "execId") ?? item.EventId,
            ["secondaryExecId"] = ExtractString(item, "secondaryExecId"),
            ["brokerOrderId"] = item.BrokerOrderId ?? ExtractString(item, "brokerOrderId"),
            ["clientOrderId"] = item.ClientOrderId ?? ExtractString(item, "clientOrderId"),
            ["symbol"] = item.Symbol ?? ExtractString(item, "symbol") ?? "EURUSD",
            ["side"] = NormalizeSide(ExtractString(item, "side")),
            ["lastQty"] = ExtractDecimal(item, "lastQty") ?? 0m,
            ["lastPx"] = ExtractDecimal(item, "lastPx") ?? 0m,
            ["tradeDate"] = NormalizeTradeDate(ExtractString(item, "tradeDate"), item.ObservedAtUtc),
            ["transactTimeUtc"] = FormatTimestamp(ExtractTimestamp(item, "transactTimeUtc") ?? item.ObservedAtUtc),
            ["tradeUti"] = ExtractString(item, "tradeUti"),
            ["lastReportRequested"] = ExtractBool(item, "lastReportRequested") ?? true,
            ["payload"] = new JsonObject
            {
                ["securityId"] = item.InstrumentId ?? ExtractString(item, "securityId") ?? "4001",
                ["securityIdSource"] = ExtractString(item, "securityIdSource") ?? "8",
                ["tradeReportId"] = ExtractString(item, "tradeReportId") ?? item.EventId
            }
        };

    private static JsonObject BuildOrderStatus(LmaxReadOnlyExternalSessionEvent item)
        => new()
        {
            ["brokerOrderId"] = item.BrokerOrderId ?? ExtractString(item, "brokerOrderId"),
            ["clientOrderId"] = item.ClientOrderId ?? ExtractString(item, "clientOrderId"),
            ["symbol"] = item.Symbol ?? ExtractString(item, "symbol") ?? "EURUSD",
            ["orderStatus"] = ExtractString(item, "orderStatus") ?? "Filled",
            ["cumQty"] = ExtractDecimal(item, "cumQty") ?? 0m,
            ["leavesQty"] = ExtractDecimal(item, "leavesQty") ?? 0m,
            ["transactTimeUtc"] = FormatTimestamp(ExtractTimestamp(item, "transactTimeUtc") ?? item.ObservedAtUtc),
            ["payload"] = new JsonObject
            {
                ["execId"] = ExtractString(item, "execId") ?? item.EventId,
                ["executionType"] = "OrderStatus",
                ["securityId"] = item.InstrumentId ?? ExtractString(item, "securityId") ?? "4001",
                ["securityIdSource"] = ExtractString(item, "securityIdSource") ?? "8"
            }
        };

    private static JsonObject BuildProtocolReject(LmaxReadOnlyExternalSessionEvent item)
        => new()
        {
            ["rejectId"] = item.EventId,
            ["observedAtUtc"] = FormatTimestamp(item.ObservedAtUtc),
            ["refMsgType"] = ExtractString(item, "refMsgType") ?? "AD",
            ["refSeqNum"] = ExtractString(item, "refSeqNum"),
            ["rejectContext"] = ExtractString(item, "rejectContext") ?? "ReadOnlyRecoveryRequest",
            ["message"] = ExtractString(item, "message") ?? "Synthetic protocol reject from fake transport.",
            ["payload"] = new JsonObject
            {
                ["sanitized"] = true
            }
        };

    private static string InferEvidenceMode(int marketData, int orderStatuses, int tradeCaptureReports, int protocolRejects)
    {
        if (orderStatuses == 0 && tradeCaptureReports == 0 && protocolRejects == 0)
        {
            return marketData > 0 ? "MarketDataOnly" : "EmptyReadOnly";
        }

        var populated = new[] { orderStatuses > 0, tradeCaptureReports > 0, protocolRejects > 0 }.Count(x => x);
        if (populated > 1 || marketData > 0 && populated > 0) return "MixedReadOnly";
        if (protocolRejects > 0) return "ProtocolRejectOnly";
        if (tradeCaptureReports > 0) return "TradeCaptureOnly";
        if (orderStatuses > 0) return "OrderStatusOnly";
        return "MixedReadOnly";
    }

    private static JsonObject Payload(LmaxReadOnlyExternalSessionEvent item)
    {
        try
        {
            return JsonNode.Parse(item.SanitizedPayloadJson)?.AsObject() ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static string? ExtractString(LmaxReadOnlyExternalSessionEvent item, string name)
        => Payload(item)[name]?.GetValue<string>();

    private static decimal? ExtractDecimal(LmaxReadOnlyExternalSessionEvent item, string name)
    {
        var node = Payload(item)[name];
        if (node is null) return null;
        if (node is JsonValue value && value.TryGetValue<decimal>(out var number)) return number;
        return decimal.TryParse(node.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static bool? ExtractBool(LmaxReadOnlyExternalSessionEvent item, string name)
    {
        var node = Payload(item)[name];
        if (node is null) return null;
        if (node is JsonValue value && value.TryGetValue<bool>(out var boolean)) return boolean;
        return bool.TryParse(node.ToString(), out var parsed) ? parsed : null;
    }

    private static DateTimeOffset? ExtractTimestamp(LmaxReadOnlyExternalSessionEvent item, string name)
        => DateTimeOffset.TryParse(ExtractString(item, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;

    private static string NormalizeSide(string? side)
        => side switch
        {
            "1" => "Buy",
            "2" => "Sell",
            "Buy" or "Sell" => side,
            "buy" => "Buy",
            "sell" => "Sell",
            _ => "Buy"
        };

    private static string NormalizeTradeDate(string? tradeDate, DateTimeOffset fallback)
    {
        if (DateOnly.TryParseExact(tradeDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var compact))
        {
            return compact.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (DateOnly.TryParseExact(tradeDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var iso))
        {
            return iso.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return DateOnly.FromDateTime(fallback.UtcDateTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string FormatTimestamp(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string? FirstNonEmpty(IEnumerable<string?> values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    private static bool ContainsSensitiveEvidence(string json)
    {
        var sensitiveTerms = new[]
        {
            "pass" + "word",
            "pass" + "wd",
            "sec" + "ret",
            "api" + "Key",
            "api" + "_key",
            "author" + "ization",
            "bearer ",
            "554="
        };

        return sensitiveTerms.Any(term => json.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
