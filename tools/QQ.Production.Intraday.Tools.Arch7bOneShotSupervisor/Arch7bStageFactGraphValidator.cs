using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bStageFactRequirementInventory(
    string FactType,
    string? ProducerStage,
    int ProducerIndex,
    int ConsumerIndex,
    string Status);

public sealed record Arch7bStageFactInventoryEntry(
    string StageId,
    IReadOnlyList<string> RequiredFactTypes,
    IReadOnlyList<string> ProducedFactTypes,
    IReadOnlyList<Arch7bStageFactRequirementInventory> Requirements,
    string Status);

public sealed record Arch7bStageFactGraphValidation(
    string ContractVersion,
    int StageCount,
    int ProducedFactReferenceCount,
    int RequiredFactReferenceCount,
    int UniqueFactTypeCount,
    int DuplicateProducerCount,
    int MissingProducerCount,
    int FutureProducerCount,
    int LegacyAliasCount,
    string ValidationStatus,
    IReadOnlyList<Arch7bStageFactInventoryEntry> Stages,
    string EvidenceSha256);

public static class Arch7bStageFactGraphValidator
{
    public const string ContractVersion = "arch7b_stage_fact_graph_validation_v1";
    public const string InventoryFileName =
        "arch7b-final-stage-fact-graph-inventory-v1.json";

    public static Arch7bStageFactGraphValidation Analyze(
        IReadOnlyList<Arch7bOneShotStageContract> stages)
    {
        var producers = stages.SelectMany((stage, index) =>
                stage.ProducedFactTypes.Select(factType =>
                    (FactType: factType, Stage: stage.StageId, Index: index)))
            .Where(value => !string.IsNullOrWhiteSpace(value.FactType))
            .GroupBy(value => value.FactType, StringComparer.Ordinal)
            .ToDictionary(value => value.Key, value => value.ToArray(),
                StringComparer.Ordinal);
        var duplicateProducerCount = producers.Count(value => value.Value.Length != 1);
        var missingProducerCount = 0;
        var futureProducerCount = 0;
        var legacyAliasCount = stages.Sum(stage => stage.RequiredFactTypes.Count(
                Arch7bClockFactContracts.LegacyAliases.Contains) +
            stage.ProducedFactTypes.Count(Arch7bClockFactContracts.LegacyAliases.Contains));
        var inventory = new List<Arch7bStageFactInventoryEntry>(stages.Count);

        for (var consumerIndex = 0; consumerIndex < stages.Count; consumerIndex++)
        {
            var stage = stages[consumerIndex];
            var requirements = new List<Arch7bStageFactRequirementInventory>();
            foreach (var factType in stage.RequiredFactTypes)
            {
                if (string.IsNullOrWhiteSpace(factType) ||
                    !producers.TryGetValue(factType, out var matches))
                {
                    missingProducerCount++;
                    requirements.Add(new(factType, null, -1, consumerIndex,
                        "MISSING_PRODUCER"));
                    continue;
                }

                if (matches.Length != 1)
                {
                    requirements.Add(new(factType,
                        string.Join('|', matches.Select(value => value.Stage)), -1,
                        consumerIndex, "DUPLICATE_PRODUCER"));
                    continue;
                }

                var producer = matches[0];
                var status = producer.Index < consumerIndex
                    ? "PASS"
                    : "PRODUCER_NOT_BEFORE_CONSUMER";
                if (status != "PASS") futureProducerCount++;
                requirements.Add(new(factType, producer.Stage, producer.Index,
                    consumerIndex, status));
            }

            var stageStatus = requirements.All(value => value.Status == "PASS") &&
                !stage.RequiredFactTypes.Any(Arch7bClockFactContracts.LegacyAliases.Contains) &&
                !stage.ProducedFactTypes.Any(Arch7bClockFactContracts.LegacyAliases.Contains) &&
                stage.ProducedFactTypes.All(value => !string.IsNullOrWhiteSpace(value))
                ? "PASS"
                : "NO_GO";
            inventory.Add(new(stage.StageId, stage.RequiredFactTypes,
                stage.ProducedFactTypes, requirements, stageStatus));
        }

        missingProducerCount += stages.Sum(stage =>
            stage.ProducedFactTypes.Count(string.IsNullOrWhiteSpace));
        var validationStatus = stages.Count == Arch7bStages.All.Count &&
            stages.Select(value => value.StageId).SequenceEqual(
                Arch7bStages.All, StringComparer.Ordinal) &&
            duplicateProducerCount == 0 && missingProducerCount == 0 &&
            futureProducerCount == 0 && legacyAliasCount == 0
            ? "PASS"
            : "NO_GO";
        var canonical = string.Join('\n', ContractVersion, stages.Count,
            stages.Sum(value => value.ProducedFactTypes.Count),
            stages.Sum(value => value.RequiredFactTypes.Count), producers.Count,
            duplicateProducerCount, missingProducerCount, futureProducerCount,
            legacyAliasCount, validationStatus,
            string.Join('\n', inventory.Select(stage => string.Join('|',
                stage.StageId, string.Join(',', stage.RequiredFactTypes),
                string.Join(',', stage.ProducedFactTypes), stage.Status,
                string.Join(';', stage.Requirements.Select(value => string.Join(':',
                    value.FactType, value.ProducerStage ?? string.Empty,
                    value.ProducerIndex, value.ConsumerIndex, value.Status)))))));
        return new(ContractVersion, stages.Count,
            stages.Sum(value => value.ProducedFactTypes.Count),
            stages.Sum(value => value.RequiredFactTypes.Count), producers.Count,
            duplicateProducerCount, missingProducerCount, futureProducerCount,
            legacyAliasCount, validationStatus, inventory,
            Arch7bOneShotContracts.Sha256(canonical));
    }

    public static Arch7bStageFactGraphValidation RequireValid(
        IReadOnlyList<Arch7bOneShotStageContract> stages)
    {
        var result = Analyze(stages);
        if (result.LegacyAliasCount != 0)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.LegacyStageFactAliasPresent,
                Detail(result, value => Arch7bClockFactContracts.LegacyAliases.Contains(
                    value.FactType)));
        if (result.DuplicateProducerCount != 0)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.StageFactDuplicateProducer,
                Detail(result, value => value.Status == "DUPLICATE_PRODUCER"));
        if (result.MissingProducerCount != 0 ||
            result.StageCount != Arch7bStages.All.Count)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.StageFactRequiredProducerMissing,
                Detail(result, value => value.Status == "MISSING_PRODUCER"));
        if (result.FutureProducerCount != 0)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.StageFactProducerNotBeforeConsumer,
                Detail(result, value => value.Status ==
                    "PRODUCER_NOT_BEFORE_CONSUMER"));
        if (result.ValidationStatus != "PASS")
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.StageFactRequiredProducerMissing);
        return result;
    }

    private static string Detail(Arch7bStageFactGraphValidation result,
        Func<Arch7bStageFactRequirementInventory, bool> predicate)
    {
        var stage = result.Stages.FirstOrDefault(value =>
            value.Requirements.Any(predicate));
        var requirement = stage?.Requirements.FirstOrDefault(predicate);
        return requirement is null ? "stage-fact-graph" : string.Join(':',
            requirement.FactType, requirement.ProducerStage ?? "missing", stage!.StageId);
    }

    public static byte[] SerializeInventory(
        Arch7bStageFactGraphValidation validation) =>
        JsonSerializer.SerializeToUtf8Bytes(validation, Arch7bJson.CanonicalOptions);
}
