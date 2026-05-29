param(
    [string]$BaseUrl = "http://localhost:5050",
    [string]$OperatorId = "local-admin",
    [string]$Reason = "Local LMAX read-only runtime fake fixture smoke",
    [switch]$ExpectFakeEnabled,
    [switch]$ExpectFakeTransportPreviewEnabled
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
        [object]$Body = $null,
        [switch]$AllowFailure
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
        if ($AllowFailure) {
            return $_
        }
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
    foreach ($name in @("value", "Value", "items", "Items", "data", "parentOrders", "fills", "positions")) {
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
    foreach ($needle in @("password", "secret", "token", "authorization", "554=", "NewOrderSingle", "OrderSent=True", "Connected=True", "account-api", "fix-order", "fix-market")) {
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

Write-Step "Read-only runtime status"
$status = Invoke-LocalApi -Method "GET" -Endpoint "/lmax-readonly-runtime/status"
Assert-True (-not [bool]$status.allowExternalConnections) "read-only runtime does not allow external connections"
Assert-True (-not [bool]$status.allowCredentialUse) "read-only runtime does not allow credential use"
Assert-True (-not [bool]$status.allowOrderSubmission) "read-only runtime does not allow order submission"
Assert-True (-not [bool]$status.persistToTradingTables) "read-only runtime does not persist to trading tables"
Assert-True (-not [bool]$status.schedulerEnabled) "read-only runtime scheduler is disabled"
Assert-NoSensitiveText -Response $status -Context "status response"
Write-Success ("Status={0} ImplementationMode={1}" -f $status.status, $status.implementationMode)

if ($ExpectFakeEnabled -and (($status.implementationMode -ne "FakeInMemory") -or (-not [bool]$status.enabled))) {
    throw ("API is not running with fake-enabled read-only runtime config. Start it with: powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-api-fake-readonly-runtime-preview.ps1. Current Status={0}; ImplementationMode={1}; Enabled={2}" -f $status.status, $status.implementationMode, $status.enabled)
}

if ($ExpectFakeTransportPreviewEnabled -and -not $ExpectFakeEnabled) {
    throw "Use -ExpectFakeEnabled together with -ExpectFakeTransportPreviewEnabled. Start API with: powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-api-fake-readonly-runtime-preview.ps1"
}

Write-Step "Run requires reason"
$badRun = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-readonly-runtime/run" -Body @{ reason = "" } -AllowFailure
Assert-True ($badRun.Exception.Response -ne $null) "missing reason returned an HTTP error"
Assert-True ([int]$badRun.Exception.Response.StatusCode -eq 400) "missing reason returned HTTP 400"
Write-Success "Reason is required"

Write-Step "Fixture run"
$run = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-readonly-runtime/run" -Body @{
    reason = $Reason
    fixtureFileName = "lmax-mixed-readonly-evidence-v1.json"
    dryRun = $true
}

if ($ExpectFakeEnabled) {
    if ($run.status -eq "Disabled" -or $run.status -eq "Blocked") {
        throw ("API is not running with fake-enabled read-only runtime config. Start it with: powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-api-fake-readonly-runtime-preview.ps1. Current run status={0}; blockedReason={1}" -f $run.status, $run.blockedReason)
    }

    Assert-True ($run.status -eq "Completed") "fake fixture run completed when explicitly enabled"
    Assert-True ($run.runMode -eq "FakeInMemoryFixtureOnly") "run mode is fake fixture only"
    Assert-True ($run.evidenceMode -eq "MixedReadOnly") "evidence mode is MixedReadOnly"
    Assert-True ([int]$run.executionReportCount -eq 0) "execution report count matches mixed read-only fixture"
    Assert-True ([int]$run.orderStatusCount -eq 1) "order status count matches mixed read-only fixture"
    Assert-True ([int]$run.tradeCaptureReportCount -eq 1) "trade capture count matches mixed read-only fixture"
    Assert-True ([int]$run.protocolRejectCount -eq 0) "protocol reject count matches mixed read-only fixture"
    Assert-True ([int]$run.marketDataSnapshotCount -eq 1) "market data count matches mixed read-only fixture"
    Assert-True ([int]$run.inputEventCount -eq 3) "event count matches mixed read-only fixture"
    Assert-True ([int]$run.validationErrorCount -eq 0) "validation errors are zero"
    Assert-True (-not [bool]$run.evidencePreview.submittedToShadowReplay) "shadow replay submit remains disabled"
} else {
    Assert-True ($run.status -eq "Disabled" -or $run.status -eq "Blocked") "run is disabled or blocked by default"
    Assert-True (-not [string]::IsNullOrWhiteSpace($run.blockedReason)) "blocked reason is present"
}

Assert-NoSensitiveText -Response $run -Context "run response"
Write-Success ("RunStatus={0} EvidenceMode={1}" -f $run.status, $run.evidenceMode)

if ($ExpectFakeTransportPreviewEnabled) {
    Write-Step "Fake transport preview"
    $preview = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-readonly-runtime/fake-transport-preview" -Body @{
        reason = $Reason
        scenario = "MixedReadOnly"
        maxEvents = 20
        dryRun = $true
        submitToShadowReplay = $false
    }

    if ($preview.status -eq "Disabled" -or $preview.status -eq "Blocked") {
        throw ("API is not running with fake transport preview enabled. Start it with: powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-api-fake-readonly-runtime-preview.ps1. Current preview status={0}; blockedReason={1}" -f $preview.status, $preview.blockedReason)
    }

    Assert-True ($preview.status -eq "Completed") "fake transport preview completed when explicitly enabled"
    Assert-True ($preview.runMode -eq "FakeTransportPreview") "run mode is fake transport preview"
    Assert-True ($preview.scenario -eq "MixedReadOnly") "scenario is MixedReadOnly"
    Assert-True ($preview.evidenceMode -eq "MixedReadOnly") "evidence mode is MixedReadOnly"
    Assert-True ($preview.source -eq "RuntimeFakeTransport") "source is RuntimeFakeTransport"
    Assert-True ($preview.captureMode -eq "FakeRuntimePreview") "capture mode is FakeRuntimePreview"
    Assert-True ([int]$preview.marketDataSnapshotCount -eq 1) "fake preview market data count matches"
    Assert-True ([int]$preview.tradeCaptureReportCount -eq 1) "fake preview trade capture count matches"
    Assert-True ([int]$preview.orderStatusReportCount -eq 1) "fake preview order status count matches"
    Assert-True ([int]$preview.protocolRejectCount -eq 1) "fake preview protocol reject count matches"
    Assert-True ([int]$preview.totalEventCount -eq 4) "fake preview total event count matches"
    Assert-True ([int]$preview.validationErrorCount -eq 0) "fake preview validation errors are zero"
    Assert-True ([bool]$preview.noSensitiveContent) "fake preview has no sensitive content"
    Assert-True (-not [bool]$preview.submitToShadowReplay) "fake preview does not submit to shadow replay"
    Assert-NoSensitiveText -Response $preview -Context "fake transport preview response"
    Write-Success ("FakeTransportPreviewStatus={0} EvidenceMode={1}" -f $preview.status, $preview.evidenceMode)
}

Write-Step "Fixture selector rejects traversal"
$badFixture = Invoke-LocalApi -Method "POST" -Endpoint "/lmax-readonly-runtime/run" -Body @{
    reason = "Bad fixture selector smoke"
    fixtureFileName = "../lmax-mixed-readonly-evidence-v1.json"
} -AllowFailure
Assert-True ($badFixture.Exception.Response -ne $null) "bad fixture selector returned an HTTP error"
Assert-True ([int]$badFixture.Exception.Response.StatusCode -eq 400) "bad fixture selector returned HTTP 400"
Write-Success "Fixture selector rejects traversal"

Write-Step "Mutation guard after runtime fake run"
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
Write-Host "No external URL, credential, live FIX, real LMAX gateway, scheduler, or order submission path was used."
