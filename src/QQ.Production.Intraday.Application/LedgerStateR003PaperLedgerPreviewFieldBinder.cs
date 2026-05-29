using System.Security.Cryptography;
using System.Text;

namespace QQ.Production.Intraday.Application;

public enum LedgerStateR003PreviewDecision
{
    PaperLedgerPreviewHardenedWithPmsQubesBindings,
    PaperLedgerPreviewHardenedWithMissingEconomicFields,
    PaperLedgerPreviewBlockedMissingCoreBindings,
    InconclusiveSafe
}

public enum LedgerStateR003BindingEvidenceStatus
{
    Bound,
    AvailableNotLineLinked,
    Missing
}

public sealed record LedgerStateR003FieldEvidence(
    string FieldName,
    string? Value,
    string SourceArtifact,
    string Confidence,
    string ContractId,
    LedgerStateR003BindingEvidenceStatus EvidenceStatus,
    IReadOnlyList<string> AppliesToClOrdIds);

public sealed record LedgerStateR003BinderRequest(
    string RequestId,
    IReadOnlyList<LedgerStateR002PaperLedgerPreviewLine> R002PreviewLines,
    IReadOnlyList<LedgerStateR003FieldEvidence> FieldEvidence,
    bool PreviewOnly,
    bool CommitAllowed,
    bool LedgerMutationAllowed,
    bool TradingStateMutationAllowed);

public sealed record LedgerStateR003FieldBinding(
    string FieldName,
    string? Value,
    string SourceArtifact,
    string Confidence,
    string ContractId,
    LedgerStateR003BindingEvidenceStatus EvidenceStatus);

public sealed record LedgerStateR003BoundPaperLedgerPreviewLine(
    string LineId,
    string SourceFillId,
    string SourceExecutionReportId,
    string SourceSandboxOrderId,
    string ClOrdId,
    string Symbol,
    string Side,
    decimal Quantity,
    decimal Price,
    string SecurityID,
    string SecurityIDSource,
    bool PreviewOnly,
    bool CommitAllowed,
    bool LedgerMutation,
    bool TradingStateMutation,
    string? AccountId,
    string? PortfolioId,
    string? StrategyId,
    string? PmsCycleId,
    string? QubesRunId,
    string? RiskReviewId,
    string? OperatorApprovalId,
    DateTimeOffset? CanonicalTargetCloseUtc,
    string? SourceRebalanceIntentId,
    string? SourceExecutionIntentId,
    IReadOnlyList<LedgerStateR003FieldBinding> FieldBindings,
    IReadOnlyList<string> MissingFieldBlockers,
    string IdempotencyKey,
    string PreviewHash,
    string AuditHash);

public sealed record LedgerStateR003HardenedPreviewContract(
    string ContractId,
    bool PreviewOnly,
    bool CommitAllowed,
    bool LedgerMutation,
    bool TradingStateMutation,
    bool MissingEconomicFieldsAllowedForPreviewOnly,
    bool MissingEconomicFieldsBlockCommit,
    int BoundLineCount,
    IReadOnlyList<string> RequiredBindingFields,
    IReadOnlyList<string> CommitBlockers);

public sealed record LedgerStateR003EconomicFieldGapDiagnostics(
    IReadOnlyList<string> ResolvedFields,
    IReadOnlyList<string> AvailableButNotLineLinkedFields,
    IReadOnlyList<string> MissingFields,
    bool CashImpactIncomplete,
    bool CommitBlocked);

public sealed record LedgerStateR003BinderResult(
    LedgerStateR003PreviewDecision Decision,
    LedgerStateR003HardenedPreviewContract HardenedContract,
    IReadOnlyList<LedgerStateR003BoundPaperLedgerPreviewLine> BoundLines,
    LedgerStateR003EconomicFieldGapDiagnostics EconomicFieldGaps,
    IReadOnlyList<string> CommitBlockers,
    IReadOnlyList<string> Diagnostics,
    string IdempotencyReviewHash,
    bool PreviewOnly,
    bool CommitAllowed,
    bool LedgerMutation,
    bool TradingStateMutation);

public sealed class LedgerStateR003PaperLedgerPreviewFieldBinder
{
    public static readonly string[] RequiredBindingFields =
    [
        "AccountId",
        "PortfolioId",
        "StrategyId",
        "PmsCycleId",
        "QubesRunId",
        "RiskReviewId",
        "OperatorApprovalId",
        "CanonicalTargetCloseUtc",
        "SourceRebalanceIntentId",
        "SourceExecutionIntentId"
    ];

    private static readonly string[] AlwaysMissingEconomicBlockers =
    [
        "MissingCommissionFeeModel",
        "MissingFxConversionModel",
        "CommitSafeIdempotencyPolicyIncomplete"
    ];

    public LedgerStateR003BinderResult Bind(LedgerStateR003BinderRequest request)
    {
        var diagnostics = ValidateRequest(request).ToList();
        var blocked = diagnostics.Any(x => x.EndsWith("Forbidden", StringComparison.Ordinal) || x.StartsWith("MissingCore", StringComparison.Ordinal));

        var boundLines = blocked
            ? Array.Empty<LedgerStateR003BoundPaperLedgerPreviewLine>()
            : request.R002PreviewLines.Select(line => BindLine(request.RequestId, line, request.FieldEvidence)).ToArray();

        var resolved = BoundFieldNames(boundLines).ToArray();
        var availableNotLinked = request.FieldEvidence
            .Where(x => x.EvidenceStatus == LedgerStateR003BindingEvidenceStatus.AvailableNotLineLinked && !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => x.FieldName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var missingFields = RequiredBindingFields
            .Where(field => !resolved.Contains(field, StringComparer.Ordinal))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var commitBlockers = missingFields.Select(field => $"Missing{field}")
            .Concat(AlwaysMissingEconomicBlockers)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var contract = new LedgerStateR003HardenedPreviewContract(
            ContractId: "paper-ledger-preview-r003.v1",
            PreviewOnly: true,
            CommitAllowed: false,
            LedgerMutation: false,
            TradingStateMutation: false,
            MissingEconomicFieldsAllowedForPreviewOnly: true,
            MissingEconomicFieldsBlockCommit: true,
            BoundLineCount: boundLines.Length,
            RequiredBindingFields,
            commitBlockers);

        var gaps = new LedgerStateR003EconomicFieldGapDiagnostics(
            ResolvedFields: resolved,
            AvailableButNotLineLinkedFields: availableNotLinked,
            MissingFields: missingFields,
            CashImpactIncomplete: true,
            CommitBlocked: true);

        var decision = blocked
            ? LedgerStateR003PreviewDecision.PaperLedgerPreviewBlockedMissingCoreBindings
            : resolved.Length == RequiredBindingFields.Length
                ? LedgerStateR003PreviewDecision.PaperLedgerPreviewHardenedWithPmsQubesBindings
                : LedgerStateR003PreviewDecision.PaperLedgerPreviewHardenedWithMissingEconomicFields;

        return new LedgerStateR003BinderResult(
            decision,
            contract,
            boundLines,
            gaps,
            commitBlockers,
            diagnostics,
            IdempotencyReviewHash: Hash(string.Join("|", boundLines.Select(x => x.PreviewHash).OrderBy(x => x, StringComparer.Ordinal))),
            PreviewOnly: true,
            CommitAllowed: false,
            LedgerMutation: false,
            TradingStateMutation: false);
    }

    private static IEnumerable<string> ValidateRequest(LedgerStateR003BinderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId)) yield return "MissingCoreRequestId";
        if (request.R002PreviewLines.Count == 0) yield return "MissingCoreR002PreviewLines";
        if (!request.PreviewOnly) yield return "PreviewOnlyRequiredForbidden";
        if (request.CommitAllowed) yield return "CommitAllowedForbidden";
        if (request.LedgerMutationAllowed) yield return "LedgerMutationForbidden";
        if (request.TradingStateMutationAllowed) yield return "TradingStateMutationForbidden";

        foreach (var line in request.R002PreviewLines)
        {
            if (!line.PreviewOnly || line.CommitAllowed || !line.NoLedgerCommit || line.LedgerMutation || line.TradingStateMutation)
            {
                yield return $"UnsafeR002LineForbidden:{line.LineId}";
            }
        }
    }

    private static LedgerStateR003BoundPaperLedgerPreviewLine BindLine(
        string requestId,
        LedgerStateR002PaperLedgerPreviewLine line,
        IReadOnlyList<LedgerStateR003FieldEvidence> fieldEvidence)
    {
        var bindings = RequiredBindingFields.Select(field => BindField(field, line.ClOrdId, fieldEvidence)).ToArray();

        var values = bindings.ToDictionary(x => x.FieldName, x => x.Value, StringComparer.Ordinal);
        var canonicalTargetCloseUtc = TryParseDateTimeOffset(values["CanonicalTargetCloseUtc"]);
        var missingBlockers = bindings
            .Where(x => x.EvidenceStatus != LedgerStateR003BindingEvidenceStatus.Bound || string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"Missing{x.FieldName}")
            .Concat(AlwaysMissingEconomicBlockers)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var idempotencyKey = Hash(string.Join("|", requestId, line.SourceFillId, line.SourceExecutionReportId, line.ClOrdId, line.Quantity, line.Price));
        var previewHash = Hash(string.Join("|", idempotencyKey, string.Join("|", bindings.Select(x => $"{x.FieldName}:{x.Value ?? "<null>"}"))));
        var auditHash = Hash(string.Join("|", previewHash, string.Join("|", missingBlockers)));

        return new LedgerStateR003BoundPaperLedgerPreviewLine(
            line.LineId,
            line.SourceFillId,
            line.SourceExecutionReportId,
            line.SourceSandboxOrderId,
            line.ClOrdId,
            line.Symbol,
            line.Side,
            line.Quantity,
            line.Price,
            line.SecurityID,
            line.SecurityIDSource,
            PreviewOnly: true,
            CommitAllowed: false,
            LedgerMutation: false,
            TradingStateMutation: false,
            AccountId: values["AccountId"],
            PortfolioId: values["PortfolioId"],
            StrategyId: values["StrategyId"],
            PmsCycleId: values["PmsCycleId"],
            QubesRunId: values["QubesRunId"],
            RiskReviewId: values["RiskReviewId"],
            OperatorApprovalId: values["OperatorApprovalId"],
            CanonicalTargetCloseUtc: canonicalTargetCloseUtc,
            SourceRebalanceIntentId: values["SourceRebalanceIntentId"],
            SourceExecutionIntentId: values["SourceExecutionIntentId"],
            FieldBindings: bindings,
            MissingFieldBlockers: missingBlockers,
            IdempotencyKey: idempotencyKey,
            PreviewHash: previewHash,
            AuditHash: auditHash);
    }

    private static LedgerStateR003FieldBinding BindField(
        string fieldName,
        string clOrdId,
        IReadOnlyList<LedgerStateR003FieldEvidence> fieldEvidence)
    {
        var evidence = fieldEvidence.FirstOrDefault(x =>
            x.FieldName.Equals(fieldName, StringComparison.Ordinal) &&
            x.EvidenceStatus == LedgerStateR003BindingEvidenceStatus.Bound &&
            !string.IsNullOrWhiteSpace(x.Value) &&
            x.AppliesToClOrdIds.Contains(clOrdId, StringComparer.Ordinal));

        if (evidence is not null)
        {
            return new LedgerStateR003FieldBinding(fieldName, evidence.Value, evidence.SourceArtifact, evidence.Confidence, evidence.ContractId, LedgerStateR003BindingEvidenceStatus.Bound);
        }

        var available = fieldEvidence.FirstOrDefault(x =>
            x.FieldName.Equals(fieldName, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(x.Value));

        if (available is not null)
        {
            return new LedgerStateR003FieldBinding(fieldName, null, available.SourceArtifact, available.Confidence, available.ContractId, LedgerStateR003BindingEvidenceStatus.AvailableNotLineLinked);
        }

        return new LedgerStateR003FieldBinding(fieldName, null, "NotFound", "None", "paper-ledger-preview-r003.v1", LedgerStateR003BindingEvidenceStatus.Missing);
    }

    private static IEnumerable<string> BoundFieldNames(IReadOnlyList<LedgerStateR003BoundPaperLedgerPreviewLine> boundLines)
        => boundLines
            .SelectMany(line => line.FieldBindings)
            .Where(x => x.EvidenceStatus == LedgerStateR003BindingEvidenceStatus.Bound && !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => x.FieldName)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal);

    private static DateTimeOffset? TryParseDateTimeOffset(string? value)
        => DateTimeOffset.TryParse(value, out var parsed) ? parsed.ToUniversalTime() : null;

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
