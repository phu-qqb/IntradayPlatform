using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public sealed record Arch6dEvidenceVerification(
    string Arch6cEvidenceSha256,
    string Arch6bEvidenceSha256,
    string SourceSessionId,
    string RowsetSha256,
    int ReferencedArtifactCount,
    int EmbeddedArtifactCount,
    IReadOnlyDictionary<string, int> ExpectedRowCounts);

public sealed record Arch6dEvidencePackage(
    PmsShadowPersistencePlan Plan,
    Arch6dEvidenceVerification Verification);

public static class Arch6dPmsShadowEvidencePackageReader
{
    public const string SourceSessionId = "arch6b-daily-tier1-20260721T130346Z-422530a8";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() }
    };

    public static Arch6dEvidencePackage Read(
        string arch6cEvidenceZip,
        string expectedArch6cSha256,
        string arch6bEvidenceZip,
        string expectedArch6bSha256)
    {
        var arch6cSha = VerifyFile(arch6cEvidenceZip, expectedArch6cSha256, "ARCH6C_EVIDENCE_SHA_MISMATCH");
        var arch6bSha = VerifyFile(arch6bEvidenceZip, expectedArch6bSha256, "ARCH6B_EVIDENCE_SHA_MISMATCH");

        using var arch6c = ZipFile.OpenRead(arch6cEvidenceZip);
        using var arch6b = ZipFile.OpenRead(arch6bEvidenceZip);
        var plan = ReadJson<PmsShadowPersistencePlan>(arch6c, "arch6b_shadow_persistence_plan.json");

        Require(plan.Ingestion.SourceSessionId == SourceSessionId, "ARCH6B_SOURCE_SESSION_MISMATCH");
        Require(plan.Ingestion.SourceEvidenceSha256 == arch6bSha, "ARCH6B_PLAN_EVIDENCE_SHA_MISMATCH");
        Require(plan.Ingestion.ContractVersion == PmsShadowStateContract.ContractVersion, "PMS_SHADOW_SCHEMA_CONTRACT_VERSION_MISMATCH");
        var validation = Arch6cPmsShadowPersistencePlanner.Validate(plan);
        Require(validation.IsValid, $"ARCH6B_PLAN_INVALID:{string.Join(',', validation.Issues)}");

        var expectedCounts = EfPmsShadowSessionImportStore.ExpectedRowCounts(plan);
        Require(expectedCounts.Values.Sum() == 1110, "ARCH6B_TOTAL_ROW_COUNT_MISMATCH");
        Require(expectedCounts["qubes_input_snapshots"] == 4 && expectedCounts["model_runs"] == 4,
            "ARCH6B_RUN_COUNT_MISMATCH");
        Require(expectedCounts["target_weights"] == 288 && expectedCounts["target_positions"] == 288 &&
            expectedCounts["position_only_drifts"] == 288, "ARCH6B_DECISION_ROW_COUNT_MISMATCH");

        var embedded = 0;
        foreach (var artifact in plan.SourceArtifacts)
        {
            Require(IsSha256(artifact.Sha256), $"ARTIFACT_SHA_INVALID:{artifact.ArtifactId}");
            Require(artifact.SizeBytes >= 0, $"ARTIFACT_SIZE_INVALID:{artifact.ArtifactId}");
            Require(!string.IsNullOrWhiteSpace(artifact.LogicalUri), $"ARTIFACT_URI_MISSING:{artifact.ArtifactId}");
            var entryName = artifact.LogicalUri.StartsWith("arch6b/", StringComparison.Ordinal)
                ? artifact.LogicalUri[7..]
                : artifact.LogicalUri;
            var entry = arch6b.GetEntry(entryName);
            if (artifact.SizeBytes > 0)
            {
                if (entry is null)
                    throw new InvalidDataException($"ARCH6B_ARTIFACT_ENTRY_MISSING:{entryName}");
                Require(entry.Length == artifact.SizeBytes, $"ARCH6B_ARTIFACT_SIZE_MISMATCH:{entryName}");
                embedded++;
            }
        }

        return new(plan, new(arch6cSha, arch6bSha, plan.Ingestion.SourceSessionId, plan.RowsetSha256,
            plan.SourceArtifacts.Count, embedded, expectedCounts));
    }

    private static T ReadJson<T>(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidDataException($"ARCH6C_ENTRY_MISSING:{name}");
        using var stream = entry.Open();
        return JsonSerializer.Deserialize<T>(stream, Json) ??
            throw new InvalidDataException($"ARCH6C_ENTRY_INVALID:{name}");
    }

    private static string VerifyFile(string path, string expectedSha256, string issue)
    {
        Require(IsSha256(expectedSha256), $"EXPECTED_SHA_INVALID:{issue}");
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexStringLower(SHA256.HashData(stream));
        Require(actual == expectedSha256, $"{issue}:{actual}");
        return actual;
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character => char.IsAsciiHexDigit(character) && !char.IsUpper(character));

    private static void Require(bool condition, string issue)
    {
        if (!condition) throw new InvalidDataException(issue);
    }
}
