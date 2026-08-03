using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;

namespace QQ.Production.Intraday.Tools.Arch7aShadowQualification;

public static class Arch7aArch7bPrivilegeContract
{
    public const string Version = "arch7a_arch7b_database_privilege_policy_v1";
    public const string QualificationRole = "qq_arch7a_shadow_qualifier";
    public const string LoginRole = "qq_arch7b_position_importer";
    public const string AdminRole = "qqpgadmin";
    public const string Database = "qq_pms_shadow_arch7b_test";
    public const string RemoteHost =
        "db-arch7b-pms-shadow-test.cx0goossu17s.eu-west-2.rds.amazonaws.com";

    public static readonly IReadOnlyList<string> SelectTables =
    [
        "public.__EFMigrationsHistory",
        "pms_shadow.intraday_projection_revisions",
        "pms_shadow.intraday_slots",
        "pms_shadow.account_snapshots",
        "pms_shadow.position_snapshots",
        "pms_shadow.position_snapshot_lines",
        "pms_shadow.working_leaves_observations",
        "pms_shadow.model_runs",
        "pms_shadow.security_mappings",
        "pms_shadow.shadow_trade_intents",
        "pms_shadow.shadow_risk_decisions",
        "pms_shadow.shadow_parent_orders",
        "pms_shadow.shadow_child_orders",
        "pms_shadow.shadow_execution_qualification_runs"
    ];

    public static readonly IReadOnlyList<string> InsertTables =
    [
        "pms_shadow.shadow_trade_intents",
        "pms_shadow.shadow_risk_decisions",
        "pms_shadow.shadow_parent_orders",
        "pms_shadow.shadow_child_orders",
        "pms_shadow.shadow_execution_qualification_runs"
    ];

    public static readonly IReadOnlyList<string> ForbiddenArch7bTables =
    [
        "pms_shadow.arch7b_qualification_runs",
        "pms_shadow.arch7b_fix_session_events",
        "pms_shadow.arch7b_order_send_ledger",
        "pms_shadow.arch7b_execution_reports",
        "pms_shadow.arch7b_fills",
        "pms_shadow.arch7b_position_ledger_events",
        "pms_shadow.arch7b_final_reconciliations"
    ];

    public static readonly IReadOnlyList<string> RequiredSchemas =
        ["pms_shadow"];
    public static readonly IReadOnlyList<string> AmbientPublicSchemas = ["public"];

    public const string RequiredFunction =
        "pg_catalog.pg_advisory_xact_lock(bigint)";

    public static void ValidatePacket(string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        var normalizedSql = string.Join(' ', sql.Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        Require(sql.Contains($"CREATE ROLE {QualificationRole}",
                StringComparison.Ordinal),
            "ARCH7A_PRIVILEGE_PACKET_ROLE_CREATE_MISSING");
        Require(normalizedSql.Contains(
                $"GRANT {QualificationRole} TO {LoginRole} WITH INHERIT FALSE, SET TRUE, ADMIN FALSE",
                StringComparison.Ordinal),
            "ARCH7A_PRIVILEGE_PACKET_MEMBERSHIP_MISMATCH");
        foreach (var table in SelectTables)
            Require(sql.Contains(SqlTableName(table), StringComparison.Ordinal),
                $"ARCH7A_PRIVILEGE_PACKET_SELECT_TABLE_MISSING:{table}");
        foreach (var table in InsertTables)
            Require(sql.Contains(SqlTableName(table), StringComparison.Ordinal),
                $"ARCH7A_PRIVILEGE_PACKET_INSERT_TABLE_MISSING:{table}");
        foreach (var table in ForbiddenArch7bTables)
            Require(!sql.Contains(SqlTableName(table), StringComparison.Ordinal),
                $"ARCH7A_PRIVILEGE_PACKET_ARCH7B_TABLE_FORBIDDEN:{table}");
        Require(!sql.Contains(" ON ALL ", StringComparison.OrdinalIgnoreCase) &&
                !sql.Contains("ALL TABLES", StringComparison.OrdinalIgnoreCase) &&
                !sql.Contains("DEFAULT PRIVILEGES", StringComparison.OrdinalIgnoreCase) &&
                !sql.Contains("GRANT ALL", StringComparison.OrdinalIgnoreCase),
            "ARCH7A_PRIVILEGE_PACKET_WILDCARD_FORBIDDEN");
        Require(!normalizedSql.Contains("GRANT EXECUTE", StringComparison.OrdinalIgnoreCase),
            "ARCH7A_PRIVILEGE_PACKET_REDUNDANT_EXECUTE_FORBIDDEN");
        Require(!normalizedSql.Contains(" TO PUBLIC", StringComparison.OrdinalIgnoreCase) &&
                !normalizedSql.Contains(" FROM PUBLIC", StringComparison.OrdinalIgnoreCase) &&
                !normalizedSql.Contains("ALTER DEFAULT PRIVILEGES",
                    StringComparison.OrdinalIgnoreCase),
            "ARCH7A_PRIVILEGE_PACKET_PUBLIC_MUTATION_FORBIDDEN");
        Require(!sql.Contains(" UPDATE ", StringComparison.OrdinalIgnoreCase) &&
                !sql.Contains(" DELETE ", StringComparison.OrdinalIgnoreCase) &&
                !sql.Contains(" TRUNCATE ", StringComparison.OrdinalIgnoreCase) &&
                !sql.Contains(" TEMP", StringComparison.OrdinalIgnoreCase),
            "ARCH7A_PRIVILEGE_PACKET_FORBIDDEN_PRIVILEGE");
    }

    private static string SqlTableName(string value) =>
        value == "public.__EFMigrationsHistory"
            ? "public.\"__EFMigrationsHistory\""
            : value;

    internal static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}

public sealed record Arch7aArch7bRoleScopeEvidence(
    string SessionUser,
    string QualificationRole,
    bool SetRoleAuthorityVerified,
    bool SetRoleVerified,
    bool ResetRoleVerified,
    int TempSchemaBeforeSetRole,
    int TempSchemaAfterSetRole,
    int TempSchemaAfterQualification,
    int TempSchemaAfterResetRole)
{
    public bool NoTemporarySchemaCreated =>
        TempSchemaBeforeSetRole == 0 &&
        TempSchemaAfterSetRole == 0 &&
        TempSchemaAfterQualification == 0 &&
        TempSchemaAfterResetRole == 0;
}

public sealed class Arch7aArch7bRoleScope : IAsyncDisposable
{
    private readonly NpgsqlConnection connection;
    private int reset;
    private int? tempSchemaAfterQualification;
    private int? tempSchemaAfterResetRole;

    private Arch7aArch7bRoleScope(
        NpgsqlConnection connection,
        string sessionUser,
        int tempSchemaBeforeSetRole,
        int tempSchemaAfterSetRole)
    {
        this.connection = connection;
        SessionUser = sessionUser;
        TempSchemaBeforeSetRole = tempSchemaBeforeSetRole;
        TempSchemaAfterSetRole = tempSchemaAfterSetRole;
    }

    public string SessionUser { get; }
    public bool SetRoleAuthorityVerified { get; private init; }
    public bool SetRoleVerified { get; private init; }
    public bool ResetRoleVerified => Volatile.Read(ref reset) != 0;
    public int TempSchemaBeforeSetRole { get; }
    public int TempSchemaAfterSetRole { get; }

    public static async Task<Arch7aArch7bRoleScope> EnterAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        Arch7aArch7bPrivilegeContract.Require(
            connection.State == ConnectionState.Open,
            "ARCH7A_QUALIFICATION_ROLE_CONNECTION_NOT_OPEN");
        try
        {
            var before = await ReadAuthorityAsync(connection, cancellationToken);
            Arch7aArch7bPrivilegeContract.Require(
                before.SessionUser == Arch7aArch7bPrivilegeContract.LoginRole &&
                before.CurrentUser == Arch7aArch7bPrivilegeContract.LoginRole,
                "ARCH7A_QUALIFICATION_PRE_SET_ROLE_IDENTITY_MISMATCH");
            Arch7aArch7bPrivilegeContract.Require(
                before.CanSet && !before.CanUse && !before.AdminOption,
                "ARCH7A_QUALIFICATION_SET_ROLE_AUTHORITY_MISMATCH");
            Arch7aArch7bPrivilegeContract.Require(before.TempSchema == 0,
                "ARCH7A_QUALIFICATION_TEMP_SCHEMA_PRESENT_BEFORE_SET_ROLE");
            await ExecuteAsync(connection,
                $"SET ROLE {Arch7aArch7bPrivilegeContract.QualificationRole}",
                cancellationToken);
            var identity = await ReadScopeStateAsync(connection, cancellationToken);
            Arch7aArch7bPrivilegeContract.Require(
                identity.SessionUser == Arch7aArch7bPrivilegeContract.LoginRole,
                "ARCH7A_QUALIFICATION_SESSION_USER_MISMATCH");
            Arch7aArch7bPrivilegeContract.Require(
                identity.CurrentUser ==
                Arch7aArch7bPrivilegeContract.QualificationRole,
                "ARCH7A_QUALIFICATION_SET_ROLE_NOT_EFFECTIVE");
            Arch7aArch7bPrivilegeContract.Require(identity.TempSchema == 0,
                "ARCH7A_QUALIFICATION_TEMP_SCHEMA_CREATED_BY_SET_ROLE");
            return new(connection, identity.SessionUser,
                before.TempSchema, identity.TempSchema)
            {
                SetRoleAuthorityVerified = true,
                SetRoleVerified = true
            };
        }
        catch
        {
            try { await ExecuteAsync(connection, "RESET ROLE", CancellationToken.None); }
            catch { }
            throw;
        }
    }

    public async Task AssertNoTemporarySchemaAsync(
        CancellationToken cancellationToken = default)
    {
        var state = await ReadScopeStateAsync(connection, cancellationToken);
        Arch7aArch7bPrivilegeContract.Require(
            state.SessionUser == SessionUser &&
            state.CurrentUser == Arch7aArch7bPrivilegeContract.QualificationRole,
            "ARCH7A_QUALIFICATION_ROLE_SCOPE_LOST");
        Arch7aArch7bPrivilegeContract.Require(state.TempSchema == 0,
            "ARCH7A_QUALIFICATION_TEMP_SCHEMA_CREATED_DURING_QUALIFICATION");
        tempSchemaAfterQualification = state.TempSchema;
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref reset) != 0) return;
        if (!tempSchemaAfterQualification.HasValue)
            await AssertNoTemporarySchemaAsync(cancellationToken);
        await ExecuteAsync(connection, "RESET ROLE", cancellationToken);
        var identity = await ReadScopeStateAsync(connection, cancellationToken);
        Arch7aArch7bPrivilegeContract.Require(
            identity.SessionUser == SessionUser &&
            identity.CurrentUser == SessionUser &&
            SessionUser == Arch7aArch7bPrivilegeContract.LoginRole,
            "ARCH7A_QUALIFICATION_RESET_ROLE_NOT_EFFECTIVE");
        Arch7aArch7bPrivilegeContract.Require(identity.TempSchema == 0,
            "ARCH7A_QUALIFICATION_TEMP_SCHEMA_PRESENT_AFTER_RESET_ROLE");
        tempSchemaAfterResetRole = identity.TempSchema;
        Volatile.Write(ref reset, 1);
    }

    public Arch7aArch7bRoleScopeEvidence Evidence()
    {
        var afterQualification = tempSchemaAfterQualification;
        var afterReset = tempSchemaAfterResetRole;
        Arch7aArch7bPrivilegeContract.Require(
            afterQualification.HasValue && afterReset.HasValue,
            "ARCH7A_QUALIFICATION_TEMP_SCHEMA_EVIDENCE_INCOMPLETE");
        var qualified = afterQualification.GetValueOrDefault();
        var resetRole = afterReset.GetValueOrDefault();
        return new(
            SessionUser,
            Arch7aArch7bPrivilegeContract.QualificationRole,
            SetRoleAuthorityVerified,
            SetRoleVerified,
            ResetRoleVerified,
            TempSchemaBeforeSetRole,
            TempSchemaAfterSetRole,
            qualified,
            resetRole);
    }

    public async ValueTask DisposeAsync() => await ResetAsync(CancellationToken.None);

    private static async Task ExecuteAsync(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(
        string SessionUser,
        string CurrentUser,
        bool CanSet,
        bool CanUse,
        bool AdminOption,
        int TempSchema)> ReadAuthorityAsync(
            NpgsqlConnection connection,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT session_user, current_user,
                   pg_catalog.pg_has_role(
                       'qq_arch7b_position_importer',
                       'qq_arch7a_shadow_qualifier', 'SET'),
                   pg_catalog.pg_has_role(
                       'qq_arch7b_position_importer',
                       'qq_arch7a_shadow_qualifier', 'USAGE'),
                   EXISTS (
                       SELECT 1 FROM pg_catalog.pg_auth_members
                       WHERE roleid = (SELECT oid FROM pg_catalog.pg_roles
                                       WHERE rolname = 'qq_arch7a_shadow_qualifier')
                         AND member = (SELECT oid FROM pg_catalog.pg_roles
                                      WHERE rolname = 'qq_arch7b_position_importer')
                         AND admin_option),
                   pg_catalog.pg_my_temp_schema()::integer
            """;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SingleRow, cancellationToken);
        Arch7aArch7bPrivilegeContract.Require(
            await reader.ReadAsync(cancellationToken),
            "ARCH7A_QUALIFICATION_ROLE_IDENTITY_MISSING");
        return (reader.GetString(0), reader.GetString(1), reader.GetBoolean(2),
            reader.GetBoolean(3), reader.GetBoolean(4), reader.GetInt32(5));
    }

    private static async Task<(
        string SessionUser,
        string CurrentUser,
        int TempSchema)> ReadScopeStateAsync(
            NpgsqlConnection connection,
            CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT session_user, current_user, pg_catalog.pg_my_temp_schema()::integer";
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SingleRow, cancellationToken);
        Arch7aArch7bPrivilegeContract.Require(
            await reader.ReadAsync(cancellationToken),
            "ARCH7A_QUALIFICATION_ROLE_SCOPE_STATE_MISSING");
        return (reader.GetString(0), reader.GetString(1), reader.GetInt32(2));
    }
}

public sealed record Arch7aArch7bPrivilegeReadback(
    bool RoleExists,
    bool RoleNoLogin,
    bool RoleAttributesExact,
    bool MembershipExact,
    bool SchemaGrantsExact,
    int SchemaGrantCount,
    bool TableGrantsExact,
    int TableGrantCount,
    bool FunctionDirectGrantAbsent,
    int FunctionDirectGrantCount,
    bool NoQualifierOwnership,
    bool NoLoginOwnership,
    bool NoDefaultPrivileges,
    bool NoQualifierDirectDatabasePrivileges,
    bool NoLoginDirectTemporary,
    bool NoQualifierSchemaCreate,
    bool NoLoginSchemaCreate,
    bool AmbientPublicTemporary,
    bool EffectiveQualifierTemporary,
    bool EffectiveLoginTemporary,
    bool NoTemporaryFromOtherMembership,
    bool AmbientPublicFunctionExecute,
    bool EffectiveQualifierFunctionExecute,
    bool AmbientPublicSchemaUsage,
    bool EffectiveQualifierPublicSchemaUsage,
    bool NoQualifierForbiddenArch7bPrivileges,
    bool NoLoginForbiddenArch7bWritePrivileges,
    bool NoQualifierDangerousTablePrivileges,
    bool NoLoginDangerousTablePrivileges,
    bool EffectiveQualifierTablePrivilegesExact,
    string AmbientPrivilegeStatus,
    bool Exact);

public static class Arch7aArch7bPrivilegeAuthorityRunner
{
    public const string ValidateMode = "validate-privilege-packet";
    public const string ApplyMode = "apply-privilege-packet";
    public const string ReadbackMode = "readback-privilege-authority";
    private const string PasswordEnvironmentVariable =
        "QQ_ARCH7A_PRIVILEGE_ADMIN_PASSWORD";

    public static bool IsAuthorityMode(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index + 1 < arguments.Count; index += 2)
            if (arguments[index] == "--mode")
                return arguments[index + 1] is ValidateMode or ApplyMode or
                    ReadbackMode;
        return false;
    }

    public static async Task RunAsync(
        IReadOnlyList<string> rawArguments,
        CancellationToken cancellationToken = default)
    {
        var arguments = AuthorityArguments.Parse(rawArguments);
        var packet = File.ReadAllText(arguments.PacketPath);
        Arch7aArch7bPrivilegeContract.ValidatePacket(packet);
        var packetSha = Convert.ToHexStringLower(SHA256.HashData(
            File.ReadAllBytes(arguments.PacketPath)));
        Arch7aArch7bPrivilegeContract.Require(
            packetSha == arguments.ExpectedPacketSha256,
            "ARCH7A_PRIVILEGE_PACKET_SHA256_MISMATCH");
        if (arguments.OfflineOnly)
        {
            Write(new
            {
                contract = Arch7aArch7bPrivilegeContract.Version,
                status = "ARCH7A_PRIVILEGE_PACKET_OFFLINE_VALIDATED",
                packet_sha256 = packetSha,
                secret_reads = 0,
                database_connections = 0,
                database_writes = 0,
                no_order = true
            });
            return;
        }

        var password = Environment.GetEnvironmentVariable(
            PasswordEnvironmentVariable);
        Arch7aArch7bPrivilegeContract.Require(
            !string.IsNullOrWhiteSpace(password),
            "ARCH7A_PRIVILEGE_ADMIN_CREDENTIAL_UNAVAILABLE");
        try
        {
            await using var connection = new NpgsqlConnection(
                arguments.BuildConnectionString(password!));
            await connection.OpenAsync(cancellationToken);
            await RequireAdminTargetAsync(connection, cancellationToken);

            Arch7aArch7bPrivilegeReadback readback;
            var committed = false;
            if (arguments.Mode == ReadbackMode)
            {
                readback = await ReadbackAsync(
                    connection, null, cancellationToken);
            }
            else
            {
                await using var transaction = await connection.BeginTransactionAsync(
                    IsolationLevel.Serializable, cancellationToken);
                try
                {
                    await using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = packet;
                    _ = await command.ExecuteNonQueryAsync(cancellationToken);
                    readback = await ReadbackAsync(
                        connection, transaction, cancellationToken);
                    if (!readback.Exact)
                    {
                        Write(new
                        {
                            contract = Arch7aArch7bPrivilegeContract.Version,
                            status = "ARCH7A_PRIVILEGE_TRANSACTION_READBACK_MISMATCH",
                            packet_sha256 = packetSha,
                            transaction_committed = false,
                            readback,
                            no_order = true
                        });
                    }
                    Arch7aArch7bPrivilegeContract.Require(readback.Exact,
                        "ARCH7A_PRIVILEGE_TRANSACTION_READBACK_MISMATCH");
                    if (arguments.Mode == ApplyMode)
                    {
                        await transaction.CommitAsync(cancellationToken);
                        committed = true;
                        readback = await ReadbackAsync(
                            connection, null, cancellationToken);
                        if (!readback.Exact)
                        {
                            Write(new
                            {
                                contract = Arch7aArch7bPrivilegeContract.Version,
                                status = "ARCH7A_PRIVILEGE_POST_COMMIT_READBACK_MISMATCH",
                                packet_sha256 = packetSha,
                                transaction_committed = true,
                                readback,
                                no_order = true
                            });
                        }
                        Arch7aArch7bPrivilegeContract.Require(readback.Exact,
                            "ARCH7A_PRIVILEGE_POST_COMMIT_READBACK_MISMATCH");
                    }
                    else
                    {
                        await transaction.RollbackAsync(cancellationToken);
                    }
                }
                catch
                {
                    try { await transaction.RollbackAsync(CancellationToken.None); }
                    catch { }
                    throw;
                }
            }

            Write(new
            {
                contract = Arch7aArch7bPrivilegeContract.Version,
                status = arguments.Mode switch
                {
                    ValidateMode => "ARCH7A_PRIVILEGE_PACKET_DRY_RUN_ROLLED_BACK",
                    ApplyMode => "ARCH7A_PRIVILEGE_AUTHORITY_APPLIED_AND_VERIFIED",
                    _ => "ARCH7A_PRIVILEGE_AUTHORITY_READBACK_VERIFIED"
                },
                packet_sha256 = packetSha,
                transaction_committed = committed,
                readback,
                secret_value_recorded = false,
                no_order = true
            });
        }
        finally
        {
            password = string.Empty;
        }
    }

    public static async Task<Arch7aArch7bPrivilegeReadback> ReadbackAsync(
        NpgsqlConnection connection,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var row = await ReadSingleAsync(connection, transaction, """
            WITH role_facts AS (
                SELECT oid, NOT rolcanlogin AS no_login,
                       NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND
                       NOT rolinherit AND NOT rolreplication AND NOT rolbypassrls AS attrs
                FROM pg_catalog.pg_roles
                WHERE rolname = 'qq_arch7a_shadow_qualifier'
            ), membership AS (
                SELECT count(*)::integer AS count,
                       COALESCE(bool_and(
                           am.roleid = (SELECT oid FROM role_facts)
                           AND am.member = (SELECT oid FROM pg_catalog.pg_roles
                                            WHERE rolname = 'qq_arch7b_position_importer')
                           AND NOT am.admin_option
                           AND NOT am.inherit_option
                           AND am.set_option), false) AS exact
                FROM pg_catalog.pg_auth_members am
                WHERE am.roleid = (SELECT oid FROM role_facts)
                   OR am.member = (SELECT oid FROM role_facts)
            ), expected_schema(schema_name, privilege_type) AS (
                VALUES ('pms_shadow', 'USAGE')
            ), actual_schema AS (
                SELECT n.nspname AS schema_name, a.privilege_type,
                       a.is_grantable
                FROM pg_catalog.pg_namespace n
                CROSS JOIN LATERAL pg_catalog.aclexplode(n.nspacl) a
                WHERE a.grantee = (SELECT oid FROM role_facts)
            ), schema_grants AS (
                SELECT (SELECT count(*)::integer FROM actual_schema) AS count,
                       NOT EXISTS (
                           SELECT schema_name, privilege_type FROM actual_schema
                           EXCEPT
                           SELECT schema_name, privilege_type FROM expected_schema
                       ) AND NOT EXISTS (
                           SELECT schema_name, privilege_type FROM expected_schema
                           EXCEPT
                           SELECT schema_name, privilege_type FROM actual_schema
                       ) AND NOT EXISTS (
                           SELECT 1 FROM actual_schema WHERE is_grantable
                       ) AS exact
            ), expected_table(schema_name, table_name, privilege_type) AS (
                VALUES
                    ('public', '__EFMigrationsHistory', 'SELECT'),
                    ('pms_shadow', 'intraday_projection_revisions', 'SELECT'),
                    ('pms_shadow', 'intraday_slots', 'SELECT'),
                    ('pms_shadow', 'account_snapshots', 'SELECT'),
                    ('pms_shadow', 'position_snapshots', 'SELECT'),
                    ('pms_shadow', 'position_snapshot_lines', 'SELECT'),
                    ('pms_shadow', 'working_leaves_observations', 'SELECT'),
                    ('pms_shadow', 'model_runs', 'SELECT'),
                    ('pms_shadow', 'security_mappings', 'SELECT'),
                    ('pms_shadow', 'shadow_trade_intents', 'SELECT'),
                    ('pms_shadow', 'shadow_risk_decisions', 'SELECT'),
                    ('pms_shadow', 'shadow_parent_orders', 'SELECT'),
                    ('pms_shadow', 'shadow_child_orders', 'SELECT'),
                    ('pms_shadow', 'shadow_execution_qualification_runs', 'SELECT'),
                    ('pms_shadow', 'shadow_trade_intents', 'INSERT'),
                    ('pms_shadow', 'shadow_risk_decisions', 'INSERT'),
                    ('pms_shadow', 'shadow_parent_orders', 'INSERT'),
                    ('pms_shadow', 'shadow_child_orders', 'INSERT'),
                    ('pms_shadow', 'shadow_execution_qualification_runs', 'INSERT')
            ), actual_table AS (
                SELECT n.nspname AS schema_name, c.relname AS table_name,
                       a.privilege_type, a.is_grantable
                FROM pg_catalog.pg_class c
                JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                CROSS JOIN LATERAL pg_catalog.aclexplode(c.relacl) a
                WHERE a.grantee = (SELECT oid FROM role_facts)
            ), table_grants AS (
                SELECT (SELECT count(*)::integer FROM actual_table) AS count,
                       NOT EXISTS (
                           SELECT schema_name, table_name, privilege_type
                           FROM actual_table
                           EXCEPT
                           SELECT schema_name, table_name, privilege_type
                           FROM expected_table
                       ) AND NOT EXISTS (
                           SELECT schema_name, table_name, privilege_type
                           FROM expected_table
                           EXCEPT
                           SELECT schema_name, table_name, privilege_type
                           FROM actual_table
                       ) AND NOT EXISTS (
                           SELECT 1 FROM actual_table WHERE is_grantable
                       ) AS exact
            ), actual_function AS (
                SELECT p.oid, a.privilege_type, a.is_grantable
                FROM pg_catalog.pg_proc p
                CROSS JOIN LATERAL pg_catalog.aclexplode(p.proacl) a
                WHERE a.grantee = (SELECT oid FROM role_facts)
            ), function_grants AS (
                SELECT (SELECT count(*)::integer FROM actual_function) AS count,
                       NOT EXISTS (SELECT 1 FROM actual_function) AS exact
            ), effective_table AS (
                SELECT n.nspname AS schema_name, c.relname AS table_name,
                       privilege.privilege_type
                FROM pg_catalog.pg_class c
                JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                CROSS JOIN (VALUES
                    ('SELECT'), ('INSERT'), ('UPDATE'), ('DELETE'),
                    ('TRUNCATE'), ('REFERENCES'), ('TRIGGER')
                ) privilege(privilege_type)
                WHERE n.nspname IN ('public', 'pms_shadow')
                  AND c.relkind IN ('r', 'p', 'v', 'm', 'f')
                  AND has_table_privilege(
                      'qq_arch7a_shadow_qualifier', c.oid,
                      privilege.privilege_type)
            ), effective_table_exact AS (
                SELECT NOT EXISTS (
                           SELECT schema_name, table_name, privilege_type
                           FROM effective_table
                           EXCEPT
                           SELECT schema_name, table_name, privilege_type
                           FROM expected_table
                       ) AND NOT EXISTS (
                           SELECT schema_name, table_name, privilege_type
                           FROM expected_table
                           EXCEPT
                           SELECT schema_name, table_name, privilege_type
                           FROM effective_table
                       ) AS exact
            )
            SELECT EXISTS(SELECT 1 FROM role_facts),
                   COALESCE((SELECT no_login FROM role_facts), false),
                   COALESCE((SELECT attrs FROM role_facts), false),
                   COALESCE((SELECT count = 1 AND exact FROM membership), false),
                   COALESCE((SELECT exact FROM schema_grants), false),
                   (SELECT count FROM schema_grants),
                   COALESCE((SELECT exact FROM table_grants), false),
                   (SELECT count FROM table_grants),
                   COALESCE((SELECT exact FROM function_grants), false),
                   (SELECT count FROM function_grants),
                   NOT EXISTS (
                       SELECT 1 FROM pg_catalog.pg_class
                       WHERE relowner = (SELECT oid FROM role_facts)
                       UNION ALL SELECT 1 FROM pg_catalog.pg_namespace
                       WHERE nspowner = (SELECT oid FROM role_facts)
                       UNION ALL SELECT 1 FROM pg_catalog.pg_proc
                       WHERE proowner = (SELECT oid FROM role_facts)
                       UNION ALL SELECT 1 FROM pg_catalog.pg_database
                       WHERE datdba = (SELECT oid FROM role_facts)),
                   NOT EXISTS (
                       SELECT 1 FROM pg_catalog.pg_class
                       WHERE relowner = (SELECT oid FROM pg_catalog.pg_roles
                                         WHERE rolname = 'qq_arch7b_position_importer')
                       UNION ALL SELECT 1 FROM pg_catalog.pg_namespace
                       WHERE nspowner = (SELECT oid FROM pg_catalog.pg_roles
                                         WHERE rolname = 'qq_arch7b_position_importer')
                       UNION ALL SELECT 1 FROM pg_catalog.pg_proc
                       WHERE proowner = (SELECT oid FROM pg_catalog.pg_roles
                                         WHERE rolname = 'qq_arch7b_position_importer')
                       UNION ALL SELECT 1 FROM pg_catalog.pg_database
                       WHERE datdba = (SELECT oid FROM pg_catalog.pg_roles
                                      WHERE rolname = 'qq_arch7b_position_importer')),
                   NOT EXISTS (
                       SELECT 1 FROM pg_catalog.pg_default_acl d
                       CROSS JOIN LATERAL pg_catalog.aclexplode(d.defaclacl) a
                       WHERE a.grantee IN (
                           (SELECT oid FROM role_facts),
                           (SELECT oid FROM pg_catalog.pg_roles
                            WHERE rolname = 'qq_arch7b_position_importer'))),
                   NOT EXISTS (
                       SELECT 1 FROM pg_catalog.pg_database d
                       CROSS JOIN LATERAL pg_catalog.aclexplode(d.datacl) a
                       WHERE d.datname = current_database()
                         AND a.grantee = (SELECT oid FROM role_facts)),
                   NOT EXISTS (
                       SELECT 1 FROM pg_catalog.pg_database d
                       CROSS JOIN LATERAL pg_catalog.aclexplode(d.datacl) a
                       WHERE d.datname = current_database()
                         AND a.grantee = (SELECT oid FROM pg_catalog.pg_roles
                                          WHERE rolname = 'qq_arch7b_position_importer')
                         AND a.privilege_type = 'TEMPORARY'),
                   NOT has_schema_privilege('qq_arch7a_shadow_qualifier', 'public', 'CREATE')
                       AND NOT has_schema_privilege('qq_arch7a_shadow_qualifier', 'pms_shadow', 'CREATE'),
                   NOT has_schema_privilege('qq_arch7b_position_importer', 'public', 'CREATE')
                       AND NOT has_schema_privilege('qq_arch7b_position_importer', 'pms_shadow', 'CREATE'),
                   has_database_privilege('public', current_database(), 'TEMP'),
                   has_database_privilege(
                       'qq_arch7a_shadow_qualifier', current_database(), 'TEMP'),
                   has_database_privilege(
                       'qq_arch7b_position_importer', current_database(), 'TEMP'),
                   NOT EXISTS (
                       SELECT 1
                       FROM pg_catalog.pg_roles inherited_role
                       JOIN pg_catalog.pg_database d ON d.datname = current_database()
                       CROSS JOIN LATERAL pg_catalog.aclexplode(d.datacl) a
                       WHERE inherited_role.oid NOT IN (
                                 (SELECT oid FROM role_facts),
                                 (SELECT oid FROM pg_catalog.pg_roles
                                  WHERE rolname = 'qq_arch7b_position_importer'))
                         AND pg_catalog.pg_has_role(
                             'qq_arch7b_position_importer', inherited_role.oid, 'USAGE')
                         AND a.grantee = inherited_role.oid
                         AND a.privilege_type = 'TEMPORARY'),
                   has_function_privilege(
                       'public', 'pg_catalog.pg_advisory_xact_lock(bigint)', 'EXECUTE'),
                   has_function_privilege(
                       'qq_arch7a_shadow_qualifier',
                       'pg_catalog.pg_advisory_xact_lock(bigint)', 'EXECUTE'),
                   has_schema_privilege('public', 'public', 'USAGE'),
                   has_schema_privilege(
                       'qq_arch7a_shadow_qualifier', 'public', 'USAGE'),
                   NOT EXISTS (
                       SELECT 1 FROM (VALUES
                           ('pms_shadow.arch7b_qualification_runs'),
                           ('pms_shadow.arch7b_fix_session_events'),
                           ('pms_shadow.arch7b_order_send_ledger'),
                           ('pms_shadow.arch7b_execution_reports'),
                           ('pms_shadow.arch7b_fills'),
                           ('pms_shadow.arch7b_position_ledger_events'),
                           ('pms_shadow.arch7b_final_reconciliations')
                       ) forbidden(name)
                       WHERE has_table_privilege('qq_arch7a_shadow_qualifier', name,
                           'SELECT,INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')),
                   NOT EXISTS (
                       SELECT 1 FROM (VALUES
                           ('pms_shadow.arch7b_qualification_runs'),
                           ('pms_shadow.arch7b_fix_session_events'),
                           ('pms_shadow.arch7b_order_send_ledger'),
                           ('pms_shadow.arch7b_execution_reports'),
                           ('pms_shadow.arch7b_fills'),
                           ('pms_shadow.arch7b_position_ledger_events'),
                           ('pms_shadow.arch7b_final_reconciliations')
                       ) forbidden(name)
                       WHERE has_table_privilege('qq_arch7b_position_importer', name,
                           'INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')),
                   NOT EXISTS (
                       SELECT 1
                       FROM pg_catalog.pg_class c
                       JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                       WHERE n.nspname IN ('public', 'pms_shadow')
                         AND c.relkind IN ('r', 'p', 'v', 'm', 'f')
                         AND has_table_privilege('qq_arch7a_shadow_qualifier', c.oid,
                             'UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')),
                   NOT EXISTS (
                       SELECT 1
                       FROM pg_catalog.pg_class c
                       JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                       WHERE n.nspname IN ('public', 'pms_shadow')
                         AND c.relkind IN ('r', 'p', 'v', 'm', 'f')
                         AND has_table_privilege('qq_arch7b_position_importer', c.oid,
                             'UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')),
                   COALESCE((SELECT exact FROM effective_table_exact), false)
            """, cancellationToken);
        var result = new Arch7aArch7bPrivilegeReadback(
            (bool)row[0], (bool)row[1], (bool)row[2], (bool)row[3], (bool)row[4],
            (int)row[5], (bool)row[6], (int)row[7], (bool)row[8], (int)row[9],
            (bool)row[10], (bool)row[11], (bool)row[12], (bool)row[13],
            (bool)row[14], (bool)row[15], (bool)row[16], (bool)row[17],
            (bool)row[18], (bool)row[19], (bool)row[20], (bool)row[21],
            (bool)row[22], (bool)row[23], (bool)row[24], (bool)row[25],
            (bool)row[26], (bool)row[27], (bool)row[28], (bool)row[29],
            "AMBIENT_PUBLIC_PRIVILEGE_ACCEPTED_NOT_DIRECTLY_GRANTED", false);
        var exact = result.RoleExists && result.RoleNoLogin &&
                    result.RoleAttributesExact && result.MembershipExact &&
                    result.SchemaGrantsExact &&
                    result.SchemaGrantCount == 1 &&
                    result.TableGrantsExact &&
                    result.TableGrantCount == 19 &&
                    result.FunctionDirectGrantAbsent &&
                    result.FunctionDirectGrantCount == 0 &&
                    result.NoQualifierOwnership && result.NoLoginOwnership &&
                    result.NoDefaultPrivileges &&
                    result.NoQualifierDirectDatabasePrivileges &&
                    result.NoLoginDirectTemporary &&
                    result.NoQualifierSchemaCreate && result.NoLoginSchemaCreate &&
                    result.AmbientPublicTemporary &&
                    result.EffectiveQualifierTemporary &&
                    result.EffectiveLoginTemporary &&
                    result.NoTemporaryFromOtherMembership &&
                    result.AmbientPublicFunctionExecute &&
                    result.EffectiveQualifierFunctionExecute &&
                    result.AmbientPublicSchemaUsage &&
                    result.EffectiveQualifierPublicSchemaUsage &&
                    result.NoQualifierForbiddenArch7bPrivileges &&
                    result.NoLoginForbiddenArch7bWritePrivileges &&
                    result.NoQualifierDangerousTablePrivileges &&
                    result.NoLoginDangerousTablePrivileges &&
                    result.EffectiveQualifierTablePrivilegesExact &&
                    result.AmbientPrivilegeStatus ==
                    "AMBIENT_PUBLIC_PRIVILEGE_ACCEPTED_NOT_DIRECTLY_GRANTED";
        return result with { Exact = exact };
    }

    private static async Task RequireAdminTargetAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var row = await ReadSingleAsync(connection, null, """
            SELECT current_database(), current_user,
                   current_setting('server_version_num')::integer / 10000,
                   COALESCE((SELECT ssl FROM pg_catalog.pg_stat_ssl
                             WHERE pid = pg_catalog.pg_backend_pid()), false)
            """, cancellationToken);
        Arch7aArch7bPrivilegeContract.Require(
            (string)row[0] == Arch7aArch7bPrivilegeContract.Database,
            "ARCH7A_PRIVILEGE_TARGET_DATABASE_MISMATCH");
        Arch7aArch7bPrivilegeContract.Require(
            (string)row[1] == Arch7aArch7bPrivilegeContract.AdminRole,
            "ARCH7A_PRIVILEGE_ADMIN_ROLE_MISMATCH");
        Arch7aArch7bPrivilegeContract.Require((int)row[2] == 18,
            "ARCH7A_PRIVILEGE_POSTGRESQL_MAJOR_MISMATCH");
        Arch7aArch7bPrivilegeContract.Require((bool)row[3],
            "ARCH7A_PRIVILEGE_TLS_REQUIRED");
    }

    private static async Task<object[]> ReadSingleAsync(
        NpgsqlConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (NpgsqlTransaction?)transaction;
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SingleRow, cancellationToken);
        Arch7aArch7bPrivilegeContract.Require(
            await reader.ReadAsync(cancellationToken),
            "ARCH7A_PRIVILEGE_READBACK_MISSING");
        var values = new object[reader.FieldCount];
        _ = reader.GetValues(values);
        return values;
    }

    private static void Write(object value) => Console.WriteLine(
        JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        }));

    private sealed class AuthorityArguments
    {
        private readonly IReadOnlyDictionary<string, string> values;

        private AuthorityArguments(
            string mode,
            IReadOnlyDictionary<string, string> values)
        {
            Mode = mode;
            this.values = values;
            PacketPath = Path.GetFullPath(Required("--packet"));
            ExpectedPacketSha256 = Required("--expected-packet-sha256");
            OfflineOnly = Boolean("--offline-only");
        }

        public string Mode { get; }
        public string PacketPath { get; }
        public string ExpectedPacketSha256 { get; }
        public bool OfflineOnly { get; }

        public static AuthorityArguments Parse(IReadOnlyList<string> arguments)
        {
            Arch7aArch7bPrivilegeContract.Require(
                arguments.Count > 0 && arguments.Count % 2 == 0,
                "ARCH7A_PRIVILEGE_ARGUMENT_SHAPE_INVALID");
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < arguments.Count; index += 2)
                Arch7aArch7bPrivilegeContract.Require(
                    values.TryAdd(arguments[index], arguments[index + 1]),
                    $"ARCH7A_PRIVILEGE_DUPLICATE_ARGUMENT:{arguments[index]}");
            var mode = values.GetValueOrDefault("--mode") ?? string.Empty;
            Arch7aArch7bPrivilegeContract.Require(
                mode is ValidateMode or ApplyMode or ReadbackMode,
                "ARCH7A_PRIVILEGE_MODE_INVALID");
            return new(mode, values);
        }

        public string BuildConnectionString(string password)
        {
            Arch7aArch7bPrivilegeContract.Require(!OfflineOnly,
                "ARCH7A_PRIVILEGE_OFFLINE_CONNECTION_FORBIDDEN");
            Arch7aArch7bPrivilegeContract.Require(
                Required("--expected-remote-host") ==
                Arch7aArch7bPrivilegeContract.RemoteHost,
                "ARCH7A_PRIVILEGE_REMOTE_HOST_MISMATCH");
            Arch7aArch7bPrivilegeContract.Require(
                Required("--database") == Arch7aArch7bPrivilegeContract.Database,
                "ARCH7A_PRIVILEGE_TARGET_DATABASE_MISMATCH");
            Arch7aArch7bPrivilegeContract.Require(
                Required("--role") == Arch7aArch7bPrivilegeContract.AdminRole,
                "ARCH7A_PRIVILEGE_ADMIN_ROLE_MISMATCH");
            var root = Path.GetFullPath(Required("--root-certificate"));
            var rootSha = Convert.ToHexStringLower(SHA256.HashData(
                File.ReadAllBytes(root)));
            Arch7aArch7bPrivilegeContract.Require(
                rootSha == Required("--expected-root-certificate-sha256"),
                "ARCH7A_PRIVILEGE_ROOT_CA_SHA256_MISMATCH");
            return new NpgsqlConnectionStringBuilder
            {
                Host = Required("--connect-host"),
                Port = int.Parse(Required("--connect-port"),
                    System.Globalization.CultureInfo.InvariantCulture),
                Database = Arch7aArch7bPrivilegeContract.Database,
                Username = Arch7aArch7bPrivilegeContract.AdminRole,
                Password = password,
                ApplicationName = "QQ_ARCH7A_PRIVILEGE_AUTHORITY",
                SslMode = SslMode.VerifyCA,
                RootCertificate = root,
                Pooling = false,
                Multiplexing = false,
                Enlist = false,
                Timeout = 20,
                CommandTimeout = 20,
                IncludeErrorDetail = false,
                LogParameters = false
            }.ConnectionString;
        }

        private string Required(string name) =>
            values.GetValueOrDefault(name) ??
            throw new InvalidDataException(
                $"ARCH7A_PRIVILEGE_ARGUMENT_REQUIRED:{name}");

        private bool Boolean(string name) => Required(name) switch
        {
            "true" => true,
            "false" => false,
            _ => throw new InvalidDataException(
                $"ARCH7A_PRIVILEGE_BOOLEAN_REQUIRED:{name}")
        };
    }
}
