using System.Text.Json.Nodes;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyMarketDataOperationalSignoffValidatorTests
{
    [Fact]
    public void Valid_audit_pack_signoff_passes()
    {
        var result = LmaxReadOnlyMarketDataOperationalSignoffValidator.ValidateJson(SignoffJson());

        Assert.Equal(LmaxReadOnlyMarketDataOperationalSignoffDecision.Pass, result.Decision);
        Assert.Equal(3, result.ArtifactCount);
        Assert.Equal(3, result.EvidencePreviewCount);
        Assert.Equal(3, result.ManualReplayCount);
        Assert.Equal(0, result.TotalObservationCount);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Audit_pack_not_pass_fails()
    {
        var json = Mutate(SignoffJson(), root => root["auditPackFinalDecision"] = "FAIL");

        var result = LmaxReadOnlyMarketDataOperationalSignoffValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataOperationalSignoffDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "AuditPackDecisionNotPass");
    }

    [Fact]
    public void Replay_count_mismatch_fails()
    {
        var json = Mutate(SignoffJson(), root => root["manualReplayCount"] = 2);

        var result = LmaxReadOnlyMarketDataOperationalSignoffValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataOperationalSignoffDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ReplayCountMismatch");
    }

    [Fact]
    public void Nonzero_observations_fail()
    {
        var json = Mutate(SignoffJson(), root => root["totalObservationCount"] = 1);

        var result = LmaxReadOnlyMarketDataOperationalSignoffValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataOperationalSignoffDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ObservationCountNonZero");
    }

    [Theory]
    [InlineData("runtimeShadowReplaySubmit")]
    [InlineData("externalConnectionAttempted")]
    [InlineData("credentialValuesReturned")]
    [InlineData("orderSubmissionAttempted")]
    [InlineData("schedulerStarted")]
    [InlineData("tradingMutationAttempted")]
    public void Unsafe_flags_fail(string propertyName)
    {
        var json = Mutate(SignoffJson(), root => root[propertyName] = true);

        var result = LmaxReadOnlyMarketDataOperationalSignoffValidator.ValidateJson(json);

        Assert.Equal(LmaxReadOnlyMarketDataOperationalSignoffDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Path == "$." + propertyName);
    }

    [Fact]
    public void Sentinel_secret_fails()
    {
        var json = Mutate(SignoffJson(), root => root["reason"] = "phase5w-secret-sentinel");

        var result = LmaxReadOnlyMarketDataOperationalSignoffValidator.ValidateJson(json, ["phase5w-secret-sentinel"]);

        Assert.Equal(LmaxReadOnlyMarketDataOperationalSignoffDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ForbiddenSensitiveValue");
    }

    [Fact]
    public void Authorized_scope_does_not_authorize_runtime_power()
    {
        var root = JsonNode.Parse(SignoffJson())!.AsObject();
        var authorizedScope = string.Join(
            " ",
            root["authorizedScope"]!.AsArray().Select(x => x!.GetValue<string>()));
        var notAuthorized = string.Join(
            " ",
            root["notAuthorized"]!.AsArray().Select(x => x!.GetValue<string>()));

        Assert.Contains("controlled manual Demo read-only MarketData workflow", authorizedScope, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("scheduler", authorizedScope, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runtime replay", authorizedScope, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("order", authorizedScope, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gateway registration", authorizedScope, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scheduler", notAuthorized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("runtime replay", notAuthorized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("orders", notAuthorized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gateway registration", notAuthorized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validator_contract_has_no_gateway_scheduler_or_order_surface()
    {
        var signoffTypes = typeof(LmaxReadOnlyMarketDataOperationalSignoffValidator).Assembly
            .GetTypes()
            .Where(x => x.Namespace == "QQ.Production.Intraday.Infrastructure.Lmax" && x.Name.Contains("OperationalSignoff", StringComparison.Ordinal))
            .ToList();

        Assert.DoesNotContain(signoffTypes.Select(x => x.Name), x => x.Contains("Gateway", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(signoffTypes.SelectMany(x => x.GetMethods()).Select(x => x.Name), x => x.Contains("SubmitOrder", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(signoffTypes.SelectMany(x => x.GetMethods()).Select(x => x.Name), x => x.Contains("Schedule", StringComparison.OrdinalIgnoreCase));
    }

    private static string Mutate(string json, Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    private static string SignoffJson()
    {
        return new JsonObject
        {
            ["signoffId"] = "phase5w-test",
            ["phase"] = "5W",
            ["createdAtUtc"] = "2026-05-08T17:00:00Z",
            ["signoffBy"] = "local-operator",
            ["role"] = "Operator",
            ["reason"] = "Phase 5W operational signoff",
            ["auditPackFile"] = "audit-pack.json",
            ["auditPackMarkdownFile"] = "audit-pack.md",
            ["auditPackFinalDecision"] = "PASS",
            ["artifactCount"] = 3,
            ["evidencePreviewCount"] = 3,
            ["manualReplayCount"] = 3,
            ["totalObservationCount"] = 0,
            ["runtimeShadowReplaySubmit"] = false,
            ["externalConnectionAttempted"] = false,
            ["orderSubmissionAttempted"] = false,
            ["shadowReplaySubmitAttempted"] = false,
            ["tradingMutationAttempted"] = false,
            ["schedulerStarted"] = false,
            ["credentialValuesReturned"] = false,
            ["noSensitiveContent"] = true,
            ["redactionStatus"] = "Redacted",
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
            ["authorizedScope"] = new JsonArray("Recognition that the controlled manual Demo read-only MarketData workflow has been validated."),
            ["notAuthorized"] = new JsonArray("scheduler", "polling", "runtime replay", "orders", "gateway registration", "production", "multi-instrument expansion", "automatic execution", "trading mutation"),
            ["finalDecision"] = "PASS"
        }.ToJsonString();
    }
}
