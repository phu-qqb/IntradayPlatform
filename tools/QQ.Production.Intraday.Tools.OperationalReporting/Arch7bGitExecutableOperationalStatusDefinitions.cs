namespace QQ.Production.Intraday.Tools.OperationalReporting;

public static class Arch7bGitExecutableOperationalStatusDefinitions
{
    public static IReadOnlyList<OperationalStatusCodeDefinition> All { get; } =
    [
        Code("ARCH7B_GIT_EXECUTABLE_ARGUMENT_REQUIRED", "SECURITY",
            "The repository-bound mode did not receive an explicit Git executable."),
        Code("ARCH7B_GIT_EXECUTABLE_PATH_NOT_ABSOLUTE", "SECURITY",
            "The Git executable path is not absolute."),
        Code("ARCH7B_GIT_EXECUTABLE_MISSING", "EVIDENCE",
            "The qualified Git executable is absent or is not a regular file."),
        Code("ARCH7B_GIT_EXECUTABLE_SHA256_MISMATCH", "LINEAGE",
            "The Git executable SHA-256 differs from the qualified profile."),
        Code("ARCH7B_GIT_EXECUTABLE_VERSION_MISMATCH", "LINEAGE",
            "The Git version output differs from the qualified profile."),
        Code("ARCH7B_GIT_EXECUTABLE_REPARSE_POINT_REJECTED", "SECURITY",
            "The Git executable path traverses a reparse point."),
        Code("ARCH7B_GIT_EXECUTABLE_ARCHITECTURE_MISMATCH", "SECURITY",
            "The Git executable is not the qualified x64 binary."),
        Code("ARCH7B_GIT_EXECUTABLE_AUTHENTICODE_INVALID", "SECURITY",
            "The Git executable Authenticode signature is not valid."),
        Code("ARCH7B_GIT_EXECUTION_HOST_INSTANCE_MISMATCH", "SECURITY",
            "The Git authority is not bound to the qualified Primary instance."),
        Code("ARCH7B_GIT_EXECUTION_HOST_NAME_MISMATCH", "SECURITY",
            "The Git authority is not executing on the qualified Primary host."),
        Code("NO_GO_ARCH7B_MINIGIT_EXECUTABLE_NOT_FOUND", "EVIDENCE",
            "No MiniGit executable candidate was found on the Primary host."),
        Code("NO_GO_ARCH7B_MINIGIT_EXECUTABLE_IDENTITY_AMBIGUOUS", "LINEAGE",
            "Multiple different MiniGit executable identities remain candidates."),
        Code("ARCH7B_GIT_COMMAND_TIMEOUT", "RUNTIME",
            "A bounded Git authority command exceeded ten seconds.")
    ];

    private static OperationalStatusCodeDefinition Code(
        string exactCode,
        string category,
        string description) =>
        new(
            exactCode,
            "ARCH7B_POSITION_IMPORT",
            category,
            OperationalBreakSeverity.Critical,
            "PMS_SHADOW",
            description,
            "Stop before any secret read and inspect the qualified MiniGit profile and repository evidence.",
            AutomaticResolutionPossible: false,
            BlocksTrading: true,
            BlocksAccounting: false,
            EvidenceRequirements:
                "Git path, SHA-256, version, host, repository HEAD, remote, worktree and index.",
            Supersedes: null,
            IntroducedByContractVersion:
                "arch7b_git_executable_authority_v1");
}
