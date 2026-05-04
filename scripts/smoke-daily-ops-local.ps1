param(
    [string]$BaseUrl = "http://localhost:5050",
    [string]$OperatorId = "local-admin"
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "== $Message ==" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
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
    if ($uri.Host -notin @("localhost", "127.0.0.1")) {
        throw "Only localhost API URLs are allowed. Refusing $Url"
    }
}

function Invoke-LocalApi {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null
    )

    $headers = @{ "X-Operator-Id" = $OperatorId }
    $params = @{
        Method = $Method
        Uri = "$BaseUrl$Path"
        Headers = $headers
    }
    if ($null -ne $Body) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 20)
    }

    try {
        Invoke-RestMethod @params
    }
    catch {
        Write-Host "API call failed: $Method $Path" -ForegroundColor Red
        if ($null -ne $Body) {
            Write-Host "Body: $($Body | ConvertTo-Json -Depth 20)" -ForegroundColor DarkGray
        }
        if ($_.ErrorDetails.Message) {
            Write-Host $_.ErrorDetails.Message -ForegroundColor Red
        }
        throw
    }
}

function Run-Job {
    param([string]$JobType)
    Write-Step "Run $JobType"
    $job = Invoke-LocalApi -Method "POST" -Path "/ops/jobs/run" -Body @{
        jobType = $JobType
        reason = "Daily operations smoke test: $JobType"
        input = @{}
    }
    Assert-True ([string]::IsNullOrWhiteSpace($job.id) -eq $false) "$JobType returned a job id"
    Write-Success "$JobType completed as $($job.status)"
    return $job
}

Assert-LocalUrl $BaseUrl

Write-Step "Health"
$health = Invoke-LocalApi -Method "GET" -Path "/health"
Assert-True ($health.databaseReachable -eq $true) "database reachable"
Assert-True ($health.executionGateway -eq "FakeLmaxGateway") "FakeLmaxGateway remains the execution gateway"
Assert-True ($health.liveTradingEnabled -eq $false) "live trading disabled"
Assert-True ($health.externalConnectionsEnabled -eq $false) "external connections disabled"
Write-Success "Health is safe local"

Write-Step "Daily summary"
$summary = Invoke-LocalApi -Method "GET" -Path "/ops/daily-summary"
Assert-True ($null -ne $summary.date) "daily summary returned a date"
Write-Success "Daily summary loaded for $($summary.date)"

$reference = Run-Job "ReferenceDataIntegrityCheck"
$bars = Run-Job "BuildMarketDataBars"
$promote = Run-Job "PromoteReadyWeightBatches"
$process = Run-Job "ProcessPendingModelRuns"

Write-Step "Optional EOD reconciliation"
$importRuns = Invoke-LocalApi -Method "GET" -Path "/lmax-eod/import-runs?limit=1"
if ($importRuns.Count -gt 0) {
    $eod = Run-Job "RunEodReconciliation"
} else {
    Write-Host "Skipping EOD reconciliation job because no local LMAX EOD import run exists." -ForegroundColor Yellow
}

Write-Step "Job history"
$runs = Invoke-LocalApi -Method "GET" -Path "/ops/jobs/runs?limit=50"
Assert-True ($runs.Count -ge 4) "job runs exist"
Assert-True (($runs | Where-Object { $_.id -eq $reference.id }).Count -eq 1) "reference job appears in history"
Write-Success "$($runs.Count) job runs loaded"

Write-Step "Audit events"
$events = Invoke-LocalApi -Method "GET" -Path "/audit/events?limit=100"
$started = ($events | Where-Object { $_.eventType -eq "OperationalJobStarted" }).Count
$completed = ($events | Where-Object { $_.eventType -in @("OperationalJobSucceeded", "OperationalJobFailed") }).Count
Assert-True ($started -gt 0) "operational job started audit exists"
Assert-True ($completed -gt 0) "operational job completion audit exists"
Write-Success "Operational job audit events found"

Write-Host ""
Write-Success "Daily operations smoke passed."
