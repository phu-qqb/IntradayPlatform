using System.Text.Json.Nodes;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyDemoSnapshotStabilitySummaryValidatorTests
{
    [Fact]
    public void Successful_stability_summary_validates()
    {
        var result = LmaxReadOnlyDemoSnapshotStabilitySummaryValidator.ValidateJson(SummaryJson());

        Assert.True(result.IsValid);
        Assert.Equal(3, result.AttemptCountRequested);
        Assert.Equal(3, result.AttemptCountCompleted);
        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(0, result.FailedSafeCount);
        Assert.Equal(3, result.SnapshotReceivedCount);
        Assert.True(result.NoSensitiveContent);
    }

    [Fact]
    public void Failed_safe_attempts_can_be_aggregated()
    {
        var json = Mutate(SummaryJson(), root =>
        {
            root["successCount"] = 2;
            root["failedSafeCount"] = 1;
            root["snapshotReceivedCount"] = 2;
            var attempt = root["attempts"]![2]!.AsObject();
            attempt["status"] = "FailedSafeSnapshotTimeout";
            attempt["snapshotReceived"] = false;
        });

        var result = LmaxReadOnlyDemoSnapshotStabilitySummaryValidator.ValidateJson(json);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(1, result.FailedSafeCount);
    }

    [Theory]
    [InlineData("credentialValuesReturned", true)]
    [InlineData("orderSubmissionAttempted", true)]
    [InlineData("shadowReplaySubmitAttempted", true)]
    [InlineData("tradingMutationAttempted", true)]
    [InlineData("schedulerStarted", true)]
    [InlineData("noSensitiveContent", false)]
    public void Unsafe_summary_flags_fail(string propertyName, bool value)
    {
        var json = Mutate(SummaryJson(), root => root[propertyName] = value);

        var result = LmaxReadOnlyDemoSnapshotStabilitySummaryValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Path == "$." + propertyName);
    }

    [Fact]
    public void Unsafe_attempt_flags_fail()
    {
        var json = Mutate(SummaryJson(), root => root["attempts"]![0]!["orderSubmissionAttempted"] = true);

        var result = LmaxReadOnlyDemoSnapshotStabilitySummaryValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Path == "$.attempts[0].orderSubmissionAttempted");
    }

    [Fact]
    public void Attempt_count_above_cap_fails()
    {
        var json = Mutate(SummaryJson(), root => root["attemptCountRequested"] = 6);

        var result = LmaxReadOnlyDemoSnapshotStabilitySummaryValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Code == "AttemptCountOutOfRange");
    }

    [Fact]
    public void Totals_must_match_completed_attempts()
    {
        var json = Mutate(SummaryJson(), root => root["failedSafeCount"] = 2);

        var result = LmaxReadOnlyDemoSnapshotStabilitySummaryValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Code == "AttemptTotalsMismatch");
    }

    [Fact]
    public void Sentinel_secret_values_fail()
    {
        var json = Mutate(SummaryJson(), root => root["warnings"] = new JsonArray("phase5o-secret-sentinel"));

        var result = LmaxReadOnlyDemoSnapshotStabilitySummaryValidator.ValidateJson(json, ["phase5o-secret-sentinel"]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Code == "ForbiddenSensitiveValue");
    }

    [Fact]
    public void Raw_fix_password_tag_fails()
    {
        var json = Mutate(SummaryJson(), root => root["errors"] = new JsonArray("8=FIX.4.4\u000135=A\u0001554=raw-password\u0001"));

        var result = LmaxReadOnlyDemoSnapshotStabilitySummaryValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Code == "ForbiddenSensitiveContent");
    }

    [Fact]
    public void Validator_contract_has_no_gateway_or_order_surface()
    {
        var assemblyTypes = typeof(LmaxReadOnlyDemoSnapshotStabilitySummaryValidator).Assembly
            .GetTypes()
            .Where(x => x.Namespace == "QQ.Production.Intraday.Infrastructure.Lmax" && x.Name.Contains("Stability", StringComparison.Ordinal))
            .ToList();

        Assert.DoesNotContain(assemblyTypes.Select(x => x.Name), x => x.Contains("NewOrder", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(assemblyTypes.SelectMany(x => x.GetMethods()).Select(x => x.Name), x => x.Contains("SubmitOrder", StringComparison.OrdinalIgnoreCase));
    }

    private static string Mutate(string json, Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    private static string SummaryJson()
        => """
           {
             "runGroupId": "phase5o-stability",
             "startedAtUtc": "2026-05-08T12:00:00Z",
             "completedAtUtc": "2026-05-08T12:00:10Z",
             "attemptCountRequested": 3,
             "attemptCountCompleted": 3,
             "successCount": 3,
             "failedSafeCount": 0,
             "snapshotReceivedCount": 3,
             "noSensitiveContent": true,
             "redactionStatus": "Redacted",
             "credentialValuesReturned": false,
             "orderSubmissionAttempted": false,
             "shadowReplaySubmitAttempted": false,
             "tradingMutationAttempted": false,
             "schedulerStarted": false,
             "warnings": [],
             "errors": [],
             "attempts": [
               {
                 "attemptNumber": 1,
                 "status": "Completed",
                 "artifactPath": "artifacts/lmax-readonly-runtime-demo-snapshot/one.json",
                 "evidencePreviewPath": "artifacts/lmax-readonly-runtime-demo-snapshot/evidence-preview/one.json",
                 "snapshotReceived": true,
                 "bestBid": 1.17662,
                 "bestAsk": 1.17667,
                 "mid": 1.176645,
                 "waitDurationMs": 133,
                 "externalConnectionAttempted": true,
                 "logonSucceeded": true,
                 "logoutSucceeded": true,
                 "credentialValuesReturned": false,
                 "orderSubmissionAttempted": false,
                 "shadowReplaySubmitAttempted": false,
                 "tradingMutationAttempted": false,
                 "schedulerStarted": false,
                 "noSensitiveContent": true
               },
               {
                 "attemptNumber": 2,
                 "status": "Completed",
                 "artifactPath": "artifacts/lmax-readonly-runtime-demo-snapshot/two.json",
                 "evidencePreviewPath": "artifacts/lmax-readonly-runtime-demo-snapshot/evidence-preview/two.json",
                 "snapshotReceived": true,
                 "bestBid": 1.17663,
                 "bestAsk": 1.17668,
                 "mid": 1.176655,
                 "waitDurationMs": 144,
                 "externalConnectionAttempted": true,
                 "logonSucceeded": true,
                 "logoutSucceeded": true,
                 "credentialValuesReturned": false,
                 "orderSubmissionAttempted": false,
                 "shadowReplaySubmitAttempted": false,
                 "tradingMutationAttempted": false,
                 "schedulerStarted": false,
                 "noSensitiveContent": true
               },
               {
                 "attemptNumber": 3,
                 "status": "Completed",
                 "artifactPath": "artifacts/lmax-readonly-runtime-demo-snapshot/three.json",
                 "evidencePreviewPath": "artifacts/lmax-readonly-runtime-demo-snapshot/evidence-preview/three.json",
                 "snapshotReceived": true,
                 "bestBid": 1.17664,
                 "bestAsk": 1.17669,
                 "mid": 1.176665,
                 "waitDurationMs": 155,
                 "externalConnectionAttempted": true,
                 "logonSucceeded": true,
                 "logoutSucceeded": true,
                 "credentialValuesReturned": false,
                 "orderSubmissionAttempted": false,
                 "shadowReplaySubmitAttempted": false,
                 "tradingMutationAttempted": false,
                 "schedulerStarted": false,
                 "noSensitiveContent": true
               }
             ],
             "rollbackInstructions": [
               "Stop the local process.",
               "Verify API/Worker remain FakeLmaxGateway.",
               "Run the Phase 5O stability gate."
             ]
           }
           """;
}
