param(
    [switch]$SkipBuild,
    [switch]$SkipFrontend,
    [switch]$SkipSmokes,
    [switch]$RequireApi,
    [string]$ApiBaseUrl = "http://localhost:5050",
    [string]$EvidenceFixtureGlob = "tests/fixtures/lmax-shadow/*.json",
    [string]$CapturedEvidenceFile
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet_home"
$env:NPM_CONFIG_UPDATE_NOTIFIER = "false"
$env:APPDATA = Join-Path $root ".appdata"
$env:LOCALAPPDATA = Join-Path $root ".localappdata"
$results = New-Object System.Collections.Generic.List[object]
$knownWarnings = New-Object System.Collections.Generic.List[string]

function Add-Result {
    param(
        [string]$Category,
        [string]$Check,
        [string]$Status,
        [string]$Detail
    )

    $results.Add([pscustomobject][ordered]@{
        category = $Category
        check = $Check
        status = $Status
        detail = $Detail
    }) | Out-Null
}

function Assert-LocalUrl {
    param([string]$Url)
    $uri = [Uri]$Url
    if ($uri.Scheme -notin @("http", "https")) {
        throw "API URL must use http/https."
    }
    if ($uri.Host -notin @("localhost", "127.0.0.1")) {
        throw "API URL must be localhost or 127.0.0.1. Refusing non-local URL."
    }
}

function Invoke-Step {
    param(
        [string]$Category,
        [string]$Check,
        [scriptblock]$Script
    )

    Write-Host ""
    Write-Host ("== {0}: {1} ==" -f $Category, $Check) -ForegroundColor Cyan
    try {
        $detail = & $Script
        if ([string]::IsNullOrWhiteSpace([string]$detail)) {
            $detail = "Completed."
        }
        Add-Result -Category $Category -Check $Check -Status "PASS" -Detail ([string]$detail)
        Write-Host ("PASS: {0}" -f $detail) -ForegroundColor Green
    } catch {
        Add-Result -Category $Category -Check $Check -Status "FAIL" -Detail $_.Exception.Message
        Write-Host ("FAIL: {0}" -f $_.Exception.Message) -ForegroundColor Red
    }
}

function Add-WarningResult {
    param([string]$Category, [string]$Check, [string]$Detail)
    Add-Result -Category $Category -Check $Check -Status "WARN" -Detail $Detail
    $knownWarnings.Add($Detail) | Out-Null
    Write-Host ("WARN: {0}" -f $Detail) -ForegroundColor Yellow
}

function Invoke-CommandLine {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$WorkingDirectory = $root
    )

    $resolvedFilePath = $FilePath
    $resolvedCommand = Get-Command $FilePath -ErrorAction SilentlyContinue
    if ($resolvedCommand -and $resolvedCommand.Source) {
        $resolvedFilePath = $resolvedCommand.Source
    }

    if ($FilePath -ieq "npm.cmd") {
        $commandText = "& '" + $resolvedFilePath + "' " + (($Arguments | ForEach-Object {
            $argument = [string]$_
            if ($argument -match '[\s'']') {
                "'" + ($argument -replace "'", "''") + "'"
            } else {
                $argument
            }
        }) -join " ")
        $FilePath = "powershell"
        $resolvedCommand = Get-Command $FilePath -ErrorAction SilentlyContinue
        $resolvedFilePath = if ($resolvedCommand -and $resolvedCommand.Source) { $resolvedCommand.Source } else { $FilePath }
        $Arguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $commandText)
    }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $resolvedFilePath
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

    if (($stdout -match "NU1903") -or ($stderr -match "NU1903")) {
        $warning = "Known NU1903 System.Security.Cryptography.Xml advisory warning observed."
        if (-not ($knownWarnings -contains $warning)) {
            $knownWarnings.Add($warning) | Out-Null
        }
    }
}

function Invoke-LocalApi {
    param([string]$Endpoint)
    Invoke-RestMethod -Method Get -Uri ("{0}{1}" -f $ApiBaseUrl, $Endpoint) -Headers @{ "X-Operator-Id" = "local-admin" }
}

Assert-LocalUrl $ApiBaseUrl

Write-Host "QQ.Production.Intraday Operational Readiness Gate #1" -ForegroundColor Cyan
Write-Host "Local-only safety gate. This script does not connect to LMAX, submit orders, or use credentials." -ForegroundColor Yellow

if ($SkipBuild.IsPresent) {
    Add-WarningResult -Category "Backend" -Check "Build/Test" -Detail "Skipped backend build/test by parameter."
} else {
    Invoke-Step -Category "Backend" -Check "Restore" -Script {
        Invoke-CommandLine -FilePath "dotnet" -Arguments @("restore", "QQ.Production.Intraday.sln", "--configfile", "NuGet.Config", "-m:1", "/p:RestoreUseStaticGraphEvaluation=false")
        "dotnet restore completed."
    }
    Invoke-Step -Category "Backend" -Check "Build" -Script {
        Invoke-CommandLine -FilePath "dotnet" -Arguments @("build", "QQ.Production.Intraday.sln", "--no-restore", "-m:1", "/p:BuildInParallel=false")
        "dotnet build completed."
    }
    Invoke-Step -Category "Backend" -Check "Test" -Script {
        Invoke-CommandLine -FilePath "dotnet" -Arguments @("test", "QQ.Production.Intraday.sln", "--no-build", "-m:1", "/p:BuildInParallel=false")
        "dotnet test completed."
    }
}

if ($SkipFrontend.IsPresent) {
    Add-WarningResult -Category "Frontend" -Check "Validation" -Detail "Skipped frontend validation by parameter."
} else {
    $uiDir = Join-Path $root "src\QQ.Production.Intraday.Ui"
    Invoke-Step -Category "Frontend" -Check "Typecheck" -Script {
        Invoke-CommandLine -FilePath "npm.cmd" -Arguments @("run", "typecheck") -WorkingDirectory $uiDir
        "npm.cmd run typecheck completed."
    }
    Invoke-Step -Category "Frontend" -Check "Build" -Script {
        Invoke-CommandLine -FilePath "npm.cmd" -Arguments @("run", "build") -WorkingDirectory $uiDir
        "npm.cmd run build completed."
    }
    Invoke-Step -Category "Frontend" -Check "Test" -Script {
        Invoke-CommandLine -FilePath "npm.cmd" -Arguments @("test") -WorkingDirectory $uiDir
        "npm.cmd test completed."
    }
}

Invoke-Step -Category "Evidence" -Check "Validate fixtures" -Script {
    $fixtures = @(Get-ChildItem -Path (Join-Path $root $EvidenceFixtureGlob) -File)
    if ($fixtures.Count -eq 0) {
        throw ("No evidence fixtures found for glob {0}" -f $EvidenceFixtureGlob)
    }
    foreach ($fixture in $fixtures) {
        Invoke-CommandLine -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\scripts\validate-lmax-lab-evidence-file.ps1", "-EvidenceFile", $fixture.FullName)
    }
    ("Validated {0} evidence fixture(s)." -f $fixtures.Count)
}

Invoke-Step -Category "LMAX Runtime" -Check "Phase 4 preflight boundary" -Script {
    Invoke-CommandLine -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\scripts\check-lmax-readonly-runtime-phase4-preflight.ps1", "-ApiBaseUrl", $ApiBaseUrl)
    "Phase 4 preflight boundary check completed."
}

Invoke-Step -Category "LMAX Runtime" -Check "Final no-socket release gate" -Script {
    Invoke-CommandLine -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\scripts\run-lmax-readonly-runtime-no-socket-release-gate.ps1", "-ApiBaseUrl", $ApiBaseUrl)
    "Final no-socket release gate completed."
}

$apiAvailable = $false
Invoke-Step -Category "API Safety" -Check "Health" -Script {
    try {
        $health = Invoke-LocalApi -Endpoint "/health"
    } catch {
        if ($RequireApi.IsPresent) {
            throw
        }
        Add-WarningResult -Category "API Safety" -Check "Health" -Detail ("API unavailable at {0}; API-dependent checks skipped." -f $ApiBaseUrl)
        return "API unavailable; recorded warning."
    }

    if ($health.executionGateway -ne "FakeLmaxGateway") {
        throw ("Expected FakeLmaxGateway, got {0}" -f $health.executionGateway)
    }
    if ([bool]$health.liveTradingEnabled) {
        throw "liveTradingEnabled must be false."
    }
    if ([bool]$health.externalConnectionsEnabled) {
        throw "externalConnectionsEnabled must be false."
    }
    $script:apiAvailable = $true
    "Health confirms FakeLmax-only runtime safety."
}

if ($apiAvailable -and -not $SkipSmokes.IsPresent) {
    Invoke-Step -Category "Smokes" -Check "LMAX shadow replay" -Script {
        Invoke-CommandLine -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\scripts\smoke-lmax-shadow-local.ps1", "-BaseUrl", $ApiBaseUrl)
        "smoke-lmax-shadow-local.ps1 completed."
    }
    Invoke-Step -Category "Smokes" -Check "LMAX shadow reader" -Script {
        Invoke-CommandLine -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\scripts\smoke-lmax-shadow-reader-local.ps1", "-BaseUrl", $ApiBaseUrl)
        "smoke-lmax-shadow-reader-local.ps1 completed."
    }
    Invoke-Step -Category "Smokes" -Check "LMAX evidence coverage" -Script {
        Invoke-CommandLine -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\scripts\smoke-lmax-evidence-coverage-local.ps1", "-BaseUrl", $ApiBaseUrl)
        "smoke-lmax-evidence-coverage-local.ps1 completed."
    }
    Invoke-Step -Category "Smokes" -Check "LMAX read-only runtime fake" -Script {
        Invoke-CommandLine -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\scripts\smoke-lmax-readonly-runtime-fake-local.ps1", "-BaseUrl", $ApiBaseUrl)
        "smoke-lmax-readonly-runtime-fake-local.ps1 completed."
    }
    Invoke-Step -Category "Smokes" -Check "LMAX external read-only preflight" -Script {
        Invoke-CommandLine -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\scripts\smoke-lmax-readonly-runtime-external-preflight-local.ps1", "-BaseUrl", $ApiBaseUrl)
        "smoke-lmax-readonly-runtime-external-preflight-local.ps1 completed."
    }
} elseif ($SkipSmokes.IsPresent) {
    Add-WarningResult -Category "Smokes" -Check "API-dependent smokes" -Detail "Skipped API-dependent smokes by parameter."
} elseif (-not $apiAvailable) {
    Add-WarningResult -Category "Smokes" -Check "API-dependent smokes" -Detail "Skipped API-dependent smokes because API is unavailable."
}

if (-not [string]::IsNullOrWhiteSpace($CapturedEvidenceFile)) {
    Invoke-Step -Category "Evidence" -Check "Validate captured evidence" -Script {
        $captured = Resolve-Path -LiteralPath $CapturedEvidenceFile
        Invoke-CommandLine -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\scripts\validate-lmax-lab-evidence-file.ps1", "-EvidenceFile", $captured.Path)
        "Captured evidence validates."
    }

    if ($apiAvailable) {
        Invoke-Step -Category "Evidence" -Check "Replay captured evidence" -Script {
            $captured = Resolve-Path -LiteralPath $CapturedEvidenceFile
            Invoke-CommandLine -FilePath "powershell" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ".\scripts\replay-lmax-lab-evidence-file.ps1", "-EvidenceFile", $captured.Path, "-BaseUrl", $ApiBaseUrl)
            "Captured evidence replay completed."
        }
    } else {
        Add-WarningResult -Category "Evidence" -Check "Replay captured evidence" -Detail "Skipped captured evidence replay because API is unavailable."
    }
}

$failed = @($results | Where-Object { $_.status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.status -eq "WARN" })
$decision = if ($failed.Count -gt 0) {
    "FAIL"
} elseif ($warnings.Count -gt 0 -or $knownWarnings.Count -gt 0) {
    "PASS WITH KNOWN WARNINGS"
} else {
    "PASS"
}

Write-Host ""
Write-Host "== Readiness Summary ==" -ForegroundColor Cyan
$results | Format-Table category, check, status, detail -AutoSize
Write-Host ("FinalDecision: {0}" -f $decision) -ForegroundColor $(if ($decision -eq "FAIL") { "Red" } elseif ($decision -eq "PASS WITH KNOWN WARNINGS") { "Yellow" } else { "Green" })

$reportDirectory = Join-Path $root "artifacts\readiness"
New-Item -ItemType Directory -Force -Path $reportDirectory | Out-Null
$timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss")
$reportPath = Join-Path $reportDirectory ("readiness-report-{0}.json" -f $timestamp)
$uniqueKnownWarnings = @()
foreach ($warning in $knownWarnings.ToArray()) {
    if ($uniqueKnownWarnings -notcontains $warning) {
        $uniqueKnownWarnings += $warning
    }
}
$resultArray = @()
foreach ($result in $results) {
    $resultArray += $result
}
$report = New-Object PSObject
$report | Add-Member -MemberType NoteProperty -Name "timestampUtc" -Value ((Get-Date).ToUniversalTime().ToString("O"))
$report | Add-Member -MemberType NoteProperty -Name "apiBaseUrl" -Value $ApiBaseUrl
$report | Add-Member -MemberType NoteProperty -Name "finalDecision" -Value $decision
$report | Add-Member -MemberType NoteProperty -Name "knownWarnings" -Value $uniqueKnownWarnings
$report | Add-Member -MemberType NoteProperty -Name "results" -Value $resultArray
$report | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Host ("Report: {0}" -f $reportPath)

if ($decision -eq "FAIL") {
    exit 1
}
