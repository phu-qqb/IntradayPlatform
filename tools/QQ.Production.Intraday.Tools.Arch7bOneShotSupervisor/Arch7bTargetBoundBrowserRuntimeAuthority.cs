namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bTargetBoundBrowserRuntimeAuthorityValidation(
    string Verdict,
    string AuthorityId,
    string Path,
    string Sha256,
    bool MustExist,
    bool MustBeInsideRunRoot,
    int PortalPathBindingCount,
    int BracketPathBindingCount,
    bool CorePrequalificationUsesTargetAuthority,
    bool BrowserChannelAbsent,
    bool CompiledContentShaUsed,
    string EvidenceSha256);

public static class Arch7bTargetBoundBrowserRuntimeAuthorityGate
{
    public const string Verdict =
        "ARCH7B_TARGET_BOUND_BROWSER_RUNTIME_AUTHORITY_QUALIFIED";
    private const string ChromePathBinding = "${authority:chrome_executable.path}";

    public static Arch7bTargetBoundBrowserRuntimeAuthorityValidation Qualify(
        Arch7bOneShotLivePlanTemplate template,
        IReadOnlyDictionary<string, Arch7bFileAuthority> targetAuthorities)
    {
        if (!template.FileAuthorities.TryGetValue("chrome_executable", out var templateAuthority) ||
            !targetAuthorities.TryGetValue("chrome_executable", out var targetAuthority))
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandNonSecretEnvironmentAuthorityMissing,
                "chrome_executable");
        if (templateAuthority != targetAuthority)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.AuthorityBindingMismatch, "chrome_executable");

        Arch7bSealedNonSecretEnvironment.ValidateChromeAuthority(targetAuthority);
        var portalBindings = BindingCount(template, "PORTAL_SESSION_PROVEN");
        var bracketBindings = BindingCount(template, "BRACKET_T2");
        if (portalBindings != 1 || bracketBindings != 1)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandTemplateInvalid, "chrome_executable");
        var browserChannelAbsent = template.CommandTemplates.All(command =>
            command.ArgumentTemplates.All(argument =>
                !argument.Value.Contains("browserChannel", StringComparison.OrdinalIgnoreCase) &&
                !argument.Value.Contains("browser-channel", StringComparison.OrdinalIgnoreCase)));
        if (!browserChannelAbsent)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandTemplateInvalid, "browserChannel");

        var provisional = new Arch7bTargetBoundBrowserRuntimeAuthorityValidation(
            Verdict, targetAuthority.AuthorityId, targetAuthority.Path,
            targetAuthority.Sha256, targetAuthority.MustExist,
            targetAuthority.MustBeInsideRunRoot, portalBindings, bracketBindings,
            true, true, false, string.Empty);
        return provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(Canonical(provisional))
        };
    }

    private static int BindingCount(Arch7bOneShotLivePlanTemplate template, string stageId)
    {
        var command = template.CommandTemplates.SingleOrDefault(value => value.StageId == stageId)
            ?? throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandTemplateInvalid, stageId);
        return command.ArgumentTemplates.Count(argument => argument.Value == ChromePathBinding);
    }

    private static string Canonical(
        Arch7bTargetBoundBrowserRuntimeAuthorityValidation value) => string.Join('\n',
        value.Verdict, value.AuthorityId, value.Path, value.Sha256,
        value.MustExist, value.MustBeInsideRunRoot, value.PortalPathBindingCount,
        value.BracketPathBindingCount, value.CorePrequalificationUsesTargetAuthority,
        value.BrowserChannelAbsent, value.CompiledContentShaUsed);
}
