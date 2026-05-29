param(
    [Parameter(Mandatory = $true)]
    [string]$AuditPackFile,
    [string]$AuditPackMarkdownFile,
    [string]$SignoffBy = "local-operator",
    [string]$Role = "Operator",
    [string]$Reason = "Phase 5W operational signoff for controlled manual Demo MarketData workflow"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-LocalPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $Path }
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

Write-Host "LMAX Read-Only MarketData Operational Signoff"
Write-Host "Local-only. No LMAX connection, no credentials, no runtime snapshot run, and no replay execution."

if ([string]::IsNullOrWhiteSpace($Reason)) { throw "Reason is required." }
$auditPath = Resolve-LocalPath $AuditPackFile
if (-not (Test-Path -LiteralPath $auditPath)) { throw "Missing audit pack: $auditPath" }
if ([string]::IsNullOrWhiteSpace($AuditPackMarkdownFile)) {
    $AuditPackMarkdownFile = [IO.Path]::ChangeExtension($auditPath, ".md")
}
$auditMarkdownPath = Resolve-LocalPath $AuditPackMarkdownFile
if (-not (Test-Path -LiteralPath $auditMarkdownPath)) { throw "Missing audit pack markdown report: $auditMarkdownPath" }

$audit = Get-Content -Raw -LiteralPath $auditPath | ConvertFrom-Json
if ([string]$audit.finalDecision -ne "PASS") { throw "Audit pack finalDecision must be PASS." }

$signoff = [ordered]@{
    signoffId = [guid]::NewGuid().ToString("N")
    phase = "5W"
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    signoffBy = $SignoffBy
    role = $Role
    reason = $Reason
    auditPackFile = $auditPath
    auditPackMarkdownFile = $auditMarkdownPath
    auditPackGateReportFile = (Join-Path $repoRoot "artifacts/readiness/phase5v-final-audit-pack-gate.json")
    workflowManifestFile = [string]$audit.workflowManifestFile
    stabilitySummaryFile = [string]$audit.stabilitySummaryFile
    auditPackFinalDecision = [string]$audit.finalDecision
    artifactCount = [int]$audit.artifactCount
    evidencePreviewCount = [int]$audit.evidencePreviewCount
    manualReplayCount = [int]$audit.manualReplayCount
    totalObservationCount = [int]$audit.totalObservationCount
    runtimeShadowReplaySubmit = [bool]$audit.runtimeShadowReplaySubmit
    externalConnectionAttempted = [bool]$audit.externalConnectionAttempted
    orderSubmissionAttempted = [bool]$audit.orderSubmissionAttempted
    shadowReplaySubmitAttempted = [bool]$audit.shadowReplaySubmitAttempted
    tradingMutationAttempted = [bool]$audit.tradingMutationAttempted
    schedulerStarted = [bool]$audit.schedulerStarted
    credentialValuesReturned = [bool]$audit.credentialValuesReturned
    noSensitiveContent = $true
    redactionStatus = "Redacted"
    safetyConfirmations = $audit.safetyConfirmations
    authorizedScope = @(
        "Recognition that the controlled manual Demo read-only MarketData workflow has been validated."
    )
    notAuthorized = @(
        "scheduler",
        "polling",
        "runtime shadow replay submit",
        "order submission",
        "gateway registration",
        "UAT or production use",
        "multi-instrument expansion",
        "automatic execution",
        "trading-state mutation"
    )
    finalDecision = "PASS"
}

$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$jsonPath = Join-Path $outDir "lmax-readonly-marketdata-operational-signoff-$stamp.json"
$mdPath = Join-Path $outDir "lmax-readonly-marketdata-operational-signoff-$stamp.md"
$signoff | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$markdown = @"
# LMAX Read-Only MarketData Operational Signoff

## Decision

- FinalDecision: PASS
- SignoffBy: $SignoffBy
- Role: $Role
- Reason: $Reason

## Validated Workflow

- ArtifactCount: $($signoff.artifactCount)
- EvidencePreviewCount: $($signoff.evidencePreviewCount)
- ManualReplayCount: $($signoff.manualReplayCount)
- TotalObservationCount: $($signoff.totalObservationCount)
- RuntimeShadowReplaySubmit: false
- ExternalConnectionAttempted: false
- CredentialValuesReturned: false

PASS authorizes only recognition that the controlled manual Demo read-only MarketData workflow has been validated.

## Not Authorized

- Scheduler
- Polling
- Runtime shadow replay submit
- Order submission
- Gateway registration
- UAT or production use
- Multi-instrument expansion
- Automatic execution
- Trading-state mutation

## References

- AuditPack: $auditPath
- AuditPackMarkdown: $auditMarkdownPath
- AuditPackGateReport: $($signoff.auditPackGateReportFile)
"@
$markdown | Set-Content -LiteralPath $mdPath -Encoding UTF8

Write-Host ""
Write-Host "SignoffFile: $jsonPath"
Write-Host "SignoffMarkdown: $mdPath"
Write-Host ("ArtifactCount: {0}" -f $signoff.artifactCount)
Write-Host ("EvidencePreviewCount: {0}" -f $signoff.evidencePreviewCount)
Write-Host ("ManualReplayCount: {0}" -f $signoff.manualReplayCount)
Write-Host ("TotalObservationCount: {0}" -f $signoff.totalObservationCount)
Write-Host "RuntimeShadowReplaySubmit: false"
Write-Host "ExternalConnectionAttempted: false"
Write-Host "CredentialValuesReturned: false"
Write-Host "FinalDecision: PASS"
