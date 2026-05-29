$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-quantity-derivation-r009"

function Read-Json([string]$Name) {
    $path = Join-Path $ArtifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing artifact: $Name" }
    Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$required = @(
    "intake-validation.json",
    "core-weight-price-metadata-join.json",
    "quantity-transformation-policy.json",
    "quantity-derivation-evidence.json",
    "pms-core-candidate-with-quantities.json",
    "exposure-concentration-preview.json",
    "risk-readiness-decision.json",
    "future-package-decision.json",
    "contract-status-update.json",
    "readiness-impact.json",
    "boundary-safety-evidence.json",
    "summary.md"
)
foreach ($name in $required) {
    Assert (Test-Path -LiteralPath (Join-Path $ArtifactDir $name)) "Missing required artifact: $name"
}

$intake = Read-Json "intake-validation.json"
$join = Read-Json "core-weight-price-metadata-join.json"
$policy = Read-Json "quantity-transformation-policy.json"
$evidence = Read-Json "quantity-derivation-evidence.json"
$candidate = Read-Json "pms-core-candidate-with-quantities.json"
$exposure = Read-Json "exposure-concentration-preview.json"
$risk = Read-Json "risk-readiness-decision.json"
$future = Read-Json "future-package-decision.json"
$contract = Read-Json "contract-status-update.json"
$impact = Read-Json "readiness-impact.json"
$boundary = Read-Json "boundary-safety-evidence.json"
$summary = Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir "summary.md")

$expectedSymbols = @("AUDUSD","CADUSD","CHFUSD","CNHUSD","EURUSD","GBPUSD","JPYUSD","MXNUSD","NOKUSD","NZDUSD","SEKUSD","SGDUSD","ZARUSD")
Assert ($intake.Classification -eq "INTAKE_READY_FOR_QUANTITY_DERIVATION") "Intake not ready."
Assert ($intake.CoreHandoffManifestHashMatchesExpected -and $intake.NettedUsdWeightsHashMatchesExpected) "Core hashes did not validate."
Assert ($intake.R009ExecutionAllowed -eq $false) "R009 execution must not be allowed."
Assert ($join.Classification -in @("JOIN_READY_ALL_NONZERO_SYMBOLS","JOIN_READY_WITH_WARNINGS")) "Join not ready."
foreach ($symbol in $expectedSymbols) {
    $row = $join.Rows | Where-Object { $_.CoreSymbol -eq $symbol } | Select-Object -First 1
    Assert ($null -ne $row) "Missing join row for $symbol."
    Assert ($row.JoinStatus -in @("JOIN_READY","JOIN_READY_ZERO_WEIGHT")) "$symbol join not ready."
}

Assert ($policy.Classification -eq "QUANTITY_TRANSFORMATION_POLICY_READY") "Quantity transformation policy not ready."
Assert ($policy.R009Submission -eq $false) "Policy must block R009 submission."
Assert ($policy.NoUSDJPYCoreEmission -eq $true) "Policy must prevent USDJPY Core emission."
Assert ($evidence.Classification -in @("QUANTITIES_DERIVED_FOR_ALL_NONZERO_SYMBOLS","QUANTITIES_DERIVED_WITH_BELOW_MIN_WARNINGS")) "Quantity evidence not acceptable."
foreach ($symbol in $expectedSymbols) {
    $row = $evidence.Rows | Where-Object { $_.CoreSymbol -eq $symbol } | Select-Object -First 1
    Assert ($null -ne $row) "Missing quantity row for $symbol."
    Assert ($row.QuantityStatus -in @("QUANTITY_DERIVED","QUANTITY_ZERO_WEIGHT","QUANTITY_BELOW_MIN")) "$symbol quantity not derived or explicitly below-min."
}

Assert (($candidate.Symbols -contains "JPYUSD") -and -not ($candidate.Symbols -contains "USDJPY")) "JPYUSD caveat failed: JPYUSD required and USDJPY forbidden as Core symbol."
Assert ($candidate.R009Ready -eq $false) "Candidate must not be R009-ready."
Assert ($candidate.NotProduction -and $candidate.NotAccounting -and $candidate.NotExecuted -and $candidate.NotLedgerCommit) "Candidate safety flags missing."
Assert ($candidate.R010Transferability -eq $false) "R010 must not transfer."
Assert ($exposure.Classification -in @("EXPOSURE_PREVIEW_READY","EXPOSURE_PREVIEW_READY_WITH_WARNINGS")) "Exposure preview not ready."
Assert ($risk.Classification -in @("CORE_CANDIDATE_READY_FOR_RISK_REVIEW_NOT_EXECUTION","CORE_CANDIDATE_QUANTITY_WARNINGS_REQUIRE_REVIEW_BEFORE_RISK")) "Risk readiness decision invalid."
Assert ($future.Decision -in @("NEXT_CORE_ANUBIS_INTRADAY_RISK_REVIEW_R010","NEXT_CORE_ANUBIS_INTRADAY_QUANTITY_REFINEMENT_R010")) "Unexpected future package decision."
Assert ($contract.Statuses."r009-execution-readiness.v1" -eq "BLOCKED_FOR_CORE_CANDIDATE") "R009 execution readiness must remain blocked."
Assert ($contract.Statuses."pms-execution-candidate.v1" -eq "BLOCKED") "PMS execution candidate must remain blocked."
Assert ($impact.NoExecutionOccurred -and $impact.NoR009ReadinessGranted -and $impact.NoLedgerReadinessChanged -and $impact.NoProductionReadinessChanged) "Readiness impact crossed boundary."
Assert ($boundary.NoCoreExecution -and $boundary.NoManager -and $boundary.NoAnubis -and $boundary.NoCuda -and $boundary.NoCoreNetting) "Core boundary failed."
Assert ($boundary.NoLmax -and $boundary.NoPolygonMassiveCall -and $boundary.NoExternalMarketDataCall) "External call boundary failed."
Assert ($boundary.NoR009 -and $boundary.NoOrderFillReport -and $boundary.NoDbMutation -and $boundary.NoLedger) "Execution/mutation boundary failed."
Assert ($boundary.NoInventedPrices -and $boundary.NoInventedMetadata -and $boundary.NoInventedQuantitiesWithoutRequiredInputs -and $boundary.NoR010Transfer) "Invention/R010 boundary failed."
Assert ($summary -match "CORE_ANUBIS_INTRADAY_QUANTITY_DERIVATION_R009_") "Summary classification missing."
Assert ($summary -match "Is R009 allowed\? no") "Summary must explicitly block R009."

Write-Host "CORE_ANUBIS_INTRADAY_QUANTITY_DERIVATION_R009_VALIDATOR_PASS"
