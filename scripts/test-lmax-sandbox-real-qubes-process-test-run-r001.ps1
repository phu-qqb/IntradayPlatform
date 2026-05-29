param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$BuildScript = Join-Path $RepoRoot "scripts\build-lmax-sandbox-real-qubes-process-test-run-r001.ps1"
$RunnerScript = Join-Path $RepoRoot "scripts\run-lmax-demo-sandbox-execution-r001.ps1"
$OldRunMainPath = Join-Path $RepoRoot "artifacts\readiness\lmax-sandbox-global-process-test-run-r001\lmax-sandbox-global-process-test-run-r001.json"

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -ne $Actual) { throw "$Message Expected=[$Expected] Actual=[$Actual]" }
}

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Missing expected artifact: $Path" }
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Write-JsonFile([string]$Path, [object]$Value) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Get-Sha256OrNull([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

function New-TestApproval([string]$Path, [string]$RunId) {
    Write-JsonFile $Path ([ordered]@{
        approval_type = "lmax_demo_sandbox_execution_approval"
        run_id = $RunId
        environment = "sandbox"
        venue = "LMAX_DEMO_OR_SANDBOX"
        simulation_already_reconciled = $false
        approved_actions = @(
            "submit_lmax_demo_sandbox_orders",
            "capture_lmax_demo_sandbox_execution_reports",
            "capture_lmax_demo_sandbox_fills",
            "flatten_lmax_demo_sandbox_positions_if_required",
            "reconcile_lmax_demo_sandbox_trade_level",
            "compute_lmax_demo_sandbox_strategy_pnl"
        )
        explicit_acknowledgement_no_production = $true
        approved_by = "codex-test"
        approved_at_utc = "2026-05-29T00:00:00Z"
        approval_sha256 = "sha256:test-approval"
    })
}

function New-TestSwitch([string]$Path, [string]$RunId, [int]$MaxOrderCount = 7) {
    Write-JsonFile $Path ([ordered]@{
        run_id = $RunId
        execution_enabled = $true
        environment = "sandbox"
        venue = "LMAX_DEMO_OR_SANDBOX"
        production_live = $false
        max_order_count = $MaxOrderCount
        max_notional_usd = 6000000
        kill_switch_active = $false
        created_by = "codex-test"
        created_at_utc = "2026-05-29T00:00:00Z"
    })
}

function New-TestConfig([string]$Path, [string]$RunId) {
    Write-JsonFile $Path ([ordered]@{
        artifact_type = "lmax_demo_execution_config"
        run_id = $RunId
        status = "LMAX_DEMO_EXECUTION_CONFIG_VALID_R001"
        environment = "sandbox"
        venue = "LMAX_DEMO_OR_SANDBOX"
        endpoint_config_reference = "QQ_LMAX_DEMO_FIX_ENDPOINT"
        credential_source_exists = $true
        tls_required = $true
        production_endpoint_detected = $false
        production_credentials_detected = $false
        raw_secret_values_persisted = $false
        raw_secrets_present = $false
        no_raw_secrets_in_artifacts = $true
    })
}

function New-TestAdapterBinding([string]$Path, [string]$RunId) {
    Write-JsonFile $Path ([ordered]@{
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
    })
}

$oldHashBefore = Get-Sha256OrNull $OldRunMainPath

$defaultSubdir = "lmax-sandbox-real-qubes-process-test-run-r001-test-default"
$defaultRunId = "LMAX_SANDBOX_REAL_QUBES_TEST_R001_20260529T000001Z"
& $BuildScript -OutputSubdir $defaultSubdir -RunId $defaultRunId
$defaultMainPath = Join-Path $RepoRoot "artifacts\readiness\$defaultSubdir\lmax-sandbox-real-qubes-process-test-run-r001.json"
$defaultMain = Read-JsonFile $defaultMainPath
Assert-Equal "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_BLOCKED_OPERATOR_APPROVAL_REQUIRED_R001" $defaultMain.status "Missing approval should block the real-Qubes run."
Assert-Equal 7 $defaultMain.order_count "Real-Qubes run must use the 7 order preview."
Assert-True ($defaultMain.generated_by_qubes_core -eq $true) "Real Qubes generated flag should be preserved."
Assert-True ($defaultMain.synthetic_fixture -eq $false) "Real Qubes synthetic fixture flag should remain false."
Assert-True ($defaultMain.global_guards.lmax_fix_api_call -eq $false) "Build must not call LMAX."

$fixtureSubdir = "lmax-sandbox-real-qubes-process-test-run-r001-test-fixture-block"
$fixtureRunId = "LMAX_SANDBOX_REAL_QUBES_TEST_R001_20260529T000002Z"
& $BuildScript -OutputSubdir $fixtureSubdir -RunId $fixtureRunId -ForceFixtureSourceForTest
$fixtureMain = Read-JsonFile (Join-Path $RepoRoot "artifacts\readiness\$fixtureSubdir\lmax-sandbox-real-qubes-process-test-run-r001.json")
Assert-Equal "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_BLOCKED_REAL_QUBES_HANDOFF_REQUIRED_R001" $fixtureMain.status "Fixture or forced fixture source must not be accepted as real Qubes handoff."

$approvedSubdir = "lmax-sandbox-real-qubes-process-test-run-r001-test-approved"
$approvedRunId = "LMAX_SANDBOX_REAL_QUBES_TEST_R001_20260529T000003Z"
$approvedDir = Join-Path $RepoRoot "artifacts\readiness\$approvedSubdir"
$approvalPath = Join-Path $approvedDir "operator-approval-lmax-demo-execution-r001.json"
$switchPath = Join-Path $approvedDir "lmax-demo-execution-switch-r001.json"
$configPath = Join-Path $approvedDir "lmax-demo-execution-config-r001.json"
$adapterPath = Join-Path $approvedDir "operator-adapter-binding-r001.json"
New-TestApproval $approvalPath $approvedRunId
New-TestSwitch $switchPath $approvedRunId
New-TestConfig $configPath $approvedRunId
New-TestAdapterBinding $adapterPath $approvedRunId

& $BuildScript `
    -OutputSubdir $approvedSubdir `
    -RunId $approvedRunId `
    -OperatorApprovalPath $approvalPath `
    -ExecutionSwitchPath $switchPath `
    -LmaxDemoConfigPath $configPath `
    -AdapterBindingPath $adapterPath

$approvedMainPath = Join-Path $approvedDir "lmax-sandbox-real-qubes-process-test-run-r001.json"
$approvedMain = Read-JsonFile $approvedMainPath
Assert-Equal "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_EXECUTION_APPROVED_READY_R001" $approvedMain.status "Approval, switch, config, and adapter should reach approved-ready."
Assert-Equal 7 $approvedMain.order_count "Approved-ready real-Qubes run must keep the 7-order manifest."

$defaultValidSubdir = "lmax-sandbox-real-qubes-process-test-run-r001-test-default-valid"
$defaultValidRunId = "LMAX_SANDBOX_REAL_QUBES_TEST_R001_20260529T000004Z"
$defaultValidDir = Join-Path $RepoRoot "artifacts\readiness\$defaultValidSubdir"
New-TestApproval (Join-Path $defaultValidDir "operator-approval-lmax-demo-execution-r001.json") $defaultValidRunId
New-TestSwitch (Join-Path $defaultValidDir "lmax-demo-execution-switch-r001.json") $defaultValidRunId
New-TestConfig (Join-Path $defaultValidDir "real-qubes-lmax-execution-config-validation-r001.json") $defaultValidRunId
New-TestAdapterBinding (Join-Path $defaultValidDir "real-qubes-actual-adapter-binding-r001.json") $defaultValidRunId
$configHashBefore = Get-Sha256OrNull (Join-Path $defaultValidDir "real-qubes-lmax-execution-config-validation-r001.json")
$adapterHashBefore = Get-Sha256OrNull (Join-Path $defaultValidDir "real-qubes-actual-adapter-binding-r001.json")
& $BuildScript -OutputSubdir $defaultValidSubdir -RunId $defaultValidRunId
$defaultValidMain = Read-JsonFile (Join-Path $defaultValidDir "lmax-sandbox-real-qubes-process-test-run-r001.json")
Assert-Equal "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_EXECUTION_APPROVED_READY_R001" $defaultValidMain.status "Default real-Qubes config and adapter paths should be consumed."
Assert-Equal "LMAX_DEMO_EXECUTION_CONFIG_VALID_R001" $defaultValidMain.lmax_demo_execution_config_status "Valid config status should be preserved."
Assert-Equal "LMAX_DEMO_ACTUAL_ADAPTER_BINDING_VALID_R001" $defaultValidMain.actual_adapter_binding_status "Valid adapter binding should be preserved."
Assert-Equal $configHashBefore (Get-Sha256OrNull (Join-Path $defaultValidDir "real-qubes-lmax-execution-config-validation-r001.json")) "Valid real-Qubes config artifact must not be overwritten."
Assert-Equal $adapterHashBefore (Get-Sha256OrNull (Join-Path $defaultValidDir "real-qubes-actual-adapter-binding-r001.json")) "Valid real-Qubes adapter artifact must not be overwritten."

$missingCredentialSubdir = "lmax-sandbox-real-qubes-process-test-run-r001-test-config-missing-credential"
$missingCredentialRunId = "LMAX_SANDBOX_REAL_QUBES_TEST_R001_20260529T000005Z"
$missingCredentialDir = Join-Path $RepoRoot "artifacts\readiness\$missingCredentialSubdir"
New-TestApproval (Join-Path $missingCredentialDir "operator-approval-lmax-demo-execution-r001.json") $missingCredentialRunId
New-TestSwitch (Join-Path $missingCredentialDir "lmax-demo-execution-switch-r001.json") $missingCredentialRunId
New-TestConfig (Join-Path $missingCredentialDir "real-qubes-lmax-execution-config-validation-r001.json") $missingCredentialRunId
$badConfig = Read-JsonFile (Join-Path $missingCredentialDir "real-qubes-lmax-execution-config-validation-r001.json")
$badConfig.credential_source_exists = $false
Write-JsonFile (Join-Path $missingCredentialDir "real-qubes-lmax-execution-config-validation-r001.json") $badConfig
New-TestAdapterBinding (Join-Path $missingCredentialDir "real-qubes-actual-adapter-binding-r001.json") $missingCredentialRunId
& $BuildScript -OutputSubdir $missingCredentialSubdir -RunId $missingCredentialRunId
$missingCredentialMain = Read-JsonFile (Join-Path $missingCredentialDir "lmax-sandbox-real-qubes-process-test-run-r001.json")
Assert-Equal "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_BLOCKED_LMAX_SANDBOX_CONFIG_REQUIRED_R001" $missingCredentialMain.status "Missing credential source should block config acceptance."
Assert-True (@($missingCredentialMain.lmax_demo_execution_config_issues) -contains "Credential source missing.") "Config issues should name missing credential source."

$oldEnv = @{
    QQ_LMAX_DEMO_FIX_ENDPOINT = [Environment]::GetEnvironmentVariable("QQ_LMAX_DEMO_FIX_ENDPOINT")
    QQ_LMAX_DEMO_FIX_SENDER_COMP_ID = [Environment]::GetEnvironmentVariable("QQ_LMAX_DEMO_FIX_SENDER_COMP_ID")
    QQ_LMAX_DEMO_FIX_TARGET_COMP_ID = [Environment]::GetEnvironmentVariable("QQ_LMAX_DEMO_FIX_TARGET_COMP_ID")
    QQ_LMAX_DEMO_FIX_USERNAME = [Environment]::GetEnvironmentVariable("QQ_LMAX_DEMO_FIX_USERNAME")
    QQ_LMAX_DEMO_FIX_PASSWORD = [Environment]::GetEnvironmentVariable("QQ_LMAX_DEMO_FIX_PASSWORD")
}
try {
    [Environment]::SetEnvironmentVariable("QQ_LMAX_DEMO_FIX_ENDPOINT", "localhost:443")
    [Environment]::SetEnvironmentVariable("QQ_LMAX_DEMO_FIX_SENDER_COMP_ID", "TESTSENDER")
    [Environment]::SetEnvironmentVariable("QQ_LMAX_DEMO_FIX_TARGET_COMP_ID", "LMXBD")
    [Environment]::SetEnvironmentVariable("QQ_LMAX_DEMO_FIX_USERNAME", "test-user")
    [Environment]::SetEnvironmentVariable("QQ_LMAX_DEMO_FIX_PASSWORD", "test-password")

    & $RunnerScript `
        -OutputSubdir $approvedSubdir `
        -RunId $approvedRunId `
        -MainArtifactName "lmax-sandbox-real-qubes-process-test-run-r001.json" `
        -ApprovedReadyStatus "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_EXECUTION_APPROVED_READY_R001" `
        -ReconciledStatus "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_RECONCILED_R001" `
        -ApprovalPath (Join-Path $approvedDir "operator-approval-lmax-demo-execution-status-r001.json") `
        -ExecutionSwitchPath (Join-Path $approvedDir "lmax-demo-execution-switch-status-r001.json") `
        -ConfigValidationPath (Join-Path $approvedDir "real-qubes-lmax-execution-config-validation-r001.json") `
        -OrderManifestPath (Join-Path $approvedDir "real-qubes-lmax-order-manifest-r001.json") `
        -ExecutionAlgoPlanPath (Join-Path $approvedDir "execution-algo-plan-r001.json") `
        -AdapterBindingPath (Join-Path $approvedDir "real-qubes-actual-adapter-binding-r001.json") `
        -ExecuteLmaxDemoSandboxOrders `
        -UseActualLmaxFixClient `
        -UseMockFixServer
} finally {
    foreach ($key in $oldEnv.Keys) {
        [Environment]::SetEnvironmentVariable($key, $oldEnv[$key])
    }
}

$mockMain = Read-JsonFile $approvedMainPath
Assert-Equal "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_RECONCILED_R001" $mockMain.status "Mock FIX path should reconcile the real-Qubes run."
Assert-Equal 7 ([int]$mockMain.fills_count) "Mock FIX path should create one fill per real-Qubes order."
Assert-True ($mockMain.residual_zero -eq $true) "Mock FIX path should reach residual zero."
Assert-Equal "LMAX_DEMO_SANDBOX_TRADE_LEVEL_RECONCILIATION_READY_R001" $mockMain.trade_level_reconciliation_status "Trade-level reconciliation should use mock captured FIX fills."
Assert-Equal "LMAX_DEMO_SANDBOX_STRATEGY_PNL_READY_R001" $mockMain.strategy_pnl_status "Strategy PnL should be ready after mock fills."
Assert-Equal "BLOCKED_SAME_RUN_BROKER_EXPORT_MISSING" $mockMain.same_run_broker_evidence_status "Same-run broker evidence must remain blocked until export exists."
Assert-True ($mockMain.production_live -eq $false) "Production/live must remain false."
Assert-True ($mockMain.trading_readiness -eq $false) "Trading readiness must remain false."

$clOrdMap = Read-JsonFile (Join-Path $approvedDir "lmax-demo-clordid-map-r001.json")
$externalIds = @($clOrdMap.mappings | ForEach-Object { [string]$_.external_cl_ord_id })
Assert-Equal 7 $externalIds.Count "ClOrdID map should contain seven mappings."
Assert-Equal 7 @($externalIds | Select-Object -Unique).Count "External ClOrdIDs must be unique."
foreach ($id in $externalIds) {
    Assert-True ($id.Length -le 20) "External ClOrdID [$id] exceeds 20 characters."
}

$ordersLog = Get-Content -Raw -LiteralPath (Join-Path $approvedDir "logs\$approvedRunId\attempt-001\orders.log")
Assert-True ($ordersLog -notmatch "21=1") "LMAX NewOrderSingle messages must not emit tag 21."
Assert-True ($ordersLog -match "22=8") "LMAX NewOrderSingle messages with tag 48 must preserve tag 22=8."
Assert-True ($ordersLog -notmatch "test-password") "Raw password must not be logged."

$retrySubdir = "lmax-sandbox-real-qubes-process-test-run-r001-test-residual-retry"
$retryRunId = "LMAX_SANDBOX_REAL_QUBES_TEST_R001_20260529T000006Z"
$retryDir = Join-Path $RepoRoot "artifacts\readiness\$retrySubdir"
$retryApprovalPath = Join-Path $retryDir "operator-approval-lmax-demo-execution-r001.json"
$retrySwitchPath = Join-Path $retryDir "lmax-demo-execution-switch-r001.json"
$retryConfigPath = Join-Path $retryDir "real-qubes-lmax-execution-config-validation-r001.json"
$retryAdapterPath = Join-Path $retryDir "real-qubes-actual-adapter-binding-r001.json"
New-TestApproval $retryApprovalPath $retryRunId
New-TestSwitch $retrySwitchPath $retryRunId
New-TestConfig $retryConfigPath $retryRunId
New-TestAdapterBinding $retryAdapterPath $retryRunId
& $BuildScript -OutputSubdir $retrySubdir -RunId $retryRunId
try {
    [Environment]::SetEnvironmentVariable("QQ_LMAX_DEMO_FIX_ENDPOINT", "localhost:443")
    [Environment]::SetEnvironmentVariable("QQ_LMAX_DEMO_FIX_SENDER_COMP_ID", "TESTSENDER")
    [Environment]::SetEnvironmentVariable("QQ_LMAX_DEMO_FIX_TARGET_COMP_ID", "LMXBD")
    [Environment]::SetEnvironmentVariable("QQ_LMAX_DEMO_FIX_USERNAME", "test-user")
    [Environment]::SetEnvironmentVariable("QQ_LMAX_DEMO_FIX_PASSWORD", "test-password")

    & $RunnerScript `
        -OutputSubdir $retrySubdir `
        -RunId $retryRunId `
        -MainArtifactName "lmax-sandbox-real-qubes-process-test-run-r001.json" `
        -ApprovedReadyStatus "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_EXECUTION_APPROVED_READY_R001" `
        -ReconciledStatus "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_RECONCILED_R001" `
        -ApprovalPath (Join-Path $retryDir "operator-approval-lmax-demo-execution-status-r001.json") `
        -ExecutionSwitchPath (Join-Path $retryDir "lmax-demo-execution-switch-status-r001.json") `
        -ConfigValidationPath (Join-Path $retryDir "real-qubes-lmax-execution-config-validation-r001.json") `
        -OrderManifestPath (Join-Path $retryDir "real-qubes-lmax-order-manifest-r001.json") `
        -ExecutionAlgoPlanPath (Join-Path $retryDir "execution-algo-plan-r001.json") `
        -AdapterBindingPath (Join-Path $retryDir "real-qubes-actual-adapter-binding-r001.json") `
        -ExecuteLmaxDemoSandboxOrders `
        -UseActualLmaxFixClient `
        -UseMockFixServer `
        -MockFixServerRejectDuplicateFirstOrder

    $retryFirst = Read-JsonFile (Join-Path $retryDir "sandbox-execution-result-r001.json")
    Assert-Equal "LMAX_SANDBOX_EXECUTION_BLOCKED_DUPLICATE_CLORDID_R001" $retryFirst.status "Duplicate ClOrdID reject must be classified explicitly."
    Assert-Equal 6 $retryFirst.fills_count "First duplicate scenario should preserve the six filled orders."
    Assert-Equal 1 $retryFirst.duplicate_clordid_reject_count "First duplicate scenario should capture one duplicate ClOrdID reject."
    Assert-Equal $false $retryFirst.residual_zero "First duplicate scenario must leave residual nonzero."
    Assert-Equal 0.2 ([decimal](@($retryFirst.final_residuals | Where-Object { $_.symbol -eq "USDCAD" })[0].final_residual_quantity)) "USDCAD residual must be 0.2 after duplicate reject."

    & $RunnerScript `
        -OutputSubdir $retrySubdir `
        -RunId $retryRunId `
        -MainArtifactName "lmax-sandbox-real-qubes-process-test-run-r001.json" `
        -ApprovedReadyStatus "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_EXECUTION_APPROVED_READY_R001" `
        -ReconciledStatus "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_RECONCILED_R001" `
        -ApprovalPath (Join-Path $retryDir "operator-approval-lmax-demo-execution-status-r001.json") `
        -ExecutionSwitchPath (Join-Path $retryDir "lmax-demo-execution-switch-status-r001.json") `
        -ConfigValidationPath (Join-Path $retryDir "real-qubes-lmax-execution-config-validation-r001.json") `
        -OrderManifestPath (Join-Path $retryDir "real-qubes-lmax-order-manifest-r001.json") `
        -ExecutionAlgoPlanPath (Join-Path $retryDir "execution-algo-plan-r001.json") `
        -AdapterBindingPath (Join-Path $retryDir "real-qubes-actual-adapter-binding-r001.json") `
        -ExecuteLmaxDemoSandboxOrders `
        -UseActualLmaxFixClient `
        -UseMockFixServer `
        -NewExecutionAttempt `
        -ResidualOnlyRetry
} finally {
    foreach ($key in $oldEnv.Keys) {
        [Environment]::SetEnvironmentVariable($key, $oldEnv[$key])
    }
}

$retryFinal = Read-JsonFile (Join-Path $retryDir "sandbox-execution-result-r001.json")
$retryMain = Read-JsonFile (Join-Path $retryDir "lmax-sandbox-real-qubes-process-test-run-r001.json")
$retryMap = Read-JsonFile (Join-Path $retryDir "lmax-demo-clordid-map-r001.json")
Assert-Equal "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_RECONCILED_R001" $retryFinal.status "Residual-only retry should reconcile the full multi-attempt run."
Assert-Equal "LMAX_SANDBOX_REAL_QUBES_PROCESS_TEST_RUN_RECONCILED_R001" $retryMain.status "Main artifact should reconcile after residual retry."
Assert-Equal $true $retryFinal.residual_zero "Residual retry should achieve residual zero across attempts."
Assert-Equal 2 @($retryFinal.attempts).Count "Retry execution should preserve both attempts."
Assert-Equal 1 ([int]@($retryFinal.attempts)[1].orders_submitted_count) "Residual-only retry should submit exactly one residual order."
Assert-Equal 7 $retryFinal.fills_count "Multi-attempt reconciliation should aggregate all seven fills."
Assert-True (Test-Path -LiteralPath (Join-Path $retryDir "logs\$retryRunId\attempt-001\orders.log")) "Attempt 1 orders log must be preserved."
Assert-True (Test-Path -LiteralPath (Join-Path $retryDir "logs\$retryRunId\attempt-002\orders.log")) "Attempt 2 orders log must be written separately."
$attempt1Ids = @($retryMap.mappings | Where-Object { $_.execution_attempt_id -eq "A001" } | ForEach-Object { [string]$_.external_cl_ord_id })
$attempt2Ids = @($retryMap.mappings | Where-Object { $_.execution_attempt_id -eq "A002" } | ForEach-Object { [string]$_.external_cl_ord_id })
Assert-True ($attempt1Ids.Count -gt 0 -and $attempt2Ids.Count -eq 1) "ClOrdID map must include attempt-specific mappings."
Assert-True (($attempt1Ids | Where-Object { $attempt2Ids -contains $_ }).Count -eq 0) "Second attempt ClOrdID must differ from first attempt IDs."
foreach ($externalId in @($attempt1Ids + $attempt2Ids)) {
    Assert-True ($externalId.Length -le 20) "Attempt-aware external ClOrdID must be <= 20 characters."
}

$oldHashAfter = Get-Sha256OrNull $OldRunMainPath
Assert-Equal $oldHashBefore $oldHashAfter "Existing reconciled/global run artifact must not be overwritten by real-Qubes tests."

Write-Host "PASS: real-Qubes LMAX sandbox process test run scenarios passed."
