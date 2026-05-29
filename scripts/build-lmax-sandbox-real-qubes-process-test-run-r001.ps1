param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputSubdir = "lmax-sandbox-real-qubes-process-test-run-r001",
    [string]$RunId,
    [string]$OperatorApprovalPath,
    [string]$ExecutionSwitchPath,
    [string]$LmaxDemoConfigPath,
    [string]$AdapterBindingPath,
    [switch]$ForceFixtureSourceForTest
)

$ErrorActionPreference = "Stop"

$Package = "NEXT_LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_R001"
$OldRunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_20260529T125324Z"
$OutputDir = Join-Path $RepoRoot "artifacts\readiness\$OutputSubdir"
$QubesDir = Join-Path $RepoRoot "artifacts\readiness\real-qubes-core-handoff-to-intraday-consumption-r001"
$QubesMainPath = Join-Path $QubesDir "real-qubes-core-handoff-to-intraday-consumption-r001.json"
$QubesHandoffPath = Join-Path $QubesDir "staging\real-qubes-core-to-intraday-handoff-r001.json"
$QubesDriftPath = Join-Path $QubesDir "real-qubes-drift-and-orders-preview-r001.json"
$QubesOrderPreviewPath = Join-Path $QubesDir "real-qubes-lmax-order-manifest-preview-r001.json"

if ([string]::IsNullOrWhiteSpace($OperatorApprovalPath)) { $OperatorApprovalPath = Join-Path $OutputDir "operator-approval-lmax-demo-execution-r001.json" }
if ([string]::IsNullOrWhiteSpace($ExecutionSwitchPath)) { $ExecutionSwitchPath = Join-Path $OutputDir "lmax-demo-execution-switch-r001.json" }
if ([string]::IsNullOrWhiteSpace($LmaxDemoConfigPath)) { $LmaxDemoConfigPath = Join-Path $OutputDir "real-qubes-lmax-execution-config-validation-r001.json" }
if ([string]::IsNullOrWhiteSpace($AdapterBindingPath)) { $AdapterBindingPath = Join-Path $OutputDir "real-qubes-actual-adapter-binding-r001.json" }

$MainPath = Join-Path $OutputDir "lmax-sandbox-real-qubes-process-test-run-r001.json"
$RunManifestPath = Join-Path $OutputDir "real-qubes-run-manifest-r001.json"
$SourceBindingPath = Join-Path $OutputDir "real-qubes-source-handoff-binding-r001.json"
$OrderManifestPath = Join-Path $OutputDir "real-qubes-lmax-order-manifest-r001.json"
$CompatOrderManifestPath = Join-Path $OutputDir "lmax-order-manifest-r001.json"
$ExecutionAlgoPlanPath = Join-Path $OutputDir "execution-algo-plan-r001.json"
$ApprovalRequiredPath = Join-Path $OutputDir "real-qubes-operator-approval-required-r001.json"
$ApprovalStatusPath = Join-Path $OutputDir "operator-approval-lmax-demo-execution-status-r001.json"
$SwitchRequiredPath = Join-Path $OutputDir "real-qubes-execution-switch-required-r001.json"
$SwitchStatusPath = Join-Path $OutputDir "lmax-demo-execution-switch-status-r001.json"
$ConfigValidationPath = Join-Path $OutputDir "real-qubes-lmax-execution-config-validation-r001.json"
$AdapterBindingOutputPath = Join-Path $OutputDir "real-qubes-actual-adapter-binding-r001.json"
$ExecutionResultPath = Join-Path $OutputDir "sandbox-execution-result-r001.json"
$TradeReconPath = Join-Path $OutputDir "sandbox-trade-level-reconciliation-r001.json"
$PnlPath = Join-Path $OutputDir "sandbox-pnl-r001.json"
$BrokerEvidenceInstructionsPath = Join-Path $OutputDir "same-run-broker-evidence-instructions-r001.md"

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Missing required artifact: $Path" }
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Write-JsonFile([string]$Path, [object]$Value) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Get-Sha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    "sha256:$((Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant())"
}

function Get-OptionalPropertyValue {
    param([AllowNull()] $Object, [Parameter(Mandatory=$true)] [string]$Name, $Default = $null)
    if ($null -eq $Object) { return $Default }
    $prop = $Object.PSObject.Properties[$Name]
    if ($null -eq $prop) { return $Default }
    return $prop.Value
}

function Get-OptionalBooleanProperty {
    param([AllowNull()] $Object, [Parameter(Mandatory=$true)] [string]$Name, [bool]$Default = $false)
    $value = Get-OptionalPropertyValue -Object $Object -Name $Name -Default $Default
    if ($null -eq $value) { return $Default }
    return [bool]$value
}

function Test-RunIdMatches([object]$Object, [string]$ExpectedRunId) {
    $value = [string](Get-OptionalPropertyValue -Object $Object -Name "run_id" -Default "")
    return ($value -eq $ExpectedRunId)
}

function Test-ProductionLikeEndpoint([string]$Endpoint) {
    if ([string]::IsNullOrWhiteSpace($Endpoint)) { return $false }
    return ($Endpoint.ToLowerInvariant() -match "prod|production|live")
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
if ([string]::IsNullOrWhiteSpace($RunId)) {
    if (Test-Path -LiteralPath $MainPath) {
        $existing = Read-JsonFile $MainPath
        $RunId = [string](Get-OptionalPropertyValue -Object $existing -Name "run_id" -Default "")
    }
    if ([string]::IsNullOrWhiteSpace($RunId)) {
        $RunId = "LMAX_SANDBOX_REAL_QUBES_TEST_R001_$((Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ"))"
    }
}
if ($RunId -eq $OldRunId -or $RunId -notmatch "^LMAX_SANDBOX_REAL_QUBES_TEST_R001_\d{8}T\d{6}Z$") {
    throw "RunId must be a new LMAX_SANDBOX_REAL_QUBES_TEST_R001_<UTC_TIMESTAMP> value and must not equal $OldRunId."
}

$qubesMain = Read-JsonFile $QubesMainPath
$qubesHandoff = Read-JsonFile $QubesHandoffPath
$qubesDrift = Read-JsonFile $QubesDriftPath
$qubesOrderPreview = Read-JsonFile $QubesOrderPreviewPath

$orders = @($qubesOrderPreview.orders)
$realHandoffAccepted = (
    [string]$qubesMain.status -eq "REAL_QUBES_CORE_HANDOFF_CONSUMED_AND_ORDER_PREVIEW_READY_R001" -and
    (Get-OptionalBooleanProperty -Object $qubesMain -Name "real_qubes_core_output_accepted" -Default $false) -eq $true -and
    (Get-OptionalBooleanProperty -Object $qubesMain -Name "generated_by_qubes_core" -Default $false) -eq $true -and
    (Get-OptionalBooleanProperty -Object $qubesMain -Name "synthetic_fixture" -Default $true) -eq $false -and
    [string]$qubesHandoff.artifact_type -eq "real_qubes_core_to_intraday_handoff" -and
    [string]$qubesHandoff.status -eq "REAL_QUBES_CORE_TO_INTRADAY_HANDOFF_READY_R001" -and
    (Get-OptionalBooleanProperty -Object $qubesHandoff -Name "generated_by_qubes_core" -Default $false) -eq $true -and
    (Get-OptionalBooleanProperty -Object $qubesHandoff -Name "synthetic_fixture" -Default $true) -eq $false -and
    [decimal]$qubesHandoff.target_notional_usd -gt 0 -and
    [string]$qubesOrderPreview.status -eq "LMAX_ORDER_MANIFEST_PREVIEW_READY_R001" -and
    $orders.Count -eq 7
)
if ($ForceFixtureSourceForTest.IsPresent) { $realHandoffAccepted = $false }

$qubesHandoffHash = Get-Sha256 $QubesHandoffPath
$qubesMainHash = Get-Sha256 $QubesMainPath
$qubesOrderPreviewHash = Get-Sha256 $QubesOrderPreviewPath
$qubesDriftHash = Get-Sha256 $QubesDriftPath

$normalizedOrders = @()
$index = 1
foreach ($order in $orders) {
    $symbol = [string]$order.symbol
    $side = [string]$order.side
    $quantity = [decimal]$order.quantity
    $securityId = [string](Get-OptionalPropertyValue -Object $order -Name "security_id" -Default "")
    $tag22 = [string](Get-OptionalPropertyValue -Object $order -Name "security_id_source_tag22" -Default "")
    if (-not [string]::IsNullOrWhiteSpace($securityId) -and $tag22 -ne "8") {
        throw "Invalid LMAX order preview: tag 22 must equal 8 when SecurityID tag 48 is present."
    }
    if ($quantity -le 0) {
        throw "Invalid LMAX order preview: quantity must be positive."
    }
    $normalizedOrders += [ordered]@{
        run_id = $RunId
        source_qubes_run_id = [string](Get-OptionalPropertyValue -Object $order -Name "run_id" -Default $qubesHandoff.run_id)
        order_index = $index
        internal_order_id = "$RunId-$symbol-$side-$quantity"
        symbol = $symbol
        side = $side
        quantity = $quantity
        target_notional_usd = [decimal](Get-OptionalPropertyValue -Object $order -Name "target_notional_usd" -Default 0)
        security_id = $securityId
        security_id_source_tag22 = $tag22
        tag22_policy_enforced = $true
        production_live = $false
        submit_allowed_without_approval = $false
        preview_only = $true
    }
    $index++
}

$manifestNotionalForRunner = [decimal]0
foreach ($order in $normalizedOrders) { $manifestNotionalForRunner += [Math]::Abs([decimal]$order.quantity) }
$targetNotionalUsd = [decimal]$qubesHandoff.target_notional_usd

$approvalInput = $null
$approvalAccepted = $false
if (-not [string]::IsNullOrWhiteSpace($OperatorApprovalPath) -and (Test-Path -LiteralPath $OperatorApprovalPath)) {
    $approvalInput = Read-JsonFile $OperatorApprovalPath
    $approvedActions = @(Get-OptionalPropertyValue -Object $approvalInput -Name "approved_actions" -Default @())
    $approvalAccepted = (
        [string](Get-OptionalPropertyValue -Object $approvalInput -Name "approval_type" -Default "") -eq "lmax_demo_sandbox_execution_approval" -and
        (Test-RunIdMatches -Object $approvalInput -ExpectedRunId $RunId) -and
        [string](Get-OptionalPropertyValue -Object $approvalInput -Name "environment" -Default "") -eq "sandbox" -and
        [string](Get-OptionalPropertyValue -Object $approvalInput -Name "venue" -Default "") -eq "LMAX_DEMO_OR_SANDBOX" -and
        (Get-OptionalBooleanProperty -Object $approvalInput -Name "explicit_acknowledgement_no_production" -Default $false) -eq $true -and
        -not [string]::IsNullOrWhiteSpace([string](Get-OptionalPropertyValue -Object $approvalInput -Name "approved_by" -Default "")) -and
        -not [string]::IsNullOrWhiteSpace([string](Get-OptionalPropertyValue -Object $approvalInput -Name "approved_at_utc" -Default "")) -and
        -not [string]::IsNullOrWhiteSpace([string](Get-OptionalPropertyValue -Object $approvalInput -Name "approval_sha256" -Default "")) -and
        $approvedActions -contains "submit_lmax_demo_sandbox_orders" -and
        $approvedActions -contains "capture_lmax_demo_sandbox_execution_reports" -and
        $approvedActions -contains "capture_lmax_demo_sandbox_fills" -and
        $approvedActions -contains "reconcile_lmax_demo_sandbox_trade_level" -and
        $approvedActions -contains "compute_lmax_demo_sandbox_strategy_pnl"
    )
}

$switchInput = $null
$switchAccepted = $false
$switchMaxOrderCount = 0
$switchMaxNotionalUsd = [decimal]0
$switchKillSwitchActive = $false
if (-not [string]::IsNullOrWhiteSpace($ExecutionSwitchPath) -and (Test-Path -LiteralPath $ExecutionSwitchPath)) {
    $switchInput = Read-JsonFile $ExecutionSwitchPath
    $switchMaxOrderCount = [int](Get-OptionalPropertyValue -Object $switchInput -Name "max_order_count" -Default 0)
    $switchMaxNotionalUsd = [decimal](Get-OptionalPropertyValue -Object $switchInput -Name "max_notional_usd" -Default 0)
    $switchKillSwitchActive = Get-OptionalBooleanProperty -Object $switchInput -Name "kill_switch_active" -Default $true
    $switchAccepted = (
        (Test-RunIdMatches -Object $switchInput -ExpectedRunId $RunId) -and
        (Get-OptionalBooleanProperty -Object $switchInput -Name "execution_enabled" -Default $false) -eq $true -and
        [string](Get-OptionalPropertyValue -Object $switchInput -Name "environment" -Default "") -eq "sandbox" -and
        [string](Get-OptionalPropertyValue -Object $switchInput -Name "venue" -Default "") -eq "LMAX_DEMO_OR_SANDBOX" -and
        (Get-OptionalBooleanProperty -Object $switchInput -Name "production_live" -Default $true) -eq $false -and
        $switchKillSwitchActive -eq $false -and
        $switchMaxOrderCount -ge $normalizedOrders.Count -and
        $switchMaxNotionalUsd -ge $manifestNotionalForRunner
    )
}

$configInput = $null
$configIssues = @()
if (-not [string]::IsNullOrWhiteSpace($LmaxDemoConfigPath) -and (Test-Path -LiteralPath $LmaxDemoConfigPath)) {
    $configInput = Read-JsonFile $LmaxDemoConfigPath
} else {
    $configIssues += "LMAX demo/sandbox config artifact missing."
}
if ($null -ne $configInput) {
    if ([string](Get-OptionalPropertyValue -Object $configInput -Name "status" -Default "") -ne "LMAX_DEMO_EXECUTION_CONFIG_VALID_R001") { $configIssues += "Config status must be LMAX_DEMO_EXECUTION_CONFIG_VALID_R001." }
    $configEnvironment = [string](Get-OptionalPropertyValue -Object $configInput -Name "environment" -Default "")
    if (-not [string]::IsNullOrWhiteSpace($configEnvironment) -and $configEnvironment -ne "sandbox") { $configIssues += "Config environment must be sandbox when present." }
    $configVenue = [string](Get-OptionalPropertyValue -Object $configInput -Name "venue" -Default "")
    if (-not [string]::IsNullOrWhiteSpace($configVenue) -and $configVenue -ne "LMAX_DEMO_OR_SANDBOX") { $configIssues += "Config venue must be LMAX_DEMO_OR_SANDBOX when present." }
    if (Get-OptionalBooleanProperty -Object $configInput -Name "production_endpoint_detected" -Default $false) { $configIssues += "Production endpoint detected." }
    if (Get-OptionalBooleanProperty -Object $configInput -Name "production_credentials_detected" -Default $false) { $configIssues += "Production credentials detected." }
    if (Get-OptionalBooleanProperty -Object $configInput -Name "raw_secret_values_persisted" -Default $false) { $configIssues += "Raw secret values persisted." }
    if (Get-OptionalBooleanProperty -Object $configInput -Name "raw_secrets_present" -Default $false) { $configIssues += "Raw secrets present." }
    if ((Get-OptionalBooleanProperty -Object $configInput -Name "credential_source_exists" -Default $false) -ne $true) { $configIssues += "Credential source missing." }
    if (Get-OptionalBooleanProperty -Object $configInput -Name "production_live" -Default $false) { $configIssues += "Config production_live must be false." }
    if (Get-OptionalBooleanProperty -Object $configInput -Name "trading_readiness" -Default $false) { $configIssues += "Config trading_readiness must be false." }
}
$configAccepted = ($configIssues.Count -eq 0)

$adapterInput = $null
$adapterIssues = @()
if (-not [string]::IsNullOrWhiteSpace($AdapterBindingPath) -and (Test-Path -LiteralPath $AdapterBindingPath)) {
    $adapterInput = Read-JsonFile $AdapterBindingPath
} else {
    $adapterIssues += "Actual adapter binding artifact missing."
}
if ($null -ne $adapterInput) {
    $labels = @(Get-OptionalPropertyValue -Object $adapterInput -Name "required_secret_labels" -Default @())
    $requiredLabels = @(
        "QQ_LMAX_DEMO_FIX_ENDPOINT",
        "QQ_LMAX_DEMO_FIX_SENDER_COMP_ID",
        "QQ_LMAX_DEMO_FIX_TARGET_COMP_ID",
        "QQ_LMAX_DEMO_FIX_USERNAME",
        "QQ_LMAX_DEMO_FIX_PASSWORD"
    )
    foreach ($label in $requiredLabels) {
        if ($labels -notcontains $label) { $adapterIssues += "Required secret label missing from adapter binding: $label" }
    }
    if ((Get-OptionalBooleanProperty -Object $adapterInput -Name "adapter_enabled" -Default $false) -ne $true) { $adapterIssues += "Adapter is not enabled." }
    if ([string](Get-OptionalPropertyValue -Object $adapterInput -Name "adapter_mode" -Default "") -ne "actual_lmax_demo_fix") { $adapterIssues += "Adapter mode must be actual_lmax_demo_fix." }
    if ([string](Get-OptionalPropertyValue -Object $adapterInput -Name "environment" -Default "") -ne "sandbox") { $adapterIssues += "Adapter environment must be sandbox." }
    if (Get-OptionalBooleanProperty -Object $adapterInput -Name "production_live" -Default $true) { $adapterIssues += "Adapter production_live must be false." }
    if (Get-OptionalBooleanProperty -Object $adapterInput -Name "production_endpoint_allowed" -Default $true) { $adapterIssues += "Adapter must not allow production endpoints." }
    if (Get-OptionalBooleanProperty -Object $adapterInput -Name "raw_secrets_persisted" -Default $false) { $adapterIssues += "Adapter raw secrets persisted flag is true." }
}
$adapterAccepted = ($adapterIssues.Count -eq 0)

$approvalStatus = if ($approvalAccepted) { "LMAX_DEMO_EXECUTION_APPROVAL_ACCEPTED_R001" } else { "LMAX_DEMO_EXECUTION_APPROVAL_REQUIRED_R001" }
$switchStatus = if ($switchAccepted) { "LMAX_DEMO_EXECUTION_SWITCH_ACCEPTED_R001" } else { "LMAX_DEMO_EXECUTION_SWITCH_REQUIRED_R001" }
$configStatus = if ($configAccepted) { "LMAX_DEMO_EXECUTION_CONFIG_VALID_R001" } else { "LMAX_DEMO_EXECUTION_CONFIG_REQUIRED_R001" }
$adapterStatus = if ($adapterAccepted) { "LMAX_DEMO_ACTUAL_ADAPTER_BINDING_VALID_R001" } else { "LMAX_DEMO_ACTUAL_ADAPTER_BINDING_REQUIRED_R001" }

$status = "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_BLOCKED_OPERATOR_APPROVAL_REQUIRED_R001"
$executionStatus = "BLOCKED_OPERATOR_APPROVAL_REQUIRED"
if (-not $realHandoffAccepted) {
    $status = "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_BLOCKED_REAL_QUBES_HANDOFF_REQUIRED_R001"
    $executionStatus = "BLOCKED_REAL_QUBES_HANDOFF_REQUIRED"
} elseif (-not $approvalAccepted) {
    $status = "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_BLOCKED_OPERATOR_APPROVAL_REQUIRED_R001"
    $executionStatus = "BLOCKED_OPERATOR_APPROVAL_REQUIRED"
} elseif (-not $switchAccepted) {
    $status = "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_BLOCKED_EXECUTION_SWITCH_REQUIRED_R001"
    $executionStatus = "BLOCKED_EXECUTION_SWITCH_REQUIRED"
} elseif (-not $configAccepted) {
    $status = "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_BLOCKED_LMAX_SANDBOX_CONFIG_REQUIRED_R001"
    $executionStatus = "BLOCKED_LMAX_SANDBOX_CONFIG_REQUIRED"
} elseif (-not $adapterAccepted) {
    $status = "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_BLOCKED_ACTUAL_ADAPTER_BINDING_REQUIRED_R001"
    $executionStatus = "BLOCKED_ACTUAL_ADAPTER_BINDING_REQUIRED"
} else {
    $status = "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_EXECUTION_APPROVED_READY_R001"
    $executionStatus = "APPROVED_READY_NOT_EXECUTED_BY_BUILD_SCRIPT"
}

$sourceBinding = [ordered]@{
    package = $Package
    artifact_type = "real_qubes_source_handoff_binding"
    status = if ($realHandoffAccepted) { "REAL_QUBES_SOURCE_HANDOFF_BOUND_R001" } else { "BLOCKED_REAL_QUBES_HANDOFF_REQUIRED_R001" }
    run_id = $RunId
    source_package = "NEXT_REAL_QUBES_CORE_HANDOFF_TO_INTRADAY_CONSUMPTION_R001"
    source_status = [string]$qubesMain.status
    source_handoff_path = $QubesHandoffPath
    source_handoff_hash = $qubesHandoffHash
    source_order_preview_hash = $qubesOrderPreviewHash
    source_drift_preview_hash = $qubesDriftHash
    real_qubes_core_output_accepted = $realHandoffAccepted
    generated_by_qubes_core = (Get-OptionalBooleanProperty -Object $qubesHandoff -Name "generated_by_qubes_core" -Default $false)
    synthetic_fixture = (Get-OptionalBooleanProperty -Object $qubesHandoff -Name "synthetic_fixture" -Default $true)
    target_notional_usd = $targetNotionalUsd
    source_qubes_run_id = [string]$qubesHandoff.run_id
    order_count = $normalizedOrders.Count
    existing_lmax_run_attribution = $qubesMain.existing_lmax_run_attribution
    production_live = $false
    trading_readiness = $false
}
Write-JsonFile $SourceBindingPath $sourceBinding

$orderManifest = [ordered]@{
    package = $Package
    artifact_type = "real_qubes_lmax_order_manifest"
    status = "REAL_QUBES_LMAX_ORDER_MANIFEST_READY_PREVIEW_ONLY_R001"
    run_id = $RunId
    source_handoff_hash = $qubesHandoffHash
    source_order_preview_hash = $qubesOrderPreviewHash
    preview_only = $true
    no_lmax_call = $true
    order_count = $normalizedOrders.Count
    short_clordid_policy = [ordered]@{
        max_external_cl_ord_id_length = 20
        deterministic = $true
        unique_within_run = $true
    }
    tag22_policy = [ordered]@{
        enforce_tag_22_equals_8_when_security_id_present = $true
    }
    orders = $normalizedOrders
    production_live = $false
    trading_readiness = $false
}
Write-JsonFile $OrderManifestPath $orderManifest
Write-JsonFile $CompatOrderManifestPath $orderManifest

$executionAlgo = [ordered]@{
    package = $Package
    artifact_type = "real_qubes_lmax_demo_execution_algo_plan"
    status = "REAL_QUBES_LMAX_EXECUTION_ALGO_PLAN_READY_R001"
    run_id = $RunId
    algorithm_name = "lmax_demo_sandbox_order_entry_with_residual_zero_gate"
    input_order_manifest = $OrderManifestPath
    order_count = $normalizedOrders.Count
    no_lmax_call_in_build = $true
    no_live_routing = $true
    tag_21_handlinst_forbidden = $true
    tag_22_equals_8_required_when_tag_48_present = $true
    short_clordid_policy = $orderManifest.short_clordid_policy
    fill_capture_policy = "actual_lmax_demo_fix_runner_only_after_operator_approval_and_switch"
    residual_zero_policy = "required_for_reconciled_status"
    same_run_broker_evidence_policy = "blocked_until_operator_exports_same_run_lmax_statement_or_report"
    production_live = $false
    trading_readiness = $false
}
Write-JsonFile $ExecutionAlgoPlanPath $executionAlgo

$approvalRequired = [ordered]@{
    package = $Package
    artifact_type = "real_qubes_lmax_demo_operator_approval_required"
    run_id = $RunId
    required_status_to_execute = "LMAX_DEMO_EXECUTION_APPROVAL_ACCEPTED_R001"
    approval_artifact_path = if ([string]::IsNullOrWhiteSpace($OperatorApprovalPath)) { Join-Path $OutputDir "operator-approval-lmax-demo-execution-r001.json" } else { $OperatorApprovalPath }
    required_actions = @(
        "submit_lmax_demo_sandbox_orders",
        "capture_lmax_demo_sandbox_execution_reports",
        "capture_lmax_demo_sandbox_fills",
        "flatten_lmax_demo_sandbox_positions_if_required",
        "reconcile_lmax_demo_sandbox_trade_level",
        "compute_lmax_demo_sandbox_strategy_pnl"
    )
    accepted = $approvalAccepted
    status = $approvalStatus
    production_live = $false
    trading_readiness = $false
}
Write-JsonFile $ApprovalRequiredPath $approvalRequired
Write-JsonFile $ApprovalStatusPath ([ordered]@{
    package = $Package
    artifact_type = "lmax_demo_execution_approval_status"
    run_id = $RunId
    status = $approvalStatus
    accepted = $approvalAccepted
    source_approval_path = $OperatorApprovalPath
    approval_hash = if ($OperatorApprovalPath) { Get-Sha256 $OperatorApprovalPath } else { $null }
    production_live = $false
    trading_readiness = $false
})

$switchRequired = [ordered]@{
    package = $Package
    artifact_type = "real_qubes_lmax_demo_execution_switch_required"
    run_id = $RunId
    required_status_to_execute = "LMAX_DEMO_EXECUTION_SWITCH_ACCEPTED_R001"
    switch_artifact_path = if ([string]::IsNullOrWhiteSpace($ExecutionSwitchPath)) { Join-Path $OutputDir "lmax-demo-execution-switch-r001.json" } else { $ExecutionSwitchPath }
    accepted = $switchAccepted
    status = $switchStatus
    max_order_count = $switchMaxOrderCount
    max_notional_usd = $switchMaxNotionalUsd
    manifest_notional_for_runner = $manifestNotionalForRunner
    kill_switch_active = $switchKillSwitchActive
    production_live = $false
    trading_readiness = $false
}
Write-JsonFile $SwitchRequiredPath $switchRequired
Write-JsonFile $SwitchStatusPath ([ordered]@{
    package = $Package
    artifact_type = "lmax_demo_execution_switch_status"
    run_id = $RunId
    status = $switchStatus
    execution_enabled = $switchAccepted
    max_order_count = $switchMaxOrderCount
    max_notional_usd = $switchMaxNotionalUsd
    kill_switch_active = $switchKillSwitchActive
    source_switch_path = $ExecutionSwitchPath
    production_live = $false
    trading_readiness = $false
})

$configEndpoint = if ($null -ne $configInput) { [string](Get-OptionalPropertyValue -Object $configInput -Name "endpoint_config_reference" -Default "") } else { "" }
$configInputIsOutput = $false
if ($null -ne $configInput) {
    $configInputIsOutput = ((Resolve-Path -LiteralPath $LmaxDemoConfigPath).Path -eq (Resolve-Path -LiteralPath $ConfigValidationPath -ErrorAction SilentlyContinue).Path)
}
if (-not ($configAccepted -and $configInputIsOutput)) {
    Write-JsonFile $ConfigValidationPath ([ordered]@{
        package = $Package
        artifact_type = "real_qubes_lmax_execution_config_validation"
        run_id = $RunId
        status = $configStatus
        environment = "sandbox"
        venue = "LMAX_DEMO_OR_SANDBOX"
        source_config_path = $LmaxDemoConfigPath
        endpoint_config_reference = $configEndpoint
        production_endpoint_detected = if ($null -ne $configInput) { Get-OptionalBooleanProperty -Object $configInput -Name "production_endpoint_detected" -Default (Test-ProductionLikeEndpoint $configEndpoint) } else { $false }
        production_credentials_detected = if ($null -ne $configInput) { Get-OptionalBooleanProperty -Object $configInput -Name "production_credentials_detected" -Default $false } else { $false }
        credential_source_exists = if ($null -ne $configInput) { Get-OptionalBooleanProperty -Object $configInput -Name "credential_source_exists" -Default $false } else { $false }
        tls_required = if ($null -ne $configInput) { Get-OptionalBooleanProperty -Object $configInput -Name "tls_required" -Default $false } else { $false }
        raw_secret_values_persisted = if ($null -ne $configInput) { Get-OptionalBooleanProperty -Object $configInput -Name "raw_secret_values_persisted" -Default $false } else { $false }
        raw_secrets_present = if ($null -ne $configInput) { Get-OptionalBooleanProperty -Object $configInput -Name "raw_secrets_present" -Default $false } else { $false }
        no_raw_secrets_in_artifacts = $true
        validation_issues = $configIssues
        production_live = $false
        trading_readiness = $false
    })
}

if ($null -ne $adapterInput) {
    $adapterOutput = $adapterInput
} else {
    $adapterOutput = [ordered]@{
        artifact_type = "real_qubes_actual_adapter_binding"
        run_id = $RunId
        environment = "sandbox"
        venue = "LMAX_DEMO_OR_SANDBOX"
        adapter_mode = "actual_lmax_demo_fix"
        adapter_enabled = $false
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
            orders_log = "artifacts/readiness/$OutputSubdir/logs/$RunId/orders.log"
            execution_reports_log = "artifacts/readiness/$OutputSubdir/logs/$RunId/execution-reports.log"
        }
    }
}
Write-JsonFile $AdapterBindingOutputPath $adapterOutput

$runManifest = [ordered]@{
    package = $Package
    artifact_type = "lmax_sandbox_real_qubes_process_run_manifest"
    run_id = $RunId
    environment = "sandbox"
    venue = "LMAX_DEMO_OR_SANDBOX"
    production_live = $false
    trading_readiness = $false
    target_notional_usd = $targetNotionalUsd
    source_systems = @("QQ.Production.Core / Anubis", "QQ.Production.Intraday")
    source_handoff_hash = $qubesHandoffHash
    source_order_preview_hash = $qubesOrderPreviewHash
    operator_approval_status = $approvalStatus
    execution_switch_status = $switchStatus
    lmax_demo_execution_config_status = $configStatus
    actual_adapter_binding_status = $adapterStatus
    lmax_demo_execution_config_issues = $configIssues
    actual_adapter_binding_issues = $adapterIssues
    execution_enabled_by_build_script = $false
    expected_artifacts = @(
        "real-qubes-source-handoff-binding-r001.json",
        "real-qubes-lmax-order-manifest-r001.json",
        "sandbox-execution-result-r001.json",
        "sandbox-trade-level-reconciliation-r001.json",
        "sandbox-pnl-r001.json"
    )
    no_production_guarantees = [ordered]@{
        no_production_lmax = $true
        no_live_credentials = $true
        no_production_db_mutation = $true
        no_production_ledger_commit = $true
        production_live_ready = $false
        trading_readiness_ready = $false
    }
}
Write-JsonFile $RunManifestPath $runManifest

Write-JsonFile $ExecutionResultPath ([ordered]@{
    package = $Package
    artifact_type = "real_qubes_sandbox_execution_result"
    run_id = $RunId
    status = $executionStatus
    execution_mode = "not_executed_by_build_script"
    orders_submitted_count = 0
    execution_reports_count = 0
    fills_count = 0
    rejected_count = 0
    cancelled_count = 0
    residual_zero = $false
    lmax_fix_api_call = $false
    broker_api_call = $false
    production_live = $false
    trading_readiness = $false
})

Write-JsonFile $TradeReconPath ([ordered]@{
    package = $Package
    artifact_type = "real_qubes_sandbox_trade_level_reconciliation"
    run_id = $RunId
    status = "BLOCKED_AWAITING_LMAX_DEMO_SANDBOX_EXECUTION"
    source_order_manifest = $OrderManifestPath
    same_run_broker_evidence_status = "BLOCKED_SAME_RUN_BROKER_EXPORT_MISSING"
    production_live = $false
    trading_readiness = $false
})

Write-JsonFile $PnlPath ([ordered]@{
    package = $Package
    artifact_type = "real_qubes_sandbox_strategy_pnl"
    run_id = $RunId
    status = "BLOCKED_AWAITING_LMAX_DEMO_SANDBOX_FILLS"
    broker_statement_pnl_comparison = "not_applicable_until_same_run_lmax_export_exists"
    production_live = $false
    trading_readiness = $false
})

@"
# Same-Run Broker Evidence Instructions R001

After the LMAX demo/sandbox execution runner completes for `$RunId`, export the LMAX statement or execution report for the same run window/order set and stage it as same-run broker evidence.

The existing historical LMAX statement is not used as same-run evidence for this real Qubes/Core run.

No production/live readiness or trading readiness is granted by this package.
"@ | Set-Content -LiteralPath $BrokerEvidenceInstructionsPath -Encoding UTF8

$main = [ordered]@{
    package = $Package
    artifact_type = "lmax_sandbox_real_qubes_process_test_run"
    status = $status
    run_id = $RunId
    source_real_qubes_package_status = [string]$qubesMain.status
    source_real_qubes_handoff_hash = $qubesHandoffHash
    source_real_qubes_order_preview_hash = $qubesOrderPreviewHash
    generated_by_qubes_core = (Get-OptionalBooleanProperty -Object $qubesHandoff -Name "generated_by_qubes_core" -Default $false)
    synthetic_fixture = (Get-OptionalBooleanProperty -Object $qubesHandoff -Name "synthetic_fixture" -Default $true)
    real_qubes_handoff_accepted = $realHandoffAccepted
    old_run_id = $OldRunId
    old_run_overwritten = $false
    order_count = $normalizedOrders.Count
    front_half_status = if ($realHandoffAccepted) { "REAL_QUBES_ORDER_PREVIEW_READY" } else { "BLOCKED_REAL_QUBES_HANDOFF_REQUIRED" }
    execution_status = $executionStatus
    fill_status = "NO_FILLS_BLOCKED_OR_NOT_EXECUTED"
    fills_count = 0
    flatten_status = "NOT_RUN"
    residual_zero = $false
    trade_level_reconciliation_status = "BLOCKED_AWAITING_LMAX_DEMO_SANDBOX_EXECUTION"
    strategy_pnl_status = "BLOCKED_AWAITING_LMAX_DEMO_SANDBOX_FILLS"
    same_run_broker_evidence_status = "BLOCKED_SAME_RUN_BROKER_EXPORT_MISSING"
    operator_approval_status = $approvalStatus
    lmax_demo_execution_switch_status = $switchStatus
    lmax_demo_execution_config_status = $configStatus
    actual_adapter_binding_status = $adapterStatus
    lmax_demo_execution_config_issues = $configIssues
    actual_adapter_binding_issues = $adapterIssues
    lmax_demo_execution_ready = ($status -eq "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_EXECUTION_APPROVED_READY_R001")
    production_live = $false
    trading_readiness = $false
    ready_outputs = [ordered]@{
        real_qubes_run_manifest_ready = $true
        real_qubes_source_handoff_binding_ready = $true
        real_qubes_lmax_order_manifest_ready = $realHandoffAccepted
        execution_harness_ready = $true
        sandbox_execution_ready = $false
    }
    still_blocked = @(
        "operator_approval_or_execution_switch_or_config_until_ready",
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
}
Write-JsonFile $MainPath $main

Write-Host "Created real-Qubes LMAX sandbox process run $RunId with status $status"
