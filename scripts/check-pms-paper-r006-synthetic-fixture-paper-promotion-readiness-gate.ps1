param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$root = [System.IO.Path]::GetFullPath($RepoRoot)
$pmsPaper = Join-Path $root "artifacts/readiness/pms-paper"
$executionSim = Join-Path $root "artifacts/readiness/execution-sim"
$failures = New-Object System.Collections.Generic.List[string]

function Add-Failure([string]$message) {
    $script:failures.Add($message) | Out-Null
}

function Read-JsonFile([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) {
        Add-Failure "Missing required file: $path"
        return $null
    }

    try {
        return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    }
    catch {
        Add-Failure "Invalid JSON: $path :: $($_.Exception.Message)"
        return $null
    }
}

function Assert-True($value, [string]$message) {
    if ($value -ne $true) {
        Add-Failure $message
    }
}

function Assert-False($value, [string]$message) {
    if ($value -ne $false) {
        Add-Failure $message
    }
}

function Has-Property($object, [string]$name) {
    return $null -ne $object -and $null -ne ($object.PSObject.Properties | Where-Object { $_.Name -eq $name })
}

function Inspect-Safety($object, [string]$path) {
    if ($null -eq $object) { return }

    foreach ($property in $object.PSObject.Properties) {
        $name = $property.Name
        $value = $property.Value

        if ($name -in @("ExecutionAllowed", "RouteGenerationAllowed", "BrokerSubmissionAllowed", "FixSessionAllowed", "LmaxLiveAllowed", "OrderGenerationAllowed", "FillGenerationAllowed") -and $value -eq $true) {
            Add-Failure "$path marks $name as true."
        }

        if ($name -in @("NoBrokerRoute", "NoFixMessage", "NotSubmitted", "NotAnOrder", "NoLmaxCall", "NoFixSession", "NoOrdersCreated", "NoFillsCreated", "NoRoutesCreated", "NoSchedulesCreated", "NoBrokerSubmission", "NoLiveTradingStateMutation", "NoQubesExecutableRun", "NoExecAlgoGate") -and $value -eq $false) {
            Add-Failure "$path marks $name as false."
        }

        if ($name -eq "QubesZeroOnlyTreatedAsPmsApproved" -and $value -eq $true) {
            Add-Failure "$path marks Qubes ZeroOnly as PMS-approved."
        }

        if ($value -is [System.Management.Automation.PSCustomObject]) {
            Inspect-Safety $value "$path.$name"
        }
        elseif ($value -is [System.Array]) {
            for ($index = 0; $index -lt $value.Count; $index++) {
                if ($value[$index] -is [System.Management.Automation.PSCustomObject]) {
                    Inspect-Safety $value[$index] "$path.$name[$index]"
                }
            }
        }
    }
}

$required = @(
    "phase-pms-paper-r006-summary.md",
    "phase-pms-paper-r006-core-synthetic-fixture-reference.json",
    "phase-pms-paper-r006-pms-input-readiness.json",
    "phase-pms-paper-r006-pms-oms-paper-preview-readiness.json",
    "phase-pms-paper-r006-ems-fix-lmax-boundary-reference.json",
    "phase-pms-paper-r006-no-external-audit.json",
    "phase-pms-paper-r006-build-test-validator-evidence.json",
    "phase-pms-paper-r006-next-gate-plan.json"
)

foreach ($file in $required) {
    $path = Join-Path $pmsPaper $file
    if (-not (Test-Path -LiteralPath $path)) {
        Add-Failure "Missing required R006 file: $path"
    }
}

$fixtureRef = Read-JsonFile (Join-Path $pmsPaper "phase-pms-paper-r006-core-synthetic-fixture-reference.json")
$pmsInput = Read-JsonFile (Join-Path $pmsPaper "phase-pms-paper-r006-pms-input-readiness.json")
$preview = Read-JsonFile (Join-Path $pmsPaper "phase-pms-paper-r006-pms-oms-paper-preview-readiness.json")
$boundary = Read-JsonFile (Join-Path $pmsPaper "phase-pms-paper-r006-ems-fix-lmax-boundary-reference.json")
$audit = Read-JsonFile (Join-Path $pmsPaper "phase-pms-paper-r006-no-external-audit.json")
$build = Read-JsonFile (Join-Path $pmsPaper "phase-pms-paper-r006-build-test-validator-evidence.json")
$next = Read-JsonFile (Join-Path $pmsPaper "phase-pms-paper-r006-next-gate-plan.json")

if ($fixtureRef) {
    if ($fixtureRef.FixtureSha256 -ne "sha256:294987DE89DCA26A56FD70278FCA2B77D085BE08D7F460626CB362163C69B5A2") {
        Add-Failure "Fixture SHA256 mismatch."
    }
    if ($fixtureRef.MetadataSha256 -ne "sha256:EC1DF00AC12DB9FC5B25ADE8CE8CFF7D9976371638EF8037B4A90B336F2D1561") {
        Add-Failure "Metadata SHA256 mismatch."
    }
    Assert-True $fixtureRef.NotQubesEconomicOutput "Fixture reference must be NotQubesEconomicOutput."
    Assert-True $fixtureRef.PaperOnly "Fixture reference must be PaperOnly."
    Assert-True $fixtureRef.NonExecutable "Fixture reference must be NonExecutable."
    Assert-False $fixtureRef.CurrentQubesZeroOnlyPmsApproved "Current Qubes ZeroOnly must not be PMS-approved."
}

if ($pmsInput) {
    Assert-True $pmsInput.NotQubesEconomicOutput "PMS input must be NotQubesEconomicOutput."
    Assert-False $pmsInput.CurrentQubesZeroOnlyPmsApproved "PMS input must not approve Qubes ZeroOnly."
    if ($pmsInput.Status -notin @("ValidatedSyntheticFixtureForPaperReadiness", "CommandPlanOnly", "Blocked")) {
        Add-Failure "Unexpected PMS input readiness status: $($pmsInput.Status)"
    }
}

if ($preview) {
    Assert-True $preview.PaperOnlyNoExternal "PMS->OMS preview must be paper-only/no-external."
    Assert-True $preview.NotAnOrder "PMS->OMS preview must be NotAnOrder."
    Assert-True $preview.NotSubmitted "PMS->OMS preview must be NotSubmitted."
    Assert-True $preview.NoBrokerRoute "PMS->OMS preview must have NoBrokerRoute."
    Assert-True $preview.NoFixMessage "PMS->OMS preview must have NoFixMessage."
    Assert-False $preview.ExecutionAllowed "PMS->OMS preview must block execution."
}

if ($boundary) {
    Assert-True $boundary.OMS_EMS_PreviewOnly "OMS->EMS must remain preview-only."
    Assert-False $boundary.RouteGenerationAllowed "Route generation must be blocked."
    Assert-False $boundary.ExecutionAllowed "Execution must be blocked."
    Assert-False $boundary.BrokerSubmissionAllowed "Broker submission must be blocked."
    Assert-False $boundary.FixSessionAllowed "FIX session must be blocked."
    Assert-False $boundary.LmaxLiveAllowed "LMAX live must be blocked."
}

if ($audit) {
    foreach ($name in @("NoLmaxCall", "NoFixSession", "NoPolygonMassiveCall", "NoSqlMutation", "NoOrdersCreated", "NoFillsCreated", "NoRoutesCreated", "NoSchedulesCreated", "NoBrokerSubmission", "NoLiveTradingStateMutation", "NoQubesExecutableRun", "NoExecAlgoGate")) {
        if ((Has-Property $audit $name) -eq $false) {
            Add-Failure "No-external audit missing $name."
        }
        else {
            Assert-True $audit.$name "No-external audit $name must be true."
        }
    }
}

foreach ($jsonName in $required | Where-Object { $_.EndsWith(".json", [System.StringComparison]::OrdinalIgnoreCase) }) {
    Inspect-Safety (Read-JsonFile (Join-Path $pmsPaper $jsonName)) $jsonName
}

if (Test-Path -LiteralPath $executionSim) {
    $r006ExecutionSimArtifacts = Get-ChildItem -LiteralPath $executionSim -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "*pms-paper-r006*" -or $_.FullName -like "*pms-paper-r006*" }
    if ($r006ExecutionSimArtifacts.Count -gt 0) {
        Add-Failure "R006 artifacts were written under execution-sim: $($r006ExecutionSimArtifacts[0].FullName)"
    }
}

if ($build -and $build.Status -notin @("Passed", "PassedWithBuildSkipped", "Recorded")) {
    Add-Failure "Build/test/validator evidence status is not accepted: $($build.Status)"
}

if ($next -and $next.RecommendedNextGate -notlike "PMS-PAPER-R007*") {
    Add-Failure "Next gate must be PMS-PAPER-R007."
}

if ($failures.Count -gt 0) {
    Write-Host "PMS-PAPER-R006 validator failed:"
    foreach ($failure in $failures) {
        Write-Host " - $failure"
    }
    exit 1
}

Write-Host "PMS-PAPER-R006 validator passed: synthetic fixture referenced, PMS paper readiness command-planned, EMS/FIX/LMAX boundary blocked, no external/order/route/fill artifacts authorized."
exit 0
