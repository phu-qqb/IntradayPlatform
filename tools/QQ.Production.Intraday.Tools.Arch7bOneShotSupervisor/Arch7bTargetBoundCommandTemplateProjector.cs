using System.Text;

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

public sealed record Arch7bTargetCommandProjectionMismatchEvidence(
    string ContractVersion,
    int CommandIndex,
    string CommandId,
    string StageId,
    string FieldName,
    string ExpectedFieldSha256,
    string ObservedFieldSha256,
    bool RawSensitiveValuesPersisted,
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
        RequireCanonicalProjectionEquality(expected, targetTemplate.CommandTemplates,
            targetTemplate.CommandTemplateSetSha256);
    }

    public static void RequireCanonicalProjectionEquality(
        Arch7bTargetBoundCommandTemplateProjection expected,
        IReadOnlyList<Arch7bOneShotCommandTemplate> observedCommands,
        string observedCommandTemplateSetSha256)
    {
        if (expected.CommandTemplates.Count != observedCommands.Count)
            throw Mismatch(Math.Min(expected.CommandTemplates.Count, observedCommands.Count),
                expected.CommandTemplates.ElementAtOrDefault(observedCommands.Count),
                observedCommands.ElementAtOrDefault(expected.CommandTemplates.Count),
                "CommandCount", expected.CommandTemplates.Count.ToString(),
                observedCommands.Count.ToString());

        for (var index = 0; index < expected.CommandTemplates.Count; index++)
        {
            var expectedCommand = expected.CommandTemplates[index];
            var observedCommand = observedCommands[index];
            var fields = Fields(expectedCommand, observedCommand);
            var mismatch = fields.FirstOrDefault(field =>
                !string.Equals(field.Expected, field.Observed, StringComparison.Ordinal));
            if (mismatch.Name is not null)
                throw Mismatch(index, expectedCommand, observedCommand, mismatch.Name,
                    mismatch.Expected, mismatch.Observed);

            var expectedCanonical = CanonicalCommand(expectedCommand);
            var observedCanonical = CanonicalCommand(observedCommand);
            if (!string.Equals(expectedCanonical, observedCanonical, StringComparison.Ordinal))
                throw Mismatch(index, expectedCommand, observedCommand, "CanonicalCommand",
                    expectedCanonical, observedCanonical);
        }

        if (!string.Equals(expected.TargetCommandTemplateSetSha256,
                observedCommandTemplateSetSha256, StringComparison.Ordinal))
            throw Mismatch(-1, null, null, "TargetCommandTemplateSetSha256",
                expected.TargetCommandTemplateSetSha256, observedCommandTemplateSetSha256);
    }

    public static byte[] CanonicalCommandBytes(Arch7bOneShotCommandTemplate command) =>
        Encoding.UTF8.GetBytes(CanonicalCommand(command));

    public static string CanonicalCommand(Arch7bOneShotCommandTemplate command) =>
        CanonicalFields(
            command.ContractVersion,
            command.CommandId,
            command.StageId,
            command.ExecutionKind.ToString(),
            command.ExecutableAuthorityId,
            CanonicalList(command.ArgumentTemplates.Select(CanonicalArgument)),
            command.WorkingDirectoryAuthorityId,
            command.AdapterId,
            command.AdapterContractVersion,
            command.ExpectedNativeOutputContract,
            command.TimeoutSeconds.ToString(),
            command.StandardOutputLimitBytes.ToString(),
            command.StandardErrorLimitBytes.ToString(),
            command.CleanupResourceType,
            command.CausesRdsRead.ToString(),
            command.CausesCapture.ToString(),
            command.ReadsSecret.ToString(),
            CanonicalList(command.SecretVariableNames),
            Arch7bSealedNonSecretEnvironment.Canonical(command.NonSecretEnvironment),
            command.LongLivedProcessKey ?? string.Empty,
            command.EvidenceSha256);

    private static IReadOnlyList<(string Name, string Expected, string Observed)> Fields(
        Arch7bOneShotCommandTemplate expected, Arch7bOneShotCommandTemplate observed) =>
    [
        (nameof(expected.ContractVersion), expected.ContractVersion, observed.ContractVersion),
        (nameof(expected.CommandId), expected.CommandId, observed.CommandId),
        (nameof(expected.StageId), expected.StageId, observed.StageId),
        (nameof(expected.ExecutionKind), expected.ExecutionKind.ToString(), observed.ExecutionKind.ToString()),
        (nameof(expected.ExecutableAuthorityId), expected.ExecutableAuthorityId, observed.ExecutableAuthorityId),
        (nameof(expected.ArgumentTemplates), CanonicalList(expected.ArgumentTemplates.Select(CanonicalArgument)), CanonicalList(observed.ArgumentTemplates.Select(CanonicalArgument))),
        (nameof(expected.WorkingDirectoryAuthorityId), expected.WorkingDirectoryAuthorityId, observed.WorkingDirectoryAuthorityId),
        (nameof(expected.AdapterId), expected.AdapterId, observed.AdapterId),
        (nameof(expected.AdapterContractVersion), expected.AdapterContractVersion, observed.AdapterContractVersion),
        (nameof(expected.ExpectedNativeOutputContract), expected.ExpectedNativeOutputContract, observed.ExpectedNativeOutputContract),
        (nameof(expected.TimeoutSeconds), expected.TimeoutSeconds.ToString(), observed.TimeoutSeconds.ToString()),
        (nameof(expected.StandardOutputLimitBytes), expected.StandardOutputLimitBytes.ToString(), observed.StandardOutputLimitBytes.ToString()),
        (nameof(expected.StandardErrorLimitBytes), expected.StandardErrorLimitBytes.ToString(), observed.StandardErrorLimitBytes.ToString()),
        (nameof(expected.CleanupResourceType), expected.CleanupResourceType, observed.CleanupResourceType),
        (nameof(expected.CausesRdsRead), expected.CausesRdsRead.ToString(), observed.CausesRdsRead.ToString()),
        (nameof(expected.CausesCapture), expected.CausesCapture.ToString(), observed.CausesCapture.ToString()),
        (nameof(expected.ReadsSecret), expected.ReadsSecret.ToString(), observed.ReadsSecret.ToString()),
        (nameof(expected.SecretVariableNames), CanonicalList(expected.SecretVariableNames), CanonicalList(observed.SecretVariableNames)),
        (nameof(expected.NonSecretEnvironment), Arch7bSealedNonSecretEnvironment.Canonical(expected.NonSecretEnvironment), Arch7bSealedNonSecretEnvironment.Canonical(observed.NonSecretEnvironment)),
        (nameof(expected.LongLivedProcessKey), expected.LongLivedProcessKey ?? string.Empty, observed.LongLivedProcessKey ?? string.Empty),
        (nameof(expected.EvidenceSha256), expected.EvidenceSha256, observed.EvidenceSha256)
    ];

    private static string CanonicalArgument(Arch7bCommandTemplateArgument argument) =>
        CanonicalFields(argument.Value, argument.ValueKind.ToString(),
            argument.ExpectedProducerStage ?? string.Empty, argument.MaximumAgeSeconds.ToString(),
            argument.MustBeInsideRunRoot.ToString());

    private static string CanonicalList(IEnumerable<string> values)
    {
        var materialized = values.ToArray();
        return CanonicalFields([materialized.Length.ToString(), .. materialized]);
    }

    private static string CanonicalFields(params string[] values) =>
        string.Join('\n', values.Select(value => Encoding.UTF8.GetByteCount(value) + ":" + value));

    private static Arch7bQualificationException Mismatch(int index,
        Arch7bOneShotCommandTemplate? expected, Arch7bOneShotCommandTemplate? observed,
        string fieldName, string expectedValue, string observedValue)
    {
        var commandId = expected?.CommandId ?? observed?.CommandId ?? "<projection-set>";
        var stageId = expected?.StageId ?? observed?.StageId ?? "<projection-set>";
        var provisional = new Arch7bTargetCommandProjectionMismatchEvidence(
            Arch7bV2Contracts.TargetCommandProjectionCanonicalEqualityVersion,
            index, commandId, stageId, fieldName,
            Arch7bOneShotContracts.Sha256(expectedValue),
            Arch7bOneShotContracts.Sha256(observedValue), false, string.Empty);
        var evidence = provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(string.Join('\n',
                provisional.ContractVersion, provisional.CommandIndex,
                provisional.CommandId, provisional.StageId, provisional.FieldName,
                provisional.ExpectedFieldSha256, provisional.ObservedFieldSha256,
                provisional.RawSensitiveValuesPersisted))
        };
        var detail = string.Join(';',
            $"command_index={evidence.CommandIndex}",
            $"command_id={evidence.CommandId}",
            $"stage_id={evidence.StageId}",
            $"field={evidence.FieldName}",
            $"expected_field_sha256={evidence.ExpectedFieldSha256}",
            $"observed_field_sha256={evidence.ObservedFieldSha256}",
            "raw_sensitive_values_persisted=false",
            $"evidence_sha256={evidence.EvidenceSha256}");
        return new Arch7bQualificationException(
            Arch7bV2Blockers.TargetCommandProjectionContentMismatch, detail);
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
            ValidateRequiredEnvironment(command, template.FileAuthorities);
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

    private static void ValidateRequiredEnvironment(
        Arch7bOneShotCommandTemplate command,
        IReadOnlyDictionary<string, Arch7bFileAuthority> authorities)
    {
        var catalog = Arch7bFinalStageExecutionCatalog.Require(command.StageId);
        if (!catalog.HasCommandTemplate || catalog.CommandId != command.CommandId) return;

        if (command.ExecutableAuthorityId == "supervisor_executable")
        {
            var names = command.NonSecretEnvironment.Select(value => value.VariableName).ToArray();
            if (names.Length != 1 || names[0] != "DOTNET_ROOT")
                throw new Arch7bQualificationException(
                    Arch7bV2Blockers.ApphostDotnetRootBindingMissing, command.CommandId);
            return;
        }

        if (command.StageId != "CORE_PREQUALIFICATION") return;
        var expected = Arch7bSealedNonSecretEnvironment
            .ForCorePrequalificationEnvironment(authorities).Single();
        if (command.NonSecretEnvironment.Count != 1 ||
            command.NonSecretEnvironment[0].VariableName != expected.VariableName)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandNonSecretEnvironmentVariableForbidden,
                command.CommandId);
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
