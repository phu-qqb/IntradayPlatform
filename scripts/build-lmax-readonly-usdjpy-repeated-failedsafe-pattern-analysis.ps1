param(
    [string]$FirstUsdJpyArtifactFile = "artifacts/lmax-readonly-runtime-additional-snapshot/usdjpy/lmax-readonly-usdjpy-demo-snapshot-result-20260511-181833.json",
    [string]$SecondUsdJpyArtifactFile = "artifacts/lmax-readonly-runtime-additional-snapshot/usdjpy/lmax-readonly-usdjpy-demo-snapshot-result-20260511-182651.json",
    [string]$UsdJpyReviewFile = "artifacts/readiness/phase7h-additional-instrument-snapshot-review-usdjpy.json",
    [string]$UsdJpyClosureFile = "artifacts/lmax-readonly-runtime-additional-snapshot/usdjpy/closure/lmax-readonly-usdjpy-closure-manifest-20260511-182711.json",
    [string]$Phase7I2DiagnosisFile = "artifacts/readiness/phase7i2-usdjpy-failedsafe-connection-diagnosis.json",
    [string]$GbpUsdArtifactFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260511-103318.json",
    [string]$EurGbpArtifactFile = "artifacts/lmax-readonly-runtime-additional-snapshot/eurgbp/lmax-readonly-eurgbp-demo-snapshot-result-20260511-163141.json",
    [string]$OutputFile = "artifacts/readiness/phase7i4-usdjpy-repeated-failedsafe-pattern-analysis.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sensitivePattern = '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix)'

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Read-SafeJson([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) {
        throw "$Label not found: $resolved"
    }

    $raw = Get-Content -LiteralPath $resolved -Raw
    $safe = $raw -replace 'LMAX_DEMO_FIX_USERNAME|LMAX_DEMO_FIX_PASSWORD|LMAX_DEMO_SENDER_COMP_ID|LMAX_DEMO_TARGET_COMP_ID|credentialProfileName|usernamePresent|passwordPresent|usernameLength|passwordLength','SAFE_METADATA'
    if ($safe -match $sensitivePattern) {
        throw "$Label contains credential-shaped or raw FIX content: $resolved"
    }

    return [ordered]@{
        path = $resolved
        json = ($raw | ConvertFrom-Json)
    }
}

function Get-Counter($Artifact, [string]$Name) {
    if ($Artifact.diagnostics -and $Artifact.diagnostics.messageCounters -and $Artifact.diagnostics.messageCounters.PSObject.Properties.Name -contains $Name) {
        return [int]$Artifact.diagnostics.messageCounters.$Name
    }
    if ($Artifact.PSObject.Properties.Name -contains $Name) {
        return [int]$Artifact.$Name
    }
    return 0
}

function Summarize-Attempt($Artifact, [string]$Label, [string]$Path) {
    $rejectTotal = (Get-Counter $Artifact "marketDataRequestReject") + (Get-Counter $Artifact "businessMessageReject") + (Get-Counter $Artifact "reject")
    [ordered]@{
        label = $Label
        artifactPath = $Path
        runId = [string]$Artifact.runId
        startedAtUtc = [string]$Artifact.startedAtUtc
        completedAtUtc = [string]$Artifact.completedAtUtc
        status = [string]$Artifact.status
        symbol = [string]$Artifact.symbol
        slashSymbol = [string]$Artifact.slashSymbol
        securityId = [string]$Artifact.securityId
        securityIdSource = [string]$Artifact.securityIdSource
        environmentName = [string]$Artifact.environmentName
        venueProfileName = [string]$Artifact.venueProfileName
        requestMode = [string]$Artifact.requestMode
        symbolEncodingMode = [string]$Artifact.symbolEncodingMode
        marketDepth = [int]$Artifact.marketDepth
        externalConnectionAttempted = [bool]$Artifact.externalConnectionAttempted
        tcpConnected = if ($Artifact.logonDiagnostics) { [bool]$Artifact.logonDiagnostics.tcpConnected } else { $false }
        tlsConnected = if ($Artifact.logonDiagnostics) { [bool]$Artifact.logonDiagnostics.tlsConnected } else { $false }
        logonAttempted = [bool]$Artifact.logonAttempted
        logonSucceeded = [bool]$Artifact.logonSucceeded
        snapshotRequestAttempted = [bool]$Artifact.snapshotRequestAttempted
        snapshotReceived = [bool]$Artifact.snapshotReceived
        entryCount = [int]$Artifact.entryCount
        marketDataSnapshotCount = Get-Counter $Artifact "marketDataSnapshot"
        marketDataRequestRejectCount = Get-Counter $Artifact "marketDataRequestReject"
        businessMessageRejectCount = Get-Counter $Artifact "businessMessageReject"
        rejectCount = Get-Counter $Artifact "reject"
        rejectTotal = $rejectTotal
        responseClassification = if ($Artifact.diagnostics) { [string]$Artifact.diagnostics.responseClassification } else { "" }
        firstInboundMsgType = if ($Artifact.logonDiagnostics) { [string]$Artifact.logonDiagnostics.firstInboundMsgType } else { "" }
        sessionErrorSummary = if ($Artifact.diagnostics) { @($Artifact.diagnostics.sessionErrors) } else { @() }
        orderSubmissionAttempted = [bool]$Artifact.orderSubmissionAttempted
        shadowReplaySubmitAttempted = [bool]$Artifact.shadowReplaySubmitAttempted
        tradingMutationAttempted = [bool]$Artifact.tradingMutationAttempted
        schedulerStarted = [bool]$Artifact.schedulerStarted
        credentialValuesReturned = [bool]$Artifact.credentialValuesReturned
        noSensitiveContent = [bool]$Artifact.noSensitiveContent
        redactionStatus = [string]$Artifact.redactionStatus
        failedBeforeLogon = ([bool]$Artifact.externalConnectionAttempted -and -not [bool]$Artifact.logonAttempted -and -not [bool]$Artifact.snapshotRequestAttempted)
        hadNoSnapshotRequest = (-not [bool]$Artifact.snapshotRequestAttempted)
        hadZeroRejects = ($rejectTotal -eq 0)
    }
}

Write-Host "LMAX Read-Only Phase 7I4 USDJPY Repeated FailedSafe Pattern Analysis"
Write-Host "Local-only. This script does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$first = Read-SafeJson $FirstUsdJpyArtifactFile "First USDJPY failed-safe artifact"
$second = Read-SafeJson $SecondUsdJpyArtifactFile "Second USDJPY failed-safe artifact"
$review = Read-SafeJson $UsdJpyReviewFile "USDJPY review"
$closure = Read-SafeJson $UsdJpyClosureFile "USDJPY closure"
$diagnosis = Read-SafeJson $Phase7I2DiagnosisFile "Phase 7I2 diagnosis"
$gbp = Read-SafeJson $GbpUsdArtifactFile "GBPUSD successful artifact"
$eur = Read-SafeJson $EurGbpArtifactFile "EURGBP successful artifact"

$firstSummary = Summarize-Attempt $first.json "USDJPY first failed-safe attempt" $first.path
$secondSummary = Summarize-Attempt $second.json "USDJPY manual retry failed-safe attempt" $second.path
$gbpSummary = Summarize-Attempt $gbp.json "GBPUSD successful comparison" $gbp.path
$eurSummary = Summarize-Attempt $eur.json "EURGBP successful comparison" $eur.path

$attempts = @($firstSummary, $secondSummary)
$bothBeforeLogon = @($attempts | Where-Object { -not $_.failedBeforeLogon }).Count -eq 0
$bothNoSnapshotRequest = @($attempts | Where-Object { -not $_.hadNoSnapshotRequest }).Count -eq 0
$bothZeroRejects = @($attempts | Where-Object { -not $_.hadZeroRejects }).Count -eq 0
$unsafeCount = @($attempts | Where-Object {
    $_.orderSubmissionAttempted -or $_.shadowReplaySubmitAttempted -or $_.tradingMutationAttempted -or $_.schedulerStarted -or $_.credentialValuesReturned -or -not $_.noSensitiveContent
}).Count
$profileDivergence = @($attempts | Where-Object {
    $_.symbol -ne "USDJPY" -or $_.slashSymbol -ne "USD/JPY" -or $_.securityId -ne "4004" -or $_.securityIdSource -ne "8" -or $_.environmentName -ne "Demo" -or $_.venueProfileName -ne "DemoLondon" -or $_.requestMode -ne "SnapshotPlusUpdates" -or $_.symbolEncodingMode -ne "SecurityIdOnly" -or $_.marketDepth -ne 1
}).Count -gt 0

$repeatedPattern = $attempts.Count -eq 2 -and $bothBeforeLogon -and $bothNoSnapshotRequest -and $bothZeroRejects -and $unsafeCount -eq 0 -and -not $profileDivergence
$repeatedFailureClass = if ($repeatedPattern) { "FailedSafeConnectionBeforeSessionEstablishment" } else { "NeedsManualDiagnostics" }

$differencesObserved = @(
    "Both USDJPY attempts used Demo/DemoLondon, SecurityID 4004, SecurityIDSource 8, SnapshotPlusUpdates, SecurityIdOnly, and MarketDepth 1.",
    "Both USDJPY attempts show tcpConnected=true and tlsConnected=false, then failed before FIX logon.",
    "GBPUSD and EURGBP successful comparison attempts show tlsConnected=true, logonSucceeded=true, snapshotRequestAttempted=true, and marketDataSnapshotCount=1.",
    "USDJPY generated no MarketDataRequestReject, BusinessMessageReject, or session Reject because no MarketDataRequest was sent.",
    "USDJPY evidence preview was not generated because FailedSafe no-snapshot artifacts are not MarketDataOnly evidence."
)

$possibleRootCauseClasses = @(
    "Repeated network/socket/TLS failure for the USDJPY wrapper invocation path.",
    "Demo endpoint/session availability issue at the USDJPY attempt times.",
    "Local process, socket, or resource issue before FIX session establishment.",
    "Venue-side pre-logon refusal or TLS/session establishment failure.",
    "Configuration or environment path divergence that only appears during USDJPY invocation, to be audited locally without reconnecting."
)

$ruledOutCauses = @(
    "Not order path: orderSubmissionAttempted=false in both USDJPY attempts.",
    "Not scheduler/polling: schedulerStarted=false in both USDJPY attempts.",
    "Not runtime shadow replay: shadowReplaySubmitAttempted=false and replay was not run.",
    "Not trading-state mutation: tradingMutationAttempted=false in both USDJPY attempts.",
    "Not evidence preview defect: preview correctly refused FailedSafe no-snapshot input.",
    "Not local replay defect: no MarketDataOnly evidence preview existed, so replay was not eligible.",
    "Not MarketDataRequestReject: no MarketDataRequest was sent and reject counters were zero.",
    "Not proven invalid SecurityID: no venue/instrument reject was observed; DemoLondon USDJPY remains 4004/source 8.",
    "Not Tokyo 600x requirement: Tokyo variants are not selected for DemoLondon and no reject evidence supports switching."
)

$recommendedNextAction = "Stop USDJPY external retries for now. Proceed to Phase 7I5 local connection/session configuration diff audit, comparing successful GBPUSD/EURGBP invocation paths with both failed USDJPY attempts without connecting externally. Keep SecurityID 4004 and wrapper validation unchanged."
$allowedNextPhase = "Phase 7I5 - Local Connection/Session Configuration Diff Audit, No External Run."
$disallowedActions = @(
    "No third USDJPY retry.",
    "No AUDUSD run.",
    "No batch additional-instrument execution.",
    "No loop.",
    "No auto-retry.",
    "No wrapper relaxation.",
    "No SecurityID switch.",
    "No Tokyo 600x switch.",
    "No replay.",
    "No MarketDataOnly preview fabrication."
)

$finalDecision = if ($repeatedPattern) { "PASS_WITH_KNOWN_WARNINGS" } else { "FAIL" }

$report = [ordered]@{
    phase = "7I4"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    instrument = "USDJPY"
    slashSymbol = "USD/JPY"
    securityId = "4004"
    securityIdSource = "8"
    attemptsAnalyzed = 2
    repeatedFailurePattern = $repeatedPattern
    repeatedFailureClass = $repeatedFailureClass
    bothAttemptsFailedBeforeLogon = $bothBeforeLogon
    bothAttemptsHadNoSnapshotRequest = $bothNoSnapshotRequest
    bothAttemptsHadZeroRejects = $bothZeroRejects
    securityIdNotBlamed = $true
    tokyo600xSwitchDisallowed = $true
    externalRetryStopRecommended = $true
    comparisonToSuccessfulInstruments = [ordered]@{
        usdJpyAttempts = $attempts
        gbpUsd = $gbpSummary
        eurGbp = $eurSummary
        phase7I2DiagnosisClass = [string]$diagnosis.json.inferredFailureClass
        currentReviewDecision = [string]$review.json.finalDecision
        currentClosureDecision = [string]$closure.json.finalClosureDecision
    }
    differencesObserved = $differencesObserved
    possibleRootCauseClasses = $possibleRootCauseClasses
    ruledOutCauses = $ruledOutCauses
    recommendedNextAction = $recommendedNextAction
    allowedNextPhase = $allowedNextPhase
    disallowedActions = $disallowedActions
    externalConnectionAttemptedInPhase7I4 = $false
    snapshotAttemptedInPhase7I4 = $false
    replayAttemptedInPhase7I4 = $false
    schedulerStartedInPhase7I4 = $false
    orderSubmissionAttemptedInPhase7I4 = $false
    shadowReplaySubmitAttemptedInPhase7I4 = $false
    tradingMutationAttemptedInPhase7I4 = $false
    credentialValuesReturned = $false
    noSensitiveContent = $true
    finalDecision = $finalDecision
}

$outPath = Resolve-LocalPath $OutputFile
New-Item -ItemType Directory -Path (Split-Path -Parent $outPath) -Force | Out-Null
$report | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host "RepeatedFailureClass: $repeatedFailureClass"
Write-Host "FinalDecision: $finalDecision"
Write-Host "PatternAnalysisReport: $outPath"
if ($finalDecision -eq "FAIL") { exit 1 }
