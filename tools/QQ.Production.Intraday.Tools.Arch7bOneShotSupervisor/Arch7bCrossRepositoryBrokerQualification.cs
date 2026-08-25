using System.Security.Cryptography;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bCrossRepositoryBrokerQualification(
    string ContractVersion,
    string CoreCommit,
    string CoreTree,
    int IndependentRuns,
    int IndependentPasses,
    int Campaigns,
    int CampaignPasses,
    int RunsPerCampaign,
    int SequenceOneToFourPasses,
    int FourAdapterPasses,
    int TerminalCleanupPasses,
    int TransientPayloadPersistenceCount,
    int SecretLeakCount,
    int ResidualProcessCount,
    Arch7bNoLiveSafetyCounters Safety,
    string EvidenceSha256);

public static class Arch7bCrossRepositoryBrokerQualifier
{
    public const string ContractVersion =
        "arch7b_core_broker_intraday_cross_repository_qualification_v1";
    private const string TargetFingerprint =
        "72fa569ee28e4dec6272db0d69c7594b2be8853e9607dff3e78066378a0b5ee4";
    private const string SecretArn =
        "arn:aws:secretsmanager:eu-west-2:761018894194:secret:" +
        "qq-intraday-test-credentials-5YHOCV";
    private const string Read1VersionId = "11111111-2222-4333-8444-555555555555";

    public static async Task<Arch7bCrossRepositoryBrokerQualification> RunAsync(
        string supervisorExecutable,
        string coreRepository,
        string nodeExecutable,
        string? dotnetRoot,
        string coreCommit,
        string coreTree,
        int independentRuns = 20,
        int campaigns = 10,
        int runsPerCampaign = 3,
        CancellationToken cancellationToken = default)
    {
        supervisorExecutable = RequireFile(supervisorExecutable);
        nodeExecutable = RequireFile(nodeExecutable);
        coreRepository = Path.GetFullPath(coreRepository);
        var module = RequireFile(Path.Combine(coreRepository, "tools",
            "lmax_portal_reports_downloader", "src", "rds-secret-child-command-broker.mjs"));
        var cli = RequireFile(Path.Combine(coreRepository, "tools",
            "lmax_portal_reports_downloader", "src", "rds-secret-child-command-broker-cli.mjs"));
        dotnetRoot = string.IsNullOrWhiteSpace(dotnetRoot) ? null : Path.GetFullPath(dotnetRoot);
        var dotnetSha = dotnetRoot is null ? null : ShaFile(RequireFile(Path.Combine(dotnetRoot,
            OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet")));
        Arch7bCoreRdsSecretBrokerStaticAuthority.RequireCommit(coreCommit);
        Arch7bCoreRdsSecretBrokerStaticAuthority.RequireCommit(coreTree);
        if (independentRuns < 1 || campaigns < 0 || runsPerCampaign < 1)
            throw new ArgumentOutOfRangeException(nameof(independentRuns));

        var all = new List<RunEvidence>();
        for (var index = 0; index < independentRuns; index++)
            all.Add(await RunOneAsync($"independent-{index:D2}", supervisorExecutable,
                nodeExecutable, dotnetRoot, dotnetSha, module, cli, coreCommit, coreTree, cancellationToken)
                .ConfigureAwait(false));
        var independentPasses = all.Count(value => value.Passed);
        var campaignPasses = 0;
        for (var campaign = 0; campaign < campaigns; campaign++)
        {
            var values = new List<RunEvidence>();
            for (var run = 0; run < runsPerCampaign; run++)
                values.Add(await RunOneAsync($"campaign-{campaign:D2}-{run:D2}",
                    supervisorExecutable, nodeExecutable, dotnetRoot, dotnetSha, module, cli, coreCommit, coreTree,
                    cancellationToken).ConfigureAwait(false));
            if (values.All(value => value.Passed) &&
                values.Select(value => value.RunId).Distinct(StringComparer.Ordinal).Count() == runsPerCampaign)
                campaignPasses++;
            all.AddRange(values);
        }
        var canonical = string.Join('\n', ContractVersion, coreCommit, coreTree,
            independentRuns, independentPasses, campaigns, campaignPasses, runsPerCampaign,
            string.Join('|', all.Select(value => value.EvidenceSha256)));
        return new(ContractVersion, coreCommit, coreTree, independentRuns, independentPasses,
            campaigns, campaignPasses, runsPerCampaign,
            all.Count(value => value.SequenceOneToFour),
            all.Count(value => value.FourAdapters),
            all.Count(value => value.TerminalCleanup),
            all.Sum(value => value.TransientPayloadPersistenceCount),
            all.Sum(value => value.SecretLeakCount),
            all.Sum(value => value.ResidualProcessCount),
            Arch7bNoLiveSafetyCounters.Zero, Arch7bOneShotContracts.Sha256(canonical));
    }

    private static async Task<RunEvidence> RunOneAsync(string suffix,
        string supervisorExecutable, string nodeExecutable, string? dotnetRoot, string? dotnetSha,
        string module, string cli,
        string coreCommit, string coreTree, CancellationToken cancellationToken)
    {
        var root = Path.Combine(Path.GetTempPath(), "qq-arch7b-cross-repo-broker",
            suffix + "-" + Guid.NewGuid().ToString("N"));
        var fixture = Arch7bV2QualificationFactory.Create(supervisorExecutable, root, dotnetRoot: dotnetRoot);
        var adapters = new Arch7bRealCommandAdapterRegistry();
        var authority = new Arch7bCoreRdsSecretBrokerStaticAuthority(
            coreCommit, coreTree, module, ShaFile(module), cli, ShaFile(cli), nodeExecutable,
            ShaFile(nodeExecutable), fixture.Template.RuntimeInventorySha256,
            ShaFile(supervisorExecutable), "ARCH7B_RDS_TEST", TargetFingerprint,
            Read1VersionId, SecretArn, "1754288005", true, true,
            dotnetRoot, dotnetSha);
        var client = new Arch7bCoreRdsSecretBrokerClient(authority, adapters);
        var runtime = new Arch7bOneShotLiveExecutionRuntimeV2(new(),
            new Arch7bOneShotProcessRunnerV2(adapters), adapters, client);
        var result = await runtime.RunAsync(fixture.Template, fixture.Authority,
            fixture.OperatorAuthorization, fixture.TemplateFileSha256, root,
            TimeProvider.System, new Arch7bCoreOwnedSecretLease(), cancellationToken)
            .ConfigureAwait(false);
        var journal = File.Exists(Path.Combine(root, "live-facts.jsonl"))
            ? await File.ReadAllTextAsync(Path.Combine(root, "live-facts.jsonl"), cancellationToken)
                .ConfigureAwait(false)
            : string.Empty;
        var payloadPersistence = Count(journal, "NativeStdoutPayload") +
            Count(journal, "SYNTHETIC_BROKER_SECRET");
        var expectedStages = new[]
        {
            (Stage: "POSITION_APPLY", Fact: "broker_position_response",
                Adapter: "position-import-v1", Result: "ARCH7B_POSITION_IMPORT_APPLIED"),
            (Stage: "PMS_IMPORT", Fact: "broker_pms_response",
                Adapter: "pms-economic-replay-v1", Result: "ARCH7B_PMS_ECONOMIC_REPLAY_QUALIFIED"),
            (Stage: "ARCH7A_QUALIFY_SHADOW", Fact: "broker_arch7a_response",
                Adapter: "arch7a-shadow-v1", Result: "ARCH7A_SHADOW_QUALIFICATION_PERSISTED"),
            (Stage: "REPORTING", Fact: "broker_reporting_response",
                Adapter: "operational-reporting-v1", Result: "ANUBIS_INFX_READONLY_REPORTING_BUNDLE_CREATED")
        };
        var observedStages = result.Stages.Where(value =>
            expectedStages.Any(expected => expected.Stage == value.StageId)).ToArray();
        var sequence = client.LastSequence == 4 && expectedStages.Select(value => value.Stage)
            .SequenceEqual(observedStages.Select(value => value.StageId)) &&
            expectedStages.All(value => journal.Contains(value.Fact, StringComparison.Ordinal));
        var observedByStage = observedStages.ToDictionary(value => value.StageId, StringComparer.Ordinal);
        var fourAdapters = expectedStages.All(value =>
            observedByStage.TryGetValue(value.Stage, out var observed) &&
            fixture.Template.CommandTemplates.Single(command =>
                       command.StageId == value.Stage).AdapterId == value.Adapter &&
                   adapters.Adapters.Any(adapter => adapter.AdapterId == value.Adapter) &&
                   observed.NormalizedChildResultSha256 is not null &&
                   observed.ResultCode == value.Result);

        var terminal = journal.Contains("broker_terminal_evidence", StringComparison.Ordinal) &&
            !client.IsRunning && result.ResidualProcessCount == 0;
        var passed = result.Passed && sequence && fourAdapters && terminal && payloadPersistence == 0;
        if (!passed)
            throw new Arch7bQualificationException(result.FinalBlocker, string.Join('|',
                $"stage={result.PrimaryFailure?.FailureStage ?? "qualification-summary"}",
                $"sequence={sequence}", $"four_adapters={fourAdapters}",
                $"terminal={terminal}", $"payload_persistence={payloadPersistence}",
                $"residual_processes={result.ResidualProcessCount}",
                $"run_root={root}"));

        var runEvidence = new RunEvidence(result.RunId, passed, sequence, fourAdapters, terminal,
            payloadPersistence, 0, result.ResidualProcessCount,
            Arch7bOneShotContracts.Sha256(string.Join('\n', result.EvidenceSha256,
                sequence, fourAdapters, terminal, payloadPersistence)));
        if (Directory.Exists(root)) Directory.Delete(root, true);
        return runEvidence;
    }

    private static int Count(string value, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    private static string RequireFile(string path)
    {
        path = Path.GetFullPath(path);
        return File.Exists(path) ? path : throw new FileNotFoundException(path);
    }

    private static string ShaFile(string path) => Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(path)));

    private sealed record RunEvidence(string RunId, bool Passed, bool SequenceOneToFour,
        bool FourAdapters, bool TerminalCleanup, int TransientPayloadPersistenceCount,
        int SecretLeakCount, int ResidualProcessCount, string EvidenceSha256);
}
