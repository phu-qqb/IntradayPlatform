param(
    [Parameter(Mandatory = $true)]
    [string]$Path,
    [string]$BaseUrl = "http://localhost:5050",
    [string]$OperatorId = "local-admin",
    [string]$Reason = "Replay local LMAX lab evidence into shadow mode"
)

$ErrorActionPreference = "Stop"

function Assert-LocalUrl {
    param([string]$Url)
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
    try {
        if ($null -eq $Body) {
            return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
        }

        $json = $Body | ConvertTo-Json -Depth 20
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

    foreach ($name in @("value", "Value", "items", "Items", "observations", "replayRuns", "data")) {
        if ($Response.PSObject.Properties.Name -contains $name) {
            $items = $Response.$name
            if ($null -eq $items) { return @() }
            return @($items)
        }
    }

    return @($Response)
}

function Convert-EvidenceToReplayBody {
    param([object]$Evidence)

    $executionReports = @()
    $tradeCaptureReports = @()
    $orderStatuses = @()
    $protocolRejects = @()

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
$resolvedPath = Resolve-Path -LiteralPath $Path
$raw = Get-Content -LiteralPath $resolvedPath -Raw
foreach ($forbidden in @("554=", "password", "authorization", "secret", "token")) {
    if ($raw -match [regex]::Escape($forbidden)) {
        throw "Evidence file appears to contain forbidden sensitive text: $forbidden"
    }
}
$evidence = $raw | ConvertFrom-Json
if (-not ($evidence.PSObject.Properties.Name -contains "schemaVersion") -or $evidence.schemaVersion -ne "lmax-fix-lifecycle-evidence-v1") {
    throw "Unsupported evidence schemaVersion. Expected lmax-fix-lifecycle-evidence-v1."
}

$body = Convert-EvidenceToReplayBody -Evidence $evidence

Write-Host "Replaying LMAX lab evidence file: $resolvedPath"
Write-Host "ExecutionReports=$(@($body.executionReports).Count) OrderStatuses=$(@($body.orderStatuses).Count) TradeCaptureReports=$(@($body.tradeCaptureReports).Count) ProtocolRejects=$(@($body.protocolRejects).Count)"
$result = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-shadow/replay" -Body $body
$replayRunId = if ($result.PSObject.Properties.Name -contains "id") { $result.id } elseif ($result.PSObject.Properties.Name -contains "replayRunId") { $result.replayRunId } else { $null }
Write-Host "ReplayRunId: $replayRunId"
Write-Host "Replay $($result.status): $($result.observationCount) observations, $($result.blockingObservationCount) blocking, $($result.warningObservationCount) warnings" -ForegroundColor Green
$result
