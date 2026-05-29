param(
    [string]$ApiBaseUrl = "http://localhost:5050",
    [string]$EvidenceFixtureGlob = "tests/fixtures/lmax-shadow/*.json",
    [switch]$RequireApi
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path $PSScriptRoot -Parent
$results = New-Object System.Collections.Generic.List[object]

function Add-Result {
    param([string]$Category, [string]$Check, [string]$Status, [string]$Detail)
    $results.Add([pscustomobject][ordered]@{
        category = $Category
        check = $Check
        status = $Status
        detail = $Detail
    }) | Out-Null

    $color = if ($Status -eq "PASS") { "Green" } elseif ($Status -eq "WARN") { "Yellow" } else { "Red" }
    Write-Host ("{0}: {1} / {2} - {3}" -f $Status, $Category, $Check, $Detail) -ForegroundColor $color
}

function Assert-LocalUrl {
    param([string]$Url)
    $uri = [Uri]$Url
    if ($uri.Scheme -notin @("http", "https") -or $uri.Host -notin @("localhost", "127.0.0.1")) {
        throw "API URL must be local only. Refusing $Url"
    }
}

function Invoke-Check {
    param([string]$Category, [string]$Check, [scriptblock]$Script)
    Write-Host ""
    Write-Host ("== {0}: {1} ==" -f $Category, $Check) -ForegroundColor Cyan
    try {
        $detail = & $Script
        if ([string]::IsNullOrWhiteSpace([string]$detail)) { $detail = "Completed." }
        Add-Result -Category $Category -Check $Check -Status "PASS" -Detail ([string]$detail)
    } catch {
        Add-Result -Category $Category -Check $Check -Status "FAIL" -Detail $_.Exception.Message
    }
}

function Invoke-Warning {
    param([string]$Category, [string]$Check, [string]$Detail)
    Add-Result -Category $Category -Check $Check -Status "WARN" -Detail $Detail
}

function Invoke-CommandLine {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$WorkingDirectory = $root
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $FilePath
    $psi.WorkingDirectory = $WorkingDirectory
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.Arguments = ($Arguments | ForEach-Object {
        $argument = [string]$_
        if ($argument -match '[\s"]') {
            '"' + ($argument -replace '"', '\"') + '"'
        } else {
            $argument
        }
    }) -join " "

    $process = [System.Diagnostics.Process]::Start($psi)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    if ($stdout) { Write-Host $stdout.TrimEnd() }
    if ($stderr) { Write-Host $stderr.TrimEnd() }
    if ($process.ExitCode -ne 0) {
        throw ("Command failed with exit code {0}: {1} {2}" -f $process.ExitCode, $FilePath, ($Arguments -join " "))
    }
}

function Test-NoText {
    param([string[]]$Paths, [string[]]$Patterns, [string]$Description)
    foreach ($path in $Paths) {
        if (-not (Test-Path -LiteralPath $path)) { throw "Missing file $path" }
        $text = Get-Content -LiteralPath $path -Raw
        foreach ($pattern in $Patterns) {
            if ($text.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw ("Forbidden {0} pattern '{1}' found in {2}" -f $Description, $pattern, $path)
            }
        }
    }
}

function Invoke-LocalApiGet {
    param([string]$Endpoint)
    Invoke-RestMethod -Method Get -Uri ("{0}{1}" -f $ApiBaseUrl, $Endpoint) -TimeoutSec 4 -Headers @{ "X-Operator-Id" = "local-admin" }
}

function Invoke-LocalApiPost {
    param([string]$Endpoint, [object]$Body)
    $json = $Body | ConvertTo-Json -Depth 20
    Invoke-RestMethod -Method Post -Uri ("{0}{1}" -f $ApiBaseUrl, $Endpoint) -TimeoutSec 8 -Headers @{ "X-Operator-Id" = "local-admin" } -ContentType "application/json" -Body $json
}

Assert-LocalUrl $ApiBaseUrl

Write-Host "LMAX Read-Only Runtime Final No-Socket Release Gate" -ForegroundColor Cyan
Write-Host "Local-only gate. No LMAX connection, socket, credential read, order submission, scheduler, shadow replay submit, or trading-state mutation." -ForegroundColor Yellow

Invoke-Check -Category "Documentation" -Check "No-socket release gate document" -Script {
    $docPath = Join-Path $root "docs\LMAX_READONLY_RUNTIME_NO_SOCKET_RELEASE_GATE.md"
    if (-not (Test-Path -LiteralPath $docPath)) { throw "Missing $docPath" }
    $text = Get-Content -LiteralPath $docPath -Raw
    foreach ($required in @("Final No-Socket Release Gate", "Phase 4A", "Phase 4O", "No socket implementation exists", "PASS WITH KNOWN WARNINGS")) {
        if ($text.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Release gate document is missing '{0}'." -f $required)
        }
    }
    "Release gate document is present and names the Phase 4A-4O boundary."
}

Invoke-Check -Category "Evidence" -Check "Validate fixtures" -Script {
    $fixtures = @(Get-ChildItem -Path (Join-Path $root $EvidenceFixtureGlob) -File)
    if ($fixtures.Count -eq 0) { throw "No evidence fixtures found for $EvidenceFixtureGlob" }
    foreach ($fixture in $fixtures) {
        Invoke-CommandLine -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\scripts\validate-lmax-lab-evidence-file.ps1", "-EvidenceFile", $fixture.FullName)
    }
    ("Validated {0} evidence fixture(s)." -f $fixtures.Count)
}

Invoke-Check -Category "Phase 4" -Check "Preflight boundary" -Script {
    Invoke-CommandLine -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\scripts\check-lmax-readonly-runtime-phase4-preflight.ps1", "-ApiBaseUrl", $ApiBaseUrl)
    "Phase 4 preflight completed."
}

Invoke-Check -Category "Source" -Check "No forbidden runtime implementation surface" -Script {
    $runtimeFiles = @(
        "LmaxReadOnlyExternalSessionContracts.cs",
        "LmaxReadOnlyExternalSessionSkeleton.cs",
        "LmaxReadOnlyGuardedTransport.cs",
        "LmaxReadOnlyExternalSessionOptions.cs",
        "LmaxReadOnlyCredentialProfile.cs",
        "LmaxReadOnlyVenueProfile.cs",
        "LmaxReadOnlyExternalSessionRunIntent.cs",
        "LmaxReadOnlyExternalSessionDryRunReport.cs",
        "LmaxReadOnlyExternalSessionSignoff.cs",
        "LmaxReadOnlyExternalSessionPreActivationAudit.cs",
        "LmaxReadOnlyExternalSessionReadinessSnapshot.cs"
    ) | ForEach-Object { Join-Path $root ("src\QQ.Production.Intraday.Infrastructure.Lmax\{0}" -f $_) }
    Test-NoText -Paths $runtimeFiles -Patterns @("TcpClient", "Socket(", "SslStream", "QuickFIX", "ClientWebSocket", "NetworkStream", "NewOrderSingle", "OrderCancelRequest", "OrderCancelReplaceRequest", "SubmitOrder", "Lmax.ConnectivityLab") -Description "network/order"
    "Runtime Phase 4 files contain no socket/network/order/lab implementation surface."
}

Invoke-Check -Category "Source" -Check "API/Worker FakeLmaxGateway only" -Script {
    $apiProgram = Get-Content -LiteralPath (Join-Path $root "src\QQ.Production.Intraday.Api\Program.cs") -Raw
    $workerProgram = Get-Content -LiteralPath (Join-Path $root "src\QQ.Production.Intraday.Worker\Program.cs") -Raw
    if ($apiProgram -notmatch "AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>") { throw "API FakeLmaxGateway registration not found." }
    if ($workerProgram -notmatch "AddSingleton<IVenueExecutionGateway, FakeLmaxGateway>") { throw "Worker FakeLmaxGateway registration not found." }
    if ($apiProgram -match "LmaxVenueGateway" -or $workerProgram -match "LmaxVenueGateway") { throw "Real LMAX gateway reference found in API/Worker." }
    if ($apiProgram -match "AddHostedService.*Lmax" -or $workerProgram -match "AddHostedService.*Lmax") { throw "LMAX hosted service registration found." }
    "API/Worker execution gateway registration remains FakeLmaxGateway only."
}

Invoke-Check -Category "Source" -Check "Generated evidence not tracked" -Script {
    $status = git -C $root status --short -- artifacts/lmax-lab/evidence 2>$null
    if ($status) { throw "Generated lab evidence appears in git status: $status" }
    "No generated lab evidence is tracked or dirty."
}

$apiAvailable = $false
Invoke-Check -Category "API" -Check "Health" -Script {
    try {
        $health = Invoke-LocalApiGet -Endpoint "/health"
    } catch {
        if ($RequireApi.IsPresent) { throw }
        Invoke-Warning -Category "API" -Check "Health" -Detail ("API unavailable at {0}; API-dependent no-socket checks skipped." -f $ApiBaseUrl)
        return "API unavailable; warning recorded."
    }
    if ($health.executionGateway -ne "FakeLmaxGateway") { throw ("Expected FakeLmaxGateway, got {0}" -f $health.executionGateway) }
    if ([bool]$health.liveTradingEnabled) { throw "liveTradingEnabled must be false." }
    if ([bool]$health.externalConnectionsEnabled) { throw "externalConnectionsEnabled must be false." }
    $script:apiAvailable = $true
    "API health confirms FakeLmaxGateway and disabled live/external flags."
}

if ($apiAvailable) {
    Invoke-Check -Category "API" -Check "Readiness snapshot endpoint" -Script {
        $body = @{
            reason = "Final no-socket release gate readiness snapshot"
            environmentName = "Demo"
            venueProfileName = "DemoLondon"
            credentialProfileName = "LmaxDemoReadOnlyProfile"
            runMode = "FutureExternalReadOnlyManual"
            dryRun = $true
            submitToShadowReplay = $false
            allowExternalConnections = $false
            allowCredentialUse = $false
            allowOrderSubmission = $false
            schedulerEnabled = $false
            persistToTradingTables = $false
        }
        $snapshot = Invoke-LocalApiPost -Endpoint "/lmax-readonly-runtime/external-run-intent/readiness-snapshot" -Body $body
        if ($snapshot.finalDecision -notin @("NotExecutable", "Blocked", "ValidateOnly")) { throw ("Unexpected finalDecision {0}" -f $snapshot.finalDecision) }
        if ([bool]$snapshot.canStartSession -or [bool]$snapshot.sessionStarted -or [bool]$snapshot.externalConnectionAttempted -or [bool]$snapshot.credentialReadAttempted -or [bool]$snapshot.shadowReplaySubmitAttempted -or [bool]$snapshot.tradingMutationAttempted) {
            throw "Readiness snapshot reported an unsafe attempted action."
        }
        $json = $snapshot | ConvertTo-Json -Depth 40
        foreach ($required in @("Phase4ExternalRunImplementationNotStarted", "CredentialResolverDisabled", "GuardedTransportImplementationDisabled")) {
            if ($json.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) { throw ("Readiness snapshot missing '{0}'." -f $required) }
        }
        "Readiness snapshot is non-executable and reports no attempted session/connection/credential/replay/mutation."
    }

    Invoke-Check -Category "Smokes" -Check "External preflight smoke" -Script {
        Invoke-CommandLine -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\scripts\smoke-lmax-readonly-runtime-external-preflight-local.ps1", "-BaseUrl", $ApiBaseUrl)
        "External preflight smoke completed."
    }

    Invoke-Check -Category "Smokes" -Check "Fake runtime smoke" -Script {
        Invoke-CommandLine -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\scripts\smoke-lmax-readonly-runtime-fake-local.ps1", "-BaseUrl", $ApiBaseUrl)
        "Fake runtime smoke completed."
    }
} else {
    Invoke-Warning -Category "API" -Check "Readiness snapshot endpoint" -Detail "Skipped readiness snapshot endpoint check because API is unavailable."
    Invoke-Warning -Category "Smokes" -Check "External preflight smoke" -Detail "Skipped external preflight smoke because API is unavailable."
    Invoke-Warning -Category "Smokes" -Check "Fake runtime smoke" -Detail "Skipped fake runtime smoke because API is unavailable."
}

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failed.Count -gt 0) {
    "FAIL"
} elseif ($warnings.Count -gt 0) {
    "PASS WITH KNOWN WARNINGS"
} else {
    "PASS"
}

Write-Host ""
Write-Host "== No-Socket Release Gate Summary ==" -ForegroundColor Cyan
$results | Format-Table category, check, status, detail -AutoSize
Write-Host ("FinalDecision: {0}" -f $decision) -ForegroundColor $(if ($decision -eq "FAIL") { "Red" } elseif ($decision -eq "PASS WITH KNOWN WARNINGS") { "Yellow" } else { "Green" })

$reportDirectory = Join-Path $root "artifacts\readiness"
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
$timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss")
$reportPath = Join-Path $reportDirectory ("lmax-readonly-no-socket-release-gate-{0}.json" -f $timestamp)
$resultArray = @()
foreach ($result in $results) {
    $resultArray += $result
}
$report = New-Object PSObject
$report | Add-Member -MemberType NoteProperty -Name "timestampUtc" -Value ((Get-Date).ToUniversalTime().ToString("O"))
$report | Add-Member -MemberType NoteProperty -Name "apiBaseUrl" -Value $ApiBaseUrl
$report | Add-Member -MemberType NoteProperty -Name "finalDecision" -Value $decision
$report | Add-Member -MemberType NoteProperty -Name "results" -Value $resultArray
$report | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Host ("Report: {0}" -f $reportPath)

if ($decision -eq "FAIL") {
    exit 1
}
