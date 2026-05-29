using System.Text.Json.Nodes;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyManualWorkflowReleaseValidatorTests
{
    [Fact]
    public void Release_manifest_with_three_replays_passes()
    {
        var result = LmaxReadOnlyManualWorkflowReleaseValidator.ValidateJson(ManifestJson(includeReplay: true));

        Assert.Equal(LmaxReadOnlyManualWorkflowReleaseDecision.Pass, result.Decision);
        Assert.Equal(3, result.ArtifactCount);
        Assert.Equal(3, result.EvidencePreviewCount);
        Assert.Equal(3, result.ManualReplayCount);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Release_manifest_without_replay_passes_with_warning()
    {
        var result = LmaxReadOnlyManualWorkflowReleaseValidator.ValidateJson(ManifestJson());

        Assert.Equal(LmaxReadOnlyManualWorkflowReleaseDecision.PassWithWarnings, result.Decision);
        Assert.Contains(result.Issues, x => x.Code == "ReplayNotRequested");
    }

    [Theory]
    [InlineData("$.snapshotArtifacts", "orderSubmissionAttempted")]
    [InlineData("$.snapshotArtifacts", "shadowReplaySubmitAttempted")]
    [InlineData("$.snapshotArtifacts", "tradingMutationAttempted")]
    [InlineData("$.snapshotArtifacts", "schedulerStarted")]
    [InlineData("$.snapshotArtifacts", "credentialValuesReturned")]
    public void Unsafe_artifact_flags_fail(string arrayPath, string propertyName)
    {
        var json = Mutate(ManifestJson(includeReplay: true), root => root[arrayPath.TrimStart('$', '.')]![0]![propertyName] = true);

        var result = LmaxReadOnlyManualWorkflowReleaseValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyManualWorkflowReleaseDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Path.Contains(propertyName, StringComparison.Ordinal));
    }

    [Fact]
    public void Non_market_data_preview_fails()
    {
        var json = Mutate(ManifestJson(includeReplay: true), root => root["evidencePreviews"]![0]!["evidenceMode"] = "Lifecycle");

        var result = LmaxReadOnlyManualWorkflowReleaseValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyManualWorkflowReleaseDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "EvidencePreviewNotMarketDataOnly");
    }

    [Fact]
    public void Preview_with_non_empty_replay_arrays_fails()
    {
        var json = Mutate(ManifestJson(includeReplay: true), root => root["evidencePreviews"]![0]!["executionReportCount"] = 1);

        var result = LmaxReadOnlyManualWorkflowReleaseValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyManualWorkflowReleaseDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "EvidencePreviewContainsNonMarketDataEvents");
    }

    [Fact]
    public void Replay_observations_fail()
    {
        var json = Mutate(ManifestJson(includeReplay: true), root => root["manualReplayResults"]![0]!["observationCount"] = 1);

        var result = LmaxReadOnlyManualWorkflowReleaseValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyManualWorkflowReleaseDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ReplayObservationsPresent");
    }

    [Fact]
    public void Replay_status_not_completed_fails()
    {
        var json = Mutate(ManifestJson(includeReplay: true), root => root["manualReplayResults"]![0]!["replayStatus"] = "Failed");

        var result = LmaxReadOnlyManualWorkflowReleaseValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyManualWorkflowReleaseDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ReplayStatusNotCompleted");
    }

    [Fact]
    public void Replay_mutation_guard_changed_fails()
    {
        var json = Mutate(ManifestJson(includeReplay: true), root => root["manualReplayResults"]![0]!["mutationGuard"] = "Changed");

        var result = LmaxReadOnlyManualWorkflowReleaseValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyManualWorkflowReleaseDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ReplayMutationGuardChanged");
    }

    [Fact]
    public void Replay_count_mismatch_fails()
    {
        var json = Mutate(ManifestJson(includeReplay: true), root => root["manualReplayResults"]!.AsArray().RemoveAt(0));

        var result = LmaxReadOnlyManualWorkflowReleaseValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyManualWorkflowReleaseDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ReplayCountDoesNotMatchPreviewCount");
    }

    [Fact]
    public void Runtime_shadow_submit_or_external_connection_fails()
    {
        var json = Mutate(ManifestJson(includeReplay: true), root =>
        {
            root["runtimeShadowReplaySubmit"] = true;
            root["externalConnectionAttempted"] = true;
        });

        var result = LmaxReadOnlyManualWorkflowReleaseValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyManualWorkflowReleaseDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Path == "$.runtimeShadowReplaySubmit");
        Assert.Contains(result.Errors, x => x.Path == "$.externalConnectionAttempted");
    }

    [Fact]
    public void Sentinel_secret_fails()
    {
        var json = Mutate(ManifestJson(), root => root["warnings"] = new JsonArray("phase5s-secret-sentinel"));

        var result = LmaxReadOnlyManualWorkflowReleaseValidator.ValidateJson(json, ["phase5s-secret-sentinel"]);

        Assert.Equal(LmaxReadOnlyManualWorkflowReleaseDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ForbiddenSensitiveValue");
    }

    [Fact]
    public void Validator_contract_has_no_gateway_scheduler_or_order_surface()
    {
        var releaseTypes = typeof(LmaxReadOnlyManualWorkflowReleaseValidator).Assembly
            .GetTypes()
            .Where(x => x.Namespace == "QQ.Production.Intraday.Infrastructure.Lmax" && x.Name.Contains("WorkflowRelease", StringComparison.Ordinal))
            .ToList();

        Assert.DoesNotContain(releaseTypes.Select(x => x.Name), x => x.Contains("Gateway", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(releaseTypes.SelectMany(x => x.GetMethods()).Select(x => x.Name), x => x.Contains("SubmitOrder", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(releaseTypes.SelectMany(x => x.GetMethods()).Select(x => x.Name), x => x.Contains("Schedule", StringComparison.OrdinalIgnoreCase));
    }

    private static string Mutate(string json, Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    private static string ManifestJson(bool includeReplay = false)
    {
        var root = new JsonObject
        {
            ["workflowId"] = "phase5s-test",
            ["phase"] = "5S",
            ["createdAtUtc"] = "2026-05-08T16:00:00Z",
            ["operatorId"] = "local-operator",
            ["reason"] = "Phase 5S unit test",
            ["replayRequested"] = includeReplay,
            ["replayPerformed"] = includeReplay,
            ["artifactCount"] = 3,
            ["evidencePreviewCount"] = 3,
            ["manualReplayCount"] = includeReplay ? 3 : 0,
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
            ["warnings"] = new JsonArray(),
            ["errors"] = new JsonArray(),
            ["finalDecision"] = includeReplay ? "PASS" : "PASS_WITH_WARNINGS"
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
            if (includeReplay)
            {
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
        }

        return root.ToJsonString();
    }
}
