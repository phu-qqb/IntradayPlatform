param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
$auditRoot = Join-Path $Root "artifacts/readiness/system-audit"
$required = @(
    "phase-system-audit-r002-summary.md",
    "phase-system-audit-r002-r001-reference.json",
    "phase-system-audit-r002-cross-thread-contract-index.json",
    "phase-system-audit-r002-canonical-timing-contract.json",
    "phase-system-audit-r002-qubes-output-contract.json",
    "phase-system-audit-r002-pms-handoff-contract.json",
    "phase-system-audit-r002-execution-intent-contract.json",
    "phase-system-audit-r002-marketdata-readiness-contract.json",
    "phase-system-audit-r002-lmax-marketdata-db-contract.json",
    "phase-system-audit-r002-r009-sandbox-execution-contract.json",
    "phase-system-audit-r002-oms-sandbox-state-model-contract.json",
    "phase-system-audit-r002-paper-ledger-separation-contract.json",
    "phase-system-audit-r002-risk-control-contract.json",
    "phase-system-audit-r002-environment-secret-contract.json",
    "phase-system-audit-r002-contract-ownership-map.json",
    "phase-system-audit-r002-contract-validation-rules.json",
    "phase-system-audit-r002-gap-to-contract-mapping.json",
    "phase-system-audit-r002-source-of-truth-artifact-index.json",
    "phase-system-audit-r002-change-control-policy.json",
    "phase-system-audit-r002-next-actions-by-thread.json",
    "phase-system-audit-r002-roadmap-update.md",
    "phase-system-audit-r002-roadmap-update.json",
    "phase-system-audit-r002-no-external-audit.json",
    "phase-system-audit-r002-no-execution-audit.json",
    "phase-system-audit-r002-no-db-mutation-audit.json",
    "phase-system-audit-r002-no-order-fill-route-audit.json",
    "phase-system-audit-r002-forbidden-actions-audit.json",
    "phase-system-audit-r002-next-phase-recommendation.json",
    "phase-system-audit-r002-build-test-validator-evidence.json"
)

$failures = New-Object System.Collections.Generic.List[string]
foreach ($file in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $auditRoot $file))) {
        $failures.Add("Missing required artifact: $file")
    }
}

function Read-Json($name) {
    Get-Content -LiteralPath (Join-Path $auditRoot $name) -Raw | ConvertFrom-Json
}

if ($failures.Count -eq 0) {
    $forbidden = Read-Json "phase-system-audit-r002-forbidden-actions-audit.json"
    foreach ($property in $forbidden.forbiddenActions.PSObject.Properties) {
        if ($property.Value -ne $false) {
            $failures.Add("Forbidden action flag is not false: $($property.Name)")
        }
    }

    $timing = Read-Json "phase-system-audit-r002-canonical-timing-contract.json"
    if ($timing.requiredFields.BarIntervalMinutes -ne 15) { $failures.Add("Canonical timing BarIntervalMinutes must equal 15.") }
    if ($timing.requiredFields.IsCanonicalQuarterHour -ne $true) { $failures.Add("Canonical timing must require quarter-hour.") }

    $qubes = Read-Json "phase-system-audit-r002-qubes-output-contract.json"
    if ($qubes.fieldDefaultsAndConstraints.DirectCrossSignalOnly -ne $true) { $failures.Add("Qubes output must keep direct crosses signal-only.") }
    if ($qubes.fieldDefaultsAndConstraints.RequiresNetting -ne $true) { $failures.Add("Qubes output must require netting.") }

    $pms = Read-Json "phase-system-audit-r002-pms-handoff-contract.json"
    if ($pms.constraints.OvernightAllowed -ne $false) { $failures.Add("PMS handoff must require OvernightAllowed=false.") }

    $intent = Read-Json "phase-system-audit-r002-execution-intent-contract.json"
    if ($intent.constraints.ProductionAllowed -ne $false) { $failures.Add("Execution intent ProductionAllowed must be false.") }

    $md = Read-Json "phase-system-audit-r002-marketdata-readiness-contract.json"
    if ($md.constraints.BarIntervalMinutes -ne 15) { $failures.Add("MarketData readiness BarIntervalMinutes must equal 15.") }

    $lmaxDb = Read-Json "phase-system-audit-r002-lmax-marketdata-db-contract.json"
    if ($lmaxDb.constraints.NoSecretsPersisted -ne $true) { $failures.Add("LMAX DB contract must require NoSecretsPersisted=true.") }

    $sandbox = Read-Json "phase-system-audit-r002-r009-sandbox-execution-contract.json"
    if ($sandbox.constraints.SandboxOnly -ne $true) { $failures.Add("R009 sandbox contract SandboxOnly must be true.") }
    if ($sandbox.constraints.ProductionOrder -ne $false) { $failures.Add("R009 sandbox contract ProductionOrder must be false.") }

    $ledger = Read-Json "phase-system-audit-r002-paper-ledger-separation-contract.json"
    if ($ledger.requiredFlags.PaperLedgerCommitAllowed -ne $false) { $failures.Add("Paper ledger commit must be false.") }
    if ($ledger.requiredFlags.ProductionLedgerCommitAllowed -ne $false) { $failures.Add("Production ledger commit must be false.") }

    $risk = Read-Json "phase-system-audit-r002-risk-control-contract.json"
    if ($risk.constraints.ProductionAllowed -ne $false) { $failures.Add("Risk ProductionAllowed must be false.") }
    if ($risk.constraints.DirectCrossExecutionAllowed -ne $false) { $failures.Add("Risk DirectCrossExecutionAllowed must be false.") }

    $env = Read-Json "phase-system-audit-r002-environment-secret-contract.json"
    if ($env.statements -join " " -notmatch "Credential values must never be printed or persisted") {
        $failures.Add("Environment/secret contract missing credential non-persistence statement.")
    }

    $ownership = Read-Json "phase-system-audit-r002-contract-ownership-map.json"
    if (-not $ownership.ownership -or $ownership.ownership.Count -lt 11) { $failures.Add("Ownership map incomplete.") }

    $rules = Read-Json "phase-system-audit-r002-contract-validation-rules.json"
    if (-not $rules.globalValidationRules -or $rules.globalValidationRules.Count -eq 0) { $failures.Add("Validation rules missing.") }

    $change = Read-Json "phase-system-audit-r002-change-control-policy.json"
    if ($change.rules -join " " -notmatch "new version") { $failures.Add("Change-control policy missing new-version rule.") }

    $roadmap = Read-Json "phase-system-audit-r002-roadmap-update.json"
    if (-not $roadmap.roadmapUpdate -or $roadmap.roadmapUpdate.Count -eq 0) { $failures.Add("Roadmap update missing.") }

    $evidence = Read-Json "phase-system-audit-r002-build-test-validator-evidence.json"
    if ($evidence.build.result -ne "Passed") { $failures.Add("Build evidence missing or not passed.") }
    if ($evidence.focusedStaticChecks.run -ne $true) { $failures.Add("Focused static checks evidence missing.") }

    $summary = Get-Content -LiteralPath (Join-Path $auditRoot "phase-system-audit-r002-summary.md") -Raw
    foreach ($classification in @(
        "SYSTEM_AUDIT_R002_PASS_CROSS_THREAD_CONTRACT_FREEZE_READY_NO_EXTERNAL",
        "SYSTEM_AUDIT_R002_PASS_SOURCE_OF_TRUTH_CONTRACT_INDEX_READY_NO_EXTERNAL",
        "SYSTEM_AUDIT_R002_PASS_OWNERSHIP_AND_VALIDATION_RULES_READY_NO_EXTERNAL",
        "SYSTEM_AUDIT_R002_PASS_NO_EXECUTION_NO_MUTATION_GATE_READY_NO_EXTERNAL"
    )) {
        if ($summary -notmatch [regex]::Escape($classification)) {
            $failures.Add("Missing expected classification in summary: $classification")
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host "SYSTEM_AUDIT_R002_GATE_FAIL"
    $failures | ForEach-Object { Write-Host "- $_" }
    exit 1
}

Write-Host "SYSTEM_AUDIT_R002_GATE_PASS"
exit 0
