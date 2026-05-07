param(
    [string]$BaseUrl = "http://localhost:5050",
    [string]$OperatorId = "local-admin",
    [string]$Reason = "Local LMAX shadow reader skeleton smoke"
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
    foreach ($name in @("value", "Value", "items", "Items", "data", "parentOrders", "fills", "positions", "events", "auditEvents")) {
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

function Assert-NoSensitiveText {
    param([object]$Response, [string]$Context)
    $json = $Response | ConvertTo-Json -Depth 40
    foreach ($needle in @("password", "secret", "token", "authorization", "554=", "Logon", "NewOrderSingle", "OrderSent=True", "Connected=True")) {
        Assert-True ($json.IndexOf($needle, [StringComparison]::OrdinalIgnoreCase) -lt 0) "$Context does not contain $needle"
    }
}

Assert-LocalUrl $BaseUrl

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

Write-Step "Reader status"
$status = Invoke-LocalApi -Method "GET" -Endpoint "/lmax-shadow-reader/status"
Assert-True ($status.status -eq "Disabled") "reader status is Disabled by default"
Assert-True (-not [string]::IsNullOrWhiteSpace($status.blockedReason)) "status blocked reason is present"
Assert-True (-not [bool]$status.connected) "reader status did not connect"
Assert-True (-not [bool]$status.externalConnectionAttempted) "reader status did not attempt external connection"
Assert-True (-not [bool]$status.credentialsUsed) "reader status did not use credentials"
Assert-True (-not [bool]$status.ordersSubmitted) "reader status did not submit orders"
Assert-True (-not [bool]$status.persistedToTradingTables) "reader status did not persist to trading tables"
$statusChecks = @($status.safetyChecks)
Assert-True ($statusChecks.Count -ge 8) "status safety gates are present"
Assert-True (@($statusChecks | Where-Object { $_.gate -eq "Enabled" -and $_.status -eq "Failed" }).Count -gt 0) "Enabled failed gate is present"
Assert-True (@($statusChecks | Where-Object { $_.gate -eq "RuntimeGatewayRegistration" -and $_.status -eq "Passed" }).Count -gt 0) "runtime gateway registration gate is present"
Assert-NoSensitiveText -Response $status -Context "status response"
Write-Success "Reader is disabled and inert"

Write-Step "Blocked run"
$run = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-shadow-reader/run" -Body @{ reason = $Reason; dryRun = $true }
Assert-True ($run.status -eq "Disabled" -or $run.status -eq "Blocked") "run is disabled or blocked"
Assert-True (-not [string]::IsNullOrWhiteSpace($run.blockedReason)) "run blocked reason is present"
Assert-True (-not [bool]$run.executed) "run was not executed"
Assert-True (-not [bool]$run.connected) "run did not connect"
Assert-True (-not [bool]$run.externalConnectionAttempted) "run did not attempt external connection"
Assert-True (-not [bool]$run.credentialsUsed) "run did not use credentials"
Assert-True (-not [bool]$run.ordersSubmitted) "run did not submit orders"
Assert-True (-not [bool]$run.persistedToTradingTables) "run did not persist to trading tables"
$blockedGates = @($run.safetyChecks | Where-Object { -not [bool]$_.passed } | ForEach-Object { $_.gate })
Assert-True ($blockedGates -contains "Enabled") "Enabled gate blocks by default"
Assert-True ($blockedGates -contains "AllowExternalConnections") "External connection gate blocks by default"
Assert-True ($blockedGates -contains "ImplementationMode") "implementation mode gate blocks by default"
Assert-NoSensitiveText -Response $run -Context "run response"
Write-Success "Reader run blocked safely"

Write-Step "Audit"
$audit = Invoke-LocalApi -Method "GET" -Endpoint "/audit/events?limit=100"
$auditEvents = Get-ItemsFromResponse -Response $audit
$readerAudit = @($auditEvents | Where-Object {
    $_.eventType -eq "LmaxShadowReaderRunBlocked" -and
    $_.reason -eq $Reason
})
Assert-True ($readerAudit.Count -gt 0) "blocked reader run audit event exists"
Assert-NoSensitiveText -Response $readerAudit[0] -Context "reader audit event"
Write-Success "Blocked reader run was audited"

Write-Step "Mutation guard after reader run"
$afterOrders = Get-CountSafely -Endpoint "/orders"
$afterFills = Get-CountSafely -Endpoint "/fills"
$afterPositions = Get-CountSafely -Endpoint "/positions/internal"
if ($beforeOrders.available -and $afterOrders.available) { Assert-True ($beforeOrders.count -eq $afterOrders.count) "order count unchanged" }
if ($beforeFills.available -and $afterFills.available) { Assert-True ($beforeFills.count -eq $afterFills.count) "fill count unchanged" }
if ($beforePositions.available -and $afterPositions.available) { Assert-True ($beforePositions.count -eq $afterPositions.count) "position count unchanged" }
Write-Success "Available internal counts are unchanged"

Write-Step "Summary"
Write-Host "Status=$($status.status)"
Write-Host "RunStatus=$($run.status)"
Write-Host "BlockedReason=$($run.blockedReason)"
Write-Host "No external URL, credential, live FIX, LMAX runtime gateway, or order submission path was used."
