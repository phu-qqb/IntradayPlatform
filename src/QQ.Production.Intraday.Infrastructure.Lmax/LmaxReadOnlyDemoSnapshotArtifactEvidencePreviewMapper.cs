using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewResult(
    bool IsValid,
    string SchemaVersion,
    string EvidenceMode,
    string NormalizedEvidenceJson,
    LmaxReadOnlyRuntimeEvidenceBatchPreview Batch,
    int ValidationErrorCount,
    int ValidationWarningCount,
    bool NoSensitiveContent,
    string Message,
    IReadOnlyList<LmaxReadOnlyDemoSnapshotArtifactValidationIssue> ArtifactIssues);

public static class LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper
{
    public const string SchemaVersion = "lmax-fix-lifecycle-evidence-v1";
    public const string EvidenceMode = "MarketDataOnly";
    public const string Source = "RuntimeDemoReadOnlySnapshotArtifact";
    public const string CaptureMode = "RuntimeDemoReadOnlySnapshotPreview";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewResult MapFile(
        string artifactFile,
        string? repositoryRoot = null,
        IEnumerable<string>? forbiddenSensitiveValues = null,
        string? reason = null)
    {
        var artifactValidation = LmaxReadOnlyDemoSnapshotArtifactValidator.ValidateFile(
            artifactFile,
            repositoryRoot,
            forbiddenSensitiveValues,
            requireSuccessfulSnapshot: true);

        if (!artifactValidation.IsValid || !artifactValidation.IsSuccessfulSnapshot)
        {
            return Invalid(artifactValidation, "Snapshot artifact failed successful sanitized artifact validation; evidence preview was not mapped.");
        }

        var json = File.ReadAllText(artifactFile);
        return MapValidatedJson(json, artifactValidation, reason);
    }

    public static LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewResult MapJson(
        string artifactJson,
        IEnumerable<string>? forbiddenSensitiveValues = null,
        string? reason = null)
    {
        var artifactValidation = LmaxReadOnlyDemoSnapshotArtifactValidator.ValidateJson(
            artifactJson,
            forbiddenSensitiveValues,
            requireSuccessfulSnapshot: true);

        if (!artifactValidation.IsValid || !artifactValidation.IsSuccessfulSnapshot)
        {
            return Invalid(artifactValidation, "Snapshot artifact failed successful sanitized artifact validation; evidence preview was not mapped.");
        }

        return MapValidatedJson(artifactJson, artifactValidation, reason);
    }

    public static LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewResult MapEmptyBookJson(
        string artifactJson,
        IEnumerable<string>? forbiddenSensitiveValues = null,
        string? reason = null)
    {
        var result = ReadGbpusdResult(artifactJson);
        var validation = LmaxReadOnlyGbpusdManualSnapshotResultValidator.Validate(result, artifactJson);
        if (validation.FinalDecision == LmaxReadOnlyGbpusdManualSnapshotResultDecision.FAIL || result.Status != "CompletedWithEmptyBook")
        {
            return Invalid(
                new(
                    IsValid: false,
                    IsSuccessfulSnapshot: false,
                    Status: result.Status,
                    SnapshotReceived: result.SnapshotReceived,
                    NoSensitiveContent: result.NoSensitiveContent,
                    RedactionStatus: result.RedactionStatus,
                    Issues:
                    [
                        new(
                            LmaxReadOnlyDemoSnapshotArtifactValidationSeverity.Error,
                            "GbpusdEmptyBookValidationFailed",
                            "$",
                            "GBPUSD empty-book artifact failed local sanitized result validation.")
                    ]),
                "GBPUSD empty-book artifact failed sanitized validation; evidence preview was not mapped.");
        }

        foreach (var sensitiveValue in forbiddenSensitiveValues ?? [])
        {
            if (!string.IsNullOrWhiteSpace(sensitiveValue) && artifactJson.Contains(sensitiveValue, StringComparison.Ordinal))
            {
                return Invalid(
                    new(false, false, result.Status, result.SnapshotReceived, result.NoSensitiveContent, result.RedactionStatus,
                    [
                        new(LmaxReadOnlyDemoSnapshotArtifactValidationSeverity.Error, "ForbiddenSensitiveValue", "$", "Artifact contains a configured sensitive sentinel value.")
                    ]),
                    "GBPUSD empty-book artifact contains forbidden sentinel content; evidence preview was not mapped.");
            }
        }

        return MapEmptyBookValidatedJson(artifactJson, result, reason);
    }

    public static LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewResult MapEmptyBookFile(
        string artifactFile,
        IEnumerable<string>? forbiddenSensitiveValues = null,
        string? reason = null)
        => MapEmptyBookJson(File.ReadAllText(artifactFile), forbiddenSensitiveValues, reason);

    public static LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewResult MapGbpusdMarketHoursJson(
        string artifactJson,
        IEnumerable<string>? forbiddenSensitiveValues = null,
        string? reason = null)
    {
        var result = ReadGbpusdResult(artifactJson);
        if (result.Status == "CompletedWithEmptyBook")
        {
            return MapEmptyBookJson(artifactJson, forbiddenSensitiveValues, reason);
        }

        var validation = LmaxReadOnlyGbpusdManualSnapshotResultValidator.Validate(result, artifactJson);
        if (validation.FinalDecision == LmaxReadOnlyGbpusdManualSnapshotResultDecision.FAIL
            || !result.SnapshotReceived
            || result.EntryCount <= 0
            || result.BestBid is null
            || result.BestAsk is null
            || result.Mid is null)
        {
            return Invalid(
                new(
                    IsValid: false,
                    IsSuccessfulSnapshot: false,
                    Status: result.Status,
                    SnapshotReceived: result.SnapshotReceived,
                    NoSensitiveContent: result.NoSensitiveContent,
                    RedactionStatus: result.RedactionStatus,
                    Issues:
                    [
                        new(
                            LmaxReadOnlyDemoSnapshotArtifactValidationSeverity.Error,
                            "GbpusdMarketHoursValidationFailed",
                            "$",
                            "GBPUSD market-hours artifact failed local sanitized result validation.")
                    ]),
                "GBPUSD market-hours artifact failed sanitized validation; evidence preview was not mapped.");
        }

        foreach (var sensitiveValue in forbiddenSensitiveValues ?? [])
        {
            if (!string.IsNullOrWhiteSpace(sensitiveValue) && artifactJson.Contains(sensitiveValue, StringComparison.Ordinal))
            {
                return Invalid(
                    new(false, false, result.Status, result.SnapshotReceived, result.NoSensitiveContent, result.RedactionStatus,
                    [
                        new(LmaxReadOnlyDemoSnapshotArtifactValidationSeverity.Error, "ForbiddenSensitiveValue", "$", "Artifact contains a configured sensitive sentinel value.")
                    ]),
                    "GBPUSD market-hours artifact contains forbidden sentinel content; evidence preview was not mapped.");
            }
        }

        return MapGbpusdMarketHoursValidatedJson(result, reason);
    }

    public static LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewResult MapGbpusdMarketHoursFile(
        string artifactFile,
        IEnumerable<string>? forbiddenSensitiveValues = null,
        string? reason = null)
        => MapGbpusdMarketHoursJson(File.ReadAllText(artifactFile), forbiddenSensitiveValues, reason);

    public static LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewResult MapAdditionalInstrumentJson(
        string artifactJson,
        IEnumerable<string>? forbiddenSensitiveValues = null,
        string? reason = null)
    {
        var review = LmaxReadOnlyAdditionalInstrumentSnapshotClosureValidator.ReviewArtifactJson(artifactJson);
        if (review.Decision == LmaxReadOnlyAdditionalInstrumentSnapshotClosureDecision.FAIL)
        {
            return Invalid(
                new(
                    IsValid: false,
                    IsSuccessfulSnapshot: false,
                    Status: review.Result.Status,
                    SnapshotReceived: review.Result.SnapshotReceived,
                    NoSensitiveContent: review.Result.NoSensitiveContent,
                    RedactionStatus: review.Result.RedactionStatus,
                    Issues: review.Issues.Select(x => new LmaxReadOnlyDemoSnapshotArtifactValidationIssue(
                        LmaxReadOnlyDemoSnapshotArtifactValidationSeverity.Error,
                        "AdditionalInstrumentValidationFailed",
                        "$",
                        x)).ToArray()),
                "Additional-instrument artifact failed sanitized validation; evidence preview was not mapped.");
        }

        foreach (var sensitiveValue in forbiddenSensitiveValues ?? [])
        {
            if (!string.IsNullOrWhiteSpace(sensitiveValue) && artifactJson.Contains(sensitiveValue, StringComparison.Ordinal))
            {
                return Invalid(
                    new(false, false, review.Result.Status, review.Result.SnapshotReceived, review.Result.NoSensitiveContent, review.Result.RedactionStatus,
                    [
                        new(LmaxReadOnlyDemoSnapshotArtifactValidationSeverity.Error, "ForbiddenSensitiveValue", "$", "Artifact contains a configured sensitive sentinel value.")
                    ]),
                    "Additional-instrument artifact contains forbidden sentinel content; evidence preview was not mapped.");
            }
        }

        return MapAdditionalInstrumentValidatedResult(review.Result, review.Classification, reason);
    }

    public static LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewResult MapAdditionalInstrumentFile(
        string artifactFile,
        IEnumerable<string>? forbiddenSensitiveValues = null,
        string? reason = null)
        => MapAdditionalInstrumentJson(File.ReadAllText(artifactFile), forbiddenSensitiveValues, reason);

    private static LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewResult MapValidatedJson(
        string artifactJson,
        LmaxReadOnlyDemoSnapshotArtifactValidationResult artifactValidation,
        string? reason)
    {
        using var document = JsonDocument.Parse(artifactJson);
        var artifact = document.RootElement;
        var capturedAt = GetTimestamp(artifact, "snapshotReceivedAtUtc") ?? GetTimestamp(artifact, "completedAtUtc") ?? DateTimeOffset.UtcNow;
        var createdAt = GetTimestamp(artifact, "completedAtUtc") ?? capturedAt;
        var securityId = GetString(artifact, "securityId", "4001");
        var instrument = GetString(artifact, "instrument", "EURUSD");
        var slashSymbol = GetString(artifact, "slashSymbol", "EUR/USD");
        var bestBid = GetDecimal(artifact, "bestBid");
        var bestAsk = GetDecimal(artifact, "bestAsk");
        var mid = GetDecimal(artifact, "mid");
        var entryCount = GetInt(artifact, "entryCount");

        var root = new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["createdAtUtc"] = FormatTimestamp(createdAt),
            ["capturedAtUtc"] = FormatTimestamp(capturedAt),
            ["source"] = Source,
            ["inputSource"] = "LabEvidenceFile",
            ["reason"] = string.IsNullOrWhiteSpace(reason) ? "Preview sanitized runtime Demo read-only market-data snapshot artifact" : reason,
            ["environment"] = "Demo",
            ["captureMode"] = CaptureMode,
            ["redaction"] = "SanitizedNoCredentialsNoRawLogon",
            ["redactionStatus"] = "Redacted",
            ["noSensitiveContent"] = true,
            ["dryRun"] = true,
            ["shadowReplaySubmitAttempted"] = false,
            ["tradingMutationAttempted"] = false,
            ["orderSubmissionAttempted"] = false,
            ["instrument"] = instrument,
            ["instrumentSymbol"] = instrument,
            ["securityId"] = securityId,
            ["lmaxInstrumentId"] = securityId,
            ["slashSymbol"] = slashSymbol,
            ["evidenceMode"] = EvidenceMode,
            ["marketData"] = new JsonObject
            {
                ["status"] = "Ok",
                ["snapshotReceived"] = true,
                ["bestBid"] = bestBid,
                ["bestAsk"] = bestAsk,
                ["mid"] = mid,
                ["entryCount"] = entryCount,
                ["entries"] = BuildEntries(slashSymbol, securityId, bestBid, bestAsk, entryCount)
            },
            ["executionReports"] = new JsonArray(),
            ["orderStatuses"] = new JsonArray(),
            ["tradeCaptureReports"] = new JsonArray(),
            ["protocolRejects"] = new JsonArray(),
            ["warnings"] = new JsonArray()
        };

        var normalizedEvidenceJson = root.ToJsonString(JsonOptions);
        var fixturePreview = WriteAndValidate(normalizedEvidenceJson);
        var containsSensitive = ContainsSensitiveEvidence(normalizedEvidenceJson);
        var validationErrors = fixturePreview.ErrorCount + (containsSensitive ? 1 : 0);
        var warnings = fixturePreview.WarningCount;
        var batch = new LmaxReadOnlyRuntimeEvidenceBatchPreview(
            "runtime-demo-snapshot-preview-" + Guid.NewGuid().ToString("N"),
            SchemaVersion,
            EvidenceMode,
            createdAt,
            ExecutionReportCount: 0,
            OrderStatusCount: 0,
            TradeCaptureReportCount: 0,
            ProtocolRejectCount: 0,
            MarketDataSnapshotCount: 1,
            Sanitized: !containsSensitive,
            ContainsRawFix: false,
            Warnings: fixturePreview.Warnings);

        return new(
            validationErrors == 0,
            SchemaVersion,
            EvidenceMode,
            normalizedEvidenceJson,
            batch,
            validationErrors,
            warnings,
            !containsSensitive,
            validationErrors == 0
                ? "Sanitized Demo snapshot artifact mapped to MarketDataOnly evidence preview. No shadow replay submit occurred."
                : "Mapped evidence preview failed local validation.",
            artifactValidation.Issues);
    }

    private static LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewResult MapEmptyBookValidatedJson(
        string artifactJson,
        LmaxReadOnlyGbpusdManualSnapshotResult result,
        string? reason)
    {
        using var document = JsonDocument.Parse(artifactJson);
        var artifact = document.RootElement;
        var capturedAt = GetTimestamp(artifact, "snapshotReceivedAtUtc") ?? result.CompletedAtUtc;
        var createdAt = result.CompletedAtUtc;
        var warnings = result.Warnings ?? ["Market data snapshot received with no entries"];

        var root = new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["createdAtUtc"] = FormatTimestamp(createdAt),
            ["capturedAtUtc"] = FormatTimestamp(capturedAt),
            ["source"] = Source,
            ["inputSource"] = "LabEvidenceFile",
            ["reason"] = string.IsNullOrWhiteSpace(reason) ? "Preview sanitized GBPUSD empty-book Demo read-only market-data snapshot artifact" : reason,
            ["environment"] = "Demo",
            ["captureMode"] = CaptureMode,
            ["redaction"] = "SanitizedNoCredentialsNoRawLogon",
            ["redactionStatus"] = "Redacted",
            ["noSensitiveContent"] = true,
            ["dryRun"] = true,
            ["shadowReplaySubmitAttempted"] = false,
            ["tradingMutationAttempted"] = false,
            ["orderSubmissionAttempted"] = false,
            ["instrument"] = result.Symbol,
            ["instrumentSymbol"] = result.Symbol,
            ["securityId"] = result.SecurityId,
            ["lmaxInstrumentId"] = result.SecurityId,
            ["slashSymbol"] = result.SlashSymbol,
            ["evidenceMode"] = EvidenceMode,
            ["marketData"] = new JsonObject
            {
                ["status"] = "EmptyBook",
                ["snapshotReceived"] = true,
                ["bestBid"] = null,
                ["bestAsk"] = null,
                ["mid"] = null,
                ["entryCount"] = 0,
                ["entries"] = new JsonArray()
            },
            ["executionReports"] = new JsonArray(),
            ["orderStatuses"] = new JsonArray(),
            ["tradeCaptureReports"] = new JsonArray(),
            ["protocolRejects"] = new JsonArray(),
            ["warnings"] = new JsonArray(warnings.Select(x => JsonValue.Create(x)).ToArray())
        };

        var normalizedEvidenceJson = root.ToJsonString(JsonOptions);
        var fixturePreview = WriteAndValidate(normalizedEvidenceJson);
        var containsSensitive = ContainsSensitiveEvidence(normalizedEvidenceJson);
        var validationErrors = fixturePreview.ErrorCount + (containsSensitive ? 1 : 0);
        var warningCount = fixturePreview.WarningCount + warnings.Count;
        var batch = new LmaxReadOnlyRuntimeEvidenceBatchPreview(
            "runtime-demo-snapshot-preview-" + Guid.NewGuid().ToString("N"),
            SchemaVersion,
            EvidenceMode,
            createdAt,
            ExecutionReportCount: 0,
            OrderStatusCount: 0,
            TradeCaptureReportCount: 0,
            ProtocolRejectCount: 0,
            MarketDataSnapshotCount: 1,
            Sanitized: !containsSensitive,
            ContainsRawFix: false,
            Warnings: fixturePreview.Warnings.Concat(warnings).ToList());

        return new(
            validationErrors == 0,
            SchemaVersion,
            EvidenceMode,
            normalizedEvidenceJson,
            batch,
            validationErrors,
            warningCount,
            !containsSensitive,
            validationErrors == 0
                ? "Sanitized GBPUSD empty-book Demo snapshot artifact mapped to MarketDataOnly evidence preview. No shadow replay submit occurred."
                : "Mapped empty-book evidence preview failed local validation.",
            []); 
    }

    private static LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewResult MapGbpusdMarketHoursValidatedJson(
        LmaxReadOnlyGbpusdManualSnapshotResult result,
        string? reason)
    {
        var createdAt = result.CompletedAtUtc == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : result.CompletedAtUtc;
        var bestBid = (decimal)result.BestBid!.Value;
        var bestAsk = (decimal)result.BestAsk!.Value;
        var mid = (decimal)result.Mid!.Value;

        var root = new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["createdAtUtc"] = FormatTimestamp(createdAt),
            ["capturedAtUtc"] = FormatTimestamp(createdAt),
            ["source"] = Source,
            ["inputSource"] = "LabEvidenceFile",
            ["reason"] = string.IsNullOrWhiteSpace(reason) ? "Preview sanitized GBPUSD market-hours Demo read-only market-data snapshot artifact" : reason,
            ["environment"] = "Demo",
            ["captureMode"] = CaptureMode,
            ["redaction"] = "SanitizedNoCredentialsNoRawLogon",
            ["redactionStatus"] = "Redacted",
            ["noSensitiveContent"] = true,
            ["dryRun"] = true,
            ["shadowReplaySubmitAttempted"] = false,
            ["tradingMutationAttempted"] = false,
            ["orderSubmissionAttempted"] = false,
            ["instrument"] = result.Symbol,
            ["instrumentSymbol"] = result.Symbol,
            ["securityId"] = result.SecurityId,
            ["lmaxInstrumentId"] = result.SecurityId,
            ["slashSymbol"] = result.SlashSymbol,
            ["evidenceMode"] = EvidenceMode,
            ["marketData"] = new JsonObject
            {
                ["status"] = "Ok",
                ["snapshotReceived"] = true,
                ["bestBid"] = bestBid,
                ["bestAsk"] = bestAsk,
                ["mid"] = mid,
                ["entryCount"] = result.EntryCount,
                ["entries"] = BuildEntries(result.SlashSymbol, result.SecurityId, bestBid, bestAsk, result.EntryCount)
            },
            ["executionReports"] = new JsonArray(),
            ["orderStatuses"] = new JsonArray(),
            ["tradeCaptureReports"] = new JsonArray(),
            ["protocolRejects"] = new JsonArray(),
            ["warnings"] = new JsonArray()
        };

        var normalizedEvidenceJson = root.ToJsonString(JsonOptions);
        var fixturePreview = WriteAndValidate(normalizedEvidenceJson);
        var containsSensitive = ContainsSensitiveEvidence(normalizedEvidenceJson);
        var validationErrors = fixturePreview.ErrorCount + (containsSensitive ? 1 : 0);
        var batch = new LmaxReadOnlyRuntimeEvidenceBatchPreview(
            "runtime-gbpusd-market-hours-preview-" + Guid.NewGuid().ToString("N"),
            SchemaVersion,
            EvidenceMode,
            createdAt,
            ExecutionReportCount: 0,
            OrderStatusCount: 0,
            TradeCaptureReportCount: 0,
            ProtocolRejectCount: 0,
            MarketDataSnapshotCount: 1,
            Sanitized: !containsSensitive,
            ContainsRawFix: false,
            Warnings: fixturePreview.Warnings);

        return new(
            validationErrors == 0,
            SchemaVersion,
            EvidenceMode,
            normalizedEvidenceJson,
            batch,
            validationErrors,
            fixturePreview.WarningCount,
            !containsSensitive,
            validationErrors == 0
                ? "Sanitized GBPUSD market-hours artifact mapped to MarketDataOnly evidence preview. No shadow replay submit occurred."
                : "Mapped evidence preview failed local validation.",
            []); 
    }

    private static LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewResult MapAdditionalInstrumentValidatedResult(
        LmaxReadOnlyAdditionalInstrumentManualSnapshotResult result,
        LmaxReadOnlyAdditionalInstrumentSnapshotClosureClassification classification,
        string? reason)
    {
        var createdAt = result.CompletedAtUtc == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : result.CompletedAtUtc;
        var isEmptyBook = classification == LmaxReadOnlyAdditionalInstrumentSnapshotClosureClassification.CompletedWithEmptyBook;
        var warnings = isEmptyBook
            ? (result.Warnings is { Count: > 0 } ? result.Warnings : ["Market data snapshot received with no entries"])
            : [];
        var bestBid = result.BestBid.HasValue ? (decimal?)result.BestBid.Value : null;
        var bestAsk = result.BestAsk.HasValue ? (decimal?)result.BestAsk.Value : null;
        var mid = result.Mid.HasValue ? (decimal?)result.Mid.Value : null;

        var root = new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["createdAtUtc"] = FormatTimestamp(createdAt),
            ["capturedAtUtc"] = FormatTimestamp(createdAt),
            ["source"] = Source,
            ["inputSource"] = "LabEvidenceFile",
            ["reason"] = string.IsNullOrWhiteSpace(reason) ? $"Preview sanitized {result.Symbol} Demo read-only market-data snapshot artifact" : reason,
            ["environment"] = "Demo",
            ["captureMode"] = CaptureMode,
            ["redaction"] = "SanitizedNoCredentialsNoRawLogon",
            ["redactionStatus"] = "Redacted",
            ["noSensitiveContent"] = true,
            ["dryRun"] = true,
            ["shadowReplaySubmitAttempted"] = false,
            ["tradingMutationAttempted"] = false,
            ["orderSubmissionAttempted"] = false,
            ["instrument"] = result.Symbol,
            ["instrumentSymbol"] = result.Symbol,
            ["securityId"] = result.SecurityId,
            ["lmaxInstrumentId"] = result.SecurityId,
            ["slashSymbol"] = result.SlashSymbol,
            ["evidenceMode"] = EvidenceMode,
            ["marketData"] = new JsonObject
            {
                ["status"] = isEmptyBook ? "EmptyBook" : "Ok",
                ["snapshotReceived"] = true,
                ["bestBid"] = bestBid,
                ["bestAsk"] = bestAsk,
                ["mid"] = mid,
                ["entryCount"] = isEmptyBook ? 0 : result.EntryCount,
                ["entries"] = isEmptyBook ? new JsonArray() : BuildEntries(result.SlashSymbol, result.SecurityId, bestBid, bestAsk, result.EntryCount)
            },
            ["executionReports"] = new JsonArray(),
            ["orderStatuses"] = new JsonArray(),
            ["tradeCaptureReports"] = new JsonArray(),
            ["protocolRejects"] = new JsonArray(),
            ["warnings"] = new JsonArray(warnings.Select(x => JsonValue.Create(x)).ToArray())
        };

        var normalizedEvidenceJson = root.ToJsonString(JsonOptions);
        var fixturePreview = WriteAndValidate(normalizedEvidenceJson);
        var containsSensitive = ContainsSensitiveEvidence(normalizedEvidenceJson);
        var validationErrors = fixturePreview.ErrorCount + (containsSensitive ? 1 : 0);
        var warningCount = fixturePreview.WarningCount + warnings.Count;
        var batch = new LmaxReadOnlyRuntimeEvidenceBatchPreview(
            $"runtime-{result.Symbol.ToLowerInvariant()}-additional-preview-" + Guid.NewGuid().ToString("N"),
            SchemaVersion,
            EvidenceMode,
            createdAt,
            ExecutionReportCount: 0,
            OrderStatusCount: 0,
            TradeCaptureReportCount: 0,
            ProtocolRejectCount: 0,
            MarketDataSnapshotCount: 1,
            Sanitized: !containsSensitive,
            ContainsRawFix: false,
            Warnings: fixturePreview.Warnings.Concat(warnings).ToList());

        return new(
            validationErrors == 0,
            SchemaVersion,
            EvidenceMode,
            normalizedEvidenceJson,
            batch,
            validationErrors,
            warningCount,
            !containsSensitive,
            validationErrors == 0
                ? $"Sanitized {result.Symbol} additional-instrument artifact mapped to MarketDataOnly evidence preview. No shadow replay submit occurred."
                : "Mapped additional-instrument evidence preview failed local validation.",
            []);
    }

    private static JsonArray BuildEntries(string slashSymbol, string securityId, decimal? bestBid, decimal? bestAsk, int entryCount)
    {
        var entries = new JsonArray();
        if (bestBid is not null)
        {
            entries.Add(new JsonObject
            {
                ["symbol"] = slashSymbol,
                ["securityId"] = securityId,
                ["entryType"] = "0",
                ["price"] = bestBid.Value,
                ["size"] = 0
            });
        }

        if (bestAsk is not null)
        {
            entries.Add(new JsonObject
            {
                ["symbol"] = slashSymbol,
                ["securityId"] = securityId,
                ["entryType"] = "1",
                ["price"] = bestAsk.Value,
                ["size"] = 0
            });
        }

        while (entries.Count > entryCount && entries.Count > 0)
        {
            entries.RemoveAt(entries.Count - 1);
        }

        return entries;
    }

    private static LmaxReadOnlyGbpusdManualSnapshotResult ReadGbpusdResult(string artifactJson)
    {
        using var document = JsonDocument.Parse(artifactJson);
        var root = document.RootElement;
        return new(
            GetString(root, "runId", string.Empty),
            GetTimestamp(root, "startedAtUtc") ?? DateTimeOffset.MinValue,
            GetTimestamp(root, "completedAtUtc") ?? DateTimeOffset.MinValue,
            GetString(root, "status", string.Empty),
            GetString(root, "symbol", GetString(root, "instrument", string.Empty)),
            GetString(root, "slashSymbol", string.Empty),
            GetString(root, "securityId", string.Empty),
            GetString(root, "securityIdSource", string.Empty),
            GetString(root, "environmentName", string.Empty),
            GetString(root, "venueProfileName", string.Empty),
            GetString(root, "requestMode", string.Empty),
            GetString(root, "symbolEncodingMode", string.Empty),
            GetInt(root, "marketDepth"),
            GetBool(root, "externalConnectionAttempted"),
            GetBool(root, "credentialReadAttempted"),
            GetBool(root, "credentialValuesReturned"),
            GetBool(root, "logonAttempted"),
            GetBool(root, "logonSucceeded"),
            GetBool(root, "snapshotRequestAttempted"),
            GetBool(root, "snapshotReceived"),
            GetBool(root, "logoutAttempted"),
            GetBool(root, "logoutSucceeded"),
            GetBool(root, "orderSubmissionAttempted"),
            GetBool(root, "shadowReplaySubmitAttempted"),
            GetBool(root, "tradingMutationAttempted"),
            GetBool(root, "schedulerStarted"),
            GetDouble(root, "bestBid"),
            GetDouble(root, "bestAsk"),
            GetDouble(root, "mid"),
            GetInt(root, "entryCount"),
            GetNestedLong(root, "diagnostics", "request", "waitDurationMs"),
            GetBool(root, "noSensitiveContent"),
            GetString(root, "redactionStatus", string.Empty),
            GetString(root, "sourceFinalReadinessFile", string.Empty),
            GetNestedInt(root, "diagnostics", "messageCounters", "marketDataSnapshot"),
            GetNestedInt(root, "diagnostics", "messageCounters", "marketDataRequestReject"),
            GetNestedInt(root, "diagnostics", "messageCounters", "businessMessageReject"),
            GetNestedInt(root, "diagnostics", "messageCounters", "reject"),
            GetStringArray(root, "warnings"),
            GetStringArray(root, "errors"));
    }

    private static LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewResult Invalid(
        LmaxReadOnlyDemoSnapshotArtifactValidationResult validation,
        string message)
    {
        var batch = new LmaxReadOnlyRuntimeEvidenceBatchPreview(
            "runtime-demo-snapshot-preview-blocked-" + Guid.NewGuid().ToString("N"),
            SchemaVersion,
            EvidenceMode,
            DateTimeOffset.UtcNow,
            ExecutionReportCount: 0,
            OrderStatusCount: 0,
            TradeCaptureReportCount: 0,
            ProtocolRejectCount: 0,
            MarketDataSnapshotCount: 0,
            Sanitized: validation.NoSensitiveContent,
            ContainsRawFix: false,
            Warnings: validation.Issues.Where(x => x.Severity != LmaxReadOnlyDemoSnapshotArtifactValidationSeverity.Error).Select(x => x.Message).ToList());

        return new(
            false,
            SchemaVersion,
            EvidenceMode,
            string.Empty,
            batch,
            validation.Errors.Count,
            validation.Issues.Count(x => x.Severity == LmaxReadOnlyDemoSnapshotArtifactValidationSeverity.Warning),
            validation.NoSensitiveContent,
            message,
            validation.Issues);
    }

    private static LmaxReadOnlyRuntimeFixturePreview WriteAndValidate(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), "lmax-runtime-demo-snapshot-preview-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        try
        {
            return LmaxReadOnlyRuntimeAdapterFakeInMemory.PreviewFixtureEvidence(path);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static string GetString(JsonElement root, string propertyName, string fallback)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static decimal? GetDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static int GetInt(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;

    private static bool GetBool(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static double? GetDouble(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? number : null;
    }

    private static int GetNestedInt(JsonElement root, string first, string second, string third)
    {
        return root.TryGetProperty(first, out var firstValue)
            && firstValue.TryGetProperty(second, out var secondValue)
            && secondValue.TryGetProperty(third, out var thirdValue)
            && thirdValue.TryGetInt32(out var parsed)
            ? parsed
            : 0;
    }

    private static long? GetNestedLong(JsonElement root, string first, string second, string third)
    {
        return root.TryGetProperty(first, out var firstValue)
            && firstValue.TryGetProperty(second, out var secondValue)
            && secondValue.TryGetProperty(third, out var thirdValue)
            && thirdValue.TryGetInt64(out var parsed)
            ? parsed
            : null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static DateTimeOffset? GetTimestamp(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var value)
           && value.ValueKind == JsonValueKind.String
           && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;

    private static string FormatTimestamp(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

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
            "554=",
            "553=",
            "rawFix"
        };

        return sensitiveTerms.Any(term => json.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
