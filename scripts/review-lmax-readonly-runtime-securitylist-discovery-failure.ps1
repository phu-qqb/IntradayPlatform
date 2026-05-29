param(
    [Parameter(Mandatory = $true)]
    [string]$DiscoveryArtifactFile
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-LocalPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return $Path }
    return Join-Path $repoRoot $Path
}

function Get-JsonCollectionCount($Value) {
    if ($null -eq $Value) { return 0 }
    if ($Value -is [array]) { return $Value.Count }
    if (@($Value.PSObject.Properties | Where-Object { $_.MemberType -eq "NoteProperty" }).Count -eq 0) { return 0 }
    return 1
}

function Get-StringArray($Value) {
    if ($null -eq $Value) { return @() }
    return @($Value | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Redact-Text([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return "" }
    $redacted = [regex]::Replace($Text, "(?i)(554=)[^\x01|,\r\n\s]+", '$1[REDACTED]')
    $redacted = [regex]::Replace($redacted, "(?i)(553=)[^\x01|,\r\n\s]+", '$1[REDACTED]')
    $redacted = [regex]::Replace($redacted, "(?i)(49=)[^\x01|,\r\n\s]+", '$1[REDACTED]')
    $redacted = [regex]::Replace($redacted, "(?i)(56=)[^\x01|,\r\n\s]+", '$1[REDACTED]')
    $redacted = [regex]::Replace($redacted, "(?i)(password|secret|token|authorization)\s*[:=]\s*[^,;\r\n\s]+", '$1=[REDACTED]')
    return $redacted
}

function Test-SensitiveContent([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $false }
    return $Text -match '(?i)(password|secret|token|apikey|api_key|privatekey|private_key|authorization|bearer|\b553=|\b554=|host=|user=|account)'
}

function Get-AttemptDiagnostics($Artifact) {
    $attempts = @()
    foreach ($attempt in @($Artifact.attempts)) {
        if ($null -eq $attempt) { continue }
        $rejectText = Redact-Text ([string]$attempt.rejectText)
        $classification = [string]$attempt.status
        if ([string]::IsNullOrWhiteSpace($classification)) { $classification = [string]$attempt.classification }
        if ([string]::IsNullOrWhiteSpace($classification)) { $classification = "FailedSafeSecurityListUnknownReject" }
        $attempts += [ordered]@{
            requestProfile = if ($attempt.requestProfile) { [string]$attempt.requestProfile } else { "Unknown" }
            firstInboundMessageType = if ($attempt.firstInboundMessageType) { [string]$attempt.firstInboundMessageType } else { $null }
            rejectMessageType = if ($attempt.rejectMessageType) { [string]$attempt.rejectMessageType } else { $null }
            rejectTag = if ($attempt.rejectTag) { [string]$attempt.rejectTag } else { $null }
            rejectText = $rejectText
            classification = $classification
            instrumentCount = if ($attempt.instrumentCount) { [int]$attempt.instrumentCount } else { 0 }
            candidateMatchCount = Get-JsonCollectionCount $attempt.candidateMatches
        }
    }
    return @($attempts)
}

function Get-FallbackDecision($FinalStatus, [array]$Attempts, [int]$CandidateMatchCount, [array]$UnmatchedCandidates) {
    $missingRejectDiagnostics = ($FinalStatus -match 'UnknownReject') -and (
        $Attempts.Count -eq 0 -or
        @($Attempts | Where-Object {
            -not [string]::IsNullOrWhiteSpace($_.rejectMessageType) -or
            -not [string]::IsNullOrWhiteSpace($_.rejectTag) -or
            -not [string]::IsNullOrWhiteSpace($_.rejectText)
        }).Count -eq 0
    )
    $unsupportedClasses = @(
        "FailedSafeSecurityListUnsupportedRequestType",
        "FailedSafeSecurityListUnsupportedSecurityRequestType",
        "FailedSafeSecurityListUnsupportedSymbolFilter",
        "FailedSafeSecurityListUnsupportedByVenue",
        "FailedSafeSecurityListRequestTypeUnsupported",
        "FailedSafeSecurityListProfileRejected",
        "FailedSafeSecurityListNoSupportedProfiles",
        "FailedSafeSecurityListBusinessReject",
        "FailedSafeSecurityListSessionReject"
    )
    $likelyUnsupported = $Attempts.Count -ge 2 -and $CandidateMatchCount -eq 0 -and @($Attempts | Where-Object { $_.classification -notin $unsupportedClasses }).Count -eq 0
    $allSameClass = $Attempts.Count -gt 1 -and @($Attempts | Select-Object -ExpandProperty classification -Unique).Count -eq 1

    if ($missingRejectDiagnostics -or $likelyUnsupported -or $FinalStatus -match 'UnknownReject') {
        $decision = "UseVendorSupportConfirmation"
        $reason = if ($missingRejectDiagnostics) {
            "SecurityList discovery ended with $FinalStatus and the sanitized artifact has no attempt-level reject tag/text. Use vendor/support or other official manual evidence before creating confirmation records."
        } elseif ($likelyUnsupported) {
            "Safe SecurityList profiles failed without candidate matches. Use vendor/support or other official manual evidence unless another operator-approved diagnostic retry is explicitly chosen."
        } else {
            "SecurityList discovery did not identify candidate SecurityIDs. Use vendor/support or other official manual evidence."
        }
    } else {
        $decision = "BlockedPendingEvidence"
        $reason = "SecurityID evidence remains pending. Do not create accepted records until a trusted confirmation source is reviewed."
    }

    return [ordered]@{
        recommendedDecision = $decision
        reason = $reason
        allProfilesFailedWithSameClass = $allSameClass
        likelySecurityListUnsupportedByVenue = $likelyUnsupported
        missingRejectDiagnostics = $missingRejectDiagnostics
        candidateMatchCount = $CandidateMatchCount
        unmatchedCandidates = $UnmatchedCandidates
        isApprovedForExternalRun = $false
        externalRunAuthorized = $false
    }
}

Write-Host "LMAX Read-Only Runtime SecurityList Discovery Failure Review"
Write-Host "Local-only review. No LMAX connection, no credentials, no snapshots, no replay, no mutation."

$artifactPath = Resolve-LocalPath $DiscoveryArtifactFile
if (-not (Test-Path -LiteralPath $artifactPath)) {
    throw "Discovery artifact not found: $artifactPath"
}

$artifactText = Get-Content -Raw -LiteralPath $artifactPath
$artifact = $artifactText | ConvertFrom-Json
$attempts = @(Get-AttemptDiagnostics $artifact)
$candidateMatchCount = Get-JsonCollectionCount $artifact.candidateMatches
$unmatchedCandidates = Get-StringArray $artifact.unmatchedCandidates
$finalStatus = if ($artifact.finalStatus) { [string]$artifact.finalStatus } else { [string]$artifact.status }
$sensitive = Test-SensitiveContent $artifactText
$unsafeFlags = @()
foreach ($flag in @("credentialValuesReturned", "orderSubmissionAttempted", "shadowReplaySubmitAttempted", "tradingMutationAttempted", "schedulerStarted", "isApprovedForExternalRun")) {
    if ([bool]$artifact.$flag) { $unsafeFlags += $flag }
}
$fallback = Get-FallbackDecision $finalStatus $attempts $candidateMatchCount $unmatchedCandidates
$safe = (-not $sensitive) -and [bool]$artifact.noSensitiveContent -and $unsafeFlags.Count -eq 0 -and (-not [bool]$fallback.externalRunAuthorized)
$decision = if ($safe) { "PASS" } else { "FAIL" }

$attemptedProfiles = @($attempts | ForEach-Object { $_.requestProfile } | Select-Object -Unique)
Write-Host ("FinalStatus: {0}" -f $finalStatus)
Write-Host ("AttemptedProfiles: {0}" -f ($(if ($attemptedProfiles.Count -gt 0) { $attemptedProfiles -join ", " } else { "none recorded" })))
Write-Host ("CandidateMatches: {0}" -f $candidateMatchCount)
Write-Host ("UnmatchedCandidates: {0}" -f ($unmatchedCandidates -join ", "))
Write-Host ("NoSensitiveContent: {0}" -f ([bool]$artifact.noSensitiveContent))
Write-Host ("RecommendedFallbackDecision: {0}" -f $fallback.recommendedDecision)
Write-Host ("Reason: {0}" -f $fallback.reason)

$reportDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$reportPath = Join-Path $reportDir "phase6l-securitylist-fallback-decision.json"
[ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "6L"
    scope = "SecurityList Unknown Reject Analysis / Fallback Decision, No External Run by Default"
    finalDecision = $decision
    discoveryArtifactPath = $artifactPath
    finalStatus = $finalStatus
    requestProfile = [string]$artifact.requestProfile
    attemptedProfiles = $attemptedProfiles
    attempts = @($attempts)
    candidateMatchCount = $candidateMatchCount
    unmatchedCandidates = $unmatchedCandidates
    noSensitiveContent = [bool]$artifact.noSensitiveContent
    unsafeFlags = $unsafeFlags
    fallbackDecision = $fallback
    isApprovedForExternalRun = $false
    externalConnectionAttemptedByReview = $false
    securityListRequestAttemptedByReview = $false
    marketDataSnapshotAttempted = $false
    replayAttempted = $false
    runtimeShadowReplaySubmit = $false
    schedulerOrPollingAdded = $false
    orderSubmissionAdded = $false
    gatewayRegistrationAdded = $false
    tradingMutationAdded = $false
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host ("Report: {0}" -f $reportPath)
if ($decision -eq "FAIL") { exit 1 }
