using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class Arch7bGitExecutableAuthorityContract
{
    public const string Version = "arch7b_git_executable_authority_v1";
    public const string Qualified =
        "ARCH7B_REPOSITORY_AUTHORITY_QUALIFIED";
    public const string ExecutionHostInstanceId = "i-05535ebe6ce80a57b";
    public const string ExecutionHostName = "EC2AMAZ-SK6JMFA";
    public const string ExpectedRepositoryRemote =
        "https://github.com/phu-qqb/IntradayPlatform.git";
    public const int CommandTimeoutSeconds = 10;
    public const string ArgumentRequired =
        "ARCH7B_GIT_EXECUTABLE_ARGUMENT_REQUIRED";
    public const string PathNotAbsolute =
        "ARCH7B_GIT_EXECUTABLE_PATH_NOT_ABSOLUTE";
    public const string Missing = "ARCH7B_GIT_EXECUTABLE_MISSING";
    public const string ShaMismatch =
        "ARCH7B_GIT_EXECUTABLE_SHA256_MISMATCH";
    public const string VersionMismatch =
        "ARCH7B_GIT_EXECUTABLE_VERSION_MISMATCH";
    public const string ReparsePointRejected =
        "ARCH7B_GIT_EXECUTABLE_REPARSE_POINT_REJECTED";
    public const string IdentityAmbiguous =
        "NO_GO_ARCH7B_MINIGIT_EXECUTABLE_IDENTITY_AMBIGUOUS";
    public const string CommandTimeout = "ARCH7B_GIT_COMMAND_TIMEOUT";
}

public sealed record Arch7bGitExecutableFacts(
    string Path,
    string Sha256,
    string GitVersion,
    string Architecture,
    string AuthenticodeStatus,
    bool IsRegularFile,
    bool HasReparsePoint);

public interface IArch7bGitExecutableInspector
{
    Arch7bGitExecutableFacts Inspect(string path);
}

public sealed class Arch7bGitExecutableInspector :
    IArch7bGitExecutableInspector
{
    public Arch7bGitExecutableFacts Inspect(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidDataException(
                Arch7bGitExecutableAuthorityContract.ArgumentRequired);
        if (!Path.IsPathFullyQualified(path))
            throw new InvalidDataException(
                Arch7bGitExecutableAuthorityContract.PathNotAbsolute);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new InvalidDataException(
                Arch7bGitExecutableAuthorityContract.Missing);

        var attributes = File.GetAttributes(fullPath);
        return new(
            fullPath,
            Convert.ToHexStringLower(
                SHA256.HashData(File.ReadAllBytes(fullPath))),
            Arch7bBoundedGitProcess.Run(
                fullPath, Environment.CurrentDirectory, ["--version"]).Output,
            ReadArchitecture(fullPath),
            WinTrust.Verify(fullPath) ? "Valid" : "Invalid",
            (attributes & FileAttributes.Directory) == 0,
            ContainsReparsePoint(fullPath));
    }

    private static bool ContainsReparsePoint(string path)
    {
        FileSystemInfo? current = new FileInfo(path);
        while (current is not null)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                return true;
            current = current is FileInfo file
                ? file.Directory
                : ((DirectoryInfo)current).Parent;
        }
        return false;
    }

    private static string ReadArchitecture(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        return reader.PEHeaders.CoffHeader.Machine switch
        {
            Machine.Amd64 => "x64",
            Machine.I386 => "x86",
            Machine.Arm64 => "arm64",
            _ => reader.PEHeaders.CoffHeader.Machine.ToString()
        };
    }

    private static class WinTrust
    {
        private static readonly Guid Action =
            new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        public static bool Verify(string path)
        {
            if (!OperatingSystem.IsWindows()) return false;
            using var file = new FileInfo(path);
            using var data = new Data(file);
            var action = Action;
            return WinVerifyTrust(IntPtr.Zero, ref action, data) == 0;
        }

        [DllImport("wintrust.dll", ExactSpelling = true,
            PreserveSig = true, SetLastError = false)]
        private static extern int WinVerifyTrust(
            IntPtr hwnd, ref Guid actionId, Data data);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private sealed class FileInfo : IDisposable
        {
            private readonly uint structureSize =
                (uint)Marshal.SizeOf<FileInfo>();
            private IntPtr filePath;
            private readonly IntPtr fileHandle = IntPtr.Zero;
            private readonly IntPtr knownSubject = IntPtr.Zero;

            public FileInfo(string path) =>
                filePath = Marshal.StringToCoTaskMemUni(path);

            public void Dispose()
            {
                if (filePath != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(filePath);
                filePath = IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private sealed class Data : IDisposable
        {
            private readonly uint structureSize =
                (uint)Marshal.SizeOf<Data>();
            private readonly IntPtr policyCallbackData = IntPtr.Zero;
            private readonly IntPtr sipClientData = IntPtr.Zero;
            private readonly uint uiChoice = 2;
            private readonly uint revocationChecks = 0;
            private readonly uint unionChoice = 1;
            private IntPtr fileInfo;
            private readonly uint stateAction = 0;
            private readonly IntPtr stateData = IntPtr.Zero;
            private readonly IntPtr urlReference = IntPtr.Zero;
            private readonly uint providerFlags = 0x00000100;
            private readonly uint uiContext = 0;
            private readonly IntPtr signatureSettings = IntPtr.Zero;

            public Data(FileInfo file)
            {
                fileInfo = Marshal.AllocCoTaskMem(
                    Marshal.SizeOf<FileInfo>());
                Marshal.StructureToPtr(file, fileInfo, false);
            }

            public void Dispose()
            {
                if (fileInfo != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(fileInfo);
                fileInfo = IntPtr.Zero;
            }
        }
    }
}

public sealed record Arch7bGitExecutableAuthority(
    string ContractVersion,
    string ExecutionHostInstanceId,
    string ExecutionHostName,
    string GitExecutablePath,
    string GitExecutableSha256,
    string GitVersion,
    string GitArchitecture,
    string GitAuthenticodeStatus,
    bool UseShellExecute,
    bool ShellUsed,
    bool AmbientPathUsed,
    int CommandTimeoutSeconds);

public sealed class Arch7bGitExecutableAuthorityQualifier(
    IArch7bGitExecutableInspector? inspector = null)
{
    private readonly IArch7bGitExecutableInspector inspector =
        inspector ?? new Arch7bGitExecutableInspector();

    public Arch7bGitExecutableAuthority Qualify(
        string path,
        string expectedSha256,
        string expectedVersion,
        string executionHostInstanceId,
        string executionHostName)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidDataException(
                Arch7bGitExecutableAuthorityContract.ArgumentRequired);
        if (!Path.IsPathFullyQualified(path))
            throw new InvalidDataException(
                Arch7bGitExecutableAuthorityContract.PathNotAbsolute);
        var facts = inspector.Inspect(path);
        Require(facts.IsRegularFile,
            Arch7bGitExecutableAuthorityContract.Missing);
        Require(!facts.HasReparsePoint,
            Arch7bGitExecutableAuthorityContract.ReparsePointRejected);
        Require(facts.Sha256 == expectedSha256,
            Arch7bGitExecutableAuthorityContract.ShaMismatch);
        Require(facts.GitVersion == expectedVersion,
            Arch7bGitExecutableAuthorityContract.VersionMismatch);
        Require(facts.Architecture == "x64",
            "ARCH7B_GIT_EXECUTABLE_ARCHITECTURE_MISMATCH");
        Require(facts.AuthenticodeStatus == "Valid",
            "ARCH7B_GIT_EXECUTABLE_AUTHENTICODE_INVALID");
        Require(executionHostInstanceId ==
                Arch7bGitExecutableAuthorityContract.ExecutionHostInstanceId,
            "ARCH7B_GIT_EXECUTION_HOST_INSTANCE_MISMATCH");
        Require(executionHostName ==
                Arch7bGitExecutableAuthorityContract.ExecutionHostName,
            "ARCH7B_GIT_EXECUTION_HOST_NAME_MISMATCH");
        return new(
            Arch7bGitExecutableAuthorityContract.Version,
            executionHostInstanceId,
            executionHostName,
            facts.Path,
            facts.Sha256,
            facts.GitVersion,
            facts.Architecture,
            facts.AuthenticodeStatus,
            false,
            false,
            false,
            Arch7bGitExecutableAuthorityContract.CommandTimeoutSeconds);
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }
}

public static class Arch7bGitExecutableCandidateAuthority
{
    public static Arch7bGitExecutableFacts Select(
        IReadOnlyCollection<Arch7bGitExecutableFacts> candidates)
    {
        if (candidates.Count == 0)
            throw new InvalidDataException(
                "NO_GO_ARCH7B_MINIGIT_EXECUTABLE_NOT_FOUND");
        if (candidates.Select(value =>
                (value.Sha256, value.GitVersion)).Distinct().Count() != 1)
            throw new InvalidDataException(
                Arch7bGitExecutableAuthorityContract.IdentityAmbiguous);
        return candidates.OrderBy(value => value.Path,
            StringComparer.OrdinalIgnoreCase).First();
    }
}

public sealed record Arch7bGitCommandResult(
    int ExitCode, string Output, string SanitizedError);

public static class Arch7bBoundedGitProcess
{
    public static Arch7bGitCommandResult Run(
        string executablePath,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        bool allowEmpty = false)
    {
        using var process = new Process
        {
            StartInfo = new(executablePath)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(checked(
                Arch7bGitExecutableAuthorityContract.CommandTimeoutSeconds *
                1000)))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new InvalidDataException(
                Arch7bGitExecutableAuthorityContract.CommandTimeout);
        }
        Task.WaitAll(outputTask, errorTask);
        var output = outputTask.Result.Trim();
        var error = Sanitize(errorTask.Result);
        if (process.ExitCode != 0 || (!allowEmpty && output.Length == 0))
            throw new InvalidDataException(
                Arch7bPositionImportContract.RepositoryStateMismatch +
                (error.Length == 0 ? string.Empty : " " + error));
        return new(process.ExitCode, output, error);
    }

    private static string Sanitize(string value)
    {
        var oneLine = string.Join(" ", value.Split(
            ['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return oneLine.Length <= 512 ? oneLine : oneLine[..512];
    }
}

public sealed class Arch7bGitCommandRunner(
    Arch7bGitExecutableAuthority authority)
{
    public string Run(string root, params string[] arguments) =>
        Arch7bBoundedGitProcess.Run(
            authority.GitExecutablePath, root, arguments).Output;

    public string RunAllowEmpty(string root, params string[] arguments) =>
        Arch7bBoundedGitProcess.Run(
            authority.GitExecutablePath, root, arguments, true).Output;
}

public sealed record Arch7bRepositoryAuthorityEvidence(
    string Result,
    string ContractVersion,
    string ExecutionHostInstanceId,
    string ExecutionHostName,
    string GitExecutablePath,
    string GitExecutableSha256,
    string GitVersion,
    string GitArchitecture,
    string GitAuthenticodeStatus,
    string RepositoryRoot,
    string ExpectedRepositoryRemote,
    string ExpectedRepositoryHead,
    string ExpectedBuildCommit,
    bool WorktreeCleanRequired,
    bool IndexCleanRequired,
    bool UseShellExecute,
    bool ShellUsed,
    bool AmbientPathUsed,
    int CommandTimeoutSeconds,
    bool NoSecretRead,
    bool BuildRuntimeCalled,
    bool OpenAsyncCalled,
    bool DatabaseConnectionOpened,
    bool ArmedStateCreated,
    bool OwnerLockCreated,
    string EvidenceSha256);

public static class Arch7bRepositoryAuthorityEvidenceWriter
{
    public static Arch7bRepositoryAuthorityEvidence Write(
        string outputDirectory,
        Arch7bGitExecutableAuthority git,
        Arch7bRepositoryState repository)
    {
        Directory.CreateDirectory(outputDirectory);
        var canonical = JsonSerializer.SerializeToUtf8Bytes(new
        {
            git,
            repository,
            expected_repository_remote =
                Arch7bGitExecutableAuthorityContract.ExpectedRepositoryRemote,
            no_secret_read = true,
            build_runtime_called = false,
            open_async_called = false,
            database_connection_opened = false,
            armed_state_created = false,
            owner_lock_created = false
        });
        var evidence = new Arch7bRepositoryAuthorityEvidence(
            Arch7bGitExecutableAuthorityContract.Qualified,
            git.ContractVersion,
            git.ExecutionHostInstanceId,
            git.ExecutionHostName,
            git.GitExecutablePath,
            git.GitExecutableSha256,
            git.GitVersion,
            git.GitArchitecture,
            git.GitAuthenticodeStatus,
            repository.RepositoryRoot,
            Arch7bGitExecutableAuthorityContract.ExpectedRepositoryRemote,
            repository.HeadCommit,
            repository.BuildCommit,
            true,
            true,
            git.UseShellExecute,
            git.ShellUsed,
            git.AmbientPathUsed,
            git.CommandTimeoutSeconds,
            true,
            false,
            false,
            false,
            false,
            false,
            Convert.ToHexStringLower(SHA256.HashData(canonical)));
        File.WriteAllText(
            Path.Combine(Path.GetFullPath(outputDirectory),
                "arch7b-repository-authority-qualification.json"),
            JsonSerializer.Serialize(evidence,
                new JsonSerializerOptions { WriteIndented = true }));
        return evidence;
    }
}
