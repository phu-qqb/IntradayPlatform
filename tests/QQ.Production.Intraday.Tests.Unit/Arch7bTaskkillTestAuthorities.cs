using System.Security.Cryptography;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

internal static class Arch7bTaskkillTestAuthorities
{
    public static Dictionary<string, Arch7bFileAuthority> Create()
    {
        var git = FindExecutable("git.exe");
        var node = FindExecutable("node.exe");
        var taskkill = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "taskkill.exe");
        return new(StringComparer.Ordinal)
        {
            ["git_executable"] = FileAuthority("git_executable", git),
            ["node_executable"] = FileAuthority("node_executable", node),
            ["taskkill_executable"] = FileAuthority("taskkill_executable", taskkill)
        };
    }

    public static Arch7bFileAuthority FileAuthority(string id, string path) =>
        new(id, Path.GetFullPath(path), Sha(path), true, false);

    public static string Sha(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static string FindExecutable(string name)
    {
        var candidates = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries |
                                      StringSplitOptions.TrimEntries)
            .Select(directory => Path.Combine(directory, name))
            .Concat(name.Equals("node.exe", StringComparison.OrdinalIgnoreCase)
                ? [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "nodejs", name)]
                : []);
        return candidates.FirstOrDefault(File.Exists) ??
            throw new FileNotFoundException(name);
    }
}
