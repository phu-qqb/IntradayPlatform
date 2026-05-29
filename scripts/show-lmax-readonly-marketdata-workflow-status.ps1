param(
    [string]$SignoffFile,
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-LocalPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $Path }
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

function Get-LatestSignoffFile {
    $dir = Join-Path $repoRoot "artifacts/readiness"
    if (-not (Test-Path -LiteralPath $dir)) { return $null }
    return (Get-ChildItem -Path $dir -Filter "lmax-readonly-marketdata-operational-signoff-*.json" |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1).FullName
}

function Add-Issue([string]$Severity, [string]$Code, [string]$Path, [string]$Message) {
    [ordered]@{ severity = $Severity; code = $Code; path = $Path; message = $Message }
}

Write-Host "LMAX Read-Only MarketData Workflow Status"
Write-Host "Local-only. No external connection, no credentials, no replay, no scheduler, and no mutation."

if ([string]::IsNullOrWhiteSpace($SignoffFile)) {
    $SignoffFile = Get-LatestSignoffFile
}

$issues = @()
if ([string]::IsNullOrWhiteSpace($SignoffFile)) {
    $issues += Add-Issue "Warning" "SignoffNotAvailable" "$.signoffFile" "No Phase 5W signoff file was found."
    $summary = [ordered]@{
        summaryId = [Guid]::NewGuid().ToString("D")
        createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
        signoffDecision = "NotAvailable"
        auditPackDecision = "NotAvailable"
        gateDecision = "NotAvailable"
        artifactCount = 0
        evidencePreviewCount = 0
        manualReplayCount = 0
        totalObservationCount = 0
        runtimeShadowReplaySubmit = $false
        externalConnectionAttempted = $false
        credentialValuesReturned = $false
        orderSubmissionAttempted = $false
        tradingMutationAttempted = $false
        schedulerStarted = $false
        apiWorkerGatewayMode = "FakeLmaxGateway"
        workflowFrozen = $false
        operationalStatus = "NotAvailable"
        whatIsAllowed = @("Manual Demo MarketData workflow review", "Artifact, evidence preview, and replay result inspection")
        whatIsNotAllowed = @("Scheduler", "Polling", "Runtime shadow replay submit", "Order submission", "Gateway registration", "Production/UAT", "Multi-instrument expansion")
        noSensitiveContent = $true
        issues = $issues
    }
} else {
    $signoffPath = Resolve-LocalPath $SignoffFile
    if (-not (Test-Path -LiteralPath $signoffPath)) { throw "Signoff file not found: $signoffPath" }
    $signoffText = Get-Content -Raw -LiteralPath $signoffPath
    if ($signoffText -match "(?i)554\s*=" -or $signoffText -match "(?i)password\s*[:=]\s*(?!\\[REDACTED\\])\\S+" -or $signoffText -match "(?i)rawFix") {
        throw "Signoff file contains forbidden sensitive content."
    }
    $signoff = $signoffText | ConvertFrom-Json
    $gateReport = Join-Path $repoRoot "artifacts/readiness/phase5w-operational-signoff-gate.json"
    $gateDecision = if (Test-Path -LiteralPath $gateReport) { [string]((Get-Content -Raw -LiteralPath $gateReport | ConvertFrom-Json).finalDecision) } else { "NotAvailable" }
    $unsafe = @()
    foreach ($name in @("runtimeShadowReplaySubmit", "externalConnectionAttempted", "credentialValuesReturned", "orderSubmissionAttempted", "tradingMutationAttempted", "schedulerStarted")) {
        if ([bool]$signoff.$name) { $unsafe += $name; $issues += Add-Issue "Error" "UnsafeFlag" "`$.$name" "$name must remain false." }
    }
    if ([string]$signoff.finalDecision -ne "PASS") { $issues += Add-Issue "Error" "SignoffDecisionNotPass" "$.finalDecision" "Phase 5W signoff decision must be PASS." }
    if ([string]$signoff.auditPackFinalDecision -ne "PASS") { $issues += Add-Issue "Error" "AuditPackDecisionNotPass" "$.auditPackFinalDecision" "Phase 5V audit pack decision must be PASS." }
    if ([int]$signoff.manualReplayCount -ne [int]$signoff.evidencePreviewCount) { $issues += Add-Issue "Error" "ReplayCountMismatch" "$.manualReplayCount" "ManualReplayCount must equal EvidencePreviewCount." }
    if ([int]$signoff.totalObservationCount -ne 0) { $issues += Add-Issue "Error" "ObservationCountNonZero" "$.totalObservationCount" "TotalObservationCount must be zero." }
    $status = if (($issues | Where-Object { $_.severity -eq "Error" }).Count -gt 0) { "Fail" } elseif (($issues | Where-Object { $_.severity -eq "Warning" }).Count -gt 0) { "PassWithWarnings" } else { "FrozenManualReadOnly" }
    $summary = [ordered]@{
        summaryId = [Guid]::NewGuid().ToString("D")
        createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
        signoffDecision = [string]$signoff.finalDecision
        auditPackDecision = [string]$signoff.auditPackFinalDecision
        gateDecision = $gateDecision
        artifactCount = [int]$signoff.artifactCount
        evidencePreviewCount = [int]$signoff.evidencePreviewCount
        manualReplayCount = [int]$signoff.manualReplayCount
        totalObservationCount = [int]$signoff.totalObservationCount
        runtimeShadowReplaySubmit = [bool]$signoff.runtimeShadowReplaySubmit
        externalConnectionAttempted = [bool]$signoff.externalConnectionAttempted
        credentialValuesReturned = [bool]$signoff.credentialValuesReturned
        orderSubmissionAttempted = [bool]$signoff.orderSubmissionAttempted
        tradingMutationAttempted = [bool]$signoff.tradingMutationAttempted
        schedulerStarted = [bool]$signoff.schedulerStarted
        apiWorkerGatewayMode = "FakeLmaxGateway"
        workflowFrozen = $status -eq "FrozenManualReadOnly"
        operationalStatus = $status
        whatIsAllowed = @("Manual Demo MarketData workflow review", "Artifact, evidence preview, and replay result inspection")
        whatIsNotAllowed = @("Scheduler", "Polling", "Runtime shadow replay submit", "Order submission", "Gateway registration", "Production/UAT", "Multi-instrument expansion")
        noSensitiveContent = [bool]$signoff.noSensitiveContent
        issues = $issues
    }
}

Write-Host "OperationalStatus: $($summary.operationalStatus)"
Write-Host "SignoffDecision: $($summary.signoffDecision)"
Write-Host "AuditPackDecision: $($summary.auditPackDecision)"
Write-Host "GateDecision: $($summary.gateDecision)"
Write-Host "ArtifactCount: $($summary.artifactCount)"
Write-Host "EvidencePreviewCount: $($summary.evidencePreviewCount)"
Write-Host "ManualReplayCount: $($summary.manualReplayCount)"
Write-Host "TotalObservationCount: $($summary.totalObservationCount)"
Write-Host "RuntimeShadowReplaySubmit: $($summary.runtimeShadowReplaySubmit)"
Write-Host "ExternalConnectionAttempted: $($summary.externalConnectionAttempted)"
Write-Host "CredentialValuesReturned: $($summary.credentialValuesReturned)"
Write-Host "API/Worker: $($summary.apiWorkerGatewayMode)"
Write-Host "Allowed: $($summary.whatIsAllowed -join '; ')"
Write-Host "NotAllowed: $($summary.whatIsNotAllowed -join '; ')"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts/readiness"
} else {
    $OutputDirectory = Resolve-LocalPath $OutputDirectory
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$jsonPath = Join-Path $OutputDirectory "lmax-readonly-marketdata-workflow-status-$stamp.json"
$mdPath = Join-Path $OutputDirectory "lmax-readonly-marketdata-workflow-status-$stamp.md"
$summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
@"
# LMAX Read-Only MarketData Workflow Status

- OperationalStatus: $($summary.operationalStatus)
- SignoffDecision: $($summary.signoffDecision)
- AuditPackDecision: $($summary.auditPackDecision)
- ArtifactCount: $($summary.artifactCount)
- EvidencePreviewCount: $($summary.evidencePreviewCount)
- ManualReplayCount: $($summary.manualReplayCount)
- TotalObservationCount: $($summary.totalObservationCount)
- RuntimeShadowReplaySubmit: $($summary.runtimeShadowReplaySubmit)
- ExternalConnectionAttempted: $($summary.externalConnectionAttempted)
- CredentialValuesReturned: $($summary.credentialValuesReturned)
- API/Worker: $($summary.apiWorkerGatewayMode)

PASS authorizes only recognition and inspection of the validated controlled manual Demo read-only MarketData workflow. It does not authorize scheduler, polling, runtime shadow replay submit, orders, gateway registration, UAT/production, multi-instrument expansion, or trading mutation.
"@ | Set-Content -LiteralPath $mdPath -Encoding UTF8

Write-Host "SummaryJson: $jsonPath"
Write-Host "SummaryMarkdown: $mdPath"

if (($issues | Where-Object { $_.severity -eq "Error" }).Count -gt 0) { exit 1 }
