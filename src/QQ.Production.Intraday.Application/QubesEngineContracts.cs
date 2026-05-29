using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

public enum QubesEngineKind
{
    SandboxPrototype,
    ExternalEngineRequired,
    RealEngine
}

public enum QubesProductionUsage
{
    Production,
    Accounting
}

public enum QubesProductionValidationIssueCode
{
    SandboxPrototypeBlocked,
    ExternalEngineRequired,
    MissingQubesInputSnapshotId,
    MissingQubesRunId,
    MissingQubesWeightsOutputId,
    MissingMarketDataSnapshotId,
    RunMarketDataBindingMissing,
    RunMarketDataBindingNotUnique,
    OutputMarketDataMismatch,
    OutputRunIdMismatch,
    OutputInputSnapshotMismatch,
    R005RunIdPrototypeOnly,
    R005OutputIdRejected,
    R005OutputHashRejected
}

public enum QubesEngineRegistrationIssueCode
{
    SandboxPrototypeCannotRegister,
    ExternalEngineRequiredNotEligible,
    MissingEngineId,
    MissingEngineName,
    MissingSourceRef,
    MissingBuildFingerprint,
    MissingAdapterName,
    MissingSupportedInputContractVersion,
    MissingSupportedOutputContractVersion,
    EngineKindMustBeRealEngine,
    R005RunIdRejected,
    R005OutputIdRejected,
    R005OutputHashRejected
}

public enum QubesInputSnapshotComponentKind
{
    Unknown,
    QubesUniverse,
    QubesSignals,
    QubesTracks,
    QubesCosts,
    QubesDates,
    QubesBenchmark
}

public enum QubesInputSnapshotIssueCode
{
    SandboxPrototypeBlocked,
    ExternalEngineRequired,
    EngineKindMustBeRealEngine,
    DescriptorInvalid,
    MissingQubesInputSnapshotId,
    MissingMarketDataSnapshotId,
    MissingInputContractVersion,
    MissingManifestHash,
    MissingComponents,
    MissingComponentName,
    MissingComponentHash,
    UnknownComponentKind,
    DuplicateComponentOrder,
    DuplicateComponentIdentity,
    DuplicateComponentHash,
    EngineIdMismatch,
    InputContractVersionMismatch,
    R005RunIdRejected,
    R005OutputIdRejected,
    R005OutputHashRejected
}

public enum QubesWeightsOutputIssueCode
{
    SandboxPrototypeBlocked,
    ExternalEngineRequired,
    EngineKindMustBeRealEngine,
    DescriptorInvalid,
    SnapshotInvalid,
    MissingQubesWeightsOutputId,
    MissingQubesRunId,
    MissingQubesInputSnapshotId,
    MissingMarketDataSnapshotId,
    MissingEngineId,
    MissingOutputContractVersion,
    MissingOutputHash,
    MissingWeightEntries,
    MissingWeightKey,
    NonFiniteWeight,
    DuplicateWeightKey,
    DuplicateEntryOrder,
    DuplicateEntryHash,
    InputSnapshotMismatch,
    MarketDataSnapshotMismatch,
    EngineIdMismatch,
    OutputContractVersionMismatch,
    R005RunIdRejected,
    R005OutputIdRejected,
    R005OutputHashRejected
}

public enum QubesRunLinkIssueCode
{
    SandboxPrototypeBlocked,
    ExternalEngineRequired,
    EngineKindMustBeRealEngine,
    DescriptorInvalid,
    SnapshotInvalid,
    OutputInvalid,
    ProductionUsageBlocked,
    MissingQubesRunId,
    MissingQubesInputSnapshotId,
    MissingQubesWeightsOutputId,
    MissingMarketDataSnapshotId,
    MissingEngineId,
    MissingRunContractVersion,
    MissingInputContractVersion,
    MissingOutputContractVersion,
    MissingRunFingerprint,
    InputSnapshotMismatch,
    WeightsOutputMismatch,
    MarketDataSnapshotMismatch,
    EngineIdMismatch,
    RunIdMismatch,
    InputContractVersionMismatch,
    OutputContractVersionMismatch,
    R005RunIdRejected,
    R005OutputIdRejected,
    R005OutputHashRejected
}

public sealed record QubesInputSnapshotRef(
    string? QubesInputSnapshotId,
    MarketDataSnapshotId? MarketDataSnapshotId,
    QubesEngineKind EngineKind);

public sealed record QubesRunRef(
    string? QubesRunId,
    QubesEngineKind EngineKind,
    IReadOnlyList<MarketDataSnapshotId> BoundMarketDataSnapshotIds);

public sealed record QubesWeightsOutputRef(
    string? QubesWeightsOutputId,
    string? QubesOutputHash,
    string? QubesRunId,
    string? QubesInputSnapshotId,
    MarketDataSnapshotId? MarketDataSnapshotId,
    QubesEngineKind EngineKind);

public sealed record QubesProductionValidationRequest(
    QubesProductionUsage Usage,
    QubesInputSnapshotRef InputSnapshot,
    QubesRunRef Run,
    QubesWeightsOutputRef Output,
    bool RealEngineAdapterRegistered);

public sealed record QubesProductionValidationIssue(
    QubesProductionValidationIssueCode Code,
    string Message);

public sealed record QubesProductionValidationResult(
    bool IsAllowed,
    IReadOnlyList<QubesProductionValidationIssue> Issues)
{
    public static QubesProductionValidationResult Allowed()
        => new(true, []);

    public static QubesProductionValidationResult Blocked(IReadOnlyList<QubesProductionValidationIssue> issues)
        => new(false, issues);
}

public static class QubesKnownPrototypeIds
{
    public const string R005RunId = "sandbox-qubes-prototype-r005-20251217T020000Z-001";
    public const string R005OutputId = "qubes-operationalization-r005:prototype-output:20251217T020000Z:001";
    public const string R005OutputHash = "5AB433ED36E08CFD8DCA7A8B02138E7CC81280F62E56D894E239D3F75F4DF79A";
}

internal static class QubesStringNormalization
{
    public static bool EqualsIdentifier(string? left, string? right)
        => string.Equals(NormalizeIdentifier(left), NormalizeIdentifier(right), StringComparison.Ordinal);

    public static bool IsKnownR005RunId(string? value)
        => EqualsIdentifier(value, QubesKnownPrototypeIds.R005RunId);

    public static bool IsKnownR005OutputId(string? value)
        => EqualsIdentifier(value, QubesKnownPrototypeIds.R005OutputId);

    public static bool IsKnownR005OutputHash(string? value)
        => string.Equals(NormalizeHashEvidence(value), NormalizeHashEvidence(QubesKnownPrototypeIds.R005OutputHash), StringComparison.Ordinal);

    public static bool IsAnyKnownR005Evidence(string? value)
        => IsKnownR005RunId(value) || IsKnownR005OutputId(value) || IsKnownR005OutputHash(value);

    private static string? NormalizeIdentifier(string? value)
        => value?.Trim();

    public static string? NormalizeHashEvidence(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return normalized;
        }

        const string sha256Prefix = "sha256:";
        if (normalized.StartsWith(sha256Prefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[sha256Prefix.Length..].Trim();
        }

        return normalized.ToUpperInvariant();
    }
}

public sealed record QubesExternalEngineDescriptor(
    string? EngineId,
    QubesEngineKind EngineKind,
    string? EngineName,
    string? SourceVersion,
    string? SourceRef,
    string? BuildFingerprint,
    string? AdapterName,
    string? SupportedInputContractVersion,
    string? SupportedOutputContractVersion,
    bool IsProductionAccountingEligible,
    string? EvidenceQubesRunId = null,
    string? EvidenceQubesWeightsOutputId = null,
    string? EvidenceQubesOutputHash = null);

public sealed record QubesEngineRegistrationIssue(
    QubesEngineRegistrationIssueCode Code,
    string Message);

public sealed record QubesEngineRegistrationValidationResult(
    bool IsValid,
    bool DescriptorCanRegisterRealEngine,
    bool AuthorizesProductionAccountingWeights,
    IQubesEngineAdapter Adapter,
    IReadOnlyList<QubesEngineRegistrationIssue> Issues)
{
    public static QubesEngineRegistrationValidationResult Valid(IQubesEngineAdapter adapter)
        => new(true, DescriptorCanRegisterRealEngine: true, AuthorizesProductionAccountingWeights: false, adapter, []);

    public static QubesEngineRegistrationValidationResult Invalid(IReadOnlyList<QubesEngineRegistrationIssue> issues)
        => new(false, DescriptorCanRegisterRealEngine: false, AuthorizesProductionAccountingWeights: false, new DisabledQubesEngineAdapter(), issues);
}

public sealed record QubesInputSnapshotComponent(
    QubesInputSnapshotComponentKind Kind,
    string? Name,
    string? ContentHash,
    int Order);

public sealed record QubesInputSnapshotManifest(
    string? QubesInputSnapshotId,
    MarketDataSnapshotId? MarketDataSnapshotId,
    string? InputContractVersion,
    string? ExpectedEngineId,
    DateTimeOffset CreatedAtUtc,
    string? ManifestHash,
    IReadOnlyList<QubesInputSnapshotComponent> Components,
    QubesEngineKind EngineKind,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record QubesInputSnapshotIssue(
    QubesInputSnapshotIssueCode Code,
    string Message);

public sealed record QubesInputSnapshotValidationResult(
    bool IsValid,
    bool SnapshotCanFeedRealEngine,
    bool AuthorizesProductionAccountingWeights,
    IReadOnlyList<QubesInputSnapshotIssue> Issues)
{
    public static QubesInputSnapshotValidationResult Valid()
        => new(true, SnapshotCanFeedRealEngine: true, AuthorizesProductionAccountingWeights: false, []);

    public static QubesInputSnapshotValidationResult Invalid(IReadOnlyList<QubesInputSnapshotIssue> issues)
        => new(false, SnapshotCanFeedRealEngine: false, AuthorizesProductionAccountingWeights: false, issues);
}

public sealed record QubesWeightEntry(
    string? WeightKey,
    double Weight,
    int Order,
    string? EntryHash = null);

public sealed record QubesWeightsOutputManifest(
    string? QubesWeightsOutputId,
    string? QubesRunId,
    string? QubesInputSnapshotId,
    MarketDataSnapshotId? MarketDataSnapshotId,
    string? EngineId,
    string? OutputContractVersion,
    string? OutputHash,
    DateTimeOffset ProducedAtUtc,
    QubesEngineKind EngineKind,
    IReadOnlyList<QubesWeightEntry> WeightEntries,
    IReadOnlyDictionary<string, string>? NativeArtifactRefs = null);

public sealed record QubesWeightsOutputIssue(
    QubesWeightsOutputIssueCode Code,
    string Message);

public sealed record QubesWeightsOutputValidationResult(
    bool IsValid,
    bool OutputContractValid,
    bool AuthorizesOrdersFillsLedgerAccountingExecution,
    IReadOnlyList<QubesWeightsOutputIssue> Issues)
{
    public static QubesWeightsOutputValidationResult Valid()
        => new(true, OutputContractValid: true, AuthorizesOrdersFillsLedgerAccountingExecution: false, []);

    public static QubesWeightsOutputValidationResult Invalid(IReadOnlyList<QubesWeightsOutputIssue> issues)
        => new(false, OutputContractValid: false, AuthorizesOrdersFillsLedgerAccountingExecution: false, issues);
}

public sealed record QubesRunLinkManifest(
    string? QubesRunId,
    string? QubesInputSnapshotId,
    string? QubesWeightsOutputId,
    MarketDataSnapshotId? MarketDataSnapshotId,
    string? EngineId,
    QubesEngineKind EngineKind,
    string? RunContractVersion,
    string? InputContractVersion,
    string? OutputContractVersion,
    string? RunFingerprint,
    DateTimeOffset CreatedAtUtc);

public sealed record QubesRunLinkIssue(
    QubesRunLinkIssueCode Code,
    string Message);

public sealed record QubesRunLinkValidationResult(
    bool IsValid,
    bool ContractValidQubesWeightsChain,
    bool AuthorizesOrdersFillsLedgerAccountingExecution,
    IReadOnlyList<QubesRunLinkIssue> Issues)
{
    public static QubesRunLinkValidationResult Valid()
        => new(true, ContractValidQubesWeightsChain: true, AuthorizesOrdersFillsLedgerAccountingExecution: false, []);

    public static QubesRunLinkValidationResult Invalid(IReadOnlyList<QubesRunLinkIssue> issues)
        => new(false, ContractValidQubesWeightsChain: false, AuthorizesOrdersFillsLedgerAccountingExecution: false, issues);
}

public sealed class QubesExternalEngineDescriptorValidator
{
    public QubesEngineRegistrationValidationResult Validate(QubesExternalEngineDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var issues = new List<QubesEngineRegistrationIssue>();
        ValidateEngineKind(descriptor, issues);
        ValidateRequiredFields(descriptor, issues);
        ValidatePrototypeEvidence(descriptor, issues);

        return issues.Count == 0
            ? QubesEngineRegistrationValidationResult.Valid(new RegisteredExternalQubesEngineAdapter(descriptor))
            : QubesEngineRegistrationValidationResult.Invalid(issues);
    }

    private static void ValidateEngineKind(QubesExternalEngineDescriptor descriptor, List<QubesEngineRegistrationIssue> issues)
    {
        if (descriptor.EngineKind == QubesEngineKind.SandboxPrototype)
        {
            Add(issues, QubesEngineRegistrationIssueCode.SandboxPrototypeCannotRegister, "SandboxPrototype cannot be registered as a real Qubes engine.");
        }

        if (descriptor.EngineKind == QubesEngineKind.ExternalEngineRequired)
        {
            Add(issues, QubesEngineRegistrationIssueCode.ExternalEngineRequiredNotEligible, "ExternalEngineRequired is blocked until a real engine descriptor is registered.");
        }

        if (descriptor.EngineKind != QubesEngineKind.RealEngine)
        {
            Add(issues, QubesEngineRegistrationIssueCode.EngineKindMustBeRealEngine, "Registered external Qubes engine descriptors must use EngineKind RealEngine.");
        }
    }

    private static void ValidateRequiredFields(QubesExternalEngineDescriptor descriptor, List<QubesEngineRegistrationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(descriptor.EngineId))
        {
            Add(issues, QubesEngineRegistrationIssueCode.MissingEngineId, "EngineId is required.");
        }

        if (string.IsNullOrWhiteSpace(descriptor.EngineName))
        {
            Add(issues, QubesEngineRegistrationIssueCode.MissingEngineName, "EngineName is required.");
        }

        if (string.IsNullOrWhiteSpace(descriptor.SourceRef))
        {
            Add(issues, QubesEngineRegistrationIssueCode.MissingSourceRef, "SourceRef or BuildRef is required.");
        }

        if (string.IsNullOrWhiteSpace(descriptor.BuildFingerprint))
        {
            Add(issues, QubesEngineRegistrationIssueCode.MissingBuildFingerprint, "BuildFingerprint or BinaryHash is required.");
        }

        if (string.IsNullOrWhiteSpace(descriptor.AdapterName))
        {
            Add(issues, QubesEngineRegistrationIssueCode.MissingAdapterName, "AdapterName or AdapterType is required.");
        }

        if (string.IsNullOrWhiteSpace(descriptor.SupportedInputContractVersion))
        {
            Add(issues, QubesEngineRegistrationIssueCode.MissingSupportedInputContractVersion, "SupportedInputContractVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(descriptor.SupportedOutputContractVersion))
        {
            Add(issues, QubesEngineRegistrationIssueCode.MissingSupportedOutputContractVersion, "SupportedOutputContractVersion is required.");
        }
    }

    private static void ValidatePrototypeEvidence(QubesExternalEngineDescriptor descriptor, List<QubesEngineRegistrationIssue> issues)
    {
        var evidence = new[]
        {
            descriptor.EngineId,
            descriptor.SourceRef,
            descriptor.BuildFingerprint,
            descriptor.AdapterName,
            descriptor.EvidenceQubesRunId,
            descriptor.EvidenceQubesWeightsOutputId,
            descriptor.EvidenceQubesOutputHash
        };

        if (evidence.Any(QubesStringNormalization.IsKnownR005RunId))
        {
            Add(issues, QubesEngineRegistrationIssueCode.R005RunIdRejected, "R005 run id cannot be used as real engine registration evidence.");
        }

        if (evidence.Any(QubesStringNormalization.IsKnownR005OutputId))
        {
            Add(issues, QubesEngineRegistrationIssueCode.R005OutputIdRejected, "R005 output id cannot be used as real engine registration evidence.");
        }

        if (evidence.Any(QubesStringNormalization.IsKnownR005OutputHash))
        {
            Add(issues, QubesEngineRegistrationIssueCode.R005OutputHashRejected, "R005 output hash cannot be used as real engine registration evidence.");
        }
    }

    private static void Add(List<QubesEngineRegistrationIssue> issues, QubesEngineRegistrationIssueCode code, string message)
    {
        if (issues.All(issue => issue.Code != code))
        {
            issues.Add(new QubesEngineRegistrationIssue(code, message));
        }
    }

    private static bool EqualsOrdinal(string? left, string? right)
        => QubesStringNormalization.EqualsIdentifier(left, right);
}

public sealed class QubesInputSnapshotManifestValidator
{
    public QubesInputSnapshotValidationResult Validate(QubesInputSnapshotManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var issues = new List<QubesInputSnapshotIssue>();
        ValidateEngineKind(manifest.EngineKind, issues);
        ValidateRequiredManifestFields(manifest, issues);
        ValidateComponents(manifest.Components, issues);
        ValidatePrototypeIdentifiers(manifest, issues);

        return issues.Count == 0
            ? QubesInputSnapshotValidationResult.Valid()
            : QubesInputSnapshotValidationResult.Invalid(issues);
    }

    public QubesInputSnapshotValidationResult ValidateForDescriptor(
        QubesExternalEngineDescriptor descriptor,
        QubesInputSnapshotManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(manifest);

        var issues = new List<QubesInputSnapshotIssue>();
        var descriptorValidation = new QubesExternalEngineDescriptorValidator().Validate(descriptor);
        if (!descriptorValidation.IsValid)
        {
            Add(issues, QubesInputSnapshotIssueCode.DescriptorInvalid, "A valid RealEngine descriptor is required for Qubes input snapshot use.");
        }

        issues.AddRange(Validate(manifest).Issues);

        if (!string.IsNullOrWhiteSpace(manifest.ExpectedEngineId) &&
            !string.IsNullOrWhiteSpace(descriptor.EngineId) &&
            !EqualsOrdinal(manifest.ExpectedEngineId, descriptor.EngineId))
        {
            Add(issues, QubesInputSnapshotIssueCode.EngineIdMismatch, "Qubes input snapshot ExpectedEngineId must match the registered real engine EngineId.");
        }

        if (!string.IsNullOrWhiteSpace(manifest.InputContractVersion) &&
            !string.IsNullOrWhiteSpace(descriptor.SupportedInputContractVersion) &&
            !EqualsOrdinal(manifest.InputContractVersion, descriptor.SupportedInputContractVersion))
        {
            Add(issues, QubesInputSnapshotIssueCode.InputContractVersionMismatch, "Qubes input snapshot contract version must match the registered engine supported input contract version.");
        }

        return issues.Count == 0
            ? QubesInputSnapshotValidationResult.Valid()
            : QubesInputSnapshotValidationResult.Invalid(issues);
    }

    private static void ValidateEngineKind(QubesEngineKind engineKind, List<QubesInputSnapshotIssue> issues)
    {
        if (engineKind == QubesEngineKind.SandboxPrototype)
        {
            Add(issues, QubesInputSnapshotIssueCode.SandboxPrototypeBlocked, "SandboxPrototype input snapshots are blocked for production/accounting use.");
        }

        if (engineKind == QubesEngineKind.ExternalEngineRequired)
        {
            Add(issues, QubesInputSnapshotIssueCode.ExternalEngineRequired, "ExternalEngineRequired cannot validate a production/accounting Qubes input snapshot.");
        }

        if (engineKind != QubesEngineKind.RealEngine)
        {
            Add(issues, QubesInputSnapshotIssueCode.EngineKindMustBeRealEngine, "Qubes input snapshots for production/accounting use must use EngineKind RealEngine.");
        }
    }

    private static void ValidateRequiredManifestFields(QubesInputSnapshotManifest manifest, List<QubesInputSnapshotIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(manifest.QubesInputSnapshotId))
        {
            Add(issues, QubesInputSnapshotIssueCode.MissingQubesInputSnapshotId, "QubesInputSnapshotId is required.");
        }

        if (manifest.MarketDataSnapshotId is null)
        {
            Add(issues, QubesInputSnapshotIssueCode.MissingMarketDataSnapshotId, "MarketDataSnapshotId is required for production/accounting-eligible Qubes input snapshots.");
        }

        if (string.IsNullOrWhiteSpace(manifest.InputContractVersion))
        {
            Add(issues, QubesInputSnapshotIssueCode.MissingInputContractVersion, "InputContractVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.ManifestHash))
        {
            Add(issues, QubesInputSnapshotIssueCode.MissingManifestHash, "ManifestHash or ContentHash is required.");
        }

        if (manifest.Components.Count == 0)
        {
            Add(issues, QubesInputSnapshotIssueCode.MissingComponents, "At least one referenced input component is required.");
        }
    }

    private static void ValidateComponents(IReadOnlyList<QubesInputSnapshotComponent> components, List<QubesInputSnapshotIssue> issues)
    {
        foreach (var component in components)
        {
            if (component.Kind == QubesInputSnapshotComponentKind.Unknown)
            {
                Add(issues, QubesInputSnapshotIssueCode.UnknownComponentKind, "Qubes input snapshot components must use a known component kind.");
            }

            if (string.IsNullOrWhiteSpace(component.Name))
            {
                Add(issues, QubesInputSnapshotIssueCode.MissingComponentName, "Qubes input snapshot component name is required.");
            }

            if (string.IsNullOrWhiteSpace(component.ContentHash))
            {
                Add(issues, QubesInputSnapshotIssueCode.MissingComponentHash, "Qubes input snapshot component content hash is required.");
            }
        }

        var duplicateOrders = components
            .GroupBy(component => component.Order)
            .Any(group => group.Count() > 1);
        if (duplicateOrders)
        {
            Add(issues, QubesInputSnapshotIssueCode.DuplicateComponentOrder, "Qubes input snapshot component order must be deterministic and unique.");
        }

        var duplicateIdentities = components
            .Where(component => !string.IsNullOrWhiteSpace(component.Name) && !string.IsNullOrWhiteSpace(component.ContentHash))
            .GroupBy(component => new { component.Kind, Name = component.Name!.Trim(), Hash = component.ContentHash!.Trim() })
            .Any(group => group.Count() > 1);
        if (duplicateIdentities)
        {
            Add(issues, QubesInputSnapshotIssueCode.DuplicateComponentIdentity, "Duplicate Qubes input snapshot component identities and hashes are rejected.");
        }

        var duplicateHashes = components
            .Where(component => !string.IsNullOrWhiteSpace(component.ContentHash))
            .GroupBy(component => QubesStringNormalization.NormalizeHashEvidence(component.ContentHash))
            .Any(group => group.Count() > 1);
        if (duplicateHashes)
        {
            Add(issues, QubesInputSnapshotIssueCode.DuplicateComponentHash, "Duplicate Qubes input snapshot component content hashes are rejected.");
        }
    }

    private static void ValidatePrototypeIdentifiers(QubesInputSnapshotManifest manifest, List<QubesInputSnapshotIssue> issues)
    {
        var evidence = new[]
        {
            manifest.QubesInputSnapshotId,
            manifest.ManifestHash
        }.Concat(manifest.Components.Select(component => component.ContentHash));

        if (evidence.Any(QubesStringNormalization.IsKnownR005RunId))
        {
            Add(issues, QubesInputSnapshotIssueCode.R005RunIdRejected, "R005 run id cannot be used as a Qubes input snapshot id.");
        }

        if (evidence.Any(QubesStringNormalization.IsKnownR005OutputId))
        {
            Add(issues, QubesInputSnapshotIssueCode.R005OutputIdRejected, "R005 output id cannot be used as Qubes input snapshot evidence.");
        }

        if (evidence.Any(QubesStringNormalization.IsKnownR005OutputHash))
        {
            Add(issues, QubesInputSnapshotIssueCode.R005OutputHashRejected, "R005 output hash cannot be used as Qubes input snapshot evidence.");
        }
    }

    private static void Add(List<QubesInputSnapshotIssue> issues, QubesInputSnapshotIssueCode code, string message)
    {
        if (issues.All(issue => issue.Code != code))
        {
            issues.Add(new QubesInputSnapshotIssue(code, message));
        }
    }

    private static bool EqualsOrdinal(string? left, string? right)
        => QubesStringNormalization.EqualsIdentifier(left, right);
}

public sealed class QubesWeightsOutputManifestValidator
{
    public QubesWeightsOutputValidationResult Validate(QubesWeightsOutputManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var issues = new List<QubesWeightsOutputIssue>();
        ValidateEngineKind(manifest.EngineKind, issues);
        ValidateRequiredManifestFields(manifest, issues);
        ValidateWeightEntries(manifest.WeightEntries, issues);
        ValidatePrototypeIdentifiers(manifest, issues);

        return issues.Count == 0
            ? QubesWeightsOutputValidationResult.Valid()
            : QubesWeightsOutputValidationResult.Invalid(issues);
    }

    public QubesWeightsOutputValidationResult ValidateForDescriptorAndSnapshot(
        QubesExternalEngineDescriptor descriptor,
        QubesInputSnapshotManifest snapshot,
        QubesWeightsOutputManifest output)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(output);

        var issues = new List<QubesWeightsOutputIssue>();
        var descriptorValidation = new QubesExternalEngineDescriptorValidator().Validate(descriptor);
        if (!descriptorValidation.IsValid)
        {
            Add(issues, QubesWeightsOutputIssueCode.DescriptorInvalid, "A valid RealEngine descriptor is required for Qubes weights output validation.");
        }

        var snapshotValidation = new QubesInputSnapshotManifestValidator().ValidateForDescriptor(descriptor, snapshot);
        if (!snapshotValidation.IsValid)
        {
            Add(issues, QubesWeightsOutputIssueCode.SnapshotInvalid, "A valid descriptor-compatible Qubes input snapshot is required for weights output validation.");
        }

        issues.AddRange(Validate(output).Issues);
        ValidateLineage(descriptor, snapshot, output, issues);

        return issues.Count == 0
            ? QubesWeightsOutputValidationResult.Valid()
            : QubesWeightsOutputValidationResult.Invalid(issues);
    }

    private static void ValidateEngineKind(QubesEngineKind engineKind, List<QubesWeightsOutputIssue> issues)
    {
        if (engineKind == QubesEngineKind.SandboxPrototype)
        {
            Add(issues, QubesWeightsOutputIssueCode.SandboxPrototypeBlocked, "SandboxPrototype Qubes weights outputs are blocked for production/accounting use.");
        }

        if (engineKind == QubesEngineKind.ExternalEngineRequired)
        {
            Add(issues, QubesWeightsOutputIssueCode.ExternalEngineRequired, "ExternalEngineRequired cannot validate a production/accounting Qubes weights output.");
        }

        if (engineKind != QubesEngineKind.RealEngine)
        {
            Add(issues, QubesWeightsOutputIssueCode.EngineKindMustBeRealEngine, "Qubes weights outputs for production/accounting use must use EngineKind RealEngine.");
        }
    }

    private static void ValidateRequiredManifestFields(QubesWeightsOutputManifest manifest, List<QubesWeightsOutputIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(manifest.QubesWeightsOutputId))
        {
            Add(issues, QubesWeightsOutputIssueCode.MissingQubesWeightsOutputId, "QubesWeightsOutputId is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.QubesRunId))
        {
            Add(issues, QubesWeightsOutputIssueCode.MissingQubesRunId, "QubesRunId is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.QubesInputSnapshotId))
        {
            Add(issues, QubesWeightsOutputIssueCode.MissingQubesInputSnapshotId, "QubesInputSnapshotId is required.");
        }

        if (manifest.MarketDataSnapshotId is null)
        {
            Add(issues, QubesWeightsOutputIssueCode.MissingMarketDataSnapshotId, "MarketDataSnapshotId is required for production/accounting-eligible Qubes weights outputs.");
        }

        if (string.IsNullOrWhiteSpace(manifest.EngineId))
        {
            Add(issues, QubesWeightsOutputIssueCode.MissingEngineId, "EngineId is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.OutputContractVersion))
        {
            Add(issues, QubesWeightsOutputIssueCode.MissingOutputContractVersion, "OutputContractVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.OutputHash))
        {
            Add(issues, QubesWeightsOutputIssueCode.MissingOutputHash, "OutputHash or ContentHash is required.");
        }

        if (manifest.WeightEntries.Count == 0)
        {
            Add(issues, QubesWeightsOutputIssueCode.MissingWeightEntries, "At least one economic weight entry is required.");
        }
    }

    private static void ValidateWeightEntries(IReadOnlyList<QubesWeightEntry> entries, List<QubesWeightsOutputIssue> issues)
    {
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.WeightKey))
            {
                Add(issues, QubesWeightsOutputIssueCode.MissingWeightKey, "Qubes weight entry key is required.");
            }

            if (!double.IsFinite(entry.Weight))
            {
                Add(issues, QubesWeightsOutputIssueCode.NonFiniteWeight, "Qubes weight entry weight must be finite.");
            }
        }

        var duplicateKeys = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.WeightKey))
            .GroupBy(entry => entry.WeightKey!.Trim())
            .Any(group => group.Count() > 1);
        if (duplicateKeys)
        {
            Add(issues, QubesWeightsOutputIssueCode.DuplicateWeightKey, "Duplicate Qubes weight entry keys are rejected.");
        }

        var duplicateOrders = entries
            .GroupBy(entry => entry.Order)
            .Any(group => group.Count() > 1);
        if (duplicateOrders)
        {
            Add(issues, QubesWeightsOutputIssueCode.DuplicateEntryOrder, "Qubes weight entry order must be deterministic and unique.");
        }

        var duplicateEntryHashes = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.EntryHash))
            .GroupBy(entry => QubesStringNormalization.NormalizeHashEvidence(entry.EntryHash))
            .Any(group => group.Count() > 1);
        if (duplicateEntryHashes)
        {
            Add(issues, QubesWeightsOutputIssueCode.DuplicateEntryHash, "Duplicate Qubes weight entry hashes are rejected.");
        }
    }

    private static void ValidatePrototypeIdentifiers(QubesWeightsOutputManifest manifest, List<QubesWeightsOutputIssue> issues)
    {
        var evidence = new[]
        {
            manifest.QubesWeightsOutputId,
            manifest.QubesRunId,
            manifest.QubesInputSnapshotId,
            manifest.EngineId,
            manifest.OutputHash
        };
        var entryHashes = manifest.WeightEntries.Select(entry => entry.EntryHash);

        if (evidence.Concat(entryHashes).Any(QubesStringNormalization.IsKnownR005RunId))
        {
            Add(issues, QubesWeightsOutputIssueCode.R005RunIdRejected, "R005 run id cannot be used as Qubes weights output evidence.");
        }

        if (evidence.Concat(entryHashes).Any(QubesStringNormalization.IsKnownR005OutputId))
        {
            Add(issues, QubesWeightsOutputIssueCode.R005OutputIdRejected, "R005 output id cannot be used as Qubes weights output evidence.");
        }

        if (evidence.Concat(entryHashes).Any(QubesStringNormalization.IsKnownR005OutputHash))
        {
            Add(issues, QubesWeightsOutputIssueCode.R005OutputHashRejected, "R005 output hash cannot be used as Qubes weights output evidence.");
        }
    }

    private static void ValidateLineage(
        QubesExternalEngineDescriptor descriptor,
        QubesInputSnapshotManifest snapshot,
        QubesWeightsOutputManifest output,
        List<QubesWeightsOutputIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(output.QubesInputSnapshotId) &&
            !string.IsNullOrWhiteSpace(snapshot.QubesInputSnapshotId) &&
            !EqualsOrdinal(output.QubesInputSnapshotId, snapshot.QubesInputSnapshotId))
        {
            Add(issues, QubesWeightsOutputIssueCode.InputSnapshotMismatch, "Qubes weights output input snapshot id must match the input snapshot manifest.");
        }

        if (output.MarketDataSnapshotId is not null &&
            snapshot.MarketDataSnapshotId is not null &&
            output.MarketDataSnapshotId.Value != snapshot.MarketDataSnapshotId.Value)
        {
            Add(issues, QubesWeightsOutputIssueCode.MarketDataSnapshotMismatch, "Qubes weights output MarketDataSnapshotId must match the input snapshot manifest.");
        }

        if (!string.IsNullOrWhiteSpace(output.EngineId) &&
            !string.IsNullOrWhiteSpace(descriptor.EngineId) &&
            !EqualsOrdinal(output.EngineId, descriptor.EngineId))
        {
            Add(issues, QubesWeightsOutputIssueCode.EngineIdMismatch, "Qubes weights output EngineId must match the registered real engine descriptor.");
        }

        if (!string.IsNullOrWhiteSpace(output.OutputContractVersion) &&
            !string.IsNullOrWhiteSpace(descriptor.SupportedOutputContractVersion) &&
            !EqualsOrdinal(output.OutputContractVersion, descriptor.SupportedOutputContractVersion))
        {
            Add(issues, QubesWeightsOutputIssueCode.OutputContractVersionMismatch, "Qubes weights output contract version must match the registered engine supported output contract version.");
        }
    }

    private static void Add(List<QubesWeightsOutputIssue> issues, QubesWeightsOutputIssueCode code, string message)
    {
        if (issues.All(issue => issue.Code != code))
        {
            issues.Add(new QubesWeightsOutputIssue(code, message));
        }
    }

    private static bool EqualsOrdinal(string? left, string? right)
        => QubesStringNormalization.EqualsIdentifier(left, right);
}

public sealed class QubesRunLinkManifestValidator
{
    public QubesRunLinkValidationResult Validate(QubesRunLinkManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var issues = new List<QubesRunLinkIssue>();
        ValidateEngineKind(manifest.EngineKind, issues);
        ValidateRequiredFields(manifest, issues);
        ValidatePrototypeIdentifiers(manifest, issues);

        return issues.Count == 0
            ? QubesRunLinkValidationResult.Valid()
            : QubesRunLinkValidationResult.Invalid(issues);
    }

    public QubesRunLinkValidationResult ValidateFullChain(
        QubesExternalEngineDescriptor descriptor,
        QubesInputSnapshotManifest snapshot,
        QubesRunLinkManifest runLink,
        QubesWeightsOutputManifest output)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(runLink);
        ArgumentNullException.ThrowIfNull(output);

        var issues = new List<QubesRunLinkIssue>();
        var descriptorValidation = new QubesExternalEngineDescriptorValidator().Validate(descriptor);
        if (!descriptorValidation.IsValid)
        {
            foreach (var descriptorIssue in descriptorValidation.Issues)
            {
                Add(issues, QubesRunLinkIssueCode.DescriptorInvalid, $"Descriptor validation failed: {descriptorIssue.Code}.");
            }
        }

        var snapshotValidation = new QubesInputSnapshotManifestValidator().ValidateForDescriptor(descriptor, snapshot);
        if (!snapshotValidation.IsValid)
        {
            foreach (var snapshotIssue in snapshotValidation.Issues)
            {
                Add(issues, QubesRunLinkIssueCode.SnapshotInvalid, $"Input snapshot validation failed: {snapshotIssue.Code}.");
            }
        }

        var outputValidation = new QubesWeightsOutputManifestValidator().ValidateForDescriptorAndSnapshot(descriptor, snapshot, output);
        if (!outputValidation.IsValid)
        {
            foreach (var outputIssue in outputValidation.Issues)
            {
                Add(issues, QubesRunLinkIssueCode.OutputInvalid, $"Weights output validation failed: {outputIssue.Code}.");
            }
        }

        issues.AddRange(Validate(runLink).Issues);
        ValidateLineage(descriptor, snapshot, runLink, output, issues);
        ValidateProductionUsage(descriptorValidation.IsValid, snapshot, runLink, output, issues);

        return issues.Count == 0
            ? QubesRunLinkValidationResult.Valid()
            : QubesRunLinkValidationResult.Invalid(issues);
    }

    private static void ValidateEngineKind(QubesEngineKind engineKind, List<QubesRunLinkIssue> issues)
    {
        if (engineKind == QubesEngineKind.SandboxPrototype)
        {
            Add(issues, QubesRunLinkIssueCode.SandboxPrototypeBlocked, "SandboxPrototype Qubes run links are blocked for production/accounting use.");
        }

        if (engineKind == QubesEngineKind.ExternalEngineRequired)
        {
            Add(issues, QubesRunLinkIssueCode.ExternalEngineRequired, "ExternalEngineRequired cannot validate a production/accounting Qubes run link.");
        }

        if (engineKind != QubesEngineKind.RealEngine)
        {
            Add(issues, QubesRunLinkIssueCode.EngineKindMustBeRealEngine, "Qubes run links for production/accounting use must use EngineKind RealEngine.");
        }
    }

    private static void ValidateRequiredFields(QubesRunLinkManifest manifest, List<QubesRunLinkIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(manifest.QubesRunId))
        {
            Add(issues, QubesRunLinkIssueCode.MissingQubesRunId, "QubesRunId is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.QubesInputSnapshotId))
        {
            Add(issues, QubesRunLinkIssueCode.MissingQubesInputSnapshotId, "QubesInputSnapshotId is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.QubesWeightsOutputId))
        {
            Add(issues, QubesRunLinkIssueCode.MissingQubesWeightsOutputId, "QubesWeightsOutputId is required.");
        }

        if (manifest.MarketDataSnapshotId is null)
        {
            Add(issues, QubesRunLinkIssueCode.MissingMarketDataSnapshotId, "MarketDataSnapshotId is required for production/accounting-eligible Qubes run links.");
        }

        if (string.IsNullOrWhiteSpace(manifest.EngineId))
        {
            Add(issues, QubesRunLinkIssueCode.MissingEngineId, "EngineId is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.RunContractVersion))
        {
            Add(issues, QubesRunLinkIssueCode.MissingRunContractVersion, "RunContractVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.InputContractVersion))
        {
            Add(issues, QubesRunLinkIssueCode.MissingInputContractVersion, "InputContractVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.OutputContractVersion))
        {
            Add(issues, QubesRunLinkIssueCode.MissingOutputContractVersion, "OutputContractVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.RunFingerprint))
        {
            Add(issues, QubesRunLinkIssueCode.MissingRunFingerprint, "RunFingerprint or RunHash is required.");
        }
    }

    private static void ValidatePrototypeIdentifiers(QubesRunLinkManifest manifest, List<QubesRunLinkIssue> issues)
    {
        var evidence = new[]
        {
            manifest.QubesRunId,
            manifest.QubesInputSnapshotId,
            manifest.QubesWeightsOutputId,
            manifest.EngineId,
            manifest.RunFingerprint
        };

        if (evidence.Any(QubesStringNormalization.IsKnownR005RunId))
        {
            Add(issues, QubesRunLinkIssueCode.R005RunIdRejected, "R005 run id cannot be used as Qubes run link evidence.");
        }

        if (evidence.Any(QubesStringNormalization.IsKnownR005OutputId))
        {
            Add(issues, QubesRunLinkIssueCode.R005OutputIdRejected, "R005 output id cannot be used as Qubes run link evidence.");
        }

        if (evidence.Any(QubesStringNormalization.IsKnownR005OutputHash))
        {
            Add(issues, QubesRunLinkIssueCode.R005OutputHashRejected, "R005 output hash cannot be used as Qubes run link evidence.");
        }
    }

    private static void ValidateLineage(
        QubesExternalEngineDescriptor descriptor,
        QubesInputSnapshotManifest snapshot,
        QubesRunLinkManifest runLink,
        QubesWeightsOutputManifest output,
        List<QubesRunLinkIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(runLink.QubesInputSnapshotId) &&
            !string.IsNullOrWhiteSpace(snapshot.QubesInputSnapshotId) &&
            !EqualsOrdinal(runLink.QubesInputSnapshotId, snapshot.QubesInputSnapshotId))
        {
            Add(issues, QubesRunLinkIssueCode.InputSnapshotMismatch, "Qubes run link input snapshot id must match the input snapshot manifest.");
        }

        if (!string.IsNullOrWhiteSpace(runLink.QubesInputSnapshotId) &&
            !string.IsNullOrWhiteSpace(output.QubesInputSnapshotId) &&
            !EqualsOrdinal(runLink.QubesInputSnapshotId, output.QubesInputSnapshotId))
        {
            Add(issues, QubesRunLinkIssueCode.InputSnapshotMismatch, "Qubes run link input snapshot id must match the weights output manifest.");
        }

        if (!string.IsNullOrWhiteSpace(runLink.QubesWeightsOutputId) &&
            !string.IsNullOrWhiteSpace(output.QubesWeightsOutputId) &&
            !EqualsOrdinal(runLink.QubesWeightsOutputId, output.QubesWeightsOutputId))
        {
            Add(issues, QubesRunLinkIssueCode.WeightsOutputMismatch, "Qubes run link weights output id must match the weights output manifest.");
        }

        if (runLink.MarketDataSnapshotId is not null &&
            snapshot.MarketDataSnapshotId is not null &&
            runLink.MarketDataSnapshotId.Value != snapshot.MarketDataSnapshotId.Value)
        {
            Add(issues, QubesRunLinkIssueCode.MarketDataSnapshotMismatch, "Qubes run link MarketDataSnapshotId must match the input snapshot manifest.");
        }

        if (runLink.MarketDataSnapshotId is not null &&
            output.MarketDataSnapshotId is not null &&
            runLink.MarketDataSnapshotId.Value != output.MarketDataSnapshotId.Value)
        {
            Add(issues, QubesRunLinkIssueCode.MarketDataSnapshotMismatch, "Qubes run link MarketDataSnapshotId must match the weights output manifest.");
        }

        if (!string.IsNullOrWhiteSpace(runLink.EngineId) &&
            !string.IsNullOrWhiteSpace(descriptor.EngineId) &&
            !EqualsOrdinal(runLink.EngineId, descriptor.EngineId))
        {
            Add(issues, QubesRunLinkIssueCode.EngineIdMismatch, "Qubes run link EngineId must match the external engine descriptor.");
        }

        if (!string.IsNullOrWhiteSpace(runLink.EngineId) &&
            !string.IsNullOrWhiteSpace(output.EngineId) &&
            !EqualsOrdinal(runLink.EngineId, output.EngineId))
        {
            Add(issues, QubesRunLinkIssueCode.EngineIdMismatch, "Qubes run link EngineId must match the weights output manifest.");
        }

        if (!string.IsNullOrWhiteSpace(runLink.QubesRunId) &&
            !string.IsNullOrWhiteSpace(output.QubesRunId) &&
            !EqualsOrdinal(runLink.QubesRunId, output.QubesRunId))
        {
            Add(issues, QubesRunLinkIssueCode.RunIdMismatch, "Qubes run link QubesRunId must match the weights output manifest.");
        }

        if (!string.IsNullOrWhiteSpace(runLink.InputContractVersion) &&
            !string.IsNullOrWhiteSpace(snapshot.InputContractVersion) &&
            !EqualsOrdinal(runLink.InputContractVersion, snapshot.InputContractVersion))
        {
            Add(issues, QubesRunLinkIssueCode.InputContractVersionMismatch, "Qubes run link input contract version must match the input snapshot manifest.");
        }

        if (!string.IsNullOrWhiteSpace(runLink.OutputContractVersion) &&
            !string.IsNullOrWhiteSpace(output.OutputContractVersion) &&
            !EqualsOrdinal(runLink.OutputContractVersion, output.OutputContractVersion))
        {
            Add(issues, QubesRunLinkIssueCode.OutputContractVersionMismatch, "Qubes run link output contract version must match the weights output manifest.");
        }
    }

    private static void ValidateProductionUsage(
        bool descriptorIsValid,
        QubesInputSnapshotManifest snapshot,
        QubesRunLinkManifest runLink,
        QubesWeightsOutputManifest output,
        List<QubesRunLinkIssue> issues)
    {
        var boundMarketDataSnapshotIds = runLink.MarketDataSnapshotId is null
            ? Array.Empty<MarketDataSnapshotId>()
            : [runLink.MarketDataSnapshotId.Value];

        var productionUsage = new QubesProductionUsageValidator().Validate(new QubesProductionValidationRequest(
            QubesProductionUsage.Production,
            new QubesInputSnapshotRef(snapshot.QubesInputSnapshotId, snapshot.MarketDataSnapshotId, snapshot.EngineKind),
            new QubesRunRef(runLink.QubesRunId, runLink.EngineKind, boundMarketDataSnapshotIds),
            new QubesWeightsOutputRef(
                output.QubesWeightsOutputId,
                output.OutputHash,
                output.QubesRunId,
                output.QubesInputSnapshotId,
                output.MarketDataSnapshotId,
                output.EngineKind),
            RealEngineAdapterRegistered: descriptorIsValid));

        if (!productionUsage.IsAllowed)
        {
            foreach (var productionIssue in productionUsage.Issues)
            {
                Add(issues, QubesRunLinkIssueCode.ProductionUsageBlocked, $"Production usage validation failed: {productionIssue.Code}.");
            }
        }
    }

    private static void Add(List<QubesRunLinkIssue> issues, QubesRunLinkIssueCode code, string message)
    {
        if (issues.All(issue => issue.Code != code || !string.Equals(issue.Message, message, StringComparison.Ordinal)))
        {
            issues.Add(new QubesRunLinkIssue(code, message));
        }
    }

    private static bool EqualsOrdinal(string? left, string? right)
        => QubesStringNormalization.EqualsIdentifier(left, right);
}

public sealed class QubesProductionUsageValidator
{
    public QubesProductionValidationResult Validate(QubesProductionValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.InputSnapshot);
        ArgumentNullException.ThrowIfNull(request.Run);
        ArgumentNullException.ThrowIfNull(request.Output);

        var issues = new List<QubesProductionValidationIssue>();
        ValidateEngineKind(request, issues);
        ValidateRequiredIds(request, issues);
        ValidateR005Identifiers(request, issues);
        ValidateLineage(request, issues);

        return issues.Count == 0
            ? QubesProductionValidationResult.Allowed()
            : QubesProductionValidationResult.Blocked(issues);
    }

    private static void ValidateEngineKind(QubesProductionValidationRequest request, List<QubesProductionValidationIssue> issues)
    {
        if (request.InputSnapshot.EngineKind == QubesEngineKind.SandboxPrototype ||
            request.Run.EngineKind == QubesEngineKind.SandboxPrototype ||
            request.Output.EngineKind == QubesEngineKind.SandboxPrototype)
        {
            Add(issues, QubesProductionValidationIssueCode.SandboxPrototypeBlocked, "Sandbox Qubes prototype output is blocked for production/accounting use.");
        }

        if (request.InputSnapshot.EngineKind == QubesEngineKind.ExternalEngineRequired ||
            request.Run.EngineKind == QubesEngineKind.ExternalEngineRequired ||
            request.Output.EngineKind == QubesEngineKind.ExternalEngineRequired ||
            !request.RealEngineAdapterRegistered)
        {
            Add(issues, QubesProductionValidationIssueCode.ExternalEngineRequired, "External Qubes engine integration is required before production/accounting use.");
        }
    }

    private static void ValidateRequiredIds(QubesProductionValidationRequest request, List<QubesProductionValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(request.InputSnapshot.QubesInputSnapshotId))
        {
            Add(issues, QubesProductionValidationIssueCode.MissingQubesInputSnapshotId, "QubesInputSnapshotId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Run.QubesRunId))
        {
            Add(issues, QubesProductionValidationIssueCode.MissingQubesRunId, "QubesRunId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Output.QubesWeightsOutputId))
        {
            Add(issues, QubesProductionValidationIssueCode.MissingQubesWeightsOutputId, "QubesWeightsOutputId is required.");
        }

        if (request.InputSnapshot.MarketDataSnapshotId is null ||
            request.Output.MarketDataSnapshotId is null ||
            request.Run.BoundMarketDataSnapshotIds.Count == 0)
        {
            Add(issues, QubesProductionValidationIssueCode.MissingMarketDataSnapshotId, "MarketDataSnapshotId is required and must be bound to the Qubes run and output.");
        }
    }

    private static void ValidateR005Identifiers(QubesProductionValidationRequest request, List<QubesProductionValidationIssue> issues)
    {
        if (QubesStringNormalization.IsKnownR005RunId(request.Run.QubesRunId) ||
            QubesStringNormalization.IsKnownR005RunId(request.Output.QubesRunId))
        {
            Add(issues, QubesProductionValidationIssueCode.R005RunIdPrototypeOnly, "R005 Qubes run id is prototype-only.");
        }

        if (QubesStringNormalization.IsKnownR005OutputId(request.Output.QubesWeightsOutputId))
        {
            Add(issues, QubesProductionValidationIssueCode.R005OutputIdRejected, "R005 Qubes output id is rejected for production/accounting use.");
        }

        if (QubesStringNormalization.IsKnownR005OutputHash(request.Output.QubesOutputHash))
        {
            Add(issues, QubesProductionValidationIssueCode.R005OutputHashRejected, "R005 Qubes output hash is rejected for production/accounting use.");
        }
    }

    private static void ValidateLineage(QubesProductionValidationRequest request, List<QubesProductionValidationIssue> issues)
    {
        var uniqueMarketDataBindings = request.Run.BoundMarketDataSnapshotIds.Distinct().ToArray();
        if (uniqueMarketDataBindings.Length == 0)
        {
            Add(issues, QubesProductionValidationIssueCode.RunMarketDataBindingMissing, "QubesRunId must be bound to exactly one MarketDataSnapshotId.");
            return;
        }

        if (uniqueMarketDataBindings.Length != 1 || request.Run.BoundMarketDataSnapshotIds.Count != 1)
        {
            Add(issues, QubesProductionValidationIssueCode.RunMarketDataBindingNotUnique, "QubesRunId has zero or multiple MarketDataSnapshotId bindings.");
            return;
        }

        var runMarketDataSnapshotId = uniqueMarketDataBindings[0];
        if (request.InputSnapshot.MarketDataSnapshotId is not null &&
            request.InputSnapshot.MarketDataSnapshotId.Value != runMarketDataSnapshotId)
        {
            Add(issues, QubesProductionValidationIssueCode.RunMarketDataBindingMissing, "Qubes input snapshot MarketDataSnapshotId must match the Qubes run binding.");
        }

        if (request.Output.MarketDataSnapshotId is not null &&
            request.Output.MarketDataSnapshotId.Value != runMarketDataSnapshotId)
        {
            Add(issues, QubesProductionValidationIssueCode.OutputMarketDataMismatch, "Qubes output MarketDataSnapshotId must match the Qubes run binding.");
        }

        if (!string.IsNullOrWhiteSpace(request.Output.QubesRunId) &&
            !EqualsOrdinal(request.Output.QubesRunId, request.Run.QubesRunId))
        {
            Add(issues, QubesProductionValidationIssueCode.OutputRunIdMismatch, "Qubes output run id must match QubesRunId.");
        }

        if (!string.IsNullOrWhiteSpace(request.Output.QubesInputSnapshotId) &&
            !EqualsOrdinal(request.Output.QubesInputSnapshotId, request.InputSnapshot.QubesInputSnapshotId))
        {
            Add(issues, QubesProductionValidationIssueCode.OutputInputSnapshotMismatch, "Qubes output input snapshot id must match QubesInputSnapshotId.");
        }
    }

    private static void Add(List<QubesProductionValidationIssue> issues, QubesProductionValidationIssueCode code, string message)
    {
        if (issues.All(issue => issue.Code != code))
        {
            issues.Add(new QubesProductionValidationIssue(code, message));
        }
    }

    private static bool EqualsOrdinal(string? left, string? right)
        => QubesStringNormalization.EqualsIdentifier(left, right);
}

public static class QubesProductionBoundaryGuard
{
    public static QubesProductionValidationResult ValidateSandboxPrototypeOutput(
        SandboxQubesOutput output,
        QubesProductionUsage usage,
        string? outputHash = null)
    {
        ArgumentNullException.ThrowIfNull(output);

        var marketDataSnapshotId = TryParseMarketDataSnapshotId(output.MarketDataSnapshotId);
        return new QubesProductionUsageValidator().Validate(new QubesProductionValidationRequest(
            usage,
            new QubesInputSnapshotRef(output.InputSnapshotId, marketDataSnapshotId, QubesEngineKind.SandboxPrototype),
            new QubesRunRef(output.SandboxQubesRunId, QubesEngineKind.SandboxPrototype, marketDataSnapshotId is null ? [] : [marketDataSnapshotId.Value]),
            new QubesWeightsOutputRef(output.QubesOutputId, outputHash, output.SandboxQubesRunId, output.InputSnapshotId, marketDataSnapshotId, QubesEngineKind.SandboxPrototype),
            RealEngineAdapterRegistered: false));
    }

    public static void EnsureSandboxPrototypeBlockedForProductionAccounting(SandboxQubesOutput output, string? outputHash = null)
    {
        var production = ValidateSandboxPrototypeOutput(output, QubesProductionUsage.Production, outputHash);
        var accounting = ValidateSandboxPrototypeOutput(output, QubesProductionUsage.Accounting, outputHash);

        if (production.IsAllowed || accounting.IsAllowed)
        {
            throw new DomainRuleViolationException("Sandbox Qubes prototype output must never be production/accounting eligible.");
        }
    }

    public static QubesProductionValidationResult ValidateFixtureIngestion(
        QubesFxWeightsIngestionResult ingestion,
        QubesProductionUsage usage)
    {
        ArgumentNullException.ThrowIfNull(ingestion);

        return new QubesProductionUsageValidator().Validate(new QubesProductionValidationRequest(
            usage,
            new QubesInputSnapshotRef(null, null, QubesEngineKind.ExternalEngineRequired),
            new QubesRunRef(ingestion.QubesRunId.Value, QubesEngineKind.ExternalEngineRequired, []),
            new QubesWeightsOutputRef(null, null, ingestion.QubesRunId.Value, null, null, QubesEngineKind.ExternalEngineRequired),
            RealEngineAdapterRegistered: false));
    }

    public static void EnsureFixtureIngestionBlockedForProductionAccounting(QubesFxWeightsIngestionResult ingestion)
    {
        var production = ValidateFixtureIngestion(ingestion, QubesProductionUsage.Production);
        var accounting = ValidateFixtureIngestion(ingestion, QubesProductionUsage.Accounting);

        if (production.IsAllowed || accounting.IsAllowed)
        {
            throw new DomainRuleViolationException("Qubes fixture ingestion must never be production/accounting eligible.");
        }
    }

    private static MarketDataSnapshotId? TryParseMarketDataSnapshotId(string? marketDataSnapshotId)
        => Guid.TryParse(marketDataSnapshotId, out var parsed)
            ? new MarketDataSnapshotId(parsed)
            : null;
}

public enum QubesEngineAdapterStatus
{
    BlockedNotConfigured,
    ExternalEngineRequired,
    Succeeded
}

public sealed record QubesEngineAdapterResult(
    QubesEngineAdapterStatus Status,
    QubesWeightsOutputRef? Output,
    IReadOnlyList<string> Issues)
{
    public bool Succeeded => Status == QubesEngineAdapterStatus.Succeeded && Output is not null;
}

public interface IQubesEngineAdapter
{
    QubesEngineKind EngineKind { get; }
    bool FabricatesMarketData { get; }
    bool RequiresAccountId { get; }
    bool RequiresAccountCurrency { get; }
    bool GeneratesOrders { get; }
    bool GeneratesFills { get; }
    bool WritesLedger { get; }

    Task<QubesEngineAdapterResult> ProduceWeightsAsync(QubesInputSnapshotRef input, CancellationToken cancellationToken);
}

public sealed class QubesEngineAdapterRegistry
{
    public IQubesEngineAdapter Resolve(QubesExternalEngineDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return new DisabledQubesEngineAdapter();
        }

        var validation = new QubesExternalEngineDescriptorValidator().Validate(descriptor);
        return validation.IsValid ? validation.Adapter : new DisabledQubesEngineAdapter();
    }
}

public sealed class RegisteredExternalQubesEngineAdapter(QubesExternalEngineDescriptor descriptor) : IQubesEngineAdapter
{
    public QubesEngineKind EngineKind => descriptor.EngineKind;
    public bool FabricatesMarketData => false;
    public bool RequiresAccountId => false;
    public bool RequiresAccountCurrency => false;
    public bool GeneratesOrders => false;
    public bool GeneratesFills => false;
    public bool WritesLedger => false;

    public Task<QubesEngineAdapterResult> ProduceWeightsAsync(QubesInputSnapshotRef input, CancellationToken cancellationToken)
        => Task.FromResult(new QubesEngineAdapterResult(
            QubesEngineAdapterStatus.BlockedNotConfigured,
            Output: null,
            Issues: ["External Qubes engine descriptor is registered, but no executable adapter is implemented in this repository."]));
}

public sealed class DisabledQubesEngineAdapter : IQubesEngineAdapter
{
    public QubesEngineKind EngineKind => QubesEngineKind.ExternalEngineRequired;
    public bool FabricatesMarketData => false;
    public bool RequiresAccountId => false;
    public bool RequiresAccountCurrency => false;
    public bool GeneratesOrders => false;
    public bool GeneratesFills => false;
    public bool WritesLedger => false;

    public Task<QubesEngineAdapterResult> ProduceWeightsAsync(QubesInputSnapshotRef input, CancellationToken cancellationToken)
        => Task.FromResult(new QubesEngineAdapterResult(
            QubesEngineAdapterStatus.ExternalEngineRequired,
            Output: null,
            Issues: ["Real Qubes engine adapter is not configured."]));
}
