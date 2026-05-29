param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error "SYSTEM_AUDIT_R004_GATE_FAIL: $Message"
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path $Path)) {
        Fail "Missing required artifact: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

$auditRoot = Join-Path $Root "artifacts/readiness/system-audit"
$required = @(
    "phase-system-audit-r004-summary.md",
    "phase-system-audit-r004-r002-contract-freeze-reference.json",
    "phase-system-audit-r004-r003-adoption-readiness-reference.json",
    "phase-system-audit-r004-marketdata-warn-adoption-reference.json",
    "phase-system-audit-r004-qubes-pms-inprogress-reference.json",
    "phase-system-audit-r004-exec-sandbox-evidence-reference.json",
    "phase-system-audit-r004-updated-adoption-evidence-matrix.json",
    "phase-system-audit-r004-status-by-contract.json",
    "phase-system-audit-r004-status-by-workstream.json",
    "phase-system-audit-r004-warnings-vs-blockers.json",
    "phase-system-audit-r004-missing-adoption-evidence-list.json",
    "phase-system-audit-r004-next-actions-by-thread.json",
    "phase-system-audit-r004-production-live-blocker-summary.json",
    "phase-system-audit-r004-roadmap-update.md",
    "phase-system-audit-r004-roadmap-update.json",
    "phase-system-audit-r004-no-external-audit.json",
    "phase-system-audit-r004-no-execution-audit.json",
    "phase-system-audit-r004-no-db-mutation-audit.json",
    "phase-system-audit-r004-no-order-fill-route-audit.json",
    "phase-system-audit-r004-forbidden-actions-audit.json",
    "phase-system-audit-r004-next-phase-recommendation.json",
    "phase-system-audit-r004-build-test-validator-evidence.json"
)

foreach ($file in $required) {
    $path = Join-Path $auditRoot $file
    if (-not (Test-Path $path)) {
        Fail "Missing required R004 artifact: $file"
    }
}

$status = Read-Json (Join-Path $auditRoot "phase-system-audit-r004-status-by-contract.json")
$marketData = Read-Json (Join-Path $auditRoot "phase-system-audit-r004-marketdata-warn-adoption-reference.json")
$qubes = Read-Json (Join-Path $auditRoot "phase-system-audit-r004-qubes-pms-inprogress-reference.json")
$warnings = Read-Json (Join-Path $auditRoot "phase-system-audit-r004-warnings-vs-blockers.json")
$productionBlockers = Read-Json (Join-Path $auditRoot "phase-system-audit-r004-production-live-blocker-summary.json")
$noExternal = Read-Json (Join-Path $auditRoot "phase-system-audit-r004-no-external-audit.json")
$noExecution = Read-Json (Join-Path $auditRoot "phase-system-audit-r004-no-execution-audit.json")
$noDb = Read-Json (Join-Path $auditRoot "phase-system-audit-r004-no-db-mutation-audit.json")
$noOrder = Read-Json (Join-Path $auditRoot "phase-system-audit-r004-no-order-fill-route-audit.json")
$forbidden = Read-Json (Join-Path $auditRoot "phase-system-audit-r004-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $auditRoot "phase-system-audit-r004-build-test-validator-evidence.json")

if ($status.statusByContract.'lmax-marketdata-db.v1' -ne "AdoptedWithWarnings") {
    Fail "MarketData lmax-marketdata-db.v1 must remain AdoptedWithWarnings, not PASS or FAIL."
}

if ($status.statusByContract.'marketdata-readiness.v1' -ne "AdoptedWithWarnings") {
    Fail "MarketData marketdata-readiness.v1 must remain AdoptedWithWarnings, not PASS or FAIL."
}

if ($status.statusByContract.'qubes-output.v1' -ne "InProgressByOtherThread") {
    Fail "Qubes/PMS qubes-output.v1 must be InProgressByOtherThread."
}

if ($status.statusByContract.'pms-handoff.v1' -ne "InProgressByOtherThread") {
    Fail "Qubes/PMS pms-handoff.v1 must be InProgressByOtherThread."
}

if ($qubes.oldQubes4EBootstrapStatus -ne "HistoricalOnlyNotActiveState") {
    Fail "Old Qubes 4E bootstrap is not confirmed historical-only."
}

if ($marketData.latestAdoptionResult -ne "WARN") {
    Fail "MarketData WARN adoption result not recorded."
}

if ($marketData.contractStatus.'lmax-marketdata-db.v1' -ne "WITH_WARNINGS" -or $marketData.contractStatus.'marketdata-readiness.v1' -ne "WITH_WARNINGS") {
    Fail "MarketData warning contracts misclassified."
}

if (-not ($warnings.warnings | Where-Object { $_.id -eq "WARN-MD-M30-MISSING" })) {
    Fail "M30 warning missing."
}

if (-not ($warnings.hardBlockers -contains "Production route still forbidden.")) {
    Fail "Production route blocker missing."
}

if ($productionBlockers.productionLiveStatus -ne "Blocked" -or -not $productionBlockers.noFrozenBlockerWeakened) {
    Fail "Production blocker summary missing or weakened."
}

if ($noExternal.externalApiCalled -or $noExternal.polygonCalled -or $noExternal.lmaxCalled -or $noExternal.brokerActivated -or $noExternal.liveMarketDataRequested) {
    Fail "No-external audit indicates a forbidden external/broker/live action."
}

if ($noExecution.pmsEmsOmsCycleRun -or $noExecution.manualNoExternalRun -or $noExecution.qubesExecutableRun -or $noExecution.pythonNotebookOrScriptRun -or $noExecution.cppOrCudaWorkloadRun -or $noExecution.backtestOrSimulationRun -or $noExecution.schedulerServicePollingBackgroundJobIntroduced) {
    Fail "No-execution audit indicates a forbidden workload."
}

if ($noDb.dbMutationOccurred -or $noDb.sqlMutationOccurred -or $noDb.connectionStringValuePersisted) {
    Fail "No-DB-mutation audit indicates mutation or persisted connection string."
}

if ($noOrder.ordersCreated -or $noOrder.childOrdersCreated -or $noOrder.routesCreated -or $noOrder.submissionsCreated -or $noOrder.fillsCreated -or $noOrder.executionReportsCreated -or $noOrder.executableSchedulesCreated -or $noOrder.ledgerCommitOccurred -or $noOrder.stateMutationOccurred) {
    Fail "No-order/fill/route audit indicates forbidden creation or mutation."
}

$fa = $forbidden.forbiddenActionsPreserved
if ($fa.externalApiPolygonLmaxCalled -or $fa.brokerActivation -or $fa.liveMarketDataRequest -or $fa.pmsEmsOmsCycleRun -or $fa.manualNoExternalRun -or $fa.qubesPythonCppCudaWorkloadRun -or $fa.backtestOrSimulationRun -or $fa.dbMutation -or $fa.orderRouteFillSubmissionCreated -or $fa.ledgerCommit -or $fa.stateMutation -or $fa.productionRouteEnabled -or $fa.directCrossExecutionAllowed -or $fa.legacy06UsedAsFutureCanonical -or $fa.credentialValuesPersisted -or $fa.oldQubes4EActiveState) {
    Fail "Forbidden-actions audit indicates a forbidden action."
}

if ($forbidden.frozenBlockersWeakened) {
    Fail "Frozen blockers were weakened."
}

if ($evidence.build.result -notin @("Passed", "PassedWithWarnings")) {
    Fail "Build evidence missing or not passing."
}

if ($evidence.validator.result -notin @("Passed", "Pending")) {
    Fail "Validator evidence invalid."
}

$artifactText = Get-ChildItem -LiteralPath $auditRoot -Filter "phase-system-audit-r004-*" -File |
    ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }

if (($artifactText -join "`n") -match "LMAX_DEMO_(FIX|MD)_(USERNAME|PASSWORD|SENDER_COMP_ID|TARGET_COMP_ID)\s*[:=]\s*['""][^'""]+['""]") {
    Fail "Possible credential value persisted in R004 artifacts."
}

Write-Output "SYSTEM_AUDIT_R004_GATE_PASS_UPDATED_ADOPTION_STATUS_READY_NO_EXTERNAL"
