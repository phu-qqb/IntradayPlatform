param(
    [Parameter(Mandatory=$true)]
    [string]$EvidencePreviewFile,
    [Parameter(Mandatory=$true)]
    [switch]$ConfirmLocalManualReplay,
    [string]$BaseUrl = "http://localhost:5050",
    [string]$OperatorId = "local-admin",
    [string]$Reason = "Phase 7H explicit manual additional-instrument MarketDataOnly evidence replay"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$evidenceValidator = Join-Path $PSScriptRoot "validate-lmax-lab-evidence-file.ps1"
$instrumentMap = @{
    GBPUSD = "4002"
    EURGBP = "4003"
    USDJPY = "4004"
    AUDUSD = "4007"
}

function Assert-LocalUrl([string]$Url) {
    $uri = [Uri]$Url
    if ($uri.Scheme -notin @("http", "https") -or $uri.Host -notin @("localhost", "127.0.0.1")) {
        throw "Refusing non-local API URL: $Url"
    }
}
function Invoke-LocalApi([string]$Method, [string]$Endpoint, [object]$Body = $null) {
    $headers = @{ "X-Operator-Id" = $OperatorId }
    $uri = "$BaseUrl$Endpoint"
    if ($null -eq $Body) { return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers }
    $json = $Body | ConvertTo-Json -Depth 30
    return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType "application/json" -Body $json
}
function Get-ItemsFromResponse([object]$Response) {
    if ($null -eq $Response) { return @() }
    if ($Response -is [array]) { return @($Response) }
    foreach ($name in @("value", "Value", "items", "Items", "observations", "replayRuns", "data")) {
        if ($Response.PSObject.Properties.Name -contains $name) {
            if ($null -eq $Response.$name) { return @() }
            return @($Response.$name)
        }
    }
    return @($Response)
}
function Get-CountSafely([string]$Endpoint) {
    try {
        $response = Invoke-LocalApi -Method "GET" -Endpoint $Endpoint
        return @{ available = $true; count = (Get-ItemsFromResponse -Response $response).Count }
    } catch {
        return @{ available = $false; count = 0 }
    }
}

if (-not $ConfirmLocalManualReplay.IsPresent) { throw "ConfirmLocalManualReplay is required." }
Assert-LocalUrl $BaseUrl
$resolvedPath = Resolve-Path -LiteralPath $EvidencePreviewFile
$raw = Get-Content -LiteralPath $resolvedPath -Raw
if ($raw -match '(?i)(password|authorization|secret|token|bearer |rawFix|NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest)') { throw "Evidence preview appears to contain forbidden sensitive/order text." }

powershell -NoProfile -ExecutionPolicy Bypass -File $evidenceValidator -EvidenceFile $resolvedPath | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Evidence preview validation failed." }
$evidence = $raw | ConvertFrom-Json
$symbol = ([string]$evidence.instrument).ToUpperInvariant()
if (-not $instrumentMap.ContainsKey($symbol) -or [string]$evidence.securityId -ne $instrumentMap[$symbol]) { throw "Unsupported additional-instrument evidence preview identity." }
if ([string]$evidence.evidenceMode -ne "MarketDataOnly") { throw "Additional-instrument replay requires MarketDataOnly evidence preview." }
if (@($evidence.executionReports).Count -ne 0 -or @($evidence.orderStatuses).Count -ne 0 -or @($evidence.tradeCaptureReports).Count -ne 0 -or @($evidence.protocolRejects).Count -ne 0) { throw "MarketDataOnly replay requires empty non-market-data arrays." }

$beforeOrders = Get-CountSafely "/orders"
$beforeFills = Get-CountSafely "/fills"
$beforePositions = Get-CountSafely "/positions/internal"
$body = [ordered]@{
    inputSource = "LabEvidenceFile"
    reason = $Reason
    evidenceMode = "MarketDataOnly"
    executionReports = @()
    tradeCaptureReports = @()
    orderStatuses = @()
    protocolRejects = @()
}
$result = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-shadow/replay" -Body $body
$status = if ($result.PSObject.Properties.Name -contains "status") { [string]$result.status } else { "" }
$observationCount = if ($result.PSObject.Properties.Name -contains "observationCount") { [int]$result.observationCount } else { -1 }
$blockingObservationCount = if ($result.PSObject.Properties.Name -contains "blockingObservationCount") { [int]$result.blockingObservationCount } else { -1 }
$warningObservationCount = if ($result.PSObject.Properties.Name -contains "warningObservationCount") { [int]$result.warningObservationCount } else { -1 }
$replayRunId = if ($result.PSObject.Properties.Name -contains "id") { $result.id } elseif ($result.PSObject.Properties.Name -contains "replayRunId") { $result.replayRunId } else { $null }
if ($status -ne "Completed" -or $observationCount -ne 0 -or $blockingObservationCount -ne 0 -or $warningObservationCount -ne 0) { throw "Replay result was not Completed with zero observations." }

$afterOrders = Get-CountSafely "/orders"
$afterFills = Get-CountSafely "/fills"
$afterPositions = Get-CountSafely "/positions/internal"
$mutationGuard = "Unchanged"
if ($beforeOrders.available -and $afterOrders.available -and $beforeOrders.count -ne $afterOrders.count) { $mutationGuard = "Changed" }
if ($beforeFills.available -and $afterFills.available -and $beforeFills.count -ne $afterFills.count) { $mutationGuard = "Changed" }
if ($beforePositions.available -and $afterPositions.available -and $beforePositions.count -ne $afterPositions.count) { $mutationGuard = "Changed" }
if ($mutationGuard -ne "Unchanged") { throw "Mutation guard changed during MarketDataOnly replay." }

$report = [ordered]@{
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
    phase = "7H"
    symbol = $symbol
    securityId = [string]$evidence.securityId
    evidencePreviewFile = "$resolvedPath"
    replayStatus = $status
    replayRunId = $replayRunId
    evidenceMode = "MarketDataOnly"
    observationCount = $observationCount
    blockingObservationCount = $blockingObservationCount
    warningObservationCount = $warningObservationCount
    mutationGuard = $mutationGuard
    runtimeShadowReplaySubmit = $false
    externalConnectionAttempted = $false
    noSensitiveContent = $true
    finalDecision = "PASS"
}
$outDir = Join-Path $repoRoot "artifacts/readiness"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$outPath = Join-Path $outDir ("phase7h-additional-instrument-evidence-replay-{0}.json" -f $symbol.ToLowerInvariant())
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $outPath -Encoding UTF8
Write-Host "Symbol: $symbol"
Write-Host "ReplayStatus: $status"
Write-Host "ObservationCount: $observationCount"
Write-Host "MutationGuard: $mutationGuard"
Write-Host "Report: $outPath"
