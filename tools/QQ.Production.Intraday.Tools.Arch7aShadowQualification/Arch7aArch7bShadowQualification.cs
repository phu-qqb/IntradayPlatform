using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tools.Arch7aShadowQualification;

public sealed class Arch7aArch7bShadowQualificationArguments
{
    public const string ContractVersion = "arch7a_arch7b_rds_shadow_qualification_v1";
    public const string ModeName = "qualify-shadow";
    public const string TargetProfile = "ARCH7B_RDS_TEST";
    public const string TargetDatabase = "qq_pms_shadow_arch7b_test";
    public const string TargetSchema = "pms_shadow";
    public const string TargetEnvironment = "TEST";
    public const string TargetFingerprint =
        "72fa569ee28e4dec6272db0d69c7594b2be8853e9607dff3e78066378a0b5ee4";
    public const string RootCertificateSha256 =
        "17976078e32d253e3d77a464933d96804357a7d61206e0ecdd38145a64f67527";
    public const string CanonicalRootCertificatePath =
        @"D:\QQFund\ARCH7B\primary-direct-vpc-20260729T1115Z\amazon-rds-eu-west-2-root-ca-rsa2048-g1.pem";
    public const string CredentialEnvironmentVariable =
        "QQ_ARCH7B_POSITION_IMPORT_FAST_PATH";
    public const string CredentialReference =
        "env:" + CredentialEnvironmentVariable;
    public const string DatabaseRole = "qq_arch7b_position_importer";

    private static readonly HashSet<string> AllowedNames = new(StringComparer.Ordinal)
    {
        "--mode",
        "--economic-revision-id",
        "--slot-id",
        "--source-session-id",
        "--position-market-revision-binding-path",
        "--expected-position-market-revision-binding-sha256",
        "--target-profile-id",
        "--expected-environment",
        "--expected-database",
        "--expected-schema",
        "--expected-postgres-major",
        "--require-tls",
        "--allow-loopback",
        "--root-certificate",
        "--expected-root-certificate-sha256",
        "--expected-target-fingerprint",
        "--repository-commit",
        "--output-directory",
        "--connection-secret-reference",
        "--role",
        "--no-order",
        "--validate-command-contract-only"
    };

    private readonly IReadOnlyDictionary<string, string> values;

    private Arch7aArch7bShadowQualificationArguments(
        IReadOnlyDictionary<string, string> values)
    {
        this.values = values;
        Mode = Required("--mode");
        EconomicRevisionId = RequiredGuid("--economic-revision-id",
            "ARCH7A_ARCH7B_ECONOMIC_REVISION_REQUIRED");
        SlotId = Required("--slot-id");
        SourceSessionId = Required("--source-session-id");
        PositionMarketRevisionBindingPath =
            RequiredAbsolutePath("--position-market-revision-binding-path");
        ExpectedPositionMarketRevisionBindingSha256 =
            RequiredSha256("--expected-position-market-revision-binding-sha256");
        TargetProfileId = Required("--target-profile-id");
        ExpectedEnvironment = Required("--expected-environment");
        ExpectedDatabase = Required("--expected-database");
        ExpectedSchema = Required("--expected-schema");
        ExpectedPostgresMajor = RequiredInteger("--expected-postgres-major");
        RequireTls = RequiredBoolean("--require-tls");
        AllowLoopback = RequiredBoolean("--allow-loopback");
        RootCertificatePath = RequiredAbsolutePath("--root-certificate");
        ExpectedRootCertificateSha256 =
            RequiredSha256("--expected-root-certificate-sha256");
        ExpectedTargetFingerprint =
            RequiredSha256("--expected-target-fingerprint");
        RepositoryCommit = RequiredCommit("--repository-commit");
        OutputDirectory = RequiredAbsolutePath("--output-directory");
        ConnectionSecretReference = Required("--connection-secret-reference");
        Role = Required("--role");
        NoOrder = RequiredBoolean("--no-order");
        ValidateCommandContractOnly =
            RequiredBoolean("--validate-command-contract-only");
        ValidateTargetContract();
    }

    public string Mode { get; }
    public Guid EconomicRevisionId { get; }
    public string SlotId { get; }
    public string SourceSessionId { get; }
    public string PositionMarketRevisionBindingPath { get; }
    public string ExpectedPositionMarketRevisionBindingSha256 { get; }
    public string TargetProfileId { get; }
    public string ExpectedEnvironment { get; }
    public string ExpectedDatabase { get; }
    public string ExpectedSchema { get; }
    public int ExpectedPostgresMajor { get; }
    public bool RequireTls { get; }
    public bool AllowLoopback { get; }
    public string RootCertificatePath { get; }
    public string ExpectedRootCertificateSha256 { get; }
    public string ExpectedTargetFingerprint { get; }
    public string RepositoryCommit { get; }
    public string OutputDirectory { get; }
    public string ConnectionSecretReference { get; }
    public string Role { get; }
    public bool NoOrder { get; }
    public bool ValidateCommandContractOnly { get; }

    public static bool IsQualifyShadow(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index + 1 < arguments.Count; index += 2)
            if (arguments[index] == "--mode")
                return arguments[index + 1] == ModeName;
        return false;
    }

    public static Arch7aArch7bShadowQualificationArguments Parse(
        IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0 || arguments.Count % 2 != 0)
            throw new InvalidDataException("ARCH7A_ARCH7B_ARGUMENT_VALUE_MISSING");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Count; index += 2)
        {
            var name = arguments[index];
            if (!AllowedNames.Contains(name))
                throw new InvalidDataException($"ARCH7A_ARCH7B_UNKNOWN_ARGUMENT:{name}");
            if (!values.TryAdd(name, arguments[index + 1]))
                throw new InvalidDataException($"ARCH7A_ARCH7B_DUPLICATE_ARGUMENT:{name}");
        }
        return new(values);
    }

    public Arch7bPostgreSqlPinnedSession BuildRuntime()
    {
        if (ValidateCommandContractOnly)
            throw new InvalidOperationException(
                "ARCH7A_ARCH7B_VALIDATE_ONLY_RUNTIME_FORBIDDEN");
        var password = Environment.GetEnvironmentVariable(
            CredentialEnvironmentVariable);
        Require(!string.IsNullOrWhiteSpace(password),
            "ARCH7A_ARCH7B_PRELOADED_CREDENTIAL_UNAVAILABLE");
        try
        {
            return Arch7bPostgreSqlPinnedSessionFactory.Create(
                Arch7bPostgreSqlPinnedTransportProfile.DirectPrimary,
                Role,
                password!,
                "QQ_ARCH7A_ARCH7B_SHADOW_QUALIFICATION_APPEND_ONLY",
                Arch7bPostgreSqlAccessMode.ApplyAppendOnly,
                RootCertificatePath);
        }
        finally
        {
            password = string.Empty;
        }
    }

    private void ValidateTargetContract()
    {
        Require(Mode == ModeName, "ARCH7A_ARCH7B_UNKNOWN_MODE");
        Require(TargetProfileId == TargetProfile,
            "ARCH7A_ARCH7B_TARGET_PROFILE_MISMATCH");
        Require(ExpectedEnvironment == TargetEnvironment,
            "ARCH7A_ARCH7B_TARGET_ENVIRONMENT_MISMATCH");
        Require(ExpectedDatabase == TargetDatabase,
            "ARCH7A_ARCH7B_TARGET_DATABASE_MISMATCH");
        Require(ExpectedSchema == TargetSchema,
            "ARCH7A_ARCH7B_TARGET_SCHEMA_MISMATCH");
        Require(ExpectedPostgresMajor == 18,
            "ARCH7A_ARCH7B_POSTGRESQL_MAJOR_MISMATCH");
        Require(RequireTls, "ARCH7A_ARCH7B_TLS_REQUIRED");
        Require(!AllowLoopback, "ARCH7A_ARCH7B_LOOPBACK_FORBIDDEN");
        Require(NoOrder, "ARCH7A_ARCH7B_NO_ORDER_REQUIRED");
        Require(ConnectionSecretReference == CredentialReference,
            "ARCH7A_ARCH7B_CREDENTIAL_REFERENCE_MISMATCH");
        Require(Role == DatabaseRole, "ARCH7A_ARCH7B_DATABASE_ROLE_MISMATCH");
        Require(Path.GetFullPath(RootCertificatePath).Equals(
                Path.GetFullPath(CanonicalRootCertificatePath),
                StringComparison.OrdinalIgnoreCase),
            "ARCH7A_ARCH7B_ROOT_CA_PATH_MISMATCH");
        Require(!Path.GetFileName(RootCertificatePath).Equals(
                "global-bundle.pem", StringComparison.OrdinalIgnoreCase),
            "ARCH7A_ARCH7B_GLOBAL_BUNDLE_FORBIDDEN");
        Require(ExpectedRootCertificateSha256 == RootCertificateSha256,
            "ARCH7A_ARCH7B_ROOT_CA_SHA256_MISMATCH");
        Require(ExpectedTargetFingerprint == TargetFingerprint,
            "ARCH7A_ARCH7B_TARGET_FINGERPRINT_MISMATCH");

        Arch7bPostgreSqlPinnedTransportProfileContract.Validate(
            Arch7bPostgreSqlPinnedTransportProfile.DirectPrimary);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = Arch7bPostgreSqlPinnedTransportProfile.DirectEndpoint,
            Port = 5432,
            Database = ExpectedDatabase,
            Username = Role,
            Password = "validation-only-placeholder",
            SslMode = SslMode.VerifyFull,
            RootCertificate = RootCertificatePath,
            Pooling = false,
            Multiplexing = false,
            Enlist = false
        };
        var target = PmsShadowPostgreSqlTargetContract.Validate(
            builder.ConnectionString,
            new(ExpectedEnvironment, ExpectedDatabase, ExpectedSchema,
                ExpectedPostgresMajor, RequireTls, AllowLoopback, TargetProfileId));
        Require(target.TargetFingerprint == ExpectedTargetFingerprint,
            "ARCH7A_ARCH7B_TARGET_FINGERPRINT_MISMATCH");
    }

    private string Required(string name) =>
        values.GetValueOrDefault(name) ??
        throw new InvalidDataException($"ARCH7A_ARCH7B_ARGUMENT_REQUIRED:{name}");

    private Guid RequiredGuid(string name, string missingCode)
    {
        var raw = Required(name);
        if (!Guid.TryParseExact(raw, "D", out var value) || value == Guid.Empty)
            throw new InvalidDataException(missingCode);
        return value;
    }

    private string RequiredAbsolutePath(string name)
    {
        var raw = Required(name);
        if (!Path.IsPathFullyQualified(raw))
            throw new InvalidDataException($"ARCH7A_ARCH7B_ABSOLUTE_PATH_REQUIRED:{name}");
        return Path.GetFullPath(raw);
    }

    private string RequiredSha256(string name)
    {
        var value = Required(name);
        if (value.Length != 64 || value.Any(character =>
                !char.IsAsciiHexDigit(character) || char.IsUpper(character)))
            throw new InvalidDataException($"ARCH7A_ARCH7B_SHA256_REQUIRED:{name}");
        return value;
    }

    private string RequiredCommit(string name)
    {
        var value = Required(name);
        if (value.Length != 40 || value.Any(character =>
                !char.IsAsciiHexDigit(character) || char.IsUpper(character)))
            throw new InvalidDataException("ARCH7A_ARCH7B_REPOSITORY_COMMIT_INVALID");
        return value;
    }

    private int RequiredInteger(string name)
    {
        if (!int.TryParse(Required(name), out var value))
            throw new InvalidDataException($"ARCH7A_ARCH7B_INTEGER_REQUIRED:{name}");
        return value;
    }

    private bool RequiredBoolean(string name)
    {
        var raw = Required(name);
        if (raw == "true") return true;
        if (raw == "false") return false;
        throw new InvalidDataException($"ARCH7A_ARCH7B_BOOLEAN_REQUIRED:{name}");
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}

public static class Arch7aArch7bMigrationContract
{
    public const string ExactSetMismatch =
        "ARCH7A_ARCH7B_EXPECTED_EXACT_MIGRATION_SET_MISMATCH";
    public const string PendingModelChanges =
        "ARCH7A_ARCH7B_PENDING_MODEL_CHANGES";

    public static void RequireExact(
        IReadOnlyList<string> appliedMigrations,
        bool hasPendingModelChanges)
    {
        if (!appliedMigrations.SequenceEqual(
                PmsShadowStateContract.MigrationIds, StringComparer.Ordinal))
            throw new InvalidDataException(ExactSetMismatch);
        if (hasPendingModelChanges)
            throw new InvalidDataException(PendingModelChanges);
    }
}

public sealed record Arch7aArch7bReadback(
    int TradeIntents,
    int RiskDecisions,
    int ParentOrders,
    int ChildOrders,
    int QualificationRuns,
    string Status,
    bool ExactRevision,
    bool ExactPlan,
    bool NoOrder);

public sealed record Arch7aArch7bShadowQualificationEvidence(
    string Contract,
    string Status,
    string TargetProfile,
    string TargetFingerprint,
    int PostgreSqlMajor,
    string TransportProfile,
    Guid EconomicRevisionId,
    string SlotId,
    string SourceSessionId,
    string PositionMarketRevisionBindingSha256,
    string SessionUser,
    string QualificationRole,
    bool SetRoleAuthorityVerified,
    bool SetRoleVerified,
    bool ResetRoleVerified,
    int TempSchemaBeforeSetRole,
    int TempSchemaAfterSetRole,
    int TempSchemaAfterQualification,
    int TempSchemaAfterResetRole,
    string AmbientPrivilegeStatus,
    string PlanSha256,
    string NettingSha256,
    int TradeIntentCount,
    int RiskDecisionCount,
    int ParentOrderCount,
    int ChildOrderCount,
    string PersistenceResult,
    string ReplayResult,
    Arch7aArch7bReadback Readback,
    bool NoFixLogon,
    bool NoBrokerSend,
    bool NoFill,
    bool NoPositionLedgerEvent,
    int NetworkLedgerCount,
    int PhysicalOpenCount,
    int PhysicalReconnectCount,
    int MaximumConcurrentLeases,
    int CloseCount,
    bool NoSecretValueRecorded,
    string EvidenceSha256);

public static class Arch7aArch7bShadowQualificationRunner
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static async Task RunAsync(
        IReadOnlyList<string> rawArguments,
        CancellationToken cancellationToken = default)
    {
        var arguments = Arch7aArch7bShadowQualificationArguments.Parse(rawArguments);
        if (arguments.ValidateCommandContractOnly)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                contract = "arch7a_arch7b_command_executable_roundtrip_v1",
                status = "ARCH7A_ARCH7B_COMMAND_EXECUTABLE_ROUNDTRIP_QUALIFIED",
                target_profile = arguments.TargetProfileId,
                target_fingerprint = arguments.ExpectedTargetFingerprint,
                secret_reads = 0,
                database_connections = 0,
                database_writes = 0,
                no_order = true
            }, Json));
            return;
        }

        var binding = Arch7bPositionMarketLiveWiring.RequireArch7aRevision(
            arguments.PositionMarketRevisionBindingPath,
            arguments.ExpectedPositionMarketRevisionBindingSha256,
            arguments.EconomicRevisionId);
        var runtime = arguments.BuildRuntime();
        Require(runtime.Target.TargetFingerprint == arguments.ExpectedTargetFingerprint,
            "ARCH7A_ARCH7B_TARGET_FINGERPRINT_MISMATCH");
        var lifecycleDirectory = Path.Combine(
            arguments.OutputDirectory, "pinned-session-lifecycle");
        var supervisor = new Arch7bPostgreSqlPinnedOpenSupervisor(
            runtime, Arch7aArch7bShadowQualificationArguments.ModeName,
            lifecycleDirectory);
        _ = supervisor.StartOpen();
        Exception? primaryFailure = null;
        QualificationResult? result = null;
        Arch7aArch7bRoleScopeEvidence? roleEvidence = null;
        try
        {
            _ = await supervisor.WaitForOpenAsync();
            await using var lease = await runtime.AcquireAsync(cancellationToken);
            var factory = new PinnedContextFactory(
                new Arch7bPostgreSqlPinnedDbContextFactory(runtime), lease);

            await using var roleScope = await Arch7aArch7bRoleScope.EnterAsync(
                lease.Connection, cancellationToken);
            await using (var context =
                         await factory.CreateDbContextAsync(cancellationToken))
            {
                var applied = (await context.Database.GetAppliedMigrationsAsync(
                    cancellationToken)).ToArray();
                Arch7aArch7bMigrationContract.RequireExact(
                    applied, context.Database.HasPendingModelChanges());
            }

            var economicStore =
                new EfPmsShadowIntradayEconomicProjectionStore(factory);
            var projections = await economicStore.ReadAllAsync(cancellationToken);
            var selected =
                EfArch7aPmsExecutionSourceReader.SelectExactQualifyingRevision(
                    projections,
                    arguments.EconomicRevisionId,
                    arguments.SlotId,
                    arguments.SourceSessionId);
            Require(binding.ProjectionRevisionId == selected.ProjectionRevisionId &&
                    binding.ProjectionInputSha256 == selected.InputSha256 &&
                    binding.ProjectionManifestSha256 == selected.ManifestSha256,
                "ARCH7A_ARCH7B_REVISION_BINDING_MISMATCH");

            var slot = new Arch7aExecutionSlot(
                selected.SlotId,
                DateOnly.FromDateTime(selected.SlotEndUtc.UtcDateTime),
                selected.SlotEndUtc,
                selected.SlotStartUtc,
                selected.SlotEndUtc);
            var reader = new EfArch7aPmsExecutionSourceReader(factory);
            var source = await reader.ReadExactRevisionAsync(
                arguments.SourceSessionId,
                slot,
                selected.SlotEndUtc.AddMinutes(1),
                arguments.EconomicRevisionId,
                cancellationToken);
            Require(source.EconomicRevisionId == arguments.EconomicRevisionId,
                "ARCH7A_ARCH7B_ECONOMIC_REVISION_MISMATCH");

            var plan = new Arch7aPmsShadowExecutionPipeline().Build(source);
            Require(plan.Units.Count > 0, "ARCH7A_NO_SHADOW_INTENTS_DERIVED");
            Require(plan.Units.All(NoSendUnit) &&
                    plan.NoFixLogon && plan.NoBrokerSend &&
                    plan.NoAccountApi && plan.NoDatabento &&
                    plan.NoRealAccount && plan.NoFill &&
                    plan.NoPositionLedgerEvent &&
                    plan.NetworkLedger.Count == 0,
                "ARCH7A_NO_EXTERNAL_INVARIANT_FAILED");

            var store = new EfArch7aShadowExecutionStore(factory);
            var persisted = await store.PersistAsync(plan, cancellationToken);
            Require(persisted is Arch7aShadowStoreResult.Persisted or
                    Arch7aShadowStoreResult.AlreadyPersistedIdentical,
                "ARCH7A_ARCH7B_PERSISTENCE_RESULT_INVALID");
            var replay = await store.PersistAsync(plan, cancellationToken);
            Require(replay == Arch7aShadowStoreResult.AlreadyPersistedIdentical,
                "ARCH7A_ARCH7B_REPLAY_NOT_IDENTICAL");
            var readback = await ReadbackAsync(
                factory, selected.ProjectionRevisionId, plan, cancellationToken);
            await roleScope.AssertNoTemporarySchemaAsync(cancellationToken);
            await roleScope.ResetAsync(CancellationToken.None);
            roleEvidence = roleScope.Evidence();
            await roleScope.DisposeAsync();
            result = new(plan, persisted, replay, readback);
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
            throw;
        }
        finally
        {
            _ = await supervisor.CompleteAsync(primaryFailure);
        }

        var session = runtime.Snapshot();
        Require(session.PhysicalOpenCount == 1 &&
                session.PhysicalReconnectCount == 0 &&
                session.BackendProcessId > 0 &&
                session.MaximumConcurrentLeases == 1 &&
                session.CloseCount == 1,
            "ARCH7A_ARCH7B_PINNED_SESSION_INVARIANT_FAILED");
        var completed = result ??
            throw new InvalidOperationException("ARCH7A_ARCH7B_RESULT_MISSING");
        var completedRole = roleEvidence ??
            throw new InvalidOperationException("ARCH7A_ARCH7B_ROLE_EVIDENCE_MISSING");
        Require(completedRole.SetRoleAuthorityVerified &&
                completedRole.SetRoleVerified && completedRole.ResetRoleVerified &&
                completedRole.NoTemporarySchemaCreated,
            "ARCH7A_ARCH7B_ROLE_SCOPE_INCOMPLETE");
        var evidence = new Arch7aArch7bShadowQualificationEvidence(
            Arch7aArch7bShadowQualificationArguments.ContractVersion,
            "ARCH7A_ARCH7B_SHADOW_QUALIFICATION_COMPLETED",
            arguments.TargetProfileId,
            arguments.ExpectedTargetFingerprint,
            arguments.ExpectedPostgresMajor,
            runtime.Profile.Profile,
            arguments.EconomicRevisionId,
            arguments.SlotId,
            arguments.SourceSessionId,
            arguments.ExpectedPositionMarketRevisionBindingSha256,
            completedRole.SessionUser,
            completedRole.QualificationRole,
            completedRole.SetRoleAuthorityVerified,
            completedRole.SetRoleVerified,
            completedRole.ResetRoleVerified,
            completedRole.TempSchemaBeforeSetRole,
            completedRole.TempSchemaAfterSetRole,
            completedRole.TempSchemaAfterQualification,
            completedRole.TempSchemaAfterResetRole,
            "AMBIENT_PUBLIC_PRIVILEGE_ACCEPTED_NOT_DIRECTLY_GRANTED",
            completed.Plan.PlanSha256,
            completed.Plan.Netting.NettingSha256,
            completed.Plan.Units.Count,
            completed.Plan.Units.Count,
            completed.Plan.Units.Count,
            completed.Plan.Units.Count,
            completed.Persistence.ToString(),
            completed.Replay.ToString(),
            completed.Readback,
            completed.Plan.NoFixLogon,
            completed.Plan.NoBrokerSend,
            completed.Plan.NoFill,
            completed.Plan.NoPositionLedgerEvent,
            completed.Plan.NetworkLedger.Count,
            session.PhysicalOpenCount,
            session.PhysicalReconnectCount,
            session.MaximumConcurrentLeases,
            session.CloseCount,
            true,
            string.Empty);
        var written = WriteEvidence(arguments.OutputDirectory, evidence);
        Console.WriteLine(JsonSerializer.Serialize(written, Json));
    }

    public static Arch7aArch7bShadowQualificationEvidence WriteEvidence(
        string outputDirectory,
        Arch7aArch7bShadowQualificationEvidence evidence)
    {
        var canonical = JsonSerializer.SerializeToUtf8Bytes(evidence, Json);
        var sha = Convert.ToHexStringLower(SHA256.HashData(canonical));
        var completed = evidence with { EvidenceSha256 = sha };
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(
            outputDirectory, "arch7a-shadow-qualification.json");
        using var stream = new FileStream(
            outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, completed, Json);
        stream.WriteByte((byte)'\n');
        return completed;
    }

    private static async Task<Arch7aArch7bReadback> ReadbackAsync(
        IDbContextFactory<PmsShadowDbContext> factory,
        Guid revisionId,
        Arch7aShadowExecutionPlan plan,
        CancellationToken cancellationToken)
    {
        await using var context =
            await factory.CreateDbContextAsync(cancellationToken);
        var intents = await context.ShadowTradeIntents.AsNoTracking()
            .Where(value => value.EconomicRevisionId == revisionId)
            .ToArrayAsync(cancellationToken);
        var intentIds = intents.Select(value => value.TradeIntentId).ToArray();
        var risks = await context.ShadowRiskDecisions.AsNoTracking()
            .Where(value => intentIds.Contains(value.TradeIntentId))
            .ToArrayAsync(cancellationToken);
        var parents = await context.ShadowParentOrders.AsNoTracking()
            .Where(value => intentIds.Contains(value.TradeIntentId))
            .ToArrayAsync(cancellationToken);
        var parentIds = parents.Select(value => value.ParentOrderId).ToArray();
        var children = await context.ShadowChildOrders.AsNoTracking()
            .Where(value => parentIds.Contains(value.ParentOrderId))
            .ToArrayAsync(cancellationToken);
        var runs = await context.ShadowExecutionQualificationRuns.AsNoTracking()
            .Where(value => value.EconomicRevisionId == revisionId)
            .ToArrayAsync(cancellationToken);
        var exactCounts = intents.Length == plan.Units.Count &&
                          risks.Length == plan.Units.Count &&
                          parents.Length == plan.Units.Count &&
                          children.Length == plan.Units.Count &&
                          runs.Length == 1;
        Require(exactCounts, "ARCH7A_READBACK_OBJECT_COUNT_MISMATCH");
        var run = runs.Single();
        var exactPlan = run.PlanSha256 == plan.PlanSha256 &&
                        intents.All(value => value.PlanSha256 == plan.PlanSha256) &&
                        risks.All(value => value.PlanSha256 == plan.PlanSha256) &&
                        parents.All(value => value.PlanSha256 == plan.PlanSha256) &&
                        children.All(value => value.PlanSha256 == plan.PlanSha256);
        var noOrder = intents.All(value =>
                              !value.Actionable &&
                              !value.ExecutionAllowed &&
                              !value.BrokerRouteAllowed) &&
                      risks.All(value =>
                              value.Outcome == "BLOCK_NEW_ORDERS" &&
                              value.ReasonCodesJson.Contains(
                                  "BROKER_WORKING_LEAVES_UNOBSERVABLE",
                                  StringComparison.Ordinal) &&
                              value.NoOrderInvariant &&
                              !value.BrokerSendAllowed) &&
                      parents.All(value => !value.RouteAllowed) &&
                      children.All(value => !value.BrokerSendAllowed) &&
                      run.NoFixLogon && run.NoBrokerSend &&
                      run.NoFill && run.NoPositionLedgerEvent;
        Require(run.Status == "COMPLETED" &&
                run.EconomicRevisionId == revisionId &&
                exactPlan && noOrder,
            "ARCH7A_READBACK_QUALIFICATION_RUN_INCOMPLETE");
        return new(
            intents.Length,
            risks.Length,
            parents.Length,
            children.Length,
            runs.Length,
            run.Status,
            true,
            exactPlan,
            noOrder);
    }

    private static bool NoSendUnit(Arch7aShadowExecutionUnit value) =>
        !value.TradeIntent.Actionable &&
        !value.TradeIntent.ExecutionAllowed &&
        !value.TradeIntent.BrokerRouteAllowed &&
        value.RiskDecision.Outcome ==
            Arch7aShadowRiskOutcome.BLOCK_NEW_ORDERS &&
        value.RiskDecision.ReasonCodes.Contains(
            "BROKER_WORKING_LEAVES_UNOBSERVABLE") &&
        !value.RiskDecision.BrokerSendAllowed &&
        !value.ParentOrder.RouteAllowed &&
        !value.ChildOrder.BrokerSendAllowed;

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }

    private sealed record QualificationResult(
        Arch7aShadowExecutionPlan Plan,
        Arch7aShadowStoreResult Persistence,
        Arch7aShadowStoreResult Replay,
        Arch7aArch7bReadback Readback);

    private sealed class PinnedContextFactory(
        Arch7bPostgreSqlPinnedDbContextFactory inner,
        Arch7bPostgreSqlPinnedSessionLease lease)
        : IDbContextFactory<PmsShadowDbContext>
    {
        public PmsShadowDbContext CreateDbContext() =>
            inner.CreateDbContext(lease);

        public Task<PmsShadowDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
