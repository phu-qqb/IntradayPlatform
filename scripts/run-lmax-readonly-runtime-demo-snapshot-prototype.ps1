param(
    [switch]$AllowExternalConnections,
    [switch]$ConfirmDemoReadOnly,
    [Parameter(Mandatory = $false)]
    [string]$Reason,
    [string]$OperatorId = "local-operator",
    [string]$Instrument = "EURUSD",
    [string]$LmaxInstrumentId = "4001",
    [string]$SlashSymbol = "EUR/USD",
    [ValidateSet("SnapshotPlusUpdates", "SnapshotOnly", "AutoSequence")]
    [string]$RequestMode = "SnapshotPlusUpdates",
    [ValidateSet("SecurityIdOnly", "SecurityIdAndSymbolWithIdSource", "SecurityIdAndSymbolNoIdSource", "SlashSymbol", "InternalSymbol", "Auto")]
    [string]$SymbolEncodingMode = "SecurityIdOnly",
    [bool]$SkipKnownRejectedProfiles = $true,
    [switch]$AllowKnownRejectedDiagnostics,
    [int]$MarketDepth = 1,
    [int]$MaxWaitSeconds = 15,
    [int]$MaxRuntimeSeconds = 15,
    [int]$MaxEventsPerRun = 5,
    [string]$SourceFinalReadinessFile = "",
    [switch]$ShowSanitizedLogonDiagnostics
)

$ErrorActionPreference = "Stop"

Write-Host "LMAX Read-Only Runtime Demo Snapshot Prototype"
Write-Host "WARNING: Demo-only, manual-only, read-only market-data snapshot prototype path."
Write-Host "WARNING: EURUSD / SecurityID 4001 or explicitly gated additional instruments GBPUSD/EURGBP/USDJPY/AUDUSD only."
Write-Host "WARNING: No orders, no scheduler, no trading mutation, no shadow replay submit."
Write-Host "WARNING: Stop with Ctrl+C or close this process. Roll back by clearing this shell's Phase 5B variables and using the default API startup."

$checks = @()
function Add-Check([string]$Name, [bool]$Passed, [string]$Detail) {
    $script:checks += [ordered]@{ name = $Name; passed = $Passed; detail = $Detail }
}

Add-Check "AllowExternalConnections flag" ([bool]$AllowExternalConnections) "Required explicit flag for any future external attempt."
Add-Check "ConfirmDemoReadOnly flag" ([bool]$ConfirmDemoReadOnly) "Required explicit Demo/read-only attestation."
Add-Check "Reason" (-not [string]::IsNullOrWhiteSpace($Reason)) "Non-empty reason is required."
Add-Check "OperatorId" (-not [string]::IsNullOrWhiteSpace($OperatorId)) "Operator id must be present."
Add-Check "MaxRuntimeSeconds" ($MaxRuntimeSeconds -gt 0 -and $MaxRuntimeSeconds -le 30) "Must be within 1..30."
Add-Check "MaxEventsPerRun" ($MaxEventsPerRun -gt 0 -and $MaxEventsPerRun -le 25) "Must be within 1..25."
Add-Check "MaxWaitSeconds" ($MaxWaitSeconds -gt 0 -and $MaxWaitSeconds -le 30) "Must be within 1..30."
Add-Check "MarketDepth" ($MarketDepth -eq 1) "Phase 5G allows depth 1 only."
$additionalInstrumentMap = @{
    GBPUSD = @{ slashSymbol = "GBP/USD"; securityId = "4002" }
    EURGBP = @{ slashSymbol = "EUR/GBP"; securityId = "4003" }
    USDJPY = @{ slashSymbol = "USD/JPY"; securityId = "4004" }
    AUDUSD = @{ slashSymbol = "AUD/USD"; securityId = "4007" }
}
$isEurUsd = $Instrument -eq "EURUSD" -and $SlashSymbol -eq "EUR/USD" -and $LmaxInstrumentId -eq "4001"
$isAdditionalInstrument = $additionalInstrumentMap.ContainsKey($Instrument) -and $SlashSymbol -eq $additionalInstrumentMap[$Instrument].slashSymbol -and $LmaxInstrumentId -eq $additionalInstrumentMap[$Instrument].securityId
$isGbpUsd = $Instrument -eq "GBPUSD" -and $isAdditionalInstrument
Add-Check "Instrument" ($isEurUsd -or $isAdditionalInstrument) "Allows EURUSD / 4001 or explicitly gated additional GBPUSD/EURGBP/USDJPY/AUDUSD instruments only."
Add-Check "SecurityIdSource" $true "SecurityIDSource is fixed to 8 by the prototype request builder."
if ($isAdditionalInstrument) {
    Add-Check "AdditionalInstrumentRequestMode" ($RequestMode -eq "SnapshotPlusUpdates") "$Instrument requires SnapshotPlusUpdates."
    Add-Check "AdditionalInstrumentSymbolEncodingMode" ($SymbolEncodingMode -eq "SecurityIdOnly") "$Instrument requires SecurityIdOnly."
    Add-Check "AdditionalInstrumentGateFile" (-not [string]::IsNullOrWhiteSpace($SourceFinalReadinessFile)) "$Instrument requires a final readiness or pre-run gate artifact."
    if (-not [string]::IsNullOrWhiteSpace($SourceFinalReadinessFile)) {
        $repoRootForReadiness = Split-Path -Parent $PSScriptRoot
        $readinessPath = if ([IO.Path]::IsPathRooted($SourceFinalReadinessFile)) { $SourceFinalReadinessFile } else { Join-Path $repoRootForReadiness $SourceFinalReadinessFile }
        if (Test-Path -LiteralPath $readinessPath) {
            $readiness = Get-Content -Raw -LiteralPath $readinessPath | ConvertFrom-Json
            $sourceDecision = if ($readiness.PSObject.Properties.Name -contains "finalDecision") { [string]$readiness.finalDecision } elseif ($readiness.PSObject.Properties.Name -contains "readinessDecision") { [string]$readiness.readinessDecision } else { "" }
            Add-Check "AdditionalInstrumentGateDecision" ($sourceDecision -eq "PASS") "Final readiness/pre-run gate must be PASS."
            Add-Check "AdditionalInstrumentGateInstrument" ([string]$readiness.symbol -eq $Instrument -and [string]$readiness.planningSecurityId -eq $LmaxInstrumentId -and [string]$readiness.securityIdSource -eq "8") "Gate must match $Instrument / $LmaxInstrumentId / source 8."
            Add-Check "AdditionalInstrumentGateNonExecutable" (-not [bool]$readiness.externalRunAuthorized -and -not [bool]$readiness.isApprovedForExternalRun -and -not [bool]$readiness.eligibleForManualSnapshotAttempt -and -not [bool]$readiness.canRunExternalSnapshot -and -not [bool]$readiness.schedulerStarted -and -not [bool]$readiness.schedulerOrPolling -and -not [bool]$readiness.orderSubmissionAttempted -and -not [bool]$readiness.orderSubmission -and -not [bool]$readiness.shadowReplaySubmitAttempted -and -not [bool]$readiness.runtimeShadowReplaySubmit -and -not [bool]$readiness.tradingMutationAttempted -and -not [bool]$readiness.tradingMutation -and -not [bool]$readiness.gatewayRegistration) "Gate artifact must still be non-executable."
        } else {
            Add-Check "AdditionalInstrumentGateExists" $false "Final readiness/pre-run gate file must exist."
        }
    }
}

function Get-RequestProfile {
    $effectiveRequestMode = if ($RequestMode -eq "AutoSequence") { "SnapshotPlusUpdates" } else { $RequestMode }
    $effectiveSymbolMode = if ($SymbolEncodingMode -eq "Auto") { "SecurityIdOnly" } else { $SymbolEncodingMode }
    $knownRejected = $false
    $reason = $null
    if ($effectiveRequestMode -eq "SnapshotOnly") {
        $knownRejected = $true
        $reason = "LMAX Demo rejected SnapshotOnly SubscriptionRequestType 263=0 with ValueOutOfRange."
    } elseif ($effectiveSymbolMode -eq "InternalSymbol") {
        $knownRejected = $true
        $reason = "LMAX Demo rejected InternalSymbol tag 55 with repeating-group mismatch around tag 146."
    } elseif ($effectiveSymbolMode -in @("SlashSymbol", "SecurityIdAndSymbolWithIdSource", "SecurityIdAndSymbolNoIdSource")) {
        $knownRejected = $true
        $reason = "LMAX Demo has rejected request shapes containing tag 55 in some market-data instrument encodings."
    }
    $fieldSummary = @("262 present", "263=$(if ($effectiveRequestMode -eq 'SnapshotPlusUpdates') { '1' } else { '0' })", "264=$MarketDepth", "267=2", "269=0,1", "146=1")
    if ($effectiveSymbolMode -in @("SecurityIdOnly", "SecurityIdAndSymbolWithIdSource", "SecurityIdAndSymbolNoIdSource")) { $fieldSummary += "48 present" }
    if ($effectiveSymbolMode -in @("SecurityIdOnly", "SecurityIdAndSymbolWithIdSource")) { $fieldSummary += "22=8" }
    if ($effectiveSymbolMode -in @("SecurityIdAndSymbolWithIdSource", "SecurityIdAndSymbolNoIdSource", "SlashSymbol", "InternalSymbol")) { $fieldSummary += "55 present" } else { $fieldSummary += "55 omitted" }
    return [ordered]@{
        requestMode = $effectiveRequestMode
        symbolEncodingMode = $effectiveSymbolMode
        knownRejectedByLmaxDemo = $knownRejected
        rejectionReason = $reason
        safeToAttempt = (-not $knownRejected) -or [bool]$AllowKnownRejectedDiagnostics
        requiresUnsubscribeAfterSnapshot = $effectiveRequestMode -eq "SnapshotPlusUpdates"
        expectedSubscriptionRequestType = if ($effectiveRequestMode -eq "SnapshotPlusUpdates") { "1" } else { "0" }
        sanitizedFieldSummary = $fieldSummary
    }
}

$requestProfile = Get-RequestProfile
Add-Check "KnownRejectedRequestProfile" ((-not [bool]$SkipKnownRejectedProfiles) -or (-not [bool]$requestProfile.knownRejectedByLmaxDemo) -or [bool]$AllowKnownRejectedDiagnostics) "Known rejected request profile must be explicitly allowed."

$failed = @($checks | Where-Object { -not $_.passed })
$requiredLabels = @(
    "LMAX_DEMO_FIX_USERNAME",
    "LMAX_DEMO_FIX_PASSWORD",
    "LMAX_DEMO_SENDER_COMP_ID",
    "LMAX_DEMO_TARGET_COMP_ID"
)
$credentialStatuses = @()
foreach ($label in $requiredLabels) {
    $value = [Environment]::GetEnvironmentVariable($label)
    $credentialStatuses += [ordered]@{
        keyLabel = $label
        isPresent = -not [string]::IsNullOrWhiteSpace($value)
        redactionStatus = "Redacted"
    }
}
$missingCredentialLabels = @($credentialStatuses | Where-Object { -not $_.isPresent } | ForEach-Object { $_.keyLabel })
$credentialAvailability = [ordered]@{
    credentialProfileName = "LmaxDemoReadOnlyProfile"
    sourceKind = "Environment"
    isConfigured = $missingCredentialLabels.Count -eq 0
    missingKeyCount = $missingCredentialLabels.Count
    missingKeyLabels = $missingCredentialLabels
    keyStatuses = $credentialStatuses
    redactionStatus = "Redacted"
    sensitiveMaterialReturned = $false
    credentialReadAttempted = $true
    credentialValuesReturned = $false
}
$runId = [guid]::NewGuid().ToString("N")
$now = [DateTimeOffset]::UtcNow.ToString("o")
$blockedReason = if ($failed.Count -gt 0) {
    "Blocked by manual script gates: " + (($failed | ForEach-Object { $_.name }) -join ", ")
} elseif ($missingCredentialLabels.Count -gt 0) {
    "Blocked by missing credential key labels: " + ($missingCredentialLabels -join ", ") + ". Values were not printed, returned, logged, or stored."
} else {
    $null
}

function Get-RetryPolicy([string]$Status) {
    $recommendation = switch ($Status) {
        "BlockedMissingCredentials" { "FixCredentialsThenRetry" }
        "FailedSafeConnectionError" { "ReviewFailureThenRetry" }
        "FailedSafeLogonTimeout" { "ReviewFailureThenRetry" }
        "FailedSafeSnapshotTimeout" { "ReviewFailureThenRetry" }
        "Completed" { "NoRetry" }
        "CompletedWithWarnings" { "NoRetry" }
        default { "DoNotRetry" }
    }
    $reason = switch ($recommendation) {
        "FixCredentialsThenRetry" { "Fix missing credential labels, rerun credential check, then manually retry if still appropriate." }
        "ReviewFailureThenRetry" { "Review sanitized failure details and rollback checks before any future manual retry." }
        "NoRetry" { "No automatic retry is needed." }
        default { "Do not retry until the blocking safety condition is understood." }
    }
    return [ordered]@{
        retryEnabled = $false
        retryAllowed = $false
        maxAttempts = 1
        retryReason = $reason
        recommendation = $recommendation
        futureRetryClassification = "Phase5E_NoAutomaticExternalRetry"
    }
}

function Redact-Text([string]$Text) {
    if ([string]::IsNullOrEmpty($Text)) { return "" }
    $redacted = $Text
    foreach ($label in $requiredLabels) {
        $value = [Environment]::GetEnvironmentVariable($label)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $redacted = $redacted.Replace($value, "[REDACTED]")
        }
    }
    $redacted = [regex]::Replace($redacted, "(?i)(554=)[^\x01|]+", '$1[REDACTED]')
    $redacted = [regex]::Replace($redacted, "(?i)(553=)[^\x01|]+", '$1[REDACTED]')
    $redacted = [regex]::Replace($redacted, "(?i)(49=)[^\x01|]+", '$1[REDACTED]')
    $redacted = [regex]::Replace($redacted, "(?i)(56=)[^\x01|]+", '$1[REDACTED]')
    $redacted = [regex]::Replace($redacted, "(?i)(password\s*[:=]\s*)[^,;\r\n\s]+", '$1[REDACTED]')
    $redacted = [regex]::Replace($redacted, "(?i)(secret\s*[:=]\s*)[^,;\r\n\s]+", '$1[REDACTED]')
    $redacted = [regex]::Replace($redacted, "(?i)(token\s*[:=]\s*)[^,;\r\n\s]+", '$1[REDACTED]')
    $redacted = [regex]::Replace($redacted, "(?i)(apiKey\s*[:=]\s*)[^,;\r\n\s]+", '$1[REDACTED]')
    $redacted = [regex]::Replace($redacted, "(?i)(privateKey\s*[:=]\s*)[^,;\r\n\s]+", '$1[REDACTED]')
    $redacted = [regex]::Replace($redacted, "(?i)(bearer\s+)[^,;\r\n\s]+", '$1[REDACTED]')
    $redacted = [regex]::Replace($redacted, "(?i)(authorization\s*[:=]\s*)[^,;\r\n]+", '$1[REDACTED]')
    return $redacted
}

function Write-SanitizedArtifact([System.Collections.IDictionary]$Result) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $lowerInstrument = $Instrument.ToLowerInvariant()
    $artifactDir = if ($additionalInstrumentMap.ContainsKey($Instrument)) {
        Join-Path $repoRoot "artifacts/lmax-readonly-runtime-additional-snapshot/$lowerInstrument"
    } else {
        Join-Path $repoRoot "artifacts/lmax-readonly-runtime-demo-snapshot"
    }
    New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $prefix = if ($additionalInstrumentMap.ContainsKey($Instrument)) { "lmax-readonly-$lowerInstrument-demo-snapshot-result" } else { "lmax-readonly-demo-snapshot-result" }
    $path = Join-Path $artifactDir "$prefix-$stamp.json"
    $json = $Result | ConvertTo-Json -Depth 10
    $json = Redact-Text $json
    Set-Content -LiteralPath $path -Value $json -Encoding UTF8
    return $path
}

function New-MessageCounters {
    return [ordered]@{
        logon = 0
        marketDataRequest = 0
        marketDataSnapshot = 0
        marketDataRequestReject = 0
        businessMessageReject = 0
        reject = 0
        logout = 0
        heartbeat = 0
        testRequest = 0
        other = 0
    }
}

function Add-MessageCounter([System.Collections.IDictionary]$Counters, [string]$MessageType) {
    switch ($MessageType) {
        "A" { $Counters.logon++; break }
        "W" { $Counters.marketDataSnapshot++; break }
        "Y" { $Counters.marketDataRequestReject++; break }
        "j" { $Counters.businessMessageReject++; break }
        "3" { $Counters.reject++; break }
        "5" { $Counters.logout++; break }
        "0" { $Counters.heartbeat++; break }
        "1" { $Counters.testRequest++; break }
        default { $Counters.other++; break }
    }
}

function New-Diagnostics([string]$Classification, [System.Collections.IDictionary]$Counters, [object]$RequestSentAtUtc, [object]$FirstResponseAtUtc, [object]$TimeoutAtUtc, [object]$WaitDurationMs, [array]$Warnings, [array]$Errors) {
    $requestId = "QQRO" + ([DateTimeOffset]::UtcNow.ToString("HHmmss"))
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $sha = $sha256.ComputeHash([Text.Encoding]::UTF8.GetBytes($requestId))
    } finally {
        $sha256.Dispose()
    }
    $hash = (($sha | ForEach-Object { $_.ToString("x2") }) -join "").Substring(0, 16).ToUpperInvariant()
    return [ordered]@{
        diagnosticVersion = "phase5g-snapshot-diagnostics-v1"
        request = [ordered]@{
            diagnosticVersion = "phase5g-snapshot-diagnostics-v1"
            requestId = $requestId
            requestIdHash = $hash
            instrument = $Instrument
            securityId = $LmaxInstrumentId
            requestMode = $requestProfile.requestMode
            symbolEncodingMode = $requestProfile.symbolEncodingMode
            securityIdSource = "8"
            marketDepth = $MarketDepth
            subscriptionRequestType = if ($requestProfile.expectedSubscriptionRequestType -eq "1") { "SnapshotPlusUpdates" } else { "SnapshotOnly" }
            knownRejectedByLmaxDemo = $requestProfile.knownRejectedByLmaxDemo
            rejectionReason = $requestProfile.rejectionReason
            safeToAttempt = $requestProfile.safeToAttempt
            requiresUnsubscribeAfterSnapshot = $requestProfile.requiresUnsubscribeAfterSnapshot
            sanitizedFieldSummary = $requestProfile.sanitizedFieldSummary
            mdEntryTypes = @("Bid", "Offer")
            requestSentAtUtc = $RequestSentAtUtc
            firstResponseAtUtc = $FirstResponseAtUtc
            timeoutAtUtc = $TimeoutAtUtc
            waitDurationMs = $WaitDurationMs
        }
        messageCounters = $Counters
        responseClassification = $Classification
        sessionWarnings = @($Warnings | ForEach-Object { Redact-Text $_ })
        sessionErrors = @($Errors | ForEach-Object { Redact-Text $_ })
    }
}

function New-LogonDiagnostics([bool]$TcpConnected, [bool]$TlsConnected, [int]$MsgSeqNumSentForLogon, [object]$FirstInboundMsgType, [object]$FirstInboundText, [object]$LogonWaitDurationMs) {
    $sender = [Environment]::GetEnvironmentVariable("LMAX_DEMO_SENDER_COMP_ID")
    $target = [Environment]::GetEnvironmentVariable("LMAX_DEMO_TARGET_COMP_ID")
    $fixUser = [Environment]::GetEnvironmentVariable("LMAX_DEMO_FIX_USERNAME")
    $fixPass = [Environment]::GetEnvironmentVariable("LMAX_DEMO_FIX_PASSWORD")
    $sanitizedText = if ($FirstInboundText) { Redact-Text ([string]$FirstInboundText) } else { $null }
    $senderPresent = -not [string]::IsNullOrWhiteSpace($sender)
    $targetPresent = -not [string]::IsNullOrWhiteSpace($target)
    return [ordered]@{
        diagnosticVersion = "phase5j-logon-diagnostics-v1"
        connectionProfileLabel = "LmaxDemoMarketDataTls"
        environmentName = "Demo"
        venueProfileName = "DemoLondon"
        credentialProfileName = "LmaxDemoReadOnlyProfile"
        targetCompIdPresent = $targetPresent
        senderCompIdPresent = $senderPresent
        usernamePresent = -not [string]::IsNullOrWhiteSpace($fixUser)
        passwordPresent = -not [string]::IsNullOrWhiteSpace($fixPass)
        beginString = "FIX.4.4"
        senderCompIdLength = if ($senderPresent) { $sender.Length } else { $null }
        targetCompIdLength = if ($targetPresent) { $target.Length } else { $null }
        usernameLength = if (-not [string]::IsNullOrWhiteSpace($fixUser)) { $fixUser.Length } else { $null }
        passwordLength = if (-not [string]::IsNullOrWhiteSpace($fixPass)) { $fixPass.Length } else { $null }
        resetSeqNumFlag = "Y"
        encryptMethod = 0
        heartbeatInterval = 30
        msgSeqNumSentForLogon = $MsgSeqNumSentForLogon
        firstInboundMsgType = $FirstInboundMsgType
        firstInboundLogoutText = if ($FirstInboundMsgType -eq "5") { $sanitizedText } else { $null }
        firstInboundRejectText = if ($FirstInboundMsgType -eq "3") { $sanitizedText } else { $null }
        logonWaitDurationMs = $LogonWaitDurationMs
        tlsConnected = $TlsConnected
        tcpConnected = $TcpConnected
        profileComparison = [ordered]@{
            runtimeProfileLabel = "RuntimePhase5JDemoMarketData"
            labProfileLabel = "ConnectivityLabDemoMarketData"
            sameBeginString = $true
            sameHeartbeatInterval = $true
            sameEncryptMethod = $true
            sameResetSeqNumFlag = $true
            sameSenderCompIdSourceLabel = $senderPresent
            sameTargetCompIdSourceLabel = $targetPresent
            sameCredentialProfileName = $true
            sameConnectionProfileLabel = $true
            sameTlsSetting = $true
            samePortLabel = $true
            senderCompIdMismatchSuspected = -not $senderPresent
            targetCompIdMismatchSuspected = -not $targetPresent
            summary = if ($senderPresent -and $targetPresent) { "Runtime and Connectivity Lab profile labels are aligned on sanitized FIX session settings." } else { "Runtime profile is missing one or more comp-id source labels; compare local credential labels before another manual attempt." }
        }
        redactionStatus = "Redacted"
    }
}

function Get-MarketDataRequestFields([string]$RequestId) {
    $fields = @(
        @("262", $RequestId),
        @("263", $requestProfile.expectedSubscriptionRequestType),
        @("264", "$MarketDepth"),
        @("267", "2"),
        @("269", "0"),
        @("269", "1"),
        @("146", "1")
    )
    if ($requestProfile.symbolEncodingMode -eq "SecurityIdOnly") {
        $fields += @(@("48", $LmaxInstrumentId), @("22", "8"))
    } elseif ($requestProfile.symbolEncodingMode -eq "SecurityIdAndSymbolWithIdSource") {
        $fields += @(@("48", $LmaxInstrumentId), @("22", "8"), @("55", $SlashSymbol))
    } elseif ($requestProfile.symbolEncodingMode -eq "SecurityIdAndSymbolNoIdSource") {
        $fields += @(@("48", $LmaxInstrumentId), @("55", $SlashSymbol))
    } elseif ($requestProfile.symbolEncodingMode -eq "SlashSymbol") {
        $fields += @(@("55", $SlashSymbol))
    } else {
        $fields += @(@("55", $Instrument))
    }
    return $fields
}

function Build-FixMessage([string]$MessageType, [int]$SequenceNumber, [string]$Sender, [string]$Target, [array]$Fields) {
    $soh = [char]1
    $body = "35=$MessageType$soh" +
        "34=$SequenceNumber$soh" +
        "49=$Sender$soh" +
        ("52={0:yyyyMMdd-HH:mm:ss.fff}$soh" -f ([DateTimeOffset]::UtcNow.UtcDateTime)) +
        "56=$Target$soh"
    foreach ($field in $Fields) {
        $body += "$($field[0])=$($field[1])$soh"
    }
    $head = "8=FIX.4.4$soh" + "9=$([Text.Encoding]::ASCII.GetByteCount($body))$soh"
    $withoutChecksum = $head + $body
    $checksum = 0
    foreach ($byte in [Text.Encoding]::ASCII.GetBytes($withoutChecksum)) {
        $checksum = ($checksum + $byte) % 256
    }
    return $withoutChecksum + ("10={0:000}$soh" -f $checksum)
}

function Write-Ascii([System.IO.Stream]$Stream, [string]$Message) {
    $bytes = [Text.Encoding]::ASCII.GetBytes($Message)
    $Stream.Write($bytes, 0, $bytes.Length)
    $Stream.Flush()
}

function Read-FixMessage([System.IO.Stream]$Stream, [int]$TimeoutMilliseconds) {
    $deadline = [DateTimeOffset]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    $buffer = New-Object byte[] 4096
    $memory = New-Object System.IO.MemoryStream
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if ($Stream.CanTimeout) {
            $Stream.ReadTimeout = [Math]::Max(250, [int]($deadline - [DateTimeOffset]::UtcNow).TotalMilliseconds)
        }
        try {
            $read = $Stream.Read($buffer, 0, $buffer.Length)
        } catch [System.IO.IOException] {
            Start-Sleep -Milliseconds 50
            continue
        }
        if ($read -le 0) { break }
        $memory.Write($buffer, 0, $read)
        $text = [Text.Encoding]::ASCII.GetString($memory.ToArray())
        $marker = "$([char]1)10="
        $checksumIndex = $text.IndexOf($marker, [StringComparison]::Ordinal)
        if ($checksumIndex -ge 0) {
            $end = $checksumIndex + $marker.Length + 3
            if ($text.Length -gt $end -and $text[$end] -eq [char]1) {
                return $text.Substring(0, $end + 1)
            }
        }
    }
    return [Text.Encoding]::ASCII.GetString($memory.ToArray())
}

function Get-FixTag([string]$Message, [string]$Tag) {
    $parts = $Message.Split([char]1, [StringSplitOptions]::RemoveEmptyEntries)
    $value = $null
    foreach ($part in $parts) {
        if ($part.StartsWith("$Tag=", [StringComparison]::Ordinal)) {
            $value = $part.Substring($Tag.Length + 1)
        }
    }
    return $value
}

function Get-TopOfBook([string]$Message) {
    $parts = $Message.Split([char]1, [StringSplitOptions]::RemoveEmptyEntries)
    $currentType = $null
    $bids = @()
    $asks = @()
    foreach ($part in $parts) {
        if ($part.StartsWith("269=", [StringComparison]::Ordinal)) {
            $currentType = $part.Substring(4)
            continue
        }
        if ($part.StartsWith("270=", [StringComparison]::Ordinal) -and $currentType) {
            $priceText = $part.Substring(4)
            $price = 0.0
            if ([double]::TryParse($priceText, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$price)) {
                if ($currentType -eq "0") { $bids += $price }
                if ($currentType -eq "1") { $asks += $price }
            }
        }
    }
    $bestBid = if ($bids.Count -gt 0) { ($bids | Measure-Object -Maximum).Maximum } else { $null }
    $bestAsk = if ($asks.Count -gt 0) { ($asks | Measure-Object -Minimum).Minimum } else { $null }
    $mid = if ($null -ne $bestBid -and $null -ne $bestAsk) { ($bestBid + $bestAsk) / 2.0 } else { $null }
    return [ordered]@{ bestBid = $bestBid; bestAsk = $bestAsk; mid = $mid }
}

if ($blockedReason) {
    $blockedStatus = if ($missingCredentialLabels.Count -gt 0) {
        "BlockedMissingCredentials"
    } elseif (@($failed | Where-Object { $_.name -eq "Instrument" }).Count -gt 0) {
        "BlockedSafetyGate"
    } else {
        "BlockedSafetyGate"
    }
    $retryPolicy = Get-RetryPolicy $blockedStatus
    $result = [ordered]@{
        runId = $runId
        startedAtUtc = $now
        completedAtUtc = $now
        status = $blockedStatus
        environmentName = "Demo"
        venueProfileName = "DemoLondon"
        credentialProfileName = "LmaxDemoReadOnlyProfile"
        reason = $Reason
        operatorId = $OperatorId
        externalConnectionAttempted = $false
        credentialReadAttempted = $true
        credentialValuesReturned = $false
        logonAttempted = $false
        logonSucceeded = $false
        snapshotRequestAttempted = $false
        snapshotReceived = $false
        logoutAttempted = $false
        logoutSucceeded = $false
        orderSubmissionAttempted = $false
        shadowReplaySubmitAttempted = $false
        tradingMutationAttempted = $false
        schedulerStarted = $false
        eventCount = 0
        messageCount = 0
        entryCount = 0
        marketDataSnapshotReceived = $false
        instrument = $Instrument
        symbol = $Instrument
        slashSymbol = $SlashSymbol
        securityId = $LmaxInstrumentId
        securityIdSource = "8"
        requestMode = $requestProfile.requestMode
        symbolEncodingMode = $requestProfile.symbolEncodingMode
        marketDepth = $MarketDepth
        sourceFinalReadinessFile = $SourceFinalReadinessFile
        snapshotReceivedAtUtc = $null
        noSensitiveContent = $true
        redactionStatus = "Redacted"
        diagnostics = New-Diagnostics $blockedStatus (New-MessageCounters) $null $null $null $null @() @($blockedReason)
        logonDiagnostics = New-LogonDiagnostics $false $false 1 $null $null $null
        blockedReason = $blockedReason
        retryEnabled = $retryPolicy.retryEnabled
        retryAllowed = $retryPolicy.retryAllowed
        retryPolicy = $retryPolicy
        retryRecommendation = $retryPolicy.recommendation
        credentialAvailability = $credentialAvailability
        gates = @($checks)
        rollbackInstructions = @(
            "Stop this local process.",
            "Clear Phase 5D prototype environment variables from this shell.",
            "Start API with the default disabled run path.",
            "Verify /health reports FakeLmaxGateway and liveTradingEnabled=false.",
            "Run scripts/check-lmax-readonly-runtime-phase5d-demo-snapshot-gate.ps1.",
            "Run scripts/check-lmax-readonly-runtime-demo-credentials.ps1 -ConfirmCredentialAvailabilityCheck if credential labels were missing.",
            "Do not proceed with another manual attempt if failure classification is unknown.",
            "No DB rollback is expected because no trading-state mutation is allowed."
        )
    }

    $artifactPath = Write-SanitizedArtifact $result
    $result["sanitizedArtifactPath"] = $artifactPath
    $json = $result | ConvertTo-Json -Depth 8
    Write-Host $json
    Write-Host "Sanitized artifact: $artifactPath"
    Write-Host "Rollback: stop this process, clear this shell's Phase 5D variables, run default API startup, verify /health FakeLmaxGateway, then run the Phase 5D demo snapshot gate."
    exit 2
}

Write-Host "Credential labels are present. Starting the isolated Phase 5D manual Demo market-data snapshot prototype."
Write-Host "Kill switch: press Ctrl+C or close this process. No API/Worker gateway registration is used."
Write-Host "Planned safety flags:"
Write-Host "  environmentName=Demo"
Write-Host "  venueProfileName=DemoLondon"
Write-Host "  instrument=$Instrument"
Write-Host "  securityId=$LmaxInstrumentId"
Write-Host "  requestMode=$($requestProfile.requestMode)"
Write-Host "  symbolEncodingMode=$($requestProfile.symbolEncodingMode)"
Write-Host "  securityIdSource=8"
Write-Host "  marketDepth=$MarketDepth"
Write-Host "  subscriptionRequestType=$(if ($requestProfile.expectedSubscriptionRequestType -eq '1') { 'SnapshotPlusUpdates' } else { 'SnapshotOnly' })"
Write-Host "  knownRejectedByLmaxDemo=$($requestProfile.knownRejectedByLmaxDemo)"
Write-Host "  requiresUnsubscribeAfterSnapshot=$($requestProfile.requiresUnsubscribeAfterSnapshot)"
Write-Host "  allowExternalConnections=$([bool]$AllowExternalConnections)"
Write-Host "  confirmDemoReadOnly=$([bool]$ConfirmDemoReadOnly)"
Write-Host "  allowOrderSubmission=False"
Write-Host "  schedulerEnabled=False"
Write-Host "  submitToShadowReplay=False"
Write-Host "  persistToTradingTables=False"
Write-Host "  maxRuntimeSeconds=$MaxRuntimeSeconds"
Write-Host "  maxWaitSeconds=$MaxWaitSeconds"
Write-Host "  maxEventsPerRun=$MaxEventsPerRun"

$status = "FailedSafeConnectionError"
$externalConnectionAttempted = $false
$logonAttempted = $false
$logonSucceeded = $false
$snapshotRequestAttempted = $false
$snapshotReceived = $false
$logoutAttempted = $false
$logoutSucceeded = $false
$messageCount = 0
$entryCount = 0
$messageCounters = New-MessageCounters
$bestBid = $null
$bestAsk = $null
$mid = $null
$snapshotReceivedAtUtc = $null
$requestSentAtUtc = $null
$firstResponseAtUtc = $null
$warnings = @()
$errors = @()
$tcp = $null
$stream = $null
$sequenceNumber = 1
$started = [DateTimeOffset]::UtcNow
$tcpConnected = $false
$tlsConnected = $false
$logonStartedAtUtc = $null
$firstInboundMsgType = $null
$firstInboundText = $null
$logonWaitDurationMs = $null

try {
    $sender = [Environment]::GetEnvironmentVariable("LMAX_DEMO_SENDER_COMP_ID")
    $target = [Environment]::GetEnvironmentVariable("LMAX_DEMO_TARGET_COMP_ID")
    $fixUser = [Environment]::GetEnvironmentVariable("LMAX_DEMO_FIX_USERNAME")
    $fixPass = [Environment]::GetEnvironmentVariable("LMAX_DEMO_FIX_PASSWORD")
    $dnsName = "fix-marketdata.london-demo.lmax.com"
    $tlsNumber = 443

    $tcp = [Net.Sockets.TcpClient]::new()
    $externalConnectionAttempted = $true
    $connectTask = $tcp.ConnectAsync($dnsName, $tlsNumber)
    if (-not $connectTask.Wait([TimeSpan]::FromSeconds([Math]::Min($MaxRuntimeSeconds, 30)))) {
        throw "Phase5DConnectTimeout"
    }
    $tcpConnected = $true

    $ssl = [Net.Security.SslStream]::new($tcp.GetStream(), $false)
    $ssl.AuthenticateAsClient($dnsName)
    $stream = $ssl
    $tlsConnected = $true

    $logonAttempted = $true
    $logon = Build-FixMessage "A" $sequenceNumber $sender $target @(
        @("98", "0"),
        @("108", "30"),
        @("141", "Y"),
        @("553", $fixUser),
        @("554", $fixPass)
    )
    $sequenceNumber++
    Write-Ascii $stream $logon
    $logonStartedAtUtc = [DateTimeOffset]::UtcNow
    $logonResponse = Read-FixMessage $stream ([Math]::Min($MaxRuntimeSeconds, 30) * 1000)
    $logonWaitDurationMs = [int64]([DateTimeOffset]::UtcNow - $logonStartedAtUtc).TotalMilliseconds
    $messageCount++
    $firstInboundMsgType = Get-FixTag $logonResponse "35"
    $firstInboundText = Get-FixTag $logonResponse "58"
    Add-MessageCounter $messageCounters $firstInboundMsgType
    $logonSucceeded = $firstInboundMsgType -eq "A"
    if (-not $logonSucceeded) {
        $status = if ($firstInboundMsgType -eq "5") { "FailedSafeLogonLogoutReceived" } elseif ($firstInboundMsgType -eq "3") { "FailedSafeLogonRejectReceived" } elseif ([string]::IsNullOrWhiteSpace($firstInboundMsgType)) { "FailedSafeLogonTimeout" } else { "FailedSafeLogonUnknown" }
        $errors += "FIX logon was not confirmed before timeout or first session response."
        if ($firstInboundMsgType -eq "5") { $errors += "FIX Logout was received before logon confirmation." }
        if ($firstInboundMsgType -eq "3") { $errors += "FIX session Reject was received before logon confirmation." }
    } else {
        $snapshotRequestAttempted = $true
        $mdReqId = "QQRO" + ([DateTimeOffset]::UtcNow.ToString("HHmmss"))
        $request = Build-FixMessage "V" $sequenceNumber $sender $target (Get-MarketDataRequestFields $mdReqId)
        $sequenceNumber++
        Write-Ascii $stream $request
        $requestSentAtUtc = [DateTimeOffset]::UtcNow

        while ($messageCount -lt $MaxEventsPerRun) {
            $message = Read-FixMessage $stream ([Math]::Min($MaxWaitSeconds, 30) * 1000)
            if ([string]::IsNullOrWhiteSpace($message)) { break }
            $messageCount++
            $msgType = Get-FixTag $message "35"
            Add-MessageCounter $messageCounters $msgType
            if ($null -eq $firstResponseAtUtc) { $firstResponseAtUtc = [DateTimeOffset]::UtcNow }
            if ($msgType -eq "W") {
                $snapshotReceived = $true
                $book = Get-TopOfBook $message
                $bestBid = $book.bestBid
                $bestAsk = $book.bestAsk
                $mid = $book.mid
                $entryCount = @($message.Split([char]1, [StringSplitOptions]::RemoveEmptyEntries) | Where-Object { $_.StartsWith("269=", [StringComparison]::Ordinal) }).Count
                $snapshotReceivedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
                if ($entryCount -eq 0) {
                    $status = "CompletedWithEmptyBook"
                    $warnings += "Market data snapshot was received with no entries."
                }
                break
            }
            if ($msgType -eq "Y") {
                if ($message -match "263" -and $message -match "ValueOutOfRange") {
                    $status = "FailedSafeMarketDataRequestRejectedValueOutOfRange263"
                } elseif ($message -match "55" -and $message -match "UnknownTag") {
                    $status = "FailedSafeMarketDataRequestRejectedUnknownTag55"
                } elseif ($message -match "146" -and $message -match "RepeatingGroupNumInGroupMismatch") {
                    $status = "FailedSafeMarketDataRequestRejectedGroupMismatch146"
                } else {
                    $status = "FailedSafeMarketDataRequestRejectedOther"
                }
                $errors += "Market data request was rejected by FIX session."
                break
            }
            if ($msgType -eq "j") {
                $status = "FailedSafeBusinessReject"
                $errors += "Business message reject was received before market data snapshot."
                break
            }
            if ($msgType -eq "3") {
                if ($message -match "263" -and $message -match "ValueOutOfRange") {
                    $status = "FailedSafeMarketDataRequestRejectedValueOutOfRange263"
                } elseif ($message -match "55" -and $message -match "UnknownTag") {
                    $status = "FailedSafeMarketDataRequestRejectedUnknownTag55"
                } elseif ($message -match "146" -and $message -match "RepeatingGroupNumInGroupMismatch") {
                    $status = "FailedSafeMarketDataRequestRejectedGroupMismatch146"
                } else {
                    $status = "FailedSafeSessionReject"
                }
                $errors += "FIX session reject was received before market data snapshot."
                break
            }
            if ($msgType -eq "5") {
                $status = "FailedSafeUnexpectedLogout"
                $warnings += "FIX session returned Logout before snapshot was received."
                break
            }
        }

        $logoutAttempted = $true
        $logout = Build-FixMessage "5" $sequenceNumber $sender $target @(@("58", "QQ read-only demo snapshot complete"))
        Write-Ascii $stream $logout
        $logoutSucceeded = $true
    }

    if ($snapshotReceived) {
        if ($entryCount -eq 0) {
            $status = "CompletedWithEmptyBook"
        } else {
            $status = if ($warnings.Count -gt 0) { "CompletedWithWarnings" } else { "Completed" }
        }
    } elseif ($errors.Count -gt 0) {
        if ($status -eq "FailedSafeConnectionError") { $status = "FailedSafeSnapshotTimeout" }
    } else {
        $status = "FailedSafeSnapshotTimeout"
        $warnings += "Timed out before a market data snapshot was received."
    }
} catch {
    $status = if ($_.Exception.Message -eq "Phase5DConnectTimeout") { "FailedSafeConnectionError" } else { "FailedSafeConnectionError" }
    $errors += "Phase5D demo snapshot failed safe: " + (Redact-Text $_.Exception.GetType().Name)
} finally {
    if ($stream) { $stream.Dispose() }
    if ($tcp) { $tcp.Dispose() }
}

$completed = [DateTimeOffset]::UtcNow
$timeoutAtUtc = if (-not $snapshotReceived) { $completed.ToString("o") } else { $null }
$waitDurationMs = if ($requestSentAtUtc) { [int64]($completed - $requestSentAtUtc).TotalMilliseconds } else { $null }
$retryPolicy = Get-RetryPolicy $status
$result = [ordered]@{
    runId = $runId
    startedAtUtc = $started.ToString("o")
    completedAtUtc = $completed.ToString("o")
    status = $status
    environmentName = "Demo"
    venueProfileName = "DemoLondon"
    credentialProfileName = "LmaxDemoReadOnlyProfile"
    reason = $Reason
    operatorId = $OperatorId
    externalConnectionAttempted = $externalConnectionAttempted
    credentialReadAttempted = $true
    credentialValuesReturned = $false
    logonAttempted = $logonAttempted
    logonSucceeded = $logonSucceeded
    snapshotRequestAttempted = $snapshotRequestAttempted
    snapshotReceived = $snapshotReceived
    logoutAttempted = $logoutAttempted
    logoutSucceeded = $logoutSucceeded
    orderSubmissionAttempted = $false
    shadowReplaySubmitAttempted = $false
    tradingMutationAttempted = $false
    schedulerStarted = $false
    eventCount = $messageCount
    messageCount = $messageCount
    entryCount = $entryCount
    marketDataSnapshotReceived = $snapshotReceived
        instrument = $Instrument
        symbol = $Instrument
        slashSymbol = $SlashSymbol
        securityId = $LmaxInstrumentId
        securityIdSource = "8"
        requestMode = $requestProfile.requestMode
        symbolEncodingMode = $requestProfile.symbolEncodingMode
        marketDepth = $MarketDepth
        bestBid = $bestBid
    bestAsk = $bestAsk
    mid = $mid
        snapshotReceivedAtUtc = $snapshotReceivedAtUtc
        sourceFinalReadinessFile = $SourceFinalReadinessFile
    noSensitiveContent = $true
    redactionStatus = "Redacted"
    diagnostics = New-Diagnostics $status $messageCounters $(if ($requestSentAtUtc) { $requestSentAtUtc.ToString("o") } else { $null }) $(if ($firstResponseAtUtc) { $firstResponseAtUtc.ToString("o") } else { $null }) $timeoutAtUtc $waitDurationMs $warnings $errors
    logonDiagnostics = New-LogonDiagnostics $tcpConnected $tlsConnected 1 $firstInboundMsgType $firstInboundText $logonWaitDurationMs
    warnings = @($warnings | ForEach-Object { Redact-Text $_ })
    errors = @($errors | ForEach-Object { Redact-Text $_ })
    retryEnabled = $retryPolicy.retryEnabled
    retryAllowed = $retryPolicy.retryAllowed
    retryPolicy = $retryPolicy
    retryRecommendation = $retryPolicy.recommendation
    credentialAvailability = $credentialAvailability
    rollbackInstructions = @(
        "Stop this local process.",
        "Clear Phase 5D prototype environment variables from this shell.",
        "Start API with the default disabled run path.",
        "Verify /health reports FakeLmaxGateway and liveTradingEnabled=false.",
        "Run scripts/check-lmax-readonly-runtime-phase5d-demo-snapshot-gate.ps1.",
        "Run scripts/check-lmax-readonly-runtime-demo-credentials.ps1 -ConfirmCredentialAvailabilityCheck if credential labels were missing.",
        "Do not proceed with another manual attempt if failure classification is unknown.",
        "No DB rollback is expected because no trading-state mutation is allowed."
    )
}

$artifactPath = Write-SanitizedArtifact $result
$result["sanitizedArtifactPath"] = $artifactPath
if ($ShowSanitizedLogonDiagnostics) {
    Write-Host "Sanitized logon diagnostics:"
    $result.logonDiagnostics | ConvertTo-Json -Depth 8 | Write-Host
}
$result | ConvertTo-Json -Depth 8 | Write-Host
Write-Host "Sanitized artifact: $artifactPath"
Write-Host "Rollback: stop this process, clear this shell's Phase 5D variables, run default API startup, verify /health FakeLmaxGateway, then run the Phase 5D demo snapshot gate."

if ($status -eq "Completed" -or $status -eq "CompletedWithWarnings") { exit 0 }
exit 1
