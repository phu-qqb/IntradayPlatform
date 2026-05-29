param(
    [switch]$AllowExternalConnections,
    [switch]$ConfirmDemoReadOnly,
    [Parameter(Mandatory = $false)]
    [string]$Reason,
    [ValidateSet("AllSecurities", "ProductFx", "SymbolExact", "SecurityTypeFx", "CandidateSymbolsOneByOne", "MinimalRequest", "LabCompatibleFallback", "AutoSequence", "AllInstruments", "ForexOnly", "SymbolFilter", "CandidateSymbolsOnly")]
    [string]$RequestProfile = "MinimalRequest",
    [switch]$AllowKnownRejectedDiagnostics,
    [string]$SymbolFilter = "",
    [int]$MaxWaitSeconds = 15,
    [int]$MaxRuntimeSeconds = 30,
    [int]$MaxMessages = 25,
    [int]$MaxInstruments = 500
)

$ErrorActionPreference = "Stop"

Write-Host "LMAX Read-Only Runtime Demo SecurityList Discovery"
Write-Host "WARNING: Demo-only, manual-only, read-only SecurityListRequest discovery."
Write-Host "WARNING: No market-data snapshots, no orders, no scheduler, no runtime shadow replay submit, no trading mutation."
Write-Host "WARNING: Discovery output is planning-only and never sets IsApprovedForExternalRun=true."

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/lmax-readonly-runtime-securityid-discovery"
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

$requiredLabels = @(
    "LMAX_DEMO_FIX_USERNAME",
    "LMAX_DEMO_FIX_PASSWORD",
    "LMAX_DEMO_SENDER_COMP_ID",
    "LMAX_DEMO_TARGET_COMP_ID"
)
$candidateSymbols = @(
    @{ symbol = "GBPUSD"; slashSymbol = "GBP/USD" },
    @{ symbol = "USDJPY"; slashSymbol = "USD/JPY" },
    @{ symbol = "EURGBP"; slashSymbol = "EUR/GBP" },
    @{ symbol = "AUDUSD"; slashSymbol = "AUD/USD" }
)
$profileDefinitions = [ordered]@{
    MinimalRequest = @{ requestType = "4"; includeProduct = $false; product = $null; includeSecurityType = $false; securityType = $null; includeSymbol = $false; knownRejected = $false; reason = $null; notes = "Minimal SecurityListRequestType=4 only." }
    ProductFx = @{ requestType = "1"; includeProduct = $true; product = "4"; includeSecurityType = $false; securityType = $null; includeSymbol = $false; knownRejected = $false; reason = $null; notes = "FX/product-filtered request." }
    SecurityTypeFx = @{ requestType = "1"; includeProduct = $false; product = $null; includeSecurityType = $true; securityType = "FOR"; includeSymbol = $false; knownRejected = $false; reason = $null; notes = "SecurityType=FOR request." }
    LabCompatibleFallback = @{ requestType = "4"; includeProduct = $false; product = $null; includeSecurityType = $false; securityType = $null; includeSymbol = $false; knownRejected = $false; reason = $null; notes = "Connectivity-lab compatible fallback." }
    AllSecurities = @{ requestType = "4"; includeProduct = $false; product = $null; includeSecurityType = $false; securityType = $null; includeSymbol = $false; knownRejected = $true; reason = "First Demo SecurityList attempt failed safely with equivalent all-instruments shape."; notes = "Diagnostic profile only unless override is supplied." }
    AllInstruments = @{ requestType = "4"; includeProduct = $false; product = $null; includeSecurityType = $false; securityType = $false; includeSymbol = $false; knownRejected = $true; reason = "Deprecated first profile failed safely."; notes = "Backward-compatible diagnostic alias." }
    SymbolExact = @{ requestType = "0"; includeProduct = $false; product = $null; includeSecurityType = $false; securityType = $null; includeSymbol = $true; knownRejected = $true; reason = "Symbol-filtered SecurityListRequest is not yet proven compatible."; notes = "Requires known-rejected diagnostics override." }
    CandidateSymbolsOneByOne = @{ requestType = "0"; includeProduct = $false; product = $null; includeSecurityType = $false; securityType = $null; includeSymbol = $true; knownRejected = $true; reason = "Candidate symbol sequence depends on symbol-filtered request support."; notes = "Requires known-rejected diagnostics override." }
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
    $redacted = [regex]::Replace($redacted, "(?i)(authorization\s*[:=]\s*)[^,;\r\n]+", '$1[REDACTED]')
    return $redacted
}

function Get-FixTag([string]$Message, [string]$Tag) {
    $value = $null
    foreach ($part in $Message.Split([char]1, [StringSplitOptions]::RemoveEmptyEntries)) {
        if ($part.StartsWith("$Tag=", [StringComparison]::Ordinal)) {
            $value = $part.Substring($Tag.Length + 1)
        }
    }
    return $value
}

function Normalize-Compact([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) { return "" }
    return $Value.Replace("/", "").ToUpperInvariant()
}

function Normalize-Slash([string]$Value) {
    $compact = Normalize-Compact $Value
    if ($compact.Length -eq 6) { return "$($compact.Substring(0, 3))/$($compact.Substring(3, 3))" }
    return $Value
}

function Parse-SecurityListInstruments([string[]]$Messages) {
    $items = @()
    foreach ($message in $Messages) {
        $msgType = Get-FixTag $message "35"
        if ($msgType -notin @("y", "d")) { continue }
        $current = [ordered]@{}
        foreach ($part in $message.Split([char]1, [StringSplitOptions]::RemoveEmptyEntries)) {
            $split = $part.Split("=", 2)
            if ($split.Count -ne 2) { continue }
            $tag = $split[0]
            $value = $split[1]
            if ($tag -eq "55" -and ($current.Contains("55") -or $current.Contains("48"))) {
                $items += New-Instrument $current $msgType
                $current = [ordered]@{}
            }
            if ($tag -in @("55", "48", "22", "167", "15", "120")) {
                $current[$tag] = $value
            }
        }
        if ($current.Count -gt 0) {
            $items += New-Instrument $current $msgType
        }
        if ($items.Count -ge $MaxInstruments) { break }
    }
    return @($items | Select-Object -First $MaxInstruments)
}

function New-Instrument([System.Collections.IDictionary]$Fields, [string]$SourceMessageType) {
    $rawSymbol = if ($Fields.Contains("55")) { [string]$Fields["55"] } else { $null }
    return [ordered]@{
        symbol = if ($rawSymbol) { Normalize-Compact $rawSymbol } else { $null }
        slashSymbol = if ($rawSymbol) { Normalize-Slash $rawSymbol } else { $null }
        securityId = if ($Fields.Contains("48")) { [string]$Fields["48"] } else { $null }
        securityIdSource = if ($Fields.Contains("22")) { [string]$Fields["22"] } else { $null }
        securityType = if ($Fields.Contains("167")) { [string]$Fields["167"] } else { $null }
        currency = if ($Fields.Contains("15")) { [string]$Fields["15"] } else { $null }
        quoteCurrency = if ($Fields.Contains("120")) { [string]$Fields["120"] } else { $null }
        sourceMessageType = $SourceMessageType
    }
}

function Match-Candidates([array]$Instruments) {
    $matches = @()
    foreach ($candidate in $candidateSymbols) {
        $candidateCompact = Normalize-Compact $candidate.symbol
        foreach ($instrument in $Instruments) {
            if ([string]::IsNullOrWhiteSpace($instrument.securityId)) { continue }
            if ((Normalize-Compact $instrument.symbol) -eq $candidateCompact -or (Normalize-Compact $instrument.slashSymbol) -eq $candidateCompact) {
                $matches += [ordered]@{
                    symbol = $candidate.symbol
                    slashSymbol = $candidate.slashSymbol
                    securityId = $instrument.securityId
                    securityIdSource = $instrument.securityIdSource
                    securityType = $instrument.securityType
                    currency = $instrument.currency
                    quoteCurrency = $instrument.quoteCurrency
                    sourceMessageType = $instrument.sourceMessageType
                    isApprovedForExternalRun = $false
                }
            }
        }
    }
    return $matches
}

function Classify-Status([string]$MsgType, [string]$RejectTag, [string]$RejectText, [bool]$TimedOut) {
    if ($MsgType -eq "y") { return "Completed" }
    if ($MsgType -eq "j") { return "FailedSafeSecurityListBusinessReject" }
    if ($MsgType -eq "3") {
        if ($RejectTag -eq "559" -or $RejectText -match "(?i)SecurityListRequestType") { return "FailedSafeSecurityListUnsupportedSecurityRequestType" }
        if ($RejectTag -eq "55" -or $RejectText -match "(?i)symbol") { return "FailedSafeSecurityListUnsupportedSymbolFilter" }
        if ($RejectText -match "(?i)unsupported") { return "FailedSafeSecurityListUnsupportedRequestType" }
        return "FailedSafeSecurityListSessionReject"
    }
    if ($MsgType -eq "5") { return "FailedSafeSecurityListRequestRejected" }
    if ($TimedOut) { return "FailedSafeSecurityListTimeout" }
    return "FailedSafeSecurityListUnknownReject"
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

function Read-FixMessage([System.IO.Stream]$Stream, [int]$TimeoutMilliseconds) {
    $deadline = [DateTimeOffset]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    $buffer = New-Object byte[] 8192
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
        if ($text.Contains("$([char]1)10=", [StringComparison]::Ordinal)) { return $text }
    }
    return [Text.Encoding]::ASCII.GetString($memory.ToArray())
}

function Write-Ascii([System.IO.Stream]$Stream, [string]$Message) {
    $bytes = [Text.Encoding]::ASCII.GetBytes($Message)
    $Stream.Write($bytes, 0, $bytes.Length)
    $Stream.Flush()
}

function Write-DiscoveryArtifact([string]$Status, [array]$Messages, [array]$Warnings, [array]$Errors, [bool]$Connected, [bool]$LoggedOn, [bool]$RequestSent, [bool]$ListReceived, [bool]$LogoutAttempted, [bool]$LogoutSucceeded, [array]$Attempts) {
    $instruments = Parse-SecurityListInstruments $Messages
    $matches = Match-Candidates $instruments
    $unmatched = @($candidateSymbols | Where-Object { $candidate = $_; @($matches | Where-Object { $_.symbol -eq $candidate.symbol }).Count -eq 0 } | ForEach-Object { $_.symbol })
    if ($Status -eq "Completed" -and $unmatched.Count -gt 0) { $Status = "CompletedWithWarnings" }
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $discoveryId = "lmax-securitylist-discovery-$stamp"
    $artifactPath = Join-Path $artifactDir "$discoveryId.json"
    $artifact = [ordered]@{
        discoveryId = $discoveryId
        createdAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
        status = $Status
        environmentName = "Demo"
        credentialProfileName = "LmaxDemoReadOnlyProfile"
        requestProfile = $RequestProfile
        finalStatus = $Status
        selectedSuccessfulProfile = $null
        externalConnectionAttempted = $Connected
        credentialReadAttempted = $true
        credentialValuesReturned = $false
        logonAttempted = $Connected
        logonSucceeded = $LoggedOn
        securityListRequestAttempted = $RequestSent
        securityListReceived = $ListReceived
        logoutAttempted = $LogoutAttempted
        logoutSucceeded = $LogoutSucceeded
        totalInstrumentCount = $instruments.Count
        candidateMatches = $matches
        unmatchedCandidates = $unmatched
        instruments = @($instruments | Select-Object -First $MaxInstruments)
        attempts = $Attempts
        warnings = @($Warnings | ForEach-Object { Redact-Text $_ })
        errors = @($Errors | ForEach-Object { Redact-Text $_ })
        noSensitiveContent = $true
        redactionStatus = "Redacted"
        isApprovedForExternalRun = $false
        orderSubmissionAttempted = $false
        shadowReplaySubmitAttempted = $false
        tradingMutationAttempted = $false
        schedulerStarted = $false
    }
    $json = Redact-Text ($artifact | ConvertTo-Json -Depth 12)
    Set-Content -LiteralPath $artifactPath -Value $json -Encoding UTF8
    return @{ path = $artifactPath; artifact = $artifact }
}

$checks = @()
$checks += @{ name = "AllowExternalConnections"; passed = [bool]$AllowExternalConnections }
$checks += @{ name = "ConfirmDemoReadOnly"; passed = [bool]$ConfirmDemoReadOnly }
$checks += @{ name = "Reason"; passed = -not [string]::IsNullOrWhiteSpace($Reason) }
$checks += @{ name = "MaxWaitSeconds"; passed = $MaxWaitSeconds -gt 0 -and $MaxWaitSeconds -le 30 }
$checks += @{ name = "MaxRuntimeSeconds"; passed = $MaxRuntimeSeconds -gt 0 -and $MaxRuntimeSeconds -le 60 }
$checks += @{ name = "MaxMessages"; passed = $MaxMessages -gt 0 -and $MaxMessages -le 100 }
$checks += @{ name = "MaxInstruments"; passed = $MaxInstruments -gt 0 -and $MaxInstruments -le 1000 }
$failedChecks = @($checks | Where-Object { -not $_.passed })
$missingCredentialLabels = @($requiredLabels | Where-Object { [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_)) })
$requestedProfiles = if ($RequestProfile -eq "AutoSequence") {
    @("MinimalRequest", "ProductFx", "SecurityTypeFx", "LabCompatibleFallback") + $(if ($AllowKnownRejectedDiagnostics.IsPresent) { @("AllSecurities", "SymbolExact", "CandidateSymbolsOneByOne") } else { @() })
} else {
    @($RequestProfile)
}
$profile = $profileDefinitions[$requestedProfiles[0]]
if ($profile.knownRejected -and -not $AllowKnownRejectedDiagnostics.IsPresent) {
    $failedChecks += @{ name = "KnownRejectedRequestProfile"; passed = $false }
}

if ($failedChecks.Count -gt 0 -or $missingCredentialLabels.Count -gt 0) {
    $errors = @()
    if ($failedChecks.Count -gt 0) { $errors += "Blocked by manual gates: " + (($failedChecks | ForEach-Object { $_.name }) -join ", ") }
    if ($missingCredentialLabels.Count -gt 0) { $errors += "Blocked by missing credential labels: " + ($missingCredentialLabels -join ", ") }
    $attempts = @($requestedProfiles | ForEach-Object {
        $p = $profileDefinitions[$_]
        [ordered]@{
            requestProfile = $_
            requestIdHash = $null
            sentAtUtc = $null
            firstResponseAtUtc = $null
            status = "BlockedBeforeExternalAttempt"
            classification = "BlockedBeforeExternalAttempt"
            rejectTag = $null
            rejectText = if ($p.knownRejected -and -not $AllowKnownRejectedDiagnostics.IsPresent) { $p.reason } else { $null }
            instrumentCount = 0
            candidateMatches = @()
        }
    })
    $written = Write-DiscoveryArtifact "BlockedMissingCredentials" @() @() $errors $false $false $false $false $false $false $attempts
    Write-Host "Status: BlockedMissingCredentials"
    Write-Host "InstrumentCount: 0"
    Write-Host "CandidateMatches: 0"
    Write-Host "NoSensitiveContent: true"
    Write-Host "IsApprovedForExternalRun: false"
    Write-Host "Sanitized artifact: $($written.path)"
    exit 2
}

$messages = @()
$warnings = @()
$errors = @()
$connected = $false
$loggedOn = $false
$requestSent = $false
$listReceived = $false
$logoutAttempted = $false
$logoutSucceeded = $false
$lastMsgType = $null
$rejectTag = $null
$rejectText = $null
$attempts = @()
$tcp = $null
$stream = $null

try {
    $sender = [Environment]::GetEnvironmentVariable("LMAX_DEMO_SENDER_COMP_ID")
    $target = [Environment]::GetEnvironmentVariable("LMAX_DEMO_TARGET_COMP_ID")
    $fixUser = [Environment]::GetEnvironmentVariable("LMAX_DEMO_FIX_USERNAME")
    $fixPass = [Environment]::GetEnvironmentVariable("LMAX_DEMO_FIX_PASSWORD")
    $host = "fix-marketdata.london-demo.lmax.com"
    $port = 443
    $sequenceNumber = 1

    $tcp = [Net.Sockets.TcpClient]::new()
    $connectTask = $tcp.ConnectAsync($host, $port)
    if (-not $connectTask.Wait([TimeSpan]::FromSeconds([Math]::Min($MaxRuntimeSeconds, 60)))) {
        throw "SecurityListConnectTimeout"
    }
    $connected = $true

    $ssl = [Net.Security.SslStream]::new($tcp.GetStream(), $false)
    $ssl.AuthenticateAsClient($host)
    $stream = $ssl

    $logon = Build-FixMessage "A" $sequenceNumber $sender $target @(
        @("98", "0"),
        @("108", "30"),
        @("141", "Y"),
        @("553", $fixUser),
        @("554", $fixPass)
    )
    $sequenceNumber++
    Write-Ascii $stream $logon
    $logonResponse = Read-FixMessage $stream ([Math]::Min($MaxWaitSeconds, 30) * 1000)
    $lastMsgType = Get-FixTag $logonResponse "35"
    $messages += $logonResponse
    $loggedOn = $lastMsgType -eq "A"
    if (-not $loggedOn) {
        $errors += "FIX logon was not confirmed before SecurityListRequest."
    } else {
        $effectiveProfileName = $requestedProfiles[0]
        $effectiveProfile = $profileDefinitions[$effectiveProfileName]
        $requestId = "QQSL" + ([DateTimeOffset]::UtcNow.ToString("HHmmss"))
        $sha = [System.Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($requestId))
        $requestIdHash = (($sha | ForEach-Object { $_.ToString("x2") }) -join "").Substring(0, 16).ToUpperInvariant()
        $fields = @(@("320", $requestId), @("559", $effectiveProfile.requestType))
        if ($effectiveProfile.includeSymbol -and -not [string]::IsNullOrWhiteSpace($SymbolFilter)) {
            $fields += @(@("55", $SymbolFilter))
        }
        if ($effectiveProfile.includeSecurityType) {
            $fields += @(@("167", $effectiveProfile.securityType))
        }
        if ($effectiveProfile.includeProduct) {
            $fields += @(@("460", $effectiveProfile.product))
        }
        $sentAtUtc = [DateTimeOffset]::UtcNow
        $request = Build-FixMessage "x" $sequenceNumber $sender $target $fields
        $sequenceNumber++
        Write-Ascii $stream $request
        $requestSent = $true

        while ($messages.Count -lt $MaxMessages) {
            $message = Read-FixMessage $stream ([Math]::Min($MaxWaitSeconds, 30) * 1000)
            if ([string]::IsNullOrWhiteSpace($message)) { break }
            $messages += $message
            $firstResponseAtUtc = [DateTimeOffset]::UtcNow
            $lastMsgType = Get-FixTag $message "35"
            $rejectTag = Get-FixTag $message "371"
            $rejectText = Redact-Text (Get-FixTag $message "58")
            if ($lastMsgType -eq "y" -or $lastMsgType -eq "d") {
                $listReceived = $true
                break
            }
            if ($lastMsgType -in @("j", "3", "5")) { break }
        }
        $attempts += [ordered]@{
            requestProfile = $effectiveProfileName
            requestIdHash = $requestIdHash
            sentAtUtc = $sentAtUtc.ToString("o")
            firstResponseAtUtc = if ($firstResponseAtUtc) { $firstResponseAtUtc.ToString("o") } else { $null }
            status = Classify-Status $lastMsgType $rejectTag $rejectText (-not $listReceived)
            classification = Classify-Status $lastMsgType $rejectTag $rejectText (-not $listReceived)
            rejectMessageType = $lastMsgType
            rejectTag = $rejectTag
            rejectText = $rejectText
            instrumentCount = @(Parse-SecurityListInstruments $messages).Count
            candidateMatches = @(Match-Candidates (Parse-SecurityListInstruments $messages))
        }
    }

    if ($stream) {
        $logoutAttempted = $true
        try {
            $logout = Build-FixMessage "5" $sequenceNumber $sender $target @(@("58", "QQ read-only SecurityList discovery complete"))
            Write-Ascii $stream $logout
            $logoutSucceeded = $true
        } catch {
            $warnings += "Logout send failed after read-only SecurityList discovery attempt."
        }
    }
} catch {
    $errors += "SecurityList discovery failed safe: " + (Redact-Text $_.Exception.GetType().Name)
} finally {
    if ($stream) { $stream.Dispose() }
    if ($tcp) { $tcp.Dispose() }
}

$timedOut = -not $listReceived -and $errors.Count -eq 0
$status = Classify-Status $lastMsgType $rejectTag $rejectText $timedOut
if ($errors.Count -gt 0 -and $status -eq "CompletedWithWarnings") {
    $status = "FailedSafeSecurityListRequestRejected"
}
$written = Write-DiscoveryArtifact $status $messages $warnings $errors $connected $loggedOn $requestSent $listReceived $logoutAttempted $logoutSucceeded $attempts
$artifact = $written.artifact

Write-Host "Status: $($artifact.status)"
Write-Host "InstrumentCount: $($artifact.totalInstrumentCount)"
Write-Host "CandidateMatches: $(@($artifact.candidateMatches).Count)"
foreach ($match in @($artifact.candidateMatches)) {
    Write-Host ("{0}: SecurityID={1}; SecurityIDSource={2}; IsApprovedForExternalRun=false" -f $match.symbol, $match.securityId, $match.securityIdSource)
}
if (@($artifact.unmatchedCandidates).Count -gt 0) {
    Write-Host "UnmatchedCandidates: $($artifact.unmatchedCandidates -join ', ')"
}
Write-Host "NoSensitiveContent: true"
Write-Host "IsApprovedForExternalRun: false"
Write-Host "OrderSubmissionAttempted: false"
Write-Host "ShadowReplaySubmitAttempted: false"
Write-Host "TradingMutationAttempted: false"
Write-Host "SchedulerStarted: false"
Write-Host "Sanitized artifact: $($written.path)"

if ($artifact.status -in @("Completed", "CompletedWithWarnings")) { exit 0 }
exit 1
