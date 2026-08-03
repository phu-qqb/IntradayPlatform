using System.Security.Cryptography;
using System.Text;

namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public static class Arch7bOneShotContracts
{
    public const string OperationalSlotSelectionPolicyVersion = "arch7b_operational_slot_selection_policy_v1";
    public const string GlobalSloRegistryVersion = "arch7b_global_slo_registry_v1";
    public const string CrossRepositoryChronologyVersion = "arch7b_one_shot_cross_repository_chronology_v1";
    public const string TerminalCleanupSupervisorVersion = "arch7b_terminal_cleanup_supervisor_v1";
    public const string LiveSupervisorVersion = "arch7b_one_shot_live_supervisor_v1";
    public const string CoreStaticCommandAuthorityBindingVersion = "arch7b_core_static_command_authority_binding_v1";
    public const string SupervisorEvidenceVersion = "arch7b_one_shot_supervisor_evidence_v1";

    public const string IntradayRepository = "phu-qqb/IntradayPlatform";
    public const string IntradayBaseCommit = "6eb13a2f1bcf77f71f12efd4f4eef1b71a43c657";
    public const string IntradayBaseTree = "d325308bc0d951468fc037feb55ffdf01c347f57";
    public const string CoreRepository = "phu-qqb/QQ.Production.Core";
    public const string CoreCommit = "9ba391dd197d51d1f44dc8c0d86ac1653f36a042";
    public const string CoreTree = "8b9fefc4b39acf19c33ab3611bd155a2cd3f736b";
    public const string CoreTrackedInventorySha256 = "822a056c1976416df8ae54deb26c5e4c5d0b90e632697714fd81661af0a51ed5";
    public const string CoreRepositoryAuthoritySha256 = "f2fd3071a66a323e0862840760bf69acac4c3917a2a5b6e860f38b852d20cf";

    public const int MaximumSlots = 1;
    public const int MaximumCaptures = 1;
    public const int MaximumRdsReads = 2;
    public const int MaximumRetries = 0;
    public const string ExpectedFinalBlocker = "ARCH7B_WORKING_ORDER_AUTHORITY_MISSING";
    public const string SuccessVerdict = "ARCH7B_ONE_SHOT_LIVE_SUPERVISOR_CORRECTIVE_PR_READY";
    public const string StaticQualificationVerdict = "ARCH7B_ONE_SHOT_STATIC_AUTHORITIES_QUALIFIED";

    public static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static bool IsSha256(string? value) => value is not null && value.Length == 64 &&
        value.All(character => char.IsAsciiHexDigit(character) && !char.IsUpper(character));
}

public static class Arch7bBlockers
{
    public const string SlotAlreadyStarted = "ARCH7B_SLOT_ALREADY_STARTED";
    public const string PreparationMarginInsufficient = "ARCH7B_PREPARATION_MARGIN_INSUFFICIENT";
    public const string SlotOutsideOperationalSession = "ARCH7B_SLOT_OUTSIDE_OPERATIONAL_SESSION";
    public const string CalendarAmbiguous = "ARCH7B_CALENDAR_AMBIGUOUS";
    public const string CalendarNotAuthoritative = "ARCH7B_CALENDAR_NOT_AUTHORITATIVE";
    public const string SlotLockAlreadyPublished = "ARCH7B_SLOT_LOCK_ALREADY_PUBLISHED";
    public const string SloContradiction = "ARCH7B_GLOBAL_SLO_REGISTRY_CONTRADICTION";
    public const string CriticalPathSloMissing = "ARCH7B_CRITICAL_PATH_SLO_MISSING";
    public const string SchedulerWakeLatenessExceeded = "ARCH7B_SCHEDULER_WAKE_LATENESS_EXCEEDED";
    public const string CleanupDeadlineExceeded = "ARCH7B_TERMINAL_CLEANUP_DEADLINE_EXCEEDED";
    public const string ChronologyCycle = "ARCH7B_CHRONOLOGY_CYCLE";
    public const string ChronologyUnknownStage = "ARCH7B_CHRONOLOGY_UNKNOWN_STAGE";
    public const string ChronologyEvidenceMissing = "ARCH7B_CHRONOLOGY_EVIDENCE_MISSING";
    public const string RdsRead2AfterBracket = "ARCH7B_RDS_READ_2_AFTER_BRACKET";
    public const string MarketPrearmAfterSlotStart = "ARCH7B_MARKET_PREARM_AFTER_SLOT_START";
    public const string Arch7aBeforeRevisionBinding = "ARCH7B_ARCH7A_BEFORE_REVISION_BINDING";
    public const string ResourceNotRegistered = "ARCH7B_RESOURCE_NOT_REGISTERED";
    public const string ResourceDoubleCleanup = "ARCH7B_RESOURCE_DOUBLE_CLEANUP";
    public const string ChildProcessResidual = "ARCH7B_CHILD_PROCESS_RESIDUAL";
    public const string MarkerResidual = "ARCH7B_MARKER_RESIDUAL";
    public const string PrimaryBlockerMasked = "ARCH7B_PRIMARY_BLOCKER_MASKED";
    public const string CleanupPathOutsideRunRoot = "ARCH7B_CLEANUP_PATH_OUTSIDE_RUN_ROOT";
    public const string CoreParserAuthorityMissing = "ARCH7B_CORE_COMMAND_PARSER_AUTHORITY_MISSING";
    public const string ExecutableShaMismatch = "ARCH7B_EXECUTABLE_SHA256_MISMATCH";
    public const string SupervisorModeUnknown = "ARCH7B_SUPERVISOR_MODE_UNKNOWN";
    public const string CorePlaceholderUnresolved = "ARCH7B_CORE_COMMAND_PLACEHOLDER_UNRESOLVED";
    public const string RetryForbidden = "ARCH7B_ONE_SHOT_RETRY_FORBIDDEN";
    public const string RdsReadLimitExceeded = "ARCH7B_ONE_SHOT_RDS_READ_LIMIT_EXCEEDED";
    public const string CaptureLimitExceeded = "ARCH7B_ONE_SHOT_CAPTURE_LIMIT_EXCEEDED";
    public const string SlotLimitExceeded = "ARCH7B_ONE_SHOT_SLOT_LIMIT_EXCEEDED";
    public const string IdentityReused = "ARCH7B_ONE_SHOT_IDENTITY_REUSED";
    public const string TerminalCleanupIncomplete = "ARCH7B_TERMINAL_CLEANUP_INCOMPLETE";
}

public sealed class Arch7bQualificationException(string blockerCode, string? detail = null)
    : InvalidOperationException(detail is null ? blockerCode : $"{blockerCode}: {detail}")
{
    public string BlockerCode { get; } = blockerCode;
}

public enum Arch7bSupervisorState
{
    Created,
    StaticValidated,
    CalendarReady,
    SlotLocked,
    Prepared,
    Armed,
    LeaseReady,
    BracketCompleted,
    PositionReady,
    MarketPrearmed,
    MarketCompleted,
    PmsCompleted,
    Arch7aCompleted,
    Reported,
    ExpectedFinalBlocker,
    PrimaryFailed,
    Cleaning,
    TerminalSuccess,
    TerminalFailed
}
