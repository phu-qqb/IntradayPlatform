using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace QQ.Production.Intraday.Application;

public sealed record Arch5bVerifiedOutput(
    string StrategyId,
    string SourceRunId,
    string LogicalRunId,
    string OutputRelativePath,
    string OutputSha256,
    long OutputSizeBytes,
    int DataRowCount,
    int SecurityIdCount,
    DateTimeOffset TargetCloseUtc,
    decimal BenchmarkParameter,
    bool R083Passed,
    bool TransferVerified);

public sealed record Arch5bArch5aEvidenceVerification(
    string EvidenceZipFileName,
    string EvidenceZipSha256,
    bool EvidenceZipHashVerified,
    string SourceSessionId,
    string SourceMasterSha,
    string RunnerPackageSha256,
    string BundleArchiveSha256,
    string BundleVersionId,
    int SessionCount,
    int RunCount,
    IReadOnlyList<Arch5bVerifiedOutput> Outputs,
    bool CrossManifestLineageVerified,
    bool FullOutputsVerified,
    bool FinalSuccess);

public sealed record Arch5bArch5aEvidenceLoadResult(
    Arch5bSessionLineageContractV1 Contract,
    Arch5bArch5aEvidenceVerification Verification);

public sealed class Arch5bArch5aEvidenceLoader
{
    public const string EvidenceZipFileName = "arch5a-industrialize-daily-tier1-gpu-session-single-start-multi-run-end-of-day-stop-no-order-evidence.zip";

    private static readonly IReadOnlyDictionary<string, decimal> ExpectedBenchmarks = new Dictionary<string, decimal>(StringComparer.Ordinal)
    {
        ["INFX7"] = 4.5m,
        ["INFX8"] = 2.1m,
        ["INFX9"] = 1.4m,
        ["INFX10"] = 0.6m
    };

    public Arch5bArch5aEvidenceLoadResult Load(string evidenceRoot, string expectedEvidenceZipSha256)
    {
        if (string.IsNullOrWhiteSpace(evidenceRoot) || !Directory.Exists(evidenceRoot))
        {
            throw new InvalidDataException("ARCH5A_EVIDENCE_ROOT_MISSING");
        }
        if (!Arch5bHashing.IsSha256(expectedEvidenceZipSha256))
        {
            throw new InvalidDataException("ARCH5A_EXPECTED_ZIP_SHA_INVALID");
        }

        var zipPath = Path.Combine(evidenceRoot, EvidenceZipFileName);
        RequireFile(zipPath, "ARCH5A_EVIDENCE_ZIP_MISSING");
        var zipSha = FileSha256(zipPath);
        if (!zipSha.Equals(expectedEvidenceZipSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("ARCH5A_EVIDENCE_HASH_MISMATCH");
        }

        using var sessionDocument = ReadJson(Path.Combine(evidenceRoot, "daily_session_manifest.json"));
        using var outputDocument = ReadJson(Path.Combine(evidenceRoot, "per_run_output_manifest.json"));
        using var transferDocument = ReadJson(Path.Combine(evidenceRoot, "per_run_transfer_manifest.json"));
        var session = sessionDocument.RootElement;
        var outputManifest = outputDocument.RootElement;
        var transferManifest = transferDocument.RootElement;

        RequireString(session, "session_contract_version", "anubis_daily_gpu_session_v1");
        RequireString(outputManifest, "schema", "arch5a_per_run_output_manifest_v1");
        RequireString(transferManifest, "schema", "arch5a_per_run_transfer_manifest_v1");
        RequireTrue(session, "safety", "order_entry_enabled", expected: false);
        RequireString(session.GetProperty("safety"), "broker_send_status", "DISABLED_NO_ORDER_ENTRY");
        RequireTrue(session, "safety", "db_apply", expected: false);
        RequireTrue(session, "safety", "real_account_operational_use", expected: false);
        RequireTrue(outputManifest, "final_success", expected: true);
        RequireTrue(transferManifest, "final_success", expected: true);

        var sessionId = RequiredString(session, "session_id");
        var masterSha = RequiredGitCommit(session, "master_sha");
        var packageSha = RequiredSha(session, "package_sha256");
        var bundleSha = RequiredSha(session, "bundle_archive_sha256");
        var bundleVersion = RequiredString(session, "bundle_version_id");
        var previewGeneratedAt = RequiredUtc(session, "final_stopped_at");
        if (RequiredString(transferManifest, "session_id") != sessionId)
        {
            throw new InvalidDataException("TRANSFER_SESSION_ID_MISMATCH");
        }

        var sessionRuns = session.GetProperty("runs").EnumerateArray().ToArray();
        var outputEntries = outputManifest.GetProperty("outputs").EnumerateArray().ToArray();
        var transferEntries = transferManifest.GetProperty("transfers").EnumerateArray().ToArray();
        if (sessionRuns.Length != 4 || outputEntries.Length != 4 || transferEntries.Length != 4)
        {
            throw new InvalidDataException("ARCH5A_FOUR_RUN_MANIFESTS_REQUIRED");
        }

        var contracts = new List<Arch5bRunLineageContractV1>();
        var verified = new List<Arch5bVerifiedOutput>();
        foreach (var strategy in ExpectedBenchmarks.Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            var sessionRun = SingleByStrategy(sessionRuns, strategy, "SESSION_RUN_STRATEGY_MISSING_OR_DUPLICATE");
            var outputEntry = SingleByStrategy(outputEntries, strategy, "OUTPUT_STRATEGY_MISSING_OR_DUPLICATE");
            var transferEntry = SingleByStrategy(transferEntries, strategy, "TRANSFER_STRATEGY_MISSING_OR_DUPLICATE");
            using var runDocument = ReadJson(Path.Combine(evidenceRoot, strategy.ToLowerInvariant() + "_run_manifest.json"));
            using var weightsValidationDocument = ReadJson(Path.Combine(evidenceRoot, "outputs", strategy, "aggregated_weights_validation.json"));
            var run = runDocument.RootElement;
            var weightsValidation = weightsValidationDocument.RootElement;

            RequireString(run, "schema", "arch5a_run_manifest_v1");
            RequireString(run, "session_id", sessionId);
            RequireString(run, "strategy", strategy);
            RequireString(run, "master_sha", masterSha);
            RequireString(run, "package_sha", packageSha);
            RequireString(run, "bundle_manifest_sha", bundleSha);
            RequireString(run, "final_run_status", "SUCCESS");
            RequireTrue(run, "semantic_validation", "final_success", expected: true);
            RequireTrue(run, "no_order_status", "order_entry_enabled", expected: false);
            RequireTrue(run, "no_order_status", "db_apply", expected: false);
            RequireTrue(run, "no_order_status", "real_account_operational_use", expected: false);
            RequireTrue(run, "r083_comparison", "final_success", expected: true);
            RequireTrue(run, "transfer_status", "complete", expected: true);
            RequireTrue(outputEntry, "finite", expected: true);
            RequireTrue(transferEntry, "complete", expected: true);
            RequireTrue(weightsValidation, "parseable", expected: true);
            RequireTrue(weightsValidation, "final_success", expected: true);
            RequireString(weightsValidation, "schema", "aggregated_weights_validation_v1");

            var sourceRunId = RequiredString(run, "run_id");
            var logicalRunId = $"{sourceRunId}:{strategy}";
            var rawBenchmark = ParseDecimalString(run, "benchmark_parameter");
            var benchmark = ExpectedBenchmarks[strategy];
            if (Math.Abs(rawBenchmark - benchmark) > 0.000000000001m)
            {
                throw new InvalidDataException("BENCHMARK_PARAMETER_DIVERGENT");
            }
            var outputRelativePath = RequiredString(outputEntry, "relative_path").Replace('\\', '/');
            var expectedRelativePath = $"outputs/{strategy}/AggregatedWeights.txt";
            if (outputRelativePath != expectedRelativePath || RequiredString(run, "output_path").Replace('\\', '/') != expectedRelativePath)
            {
                throw new InvalidDataException("OUTPUT_STRATEGY_OR_PATH_MISMATCH");
            }
            var outputPath = Path.Combine(evidenceRoot, outputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            RequireFile(outputPath, "QUBES_OUTPUT_MISSING");
            var outputSha = RequiredSha(outputEntry, "sha256");
            var outputSize = outputEntry.GetProperty("size_bytes").GetInt64();
            if (!FileSha256(outputPath).Equals(outputSha, StringComparison.OrdinalIgnoreCase) || new FileInfo(outputPath).Length != outputSize)
            {
                throw new InvalidDataException("QUBES_OUTPUT_HASH_MISMATCH");
            }
            if (RequiredSha(run, "output_sha") != outputSha || run.GetProperty("output_size").GetInt64() != outputSize ||
                RequiredSha(sessionRun, "output_sha256") != outputSha || RequiredSha(weightsValidation, "sha256") != outputSha)
            {
                throw new InvalidDataException("RUN_MANIFEST_LINEAGE_INCOMPLETE");
            }

            var materialDifferenceCount = run.GetProperty("r083_comparison").GetProperty("material_difference_count").GetInt32();
            var signFlipCount = run.GetProperty("r083_comparison").GetProperty("sign_flip_count").GetInt32();
            if (materialDifferenceCount != 0 || signFlipCount != 0 ||
                outputEntry.GetProperty("material_difference_count").GetInt32() != 0 || outputEntry.GetProperty("sign_flip_count").GetInt32() != 0)
            {
                throw new InvalidDataException("R083_MATERIAL_OR_SIGN_REGRESSION");
            }

            var transferVerified = transferEntry.GetProperty("complete").GetBoolean() &&
                run.GetProperty("transfer_status").GetProperty("complete").GetBoolean() &&
                RequiredSha(transferEntry, "evidence_zip_sha256") == RequiredSha(run.GetProperty("transfer_status"), "evidence_zip_sha256") &&
                RequiredSha(sessionRun, "evidence_zip_sha256") == RequiredSha(transferEntry, "evidence_zip_sha256");
            if (!transferVerified)
            {
                throw new InvalidDataException("TRANSFER_INCOMPLETE");
            }

            var parsed = new Arch5bAggregatedWeightsParser().Parse(outputPath, outputSha);
            if (parsed.DataRowCount != outputEntry.GetProperty("rows").GetInt32() ||
                parsed.SecurityIdCount + 1 != outputEntry.GetProperty("columns").GetInt32() ||
                parsed.DataRowCount != weightsValidation.GetProperty("data_row_count").GetInt32() ||
                parsed.SecurityIdCount != weightsValidation.GetProperty("header_security_id_count").GetInt32())
            {
                throw new InvalidDataException("QUBES_OUTPUT_DECLARED_SHAPE_MISMATCH");
            }

            var producedAt = RequiredUtc(run, "end_utc");
            var executableSha = RequiredSha(run, "executable_sha");
            contracts.Add(new Arch5bRunLineageContractV1(
                Arch5bLineageContractVersions.LineageV1,
                Arch5bLineageContractVersions.SourceQubesWeightsOutputV1,
                sessionId,
                sourceRunId,
                logicalRunId,
                strategy,
                benchmark,
                masterSha,
                packageSha,
                bundleSha,
                bundleVersion,
                executableSha,
                outputSha,
                outputSize,
                outputRelativePath,
                Arch5bLineageContractVersions.OutputQubesWeightsOutputV1,
                producedAt,
                parsed.TargetCloseUtc,
                parsed.TargetCloseUtc,
                parsed.TargetCloseSourceValue,
                "PRODMANAGERV4_LAST_CHRONOLOGICAL_DATA_ROW",
                "PASS",
                materialDifferenceCount,
                signFlipCount,
                transferVerified,
                MarketDataSnapshotId: null,
                MarketDataSnapshotEvidenceSha256: null,
                Arch5bLineageContractVersions.MissingMarketDataSnapshot,
                Arch5bLineageContractVersions.EvidenceOnlyClassification,
                EvidenceOnlyNonAccounting: true,
                AccountingEligible: false,
                ExecutionAllowed: false,
                parsed.TargetCloseWeights));
            verified.Add(new Arch5bVerifiedOutput(
                strategy,
                sourceRunId,
                logicalRunId,
                outputRelativePath,
                outputSha,
                outputSize,
                parsed.DataRowCount,
                parsed.SecurityIdCount,
                parsed.TargetCloseUtc,
                benchmark,
                R083Passed: true,
                TransferVerified: true));
        }

        var contract = new Arch5bSessionLineageContractV1(
            Arch5bLineageContractVersions.LineageV1,
            Arch5bLineageContractVersions.SourceQubesWeightsOutputV1,
            sessionId,
            Arch5bLineageContractVersions.TestAccountId,
            Arch5bLineageContractVersions.TestAccountScope,
            masterSha,
            packageSha,
            bundleSha,
            bundleVersion,
            previewGeneratedAt,
            Arch5bLineageContractVersions.EvidenceOnlyClassification,
            EvidenceOnlyNonAccounting: true,
            AccountingEligible: false,
            ExecutionAllowed: false,
            contracts);
        var contractValidation = new Arch5bLineageContractValidator().Validate(contract);
        if (!contractValidation.IsValid)
        {
            throw new InvalidDataException(string.Join(";", contractValidation.Issues));
        }

        return new Arch5bArch5aEvidenceLoadResult(
            contract,
            new Arch5bArch5aEvidenceVerification(
                EvidenceZipFileName,
                zipSha,
                EvidenceZipHashVerified: true,
                sessionId,
                masterSha,
                packageSha,
                bundleSha,
                bundleVersion,
                SessionCount: 1,
                RunCount: verified.Count,
                verified,
                CrossManifestLineageVerified: true,
                FullOutputsVerified: true,
                FinalSuccess: true));
    }

    private static JsonDocument ReadJson(string path)
    {
        RequireFile(path, "ARCH5A_REQUIRED_MANIFEST_MISSING");
        return JsonDocument.Parse(File.ReadAllBytes(path));
    }

    private static JsonElement SingleByStrategy(IEnumerable<JsonElement> values, string strategy, string error)
    {
        var matches = values.Where(x => RequiredString(x, "strategy") == strategy).ToArray();
        return matches.Length == 1 ? matches[0] : throw new InvalidDataException(error);
    }

    private static string RequiredString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"PROVENANCE_FIELD_MISSING:{property}");
        }
        return value.GetString()!;
    }

    private static string RequiredSha(JsonElement element, string property)
    {
        var value = RequiredString(element, property).ToLowerInvariant();
        return Arch5bHashing.IsSha256(value) ? value : throw new InvalidDataException($"PROVENANCE_SHA_INVALID:{property}");
    }

    private static string RequiredGitCommit(JsonElement element, string property)
    {
        var value = RequiredString(element, property).ToLowerInvariant();
        return value.Length is 40 or 64 && value.All(Uri.IsHexDigit)
            ? value
            : throw new InvalidDataException($"PROVENANCE_GIT_COMMIT_INVALID:{property}");
    }

    private static DateTimeOffset RequiredUtc(JsonElement element, string property)
    {
        var raw = RequiredString(element, property);
        if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) || value.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException($"PROVENANCE_TIMESTAMP_INVALID:{property}");
        }
        return value;
    }

    private static decimal ParseDecimalString(JsonElement element, string property)
    {
        var raw = RequiredString(element, property);
        return decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidDataException($"PROVENANCE_DECIMAL_INVALID:{property}");
    }

    private static void RequireString(JsonElement element, string property, string expected)
    {
        if (RequiredString(element, property) != expected)
        {
            throw new InvalidDataException($"PROVENANCE_FIELD_MISMATCH:{property}");
        }
    }

    private static void RequireTrue(JsonElement element, string property, bool expected)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False) || value.GetBoolean() != expected)
        {
            throw new InvalidDataException($"PROVENANCE_BOOLEAN_MISMATCH:{property}");
        }
    }

    private static void RequireTrue(JsonElement element, string parent, string property, bool expected)
    {
        if (!element.TryGetProperty(parent, out var parentValue))
        {
            throw new InvalidDataException($"PROVENANCE_OBJECT_MISSING:{parent}");
        }
        RequireTrue(parentValue, property, expected);
    }

    private static void RequireFile(string path, string error)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException(error);
        }
    }

    private static string FileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
