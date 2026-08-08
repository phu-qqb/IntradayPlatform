namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public static class Arch7bV2Contracts
{
    public const string LivePlanTemplateVersion = "arch7b_one_shot_live_plan_template_v1";
    public const string LiveFactStoreVersion = "arch7b_one_shot_live_fact_store_v1";
    public const string CommandTemplateVersion = "arch7b_one_shot_command_template_v1";
    public const string MaterializedCommandVersion = "arch7b_one_shot_materialized_command_v1";
    public const string MaterializedCommandNonSecretEnvironmentVersion =
        "arch7b_materialized_command_non_secret_environment_v1";
    public const string LongLivedProcessRegistryVersion = "arch7b_one_shot_long_lived_process_registry_v1";
    public const string SecretEnvironmentInjectionVersion = "arch7b_one_shot_secret_environment_injection_v1";
    public const string ChildResultAdapterVersion = "arch7b_one_shot_child_result_adapter_v1";
    public const string OperatorAuthorizationVersion = "arch7b_one_shot_operator_authorization_v2";
    public const string LiveExecutionAuthorityVersion = "arch7b_one_shot_live_execution_authority_v2";
    public const string LiveExecutionRuntimeVersion = "arch7b_one_shot_live_execution_runtime_v2";
    public const string LiveCandidatePacketVersion = "arch7b_one_shot_live_execution_candidate_v2";
    public const string StageValidatorVersion = "arch7b_one_shot_stage_validator_set_v1";
    public const string ArtifactReconciliationVersion = "arch7b_pr53_artifact_identity_reconciliation_v1";
    public const string IntegrationGapForensicVersion = "arch7b_pr53_live_runtime_integration_gap_forensic_v1";
    public const string SecretLifecycleClassification = "CORE_LEASE_PROCESS_OWNS_SECRET_AND_SPAWNS_SECRET_CHILDREN";
    public const string ReadyVerdict = "ARCH7B_PR53_UPDATED_REAL_PLAN_MATERIALIZATION_AND_COMMAND_ADAPTERS_READY";
    public const string AdapterSetQualified = "ARCH7B_REAL_CHILD_OUTPUT_ADAPTER_SET_QUALIFIED";
}

public static class Arch7bV2Blockers
{
    public const string FactInvalid = "ARCH7B_ONE_SHOT_FACT_INVALID";
    public const string FactReplacementForbidden = "ARCH7B_ONE_SHOT_FACT_REPLACEMENT_FORBIDDEN";
    public const string RequiredFactMissing = "ARCH7B_ONE_SHOT_REQUIRED_FACT_MISSING";
    public const string MutableFactForbidden = "ARCH7B_ONE_SHOT_MUTABLE_FACT_FORBIDDEN";
    public const string FactProducerMismatch = "ARCH7B_ONE_SHOT_FACT_PRODUCER_MISMATCH";
    public const string FactStale = "ARCH7B_ONE_SHOT_FACT_STALE";
    public const string CommandTemplateInvalid = "ARCH7B_ONE_SHOT_COMMAND_TEMPLATE_INVALID";
    public const string PlaceholderUnknown = "ARCH7B_ONE_SHOT_PLACEHOLDER_UNKNOWN";
    public const string PlaceholderTypeMismatch = "ARCH7B_ONE_SHOT_PLACEHOLDER_TYPE_MISMATCH";
    public const string AuthorityBindingMismatch = "ARCH7B_ONE_SHOT_AUTHORITY_BINDING_MISMATCH";
    public const string MaterializedCommandAlreadyExists = "ARCH7B_ONE_SHOT_MATERIALIZED_COMMAND_ALREADY_EXISTS";
    public const string DuplicateProcessKey = "ARCH7B_ONE_SHOT_DUPLICATE_PROCESS_KEY";
    public const string LongLivedProcessExited = "ARCH7B_ONE_SHOT_LONG_LIVED_PROCESS_EXITED_PREMATURELY";
    public const string LongLivedProcessStateInvalid = "ARCH7B_ONE_SHOT_LONG_LIVED_PROCESS_STATE_INVALID";
    public const string ProcessSignalForbidden = "ARCH7B_ONE_SHOT_PROCESS_SIGNAL_FORBIDDEN";
    public const string ChildOutputLimitExceeded = "ARCH7B_ONE_SHOT_CHILD_OUTPUT_LIMIT_EXCEEDED";
    public const string ChildOutputSecretValueDetected = "ARCH7B_ONE_SHOT_CHILD_OUTPUT_SECRET_VALUE_DETECTED";
    public const string SecretLeaseMissing = "ARCH7B_ONE_SHOT_SECRET_LEASE_MISSING";
    public const string SecretCommandScopeMismatch = "ARCH7B_ONE_SHOT_SECRET_COMMAND_SCOPE_MISMATCH";
    public const string ChildAdapterMissing = "ARCH7B_ONE_SHOT_CHILD_OUTPUT_ADAPTER_MISSING";
    public const string ChildAdapterContractMismatch = "ARCH7B_ONE_SHOT_CHILD_OUTPUT_ADAPTER_CONTRACT_MISMATCH";
    public const string ChildNativeStatusUnknown = "ARCH7B_ONE_SHOT_CHILD_NATIVE_STATUS_UNKNOWN";
    public const string ChildNativeArtifactCardinality = "ARCH7B_ONE_SHOT_CHILD_NATIVE_ARTIFACT_CARDINALITY_INVALID";
    public const string StageSloMissing = "ARCH7B_ONE_SHOT_STAGE_SLO_MISSING";
    public const string StagePredecessorMissing = "ARCH7B_ONE_SHOT_STAGE_PREDECESSOR_MISSING";
    public const string OperatorAuthorizationMissing = "ARCH7B_OPERATOR_AUTHORIZATION_FILE_MISSING";
    public const string OperatorAuthorizationExpired = "ARCH7B_OPERATOR_AUTHORIZATION_EXPIRED";
    public const string CommandNonSecretEnvironmentAuthorityMissing =
        "ARCH7B_COMMAND_NON_SECRET_ENVIRONMENT_AUTHORITY_MISSING";
    public const string CommandNonSecretEnvironmentVariableForbidden =
        "ARCH7B_COMMAND_NON_SECRET_ENVIRONMENT_VARIABLE_FORBIDDEN";
    public const string CommandDotnetRootAuthorityMismatch =
        "ARCH7B_COMMAND_DOTNET_ROOT_AUTHORITY_MISMATCH";
    public const string CommandDotnetExecutableShaMismatch =
        "ARCH7B_COMMAND_DOTNET_EXECUTABLE_SHA_MISMATCH";
}

public static class Arch7bV2ArgumentSafety
{
    public static bool IsSecretArgumentValue(string value)
    {
        var normalized = value.ToLowerInvariant();
        return normalized.Contains("password=", StringComparison.Ordinal) ||
            normalized.Contains("secret=", StringComparison.Ordinal) ||
            normalized.Contains("token=", StringComparison.Ordinal) ||
            normalized.Contains("connectionstring", StringComparison.Ordinal);
    }
}
