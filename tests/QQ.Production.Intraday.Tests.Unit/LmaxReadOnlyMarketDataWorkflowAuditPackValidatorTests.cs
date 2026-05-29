using System.Text.Json.Nodes;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyMarketDataWorkflowAuditPackValidatorTests
{
    [Fact]
    public void Valid_audit_pack_with_three_artifacts_previews_and_replays_passes()
    {
        var result = LmaxReadOnlyMarketDataWorkflowAuditPackValidator.ValidateJson(AuditPackJson());

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowAuditPackDecision.Pass, result.Decision);
        Assert.Equal(3, result.ArtifactCount);
        Assert.Equal(3, result.EvidencePreviewCount);
        Assert.Equal(3, result.ManualReplayCount);
        Assert.Equal(0, result.TotalObservationCount);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Manual_replay_count_mismatch_fails()
    {
        var json = Mutate(AuditPackJson(), root =>
        {
            root["manualReplayCount"] = 2;
            root["manualReplayResults"]!.AsArray().RemoveAt(0);
        });

        var result = LmaxReadOnlyMarketDataWorkflowAuditPackValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowAuditPackDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ReplayCountMismatch");
    }

    [Fact]
    public void Nonzero_observations_fail()
    {
        var json = Mutate(AuditPackJson(), root => root["manualReplayResults"]![0]!["observationCount"] = 1);

        var result = LmaxReadOnlyMarketDataWorkflowAuditPackValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowAuditPackDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ReplayObservationsPresent");
    }

    [Fact]
    public void Mutation_guard_changed_fails()
    {
        var json = Mutate(AuditPackJson(), root => root["manualReplayResults"]![0]!["mutationGuard"] = "Changed");

        var result = LmaxReadOnlyMarketDataWorkflowAuditPackValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowAuditPackDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ReplayMutationGuardChanged");
    }

    [Theory]
    [InlineData("runtimeShadowReplaySubmit")]
    [InlineData("externalConnectionAttempted")]
    [InlineData("orderSubmissionAttempted")]
    [InlineData("credentialValuesReturned")]
    public void Unsafe_root_flags_fail(string propertyName)
    {
        var json = Mutate(AuditPackJson(), root => root[propertyName] = true);

        var result = LmaxReadOnlyMarketDataWorkflowAuditPackValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowAuditPackDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Path == "$." + propertyName);
    }

    [Fact]
    public void Unsafe_artifact_flags_fail()
    {
        var json = Mutate(AuditPackJson(), root => root["snapshotArtifacts"]![0]!["orderSubmissionAttempted"] = true);

        var result = LmaxReadOnlyMarketDataWorkflowAuditPackValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowAuditPackDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Path.Contains("orderSubmissionAttempted", StringComparison.Ordinal));
    }

    [Fact]
    public void Evidence_preview_not_market_data_only_fails()
    {
        var json = Mutate(AuditPackJson(), root => root["evidencePreviews"]![0]!["evidenceMode"] = "SyntheticLifecycle");

        var result = LmaxReadOnlyMarketDataWorkflowAuditPackValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowAuditPackDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "EvidencePreviewNotMarketDataOnly");
    }

    [Fact]
    public void Evidence_preview_with_non_empty_readonly_arrays_fails()
    {
        var json = Mutate(AuditPackJson(), root => root["evidencePreviews"]![0]!["executionReportCount"] = 1);

        var result = LmaxReadOnlyMarketDataWorkflowAuditPackValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowAuditPackDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "EvidencePreviewContainsNonMarketDataEvents");
    }

    [Fact]
    public void Sentinel_secret_fails()
    {
        var json = Mutate(AuditPackJson(), root => root["issues"] = new JsonArray("phase5v-secret-sentinel"));

        var result = LmaxReadOnlyMarketDataWorkflowAuditPackValidator.ValidateJson(json, ["phase5v-secret-sentinel"]);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowAuditPackDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ForbiddenSensitiveValue");
    }

    [Fact]
    public void Validator_contract_has_no_gateway_scheduler_or_order_surface()
    {
        var auditTypes = typeof(LmaxReadOnlyMarketDataWorkflowAuditPackValidator).Assembly
            .GetTypes()
            .Where(x => x.Namespace == "QQ.Production.Intraday.Infrastructure.Lmax" && x.Name.Contains("AuditPack", StringComparison.Ordinal))
            .ToList();

        Assert.DoesNotContain(auditTypes.Select(x => x.Name), x => x.Contains("Gateway", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(auditTypes.SelectMany(x => x.GetMethods()).Select(x => x.Name), x => x.Contains("SubmitOrder", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(auditTypes.SelectMany(x => x.GetMethods()).Select(x => x.Name), x => x.Contains("Schedule", StringComparison.OrdinalIgnoreCase));
    }

    private static string Mutate(string json, Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    private static string AuditPackJson()
    {
        var root = new JsonObject
        {
            ["auditPackId"] = "phase5v-test",
            ["phase"] = "5V",
            ["createdAtUtc"] = "2026-05-08T16:30:00Z",
            ["stabilitySummaryFile"] = "stability.json",
            ["workflowManifestFile"] = "workflow.json",
            ["stabilityDecision"] = "PASS",
            ["workflowFinalDecision"] = "PASS",
            ["artifactCount"] = 3,
            ["evidencePreviewCount"] = 3,
            ["manualReplayCount"] = 3,
            ["runtimeShadowReplaySubmit"] = false,
            ["externalConnectionAttempted"] = false,
            ["orderSubmissionAttempted"] = false,
            ["shadowReplaySubmitAttempted"] = false,
            ["tradingMutationAttempted"] = false,
            ["schedulerStarted"] = false,
            ["credentialValuesReturned"] = false,
            ["noSensitiveContent"] = true,
            ["redactionStatus"] = "Redacted",
            ["snapshotArtifacts"] = new JsonArray(),
            ["evidencePreviews"] = new JsonArray(),
            ["manualReplayResults"] = new JsonArray(),
            ["safetyConfirmations"] = new JsonObject
            {
                ["apiWorkerFakeLmaxGatewayOnly"] = true,
                ["noSchedulerOrPolling"] = true,
                ["noRuntimeShadowReplaySubmit"] = true,
                ["noOrderSurface"] = true,
                ["noGatewayRegistration"] = true,
                ["noTradingMutation"] = true,
                ["noCredentialExposure"] = true
            },
            ["issues"] = new JsonArray(),
            ["finalDecision"] = "PASS"
        };

        for (var i = 1; i <= 3; i++)
        {
            root["snapshotArtifacts"]!.AsArray().Add(new JsonObject
            {
                ["path"] = $"artifact-{i}.json",
                ["validationStatus"] = "PASS",
                ["status"] = "Completed",
                ["snapshotReceived"] = true,
                ["orderSubmissionAttempted"] = false,
                ["shadowReplaySubmitAttempted"] = false,
                ["tradingMutationAttempted"] = false,
                ["schedulerStarted"] = false,
                ["credentialValuesReturned"] = false,
                ["noSensitiveContent"] = true
            });
            root["evidencePreviews"]!.AsArray().Add(new JsonObject
            {
                ["path"] = $"preview-{i}.json",
                ["validationStatus"] = "PASS",
                ["evidenceMode"] = "MarketDataOnly",
                ["executionReportCount"] = 0,
                ["orderStatusCount"] = 0,
                ["tradeCaptureReportCount"] = 0,
                ["protocolRejectCount"] = 0,
                ["marketDataSnapshotCount"] = 1,
                ["noSensitiveContent"] = true
            });
            root["manualReplayResults"]!.AsArray().Add(new JsonObject
            {
                ["evidencePreviewFile"] = $"preview-{i}.json",
                ["replayRunId"] = $"replay-{i}",
                ["replayStatus"] = "Completed",
                ["observationCount"] = 0,
                ["blockingObservationCount"] = 0,
                ["warningObservationCount"] = 0,
                ["mutationGuard"] = "Unchanged",
                ["noSensitiveContent"] = true
            });
        }

        return root.ToJsonString();
    }
}
