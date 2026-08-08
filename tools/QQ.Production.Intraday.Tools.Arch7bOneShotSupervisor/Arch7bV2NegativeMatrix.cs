namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public sealed record Arch7bV2NegativeCase(
    int Id,
    string Category,
    string Scenario,
    string ExpectedBlocker,
    string ValidatorId);

public static class Arch7bV2NegativeMatrix
{
    public const string ContractVersion = "arch7b_one_shot_extended_negative_matrix_v1";

    public static IReadOnlyList<Arch7bV2NegativeCase> Cases { get; } =
    [
        C(1, "PLAN", "prefilled live plan in freeze", Arch7bV2Blockers.CommandTemplateInvalid, "template-validator"),
        C(2, "PLAN", "selected slot absent", Arch7bV2Blockers.RequiredFactMissing, "fact-store"),
        C(3, "PLAN", "identity created before slot lock", Arch7bV2Blockers.StagePredecessorMissing, "chronology"),
        C(4, "PLAN", "unknown placeholder", Arch7bV2Blockers.PlaceholderUnknown, "materializer"),
        C(5, "PLAN", "fact from wrong producer", Arch7bV2Blockers.FactProducerMismatch, "fact-store"),
        C(6, "AUTHORITY", "runtime inventory mismatch", Arch7bV2Blockers.AuthorityBindingMismatch, "authority-v2"),
        C(7, "AUTHORITY", "Core repository authority mismatch", Arch7bV2Blockers.AuthorityBindingMismatch, "authority-v2"),
        C(8, "AUTHORITY", "static authority mismatch", Arch7bV2Blockers.AuthorityBindingMismatch, "authority-v2"),
        C(9, "AUTHORITY", "adapter set mismatch", Arch7bV2Blockers.AuthorityBindingMismatch, "authority-v2"),
        C(10, "AUTHORITY", "CLI path differs from authority", Arch7bV2Blockers.AuthorityBindingMismatch, "cli-authority-binder"),
        C(11, "AUTHORITY", "operator authorization file mismatch", Arch7bBlockers.OperatorAuthorizationMismatch, "operator-authority-v2"),
        C(12, "SECRET", "declared secret variable value absent", Arch7bV2Blockers.SecretLeaseMissing, "scoped-secret-lease"),
        C(13, "SECRET", "secret injected into wrong child", Arch7bV2Blockers.SecretCommandScopeMismatch, "scoped-secret-lease"),
        C(14, "SECRET", "secret present in argument", Arch7bBlockers.SecretInArgument, "argument-safety"),
        C(15, "SECRET", "exact secret value in stdout", Arch7bV2Blockers.ChildOutputSecretValueDetected, "bounded-stream-reader"),
        C(16, "SECRET", "third RDS read", Arch7bBlockers.RdsReadLimitExceeded, "one-shot-budget"),
        C(17, "SECRET", "secret read after bracket", Arch7bBlockers.SecretReadAfterBracket, "scoped-secret-lease"),
        C(18, "PROCESS", "long-lived process exits prematurely", Arch7bV2Blockers.LongLivedProcessExited, "long-lived-registry"),
        C(19, "PROCESS", "duplicate process key", Arch7bV2Blockers.DuplicateProcessKey, "long-lived-registry"),
        C(20, "PROCESS", "signal not allowlisted", Arch7bV2Blockers.ProcessSignalForbidden, "long-lived-registry"),
        C(21, "PROCESS", "stream output limit exceeded", Arch7bV2Blockers.ChildOutputLimitExceeded, "bounded-stream-reader"),
        C(22, "PROCESS", "process tree persists", Arch7bBlockers.ChildProcessResidual, "terminal-cleanup"),
        C(23, "ADAPTER", "native output without adapter", Arch7bV2Blockers.ChildAdapterMissing, "adapter-registry"),
        C(24, "ADAPTER", "wrong adapter contract", Arch7bV2Blockers.ChildAdapterContractMismatch, "strict-native-adapter"),
        C(25, "ADAPTER", "native artifact missing", Arch7bBlockers.ChildEvidenceMissing, "strict-native-adapter"),
        C(26, "ADAPTER", "native artifact SHA mismatch", Arch7bBlockers.ChildOutputShaMismatch, "strict-native-adapter"),
        C(27, "ADAPTER", "unknown native status", Arch7bV2Blockers.ChildNativeStatusUnknown, "strict-native-adapter"),
        C(28, "CHRONOLOGY", "slot budget recorded before lock", Arch7bBlockers.SlotLimitExceeded, "slot-lock-budget"),
        C(29, "CHRONOLOGY", "capture budget without recorder start", Arch7bBlockers.CaptureLimitExceeded, "capture-budget"),
        C(30, "CHRONOLOGY", "stage has no SLO", Arch7bV2Blockers.StageSloMissing, "stage-entry-validator"),
        C(31, "CHRONOLOGY", "stage predecessor absent", Arch7bV2Blockers.StagePredecessorMissing, "stage-entry-validator")
    ];

    public static string EvidenceSha256 => Arch7bOneShotContracts.Sha256(string.Join('\n',
        ContractVersion, string.Join('|', Cases.Select(value =>
            $"{value.Id}:{value.Category}:{value.Scenario}:{value.ExpectedBlocker}:{value.ValidatorId}"))));

    private static Arch7bV2NegativeCase C(int id, string category, string scenario,
        string blocker, string validator) => new(id, category, scenario, blocker, validator);
}
