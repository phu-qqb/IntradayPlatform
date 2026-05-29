param(
    [Parameter(Mandatory=$true)]
    [string]$ArtifactFile
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Resolve-LocalPath([string]$PathValue) {
    if ([IO.Path]::IsPathRooted($PathValue)) { return $PathValue }
    return Join-Path $repoRoot $PathValue
}
function Get-Prop($Object, [string]$Name, $Default = $null) {
    if ($null -ne $Object -and $Object.PSObject.Properties.Name -contains $Name) { return $Object.$Name }
    return $Default
}
function Get-Counter($Artifact, [string]$Name, [int]$Default = 0) {
    if ($Artifact.PSObject.Properties.Name -contains $Name) { return [int]$Artifact.$Name }
    $diag = Get-Prop $Artifact "diagnostics"
    $counters = Get-Prop $diag "messageCounters"
    if ($null -ne $counters -and $counters.PSObject.Properties.Name -contains $Name) { return [int]$counters.$Name }
    return $Default
}

$instrumentMap = @{
    GBPUSD = @{ slashSymbol = "GBP/USD"; securityId = "4002" }
    EURGBP = @{ slashSymbol = "EUR/GBP"; securityId = "4003" }
    USDJPY = @{ slashSymbol = "USD/JPY"; securityId = "4004" }
    AUDUSD = @{ slashSymbol = "AUD/USD"; securityId = "4007" }
}

$artifactPath = Resolve-LocalPath $ArtifactFile
if (-not (Test-Path -LiteralPath $artifactPath)) { throw "Artifact not found: $artifactPath" }
$raw = Get-Content -Raw -LiteralPath $artifactPath
$safeScanText = $raw -replace 'LMAX_DEMO_FIX_USERNAME|LMAX_DEMO_FIX_PASSWORD|LMAX_DEMO_SENDER_COMP_ID|LMAX_DEMO_TARGET_COMP_ID|credentialProfileName|usernamePresent|passwordPresent|senderCompIdPresent|targetCompIdPresent|usernameLength|passwordLength|senderCompIdLength|targetCompIdLength','SAFE_METADATA'
$artifact = $raw | ConvertFrom-Json
$symbol = ([string](Get-Prop $artifact "symbol" (Get-Prop $artifact "instrument"))).ToUpperInvariant()
$issues = @()

if (-not $instrumentMap.ContainsKey($symbol)) { $issues += "UnsupportedSymbol" }
if ($instrumentMap.ContainsKey($symbol)) {
    $def = $instrumentMap[$symbol]
    if ([string]$artifact.slashSymbol -ne $def.slashSymbol) { $issues += "WrongSlashSymbol" }
    if ([string]$artifact.securityId -ne $def.securityId) { $issues += "WrongSecurityId" }
}
if ([string]$artifact.securityIdSource -ne "8") { $issues += "WrongSecurityIdSource" }
if ([string]$artifact.environmentName -ne "Demo") { $issues += "WrongEnvironment" }
if ([string]$artifact.venueProfileName -ne "DemoLondon") { $issues += "WrongVenueProfile" }
if ([string]$artifact.requestMode -ne "SnapshotPlusUpdates") { $issues += "WrongRequestMode" }
if ([string]$artifact.symbolEncodingMode -ne "SecurityIdOnly") { $issues += "WrongSymbolEncodingMode" }
if ([int]$artifact.marketDepth -ne 1) { $issues += "WrongMarketDepth" }

$status = [string]$artifact.status
$snapshotCount = Get-Counter $artifact "marketDataSnapshotCount" $(if ($status -in @("Completed","CompletedWithEmptyBook")) { 1 } else { 0 })
$mdRejectCount = Get-Counter $artifact "marketDataRequestRejectCount"
$businessRejectCount = Get-Counter $artifact "businessMessageRejectCount"
$rejectCount = Get-Counter $artifact "rejectCount"
$errors = @((Get-Prop $artifact "errors" @()))
$warnings = @((Get-Prop $artifact "warnings" @()))
$entryCount = [int](Get-Prop $artifact "entryCount" 0)
$snapshotReceived = [bool](Get-Prop $artifact "snapshotReceived" $false)
$hasBook = $status -in @("Completed","CompletedWithWarnings")
$emptyBook = $status -eq "CompletedWithEmptyBook"
$failedSafe = $status.StartsWith("FailedSafe", [StringComparison]::OrdinalIgnoreCase) -or $status.StartsWith("Blocked", [StringComparison]::OrdinalIgnoreCase)

if (-not ($hasBook -or $emptyBook -or $failedSafe)) { $issues += "UnknownStatus" }
if ($hasBook -and (-not $snapshotReceived -or $entryCount -le 0 -or $null -eq $artifact.bestBid -or $null -eq $artifact.bestAsk -or $null -eq $artifact.mid)) { $issues += "CompletedBookMissingTopOfBook" }
if ($hasBook -and ($snapshotCount -ne 1 -or $mdRejectCount -ne 0 -or $businessRejectCount -ne 0 -or $rejectCount -ne 0 -or $errors.Count -ne 0)) { $issues += "CompletedBookRejectsOrErrors" }
if ($emptyBook -and (-not [bool]$artifact.logonSucceeded -or -not [bool]$artifact.snapshotRequestAttempted -or -not $snapshotReceived -or $entryCount -ne 0)) { $issues += "EmptyBookDidNotReceiveEmptySnapshot" }
if ($emptyBook -and ($null -ne $artifact.bestBid -or $null -ne $artifact.bestAsk -or $null -ne $artifact.mid)) { $issues += "EmptyBookHasTopOfBook" }
if ($emptyBook -and ($snapshotCount -ne 1 -or $mdRejectCount -ne 0 -or $businessRejectCount -ne 0 -or $rejectCount -ne 0 -or $errors.Count -ne 0)) { $issues += "EmptyBookRejectsOrErrors" }
foreach ($flag in @("orderSubmissionAttempted","shadowReplaySubmitAttempted","tradingMutationAttempted","schedulerStarted","credentialValuesReturned")) {
    if ([bool](Get-Prop $artifact $flag $false)) { $issues += "UnsafeFlag:$flag" }
}
if (-not [bool](Get-Prop $artifact "noSensitiveContent" $false)) { $issues += "NoSensitiveContentFalse" }
if ([string](Get-Prop $artifact "redactionStatus" "") -ne "Redacted") { $issues += "RedactionStatusNotRedacted" }
if ($safeScanText -match '(?i)(password\s*[:=]|secret\s*[:=]|token\s*[:=]|apikey\s*[:=]|api_key\s*[:=]|privatekey\s*[:=]|private_key\s*[:=]|authorization\s*[:=]|bearer\s+|\b553=|\b554=|host\s*=|user\s*=|account\s*=|raw\s*fix|NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|TradeCapture|OrderStatusRequest|SubmitOrder)') { $issues += "SensitiveOrOrderText" }

$classification = if ($issues.Count -gt 0) { "UnsafeFail" } elseif ($emptyBook) { "CompletedWithEmptyBook" } elseif ($failedSafe) { "FailedSafe" } else { "CompletedWithBook" }
$decision = if ($classification -eq "UnsafeFail") { "FAIL" } elseif ($classification -eq "CompletedWithBook") { "PASS" } else { "PASS_WITH_KNOWN_WARNINGS" }

$report = [ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7H"
    sourceArtifactFile = "$artifactPath"
    symbol = $symbol
    slashSymbol = [string]$artifact.slashSymbol
    securityId = [string]$artifact.securityId
    securityIdSource = [string]$artifact.securityIdSource
    status = $status
    closureClassification = $classification
    finalDecision = $decision
    snapshotReceived = $snapshotReceived
    entryCount = $entryCount
    bestBid = $artifact.bestBid
    bestAsk = $artifact.bestAsk
    mid = $artifact.mid
    marketDataSnapshotCount = $snapshotCount
    marketDataRequestRejectCount = $mdRejectCount
    businessMessageRejectCount = $businessRejectCount
    rejectCount = $rejectCount
    warnings = $warnings
    errors = $errors
    orderSubmissionAttempted = [bool](Get-Prop $artifact "orderSubmissionAttempted" $false)
    shadowReplaySubmitAttempted = [bool](Get-Prop $artifact "shadowReplaySubmitAttempted" $false)
    tradingMutationAttempted = [bool](Get-Prop $artifact "tradingMutationAttempted" $false)
    schedulerStarted = [bool](Get-Prop $artifact "schedulerStarted" $false)
    credentialValuesReturned = [bool](Get-Prop $artifact "credentialValuesReturned" $false)
    noSensitiveContent = [bool](Get-Prop $artifact "noSensitiveContent" $false)
    redactionStatus = [string](Get-Prop $artifact "redactionStatus" "")
    issues = $issues
}

$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir ("phase7h-additional-instrument-snapshot-review-{0}.json" -f $symbol.ToLowerInvariant())
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outPath -Encoding UTF8
Write-Host "Symbol: $symbol"
Write-Host "Status: $status"
Write-Host "Classification: $classification"
Write-Host "EntryCount: $entryCount"
Write-Host ("BestBid/BestAsk/Mid: {0}/{1}/{2}" -f $artifact.bestBid,$artifact.bestAsk,$artifact.mid)
Write-Host "Decision: $decision"
Write-Host "Report: $outPath"
if ($decision -eq "FAIL") { exit 1 }
$global:LASTEXITCODE = 0
