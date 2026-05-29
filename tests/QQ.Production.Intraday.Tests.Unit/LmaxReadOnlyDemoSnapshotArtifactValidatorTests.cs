using System.Text.Json.Nodes;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyDemoSnapshotArtifactValidatorTests
{
    [Fact]
    public void Successful_sanitized_artifact_validates()
    {
        var result = LmaxReadOnlyDemoSnapshotArtifactValidator.ValidateJson(SuccessJson());

        Assert.True(result.IsValid);
        Assert.True(result.IsSuccessfulSnapshot);
        Assert.Equal("Completed", result.Status);
        Assert.True(result.SnapshotReceived);
        Assert.True(result.NoSensitiveContent);
        Assert.Equal("Redacted", result.RedactionStatus);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("credentialValuesReturned", true)]
    [InlineData("orderSubmissionAttempted", true)]
    [InlineData("shadowReplaySubmitAttempted", true)]
    [InlineData("tradingMutationAttempted", true)]
    [InlineData("schedulerStarted", true)]
    [InlineData("noSensitiveContent", false)]
    public void Unsafe_boolean_flags_fail(string propertyName, bool value)
    {
        var json = Mutate(SuccessJson(), root => root[propertyName] = value);

        var result = LmaxReadOnlyDemoSnapshotArtifactValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Path == "$." + propertyName);
    }

    [Fact]
    public void Sentinel_secret_values_fail()
    {
        var json = Mutate(SuccessJson(), root => root["warnings"] = new JsonArray("phase5l-secret-sentinel"));

        var result = LmaxReadOnlyDemoSnapshotArtifactValidator.ValidateJson(json, ["phase5l-secret-sentinel"]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Code == "ForbiddenSensitiveValue");
    }

    [Fact]
    public void Raw_fix_password_tag_fails()
    {
        var json = Mutate(SuccessJson(), root => root["errors"] = new JsonArray("8=FIX.4.4\u000135=A\u0001554=raw-password\u0001"));

        var result = LmaxReadOnlyDemoSnapshotArtifactValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Code == "ForbiddenSensitiveContent");
    }

    [Theory]
    [InlineData("bestBid")]
    [InlineData("bestAsk")]
    [InlineData("mid")]
    public void Completed_snapshot_with_missing_price_fields_fails(string propertyName)
    {
        var json = Mutate(SuccessJson(), root => root.Remove(propertyName));

        var result = LmaxReadOnlyDemoSnapshotArtifactValidator.ValidateJson(json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Path == "$." + propertyName);
    }

    [Fact]
    public void Failed_safe_artifact_can_be_shape_checked_but_not_success_closed()
    {
        var json = Mutate(SuccessJson(), root =>
        {
            root["status"] = "FailedSafeSnapshotTimeout";
            root["snapshotReceived"] = false;
            root["marketDataSnapshotReceived"] = false;
        });

        var shapeResult = LmaxReadOnlyDemoSnapshotArtifactValidator.ValidateJson(json, requireSuccessfulSnapshot: false);
        var closureResult = LmaxReadOnlyDemoSnapshotArtifactValidator.ValidateJson(json, requireSuccessfulSnapshot: true);

        Assert.False(shapeResult.IsSuccessfulSnapshot);
        Assert.False(closureResult.IsValid);
        Assert.Contains(closureResult.Errors, x => x.Code == "StatusNotSuccessful");
    }

    [Fact]
    public void Validator_contract_has_no_gateway_or_order_surface()
    {
        var assemblyTypes = typeof(LmaxReadOnlyDemoSnapshotArtifactValidator).Assembly
            .GetTypes()
            .Where(x => x.Namespace == "QQ.Production.Intraday.Infrastructure.Lmax" && x.Name.Contains("DemoSnapshotArtifact", StringComparison.Ordinal))
            .ToList();

        Assert.DoesNotContain(assemblyTypes.Select(x => x.Name), x => x.Contains("NewOrder", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(assemblyTypes.Select(x => x.Name), x => x.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(assemblyTypes.Select(x => x.Name), x => x.Contains("Replace", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(assemblyTypes.SelectMany(x => x.GetMethods()).Select(x => x.Name), x => x.Contains("SubmitOrder", StringComparison.OrdinalIgnoreCase));
    }

    private static string Mutate(string json, Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    private static string SuccessJson()
        => """
           {
             "runId": "phase5l-success",
             "status": "Completed",
             "environmentName": "Demo",
             "venueProfileName": "DemoLondon",
             "credentialProfileName": "LmaxDemoReadOnlyProfile",
             "externalConnectionAttempted": true,
             "credentialReadAttempted": true,
             "credentialValuesReturned": false,
             "logonAttempted": true,
             "logonSucceeded": true,
             "snapshotRequestAttempted": true,
             "snapshotReceived": true,
             "logoutAttempted": true,
             "logoutSucceeded": true,
             "orderSubmissionAttempted": false,
             "shadowReplaySubmitAttempted": false,
             "tradingMutationAttempted": false,
             "schedulerStarted": false,
             "eventCount": 2,
             "messageCount": 2,
             "entryCount": 2,
             "marketDataSnapshotReceived": true,
             "instrument": "EURUSD",
             "securityId": "4001",
             "bestBid": 1.17662,
             "bestAsk": 1.17667,
             "mid": 1.176645,
             "noSensitiveContent": true,
             "redactionStatus": "Redacted",
             "warnings": [],
             "errors": [],
             "credentialAvailability": {
               "keyStatuses": [
                 { "keyLabel": "LMAX_DEMO_FIX_USERNAME", "isPresent": true, "redactionStatus": "Redacted" },
                 { "keyLabel": "LMAX_DEMO_FIX_PASSWORD", "isPresent": true, "redactionStatus": "Redacted" }
               ],
               "credentialValuesReturned": false,
               "sensitiveMaterialReturned": false
             }
           }
           """;
}
