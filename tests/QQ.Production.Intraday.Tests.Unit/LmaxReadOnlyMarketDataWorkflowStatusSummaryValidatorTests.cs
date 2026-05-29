using System.Text.Json.Nodes;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyMarketDataWorkflowStatusSummaryValidatorTests
{
    [Fact]
    public void Valid_signoff_returns_frozen_manual_readonly_pass_summary()
    {
        var file = WriteTemp(SignoffJson());

        var result = LmaxReadOnlyMarketDataWorkflowStatusSummaryValidator.FromSignoffFile(file, createdAtUtc: DateTimeOffset.Parse("2026-05-08T17:10:00Z"));

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowOperationalStatus.FrozenManualReadOnly, result.OperationalStatus);
        Assert.Equal("PASS", result.Summary.SignoffDecision);
        Assert.Equal("PASS", result.Summary.AuditPackDecision);
        Assert.Equal(3, result.Summary.ArtifactCount);
        Assert.Equal(3, result.Summary.EvidencePreviewCount);
        Assert.Equal(3, result.Summary.ManualReplayCount);
        Assert.Equal(0, result.Summary.TotalObservationCount);
        Assert.Equal("FakeLmaxGateway", result.Summary.ApiWorkerGatewayMode);
        Assert.True(result.Summary.WorkflowFrozen);
        Assert.True(result.Summary.NoSensitiveContent);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Missing_signoff_returns_not_available_warning()
    {
        var result = LmaxReadOnlyMarketDataWorkflowStatusSummaryValidator.FromSignoffFile("missing-phase5w-signoff.json");

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowOperationalStatus.NotAvailable, result.OperationalStatus);
        Assert.Equal("NotAvailable", result.Summary.SignoffDecision);
        Assert.Contains(result.Issues, x => x.Code == "SignoffNotAvailable");
    }

    [Theory]
    [InlineData("finalDecision", "FAIL", "SignoffDecisionNotPass")]
    [InlineData("manualReplayCount", 2, "ReplayCountMismatch")]
    [InlineData("totalObservationCount", 1, "ObservationCountNonZero")]
    [InlineData("runtimeShadowReplaySubmit", true, "UnsafeBooleanFlag")]
    [InlineData("credentialValuesReturned", true, "UnsafeBooleanFlag")]
    [InlineData("orderSubmissionAttempted", true, "UnsafeBooleanFlag")]
    [InlineData("tradingMutationAttempted", true, "UnsafeBooleanFlag")]
    public void Unsafe_or_incomplete_signoff_fails(string property, object value, string expectedCode)
    {
        var json = Mutate(SignoffJson(), root =>
        {
            root[property] = value switch
            {
                bool boolean => boolean,
                int number => number,
                string text => text,
                _ => throw new InvalidOperationException("Unsupported value")
            };
        });
        var file = WriteTemp(json);

        var result = LmaxReadOnlyMarketDataWorkflowStatusSummaryValidator.FromSignoffFile(file);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowOperationalStatus.Fail, result.OperationalStatus);
        Assert.Contains(result.Errors, x => x.Code == expectedCode);
    }

    [Fact]
    public void Sentinel_secret_fails()
    {
        var file = WriteTemp(Mutate(SignoffJson(), root => root["reason"] = "phase5x-secret-sentinel"));

        var result = LmaxReadOnlyMarketDataWorkflowStatusSummaryValidator.FromSignoffFile(file, forbiddenSensitiveValues: ["phase5x-secret-sentinel"]);

        Assert.Equal(LmaxReadOnlyMarketDataWorkflowOperationalStatus.Fail, result.OperationalStatus);
        Assert.Contains(result.Errors, x => x.Code == "ForbiddenSensitiveValue");
    }

    [Fact]
    public void Summary_scope_does_not_include_live_controls()
    {
        var file = WriteTemp(SignoffJson());

        var result = LmaxReadOnlyMarketDataWorkflowStatusSummaryValidator.FromSignoffFile(file);

        Assert.Contains(result.Summary.WhatIsAllowed, x => x.Contains("Manual Demo MarketData workflow review", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Summary.WhatIsNotAllowed, x => x.Contains("Scheduler", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Summary.WhatIsNotAllowed, x => x.Contains("Runtime shadow replay submit", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Summary.WhatIsNotAllowed, x => x.Contains("Order submission", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Summary.WhatIsNotAllowed, x => x.Contains("Gateway registration", StringComparison.OrdinalIgnoreCase));
    }

    private static string WriteTemp(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"phase5x-status-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static string Mutate(string json, Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    private static string SignoffJson()
        => new JsonObject
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
            ["authorizedScope"] = new JsonArray("Recognition that the controlled manual Demo read-only MarketData workflow has been validated."),
            ["notAuthorized"] = new JsonArray("scheduler", "polling", "runtime replay", "orders", "gateway registration", "production", "multi-instrument expansion", "automatic execution", "trading mutation"),
            ["finalDecision"] = "PASS"
        }.ToJsonString();
}
