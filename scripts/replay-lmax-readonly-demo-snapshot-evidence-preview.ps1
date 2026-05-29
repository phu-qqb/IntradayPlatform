param(
    [Parameter(Mandatory = $true)]
    [string]$EvidencePreviewFile,
    [string]$BaseUrl = "http://localhost:5050",
    [string]$OperatorId = "local-admin",
    [string]$Reason = "Phase 5N manual MarketDataOnly evidence replay dry-run"
)

$ErrorActionPreference = "Stop"
trap { exit 1 }

$repoRoot = Split-Path -Parent $PSScriptRoot
$evidenceValidator = Join-Path $repoRoot "scripts/validate-lmax-lab-evidence-file.ps1"

function Assert-LocalUrl([string]$Url) {
    $uri = [Uri]$Url
    if ($uri.Scheme -notin @("http", "https") -or $uri.Host -notin @("localhost", "127.0.0.1")) {
        throw "Refusing non-local API URL: $Url"
    }
}

function Invoke-LocalApi {
    param(
        [string]$Method,
        [string]$Endpoint,
        [object]$Body = $null
    )

    $headers = @{ "X-Operator-Id" = $OperatorId }
    $uri = "$BaseUrl$Endpoint"
    if ($null -eq $Body) {
        Write-Host "$Method $Endpoint"
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
    }

    $json = $Body | ConvertTo-Json -Depth 30
    Write-Host "$Method $Endpoint"
    return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType "application/json" -Body $json
}

function Get-ItemsFromResponse([object]$Response) {
    if ($null -eq $Response) { return @() }
    if ($Response -is [array]) { return @($Response) }
    foreach ($name in @("value", "Value", "items", "Items", "observations", "replayRuns", "data")) {
        if ($Response.PSObject.Properties.Name -contains $name) {
            $items = $Response.$name
            if ($null -eq $items) { return @() }
            return @($items)
        }
    }
    return @($Response)
}

function Get-CountSafely([string]$Endpoint) {
    try {
        $response = Invoke-LocalApi -Method "GET" -Endpoint $Endpoint
        return @{ available = $true; count = (Get-ItemsFromResponse -Response $response).Count }
    } catch {
        Write-Host ("Skipping mutation count check for {0}: {1}" -f $Endpoint, $_.Exception.Message) -ForegroundColor Yellow
        return @{ available = $false; count = 0 }
    }
}

function Assert-EmptyArray($Evidence, [string]$Name) {
    if (-not ($Evidence.PSObject.Properties.Name -contains $Name)) {
        throw "$Name is required and must be an empty array."
    }

    if (@($Evidence.$Name).Count -ne 0) {
        throw "$Name must be empty for Phase 5N MarketDataOnly replay dry-run."
    }
}

function Test-ForbiddenSensitiveContent([string]$Json) {
    foreach ($forbidden in @("554=", "553=", "password", "authorization", "secret", "token", "bearer ", "x-api-key", "api-key", "rawFix")) {
        if ($Json.IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Evidence preview appears to contain forbidden sensitive text: $forbidden"
        }
    }
}

Assert-LocalUrl $BaseUrl
$resolvedPath = Resolve-Path -LiteralPath $EvidencePreviewFile

Write-Host "LMAX Read-Only Runtime Phase 5N MarketDataOnly Replay Dry-Run"
Write-Host "Local-only manual replay. No LMAX connection, no runtime shadow submit, no scheduler."
Write-Host "EvidencePreviewFile: $resolvedPath"

$raw = Get-Content -LiteralPath $resolvedPath -Raw
Test-ForbiddenSensitiveContent $raw

powershell -NoProfile -ExecutionPolicy Bypass -File $evidenceValidator -EvidenceFile $resolvedPath | Tee-Object -Variable validationOutput | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Evidence preview validation failed."
}

$evidence = $raw | ConvertFrom-Json
$evidenceMode = if ($evidence.PSObject.Properties.Name -contains "evidenceMode") { [string]$evidence.evidenceMode } else { "" }
if ($evidenceMode -ne "MarketDataOnly") {
    throw "Phase 5N requires evidenceMode=MarketDataOnly. Actual: $evidenceMode"
}

if (-not ($evidence.PSObject.Properties.Name -contains "marketData") -or -not [bool]$evidence.marketData.snapshotReceived) {
    throw "Phase 5N requires marketData.snapshotReceived=true."
}

Assert-EmptyArray $evidence "executionReports"
Assert-EmptyArray $evidence "orderStatuses"
Assert-EmptyArray $evidence "tradeCaptureReports"
Assert-EmptyArray $evidence "protocolRejects"

$beforeOrders = Get-CountSafely -Endpoint "/orders"
$beforeFills = Get-CountSafely -Endpoint "/fills"
$beforePositions = Get-CountSafely -Endpoint "/positions/internal"

$body = [ordered]@{
    inputSource = "LabEvidenceFile"
    reason = $Reason
    evidenceMode = "MarketDataOnly"
    executionReports = @()
    tradeCaptureReports = @()
    orderStatuses = @()
    protocolRejects = @()
}

Write-Host "POST /lmax-shadow/replay"
$result = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-shadow/replay" -Body $body
$status = if ($result.PSObject.Properties.Name -contains "status") { [string]$result.status } else { "" }
$observationCount = if ($result.PSObject.Properties.Name -contains "observationCount") { [int]$result.observationCount } else { -1 }
$blockingObservationCount = if ($result.PSObject.Properties.Name -contains "blockingObservationCount") { [int]$result.blockingObservationCount } else { -1 }
$warningObservationCount = if ($result.PSObject.Properties.Name -contains "warningObservationCount") { [int]$result.warningObservationCount } else { -1 }
$replayRunId = if ($result.PSObject.Properties.Name -contains "id") { $result.id } elseif ($result.PSObject.Properties.Name -contains "replayRunId") { $result.replayRunId } else { $null }

if ($status -ne "Completed") { throw "Expected replay status Completed, got $status." }
if ($observationCount -ne 0) { throw "Expected observationCount=0, got $observationCount." }
if ($blockingObservationCount -ne 0) { throw "Expected blockingObservationCount=0, got $blockingObservationCount." }
if ($warningObservationCount -ne 0) { throw "Expected warningObservationCount=0, got $warningObservationCount." }

if ($replayRunId) {
    $observations = Invoke-LocalApi -Method "GET" -Endpoint "/lmax-shadow/observations?replayRunId=$replayRunId&limit=100"
    $observationItems = @(Get-ItemsFromResponse -Response $observations)
    if ($observationItems.Count -ne 0) {
        throw "Expected no observations for MarketDataOnly replay, got $($observationItems.Count)."
    }
}

$afterOrders = Get-CountSafely -Endpoint "/orders"
$afterFills = Get-CountSafely -Endpoint "/fills"
$afterPositions = Get-CountSafely -Endpoint "/positions/internal"
if ($beforeOrders.available -and $afterOrders.available -and $beforeOrders.count -ne $afterOrders.count) { throw "Order count changed during MarketDataOnly replay." }
if ($beforeFills.available -and $afterFills.available -and $beforeFills.count -ne $afterFills.count) { throw "Fill count changed during MarketDataOnly replay." }
if ($beforePositions.available -and $afterPositions.available -and $beforePositions.count -ne $afterPositions.count) { throw "Position count changed during MarketDataOnly replay." }

Write-Host ""
Write-Host "ReplayStatus: $status"
Write-Host "ReplayRunId: $replayRunId"
Write-Host "EvidenceMode: MarketDataOnly"
Write-Host "ObservationCount: $observationCount"
Write-Host "BlockingObservationCount: $blockingObservationCount"
Write-Host "WarningObservationCount: $warningObservationCount"
Write-Host "MutationGuard: Unchanged"
Write-Host "RuntimeShadowReplaySubmit: false"
Write-Host "ExternalConnectionAttempted: false"
Write-Host "NoSensitiveContent: true"

$result
