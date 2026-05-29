$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"

function Fail {
    param([string]$Message)
    throw "EXEC-LIVE-R006 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-live-r006-summary.md",
    "phase-exec-live-r006-r005-consumer-boundary-reference.json",
    "phase-exec-live-r006-r009-contract-reference.json",
    "phase-exec-live-r006-preview-request-audit-contract.json",
    "phase-exec-live-r006-preview-response-audit-contract.json",
    "phase-exec-live-r006-preview-batch-audit-contract.json",
    "phase-exec-live-r006-preview-audit-envelope-contract.json",
    "phase-exec-live-r006-preview-audit-store-contract.json",
    "phase-exec-live-r006-artifact-audit-writer-contract.json",
    "phase-exec-live-r006-sample-single-preview-audit-record.json",
    "phase-exec-live-r006-sample-batch-preview-audit-record.json",
    "phase-exec-live-r006-idempotency-replay-semantics.json",
    "phase-exec-live-r006-idempotency-conflict-results.json",
    "phase-exec-live-r006-audit-path-safety-review.json",
    "phase-exec-live-r006-no-order-domain-persistence-audit.json",
    "phase-exec-live-r006-no-route-submission-persistence-audit.json",
    "phase-exec-live-r006-no-ledger-persistence-audit.json",
    "phase-exec-live-r006-no-trading-state-mutation-audit.json",
    "phase-exec-live-r006-kill-switch-feature-flag-review.json",
    "phase-exec-live-r006-disabled-boundary-guard-review.json",
    "phase-exec-live-r006-no-broker-activation-audit.json",
    "phase-exec-live-r006-no-live-marketdata-audit.json",
    "phase-exec-live-r006-no-scheduler-service-polling-audit.json",
    "phase-exec-live-r006-no-order-created-audit.json",
    "phase-exec-live-r006-no-child-order-audit.json",
    "phase-exec-live-r006-no-executable-schedule-audit.json",
    "phase-exec-live-r006-no-route-no-submission-audit.json",
    "phase-exec-live-r006-no-fill-execution-report-audit.json",
    "phase-exec-live-r006-no-paper-ledger-commit-audit.json",
    "phase-exec-live-r006-no-state-mutation-audit.json",
    "phase-exec-live-r006-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-live-r006-legacy-compatibility-preservation.json",
    "phase-exec-live-r006-direct-cross-exclusion-preservation.json",
    "phase-exec-live-r006-usd-pair-netting-requirement.json",
    "phase-exec-live-r006-usdjpy-caveat-preservation.json",
    "phase-exec-live-r006-cost-guidance-preservation.json",
    "phase-exec-live-r006-nonmajor-calibration-preservation.json",
    "phase-exec-live-r006-no-external-audit.json",
    "phase-exec-live-r006-forbidden-actions-audit.json",
    "phase-exec-live-r006-next-phase-recommendation.json",
    "phase-exec-live-r006-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$sourcePath = Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009EmsOmsDisabledScaffold.cs"
$testPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009PreviewAuditPersistenceTests.cs"
if (-not (Test-Path -LiteralPath $sourcePath)) { Fail "Missing R009 scaffold source file" }
if (-not (Test-Path -LiteralPath $testPath)) { Fail "Missing focused R006 tests" }

$source = Get-Content -LiteralPath $sourcePath -Raw
foreach ($needle in @(
    "R009PreviewRequestAuditRecord",
    "R009PreviewResponseAuditRecord",
    "R009PreviewBatchAuditRecord",
    "R009PreviewAuditEnvelope",
    "R009PreviewAuditStoreContract",
    "R009PreviewArtifactAuditWriter",
    "R009PreviewAuditPersistenceResult",
    "PreviewAuditOnly",
    "SameRequestIdDifferentInputHash",
    "UnsafeRequestCannotPersistAudit",
    "artifacts/readiness/execution-live/audit"
)) {
    if ($source -notmatch [regex]::Escape($needle)) { Fail "Source missing $needle" }
}

$tests = Get-Content -LiteralPath $testPath -Raw
foreach ($needle in @(
    "Audit_record_written_for_valid_single_preview",
    "Audit_record_written_for_valid_batch_preview",
    "Same_request_replay_is_idempotent",
    "Same_request_id_with_different_input_is_conflict",
    "Audit_path_must_be_artifact_only",
    "Audit_store_contract_never_allows_order_route_ledger_or_state_persistence",
    "Forbidden_consumer_cannot_persist_audit",
    "Live_broker_order_enabled_request_cannot_persist_audit",
    "Legacy_06_is_rejected_and_not_persisted_as_valid_preview",
    "Direct_cross_is_audited_as_rejection_only",
    "Usdjpy_caveat_is_preserved_in_audit"
)) {
    if ($tests -notmatch [regex]::Escape($needle)) { Fail "Focused test missing $needle" }
}

$r009 = Read-Json "phase-exec-live-r006-r009-contract-reference.json"
if ($r009.NonExecutable -ne $true -or $r009.NotAnOrder -ne $true -or $r009.NoBrokerRoute -ne $true) { Fail "R009 non-executable contract weakened" }
if ($r009.BrokerReady -ne $false -or $r009.LiveReady -ne $false -or $r009.ExecutablePromotionAuthorized -ne $false) { Fail "R009 promoted or live/broker ready" }

$store = Read-Json "phase-exec-live-r006-preview-audit-store-contract.json"
if ($store.ArtifactOnly -ne $true -or $store.DbRequired -ne $false -or $store.ExternalServiceRequired -ne $false) { Fail "Audit store is not artifact-only/no-db/no-external" }
if ($store.OrderDomainPersistenceAllowed -ne $false -or $store.RouteSubmissionPersistenceAllowed -ne $false -or $store.LedgerPersistenceAllowed -ne $false -or $store.TradingStateMutationAllowed -ne $false) {
    Fail "Audit store permits forbidden persistence"
}

$writer = Read-Json "phase-exec-live-r006-artifact-audit-writer-contract.json"
if ($writer.RequiredRootRelativePath -ne "artifacts/readiness/execution-live/audit") { Fail "Audit writer path safety review missing expected artifact path" }
if ($writer.WritesOrderTables -ne $false -or $writer.WritesRouteSubmissionTables -ne $false -or $writer.WritesLedgerTables -ne $false -or $writer.MutatesTradingState -ne $false) {
    Fail "Audit writer permits order/route/ledger/state persistence"
}
if ($writer.ReplaySafe -ne $true -or $writer.SameRequestIdDifferentInputRejected -ne $true) { Fail "Audit writer idempotency conflict handling weakened" }

$envelopeContract = Read-Json "phase-exec-live-r006-preview-audit-envelope-contract.json"
foreach ($property in @("ArtifactOnly", "NoDbPersistence", "NoOrderDomainPersistence", "NoRouteSubmissionPersistence", "NoLedgerPersistence", "NoTradingStateMutation")) {
    if ($envelopeContract.$property -ne $true) { Fail "Audit envelope missing $property=true" }
}

$single = Read-Json "phase-exec-live-r006-sample-single-preview-audit-record.json"
foreach ($record in @($single.RequestAudit, $single.ResponseAudit)) {
    if ($record.RequestMode -ne "DisabledPreviewOnly") { Fail "Single sample audit is not DisabledPreviewOnly" }
    foreach ($property in @("DryRunOnly", "NonExecutable", "NotAnOrder", "NotSubmitted", "NoBrokerRoute", "NoOrderDomainPersistence", "NoTradingStateMutation", "NoPaperLedgerCommit")) {
        if ($record.$property -ne $true) { Fail "Single sample audit missing $property=true" }
    }
    if ($record.RetentionCategory -ne "PreviewAuditOnly") { Fail "Single sample retention is not PreviewAuditOnly" }
}

$batch = Read-Json "phase-exec-live-r006-sample-batch-preview-audit-record.json"
if ($batch.BatchAudit.RequestMode -ne "DisabledPreviewOnly") { Fail "Batch sample audit is not DisabledPreviewOnly" }
if ($batch.BatchAudit.ItemCount -ne 3 -or $batch.BatchAudit.PreviewReadyCount -ne 2 -or $batch.BatchAudit.HeldMissingReadinessCount -ne 1 -or $batch.BatchAudit.RejectedCount -ne 0) { Fail "Batch sample counts unexpected" }
foreach ($property in @("DryRunOnly", "NonExecutable", "NotAnOrder", "NotSubmitted", "NoBrokerRoute", "NoOrderDomainPersistence", "NoTradingStateMutation", "NoPaperLedgerCommit")) {
    if ($batch.BatchAudit.$property -ne $true) { Fail "Batch sample audit missing $property=true" }
}
if ($batch.BatchAudit.RetentionCategory -ne "PreviewAuditOnly") { Fail "Batch sample retention is not PreviewAuditOnly" }

$semantics = Read-Json "phase-exec-live-r006-idempotency-replay-semantics.json"
if ($semantics.SameRequestIdSameInputHash -ne "ReplaySafe") { Fail "Replay-safe semantic missing" }
if ($semantics.SameRequestIdDifferentInputHash -ne "Conflict") { Fail "Conflict semantic missing" }
if ($semantics.NewRequestIdSameInputHash -ne "AllowedLinkedByInputHash") { Fail "New request same input semantic missing" }
if ($semantics.AuditHashDeterministic -ne $true -or $semantics.SameRequestIdDifferentInputRejected -ne $true) { Fail "Deterministic audit/conflict handling missing" }

$conflict = Read-Json "phase-exec-live-r006-idempotency-conflict-results.json"
if ($conflict.ReplaySafeExample.Result -ne "ReplaySafe") { Fail "Replay-safe example missing" }
if ($conflict.ConflictExample.Result -ne "Conflict" -or $conflict.ConflictExample.Reason -ne "SameRequestIdDifferentInputHash") { Fail "Conflict example missing expected reason" }

$pathSafety = Read-Json "phase-exec-live-r006-audit-path-safety-review.json"
if ($pathSafety.RequiredRootRelativePath -ne "artifacts/readiness/execution-live/audit") { Fail "Audit path safety review missing" }
if ($pathSafety.ArtifactOnly -ne $true -or $pathSafety.DbRequired -ne $false -or $pathSafety.ExternalServiceRequired -ne $false) { Fail "Audit path is not artifact-only/no-db/no-external" }
if ($pathSafety.OrderDomainPersistenceAllowed -ne $false -or $pathSafety.RouteSubmissionPersistenceAllowed -ne $false -or $pathSafety.LedgerPersistenceAllowed -ne $false -or $pathSafety.TradingStateMutationAllowed -ne $false) {
    Fail "Audit path safety allows forbidden persistence"
}

$orderPersistence = Read-Json "phase-exec-live-r006-no-order-domain-persistence-audit.json"
if ($orderPersistence.WritesOrderTables -ne $false -or $orderPersistence.WritesChildOrderTables -ne $false -or $orderPersistence.PersistsPreviewAsOrderDomainEntity -ne $false -or $orderPersistence.NoOrderDomainPersistence -ne $true) { Fail "Order-domain persistence audit failed" }
$routePersistence = Read-Json "phase-exec-live-r006-no-route-submission-persistence-audit.json"
if ($routePersistence.WritesRouteTables -ne $false -or $routePersistence.WritesSubmissionTables -ne $false -or $routePersistence.NoRouteSubmissionPersistence -ne $true) { Fail "Route/submission persistence audit failed" }
$ledgerPersistence = Read-Json "phase-exec-live-r006-no-ledger-persistence-audit.json"
if ($ledgerPersistence.WritesLedgerTables -ne $false -or $ledgerPersistence.CommitsPaperLedger -ne $false -or $ledgerPersistence.NoPaperLedgerCommit -ne $true) { Fail "Ledger persistence audit failed" }
$stateMutation = Read-Json "phase-exec-live-r006-no-trading-state-mutation-audit.json"
if ($stateMutation.MutatesTradingState -ne $false -or $stateMutation.MutatesLiveBrokerProductionTradingState -ne $false -or $stateMutation.NoTradingStateMutation -ne $true) { Fail "Trading state mutation audit failed" }

$flags = Read-Json "phase-exec-live-r006-kill-switch-feature-flag-review.json"
if ($flags.R009LiveTradingEnabled -ne $false -or
    $flags.R009BrokerRoutingEnabled -ne $false -or
    $flags.R009OrderSubmissionEnabled -ne $false -or
    $flags.R009ExecutableScheduleEnabled -ne $false -or
    $flags.R009PaperLedgerCommitEnabled -ne $false -or
    $flags.R009SchedulerEnabled -ne $false -or
    $flags.R009BackgroundWorkerEnabled -ne $false -or
    $flags.R009DryRunOnly -ne $true) { Fail "Kill-switch flags weakened" }

$disabled = Read-Json "phase-exec-live-r006-disabled-boundary-guard-review.json"
foreach ($property in @("BrokerRouteCreationAllowed", "OrderCreationAllowed", "ChildSliceCreationAllowed", "ChildOrderCreationAllowed", "ScheduleExecutionAllowed", "SubmissionAllowed", "FillCreationAllowed", "ExecutionReportCreationAllowed", "StateMutationAllowed", "PaperLedgerCommitAllowed")) {
    if ($disabled.$property -ne $false) { Fail "Disabled boundary guard weakened: $property" }
}

$legacy = Read-Json "phase-exec-live-r006-legacy-compatibility-preservation.json"
if ($legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 used as future canonical" }
$direct = Read-Json "phase-exec-live-r006-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed -ne $false) { Fail "Direct-cross execution allowed" }
$cost = Read-Json "phase-exec-live-r006-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$usdPair = Read-Json "phase-exec-live-r006-usd-pair-netting-requirement.json"
if ($usdPair.AudusdStatus -ne "SupportedAndNotFailed") { Fail "AUDUSD misclassified" }
$usdjpy = Read-Json "phase-exec-live-r006-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8") {
    Fail "USDJPY caveat weakened"
}

$forbidden = Read-Json "phase-exec-live-r006-forbidden-actions-audit.json"
foreach ($property in @(
    "ExternalApiCallsMade",
    "PolygonCallsMade",
    "LmaxCallsMade",
    "BrokerActivationOccurred",
    "LiveMarketDataRequested",
    "SchedulerServicePollingBackgroundJobIntroduced",
    "PmsEmsOmsCycleRun",
    "ManualNoExternalCommandRun",
    "BacktestSimulationRun",
    "TcaResultLinesCreated",
    "ExecutableScheduleCreated",
    "OrdersChildOrdersRoutesSubmissionsFillsReportsCreated",
    "PaperLedgerCommitCreated",
    "StateMutationOccurred",
    "R009PromotedToExecutableUse"
)) {
    if ($forbidden.$property -ne $false) { Fail "Forbidden action observed: $property" }
}

$evidence = Read-Json "phase-exec-live-r006-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedR006Tests -ne "Passed") { Fail "Focused R006 test evidence missing or not passed" }
if ($evidence.UnitTests -ne "Passed") { Fail "Unit test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-LIVE-R006 validator passed."
