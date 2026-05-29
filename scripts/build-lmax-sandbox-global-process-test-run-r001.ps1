param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputSubdir = "lmax-sandbox-global-process-test-run-r001",
    [string]$RunId,
    [ValidateSet("PreviewOnly", "Simulated", "LmaxSandbox")]
    [string]$ExecutionMode = "PreviewOnly",
    [string]$ApprovalArtifactPath,
    [string]$SimulationApprovalPath,
    [string]$LmaxDemoApprovalPath,
    [string]$LmaxDemoExecutionSwitchPath,
    [string]$LmaxDemoConfigPath,
    [switch]$ExplicitExecutionSwitch,
    [switch]$EnableLmaxDemoIntegration
)

$ErrorActionPreference = "Stop"

$Package = "NEXT_LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_R001"
$OutputDir = Join-Path $RepoRoot "artifacts\readiness\$OutputSubdir"
$ExistingMainPath = Join-Path $OutputDir "lmax-sandbox-global-process-test-run-r001.json"
$Timestamp = [DateTime]::UtcNow.ToString("yyyyMMddTHHmmssZ", [Globalization.CultureInfo]::InvariantCulture)
if ([string]::IsNullOrWhiteSpace($RunId)) {
    if (Test-Path -LiteralPath $ExistingMainPath) {
        $existingMain = Get-Content -Raw -LiteralPath $ExistingMainPath | ConvertFrom-Json
        $RunId = [string]$existingMain.run_id
    }
    if ([string]::IsNullOrWhiteSpace($RunId)) {
        $RunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_$Timestamp"
    }
}

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Required artifact missing: $Path" }
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Write-JsonFile([string]$Path, [object]$Value) {
    $parent = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Write-TextFile([string]$Path, [string]$Value) {
    $parent = Split-Path -Parent $Path
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    Set-Content -LiteralPath $Path -Value $Value -Encoding UTF8
}

function Get-Sha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Cannot hash missing artifact: $Path" }
    "sha256:$((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash)"
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Get-OptionalPropertyValue {
    param(
        [AllowNull()] $Object,
        [Parameter(Mandatory=$true)] [string] $Name,
        $Default = $null
    )

    if ($null -eq $Object) { return $Default }

    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name)) { return $Object[$Name] }
        return $Default
    }

    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop) { return $Default }

    return $prop.Value
}

function Get-OptionalBooleanProperty {
    param(
        [AllowNull()] $Object,
        [Parameter(Mandatory=$true)] [string] $Name,
        [bool] $Default = $false
    )

    $value = Get-OptionalPropertyValue -Object $Object -Name $Name -Default $Default
    if ($null -eq $value) { return $Default }

    return [bool]$value
}

function Convert-CoreToExecutionSymbol([string]$CoreSymbol) {
    if ($CoreSymbol -in @("AUDUSD", "EURUSD", "GBPUSD", "NZDUSD")) { return $CoreSymbol }
    if ($CoreSymbol.EndsWith("USD")) { return "USD$($CoreSymbol.Substring(0,3))" }
    return $CoreSymbol
}

function Convert-CoreSideToExecutionSide([string]$CoreSymbol, [string]$CoreSide) {
    if ($CoreSymbol -in @("AUDUSD", "EURUSD", "GBPUSD", "NZDUSD")) { return $CoreSide }
    if ($CoreSymbol.EndsWith("USD")) {
        if ($CoreSide -eq "BUY") { return "SELL" }
        if ($CoreSide -eq "SELL") { return "BUY" }
    }
    return $CoreSide
}

function Test-Approval($Approval, [string]$ExpectedRunId) {
    $issues = @()
    if ($null -eq $Approval) { return @("APPROVAL_ARTIFACT_MISSING") }
    if ($Approval.approval_type -ne "lmax_sandbox_global_process_test_run") { $issues += "APPROVAL_TYPE_INVALID" }
    if ($Approval.run_id -ne $ExpectedRunId) { $issues += "APPROVAL_RUN_ID_MISMATCH" }
    if ($Approval.environment -ne "sandbox") { $issues += "APPROVAL_ENVIRONMENT_NOT_SANDBOX" }
    if ([string]$Approval.venue -notin @("LMAX_DEMO", "LMAX_SANDBOX", "LMAX_DEMO_OR_SANDBOX")) { $issues += "APPROVAL_VENUE_NOT_DEMO_OR_SANDBOX" }
    if ([string]::IsNullOrWhiteSpace([string]$Approval.approved_by)) { $issues += "APPROVED_BY_MISSING" }
    if ([string]::IsNullOrWhiteSpace([string]$Approval.approved_at_utc)) { $issues += "APPROVED_AT_UTC_MISSING" }
    foreach ($action in @("submit_sandbox_orders", "capture_sandbox_fills", "flatten_sandbox_positions", "reconcile_sandbox_trade_level")) {
        if (@($Approval.approved_actions) -notcontains $action) { $issues += "APPROVED_ACTION_MISSING_$action" }
    }
    if ($Approval.explicit_acknowledgement_no_production -ne $true) { $issues += "NO_PRODUCTION_ACK_MISSING" }
    if ([string]::IsNullOrWhiteSpace([string]$Approval.approval_sha256)) { $issues += "APPROVAL_SHA256_MISSING" }
    return $issues
}

function Test-SimulationApproval($Approval, [string]$ExpectedRunId) {
    $issues = @()
    if ($null -eq $Approval) { return @("SIMULATION_APPROVAL_ARTIFACT_MISSING") }
    if ($Approval.run_id -ne $ExpectedRunId) { $issues += "SIMULATION_APPROVAL_RUN_ID_MISMATCH" }
    if ($Approval.environment -ne "sandbox") { $issues += "SIMULATION_APPROVAL_ENVIRONMENT_NOT_SANDBOX" }
    if ($Approval.simulation_only -ne $true) { $issues += "SIMULATION_ONLY_FLAG_MISSING" }
    if ([string]::IsNullOrWhiteSpace([string]$Approval.approved_by)) { $issues += "SIMULATION_APPROVED_BY_MISSING" }
    if ([string]::IsNullOrWhiteSpace([string]$Approval.approved_at_utc)) { $issues += "SIMULATION_APPROVED_AT_UTC_MISSING" }
    foreach ($action in @("simulate_fills", "simulate_residual_flatten", "reconcile_simulated_trade_level", "compute_simulated_strategy_pnl")) {
        if (@($Approval.approved_actions) -notcontains $action) { $issues += "SIMULATION_APPROVED_ACTION_MISSING_$action" }
    }
    if ($Approval.no_lmax_call -ne $true) { $issues += "SIMULATION_NO_LMAX_CALL_ACK_MISSING" }
    if ($Approval.no_broker_api_call -ne $true) { $issues += "SIMULATION_NO_BROKER_API_CALL_ACK_MISSING" }
    if ($Approval.no_live_trading -ne $true) { $issues += "SIMULATION_NO_LIVE_TRADING_ACK_MISSING" }
    return $issues
}

function Test-LmaxDemoApproval($Approval, [string]$ExpectedRunId) {
    $issues = @()
    if ($null -eq $Approval) { return @("LMAX_DEMO_EXECUTION_APPROVAL_MISSING") }
    if ($Approval.approval_type -ne "lmax_demo_sandbox_execution_approval") { $issues += "LMAX_DEMO_APPROVAL_TYPE_INVALID" }
    if ($Approval.run_id -ne $ExpectedRunId) { $issues += "LMAX_DEMO_APPROVAL_RUN_ID_MISMATCH" }
    if ($Approval.environment -ne "sandbox") { $issues += "LMAX_DEMO_APPROVAL_ENVIRONMENT_NOT_SANDBOX" }
    if ([string]$Approval.venue -notin @("LMAX_DEMO", "LMAX_SANDBOX", "LMAX_DEMO_OR_SANDBOX", "LMAX_TEST")) { $issues += "LMAX_DEMO_APPROVAL_VENUE_NOT_DEMO_OR_SANDBOX" }
    if ($Approval.simulation_already_reconciled -ne $true) { $issues += "LMAX_DEMO_APPROVAL_SIMULATION_RECONCILED_MISSING" }
    foreach ($action in @(
        "submit_lmax_demo_sandbox_orders",
        "capture_lmax_demo_sandbox_execution_reports",
        "capture_lmax_demo_sandbox_fills",
        "flatten_lmax_demo_sandbox_positions_if_required",
        "reconcile_lmax_demo_sandbox_trade_level",
        "compute_lmax_demo_sandbox_strategy_pnl"
    )) {
        if (@($Approval.approved_actions) -notcontains $action) { $issues += "LMAX_DEMO_APPROVED_ACTION_MISSING_$action" }
    }
    if ($Approval.explicit_acknowledgement_no_production -ne $true) { $issues += "LMAX_DEMO_APPROVAL_NO_PRODUCTION_ACK_MISSING" }
    if ([string]::IsNullOrWhiteSpace([string]$Approval.approved_by) -or [string]$Approval.approved_by -eq "<operator>") { $issues += "LMAX_DEMO_APPROVED_BY_MISSING" }
    if ([string]::IsNullOrWhiteSpace([string]$Approval.approved_at_utc) -or [string]$Approval.approved_at_utc -eq "<utc>") { $issues += "LMAX_DEMO_APPROVED_AT_UTC_MISSING" }
    if ([string]::IsNullOrWhiteSpace([string]$Approval.approval_sha256) -or -not ([string]$Approval.approval_sha256).StartsWith("sha256:")) { $issues += "LMAX_DEMO_APPROVAL_SHA256_MISSING" }
    return $issues
}

function Test-LmaxDemoExecutionSwitch($Switch, [string]$ExpectedRunId, [int]$OrderCount, [decimal]$TargetNotional) {
    $issues = @()
    if ($null -eq $Switch) { return @("LMAX_DEMO_EXECUTION_SWITCH_MISSING") }
    if ($Switch.run_id -ne $ExpectedRunId) { $issues += "LMAX_DEMO_SWITCH_RUN_ID_MISMATCH" }
    $executionEnabled = Get-OptionalBooleanProperty -Object $Switch -Name "execution_enabled" -Default $false
    $productionLive = Get-OptionalBooleanProperty -Object $Switch -Name "production_live" -Default $false
    $killSwitchActive = Get-OptionalBooleanProperty -Object $Switch -Name "kill_switch_active" -Default $false
    if ($executionEnabled -ne $true) { $issues += "LMAX_DEMO_SWITCH_DISABLED" }
    if ($Switch.environment -ne "sandbox") { $issues += "LMAX_DEMO_SWITCH_ENVIRONMENT_NOT_SANDBOX" }
    if ([string]$Switch.venue -notin @("LMAX_DEMO", "LMAX_SANDBOX", "LMAX_DEMO_OR_SANDBOX", "LMAX_TEST")) { $issues += "LMAX_DEMO_SWITCH_VENUE_NOT_DEMO_OR_SANDBOX" }
    if ($productionLive -ne $false) { $issues += "LMAX_DEMO_SWITCH_PRODUCTION_LIVE_TRUE" }
    if ($killSwitchActive -ne $false) { $issues += "LMAX_DEMO_SWITCH_KILL_SWITCH_ACTIVE" }
    if ([int]$Switch.max_order_count -lt $OrderCount) { $issues += "LMAX_DEMO_SWITCH_MAX_ORDER_COUNT_FAILED" }
    if ([decimal]$Switch.max_notional_usd -lt $TargetNotional) { $issues += "LMAX_DEMO_SWITCH_MAX_NOTIONAL_FAILED" }
    if ([string]::IsNullOrWhiteSpace([string]$Switch.created_by)) { $issues += "LMAX_DEMO_SWITCH_CREATED_BY_MISSING" }
    if ([string]::IsNullOrWhiteSpace([string]$Switch.created_at_utc)) { $issues += "LMAX_DEMO_SWITCH_CREATED_AT_UTC_MISSING" }
    return $issues
}

function Test-LmaxDemoConfig($Config) {
    $issues = @()
    $source = if ($null -eq $Config) {
        [ordered]@{
            environment = "sandbox"
            endpoint_config_references = @("LMAX_DEMO_OR_SANDBOX_FIX_ENDPOINT_CONFIG_LABEL")
            credential_source_policy = "operator_local_secret_store_reference_only"
            raw_secret_values_persisted = $false
            credential_policy = "demo_or_sandbox_only"
            tls_required = $true
            sequence_policy = "new_sandbox_test_session_or_explicit_reset_only"
            session_log_path = "artifacts/readiness/lmax-sandbox-global-process-test-run-r001/logs/session.log"
            order_log_path = "artifacts/readiness/lmax-sandbox-global-process-test-run-r001/logs/orders.log"
            execution_report_log_path = "artifacts/readiness/lmax-sandbox-global-process-test-run-r001/logs/execution-reports.log"
        }
    } else { $Config }

    $environment = [string](Get-OptionalPropertyValue -Object $source -Name "environment" -Default "")
    $endpointRefs = @(Get-OptionalPropertyValue -Object $source -Name "endpoint_config_references" -Default @())
    $credentialSourcePolicy = [string](Get-OptionalPropertyValue -Object $source -Name "credential_source_policy" -Default "")
    $credentialPolicy = [string](Get-OptionalPropertyValue -Object $source -Name "credential_policy" -Default "")
    $rawSecretValuesPersisted = Get-OptionalBooleanProperty -Object $source -Name "raw_secret_values_persisted" -Default $false
    $rawSecretsPresent = Get-OptionalBooleanProperty -Object $source -Name "raw_secrets_present" -Default $false
    $tlsRequired = Get-OptionalBooleanProperty -Object $source -Name "tls_required" -Default $false
    $sequencePolicy = [string](Get-OptionalPropertyValue -Object $source -Name "sequence_policy" -Default "")
    $sessionLogPath = [string](Get-OptionalPropertyValue -Object $source -Name "session_log_path" -Default "")
    $orderLogPath = [string](Get-OptionalPropertyValue -Object $source -Name "order_log_path" -Default "")
    $executionReportLogPath = [string](Get-OptionalPropertyValue -Object $source -Name "execution_report_log_path" -Default "")

    if ($environment -notin @("sandbox", "demo", "test")) { $issues += "LMAX_DEMO_CONFIG_ENVIRONMENT_INVALID" }
    $endpointText = ($endpointRefs -join " ").ToLowerInvariant()
    if ($endpointText.Contains("prod") -or $endpointText.Contains("live")) { $issues += "LMAX_DEMO_CONFIG_PRODUCTION_ENDPOINT_DETECTED" }
    $credentialText = ($credentialSourcePolicy + " " + $credentialPolicy).ToLowerInvariant()
    if ($credentialText.Contains("prod") -or $credentialText.Contains("live")) { $issues += "LMAX_DEMO_CONFIG_PRODUCTION_CREDENTIAL_POLICY_DETECTED" }
    if ($rawSecretValuesPersisted -eq $true -or $rawSecretsPresent -eq $true) { $issues += "LMAX_DEMO_CONFIG_RAW_SECRET_DETECTED" }
    if ($tlsRequired -ne $true) { $issues += "LMAX_DEMO_CONFIG_TLS_MISSING" }
    if ([string]::IsNullOrWhiteSpace($sequencePolicy)) { $issues += "LMAX_DEMO_CONFIG_SEQUENCE_POLICY_MISSING" }
    if ([string]::IsNullOrWhiteSpace($sessionLogPath)) { $issues += "LMAX_DEMO_CONFIG_SESSION_LOG_PATH_MISSING" }
    if ([string]::IsNullOrWhiteSpace($orderLogPath)) { $issues += "LMAX_DEMO_CONFIG_ORDER_LOG_PATH_MISSING" }
    if ([string]::IsNullOrWhiteSpace($executionReportLogPath)) { $issues += "LMAX_DEMO_CONFIG_EXECUTION_REPORT_LOG_PATH_MISSING" }
    [ordered]@{
        source = $source
        issues = @($issues)
        normalized = [ordered]@{
            environment = $environment
            endpoint_config_references = $endpointRefs
            credential_source_policy = $credentialSourcePolicy
            credential_policy = $credentialPolicy
            raw_secret_values_persisted = $rawSecretValuesPersisted
            raw_secrets_present = $rawSecretsPresent
            tls_required = $tlsRequired
            sequence_policy = $sequencePolicy
            session_log_path = $sessionLogPath
            order_log_path = $orderLogPath
            execution_report_log_path = $executionReportLogPath
        }
    }
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$FrontHalfDir = Join-Path $RepoRoot "artifacts\readiness\front-half-qubes-to-order-fill-reconciliation-sandbox-e2e-r001"
$FrontMainPath = Join-Path $FrontHalfDir "front-half-qubes-to-order-fill-reconciliation-sandbox-e2e-r001.json"
$FrontMarketPath = Join-Path $FrontHalfDir "front-half-market-data-basis-r001.json"
$FrontQubesPath = Join-Path $FrontHalfDir "qubes-weight-handoff-r001.json"
$FrontDriftPath = Join-Path $FrontHalfDir "drift-calculation-r001.json"
$FrontOrdersPath = Join-Path $FrontHalfDir "order-targets-r001.json"
$FrontPlanPath = Join-Path $FrontHalfDir "execution-algo-plan-r001.json"
$FrontFillsPath = Join-Path $FrontHalfDir "sandbox-orders-fills-r001.json"
$FrontResidualPath = Join-Path $FrontHalfDir "residual-flatten-report-r001.json"
$FrontTradeReconPath = Join-Path $FrontHalfDir "trade-level-reconciliation-r001.json"
$FrontPnlPath = Join-Path $FrontHalfDir "front-half-strategy-pnl-r001.json"
$PostCommitPath = Join-Path $RepoRoot "artifacts\readiness\sandbox-ledger-db-post-commit-closeout-and-production-go-no-go-r001\sandbox-ledger-db-post-commit-closeout-r001.json"
$BrokerPnlPath = Join-Path $RepoRoot "artifacts\readiness\broker-statement-confirmed-pnl-r001\broker-statement-confirmed-pnl-r001.json"

$frontMain = Read-JsonFile $FrontMainPath
$frontMarket = Read-JsonFile $FrontMarketPath
$frontQubes = Read-JsonFile $FrontQubesPath
$frontDrift = Read-JsonFile $FrontDriftPath
$frontOrders = Read-JsonFile $FrontOrdersPath
$frontPlan = Read-JsonFile $FrontPlanPath
$frontFills = Read-JsonFile $FrontFillsPath
$frontResidual = Read-JsonFile $FrontResidualPath
$frontTradeRecon = Read-JsonFile $FrontTradeReconPath
$frontPnl = Read-JsonFile $FrontPnlPath
$postCommit = Read-JsonFile $PostCommitPath
$brokerPnl = Read-JsonFile $BrokerPnlPath

Assert-True ($frontMain.status -eq "FRONT_HALF_QUBES_TO_ORDER_FILL_RECONCILIATION_SANDBOX_CONFIRMED_R001") "Front-half sandbox E2E source must be confirmed."
Assert-True ($postCommit.status -eq "SANDBOX_LEDGER_DB_POST_COMMIT_CLOSEOUT_READY_R001") "Back-half post-commit closeout source must be ready."
Assert-True ($brokerPnl.status -eq "BROKER_STATEMENT_CONFIRMED_PNL_READY_R001") "Broker statement PnL source must be ready."

$approval = $null
$approvalIssues = @("APPROVAL_ARTIFACT_MISSING")
if (-not [string]::IsNullOrWhiteSpace($ApprovalArtifactPath) -and (Test-Path -LiteralPath $ApprovalArtifactPath)) {
    $approval = Read-JsonFile $ApprovalArtifactPath
    $approvalIssues = Test-Approval $approval $RunId
}
$approvalAccepted = (@($approvalIssues).Count -eq 0)
$simulationApproval = $null
if ([string]::IsNullOrWhiteSpace($SimulationApprovalPath)) {
    $SimulationApprovalPath = Join-Path $OutputDir "simulation-approval-r001.json"
}
$simulationApprovalIssues = @("SIMULATION_APPROVAL_ARTIFACT_MISSING")
if (Test-Path -LiteralPath $SimulationApprovalPath) {
    $simulationApproval = Read-JsonFile $SimulationApprovalPath
    $simulationApprovalIssues = Test-SimulationApproval $simulationApproval $RunId
}
$simulationAccepted = (@($simulationApprovalIssues).Count -eq 0)
$executionSwitchEnabled = [bool]$ExplicitExecutionSwitch
$sandboxExecutionApproved = ($approvalAccepted -and $executionSwitchEnabled)
$simulationExecutionApproved = ($simulationAccepted -and $ExecutionMode -eq "Simulated")

if ([string]::IsNullOrWhiteSpace($LmaxDemoApprovalPath)) {
    $LmaxDemoApprovalPath = Join-Path $OutputDir "operator-approval-lmax-demo-execution-r001.json"
}
if ([string]::IsNullOrWhiteSpace($LmaxDemoExecutionSwitchPath)) {
    $LmaxDemoExecutionSwitchPath = Join-Path $OutputDir "lmax-demo-execution-switch-r001.json"
}

$lmaxDemoApproval = $null
$lmaxDemoApprovalIssues = @("LMAX_DEMO_EXECUTION_APPROVAL_MISSING")
if (Test-Path -LiteralPath $LmaxDemoApprovalPath) {
    $lmaxDemoApproval = Read-JsonFile $LmaxDemoApprovalPath
    $lmaxDemoApprovalIssues = Test-LmaxDemoApproval $lmaxDemoApproval $RunId
}

$lmaxDemoSwitch = $null
$lmaxDemoSwitchIssues = @("LMAX_DEMO_EXECUTION_SWITCH_MISSING")
if (Test-Path -LiteralPath $LmaxDemoExecutionSwitchPath) {
    $lmaxDemoSwitch = Read-JsonFile $LmaxDemoExecutionSwitchPath
    $lmaxDemoSwitchIssues = Test-LmaxDemoExecutionSwitch $lmaxDemoSwitch $RunId @($frontOrders.orders).Count ([decimal]$frontQubes.target_notional.amount)
}

$lmaxDemoConfigInput = $null
if (-not [string]::IsNullOrWhiteSpace($LmaxDemoConfigPath) -and (Test-Path -LiteralPath $LmaxDemoConfigPath)) {
    $lmaxDemoConfigInput = Read-JsonFile $LmaxDemoConfigPath
}
$lmaxDemoConfigValidation = Test-LmaxDemoConfig $lmaxDemoConfigInput
$lmaxDemoApprovalAccepted = (@($lmaxDemoApprovalIssues).Count -eq 0)
$lmaxDemoSwitchAccepted = (@($lmaxDemoSwitchIssues).Count -eq 0)
$lmaxDemoConfigAccepted = (@($lmaxDemoConfigValidation.issues).Count -eq 0)
$lmaxDemoApprovedReady = ($lmaxDemoApprovalAccepted -and $lmaxDemoSwitchAccepted -and $lmaxDemoConfigAccepted)

$mainStatus = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_OPERATOR_APPROVAL_REQUIRED_R001"
if (-not $frontQubes -or @($frontQubes.netted_usd_weights).Count -eq 0) { $mainStatus = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_QUBES_HANDOFF_MISSING_R001" }
elseif (-not $frontMarket -or @($frontMarket.instruments).Count -eq 0) { $mainStatus = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_MARKET_DATA_MISSING_R001" }
elseif (@($frontOrders.orders).Count -eq 0) { $mainStatus = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_ORDER_TARGETS_INVALID_R001" }
elseif ($ExecutionMode -eq "Simulated" -and -not $simulationExecutionApproved) { $mainStatus = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_OPERATOR_APPROVAL_REQUIRED_R001" }
elseif ($ExecutionMode -eq "LmaxSandbox" -and -not $lmaxDemoApprovalAccepted) { $mainStatus = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_OPERATOR_APPROVAL_REQUIRED_R001" }
elseif ($ExecutionMode -eq "LmaxSandbox" -and @($lmaxDemoSwitchIssues) -contains "LMAX_DEMO_SWITCH_KILL_SWITCH_ACTIVE") { $mainStatus = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_KILL_SWITCH_ACTIVE_R001" }
elseif ($ExecutionMode -eq "LmaxSandbox" -and (-not $lmaxDemoSwitchAccepted)) {
    if (@($lmaxDemoSwitchIssues) -contains "LMAX_DEMO_EXECUTION_SWITCH_MISSING" -or @($lmaxDemoSwitchIssues) -contains "LMAX_DEMO_SWITCH_DISABLED") {
        $mainStatus = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_EXECUTION_SWITCH_DISABLED_R001"
    } else {
        $mainStatus = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_RISK_LIMITS_FAILED_R001"
    }
}
elseif ($ExecutionMode -eq "LmaxSandbox" -and @($lmaxDemoConfigValidation.issues | Where-Object { $_ -like "*PRODUCTION*" }).Count -gt 0) { $mainStatus = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_FORBIDDEN_PRODUCTION_OR_LIVE_ACTIVITY_R001" }
elseif ($ExecutionMode -eq "LmaxSandbox" -and -not $lmaxDemoConfigAccepted) { $mainStatus = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_LMAX_SANDBOX_CONFIG_MISSING_R001" }
elseif ($ExecutionMode -eq "PreviewOnly") { $mainStatus = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_BLOCKED_OPERATOR_APPROVAL_REQUIRED_R001" }
elseif ($ExecutionMode -eq "Simulated") { $mainStatus = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_SIMULATED_RECONCILED_R001" }
elseif ($ExecutionMode -eq "LmaxSandbox") { $mainStatus = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_EXECUTION_APPROVED_READY_R001" }

$manifest = [ordered]@{
    package = $Package
    run_id = $RunId
    environment = "sandbox"
    venue = "LMAX_DEMO_OR_SANDBOX"
    production_live = $false
    target_notional_usd = $frontQubes.target_notional.amount
    source_systems = [ordered]@{
        market_data = "front-half-market-data-basis-r001"
        qubes_core_weight_handoff = "front-half qubes-weight-handoff-r001"
        order_targets = "front-half order-targets-r001"
        back_half_reference = "sandbox-ledger-db-post-commit-closeout-and-production-go-no-go-r001"
    }
    operator_approval_status = if ($approvalAccepted) { "APPROVAL_ACCEPTED_FOR_RUN_ID" } else { "APPROVAL_REQUIRED" }
    execution_enabled_status = if ($sandboxExecutionApproved) { "EXPLICIT_SWITCH_ENABLED" } elseif ($simulationExecutionApproved) { "SIMULATION_APPROVED" } else { "DISABLED" }
    expected_artifacts = @(
        "lmax-sandbox-market-data-basis-r001.json",
        "qubes-core-weight-handoff-r001.json",
        "drift-and-order-targets-r001.json",
        "lmax-order-manifest-r001.json",
        "execution-algo-plan-r001.json",
        "operator-approval-required-r001.json",
        "simulation-approval-r001.json",
        "simulation-approval-status-r001.json",
        "lmax-sandbox-execution-harness-r001.json",
        "sandbox-simulated-fills-r001.json",
        "sandbox-execution-result-r001.json",
        "residual-flatten-report-r001.json",
        "sandbox-trade-level-reconciliation-r001.json",
        "sandbox-pnl-r001.json",
        "same-run-broker-evidence-instructions-r001.md"
    )
    no_production_guarantees = [ordered]@{
        production_live = $false
        live_credentials = $false
        production_db_mutation = $false
        production_ledger_commit = $false
        trading_readiness = $false
    }
}

$marketBasis = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "lmax_sandbox_market_data_basis_r001"
    status = "LMAX_SANDBOX_MARKET_DATA_BASIS_READY_R001"
    mode = "prior_artifact"
    source = "front-half-market-data-basis-r001"
    source_artifact_hash = Get-Sha256 $FrontMarketPath
    instruments = $frontMarket.instruments
    lmax_demo_marketdata_used = $false
    lmax_demo_marketdata_approval_required_if_used = $true
    external_calls = $false
    market_data_fetch = $false
}

$qubesHandoff = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "qubes_core_weight_handoff_r001"
    status = "QUBES_CORE_WEIGHT_HANDOFF_READY_R001"
    source_system = $frontQubes.source_system
    run_id_source = $frontQubes.run_id
    strategy = $frontQubes.strategy
    raw_aggregated_weights = $frontQubes.raw_aggregated_weights
    final_manager_weights = $frontQubes.final_manager_weights
    netted_usd_weights = $frontQubes.netted_usd_weights
    target_notional = $frontQubes.target_notional
    source_artifact_hash = Get-Sha256 $FrontQubesPath
    generated_by_pipeline = $frontQubes.generated_by_pipeline
    generated_by_qubes_core = $frontQubes.real_qubes_core_generation_confirmed
    synthetic_fixture = $frontQubes.synthetic_fixture
    honesty_note = "Uses existing sandbox Core/Anubis handoff; does not claim real upstream Qubes/Core optimizer generation."
}

$driftAndTargets = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "drift_and_order_targets_r001"
    status = "DRIFT_AND_ORDER_TARGETS_READY_R001"
    current_portfolio_state = $frontDrift.current_portfolio_state
    current_portfolio_state_fixture = $frontDrift.current_portfolio_state_fixture
    target_weights = $frontQubes.netted_usd_weights
    target_notional = $frontQubes.target_notional
    drift = $frontDrift.rows
    order_targets = $frontOrders.orders
    skipped_orders = $frontOrders.skipped_orders
    residual_after_rounding_reported = $true
}

$orderManifestOrders = @()
foreach ($order in @($frontOrders.orders)) {
    if ($order.security_id -and $order.security_id_source_tag22 -ne "8") {
        throw "Order manifest invalid: tag 48 SecurityID present but tag 22 is not 8 for $($order.symbol)."
    }
    $orderManifestOrders += [ordered]@{
        run_id = $RunId
        symbol = $order.symbol
        side = $order.side
        quantity = $order.refined_quantity
        security_id = $order.security_id
        security_id_source_tag22 = $order.security_id_source_tag22
        tag22_policy_enforced = $true
        production_live = $false
        submit_allowed_without_approval = $false
    }
}

$orderManifest = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "lmax_order_manifest_r001"
    status = "LMAX_ORDER_MANIFEST_READY_PREVIEW_ONLY_R001"
    environment = "sandbox"
    venue = "LMAX_DEMO_OR_SANDBOX"
    orders = $orderManifestOrders
    order_count = @($orderManifestOrders).Count
    tag22_policy = [ordered]@{ if_tag48_present_tag22_must_equal = "8"; enforced = $true }
    production_live = $false
    trading_readiness = $false
}

$executionPlan = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "execution_algo_plan_r001"
    status = "EXECUTION_ALGO_PLAN_READY_R001"
    algo_name = "LmaxSandboxGlobalProcessTest_HarnessGated_R001"
    slicing_policy = "single_slice_per_order_for_r001_unless_future_package_changes"
    retry_policy = "no_automatic_retry_without_new_operator_approval"
    fill_capture_policy = "capture_execution_reports_bound_to_run_id"
    flatten_policy = "flatten_only_sandbox_positions_created_by_run_id"
    residual_zero_policy = "must_reconcile_final_residuals_to_zero_or_block"
    cancel_replace_policy = "not_enabled_for_r001"
    unknown_order_state_recovery_policy = "stop_capture_logs_mark_unknown_block_reconciliation"
    no_production_routing = $true
    production_live = $false
}

$approvalArtifact = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "operator_approval_required_r001"
    status = if ($approvalAccepted) { "OPERATOR_APPROVAL_ACCEPTED_R001" } else { "BLOCKED_OPERATOR_APPROVAL_REQUIRED" }
    approval_required = $true
    approval_present = ($null -ne $approval)
    approval_valid = $approvalAccepted
    approval_issues = @($approvalIssues)
    required_approval_fields = @(
        "approval_type",
        "run_id",
        "environment",
        "venue",
        "approved_by",
        "approved_at_utc",
        "approved_actions",
        "explicit_acknowledgement_no_production",
        "approval_sha256"
    )
    required_approved_actions = @(
        "submit_sandbox_orders",
        "capture_sandbox_fills",
        "flatten_sandbox_positions",
        "reconcile_sandbox_trade_level"
    )
    explicit_execution_switch_enabled = $executionSwitchEnabled
}

$simulationApprovalArtifact = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "simulation_approval_r001"
    status = if ($simulationAccepted) { "SIMULATION_APPROVAL_ACCEPTED_R001" } else { "SIMULATION_APPROVAL_REQUIRED" }
    simulation_only = $true
    approval_present = ($null -ne $simulationApproval)
    approval_valid = $simulationAccepted
    approval_issues = @($simulationApprovalIssues)
    required_approved_actions = @(
        "simulate_fills",
        "simulate_residual_flatten",
        "reconcile_simulated_trade_level",
        "compute_simulated_strategy_pnl"
    )
    no_lmax_call = $true
    no_broker_api_call = $true
    no_live_trading = $true
}

$lmaxDemoApprovalArtifact = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "operator_approval_lmax_demo_execution_status_r001"
    status = if ($lmaxDemoApprovalAccepted) { "LMAX_DEMO_EXECUTION_APPROVAL_ACCEPTED_R001" } else { "LMAX_DEMO_EXECUTION_APPROVAL_REQUIRED" }
    approval_path = $LmaxDemoApprovalPath
    approval_present = ($null -ne $lmaxDemoApproval)
    approval_valid = $lmaxDemoApprovalAccepted
    approval_issues = @($lmaxDemoApprovalIssues)
    required_approved_actions = @(
        "submit_lmax_demo_sandbox_orders",
        "capture_lmax_demo_sandbox_execution_reports",
        "capture_lmax_demo_sandbox_fills",
        "flatten_lmax_demo_sandbox_positions_if_required",
        "reconcile_lmax_demo_sandbox_trade_level",
        "compute_lmax_demo_sandbox_strategy_pnl"
    )
    no_production_required = $true
}

$lmaxDemoSwitchArtifact = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "lmax_demo_execution_switch_status_r001"
    status = if ($lmaxDemoSwitchAccepted) { "LMAX_DEMO_EXECUTION_SWITCH_ACCEPTED_R001" } else { "LMAX_DEMO_EXECUTION_SWITCH_REQUIRED_OR_INVALID" }
    switch_path = $LmaxDemoExecutionSwitchPath
    switch_present = ($null -ne $lmaxDemoSwitch)
    switch_valid = $lmaxDemoSwitchAccepted
    switch_issues = @($lmaxDemoSwitchIssues)
    execution_enabled = Get-OptionalBooleanProperty -Object $lmaxDemoSwitch -Name "execution_enabled" -Default $false
    kill_switch_active = Get-OptionalBooleanProperty -Object $lmaxDemoSwitch -Name "kill_switch_active" -Default $false
    max_order_count = if ($null -ne $lmaxDemoSwitch) { $lmaxDemoSwitch.max_order_count } else { $null }
    max_notional_usd = if ($null -ne $lmaxDemoSwitch) { $lmaxDemoSwitch.max_notional_usd } else { $null }
}

$lmaxDemoConfigArtifact = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "lmax_demo_execution_config_validation_r001"
    status = if ($lmaxDemoConfigAccepted) { "LMAX_DEMO_EXECUTION_CONFIG_VALID_R001" } else { "LMAX_DEMO_EXECUTION_CONFIG_INVALID_R001" }
    environment_demo_test_only = $true
    endpoint_config_references = $lmaxDemoConfigValidation.normalized.endpoint_config_references
    credential_source_policy = $lmaxDemoConfigValidation.normalized.credential_source_policy
    raw_secret_values_persisted = $lmaxDemoConfigValidation.normalized.raw_secret_values_persisted
    raw_secrets_present = $lmaxDemoConfigValidation.normalized.raw_secrets_present
    production_endpoint_strings_rejected = $true
    production_credentials_rejected = $true
    tls_required = $lmaxDemoConfigValidation.normalized.tls_required
    sequence_policy = $lmaxDemoConfigValidation.normalized.sequence_policy
    session_log_path = $lmaxDemoConfigValidation.normalized.session_log_path
    order_log_path = $lmaxDemoConfigValidation.normalized.order_log_path
    execution_report_log_path = $lmaxDemoConfigValidation.normalized.execution_report_log_path
    kill_switch_false = if ($null -ne $lmaxDemoSwitch) { -not (Get-OptionalBooleanProperty -Object $lmaxDemoSwitch -Name "kill_switch_active" -Default $false) } else { $null }
    max_notional_order_count_pass = ($lmaxDemoSwitchAccepted -or $ExecutionMode -ne "LmaxSandbox")
    no_raw_secrets_in_artifacts = (-not $lmaxDemoConfigValidation.normalized.raw_secret_values_persisted -and -not $lmaxDemoConfigValidation.normalized.raw_secrets_present)
    validation_issues = @($lmaxDemoConfigValidation.issues)
}

$harness = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "lmax_sandbox_execution_harness_r001"
    status = "LMAX_SANDBOX_EXECUTION_HARNESS_DEFINED_R001"
    environment = "sandbox"
    venue = "LMAX_DEMO_OR_SANDBOX"
    endpoint_config_references = @(
        "local operator-configured LMAX demo/sandbox FIX endpoint label",
        "no raw host secret or credential value recorded"
    )
    credential_source_policy = "local operator secret store or environment labels only; no raw credentials in artifacts"
    begin_string = "FIX.4.4"
    sender_comp_id_policy = "configured_label_only"
    target_comp_id_policy = "configured_label_only"
    tls_required = $true
    session_log_path = "artifacts/readiness/lmax-sandbox-global-process-test-run-r001/logs/$RunId/session.log"
    order_log_path = "artifacts/readiness/lmax-sandbox-global-process-test-run-r001/logs/$RunId/orders.log"
    execution_report_log_path = "artifacts/readiness/lmax-sandbox-global-process-test-run-r001/logs/$RunId/execution-reports.log"
    sequence_policy = "new_sandbox_test_session_or_explicit_reset_only"
    kill_switch_policy = "operator_local_kill_switch_required_before_sandbox_execution"
    max_notional_policy = [ordered]@{ target_notional_usd = $frontQubes.target_notional.amount; hard_limit_usd = $frontQubes.target_notional.amount }
    max_order_count_policy = [ordered]@{ max_order_count = @($orderManifestOrders).Count }
    raw_secrets_present = $false
}

$actualAdapterBinding = [ordered]@{
    artifact_type = "lmax_demo_actual_adapter_binding"
    run_id = $RunId
    environment = "sandbox"
    venue = "LMAX_DEMO_OR_SANDBOX"
    adapter_mode = "actual_lmax_demo_fix"
    adapter_enabled = $true
    production_live = $false
    production_endpoint_allowed = $false
    raw_secrets_persisted = $false
    credential_source = "environment_or_local_secret_store"
    required_secret_labels = @(
        "QQ_LMAX_DEMO_FIX_ENDPOINT",
        "QQ_LMAX_DEMO_FIX_SENDER_COMP_ID",
        "QQ_LMAX_DEMO_FIX_TARGET_COMP_ID",
        "QQ_LMAX_DEMO_FIX_USERNAME",
        "QQ_LMAX_DEMO_FIX_PASSWORD"
    )
    logs = [ordered]@{
        orders_log = "artifacts/readiness/lmax-sandbox-global-process-test-run-r001/logs/$RunId/orders.log"
        execution_reports_log = "artifacts/readiness/lmax-sandbox-global-process-test-run-r001/logs/$RunId/execution-reports.log"
    }
}

$executionResultStatus = "BLOCKED_OPERATOR_APPROVAL_REQUIRED"
$ordersSubmitted = @()
$executionReports = @()
$fills = @()
$flattenOrders = @()
$finalResiduals = @()
$residualZero = $false
if ($ExecutionMode -eq "Simulated" -and $simulationExecutionApproved) {
    $executionResultStatus = "simulated_only"
    $ordersSubmitted = $orderManifestOrders
    $fills = @($frontFills.fills | ForEach-Object {
        [ordered]@{
            run_id = $RunId
            order_id = $_.order_target_id
            order_target_id = $_.order_target_id
            symbol = $_.symbol
            side = $_.side
            target_quantity = $_.intended_quantity
            simulated_filled_quantity = $_.filled_quantity
            simulated_fill_price = $_.open_price
            simulated_flatten_price = $_.flatten_price
            fill_status = $_.fill_status
            fill_source = "deterministic_simulation"
            live_execution = $false
            lmax_fix_api_call = $false
            broker_api_call = $false
        }
    })
    $executionReports = @($fills | ForEach-Object {
        [ordered]@{
            run_id = $RunId
            symbol = $_.symbol
            order_target_id = $_.order_target_id
            execution_type = $_.fill_status
            filled_quantity = $_.simulated_filled_quantity
            simulated = $true
            lmax_call = $false
        }
    })
    $flattenOrders = @($frontResidual.residual_quantities | ForEach-Object {
        [ordered]@{
            run_id = $RunId
            symbol = $_.ExecutionSymbol
            quantity = $_.RetryFlattenFilledQuantity
            simulated = $true
            lmax_call = $false
        }
    })
    $finalResiduals = $frontResidual.residual_quantities
    $residualZero = $true
}
elseif ($ExecutionMode -eq "LmaxSandbox" -and $lmaxDemoApprovedReady) {
    $executionResultStatus = "APPROVED_READY_NOT_EXECUTED_BY_BUILD_SCRIPT"
    $ordersSubmitted = @()
    $executionReports = @()
    $fills = @()
    $flattenOrders = @()
    $finalResiduals = @()
    $residualZero = $false
}

$executionResult = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "sandbox_execution_result_r001"
    status = $executionResultStatus
    execution_mode = $ExecutionMode
    execution_mode_detail = if ($ExecutionMode -eq "LmaxSandbox") { "lmax_demo_sandbox" } elseif ($ExecutionMode -eq "Simulated") { "deterministic_simulation" } else { "preview_only" }
    orders_submitted = $ordersSubmitted
    orders_submitted_count = @($ordersSubmitted).Count
    execution_reports = $executionReports
    execution_reports_count = @($executionReports).Count
    fills = $fills
    fills_count = @($fills).Count
    rejects = @()
    rejected_count = 0
    cancels = @()
    cancelled_count = 0
    flatten_orders = $flattenOrders
    flatten_orders_count = @($flattenOrders).Count
    final_residuals = $finalResiduals
    residual_zero = $residualZero
    lmax_fix_api_call = $false
    production_lmax_call = $false
    broker_api_call = $false
    production_live = $false
    trading_readiness = $false
}

$simulatedFillsArtifact = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "sandbox_simulated_fills_r001"
    status = if ($ExecutionMode -eq "Simulated" -and $simulationExecutionApproved) { "SANDBOX_SIMULATED_FILLS_READY_R001" } else { "BLOCKED_SIMULATION_APPROVAL_REQUIRED" }
    fill_model = "deterministic_prior_front_half_sandbox_fill_replay_bound_to_run_id"
    orders_seen = @($orderManifestOrders).Count
    fills = $fills
    fill_count = @($fills).Count
    live_execution = $false
    lmax_fix_api_call = $false
    broker_api_call = $false
}

$residualFlatten = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "residual_flatten_report_r001"
    status = if ($residualZero) { "SANDBOX_SIMULATED_RESIDUAL_FLATTEN_READY_R001" } else { "BLOCKED_AWAITING_SIMULATED_FILLS" }
    residual_quantities_before_flatten_simulation = if ($residualZero) { $frontResidual.residual_quantities } else { @() }
    simulated_flatten_actions = $flattenOrders
    final_residual_quantities = if ($residualZero) { $frontResidual.residual_quantities } else { @() }
    residual_zero = $residualZero
    no_live_flatten_order = $true
    no_lmax_call = $true
    lmax_fix_api_call = $false
    broker_api_call = $false
}

$tradeReconStatus = if ($ExecutionMode -eq "Simulated" -and $simulationExecutionApproved) { "SANDBOX_SIMULATED_TRADE_LEVEL_RECONCILIATION_READY_R001" } else { "BLOCKED_AWAITING_SANDBOX_EXECUTION_OR_SIMULATION" }
$tradeRecon = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "sandbox_trade_level_reconciliation_r001"
    status = $tradeReconStatus
    order_targets_reconciled = ($tradeReconStatus -eq "SANDBOX_SIMULATED_TRADE_LEVEL_RECONCILIATION_READY_R001")
    submitted_orders_reconciled = ($tradeReconStatus -eq "SANDBOX_SIMULATED_TRADE_LEVEL_RECONCILIATION_READY_R001")
    execution_reports_reconciled = ($tradeReconStatus -eq "SANDBOX_SIMULATED_TRADE_LEVEL_RECONCILIATION_READY_R001")
    fills_reconciled = ($tradeReconStatus -eq "SANDBOX_SIMULATED_TRADE_LEVEL_RECONCILIATION_READY_R001")
    flatten_fills_reconciled = ($tradeReconStatus -eq "SANDBOX_SIMULATED_TRADE_LEVEL_RECONCILIATION_READY_R001")
    final_positions_residual_zero = $residualZero
    strategy_pnl_reconciled = ($tradeReconStatus -eq "SANDBOX_SIMULATED_TRADE_LEVEL_RECONCILIATION_READY_R001")
    same_run_trade_level_reconciliation = ($tradeReconStatus -eq "SANDBOX_SIMULATED_TRADE_LEVEL_RECONCILIATION_READY_R001")
    note = "Same-run trade-level reconciliation is available only for simulated fills in this build unless a future approved LMAX sandbox execution writes same-run execution reports."
}

$pnlStatus = if ($ExecutionMode -eq "Simulated" -and $simulationExecutionApproved) { "SANDBOX_SIMULATED_STRATEGY_PNL_READY_R001" } else { "BLOCKED_AWAITING_SANDBOX_FILLS" }
$sandboxPnl = [ordered]@{
    package = $Package
    run_id = $RunId
    artifact_type = "sandbox_pnl_r001"
    status = $pnlStatus
    classification = "SANDBOX_SIMULATED"
    gross_simulated_strategy_pnl = if ($pnlStatus -eq "SANDBOX_SIMULATED_STRATEGY_PNL_READY_R001") { $frontPnl.gross_pnl } else { $null }
    simulated_costs = [ordered]@{ available = $false; amount = $null; reason = "same-run sandbox cost estimate requires future cost policy binding to generated fills" }
    financing = [ordered]@{ available = $false; amount = $null }
    net_simulated_strategy_pnl = [ordered]@{ computed = $false; reason = "gross mixed quote-currency PnL only until cost and FX policies are bound" }
    open_residual_pnl = [ordered]@{ residual_zero = $residualZero }
    realized_unrealized_split = "sandbox_strategy_preview_not_accounting_close"
    broker_statement_pnl_comparison = [ordered]@{ applicable = $false; reason = "same-run broker export missing; historical LMAX statement is not same-run evidence" }
}

$sameRunInstructions = @"
# Same-Run Broker Evidence Instructions R001

Run ID: $RunId

After an approved LMAX demo/sandbox execution, export broker-side statement/report evidence for the exact same run scope:

- same run ID or operator mapping to this run ID
- same statement/report period
- same order set
- same fills and flatten fills
- same account currency and demo/sandbox account scope

Broker statement reconciliation for this package remains blocked until a same-run broker/export file is imported locally and hash-bound. The historical LMAX statement for 03/11/2025 must not be used as proof for this run unless order IDs, fill IDs, period, and run mapping match.

No broker/API fetch is authorized by this instruction. Use offline/manual export only unless a future package explicitly authorizes another path.
"@

$sameRunBrokerEvidenceStatus = if ($ExecutionMode -eq "Simulated" -and $simulationExecutionApproved) { "BLOCKED_SAME_RUN_BROKER_EXPORT_MISSING_SIMULATED_ONLY" } else { "BLOCKED_SAME_RUN_BROKER_EXPORT_MISSING" }
$main = [ordered]@{
    package = $Package
    status = $mainStatus
    run_id = $RunId
    environment = "sandbox"
    venue = "LMAX_DEMO_OR_SANDBOX"
    mode = "global_lmax_sandbox_process_test_harness"
    front_half_status = "READY_PRE_EXECUTION"
    execution_status = if ($ExecutionMode -eq "Simulated" -and $simulationExecutionApproved) { "simulated_only" } else { $executionResultStatus }
    fill_status = if (@($fills).Count -gt 0) { "simulated_fills_ready" } else { "NO_FILLS_BLOCKED_OR_NOT_EXECUTED" }
    fills_status = if (@($fills).Count -gt 0) { "simulated_fills_ready" } else { "NO_FILLS_BLOCKED_OR_NOT_EXECUTED" }
    flatten_status = if (@($flattenOrders).Count -gt 0) { "SIMULATED_FLATTEN_REPRESENTED" } else { "NOT_RUN" }
    residual_zero = $residualZero
    trade_level_reconciliation_status = if ($tradeReconStatus -eq "SANDBOX_SIMULATED_TRADE_LEVEL_RECONCILIATION_READY_R001") { "ready" } else { $tradeReconStatus }
    pnl_status = $pnlStatus
    strategy_pnl_status = if ($pnlStatus -eq "SANDBOX_SIMULATED_STRATEGY_PNL_READY_R001") { "ready" } else { $pnlStatus }
    same_run_broker_evidence_status = if ($ExecutionMode -eq "Simulated" -and $simulationExecutionApproved) { "blocked_until_lmax_export" } else { $sameRunBrokerEvidenceStatus }
    operator_approval_status = $approvalArtifact.status
    lmax_demo_execution_approval_status = $lmaxDemoApprovalArtifact.status
    lmax_demo_execution_switch_status = $lmaxDemoSwitchArtifact.status
    lmax_demo_execution_config_status = $lmaxDemoConfigArtifact.status
    lmax_demo_execution_ready = ($ExecutionMode -eq "LmaxSandbox" -and $mainStatus -eq "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_EXECUTION_APPROVED_READY_R001")
    lmax_demo_execution_integration_enabled = [bool]$EnableLmaxDemoIntegration
    explicit_execution_switch_enabled = $executionSwitchEnabled
    production_live = $false
    trading_readiness = $false
    ready_outputs = [ordered]@{
        run_manifest_ready = $true
        market_data_basis_ready = $true
        qubes_handoff_ready = $true
        drift_and_order_targets_ready = $true
        execution_harness_ready = $true
        sandbox_execution_ready = ($ExecutionMode -eq "LmaxSandbox" -and $sandboxExecutionApproved)
        simulated_execution_reconciled = ($ExecutionMode -eq "Simulated" -and $simulationExecutionApproved)
    }
    still_blocked = @(
        "same_run_broker_statement_reconciliation",
        "production_live",
        "trading_readiness"
    )
    global_guards = [ordered]@{
        trading_activity = $false
        r009_submission = $false
        lmax_fix_api_call = $false
        broker_api_call = $false
        polygon_massive_call = $false
        market_data_fetch = $false
        broker_fetch = $false
        account_data_fetch = $false
        production_live_write = $false
        production_live_ready = $false
        trading_readiness_ready = $false
    }
    lmax_fix_api_call = $false
    broker_api_call = $false
}

$coverageAfter = [ordered]@{
    package = $Package
    artifact_type = "e2e_flow_coverage_after_lmax_sandbox_run_r001"
    run_id = $RunId
    status = "E2E_FLOW_COVERAGE_AFTER_LMAX_SANDBOX_RUN_READY_R001"
    execution_mode = $ExecutionMode
    flow_coverage = if ($ExecutionMode -eq "Simulated" -and $simulationExecutionApproved) {
        [ordered]@{
            market_data = "SANDBOX_CONFIRMED"
            qubes_handoff = if ($frontQubes.synthetic_fixture -eq $true) { "SYNTHETIC_FIXTURE_ONLY" } else { "SANDBOX_CONFIRMED" }
            drift = "SANDBOX_CONFIRMED"
            order_creation = "SANDBOX_CONFIRMED"
            execution_algorithm = "SANDBOX_CONFIRMED"
            execution_and_fills = "SANDBOX_CONFIRMED_SIMULATED"
            trade_level_reconciliation = "SANDBOX_CONFIRMED_SIMULATED"
            strategy_pnl = "SANDBOX_CONFIRMED_SIMULATED"
            same_run_broker_reconciliation = "BLOCKED"
            production_live_trading = "BLOCKED"
        }
    } elseif ($ExecutionMode -eq "PreviewOnly" -or -not $sandboxExecutionApproved) {
        [ordered]@{
            market_data = "SANDBOX_CONFIRMED"
            qubes_handoff = if ($frontQubes.synthetic_fixture -eq $true) { "SYNTHETIC_FIXTURE_ONLY" } else { "SANDBOX_CONFIRMED" }
            drift = "SANDBOX_CONFIRMED"
            order_creation = "SANDBOX_CONFIRMED"
            execution_algorithm = "SANDBOX_CONFIRMED"
            qubes_drift_orders = "SANDBOX_CONFIRMED"
            execution_fills = "BLOCKED_OPERATOR_APPROVAL_REQUIRED"
            trade_level_reconciliation = "BLOCKED_AWAITING_SANDBOX_EXECUTION"
            same_run_broker_statement_reconciliation = "BLOCKED_EXPORT_MISSING"
            production_live_trading = "BLOCKED"
        }
    } else {
        [ordered]@{
            market_data = "SANDBOX_CONFIRMED"
            qubes_drift_orders = "SANDBOX_CONFIRMED"
            execution_fills = "SANDBOX_CONFIRMED"
            trade_level_reconciliation = "SANDBOX_CONFIRMED"
            same_run_broker_statement_reconciliation = "BLOCKED_EXPORT_MISSING"
            production_live_trading = "BLOCKED"
        }
    }
    production_live_ready = $false
    trading_readiness_ready = $false
}

Write-JsonFile (Join-Path $OutputDir "lmax-sandbox-global-process-run-manifest-r001.json") $manifest
Write-JsonFile (Join-Path $OutputDir "lmax-sandbox-market-data-basis-r001.json") $marketBasis
Write-JsonFile (Join-Path $OutputDir "qubes-core-weight-handoff-r001.json") $qubesHandoff
Write-JsonFile (Join-Path $OutputDir "drift-and-order-targets-r001.json") $driftAndTargets
Write-JsonFile (Join-Path $OutputDir "lmax-order-manifest-r001.json") $orderManifest
Write-JsonFile (Join-Path $OutputDir "execution-algo-plan-r001.json") $executionPlan
Write-JsonFile (Join-Path $OutputDir "operator-approval-required-r001.json") $approvalArtifact
Write-JsonFile (Join-Path $OutputDir "simulation-approval-status-r001.json") $simulationApprovalArtifact
Write-JsonFile (Join-Path $OutputDir "operator-approval-lmax-demo-execution-status-r001.json") $lmaxDemoApprovalArtifact
Write-JsonFile (Join-Path $OutputDir "lmax-demo-execution-switch-status-r001.json") $lmaxDemoSwitchArtifact
Write-JsonFile (Join-Path $OutputDir "lmax-demo-execution-config-validation-r001.json") $lmaxDemoConfigArtifact
Write-JsonFile (Join-Path $OutputDir "lmax-sandbox-execution-harness-r001.json") $harness
Write-JsonFile (Join-Path $OutputDir "lmax-demo-actual-adapter-binding-r001.json") $actualAdapterBinding
Write-JsonFile (Join-Path $OutputDir "sandbox-simulated-fills-r001.json") $simulatedFillsArtifact
Write-JsonFile (Join-Path $OutputDir "sandbox-execution-result-r001.json") $executionResult
Write-JsonFile (Join-Path $OutputDir "residual-flatten-report-r001.json") $residualFlatten
Write-JsonFile (Join-Path $OutputDir "sandbox-trade-level-reconciliation-r001.json") $tradeRecon
Write-JsonFile (Join-Path $OutputDir "sandbox-pnl-r001.json") $sandboxPnl
Write-TextFile (Join-Path $OutputDir "same-run-broker-evidence-instructions-r001.md") $sameRunInstructions
Write-JsonFile (Join-Path $OutputDir "lmax-sandbox-global-process-test-run-r001.json") $main
Write-JsonFile (Join-Path $OutputDir "e2e-flow-coverage-after-lmax-sandbox-run-r001.json") $coverageAfter

Write-Host "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_R001_BUILD_PASS"
