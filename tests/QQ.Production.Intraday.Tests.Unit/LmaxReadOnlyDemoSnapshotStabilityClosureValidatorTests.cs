using System.Text.Json.Nodes;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyDemoSnapshotStabilityClosureValidatorTests
{
    [Fact]
    public void Successful_three_of_three_summary_validates_pass()
    {
        using var fixture = StabilityFixture.Create();

        var result = LmaxReadOnlyDemoSnapshotStabilityClosureValidator.ValidateFile(fixture.SummaryFile, fixture.Root);

        Assert.Equal(LmaxReadOnlyDemoSnapshotStabilityClosureDecision.Pass, result.Decision);
        Assert.Equal(3, result.AttemptCountRequested);
        Assert.Equal(3, result.AttemptCountCompleted);
        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(0, result.FailedSafeCount);
        Assert.Equal(3, result.SnapshotReceivedCount);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Failed_attempt_fails_closure()
    {
        using var fixture = StabilityFixture.Create(root =>
        {
            root["successCount"] = 2;
            root["failedSafeCount"] = 1;
            root["snapshotReceivedCount"] = 2;
            var attempt = root["attempts"]![2]!.AsObject();
            attempt["status"] = "FailedSafeSnapshotTimeout";
            attempt["snapshotReceived"] = false;
        });

        var result = LmaxReadOnlyDemoSnapshotStabilityClosureValidator.ValidateFile(fixture.SummaryFile, fixture.Root);

        Assert.Equal(LmaxReadOnlyDemoSnapshotStabilityClosureDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "SuccessCountMismatch");
        Assert.Contains(result.Errors, x => x.Code == "FailedSafeCountNonZero");
    }

    [Theory]
    [InlineData("orderSubmissionAttempted")]
    [InlineData("shadowReplaySubmitAttempted")]
    [InlineData("tradingMutationAttempted")]
    [InlineData("schedulerStarted")]
    [InlineData("credentialValuesReturned")]
    public void Unsafe_root_flags_fail_closure(string propertyName)
    {
        using var fixture = StabilityFixture.Create(root => root[propertyName] = true);

        var result = LmaxReadOnlyDemoSnapshotStabilityClosureValidator.ValidateFile(fixture.SummaryFile, fixture.Root);

        Assert.Equal(LmaxReadOnlyDemoSnapshotStabilityClosureDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Path == "$." + propertyName);
    }

    [Fact]
    public void Sentinel_secret_in_summary_fails_closure()
    {
        using var fixture = StabilityFixture.Create(root => root["warnings"] = new JsonArray("phase5p-secret-sentinel"));

        var result = LmaxReadOnlyDemoSnapshotStabilityClosureValidator.ValidateFile(
            fixture.SummaryFile,
            fixture.Root,
            ["phase5p-secret-sentinel"]);

        Assert.Equal(LmaxReadOnlyDemoSnapshotStabilityClosureDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ForbiddenSensitiveValue");
    }

    [Fact]
    public void Missing_referenced_artifact_warns()
    {
        using var fixture = StabilityFixture.Create();
        var root = JsonNode.Parse(File.ReadAllText(fixture.SummaryFile))!.AsObject();
        root["attempts"]![0]!["artifactPath"] = Path.Combine(fixture.Root, "artifacts", "lmax-readonly-runtime-demo-snapshot", "missing.json");
        File.WriteAllText(fixture.SummaryFile, root.ToJsonString());

        var result = LmaxReadOnlyDemoSnapshotStabilityClosureValidator.ValidateFile(fixture.SummaryFile, fixture.Root);

        Assert.Equal(LmaxReadOnlyDemoSnapshotStabilityClosureDecision.PassWithWarnings, result.Decision);
        Assert.Contains(result.Warnings, x => x.Code == "ReferencedArtifactMissing");
    }

    [Fact]
    public void Referenced_unsafe_artifact_fails()
    {
        using var fixture = StabilityFixture.Create();
        var unsafeArtifact = File.ReadAllText(fixture.ArtifactFiles[0]).Replace("\"orderSubmissionAttempted\": false", "\"orderSubmissionAttempted\": true", StringComparison.Ordinal);
        File.WriteAllText(fixture.ArtifactFiles[0], unsafeArtifact);

        var result = LmaxReadOnlyDemoSnapshotStabilityClosureValidator.ValidateFile(fixture.SummaryFile, fixture.Root);

        Assert.Equal(LmaxReadOnlyDemoSnapshotStabilityClosureDecision.Fail, result.Decision);
        Assert.Contains(result.Errors, x => x.Code == "ArtifactUnexpectedBooleanFlag");
    }

    [Fact]
    public void Validator_contract_has_no_gateway_scheduler_or_order_surface()
    {
        var closureTypes = typeof(LmaxReadOnlyDemoSnapshotStabilityClosureValidator).Assembly
            .GetTypes()
            .Where(x => x.Namespace == "QQ.Production.Intraday.Infrastructure.Lmax" && x.Name.Contains("StabilityClosure", StringComparison.Ordinal))
            .ToList();

        Assert.DoesNotContain(closureTypes.Select(x => x.Name), x => x.Contains("Gateway", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(closureTypes.SelectMany(x => x.GetMethods()).Select(x => x.Name), x => x.Contains("SubmitOrder", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(closureTypes.SelectMany(x => x.GetMethods()).Select(x => x.Name), x => x.Contains("Schedule", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class StabilityFixture : IDisposable
    {
        public string Root { get; }
        public string SummaryFile { get; }
        public List<string> ArtifactFiles { get; } = [];

        private StabilityFixture(string root, string summaryFile)
        {
            Root = root;
            SummaryFile = summaryFile;
        }

        public static StabilityFixture Create(Action<JsonObject>? mutateSummary = null)
        {
            var root = Path.Combine(Path.GetTempPath(), "phase5p-stability-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "artifacts", "lmax-readonly-runtime-demo-snapshot", "stability"));
            Directory.CreateDirectory(Path.Combine(root, "artifacts", "lmax-readonly-runtime-demo-snapshot", "evidence-preview"));
            File.WriteAllText(Path.Combine(root, ".gitignore"), "artifacts/");

            var fixture = new StabilityFixture(root, Path.Combine(root, "artifacts", "lmax-readonly-runtime-demo-snapshot", "stability", "summary.json"));
            var attempts = new JsonArray();
            for (var i = 1; i <= 3; i++)
            {
                var artifactFile = Path.Combine(root, "artifacts", "lmax-readonly-runtime-demo-snapshot", $"artifact-{i}.json");
                File.WriteAllText(artifactFile, ArtifactJson(i));
                fixture.ArtifactFiles.Add(artifactFile);

                var preview = LmaxReadOnlyDemoSnapshotArtifactEvidencePreviewMapper.MapJson(File.ReadAllText(artifactFile));
                Assert.True(preview.IsValid);
                var previewFile = Path.Combine(root, "artifacts", "lmax-readonly-runtime-demo-snapshot", "evidence-preview", $"preview-{i}.json");
                File.WriteAllText(previewFile, preview.NormalizedEvidenceJson);

                attempts.Add(new JsonObject
                {
                    ["attemptNumber"] = i,
                    ["status"] = "Completed",
                    ["artifactPath"] = artifactFile,
                    ["evidencePreviewPath"] = previewFile,
                    ["snapshotReceived"] = true,
                    ["bestBid"] = 1.17662m + i / 100000m,
                    ["bestAsk"] = 1.17667m + i / 100000m,
                    ["mid"] = 1.176645m + i / 100000m,
                    ["waitDurationMs"] = 120 + i,
                    ["externalConnectionAttempted"] = true,
                    ["logonSucceeded"] = true,
                    ["logoutSucceeded"] = true,
                    ["credentialValuesReturned"] = false,
                    ["orderSubmissionAttempted"] = false,
                    ["shadowReplaySubmitAttempted"] = false,
                    ["tradingMutationAttempted"] = false,
                    ["schedulerStarted"] = false,
                    ["noSensitiveContent"] = true
                });
            }

            var summary = new JsonObject
            {
                ["runGroupId"] = "phase5p-test",
                ["startedAtUtc"] = "2026-05-08T12:45:03Z",
                ["completedAtUtc"] = "2026-05-08T12:45:17Z",
                ["attemptCountRequested"] = 3,
                ["attemptCountCompleted"] = 3,
                ["successCount"] = 3,
                ["failedSafeCount"] = 0,
                ["snapshotReceivedCount"] = 3,
                ["noSensitiveContent"] = true,
                ["redactionStatus"] = "Redacted",
                ["credentialValuesReturned"] = false,
                ["orderSubmissionAttempted"] = false,
                ["shadowReplaySubmitAttempted"] = false,
                ["tradingMutationAttempted"] = false,
                ["schedulerStarted"] = false,
                ["attempts"] = attempts,
                ["warnings"] = new JsonArray(),
                ["errors"] = new JsonArray()
            };
            mutateSummary?.Invoke(summary);
            File.WriteAllText(fixture.SummaryFile, summary.ToJsonString());
            return fixture;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private static string ArtifactJson(int attemptNumber)
        => $$"""
           {
             "runId": "phase5p-test-run-{{attemptNumber}}",
             "startedAtUtc": "2026-05-08T12:45:00Z",
             "completedAtUtc": "2026-05-08T12:45:01Z",
             "status": "Completed",
             "environmentName": "Demo",
             "venueProfileName": "DemoLondon",
             "credentialProfileName": "LmaxDemoReadOnlyProfile",
             "reason": "Phase 5P unit test sanitized snapshot",
             "operatorId": "local-operator",
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
             "entryCount": 2,
             "marketDataSnapshotReceived": true,
             "instrument": "EURUSD",
             "securityId": "4001",
             "bestBid": 1.17662,
             "bestAsk": 1.17667,
             "mid": 1.176645,
             "snapshotReceivedAtUtc": "2026-05-08T12:45:01Z",
             "noSensitiveContent": true,
             "redactionStatus": "Redacted",
             "warnings": [],
             "errors": [],
             "retryEnabled": false,
             "retryAllowed": false
           }
           """;
}
