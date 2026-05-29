param(
    [switch]$ConfirmCredentialAvailabilityCheck,
    [switch]$WriteReport
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$requiredLabels = @(
    "LMAX_DEMO_FIX_USERNAME",
    "LMAX_DEMO_FIX_PASSWORD",
    "LMAX_DEMO_SENDER_COMP_ID",
    "LMAX_DEMO_TARGET_COMP_ID"
)

Write-Host "LMAX Read-Only Runtime Demo Credential Availability Check"
Write-Host "Local-only. No LMAX connection, no socket, no FIX logon, no order submission."
Write-Host "This script checks presence only and never prints credential values."

if (-not $ConfirmCredentialAvailabilityCheck.IsPresent) {
    Write-Error "Refusing to check credential availability without -ConfirmCredentialAvailabilityCheck."
    exit 2
}

$statuses = @()
foreach ($label in $requiredLabels) {
    $value = [Environment]::GetEnvironmentVariable($label)
    $statuses += [ordered]@{
        keyLabel = $label
        isPresent = -not [string]::IsNullOrWhiteSpace($value)
        redactionStatus = "Redacted"
    }
}

$missing = @($statuses | Where-Object { -not $_.isPresent } | ForEach-Object { $_.keyLabel })
$result = [ordered]@{
    credentialProfileName = "LmaxDemoReadOnlyProfile"
    sourceKind = "Environment"
    isConfigured = $missing.Count -eq 0
    missingKeyCount = $missing.Count
    missingKeyLabels = $missing
    keyStatuses = $statuses
    redactionStatus = "Redacted"
    sensitiveMaterialReturned = $false
    credentialReadAttempted = $true
    credentialValuesReturned = $false
    externalConnectionAttempted = $false
    sessionStarted = $false
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    message = if ($missing.Count -eq 0) { "All required credential labels are present. Values were checked for presence only and were not printed, returned, logged, or stored." } else { "Missing credential labels: " + ($missing -join ", ") + ". Values were not printed, returned, logged, or stored." }
}

$json = $result | ConvertTo-Json -Depth 8
Write-Host $json

if ($WriteReport.IsPresent) {
    $reportDir = Join-Path $repoRoot "artifacts/readiness"
    New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $reportPath = Join-Path $reportDir "lmax-readonly-demo-credential-availability-$stamp.json"
    $json | Set-Content -LiteralPath $reportPath -Encoding UTF8
    Write-Host "SanitizedReport: $reportPath"
}

if ($missing.Count -gt 0) {
    exit 1
}
