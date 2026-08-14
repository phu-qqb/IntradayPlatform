extern alias M2C1ATool;

using System.Diagnostics;
using QQ.Production.Intraday.Infrastructure.PostgreSql;
using QQ.Production.Intraday.Tools.Arch7aShadowQualification;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;
using QQ.Production.Intraday.Tools.OperationalReporting;
using M2C1ATool::QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bOperationalCommandParserRoundtripTests : IDisposable
{
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string CoreCommit = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string IntradayCommit = "cccccccccccccccccccccccccccccccccccccccc";
    private readonly string root = Path.Combine(Path.GetTempPath(),
        "arch7b-parser-roundtrip", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Six_materialized_commands_roundtrip_through_real_parsers()
    {
        Directory.CreateDirectory(root);
        var observed = new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.Zero);
        var facts = Facts(observed);
        var authorities = Authorities();

        foreach (var command in Arch7bOperationalLiveFactBindingCatalog.Build())
        {
            var template = Template(command);
            var first = await new Arch7bOneShotCommandMaterializer().MaterializeAsync(
                template, facts, authorities, root, observed);
            AssertBindings(command, first.ArgumentList);
            await AssertParserAsync(command, first.ArgumentList);

            Directory.Delete(Path.GetDirectoryName(first.AuthorityPath)!, true);
            var second = await new Arch7bOneShotCommandMaterializer().MaterializeAsync(
                template, facts, authorities, root, observed);
            Assert.Equal(first.ArgumentList, second.ArgumentList);
            Assert.Equal(first.EvidenceSha256, second.EvidenceSha256);
        }
    }

    private Arch7bOneShotLiveFactStore Facts(DateTimeOffset observed)
    {
        var facts = new Arch7bOneShotLiveFactStore(root);
        Add(facts, "runtime_run_root", "STATIC_AUTHORITY_VALIDATION",
            new { path = root }, observed);
        Add(facts, "core_commit", "STATIC_AUTHORITY_VALIDATION",
            new { value = CoreCommit }, observed);
        Add(facts, "intraday_commit", "STATIC_AUTHORITY_VALIDATION",
            new { value = IntradayCommit }, observed);
        Add(facts, "run_identity", "ONE_SHOT_IDENTITIES_CREATED",
            new { value = "arch7b-parser-roundtrip" }, observed);
        Add(facts, "source_session_identity", "ONE_SHOT_IDENTITIES_CREATED",
            new { value = "arch7b-source-session" }, observed);
        Add(facts, "market_capture_session_identity", "ONE_SHOT_IDENTITIES_CREATED",
            new { value = "11111111-1111-1111-1111-111111111111" }, observed);
        Add(facts, "position_market_draft_output_path", "ONE_SHOT_IDENTITIES_CREATED",
            new { path = Path.Combine(root, Arch7bOneShotRunArtifactPath.PositionMarketDraftFilename) }, observed);
        Add(facts, "position_market_lineage_output_path", "ONE_SHOT_IDENTITIES_CREATED",
            new { path = Path.Combine(root, Arch7bOneShotRunArtifactPath.PositionMarketLineageFilename) }, observed);
        Add(facts, "position_market_revision_binding_output_path", "ONE_SHOT_IDENTITIES_CREATED",
            new { path = Path.Combine(root, Arch7bOneShotRunArtifactPath.PositionMarketRevisionBindingFilename) }, observed);
        Add(facts, Arch7bClockFactContracts.PreflightFactType, "CLOCK_PREFLIGHT",
            new { path = Path.Combine(root, "clock-preflight.json") }, observed);
        Add(facts, Arch7bClockFactContracts.CaptureStartFactType, "CLOCK_CAPTURE_START",
            new { path = Path.Combine(root, "clock-capture.json") }, observed);
        Add(facts, Arch7bClockFactContracts.PostCloseFactType, "CLOCK_POST_CLOSE",
            new { path = Path.Combine(root, "clock-post-close.json") }, observed);
        Add(facts, "position_market_draft_artifact", "POSITION_MARKET_DRAFT",
            new { path = Path.Combine(root, Arch7bOneShotRunArtifactPath.PositionMarketDraftFilename), sha256 = Sha }, observed);
        Add(facts, "selected_slot", "SLOT_SELECTED",
            new { slot_id = "pms-shadow-15m-20260810T1200Z" }, observed);
        Add(facts, "position_market_lineage_artifact", "POSITION_MARKET_LINEAGE",
            new { path = Path.Combine(root, Arch7bOneShotRunArtifactPath.PositionMarketLineageFilename), sha256 = Sha }, observed);
        Add(facts, "economic_revision_artifact", "ECONOMIC_REVISION",
            new { economic_revision_id = "22222222-2222-2222-2222-222222222222" }, observed);
        Add(facts, "position_market_revision_binding_artifact", "REVISION_BINDING",
            new { path = Path.Combine(root, Arch7bOneShotRunArtifactPath.PositionMarketRevisionBindingFilename), sha256 = Sha }, observed);
        return facts;
    }

    private IReadOnlyDictionary<string, Arch7bFileAuthority> Authorities()
    {
        var executable = Path.Combine(root, "qualified-child.exe");
        var config = Path.Combine(root, "market-data-config.json");
        File.WriteAllText(executable, "offline parser qualification");
        File.WriteAllText(config, "{}");
        return new Dictionary<string, Arch7bFileAuthority>(StringComparer.Ordinal)
        {
            ["child_executable"] = new("child_executable", executable, Sha, true, false),
            ["child_working_directory"] = new("child_working_directory", root, Sha, true, false),
            ["market_data_config"] = new("market_data_config", config, Sha, true, false)
        };
    }

    private Arch7bOneShotCommandTemplate Template(Arch7bOperationalCommandBindingSet command)
    {
        var arguments = command.CommandId switch
        {
            "prearmed-importer" => Handoff(command, false),
            "capture-starter" or "canonical-slot-finalizer" => Handoff(command, true),
            "market-data-recorder" => Market(command),
            "arch7a-qualification" => Arch7a(command),
            "read-only-reporting" => Reporting(command),
            _ => throw new InvalidOperationException(command.CommandId)
        };
        var value = new Arch7bOneShotCommandTemplate(
            Arch7bV2Contracts.CommandTemplateVersion, command.CommandId, command.StageId,
            Arch7bExecutionKind.ChildInvoke, "child_executable", arguments,
            "child_working_directory", "qualification-parser-roundtrip",
            Arch7bV2Contracts.ChildResultAdapterVersion, command.CommandId + "_parser_v1",
            30, 1_048_576, 1_048_576, "qualification-child-process",
            false, false, false, [], [], null, string.Empty);
        return value with { EvidenceSha256 = Arch7bOneShotContracts.Sha256(
            string.Join('\n', command.CommandId, string.Join('|', arguments.Select(x => x.Value)))) };
    }

    private IReadOnlyList<Arch7bCommandTemplateArgument> Handoff(
        Arch7bOperationalCommandBindingSet command, bool draftSha)
    {
        var a = new List<Arch7bCommandTemplateArgument>();
        P(a, "--mode", L(command.Mode));
        P(a, "--slot-close-utc", L("2026-08-10T12:00:00.0000000+00:00"));
        P(a, "--handoff-root", RunRoot());
        P(a, "--target-profile-id", L("ARCH7B_RDS_TEST"));
        P(a, "--broker-password-only", L("false"));
        P(a, "--repository-commit", Intraday());
        P(a, "--core-commit", B(command, "--core-commit"));
        P(a, "--market-capture-session-id", B(command, "--market-capture-session-id"));
        P(a, "--market-data-config-path", BOr(command, "--market-data-config-path",
            L(Path.Combine(root, "missing-market-data-config.json"))));
        P(a, "--expected-market-data-config-sha256",
            BOr(command, "--expected-market-data-config-sha256", L(new string('a', 64))));
        P(a, "--position-market-draft-path", B(command, "--position-market-draft-path"));
        if (draftSha) P(a, "--expected-position-market-draft-sha256",
            B(command, "--expected-position-market-draft-sha256"));
        P(a, "--position-market-lineage-path", BOr(command, "--position-market-lineage-path",
            F("position_market_lineage_output_path", "path", Arch7bPlaceholderValueKind.AbsolutePath,
                "ONE_SHOT_IDENTITIES_CREATED", true)));
        P(a, "--position-market-revision-binding-path",
            BOr(command, "--position-market-revision-binding-path",
                F("position_market_revision_binding_output_path", "path",
                    Arch7bPlaceholderValueKind.AbsolutePath, "ONE_SHOT_IDENTITIES_CREATED", true)));
        P(a, "--source-session-id", F("source_session_identity", "value",
            Arch7bPlaceholderValueKind.String, "ONE_SHOT_IDENTITIES_CREATED"));
        P(a, "--run-id", F("run_identity", "value",
            Arch7bPlaceholderValueKind.String, "ONE_SHOT_IDENTITIES_CREATED"));
        P(a, "--expected-environment", L("TEST"));
        P(a, "--expected-database", L("qq_pms_shadow_arch7b_test"));
        P(a, "--expected-schema", L("pms_shadow"));
        P(a, "--expected-postgres-major", L("18"));
        P(a, "--require-tls", L("true"));
        P(a, "--allow-loopback", L("false"));
        P(a, "--capture-root", RunRoot());
        P(a, "--clock-authority-preflight-snapshot", BOr(command, "--clock-authority-preflight-snapshot", L(Path.Combine(root, "clock-preflight.json"))));
        P(a, "--clock-authority-capture-snapshot", BOr(command, "--clock-authority-capture-snapshot", L(Path.Combine(root, "clock-capture.json"))));
        P(a, "--clock-authority-post-close-snapshot", BOr(command, "--clock-authority-post-close-snapshot", L(Path.Combine(root, "clock-post-close.json"))));
        P(a, "--artifact-path", L(Path.Combine(root, "slot.jsonl")));
        P(a, "--manifest-path", L(Path.Combine(root, "slot_manifest.json")));
        return a;
    }

    private IReadOnlyList<Arch7bCommandTemplateArgument> Market(
        Arch7bOperationalCommandBindingSet command)
    {
        var a = new List<Arch7bCommandTemplateArgument> { L("capture") };
        P(a, "--config", L(Path.Combine(root, "missing-config.json")));
        P(a, "--position-market-draft-path", B(command, "--position-market-draft-path"));
        P(a, "--expected-position-market-draft-sha256",
            B(command, "--expected-position-market-draft-sha256"));
        a.Add(L("--no-order-entry")); a.Add(L("--no-account-api")); a.Add(L("--no-db"));
        return a;
    }

    private IReadOnlyList<Arch7bCommandTemplateArgument> Arch7a(
        Arch7bOperationalCommandBindingSet command)
    {
        var a = new List<Arch7bCommandTemplateArgument>();
        P(a, "--mode", L("qualify-shadow"));
        foreach (var name in new[] { "--economic-revision-id", "--slot-id", "--source-session-id",
                     "--position-market-revision-binding-path",
                     "--expected-position-market-revision-binding-sha256" })
            P(a, name, B(command, name));
        P(a, "--target-profile-id", L("ARCH7B_RDS_TEST"));
        P(a, "--expected-environment", L("TEST"));
        P(a, "--expected-database", L("qq_pms_shadow_arch7b_test"));
        P(a, "--expected-schema", L("pms_shadow"));
        P(a, "--expected-postgres-major", L("18"));
        P(a, "--require-tls", L("true")); P(a, "--allow-loopback", L("false"));
        P(a, "--root-certificate", L(Arch7aArch7bShadowQualificationArguments.CanonicalRootCertificatePath));
        P(a, "--expected-root-certificate-sha256", L(Arch7aArch7bShadowQualificationArguments.RootCertificateSha256));
        P(a, "--expected-target-fingerprint", L(Arch7aArch7bShadowQualificationArguments.TargetFingerprint));
        P(a, "--repository-commit", B(command, "--repository-commit"));
        P(a, "--output-directory", B(command, "--output-directory"));
        P(a, "--connection-secret-reference", L(Arch7aArch7bShadowQualificationArguments.CredentialReference));
        P(a, "--role", L(Arch7aArch7bShadowQualificationArguments.DatabaseRole));
        P(a, "--no-order", L("true"));
        P(a, "--validate-command-contract-only", L("true"));
        return a;
    }

    private IReadOnlyList<Arch7bCommandTemplateArgument> Reporting(
        Arch7bOperationalCommandBindingSet command)
    {
        var a = new List<Arch7bCommandTemplateArgument> { L("report-operational-state"), L("--no-order") };
        P(a, "--expected-environment", L("TEST"));
        P(a, "--expected-database", L("qq_pms_shadow_arch7b_test"));
        P(a, "--expected-schema", L("pms_shadow"));
        P(a, "--expected-postgresql-major", L("18"));
        P(a, "--target-profile", L("ARCH7B_RDS_TEST"));
        P(a, "--expected-target-fingerprint", L(Sha));
        P(a, "--output-directory", RunRoot());
        P(a, "--repository-commit", Intraday());
        P(a, "--repository-root", L(root));
        foreach (var name in new[] { "--position-market-lineage-path",
                     "--expected-position-market-lineage-sha256",
                     "--position-market-revision-binding-path",
                     "--expected-position-market-revision-binding-sha256" })
            P(a, name, B(command, name));
        return a;
    }

    private static async Task AssertParserAsync(
        Arch7bOperationalCommandBindingSet command, IReadOnlyList<string> arguments)
    {
        if (command.CommandId is "prearmed-importer" or "capture-starter" or "canonical-slot-finalizer")
        {
            var result = await HandoffChild(arguments,
                command.CommandId == "prearmed-importer");
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains(command.CommandId == "prearmed-importer"
                    ? "QQ_PMS_SHADOW_ARCH7B_CONNECTION_STRING_REQUIRED"
                    : "HANDOFF_IMPORTER_NOT_PREARMED",
                result.Output, StringComparison.Ordinal);
        }
        else if (command.CommandId == "market-data-recorder")
            await Assert.ThrowsAnyAsync<IOException>(() =>
                LmaxMarketDataCaptureOnlyPreflightCommand.RunAsync(arguments.ToArray()));
        else if (command.CommandId == "arch7a-qualification")
        {
            var parsed = Arch7aArch7bShadowQualificationArguments.Parse(arguments.ToArray());
            Assert.True(parsed.ValidateCommandContractOnly);
            Assert.True(parsed.NoOrder);
        }
        else
            Assert.Equal("report-operational-state",
                ReportingArguments.Parse(arguments.ToArray()).Mode);
    }

    private static async Task<ChildResult> HandoffChild(
        IReadOnlyList<string> arguments, bool removeConnection)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true
        };
        start.ArgumentList.Add(typeof(Arch7bPrearmedFreshSlotHandoffCli).Assembly.Location);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        if (removeConnection) start.Environment.Remove("QQ_PMS_SHADOW_ARCH7B_CONNECTION_STRING");
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        return new(process.ExitCode, (await output) + Environment.NewLine + (await error));
    }

    private static void AssertBindings(
        Arch7bOperationalCommandBindingSet command, IReadOnlyList<string> arguments)
    {
        foreach (var binding in command.Bindings)
        {
            var index = arguments.ToList().IndexOf(binding.ArgumentName);
            Assert.True(index >= 0 && index + 1 < arguments.Count, binding.BindingId);
            Assert.NotEqual(Arch7bOperationalLiveFactBindingCatalog.Marker, arguments[index + 1]);
        }
    }

    private static void Add(Arch7bOneShotLiveFactStore facts, string type,
        string producer, object value, DateTimeOffset observed) =>
        facts.Append(type, producer, value,
            Arch7bOneShotContracts.Sha256(type + ":" + producer), observed);

    private static void P(ICollection<Arch7bCommandTemplateArgument> values,
        string name, Arch7bCommandTemplateArgument value)
    { values.Add(L(name)); values.Add(value); }

    private static Arch7bCommandTemplateArgument B(
        Arch7bOperationalCommandBindingSet command, string name)
    {
        var value = command.Bindings.Single(x => x.ArgumentName == name);
        return new(value.Placeholder, value.ValueKind, value.RequiredProducerStage,
            value.MaximumAgeSeconds, value.MustBeInsideRunRoot);
    }

    private static Arch7bCommandTemplateArgument BOr(
        Arch7bOperationalCommandBindingSet command, string name,
        Arch7bCommandTemplateArgument fallback) =>
        command.Bindings.Any(x => x.ArgumentName == name) ? B(command, name) : fallback;

    private static Arch7bCommandTemplateArgument L(string value) =>
        new(value, Arch7bPlaceholderValueKind.Literal, null, -1, false);

    private static Arch7bCommandTemplateArgument F(string name, string field,
        Arch7bPlaceholderValueKind kind, string producer, bool inside = false) =>
        new("$" + "{fact:" + name + "." + field + "}", kind, producer, -1, inside);

    private static Arch7bCommandTemplateArgument RunRoot() =>
        F("runtime_run_root", "path", Arch7bPlaceholderValueKind.AbsolutePath,
            "STATIC_AUTHORITY_VALIDATION", true);

    private static Arch7bCommandTemplateArgument Intraday() =>
        F("intraday_commit", "value", Arch7bPlaceholderValueKind.GitCommit,
            "STATIC_AUTHORITY_VALIDATION");

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private sealed record ChildResult(int ExitCode, string Output);
}
