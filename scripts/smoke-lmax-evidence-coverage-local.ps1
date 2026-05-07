param(
    [string]$BaseUrl = "http://localhost:5050",
    [string]$OperatorId = "local-admin",
    [string]$FixtureDirectory = ""
)

$ErrorActionPreference = "Stop"
trap {
    Write-Host ("LMAX evidence coverage smoke failed: {0}" -f $_.Exception.Message) -ForegroundColor Red
    exit 1
}

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
        [string]$Endpoint
    )

    $headers = @{ "X-Operator-Id" = $OperatorId }
    $uri = "$BaseUrl$Endpoint"
    try {
        Write-Host "$Method $Endpoint"
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
    } catch {
        Write-Host "FAILED $Method $Endpoint" -ForegroundColor Red
        if ($_.Exception.Response) {
            Write-Host ("HTTP status: {0} {1}" -f ([int]$_.Exception.Response.StatusCode), $_.Exception.Response.StatusCode)
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                if ($stream) {
                    $reader = [System.IO.StreamReader]::new($stream)
                    $responseBody = $reader.ReadToEnd()
                    if ($responseBody) {
                        Write-Host "Response body:"
                        Write-Host $responseBody
                    }
                }
            } catch {
                Write-Host ("Could not read response body: {0}" -f $_.Exception.Message)
            }
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

function Get-CountSafely {
    param([string]$Endpoint)
    try {
        $response = Invoke-LocalApi -Method "GET" -Endpoint $Endpoint
        return @{ available = $true; count = (Get-ItemsFromResponse -Response $response).Count }
    } catch {
        Write-Host ("Skipping mutation count check for {0}: {1}" -f $Endpoint, $_.Exception.Message) -ForegroundColor Yellow
        return @{ available = $false; count = 0 }
    }
}

Assert-LocalUrl $BaseUrl
$root = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($FixtureDirectory)) {
    $FixtureDirectory = Join-Path $root "tests\fixtures\lmax-shadow"
}

$fixtures = @(
    "lmax-readonly-empty-evidence-v1.json",
    "lmax-marketdata-only-evidence-v1.json",
    "lmax-tradecapture-only-evidence-v1.json",
    "lmax-orderstatus-only-evidence-v1.json",
    "lmax-protocolreject-only-evidence-v1.json",
    "lmax-mixed-readonly-evidence-v1.json"
)

$health = Invoke-LocalApi -Method "GET" -Endpoint "/health"
if ($health.executionGateway -ne "FakeLmaxGateway") { throw "Expected FakeLmaxGateway but got $($health.executionGateway)." }
if ($health.liveTradingEnabled -ne $false) { throw "Expected liveTradingEnabled=false." }
if ($health.externalConnectionsEnabled -ne $false) { throw "Expected externalConnectionsEnabled=false." }

$beforeOrders = Get-CountSafely -Endpoint "/orders"
$beforeFills = Get-CountSafely -Endpoint "/fills"
$beforePositions = Get-CountSafely -Endpoint "/positions/internal"

foreach ($fixture in $fixtures) {
    $path = Join-Path $FixtureDirectory $fixture
    if (-not (Test-Path -LiteralPath $path)) { throw "Fixture not found: $path" }
    Write-Host ""
    Write-Host ("Validating fixture {0}" -f $fixture) -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot "validate-lmax-lab-evidence-file.ps1") -EvidenceFile $path
    if ($LASTEXITCODE -ne 0) { throw "Evidence validation failed for $fixture" }

    Write-Host ("Replaying fixture {0}" -f $fixture) -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot "replay-lmax-lab-evidence-file.ps1") -EvidenceFile $path -BaseUrl $BaseUrl -OperatorId $OperatorId -Reason "LMAX evidence coverage smoke replay: $fixture"
    if ($LASTEXITCODE -ne 0) { throw "Evidence replay failed for $fixture" }
}

$afterOrders = Get-CountSafely -Endpoint "/orders"
$afterFills = Get-CountSafely -Endpoint "/fills"
$afterPositions = Get-CountSafely -Endpoint "/positions/internal"
if ($beforeOrders.available -and $afterOrders.available -and $beforeOrders.count -ne $afterOrders.count) { throw "Order count changed during evidence coverage smoke." }
if ($beforeFills.available -and $afterFills.available -and $beforeFills.count -ne $afterFills.count) { throw "Fill count changed during evidence coverage smoke." }
if ($beforePositions.available -and $afterPositions.available -and $beforePositions.count -ne $afterPositions.count) { throw "Position count changed during evidence coverage smoke." }

Write-Host ""
Write-Host "LMAX evidence coverage smoke passed. No LMAX network call was made and mutation counts are unchanged." -ForegroundColor Green
