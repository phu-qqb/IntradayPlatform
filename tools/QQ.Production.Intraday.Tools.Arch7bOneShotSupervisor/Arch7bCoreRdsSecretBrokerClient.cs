using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public static class Arch7bCoreRdsSecretBrokerContracts
{
    public const string Client = "arch7b_core_rds_secret_broker_client_v1";
    public const string ProcessAuthority = "arch7b_core_rds_secret_broker_process_authority_v1";
    public const string Plan = "arch7b_rds_secret_child_command_plan_v1";
    public const string Template = "arch7b_rds_secret_child_command_template_v1";
    public const string Request = "arch7b_rds_secret_child_command_request_v1";
    public const string Response = "arch7b_rds_secret_child_command_response_v1";
    public const string Protocol = "arch7b_rds_secret_child_protocol_v1";
    public const string Ready = "arch7b_rds_secret_broker_ready_v1";
    public const string TerminalTransition =
        "arch7b_rds_secret_broker_terminal_readonly_transition_v1";
    public const string TerminalCleanup = "arch7b_rds_secret_broker_terminal_cleanup_v1";
    public const string Broker = "arch7b_rds_secret_child_command_broker_v1";
    public const string SecretVariable = "QQ_ARCH7B_POSITION_IMPORT_FAST_PATH";
    public const int MaximumFrameBytes = 1_048_576;
    public const string InitialResponseSha256 =
        "0000000000000000000000000000000000000000000000000000000000000000";
}

public static class Arch7bCoreRdsSecretBrokerBlockers
{
    public const string AuthorityInvalid = "ARCH7B_CORE_BROKER_PROCESS_AUTHORITY_INVALID";
    public const string PlanInvalid = "ARCH7B_CORE_BROKER_COMMAND_PLAN_INVALID";
    public const string FrameOversized = "ARCH7B_CORE_BROKER_FRAME_OVERSIZED";
    public const string FrameNonCanonical = "ARCH7B_CORE_BROKER_FRAME_NONCANONICAL";
    public const string FrameInvalid = "ARCH7B_CORE_BROKER_FRAME_INVALID";
    public const string FrameUnexpected = "ARCH7B_CORE_BROKER_FRAME_UNEXPECTED";
    public const string FrameTimeout = "ARCH7B_CORE_BROKER_FRAME_TIMEOUT";
    public const string PrematureEof = "ARCH7B_CORE_BROKER_PREMATURE_EOF";
    public const string SequenceMismatch = "ARCH7B_CORE_BROKER_SEQUENCE_MISMATCH";
    public const string PreviousResponseMismatch = "ARCH7B_CORE_BROKER_PREVIOUS_RESPONSE_MISMATCH";
    public const string PayloadPersistence = "ARCH7B_CORE_BROKER_TRANSIENT_PAYLOAD_PERSISTENCE_FORBIDDEN";
    public const string StateInvalid = "ARCH7B_CORE_BROKER_STATE_INVALID";
    public const string CleanupIncomplete = "ARCH7B_CORE_BROKER_CLEANUP_INCOMPLETE";
}

public sealed record Arch7bCoreRdsSecretBrokerStaticAuthority(
    string CoreCommit,
    string CoreTree,
    string CoreBrokerModulePath,
    string CoreBrokerModuleSha256,
    string CoreBrokerCliPath,
    string CoreBrokerCliSha256,
    string NodeExecutablePath,
    string NodeExecutableSha256,
    string IntradayRuntimeInventorySha256,
    string IntradayBinarySha256,
    string TargetProfileId,
    string TargetFingerprint,
    string Read1VersionId,
    string SecretArn,
    string AccountId,
    bool QualificationOnly,
    bool NoOrder,
    string? DotnetRootPath = null,
    string? DotnetExecutableSha256 = null)
{
    public void Validate()
    {
        RequireCommit(CoreCommit);
        RequireCommit(CoreTree);
        RequireSha(IntradayRuntimeInventorySha256);
        RequireSha(IntradayBinarySha256);
        RequireSha(TargetFingerprint);
        if (!Guid.TryParseExact(Read1VersionId, "D", out _))
            Fail(Arch7bCoreRdsSecretBrokerBlockers.AuthorityInvalid);
        RequireFile(CoreBrokerModulePath, CoreBrokerModuleSha256);
        RequireFile(CoreBrokerCliPath, CoreBrokerCliSha256);
        RequireFile(NodeExecutablePath, NodeExecutableSha256);
        if ((DotnetRootPath is null) != (DotnetExecutableSha256 is null))
            Fail(Arch7bCoreRdsSecretBrokerBlockers.AuthorityInvalid, "dotnet-root-pair");
        if (DotnetRootPath is not null)
        {
            if (!Path.IsPathFullyQualified(DotnetRootPath) || !Directory.Exists(DotnetRootPath))
                Fail(Arch7bCoreRdsSecretBrokerBlockers.AuthorityInvalid, "dotnet-root");
            var dotnetExecutable = Path.Combine(Path.GetFullPath(DotnetRootPath),
                OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            RequireFile(dotnetExecutable, DotnetExecutableSha256!);
        }
        if (TargetProfileId != "ARCH7B_RDS_TEST" || AccountId != "1754288005" ||
            !SecretArn.StartsWith("arn:aws:secretsmanager:", StringComparison.Ordinal) || !NoOrder)
            Fail(Arch7bCoreRdsSecretBrokerBlockers.AuthorityInvalid);
    }

    private static void RequireFile(string path, string expectedSha)
    {
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
            Fail(Arch7bCoreRdsSecretBrokerBlockers.AuthorityInvalid);
        RequireSha(expectedSha);
        var actual = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        if (actual != expectedSha) Fail(Arch7bCoreRdsSecretBrokerBlockers.AuthorityInvalid);
    }

    internal static void RequireCommit(string value)
    {
        if (value.Length != 40 || value.Any(value => !char.IsAsciiHexDigit(value) || char.IsUpper(value)))
            Fail(Arch7bCoreRdsSecretBrokerBlockers.AuthorityInvalid);
    }

    internal static void RequireSha(string value)
    {
        if (!Arch7bOneShotContracts.IsSha256(value) || value.Any(char.IsUpper))
            Fail(Arch7bCoreRdsSecretBrokerBlockers.AuthorityInvalid);
    }

    [DoesNotReturn]
    internal static void Fail(string blocker, string? detail = null) =>
        throw new Arch7bQualificationException(blocker, detail);
}

public sealed record Arch7bCoreRdsSecretBrokerProcessAuthority(
    string ContractVersion,
    string CoreCommit,
    string CoreTree,
    string CoreBrokerModulePath,
    string CoreBrokerModuleSha256,
    string CoreBrokerCliPath,
    string CoreBrokerCliSha256,
    string NodeExecutablePath,
    string NodeExecutableSha256,
    string CommandPlanPath,
    string CommandPlanSha256,
    string ConfigPath,
    string ConfigSha256,
    string ProtocolVersion,
    string BrokerContractVersion,
    string ExpectedReadyContract,
    string ExpectedTerminalContract,
    string RunId,
    string OwnerId,
    string FutureAuthorizationId,
    string Read1VersionId,
    string TargetProfileId,
    string TargetFingerprint,
    string AccountId,
    bool NoOrder,
    string EvidenceSha256);

public sealed record Arch7bCoreBrokerMaterialization(
    Arch7bCoreRdsSecretBrokerProcessAuthority Authority,
    IReadOnlyDictionary<string, string> CommandIdsByStage);

public sealed record Arch7bCoreBrokerPersistableResponse(
    string ContractVersion,
    string CommandId,
    string StageId,
    int SequenceNumber,
    string Phase,
    string ChildExitClassification,
    string PreviousResponseEvidenceSha256,
    string NativeOutputContract,
    string NativeOutputSha256,
    int NativeOutputByteCount,
    string EvidenceSha256,
    string PersistableJson);

public sealed record Arch7bCoreBrokerCommandResult(
    Arch7bCoreBrokerPersistableResponse Response,
    Arch7bNormalizedChildResult AdaptedResult);

public interface IArch7bCoreRdsSecretBrokerClient : IAsyncDisposable
{
    bool IsRunning { get; }
    string Phase { get; }
    int LastSequence { get; }
    string PreviousResponseSha256 { get; }
    string ExpectedRead1VersionId { get; }
    Arch7bCoreBrokerMaterialization? Materialization { get; }
    Task<Arch7bCoreBrokerMaterialization> MaterializeAndStartAsync(
        Arch7bOneShotLivePlanTemplate template, Arch7bOneShotLiveFactStore facts,
        string runRoot, string read1VersionId, CancellationToken cancellationToken = default);
    Task<JsonObject> ReadReadyAsync(CancellationToken cancellationToken = default);
    Task<JsonObject> MarkBracketStartedAsync(string bracketEvidenceSha256,
        CancellationToken cancellationToken = default);
    Task<Arch7bCoreBrokerCommandResult> ExecuteAsync(Arch7bOneShotMaterializedCommand command,
        string inputFactEvidenceSha256, string runRoot,
        CancellationToken cancellationToken = default);
    Task<JsonObject> MarkTerminalReadonlyAsync(string economicRevisionBindingEvidenceSha256,
        string arch7aQualificationEvidenceSha256, string reportingInputAuthoritySha256,
        CancellationToken cancellationToken = default);
    Task<JsonObject> ShutdownAsync(CancellationToken cancellationToken = default);
}

public sealed class Arch7bCoreRdsSecretBrokerPlanMaterializer(
    Arch7bCoreRdsSecretBrokerStaticAuthority staticAuthority)
{
    private static readonly IReadOnlyDictionary<string, (string CommandId, string Phase)> BrokerStages =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["POSITION_APPLY"] = ("position-import-apply", "POST_BRACKET"),
            ["PMS_IMPORT"] = ("pms-economic-replay", "POST_BRACKET"),
            ["ARCH7A_QUALIFY_SHADOW"] = ("arch7a-qualify-shadow", "POST_BRACKET"),
            ["REPORTING"] = ("operational-reporting", "TERMINAL_READONLY")
        };

    public async Task<Arch7bCoreBrokerMaterialization> MaterializeAsync(
        Arch7bOneShotLivePlanTemplate template, Arch7bOneShotLiveFactStore facts,
        string runRoot, string read1VersionId, CancellationToken cancellationToken = default)
    {
        staticAuthority.Validate();
        if (!Guid.TryParseExact(read1VersionId, "D", out _))
            Arch7bCoreRdsSecretBrokerStaticAuthority.Fail(
                Arch7bCoreRdsSecretBrokerBlockers.PlanInvalid, "read1-version");
        var now = DateTimeOffset.UtcNow;
        var runId = FactValue(facts.Require("run_identity", "ONE_SHOT_IDENTITIES_CREATED", now, int.MaxValue));
        var ownerId = FactValue(facts.Require("owner_identity", "ONE_SHOT_IDENTITIES_CREATED", now, int.MaxValue));
        var futureId = FactValue(facts.Require("future_authorization_identity",
            "ONE_SHOT_IDENTITIES_CREATED", now, int.MaxValue));
        runRoot = Path.GetFullPath(runRoot);
        var brokerRoot = Path.Combine(runRoot, "core-rds-secret-broker");
        Directory.CreateDirectory(brokerRoot);
        var commands = new JsonArray();
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var sequence = 0;
        foreach (var pair in BrokerStages)
        {
            sequence++;
            var source = template.CommandTemplates.Single(value => value.StageId == pair.Key);
            var executable = RequireAuthority(template, source.ExecutableAuthorityId);
            var workingDirectory = RequireAuthority(template, source.WorkingDirectoryAuthorityId);
            var schemas = new JsonArray();
            for (var index = 0; index < source.ArgumentTemplates.Count; index++)
                schemas.Add(ArgumentSchema(index, source.ArgumentTemplates[index]));
            var command = new JsonObject
            {
                ["ContractVersion"] = Arch7bCoreRdsSecretBrokerContracts.Template,
                ["CommandId"] = pair.Value.CommandId,
                ["StageId"] = pair.Key,
                ["SequenceNumber"] = sequence,
                ["ExecutablePath"] = Path.GetFullPath(executable.Path),
                ["ExecutableSha256"] = executable.Sha256,
                ["WorkingDirectory"] = Path.GetFullPath(workingDirectory.Path),
                ["IntradayCommit"] = template.IntradayCommit,
                ["IntradayTree"] = template.IntradayTree,
                ["RuntimeInventorySha256"] = staticAuthority.IntradayRuntimeInventorySha256,
                ["ArgumentSchema"] = schemas,
                ["SecretVariableNames"] = new JsonArray(Arch7bCoreRdsSecretBrokerContracts.SecretVariable),
                ["NonSecretEnvironment"] = staticAuthority.DotnetRootPath is null
                    ? new JsonObject()
                    : new JsonObject { ["DOTNET_ROOT"] = Path.GetFullPath(staticAuthority.DotnetRootPath) },
                ["NativeOutputContract"] = source.ExpectedNativeOutputContract,
                ["TimeoutMilliseconds"] = checked(source.TimeoutSeconds * 1000),
                ["StandardOutputMaximumBytes"] = source.StandardOutputLimitBytes,
                ["StandardErrorMaximumBytes"] = source.StandardErrorLimitBytes,
                ["AllowedPhase"] = pair.Value.Phase,
                ["OneShot"] = true,
                ["NoOrder"] = true
            };
            AddEvidence(command);
            commands.Add(command);
            ids.Add(pair.Key, pair.Value.CommandId);
        }
        var plan = new JsonObject
        {
            ["ContractVersion"] = Arch7bCoreRdsSecretBrokerContracts.Plan,
            ["RunId"] = runId,
            ["OwnerId"] = ownerId,
            ["FutureAuthorizationId"] = futureId,
            ["CoreCommit"] = staticAuthority.CoreCommit,
            ["CoreTree"] = staticAuthority.CoreTree,
            ["IntradayCommit"] = template.IntradayCommit,
            ["IntradayTree"] = template.IntradayTree,
            ["IntradayRuntimeInventorySha256"] = staticAuthority.IntradayRuntimeInventorySha256,
            ["TargetProfileId"] = staticAuthority.TargetProfileId,
            ["TargetFingerprint"] = staticAuthority.TargetFingerprint,
            ["AccountId"] = staticAuthority.AccountId,
            ["SecretArn"] = staticAuthority.SecretArn,
            ["ExpectedRead1VersionId"] = read1VersionId,
            ["EnvironmentVariableName"] = Arch7bCoreRdsSecretBrokerContracts.SecretVariable,
            ["ProtocolVersion"] = Arch7bCoreRdsSecretBrokerContracts.Protocol,
            ["MaximumCommands"] = 4,
            ["MaximumRetries"] = 0,
            ["Commands"] = commands
        };
        AddEvidence(plan);
        var planPath = Path.Combine(brokerRoot, "arch7b-rds-secret-child-command-plan.json");
        var configPath = Path.Combine(brokerRoot, "arch7b-rds-secret-child-command-broker-config.json");
        var config = new JsonObject
        {
            ["ContractVersion"] = "arch7b_rds_secret_child_command_broker_config_v1",
            ["CoreCommit"] = staticAuthority.CoreCommit,
            ["CoreTree"] = staticAuthority.CoreTree,
            ["IntradayCommit"] = template.IntradayCommit,
            ["IntradayTree"] = template.IntradayTree,
            ["IntradayRuntimeInventorySha256"] = staticAuthority.IntradayRuntimeInventorySha256,
            ["IntradayBinarySha256"] = staticAuthority.IntradayBinarySha256,
            ["TargetFingerprint"] = staticAuthority.TargetFingerprint,
            ["SecretArn"] = staticAuthority.SecretArn
        };
        await WriteCreateNewAsync(planPath, plan, cancellationToken).ConfigureAwait(false);
        await WriteCreateNewAsync(configPath, config, cancellationToken).ConfigureAwait(false);
        var planSha = ShaFile(planPath);
        var configSha = ShaFile(configPath);
        var authority = new Arch7bCoreRdsSecretBrokerProcessAuthority(
            Arch7bCoreRdsSecretBrokerContracts.ProcessAuthority,
            staticAuthority.CoreCommit, staticAuthority.CoreTree,
            Path.GetFullPath(staticAuthority.CoreBrokerModulePath), staticAuthority.CoreBrokerModuleSha256,
            Path.GetFullPath(staticAuthority.CoreBrokerCliPath), staticAuthority.CoreBrokerCliSha256,
            Path.GetFullPath(staticAuthority.NodeExecutablePath), staticAuthority.NodeExecutableSha256,
            planPath, planSha, configPath, configSha, Arch7bCoreRdsSecretBrokerContracts.Protocol,
            Arch7bCoreRdsSecretBrokerContracts.Broker, Arch7bCoreRdsSecretBrokerContracts.Ready,
            Arch7bCoreRdsSecretBrokerContracts.TerminalCleanup, runId, ownerId, futureId,
            read1VersionId, staticAuthority.TargetProfileId, staticAuthority.TargetFingerprint,
            staticAuthority.AccountId, true, string.Empty);
        authority = authority with { EvidenceSha256 = AuthorityEvidence(authority) };
        return new(authority, ids);
    }

    private static Arch7bFileAuthority RequireAuthority(Arch7bOneShotLivePlanTemplate template,
        string authorityId)
    {
        if (!template.FileAuthorities.TryGetValue(authorityId, out var authority) ||
            !Path.IsPathFullyQualified(authority.Path) ||
            !File.Exists(authority.Path) && !Directory.Exists(authority.Path) ||
            File.Exists(authority.Path) && ShaFile(authority.Path) != authority.Sha256 ||
            !Arch7bOneShotContracts.IsSha256(authority.Sha256))
            Arch7bCoreRdsSecretBrokerStaticAuthority.Fail(
                Arch7bCoreRdsSecretBrokerBlockers.AuthorityInvalid, authorityId);
        return authority;
    }

    private static JsonObject ArgumentSchema(int index, Arch7bCommandTemplateArgument argument)
    {
        var placeholder = Arch7bTypedPlaceholder.Parse(argument.Value);
        var kind = placeholder is null ? "EXACT_LITERAL" : argument.ValueKind switch
        {
            Arch7bPlaceholderValueKind.String => "STRING",
            Arch7bPlaceholderValueKind.Sha256 => "SHA256",
            Arch7bPlaceholderValueKind.AbsolutePath when argument.MustBeInsideRunRoot => "RUN_ROOT_PATH",
            Arch7bPlaceholderValueKind.AbsolutePath => "ABSOLUTE_PATH",
            Arch7bPlaceholderValueKind.UtcTimestamp => "UTC_TIMESTAMP",
            Arch7bPlaceholderValueKind.Integer => "INTEGER",
            _ => "EXACT_LITERAL"
        };
        var schema = new JsonObject
        {
            ["ArgumentIndex"] = index,
            ["Kind"] = kind,
            ["ExpectedLiteral"] = kind == "EXACT_LITERAL" ? argument.Value : null,
            ["AllowedEnum"] = null,
            ["RequiredProducerStage"] = argument.ExpectedProducerStage,
            ["MustBeInsideRunRoot"] = argument.MustBeInsideRunRoot,
            ["MaximumLength"] = 16_384
        };
        AddEvidence(schema);
        return schema;
    }

    private static string FactValue(Arch7bOneShotFact fact)
    {
        using var document = JsonDocument.Parse(fact.ValueJson);
        return document.RootElement.GetProperty("value").GetString()
            ?? throw new Arch7bQualificationException(Arch7bV2Blockers.FactInvalid, fact.FactType);
    }

    private static void AddEvidence(JsonObject value) =>
        value["EvidenceSha256"] = Arch7bCanonicalJson.Sha256(value);

    private static string AuthorityEvidence(Arch7bCoreRdsSecretBrokerProcessAuthority value) =>
        Arch7bOneShotContracts.Sha256(string.Join('\n', value.ContractVersion, value.CoreCommit,
            value.CoreTree, value.CoreBrokerModulePath, value.CoreBrokerModuleSha256,
            value.CoreBrokerCliPath, value.CoreBrokerCliSha256, value.NodeExecutablePath,
            value.NodeExecutableSha256, value.CommandPlanPath, value.CommandPlanSha256,
            value.ConfigPath, value.ConfigSha256, value.ProtocolVersion, value.BrokerContractVersion,
            value.ExpectedReadyContract, value.ExpectedTerminalContract, value.RunId, value.OwnerId,
            value.FutureAuthorizationId, value.Read1VersionId, value.TargetProfileId,
            value.TargetFingerprint, value.AccountId, value.NoOrder));

    private static async Task WriteCreateNewAsync(string path, JsonObject value,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(Arch7bCanonicalJson.Serialize(value) + "\n");
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ShaFile(string path) => Convert.ToHexStringLower(
        SHA256.HashData(File.ReadAllBytes(path)));
}

public sealed class Arch7bCoreRdsSecretBrokerClient : IArch7bCoreRdsSecretBrokerClient
{
    private readonly Arch7bCoreRdsSecretBrokerStaticAuthority staticAuthority;
    private readonly Arch7bCoreRdsSecretBrokerPlanMaterializer materializer;
    private readonly Arch7bRealCommandAdapterRegistry adapters;
    private readonly TimeSpan frameTimeout;
    private readonly SemaphoreSlim operation = new(1, 1);
    private Process? process;
    private StreamWriter? input;
    private StreamReader? output;
    private Task<string>? stderrTask;
    private JsonObject? ready;
    private bool terminalTransition;
    private bool terminal;

    public Arch7bCoreRdsSecretBrokerClient(Arch7bCoreRdsSecretBrokerStaticAuthority staticAuthority,
        Arch7bRealCommandAdapterRegistry adapters, TimeSpan? frameTimeout = null)
    {
        this.staticAuthority = staticAuthority;
        materializer = new(staticAuthority);
        this.adapters = adapters;
        this.frameTimeout = frameTimeout ?? TimeSpan.FromSeconds(30);
    }

    public bool IsRunning => process is { HasExited: false };
    public string Phase { get; private set; } = "STARTING";
    public int LastSequence { get; private set; }
    public string PreviousResponseSha256 { get; private set; } =
        Arch7bCoreRdsSecretBrokerContracts.InitialResponseSha256;
    public string ExpectedRead1VersionId => staticAuthority.Read1VersionId;
    public Arch7bCoreBrokerMaterialization? Materialization { get; private set; }

    public async Task<Arch7bCoreBrokerMaterialization> MaterializeAndStartAsync(
        Arch7bOneShotLivePlanTemplate template, Arch7bOneShotLiveFactStore facts,
        string runRoot, string read1VersionId, CancellationToken cancellationToken = default)
    {
        await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (process is not null || Materialization is not null)
                Fail(Arch7bCoreRdsSecretBrokerBlockers.StateInvalid, "start-duplicate");
            Materialization = await materializer.MaterializeAsync(template, facts, runRoot,
                read1VersionId, cancellationToken).ConfigureAwait(false);
            var authority = Materialization.Authority;
            ValidateAuthorityReadback(authority);
            var outputRoot = Path.GetFullPath(runRoot);
            var mode = staticAuthority.QualificationOnly
                ? "qualify-rds-secret-child-command-broker"
                : "prepare-rds-secret-lease-and-serve-authorized-children";
            var start = new ProcessStartInfo
            {
                FileName = authority.NodeExecutablePath,
                WorkingDirectory = Path.GetDirectoryName(authority.CoreBrokerCliPath)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false, true),
                StandardOutputEncoding = new UTF8Encoding(false, true),
                StandardErrorEncoding = new UTF8Encoding(false, true)
            };
            foreach (var value in Arguments(mode, authority, outputRoot)) start.ArgumentList.Add(value);
            process = Process.Start(start) ?? throw new Arch7bQualificationException(
                Arch7bCoreRdsSecretBrokerBlockers.StateInvalid, "process-start");
            input = process.StandardInput;
            input.AutoFlush = true;
            input.NewLine = "\n";
            output = process.StandardOutput;
            stderrTask = ReadBoundedAsync(process.StandardError,
                Arch7bCoreRdsSecretBrokerContracts.MaximumFrameBytes, cancellationToken);
            return Materialization;
        }
        finally
        {
            operation.Release();
        }
    }

    public async Task<JsonObject> ReadReadyAsync(CancellationToken cancellationToken = default)
    {
        await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ready is not null || !IsRunning) Fail(Arch7bCoreRdsSecretBrokerBlockers.StateInvalid, "ready");
            ready = await ReadFrameAsync(cancellationToken).ConfigureAwait(false);
            ValidateEvidence(ready, []);
            var authority = RequiredAuthority();
            RequireString(ready, "ContractVersion", authority.ExpectedReadyContract);
            RequireString(ready, "Control", "BROKER_READY");
            RequireString(ready, "RunId", authority.RunId);
            RequireString(ready, "OwnerId", authority.OwnerId);
            RequireString(ready, "FutureAuthorizationId", authority.FutureAuthorizationId);
            RequireString(ready, "CommandPlanSha256", authority.CommandPlanSha256);
            RequireString(ready, "SecretVersionId", authority.Read1VersionId);
            if (RequiredInt(ready, "MaximumCommands") != 4 || RequiredInt(ready, "MaximumRetries") != 0)
                Fail(Arch7bCoreRdsSecretBrokerBlockers.FrameUnexpected, "ready-budget");
            Phase = "PRE_BRACKET";
            return PersistableClone(ready, []);
        }
        finally { operation.Release(); }
    }

    public Task<JsonObject> MarkBracketStartedAsync(string bracketEvidenceSha256,
        CancellationToken cancellationToken = default) => SendControlAsync(new JsonObject
        {
            ["ContractVersion"] = Arch7bCoreRdsSecretBrokerContracts.Protocol,
            ["Control"] = "MARK_BRACKET_STARTED",
            ["RunId"] = RequiredAuthority().RunId,
            ["OwnerId"] = RequiredAuthority().OwnerId,
            ["FutureAuthorizationId"] = RequiredAuthority().FutureAuthorizationId,
            ["BracketT0EvidenceSha256"] = bracketEvidenceSha256,
            ["NoOrder"] = true
        }, "BRACKET_STARTED", "POST_BRACKET", cancellationToken);

    public async Task<Arch7bCoreBrokerCommandResult> ExecuteAsync(
        Arch7bOneShotMaterializedCommand command, string inputFactEvidenceSha256, string runRoot,
        CancellationToken cancellationToken = default)
    {
        await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureRunning();
            Arch7bCoreRdsSecretBrokerStaticAuthority.RequireSha(inputFactEvidenceSha256);
            var authority = RequiredAuthority();
            var commandId = Materialization!.CommandIdsByStage.GetValueOrDefault(command.StageId)
                ?? throw new Arch7bQualificationException(
                    Arch7bCoreRdsSecretBrokerBlockers.PlanInvalid, command.StageId);
            var expectedSequence = LastSequence + 1;
            var phase = command.StageId == "REPORTING" ? "TERMINAL_READONLY" : "POST_BRACKET";
            if (phase == "TERMINAL_READONLY" && !terminalTransition)
                Fail(Arch7bCoreRdsSecretBrokerBlockers.StateInvalid, "terminal-transition-required");
            var request = new JsonObject
            {
                ["ContractVersion"] = Arch7bCoreRdsSecretBrokerContracts.Request,
                ["Control"] = "EXECUTE_AUTHORIZED_CHILD",
                ["RequestId"] = Guid.NewGuid().ToString("D"),
                ["SequenceNumber"] = expectedSequence,
                ["RunId"] = authority.RunId,
                ["OwnerId"] = authority.OwnerId,
                ["FutureAuthorizationId"] = authority.FutureAuthorizationId,
                ["CommandPlanSha256"] = authority.CommandPlanSha256,
                ["CommandId"] = commandId,
                ["StageId"] = command.StageId,
                ["Phase"] = phase,
                ["ArgumentValues"] = new JsonArray(command.ArgumentList
                    .Select(value => JsonValue.Create(value)).ToArray()),
                ["InputFactEvidenceSha256"] = inputFactEvidenceSha256,
                ["PreviousResponseEvidenceSha256"] = PreviousResponseSha256,
                ["NoOrder"] = true
            };
            AddEvidence(request);
            await WriteFrameAsync(request, cancellationToken).ConfigureAwait(false);
            var response = await ReadFrameAsync(cancellationToken).ConfigureAwait(false);
            ValidateEvidence(response, ["NativeStdoutPayload", "NativeStderrPayload"]);
            RequireString(response, "ContractVersion", Arch7bCoreRdsSecretBrokerContracts.Response);
            RequireString(response, "Control", "AUTHORIZED_CHILD_RESPONSE");
            RequireString(response, "CommandId", commandId);
            RequireString(response, "StageId", command.StageId);
            if (RequiredInt(response, "SequenceNumber") != expectedSequence)
                Fail(Arch7bCoreRdsSecretBrokerBlockers.SequenceMismatch, commandId);
            RequireString(response, "PreviousResponseEvidenceSha256", PreviousResponseSha256);
            var payloadNode = response["NativeStdoutPayload"];
            if (payloadNode is null || payloadNode.GetValueKind() != JsonValueKind.String)
                Fail(Arch7bCoreRdsSecretBrokerBlockers.FrameUnexpected, "native-payload");
            var payload = payloadNode.GetValue<string>();
            var persistable = PersistableClone(response, ["NativeStdoutPayload", "NativeStderrPayload"]);
            var persistableJson = Arch7bCanonicalJson.Serialize(persistable);
            if (persistableJson.Contains("NativeStdoutPayload", StringComparison.Ordinal) ||
                persistableJson.Contains(payload, StringComparison.Ordinal))
                Fail(Arch7bCoreRdsSecretBrokerBlockers.PayloadPersistence);
            Arch7bNormalizedChildResult adapted;
            try
            {
                adapted = await adapters.Require(command.AdapterId).AdaptAsync(payload, command,
                    runRoot, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                payload = string.Empty;
                response.Remove("NativeStdoutPayload");
                response.Remove("NativeStderrPayload");
            }
            var evidenceSha = RequireString(persistable, "EvidenceSha256");
            var value = new Arch7bCoreBrokerPersistableResponse(
                Arch7bCoreRdsSecretBrokerContracts.Response, commandId, command.StageId,
                expectedSequence, phase, RequireString(persistable, "ChildExitClassification"),
                PreviousResponseSha256, RequireString(persistable, "NativeOutputContract"),
                RequireString(persistable, "NativeOutputSha256"),
                RequiredInt(persistable, "NativeOutputByteCount"), evidenceSha, persistableJson);
            LastSequence = expectedSequence;
            PreviousResponseSha256 = evidenceSha;
            if (phase == "TERMINAL_READONLY") Phase = "SHUTTING_DOWN";
            return new(value, adapted);
        }
        finally { operation.Release(); }
    }

    public async Task<JsonObject> MarkTerminalReadonlyAsync(
        string economicRevisionBindingEvidenceSha256, string arch7aQualificationEvidenceSha256,
        string reportingInputAuthoritySha256, CancellationToken cancellationToken = default)
    {
        await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureRunning();
            if (terminalTransition || Phase != "POST_BRACKET" || LastSequence != 3)
                Fail(Arch7bCoreRdsSecretBrokerBlockers.StateInvalid, "terminal-transition");
            foreach (var sha in new[] { economicRevisionBindingEvidenceSha256,
                         arch7aQualificationEvidenceSha256, reportingInputAuthoritySha256 })
                Arch7bCoreRdsSecretBrokerStaticAuthority.RequireSha(sha);
            var authority = RequiredAuthority();
            var request = new JsonObject
            {
                ["ContractVersion"] = Arch7bCoreRdsSecretBrokerContracts.TerminalTransition,
                ["Control"] = "MARK_TERMINAL_READONLY",
                ["RunId"] = authority.RunId,
                ["OwnerId"] = authority.OwnerId,
                ["FutureAuthorizationId"] = authority.FutureAuthorizationId,
                ["CommandPlanSha256"] = authority.CommandPlanSha256,
                ["LastCompletedSequenceNumber"] = LastSequence,
                ["LastCompletedCommandId"] = "arch7a-qualify-shadow",
                ["LastResponseEvidenceSha256"] = PreviousResponseSha256,
                ["EconomicRevisionBindingEvidenceSha256"] = economicRevisionBindingEvidenceSha256,
                ["Arch7aQualificationEvidenceSha256"] = arch7aQualificationEvidenceSha256,
                ["ReportingInputAuthoritySha256"] = reportingInputAuthoritySha256,
                ["NoOrder"] = true
            };
            AddEvidence(request);
            await WriteFrameAsync(request, cancellationToken).ConfigureAwait(false);
            var response = await ReadFrameAsync(cancellationToken).ConfigureAwait(false);
            ValidateEvidence(response, []);
            RequireString(response, "Control", "TERMINAL_READONLY_READY");
            RequireString(response, "LastResponseEvidenceSha256", PreviousResponseSha256);
            PreviousResponseSha256 = RequireString(response, "EvidenceSha256");
            terminalTransition = true;
            Phase = "TERMINAL_READONLY";
            return PersistableClone(response, []);
        }
        finally { operation.Release(); }
    }

    public async Task<JsonObject> ShutdownAsync(CancellationToken cancellationToken = default)
    {
        await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (terminal) return new JsonObject();
            EnsureRunning();
            var authority = RequiredAuthority();
            var request = new JsonObject
            {
                ["ContractVersion"] = Arch7bCoreRdsSecretBrokerContracts.Protocol,
                ["Control"] = "SHUTDOWN",
                ["RunId"] = authority.RunId,
                ["OwnerId"] = authority.OwnerId,
                ["NoOrder"] = true
            };
            AddEvidence(request);
            await WriteFrameAsync(request, cancellationToken).ConfigureAwait(false);
            var response = await ReadFrameAsync(cancellationToken).ConfigureAwait(false);
            ValidateEvidence(response, []);
            RequireString(response, "BrokerContract", Arch7bCoreRdsSecretBrokerContracts.Broker);
            RequireString(response, "CleanupContract", authority.ExpectedTerminalContract);
            RequireString(response, "TerminalResult", "SHUTDOWN");
            RequireString(response, "FinalState", "TERMINAL_SUCCESS");
            if (RequiredInt(response, "SecretReadCount") != 1 ||
                RequiredInt(response, "PostBracketSecretReadCount") != 0 ||
                RequiredInt(response, "CommandsAuthorized") != 4 ||
                RequiredInt(response, "RetryCount") != 0 ||
                RequiredInt(response, "CommandsExecuted") != 4 ||
                RequiredInt(response, "CommandsExitedZero") != 4 ||
                RequiredInt(response, "LastSequence") != 4 ||
                RequiredInt(response, "ChildSecretLeakCount") != 0 ||
                RequiredInt(response, "ChildOutputOverflowCount") != 0 ||
                RequiredInt(response, "ChildTimeoutCount") != 0 ||
                RequiredInt(response, "LeaseReleaseCount") != 1 ||
                response["BracketStarted"]?.GetValue<bool>() != true ||
                RequiredInt(response, "ActiveChildCount") != 0 ||
                RequiredInt(response, "ResidualProcessCount") != 0 ||
                response["ReferenceReleaseCompleted"]?.GetValue<bool>() != true ||
                response["AwsClientDestroyed"]?.GetValue<bool>() != true)
                Fail(Arch7bCoreRdsSecretBrokerBlockers.CleanupIncomplete);
            input!.Close();
            await process!.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var stderr = stderrTask is null ? string.Empty : await stderrTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
                Fail(Arch7bCoreRdsSecretBrokerBlockers.CleanupIncomplete,
                    Arch7bOneShotContracts.Sha256(stderr));
            terminal = true;
            Phase = RequireString(response, "FinalState");
            return PersistableClone(response, []);
        }
        finally { operation.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (process is { HasExited: false })
        {
            try { await ShutdownAsync().ConfigureAwait(false); }
            catch
            {
                process.Kill(true);
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        input?.Dispose();
        output?.Dispose();
        process?.Dispose();
        process = null;
        operation.Dispose();
    }

    private async Task<JsonObject> SendControlAsync(JsonObject request, string expectedControl,
        string nextPhase, CancellationToken cancellationToken)
    {
        await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureRunning();
            AddEvidence(request);
            await WriteFrameAsync(request, cancellationToken).ConfigureAwait(false);
            var response = await ReadFrameAsync(cancellationToken).ConfigureAwait(false);
            ValidateEvidence(response, []);
            RequireString(response, "Control", expectedControl);
            Phase = nextPhase;
            return PersistableClone(response, []);
        }
        finally { operation.Release(); }
    }

    private async Task WriteFrameAsync(JsonObject value, CancellationToken cancellationToken)
    {
        var frame = Arch7bCanonicalJson.Serialize(value);
        if (Encoding.UTF8.GetByteCount(frame) > Arch7bCoreRdsSecretBrokerContracts.MaximumFrameBytes)
            Fail(Arch7bCoreRdsSecretBrokerBlockers.FrameOversized);
        await input!.WriteLineAsync(frame.AsMemory(), cancellationToken).ConfigureAwait(false);
        await input.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<JsonObject> ReadFrameAsync(CancellationToken cancellationToken)
    {
        string? line;
        try
        {
            line = await output!.ReadLineAsync(cancellationToken).AsTask()
                .WaitAsync(frameTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Fail(Arch7bCoreRdsSecretBrokerBlockers.FrameTimeout);
            throw;
        }
        catch (DecoderFallbackException)
        {
            Fail(Arch7bCoreRdsSecretBrokerBlockers.FrameInvalid, "utf8");
            throw;
        }
        if (line is null) Fail(Arch7bCoreRdsSecretBrokerBlockers.PrematureEof);
        if (Encoding.UTF8.GetByteCount(line!) > Arch7bCoreRdsSecretBrokerContracts.MaximumFrameBytes)
            Fail(Arch7bCoreRdsSecretBrokerBlockers.FrameOversized);
        JsonObject value;
        try
        {
            value = JsonNode.Parse(line!, documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64
            }) as JsonObject ?? throw new JsonException();
        }
        catch (JsonException)
        {
            Fail(Arch7bCoreRdsSecretBrokerBlockers.FrameInvalid);
            throw;
        }
        if (Arch7bCanonicalJson.Serialize(value) != line)
            Fail(Arch7bCoreRdsSecretBrokerBlockers.FrameNonCanonical);
        return value;
    }

    private static void ValidateEvidence(JsonObject value, IReadOnlyCollection<string> transientFields)
    {
        var expected = RequireString(value, "EvidenceSha256");
        var core = PersistableClone(value, transientFields.Append("EvidenceSha256"));
        if (Arch7bCanonicalJson.Sha256(core) != expected)
            Fail(Arch7bCoreRdsSecretBrokerBlockers.FrameInvalid, "evidence");
    }

    private static JsonObject PersistableClone(JsonObject value,
        IEnumerable<string> removedFields)
    {
        var clone = JsonNode.Parse(value.ToJsonString())!.AsObject();
        foreach (var field in removedFields) clone.Remove(field);
        return clone;
    }

    private static void AddEvidence(JsonObject value) =>
        value["EvidenceSha256"] = Arch7bCanonicalJson.Sha256(value);

    private Arch7bCoreRdsSecretBrokerProcessAuthority RequiredAuthority() =>
        Materialization?.Authority ?? throw new Arch7bQualificationException(
            Arch7bCoreRdsSecretBrokerBlockers.StateInvalid, "authority");

    private void EnsureRunning()
    {
        if (!IsRunning || output is null || input is null)
            Fail(Arch7bCoreRdsSecretBrokerBlockers.StateInvalid, "not-running");
    }

    private static void ValidateAuthorityReadback(Arch7bCoreRdsSecretBrokerProcessAuthority value)
    {
        if (value.ContractVersion != Arch7bCoreRdsSecretBrokerContracts.ProcessAuthority ||
            value.ProtocolVersion != Arch7bCoreRdsSecretBrokerContracts.Protocol || !value.NoOrder)
            Fail(Arch7bCoreRdsSecretBrokerBlockers.AuthorityInvalid);
        foreach (var pair in new[]
        {
            (value.CoreBrokerModulePath, value.CoreBrokerModuleSha256),
            (value.CoreBrokerCliPath, value.CoreBrokerCliSha256),
            (value.NodeExecutablePath, value.NodeExecutableSha256),
            (value.CommandPlanPath, value.CommandPlanSha256),
            (value.ConfigPath, value.ConfigSha256)
        })
            if (!File.Exists(pair.Item1) || Convert.ToHexStringLower(
                    SHA256.HashData(File.ReadAllBytes(pair.Item1))) != pair.Item2)
                Fail(Arch7bCoreRdsSecretBrokerBlockers.AuthorityInvalid, pair.Item1);
    }

    private static IEnumerable<string> Arguments(string mode,
        Arch7bCoreRdsSecretBrokerProcessAuthority value, string outputRoot)
    {
        yield return value.CoreBrokerCliPath;
        yield return mode;
        yield return "--config"; yield return value.ConfigPath;
        yield return "--command-plan"; yield return value.CommandPlanPath;
        yield return "--expected-command-plan-sha256"; yield return value.CommandPlanSha256;
        yield return "--expected-rds-version-id-read-1"; yield return value.Read1VersionId;
        yield return "--run-id"; yield return value.RunId;
        yield return "--owner-id"; yield return value.OwnerId;
        yield return "--future-authorization-id"; yield return value.FutureAuthorizationId;
        yield return "--target-profile-id"; yield return value.TargetProfileId;
        yield return "--account-id"; yield return value.AccountId;
        yield return "--no-order"; yield return "true";
        yield return "--output-root"; yield return outputRoot;
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int maximum,
        CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var result = new StringBuilder();
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            if (result.Length + count > maximum)
                Fail(Arch7bCoreRdsSecretBrokerBlockers.FrameOversized, "stderr");
            result.Append(buffer, 0, count);
        }
        return result.ToString();
    }

    private static string RequireString(JsonObject value, string name, string? expected = null)
    {
        var node = value[name] as JsonValue ?? throw new Arch7bQualificationException(
            Arch7bCoreRdsSecretBrokerBlockers.FrameUnexpected, name);
        if (!node.TryGetValue<string>(out var result) || string.IsNullOrWhiteSpace(result) ||
            expected is not null && result != expected)
            throw new Arch7bQualificationException(
                Arch7bCoreRdsSecretBrokerBlockers.FrameUnexpected, name);
        return result;
    }

    private static int RequiredInt(JsonObject value, string name)
    {
        var node = value[name] as JsonValue ?? throw new Arch7bQualificationException(
            Arch7bCoreRdsSecretBrokerBlockers.FrameUnexpected, name);
        if (!node.TryGetValue<int>(out var result))
            throw new Arch7bQualificationException(
                Arch7bCoreRdsSecretBrokerBlockers.FrameUnexpected, name);
        return result;
    }

    [DoesNotReturn]
    private static void Fail(string blocker, string? detail = null) =>
        Arch7bCoreRdsSecretBrokerStaticAuthority.Fail(blocker, detail);
}

public static class Arch7bCanonicalJson
{
    private static readonly JsonSerializerOptions NodeCompatibleOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    public static string Serialize(JsonNode value) => SerializeElement(
        JsonDocument.Parse(value.ToJsonString()).RootElement);

    public static string Sha256(JsonNode value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(value))));

    private static string SerializeElement(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(',', value.EnumerateObject()
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .Select(item => JsonSerializer.Serialize(item.Name, NodeCompatibleOptions) + ":" + SerializeElement(item.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(',', value.EnumerateArray().Select(SerializeElement)) + "]",
        JsonValueKind.String => JsonSerializer.Serialize(value.GetString(), NodeCompatibleOptions),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        _ => throw new InvalidDataException(Arch7bCoreRdsSecretBrokerBlockers.FrameInvalid)
    };
}
