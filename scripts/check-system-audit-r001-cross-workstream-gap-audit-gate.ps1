param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$auditRoot = Join-Path $Root "artifacts/readiness/system-audit"
$required = @(
    "phase-system-audit-r001-summary.md",
    "phase-system-audit-r001-source-workstream-inventory.json",
    "phase-system-audit-r001-evidence-chain-summary.json",
    "phase-system-audit-r001-system-gap-matrix.json",
    "phase-system-audit-r001-cross-workstream-dependency-map.json",
    "phase-system-audit-r001-critical-blocker-list.json",
    "phase-system-audit-r001-readiness-status-by-domain.json",
    "phase-system-audit-r001-ownership-map.json",
    "phase-system-audit-r001-risk-control-gap-assessment.json",
    "phase-system-audit-r001-marketdata-lmax-db-gap-assessment.json",
    "phase-system-audit-r001-qubes-pms-gap-assessment.json",
    "phase-system-audit-r001-ems-oms-gap-assessment.json",
    "phase-system-audit-r001-ledger-state-gap-assessment.json",
    "phase-system-audit-r001-ops-runbook-gap-assessment.json",
    "phase-system-audit-r001-test-evidence-gap-assessment.json",
    "phase-system-audit-r001-production-live-blockers.json",
    "phase-system-audit-r001-paper-ledger-blockers.json",
    "phase-system-audit-r001-sandbox-expansion-blockers.json",
    "phase-system-audit-r001-immediate-next-actions.json",
    "phase-system-audit-r001-roadmap-recommendation.md",
    "phase-system-audit-r001-roadmap-recommendation.json",
    "phase-system-audit-r001-no-external-audit.json",
    "phase-system-audit-r001-no-execution-audit.json",
    "phase-system-audit-r001-no-db-mutation-audit.json",
    "phase-system-audit-r001-no-order-fill-route-audit.json",
    "phase-system-audit-r001-forbidden-actions-audit.json",
    "phase-system-audit-r001-next-phase-recommendation.json",
    "phase-system-audit-r001-build-test-validator-evidence.json"
)

$failures = New-Object System.Collections.Generic.List[string]

foreach ($file in $required) {
    $path = Join-Path $auditRoot $file
    if (-not (Test-Path -LiteralPath $path)) {
        $failures.Add("Missing required artifact: $file")
    }
}

function Read-Json($name) {
    Get-Content -LiteralPath (Join-Path $auditRoot $name) -Raw | ConvertFrom-Json
}

if ($failures.Count -eq 0) {
    $forbidden = Read-Json "phase-system-audit-r001-forbidden-actions-audit.json"
    $flags = $forbidden.forbiddenActions
    foreach ($property in $flags.PSObject.Properties) {
        if ($property.Value -ne $false) {
            $failures.Add("Forbidden action flag is not false: $($property.Name)")
        }
    }

    $noExternal = Read-Json "phase-system-audit-r001-no-external-audit.json"
    foreach ($name in @("externalApiCalled", "polygonCalled", "massiveCalled", "lmaxCalled", "brokerActivated", "socketTlsFixOpened", "liveMarketDataRequested", "credentialValuesPrintedOrPersisted")) {
        if ($noExternal.$name -ne $false) {
            $failures.Add("No-external audit failed: $name")
        }
    }

    $noExec = Read-Json "phase-system-audit-r001-no-execution-audit.json"
    foreach ($name in @("pmsEmsOmsCycleRun", "manualNoExternalCommandRun", "qubesExecutableRun", "pythonWorkloadRun", "cppWorkloadRun", "cudaWorkloadRun", "backtestRun", "simulationRun", "tcaResultLinesCreated", "schedulerServicePollingBackgroundJobIntroduced", "executableScheduleCreated", "productionRouteEnabled")) {
        if ($noExec.$name -ne $false) {
            $failures.Add("No-execution audit failed: $name")
        }
    }

    $noDb = Read-Json "phase-system-audit-r001-no-db-mutation-audit.json"
    foreach ($name in @("dbMutationOccurred", "sqlMutationRun", "migrationRun", "dbAuditToolRun")) {
        if ($noDb.$name -ne $false) {
            $failures.Add("No-DB-mutation audit failed: $name")
        }
    }

    $noOrder = Read-Json "phase-system-audit-r001-no-order-fill-route-audit.json"
    foreach ($name in @("ordersCreated", "childOrdersCreated", "childSlicesCreated", "omsExecutableOrdersCreated", "routesCreated", "submissionsCreated", "fillsCreated", "executionReportsCreated", "paperLedgerCommitOccurred", "stateMutationOccurred")) {
        if ($noOrder.$name -ne $false) {
            $failures.Add("No-order/fill/route audit failed: $name")
        }
    }

    $matrix = Read-Json "phase-system-audit-r001-system-gap-matrix.json"
    if (-not $matrix.gaps -or $matrix.gaps.Count -eq 0) {
        $failures.Add("Gap matrix missing gaps.")
    }

    $dependency = Read-Json "phase-system-audit-r001-cross-workstream-dependency-map.json"
    if (-not $dependency.dependencies -or $dependency.dependencies.Count -eq 0) {
        $failures.Add("Dependency map missing dependencies.")
    }

    $ownership = Read-Json "phase-system-audit-r001-ownership-map.json"
    if (-not $ownership.owners -or $ownership.owners.Count -eq 0) {
        $failures.Add("Ownership map missing owners.")
    }

    $roadmap = Read-Json "phase-system-audit-r001-roadmap-recommendation.json"
    if (-not $roadmap.roadmap -or $roadmap.roadmap.Count -eq 0) {
        $failures.Add("Roadmap missing phases.")
    }

    $evidence = Read-Json "phase-system-audit-r001-build-test-validator-evidence.json"
    if ($evidence.build.result -ne "Passed") {
        $failures.Add("Build evidence is not Passed.")
    }

    if ($evidence.focusedStaticChecks.run -ne $true) {
        $failures.Add("Focused static check evidence missing.")
    }

    $summary = Get-Content -LiteralPath (Join-Path $auditRoot "phase-system-audit-r001-summary.md") -Raw
    foreach ($classification in @(
        "SYSTEM_AUDIT_R001_PASS_CROSS_WORKSTREAM_GAP_AUDIT_READY_NO_EXTERNAL",
        "SYSTEM_AUDIT_R001_PASS_DEPENDENCY_AND_OWNERSHIP_MAP_READY_NO_EXTERNAL",
        "SYSTEM_AUDIT_R001_PASS_ROADMAP_RECOMMENDATION_READY_NO_EXTERNAL",
        "SYSTEM_AUDIT_R001_PASS_NO_EXECUTION_NO_MUTATION_GATE_READY_NO_EXTERNAL"
    )) {
        if ($summary -notmatch [regex]::Escape($classification)) {
            $failures.Add("Missing expected classification in summary: $classification")
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host "SYSTEM_AUDIT_R001_GATE_FAIL"
    $failures | ForEach-Object { Write-Host "- $_" }
    exit 1
}

Write-Host "SYSTEM_AUDIT_R001_GATE_PASS"
exit 0
