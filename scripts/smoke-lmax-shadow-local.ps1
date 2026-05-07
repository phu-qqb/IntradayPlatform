param(
    [string]$BaseUrl = "http://localhost:5050",
    [string]$OperatorId = "local-admin",
    [string]$FixturePath = ".\tests\fixtures\lmax-shadow\lmax-fix-lifecycle-evidence-v1.json",
    [string]$Reason = "Replay synthetic LMAX lifecycle evidence through local shadow smoke"
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "== $Message ==" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "OK: $Message" -ForegroundColor Green
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw "Assertion failed: $Message"
    }
}

function Assert-LocalUrl {
    param([string]$Url)
    $uri = [Uri]$Url
    Assert-True ($uri.Scheme -in @("http", "https")) "API URL uses http/https"
    Assert-True ($uri.Host -in @("localhost", "127.0.0.1")) "API URL is local only"
}

function Invoke-LocalApi {
    param(
        [string]$Method,
        [string]$Endpoint,
        [object]$Body = $null
    )

    $headers = @{ "X-Operator-Id" = $OperatorId }
    $uri = "$BaseUrl$Endpoint"
    try {
        if ($null -eq $Body) {
            Write-Host "$Method $Endpoint"
            return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
        }

        $json = $Body | ConvertTo-Json -Depth 30
        Write-Host "$Method $Endpoint"
        Write-Host "Body: $json"
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType "application/json" -Body $json
    } catch {
        Write-Host "FAILED $Method $Endpoint" -ForegroundColor Red
        if ($_.Exception.Response) {
            Write-Host "HTTP status: $([int]$_.Exception.Response.StatusCode) $($_.Exception.Response.StatusCode)"
        }
        if ($_.ErrorDetails.Message) {
            Write-Host $_.ErrorDetails.Message
        }
        throw
    }
}

function Get-ItemsFromResponse {
    param([object]$Response)

    if ($null -eq $Response) { return @() }
    if ($Response -is [array]) { return @($Response) }

    foreach ($name in @("value", "Value", "items", "Items", "observations", "replayRuns", "events", "auditEvents", "data")) {
        if ($Response.PSObject.Properties.Name -contains $name) {
            $items = $Response.$name
            if ($null -eq $items) { return @() }
            return @($items)
        }
    }

    return @($Response)
}

function Get-CountSafely {
    param([string]$Endpoint)
    try {
        $response = Invoke-LocalApi -Method "GET" -Endpoint $Endpoint
        $items = Get-ItemsFromResponse -Response $response
        return @{ available = $true; count = $items.Count }
    } catch {
        Write-Host ("Skipping mutation count check for {0}: {1}" -f $Endpoint, $_.Exception.Message) -ForegroundColor Yellow
        return @{ available = $false; count = 0 }
    }
}

function Convert-EvidenceToReplayBody {
    param([object]$Evidence)

    $executionReports = @()
    $tradeCaptureReports = @()
    $protocolRejects = @()
    $orderStatuses = @()
    if ($Evidence.PSObject.Properties.Name -contains "executionReports") {
        $executionReports = @($Evidence.executionReports)
    }

    if ($Evidence.PSObject.Properties.Name -contains "tradeCaptureReports") {
        $tradeCaptureReports = @($Evidence.tradeCaptureReports)
    }

    if ($Evidence.PSObject.Properties.Name -contains "orderStatusReports") {
        $orderStatuses = @($Evidence.orderStatusReports)
    } elseif ($Evidence.PSObject.Properties.Name -contains "orderStatuses") {
        $orderStatuses = @($Evidence.orderStatuses)
    }

    if ($Evidence.PSObject.Properties.Name -contains "protocolRejects") {
        $protocolRejects = @($Evidence.protocolRejects)
    }

    return [ordered]@{
        inputSource = "LabEvidenceFile"
        reason = $Reason
        executionReports = $executionReports
        tradeCaptureReports = $tradeCaptureReports
        orderStatuses = $orderStatuses
        protocolRejects = $protocolRejects
    }
}

Assert-LocalUrl $BaseUrl
$resolvedFixturePath = Resolve-Path -LiteralPath $FixturePath
$rawFixture = Get-Content -LiteralPath $resolvedFixturePath -Raw
foreach ($forbidden in @("554=", "password", "authorization", "secret", "token")) {
    Assert-True (-not ($rawFixture -match [regex]::Escape($forbidden))) "fixture does not contain forbidden sensitive text: $forbidden"
}
$evidence = $rawFixture | ConvertFrom-Json
Assert-True (($evidence.PSObject.Properties.Name -contains "schemaVersion") -and $evidence.schemaVersion -eq "lmax-fix-lifecycle-evidence-v1") "fixture schemaVersion is lmax-fix-lifecycle-evidence-v1"

Write-Step "Health"
$health = Invoke-LocalApi -Method "GET" -Endpoint "/health"
Assert-True ($health.executionGateway -eq "FakeLmaxGateway") "execution gateway remains FakeLmaxGateway"
Assert-True (-not [bool]$health.liveTradingEnabled) "live trading remains disabled"
Assert-True (-not [bool]$health.externalConnectionsEnabled) "external connections remain disabled"
Write-Success "Runtime safety flags are unchanged"

Write-Step "Mutation guard baseline"
$beforeOrders = Get-CountSafely -Endpoint "/orders"
$beforeFills = Get-CountSafely -Endpoint "/fills"
$beforePositions = Get-CountSafely -Endpoint "/positions/internal"
Write-Success "Captured available baseline counts"

Write-Step "Replay fixture"
$body = Convert-EvidenceToReplayBody -Evidence $evidence
$result = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-shadow/replay" -Body $body
$replayRunId = if ($result.PSObject.Properties.Name -contains "id") { $result.id } elseif ($result.PSObject.Properties.Name -contains "replayRunId") { $result.replayRunId } else { $null }
Assert-True (-not [string]::IsNullOrWhiteSpace($replayRunId)) "replay returned a replay run id"
Write-Success "Replay run created: $replayRunId"

Write-Step "Replay run detail"
$run = Invoke-LocalApi -Method "GET" -Endpoint "/lmax-shadow/replay-runs/$replayRunId"
Assert-True ($run.status -in @("Completed", "CompletedWithWarnings")) "replay run completed"
Assert-True ([int]$run.observationCount -gt 0) "replay run has observations"
Write-Success "Replay status $($run.status), observations $($run.observationCount), blocking $($run.blockingObservationCount), warnings $($run.warningObservationCount)"

Write-Step "Observations"
$observationsResponse = Invoke-LocalApi -Method "GET" -Endpoint "/lmax-shadow/observations?replayRunId=$replayRunId&limit=100"
$observations = Get-ItemsFromResponse -Response $observationsResponse
Assert-True ($observations.Count -gt 0) "observations endpoint returned replay observations"
Assert-True (@($observations | Where-Object { -not [string]::IsNullOrWhiteSpace($_.fingerprint) }).Count -eq $observations.Count) "all observations include fingerprints"
$types = ($observations | ForEach-Object { $_.type } | Sort-Object -Unique) -join ", "
Write-Success "Observation types: $types"

Write-Step "Mutation guard after replay"
$afterOrders = Get-CountSafely -Endpoint "/orders"
$afterFills = Get-CountSafely -Endpoint "/fills"
$afterPositions = Get-CountSafely -Endpoint "/positions/internal"
if ($beforeOrders.available -and $afterOrders.available) { Assert-True ($beforeOrders.count -eq $afterOrders.count) "order count unchanged" }
if ($beforeFills.available -and $afterFills.available) { Assert-True ($beforeFills.count -eq $afterFills.count) "fill count unchanged" }
if ($beforePositions.available -and $afterPositions.available) { Assert-True ($beforePositions.count -eq $afterPositions.count) "position count unchanged" }
Write-Success "Available internal counts are unchanged"

Write-Step "Audit events"
$auditResponse = Invoke-LocalApi -Method "GET" -Endpoint "/audit/events?limit=100"
$auditEvents = Get-ItemsFromResponse -Response $auditResponse
$shadowAudit = @($auditEvents | Where-Object {
    $_.eventType -in @("LmaxShadowReplayStarted", "LmaxShadowReplayCompleted", "LmaxShadowObservationCreated") -and
    (([string]$_.entityId -eq [string]$replayRunId) -or ([string]$_.metadataJson -like "*$replayRunId*"))
})
Assert-True ($shadowAudit.Count -gt 0) "shadow replay audit events exist"
Write-Success "Shadow audit events found: $($shadowAudit.Count)"

Write-Step "Summary"
Write-Host "ReplayRunId=$replayRunId"
Write-Host "Status=$($run.status)"
Write-Host "ObservationCount=$($run.observationCount)"
Write-Host "BlockingObservationCount=$($run.blockingObservationCount)"
Write-Host "WarningObservationCount=$($run.warningObservationCount)"
Write-Host "No external URL, credential, live FIX, or LMAX runtime integration was used by this smoke."
