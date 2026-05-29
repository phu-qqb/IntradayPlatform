using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class QubesEngineContractsTests
{
    [Fact]
    public void R005_run_id_is_prototype_only()
    {
        var request = ValidRequest() with
        {
            Run = ValidRun() with { QubesRunId = QubesKnownPrototypeIds.R005RunId }
        };

        var result = Validate(request);

        Assert.False(result.IsAllowed);
        Assert.Contains(result.Issues, x => x.Code == QubesProductionValidationIssueCode.R005RunIdPrototypeOnly);
    }

    [Fact]
    public void R005_output_id_is_rejected_for_production_accounting()
    {
        var request = ValidRequest() with
        {
            Output = ValidOutput() with { QubesWeightsOutputId = QubesKnownPrototypeIds.R005OutputId }
        };

        var result = Validate(request);

        Assert.False(result.IsAllowed);
        Assert.Contains(result.Issues, x => x.Code == QubesProductionValidationIssueCode.R005OutputIdRejected);
    }

    [Fact]
    public void R005_output_hash_is_rejected_for_production_accounting()
    {
        var request = ValidRequest() with
        {
            Output = ValidOutput() with { QubesOutputHash = QubesKnownPrototypeIds.R005OutputHash }
        };

        var result = Validate(request);

        Assert.False(result.IsAllowed);
        Assert.Contains(result.Issues, x => x.Code == QubesProductionValidationIssueCode.R005OutputHashRejected);
    }

    [Fact]
    public void Null_market_data_snapshot_id_is_blocked()
    {
        var request = ValidRequest() with
        {
            InputSnapshot = ValidInput() with { MarketDataSnapshotId = null },
            Output = ValidOutput() with { MarketDataSnapshotId = null },
            Run = ValidRun([])
        };

        var result = Validate(request);

        Assert.False(result.IsAllowed);
        Assert.Contains(result.Issues, x => x.Code == QubesProductionValidationIssueCode.MissingMarketDataSnapshotId);
        Assert.Contains(result.Issues, x => x.Code == QubesProductionValidationIssueCode.RunMarketDataBindingMissing);
    }

    [Fact]
    public void External_engine_required_without_adapter_is_blocked()
    {
        var request = ValidRequest() with
        {
            InputSnapshot = ValidInput() with { EngineKind = QubesEngineKind.ExternalEngineRequired },
            Run = ValidRun() with { EngineKind = QubesEngineKind.ExternalEngineRequired },
            Output = ValidOutput() with { EngineKind = QubesEngineKind.ExternalEngineRequired },
            RealEngineAdapterRegistered = false
        };

        var result = Validate(request);

        Assert.False(result.IsAllowed);
        Assert.Contains(result.Issues, x => x.Code == QubesProductionValidationIssueCode.ExternalEngineRequired);
    }

    [Fact]
    public void Mismatched_market_data_snapshot_between_run_and_output_is_invalid()
    {
        var otherMarketDataSnapshotId = MarketDataSnapshotId.New();
        var request = ValidRequest() with
        {
            Output = ValidOutput() with { MarketDataSnapshotId = otherMarketDataSnapshotId }
        };

        var result = Validate(request);

        Assert.False(result.IsAllowed);
        Assert.Contains(result.Issues, x => x.Code == QubesProductionValidationIssueCode.OutputMarketDataMismatch);
    }

    [Fact]
    public void Valid_non_null_real_engine_linkage_passes_validation()
    {
        var result = Validate(ValidRequest());

        Assert.True(result.IsAllowed);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Sandbox_prototype_never_passes_production_accounting_validation()
    {
        var request = ValidRequest() with
        {
            InputSnapshot = ValidInput() with { EngineKind = QubesEngineKind.SandboxPrototype },
            Run = ValidRun() with { EngineKind = QubesEngineKind.SandboxPrototype },
            Output = ValidOutput() with { EngineKind = QubesEngineKind.SandboxPrototype }
        };

        var result = Validate(request);

        Assert.False(result.IsAllowed);
        Assert.Contains(result.Issues, x => x.Code == QubesProductionValidationIssueCode.SandboxPrototypeBlocked);
    }

    [Fact]
    public async Task Disabled_adapter_is_external_engine_required_and_produces_no_weights()
    {
        var adapter = new DisabledQubesEngineAdapter();

        var result = await adapter.ProduceWeightsAsync(ValidInput(), CancellationToken.None);

        Assert.Equal(QubesEngineKind.ExternalEngineRequired, adapter.EngineKind);
        Assert.False(adapter.FabricatesMarketData);
        Assert.False(adapter.RequiresAccountId);
        Assert.False(adapter.RequiresAccountCurrency);
        Assert.False(adapter.GeneratesOrders);
        Assert.False(adapter.GeneratesFills);
        Assert.False(adapter.WritesLedger);
        Assert.Equal(QubesEngineAdapterStatus.ExternalEngineRequired, result.Status);
        Assert.False(result.Succeeded);
        Assert.Null(result.Output);
    }

    [Fact]
    public void Run_id_must_bind_to_exactly_one_market_data_snapshot_id()
    {
        var request = ValidRequest() with
        {
            Run = ValidRun([MarketDataSnapshotIdValue, MarketDataSnapshotId.New()])
        };

        var result = Validate(request);

        Assert.False(result.IsAllowed);
        Assert.Contains(result.Issues, x => x.Code == QubesProductionValidationIssueCode.RunMarketDataBindingNotUnique);
    }

    [Fact]
    public void Sandbox_prototype_output_boundary_blocks_r005_for_production_accounting()
    {
        var output = R005SandboxOutput();

        var production = QubesProductionBoundaryGuard.ValidateSandboxPrototypeOutput(
            output,
            QubesProductionUsage.Production,
            QubesKnownPrototypeIds.R005OutputHash);
        var accounting = QubesProductionBoundaryGuard.ValidateSandboxPrototypeOutput(
            output,
            QubesProductionUsage.Accounting,
            QubesKnownPrototypeIds.R005OutputHash);

        Assert.False(production.IsAllowed);
        Assert.False(accounting.IsAllowed);
        Assert.Contains(production.Issues, x => x.Code == QubesProductionValidationIssueCode.SandboxPrototypeBlocked);
        Assert.Contains(production.Issues, x => x.Code == QubesProductionValidationIssueCode.R005RunIdPrototypeOnly);
        Assert.Contains(production.Issues, x => x.Code == QubesProductionValidationIssueCode.R005OutputIdRejected);
        Assert.Contains(production.Issues, x => x.Code == QubesProductionValidationIssueCode.R005OutputHashRejected);
        Assert.Contains(production.Issues, x => x.Code == QubesProductionValidationIssueCode.MissingMarketDataSnapshotId);
    }

    [Fact]
    public void Sandbox_candidate_factory_keeps_r005_output_preview_only_after_guardrail_adoption()
    {
        var output = R005SandboxOutput();
        var transform = new SandboxQubesExecutionUniverseTransformer().Transform(output);

        var candidate = new SandboxQubesPmsIntentCandidateFactory().CreatePreviewOnlyCandidate(
            output,
            transform,
            "qubes-operationalization-r005-cycle-20251217T020000Z-001",
            "ExistingSandboxProfile",
            "SANDBOX_QUANTITY_POLICY_BLOCKED_MISSING_TARGET_NOTIONAL");

        Assert.False(candidate.ExecutionReady);
        Assert.True(candidate.SandboxOnly);
        Assert.True(candidate.NotProduction);
        Assert.True(candidate.NotAccounting);
        Assert.True(candidate.NotExecuted);
        Assert.True(candidate.NotLedgerCommit);
        Assert.Equal(QubesKnownPrototypeIds.R005RunId, candidate.SandboxQubesRunId);
        Assert.Equal(QubesKnownPrototypeIds.R005OutputId, candidate.QubesOutputId);
        Assert.Null(candidate.MarketDataSnapshotId);
    }

    [Fact]
    public void Qubes_fixture_ingestion_boundary_is_external_engine_required_and_not_production_eligible()
    {
        var ingestion = new QubesFxWeightsFixtureIngestionService().ParseNormalizeAndMap(new QubesFxWeightsIngestionRequest(
            new QubesRunId("qubes-fixture-boundary-r001"),
            new DateTimeOffset(2026, 05, 27, 12, 00, 00, TimeSpan.Zero),
            new DateTimeOffset(2026, 05, 27, 12, 15, 00, TimeSpan.Zero),
            15,
            "QQ_MASTER",
            "IntradayFxModel",
            1_000_000m,
            TargetQuantityMode.PortfolioBaseCurrencyNotional,
            ["EURUSD Curncy;0.10"]));

        var result = QubesProductionBoundaryGuard.ValidateFixtureIngestion(ingestion, QubesProductionUsage.Production);

        Assert.True(ingestion.Succeeded);
        Assert.False(result.IsAllowed);
        Assert.Contains(result.Issues, x => x.Code == QubesProductionValidationIssueCode.ExternalEngineRequired);
        Assert.Contains(result.Issues, x => x.Code == QubesProductionValidationIssueCode.MissingQubesInputSnapshotId);
        Assert.Contains(result.Issues, x => x.Code == QubesProductionValidationIssueCode.MissingQubesWeightsOutputId);
        Assert.Contains(result.Issues, x => x.Code == QubesProductionValidationIssueCode.MissingMarketDataSnapshotId);
    }

    [Fact]
    public void Sandbox_prototype_descriptor_registration_is_rejected()
    {
        var result = ValidateDescriptor(ValidDescriptor() with { EngineKind = QubesEngineKind.SandboxPrototype });

        Assert.False(result.IsValid);
        Assert.False(result.DescriptorCanRegisterRealEngine);
        Assert.False(result.AuthorizesProductionAccountingWeights);
        Assert.Contains(result.Issues, x => x.Code == QubesEngineRegistrationIssueCode.SandboxPrototypeCannotRegister);
        Assert.Contains(result.Issues, x => x.Code == QubesEngineRegistrationIssueCode.EngineKindMustBeRealEngine);
    }

    [Fact]
    public void External_engine_required_descriptor_is_blocked_not_production_eligible()
    {
        var result = ValidateDescriptor(ValidDescriptor() with { EngineKind = QubesEngineKind.ExternalEngineRequired });

        Assert.False(result.IsValid);
        Assert.False(result.DescriptorCanRegisterRealEngine);
        Assert.False(result.AuthorizesProductionAccountingWeights);
        Assert.Contains(result.Issues, x => x.Code == QubesEngineRegistrationIssueCode.ExternalEngineRequiredNotEligible);
        Assert.IsType<DisabledQubesEngineAdapter>(result.Adapter);
    }

    [Fact]
    public void Real_engine_descriptor_missing_engine_id_is_invalid()
    {
        var result = ValidateDescriptor(ValidDescriptor() with { EngineId = "" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesEngineRegistrationIssueCode.MissingEngineId);
    }

    [Fact]
    public void Real_engine_descriptor_missing_build_fingerprint_is_invalid()
    {
        var result = ValidateDescriptor(ValidDescriptor() with { BuildFingerprint = " " });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesEngineRegistrationIssueCode.MissingBuildFingerprint);
    }

    [Fact]
    public void Real_engine_descriptor_with_r005_output_id_or_hash_is_invalid()
    {
        var result = ValidateDescriptor(ValidDescriptor() with
        {
            EvidenceQubesWeightsOutputId = QubesKnownPrototypeIds.R005OutputId,
            EvidenceQubesOutputHash = QubesKnownPrototypeIds.R005OutputHash,
            BuildFingerprint = QubesKnownPrototypeIds.R005OutputHash
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesEngineRegistrationIssueCode.R005OutputIdRejected);
        Assert.Contains(result.Issues, x => x.Code == QubesEngineRegistrationIssueCode.R005OutputHashRejected);
    }

    [Fact]
    public void Valid_real_engine_descriptor_passes_registration_validation()
    {
        var result = ValidateDescriptor(ValidDescriptor());

        Assert.True(result.IsValid);
        Assert.True(result.DescriptorCanRegisterRealEngine);
        Assert.False(result.AuthorizesProductionAccountingWeights);
        Assert.Empty(result.Issues);
        Assert.Equal(QubesEngineKind.RealEngine, result.Adapter.EngineKind);
    }

    [Fact]
    public async Task Valid_real_engine_descriptor_alone_does_not_produce_or_authorize_production_accounting_weights()
    {
        var registration = ValidateDescriptor(ValidDescriptor());

        var adapterResult = await registration.Adapter.ProduceWeightsAsync(ValidInput(), CancellationToken.None);

        Assert.True(registration.IsValid);
        Assert.False(registration.AuthorizesProductionAccountingWeights);
        Assert.Equal(QubesEngineAdapterStatus.BlockedNotConfigured, adapterResult.Status);
        Assert.False(adapterResult.Succeeded);
        Assert.Null(adapterResult.Output);
    }

    [Fact]
    public void Valid_descriptor_with_null_market_data_snapshot_usage_remains_blocked()
    {
        var registration = ValidateDescriptor(ValidDescriptor());
        var request = ValidRequest() with
        {
            InputSnapshot = ValidInput() with { MarketDataSnapshotId = null },
            Output = ValidOutput() with { MarketDataSnapshotId = null },
            Run = ValidRun([]),
            RealEngineAdapterRegistered = registration.IsValid
        };

        var result = Validate(request);

        Assert.True(registration.IsValid);
        Assert.False(result.IsAllowed);
        Assert.Contains(result.Issues, x => x.Code == QubesProductionValidationIssueCode.MissingMarketDataSnapshotId);
    }

    [Fact]
    public void Valid_descriptor_plus_valid_real_engine_linkage_passes_existing_production_usage_validator()
    {
        var registration = ValidateDescriptor(ValidDescriptor());

        var result = Validate(ValidRequest() with { RealEngineAdapterRegistered = registration.IsValid });

        Assert.True(registration.IsValid);
        Assert.True(result.IsAllowed);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Registry_returns_disabled_adapter_when_no_valid_descriptor_is_configured()
    {
        var registry = new QubesEngineAdapterRegistry();

        var missing = registry.Resolve(null);
        var invalid = registry.Resolve(ValidDescriptor() with { EngineId = "" });

        Assert.IsType<DisabledQubesEngineAdapter>(missing);
        Assert.IsType<DisabledQubesEngineAdapter>(invalid);
    }

    [Fact]
    public void Empty_qubes_input_snapshot_id_is_invalid()
    {
        var result = ValidateSnapshot(ValidSnapshot() with { QubesInputSnapshotId = "" });

        Assert.False(result.IsValid);
        Assert.False(result.SnapshotCanFeedRealEngine);
        Assert.False(result.AuthorizesProductionAccountingWeights);
        Assert.Contains(result.Issues, x => x.Code == QubesInputSnapshotIssueCode.MissingQubesInputSnapshotId);
    }

    [Fact]
    public void Empty_snapshot_market_data_snapshot_id_is_blocked()
    {
        var result = ValidateSnapshot(ValidSnapshot() with { MarketDataSnapshotId = null });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesInputSnapshotIssueCode.MissingMarketDataSnapshotId);
    }

    [Fact]
    public void Empty_input_contract_version_is_invalid()
    {
        var result = ValidateSnapshot(ValidSnapshot() with { InputContractVersion = " " });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesInputSnapshotIssueCode.MissingInputContractVersion);
    }

    [Fact]
    public void Empty_manifest_hash_is_invalid()
    {
        var result = ValidateSnapshot(ValidSnapshot() with { ManifestHash = "" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesInputSnapshotIssueCode.MissingManifestHash);
    }

    [Fact]
    public void Zero_snapshot_components_are_invalid_for_real_engine_production_accounting_use()
    {
        var result = ValidateSnapshot(ValidSnapshot() with { Components = [] });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesInputSnapshotIssueCode.MissingComponents);
    }

    [Fact]
    public void Snapshot_component_missing_hash_is_invalid()
    {
        var result = ValidateSnapshot(ValidSnapshot() with
        {
            Components =
            [
                ValidComponent(QubesInputSnapshotComponentKind.QubesUniverse, "universe", null, 0)
            ]
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesInputSnapshotIssueCode.MissingComponentHash);
    }

    [Fact]
    public void Duplicate_snapshot_component_identity_or_order_is_invalid()
    {
        var result = ValidateSnapshot(ValidSnapshot() with
        {
            Components =
            [
                ValidComponent(QubesInputSnapshotComponentKind.QubesSignals, "signals", "sha256:signals", 0),
                ValidComponent(QubesInputSnapshotComponentKind.QubesSignals, "signals", "sha256:signals", 0)
            ]
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesInputSnapshotIssueCode.DuplicateComponentOrder);
        Assert.Contains(result.Issues, x => x.Code == QubesInputSnapshotIssueCode.DuplicateComponentIdentity);
        Assert.Contains(result.Issues, x => x.Code == QubesInputSnapshotIssueCode.DuplicateComponentHash);
    }

    [Fact]
    public void R005_output_hash_used_as_snapshot_manifest_or_component_hash_is_rejected()
    {
        var result = ValidateSnapshot(ValidSnapshot() with
        {
            ManifestHash = QubesKnownPrototypeIds.R005OutputHash,
            Components =
            [
                ValidComponent(QubesInputSnapshotComponentKind.QubesUniverse, "universe", QubesKnownPrototypeIds.R005OutputHash, 0)
            ]
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesInputSnapshotIssueCode.R005OutputHashRejected);
    }

    [Fact]
    public void R005_run_id_or_output_id_used_as_snapshot_evidence_is_rejected()
    {
        var runIdResult = ValidateSnapshot(ValidSnapshot() with
        {
            ManifestHash = QubesKnownPrototypeIds.R005RunId,
            Components =
            [
                ValidComponent(QubesInputSnapshotComponentKind.QubesUniverse, "universe", QubesKnownPrototypeIds.R005RunId, 0)
            ]
        });
        var outputIdResult = ValidateSnapshot(ValidSnapshot() with
        {
            QubesInputSnapshotId = QubesKnownPrototypeIds.R005OutputId,
            Components =
            [
                ValidComponent(QubesInputSnapshotComponentKind.QubesUniverse, "universe", QubesKnownPrototypeIds.R005OutputId, 0)
            ]
        });

        Assert.False(runIdResult.IsValid);
        Assert.False(outputIdResult.IsValid);
        Assert.Contains(runIdResult.Issues, x => x.Code == QubesInputSnapshotIssueCode.R005RunIdRejected);
        Assert.Contains(outputIdResult.Issues, x => x.Code == QubesInputSnapshotIssueCode.R005OutputIdRejected);
    }

    [Fact]
    public void Sandbox_prototype_snapshot_is_blocked()
    {
        var result = ValidateSnapshot(ValidSnapshot() with { EngineKind = QubesEngineKind.SandboxPrototype });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesInputSnapshotIssueCode.SandboxPrototypeBlocked);
        Assert.Contains(result.Issues, x => x.Code == QubesInputSnapshotIssueCode.EngineKindMustBeRealEngine);
    }

    [Fact]
    public void External_engine_required_snapshot_is_blocked()
    {
        var result = ValidateSnapshot(ValidSnapshot() with { EngineKind = QubesEngineKind.ExternalEngineRequired });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesInputSnapshotIssueCode.ExternalEngineRequired);
        Assert.Contains(result.Issues, x => x.Code == QubesInputSnapshotIssueCode.EngineKindMustBeRealEngine);
    }

    [Fact]
    public void Valid_real_engine_descriptor_and_matching_snapshot_pass_snapshot_validation()
    {
        var result = ValidateSnapshot(ValidDescriptor(), ValidSnapshot());

        Assert.True(result.IsValid);
        Assert.True(result.SnapshotCanFeedRealEngine);
        Assert.False(result.AuthorizesProductionAccountingWeights);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Valid_real_engine_descriptor_and_mismatched_input_contract_version_is_invalid()
    {
        var result = ValidateSnapshot(
            ValidDescriptor(),
            ValidSnapshot() with { InputContractVersion = "qubes-input-snapshot.v2" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesInputSnapshotIssueCode.InputContractVersionMismatch);
    }

    [Fact]
    public void Valid_snapshot_alone_does_not_produce_weights_or_authorize_production_accounting_output()
    {
        var result = ValidateSnapshot(ValidSnapshot());

        Assert.True(result.IsValid);
        Assert.True(result.SnapshotCanFeedRealEngine);
        Assert.False(result.AuthorizesProductionAccountingWeights);
    }

    [Fact]
    public void Valid_snapshot_with_null_market_data_snapshot_id_remains_blocked()
    {
        var result = ValidateSnapshot(
            ValidDescriptor(),
            ValidSnapshot() with { MarketDataSnapshotId = null });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesInputSnapshotIssueCode.MissingMarketDataSnapshotId);
    }

    [Fact]
    public void Empty_qubes_weights_output_id_is_invalid()
    {
        var result = ValidateWeightsOutput(ValidWeightsOutput() with { QubesWeightsOutputId = "" });

        Assert.False(result.IsValid);
        Assert.False(result.OutputContractValid);
        Assert.False(result.AuthorizesOrdersFillsLedgerAccountingExecution);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.MissingQubesWeightsOutputId);
    }

    [Fact]
    public void Empty_qubes_weights_run_id_is_invalid()
    {
        var result = ValidateWeightsOutput(ValidWeightsOutput() with { QubesRunId = " " });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.MissingQubesRunId);
    }

    [Fact]
    public void Empty_qubes_weights_input_snapshot_id_is_invalid()
    {
        var result = ValidateWeightsOutput(ValidWeightsOutput() with { QubesInputSnapshotId = null });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.MissingQubesInputSnapshotId);
    }

    [Fact]
    public void Empty_qubes_weights_market_data_snapshot_id_is_blocked()
    {
        var result = ValidateWeightsOutput(ValidWeightsOutput() with { MarketDataSnapshotId = null });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.MissingMarketDataSnapshotId);
    }

    [Fact]
    public void Empty_qubes_weights_engine_id_is_invalid()
    {
        var result = ValidateWeightsOutput(ValidWeightsOutput() with { EngineId = "" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.MissingEngineId);
    }

    [Fact]
    public void Empty_qubes_weights_output_contract_version_is_invalid()
    {
        var result = ValidateWeightsOutput(ValidWeightsOutput() with { OutputContractVersion = "" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.MissingOutputContractVersion);
    }

    [Fact]
    public void Empty_qubes_weights_output_hash_is_invalid()
    {
        var result = ValidateWeightsOutput(ValidWeightsOutput() with { OutputHash = " " });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.MissingOutputHash);
    }

    [Fact]
    public void Sandbox_prototype_weights_output_is_blocked()
    {
        var result = ValidateWeightsOutput(ValidWeightsOutput() with { EngineKind = QubesEngineKind.SandboxPrototype });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.SandboxPrototypeBlocked);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.EngineKindMustBeRealEngine);
    }

    [Fact]
    public void External_engine_required_weights_output_is_blocked()
    {
        var result = ValidateWeightsOutput(ValidWeightsOutput() with { EngineKind = QubesEngineKind.ExternalEngineRequired });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.ExternalEngineRequired);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.EngineKindMustBeRealEngine);
    }

    [Fact]
    public void R005_identifiers_used_anywhere_in_weights_output_are_rejected()
    {
        var result = ValidateWeightsOutput(ValidWeightsOutput() with
        {
            QubesWeightsOutputId = QubesKnownPrototypeIds.R005OutputId,
            QubesRunId = QubesKnownPrototypeIds.R005RunId,
            OutputHash = QubesKnownPrototypeIds.R005OutputHash,
            WeightEntries =
            [
                ValidWeightEntry("EURUSD", 0.10d, 0, QubesKnownPrototypeIds.R005OutputHash)
            ]
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.R005RunIdRejected);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.R005OutputIdRejected);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.R005OutputHashRejected);
    }

    [Fact]
    public void Zero_qubes_weight_entries_are_invalid()
    {
        var result = ValidateWeightsOutput(ValidWeightsOutput() with { WeightEntries = [] });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.MissingWeightEntries);
    }

    [Fact]
    public void Qubes_weight_entry_missing_key_is_invalid()
    {
        var result = ValidateWeightsOutput(ValidWeightsOutput() with
        {
            WeightEntries = [ValidWeightEntry("", 0.10d, 0, "sha256:entry")]
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.MissingWeightKey);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Qubes_weight_entry_nan_or_infinity_is_invalid(double weight)
    {
        var result = ValidateWeightsOutput(ValidWeightsOutput() with
        {
            WeightEntries = [ValidWeightEntry("EURUSD", weight, 0, "sha256:entry")]
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.NonFiniteWeight);
    }

    [Fact]
    public void Duplicate_qubes_weight_key_order_or_entry_hash_is_invalid()
    {
        var result = ValidateWeightsOutput(ValidWeightsOutput() with
        {
            WeightEntries =
            [
                ValidWeightEntry("EURUSD", 0.10d, 0, "sha256:entry-dup"),
                ValidWeightEntry("EURUSD", 0.20d, 0, "sha256:entry-dup")
            ]
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.DuplicateWeightKey);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.DuplicateEntryOrder);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.DuplicateEntryHash);
    }

    [Fact]
    public void Qubes_weights_output_snapshot_id_mismatch_vs_input_snapshot_is_invalid()
    {
        var result = ValidateWeightsOutput(
            ValidDescriptor(),
            ValidSnapshot(),
            ValidWeightsOutput() with { QubesInputSnapshotId = "qubes-input-snapshot-other-001" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.InputSnapshotMismatch);
    }

    [Fact]
    public void Qubes_weights_output_market_data_snapshot_id_mismatch_vs_input_snapshot_is_invalid()
    {
        var result = ValidateWeightsOutput(
            ValidDescriptor(),
            ValidSnapshot(),
            ValidWeightsOutput() with { MarketDataSnapshotId = OtherMarketDataSnapshotIdValue });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.MarketDataSnapshotMismatch);
    }

    [Fact]
    public void Qubes_weights_output_engine_id_mismatch_vs_descriptor_is_invalid()
    {
        var result = ValidateWeightsOutput(
            ValidDescriptor(),
            ValidSnapshot(),
            ValidWeightsOutput() with { EngineId = "qubes-external-other-engine" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.EngineIdMismatch);
    }

    [Fact]
    public void Qubes_weights_output_contract_version_mismatch_vs_descriptor_is_invalid()
    {
        var result = ValidateWeightsOutput(
            ValidDescriptor(),
            ValidSnapshot(),
            ValidWeightsOutput() with { OutputContractVersion = "qubes-weights-output.v2" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesWeightsOutputIssueCode.OutputContractVersionMismatch);
    }

    [Fact]
    public void Valid_descriptor_snapshot_and_weights_output_pass_output_contract_validation()
    {
        var result = ValidateWeightsOutput(ValidDescriptor(), ValidSnapshot(), ValidWeightsOutput());

        Assert.True(result.IsValid);
        Assert.True(result.OutputContractValid);
        Assert.False(result.AuthorizesOrdersFillsLedgerAccountingExecution);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Valid_weights_output_contract_does_not_create_or_authorize_execution_side_effects()
    {
        var result = ValidateWeightsOutput(ValidWeightsOutput());

        Assert.True(result.IsValid);
        Assert.True(result.OutputContractValid);
        Assert.False(result.AuthorizesOrdersFillsLedgerAccountingExecution);
    }

    [Fact]
    public void Empty_qubes_run_link_run_id_is_invalid()
    {
        var result = ValidateRunLink(ValidRunLink() with { QubesRunId = "" });

        Assert.False(result.IsValid);
        Assert.False(result.ContractValidQubesWeightsChain);
        Assert.False(result.AuthorizesOrdersFillsLedgerAccountingExecution);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.MissingQubesRunId);
    }

    [Fact]
    public void Empty_qubes_run_link_input_snapshot_id_is_invalid()
    {
        var result = ValidateRunLink(ValidRunLink() with { QubesInputSnapshotId = null });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.MissingQubesInputSnapshotId);
    }

    [Fact]
    public void Empty_qubes_run_link_weights_output_id_is_invalid()
    {
        var result = ValidateRunLink(ValidRunLink() with { QubesWeightsOutputId = " " });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.MissingQubesWeightsOutputId);
    }

    [Fact]
    public void Empty_qubes_run_link_market_data_snapshot_id_is_blocked()
    {
        var result = ValidateRunLink(ValidRunLink() with { MarketDataSnapshotId = null });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.MissingMarketDataSnapshotId);
    }

    [Fact]
    public void Empty_qubes_run_link_engine_id_is_invalid()
    {
        var result = ValidateRunLink(ValidRunLink() with { EngineId = "" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.MissingEngineId);
    }

    [Fact]
    public void Empty_qubes_run_link_contract_version_is_invalid()
    {
        var result = ValidateRunLink(ValidRunLink() with { RunContractVersion = "" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.MissingRunContractVersion);
    }

    [Fact]
    public void Empty_qubes_run_link_fingerprint_is_invalid()
    {
        var result = ValidateRunLink(ValidRunLink() with { RunFingerprint = " " });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.MissingRunFingerprint);
    }

    [Fact]
    public void Sandbox_prototype_run_link_is_blocked()
    {
        var result = ValidateRunLink(ValidRunLink() with { EngineKind = QubesEngineKind.SandboxPrototype });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.SandboxPrototypeBlocked);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.EngineKindMustBeRealEngine);
    }

    [Fact]
    public void External_engine_required_run_link_is_blocked()
    {
        var result = ValidateRunLink(ValidRunLink() with { EngineKind = QubesEngineKind.ExternalEngineRequired });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.ExternalEngineRequired);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.EngineKindMustBeRealEngine);
    }

    [Fact]
    public void R005_identifiers_used_anywhere_in_run_link_are_rejected()
    {
        var result = ValidateRunLink(ValidRunLink() with
        {
            QubesRunId = QubesKnownPrototypeIds.R005RunId,
            QubesWeightsOutputId = QubesKnownPrototypeIds.R005OutputId,
            RunFingerprint = QubesKnownPrototypeIds.R005OutputHash
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.R005RunIdRejected);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.R005OutputIdRejected);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.R005OutputHashRejected);
    }

    [Fact]
    public void Run_link_snapshot_id_mismatch_vs_snapshot_is_invalid()
    {
        var result = ValidateRunLink(
            ValidDescriptor(),
            ValidSnapshot(),
            ValidRunLink() with { QubesInputSnapshotId = "qubes-input-snapshot-other-001" },
            ValidWeightsOutput());

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.InputSnapshotMismatch);
    }

    [Fact]
    public void Run_link_output_id_mismatch_vs_weights_output_is_invalid()
    {
        var result = ValidateRunLink(
            ValidDescriptor(),
            ValidSnapshot(),
            ValidRunLink() with { QubesWeightsOutputId = "qubes-real-engine-output-other-001" },
            ValidWeightsOutput());

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.WeightsOutputMismatch);
    }

    [Fact]
    public void Run_link_market_data_snapshot_id_mismatch_vs_snapshot_or_output_is_invalid()
    {
        var result = ValidateRunLink(
            ValidDescriptor(),
            ValidSnapshot(),
            ValidRunLink() with { MarketDataSnapshotId = OtherMarketDataSnapshotIdValue },
            ValidWeightsOutput());

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.MarketDataSnapshotMismatch);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.ProductionUsageBlocked);
    }

    [Fact]
    public void Run_link_engine_id_mismatch_vs_descriptor_or_output_is_invalid()
    {
        var result = ValidateRunLink(
            ValidDescriptor(),
            ValidSnapshot(),
            ValidRunLink() with { EngineId = "qubes-external-other-engine" },
            ValidWeightsOutput());

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.EngineIdMismatch);
    }

    [Fact]
    public void Run_link_run_id_mismatch_vs_output_is_invalid()
    {
        var result = ValidateRunLink(
            ValidDescriptor(),
            ValidSnapshot(),
            ValidRunLink() with { QubesRunId = "qubes-real-engine-run-other-001" },
            ValidWeightsOutput());

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.RunIdMismatch);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.ProductionUsageBlocked);
    }

    [Fact]
    public void Run_link_input_contract_version_mismatch_vs_snapshot_is_invalid()
    {
        var result = ValidateRunLink(
            ValidDescriptor(),
            ValidSnapshot(),
            ValidRunLink() with { InputContractVersion = "qubes-input-snapshot.v2" },
            ValidWeightsOutput());

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.InputContractVersionMismatch);
    }

    [Fact]
    public void Run_link_output_contract_version_mismatch_vs_output_is_invalid()
    {
        var result = ValidateRunLink(
            ValidDescriptor(),
            ValidSnapshot(),
            ValidRunLink() with { OutputContractVersion = "qubes-weights-output.v2" },
            ValidWeightsOutput());

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.OutputContractVersionMismatch);
    }

    [Fact]
    public void Valid_descriptor_snapshot_run_link_and_weights_output_pass_full_chain_validation()
    {
        var result = ValidateRunLink(ValidDescriptor(), ValidSnapshot(), ValidRunLink(), ValidWeightsOutput());

        Assert.True(result.IsValid);
        Assert.True(result.ContractValidQubesWeightsChain);
        Assert.False(result.AuthorizesOrdersFillsLedgerAccountingExecution);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Full_chain_contract_valid_result_does_not_authorize_execution_side_effects()
    {
        var result = ValidateRunLink(ValidDescriptor(), ValidSnapshot(), ValidRunLink(), ValidWeightsOutput());

        Assert.True(result.IsValid);
        Assert.False(result.AuthorizesOrdersFillsLedgerAccountingExecution);
    }

    [Fact]
    public void Whitespace_only_required_fields_are_invalid()
    {
        var descriptor = ValidateDescriptor(ValidDescriptor() with { EngineId = " " });
        var snapshot = ValidateSnapshot(ValidSnapshot() with { QubesInputSnapshotId = " " });
        var output = ValidateWeightsOutput(ValidWeightsOutput() with { QubesRunId = " " });
        var runLink = ValidateRunLink(ValidRunLink() with { RunContractVersion = " " });

        Assert.False(descriptor.IsValid);
        Assert.False(snapshot.IsValid);
        Assert.False(output.IsValid);
        Assert.False(runLink.IsValid);
        Assert.Contains(descriptor.Issues, x => x.Code == QubesEngineRegistrationIssueCode.MissingEngineId);
        Assert.Contains(snapshot.Issues, x => x.Code == QubesInputSnapshotIssueCode.MissingQubesInputSnapshotId);
        Assert.Contains(output.Issues, x => x.Code == QubesWeightsOutputIssueCode.MissingQubesRunId);
        Assert.Contains(runLink.Issues, x => x.Code == QubesRunLinkIssueCode.MissingRunContractVersion);
    }

    [Fact]
    public void R005_run_id_with_whitespace_is_rejected()
    {
        var runLink = ValidateRunLink(ValidRunLink() with { QubesRunId = $" {QubesKnownPrototypeIds.R005RunId} " });
        var production = Validate(ValidRequest() with { Run = ValidRun() with { QubesRunId = $" {QubesKnownPrototypeIds.R005RunId} " } });

        Assert.False(runLink.IsValid);
        Assert.False(production.IsAllowed);
        Assert.Contains(runLink.Issues, x => x.Code == QubesRunLinkIssueCode.R005RunIdRejected);
        Assert.Contains(production.Issues, x => x.Code == QubesProductionValidationIssueCode.R005RunIdPrototypeOnly);
    }

    [Fact]
    public void R005_output_id_with_whitespace_is_rejected()
    {
        var output = ValidateWeightsOutput(ValidWeightsOutput() with { QubesWeightsOutputId = $" {QubesKnownPrototypeIds.R005OutputId} " });
        var production = Validate(ValidRequest() with { Output = ValidOutput() with { QubesWeightsOutputId = $" {QubesKnownPrototypeIds.R005OutputId} " } });

        Assert.False(output.IsValid);
        Assert.False(production.IsAllowed);
        Assert.Contains(output.Issues, x => x.Code == QubesWeightsOutputIssueCode.R005OutputIdRejected);
        Assert.Contains(production.Issues, x => x.Code == QubesProductionValidationIssueCode.R005OutputIdRejected);
    }

    [Fact]
    public void R005_hash_lowercase_and_sha256_prefixed_variants_are_rejected()
    {
        var lowercaseHash = QubesKnownPrototypeIds.R005OutputHash.ToLowerInvariant();
        var prefixedHash = $"sha256:{lowercaseHash}";

        var descriptor = ValidateDescriptor(ValidDescriptor() with { BuildFingerprint = prefixedHash });
        var snapshot = ValidateSnapshot(ValidSnapshot() with
        {
            ManifestHash = lowercaseHash,
            Components =
            [
                ValidComponent(QubesInputSnapshotComponentKind.QubesUniverse, "universe", prefixedHash, 0)
            ]
        });
        var output = ValidateWeightsOutput(ValidWeightsOutput() with
        {
            OutputHash = lowercaseHash,
            WeightEntries =
            [
                ValidWeightEntry("EURUSD", 0.10d, 0, prefixedHash)
            ]
        });
        var runLink = ValidateRunLink(ValidRunLink() with { RunFingerprint = prefixedHash });

        Assert.False(descriptor.IsValid);
        Assert.False(snapshot.IsValid);
        Assert.False(output.IsValid);
        Assert.False(runLink.IsValid);
        Assert.Contains(descriptor.Issues, x => x.Code == QubesEngineRegistrationIssueCode.R005OutputHashRejected);
        Assert.Contains(snapshot.Issues, x => x.Code == QubesInputSnapshotIssueCode.R005OutputHashRejected);
        Assert.Contains(output.Issues, x => x.Code == QubesWeightsOutputIssueCode.R005OutputHashRejected);
        Assert.Contains(runLink.Issues, x => x.Code == QubesRunLinkIssueCode.R005OutputHashRejected);
    }

    [Fact]
    public void R005_evidence_is_rejected_from_descriptor_identity_and_adapter_fields()
    {
        var descriptor = ValidateDescriptor(ValidDescriptor() with
        {
            EngineId = $" {QubesKnownPrototypeIds.R005RunId} ",
            SourceRef = QubesKnownPrototypeIds.R005OutputId,
            AdapterName = $"sha256:{QubesKnownPrototypeIds.R005OutputHash.ToLowerInvariant()}"
        });

        Assert.False(descriptor.IsValid);
        Assert.Contains(descriptor.Issues, x => x.Code == QubesEngineRegistrationIssueCode.R005RunIdRejected);
        Assert.Contains(descriptor.Issues, x => x.Code == QubesEngineRegistrationIssueCode.R005OutputIdRejected);
        Assert.Contains(descriptor.Issues, x => x.Code == QubesEngineRegistrationIssueCode.R005OutputHashRejected);
    }

    [Fact]
    public void Child_descriptor_failure_makes_full_chain_validation_invalid_and_is_propagated()
    {
        var result = ValidateRunLink(ValidDescriptor() with { EngineId = "" }, ValidSnapshot(), ValidRunLink(), ValidWeightsOutput());

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.DescriptorInvalid && x.Message.Contains(nameof(QubesEngineRegistrationIssueCode.MissingEngineId), StringComparison.Ordinal));
    }

    [Fact]
    public void Child_snapshot_failure_makes_full_chain_validation_invalid_and_is_propagated()
    {
        var result = ValidateRunLink(ValidDescriptor(), ValidSnapshot() with { ManifestHash = "" }, ValidRunLink(), ValidWeightsOutput());

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.SnapshotInvalid && x.Message.Contains(nameof(QubesInputSnapshotIssueCode.MissingManifestHash), StringComparison.Ordinal));
    }

    [Fact]
    public void Child_output_failure_makes_full_chain_validation_invalid_and_is_propagated()
    {
        var result = ValidateRunLink(ValidDescriptor(), ValidSnapshot(), ValidRunLink(), ValidWeightsOutput() with { OutputHash = "" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.OutputInvalid && x.Message.Contains(nameof(QubesWeightsOutputIssueCode.MissingOutputHash), StringComparison.Ordinal));
    }

    [Fact]
    public void Child_run_link_failure_makes_full_chain_validation_invalid()
    {
        var result = ValidateRunLink(ValidDescriptor(), ValidSnapshot(), ValidRunLink() with { RunFingerprint = "" }, ValidWeightsOutput());

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, x => x.Code == QubesRunLinkIssueCode.MissingRunFingerprint);
    }

    private static QubesProductionValidationResult Validate(QubesProductionValidationRequest request)
        => new QubesProductionUsageValidator().Validate(request);

    private static QubesEngineRegistrationValidationResult ValidateDescriptor(QubesExternalEngineDescriptor descriptor)
        => new QubesExternalEngineDescriptorValidator().Validate(descriptor);

    private static QubesInputSnapshotValidationResult ValidateSnapshot(QubesInputSnapshotManifest manifest)
        => new QubesInputSnapshotManifestValidator().Validate(manifest);

    private static QubesInputSnapshotValidationResult ValidateSnapshot(
        QubesExternalEngineDescriptor descriptor,
        QubesInputSnapshotManifest manifest)
        => new QubesInputSnapshotManifestValidator().ValidateForDescriptor(descriptor, manifest);

    private static QubesWeightsOutputValidationResult ValidateWeightsOutput(QubesWeightsOutputManifest manifest)
        => new QubesWeightsOutputManifestValidator().Validate(manifest);

    private static QubesWeightsOutputValidationResult ValidateWeightsOutput(
        QubesExternalEngineDescriptor descriptor,
        QubesInputSnapshotManifest snapshot,
        QubesWeightsOutputManifest output)
        => new QubesWeightsOutputManifestValidator().ValidateForDescriptorAndSnapshot(descriptor, snapshot, output);

    private static QubesRunLinkValidationResult ValidateRunLink(QubesRunLinkManifest manifest)
        => new QubesRunLinkManifestValidator().Validate(manifest);

    private static QubesRunLinkValidationResult ValidateRunLink(
        QubesExternalEngineDescriptor descriptor,
        QubesInputSnapshotManifest snapshot,
        QubesRunLinkManifest runLink,
        QubesWeightsOutputManifest output)
        => new QubesRunLinkManifestValidator().ValidateFullChain(descriptor, snapshot, runLink, output);

    private static QubesProductionValidationRequest ValidRequest()
        => new(
            QubesProductionUsage.Production,
            ValidInput(),
            ValidRun(),
            ValidOutput(),
            RealEngineAdapterRegistered: true);

    private static QubesInputSnapshotRef ValidInput()
        => new(
            "qubes-input-snapshot-real-20260527T120000Z-001",
            MarketDataSnapshotIdValue,
            QubesEngineKind.RealEngine);

    private static QubesRunRef ValidRun(IReadOnlyList<MarketDataSnapshotId>? bindings = null)
        => new(
            "qubes-real-engine-run-20260527T120000Z-001",
            QubesEngineKind.RealEngine,
            bindings ?? [MarketDataSnapshotIdValue]);

    private static QubesWeightsOutputRef ValidOutput()
        => new(
            "qubes-real-engine-output-20260527T120000Z-001",
            "3FB3776C6F7894E1253A2DA7D8088AA76F7B1B86B28B6B967026D6C4F055C9F6",
            "qubes-real-engine-run-20260527T120000Z-001",
            "qubes-input-snapshot-real-20260527T120000Z-001",
            MarketDataSnapshotIdValue,
            QubesEngineKind.RealEngine);

    private static SandboxQubesOutput R005SandboxOutput()
        => new(
            SandboxQubesRunId: QubesKnownPrototypeIds.R005RunId,
            QubesOutputId: QubesKnownPrototypeIds.R005OutputId,
            InputSnapshotId: "qubes-operationalization-r005:prototype-input:20251217T020000Z:001",
            MarketDataSnapshotId: null,
            CanonicalTargetCloseUtc: new DateTimeOffset(2025, 12, 17, 2, 0, 0, TimeSpan.Zero),
            Weights:
            [
                new SandboxQubesOutputWeight("AUDUSD", -0.053856m),
                new SandboxQubesOutputWeight("EURUSD", -0.013900m),
                new SandboxQubesOutputWeight("GBPUSD", 0.039348m),
                new SandboxQubesOutputWeight("JPYUSD", 0.001663m)
            ],
            WeightUnits: "PrototypeSignalWeight",
            DirectCrossesPresent: false,
            DirectCrossPolicy: "DirectCrossSignalOnlyNettingFirstExecutionDisabled",
            RunnerType: "SandboxQubesPrototype",
            SandboxOnly: true,
            NotProduction: true,
            NotAccounting: true,
            NotExecuted: true,
            NotLedgerCommit: true);

    private static QubesExternalEngineDescriptor ValidDescriptor()
        => new(
            EngineId: "qubes-external-real-engine-r001",
            EngineKind: QubesEngineKind.RealEngine,
            EngineName: "External Qubes Engine",
            SourceVersion: "source-version-r001",
            SourceRef: "external-qubes/source@abc123",
            BuildFingerprint: "sha256:8B5C931FD4B8B3361E23F677B01CC6413A22AE53B6745D75A4C2FD2085A0F7BB",
            AdapterName: "ExternalQubesEngineAdapter",
            SupportedInputContractVersion: "qubes-input-snapshot.v1",
            SupportedOutputContractVersion: "qubes-weights-output.v1",
            IsProductionAccountingEligible: true);

    private static QubesInputSnapshotManifest ValidSnapshot()
        => new(
            QubesInputSnapshotId: "qubes-input-snapshot-real-20260527T120000Z-001",
            MarketDataSnapshotId: MarketDataSnapshotIdValue,
            InputContractVersion: "qubes-input-snapshot.v1",
            ExpectedEngineId: "qubes-external-real-engine-r001",
            CreatedAtUtc: new DateTimeOffset(2026, 05, 27, 12, 00, 00, TimeSpan.Zero),
            ManifestHash: "sha256:A27D5B6D4F21B24D7F2A4DF27EF197F923B8EC78E62F4D2E89D704B278EF8C21",
            Components:
            [
                ValidComponent(QubesInputSnapshotComponentKind.QubesUniverse, "universe", "sha256:934BF91B24820D1C4890A14C3C247C54F7252BF543057C9CB1986CF7D20F26E0", 0),
                ValidComponent(QubesInputSnapshotComponentKind.QubesSignals, "signals", "sha256:6538026A68572B5A60135ED4A5A6B9EC61C9DA2986E6F759B9D203A08F76333E", 1),
                ValidComponent(QubesInputSnapshotComponentKind.QubesDates, "dates", "sha256:5043D52B48F62CE3D06B0874E1D8FCB968C8D89449900B0B0B68B40FF9024E0F", 2)
            ],
            EngineKind: QubesEngineKind.RealEngine);

    private static QubesInputSnapshotComponent ValidComponent(
        QubesInputSnapshotComponentKind kind,
        string name,
        string? contentHash,
        int order)
        => new(kind, name, contentHash, order);

    private static QubesWeightsOutputManifest ValidWeightsOutput()
        => new(
            QubesWeightsOutputId: "qubes-real-engine-output-20260527T120000Z-001",
            QubesRunId: "qubes-real-engine-run-20260527T120000Z-001",
            QubesInputSnapshotId: "qubes-input-snapshot-real-20260527T120000Z-001",
            MarketDataSnapshotId: MarketDataSnapshotIdValue,
            EngineId: "qubes-external-real-engine-r001",
            OutputContractVersion: "qubes-weights-output.v1",
            OutputHash: "sha256:3FB3776C6F7894E1253A2DA7D8088AA76F7B1B86B28B6B967026D6C4F055C9F6",
            ProducedAtUtc: new DateTimeOffset(2026, 05, 27, 12, 01, 00, TimeSpan.Zero),
            EngineKind: QubesEngineKind.RealEngine,
            WeightEntries:
            [
                ValidWeightEntry("EURUSD", 0.10d, 0, "sha256:7C114B5EC0665CBA0EE8F606C0A15101D2785A10B5995B65618F750E9AA79066"),
                ValidWeightEntry("GBPUSD", -0.05d, 1, "sha256:D0B3C58C5E21D5D74686C1F4DDF89DC7DF4B8D3B72091C1DE517E08D0D541DF4")
            ]);

    private static QubesWeightEntry ValidWeightEntry(string? key, double weight, int order, string? entryHash)
        => new(key, weight, order, entryHash);

    private static QubesRunLinkManifest ValidRunLink()
        => new(
            QubesRunId: "qubes-real-engine-run-20260527T120000Z-001",
            QubesInputSnapshotId: "qubes-input-snapshot-real-20260527T120000Z-001",
            QubesWeightsOutputId: "qubes-real-engine-output-20260527T120000Z-001",
            MarketDataSnapshotId: MarketDataSnapshotIdValue,
            EngineId: "qubes-external-real-engine-r001",
            EngineKind: QubesEngineKind.RealEngine,
            RunContractVersion: "qubes-run-link.v1",
            InputContractVersion: "qubes-input-snapshot.v1",
            OutputContractVersion: "qubes-weights-output.v1",
            RunFingerprint: "sha256:4957EC7BDBA13E62651848309011FE850E644C78AD7566872455314C8B39B9BD",
            CreatedAtUtc: new DateTimeOffset(2026, 05, 27, 12, 02, 00, TimeSpan.Zero));

    private static readonly MarketDataSnapshotId MarketDataSnapshotIdValue = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly MarketDataSnapshotId OtherMarketDataSnapshotIdValue = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
}
