param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$AuditDir = Join-Path $RepoRoot "artifacts/readiness/system-audit"

function Fail([string]$Message) {
    Write-Error "SYSTEM_AUDIT_R003_GATE_FAIL: $Message"
    exit 1
}

function Read-Json([string]$RelativePath) {
    $path = Join-Path $AuditDir $RelativePath
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required artifact: $RelativePath"
    }
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$required = @(
    "phase-system-audit-r003-summary.md",
    "phase-system-audit-r003-r002-contract-freeze-reference.json",
    "phase-system-audit-r003-contract-adoption-matrix.json",
    "phase-system-audit-r003-workstream-adoption-status.json",
    "phase-system-audit-r003-conformance-gap-list.json",
    "phase-system-audit-r003-required-evidence-by-contract.json",
    "phase-system-audit-r003-qubes-pms-adoption-checklist.json",
    "phase-system-audit-r003-marketdata-lmax-db-adoption-checklist.json",
    "phase-system-audit-r003-exec-r009-adoption-checklist.json",
    "phase-system-audit-r003-sandbox-lmax-adoption-checklist.json",
    "phase-system-audit-r003-ledger-state-adoption-checklist.json",
    "phase-system-audit-r003-risk-control-adoption-checklist.json",
    "phase-system-audit-r003-ops-runbook-adoption-checklist.json",
    "phase-system-audit-r003-cross-thread-next-actions.json",
    "phase-system-audit-r003-production-live-blocker-summary.json",
    "phase-system-audit-r003-roadmap-update.md",
    "phase-system-audit-r003-roadmap-update.json",
    "phase-system-audit-r003-no-external-audit.json",
    "phase-system-audit-r003-no-execution-audit.json",
    "phase-system-audit-r003-no-db-mutation-audit.json",
    "phase-system-audit-r003-no-order-fill-route-audit.json",
    "phase-system-audit-r003-forbidden-actions-audit.json",
    "phase-system-audit-r003-next-phase-recommendation.json",
    "phase-system-audit-r003-build-test-validator-evidence.json"
)

foreach ($file in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $AuditDir $file))) {
        Fail "Missing required artifact: $file"
    }
}

$summary = Get-Content -LiteralPath (Join-Path $AuditDir "phase-system-audit-r003-summary.md") -Raw
$expectedClassifications = @(
    "SYSTEM_AUDIT_R003_PASS_CONTRACT_ADOPTION_READINESS_READY_NO_EXTERNAL",
    "SYSTEM_AUDIT_R003_PASS_CONFORMANCE_GAP_LIST_READY_NO_EXTERNAL",
    "SYSTEM_AUDIT_R003_PASS_CROSS_THREAD_NEXT_ACTIONS_READY_NO_EXTERNAL",
    "SYSTEM_AUDIT_R003_PASS_NO_EXECUTION_NO_MUTATION_GATE_READY_NO_EXTERNAL"
)
foreach ($classification in $expectedClassifications) {
    if ($summary -notmatch [regex]::Escape($classification)) {
        Fail "Missing expected classification in summary: $classification"
    }
}

$matrix = Read-Json "phase-system-audit-r003-contract-adoption-matrix.json"
if (-not $matrix.matrix -or $matrix.matrix.Count -lt 11) {
    Fail "Adoption matrix missing or incomplete"
}

$requiredContracts = @(
    "canonical-timing.v1",
    "qubes-output.v1",
    "pms-handoff.v1",
    "execution-intent.v1",
    "marketdata-readiness.v1",
    "lmax-marketdata-db.v1",
    "r009-sandbox-execution.v1",
    "oms-sandbox-state-model.v1",
    "paper-ledger-separation.v1",
    "risk-control.v1",
    "environment-secret.v1"
)
$presentContracts = @($matrix.matrix | ForEach-Object { $_.contractId })
foreach ($contract in $requiredContracts) {
    if ($presentContracts -notcontains $contract) {
        Fail "Adoption matrix missing contract: $contract"
    }
}

$gaps = Read-Json "phase-system-audit-r003-conformance-gap-list.json"
if (-not $gaps.gaps -or $gaps.gaps.Count -lt 8) {
    Fail "Conformance gap list missing or too small"
}
if ($gaps.contractWeakeningCheck.directCrossExclusionWeakened -or
    $gaps.contractWeakeningCheck.usdjpyCaveatWeakened -or
    $gaps.contractWeakeningCheck.productionRouteWeakened -or
    $gaps.contractWeakeningCheck.credentialPersistenceWeakened -or
    $gaps.contractWeakeningCheck.ledgerMutationWeakened -or
    $gaps.contractWeakeningCheck.legacyCompatibilityOnlyWeakened) {
    Fail "A frozen blocker was weakened"
}

$nextActions = Read-Json "phase-system-audit-r003-cross-thread-next-actions.json"
if (-not $nextActions.threads -or $nextActions.threads.Count -lt 3) {
    Fail "Cross-thread next actions missing"
}

$productionBlockers = Read-Json "phase-system-audit-r003-production-live-blocker-summary.json"
if ($productionBlockers.productionLiveDiscussionAllowed -ne $false -or $productionBlockers.productionLiveBlocked -ne $true) {
    Fail "Production blocker summary does not keep production blocked"
}
if ($productionBlockers.nonWeakeningConfirmations.directCrossExecutionAllowed -ne $false -or
    $productionBlockers.nonWeakeningConfirmations.productionRouteEnabled -ne $false -or
    $productionBlockers.nonWeakeningConfirmations.credentialValuesPersisted -ne $false -or
    $productionBlockers.nonWeakeningConfirmations.paperLedgerCommitAllowed -ne $false -or
    $productionBlockers.nonWeakeningConfirmations.legacy0600FutureCanonicalAllowed -ne $false) {
    Fail "Production blocker summary weakens a frozen blocker"
}

$noExternal = Read-Json "phase-system-audit-r003-no-external-audit.json"
if ($noExternal.externalApiCalled -or $noExternal.polygonCalled -or $noExternal.massiveCalled -or $noExternal.lmaxCalled -or $noExternal.brokerActivated -or $noExternal.liveMarketDataRequested) {
    Fail "External API, LMAX, broker, or live market data marked as used"
}

$noExecution = Read-Json "phase-system-audit-r003-no-execution-audit.json"
if ($noExecution.pmsCycleRun -or $noExecution.emsCycleRun -or $noExecution.omsCycleRun -or
    $noExecution.manualNoExternalRun -or $noExecution.qubesExecutableRun -or
    $noExecution.pythonNotebookOrScriptRun -or $noExecution.cppOrCudaWorkloadRun -or
    $noExecution.backtestRun -or $noExecution.simulationRun -or
    $noExecution.r009ProductionPromoted) {
    Fail "Execution workload marked as run"
}

$noDb = Read-Json "phase-system-audit-r003-no-db-mutation-audit.json"
if ($noDb.dbMutation -or $noDb.sqlRun -or $noDb.insertUpdateDeleteMergeTruncateDropAlter -or $noDb.saveChanges -or $noDb.migrateOrEnsureCreated) {
    Fail "DB mutation or SQL marked as run"
}

$noOrder = Read-Json "phase-system-audit-r003-no-order-fill-route-audit.json"
if ($noOrder.ordersCreated -or $noOrder.childOrdersCreated -or $noOrder.routesCreated -or
    $noOrder.submissionsCreated -or $noOrder.fillsCreated -or $noOrder.executionReportsCreated -or
    $noOrder.executableSchedulesCreated -or $noOrder.ledgerCommitsCreated -or $noOrder.stateMutationsCreated) {
    Fail "Order/fill/route/submission/ledger/state artifact marked as created"
}

$forbidden = Read-Json "phase-system-audit-r003-forbidden-actions-audit.json"
if ($forbidden.forbiddenActionsObserved) {
    Fail "Forbidden actions observed"
}
foreach ($property in $forbidden.checks.PSObject.Properties) {
    if ($property.Value -eq $true) {
        Fail "Forbidden action check is true: $($property.Name)"
    }
}

$evidence = Read-Json "phase-system-audit-r003-build-test-validator-evidence.json"
if ($evidence.build.result -ne "Passed") {
    Fail "Build evidence missing or not passed"
}
if (-not $evidence.focusedStaticChecks.run) {
    Fail "Focused static checks evidence missing"
}
if (-not $evidence.validator.script) {
    Fail "Validator evidence missing"
}

$r003Text = Get-ChildItem -LiteralPath $AuditDir -Filter "phase-system-audit-r003-*" |
    Where-Object { -not $_.PSIsContainer } |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = $r003Text -join "`n"

$forbiddenPatterns = @(
    '"ProductionAllowed"\s*:\s*true',
    '"ProductionOrder"\s*:\s*true',
    '"directCrossExecutionAllowed"\s*:\s*true',
    '"DirectCrossExecutionAllowed"\s*:\s*true',
    '"productionRouteEnabled"\s*:\s*true',
    '"credentialValuesPersisted"\s*:\s*true',
    '"legacy0600FutureCanonicalAllowed"\s*:\s*true',
    '"paperLedgerCommitAllowed"\s*:\s*true',
    '"productionLedgerCommitAllowed"\s*:\s*true'
)
foreach ($pattern in $forbiddenPatterns) {
    if ($combined -match $pattern) {
        Fail "Forbidden weakening pattern found: $pattern"
    }
}

Write-Output "SYSTEM_AUDIT_R003_GATE_PASS"
