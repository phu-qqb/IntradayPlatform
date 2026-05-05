param(
    [string]$BaseUrl = "http://localhost:5050",
    [string]$OperatorId = "local-admin"
)

$ErrorActionPreference = "Stop"

function Assert-LocalUrl {
    param([string]$Url)
    if ($Url -notmatch '^https?://(localhost|127\.0\.0\.1)(:\d+)?/?$') {
        throw "smoke-runbooks-local.ps1 is local-only. Refusing non-local BaseUrl '$Url'."
    }
}

function Get-ItemsFromResponse {
    param([object]$Response)
    if ($null -eq $Response) { return @() }
    if ($Response -is [System.Array]) { return @($Response) }
    foreach ($name in @("value", "Value", "items", "Items", "runs", "runbooks", "data")) {
        if ($Response.PSObject.Properties.Name -contains $name) { return @($Response.$name) }
    }
    return @($Response)
}

function Invoke-LocalApi {
    param([string]$Method, [string]$Path, [object]$Body = $null)
    $uri = "$($BaseUrl.TrimEnd('/'))$Path"
    $headers = @{ "X-Operator-Id" = $OperatorId }
    try {
        if ($null -eq $Body) {
            return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers
        }
        $json = $Body | ConvertTo-Json -Depth 30
        return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType "application/json" -Body $json
    } catch {
        Write-Host "$Method $Path failed" -ForegroundColor Red
        if ($null -ne $Body) { Write-Host ($Body | ConvertTo-Json -Depth 30) -ForegroundColor DarkGray }
        if ($_.ErrorDetails.Message) { Write-Host $_.ErrorDetails.Message -ForegroundColor Red }
        throw
    }
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "Assertion failed: $Message" }
    Write-Host "OK: $Message" -ForegroundColor Green
}

function Complete-ManualSteps {
    param([object]$Run)
    $steps = Get-ItemsFromResponse (Invoke-LocalApi "GET" "/ops/runbooks/runs/$($Run.id)/steps")
    foreach ($step in @($steps | Where-Object { $_.status -eq "WaitingForOperator" })) {
        Write-Host "Completing manual gate: $($step.name)" -ForegroundColor Cyan
        $Run = Invoke-LocalApi "POST" "/ops/runbooks/runs/$($Run.id)/complete-manual-step" @{
            stepRunId = $step.id
            reason = "Runbook smoke test operator confirmation"
        }
    }
    return $Run
}

Assert-LocalUrl $BaseUrl

Write-Host "Runbook smoke: health" -ForegroundColor Cyan
$health = Invoke-LocalApi "GET" "/health"
Assert-True ($health.executionGateway -eq "FakeLmaxGateway") "execution gateway remains FakeLmaxGateway"
Assert-True ($health.liveTradingEnabled -eq $false) "live trading remains disabled"
Assert-True ($health.externalConnectionsEnabled -eq $false) "external connections remain disabled"

Write-Host "Runbook definitions" -ForegroundColor Cyan
$definitions = Get-ItemsFromResponse (Invoke-LocalApi "GET" "/ops/runbooks/definitions")
Assert-True (($definitions | Where-Object { $_.runbookType -eq "StartOfDay" }).Count -gt 0) "StartOfDay definition exists"
Assert-True (($definitions | Where-Object { $_.runbookType -eq "IntradayCycle" }).Count -gt 0) "IntradayCycle definition exists"
Assert-True (($definitions | Where-Object { $_.runbookType -eq "EndOfDay" }).Count -gt 0) "EndOfDay definition exists"

Write-Host "Run StartOfDay" -ForegroundColor Cyan
$sod = Invoke-LocalApi "POST" "/ops/runbooks/run" @{ runbookType = "StartOfDay"; reason = "Runbook smoke StartOfDay"; input = @{} }
$sod = Complete-ManualSteps $sod
Assert-True ($sod.status -in @("Succeeded", "PartiallySucceeded", "WaitingForOperator", "Failed")) "StartOfDay returned controlled status $($sod.status)"

Write-Host "Run IntradayCycle" -ForegroundColor Cyan
$intraday = Invoke-LocalApi "POST" "/ops/runbooks/run" @{ runbookType = "IntradayCycle"; reason = "Runbook smoke IntradayCycle"; input = @{} }
$intradaySteps = Get-ItemsFromResponse (Invoke-LocalApi "GET" "/ops/runbooks/runs/$($intraday.id)/steps")
Assert-True (($intradaySteps | Where-Object { $_.jobRunId }).Count -gt 0) "IntradayCycle linked job runs exist"

Write-Host "Run EndOfDay" -ForegroundColor Cyan
$eod = Invoke-LocalApi "POST" "/ops/runbooks/run" @{ runbookType = "EndOfDay"; reason = "Runbook smoke EndOfDay"; input = @{} }
$eod = Complete-ManualSteps $eod
Assert-True ([string]::IsNullOrWhiteSpace($eod.id) -eq $false) "EndOfDay returned runbook id"

Write-Host "History and audit" -ForegroundColor Cyan
$runs = Get-ItemsFromResponse (Invoke-LocalApi "GET" "/ops/runbooks/runs?limit=100")
Assert-True (($runs | Where-Object { $_.id -eq $sod.id -or $_.runbookType -eq "StartOfDay" }).Count -gt 0) "runbook history contains StartOfDay"
$audit = Get-ItemsFromResponse (Invoke-LocalApi "GET" "/audit/events?limit=200")
Assert-True (($audit | Where-Object { $_.eventType -eq "RunbookStarted" }).Count -gt 0) "RunbookStarted audit exists"
Assert-True (($audit | Where-Object { $_.eventType -in @("RunbookCompleted", "RunbookWaitingForOperator", "RunbookFailed") }).Count -gt 0) "runbook completion/wait/fail audit exists"

Write-Host "Schedules" -ForegroundColor Cyan
$schedules = Invoke-LocalApi "GET" "/ops/schedules"
Assert-True ($schedules.schedulerEnabled -eq $false) "local scheduler is disabled by default"

Write-Host "Runbook smoke completed." -ForegroundColor Green
