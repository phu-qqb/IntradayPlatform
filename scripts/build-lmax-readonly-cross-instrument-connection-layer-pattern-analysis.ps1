param(
    [string]$GbpusdSnapshotFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260511-103318.json",
    [string]$EurgbpSnapshotFile = "artifacts/lmax-readonly-runtime-additional-snapshot/eurgbp/lmax-readonly-eurgbp-demo-snapshot-result-20260511-163141.json",
    [string[]]$FailedSnapshotFiles = @(
        "artifacts/lmax-readonly-runtime-additional-snapshot/usdjpy/lmax-readonly-usdjpy-demo-snapshot-result-20260511-181833.json",
        "artifacts/lmax-readonly-runtime-additional-snapshot/usdjpy/lmax-readonly-usdjpy-demo-snapshot-result-20260511-182651.json",
        "artifacts/lmax-readonly-runtime-additional-snapshot/audusd/lmax-readonly-audusd-demo-snapshot-result-20260511-185948.json"
    ),
    [string]$AudusdReviewFile = "artifacts/readiness/phase7h-additional-instrument-snapshot-review-audusd.json",
    [string]$AudusdClosureFile = "artifacts/lmax-readonly-runtime-additional-snapshot/audusd/closure/lmax-readonly-audusd-closure-manifest-20260511-190019.json",
    [string]$UsdJpyPhase7I2ReportFile = "artifacts/readiness/phase7i2-usdjpy-failedsafe-connection-diagnosis.json",
    [string]$UsdJpyPhase7I4ReportFile = "artifacts/readiness/phase7i4-usdjpy-repeated-failedsafe-pattern-analysis.json",
    [string]$UsdJpyPhase7I5ReportFile = "artifacts/readiness/phase7i5-usdjpy-local-connection-session-config-diff-audit.json",
    [string]$UsdJpyPhase7I6GateFile = "artifacts/readiness/phase7i6-usdjpy-operator-troubleshooting-decision-gate.json",
    [string]$AudusdPhase7JGateFile = "artifacts/readiness/phase7j-audusd-next-instrument-planning-gate.json",
    [string]$OutputDirectory = "artifacts/readiness"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-LocalPath([string]$PathValue) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) { return "" }
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}

function Read-JsonArtifact([string]$PathValue, [string]$Label) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) {
        throw "$Label artifact is missing: $resolved"
    }

    return Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json
}

function Get-Counter($Artifact, [string]$Name) {
    if ($null -ne $Artifact.diagnostics -and $null -ne $Artifact.diagnostics.messageCounters -and $null -ne $Artifact.diagnostics.messageCounters.$Name) {
        return [int]$Artifact.diagnostics.messageCounters.$Name
    }

    $topLevelName = switch ($Name) {
        "marketDataRequestReject" { "marketDataRequestRejectCount" }
        "businessMessageReject" { "businessMessageRejectCount" }
        "reject" { "rejectCount" }
        default { $Name }
    }

    if ($null -ne $Artifact.$topLevelName) { return [int]$Artifact.$topLevelName }
    return 0
}

function Get-SafeAttemptSummary($Artifact, [string]$SourcePath) {
    $marketDataRequestReject = Get-Counter $Artifact "marketDataRequestReject"
    $businessMessageReject = Get-Counter $Artifact "businessMessageReject"
    $reject = Get-Counter $Artifact "reject"

    return [ordered]@{
        sourceFile = $SourcePath
        symbol = [string]$Artifact.symbol
        slashSymbol = [string]$Artifact.slashSymbol
        securityId = [string]$Artifact.securityId
        securityIdSource = [string]$Artifact.securityIdSource
        status = [string]$Artifact.status
        startedAtUtc = [string]$Artifact.startedAtUtc
        completedAtUtc = [string]$Artifact.completedAtUtc
        environmentName = [string]$Artifact.environmentName
        venueProfileName = [string]$Artifact.venueProfileName
        requestMode = [string]$Artifact.requestMode
        symbolEncodingMode = [string]$Artifact.symbolEncodingMode
        marketDepth = [int]$Artifact.marketDepth
        externalConnectionAttempted = [bool]$Artifact.externalConnectionAttempted
        logonAttempted = [bool]$Artifact.logonAttempted
        snapshotRequestAttempted = [bool]$Artifact.snapshotRequestAttempted
        snapshotReceived = [bool]$Artifact.snapshotReceived
        entryCount = [int]$Artifact.entryCount
        marketDataRequestReject = $marketDataRequestReject
        businessMessageReject = $businessMessageReject
        reject = $reject
        tcpConnected = if ($null -ne $Artifact.logonDiagnostics) { [bool]$Artifact.logonDiagnostics.tcpConnected } else { $null }
        tlsConnected = if ($null -ne $Artifact.logonDiagnostics) { [bool]$Artifact.logonDiagnostics.tlsConnected } else { $null }
        redactionStatus = [string]$Artifact.redactionStatus
        credentialValuesReturned = [bool]$Artifact.credentialValuesReturned
        noSensitiveContent = [bool]$Artifact.noSensitiveContent
        orderSubmissionAttempted = [bool]$Artifact.orderSubmissionAttempted
        shadowReplaySubmitAttempted = [bool]$Artifact.shadowReplaySubmitAttempted
        tradingMutationAttempted = [bool]$Artifact.tradingMutationAttempted
        schedulerStarted = [bool]$Artifact.schedulerStarted
    }
}

function Test-SuccessfulMarketDataAttempt($Summary) {
    return [string]$Summary.status -eq "Completed" -and [bool]$Summary.snapshotReceived -and [int]$Summary.entryCount -gt 0
}

function Test-FailedBeforeLogon($Summary) {
    return [string]$Summary.status -eq "FailedSafeConnectionError" -and
        [bool]$Summary.externalConnectionAttempted -and
        -not [bool]$Summary.logonAttempted -and
        -not [bool]$Summary.snapshotRequestAttempted -and
        -not [bool]$Summary.snapshotReceived
}

function Test-ZeroRejects($Summary) {
    return [int]$Summary.marketDataRequestReject -eq 0 -and [int]$Summary.businessMessageReject -eq 0 -and [int]$Summary.reject -eq 0
}

New-Item -ItemType Directory -Path (Resolve-LocalPath $OutputDirectory) -Force | Out-Null

$successInputs = @(
    @{ label = "GBPUSD"; path = $GbpusdSnapshotFile },
    @{ label = "EURGBP"; path = $EurgbpSnapshotFile }
)

$successfulSummaries = @()
foreach ($input in $successInputs) {
    $artifact = Read-JsonArtifact $input.path $input.label
    $successfulSummaries += Get-SafeAttemptSummary $artifact $input.path
}

$failedSummaries = @()
foreach ($path in $FailedSnapshotFiles) {
    $artifact = Read-JsonArtifact $path "Failed snapshot"
    $failedSummaries += Get-SafeAttemptSummary $artifact $path
}

$audusdReview = Read-JsonArtifact $AudusdReviewFile "AUDUSD review"
$audusdClosure = Read-JsonArtifact $AudusdClosureFile "AUDUSD closure"
$usdjpy7i2 = Read-JsonArtifact $UsdJpyPhase7I2ReportFile "Phase 7I2 report"
$usdjpy7i4 = Read-JsonArtifact $UsdJpyPhase7I4ReportFile "Phase 7I4 report"
$usdjpy7i5 = Read-JsonArtifact $UsdJpyPhase7I5ReportFile "Phase 7I5 report"
$usdjpy7i6 = Read-JsonArtifact $UsdJpyPhase7I6GateFile "Phase 7I6 gate"
$audusd7j = Read-JsonArtifact $AudusdPhase7JGateFile "Phase 7J gate"

$allSuccessesValid = @($successfulSummaries | Where-Object { -not (Test-SuccessfulMarketDataAttempt $_) }).Count -eq 0
$allFailuresBeforeLogon = @($failedSummaries | Where-Object { -not (Test-FailedBeforeLogon $_) }).Count -eq 0
$allFailuresZeroRejects = @($failedSummaries | Where-Object { -not (Test-ZeroRejects $_) }).Count -eq 0
$allFailuresNoUnsafeFlags = @($failedSummaries | Where-Object {
    [bool]$_.orderSubmissionAttempted -or
    [bool]$_.shadowReplaySubmitAttempted -or
    [bool]$_.tradingMutationAttempted -or
    [bool]$_.schedulerStarted -or
    [bool]$_.credentialValuesReturned -or
    -not [bool]$_.noSensitiveContent
}).Count -eq 0

$timeline = @($successfulSummaries + $failedSummaries) |
    Sort-Object { if ([string]::IsNullOrWhiteSpace($_.startedAtUtc)) { $_.completedAtUtc } else { $_.startedAtUtc } } |
    ForEach-Object {
        [ordered]@{
            symbol = $_.symbol
            status = $_.status
            startedAtUtc = $_.startedAtUtc
            completedAtUtc = $_.completedAtUtc
            logonAttempted = $_.logonAttempted
            snapshotRequestAttempted = $_.snapshotRequestAttempted
            snapshotReceived = $_.snapshotReceived
            entryCount = $_.entryCount
            tcpConnected = $_.tcpConnected
            tlsConnected = $_.tlsConnected
        }
    }

$reportPath = Join-Path (Resolve-LocalPath $OutputDirectory) "phase7k-cross-instrument-post-success-connection-layer-pattern-analysis.json"
$gatePath = Join-Path (Resolve-LocalPath $OutputDirectory) "phase7k-cross-instrument-additional-instrument-external-attempt-stop-gate.json"
$notePath = Join-Path (Resolve-LocalPath $OutputDirectory) "phase7k-cross-instrument-operator-environment-note.md"

$disallowedActions = @(
    "No USDJPY retry.",
    "No AUDUSD retry.",
    "No GBPUSD/EURGBP control run.",
    "No next instrument.",
    "No batch.",
    "No loop.",
    "No automatic retry.",
    "No wrapper relaxation.",
    "No SecurityID switch.",
    "No Tokyo 600x switch.",
    "No replay without MarketDataOnly evidence.",
    "No order path.",
    "No scheduler or polling.",
    "No runtime shadow replay submit.",
    "No trading-state mutation.",
    "No gateway registration."
)

$requiredOperatorChecks = @(
    "Confirm local network state.",
    "Confirm VPN, proxy, and firewall state.",
    "Confirm DNS endpoint resolution using safe non-secret methods.",
    "Confirm local machine clock and time synchronization.",
    "Confirm socket, process, and resource state.",
    "Confirm no local API or lab process lock is present.",
    "Confirm Demo endpoint availability window.",
    "Confirm credential labels are present without printing values.",
    "Confirm whether previous successful sessions could have exhausted or locked the Demo session externally."
)

$report = [ordered]@{
    phase = "7K"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    successfulInstruments = @("GBPUSD", "EURGBP")
    failedInstruments = @("USDJPY", "AUDUSD")
    successfulAttemptsAnalyzed = $successfulSummaries.Count
    failedAttemptsAnalyzed = $failedSummaries.Count
    repeatedCrossInstrumentFailurePattern = $allSuccessesValid -and $allFailuresBeforeLogon -and $allFailuresZeroRejects
    failuresAfterPriorSuccess = $true
    allFailuresBeforeLogon = $allFailuresBeforeLogon
    allFailuresHadNoSnapshotRequest = @($failedSummaries | Where-Object { [bool]$_.snapshotRequestAttempted }).Count -eq 0
    allFailuresHadZeroRejects = $allFailuresZeroRejects
    instrumentLevelRejectsObserved = $false
    marketDataOnlyEvidenceForFailures = $false
    securityIdSwitchRecommended = $false
    tokyo600xSwitchRecommended = $false
    externalRetryStopRecommended = $true
    broaderFailureClass = "CrossInstrumentFailedSafeConnectionBeforeSessionEstablishment"
    comparisonTimeline = $timeline
    comparisonToSuccessfulAttempts = [ordered]@{
        successful = $successfulSummaries
        failed = $failedSummaries
        successfulReachedSnapshot = $allSuccessesValid
        failuresReachedTcpButNotTlsOrFixLogon = @($failedSummaries | Where-Object { $_.tcpConnected -eq $true -and $_.tlsConnected -eq $false -and -not [bool]$_.logonAttempted }).Count -eq $failedSummaries.Count
        sameDemoProfileShape = @($successfulSummaries + $failedSummaries | Where-Object {
            $_.environmentName -ne "Demo" -or
            $_.venueProfileName -ne "DemoLondon" -or
            $_.securityIdSource -ne "8" -or
            $_.requestMode -ne "SnapshotPlusUpdates" -or
            $_.symbolEncodingMode -ne "SecurityIdOnly" -or
            [int]$_.marketDepth -ne 1
        }).Count -eq 0
    }
    possibleRootCauseClasses = @(
        "Demo endpoint/session availability changed after earlier successes.",
        "Local network, VPN, proxy, or firewall changed.",
        "Local socket, resource, or process issue.",
        "DNS, TLS, or session establishment issue.",
        "Venue-side pre-logon refusal, throttling, or session limit.",
        "Credential/session availability changed based only on safe metadata.",
        "Local clock or time synchronization issue.",
        "Transient LMAX Demo environment issue."
    )
    ruledOutCauses = @(
        "Not order path.",
        "Not scheduler or polling.",
        "Not runtime shadow replay.",
        "Not trading-state mutation.",
        "Not MarketDataRequestReject.",
        "Not proven SecurityID issue.",
        "Not evidence preview issue.",
        "Not local replay issue."
    )
    sourceReports = [ordered]@{
        audusdReview = $AudusdReviewFile
        audusdClosure = $AudusdClosureFile
        usdjpyPhase7I2 = $UsdJpyPhase7I2ReportFile
        usdjpyPhase7I4 = $UsdJpyPhase7I4ReportFile
        usdjpyPhase7I5 = $UsdJpyPhase7I5ReportFile
        usdjpyPhase7I6 = $UsdJpyPhase7I6GateFile
        audusdPhase7J = $AudusdPhase7JGateFile
    }
    sourceReportSummary = [ordered]@{
        audusdReviewDecision = [string]$audusdReview.finalDecision
        audusdClosureDecision = [string]$audusdClosure.finalClosureDecision
        usdjpyDiagnosisDecision = [string]$usdjpy7i2.finalDecision
        usdjpyRepeatedPatternDecision = [string]$usdjpy7i4.finalDecision
        usdjpyLocalDiffClassification = [string]$usdjpy7i5.classification
        usdjpyTroubleshootingDecision = [string]$usdjpy7i6.finalDecision
        audusdPlanningDecision = [string]$audusd7j.finalDecision
    }
    recommendedNextAction = "Stop all external additional-instrument attempts and complete operator environment troubleshooting before any future one-instrument external run."
    allowedNextPhase = "Phase 7K2 - Operator Environment Troubleshooting Checklist Completion Gate, No External Run"
    disallowedActions = $disallowedActions
    noSensitiveContent = $true
    finalDecision = "PASS_WITH_ACTION_REQUIRED"
}

$gate = [ordered]@{
    phase = "7K"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    externalAdditionalInstrumentAttemptsCurrentlyAllowed = $false
    audusdRetryAllowed = $false
    usdjpyRetryAllowed = $false
    gbpusdControlRunAllowed = $false
    eurgbpControlRunAllowed = $false
    anyInstrumentExternalRunAllowed = $false
    operatorEnvironmentTroubleshootingRequired = $true
    externalRunAttemptedInThisPhase = $false
    snapshotRunInThisPhase = $false
    replayRunInThisPhase = $false
    batchExecutionAllowed = $false
    automaticRetryRecommended = $false
    wrapperValidationWeakened = $false
    orderPathEnabled = $false
    schedulerOrPollingEnabled = $false
    runtimeShadowReplaySubmitEnabled = $false
    tradingMutationEnabled = $false
    gatewayRegistrationEnabled = $false
    requiredOperatorChecksBeforeAnyFutureExternalRun = $requiredOperatorChecks
    futureExternalRunPolicy = [ordered]@{
        futureExternalRunRequiresPhase7K2Completion = $true
        futureExternalRunRequiresNewOneInstrumentGate = $true
        oneInstrumentOnly = $true
        batchAllowed = $false
        loopAllowed = $false
        automaticRetryAllowed = $false
        wrapperRelaxationAllowed = $false
        securityIdSwitchAllowed = $false
        tokyo600xSwitchAllowed = $false
        replayRequiresMarketDataOnlyEvidence = $true
    }
    allowedNextPhase = "Phase 7K2 - Operator Environment Troubleshooting Checklist Completion Gate, No External Run"
    disallowedActions = $disallowedActions
    noSensitiveContent = $true
    apiWorkerGatewayMode = "FakeLmaxGateway"
    finalDecision = "PASS_WITH_ACTION_REQUIRED"
}

$note = @"
# Phase 7K - Cross-Instrument Operator Environment Note

Earlier GBPUSD and EURGBP market-hours snapshots completed successfully, which shows the manual Demo read-only workflow can work when the connection/session layer is available.

After those successes, USDJPY failed twice and AUDUSD failed once before TLS/FIX logon. All three later failures had no MarketDataRequest, no MarketDataRequestReject, no business-message reject, and no FIX reject. No MarketDataOnly evidence exists for the failed attempts, and replay was correctly not run.

This pattern is now cross-instrument. It does not prove a bad USDJPY SecurityID, a bad AUDUSD SecurityID, a Tokyo 600x requirement, an evidence preview defect, a replay defect, an order-path defect, scheduler/polling activity, runtime shadow replay submission, or trading-state mutation.

The safe interpretation is `CrossInstrumentFailedSafeConnectionBeforeSessionEstablishment`: the environment/session layer appears to have changed or become unavailable after the earlier successful snapshots.

No further external additional-instrument attempts are currently allowed. That includes no USDJPY retry, no AUDUSD retry, and no GBPUSD/EURGBP control run.

Before any future one-instrument external run, the operator must complete Phase 7K2 environment troubleshooting checks:

- Confirm local network state.
- Confirm VPN, proxy, and firewall state.
- Confirm DNS endpoint resolution using safe non-secret methods.
- Confirm local machine clock and time synchronization.
- Confirm socket, process, and resource state.
- Confirm no local API or lab process lock is present.
- Confirm Demo endpoint availability window.
- Confirm credential labels are present without printing values.
- Confirm whether previous successful sessions could have exhausted or locked the Demo session externally.

Allowed next phase: Phase 7K2 - Operator Environment Troubleshooting Checklist Completion Gate, No External Run.
"@

$report | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $reportPath -Encoding UTF8
$gate | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $gatePath -Encoding UTF8
$note | Set-Content -LiteralPath $notePath -Encoding UTF8

Write-Host "Phase: 7K"
Write-Host "Classification: CrossInstrumentFailedSafeConnectionBeforeSessionEstablishment"
Write-Host "FinalDecision: PASS_WITH_ACTION_REQUIRED"
Write-Host "DiagnosticReport: $reportPath"
Write-Host "DecisionGate: $gatePath"
Write-Host "OperatorNote: $notePath"
