using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch5bQubesLineagePreviewTests
{
    private static readonly DateTimeOffset TargetClose = new(2026, 6, 11, 19, 15, 0, TimeSpan.Zero);

    [Fact]
    public void Parser_uses_last_chronological_row_and_preserves_exact_weight_provenance()
    {
        var path = WriteWeights(";58;59\n202606111900;0.1;-0.2\n202606111915;1e-05;-2.5\n");
        try
        {
            var parsed = new Arch5bAggregatedWeightsParser().Parse(path, Sha('a'));

            Assert.Equal(2, parsed.DataRowCount);
            Assert.Equal(2, parsed.SecurityIdCount);
            Assert.Equal(TargetClose, parsed.TargetCloseUtc);
            Assert.Equal("202606111915", parsed.TargetCloseSourceValue);
            Assert.Collection(parsed.TargetCloseWeights,
                first =>
                {
                    Assert.Equal("58", first.SecurityId);
                    Assert.Equal("1e-05", first.ExactWeightText);
                    Assert.Equal(0, first.Order);
                    Assert.Equal("202606111915:58", first.SourceRowKey);
                },
                second =>
                {
                    Assert.Equal("59", second.SecurityId);
                    Assert.Equal("-2.5", second.ExactWeightText);
                    Assert.Equal(1, second.Order);
                });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(";58;59\n202606111915;0.1\n", "QUBES_OUTPUT_ROW_SHAPE_MALFORMED")]
    [InlineData(";58;58\n202606111915;0.1;0.2\n", "QUBES_OUTPUT_SECURITY_ID_AMBIGUOUS")]
    [InlineData(";58\nnot-a-time;0.1\n", "QUBES_OUTPUT_TIMESTAMP_MALFORMED")]
    [InlineData(";58\n202606111915;NaN\n", "QUBES_OUTPUT_NON_FINITE_OR_INVALID_WEIGHT")]
    [InlineData(";58\n202606111915;Infinity\n", "QUBES_OUTPUT_NON_FINITE_OR_INVALID_WEIGHT")]
    [InlineData(";58\n202606111915;0.1\n202606111900;0.2\n", "QUBES_OUTPUT_TIMESTAMP_ORDER_INVALID")]
    [InlineData(";58\n\n", "QUBES_OUTPUT_BLANK_DATA_ROW")]
    public void Parser_fails_closed_for_malformed_or_ambiguous_matrix(string content, string expected)
    {
        var path = WriteWeights(content);
        try
        {
            var error = Assert.Throws<InvalidDataException>(() => new Arch5bAggregatedWeightsParser().Parse(path, Sha('a')));
            Assert.Equal(expected, error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Contract_accepts_four_separate_evidence_only_lineages_with_missing_market_data_snapshot()
    {
        var validation = new Arch5bLineageContractValidator().Validate(ValidContract());

        Assert.True(validation.IsValid, string.Join(";", validation.Issues));
        Assert.Empty(validation.Issues);
    }

    [Fact]
    public void Contract_accepts_standard_40_character_git_commit_provenance()
    {
        var gitCommit = new string('a', 40);
        var contract = ValidContract() with
        {
            SourceMasterSha = gitCommit,
            Runs = ValidContract().Runs.Select(run => run with { SourceMasterSha = gitCommit }).ToArray()
        };

        var validation = new Arch5bLineageContractValidator().Validate(contract);

        Assert.True(validation.IsValid, string.Join(";", validation.Issues));
    }

    [Fact]
    public void Preview_builds_four_distinct_model_runs_and_target_weight_stages_without_aggregation()
    {
        var result = new Arch5bQubesLineagePreviewService().Build(ValidContract());

        Assert.Equal(4, result.Runs.Count);
        Assert.Equal(4, result.Runs.Select(x => x.ModelRun.ModelRunPreviewId).Distinct().Count());
        Assert.All(result.Runs, x => Assert.Single(x.TargetWeights));
        Assert.True(result.FourIndependentLineages);
        Assert.False(result.CrossStrategyAggregationUsed);
        Assert.False(result.AccountingEligible);
        Assert.False(result.ExecutionAllowed);
    }

    [Fact]
    public void Missing_canonical_inputs_leave_target_position_and_drift_stages_explicitly_blocked()
    {
        var result = new Arch5bQubesLineagePreviewService().Build(ValidContract());

        Assert.All(result.Runs, run =>
        {
            Assert.Contains(Arch5bComputationStatus.BLOCKED_MISSING_CANONICAL_MARKET_DATA_SNAPSHOT, run.TargetPositions.BlockingReasons);
            Assert.Contains(Arch5bComputationStatus.BLOCKED_MISSING_SECURITY_MAPPING, run.TargetPositions.BlockingReasons);
            Assert.Empty(run.TargetPositions.Positions);
            Assert.Contains(Arch5bComputationStatus.BLOCKED_MISSING_CANONICAL_WORKING_LEAVES, run.DriftSnapshot.BlockingReasons);
            Assert.Equal(Arch5bWorkingLeavesStatus.ABSENT_NOT_ASSUMED_ZERO, run.DriftSnapshot.WorkingLeavesStatus);
            Assert.Empty(run.DriftSnapshot.Drifts);
            Assert.False(run.DriftSnapshot.ProducedTradeIntent);
            Assert.False(run.DriftSnapshot.ProducedExecutableQuantity);
        });
    }

    [Fact]
    public void Canonical_test_inputs_use_existing_target_calculator_and_working_leaves_delta_rule()
    {
        var snapshotId = new MarketDataSnapshotId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var instrumentId = new InstrumentId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var venueId = new VenueId(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var contract = WithCanonicalMarketData(ValidContract(), snapshotId);
        var marketData = new MarketDataSnapshot(
            snapshotId,
            instrumentId,
            venueId,
            1.0m,
            1.0m,
            1.0m,
            "CanonicalTestSnapshot",
            TargetClose,
            TargetClose);
        var mapping = new VenueInstrumentMapping(
            new VenueInstrumentId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
            venueId,
            instrumentId,
            "EURUSD",
            "EURUSD",
            1m,
            0m,
            0.0001m,
            0.00001m);
        var inputs = new Arch5bCanonicalPreviewInputs(
            Arch5bLineageContractVersions.TestAccountId,
            Arch5bLineageContractVersions.TestAccountScope,
            new FundId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
            1_000_000m,
            Sha('b'),
            snapshotId,
            TargetClose,
            new Dictionary<string, Arch5bCanonicalSecurityPreviewInput>
            {
                ["58"] = new(
                    "58",
                    instrumentId,
                    "EURUSD",
                    marketData,
                    mapping,
                    CurrentBaseQuantity: 25_000m,
                    SignedReservedWorkingLeaves: 10_000m,
                    PositionSnapshotSha256: Sha('c'),
                    WorkingLeavesSnapshotSha256: Sha('d'))
            });

        var result = new Arch5bQubesLineagePreviewService().Build(contract, inputs);

        Assert.All(result.Runs, run =>
        {
            Assert.Equal(Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW, run.TargetPositions.ComputationStatus);
            var target = Assert.Single(run.TargetPositions.Positions);
            Assert.Equal(100_000m, target.TargetBaseQuantity);
            Assert.Equal(Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW, run.DriftSnapshot.ComputationStatus);
            var drift = Assert.Single(run.DriftSnapshot.Drifts);
            Assert.Equal(65_000m, drift.RemainingDeltaBaseQuantity);
            Assert.Equal(Arch5bWorkingLeavesStatus.CANONICAL_SNAPSHOT_PRESENT, run.DriftSnapshot.WorkingLeavesStatus);
            Assert.False(run.DriftSnapshot.ProducedTradeIntent);
        });
    }

    [Fact]
    public void Missing_working_leaves_blocks_only_drift_and_does_not_block_target_position()
    {
        var canonical = CanonicalInputs();
        var withoutWorkingLeaves = canonical with
        {
            Securities = canonical.Securities.ToDictionary(
                entry => entry.Key,
                entry => entry.Value with { WorkingLeavesSnapshotSha256 = "" },
                StringComparer.Ordinal)
        };
        var contract = WithCanonicalMarketData(ValidContract(), canonical.MarketDataSnapshotId);

        var result = new Arch5bQubesLineagePreviewService().Build(contract, withoutWorkingLeaves);

        Assert.All(result.Runs, run =>
        {
            Assert.Equal(Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW, run.TargetPositions.ComputationStatus);
            Assert.Single(run.TargetPositions.Positions);
            Assert.Equal(Arch5bComputationStatus.BLOCKED_MISSING_CANONICAL_WORKING_LEAVES, run.DriftSnapshot.ComputationStatus);
            Assert.Equal(Arch5bWorkingLeavesStatus.ABSENT_NOT_ASSUMED_ZERO, run.DriftSnapshot.WorkingLeavesStatus);
            Assert.Empty(run.DriftSnapshot.Drifts);
        });
    }

    [Fact]
    public void Preview_is_deterministic_and_repeated_registration_is_idempotent()
    {
        var service = new Arch5bQubesLineagePreviewService();
        var first = service.Build(ValidContract());
        var second = service.Build(ValidContract());
        var registry = new Arch5bLineagePreviewRegistry();

        Assert.Equal(first.PreviewSha256, second.PreviewSha256);
        foreach (var preview in first.Runs)
        {
            Assert.Same(preview, registry.Register(preview));
            Assert.Same(preview, registry.Register(preview));
        }
    }

    [Fact]
    public void Same_logical_run_with_different_output_sha_fails_closed()
    {
        var service = new Arch5bQubesLineagePreviewService();
        var preview = service.Build(ValidContract()).Runs[0];
        var registry = new Arch5bLineagePreviewRegistry();
        registry.Register(preview);
        var conflicting = preview with
        {
            Lineage = preview.Lineage with { OutputSha256 = Sha('e') },
            PreviewSha256 = Sha('f')
        };

        var error = Assert.Throws<InvalidDataException>(() => registry.Register(conflicting));
        Assert.Equal("SAME_RUN_ID_DIFFERENT_SHA_REJECTED", error.Message);
    }

    [Fact]
    public void ManualPaperCycle_and_R009_integration_remain_completed_no_external_and_no_order()
    {
        var result = new Arch5bQubesLineagePreviewService().Build(ValidContract());

        Assert.All(result.Runs, run =>
        {
            Assert.Equal(ManualPaperCycleCliStatus.CompletedNoExternal, run.ManualPaperCycle.Status);
            Assert.True(run.ManualPaperCycle.CompletedNoExternal);
            Assert.False(run.ManualPaperCycle.EconomicCycleExecuted);
            Assert.False(run.ManualPaperCycle.CreatedOrder);
            Assert.Equal("CompletedNoExternal", run.R009.Status);
            Assert.Equal(0, run.R009.ExecutionIntentCount);
            Assert.False(run.R009.ExecutionAllowed);
            Assert.True(run.R009.NotAnOrder);
            Assert.True(run.R009.NoBrokerRoute);
            Assert.True(run.R009.NoFixMessage);
            Assert.False(run.R009.OrderEntryEnabled);
            Assert.Equal("DISABLED_NO_ORDER_ENTRY", run.R009.BrokerSendStatus);
        });
    }

    [Theory]
    [InlineData("unknown-lineage-version")]
    [InlineData("real-account")]
    [InlineData("missing-run-id")]
    [InlineData("duplicate-logical-run")]
    [InlineData("strategy-mismatch")]
    [InlineData("benchmark-divergent")]
    [InlineData("r083-failed")]
    [InlineData("material-difference")]
    [InlineData("sign-flip")]
    [InlineData("transfer-incomplete")]
    [InlineData("fake-market-data-id")]
    [InlineData("target-close-contradictory")]
    [InlineData("unknown-output-version")]
    [InlineData("accounting-enabled")]
    [InlineData("partial-provenance")]
    [InlineData("invalid-git-commit")]
    [InlineData("weight-order")]
    [InlineData("ambiguous-security")]
    public void Contract_negative_matrix_fails_closed(string mutation)
    {
        var invalid = Mutate(ValidContract(), mutation);
        var validation = new Arch5bLineageContractValidator().Validate(invalid);

        Assert.False(validation.IsValid);
        Assert.NotEmpty(validation.Issues);
        Assert.Throws<InvalidDataException>(() => new Arch5bQubesLineagePreviewService().Build(invalid));
    }

    [Fact]
    public void Contract_v2_accepts_exact_historical_diagnostic_flip()
    {
        var contract = WithR083V2(ValidContract());
        var validation = new Arch5bLineageContractValidator().Validate(contract);

        Assert.True(validation.IsValid, string.Join(";", validation.Issues));
        Assert.All(contract.Runs, run =>
        {
            Assert.Equal(1, run.FullMatrixRawSignFlipCount);
            Assert.Equal(0, run.DecisionSliceSignFlipCount);
            Assert.Equal(1, run.DiagnosticOnlyHistoricalFlipCount);
        });
    }

    [Fact]
    public void Contract_v2_round_trips_all_decision_scope_fields_with_deterministic_json()
    {
        var contract = WithR083V2(ValidContract());
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var firstJson = JsonSerializer.Serialize(contract, options);
        var roundTripped = JsonSerializer.Deserialize<Arch5bSessionLineageContractV1>(firstJson, options);
        var secondJson = JsonSerializer.Serialize(roundTripped, options);

        Assert.NotNull(roundTripped);
        Assert.Equal(firstJson, secondJson);
        Assert.All(roundTripped.Runs, run =>
        {
            Assert.Equal(Arch5bLineageContractVersions.R083DecisionEffectiveScopeV2, run.R083ContractVersion);
            Assert.Equal(Arch5bLineageContractVersions.R083DecisionSliceRule, run.R083DecisionSliceSelectionRule);
            Assert.Equal(1, run.FullMatrixRawSignFlipCount);
            Assert.Equal(0, run.FullMatrixMaterialDifferenceCount);
            Assert.Equal(0, run.DecisionSliceSignFlipCount);
            Assert.Equal(0, run.DecisionSliceMaterialDifferenceCount);
            Assert.Equal(1, run.DiagnosticOnlyHistoricalFlipCount);
        });
        var validation = new Arch5bLineageContractValidator().Validate(roundTripped);
        Assert.True(validation.IsValid, string.Join(";", validation.Issues));
    }

    [Theory]
    [InlineData("target-close-flip", "R083_DECISION_SLICE_SIGN_FLIP_NONZERO")]
    [InlineData("historical-material", "R083_FULL_MATRIX_MATERIAL_DIFFERENCE_NONZERO")]
    [InlineData("alias-mismatch", "R083_SIGN_FLIP_ALIAS_MISMATCH")]
    [InlineData("counter-mismatch", "R083_SIGN_FLIP_COUNTERS_INCONSISTENT")]
    [InlineData("selection-rule", "R083_DECISION_SLICE_SELECTION_RULE_INVALID")]
    [InlineData("target-close-source", "R083_TARGET_CLOSE_SOURCE_MISMATCH")]
    [InlineData("unknown-version", "R083_CONTRACT_VERSION_UNKNOWN")]
    public void Contract_v2_fail_closed_matrix_rejects_unsafe_scope(string mutation, string expectedIssue)
    {
        var contract = WithR083V2(ValidContract());
        var runs = contract.Runs.ToArray();
        runs[0] = mutation switch
        {
            "target-close-flip" => runs[0] with
            {
                DecisionSliceSignFlipCount = 1,
                DiagnosticOnlyHistoricalFlipCount = 0
            },
            "historical-material" => runs[0] with
            {
                MaterialDifferenceCount = 1,
                FullMatrixMaterialDifferenceCount = 1
            },
            "alias-mismatch" => runs[0] with { SignFlipCount = 0 },
            "counter-mismatch" => runs[0] with { DiagnosticOnlyHistoricalFlipCount = 0 },
            "selection-rule" => runs[0] with { R083DecisionSliceSelectionRule = "UNKNOWN" },
            "target-close-source" => runs[0] with { TargetCloseSourceValue = "202606111914" },
            "unknown-version" => runs[0] with { R083ContractVersion = "r083_unknown_v99" },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        contract = contract with { Runs = runs };

        var validation = new Arch5bLineageContractValidator().Validate(contract);

        Assert.False(validation.IsValid);
        Assert.Contains(expectedIssue, validation.Issues);
        Assert.Throws<InvalidDataException>(() => new Arch5bQubesLineagePreviewService().Build(contract));
    }

    [Fact]
    public void Historical_v2_diagnostic_counts_do_not_change_any_downstream_decision_output()
    {
        var canonical = CanonicalInputs();
        var baselineContract = WithCanonicalMarketData(ValidContract(), canonical.MarketDataSnapshotId);
        var v2Contract = WithCanonicalMarketData(WithR083V2(ValidContract()), canonical.MarketDataSnapshotId);
        var baseline = new Arch5bQubesLineagePreviewService().Build(baselineContract, canonical);
        var v2 = new Arch5bQubesLineagePreviewService().Build(v2Contract, canonical);

        Assert.Equal(
            Arch5bHashing.HashCanonical(baseline.Runs.Select(run => new
            {
                run.TargetWeights,
                run.TargetPositions,
                run.DriftSnapshot,
                run.ManualPaperCycle,
                run.R009
            })),
            Arch5bHashing.HashCanonical(v2.Runs.Select(run => new
            {
                run.TargetWeights,
                run.TargetPositions,
                run.DriftSnapshot,
                run.ManualPaperCycle,
                run.R009
            })));
        Assert.All(v2.Runs, run =>
        {
            Assert.Single(run.TargetWeights);
            Assert.Equal(Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW, run.TargetPositions.ComputationStatus);
            Assert.Equal(Arch5bComputationStatus.COMPUTED_CANONICAL_PREVIEW, run.DriftSnapshot.ComputationStatus);
            Assert.False(run.DriftSnapshot.ProducedTradeIntent);
            Assert.Equal("CompletedNoExternal", run.R009.Status);
        });
    }

    [Fact]
    public void Same_near_zero_flip_at_target_close_changes_effective_position_sign()
    {
        var canonical = CanonicalInputs();
        var reference = WithTargetCloseWeight(
            WithCanonicalMarketData(WithR083V2(ValidContract(), fullFlips: 0), canonical.MarketDataSnapshotId),
            "-6.26084e-08",
            -6.26084e-08);
        var candidate = WithTargetCloseWeight(
            WithCanonicalMarketData(WithR083V2(ValidContract(), fullFlips: 0), canonical.MarketDataSnapshotId),
            "7.80437e-09",
            7.80437e-09);

        var referencePreview = new Arch5bQubesLineagePreviewService().Build(reference, canonical);
        var candidatePreview = new Arch5bQubesLineagePreviewService().Build(candidate, canonical);

        Assert.All(referencePreview.Runs, run => Assert.True(Assert.Single(run.TargetPositions.Positions).TargetVenueQuantity < 0));
        Assert.All(candidatePreview.Runs, run => Assert.True(Assert.Single(run.TargetPositions.Positions).TargetVenueQuantity > 0));
    }

    [Fact]
    public void Evidence_loader_rejects_missing_root_and_invalid_expected_zip_hash()
    {
        Assert.Equal("ARCH5A_EVIDENCE_ROOT_MISSING", Assert.Throws<InvalidDataException>(() =>
            new Arch5bArch5aEvidenceLoader().Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), Sha('a'))).Message);

        var root = Directory.CreateTempSubdirectory("arch5b-loader-");
        try
        {
            Assert.Equal("ARCH5A_EXPECTED_ZIP_SHA_INVALID", Assert.Throws<InvalidDataException>(() =>
                new Arch5bArch5aEvidenceLoader().Load(root.FullName, "short")).Message);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void Evidence_loader_accepts_complete_synthetic_four_run_lineage()
    {
        using var fixture = new SyntheticArch5aEvidence();

        var result = new Arch5bArch5aEvidenceLoader().Load(fixture.Root, fixture.EvidenceZipSha256);

        Assert.True(result.Verification.FinalSuccess);
        Assert.True(result.Verification.CrossManifestLineageVerified);
        Assert.True(result.Verification.FullOutputsVerified);
        Assert.Equal(4, result.Contract.Runs.Count);
        Assert.All(result.Contract.Runs, run => Assert.Null(run.MarketDataSnapshotId));
    }

    [Theory]
    [InlineData("zip-sha", "ARCH5A_EVIDENCE_HASH_MISMATCH")]
    [InlineData("output-hash", "QUBES_OUTPUT_HASH_MISMATCH")]
    [InlineData("output-missing", "QUBES_OUTPUT_MISSING")]
    [InlineData("run-id", "PROVENANCE_FIELD_MISSING:run_id")]
    [InlineData("strategy", "PROVENANCE_FIELD_MISMATCH:strategy")]
    [InlineData("benchmark", "BENCHMARK_PARAMETER_DIVERGENT")]
    [InlineData("r083", "PROVENANCE_BOOLEAN_MISMATCH:final_success")]
    [InlineData("material-difference", "R083_MATERIAL_OR_SIGN_REGRESSION")]
    [InlineData("transfer", "PROVENANCE_BOOLEAN_MISMATCH:complete")]
    [InlineData("manifest-lineage", "RUN_MANIFEST_LINEAGE_INCOMPLETE")]
    public void Evidence_loader_fail_closed_matrix_rejects_invalid_provenance(string mutation, string expected)
    {
        using var fixture = new SyntheticArch5aEvidence();
        var expectedZipSha = fixture.EvidenceZipSha256;
        switch (mutation)
        {
            case "zip-sha": expectedZipSha = Sha('f'); break;
            case "output-hash": File.AppendAllText(fixture.Infx7OutputPath, "corruption"); break;
            case "output-missing": File.Delete(fixture.Infx7OutputPath); break;
            case "run-id": fixture.Edit("infx7_run_manifest.json", root => root["run_id"] = ""); break;
            case "strategy": fixture.Edit("infx7_run_manifest.json", root => root["strategy"] = "INFX11"); break;
            case "benchmark": fixture.Edit("infx7_run_manifest.json", root => root["benchmark_parameter"] = "9"); break;
            case "r083": fixture.Edit("infx7_run_manifest.json", root => root["r083_comparison"]!["final_success"] = false); break;
            case "material-difference": fixture.Edit("infx7_run_manifest.json", root => root["r083_comparison"]!["material_difference_count"] = 1); break;
            case "transfer": fixture.Edit("infx7_run_manifest.json", root => root["transfer_status"]!["complete"] = false); break;
            case "manifest-lineage": fixture.Edit("infx7_run_manifest.json", root => root["output_sha"] = Sha('e')); break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        var error = Assert.Throws<InvalidDataException>(() =>
            new Arch5bArch5aEvidenceLoader().Load(fixture.Root, expectedZipSha));

        Assert.Equal(expected, error.Message);
    }

    private static Arch5bSessionLineageContractV1 ValidContract()
    {
        var strategies = new[] { ("INFX7", 4.5m), ("INFX8", 2.1m), ("INFX9", 1.4m), ("INFX10", 0.6m) };
        var runs = strategies.Select((item, index) =>
        {
            var outputSha = Sha((char)('1' + index));
            var exactWeight = "0.1";
            return new Arch5bRunLineageContractV1(
                Arch5bLineageContractVersions.LineageV1,
                Arch5bLineageContractVersions.SourceQubesWeightsOutputV1,
                "session-001",
                "session-001",
                $"session-001:{item.Item1}",
                item.Item1,
                item.Item2,
                Sha('a'),
                Sha('b'),
                Sha('c'),
                "bundle-version-001",
                Sha('d'),
                outputSha,
                100 + index,
                $"outputs/{item.Item1}/AggregatedWeights.txt",
                Arch5bLineageContractVersions.OutputQubesWeightsOutputV1,
                TargetClose.AddMinutes(index),
                TargetClose,
                TargetClose,
                "202606111915",
                "PRODMANAGERV4_LAST_CHRONOLOGICAL_DATA_ROW",
                "PASS",
                0,
                0,
                true,
                null,
                null,
                Arch5bLineageContractVersions.MissingMarketDataSnapshot,
                Arch5bLineageContractVersions.EvidenceOnlyClassification,
                true,
                false,
                false,
                [new Arch5bTargetCloseWeightV1("58", exactWeight, 0.1, 0, "202606111915:58", Arch5bHashing.Sha256Hex($"{outputSha}:202606111915:58:0:{exactWeight}"))]);
        }).ToArray();

        return new Arch5bSessionLineageContractV1(
            Arch5bLineageContractVersions.LineageV1,
            Arch5bLineageContractVersions.SourceQubesWeightsOutputV1,
            "session-001",
            Arch5bLineageContractVersions.TestAccountId,
            Arch5bLineageContractVersions.TestAccountScope,
            Sha('a'),
            Sha('b'),
            Sha('c'),
            "bundle-version-001",
            new DateTimeOffset(2026, 7, 16, 12, 21, 47, TimeSpan.Zero),
            Arch5bLineageContractVersions.EvidenceOnlyClassification,
            true,
            false,
            false,
            runs);
    }

    private static Arch5bSessionLineageContractV1 WithR083V2(
        Arch5bSessionLineageContractV1 value,
        int fullFlips = 1)
        => value with
        {
            Runs = value.Runs.Select(run => run with
            {
                R083ContractVersion = Arch5bLineageContractVersions.R083DecisionEffectiveScopeV2,
                R083DecisionSliceSelectionRule = Arch5bLineageContractVersions.R083DecisionSliceRule,
                SignFlipCount = fullFlips,
                MaterialDifferenceCount = 0,
                FullMatrixRawSignFlipCount = fullFlips,
                FullMatrixMaterialDifferenceCount = 0,
                DecisionSliceSignFlipCount = 0,
                DecisionSliceMaterialDifferenceCount = 0,
                DiagnosticOnlyHistoricalFlipCount = fullFlips
            }).ToArray()
        };

    private static Arch5bSessionLineageContractV1 WithTargetCloseWeight(
        Arch5bSessionLineageContractV1 value,
        string exactWeight,
        double weight)
        => value with
        {
            Runs = value.Runs.Select(run => run with
            {
                TargetCloseWeights =
                [
                    run.TargetCloseWeights[0] with
                    {
                        ExactWeightText = exactWeight,
                        Weight = weight
                    }
                ]
            }).ToArray()
        };

    private static Arch5bSessionLineageContractV1 WithCanonicalMarketData(Arch5bSessionLineageContractV1 value, MarketDataSnapshotId snapshotId)
        => value with
        {
            Runs = value.Runs.Select(run => run with
            {
                MarketDataSnapshotId = snapshotId.Value.ToString("D"),
                MarketDataSnapshotEvidenceSha256 = Sha('9'),
                MarketDataSnapshotStatus = "CANONICAL_MARKET_DATA_SNAPSHOT_PRESENT"
            }).ToArray()
        };

    private static Arch5bCanonicalPreviewInputs CanonicalInputs()
    {
        var snapshotId = new MarketDataSnapshotId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var instrumentId = new InstrumentId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var venueId = new VenueId(Guid.Parse("33333333-3333-3333-3333-333333333333"));
        return new Arch5bCanonicalPreviewInputs(
            Arch5bLineageContractVersions.TestAccountId,
            Arch5bLineageContractVersions.TestAccountScope,
            new FundId(Guid.Parse("55555555-5555-5555-5555-555555555555")),
            1_000_000m,
            Sha('b'),
            snapshotId,
            TargetClose,
            new Dictionary<string, Arch5bCanonicalSecurityPreviewInput>
            {
                ["58"] = new(
                    "58",
                    instrumentId,
                    "EURUSD",
                    new MarketDataSnapshot(snapshotId, instrumentId, venueId, 1.0m, 1.0m, 1.0m, "CanonicalTestSnapshot", TargetClose, TargetClose),
                    new VenueInstrumentMapping(
                        new VenueInstrumentId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
                        venueId,
                        instrumentId,
                        "EURUSD",
                        "EURUSD",
                        1m,
                        0m,
                        0.0001m,
                        0.00001m),
                    CurrentBaseQuantity: 25_000m,
                    SignedReservedWorkingLeaves: 10_000m,
                    PositionSnapshotSha256: Sha('c'),
                    WorkingLeavesSnapshotSha256: Sha('d'))
            });
    }

    private static Arch5bSessionLineageContractV1 Mutate(Arch5bSessionLineageContractV1 value, string mutation)
    {
        var first = value.Runs[0];
        Arch5bRunLineageContractV1 ReplaceFirst(Arch5bRunLineageContractV1 replacement)
            => replacement;
        var runs = value.Runs.ToArray();

        switch (mutation)
        {
            case "unknown-lineage-version": return value with { LineageContractVersion = "unknown" };
            case "real-account": return value with { PreviewAccountId = Arch5bLineageContractVersions.RealAccountId };
            case "missing-run-id": runs[0] = ReplaceFirst(first with { SourceRunId = "" }); break;
            case "duplicate-logical-run": runs[1] = runs[1] with { LogicalRunId = runs[0].LogicalRunId }; break;
            case "strategy-mismatch": runs[0] = ReplaceFirst(first with { StrategyId = "INFX11" }); break;
            case "benchmark-divergent": runs[0] = ReplaceFirst(first with { BenchmarkParameter = 9m }); break;
            case "r083-failed": runs[0] = ReplaceFirst(first with { R083Status = "FAIL" }); break;
            case "material-difference": runs[0] = ReplaceFirst(first with { MaterialDifferenceCount = 1 }); break;
            case "sign-flip": runs[0] = ReplaceFirst(first with { SignFlipCount = 1 }); break;
            case "transfer-incomplete": runs[0] = ReplaceFirst(first with { TransferVerified = false }); break;
            case "fake-market-data-id": runs[0] = ReplaceFirst(first with { MarketDataSnapshotId = Guid.NewGuid().ToString("D") }); break;
            case "target-close-contradictory": runs[0] = ReplaceFirst(first with { OutputAsOfUtc = first.TargetCloseUtc.AddMinutes(1) }); break;
            case "unknown-output-version": runs[0] = ReplaceFirst(first with { OutputContractVersion = "unknown" }); break;
            case "accounting-enabled": return value with { AccountingEligible = true };
            case "partial-provenance": return value with { RunnerPackageSha256 = "" };
            case "invalid-git-commit": return value with { SourceMasterSha = "not-a-git-commit" };
            case "weight-order": runs[0] = ReplaceFirst(first with { TargetCloseWeights = [first.TargetCloseWeights[0] with { Order = 2 }] }); break;
            case "ambiguous-security": runs[0] = ReplaceFirst(first with { TargetCloseWeights = [first.TargetCloseWeights[0], first.TargetCloseWeights[0] with { Order = 1 }] }); break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        return value with { Runs = runs };
    }

    private sealed class SyntheticArch5aEvidence : IDisposable
    {
        private static readonly (string Strategy, string Benchmark)[] Strategies =
        [
            ("INFX7", "4.5"),
            ("INFX8", "2.1000000000000001"),
            ("INFX9", "1.3999999999999999"),
            ("INFX10", "0.59999999999999998")
        ];

        public SyntheticArch5aEvidence()
        {
            Root = Directory.CreateTempSubdirectory("arch5b-arch5a-").FullName;
            var evidenceZip = Path.Combine(Root, Arch5bArch5aEvidenceLoader.EvidenceZipFileName);
            File.WriteAllText(evidenceZip, "synthetic ARCH5A evidence package");
            EvidenceZipSha256 = FileSha256(evidenceZip);

            var sessionRuns = new List<object>();
            var outputs = new List<object>();
            var transfers = new List<object>();
            foreach (var (strategy, benchmark) in Strategies)
            {
                var outputDirectory = Path.Combine(Root, "outputs", strategy);
                Directory.CreateDirectory(outputDirectory);
                var outputPath = Path.Combine(outputDirectory, "AggregatedWeights.txt");
                File.WriteAllText(outputPath, ";58" + Environment.NewLine + "202606111915;0.1" + Environment.NewLine);
                var outputSha = FileSha256(outputPath);
                var outputSize = new FileInfo(outputPath).Length;
                if (strategy == "INFX7")
                {
                    Infx7OutputPath = outputPath;
                }

                WriteJson(Path.Combine(outputDirectory, "aggregated_weights_validation.json"), new
                {
                    schema = "aggregated_weights_validation_v1",
                    parseable = true,
                    final_success = true,
                    sha256 = outputSha,
                    data_row_count = 1,
                    header_security_id_count = 1
                });
                WriteJson(Path.Combine(Root, strategy.ToLowerInvariant() + "_run_manifest.json"), new
                {
                    schema = "arch5a_run_manifest_v1",
                    session_id = "synthetic-session",
                    run_id = "synthetic-session",
                    strategy,
                    benchmark_parameter = benchmark,
                    master_sha = new string('a', 40),
                    package_sha = Sha('b'),
                    bundle_manifest_sha = Sha('c'),
                    final_run_status = "SUCCESS",
                    semantic_validation = new { final_success = true },
                    no_order_status = new { order_entry_enabled = false, db_apply = false, real_account_operational_use = false },
                    r083_comparison = new { final_success = true, material_difference_count = 0, sign_flip_count = 0 },
                    transfer_status = new { complete = true, evidence_zip_sha256 = EvidenceZipSha256 },
                    output_path = $"outputs/{strategy}/AggregatedWeights.txt",
                    output_sha = outputSha,
                    output_size = outputSize,
                    end_utc = "2026-07-16T12:21:47Z",
                    executable_sha = Sha('d')
                });
                sessionRuns.Add(new { strategy, output_sha256 = outputSha, evidence_zip_sha256 = EvidenceZipSha256 });
                outputs.Add(new
                {
                    strategy,
                    finite = true,
                    relative_path = $"outputs/{strategy}/AggregatedWeights.txt",
                    sha256 = outputSha,
                    size_bytes = outputSize,
                    material_difference_count = 0,
                    sign_flip_count = 0,
                    rows = 1,
                    columns = 2
                });
                transfers.Add(new { strategy, complete = true, evidence_zip_sha256 = EvidenceZipSha256 });
            }

            WriteJson(Path.Combine(Root, "daily_session_manifest.json"), new
            {
                session_contract_version = "anubis_daily_gpu_session_v1",
                session_id = "synthetic-session",
                master_sha = new string('a', 40),
                package_sha256 = Sha('b'),
                bundle_archive_sha256 = Sha('c'),
                bundle_version_id = "synthetic-bundle-version",
                final_stopped_at = "2026-07-16T12:21:47Z",
                safety = new { order_entry_enabled = false, broker_send_status = "DISABLED_NO_ORDER_ENTRY", db_apply = false, real_account_operational_use = false },
                runs = sessionRuns
            });
            WriteJson(Path.Combine(Root, "per_run_output_manifest.json"), new
            {
                schema = "arch5a_per_run_output_manifest_v1",
                final_success = true,
                outputs
            });
            WriteJson(Path.Combine(Root, "per_run_transfer_manifest.json"), new
            {
                schema = "arch5a_per_run_transfer_manifest_v1",
                session_id = "synthetic-session",
                final_success = true,
                transfers
            });
        }

        public string Root { get; }
        public string EvidenceZipSha256 { get; }
        public string Infx7OutputPath { get; } = "";

        public void Edit(string relativePath, Action<JsonObject> edit)
        {
            var path = Path.Combine(Root, relativePath);
            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            edit(root);
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private static void WriteJson(string path, object value)
            => File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));

        private static string FileSha256(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }
    }

    private static string WriteWeights(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"arch5b-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, content.Replace("\n", Environment.NewLine));
        return path;
    }

    private static string Sha(char value) => new(value, 64);
}
