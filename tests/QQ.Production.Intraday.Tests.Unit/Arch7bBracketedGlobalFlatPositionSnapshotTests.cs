using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bBracketedGlobalFlatPositionSnapshotTests
{
    [Fact]
    public void Case01_contract_v2_is_accepted() =>
        Arch7bCoreBracketEvidencePackageReader.ValidateSemanticContract(ValidCore());

    [Fact]
    public void Case02_contract_v1_is_rejected() =>
        AssertCode("ARCH7B_CORE_CONTRACT_VERSION_REJECTED",
            () => Validate(ValidCore() with { CoreContractVersion = "v1" }));

    [Fact]
    public void Case03_wrong_account_is_rejected() =>
        AssertCode("ARCH7B_CORE_ACCOUNT_ID_MISMATCH",
            () => Validate(ValidCore() with { AccountId = "wrong" }));

    [Fact]
    public void Case04_wrong_environment_is_rejected() =>
        AssertCode("ARCH7B_CORE_ENVIRONMENT_MISMATCH",
            () => Validate(ValidCore() with { Environment = "LIVE" }));

    [Fact]
    public void Case05_unproven_current_snapshot_is_rejected() =>
        AssertCode("ARCH7B_CORE_CURRENT_SNAPSHOT_NOT_PROVEN",
            () => Validate(ValidCore() with { CurrentSnapshotStatus = "INVALID" }));

    [Fact]
    public void Case06_invalid_broker_date_sequence_is_rejected() =>
        AssertCode("ARCH7B_CORE_BROKER_DATE_SEQUENCE_INVALID",
            () => Validate(ValidCore() with { BrokerDateSequenceStatus = "INVALID" }));

    [Fact]
    public void Case07_span_above_thirty_seconds_is_rejected() =>
        AssertCode("ARCH7B_CORE_BRACKET_SPAN_EXCEEDED",
            () => Validate(ValidCore() with { BrokerBracketSpanSeconds = 31 }));

    [Fact]
    public void Case08_nonzero_position_count_is_rejected() =>
        AssertCode(Arch7bBracketedGlobalFlatContract.NonzeroPositionBlocker,
            () => Validate(ValidCore() with { PositionCount = 1 }));

    [Fact]
    public void Case09_unstable_position_set_is_rejected() =>
        AssertCode("ARCH7B_CORE_POSITION_SET_UNSTABLE",
            () => Validate(ValidCore() with { StablePositionSet = false }));

    [Fact]
    public void Case10_wrong_evidence_sha_is_rejected()
    {
        using var fixture = new CorePackageFixture();
        var expected = fixture.Expectations with { EvidenceSha256 = Hash('9') };
        AssertCode("ARCH7B_CORE_EVIDENCE_SHA_MISMATCH",
            () => Arch7bCoreBracketEvidencePackageReader.Read(fixture.Root, expected));
    }

    [Fact]
    public void Case11_wrong_contract_file_sha_is_rejected()
    {
        using var fixture = new CorePackageFixture();
        var expected = fixture.Expectations with { ContractFileSha256 = Hash('9') };
        AssertCode("ARCH7B_CORE_CONTRACT_FILE_SHA_MISMATCH",
            () => Arch7bCoreBracketEvidencePackageReader.Read(fixture.Root, expected));
    }

    [Fact]
    public void Case12_wrong_final_index_sha_is_rejected()
    {
        using var fixture = new CorePackageFixture();
        var expected = fixture.Expectations with { FinalIndexSha256 = Hash('9') };
        AssertCode("ARCH7B_CORE_FINAL_INDEX_SHA_MISMATCH",
            () => Arch7bCoreBracketEvidencePackageReader.Read(fixture.Root, expected));
    }

    [Fact]
    public void Case13_missing_artifact_is_rejected()
    {
        using var fixture = new CorePackageFixture();
        File.Delete(Path.Combine(fixture.Root, "attempt-1", "P2-open-positions.csv"));
        AssertCode("ARCH7B_CORE_INDEXED_ARTIFACT_MISSING:attempt-1/P2-open-positions.csv",
            () => Arch7bCoreBracketEvidencePackageReader.Read(
                fixture.Root, fixture.Expectations));
    }

    [Fact]
    public void Case14_path_traversal_is_rejected()
    {
        using var fixture = new CorePackageFixture();
        fixture.AddTraversalIndexEntry();
        AssertCode("ARCH7B_CORE_PATH_TRAVERSAL_REJECTED",
            () => Arch7bCoreBracketEvidencePackageReader.Read(
                fixture.Root, fixture.Expectations));
    }

    [Fact]
    public void Case15_secret_pattern_is_rejected()
    {
        using var fixture = new CorePackageFixture();
        fixture.WriteIndexedFile("validation/runner-tests.stdout.log",
            "password=not-a-real-secret-fixture");
        AssertCode(
            "ARCH7B_CORE_SECRET_PATTERN_DETECTED:validation/runner-tests.stdout.log",
            () => Arch7bCoreBracketEvidencePackageReader.Read(
                fixture.Root, fixture.Expectations));
    }

    [Fact]
    public void Case16_four_exact_infx_models_are_required()
    {
        var plan = Plan();
        var universe = Universe(plan);
        Assert.Equal(
            Arch7bBracketedGlobalFlatContract.RequiredStrategies.Order(StringComparer.Ordinal),
            universe.Models.Select(value => value.StrategyId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Case17_strategy_counts_are_66_66_78_78()
    {
        var counts = Universe(Plan()).StrategyCounts;
        Assert.Equal(66, counts["INFX7"]);
        Assert.Equal(66, counts["INFX8"]);
        Assert.Equal(78, counts["INFX9"]);
        Assert.Equal(78, counts["INFX10"]);
    }

    [Fact]
    public void Case18_required_universe_has_99_instruments() =>
        Assert.Equal(99, Universe(Plan()).Instruments.Count);

    [Fact]
    public void Case19_missing_security_mapping_is_rejected()
    {
        var plan = Plan();
        AssertCode("ARCH7B_PMS_SECURITY_MAPPING_MISSING",
            () => Universe(plan, mappings: plan.SecurityMappings.Skip(1).ToArray()));
    }

    [Fact]
    public void Case20_duplicate_instrument_id_is_rejected()
    {
        var plan = Plan();
        AssertCode("ARCH7B_PMS_DUPLICATE_INSTRUMENT_ID",
            () => Universe(plan,
                mappings: [.. plan.SecurityMappings, plan.SecurityMappings[0]]));
    }

    [Fact]
    public void Case21_universe_sha_is_deterministic()
    {
        var plan = Plan();
        Assert.Equal(Universe(plan).RequiredUniverseSha256,
            Universe(plan).RequiredUniverseSha256);
    }

    [Fact]
    public void Case22_empty_broker_snapshot_produces_99_zeros()
    {
        var snapshot = Snapshot();
        Assert.Equal(99, snapshot.Lines.Count);
        Assert.All(snapshot.Lines, value => Assert.Equal(0m, value.CurrentBaseQuantity));
    }

    [Fact]
    public void Case23_all_lines_have_explicit_provenance() =>
        Assert.All(Snapshot().Lines, value =>
            Assert.Equal(Arch7bBracketedGlobalFlatContract.ProvenanceKind,
                value.ProvenanceKind));

    [Fact]
    public void Case24_zero_derivation_is_explicit_not_missing_value_default()
    {
        var snapshot = Snapshot();
        Assert.Equal(99, snapshot.DerivedZeroCount);
        Assert.All(snapshot.Lines, value =>
            Assert.Equal(0, value.BrokerPositionCount));
    }

    [Fact]
    public void Case25_same_evidence_produces_same_ids()
    {
        var universe = Universe(Plan());
        var first = Arch7bGlobalFlatPositionSnapshotBuilder.Build(ValidCore(), universe);
        var second = Arch7bGlobalFlatPositionSnapshotBuilder.Build(ValidCore(), universe);
        Assert.Equal(first.AccountSnapshotId, second.AccountSnapshotId);
        Assert.Equal(first.PositionSnapshotId, second.PositionSnapshotId);
        Assert.Equal(first.Lines.Select(value => value.PositionSnapshotLineId),
            second.Lines.Select(value => value.PositionSnapshotLineId));
    }

    [Fact]
    public void Case26_different_evidence_changes_ids()
    {
        var universe = Universe(Plan());
        var first = Arch7bGlobalFlatPositionSnapshotBuilder.Build(ValidCore(), universe);
        var second = Arch7bGlobalFlatPositionSnapshotBuilder.Build(
            ValidCore() with { EvidenceSha256 = Hash('9') }, universe);
        Assert.NotEqual(first.AccountSnapshotId, second.AccountSnapshotId);
        Assert.NotEqual(first.PositionSnapshotId, second.PositionSnapshotId);
    }

    [Fact]
    public void Case27_different_universe_changes_ids()
    {
        var universe = Universe(Plan());
        var first = Arch7bGlobalFlatPositionSnapshotBuilder.Build(ValidCore(), universe);
        var second = Arch7bGlobalFlatPositionSnapshotBuilder.Build(
            ValidCore(), universe with { RequiredUniverseSha256 = Hash('9') });
        Assert.NotEqual(first.AccountSnapshotId, second.AccountSnapshotId);
        Assert.NotEqual(first.PositionSnapshotId, second.PositionSnapshotId);
    }

    [Fact]
    public void Case28_snapshot_as_of_equals_p2_broker_time()
    {
        var core = ValidCore();
        Assert.Equal(core.PositionReportP2Utc,
            Arch7bGlobalFlatPositionSnapshotBuilder.Build(
                core, Universe(Plan())).PositionSnapshotAsOfUtc);
    }

    [Fact]
    public void Case29_snapshot_has_99_lines_and_zero_unknown()
    {
        var snapshot = Snapshot();
        Assert.Equal(99, snapshot.NormalizedLineCount);
        Assert.Equal(0, snapshot.UnknownCount);
    }

    [Fact]
    public void Case30_smoke_has_99_market_observations() =>
        Assert.Equal(99, Smoke().ObservationCount);

    [Fact]
    public void Case31_smoke_has_288_targets() =>
        Assert.Equal(288, Smoke().TargetPositionCount);

    [Fact]
    public void Case32_smoke_has_288_drifts() =>
        Assert.Equal(288, Smoke().PositionOnlyDriftCount);

    [Fact]
    public void Case33_smoke_preserves_strategy_counts()
    {
        var counts = Smoke().StrategyCounts;
        Assert.Equal(66, counts["INFX7"]);
        Assert.Equal(66, counts["INFX8"]);
        Assert.Equal(78, counts["INFX9"]);
        Assert.Equal(78, counts["INFX10"]);
    }

    [Fact]
    public void Case34_smoke_current_quantities_are_zero() =>
        Assert.Equal(288, Smoke().ZeroCurrentQuantityCount);

    [Fact]
    public void Case35_smoke_delta_equals_target() =>
        Assert.Equal(288, Smoke().ExactDeltaCount);

    [Fact]
    public void Case36_smoke_projection_integrity_is_proven() =>
        Assert.Equal(PmsShadowEconomicProjectionIntegrityVerifier.Proven,
            Smoke().ProjectionIntegrityStatus);

    [Fact]
    public void Case37_double_smoke_is_byte_for_byte_identical()
    {
        var plan = Plan();
        var universe = Universe(plan);
        var snapshot = Arch7bGlobalFlatPositionSnapshotBuilder.Build(ValidCore(), universe);
        var first = Arch7bGlobalFlatEconomicSmokeRunner.Run(snapshot, universe);
        var second = Arch7bGlobalFlatEconomicSmokeRunner.Run(snapshot, universe);
        Assert.Equal(Arch7bGlobalFlatOutputWriter.SerializeSmoke(first),
            Arch7bGlobalFlatOutputWriter.SerializeSmoke(second));
    }

    [Fact]
    public void Case38_working_order_authority_remains_unknown() =>
        Assert.Equal(Arch7bBracketedGlobalFlatContract.WorkingOrderAuthority,
            Snapshot().WorkingOrderAuthority);

    [Fact]
    public void Case39_broker_send_is_disabled() =>
        Assert.False(Snapshot().BrokerSendAllowed);

    [Fact]
    public void Case40_database_write_is_disabled() =>
        Assert.True(Snapshot().NoDatabaseWrite);

    [Fact]
    public void Case41_fix_is_disabled() =>
        Assert.True(Snapshot().NoFix);

    [Fact]
    public void Case42_order_is_disabled() =>
        Assert.True(Snapshot().NoOrder);

    [Fact]
    public void Case43_output_manifest_declares_no_fill()
    {
        using var fixture = new OutputFixture();
        Assert.True(fixture.Manifest["no_fill"]!.GetValue<bool>());
    }

    [Fact]
    public void Case44_output_manifest_declares_no_ledger_write()
    {
        using var fixture = new OutputFixture();
        Assert.True(fixture.Manifest["no_ledger_write"]!.GetValue<bool>());
    }

    [Fact]
    public void Case45_output_manifest_declares_no_account_api()
    {
        using var fixture = new OutputFixture();
        Assert.True(fixture.Manifest["no_account_api"]!.GetValue<bool>());
    }

    [Fact]
    public void Case46_output_manifest_declares_no_databento()
    {
        using var fixture = new OutputFixture();
        Assert.True(fixture.Manifest["no_databento"]!.GetValue<bool>());
    }

    [Fact]
    public void Case47_output_bundle_contains_no_secret()
    {
        using var fixture = new OutputFixture();
        foreach (var file in Directory.EnumerateFiles(fixture.Root))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("password=", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret_access_key", text,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Case48_legacy_0_5_package_is_accepted_without_aws_metadata()
    {
        using var fixture = new CorePackageFixture();
        var evidence = Arch7bCoreBracketEvidencePackageReader.Read(
            fixture.Root, fixture.Expectations);
        Assert.Equal(
            Arch7bCoreDownloaderCompatibilityContract.LegacyProfile,
            evidence.DownloaderCompatibility!.Profile);
        Assert.Null(evidence.DownloaderCompatibility.SessionRecoveryMode);
        Assert.Null(evidence.DownloaderCompatibility.SecretReferenceSha256);
    }

    [Fact]
    public void Case49_aws_recovery_0_6_package_is_accepted()
    {
        using var fixture = AwsRecoveryFixture();
        var evidence = Arch7bCoreBracketEvidencePackageReader.Read(
            fixture.Root, fixture.Expectations);
        Assert.Equal(
            Arch7bCoreDownloaderCompatibilityContract.AwsRecoveryProfile,
            evidence.DownloaderCompatibility!.Profile);
        Assert.Equal(
            Arch7bCoreDownloaderCompatibilityContract.AwsRecoveryMode,
            evidence.DownloaderCompatibility.SessionRecoveryMode);
        Assert.True(evidence.DownloaderCompatibility.ManualSessionReopenProven);
    }

    [Theory]
    [InlineData("0.4.0")]
    [InlineData("0.7.0")]
    [InlineData("not-a-version")]
    public void Case50_only_explicit_downloader_versions_are_accepted(string version) =>
        AssertCode(Arch7bCoreDownloaderCompatibilityContract.VersionRejected,
            () => Validate(ValidCore() with { DownloaderVersion = version }));

    [Fact]
    public void Case51_manifest_contract_downloader_mismatch_is_rejected()
    {
        using var fixture = AwsRecoveryFixture();
        fixture.MutateManifest(value =>
            value["downloader_version"] =
                Arch7bCoreDownloaderCompatibilityContract.LegacyDownloaderVersion);
        AssertRecoveryCode(
            Arch7bCoreDownloaderCompatibilityContract.ManifestContractMismatch,
            fixture);
    }

    [Fact]
    public void Case52_missing_recovery_mode_is_rejected()
    {
        using var fixture = AwsRecoveryFixture();
        fixture.MutateManifest(value => value.Remove("session_recovery_mode"));
        AssertRecoveryInvalid(fixture);
    }

    [Fact]
    public void Case53_wrong_recovery_mode_is_rejected()
    {
        using var fixture = AwsRecoveryFixture();
        fixture.MutateManifest(value => value["session_recovery_mode"] = "OTHER");
        AssertRecoveryInvalid(fixture);
    }

    [Theory]
    [InlineData("credentials_recorded")]
    [InlineData("secret_values_recorded")]
    [InlineData("totp_recorded")]
    public void Case54_recorded_secret_material_is_rejected(string property)
    {
        using var fixture = AwsRecoveryFixture();
        fixture.MutateManifest(value => value[property] = true);
        AssertRecoveryInvalid(fixture);
    }

    [Fact]
    public void Case55_invalid_secret_reference_sha_is_rejected()
    {
        using var fixture = AwsRecoveryFixture();
        fixture.MutateManifest(value => value["secret_reference_sha256"] = "bad");
        AssertRecoveryInvalid(fixture);
    }

    [Theory]
    [InlineData("secret_keys_contract_version")]
    [InlineData("login_form_contract")]
    [InlineData("automated_bootstrap_contract")]
    public void Case56_wrong_recovery_contract_is_rejected(string property)
    {
        using var fixture = AwsRecoveryFixture();
        fixture.MutateManifest(value => value[property] = "wrong");
        AssertRecoveryInvalid(fixture);
    }

    [Fact]
    public void Case57_secret_fetch_without_login_is_rejected()
    {
        using var fixture = AwsRecoveryFixture();
        fixture.MutateManifest(value => value["login_performed"] = false);
        AssertRecoveryInvalid(fixture);
    }

    [Fact]
    public void Case58_secret_fetch_without_version_id_is_rejected()
    {
        using var fixture = AwsRecoveryFixture();
        fixture.MutateManifest(value => value.Remove("secret_version_id"));
        AssertRecoveryInvalid(fixture);
    }

    [Fact]
    public void Case59_login_without_reopen_proof_is_rejected()
    {
        using var fixture = AwsRecoveryFixture();
        fixture.MutateManifest(value =>
            value.Remove("manual_session_reopen_proof"));
        AssertRecoveryInvalid(fixture);
    }

    [Fact]
    public void Case60_already_active_session_without_secret_read_is_accepted()
    {
        using var fixture = AwsRecoveryFixture();
        fixture.MutateManifest(value =>
        {
            value["session_already_active"] = true;
            value["secret_fetched"] = false;
            value["login_performed"] = false;
            value.Remove("secret_version_id");
        });
        var evidence = Arch7bCoreBracketEvidencePackageReader.Read(
            fixture.Root, fixture.Expectations);
        Assert.True(evidence.DownloaderCompatibility!.SessionAlreadyActive);
        Assert.False(evidence.DownloaderCompatibility.SecretFetched);
        Assert.False(evidence.DownloaderCompatibility.LoginPerformed);
        Assert.False(evidence.DownloaderCompatibility.SecretVersionIdPresent);
    }

    [Fact]
    public void Case61_reopen_proof_wrong_account_is_rejected()
    {
        using var fixture = AwsRecoveryFixture();
        fixture.MutateManifest(value =>
            value["manual_session_reopen_proof"]!["account_id"] = "wrong");
        AssertRecoveryInvalid(fixture);
    }

    [Fact]
    public void Case62_reopen_proof_secret_read_is_rejected()
    {
        using var fixture = AwsRecoveryFixture();
        fixture.MutateManifest(value =>
            value["manual_session_reopen_proof"]!["secret_read_during_probe"] = true);
        AssertRecoveryInvalid(fixture);
    }

    [Fact]
    public void Case63_raw_credential_property_is_rejected()
    {
        using var fixture = AwsRecoveryFixture();
        fixture.MutateManifest(value => value["password"] = null);
        AssertRecoveryInvalid(fixture);
    }

    [Fact]
    public void Case64_real_0_6_package_is_requalified_when_present()
    {
        var root = Environment.GetEnvironmentVariable(
            "ARCH7B_REAL_CORE_EVIDENCE_ROOT");
        if (string.IsNullOrWhiteSpace(root)) return;
        var evidence = Arch7bCoreBracketEvidencePackageReader.Read(
            root,
            new(
                "e3c01e8100740b89702bc85961442e5b4e3f6d92",
                "5ecc7aafaa75ec59c23deda97f8f4d471e5f035b4c394dcfe470dea3e8a13d04",
                "16e327ccb23b4b5655913e00a1e07c924b749d284ebaaaf27507c8929c054e08",
                "c535a14f75b4e99a66fe92ca77304323de94fb921301a9ca0221136203f1f114"));
        Assert.Equal(
            Arch7bCoreDownloaderCompatibilityContract.AwsRecoveryDownloaderVersion,
            evidence.DownloaderVersion);
        Assert.Equal(
            Arch7bCoreDownloaderCompatibilityContract.AwsRecoveryProfile,
            evidence.DownloaderCompatibility!.Profile);
        Assert.Equal(0, evidence.ExecutionCount);
        Assert.Equal(0, evidence.PositionCount);
        Assert.True(evidence.StableExecutionSet);
        Assert.True(evidence.StablePositionSet);
        Assert.False(evidence.DownloaderCompatibility.CredentialsRecorded);
        Assert.False(evidence.DownloaderCompatibility.SecretValuesRecorded);
        Assert.False(evidence.DownloaderCompatibility.TotpRecorded);
    }

    [Fact]
    public void Case65_fast_package_has_exact_inventory_and_is_import_readable()
    {
        var root = TempRoot("arch7b-fast-package");
        try
        {
            var plan = Plan();
            var universe = Universe(plan);
            var snapshot = Arch7bGlobalFlatPositionSnapshotBuilder.Build(
                ValidCore(), universe);
            _ = Arch7bFreshPositionImportPackageWriter.Write(
                root, ValidCore(), universe, snapshot);

            Assert.Equal(
                new[]
                {
                    "manifest.json",
                    "normalized-position-lines.csv",
                    "pms-bracketed-global-flat-position-snapshot.json",
                    "required-pms-universe.json"
                },
                Directory.EnumerateFiles(root).Select(Path.GetFileName)
                    .Order(StringComparer.Ordinal));
            var package = Arch7bPositionImportPackageReader.Read(root);
            Assert.Equal(99, package.Snapshot.NormalizedLineCount);
            Assert.Equal(99, package.Snapshot.DerivedZeroCount);
            Assert.Equal(0, package.Snapshot.UnknownCount);
            var manifest = JsonNode.Parse(File.ReadAllText(
                Path.Combine(root, "manifest.json")))!.AsObject();
            Assert.Equal(
                Arch7bFreshPositionImportFastPathContract.Version,
                manifest["contract_version"]!.GetValue<string>());
            Assert.Equal(
                Arch7bFreshPositionImportFastPathContract
                    .SmokeQualificationStatus,
                manifest["smoke_qualification_status"]!.GetValue<string>());
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void Case66_full_and_fast_canonical_packages_are_byte_identical()
    {
        var fullBundleRoot = TempRoot("arch7b-full-bundle");
        var fullPackageRoot = TempRoot("arch7b-full-package");
        var fastPackageRoot = TempRoot("arch7b-fast-package");
        try
        {
            var core = ValidCore();
            var universe = Universe(Plan());
            var snapshot = Arch7bGlobalFlatPositionSnapshotBuilder.Build(
                core, universe);
            var smokeA = Arch7bGlobalFlatEconomicSmokeRunner.Run(
                snapshot, universe);
            var smokeB = Arch7bGlobalFlatEconomicSmokeRunner.Run(
                snapshot, universe);
            _ = Arch7bGlobalFlatOutputWriter.Write(
                fullBundleRoot, core, universe, snapshot, smokeA, smokeB);
            _ = Arch7bFreshPositionImportPackageWriter.Write(
                fullPackageRoot, core, universe, snapshot);
            _ = Arch7bFreshPositionImportPackageWriter.Write(
                fastPackageRoot, core, universe, snapshot);

            foreach (var name in Directory.EnumerateFiles(fullPackageRoot)
                         .Select(Path.GetFileName))
            {
                Assert.Equal(
                    File.ReadAllBytes(Path.Combine(fullPackageRoot, name!)),
                    File.ReadAllBytes(Path.Combine(fastPackageRoot, name!)));
            }
        }
        finally
        {
            DeleteRoot(fullBundleRoot);
            DeleteRoot(fullPackageRoot);
            DeleteRoot(fastPackageRoot);
        }
    }

    [Fact]
    public void Case67_timing_evidence_stays_outside_minimal_package()
    {
        var packageRoot = TempRoot("arch7b-fast-package");
        var evidenceRoot = TempRoot("arch7b-fast-timing");
        try
        {
            var core = ValidCore();
            var universe = Universe(Plan());
            var snapshot = Arch7bGlobalFlatPositionSnapshotBuilder.Build(
                core, universe);
            _ = Arch7bFreshPositionImportPackageWriter.Write(
                packageRoot, core, universe, snapshot);
            var timing = new Arch7bFreshPositionImportTimingCollector();
            _ = timing.Measure("MINIMAL_PACKAGE_TEST", () => 1);
            var evidence = timing.Complete(
                "prepare-fresh-position-import-package",
                core.PositionReportP2Utc,
                smokeAExecuted: false,
                smokeBExecuted: false);
            var timingPath = Path.Combine(evidenceRoot, "timing.json");
            Arch7bFreshPositionImportTimingWriter.Write(
                timingPath, packageRoot, evidence);

            Assert.True(File.Exists(timingPath));
            Assert.Equal(4, Directory.EnumerateFiles(packageRoot).Count());
            Assert.False(evidence.SmokeAExecuted);
            Assert.False(evidence.SmokeBExecuted);
            Assert.False(evidence.ZipExecuted);
        }
        finally
        {
            DeleteRoot(packageRoot);
            DeleteRoot(evidenceRoot);
        }
    }

    [Fact]
    public void Case68_fast_path_slo_boundaries_and_blockers_are_exact()
    {
        var p2 = DateTimeOffset.Parse("2026-07-27T11:23:45Z");
        Assert.Equal("PASS",
            Arch7bFreshPositionImportSloPolicy.RequirePackageReady(
                p2, p2.AddSeconds(60)).Status);
        Assert.Equal("PASS",
            Arch7bFreshPositionImportSloPolicy.RequireReady(
                p2, p2.AddSeconds(90)).Status);
        Assert.Equal("PASS",
            Arch7bFreshPositionImportSloPolicy.RequirePlan(
                p2, p2.AddSeconds(120)).Status);
        Assert.Equal("PASS",
            Arch7bFreshPositionImportSloPolicy.RequireApplyStart(
                p2, p2.AddSeconds(150)).Status);
        Assert.Equal("PASS",
            Arch7bFreshPositionImportSloPolicy.ObserveCommitReadback(
                p2, p2.AddSeconds(180)).Status);
        Assert.Equal("EXPECTATION_EXCEEDED",
            Arch7bFreshPositionImportSloPolicy.ObserveCommitReadback(
                p2, p2.AddMilliseconds(180001)).Status);
        AssertCode(
            Arch7bFreshPositionImportFastPathContract.PackageSloExceeded,
            () => Arch7bFreshPositionImportSloPolicy.RequirePackageReady(
                p2, p2.AddMilliseconds(60001)));
        AssertCode(
            Arch7bFreshPositionImportFastPathContract.ReadySloExceeded,
            () => Arch7bFreshPositionImportSloPolicy.RequireReady(
                p2, p2.AddMilliseconds(90001)));
        AssertCode(
            Arch7bFreshPositionImportFastPathContract.PlanSloExceeded,
            () => Arch7bFreshPositionImportSloPolicy.RequirePlan(
                p2, p2.AddMilliseconds(120001)));
        AssertCode(
            Arch7bFreshPositionImportFastPathContract.ApplyStartSloExceeded,
            () => Arch7bFreshPositionImportSloPolicy.RequireApplyStart(
                p2, p2.AddMilliseconds(150001)));
    }

    [Fact]
    public void Case69_slo_uses_utc_database_authority_only()
    {
        var p2 = DateTimeOffset.Parse("2026-07-27T11:23:45Z");
        AssertCode("ARCH7B_POSITION_IMPORT_TIMESTAMP_NOT_UTC",
            () => Arch7bFreshPositionImportSloPolicy.RequirePackageReady(
                p2.ToOffset(TimeSpan.FromHours(1)), p2.AddSeconds(1)));
        AssertCode(Arch7bPositionImportContract.FromFuture,
            () => Arch7bFreshPositionImportSloPolicy.RequirePackageReady(
                p2, p2.AddMilliseconds(-1)));
    }

    [Fact]
    public void Case70_fast_path_timeline_is_append_only()
    {
        var root = TempRoot("arch7b-fast-timeline");
        try
        {
            var p2 = DateTimeOffset.Parse("2026-07-27T11:23:45Z");
            var timeline = new Arch7bFreshPositionImportAppendOnlyTimeline(root);
            timeline.Record(
                Arch7bFreshPositionImportSloPolicy.RequirePackageReady(
                    p2, p2.AddSeconds(10)));
            timeline.RecordFailure(
                "READY", p2, p2.AddSeconds(91),
                Arch7bFreshPositionImportFastPathContract.ReadySloExceeded);
            Assert.Equal(2, Directory.EnumerateFiles(root).Count());
            AssertCode("ARCH7B_POSITION_FAST_PATH_TIMELINE_ALREADY_EXISTS",
                () => new Arch7bFreshPositionImportAppendOnlyTimeline(root));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public void Case71_fast_branch_invokes_no_smoke_projection_bundle_or_zip()
    {
        var program = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "tools",
            "QQ.Production.Intraday.Tools.Arch7bGlobalFlatPositionSnapshot",
            "Program.cs"));
        var start = program.IndexOf("if (arguments.FastPath)",
            StringComparison.Ordinal);
        var end = program.IndexOf("var smokeA =", start,
            StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var fastBranch = program[start..end];
        Assert.DoesNotContain("Arch7bGlobalFlatEconomicSmokeRunner",
            fastBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("Arch7bGlobalFlatOutputWriter.Write(",
            fastBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("ZipFile.", fastBranch,
            StringComparison.Ordinal);
        Assert.DoesNotContain("DeterministicZip", fastBranch,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Case72_full_branch_retains_both_smokes_and_full_bundle()
    {
        var program = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "tools",
            "QQ.Production.Intraday.Tools.Arch7bGlobalFlatPositionSnapshot",
            "Program.cs"));
        Assert.Equal(2, program.Split(
            "Arch7bGlobalFlatEconomicSmokeRunner.Run",
            StringSplitOptions.None).Length - 1);
        Assert.Contains("FULL_BUNDLE_WRITE", program,
            StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(
                    current.FullName, "QQ.Production.Intraday.sln")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException(
            "ARCH7B_TEST_REPOSITORY_ROOT_NOT_FOUND");
    }
    private static string TempRoot(string prefix) =>
        Path.Combine(Path.GetTempPath(), prefix + "-" +
            Guid.NewGuid().ToString("N"));

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
    private static CorePackageFixture AwsRecoveryFixture() =>
        new(Arch7bCoreDownloaderCompatibilityContract.AwsRecoveryDownloaderVersion);

    private static void AssertRecoveryInvalid(CorePackageFixture fixture) =>
        AssertRecoveryCode(
            Arch7bCoreDownloaderCompatibilityContract.RecoveryMetadataInvalid,
            fixture);

    private static void AssertRecoveryCode(
        string code,
        CorePackageFixture fixture) =>
        AssertCode(code, () => Arch7bCoreBracketEvidencePackageReader.Read(
            fixture.Root, fixture.Expectations));

    private static void Validate(Arch7bCoreBracketEvidence value) =>
        Arch7bCoreBracketEvidencePackageReader.ValidateSemanticContract(value);

    private static Arch7bCoreBracketEvidence ValidCore() => new(
        Hash('a', 40),
        Arch7bBracketedGlobalFlatContract.CoreContractVersion,
        Arch7bCoreDownloaderCompatibilityContract.LegacyDownloaderVersion,
        Arch7bBracketedGlobalFlatContract.AccountId,
        Arch7bBracketedGlobalFlatContract.Environment,
        Arch7bBracketedGlobalFlatContract.SessionMode,
        0,
        0,
        true,
        true,
        Arch7bBracketedGlobalFlatContract.ExecutionReportSchemaVersion,
        Arch7bBracketedGlobalFlatContract.PositionReportSchemaVersion,
        Arch7bBracketedGlobalFlatContract.ExecutionHeaderSetSha256,
        Arch7bBracketedGlobalFlatContract.PositionHeaderSetSha256,
        Arch7bBracketedGlobalFlatContract.EmptyPositionSetAuthority,
        Arch7bBracketedGlobalFlatContract.AccountAuthorityMode,
        Arch7bBracketedGlobalFlatContract.CurrentSnapshotStatus,
        Arch7bBracketedGlobalFlatContract.BrokerDateSequenceStatus,
        0,
        30,
        DateTimeOffset.Parse("2026-07-27T11:23:45Z"),
        DateTimeOffset.Parse("2026-07-27T11:23:45Z"),
        DateTimeOffset.Parse("2026-07-27T11:23:45Z"),
        Hash('b'),
        Hash('c'),
        Hash('d'),
        Hash('e'),
        Hash('f'),
        true,
        true,
        true,
        true,
        true,
        "fixture",
        15,
        null,
        Arch7bCoreDownloaderCompatibilityContract.LegacyEvidence());

    private static PmsShadowPersistencePlan Plan()
    {
        var plan = Arch6cPostgreSqlPmsShadowStateTests.BuildPlan();
        var venueId = plan.SecurityMappings[0].VenueId;
        var lineageVersion = plan.TargetWeights[0].LineageVersion;
        var mappings = Enumerable.Range(1, 99).Select(index =>
        {
            var securityId = index.ToString();
            var symbol = TestPair(index - 1);
            return new PmsShadowSecurityMappingRow(
                plan.Ingestion.IngestionId,
                Arch5bHashing.GuidFromSha256($"instrument:{securityId}"),
                venueId,
                Arch5bHashing.GuidFromSha256($"venue-instrument:{securityId}"),
                securityId,
                symbol,
                "lmax-" + symbol,
                1m,
                1m,
                0.00001m,
                Arch5bHashing.Sha256Hex($"mapping:{securityId}"));
        }).ToArray();
        var weights = plan.ModelRuns.SelectMany(model =>
            StrategySecurityIds(model.StrategyId).Select((index, sourceOrder) =>
            {
                var securityId = index.ToString();
                return new PmsShadowTargetWeightRow(
                    model.ModelRunId,
                    Arch5bHashing.GuidFromSha256($"instrument:{securityId}"),
                    securityId,
                    0.001m,
                    model.TargetCloseUtc,
                    $"{model.StrategyId}:{securityId}",
                    sourceOrder,
                    model.OutputSha256,
                    lineageVersion);
            })).ToArray();
        return plan with { SecurityMappings = mappings, TargetWeights = weights };
    }

    private static IEnumerable<int> StrategySecurityIds(string strategyId) =>
        strategyId switch
        {
            "INFX7" => Enumerable.Range(1, 66),
            "INFX8" => Enumerable.Range(34, 66),
            "INFX9" => Enumerable.Range(1, 78),
            "INFX10" => Enumerable.Range(22, 78),
            _ => throw new InvalidDataException("ARCH7B_TEST_UNKNOWN_STRATEGY")
        };

    private static string TestPair(int index)
    {
        var currencies = new[] { "USD", "EUR", "GBP", "JPY", "AUD", "CAD", "CHF",
            "NZD", "NOK", "SEK", "DKK", "SGD", "HKD" };
        return (from baseCurrency in currencies
                from quoteCurrency in currencies
                where baseCurrency != quoteCurrency
                select baseCurrency + quoteCurrency).ElementAt(index);
    }

    private static Arch7bRequiredPmsUniverse Universe(
        PmsShadowPersistencePlan plan,
        IReadOnlyList<PmsShadowSecurityMappingRow>? mappings = null) =>
        Arch7bRequiredPmsUniverseBuilder.Build(
            plan.Ingestion,
            plan.AccountSnapshot,
            plan.ModelRuns,
            plan.QubesInputSnapshots,
            plan.TargetWeights,
            mappings ?? plan.SecurityMappings,
            Arch7bBracketedGlobalFlatContract.TargetProfile,
            Hash('1'),
            transactionReadOnly: true,
            pendingModelChanges: false);

    private static Arch7bPmsGlobalFlatPositionSnapshot Snapshot()
    {
        var plan = Plan();
        return Arch7bGlobalFlatPositionSnapshotBuilder.Build(
            ValidCore(), Universe(plan));
    }

    private static Arch7bGlobalFlatEconomicSmoke Smoke()
    {
        var plan = Plan();
        var universe = Universe(plan);
        return Arch7bGlobalFlatEconomicSmokeRunner.Run(
            Arch7bGlobalFlatPositionSnapshotBuilder.Build(ValidCore(), universe),
            universe);
    }

    private static string Hash(char value, int length = 64) => new(value, length);

    private static void AssertCode(string expected, Action action) =>
        Assert.Equal(expected, Assert.Throws<InvalidDataException>(action).Message);

    private sealed class OutputFixture : IDisposable
    {
        public OutputFixture()
        {
            Root = Path.Combine(Path.GetTempPath(),
                "arch7b-global-flat-output-" + Guid.NewGuid().ToString("N"));
            var plan = Plan();
            var universe = Universe(plan);
            var core = ValidCore();
            var snapshot = Arch7bGlobalFlatPositionSnapshotBuilder.Build(core, universe);
            var smoke = Arch7bGlobalFlatEconomicSmokeRunner.Run(snapshot, universe);
            Arch7bGlobalFlatOutputWriter.Write(
                Root, core, universe, snapshot, smoke, smoke);
            Manifest = JsonNode.Parse(File.ReadAllText(
                Path.Combine(Root, "manifest.json")))!.AsObject();
        }

        public string Root { get; }
        public JsonObject Manifest { get; }
        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }

    internal sealed class CorePackageFixture : IDisposable
    {
        private static readonly string[] ExecutionHeaders =
        [
            "Execution ID", "Mtf Execution ID", "Timestamp", "Trade Quantity",
            "Trade Price", "Trade Date", "Instrument ID", "Symbol", "Instruction ID",
            "Order ID", "Stop Price", "Limit Price", "Order Placement Timestamp",
            "Type", "Remote Venue", "User Placing Order", "Total Profit Loss",
            "Total Commission", "Account Id", "Units Bought/Sold", "Notional Value",
            "Trade UTI"
        ];

        private static readonly string[] PositionHeaders =
        [
            "Instrument", "CCY", "Open Quantity", "Margin on Open Position",
            "Average Opening Price", "Closing Price", "Open Profit / Loss",
            "MTM Valuation Rate to Base CCY", "LMAX Symbol", "Account Id",
            "Position UTI"
        ];

        private readonly string downloaderVersion;

        public CorePackageFixture(
            string downloaderVersion =
                Arch7bCoreDownloaderCompatibilityContract.LegacyDownloaderVersion)
        {
            this.downloaderVersion = downloaderVersion;
            Root = Path.Combine(Path.GetTempPath(),
                "arch7b-core-package-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "attempt-1"));
            Directory.CreateDirectory(Path.Combine(Root, "complementary"));
            Directory.CreateDirectory(Path.Combine(Root, "validation"));
            Build();
        }

        public string Root { get; }
        public Arch7bCoreEvidenceExpectations Expectations { get; private set; } = null!;

        public void AddTraversalIndexEntry()
        {
            var indexPath = Path.Combine(Root, "validation", "final-evidence-index.json");
            var index = JsonNode.Parse(File.ReadAllText(indexPath))!.AsObject();
            index["files"]!.AsArray().Add(new JsonObject
            {
                ["relative_path"] = "../outside.txt",
                ["bytes"] = 0,
                ["sha256"] = Hash('0')
            });
            index["file_count_excluding_index"] = index["files"]!.AsArray().Count;
            WriteJson(indexPath, index);
            Expectations = Expectations with { FinalIndexSha256 = FileHash(indexPath) };
        }

        public void MutateManifest(Action<JsonObject> mutate)
        {
            var path = Path.Combine(Root, "acquisition-manifest.json");
            var manifest = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            mutate(manifest);
            WriteJson(path, manifest);
            RebuildIndex();
        }

        public void WriteIndexedFile(string relative, string content)
        {
            File.WriteAllText(Path.Combine(Root,
                relative.Replace('/', Path.DirectorySeparatorChar)), content,
                new UTF8Encoding(false));
            RebuildIndex();
        }

        private void Build()
        {
            var emptySemanticSha =
                "4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945";
            foreach (var label in new[] { "T0", "T1", "T2" })
                File.WriteAllText(Path.Combine(Root, "attempt-1",
                    $"{label}-individual-trades.csv"),
                    string.Join(',', ExecutionHeaders) + "\n", new UTF8Encoding(false));
            foreach (var label in new[] { "P1", "P2" })
                File.WriteAllText(Path.Combine(Root, "attempt-1",
                    $"{label}-open-positions.csv"),
                    string.Join(',', PositionHeaders) + "\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(Root, "complementary", "account-statement.pdf"),
                "fixture", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(Root, "complementary", "account-summary.csv"),
                "Account Id\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(Root, "complementary", "currency-wallets.csv"),
                "Currency\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(Root, "complementary", "trades.csv"),
                "Trade\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(Root, "validation", "runner-tests.stdout.log"),
                "pass 115\n", new UTF8Encoding(false));
            File.WriteAllBytes(Path.Combine(Root, "validation", "runner-tests.stderr.log"), []);

            JsonObject Report(string label, string file, string headerSha) => new()
            {
                ["Label"] = label,
                ["ResponseServerDateUtc"] = "Mon, 27 Jul 2026 11:23:45 GMT",
                ["RawSha256"] = FileHash(Path.Combine(Root, "attempt-1", file)),
                ["SemanticSha256"] = emptySemanticSha,
                ["HeaderSetSha256"] = headerSha,
                ["RowCount"] = 0
            };
            var attempt = new JsonObject
            {
                ["Attempt"] = 1,
                ["T0"] = Report("T0", "T0-individual-trades.csv",
                    Arch7bBracketedGlobalFlatContract.ExecutionHeaderSetSha256),
                ["P1"] = Report("P1", "P1-open-positions.csv",
                    Arch7bBracketedGlobalFlatContract.PositionHeaderSetSha256),
                ["T1"] = Report("T1", "T1-individual-trades.csv",
                    Arch7bBracketedGlobalFlatContract.ExecutionHeaderSetSha256),
                ["P2"] = Report("P2", "P2-open-positions.csv",
                    Arch7bBracketedGlobalFlatContract.PositionHeaderSetSha256),
                ["T2"] = Report("T2", "T2-individual-trades.csv",
                    Arch7bBracketedGlobalFlatContract.ExecutionHeaderSetSha256),
                ["BrokerDateSequenceStatus"] =
                    Arch7bBracketedGlobalFlatContract.BrokerDateSequenceStatus,
                ["BrokerBracketSpanSeconds"] = 0,
                ["MaximumBrokerBracketSpanSeconds"] = 30,
                ["Stable"] = true
            };
            WriteJson(Path.Combine(Root, "attempt-1", "attempt-manifest.json"), attempt);

            var accountEvidence = new JsonObject
            {
                ["ReportType"] = "account-summary",
                ["SelectedAccountId"] = Arch7bBracketedGlobalFlatContract.AccountId
            };
            accountEvidence["EvidenceSha256"] = JsonHash(accountEvidence);
            var decision = new JsonObject
            {
                ["ContractVersion"] =
                    "lmax_open_positions_snapshot_semantic_decision_v1",
                ["CurrentSnapshotStatus"] =
                    Arch7bBracketedGlobalFlatContract.CurrentSnapshotStatus
            };
            decision["EvidenceSha256"] = JsonHash(decision);
            var complementaryReportFiles = new[]
            {
                (Type: "account-statement", File: "account-statement.pdf"),
                (Type: "account-summary", File: "account-summary.csv"),
                (Type: "currency-wallets", File: "currency-wallets.csv"),
                (Type: "trades", File: "trades.csv")
            };
            var complementaryReports = complementaryReportFiles.Select(item =>
            {
                var path = Path.Combine(Root, "complementary", item.File);
                var sha = FileHash(path);
                return new JsonObject
                {
                    ["ReportType"] = item.Type,
                    ["RawSha256"] = sha,
                    ["Artifact"] = new JsonObject
                    {
                        ["path"] = $"D:\\evidence\\complementary\\{item.File}",
                        ["size"] = new FileInfo(path).Length,
                        ["sha256"] = sha
                    },
                    ["SelectedAccountId"] =
                        Arch7bBracketedGlobalFlatContract.AccountId
                };
            }).ToArray();
            var rawSet = new[] { "T0", "P1", "T1", "P2", "T2" }
                .Select(label => attempt[label]!["RawSha256"]!.GetValue<string>())
                .Concat(complementaryReports.Select(report =>
                    report["RawSha256"]!.GetValue<string>()))
                .Order(StringComparer.Ordinal);
            var semanticCore = new JsonObject
            {
                ["T0"] = emptySemanticSha,
                ["T1"] = emptySemanticSha,
                ["T2"] = emptySemanticSha,
                ["P1"] = emptySemanticSha,
                ["P2"] = emptySemanticSha,
                ["ExecutionHeaders"] =
                    Arch7bBracketedGlobalFlatContract.ExecutionHeaderSetSha256,
                ["PositionHeaders"] =
                    Arch7bBracketedGlobalFlatContract.PositionHeaderSetSha256,
                ["ComplementaryAccountEvidence"] =
                    accountEvidence["EvidenceSha256"]!.GetValue<string>(),
                ["CurrentSnapshotDecision"] =
                    decision["EvidenceSha256"]!.GetValue<string>()
            };
            var contract = new JsonObject
            {
                ["ContractVersion"] = Arch7bBracketedGlobalFlatContract.CoreContractVersion,
                ["AccountId"] = Arch7bBracketedGlobalFlatContract.AccountId,
                ["Environment"] = Arch7bBracketedGlobalFlatContract.Environment,
                ["SessionMode"] = Arch7bBracketedGlobalFlatContract.SessionMode,
                ["DownloaderVersion"] = downloaderVersion,
                ["PositionCount"] = 0,
                ["ExecutionCount"] = 0,
                ["DuplicateIdenticalExecutionCount"] = 0,
                ["LatestExecutionTime"] = null,
                ["StableExecutionSet"] = true,
                ["StablePositionSet"] = true,
                ["AsOfLowerBoundUtc"] = "2026-07-27T11:23:45.000Z",
                ["AsOfUpperBoundUtc"] = "2026-07-27T11:23:45.000Z",
                ["BrokerDateSequenceStatus"] =
                    Arch7bBracketedGlobalFlatContract.BrokerDateSequenceStatus,
                ["BrokerBracketSpanSeconds"] = 0,
                ["MaximumBrokerBracketSpanSeconds"] = 30,
                ["Attempts"] = new JsonArray(attempt.DeepClone()),
                ["ExecutionReportSchemaVersion"] =
                    Arch7bBracketedGlobalFlatContract.ExecutionReportSchemaVersion,
                ["PositionReportSchemaVersion"] =
                    Arch7bBracketedGlobalFlatContract.PositionReportSchemaVersion,
                ["T2HeaderSetSha256"] =
                    Arch7bBracketedGlobalFlatContract.ExecutionHeaderSetSha256,
                ["P2HeaderSetSha256"] =
                    Arch7bBracketedGlobalFlatContract.PositionHeaderSetSha256,
                ["EmptyPositionSetAuthority"] =
                    Arch7bBracketedGlobalFlatContract.EmptyPositionSetAuthority,
                ["AccountAuthorityMode"] =
                    Arch7bBracketedGlobalFlatContract.AccountAuthorityMode,
                ["ComplementaryAccountEvidence"] = accountEvidence,
                ["ComplementaryReports"] = new JsonArray(complementaryReports),
                ["OpenPositionsSnapshotSemanticDecision"] = decision,
                ["RawArtifactSetSha256"] = Sha(string.Join("\n", rawSet)),
                ["SemanticArtifactSetSha256"] = Sha(semanticCore.ToJsonString()),
                ["NoOrder"] = true,
                ["NoFix"] = true,
                ["NoDatabaseWrite"] = true
            };
            contract["EvidenceSha256"] = JsonHash(contract);
            var contractPath = Path.Combine(Root,
                "lmax-portal-bracketed-current-position-snapshot-v2.json");
            WriteJson(contractPath, contract);
            var contractFileSha = FileHash(contractPath);

            var manifest = new JsonObject
            {
                ["account_id"] = Arch7bBracketedGlobalFlatContract.AccountId,
                ["session_mode"] = Arch7bBracketedGlobalFlatContract.SessionMode,
                ["flow_capture_contains_headers"] = false,
                ["flow_capture_contains_cookies"] = false,
                ["flow_capture_contains_credentials"] = false,
                ["safety"] = new JsonObject
                {
                    ["order_entry_enabled"] = false,
                    ["operational_orders"] = false,
                    ["production_live"] = false,
                    ["lmax_order_entry_used"] = false,
                    ["lmax_fix_order_entry_used"] = false,
                    ["lmax_accountapi_used"] = false
                },
                ["bracketed_contract_artifact"] = new JsonObject
                {
                    ["path"] =
                        "D:\\evidence\\lmax-portal-bracketed-current-position-snapshot-v2.json",
                    ["size"] = new FileInfo(contractPath).Length,
                    ["sha256"] = contractFileSha
                }
            };
            if (downloaderVersion ==
                Arch7bCoreDownloaderCompatibilityContract.AwsRecoveryDownloaderVersion)
            {
                manifest["downloader_version"] = downloaderVersion;
                manifest["session_recovery_mode"] =
                    Arch7bCoreDownloaderCompatibilityContract.AwsRecoveryMode;
                manifest["credentials_recorded"] = false;
                manifest["secret_values_recorded"] = false;
                manifest["totp_recorded"] = false;
                manifest["secret_source"] =
                    Arch7bCoreDownloaderCompatibilityContract.AwsSecretSource;
                manifest["secret_reference_sha256"] = Hash('7');
                manifest["secret_version_id"] = "sanitized-version-id";
                manifest["secret_keys_contract_version"] =
                    Arch7bCoreDownloaderCompatibilityContract.CredentialSecretContractVersion;
                manifest["login_form_contract"] =
                    Arch7bCoreDownloaderCompatibilityContract.LoginFormContractVersion;
                manifest["automated_bootstrap_contract"] =
                    Arch7bCoreDownloaderCompatibilityContract.AutomatedBootstrapContractVersion;
                manifest["session_already_active"] = false;
                manifest["secret_fetched"] = true;
                manifest["login_performed"] = true;
                manifest["mfa_mode"] = "NOT_CHALLENGED";
                manifest["manual_session_reopen_proof"] = new JsonObject
                {
                    ["status"] =
                        Arch7bCoreDownloaderCompatibilityContract.ManualSessionReopenStatus,
                    ["account_id"] = Arch7bBracketedGlobalFlatContract.AccountId,
                    ["report_type"] =
                        Arch7bCoreDownloaderCompatibilityContract.ManualSessionReopenReportType,
                    ["form_id"] =
                        Arch7bCoreDownloaderCompatibilityContract.ManualSessionReopenFormId,
                    ["secret_read_during_probe"] = false,
                    ["credentials_recorded"] = false
                };
            }
            WriteJson(Path.Combine(Root, "acquisition-manifest.json"), manifest);
            var qualification = new JsonObject
            {
                ["repository"] = "phu-qqb/QQ.Production.Core",
                ["merge_commit"] = Hash('a', 40),
                ["tree"] = Hash('b', 40),
                ["no_order"] = true,
                ["no_fix"] = true,
                ["no_account_api"] = true,
                ["no_database_write"] = true,
                ["no_databento"] = true,
                ["secret_values_recorded"] = false
            };
            WriteJson(Path.Combine(Root, "validation",
                "core-master-qualification-summary.json"), qualification);
            RebuildIndex();
            Expectations = new(
                Hash('a', 40),
                contract["EvidenceSha256"]!.GetValue<string>(),
                contractFileSha,
                FileHash(Path.Combine(Root, "validation",
                    "final-evidence-index.json")));
        }

        private void RebuildIndex()
        {
            var indexPath = Path.Combine(Root, "validation", "final-evidence-index.json");
            if (File.Exists(indexPath)) File.Delete(indexPath);
            var files = Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(path => new JsonObject
                {
                    ["relative_path"] = Path.GetRelativePath(Root, path),
                    ["bytes"] = new FileInfo(path).Length,
                    ["sha256"] = FileHash(path)
                }).ToArray();
            var index = new JsonObject
            {
                ["contract"] = "arch7b_core_master_final_evidence_index_v1",
                ["core_repository_commit"] = Hash('a', 40),
                ["file_count_excluding_index"] = files.Length,
                ["files"] = new JsonArray(files),
                ["no_order"] = true,
                ["no_fix"] = true,
                ["no_account_api"] = true,
                ["no_database_write"] = true,
                ["no_databento"] = true,
                ["secret_values_recorded"] = false
            };
            WriteJson(indexPath, index);
            if (Expectations is not null)
                Expectations = Expectations with { FinalIndexSha256 = FileHash(indexPath) };
        }

        private static void WriteJson(string path, JsonNode value) =>
            File.WriteAllText(path, value.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            }), new UTF8Encoding(false));

        private static string JsonHash(JsonObject value) =>
            Sha(value.ToJsonString());

        private static string FileHash(string path) =>
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

        private static string Sha(string value) =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
