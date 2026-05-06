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
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType "application/json" -Body $json
    } catch {
        Write-Host "FAILED $Method $Endpoint" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host $_.ErrorDetails.Message
        }
        throw
    }
}

Assert-LocalUrl $BaseUrl
$resolvedPath = Resolve-Path -LiteralPath $Path
$raw = Get-Content -LiteralPath $resolvedPath -Raw
$evidence = $raw | ConvertFrom-Json

$body = [ordered]@{
    inputSource = "LabEvidenceFile"
    reason = $Reason
    executionReports = @()
    tradeCaptureReports = @()
    orderStatuses = @()
    protocolRejects = @()
}

foreach ($name in @("executionReports", "tradeCaptureReports", "orderStatuses", "protocolRejects")) {
    if ($evidence.PSObject.Properties.Name -contains $name) {
        $body[$name] = $evidence.$name
    }
}

Write-Host "Replaying LMAX lab evidence file: $resolvedPath"
$result = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-shadow/replay" -Body $body
Write-Host "Replay $($result.status): $($result.observationCount) observations, $($result.blockingObservationCount) blocking, $($result.warningObservationCount) warnings" -ForegroundColor Green
$result
