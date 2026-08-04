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
    public const string LiveExecutionRuntimeVersion = "arch7b_one_shot_live_execution_runtime_v1";
    public const string CommandRunnerVersion = "arch7b_one_shot_command_runner_v1";
    public const string LiveExecutionAuthorityVersion = "arch7b_one_shot_live_execution_authority_v1";
    public const string StageEvidenceVersion = "arch7b_one_shot_stage_evidence_v1";
    public const string ProcessEnvironmentAuthorityVersion = "arch7b_one_shot_process_environment_authority_v1";
    public const string CommandResultVersion = "arch7b_one_shot_command_result_v1";
    public const string LivePlanVersion = "arch7b_one_shot_live_plan_v1";
    public const string OperatorAuthorizationVersion = "arch7b_one_shot_operator_authorization_v1";
    public const string ExecutionGapVersion = "arch7b_one_shot_supervisor_execution_gap_v1";
    public const string LiveCandidatePacketVersion = "arch7b_one_shot_live_execution_candidate_v1";

    public const string IntradayRepository = "phu-qqb/IntradayPlatform";
    public const string IntradayBaseCommit = "6eb13a2f1bcf77f71f12efd4f4eef1b71a43c657";
    public const string IntradayBaseTree = "d325308bc0d951468fc037feb55ffdf01c347f57";
    public const string CoreRepository = "phu-qqb/QQ.Production.Core";
    public const string CoreCommit = "be5e969fbeae56cf8de673023a36062a26f52e64";
    public const string CoreTree = "03229eb69a859927bfcd27ff2796fe3051df33c3";
    public const string CoreTrackedInventorySha256 = "532a8774c00717bf67fa0a7e44e8eb1fa6f44a4b4135121dcbebc46985ed408d";
    public const string CoreRepositoryAuthoritySha256 = "d58a6bf3e6b7c62c68d8a3df0924ae8f7bfa3965ea5f5a6553735a785b66be89";

    public const int MaximumSlots = 1;
    public const int MaximumCaptures = 1;
    public const int MaximumRdsReads = 2;
    public const int MaximumRetries = 0;
    public const string ExpectedFinalBlocker = "ARCH7B_WORKING_ORDER_AUTHORITY_MISSING";
    public const string SuccessVerdict = "ARCH7B_ONE_SHOT_LIVE_SUPERVISOR_CORRECTIVE_PR_READY";
    public const string StaticQualificationVerdict = "ARCH7B_ONE_SHOT_STATIC_AUTHORITIES_QUALIFIED";
    public const string LiveRuntimeCandidateVerdict = "ARCH7B_ONE_SHOT_LIVE_EXECUTION_RUNTIME_CORRECTIVE_PR_READY";
    public const string ExecutionGapVerdict = "ARCH7B_ONE_SHOT_GLOBAL_AUTHORITIES_MERGED_LIVE_EXECUTION_MODE_ABSENT";

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
    public const string LiveCommandAuthorityIncomplete = "ARCH7B_LIVE_COMMAND_AUTHORITY_INCOMPLETE";
    public const string LiveAuthorityMissing = "ARCH7B_LIVE_EXECUTION_AUTHORITY_MISSING";
    public const string LiveAuthorityExpired = "ARCH7B_LIVE_EXECUTION_AUTHORITY_EXPIRED";
    public const string OperatorAuthorizationMismatch = "ARCH7B_OPERATOR_AUTHORIZATION_MISMATCH";
    public const string TargetEnvironmentNotTest = "ARCH7B_TARGET_ENVIRONMENT_NOT_TEST";
    public const string NoOrderRequired = "ARCH7B_NO_ORDER_REQUIRED";
    public const string LiveAuthorityCommitMismatch = "ARCH7B_LIVE_AUTHORITY_COMMIT_MISMATCH";
    public const string FreezeAuthorityMismatch = "ARCH7B_FREEZE_AUTHORITY_MISMATCH";
    public const string CommandAuthorityMismatch = "ARCH7B_COMMAND_AUTHORITY_MISMATCH";
    public const string QualificationModeMismatch = "ARCH7B_QUALIFICATION_MODE_MISMATCH";
    public const string AbsolutePathRequired = "ARCH7B_ABSOLUTE_PATH_REQUIRED";
    public const string RunRootNotEmpty = "ARCH7B_RUN_ROOT_NOT_EMPTY";
    public const string RunRootReused = "ARCH7B_RUN_ROOT_REUSED";
    public const string SecretInArgument = "ARCH7B_SECRET_IN_ARGUMENT";
    public const string AmbientPathForbidden = "ARCH7B_AMBIENT_PATH_FORBIDDEN";
    public const string ChildProcessTimeout = "ARCH7B_ONE_SHOT_CHILD_PROCESS_TIMEOUT";
    public const string ChildProcessFailedUncatalogued = "ARCH7B_ONE_SHOT_CHILD_PROCESS_FAILED_UNCATALOGUED";
    public const string ChildOutputInvalid = "ARCH7B_ONE_SHOT_CHILD_OUTPUT_INVALID";
    public const string ChildOutputShaMismatch = "ARCH7B_ONE_SHOT_CHILD_OUTPUT_SHA_MISMATCH";
    public const string ChildEvidenceMissing = "ARCH7B_ONE_SHOT_CHILD_EVIDENCE_MISSING";
    public const string ChildOutputSecretDetected = "ARCH7B_ONE_SHOT_CHILD_OUTPUT_SECRET_DETECTED";
    public const string SecretReadAfterBracket = "ARCH7B_SECRET_READ_AFTER_BRACKET_T0";
    public const string StageEvidenceMissing = "ARCH7B_ONE_SHOT_STAGE_EVIDENCE_MISSING";
    public const string StageOrderViolation = "ARCH7B_ONE_SHOT_STAGE_ORDER_VIOLATION";
    public const string DuplicateArgument = "ARCH7B_DUPLICATE_ARGUMENT_FORBIDDEN";
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
