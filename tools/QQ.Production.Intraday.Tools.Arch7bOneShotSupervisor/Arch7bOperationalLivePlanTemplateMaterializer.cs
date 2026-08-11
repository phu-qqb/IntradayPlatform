namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bOperationalTemplateMaterialization(
    Arch7bOneShotLivePlanTemplate Template,
    int CommandCount,
    int BindingCount,
    int UnresolvedBindingCount,
    int SyntheticCommandCount,
    string EvidenceSha256);

public sealed record Arch7bOperationalTemplateFileMaterialization(
    string ContractVersion,
    string SourceTemplatePath,
    string SourceTemplateSha256,
    string OutputPath,
    string OutputSha256,
    string TemplateEvidenceSha256,
    int CommandCount,
    int BindingCount,
    int RegenerateCount,
    int FakeNativeChildCount,
    int SyntheticAuthorityCount,
    int UnresolvedProducerCount,
    bool ReadbackIdentical,
    string EvidenceSha256);

public static class Arch7bOperationalLivePlanTemplateMaterializer
{
    public const string Version = "arch7b_operational_live_plan_template_materializer_v1";
    public const string FileVersion =
        "arch7b_operational_live_plan_template_file_materialization_v1";

    public static Arch7bOperationalTemplateMaterialization Materialize(
        Arch7bOneShotLivePlanTemplate skeleton,
        byte[] sourceCommandManifestBytes)
    {
        var inventory = Arch7bOperationalLiveFactBindingCatalog.InventoryMarkers(
            Arch7bOperationalCatalogMaterializer.SourceManifestLabel,
            sourceCommandManifestBytes);
        var catalog = Arch7bOperationalLiveFactBindingCatalog.Build();
        var classification = Arch7bFinalStageExecutionCatalog.All;
        var expectedCommandStages = classification.Where(value => value.HasCommandTemplate)
            .Select(value => value.StageId).ToHashSet(StringComparer.Ordinal);
        var stageOrder = Arch7bStages.All.Select((stage, index) => (stage, index))
            .ToDictionary(value => value.stage, value => value.index,
                StringComparer.Ordinal);
        var commandByStage = skeleton.CommandTemplates.ToDictionary(
            value => value.StageId, StringComparer.Ordinal);
        var replacements = new Dictionary<string, Arch7bOneShotCommandTemplate>(
            StringComparer.Ordinal);
        foreach (var commandCatalog in catalog)
        {
            if (!commandByStage.TryGetValue(commandCatalog.StageId, out var prototype))
            {
                var source = skeleton.CommandTemplates.SingleOrDefault(value =>
                    value.CommandId == commandCatalog.CommandId);
                if (source is null && (commandCatalog.StageId != "CLOCK_CAPTURE_START" ||
                    !commandByStage.TryGetValue("PMS_IMPORT", out source)))
                    throw new Arch7bQualificationException(
                        Arch7bV2Blockers.CommandTemplateInvalid, commandCatalog.CommandId);
                var entry = Arch7bFinalStageExecutionCatalog.Require(commandCatalog.StageId);
                var reusesExistingCommand = source.CommandId == commandCatalog.CommandId;
                var arguments = reusesExistingCommand
                    ? new List<Arch7bCommandTemplateArgument>(source.ArgumentTemplates)
                    : new List<Arch7bCommandTemplateArgument>
                    {
                        new("--mode", Arch7bPlaceholderValueKind.Literal, null, -1, false),
                        new(commandCatalog.Mode, Arch7bPlaceholderValueKind.Literal, null, -1, false)
                    };
                if (!reusesExistingCommand)
                {
                    foreach (var binding in commandCatalog.Bindings)
                    {
                        arguments.Add(new(binding.ArgumentName,
                            Arch7bPlaceholderValueKind.Literal, null, -1, false));
                        arguments.Add(new(Arch7bOperationalLiveFactBindingCatalog.Marker,
                            Arch7bPlaceholderValueKind.Literal, null, -1, false));
                    }
                }
                prototype = source with
                {
                    CommandId = commandCatalog.CommandId,
                    StageId = commandCatalog.StageId,
                    ExecutionKind = entry.ExecutionKind,
                    ArgumentTemplates = arguments,
                    CausesRdsRead = false,
                    CausesCapture = commandCatalog.StageId == "MARKET_CAPTURE",
                    LongLivedProcessKey = null,
                    EvidenceSha256 = string.Empty
                };
            }
            replacements.Add(commandCatalog.StageId,
                BindCommand(prototype, commandCatalog));
        }

        if (!commandByStage.TryGetValue("CORE_PREQUALIFICATION", out var corePrototype))
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandTemplateInvalid, "CORE_PREQUALIFICATION");
        replacements["CORE_PREQUALIFICATION"] = BindDirectCorePrequalification(corePrototype);

        var commands = skeleton.CommandTemplates
            .Where(command => expectedCommandStages.Contains(command.StageId))
            .Select(command => replacements.GetValueOrDefault(command.StageId) ?? command)
            .Concat(replacements.Where(value => !commandByStage.ContainsKey(value.Key))
                .Select(value => value.Value))
            .OrderBy(command => stageOrder[command.StageId])
            .ToArray();
        if (commands.Length != Arch7bFinalStageExecutionCatalog.CommandTemplateCount ||
            !commands.Select(value => value.StageId).ToHashSet(StringComparer.Ordinal)
                .SetEquals(expectedCommandStages))
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandTemplateInvalid, "final-stage-classification");
        var commandSet = Arch7bOneShotContracts.Sha256(string.Join('\n',
            commands.Select(value => value.EvidenceSha256)));
        var stages = BindStageContracts(skeleton.StageContracts, catalog, commands);
        var provisional = skeleton with
        {
            CommandTemplates = commands,
            StageContracts = stages,
            CommandTemplateSetSha256 = commandSet,
            EvidenceSha256 = string.Empty
        };
        var template = provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(provisional.Canonical())
        };
        var text = System.Text.Json.JsonSerializer.Serialize(template,
            Arch7bJson.CanonicalOptions);
        var unresolved = text.Contains(Arch7bOperationalLiveFactBindingCatalog.Marker,
            StringComparison.Ordinal) ? 1 : 0;
        var synthetic = Count(text, "fake-native-child") + Count(text, "fake-child") +
            Count(text, "offline-qualified-child") + Count(text, "unsupported");
        if (inventory.MarkerCount != catalog.Sum(value => value.Bindings.Count) ||
            unresolved != 0 || synthetic != 0)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandTemplateInvalid, "operational-template");
        Arch7bLiveTemplateValidator.Validate(template, new Arch7bRealCommandAdapterRegistry());
        return new(template, commands.Length, inventory.MarkerCount, unresolved, synthetic,
            Arch7bOneShotContracts.Sha256(string.Join('\n', Version,
                template.EvidenceSha256, inventory.EvidenceSha256,
                string.Join('|', catalog.SelectMany(value => value.Bindings)
                    .Select(value => value.EvidenceSha256)))));
    }

    public static async Task<Arch7bOperationalTemplateFileMaterialization> WriteAsync(
        string sourceTemplatePath,
        string sourceManifestPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        sourceTemplatePath = Path.GetFullPath(sourceTemplatePath);
        sourceManifestPath = Path.GetFullPath(sourceManifestPath);
        outputPath = Path.GetFullPath(outputPath);
        var sourceTemplateBytes = await File.ReadAllBytesAsync(sourceTemplatePath,
            cancellationToken).ConfigureAwait(false);
        var sourceManifestBytes = await File.ReadAllBytesAsync(sourceManifestPath,
            cancellationToken).ConfigureAwait(false);
        var skeleton = System.Text.Json.JsonSerializer.Deserialize<
            Arch7bOneShotLivePlanTemplate>(sourceTemplateBytes, Arch7bJson.CanonicalOptions)
            ?? throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandTemplateInvalid, "source-template");
        skeleton.ValidateEvidence();
        var materialized = Materialize(skeleton, sourceManifestBytes);
        var outputBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            materialized.Template, Arch7bJson.CanonicalOptions);
        var outputText = System.Text.Encoding.UTF8.GetString(outputBytes);
        var regenerateCount = Count(outputText,
            Arch7bOperationalLiveFactBindingCatalog.Marker);
        var fakeNativeChildCount = Count(outputText, "fake-native-child") +
            Count(outputText, "fake-child");
        var syntheticAuthorityCount = Count(outputText, "synthetic-authority") +
            Count(outputText, "synthetic_authority");
        var unresolvedProducerCount = Arch7bOperationalBindingProducerAudit.Build()
            .MissingProducerCount;
        if (regenerateCount != 0 || fakeNativeChildCount != 0 ||
            syntheticAuthorityCount != 0 || unresolvedProducerCount != 0)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandTemplateInvalid, "operational-template-counts");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await using (var stream = new FileStream(outputPath, FileMode.CreateNew,
                         FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            await stream.WriteAsync(outputBytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        var readback = await File.ReadAllBytesAsync(outputPath, cancellationToken)
            .ConfigureAwait(false);
        var identical = outputBytes.AsSpan().SequenceEqual(readback);
        if (!identical)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.AuthorityBindingMismatch, "template-readback");
        var sourceSha = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(sourceTemplateBytes));
        var outputSha = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(outputBytes));
        var evidence = Arch7bOneShotContracts.Sha256(string.Join('\n', FileVersion,
            sourceTemplatePath, sourceSha, outputPath, outputSha,
            materialized.Template.EvidenceSha256, materialized.CommandCount,
            materialized.BindingCount, regenerateCount, fakeNativeChildCount,
            syntheticAuthorityCount, unresolvedProducerCount, identical));
        return new(FileVersion, sourceTemplatePath, sourceSha, outputPath, outputSha,
            materialized.Template.EvidenceSha256, materialized.CommandCount,
            materialized.BindingCount, regenerateCount, fakeNativeChildCount,
            syntheticAuthorityCount, unresolvedProducerCount, identical, evidence);
    }

    private static Arch7bOneShotCommandTemplate BindCommand(
        Arch7bOneShotCommandTemplate prototype,
        Arch7bOperationalCommandBindingSet commandCatalog)
    {
        var arguments = prototype.ArgumentTemplates.ToArray();
        var used = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Length; index++)
        {
            if (arguments[index].Value !=
                Arch7bOperationalLiveFactBindingCatalog.Marker) continue;
            if (index == 0 || !arguments[index - 1].Value.StartsWith("--",
                    StringComparison.Ordinal) ||
                arguments[index - 1].ValueKind != Arch7bPlaceholderValueKind.Literal)
                throw new Arch7bQualificationException(
                    Arch7bV2Blockers.CommandTemplateInvalid, prototype.CommandId);
            var argumentName = arguments[index - 1].Value;
            var binding = commandCatalog.Bindings.SingleOrDefault(value =>
                value.ArgumentName == argumentName)
                ?? throw new Arch7bQualificationException(
                    Arch7bV2Blockers.RequiredFactMissing,
                    prototype.CommandId + ":" + argumentName);
            arguments[index] = new(binding.Placeholder, binding.ValueKind,
                binding.PlaceholderScope == Arch7bOperationalPlaceholderScope.Authority
                    ? null : binding.RequiredProducerStage,
                binding.MaximumAgeSeconds, binding.MustBeInsideRunRoot);
            used.Add(binding.BindingId);
        }
        if (used.Count != commandCatalog.Bindings.Count)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.CommandTemplateInvalid,
                prototype.CommandId + ":unused-binding");

        var provisional = prototype with
        {
            ArgumentTemplates = arguments,
            EvidenceSha256 = string.Empty
        };
        var canonical = string.Join('\n', Arch7bV2Contracts.CommandTemplateVersion,
            provisional.CommandId, provisional.StageId, provisional.ExecutionKind,
            provisional.ExecutableAuthorityId,
            string.Join('|', provisional.ArgumentTemplates.Select(value =>
                $"{value.Value}:{value.ValueKind}:{value.ExpectedProducerStage}:" +
                $"{value.MaximumAgeSeconds}:{value.MustBeInsideRunRoot}")),
            provisional.WorkingDirectoryAuthorityId, provisional.AdapterId,
            provisional.AdapterContractVersion, provisional.ExpectedNativeOutputContract,
            provisional.TimeoutSeconds, provisional.StandardOutputLimitBytes,
            provisional.StandardErrorLimitBytes, provisional.CleanupResourceType,
            provisional.CausesRdsRead, provisional.CausesCapture, provisional.ReadsSecret,
            string.Join('|', provisional.SecretVariableNames),
            Arch7bSealedNonSecretEnvironment.Canonical(provisional.NonSecretEnvironment),
            provisional.LongLivedProcessKey ?? string.Empty,
            string.Join('|', commandCatalog.Bindings.Select(value => value.EvidenceSha256)));
        return provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(canonical)
        };
    }

    private static IReadOnlyList<Arch7bOneShotStageContract> BindStageContracts(
        IReadOnlyList<Arch7bOneShotStageContract> stages,
        IReadOnlyList<Arch7bOperationalCommandBindingSet> catalog,
        IReadOnlyList<Arch7bOneShotCommandTemplate> commands)
    {
        var bindings = catalog.SelectMany(value => value.Bindings).ToArray();
        return stages.Select(stage =>
        {
            var required = stage.RequiredFactTypes.Concat(bindings.Where(value =>
                    value.StageId == stage.StageId &&
                    value.PlaceholderScope != Arch7bOperationalPlaceholderScope.Authority)
                .Select(value => value.PlaceholderName))
                .Distinct(StringComparer.Ordinal).ToArray();
            string[] stageRequired = stage.StageId switch
            {
                "POSITION_MARKET_DRAFT" => ["runtime_selection_artifact"],
                "CORE_PREQUALIFICATION" => ["core_prequalification_config"],
                _ => []
            };
            string[] stageProduced = stage.StageId switch
            {
                "RUNTIME_SELECTION" => ["runtime_selection_artifact"],
                "SLOT_LOCKED" => ["core_prequalification_config"],
                _ => []
            };
            required = required.Concat(stageRequired)
                .Distinct(StringComparer.Ordinal).ToArray();
            var produced = stage.ProducedFactTypes.Concat(bindings.Where(value =>
                    value.RequiredProducerStage == stage.StageId)
                .Select(value => value.PlaceholderName)).Concat(stageProduced)
                .Distinct(StringComparer.Ordinal).ToArray();
            var kind = Arch7bFinalStageExecutionCatalog.Require(stage.StageId)
                .ExecutionKind;
            var provisional = stage with
            {
                ExecutionKind = kind,
                RequiredFactTypes = required,
                ProducedFactTypes = produced,
                EvidenceSha256 = string.Empty
            };
            var canonical = string.Join('\n', provisional.StageId,
                provisional.ExecutionKind, string.Join('|', provisional.Predecessors),
                string.Join('|', required), string.Join('|', produced),
                provisional.SloId ?? string.Empty, provisional.ValidatorId);
            return provisional with
            {
                EvidenceSha256 = Arch7bOneShotContracts.Sha256(canonical)
            };
        }).ToArray();
    }

    private static Arch7bOneShotCommandTemplate BindDirectCorePrequalification(
        Arch7bOneShotCommandTemplate prototype)
    {
        var provisional = prototype with
        {
            ExecutableAuthorityId = "node_executable",
            WorkingDirectoryAuthorityId = "core_node_runtime",
            ArgumentTemplates =
            [
                new("src/fast-seal-cli.mjs", Arch7bPlaceholderValueKind.Literal,
                    null, -1, false),
                new("prequalify-bracket-runtime", Arch7bPlaceholderValueKind.Literal,
                    null, -1, false),
                new("--config", Arch7bPlaceholderValueKind.Literal,
                    null, -1, false),
                new("${fact:core_prequalification_config.path}",
                    Arch7bPlaceholderValueKind.AbsolutePath, "SLOT_LOCKED",
                    int.MaxValue, true)
            ],
            EvidenceSha256 = string.Empty
        };
        var canonical = string.Join('\n', Arch7bV2Contracts.CommandTemplateVersion,
            provisional.CommandId, provisional.StageId, provisional.ExecutionKind,
            provisional.ExecutableAuthorityId,
            string.Join('|', provisional.ArgumentTemplates.Select(value =>
                $"{value.Value}:{value.ValueKind}:{value.ExpectedProducerStage}:" +
                $"{value.MaximumAgeSeconds}:{value.MustBeInsideRunRoot}")),
            provisional.WorkingDirectoryAuthorityId, provisional.AdapterId,
            provisional.AdapterContractVersion,
            provisional.ExpectedNativeOutputContract, provisional.TimeoutSeconds,
            provisional.StandardOutputLimitBytes, provisional.StandardErrorLimitBytes,
            provisional.CleanupResourceType, provisional.CausesRdsRead,
            provisional.CausesCapture, provisional.ReadsSecret,
            string.Join('|', provisional.SecretVariableNames),
            Arch7bSealedNonSecretEnvironment.Canonical(
                provisional.NonSecretEnvironment),
            provisional.LongLivedProcessKey ?? string.Empty);
        return provisional with
        {
            EvidenceSha256 = Arch7bOneShotContracts.Sha256(canonical)
        };
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }
}
