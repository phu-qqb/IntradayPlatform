using System.Text.Json.Nodes;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyMarketDataWorkflowValidatorTests
{
    [Fact]
    public void Valid_manifest_without_replay_passes_with_warning()
    {
        var result = LmaxReadOnlyMarketDataWorkflowValidator.ValidateJson(ManifestJson());

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowDecision.PassWithWarnings, result.Decision);
        Assert.Equal(3, result.ArtifactCount);
        Assert.Equal(3, result.EvidencePreviewCount);
        Assert.Equal(0, result.ReplayResultCount);
        Assert.Empty(result.Errors);
        Assert.Contains(result.Issues, x => x.Code == "ReplayNotRequested");
    }

    [Fact]
    public void Valid_manifest_with_three_replay_results_passes()
    {
        var result = LmaxReadOnlyMarketDataWorkflowValidator.ValidateJson(ManifestJson(includeReplay: true));

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowDecision.Pass, result.Decision);
        Assert.Equal(3, result.ArtifactCount);
        Assert.Equal(3, result.EvidencePreviewCount);
        Assert.Equal(3, result.ReplayResultCount);
        Assert.Empty(result.Errors);
        Assert.DoesNotContain(result.Issues, x => x.Code == "ReplayNotRequested");
    }

    [Theory]
    [InlineData("$.snapshotArtifacts", "orderSubmissionAttempted")]
    [InlineData("$.snapshotArtifacts", "shadowReplaySubmitAttempted")]
    [InlineData("$.snapshotArtifacts", "tradingMutationAttempted")]
    [InlineData("$.snapshotArtifacts", "schedulerStarted")]
    [InlineData("$.snapshotArtifacts", "credentialValuesReturned")]
    public void Unsafe_artifact_flags_fail(string arrayPath, string propertyName)
    {
        var json = Mutate(ManifestJson(), root => root[arrayPath.TrimStart('$', '.')]![0]![propertyName] = true);

        var result = LmaxReadOnlyMarketDataWorkflowValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Path.Contains(propertyName, StringComparison.Ordinal));
    }

    [Fact]
    public void Non_market_data_preview_fails()
    {
        var json = Mutate(ManifestJson(), root => root["evidencePreviews"]![0]!["evidenceMode"] = "SyntheticLifecycle");

        var result = LmaxReadOnlyMarketDataWorkflowValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "EvidencePreviewNotMarketDataOnly");
    }

    [Fact]
    public void Preview_with_execution_reports_fails()
    {
        var json = Mutate(ManifestJson(), root => root["evidencePreviews"]![0]!["executionReportCount"] = 1);

        var result = LmaxReadOnlyMarketDataWorkflowValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "EvidencePreviewContainsNonMarketDataEvents");
    }

    [Fact]
    public void Replay_observations_fail()
    {
        var json = Mutate(ManifestJson(includeReplay: true), root => root["manualReplayResults"]![0]!["observationCount"] = 1);

        var result = LmaxReadOnlyMarketDataWorkflowValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ReplayObservationsPresent");
    }

    [Fact]
    public void Replay_status_not_completed_fails()
    {
        var json = Mutate(ManifestJson(includeReplay: true), root => root["manualReplayResults"]![0]!["replayStatus"] = "Failed");

        var result = LmaxReadOnlyMarketDataWorkflowValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ReplayStatusNotCompleted");
    }

    [Fact]
    public void Replay_mutation_guard_changed_fails()
    {
        var json = Mutate(ManifestJson(includeReplay: true), root => root["manualReplayResults"]![0]!["mutationGuard"] = "Changed");

        var result = LmaxReadOnlyMarketDataWorkflowValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ReplayMutationGuardChanged");
    }

    [Fact]
    public void Replay_count_mismatch_fails()
    {
        var json = Mutate(ManifestJson(includeReplay: true), root => root["manualReplayResults"]!.AsArray().RemoveAt(0));

        var result = LmaxReadOnlyMarketDataWorkflowValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ReplayCountDoesNotMatchPreviewCount");
    }

    [Fact]
    public void Runtime_shadow_replay_submit_true_fails()
    {
        var json = Mutate(ManifestJson(), root => root["runtimeShadowReplaySubmit"] = true);

        var result = LmaxReadOnlyMarketDataWorkflowValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Path == "$.runtimeShadowReplaySubmit");
    }

    [Fact]
    public void External_connection_attempted_during_workflow_review_fails()
    {
        var json = Mutate(ManifestJson(), root => root["externalConnectionAttempted"] = true);

        var result = LmaxReadOnlyMarketDataWorkflowValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Path == "$.externalConnectionAttempted");
    }

    [Fact]
    public void Sentinel_secret_fails()
    {
        var json = Mutate(ManifestJson(), root => root["warnings"] = new JsonArray("phase5q-secret-sentinel"));

        var result = LmaxReadOnlyMarketDataWorkflowValidator.ValidateJson(json, ["phase5q-secret-sentinel"]);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ForbiddenSensitiveValue");
    }

    [Fact]
    public void Validator_contract_has_no_gateway_scheduler_or_order_surface()
    {
        var workflowTypes = typeof(LmaxReadOnlyMarketDataWorkflowValidator).Assembly
            .GetTypes()
            .Where(x => x.Namespace == "QQ.Production.Intraday.Infrastructure.Lmax"
                && x.Name.Contains("Workflow", StringComparison.Ordinal)
                && !x.Name.Contains("StatusSummary", StringComparison.Ordinal)
                && !x.Name.Contains("ControlledManual", StringComparison.Ordinal))
            .ToList();

        Assert.DoesNotContain(workflowTypes.Select(x => x.Name), x => x.Contains("Gateway", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(workflowTypes.SelectMany(x => x.GetMethods()).Select(x => x.Name), x => x.Contains("SubmitOrder", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(workflowTypes.SelectMany(x => x.GetMethods()).Select(x => x.Name), x => x.Contains("Schedule", StringComparison.OrdinalIgnoreCase));
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
            ["workflowId"] = "phase5q-test",
            ["createdAtUtc"] = "2026-05-08T15:00:00Z",
            ["operatorId"] = "local-operator",
            ["reason"] = "Phase 5Q unit test",
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
            ["errors"] = new JsonArray()
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
        }

        if (includeReplay)
        {
            for (var i = 1; i <= 3; i++)
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
