param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$BuildScript = Join-Path $PSScriptRoot "build-real-qubes-core-handoff-to-intraday-consumption-r001.ps1"
$Package = "NEXT_REAL_QUBES_CORE_HANDOFF_TO_INTRADAY_CONSUMPTION_R001"
$PreviousRunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_20260529T125324Z"
$PreviousRunDir = Join-Path $RepoRoot "artifacts\readiness\lmax-sandbox-global-process-test-run-r001"
$TestRoot = Join-Path $RepoRoot ("artifacts\readiness\real-qubes-core-handoff-to-intraday-consumption-r001-test-fixtures\" + (Get-Date -Format "yyyyMMddHHmmssfff"))

function Write-JsonFile([string]$Path, [object]$Value) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Missing JSON file: $Path" }
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -ne $Actual) { throw "$Message Expected=[$Expected] Actual=[$Actual]" }
}

function Invoke-Scenario([string]$Name, [string[]]$CandidateRoots, [switch]$DisablePreviousFallback, [switch]$AllowTestFixtureRealAcceptance) {
    $outputSubdir = "real-qubes-core-handoff-to-intraday-consumption-r001-test\$Name"
    $invokeArgs = @{
        RepoRoot = $RepoRoot
        OutputSubdir = $outputSubdir
        MaxCandidateFiles = 60
    }
    if ($CandidateRoots.Count -gt 0) { $invokeArgs.CandidateRoots = $CandidateRoots }
    if ($DisablePreviousFallback) { $invokeArgs.DisablePreviousLmaxFixtureFallback = $true }
    if ($AllowTestFixtureRealAcceptance) { $invokeArgs.AllowTestFixtureRealAcceptance = $true }
    & $BuildScript @invokeArgs | Out-Host
    $dir = Join-Path $RepoRoot "artifacts\readiness\$outputSubdir"
    [ordered]@{
        dir = $dir
        main = Read-JsonFile (Join-Path $dir "real-qubes-core-handoff-to-intraday-consumption-r001.json")
        manifest = Read-JsonFile (Join-Path $dir "core-to-intraday-handoff-manifest-r001.json")
        discovery = Read-JsonFile (Join-Path $dir "qubes-core-discovery-report-r001.json")
        consumer = Read-JsonFile (Join-Path $dir "intraday-handoff-consumption-r001.json")
        orders = Read-JsonFile (Join-Path $dir "real-qubes-lmax-order-manifest-preview-r001.json")
        bridge = Read-JsonFile (Join-Path $dir "real-qubes-to-existing-lmax-run-scope-bridge-r001.json")
        coverage = Read-JsonFile (Join-Path $dir "e2e-flow-coverage-after-real-qubes-handoff-r001.json")
    }
}

function New-ValidRealQubesArtifact([string]$RunId, [string]$Symbol = "USDCAD", [string]$Side = "SELL", [decimal]$Quantity = 0.2, [string]$SecurityId = "4013") {
    [ordered]@{
        package = $Package
        artifact_type = "real_qubes_core_output_test_fixture"
        source_system = "QQ.Production.Core / Anubis"
        generated_by_qubes_core = $true
        synthetic_fixture = $false
        run_id = $RunId
        strategy = "fx_intraday_qubes_test_strategy"
        target_notional_usd = 6000000
        raw_aggregated_weights = @(
            [ordered]@{ symbol = $Symbol; weight = "0.00010000" }
        )
        final_manager_weights = @(
            [ordered]@{ symbol = $Symbol; weight = "0.00010000" }
        )
        netted_usd_weights = @(
            [ordered]@{
                symbol = $Symbol
                execution_symbol = $Symbol
                weight = "0.00010000"
                target_notional_usd = 600
                rounded_notional_usd = 600
                side = $Side
                quantity = $Quantity
                security_id = $SecurityId
                security_id_source_tag22 = "8"
            }
        )
        order_targets = @(
            [ordered]@{
                order_target_id = "real-qubes-test-$Symbol"
                core_symbol = $Symbol
                symbol = $Symbol
                side = $Side
                target_notional_usd = 600
                rounded_notional_usd = 600
                raw_quantity = $Quantity
                refined_quantity = $Quantity
                security_id = $SecurityId
                security_id_source_tag22 = "8"
                residual_after_rounding_usd = 0
                live_order = $false
                production_order = $false
            }
        )
    }
}

function Assert-GuardsFalse($Main) {
    foreach ($guard in @("trading_activity", "lmax_fix_api_call", "broker_api_call", "polygon_massive_call", "market_data_fetch", "broker_fetch", "account_data_fetch", "production_live_write", "production_live_ready", "trading_readiness_ready")) {
        Assert-Equal $false $Main.global_guards.$guard "Guard $guard must remain false."
    }
}

New-Item -ItemType Directory -Force -Path $TestRoot | Out-Null

$emptyRoot = Join-Path $TestRoot "empty-root"
New-Item -ItemType Directory -Force -Path $emptyRoot | Out-Null
$empty = Invoke-Scenario -Name "empty-staging" -CandidateRoots @($emptyRoot) -DisablePreviousFallback
Assert-Equal "BLOCKED_REAL_QUBES_CORE_OUTPUT_MISSING_R001" $empty.main.status "Missing real Qubes/Core output should block when fixture fallback is disabled."
Assert-Equal $false $empty.main.real_qubes_core_output_accepted "Missing scenario must not accept real output."
Assert-GuardsFalse $empty.main

$syntheticRoot = Join-Path $TestRoot "synthetic-candidates"
$synthetic = New-ValidRealQubesArtifact -RunId "SYNTHETIC_QUBES_RUN_R001"
$synthetic.synthetic_fixture = $true
Write-JsonFile (Join-Path $syntheticRoot "synthetic-qubes-core-output-r001.json") $synthetic
$syntheticScenario = Invoke-Scenario -Name "synthetic-only" -CandidateRoots @($syntheticRoot) -DisablePreviousFallback
Assert-Equal $false $syntheticScenario.main.real_qubes_core_output_accepted "Synthetic fixture must not be accepted as real."
Assert-True ((@($syntheticScenario.discovery.candidate_files_found | Where-Object { $_.artifact_type_classification -eq "SYNTHETIC_FIXTURE" }).Count -gt 0)) "Synthetic fixture should be classified."
Assert-GuardsFalse $syntheticScenario.main

$invalidRoot = Join-Path $TestRoot "invalid-real"
$invalid = New-ValidRealQubesArtifact -RunId "INVALID_QUBES_RUN_R001"
$invalid.netted_usd_weights[0].weight = "NaN"
Write-JsonFile (Join-Path $invalidRoot "invalid-real-qubes-core-output-r001.json") $invalid
$invalidScenario = Invoke-Scenario -Name "invalid-real" -CandidateRoots @($invalidRoot) -DisablePreviousFallback -AllowTestFixtureRealAcceptance
Assert-Equal "BLOCKED_REAL_QUBES_CORE_OUTPUT_INVALID_R001" $invalidScenario.main.status "NaN/null real weights must block real output."
Assert-Equal $false $invalidScenario.main.real_qubes_core_output_accepted "Invalid real output must not be accepted."
Assert-GuardsFalse $invalidScenario.main

$missingRunRoot = Join-Path $TestRoot "missing-run-real"
$missingRun = New-ValidRealQubesArtifact -RunId ""
Write-JsonFile (Join-Path $missingRunRoot "missing-run-real-qubes-core-output-r001.json") $missingRun
$missingRunScenario = Invoke-Scenario -Name "missing-run-real" -CandidateRoots @($missingRunRoot) -DisablePreviousFallback -AllowTestFixtureRealAcceptance
Assert-Equal "BLOCKED_REAL_QUBES_CORE_OUTPUT_INVALID_R001" $missingRunScenario.main.status "Missing run_id must block real output."
Assert-Equal $false $missingRunScenario.main.real_qubes_core_output_accepted "Missing run_id output must not be accepted."
Assert-GuardsFalse $missingRunScenario.main

$validRoot = Join-Path $TestRoot "valid-real"
$valid = New-ValidRealQubesArtifact -RunId "REAL_QUBES_CORE_TEST_RUN_R001"
Write-JsonFile (Join-Path $validRoot "valid-real-qubes-core-output-r001.json") $valid
$validScenario = Invoke-Scenario -Name "valid-real" -CandidateRoots @($validRoot) -DisablePreviousFallback -AllowTestFixtureRealAcceptance
Assert-Equal "REAL_QUBES_CORE_HANDOFF_CONSUMED_AND_ORDER_PREVIEW_READY_R001" $validScenario.main.status "Valid real output should consume and create order preview."
Assert-Equal $true $validScenario.main.real_qubes_core_output_accepted "Valid real output should be accepted."
Assert-Equal $true $validScenario.main.intraday_consumption_ready "Intraday should consume valid real manifest."
Assert-True ($validScenario.orders.order_count -gt 0) "Valid real output should produce LMAX order preview."
foreach ($order in @($validScenario.orders.orders)) {
    if (-not [string]::IsNullOrWhiteSpace([string]$order.security_id)) {
        Assert-Equal "8" $order.security_id_source_tag22 "Tag 22 must equal 8 when tag 48/security_id is present."
    }
}
Assert-Equal $false $validScenario.bridge.existing_lmax_run_attributed_to_real_qubes_core "Different run/hash must not be attributed to existing LMAX run."
Assert-GuardsFalse $validScenario.main

$previousHandoffPath = Join-Path $PreviousRunDir "qubes-core-weight-handoff-r001.json"
$previousDriftPath = Join-Path $PreviousRunDir "drift-and-order-targets-r001.json"
Assert-True (Test-Path -LiteralPath $previousHandoffPath) "Previous LMAX handoff must exist for attribution test."
Assert-True (Test-Path -LiteralPath $previousDriftPath) "Previous LMAX drift/order targets must exist for attribution test."
$matchingRoot = Join-Path $TestRoot "matching-real"
$matching = Read-JsonFile $previousHandoffPath
$matching | Add-Member -NotePropertyName generated_by_qubes_core -NotePropertyValue $true -Force
$matching | Add-Member -NotePropertyName synthetic_fixture -NotePropertyValue $false -Force
$matching | Add-Member -NotePropertyName source_system -NotePropertyValue "QQ.Production.Core / Anubis" -Force
$matching | Add-Member -NotePropertyName strategy -NotePropertyValue "fx_intraday_qubes_matching_real_test" -Force
$matching | Add-Member -NotePropertyName target_notional_usd -NotePropertyValue 6000000 -Force
$matching | Add-Member -NotePropertyName order_targets -NotePropertyValue @((Read-JsonFile $previousDriftPath).order_targets) -Force
Write-JsonFile (Join-Path $matchingRoot "matching-real-qubes-core-output-r001.json") $matching
$matchingScenario = Invoke-Scenario -Name "matching-real" -CandidateRoots @($matchingRoot) -DisablePreviousFallback -AllowTestFixtureRealAcceptance
Assert-Equal "REAL_QUBES_CORE_HANDOFF_CONSUMED_AND_MATCHES_EXISTING_LMAX_RUN_R001" $matchingScenario.main.status "Matching real handoff/order signature should attribute existing LMAX run."
Assert-Equal $true $matchingScenario.bridge.existing_lmax_run_attributed_to_real_qubes_core "Matching hashes and run IDs should attribute existing LMAX run."
Assert-Equal $true $matchingScenario.bridge.hashes_match "Matching scenario should have matching order signatures."
Assert-Equal $true $matchingScenario.bridge.run_ids_match "Matching scenario should have matching run IDs."
Assert-GuardsFalse $matchingScenario.main

$defaultScenario = Invoke-Scenario -Name "default-live-context" -CandidateRoots @()
Assert-True ($defaultScenario.main.status -in @(
    "FIXTURE_ONLY_QUBES_HANDOFF_CONSUMED_R001",
    "REAL_QUBES_CORE_HANDOFF_CONSUMED_R001",
    "REAL_QUBES_CORE_HANDOFF_CONSUMED_AND_ORDER_PREVIEW_READY_R001",
    "REAL_QUBES_CORE_HANDOFF_CONSUMED_AND_MATCHES_EXISTING_LMAX_RUN_R001",
    "BLOCKED_REAL_QUBES_CORE_OUTPUT_MISSING_R001",
    "BLOCKED_REAL_QUBES_CORE_OUTPUT_INVALID_R001",
    "AMBIGUOUS_QUBES_CORE_HANDOFF_EVIDENCE_R001"
)) "Default scenario must produce a recognized status."
Assert-GuardsFalse $defaultScenario.main
Assert-True (Test-Path -LiteralPath (Join-Path $defaultScenario.dir "real-qubes-core-handoff-to-intraday-consumption-summary-r001.md")) "Summary must exist."

Write-Host "REAL_QUBES_CORE_HANDOFF_TO_INTRADAY_CONSUMPTION_R001_TEST_PASS"
