param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$auditRoot = Join-Path $Root "artifacts/readiness/capability-inventory"
$required = @(
    "capability-inventory-summary.md",
    "capability-inventory-matrix.json",
    "lmax-fix-capability-review.json",
    "pms-oms-ems-capability-review.json",
    "reconciliation-pnl-capability-review.json",
    "evidence-index.json",
    "build-test-evidence.json",
    "no-external-safety-audit.json"
)

$failures = New-Object System.Collections.Generic.List[string]

foreach ($file in $required) {
    $path = Join-Path $auditRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        $failures.Add("Missing required audit file: $file")
    }
}

if ($failures.Count -eq 0) {
    $matrixPath = Join-Path $auditRoot "capability-inventory-matrix.json"
    $matrix = Get-Content -LiteralPath $matrixPath -Raw | ConvertFrom-Json

    foreach ($component in $matrix.components) {
        if ($component.Classification -eq "ExecutableLiveCapable") {
            if (-not $component.EvidenceFiles -or $component.EvidenceFiles.Count -eq 0) {
                $failures.Add("ExecutableLiveCapable component lacks evidence: $($component.Component)")
            }

            if ($component.SafetyStatus -notin @("CouldSubmitOrders", "CouldTouchBroker", "CouldTouchLiveTrading", "UnknownRisk")) {
                $failures.Add("ExecutableLiveCapable component is not flagged as risk: $($component.Component)")
            }
        }
    }

    $qubes = $matrix.components | Where-Object { $_.Component -eq "Qubes input boundary" } | Select-Object -First 1
    if (-not $qubes) {
        $failures.Add("Missing Qubes input boundary component.")
    }
    else {
        if ($qubes.Classification -eq "ExecutableLiveCapable") {
            $failures.Add("Qubes input boundary must not be marked ExecutableLiveCapable.")
        }

        if ($qubes.SafetyStatus -in @("CouldSubmitOrders", "CouldTouchBroker", "CouldTouchLiveTrading")) {
            $failures.Add("Qubes input boundary must not be marked broker/live capable.")
        }

        if ($qubes.Notes.ZeroOnlyPmsApproved -ne $false) {
            $failures.Add("Qubes ZeroOnly must not be marked PMS-approved.")
        }
    }

    foreach ($name in @("PMS -> OMS", "OMS -> EMS routing", "EMS intraday algo")) {
        $component = $matrix.components | Where-Object { $_.Component -eq $name } | Select-Object -First 1
        if (-not $component) {
            $failures.Add("Missing component: $name")
            continue
        }

        if ($component.Classification -eq "ExecutableLiveCapable") {
            $failures.Add("$name is marked ExecutableLiveCapable without an explicit policy-approval gate.")
        }
    }

    $lmax = Get-Content -LiteralPath (Join-Path $auditRoot "lmax-fix-capability-review.json") -Raw | ConvertFrom-Json
    if ($lmax.orderSubmissionPossible -and -not $lmax.guarded) {
        $failures.Add("LMAX/FIX order submission possible but not marked guarded.")
    }

    $safety = Get-Content -LiteralPath (Join-Path $auditRoot "no-external-safety-audit.json") -Raw | ConvertFrom-Json
    foreach ($key in @("noLmaxCalls", "noPolygonMassiveCalls", "noSqlMutation", "noBrokerSubmission", "noOrdersCreated", "noFillsCreated", "noRoutesCreated", "noExecutableSchedulesCreated", "noTradingStateMutation", "noLivePathRun")) {
        if ($safety.confirmations.$key -ne $true) {
            $failures.Add("Safety confirmation failed: $key")
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host "CAPABILITY_INVENTORY_GATE_FAIL"
    $failures | ForEach-Object { Write-Host "- $_" }
    exit 1
}

Write-Host "CAPABILITY_INVENTORY_GATE_PASS"
exit 0
