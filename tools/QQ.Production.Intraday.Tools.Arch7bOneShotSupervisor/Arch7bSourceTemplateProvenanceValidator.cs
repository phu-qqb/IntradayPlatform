using System.Security.Cryptography;
using System.Text.Json;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bSourceTemplateProvenanceValidation(
    string ContractVersion,
    string SourceTemplatePath,
    string SourceTemplateSha256,
    string IntradayCommit,
    string IntradayTree,
    int StageCount,
    int LegacyAliasCount,
    int MissingProducerCount,
    int DuplicateProducerCount,
    int FutureProducerCount,
    int ClockContractCount,
    string GraphValidationStatus,
    string GraphEvidenceSha256,
    string EvidenceSha256);

public static class Arch7bSourceTemplateProvenanceValidator
{
    public const string ContractVersion =
        "arch7b_source_template_provenance_validation_v1";

    public static async Task<Arch7bSourceTemplateProvenanceValidation> ValidateAsync(
        string sourceTemplatePath, string expectedSha256,
        string expectedIntradayCommit, string expectedIntradayTree,
        CancellationToken cancellationToken = default)
    {
        sourceTemplatePath = Path.GetFullPath(sourceTemplatePath);
        Require(Arch7bOneShotContracts.IsSha256(expectedSha256),
            "expected-source-template-sha256");
        Require(IsGitObjectId(expectedIntradayCommit),
            "expected-source-template-commit");
        Require(IsGitObjectId(expectedIntradayTree),
            "expected-source-template-tree");
        var bytes = await File.ReadAllBytesAsync(sourceTemplatePath, cancellationToken)
            .ConfigureAwait(false);
        var actualSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        Require(actualSha256 == expectedSha256, "source-template-sha256");
        var template = JsonSerializer.Deserialize<Arch7bOneShotLivePlanTemplate>(
            bytes, Arch7bJson.CanonicalOptions) ?? throw Failure("source-template-json");
        template.ValidateEvidence();
        Require(template.ContractVersion == Arch7bV2Contracts.LivePlanTemplateVersion,
            "source-template-contract-version");
        Require(template.IntradayCommit == expectedIntradayCommit,
            "source-template-intraday-commit");
        Require(template.IntradayTree == expectedIntradayTree,
            "source-template-intraday-tree");
        var graph = Arch7bStageFactGraphValidator.RequireValid(template.StageContracts);
        Require(graph.StageCount == Arch7bStages.All.Count, "source-template-stage-count");
        foreach (var clock in Arch7bClockFactContracts.All)
        {
            var producer = template.StageContracts.SingleOrDefault(value =>
                value.StageId == clock.ProducerStage);
            var consumer = template.StageContracts.SingleOrDefault(value =>
                value.StageId == clock.ConsumerStage);
            Require(producer is not null && producer.ProducedFactTypes.Count(value =>
                value == clock.FactType) == 1, $"clock-producer:{clock.ProducerStage}");
            Require(consumer is not null && consumer.RequiredFactTypes.Count(value =>
                value == clock.FactType) == 1, $"clock-consumer:{clock.ConsumerStage}");
        }

        var canonical = string.Join('\n', ContractVersion, actualSha256,
            template.IntradayCommit, template.IntradayTree, graph.StageCount,
            graph.LegacyAliasCount, graph.MissingProducerCount,
            graph.DuplicateProducerCount, graph.FutureProducerCount,
            Arch7bClockFactContracts.All.Count, graph.ValidationStatus,
            graph.EvidenceSha256);
        return new(ContractVersion, sourceTemplatePath, actualSha256,
            template.IntradayCommit, template.IntradayTree, graph.StageCount,
            graph.LegacyAliasCount, graph.MissingProducerCount,
            graph.DuplicateProducerCount, graph.FutureProducerCount,
            Arch7bClockFactContracts.All.Count, graph.ValidationStatus,
            graph.EvidenceSha256, Arch7bOneShotContracts.Sha256(canonical));
    }

    private static bool IsGitObjectId(string value) => value.Length == 40 &&
        value.All(character => char.IsAsciiHexDigit(character) && !char.IsUpper(character));

    private static void Require(bool condition, string detail)
    {
        if (!condition) throw Failure(detail);
    }

    private static Arch7bQualificationException Failure(string detail) =>
        new(Arch7bV2Blockers.SourceTemplateProvenanceMismatch, detail);
}
