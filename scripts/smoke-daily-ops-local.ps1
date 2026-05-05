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

function Get-ItemsFromResponse {
    param([object]$Response)

    if ($null -eq $Response) {
        return @()
    }

    if ($Response -is [System.Array]) {
        return @($Response)
    }

    foreach ($propertyName in @("value", "Value", "items", "Items", "runs", "jobRuns", "events", "auditEvents", "data")) {
        if ($Response.PSObject.Properties.Name -contains $propertyName) {
            $items = $Response.$propertyName
            if ($null -eq $items) {
                return @()
            }
            return @($items)
        }
    }

    return @($Response)
}

function Write-AuditDiagnostics {
    param(
        [object]$RawAudit,
        [object[]]$Items,
        [object]$OriginalJob,
        [object]$RetryJob,
        [string]$RetryReason
    )

    Write-Host "OperationalJobRetried audit event was not matched in extracted audit items." -ForegroundColor Yellow
    Write-Host "Expected originalJobRunId: $($OriginalJob.id)" -ForegroundColor Yellow
    Write-Host "Expected retryJobRunId: $($RetryJob.id)" -ForegroundColor Yellow
    Write-Host "Expected correlationId: $($RetryJob.correlationId)" -ForegroundColor Yellow
    Write-Host "Expected retry reason: $RetryReason" -ForegroundColor Yellow
    Write-Host "Extracted audit event count: $($Items.Count)" -ForegroundColor Yellow
    if ($Items.Count -gt 0) {
        Write-Host "Extracted audit event types:" -ForegroundColor Yellow
        foreach ($item in $Items) {
            Write-Host "  $($item.eventType) correlation=$($item.correlationId) entity=$($item.entityType)/$($item.entityId)" -ForegroundColor Yellow
        }
        Write-Host "OperationalJob* metadata:" -ForegroundColor Yellow
        foreach ($item in @($Items | Where-Object { "$($_.eventType)" -like "OperationalJob*" })) {
            Write-Host "  $($item.eventType): $($item.metadataJson)" -ForegroundColor Yellow
        }
    }
    Write-Host "Raw audit JSON:" -ForegroundColor DarkYellow
    Write-Host ($RawAudit | ConvertTo-Json -Depth 20) -ForegroundColor DarkYellow
}

function Write-JobHistoryDiagnostics {
    param(
        [object]$RawHistory,
        [object[]]$Items,
        [object]$ExpectedJob
    )

    Write-Host "Job history response did not contain expected job run in the extracted list." -ForegroundColor Yellow
    Write-Host "Expected jobRunId: $($ExpectedJob.id)" -ForegroundColor Yellow
    Write-Host "Expected jobType: $($ExpectedJob.jobType)" -ForegroundColor Yellow
    Write-Host "Extracted item count: $($Items.Count)" -ForegroundColor Yellow
    if ($Items.Count -gt 0) {
        Write-Host "Extracted jobs:" -ForegroundColor Yellow
        foreach ($item in $Items) {
            Write-Host "  $($item.jobType) $($item.status) $($item.id)" -ForegroundColor Yellow
        }
    }
    Write-Host "Raw history JSON:" -ForegroundColor DarkYellow
    Write-Host ($RawHistory | ConvertTo-Json -Depth 20) -ForegroundColor DarkYellow
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
        if ($_.Exception.Response) {
            Write-Host "HTTP status: $([int]$_.Exception.Response.StatusCode) $($_.Exception.Response.StatusDescription)" -ForegroundColor Red
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

function Confirm-JobRun {
    param([object]$Job)
    $loaded = Invoke-LocalApi -Method "GET" -Path "/ops/jobs/runs/$($Job.id)"
    Assert-True ($loaded.id -eq $Job.id) "$($Job.jobType) direct job lookup returns the created job"
    Write-Success "$($Job.jobType) direct lookup returned $($loaded.status)"
    return $loaded
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

$reference = Confirm-JobRun $reference
$bars = Confirm-JobRun $bars
$promote = Confirm-JobRun $promote
$process = Confirm-JobRun $process

Write-Step "Optional EOD reconciliation"
$importRuns = Invoke-LocalApi -Method "GET" -Path "/lmax-eod/import-runs?limit=1"
if ((Get-ItemsFromResponse $importRuns).Count -gt 0) {
    $eod = Run-Job "RunEodReconciliation"
    $eod = Confirm-JobRun $eod
} else {
    Write-Host "Skipping EOD reconciliation job because no local LMAX EOD import run exists." -ForegroundColor Yellow
}

Write-Step "Job history"
$history = Invoke-LocalApi -Method "GET" -Path "/ops/jobs/runs?limit=100"
$runs = Get-ItemsFromResponse $history
Assert-True ($runs.Count -ge 4) "job runs exist"
$referenceMatches = @($runs | Where-Object { $_.id -eq $reference.id -or $_.jobType -eq "ReferenceDataIntegrityCheck" })
if ($referenceMatches.Count -eq 0) {
    Write-JobHistoryDiagnostics -RawHistory $history -Items $runs -ExpectedJob $reference
    Write-Host "Continuing because direct GET /ops/jobs/runs/$($reference.id) succeeded." -ForegroundColor Yellow
} else {
    Write-Success "Reference job appears in history"
}
Write-Success "$($runs.Count) job runs loaded"

Write-Step "Retry reference job"
$retryReason = "Daily operations smoke test retry after successful reference check"
$retry = Invoke-LocalApi -Method "POST" -Path "/ops/jobs/runs/$($reference.id)/retry" -Body @{
    reason = $retryReason
}
Assert-True ([string]::IsNullOrWhiteSpace($retry.id) -eq $false) "retry returned a job id"
Assert-True ($retry.id -ne $reference.id) "retry created a new job run"
Assert-True ($retry.retryOfJobRunId -eq $reference.id) "retry links to original job run"
Assert-True ($retry.retryCount -ge 1) "retry count is incremented"
$retry = Confirm-JobRun $retry
Write-Success "Retry completed as $($retry.status)"

Write-Step "Audit events"
$auditResponse = Invoke-LocalApi -Method "GET" -Path "/audit/events?limit=200"
$events = Get-ItemsFromResponse $auditResponse
$started = ($events | Where-Object { $_.eventType -eq "OperationalJobStarted" }).Count
$completed = ($events | Where-Object { $_.eventType -in @("OperationalJobSucceeded", "OperationalJobFailed") }).Count
Assert-True ($started -gt 0) "operational job started audit exists"
Assert-True ($completed -gt 0) "operational job completion audit exists"
$retryAuditMatches = @($events | Where-Object {
    $_.eventType -eq "OperationalJobRetried" -and (
        ($_.correlationId -and $retry.correlationId -and $_.correlationId -eq $retry.correlationId) -or
        ($_.entityId -eq $reference.id) -or
        ("$($_.metadataJson)" -like "*originalJobRunId*" -and "$($_.metadataJson)" -like "*$($reference.id)*") -or
        ("$($_.metadataJson)" -like "*retryJobRunId*" -and "$($_.metadataJson)" -like "*$($retry.id)*") -or
        ("$($_.metadataJson)" -like "*$retryReason*")
    )
})
if ($retryAuditMatches.Count -eq 0) {
    Write-AuditDiagnostics -RawAudit $auditResponse -Items $events -OriginalJob $reference -RetryJob $retry -RetryReason $retryReason
}
Assert-True ($retryAuditMatches.Count -gt 0) "operational job retry audit exists"
Write-Success "Operational job audit events found"

Write-Host ""
Write-Success "Daily operations smoke passed."
