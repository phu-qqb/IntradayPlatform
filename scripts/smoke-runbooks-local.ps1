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
    if ($null -eq $Response) { return }
    if ($Response -is [System.Array]) {
        foreach ($item in $Response) { $item }
        return
    }
    foreach ($name in @("value", "Value", "items", "Items", "runs", "runbookRuns", "runbookDefinitions", "definitions", "events", "auditEvents", "steps", "data")) {
        $property = $Response.PSObject.Properties | Where-Object { $_.Name -eq $name } | Select-Object -First 1
        if ($null -ne $property) {
            if ($null -eq $property.Value) { return }
            if ($property.Value -is [System.Array]) {
                foreach ($item in $property.Value) { $item }
                return
            }
            $property.Value
            return
        }
    }
    $Response
}

function Write-ListDiagnostics {
    param(
        [string]$Label,
        [object]$RawResponse,
        [object[]]$Items,
        [string]$Expected
    )
    Write-Host "$Label diagnostics" -ForegroundColor Yellow
    Write-Host "Expected: $Expected" -ForegroundColor Yellow
    Write-Host "Extracted item count: $($Items.Count)" -ForegroundColor Yellow
    Write-Host "Extracted id/name/runbookType values:" -ForegroundColor Yellow
    foreach ($item in $Items) {
        Write-Host "  id=$($item.id) name=$($item.name) runbookType=$($item.runbookType) status=$($item.status)" -ForegroundColor Yellow
    }
    Write-Host "Raw response JSON:" -ForegroundColor Yellow
    Write-Host ($RawResponse | ConvertTo-Json -Depth 30) -ForegroundColor DarkYellow
}

function Find-RunbookDefinition {
    param([object[]]$Definitions, [string]$RunbookType, [string]$NameFragment)
    $match = $Definitions | Where-Object { $_.runbookType -eq $RunbookType } | Select-Object -First 1
    if ($null -ne $match) { return $match }
    return $Definitions | Where-Object { "$($_.name)" -like "*$NameFragment*" } | Select-Object -First 1
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
$definitionsResponse = Invoke-LocalApi "GET" "/ops/runbooks/definitions"
$definitions = @(Get-ItemsFromResponse $definitionsResponse)
$startOfDayDefinition = Find-RunbookDefinition -Definitions $definitions -RunbookType "StartOfDay" -NameFragment "Start of Day"
$intradayDefinition = Find-RunbookDefinition -Definitions $definitions -RunbookType "IntradayCycle" -NameFragment "Intraday"
$endOfDayDefinition = Find-RunbookDefinition -Definitions $definitions -RunbookType "EndOfDay" -NameFragment "End of Day"
if ($null -eq $startOfDayDefinition) { Write-ListDiagnostics "Runbook definitions" $definitionsResponse $definitions "StartOfDay"; throw "Assertion failed: StartOfDay definition exists" }
if ($null -eq $intradayDefinition) { Write-ListDiagnostics "Runbook definitions" $definitionsResponse $definitions "IntradayCycle"; throw "Assertion failed: IntradayCycle definition exists" }
if ($null -eq $endOfDayDefinition) { Write-ListDiagnostics "Runbook definitions" $definitionsResponse $definitions "EndOfDay"; throw "Assertion failed: EndOfDay definition exists" }
Write-Host "OK: StartOfDay definition exists" -ForegroundColor Green
Write-Host "OK: IntradayCycle definition exists" -ForegroundColor Green
Write-Host "OK: EndOfDay definition exists" -ForegroundColor Green

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
$runsResponse = Invoke-LocalApi "GET" "/ops/runbooks/runs?limit=100"
$runs = @(Get-ItemsFromResponse $runsResponse)
if ((@($runs | Where-Object { $_.id -eq $sod.id -or $_.runbookType -eq "StartOfDay" }).Count) -eq 0) {
    Write-ListDiagnostics "Runbook history" $runsResponse $runs "StartOfDay run id $($sod.id)"
    throw "Assertion failed: runbook history contains StartOfDay"
}
Write-Host "OK: runbook history contains StartOfDay" -ForegroundColor Green
$auditResponse = Invoke-LocalApi "GET" "/audit/events?limit=200"
$audit = @(Get-ItemsFromResponse $auditResponse)
Assert-True (($audit | Where-Object { $_.eventType -eq "RunbookStarted" }).Count -gt 0) "RunbookStarted audit exists"
Assert-True (($audit | Where-Object { $_.eventType -in @("RunbookCompleted", "RunbookWaitingForOperator", "RunbookFailed") }).Count -gt 0) "runbook completion/wait/fail audit exists"

Write-Host "Schedules" -ForegroundColor Cyan
$schedules = Invoke-LocalApi "GET" "/ops/schedules"
Assert-True ($schedules.schedulerEnabled -eq $false) "local scheduler is disabled by default"

Write-Host "Runbook smoke completed." -ForegroundColor Green
