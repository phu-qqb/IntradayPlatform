$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-operator-approval-r012"

function Read-Json([string]$Name) {
    Get-Content -Raw -LiteralPath (Join-Path $ArtifactDir $Name) | ConvertFrom-Json
}

function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function New-ShortHash([string]$InputText) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $bytes = [Text.Encoding]::UTF8.GetBytes($InputText)
    (($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("X2") }) -join "").Substring(0,24)
}

$binding = Read-Json "exact-approved-candidate-binding.json"
$disclosure = Read-Json "quantity-warning-disclosure-statement.json"
$statement = Read-Json "operator-approval-statement.json"
$approval = Read-Json "operator-approval-id.json"
$preconditions = Read-Json "future-r013-execution-preconditions.json"
$contract = Read-Json "contract-status-update.json"
$boundary = Read-Json "boundary-safety-evidence.json"

$inputText = ($approval.HashInputs.PSObject.Properties | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join "`n"
$expectedId = "core-anubis-intraday-operator-approval-r012:" + (New-ShortHash $inputText)
Assert ($approval.OperatorApprovalId -eq $expectedId) "Approval ID determinism failed."

$expectedQuantities = @("AUDUSD:0","CADUSD:0.2","CHFUSD:0","CNHUSD:0.2","EURUSD:0","GBPUSD:0","JPYUSD:88.4","MXNUSD:1.1","NOKUSD:3.1","NZDUSD:0.1","SEKUSD:0.4","SGDUSD:0.5","ZARUSD:7.1")
foreach ($expected in $expectedQuantities) {
    $parts = $expected.Split(":")
    $row = $binding.Quantities | Where-Object { $_.Symbol -eq $parts[0] } | Select-Object -First 1
    Assert ($null -ne $row) "Missing quantity for $($parts[0])."
    Assert ([string]$row.Quantity -eq $parts[1]) "Quantity mismatch for $($parts[0])."
}

Assert ($disclosure.TotalOmittedExposureUsd -eq "601.92") "Omitted exposure disclosure missing."
Assert ($statement.DoesNotApplyTo -contains "R010 SandboxQubesPrototype approval") "R010 prototype non-transferability missing."
Assert ($binding.JPYUSDCaveat -match "JPYUSD") "JPYUSD caveat missing."
Assert ($statement.NoExecutionInThisPackage -and $contract.R009SubmissionAllowedNow -eq $false -and $boundary.NoR009) "R012 must not allow R009 execution."
Assert ($preconditions.Preconditions -match "sandbox/demo profile only.") "Future R013 must require sandbox-only route."

Write-Host "CORE_ANUBIS_INTRADAY_OPERATOR_APPROVAL_R012_FOCUSED_TEST_PASS"
