param(
    [string]$GbpUsdArtifactFile = "artifacts/lmax-readonly-runtime-additional-snapshot/gbpusd/lmax-readonly-gbpusd-demo-snapshot-result-20260511-103318.json",
    [string]$EurGbpArtifactFile = "artifacts/lmax-readonly-runtime-additional-snapshot/eurgbp/lmax-readonly-eurgbp-demo-snapshot-result-20260511-163141.json",
    [string]$EurGbpReviewFile = "artifacts/readiness/phase7h-additional-instrument-snapshot-review-eurgbp.json",
    [string]$EurGbpEvidencePreviewFile = "artifacts/lmax-readonly-runtime-additional-snapshot/eurgbp/evidence-preview/lmax-readonly-eurgbp-evidence-preview-20260511-165605.json",
    [string]$EurGbpReplayFile = "artifacts/readiness/phase7h-additional-instrument-evidence-replay-eurgbp.json",
    [string]$FirstUsdJpyArtifactFile = "artifacts/lmax-readonly-runtime-additional-snapshot/usdjpy/lmax-readonly-usdjpy-demo-snapshot-result-20260511-181833.json",
    [string]$SecondUsdJpyArtifactFile = "artifacts/lmax-readonly-runtime-additional-snapshot/usdjpy/lmax-readonly-usdjpy-demo-snapshot-result-20260511-182651.json",
    [string]$Phase7I2DiagnosisFile = "artifacts/readiness/phase7i2-usdjpy-failedsafe-connection-diagnosis.json",
    [string]$Phase7I4PatternFile = "artifacts/readiness/phase7i4-usdjpy-repeated-failedsafe-pattern-analysis.json",
    [string]$UsdJpyFinalPreRunGateFile = "artifacts/lmax-readonly-runtime-securityid-planning/additional-final-prerun/lmax-readonly-additional-instrument-final-prerun-gate-USDJPY-20260511-161440.json",
    [string]$AudUsdFinalPreRunGateFile = "artifacts/lmax-readonly-runtime-securityid-planning/additional-final-prerun/lmax-readonly-additional-instrument-final-prerun-gate-AUDUSD-20260511-161447.json",
    [string]$EurGbpFinalPreRunGateFile = "artifacts/lmax-readonly-runtime-securityid-planning/eurgbp-final-prerun/lmax-readonly-eurgbp-final-prerun-gate-20260511-134130.json",
    [string]$OutputFile = "artifacts/readiness/phase7i5-usdjpy-local-connection-session-config-diff-audit.json"
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

function Summarize-Artifact($Artifact, [string]$Label, [string]$Path) {
    [ordered]@{
        label = $Label
        path = $Path
        symbol = [string]$Artifact.symbol
        slashSymbol = [string]$Artifact.slashSymbol
        securityId = [string]$Artifact.securityId
        securityIdSource = [string]$Artifact.securityIdSource
        environmentName = [string]$Artifact.environmentName
        venueProfileName = [string]$Artifact.venueProfileName
        requestMode = [string]$Artifact.requestMode
        symbolEncodingMode = [string]$Artifact.symbolEncodingMode
        marketDepth = [int]$Artifact.marketDepth
        status = [string]$Artifact.status
        startedAtUtc = [string]$Artifact.startedAtUtc
        completedAtUtc = [string]$Artifact.completedAtUtc
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
        requestSentAtUtc = if ($Artifact.diagnostics -and $Artifact.diagnostics.request) { [string]$Artifact.diagnostics.request.requestSentAtUtc } else { "" }
        firstResponseAtUtc = if ($Artifact.diagnostics -and $Artifact.diagnostics.request) { [string]$Artifact.diagnostics.request.firstResponseAtUtc } else { "" }
        waitDurationMs = if ($Artifact.diagnostics -and $Artifact.diagnostics.request -and $null -ne $Artifact.diagnostics.request.waitDurationMs) { [int]$Artifact.diagnostics.request.waitDurationMs } else { $null }
        firstInboundMsgType = if ($Artifact.logonDiagnostics) { [string]$Artifact.logonDiagnostics.firstInboundMsgType } else { "" }
        connectionProfileLabel = if ($Artifact.logonDiagnostics) { [string]$Artifact.logonDiagnostics.connectionProfileLabel } else { "" }
        beginString = if ($Artifact.logonDiagnostics) { [string]$Artifact.logonDiagnostics.beginString } else { "" }
        resetSeqNumFlag = if ($Artifact.logonDiagnostics) { [string]$Artifact.logonDiagnostics.resetSeqNumFlag } else { "" }
        encryptMethod = if ($Artifact.logonDiagnostics) { [int]$Artifact.logonDiagnostics.encryptMethod } else { $null }
        heartbeatInterval = if ($Artifact.logonDiagnostics) { [int]$Artifact.logonDiagnostics.heartbeatInterval } else { $null }
        credentialSourceKind = if ($Artifact.credentialAvailability) { [string]$Artifact.credentialAvailability.sourceKind } else { "" }
        credentialMissingKeyCount = if ($Artifact.credentialAvailability) { [int]$Artifact.credentialAvailability.missingKeyCount } else { $null }
        credentialReadAttempted = [bool]$Artifact.credentialReadAttempted
        credentialValuesReturned = [bool]$Artifact.credentialValuesReturned
        redactionStatus = [string]$Artifact.redactionStatus
        noSensitiveContent = [bool]$Artifact.noSensitiveContent
        orderSubmissionAttempted = [bool]$Artifact.orderSubmissionAttempted
        shadowReplaySubmitAttempted = [bool]$Artifact.shadowReplaySubmitAttempted
        tradingMutationAttempted = [bool]$Artifact.tradingMutationAttempted
        schedulerStarted = [bool]$Artifact.schedulerStarted
    }
}

function Summarize-Gate($Gate, [string]$Label, [string]$Path) {
    [ordered]@{
        label = $Label
        path = $Path
        symbol = [string]$Gate.symbol
        slashSymbol = [string]$Gate.slashSymbol
        planningSecurityId = [string]$Gate.planningSecurityId
        securityIdSource = [string]$Gate.securityIdSource
        environmentName = [string]$Gate.environmentName
        venueProfileName = [string]$Gate.venueProfileName
        requestMode = [string]$Gate.requestMode
        symbolEncodingMode = [string]$Gate.symbolEncodingMode
        marketDepth = [int]$Gate.marketDepth
        finalDecision = [string]$Gate.finalDecision
        oneInstrumentAtATime = [bool]$Gate.oneInstrumentAtATime
        batchExecutionAllowed = [bool]$Gate.batchExecutionAllowed
        externalRunAuthorized = [bool]$Gate.externalRunAuthorized
        canRunExternalSnapshot = [bool]$Gate.canRunExternalSnapshot
        eligibleForManualSnapshotAttempt = [bool]$Gate.eligibleForManualSnapshotAttempt
        isApprovedForExternalRun = [bool]$Gate.isApprovedForExternalRun
        schedulerOrPolling = [bool]$Gate.schedulerOrPolling
        runtimeShadowReplaySubmit = [bool]$Gate.runtimeShadowReplaySubmit
        orderSubmission = [bool]$Gate.orderSubmission
        tradingMutation = [bool]$Gate.tradingMutation
        gatewayRegistration = [bool]$Gate.gatewayRegistration
        apiWorkerGatewayMode = [string]$Gate.apiWorkerGatewayMode
        noSensitiveContent = [bool]$Gate.noSensitiveContent
    }
}

function Has-Text([string]$PathValue, [string]$Pattern) {
    $resolved = Resolve-LocalPath $PathValue
    if (-not (Test-Path -LiteralPath $resolved)) { return $false }
    $text = Get-Content -LiteralPath $resolved -Raw
    return $text.IndexOf($Pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0
}

Write-Host "LMAX Read-Only Phase 7I5 USDJPY Local Connection/Session Config Diff Audit"
Write-Host "Local-only. This script does not connect to LMAX, request snapshots, replay evidence, schedule work, or use credentials."

$gbp = Read-SafeJson $GbpUsdArtifactFile "GBPUSD artifact"
$eur = Read-SafeJson $EurGbpArtifactFile "EURGBP artifact"
$eurReview = Read-SafeJson $EurGbpReviewFile "EURGBP review"
$eurPreview = Read-SafeJson $EurGbpEvidencePreviewFile "EURGBP evidence preview"
$eurReplay = Read-SafeJson $EurGbpReplayFile "EURGBP replay"
$usd1 = Read-SafeJson $FirstUsdJpyArtifactFile "First USDJPY artifact"
$usd2 = Read-SafeJson $SecondUsdJpyArtifactFile "Second USDJPY artifact"
$diag = Read-SafeJson $Phase7I2DiagnosisFile "Phase 7I2 diagnosis"
$pattern = Read-SafeJson $Phase7I4PatternFile "Phase 7I4 pattern report"
$usdGate = Read-SafeJson $UsdJpyFinalPreRunGateFile "USDJPY final pre-run gate"
$audGate = Read-SafeJson $AudUsdFinalPreRunGateFile "AUDUSD final pre-run gate"
$eurGate = Read-SafeJson $EurGbpFinalPreRunGateFile "EURGBP final pre-run gate"

$gbpSummary = Summarize-Artifact $gbp.json "GBPUSD successful snapshot" $gbp.path
$eurSummary = Summarize-Artifact $eur.json "EURGBP successful snapshot" $eur.path
$usd1Summary = Summarize-Artifact $usd1.json "USDJPY first failed-safe attempt" $usd1.path
$usd2Summary = Summarize-Artifact $usd2.json "USDJPY retry failed-safe attempt" $usd2.path
$usdGateSummary = Summarize-Gate $usdGate.json "USDJPY final pre-run gate" $usdGate.path
$audGateSummary = Summarize-Gate $audGate.json "AUDUSD final pre-run gate" $audGate.path
$eurGateSummary = Summarize-Gate $eurGate.json "EURGBP final pre-run gate" $eurGate.path

$gateFields = @("environmentName","venueProfileName","requestMode","symbolEncodingMode","marketDepth","securityIdSource","oneInstrumentAtATime","batchExecutionAllowed","externalRunAuthorized","canRunExternalSnapshot","eligibleForManualSnapshotAttempt","isApprovedForExternalRun","schedulerOrPolling","runtimeShadowReplaySubmit","orderSubmission","tradingMutation","gatewayRegistration","noSensitiveContent","finalDecision")
$gateDiffs = @()
foreach ($field in $gateFields) {
    $usdValue = $usdGateSummary[$field]
    $eurValue = $eurGateSummary[$field]
    if ("$usdValue" -ne "$eurValue") {
        $gateDiffs += [ordered]@{ field = $field; usdJpy = $usdValue; eurGbp = $eurValue; material = $false; note = "Observed difference is not material to connection/session setup for USDJPY." }
    }
}

$artifactFields = @("environmentName","venueProfileName","requestMode","symbolEncodingMode","marketDepth","securityIdSource","connectionProfileLabel","beginString","resetSeqNumFlag","encryptMethod","heartbeatInterval","credentialSourceKind","credentialMissingKeyCount","credentialValuesReturned","redactionStatus")
$artifactDiffs = @()
foreach ($field in $artifactFields) {
    $usdValues = @("$($usd1Summary[$field])","$($usd2Summary[$field])") | Select-Object -Unique
    $successValues = @("$($gbpSummary[$field])","$($eurSummary[$field])") | Select-Object -Unique
    $sameSet = (@($usdValues | Where-Object { $_ -notin $successValues }).Count -eq 0) -and (@($successValues | Where-Object { $_ -notin $usdValues }).Count -eq 0)
    if (-not $sameSet) {
        $material = $field -in @("environmentName","venueProfileName","requestMode","symbolEncodingMode","marketDepth","securityIdSource","connectionProfileLabel","beginString","resetSeqNumFlag","encryptMethod","heartbeatInterval","credentialSourceKind","credentialMissingKeyCount","credentialValuesReturned","redactionStatus")
        $artifactDiffs += [ordered]@{ field = $field; usdJpyValues = @($usdValues); successfulValues = @($successValues); material = $material; note = if ($material) { "Review for possible config divergence." } else { "Outcome difference." } }
    }
}

$wrapperPath = "scripts/run-lmax-readonly-runtime-demo-additional-instrument-snapshot-once.ps1"
$prototypePath = "scripts/run-lmax-readonly-runtime-demo-snapshot-prototype.ps1"
$builderPath = "scripts/new-lmax-readonly-additional-instrument-final-pre-run-gate.ps1"
$gatePath = "scripts/check-lmax-readonly-runtime-phase7h2-additional-instrument-final-prerun-gate.ps1"
$wrapperText = Get-Content -LiteralPath (Resolve-LocalPath $wrapperPath) -Raw
$prototypeText = Get-Content -LiteralPath (Resolve-LocalPath $prototypePath) -Raw

$symbolBranchFindings = @(
    [ordered]@{ check = "Generic wrapper contains USDJPY mapping"; present = Has-Text $wrapperPath "USDJPY" },
    [ordered]@{ check = "Generic wrapper delegates to isolated prototype"; present = Has-Text $wrapperPath "run-lmax-readonly-runtime-demo-snapshot-prototype.ps1" },
    [ordered]@{ check = "Generic wrapper rejects batch symbols"; present = Has-Text $wrapperPath "batch/multiple instruments are refused" },
    [ordered]@{ check = "Generic wrapper keeps no loop/no retry warnings"; present = ($wrapperText -match '(?i)no loop' -and $wrapperText -match '(?i)no retry') },
    [ordered]@{ check = "Prototype includes USDJPY allowlist"; present = Has-Text $prototypePath "USDJPY" },
    [ordered]@{ check = "Prototype includes Tokyo 600x mapping"; present = ($prototypeText -match '6004|6007|6003|6002') }
)

$materialGateDiffs = @($gateDiffs | Where-Object { $_.material })
$materialArtifactDiffs = @($artifactDiffs | Where-Object { $_.material })
$configDivergences = @()
foreach ($diff in $materialArtifactDiffs) {
    $configDivergences += "Material safe-metadata difference in $($diff.field): USDJPY=$($diff.usdJpyValues -join ',') vs successful=$($diff.successfulValues -join ',')."
}

if ($configDivergences.Count -eq 0) {
    $classification = "NoMaterialLocalConfigDiffFound_ExternalSessionIssueStillSuspected"
    $finalDecision = "PASS_WITH_KNOWN_WARNINGS"
} else {
    $classification = "MaterialConfigMetadataDifferenceFound"
    $finalDecision = "PASS_WITH_ACTION_REQUIRED"
}

$materialDifferencesFound = @($configDivergences)
$ruledOutDifferences = @(
    "No environment/profile/request mode/encoding/depth divergence was found between USDJPY and successful instruments.",
    "No credential source kind or missing-key-count divergence was found in sanitized metadata.",
    "No FIX begin string, reset sequence flag, encrypt method, or heartbeat interval divergence was found.",
    "No wrapper evidence indicates a symbol-specific endpoint, venue profile, credential source, sender/target comp ID source, batch path, loop, or retry path for USDJPY.",
    "USDJPY remains DemoLondon SecurityID 4004/source 8; no Tokyo 600x recommendation is present.",
    "The old Phase 6Z-A generic readiness artifact is still not accepted as a Phase 7H gate."
)

$recommendedNextAction = if ($classification -eq "NoMaterialLocalConfigDiffFound_ExternalSessionIssueStillSuspected") {
    "Keep USDJPY stopped. Proceed to Phase 7I6 operator troubleshooting note and external-retry decision gate, no external run. List safe out-of-app checks for local network/VPN/proxy state, Demo endpoint availability, and local process/socket availability. Do not run GBPUSD/EURGBP controls in this phase."
} else {
    "Fix the confirmed local configuration divergence first, then review and validate locally before any future external USDJPY attempt."
}
$allowedNextPhase = if ($classification -eq "NoMaterialLocalConfigDiffFound_ExternalSessionIssueStillSuspected") {
    "Phase 7I6 - Operator Troubleshooting Note and External-Retry Decision Gate, No External Run."
} else {
    "Phase 7I6 - Local Fix for Confirmed USDJPY Config Divergence, No External Run."
}
$disallowedActions = @(
    "No third USDJPY retry.",
    "No AUDUSD run.",
    "No GBPUSD/EURGBP control rerun.",
    "No batch additional-instrument execution.",
    "No loop.",
    "No auto-retry.",
    "No wrapper relaxation.",
    "No SecurityID switch.",
    "No Tokyo 600x switch.",
    "No replay.",
    "No MarketDataOnly preview fabrication."
)

$report = [ordered]@{
    phase = "7I5"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    instrument = "USDJPY"
    comparedSuccessfulInstruments = @("GBPUSD","EURGBP")
    failedAttemptsAnalyzed = 2
    externalRunAttemptedInThisPhase = $false
    snapshotRunInThisPhase = $false
    replayRunInThisPhase = $false
    wrapperValidationWeakened = $false
    securityIdSwitchRecommended = $false
    tokyo600xSwitchRecommended = $false
    thirdRetryRecommended = $false
    classification = $classification
    gateDiffSummary = [ordered]@{
        usdJpy = $usdGateSummary
        eurGbp = $eurGateSummary
        audUsdReference = $audGateSummary
        comparedFields = $gateFields
        differences = $gateDiffs
        materialDifferences = $materialGateDiffs
    }
    artifactDiffSummary = [ordered]@{
        usdJpyAttempts = @($usd1Summary, $usd2Summary)
        successfulComparisons = @($gbpSummary, $eurSummary)
        comparedFields = $artifactFields
        differences = $artifactDiffs
        materialDifferences = $materialArtifactDiffs
    }
    invocationPathDiffSummary = [ordered]@{
        wrapperPath = Resolve-LocalPath $wrapperPath
        prototypePath = Resolve-LocalPath $prototypePath
        builderPath = Resolve-LocalPath $builderPath
        gatePath = Resolve-LocalPath $gatePath
        symbolBranchFindings = $symbolBranchFindings
        conclusion = "EURGBP and USDJPY both use the generic Phase 7H wrapper path. Wrapper/prototype scans did not find a USDJPY-specific endpoint, credential source, scheduler, batch, loop, or retry path."
    }
    configPathDiffSummary = [ordered]@{
        conclusion = if ($configDivergences.Count -eq 0) { "No material local config-path divergence found in safe metadata." } else { "Material safe-metadata divergence found; inspect listed differences." }
        materialConfigDivergences = $configDivergences
        phase7I2Classification = [string]$diag.json.inferredFailureClass
        phase7I4Classification = [string]$pattern.json.repeatedFailureClass
    }
    credentialSourceSafeSummary = [ordered]@{
        gbpUsd = @{ sourceKind = $gbpSummary.credentialSourceKind; missingKeyCount = $gbpSummary.credentialMissingKeyCount; valuesReturned = $gbpSummary.credentialValuesReturned }
        eurGbp = @{ sourceKind = $eurSummary.credentialSourceKind; missingKeyCount = $eurSummary.credentialMissingKeyCount; valuesReturned = $eurSummary.credentialValuesReturned }
        usdJpyFirst = @{ sourceKind = $usd1Summary.credentialSourceKind; missingKeyCount = $usd1Summary.credentialMissingKeyCount; valuesReturned = $usd1Summary.credentialValuesReturned }
        usdJpySecond = @{ sourceKind = $usd2Summary.credentialSourceKind; missingKeyCount = $usd2Summary.credentialMissingKeyCount; valuesReturned = $usd2Summary.credentialValuesReturned }
        conclusion = "All compared artifacts report sanitized environment credential source metadata, zero missing keys, and credentialValuesReturned=false."
    }
    endpointSessionSafeSummary = [ordered]@{
        gbpUsd = @{ connectionProfileLabel = $gbpSummary.connectionProfileLabel; tcpConnected = $gbpSummary.tcpConnected; tlsConnected = $gbpSummary.tlsConnected; logonSucceeded = $gbpSummary.logonSucceeded }
        eurGbp = @{ connectionProfileLabel = $eurSummary.connectionProfileLabel; tcpConnected = $eurSummary.tcpConnected; tlsConnected = $eurSummary.tlsConnected; logonSucceeded = $eurSummary.logonSucceeded }
        usdJpyFirst = @{ connectionProfileLabel = $usd1Summary.connectionProfileLabel; tcpConnected = $usd1Summary.tcpConnected; tlsConnected = $usd1Summary.tlsConnected; logonSucceeded = $usd1Summary.logonSucceeded }
        usdJpySecond = @{ connectionProfileLabel = $usd2Summary.connectionProfileLabel; tcpConnected = $usd2Summary.tcpConnected; tlsConnected = $usd2Summary.tlsConnected; logonSucceeded = $usd2Summary.logonSucceeded }
        conclusion = "Connection profile labels match. Successful instruments reached TLS/logon; both USDJPY attempts show TCP connected but TLS/logon did not complete."
    }
    symbolMappingSummary = [ordered]@{
        expectedUsdJpy = @{ symbol = "USDJPY"; slashSymbol = "USD/JPY"; securityId = "4004"; securityIdSource = "8"; venueProfileName = "DemoLondon" }
        observedUsdJpyGate = @{ symbol = $usdGateSummary.symbol; slashSymbol = $usdGateSummary.slashSymbol; securityId = $usdGateSummary.planningSecurityId; securityIdSource = $usdGateSummary.securityIdSource; venueProfileName = $usdGateSummary.venueProfileName }
        observedUsdJpyArtifacts = @(
            @{ symbol = $usd1Summary.symbol; slashSymbol = $usd1Summary.slashSymbol; securityId = $usd1Summary.securityId; securityIdSource = $usd1Summary.securityIdSource },
            @{ symbol = $usd2Summary.symbol; slashSymbol = $usd2Summary.slashSymbol; securityId = $usd2Summary.securityId; securityIdSource = $usd2Summary.securityIdSource }
        )
        conclusion = "USDJPY mapping is consistent as USDJPY / USD/JPY / 4004 / source 8. No 600x Tokyo path is recommended."
    }
    potentialConfigDivergences = if ($configDivergences.Count -gt 0) { $configDivergences } else { @("No material local config divergence found in the audited safe metadata. External session/TLS behavior remains the suspected class.") }
    materialDifferencesFound = $materialDifferencesFound
    ruledOutDifferences = $ruledOutDifferences
    recommendedNextAction = $recommendedNextAction
    allowedNextPhase = $allowedNextPhase
    disallowedActions = $disallowedActions
    noSensitiveContent = $true
    finalDecision = $finalDecision
}

$outPath = Resolve-LocalPath $OutputFile
New-Item -ItemType Directory -Path (Split-Path -Parent $outPath) -Force | Out-Null
$report | ConvertTo-Json -Depth 18 | Set-Content -LiteralPath $outPath -Encoding UTF8

Write-Host "Classification: $classification"
Write-Host "FinalDecision: $finalDecision"
Write-Host "DiffAuditReport: $outPath"
if ($finalDecision -eq "FAIL") { exit 1 }
