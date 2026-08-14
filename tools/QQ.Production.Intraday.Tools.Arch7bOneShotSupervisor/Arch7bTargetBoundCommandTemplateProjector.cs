namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bTargetBoundCommandTemplateProjection(
    string ContractVersion,
    IReadOnlyList<Arch7bOneShotCommandTemplate> CommandTemplates,
    string SourceCommandTemplateSetSha256,
    string TargetCommandTemplateSetSha256,
    string EvidenceSha256);

public sealed record Arch7bTargetCommandEnvironmentValidation(
    string ContractVersion,
    bool Passed,
    int CommandCount,
    int EnvironmentVariableCount,
    int ForbiddenSourceHostPathCount,
    string EvidenceSha256);

public static class Arch7bTargetBoundCommandTemplateProjector
{
    public static Arch7bTargetBoundCommandTemplateProjection Project(
        IReadOnlyList<Arch7bOneShotCommandTemplate> sourceCommands,
        IReadOnlyDictionary<string, Arch7bFileAuthority> targetAuthorities)
    {
        var sourceSet = CommandSet(sourceCommands);
        var projected = sourceCommands.Select(command => ProjectCommand(command,
            targetAuthorities)).ToArray();
        var targetSet = CommandSet(projected);
        var canonical = string.Join('\n',
            Arch7bV2Contracts.TargetBoundCommandTemplateProjectionVersion,
            sourceSet, targetSet,
            string.Join('|', projected.Select(value => value.EvidenceSha256)));
        return new(Arch7bV2Contracts.TargetBoundCommandTemplateProjectionVersion,
            projected, sourceSet, targetSet, Arch7bOneShotContracts.Sha256(canonical));
    }

    public static void RequireExactProjection(
        IReadOnlyList<Arch7bOneShotCommandTemplate> sourceCommands,
        Arch7bOneShotLivePlanTemplate targetTemplate)
    {
        var expected = Project(sourceCommands, targetTemplate.FileAuthorities);
        if (!expected.CommandTemplates.SequenceEqual(targetTemplate.CommandTemplates) ||
            expected.TargetCommandTemplateSetSha256 != targetTemplate.CommandTemplateSetSha256)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.TargetCommandEnvironmentMismatch);
    }

    private static Arch7bOneShotCommandTemplate ProjectCommand(
        Arch7bOneShotCommandTemplate source,
        IReadOnlyDictionary<string, Arch7bFileAuthority> targetAuthorities)
    {
        var environment = ProjectEnvironment(source, targetAuthorities);
        var provisional = source with
        {
            NonSecretEnvironment = environment,
            EvidenceSha256 = string.Empty
        };
        return provisional with
        {
            EvidenceSha256 = CommandEvidence(provisional)
        };
    }

    private static IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> ProjectEnvironment(
        Arch7bOneShotCommandTemplate source,
        IReadOnlyDictionary<string, Arch7bFileAuthority> targetAuthorities)
    {
        if (source.NonSecretEnvironment.Count == 0) return [];
        var names = source.NonSecretEnvironment.Select(value => value.VariableName)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (names.Length != source.NonSecretEnvironment.Count)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.TargetCommandEnvironmentProjectorMissing,
                source.CommandId);
        var projected = new List<Arch7bSealedNonSecretEnvironmentVariable>();
        foreach (var name in names)
        {
            IReadOnlyList<Arch7bSealedNonSecretEnvironmentVariable> values = name switch
            {
                "PATH" when source.StageId == "CORE_PREQUALIFICATION" =>
                    Arch7bSealedNonSecretEnvironment
                        .ForCorePrequalificationEnvironment(targetAuthorities),
                "DOTNET_ROOT" => Arch7bSealedNonSecretEnvironment.ForDotnetRoot(targetAuthorities),
                _ => throw new Arch7bQualificationException(
                    Arch7bV2Blockers.TargetCommandEnvironmentProjectorMissing,
                    source.CommandId + ":" + name)
            };
            projected.AddRange(values);
        }
        _ = Arch7bSealedNonSecretEnvironment.ValidateTemplate(projected,
            targetAuthorities, source.CommandId, source.StageId);
        return projected;
    }

    public static string CommandEvidence(Arch7bOneShotCommandTemplate command) =>
        Arch7bOneShotContracts.Sha256(string.Join('\n',
            Arch7bV2Contracts.TargetBoundCommandTemplateProjectionVersion,
            Arch7bV2Contracts.CommandTemplateVersion,
            command.CommandId, command.StageId, command.ExecutionKind,
            command.ExecutableAuthorityId,
            string.Join('|', command.ArgumentTemplates.Select(value =>
                $"{value.Value}:{value.ValueKind}:{value.ExpectedProducerStage}:" +
                $"{value.MaximumAgeSeconds}:{value.MustBeInsideRunRoot}")),
            command.WorkingDirectoryAuthorityId, command.AdapterId,
            command.AdapterContractVersion, command.ExpectedNativeOutputContract,
            command.TimeoutSeconds, command.StandardOutputLimitBytes,
            command.StandardErrorLimitBytes, command.CleanupResourceType,
            command.CausesRdsRead, command.CausesCapture, command.ReadsSecret,
            string.Join('|', command.SecretVariableNames),
            Arch7bSealedNonSecretEnvironment.Canonical(command.NonSecretEnvironment),
            command.LongLivedProcessKey ?? string.Empty));

    public static string CommandSet(
        IReadOnlyList<Arch7bOneShotCommandTemplate> commands) =>
        Arch7bOneShotContracts.Sha256(string.Join('\n',
            commands.Select(value => value.EvidenceSha256)));

}

public static class Arch7bTargetCommandEnvironmentValidator
{
    public static Arch7bTargetCommandEnvironmentValidation Validate(
        Arch7bOneShotLivePlanTemplate template)
    {
        var variableCount = 0;
        var forbiddenCount = 0;
        var projectedEvidenceCount = 0;
        foreach (var command in template.CommandTemplates)
        {
            variableCount += command.NonSecretEnvironment.Count;
            foreach (var argument in command.ArgumentTemplates.Where(argument =>
                         Path.IsPathFullyQualified(argument.Value)))
            {
                if (template.FileAuthorities.Values.Any(authority =>
                        SamePath(argument.Value, authority.Path))) continue;
                forbiddenCount++;
                throw new Arch7bQualificationException(
                    Arch7bV2Blockers.TargetCommandEnvironmentSourcePathPresent,
                    command.CommandId + ":argument");
            }
            foreach (var value in command.NonSecretEnvironment)
            {
                var expected = Expected(value.VariableName, command,
                    template.FileAuthorities);
                if (ContainsUnexpectedAbsolutePath(value.Value, expected.Value))
                {
                    forbiddenCount++;
                    throw new Arch7bQualificationException(
                        Arch7bV2Blockers.TargetCommandEnvironmentSourcePathPresent,
                        command.CommandId + ":" + value.VariableName);
                }
                if (value with { EvidenceSha256 = expected.EvidenceSha256 } != expected)
                    throw new Arch7bQualificationException(
                        Arch7bV2Blockers.TargetCommandEnvironmentMismatch,
                        command.CommandId + ":" + value.VariableName);
                if (value.EvidenceSha256 != expected.EvidenceSha256)
                    throw new Arch7bQualificationException(
                        Arch7bV2Blockers.TargetCommandEnvironmentEvidenceMismatch,
                        command.CommandId + ":" + value.VariableName);
            }
            _ = Arch7bSealedNonSecretEnvironment.ValidateTemplate(
                command.NonSecretEnvironment, template.FileAuthorities,
                command.CommandId, command.StageId);
            var expectedCommandEvidence = Arch7bTargetBoundCommandTemplateProjector
                .CommandEvidence(command with { EvidenceSha256 = string.Empty });
            if (command.EvidenceSha256 == expectedCommandEvidence)
                projectedEvidenceCount++;
        }
        if (projectedEvidenceCount != 0)
        {
            if (projectedEvidenceCount != template.CommandTemplates.Count)
                throw new Arch7bQualificationException(
                    Arch7bV2Blockers.TargetCommandEnvironmentEvidenceMismatch,
                    "mixed-source-and-target-command-evidence");
            var expectedCommandSet = Arch7bTargetBoundCommandTemplateProjector
                .CommandSet(template.CommandTemplates);
            if (template.CommandTemplateSetSha256 != expectedCommandSet ||
                template.EvidenceSha256 != Arch7bOneShotContracts.Sha256(template.Canonical()))
                throw new Arch7bQualificationException(
                    Arch7bV2Blockers.TargetCommandEnvironmentEvidenceMismatch,
                    "command-set-or-template-evidence");
        }
        var canonical = string.Join('\n',
            Arch7bV2Contracts.TargetCommandEnvironmentValidationVersion,
            true, template.CommandTemplates.Count, variableCount, forbiddenCount,
            template.CommandTemplateSetSha256,
            template.StaticAuthoritySetSha256);
        return new(Arch7bV2Contracts.TargetCommandEnvironmentValidationVersion,
            true, template.CommandTemplates.Count, variableCount, forbiddenCount,
            Arch7bOneShotContracts.Sha256(canonical));
    }

    private static Arch7bSealedNonSecretEnvironmentVariable Expected(
        string variableName, Arch7bOneShotCommandTemplate command,
        IReadOnlyDictionary<string, Arch7bFileAuthority> authorities) =>
        variableName switch
        {
            "PATH" when command.StageId == "CORE_PREQUALIFICATION" =>
                Arch7bSealedNonSecretEnvironment
                    .ForCorePrequalificationEnvironment(authorities).Single(),
            "DOTNET_ROOT" => Arch7bSealedNonSecretEnvironment
                .ForDotnetRoot(authorities).Single(),
            _ => throw new Arch7bQualificationException(
                Arch7bV2Blockers.TargetCommandEnvironmentMismatch,
                command.CommandId + ":" + variableName)
        };

    private static bool ContainsUnexpectedAbsolutePath(string actual, string expected)
    {
        var expectedParts = expected.Split(Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var actualParts = actual.Split(Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return actualParts.Any(part => Path.IsPathFullyQualified(part) &&
            !expectedParts.Contains(part, StringComparer.OrdinalIgnoreCase));
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
}
