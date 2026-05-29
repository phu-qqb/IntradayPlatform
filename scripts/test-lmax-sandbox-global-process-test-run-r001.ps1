param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$BuildScript = Join-Path $PSScriptRoot "build-lmax-sandbox-global-process-test-run-r001.ps1"
$RunnerScript = Join-Path $PSScriptRoot "run-lmax-demo-sandbox-execution-r001.ps1"
$PackageDir = Join-Path $RepoRoot "artifacts\readiness\lmax-sandbox-global-process-test-run-r001"

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Missing JSON artifact: $Path" }
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -ne $Actual) {
        throw "$Message Expected=[$Expected] Actual=[$Actual]"
    }
}

function Assert-GuardSet($Guards) {
    foreach ($name in @(
        "trading_activity",
        "r009_submission",
        "lmax_fix_api_call",
        "broker_api_call",
        "polygon_massive_call",
        "market_data_fetch",
        "broker_fetch",
        "account_data_fetch",
        "production_live_write",
        "production_live_ready",
        "trading_readiness_ready"
    )) {
        Assert-Equal $false $Guards.$name "Guard $name must remain false."
    }
}

function New-ApprovalFile([string]$Path, [string]$RunId, [string]$Venue) {
    $approval = [ordered]@{
        approval_type = "lmax_sandbox_global_process_test_run"
        run_id = $RunId
        environment = "sandbox"
        venue = $Venue
        approved_by = "test_operator"
        approved_at_utc = "2026-05-29T00:00:00Z"
        approved_actions = @(
            "submit_sandbox_orders",
            "capture_sandbox_fills",
            "flatten_sandbox_positions",
            "reconcile_sandbox_trade_level"
        )
        explicit_acknowledgement_no_production = $true
        approval_sha256 = "sha256:test-approval"
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $approval | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function New-SimulationApprovalFile([string]$Path, [string]$RunId) {
    $approval = [ordered]@{
        run_id = $RunId
        environment = "sandbox"
        simulation_only = $true
        approved_by = "test_operator"
        approved_at_utc = "2026-05-29T00:00:00Z"
        approved_actions = @(
            "simulate_fills",
            "simulate_residual_flatten",
            "reconcile_simulated_trade_level",
            "compute_simulated_strategy_pnl"
        )
        no_lmax_call = $true
        no_broker_api_call = $true
        no_live_trading = $true
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $approval | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function New-LmaxDemoApprovalFile([string]$Path, [string]$RunId) {
    $approval = [ordered]@{
        approval_type = "lmax_demo_sandbox_execution_approval"
        run_id = $RunId
        environment = "sandbox"
        venue = "LMAX_DEMO_OR_SANDBOX"
        simulation_already_reconciled = $true
        approved_actions = @(
            "submit_lmax_demo_sandbox_orders",
            "capture_lmax_demo_sandbox_execution_reports",
            "capture_lmax_demo_sandbox_fills",
            "flatten_lmax_demo_sandbox_positions_if_required",
            "reconcile_lmax_demo_sandbox_trade_level",
            "compute_lmax_demo_sandbox_strategy_pnl"
        )
        explicit_acknowledgement_no_production = $true
        approved_by = "test_operator"
        approved_at_utc = "2026-05-29T00:00:00Z"
        approval_sha256 = "sha256:test-lmax-demo-approval"
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $approval | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function New-LmaxDemoSwitchFile([string]$Path, [string]$RunId, [int]$MaxOrderCount, [decimal]$MaxNotionalUsd, [bool]$KillSwitchActive) {
    $switch = [ordered]@{
        run_id = $RunId
        execution_enabled = $true
        environment = "sandbox"
        venue = "LMAX_DEMO_OR_SANDBOX"
        production_live = $false
        max_order_count = $MaxOrderCount
        max_notional_usd = $MaxNotionalUsd
        kill_switch_active = $KillSwitchActive
        created_by = "test_operator"
        created_at_utc = "2026-05-29T00:00:00Z"
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $switch | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function New-LmaxDemoConfigFile(
    [string]$Path,
    [string]$EndpointRef,
    [string]$CredentialPolicy,
    [bool]$IncludeRawSecretValuesPersisted = $true,
    [bool]$IncludeRawSecretsPresent = $false,
    [bool]$RawSecretValuesPersisted = $false,
    [bool]$RawSecretsPresent = $false
) {
    $config = [ordered]@{
        environment = "sandbox"
        endpoint_config_references = @($EndpointRef)
        credential_source_policy = $CredentialPolicy
        credential_policy = $CredentialPolicy
        tls_required = $true
        sequence_policy = "new_sandbox_test_session_or_explicit_reset_only"
        session_log_path = "artifacts/readiness/lmax-sandbox-global-process-test-run-r001/logs/test-session.log"
        order_log_path = "artifacts/readiness/lmax-sandbox-global-process-test-run-r001/logs/test-orders.log"
        execution_report_log_path = "artifacts/readiness/lmax-sandbox-global-process-test-run-r001/logs/test-execution-reports.log"
    }
    if ($IncludeRawSecretValuesPersisted) {
        $config.raw_secret_values_persisted = $RawSecretValuesPersisted
    }
    if ($IncludeRawSecretsPresent) {
        $config.raw_secrets_present = $RawSecretsPresent
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $config | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function New-LmaxDemoActualAdapterBindingFile(
    [string]$Path,
    [string]$RunId,
    [bool]$AdapterEnabled = $true,
    [bool]$ProductionLive = $false,
    [bool]$RawSecretsPersisted = $false,
    [string[]]$RequiredSecretLabels = @(
        "QQ_LMAX_DEMO_FIX_ENDPOINT",
        "QQ_LMAX_DEMO_FIX_SENDER_COMP_ID",
        "QQ_LMAX_DEMO_FIX_TARGET_COMP_ID",
        "QQ_LMAX_DEMO_FIX_USERNAME",
        "QQ_LMAX_DEMO_FIX_PASSWORD"
    )
) {
    $binding = [ordered]@{
        artifact_type = "lmax_demo_actual_adapter_binding"
        run_id = $RunId
        environment = "sandbox"
        venue = "LMAX_DEMO_OR_SANDBOX"
        adapter_mode = "actual_lmax_demo_fix"
        adapter_enabled = $AdapterEnabled
        production_live = $ProductionLive
        production_endpoint_allowed = $false
        raw_secrets_persisted = $RawSecretsPersisted
        credential_source = "environment_or_local_secret_store"
        required_secret_labels = $RequiredSecretLabels
        logs = [ordered]@{
            orders_log = "artifacts/readiness/lmax-sandbox-global-process-test-run-r001/logs/$RunId/orders.log"
            execution_reports_log = "artifacts/readiness/lmax-sandbox-global-process-test-run-r001/logs/$RunId/execution-reports.log"
        }
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $binding | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Set-TestLmaxCredentialEnvironment([string]$Endpoint = "fix-demo-sandbox.test.local:443") {
    [Environment]::SetEnvironmentVariable("QQ_LMAX_DEMO_FIX_ENDPOINT", $Endpoint, "Process")
    [Environment]::SetEnvironmentVariable("QQ_LMAX_DEMO_FIX_SENDER_COMP_ID", "TEST_SENDER", "Process")
    [Environment]::SetEnvironmentVariable("QQ_LMAX_DEMO_FIX_TARGET_COMP_ID", "TEST_TARGET", "Process")
    [Environment]::SetEnvironmentVariable("QQ_LMAX_DEMO_FIX_USERNAME", "TEST_USERNAME", "Process")
    [Environment]::SetEnvironmentVariable("QQ_LMAX_DEMO_FIX_PASSWORD", "TEST_PASSWORD", "Process")
}

function Clear-TestLmaxCredentialEnvironment() {
    foreach ($name in @(
        "QQ_LMAX_DEMO_FIX_ENDPOINT",
        "QQ_LMAX_DEMO_FIX_SENDER_COMP_ID",
        "QQ_LMAX_DEMO_FIX_TARGET_COMP_ID",
        "QQ_LMAX_DEMO_FIX_USERNAME",
        "QQ_LMAX_DEMO_FIX_PASSWORD"
    )) {
        [Environment]::SetEnvironmentVariable($name, $null, "Process")
    }
}

function Write-JsonFile([string]$Path, [object]$Value) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $Value | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $Path -Encoding UTF8
}

$isolatedRoot = Join-Path $RepoRoot "artifacts\readiness\lmax-sandbox-global-process-test-run-r001-test"
$defaultRunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_TEST_DEFAULT_BLOCKED"
& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\default-blocked" -RunId $defaultRunId

$defaultDir = Join-Path $isolatedRoot "default-blocked"
$main = Read-JsonFile (Join-Path $defaultDir "lmax-sandbox-global-process-test-run-r001.json")
$execution = Read-JsonFile (Join-Path $defaultDir "sandbox-execution-result-r001.json")
$orders = Read-JsonFile (Join-Path $defaultDir "lmax-order-manifest-r001.json")
$harness = Read-JsonFile (Join-Path $defaultDir "lmax-sandbox-execution-harness-r001.json")
$coverage = Read-JsonFile (Join-Path $defaultDir "e2e-flow-coverage-after-lmax-sandbox-run-r001.json")

foreach ($artifact in @(
    "lmax-sandbox-global-process-run-manifest-r001.json",
    "lmax-sandbox-market-data-basis-r001.json",
    "qubes-core-weight-handoff-r001.json",
    "drift-and-order-targets-r001.json",
    "lmax-order-manifest-r001.json",
    "execution-algo-plan-r001.json",
    "operator-approval-required-r001.json",
    "lmax-sandbox-execution-harness-r001.json",
    "lmax-demo-actual-adapter-binding-r001.json",
    "sandbox-execution-result-r001.json",
    "sandbox-trade-level-reconciliation-r001.json",
    "sandbox-pnl-r001.json",
    "same-run-broker-evidence-instructions-r001.md",
    "lmax-sandbox-global-process-test-run-r001.json",
    "e2e-flow-coverage-after-lmax-sandbox-run-r001.json"
)) {
    Assert-True (Test-Path -LiteralPath (Join-Path $defaultDir $artifact)) "Required artifact missing in isolated default scenario: $artifact"
}

Assert-True ($main.run_id -like "LMAX_SANDBOX_GLOBAL_TEST_R001_*") "Run ID must use the required prefix."
Assert-Equal "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_OPERATOR_APPROVAL_REQUIRED_R001" $main.status "Default build must block for operator approval."
Assert-Equal "BLOCKED_OPERATOR_APPROVAL_REQUIRED" $execution.status "Default execution result must block before execution."
Assert-Equal 0 @($execution.orders_submitted).Count "Default build must submit no orders."
Assert-Equal $false $main.explicit_execution_switch_enabled "Default build must not have the execution switch enabled."
Assert-Equal $false $main.production_live "Production/live must remain false."
Assert-Equal $false $main.trading_readiness "Trading readiness must remain false."
Assert-GuardSet $main.global_guards

Assert-True (@($orders.orders).Count -gt 0) "Order manifest must contain preview orders."
foreach ($order in @($orders.orders)) {
    if (-not [string]::IsNullOrWhiteSpace([string]$order.security_id)) {
        Assert-Equal "8" $order.security_id_source_tag22 "FIX tag 22 must equal 8 when tag 48 SecurityID is present."
    }
    Assert-Equal $false $order.submit_allowed_without_approval "Orders must not be sendable without approval."
}

Assert-Equal $false $harness.raw_secrets_present "Harness artifacts must not contain raw secrets."
Assert-Equal "BLOCKED_OPERATOR_APPROVAL_REQUIRED" $coverage.flow_coverage.execution_fills "Coverage must block execution/fills by approval in default mode."

$testRoot = $isolatedRoot
$simulationApprovalPath = Join-Path $testRoot "simulation-approval.json"
$runId = "LMAX_SANDBOX_GLOBAL_TEST_R001_TEST_SIMULATED"
New-SimulationApprovalFile $simulationApprovalPath $runId
& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\simulated" -RunId $runId -ExecutionMode Simulated -SimulationApprovalPath $simulationApprovalPath

$simDir = Join-Path $testRoot "simulated"
$simMain = Read-JsonFile (Join-Path $simDir "lmax-sandbox-global-process-test-run-r001.json")
$simExecution = Read-JsonFile (Join-Path $simDir "sandbox-execution-result-r001.json")
$simFills = Read-JsonFile (Join-Path $simDir "sandbox-simulated-fills-r001.json")
$simResidual = Read-JsonFile (Join-Path $simDir "residual-flatten-report-r001.json")
$simRecon = Read-JsonFile (Join-Path $simDir "sandbox-trade-level-reconciliation-r001.json")
$simPnl = Read-JsonFile (Join-Path $simDir "sandbox-pnl-r001.json")

Assert-Equal "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_SIMULATED_RECONCILED_R001" $simMain.status "Explicitly approved simulated run must reconcile."
Assert-Equal "simulated_only" $simExecution.status "Simulated fill mode must complete in local simulation only."
Assert-Equal "SANDBOX_SIMULATED_FILLS_READY_R001" $simFills.status "Simulated fills artifact must be ready."
Assert-True (@($simExecution.fills).Count -gt 0) "Simulated mode must produce fills from local sandbox evidence."
Assert-Equal $true $simMain.residual_zero "Simulated mode must report residual zero."
Assert-Equal "SANDBOX_SIMULATED_RESIDUAL_FLATTEN_READY_R001" $simResidual.status "Residual flatten simulation must run."
Assert-Equal "SANDBOX_SIMULATED_TRADE_LEVEL_RECONCILIATION_READY_R001" $simRecon.status "Simulated mode must produce trade-level reconciliation."
Assert-Equal "SANDBOX_SIMULATED_STRATEGY_PNL_READY_R001" $simPnl.status "Simulated mode must compute strategy PnL."
Assert-Equal "blocked_until_lmax_export" $simMain.same_run_broker_evidence_status "Same-run broker evidence must remain blocked for simulated mode."
Assert-Equal $false $simPnl.broker_statement_pnl_comparison.applicable "Historical LMAX statement PnL must not be used as same-run evidence."
Assert-GuardSet $simMain.global_guards

$noSwitchRunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_TEST_NO_SWITCH"
$noSwitchApproval = Join-Path $testRoot "approval-no-switch.json"
New-ApprovalFile $noSwitchApproval $noSwitchRunId "LMAX_DEMO_OR_SANDBOX"
& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\no-switch" -RunId $noSwitchRunId -ExecutionMode Simulated -ApprovalArtifactPath $noSwitchApproval
$noSwitchExecution = Read-JsonFile (Join-Path $testRoot "no-switch\sandbox-execution-result-r001.json")
Assert-Equal "BLOCKED_OPERATOR_APPROVAL_REQUIRED" $noSwitchExecution.status "Approved run without explicit switch must not execute."
Assert-Equal 0 @($noSwitchExecution.orders_submitted).Count "Approved run without explicit switch must submit no orders."

$badVenueRunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_TEST_BAD_VENUE"
$badVenueApproval = Join-Path $testRoot "approval-bad-venue.json"
New-ApprovalFile $badVenueApproval $badVenueRunId "LMAX_PRODUCTION"
& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\bad-venue" -RunId $badVenueRunId -ExecutionMode LmaxSandbox -ApprovalArtifactPath $badVenueApproval -ExplicitExecutionSwitch
$badApproval = Read-JsonFile (Join-Path $testRoot "bad-venue\operator-approval-required-r001.json")
$badExecution = Read-JsonFile (Join-Path $testRoot "bad-venue\sandbox-execution-result-r001.json")
Assert-True (@($badApproval.approval_issues) -contains "APPROVAL_VENUE_NOT_DEMO_OR_SANDBOX") "Production/live venue approvals must be rejected."
Assert-Equal 0 @($badExecution.orders_submitted).Count "Rejected venue must submit no orders."

$lmaxReadyRunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_TEST_LMAX_READY"
$lmaxReadyApproval = Join-Path $testRoot "lmax-ready-approval.json"
$lmaxReadySwitch = Join-Path $testRoot "lmax-ready-switch.json"
$lmaxReadyConfig = Join-Path $testRoot "lmax-ready-config.json"
New-LmaxDemoApprovalFile $lmaxReadyApproval $lmaxReadyRunId
New-LmaxDemoSwitchFile $lmaxReadySwitch $lmaxReadyRunId 9 6000000 $false
New-LmaxDemoConfigFile $lmaxReadyConfig "LMAX_DEMO_OR_SANDBOX_FIX_ENDPOINT_CONFIG_LABEL" "demo_or_sandbox_only"
& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
$lmaxReadyDir = Join-Path $testRoot "lmax-ready"
$lmaxReadyMain = Read-JsonFile (Join-Path $lmaxReadyDir "lmax-sandbox-global-process-test-run-r001.json")
$lmaxReadyExecution = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
Assert-Equal "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_EXECUTION_APPROVED_READY_R001" $lmaxReadyMain.status "Approval, switch, and demo config must reach approved-ready mode."
Assert-Equal "APPROVED_READY_NOT_EXECUTED_BY_BUILD_SCRIPT" $lmaxReadyExecution.status "Tests must not execute LMAX demo orders without integration flag."
Assert-Equal 0 $lmaxReadyExecution.orders_submitted_count "Approved-ready mode must not submit orders in test mode."
Assert-Equal $false $lmaxReadyExecution.lmax_fix_api_call "Approved-ready test mode must not call LMAX."
Assert-GuardSet $lmaxReadyMain.global_guards

& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId
$runnerNoFlag = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_EXPLICIT_FLAG_MISSING_R001" $runnerNoFlag.status "Runner missing explicit execution flag must refuse to run."
Assert-Equal 0 $runnerNoFlag.orders_submitted_count "Runner missing explicit flag must submit no orders."

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
$missingRunnerApprovalPath = Join-Path $testRoot "runner-missing-approval-status.json"
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ApprovalPath $missingRunnerApprovalPath -ExecuteLmaxDemoSandboxOrders
$runnerMissingApproval = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_OPERATOR_APPROVAL_REQUIRED_R001" $runnerMissingApproval.status "Runner missing approval must refuse to run."

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
$missingRunnerSwitchPath = Join-Path $testRoot "runner-missing-switch-status.json"
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionSwitchPath $missingRunnerSwitchPath -ExecuteLmaxDemoSandboxOrders
$runnerMissingSwitch = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_EXECUTION_SWITCH_DISABLED_R001" $runnerMissingSwitch.status "Runner missing switch must refuse to run."

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
$missingRunnerConfigPath = Join-Path $testRoot "runner-missing-config-status.json"
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ConfigValidationPath $missingRunnerConfigPath -ExecuteLmaxDemoSandboxOrders
$runnerMissingConfig = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_LMAX_SANDBOX_CONFIG_MISSING_R001" $runnerMissingConfig.status "Runner missing valid config must refuse to run."

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
$missingAdapterBindingPath = Join-Path $testRoot "runner-missing-adapter-binding.json"
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -AdapterBindingPath $missingAdapterBindingPath -ExecuteLmaxDemoSandboxOrders
$runnerMissingAdapter = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
$runnerMissingAdapterStatus = Read-JsonFile (Join-Path $lmaxReadyDir "lmax-demo-actual-adapter-binding-status-r001.json")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_ACTUAL_ADAPTER_BINDING_MISSING_R001" $runnerMissingAdapter.status "Runner missing adapter binding must refuse to run."
Assert-Equal $false $runnerMissingAdapterStatus.adapter_binding_present "Missing adapter binding status must be explicit."

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
$disabledAdapterBindingPath = Join-Path $testRoot "runner-disabled-adapter-binding.json"
New-LmaxDemoActualAdapterBindingFile $disabledAdapterBindingPath $lmaxReadyRunId $false
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -AdapterBindingPath $disabledAdapterBindingPath -ExecuteLmaxDemoSandboxOrders
$runnerDisabledAdapter = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_ACTUAL_ADAPTER_BINDING_MISSING_R001" $runnerDisabledAdapter.status "Disabled adapter binding must block."

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
$productionAdapterBindingPath = Join-Path $testRoot "runner-production-adapter-binding.json"
New-LmaxDemoActualAdapterBindingFile $productionAdapterBindingPath $lmaxReadyRunId $true $true
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -AdapterBindingPath $productionAdapterBindingPath -ExecuteLmaxDemoSandboxOrders
$runnerProductionAdapter = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_FORBIDDEN_PRODUCTION_OR_LIVE_ACTIVITY_R001" $runnerProductionAdapter.status "Production/live adapter binding must block."

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
$rawSecretsAdapterBindingPath = Join-Path $testRoot "runner-raw-secrets-adapter-binding.json"
New-LmaxDemoActualAdapterBindingFile $rawSecretsAdapterBindingPath $lmaxReadyRunId $true $false $true
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -AdapterBindingPath $rawSecretsAdapterBindingPath -ExecuteLmaxDemoSandboxOrders
$runnerRawSecretsAdapter = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_RAW_SECRET_FLAG_DETECTED_R001" $runnerRawSecretsAdapter.status "Raw secret adapter binding must block."

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
$missingLabelAdapterBindingPath = Join-Path $testRoot "runner-missing-label-adapter-binding.json"
New-LmaxDemoActualAdapterBindingFile $missingLabelAdapterBindingPath $lmaxReadyRunId $true $false $false @("QQ_LMAX_DEMO_FIX_ENDPOINT")
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -AdapterBindingPath $missingLabelAdapterBindingPath -ExecuteLmaxDemoSandboxOrders
$runnerMissingLabelAdapter = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_CREDENTIAL_SOURCE_MISSING_R001" $runnerMissingLabelAdapter.status "Adapter binding missing required secret labels must block."

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
Clear-TestLmaxCredentialEnvironment
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecuteLmaxDemoSandboxOrders
$runnerMissingCreds = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_CREDENTIAL_SOURCE_MISSING_R001" $runnerMissingCreds.status "Missing credential labels must block actual adapter."

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
Set-TestLmaxCredentialEnvironment "fix-production.example:443"
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecuteLmaxDemoSandboxOrders
$runnerProductionEndpoint = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_FORBIDDEN_PRODUCTION_OR_LIVE_ACTIVITY_R001" $runnerProductionEndpoint.status "Production-looking endpoint must block actual adapter."

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
Set-TestLmaxCredentialEnvironment
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecuteLmaxDemoSandboxOrders
$runnerFixMissing = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
$runnerFixMissingStatus = Read-JsonFile (Join-Path $lmaxReadyDir "lmax-demo-actual-adapter-binding-status-r001.json")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_FIX_CLIENT_IMPLEMENTATION_MISSING_R001" $runnerFixMissing.status "Adapter binding with credentials but no FIX implementation must block clearly."
Assert-Equal $true $runnerFixMissingStatus.adapter_binding_present "Adapter binding must be represented in status."
Assert-Equal $true $runnerFixMissingStatus.adapter_enabled "Adapter enabled must be represented in status."
Assert-Equal "actual_lmax_demo_fix" $runnerFixMissingStatus.adapter_mode "Adapter mode must be represented in status."
Assert-Equal $true $runnerFixMissingStatus.fix_client_implementation_present "FIX client implementation must be bound, with actual use gated by UseActualLmaxFixClient."
Clear-TestLmaxCredentialEnvironment

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
$runnerKillSwitchPath = Join-Path $testRoot "runner-kill-switch-status.json"
Write-JsonFile $runnerKillSwitchPath ([ordered]@{
    status = "LMAX_DEMO_EXECUTION_SWITCH_ACCEPTED_R001"
    kill_switch_active = $true
    max_order_count = 9
    max_notional_usd = 6000000
})
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionSwitchPath $runnerKillSwitchPath -ExecuteLmaxDemoSandboxOrders
$runnerKill = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_KILL_SWITCH_ACTIVE_R001" $runnerKill.status "Runner must block kill switch active."

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
$runnerLowCountSwitchPath = Join-Path $testRoot "runner-low-count-switch-status.json"
Write-JsonFile $runnerLowCountSwitchPath ([ordered]@{
    status = "LMAX_DEMO_EXECUTION_SWITCH_ACCEPTED_R001"
    kill_switch_active = $false
    max_order_count = 8
    max_notional_usd = 6000000
})
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionSwitchPath $runnerLowCountSwitchPath -ExecuteLmaxDemoSandboxOrders
$runnerLowCount = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_RISK_LIMITS_FAILED_R001" $runnerLowCount.status "Runner must block max order count failure."

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
$runnerLowNotionalSwitchPath = Join-Path $testRoot "runner-low-notional-switch-status.json"
Write-JsonFile $runnerLowNotionalSwitchPath ([ordered]@{
    status = "LMAX_DEMO_EXECUTION_SWITCH_ACCEPTED_R001"
    kill_switch_active = $false
    max_order_count = 9
    max_notional_usd = 0
})
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionSwitchPath $runnerLowNotionalSwitchPath -ExecuteLmaxDemoSandboxOrders
$runnerLowNotional = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_RISK_LIMITS_FAILED_R001" $runnerLowNotional.status "Runner must block max notional failure."

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecuteLmaxDemoSandboxOrders -UseMockLmaxAdapter
$runnerMockMain = Read-JsonFile (Join-Path $lmaxReadyDir "lmax-sandbox-global-process-test-run-r001.json")
$runnerMockExecution = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
$runnerMockRecon = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-trade-level-reconciliation-r001.json")
$runnerMockPnl = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-pnl-r001.json")
Assert-Equal "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_RECONCILED_R001" $runnerMockMain.status "Mocked runner must reconcile."
Assert-Equal 9 $runnerMockExecution.orders_submitted_count "Mocked runner must submit current 9 preview orders to mock adapter."
Assert-Equal 18 $runnerMockExecution.execution_reports_count "Mocked runner must capture ack and fill reports."
Assert-Equal 9 $runnerMockExecution.fills_count "Mocked runner must capture fills."
Assert-Equal $true $runnerMockExecution.residual_zero "Mocked runner must residual-zero."
Assert-Equal $false $runnerMockExecution.lmax_fix_api_call "Mocked runner must not call LMAX."
Assert-Equal "LMAX_DEMO_SANDBOX_TRADE_LEVEL_RECONCILIATION_READY_R001" $runnerMockRecon.status "Mocked runner must update reconciliation."
Assert-Equal "LMAX_DEMO_SANDBOX_STRATEGY_PNL_READY_R001" $runnerMockPnl.status "Mocked runner must update PnL."
Assert-Equal $false $runnerMockPnl.broker_statement_pnl_comparison.applicable "Mocked runner must not use historical LMAX statement PnL."
Assert-True (Test-Path -LiteralPath (Join-Path $lmaxReadyDir "logs\$lmaxReadyRunId\attempt-001\orders.log")) "Mocked runner must write attempt-scoped orders.log."
Assert-True (Test-Path -LiteralPath (Join-Path $lmaxReadyDir "logs\$lmaxReadyRunId\attempt-001\execution-reports.log")) "Mocked runner must write attempt-scoped execution-reports.log."

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
Set-TestLmaxCredentialEnvironment
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecuteLmaxDemoSandboxOrders -UseActualLmaxFixClient -UseMockFixServer
$runnerMockFixMain = Read-JsonFile (Join-Path $lmaxReadyDir "lmax-sandbox-global-process-test-run-r001.json")
$runnerMockFixExecution = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
$runnerMockFixRecon = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-trade-level-reconciliation-r001.json")
$runnerMockFixPnl = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-pnl-r001.json")
$runnerMockFixStatus = Read-JsonFile (Join-Path $lmaxReadyDir "lmax-demo-actual-adapter-binding-status-r001.json")
$runnerMockFixClOrdIdMap = Read-JsonFile (Join-Path $lmaxReadyDir "lmax-demo-clordid-map-r001.json")
$mockFixOrdersLog = Get-Content -Raw -LiteralPath (Join-Path $lmaxReadyDir "logs\$lmaxReadyRunId\attempt-001\orders.log")
$mockFixReportsLog = Get-Content -Raw -LiteralPath (Join-Path $lmaxReadyDir "logs\$lmaxReadyRunId\attempt-001\execution-reports.log")
Assert-Equal "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_RECONCILED_R001" $runnerMockFixMain.status "Mock FIX-server path must reconcile through FIX framing."
Assert-Equal "mock_fix_server" $runnerMockFixExecution.execution_mode_detail "Mock FIX-server path must use FIX adapter path."
Assert-Equal 9 $runnerMockFixExecution.orders_submitted_count "Mock FIX-server path must submit current 9 preview orders."
Assert-Equal 18 $runnerMockFixExecution.execution_reports_count "Mock FIX-server path must capture ack and fill FIX reports."
Assert-Equal 9 $runnerMockFixExecution.fills_count "Mock FIX-server path must parse fills."
Assert-Equal $true $runnerMockFixExecution.residual_zero "Mock FIX-server path must residual-zero."
Assert-Equal $false $runnerMockFixExecution.lmax_fix_api_call "Mock FIX-server path must not call LMAX."
Assert-Equal "LMAX_DEMO_SANDBOX_TRADE_LEVEL_RECONCILIATION_READY_R001" $runnerMockFixRecon.status "Mock FIX-server path must update reconciliation."
Assert-Equal "LMAX_DEMO_SANDBOX_STRATEGY_PNL_READY_R001" $runnerMockFixPnl.status "Mock FIX-server path must update PnL."
Assert-Equal $false $runnerMockFixPnl.broker_statement_pnl_comparison.applicable "Mock FIX-server path must not use historical LMAX statement PnL."
Assert-Equal $true $runnerMockFixStatus.fix_client_implementation_present "Adapter status must record the FIX implementation binding."
Assert-Equal 20 $runnerMockFixClOrdIdMap.policy.max_external_cl_ord_id_length "ClOrdID policy must cap external IDs at 20 characters."
Assert-Equal $true $runnerMockFixClOrdIdMap.policy.deterministic "ClOrdID policy must be deterministic."
Assert-Equal 9 @($runnerMockFixClOrdIdMap.mappings).Count "ClOrdID mapping artifact must include one mapping per order."
$externalIds = @($runnerMockFixClOrdIdMap.mappings | ForEach-Object { [string]$_.external_cl_ord_id })
Assert-Equal 9 @($externalIds | Select-Object -Unique).Count "External ClOrdIDs must be unique within the run."
foreach ($externalId in $externalIds) {
    Assert-True ($externalId.Length -le 20) "External ClOrdID must be <= 20 characters."
}
$firstMapping = @($runnerMockFixClOrdIdMap.mappings)[0]
Assert-True ([string]$firstMapping.internal_order_id -match [regex]::Escape($lmaxReadyRunId)) "ClOrdID mapping must preserve long internal order id."
Assert-Equal "LXR1A001O001" $firstMapping.external_cl_ord_id "First external ClOrdID must be deterministic and attempt-scoped."
Assert-Equal $firstMapping.internal_order_id @($runnerMockFixExecution.orders_submitted)[0].internal_order_id "Execution submitted order must retain internal order id."
Assert-Equal $firstMapping.external_cl_ord_id @($runnerMockFixExecution.orders_submitted)[0].external_cl_ord_id "Execution submitted order must retain external ClOrdID."
Assert-Equal $firstMapping.internal_order_id @($runnerMockFixExecution.fills)[0].internal_order_id "Parsed fills must map external ClOrdID back to internal order id."
Assert-Equal $firstMapping.external_cl_ord_id @($runnerMockFixExecution.fills)[0].external_cl_ord_id "Parsed fills must preserve external ClOrdID."
Assert-True ($mockFixOrdersLog -match "35=A" -and $mockFixOrdersLog -match "35=D" -and $mockFixOrdersLog -match "9=\d+" -and $mockFixOrdersLog -match "10=\d{3}") "Mock FIX-server path must log sanitized FIX logon and NewOrderSingle messages."
Assert-True ($mockFixReportsLog -match "35=A" -and $mockFixReportsLog -match "35=8" -and $mockFixReportsLog -match "150=F" -and $mockFixReportsLog -match "10=\d{3}") "Mock FIX-server path must log sanitized FIX logon and ExecutionReport messages."
Assert-True ($mockFixOrdersLog -notmatch "TEST_PASSWORD" -and $mockFixOrdersLog -notmatch "554=TEST_PASSWORD") "Mock FIX-server path must not log raw password."
Assert-True ($mockFixOrdersLog -notmatch "(^|\|)21=") "LMAX NewOrderSingle must not include tag 21 HandlInst."
Assert-True ($mockFixOrdersLog -match "(^|\|)48=" -and $mockFixOrdersLog -match "(^|\|)22=8(\||$)") "LMAX NewOrderSingle must preserve SecurityID tag 48 and tag 22=8."
Assert-True ($mockFixOrdersLog -match "(^|\|)11=LXR1A001O001(\||$)") "Outbound FIX tag 11 must use short attempt-scoped external ClOrdID."
Assert-True ($mockFixOrdersLog -notmatch [regex]::Escape([string]$firstMapping.internal_order_id)) "Outbound FIX tag 11 must not use long internal order id."
Clear-TestLmaxCredentialEnvironment

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
Set-TestLmaxCredentialEnvironment
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecuteLmaxDemoSandboxOrders -UseActualLmaxFixClient -UseMockFixServer -InjectLongClOrdIdForTest
$runnerLongClOrdId = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_CLORDID_TOO_LONG_R001" $runnerLongClOrdId.status "Generated tag 11 longer than 20 must block before order send."
Assert-Equal 0 $runnerLongClOrdId.orders_submitted_count "Too-long ClOrdID must block before submitted order accounting."
Clear-TestLmaxCredentialEnvironment

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
Set-TestLmaxCredentialEnvironment
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecuteLmaxDemoSandboxOrders -UseActualLmaxFixClient -UseMockFixServer -InjectForbiddenTag21ForTest
$runnerForbiddenTag21 = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_FORBIDDEN_TAG_21_R001" $runnerForbiddenTag21.status "Generated tag 21 must block before order send."
Assert-Equal 0 $runnerForbiddenTag21.orders_submitted_count "Forbidden tag 21 must block before submitted order accounting."
Clear-TestLmaxCredentialEnvironment

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
Set-TestLmaxCredentialEnvironment
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecuteLmaxDemoSandboxOrders -UseActualLmaxFixClient -UseMockFixServer -MockFixServerRejectClOrdIdLength
$runnerClOrdIdRejectMain = Read-JsonFile (Join-Path $lmaxReadyDir "lmax-sandbox-global-process-test-run-r001.json")
$runnerClOrdIdRejectExecution = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
$clOrdIdRejectReportsLog = Get-Content -Raw -LiteralPath (Join-Path $lmaxReadyDir "logs\$lmaxReadyRunId\attempt-001\execution-reports.log")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_CLORDID_TOO_LONG_R001" $runnerClOrdIdRejectMain.status "Session rejects for tag 11 length must classify as ClOrdID too long."
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_CLORDID_TOO_LONG_R001" $runnerClOrdIdRejectExecution.status "Execution result must classify tag 11 length rejects."
Assert-Equal 9 $runnerClOrdIdRejectExecution.orders_submitted_count "ClOrdID reject scenario must preserve submitted order count."
Assert-Equal 9 $runnerClOrdIdRejectExecution.session_reject_count "ClOrdID reject scenario must count session rejects."
Assert-Equal "11" $runnerClOrdIdRejectExecution.rejected_tag "ClOrdID reject scenario must identify rejected tag."
Assert-Equal "CLORDID_TOO_LONG" $runnerClOrdIdRejectExecution.primary_failure "ClOrdID reject scenario must identify primary failure."
Assert-Equal @($runnerMockFixClOrdIdMap.mappings)[0].internal_order_id @($runnerClOrdIdRejectExecution.rejects)[0].internal_order_id "ClOrdID rejects must map external tag 11 back to internal order id."
Assert-Equal 0 $runnerClOrdIdRejectExecution.fills_count "ClOrdID reject scenario must have no fills."
Assert-Equal $false $runnerClOrdIdRejectExecution.residual_zero "ClOrdID reject scenario must not be residual zero."
Assert-True ($clOrdIdRejectReportsLog -match "35=3" -and $clOrdIdRejectReportsLog -match "371=11" -and $clOrdIdRejectReportsLog -match "length less than or equal to 20") "Mock FIX server must emit tag 11 length session rejects."
Clear-TestLmaxCredentialEnvironment

& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $lmaxReadyApproval -LmaxDemoExecutionSwitchPath $lmaxReadySwitch -LmaxDemoConfigPath $lmaxReadyConfig
Set-TestLmaxCredentialEnvironment
& $RunnerScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-ready" -RunId $lmaxReadyRunId -ExecuteLmaxDemoSandboxOrders -UseActualLmaxFixClient -UseMockFixServer -MockFixServerRejectTag21
$runnerTag21RejectMain = Read-JsonFile (Join-Path $lmaxReadyDir "lmax-sandbox-global-process-test-run-r001.json")
$runnerTag21RejectExecution = Read-JsonFile (Join-Path $lmaxReadyDir "sandbox-execution-result-r001.json")
$tag21RejectOrdersLog = Get-Content -Raw -LiteralPath (Join-Path $lmaxReadyDir "logs\$lmaxReadyRunId\attempt-001\orders.log")
$tag21RejectReportsLog = Get-Content -Raw -LiteralPath (Join-Path $lmaxReadyDir "logs\$lmaxReadyRunId\attempt-001\execution-reports.log")
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_ORDER_REJECTED_UNKNOWN_TAG_R001" $runnerTag21RejectMain.status "Session rejects for tag 21 must be classified as order-level unknown tag rejects."
Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_ORDER_REJECTED_UNKNOWN_TAG_R001" $runnerTag21RejectExecution.status "Execution result must classify tag 21 session rejects."
Assert-Equal 9 $runnerTag21RejectExecution.orders_submitted_count "Tag 21 reject scenario must preserve submitted order count."
Assert-Equal 9 $runnerTag21RejectExecution.session_reject_count "Tag 21 reject scenario must count session rejects."
Assert-Equal "21" $runnerTag21RejectExecution.rejected_tag "Tag 21 reject scenario must identify rejected tag."
Assert-Equal "FORBIDDEN_TAG_21" $runnerTag21RejectExecution.primary_failure "Tag 21 reject scenario must identify primary failure."
Assert-Equal 0 $runnerTag21RejectExecution.fills_count "Tag 21 reject scenario must have no fills."
Assert-Equal $false $runnerTag21RejectExecution.residual_zero "Tag 21 reject scenario must not be residual zero."
Assert-Equal $true $runnerTag21RejectExecution.logon_success "Tag 21 session rejects must be distinguished from logon failure."
Assert-True ($tag21RejectOrdersLog -notmatch "(^|\|)21=") "Tag 21 reject classification must not reintroduce tag 21 in generated orders."
Assert-True ($tag21RejectReportsLog -match "35=3" -and $tag21RejectReportsLog -match "371=21" -and $tag21RejectReportsLog -match "372=D") "Mock FIX server must emit session rejects for tag 21."
Clear-TestLmaxCredentialEnvironment

$missingRawFlagsRunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_TEST_LMAX_MISSING_RAW_FLAGS"
$missingRawFlagsApproval = Join-Path $testRoot "lmax-missing-raw-flags-approval.json"
$missingRawFlagsSwitch = Join-Path $testRoot "lmax-missing-raw-flags-switch.json"
$missingRawFlagsConfig = Join-Path $testRoot "lmax-missing-raw-flags-config.json"
New-LmaxDemoApprovalFile $missingRawFlagsApproval $missingRawFlagsRunId
New-LmaxDemoSwitchFile $missingRawFlagsSwitch $missingRawFlagsRunId 9 6000000 $false
New-LmaxDemoConfigFile $missingRawFlagsConfig "LMAX_DEMO_OR_SANDBOX_FIX_ENDPOINT_CONFIG_LABEL" "demo_or_sandbox_only" $false $false
& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-missing-raw-flags" -RunId $missingRawFlagsRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $missingRawFlagsApproval -LmaxDemoExecutionSwitchPath $missingRawFlagsSwitch -LmaxDemoConfigPath $missingRawFlagsConfig
$missingRawFlagsMain = Read-JsonFile (Join-Path $testRoot "lmax-missing-raw-flags\lmax-sandbox-global-process-test-run-r001.json")
$missingRawFlagsValidation = Read-JsonFile (Join-Path $testRoot "lmax-missing-raw-flags\lmax-demo-execution-config-validation-r001.json")
Assert-Equal "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_EXECUTION_APPROVED_READY_R001" $missingRawFlagsMain.status "Missing raw secret flags must default to false and not crash."
Assert-Equal $false $missingRawFlagsValidation.raw_secret_values_persisted "Missing raw_secret_values_persisted must emit false."
Assert-Equal $false $missingRawFlagsValidation.raw_secrets_present "Missing raw_secrets_present must emit false."
Assert-Equal $true $missingRawFlagsValidation.no_raw_secrets_in_artifacts "Missing raw secret flags must be treated as no raw secrets."

$rawSecretsPresentRunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_TEST_LMAX_RAW_SECRETS_PRESENT"
$rawSecretsPresentApproval = Join-Path $testRoot "lmax-raw-secrets-present-approval.json"
$rawSecretsPresentSwitch = Join-Path $testRoot "lmax-raw-secrets-present-switch.json"
$rawSecretsPresentConfig = Join-Path $testRoot "lmax-raw-secrets-present-config.json"
New-LmaxDemoApprovalFile $rawSecretsPresentApproval $rawSecretsPresentRunId
New-LmaxDemoSwitchFile $rawSecretsPresentSwitch $rawSecretsPresentRunId 9 6000000 $false
New-LmaxDemoConfigFile $rawSecretsPresentConfig "LMAX_DEMO_OR_SANDBOX_FIX_ENDPOINT_CONFIG_LABEL" "demo_or_sandbox_only" $true $true $false $true
& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-raw-secrets-present" -RunId $rawSecretsPresentRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $rawSecretsPresentApproval -LmaxDemoExecutionSwitchPath $rawSecretsPresentSwitch -LmaxDemoConfigPath $rawSecretsPresentConfig
$rawSecretsPresentMain = Read-JsonFile (Join-Path $testRoot "lmax-raw-secrets-present\lmax-sandbox-global-process-test-run-r001.json")
$rawSecretsPresentValidation = Read-JsonFile (Join-Path $testRoot "lmax-raw-secrets-present\lmax-demo-execution-config-validation-r001.json")
Assert-Equal "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_LMAX_SANDBOX_CONFIG_MISSING_R001" $rawSecretsPresentMain.status "raw_secrets_present true must block config validation."
Assert-Equal $true $rawSecretsPresentValidation.raw_secrets_present "Config validation artifact must emit raw_secrets_present true."
Assert-Equal $false $rawSecretsPresentValidation.no_raw_secrets_in_artifacts "Raw secrets present must fail no-raw-secrets policy."

$rawSecretValuesRunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_TEST_LMAX_RAW_SECRET_VALUES"
$rawSecretValuesApproval = Join-Path $testRoot "lmax-raw-secret-values-approval.json"
$rawSecretValuesSwitch = Join-Path $testRoot "lmax-raw-secret-values-switch.json"
$rawSecretValuesConfig = Join-Path $testRoot "lmax-raw-secret-values-config.json"
New-LmaxDemoApprovalFile $rawSecretValuesApproval $rawSecretValuesRunId
New-LmaxDemoSwitchFile $rawSecretValuesSwitch $rawSecretValuesRunId 9 6000000 $false
New-LmaxDemoConfigFile $rawSecretValuesConfig "LMAX_DEMO_OR_SANDBOX_FIX_ENDPOINT_CONFIG_LABEL" "demo_or_sandbox_only" $true $true $true $false
& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-raw-secret-values" -RunId $rawSecretValuesRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $rawSecretValuesApproval -LmaxDemoExecutionSwitchPath $rawSecretValuesSwitch -LmaxDemoConfigPath $rawSecretValuesConfig
$rawSecretValuesMain = Read-JsonFile (Join-Path $testRoot "lmax-raw-secret-values\lmax-sandbox-global-process-test-run-r001.json")
$rawSecretValuesValidation = Read-JsonFile (Join-Path $testRoot "lmax-raw-secret-values\lmax-demo-execution-config-validation-r001.json")
Assert-Equal "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_LMAX_SANDBOX_CONFIG_MISSING_R001" $rawSecretValuesMain.status "raw_secret_values_persisted true must block config validation."
Assert-Equal $true $rawSecretValuesValidation.raw_secret_values_persisted "Config validation artifact must emit raw_secret_values_persisted true."
Assert-Equal $false $rawSecretValuesValidation.no_raw_secrets_in_artifacts "Persisted raw secret values must fail no-raw-secrets policy."

$missingApprovalRunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_TEST_LMAX_MISSING_APPROVAL"
$missingSwitch = Join-Path $testRoot "lmax-missing-approval-switch.json"
New-LmaxDemoSwitchFile $missingSwitch $missingApprovalRunId 9 6000000 $false
& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-missing-approval" -RunId $missingApprovalRunId -ExecutionMode LmaxSandbox -LmaxDemoExecutionSwitchPath $missingSwitch -LmaxDemoConfigPath $lmaxReadyConfig
$missingApprovalMain = Read-JsonFile (Join-Path $testRoot "lmax-missing-approval\lmax-sandbox-global-process-test-run-r001.json")
Assert-Equal "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_OPERATOR_APPROVAL_REQUIRED_R001" $missingApprovalMain.status "Missing LMAX demo approval must block actual execution."

$missingSwitchRunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_TEST_LMAX_MISSING_SWITCH"
$missingSwitchApproval = Join-Path $testRoot "lmax-missing-switch-approval.json"
New-LmaxDemoApprovalFile $missingSwitchApproval $missingSwitchRunId
& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-missing-switch" -RunId $missingSwitchRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $missingSwitchApproval -LmaxDemoConfigPath $lmaxReadyConfig
$missingSwitchMain = Read-JsonFile (Join-Path $testRoot "lmax-missing-switch\lmax-sandbox-global-process-test-run-r001.json")
Assert-Equal "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_EXECUTION_SWITCH_DISABLED_R001" $missingSwitchMain.status "Missing LMAX demo execution switch must block actual execution."

$killRunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_TEST_LMAX_KILL"
$killApproval = Join-Path $testRoot "lmax-kill-approval.json"
$killSwitch = Join-Path $testRoot "lmax-kill-switch.json"
New-LmaxDemoApprovalFile $killApproval $killRunId
New-LmaxDemoSwitchFile $killSwitch $killRunId 9 6000000 $true
& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-kill" -RunId $killRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $killApproval -LmaxDemoExecutionSwitchPath $killSwitch -LmaxDemoConfigPath $lmaxReadyConfig
$killMain = Read-JsonFile (Join-Path $testRoot "lmax-kill\lmax-sandbox-global-process-test-run-r001.json")
Assert-Equal "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_KILL_SWITCH_ACTIVE_R001" $killMain.status "Kill switch must block actual execution."

$prodEndpointRunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_TEST_LMAX_PROD_ENDPOINT"
$prodEndpointApproval = Join-Path $testRoot "lmax-prod-endpoint-approval.json"
$prodEndpointSwitch = Join-Path $testRoot "lmax-prod-endpoint-switch.json"
$prodEndpointConfig = Join-Path $testRoot "lmax-prod-endpoint-config.json"
New-LmaxDemoApprovalFile $prodEndpointApproval $prodEndpointRunId
New-LmaxDemoSwitchFile $prodEndpointSwitch $prodEndpointRunId 9 6000000 $false
New-LmaxDemoConfigFile $prodEndpointConfig "LMAX_PRODUCTION_FIX_ENDPOINT_CONFIG_LABEL" "demo_or_sandbox_only"
& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-prod-endpoint" -RunId $prodEndpointRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $prodEndpointApproval -LmaxDemoExecutionSwitchPath $prodEndpointSwitch -LmaxDemoConfigPath $prodEndpointConfig
$prodEndpointMain = Read-JsonFile (Join-Path $testRoot "lmax-prod-endpoint\lmax-sandbox-global-process-test-run-r001.json")
Assert-Equal "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_FORBIDDEN_PRODUCTION_OR_LIVE_ACTIVITY_R001" $prodEndpointMain.status "Production endpoint config must be rejected."

$prodCredRunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_TEST_LMAX_PROD_CRED"
$prodCredApproval = Join-Path $testRoot "lmax-prod-cred-approval.json"
$prodCredSwitch = Join-Path $testRoot "lmax-prod-cred-switch.json"
$prodCredConfig = Join-Path $testRoot "lmax-prod-cred-config.json"
New-LmaxDemoApprovalFile $prodCredApproval $prodCredRunId
New-LmaxDemoSwitchFile $prodCredSwitch $prodCredRunId 9 6000000 $false
New-LmaxDemoConfigFile $prodCredConfig "LMAX_DEMO_OR_SANDBOX_FIX_ENDPOINT_CONFIG_LABEL" "production_credentials_policy"
& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-prod-cred" -RunId $prodCredRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $prodCredApproval -LmaxDemoExecutionSwitchPath $prodCredSwitch -LmaxDemoConfigPath $prodCredConfig
$prodCredMain = Read-JsonFile (Join-Path $testRoot "lmax-prod-cred\lmax-sandbox-global-process-test-run-r001.json")
Assert-Equal "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_FORBIDDEN_PRODUCTION_OR_LIVE_ACTIVITY_R001" $prodCredMain.status "Production credential policy must be rejected."

$countFailRunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_TEST_LMAX_COUNT_FAIL"
$countFailApproval = Join-Path $testRoot "lmax-count-fail-approval.json"
$countFailSwitch = Join-Path $testRoot "lmax-count-fail-switch.json"
New-LmaxDemoApprovalFile $countFailApproval $countFailRunId
New-LmaxDemoSwitchFile $countFailSwitch $countFailRunId 8 6000000 $false
& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-count-fail" -RunId $countFailRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $countFailApproval -LmaxDemoExecutionSwitchPath $countFailSwitch -LmaxDemoConfigPath $lmaxReadyConfig
$countFailMain = Read-JsonFile (Join-Path $testRoot "lmax-count-fail\lmax-sandbox-global-process-test-run-r001.json")
Assert-Equal "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_RISK_LIMITS_FAILED_R001" $countFailMain.status "Max order count failure must block."

$notionalFailRunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_TEST_LMAX_NOTIONAL_FAIL"
$notionalFailApproval = Join-Path $testRoot "lmax-notional-fail-approval.json"
$notionalFailSwitch = Join-Path $testRoot "lmax-notional-fail-switch.json"
New-LmaxDemoApprovalFile $notionalFailApproval $notionalFailRunId
New-LmaxDemoSwitchFile $notionalFailSwitch $notionalFailRunId 9 1 $false
& $BuildScript -RepoRoot $RepoRoot -OutputSubdir "lmax-sandbox-global-process-test-run-r001-test\lmax-notional-fail" -RunId $notionalFailRunId -ExecutionMode LmaxSandbox -LmaxDemoApprovalPath $notionalFailApproval -LmaxDemoExecutionSwitchPath $notionalFailSwitch -LmaxDemoConfigPath $lmaxReadyConfig
$notionalFailMain = Read-JsonFile (Join-Path $testRoot "lmax-notional-fail\lmax-sandbox-global-process-test-run-r001.json")
Assert-Equal "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_RISK_LIMITS_FAILED_R001" $notionalFailMain.status "Max notional failure must block."

& $BuildScript -RepoRoot $RepoRoot -ExecutionMode LmaxSandbox
$currentMain = Read-JsonFile (Join-Path $PackageDir "lmax-sandbox-global-process-test-run-r001.json")
$currentExecution = Read-JsonFile (Join-Path $PackageDir "sandbox-execution-result-r001.json")
Assert-True ($currentMain.status -in @(
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_OPERATOR_APPROVAL_REQUIRED_R001",
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_EXECUTION_SWITCH_DISABLED_R001",
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_LMAX_SANDBOX_CONFIG_MISSING_R001",
    "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_EXECUTION_APPROVED_READY_R001"
)) "Current package must be blocked or approved-ready without executing in tests."
if ($currentMain.status -eq "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_EXECUTION_APPROVED_READY_R001") {
    Assert-Equal "APPROVED_READY_NOT_EXECUTED_BY_BUILD_SCRIPT" $currentExecution.status "Current approved-ready path must not execute LMAX in tests."
}
Assert-Equal $false $currentExecution.lmax_fix_api_call "Current test path must not call LMAX."
Assert-GuardSet $currentMain.global_guards

Write-Host "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_R001_TEST_PASS"
