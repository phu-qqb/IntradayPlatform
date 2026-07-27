using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

internal sealed record Arch7bPostgreSqlClockQualificationEvidence(
    string OutputDirectory,
    string ManifestSha256,
    string ZipSha256,
    IReadOnlyList<string> SampleEvidenceSha256);

internal static class Arch7bPostgreSqlClockQualificationEvidenceWriter
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        };

    public static Arch7bPostgreSqlClockQualificationEvidence Write(
        string outputDirectory,
        Arch7bPostgreSqlClockQualification qualification,
        PmsShadowPostgreSqlTarget target)
    {
        var root = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(root) || File.Exists(root))
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_CLOCK_EVIDENCE_ALREADY_EXISTS");
        Directory.CreateDirectory(root);

        var sampleNames = new[] { "sample-a.json", "sample-b.json", "sample-c.json" };
        if (qualification.Samples.Count != sampleNames.Length ||
            !qualification.TransactionReadOnly ||
            !qualification.SamplesMonotonic ||
            !qualification.NoDatabaseWrite)
            throw new InvalidDataException(
                "ARCH7B_POSITION_IMPORT_CLOCK_QUALIFICATION_INVALID");
        for (var index = 0; index < sampleNames.Length; index++)
            WriteJson(Path.Combine(root, sampleNames[index]),
                qualification.Samples[index]);

        var report = $"""
            # ARCH7B PostgreSQL Database Clock Authority

            - Contract: `{qualification.ContractVersion}`
            - Target: `{target.ObservableIdentity}`
            - PostgreSQL: `{qualification.PostgreSqlVersion}`
            - Transaction read-only: `{qualification.TransactionReadOnly}`
            - Samples monotonic: `{qualification.SamplesMonotonic}`
            - Application rows written: `0`
            - Armed state created: `false`
            - Owner lock created: `false`
            - Ready marker created: `false`
            - LMAX acquisition: `false`
            - FIX or order activity: `false`

            The database timestamp is read as a typed CLR value and cross-checked
            against the Unix epoch returned from the same PostgreSQL CTE instant.
            Host timestamps are diagnostic only and never participate in freshness.
            """;
        File.WriteAllText(
            Path.Combine(root, "report.md"),
            report.ReplaceLineEndings("\n"),
            new UTF8Encoding(false));

        var files = Directory.GetFiles(root)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(path => new
            {
                path = Path.GetFileName(path),
                size_bytes = new FileInfo(path).Length,
                sha256 = FileSha(path)
            })
            .ToArray();
        var manifestPath = Path.Combine(root, "manifest.json");
        WriteJson(manifestPath, new
        {
            contract_version = qualification.ContractVersion,
            target_profile = target.TargetProfileId,
            target_fingerprint = target.TargetFingerprint,
            database = target.Database,
            postgresql_version = qualification.PostgreSqlVersion,
            transaction_read_only = qualification.TransactionReadOnly,
            samples_monotonic = qualification.SamplesMonotonic,
            sample_count = qualification.Samples.Count,
            sample_evidence_sha256 = qualification.Samples.Select(
                value => value.EvidenceSha256).ToArray(),
            files,
            no_database_write = true,
            no_armed_state = true,
            no_owner_lock = true,
            no_ready_marker = true,
            no_lmax_acquisition = true,
            no_fix = true,
            no_order = true,
            no_fill = true,
            no_position_ledger_event = true
        });
        var manifestSha256 = FileSha(manifestPath);
        var zipPath = Path.Combine(root, "evidence.zip");
        var zipEntries = sampleNames.Append("report.md").Append("manifest.json")
            .Order(StringComparer.Ordinal).ToArray();
        WriteDeterministicZip(root, zipPath, zipEntries);
        var verificationPath = Path.Combine(
            root, $".evidence.{Environment.ProcessId}.verification.zip");
        try
        {
            WriteDeterministicZip(root, verificationPath, zipEntries);
            if (!File.ReadAllBytes(zipPath)
                    .SequenceEqual(File.ReadAllBytes(verificationPath)))
                throw new InvalidDataException(
                    "ARCH7B_POSITION_IMPORT_CLOCK_ZIP_NOT_DETERMINISTIC");
        }
        finally
        {
            if (File.Exists(verificationPath))
                File.Delete(verificationPath);
        }
        var zipSha256 = FileSha(zipPath);
        File.WriteAllText(
            Path.Combine(root, "evidence.zip.sha256"),
            $"{zipSha256}  evidence.zip\n",
            new UTF8Encoding(false));
        return new(
            root,
            manifestSha256,
            zipSha256,
            qualification.Samples.Select(value => value.EvidenceSha256).ToArray());
    }

    private static void WriteJson<T>(string path, T value) =>
        File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(value, Json));

    private static string FileSha(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static void WriteDeterministicZip(
        string root,
        string path,
        IEnumerable<string> relativePaths)
    {
        using var stream = new FileStream(
            path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(
            stream, ZipArchiveMode.Create, leaveOpen: false);
        foreach (var relativePath in relativePaths.Order(StringComparer.Ordinal))
        {
            var entry = archive.CreateEntry(
                relativePath, CompressionLevel.Optimal);
            entry.LastWriteTime =
                new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using var input = File.OpenRead(Path.Combine(root, relativePath));
            using var output = entry.Open();
            input.CopyTo(output);
        }
    }
}
