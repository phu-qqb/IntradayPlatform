namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public static class Arch7bOperationalBindingProducerClassifications
{
    public const string StaticAuthorityExists = "STATIC_AUTHORITY_EXISTS";
    public const string FactProducerExists = "FACT_PRODUCER_EXISTS";
    public const string DeterministicRunOutputPathProducerExists =
        "DETERMINISTIC_RUN_OUTPUT_PATH_PRODUCER_EXISTS";
    public const string ArtifactProducerAndValidationGateExist =
        "ARTIFACT_PRODUCER_AND_VALIDATION_GATE_EXIST";
    public const string RealProducerMissing = "REAL_PRODUCER_MISSING";
}

public sealed record Arch7bOperationalBindingProducerAuditEntry(
    string BindingId,
    string CommandId,
    string ArgumentName,
    string Classification,
    string ProducerStage,
    string ProducerContract,
    string ProducerSourceFile,
    string ProducerSymbol,
    string EvidenceSha256);

public sealed record Arch7bOperationalBindingProducerAuditDocument(
    string ContractVersion,
    int BindingCount,
    int MissingProducerCount,
    IReadOnlyList<Arch7bOperationalBindingProducerAuditEntry> Bindings,
    string EvidenceSha256);

public static class Arch7bOperationalBindingProducerAudit
{
    public const string ContractVersion =
        "arch7b_operational_binding_producer_audit_v1";

    public static Arch7bOperationalBindingProducerAuditDocument Build()
    {
        var bindings = Arch7bOperationalLiveFactBindingCatalog.Build()
            .SelectMany(value => value.Bindings)
            .Select(Classify)
            .ToArray();
        var missing = bindings.Count(value => value.Classification ==
            Arch7bOperationalBindingProducerClassifications.RealProducerMissing);
        if (bindings.Length != 34 || missing != 0)
            throw new Arch7bQualificationException(
                Arch7bV2Blockers.RequiredFactMissing,
                bindings.FirstOrDefault(value => value.Classification ==
                    Arch7bOperationalBindingProducerClassifications.RealProducerMissing)
                    ?.ArgumentName ?? "binding-producer-audit");
        var canonical = string.Join('\n', ContractVersion, bindings.Length, missing,
            string.Join('|', bindings.Select(value => value.EvidenceSha256)));
        return new(ContractVersion, bindings.Length, missing, bindings,
            Arch7bOneShotContracts.Sha256(canonical));
    }

    private static Arch7bOperationalBindingProducerAuditEntry Classify(
        Arch7bOperationalLiveFactBinding binding)
    {
        var classification = binding.PlaceholderScope switch
        {
            Arch7bOperationalPlaceholderScope.Authority when
                binding.RequiredProducerStage is null &&
                !string.IsNullOrWhiteSpace(binding.ProducerContract) &&
                !string.IsNullOrWhiteSpace(binding.ProducerSourceFile) =>
                Arch7bOperationalBindingProducerClassifications.StaticAuthorityExists,
            Arch7bOperationalPlaceholderScope.Artifact when
                HasStage(binding.RequiredProducerStage) &&
                !string.IsNullOrWhiteSpace(binding.ProducerSymbol) =>
                Arch7bOperationalBindingProducerClassifications
                    .ArtifactProducerAndValidationGateExist,
            Arch7bOperationalPlaceholderScope.Fact when
                binding.ValueKind == Arch7bPlaceholderValueKind.AbsolutePath &&
                binding.PlaceholderName is "runtime_run_root" or
                    "position_market_draft_output_path" or
                    "position_market_lineage_output_path" or
                    "position_market_revision_binding_output_path" &&
                HasStage(binding.RequiredProducerStage) =>
                Arch7bOperationalBindingProducerClassifications
                    .DeterministicRunOutputPathProducerExists,
            Arch7bOperationalPlaceholderScope.Fact when
                HasStage(binding.RequiredProducerStage) &&
                !string.IsNullOrWhiteSpace(binding.ProducerSymbol) =>
                Arch7bOperationalBindingProducerClassifications.FactProducerExists,
            _ => Arch7bOperationalBindingProducerClassifications.RealProducerMissing
        };
        var canonical = string.Join('\n', ContractVersion, binding.BindingId,
            binding.CommandId, binding.ArgumentName, classification,
            binding.RequiredProducerStage ?? string.Empty, binding.ProducerContract,
            binding.ProducerSourceFile, binding.ProducerSymbol);
        return new(binding.BindingId, binding.CommandId, binding.ArgumentName,
            classification, binding.RequiredProducerStage ?? string.Empty,
            binding.ProducerContract, binding.ProducerSourceFile,
            binding.ProducerSymbol, Arch7bOneShotContracts.Sha256(canonical));
    }

    private static bool HasStage(string? stage) =>
        stage is not null && Arch7bStages.All.Contains(stage, StringComparer.Ordinal);
}
