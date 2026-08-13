using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bCorePrequalificationOutputContractTests
{
    [Theory]
    [InlineData("{\"qualification\":{},\"manifest\":{}}",
        Arch7bChildOutputClassifier.PureExpected)]
    [InlineData("npm banner\n{\"qualification\":{},\"manifest\":{}}",
        Arch7bChildOutputClassifier.WrapperContamination)]
    [InlineData("{\"qualification\":{},\"manifest\":{}}\nwrapper suffix",
        Arch7bChildOutputClassifier.WrapperContamination)]
    [InlineData("{}\n{}",
        Arch7bChildOutputClassifier.MultipleDocuments)]
    [InlineData("{not-json", Arch7bChildOutputClassifier.Other)]
    [InlineData("\uFEFF{\"qualification\":{},\"manifest\":{}}",
        Arch7bChildOutputClassifier.BomOnly)]
    [InlineData("{\"contract\":\"wrong-shape\"}",
        Arch7bChildOutputClassifier.ShapeMismatch)]
    public void Stdout_classifier_is_strict_and_never_extracts_embedded_json(
        string stdout, string expected)
    {
        Assert.Equal(expected, Arch7bChildOutputClassifier.Classify(stdout));
    }

    [Fact]
    public async Task Invalid_utf8_is_rejected_before_any_text_can_be_persisted()
    {
        await using var stream = new MemoryStream([0xc3, 0x28]);

        var failure = await Assert.ThrowsAsync<Arch7bQualificationException>(() =>
            Arch7bBoundedStreamReader.ReadAsync(stream, 1024, [], [],
                CancellationToken.None));

        Assert.Equal(Arch7bBlockers.ChildOutputInvalid, failure.BlockerCode);
        Assert.Contains(Arch7bChildOutputClassifier.InvalidUtf8, failure.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("malformed", 0, "{not-json")]
    [InlineData("crash", 5, "")]
    public async Task Receipt_precedes_adapter_and_survives_adapter_failure(
        string behavior, int expectedExitCode, string expectedStdout)
    {
        var root = Root("receipt-" + behavior);
        var fixture = Arch7bV2QualificationFactory.Create(
            SupervisorExecutable(), root, "CORE_PREQUALIFICATION", behavior);
        var adapters = new Arch7bRealCommandAdapterRegistry();
        var runtime = new Arch7bOneShotLiveExecutionRuntimeV2(new(),
            new Arch7bOneShotProcessRunnerV2(adapters), adapters);

        var result = await runtime.RunAsync(fixture.Template, fixture.Authority,
            fixture.OperatorAuthorization, fixture.TemplateFileSha256, root,
            TimeProvider.System, new Arch7bCoreOwnedSecretLease());

        Assert.False(result.Passed);
        var commandRoot = Path.Combine(root, "commands",
            "CORE_PREQUALIFICATION", "offline-core-prequalification");
        var receiptPath = Path.Combine(commandRoot,
            "child-process-output-receipt.json");
        var failurePath = Path.Combine(commandRoot,
            "child-adapter-failure.json");
        Assert.True(File.Exists(receiptPath));
        Assert.True(File.Exists(failurePath));
        using var receipt = JsonDocument.Parse(
            await File.ReadAllBytesAsync(receiptPath));
        var receiptRoot = receipt.RootElement;
        Assert.Equal(Arch7bV2Contracts.ChildProcessOutputReceiptVersion,
            receiptRoot.GetProperty("contract_version").GetString());
        Assert.Equal(expectedExitCode,
            receiptRoot.GetProperty("exit_code").GetInt32());
        Assert.Equal(Encoding.UTF8.GetByteCount(expectedStdout),
            receiptRoot.GetProperty("stdout_byte_count").GetInt64());
        Assert.Equal(Sha(expectedStdout),
            receiptRoot.GetProperty("stdout_sha256").GetString());
        Assert.Equal(Sha(string.Empty),
            receiptRoot.GetProperty("stderr_sha256").GetString());
        Assert.False(receiptRoot.GetProperty("raw_output_recorded").GetBoolean());
        Assert.True(receiptRoot.GetProperty("utf8_validated").GetBoolean());
        Assert.True(receiptRoot.GetProperty("secret_scan_passed").GetBoolean());

        using var adapterFailure = JsonDocument.Parse(
            await File.ReadAllBytesAsync(failurePath));
        var failureRoot = adapterFailure.RootElement;
        Assert.Equal(ShaFile(receiptPath),
            failureRoot.GetProperty("receipt_sha256").GetString());
        Assert.False(failureRoot.GetProperty("raw_output_recorded").GetBoolean());
        Assert.Equal(Arch7bBlockers.ChildOutputInvalid,
            failureRoot.GetProperty("native_blocker").GetString());
        var evidenceText = await File.ReadAllTextAsync(failurePath);
        if (expectedStdout.Length > 0)
            Assert.DoesNotContain(expectedStdout, evidenceText,
            StringComparison.Ordinal);
        Assert.DoesNotContain("password=", evidenceText,
            StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.EnumerateFiles(commandRoot, "*stdout*",
            SearchOption.TopDirectoryOnly));
        Assert.Empty(Directory.EnumerateFiles(commandRoot, "*stderr*",
            SearchOption.TopDirectoryOnly));
        Assert.True(result.Cleanup.Complete);
        Assert.Equal(0, result.ResidualProcessCount);
        Assert.Equal(0, result.ResidualMarkerCount);
        Directory.Delete(root, true);
    }

    [Fact]
    public void Receipt_rebuild_is_byte_for_byte_deterministic_for_equal_facts()
    {
        var at = DateTimeOffset.Parse("2026-08-12T00:00:00Z");
        var provisional = new Arch7bChildProcessOutputReceipt(
            Arch7bV2Contracts.ChildProcessOutputReceiptVersion, "command",
            "CORE_PREQUALIFICATION", 123, 0, at, at.AddSeconds(1), 1000,
            10, new string('1', 64), 0, Sha(string.Empty), true, true, 2,
            false, "core-prequalification-v1",
            Arch7bCoreRuntimePrequalificationAdapter.NativeContract,
            new string('2', 64), string.Empty);
        var receipt = provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(
                provisional.Canonical())
        };

        var first = JsonSerializer.SerializeToUtf8Bytes(receipt,
            Arch7bJson.CanonicalOptions);
        var second = JsonSerializer.SerializeToUtf8Bytes(receipt,
            Arch7bJson.CanonicalOptions);

        Assert.Equal(first, second);
        Assert.DoesNotContain("password", Encoding.UTF8.GetString(first),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void One_shot_exit_policy_is_authoritative_for_native_evidence()
    {
        var pass = Evidence(true);
        var noGo = Evidence(false);

        Assert.Equal(0, Program.ExitCodeFor("run-one-shot", pass));
        Assert.Equal(2, Program.ExitCodeFor("run-one-shot", noGo));
        Assert.Equal(1, Program.ExitCodeFor("run-one-shot", new object()));
        Assert.Equal(0, Program.ExitCodeFor("validate-one-shot-plan", new object()));
    }

    [Fact]
    public void Qualified_static_preflight_has_zero_exit_without_live_evidence()
    {
        var authority = new Arch7bOperationalExecutionAuthorityValidation(
            "validation-v1", 0, 0, 0, 0, 0, 0, 0, [], new string('1', 64));
        var entrypoints = new Arch7bChildEntrypointValidation(
            "entrypoints-v1", 0, 0, 0, 0, [], new string('2', 64));
        var evidence = new Arch7bOneShotStaticPreflightEvidence(
            "ARCH7B_ONE_SHOT_STATIC_PREFLIGHT_QUALIFIED", true,
            "STATIC_AUTHORITY_VALIDATION", authority, entrypoints,
            false, false, false, false, false, 0, 0,
            Arch7bNoLiveSafetyCounters.Zero);

        Assert.Equal(0, Program.ExitCodeFor("run-one-shot", evidence));
    }

    [Fact]
    public void Ssm_result_requires_both_zero_exit_and_passed_evidence()
    {
        var pass = Evidence(true);
        var noGo = Evidence(false);

        Assert.True(Program.IsSuccessfulSsmResult(0, pass));
        Assert.False(Program.IsSuccessfulSsmResult(0, noGo));
        Assert.False(Program.IsSuccessfulSsmResult(2, pass));
    }

    private static Arch7bV2ExecutionEvidence Evidence(bool passed)
    {
        var cleanup = new Arch7bCleanupReport("cleanup-v1", null, null, [], [],
            passed, TimeSpan.Zero, new string('3', 64));
        return new(Arch7bV2Contracts.LiveExecutionRuntimeVersion, "run", "slot",
            [], new(1, 2, 1, 0),
            passed ? Arch7bOneShotContracts.ExpectedFinalBlocker : "BLOCKED",
            null, cleanup, [], 0, 0, passed, Arch7bNoLiveSafetyCounters.Zero,
            new string('4', 64));
    }

    private static string Root(string suffix) => Path.Combine(Path.GetTempPath(),
        "qq-arch7b-output-contract-tests",
        suffix + "-" + Guid.NewGuid().ToString("N"));

    private static string SupervisorExecutable()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName,
                   "QQ.Production.Intraday.sln"))) current = current.Parent;
        var repository = current?.FullName ??
            throw new DirectoryNotFoundException("repository root");
        var extension = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        return Path.Combine(repository, "tools",
            "QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor", "bin",
            "Release", "net10.0",
            "QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor" + extension);
    }

    private static string Sha(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string ShaFile(string path) => Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(path)));
}
