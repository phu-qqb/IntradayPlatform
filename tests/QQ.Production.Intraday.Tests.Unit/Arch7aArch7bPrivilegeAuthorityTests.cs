using QQ.Production.Intraday.Tools.Arch7aShadowQualification;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7aArch7bPrivilegeAuthorityTests
{
    [Fact]
    public void Packet_matches_the_versioned_call_graph_without_wildcards()
    {
        var sql = File.ReadAllText(PacketPath());

        Arch7aArch7bPrivilegeContract.ValidatePacket(sql);

        Assert.Equal(14, Arch7aArch7bPrivilegeContract.SelectTables.Count);
        Assert.Equal(5, Arch7aArch7bPrivilegeContract.InsertTables.Count);
        Assert.Contains("GRANT USAGE ON SCHEMA pms_shadow", sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GRANT EXECUTE", sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" TO PUBLIC", sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" FROM PUBLIC", sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "WITH INHERIT FALSE, SET TRUE, ADMIN FALSE", sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ALL TABLES", sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DEFAULT PRIVILEGES", sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GRANT ALL", sql,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Packet_does_not_name_any_arch7b_order_or_ledger_table()
    {
        var sql = File.ReadAllText(PacketPath());

        foreach (var table in
                 Arch7aArch7bPrivilegeContract.ForbiddenArch7bTables)
            Assert.DoesNotContain(table, sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_scopes_the_qualification_role_on_the_pinned_connection()
    {
        var root = RepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools",
            "QQ.Production.Intraday.Tools.Arch7aShadowQualification",
            "Arch7aArch7bShadowQualification.cs"));
        var authority = File.ReadAllText(Path.Combine(root, "tools",
            "QQ.Production.Intraday.Tools.Arch7aShadowQualification",
            "Arch7aArch7bPrivilegeAuthority.cs"));

        Assert.Contains("Arch7aArch7bRoleScope.EnterAsync", runtime,
            StringComparison.Ordinal);
        Assert.Contains("lease.Connection", runtime, StringComparison.Ordinal);
        Assert.Contains(
            "SET ROLE {Arch7aArch7bPrivilegeContract.QualificationRole}",
            authority,
            StringComparison.Ordinal);
        Assert.Contains("RESET ROLE", authority, StringComparison.Ordinal);
        Assert.Contains("expected_schema", authority, StringComparison.Ordinal);
        Assert.Contains("expected_table", authority, StringComparison.Ordinal);
        Assert.DoesNotContain("expected_function", authority, StringComparison.Ordinal);
        Assert.Contains("AMBIENT_PUBLIC_PRIVILEGE_ACCEPTED_NOT_DIRECTLY_GRANTED",
            authority, StringComparison.Ordinal);
        Assert.Contains("pg_my_temp_schema()::integer", authority,
            StringComparison.Ordinal);
        Assert.Contains("AssertNoTemporarySchemaAsync", authority,
            StringComparison.Ordinal);
        Assert.Contains("OR am.member = (SELECT oid FROM role_facts)", authority,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TEMP", authority,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT INTO TEMP", authority,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("search_path", authority,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OpenAsync", authority[
            authority.IndexOf("public sealed class Arch7aArch7bRoleScope",
                StringComparison.Ordinal)..
            authority.IndexOf("public sealed record Arch7aArch7bPrivilegeReadback",
                StringComparison.Ordinal)], StringComparison.Ordinal);
    }

    [Fact]
    public void Authority_runner_reads_only_the_preloaded_admin_environment()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "tools",
            "QQ.Production.Intraday.Tools.Arch7aShadowQualification",
            "Arch7aArch7bPrivilegeAuthority.cs"));

        Assert.Equal(1, Count(source, "Environment.GetEnvironmentVariable("));
        Assert.DoesNotContain("SecretsManager", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSecretValue", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MigrateAsync", source, StringComparison.Ordinal);
        Assert.Contains("IsolationLevel.Serializable", source,
            StringComparison.Ordinal);
        Assert.Contains("transaction.RollbackAsync", source,
            StringComparison.Ordinal);
        Assert.Contains("transaction.CommitAsync", source,
            StringComparison.Ordinal);
    }

    private static string PacketPath() => Path.Combine(
        RepositoryRoot(), "deploy", "postgresql", "arch7b",
        "arch7a-shadow-qualifier-privileges-v1.sql");

    private static int Count(string value, string text)
    {
        var count = 0;
        for (var index = 0;
             (index = value.IndexOf(text, index, StringComparison.Ordinal)) >= 0;
             index += text.Length)
            count++;
        return count;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName, "QQ.Production.Intraday.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("test repository root not found");
    }
}
