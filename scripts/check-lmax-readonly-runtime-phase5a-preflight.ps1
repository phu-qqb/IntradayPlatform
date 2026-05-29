param(
    [string]$ApiBaseUrl = "http://localhost:5050",
    [switch]$RequireApi,
    [switch]$SkipNoSocketReleaseGate
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

function Assert-LocalUrl {
    param([string]$Url)
    $uri = [Uri]$Url
    if ($uri.Scheme -notin @("http", "https") -or $uri.Host -notin @("localhost", "127.0.0.1")) {
        throw "API URL must be local only. Refusing $Url"
    }
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

Assert-LocalUrl $ApiBaseUrl

Write-Host "LMAX Read-Only Runtime Phase 5A Preflight" -ForegroundColor Cyan
Write-Host "Local-only planning gate. No LMAX connection, socket, credential read, order submission, scheduler, shadow replay submit, or trading-state mutation." -ForegroundColor Yellow

Invoke-Check -Category "Documentation" -Check "Phase 5A preflight docs" -Script {
    $docPath = Join-Path $root "docs\LMAX_READONLY_RUNTIME_FIRST_TRANSPORT_PREFLIGHT.md"
    $checklistPath = Join-Path $root "docs\LMAX_READONLY_RUNTIME_PHASE5A_CHECKLIST.md"
    if (-not (Test-Path -LiteralPath $docPath)) { throw "Missing $docPath" }
    if (-not (Test-Path -LiteralPath $checklistPath)) { throw "Missing $checklistPath" }
    $docText = Get-Content -LiteralPath $docPath -Raw
    foreach ($required in @("Phase 5A does not implement a socket", "Kill / Rollback Plan", "Abort Conditions", "Future Phase 5B Scope", "CredentialProfileName remains a label only")) {
        if ($docText.IndexOf($required, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw ("Phase 5A preflight doc is missing '{0}'." -f $required)
        }
    }
    "Phase 5A preflight and checklist docs exist."
}

Invoke-Check -Category "Release gate" -Check "Phase 4P no-socket release gate available" -Script {
    $gatePath = Join-Path $root "scripts\run-lmax-readonly-runtime-no-socket-release-gate.ps1"
    if (-not (Test-Path -LiteralPath $gatePath)) { throw "Missing $gatePath" }
    $phase5dPrototype = Join-Path $root "src\QQ.Production.Intraday.Infrastructure.Lmax\LmaxReadOnlySocketPrototype.cs"
    if ((Test-Path -LiteralPath $phase5dPrototype) -and (Select-String -Path $phase5dPrototype -Pattern "Phase5DManualScriptOnly" -SimpleMatch -Quiet)) {
        return "No-socket release gate exists; Phase 5D has superseded no-socket semantics with an isolated manual-only socket prototype."
    }
    if ($SkipNoSocketReleaseGate.IsPresent) {
        return "No-socket release gate exists; execution skipped by parameter."
    }
    Invoke-CommandLine -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\scripts\run-lmax-readonly-runtime-no-socket-release-gate.ps1", "-ApiBaseUrl", $ApiBaseUrl)
    "No-socket release gate completed."
}

Invoke-Check -Category "Configuration" -Check "Runtime defaults remain disabled" -Script {
    $settingsPath = Join-Path $root "src\QQ.Production.Intraday.Api\appsettings.json"
    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    if ([bool]$settings.LmaxReadOnlyRuntime.Enabled) { throw "LmaxReadOnlyRuntime:Enabled must default to false." }
    if ($settings.LmaxReadOnlyRuntime.ImplementationMode -ne "DesignOnly") { throw "ImplementationMode must default to DesignOnly." }
    if ([bool]$settings.LmaxReadOnlyRuntime.AllowExternalConnections) { throw "AllowExternalConnections must default to false." }
    if ([bool]$settings.LmaxReadOnlyRuntime.AllowCredentialUse) { throw "AllowCredentialUse must default to false." }
    if ([bool]$settings.LmaxReadOnlyRuntime.AllowOrderSubmission) { throw "AllowOrderSubmission must default to false." }
    if ([bool]$settings.LmaxReadOnlyRuntime.SchedulerEnabled) { throw "SchedulerEnabled must default to false." }
    if ([bool]$settings.LmaxReadOnlyRuntime.SubmitToShadowReplay) { throw "SubmitToShadowReplay must default to false." }
    "Default appsettings remain disabled/design-only."
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
    "Runtime Phase 4/5A files contain no socket/network/order/lab implementation surface."
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
        Invoke-Warning -Category "API" -Check "Health" -Detail ("API unavailable at {0}; API-dependent Phase 5A checks skipped." -f $ApiBaseUrl)
        return "API unavailable; warning recorded."
    }
    if ($health.executionGateway -ne "FakeLmaxGateway") { throw ("Expected FakeLmaxGateway, got {0}" -f $health.executionGateway) }
    if ([bool]$health.liveTradingEnabled) { throw "liveTradingEnabled must be false." }
    if ([bool]$health.externalConnectionsEnabled) { throw "externalConnectionsEnabled must be false." }
    $script:apiAvailable = $true
    "API health confirms FakeLmaxGateway and disabled live/external flags."
}

if ($apiAvailable) {
    Invoke-Check -Category "Smokes" -Check "External preflight smoke" -Script {
        Invoke-CommandLine -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\scripts\smoke-lmax-readonly-runtime-external-preflight-local.ps1", "-BaseUrl", $ApiBaseUrl)
        "External preflight smoke completed."
    }
} else {
    Invoke-Warning -Category "Smokes" -Check "External preflight smoke" -Detail "Skipped external preflight smoke because API is unavailable."
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
Write-Host "== Phase 5A Preflight Summary ==" -ForegroundColor Cyan
$results | Format-Table category, check, status, detail -AutoSize
Write-Host ("FinalDecision: {0}" -f $decision) -ForegroundColor $(if ($decision -eq "FAIL") { "Red" } elseif ($decision -eq "PASS WITH KNOWN WARNINGS") { "Yellow" } else { "Green" })

$reportDirectory = Join-Path $root "artifacts\readiness"
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
$timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss")
$reportPath = Join-Path $reportDirectory ("lmax-readonly-phase5a-preflight-{0}.json" -f $timestamp)
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
