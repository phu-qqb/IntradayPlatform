$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"

function Fail {
    param([string]$Message)
    throw "EXEC-LIVE-R012 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-live-r012-summary.md",
    "phase-exec-live-r012-r011-preledger-reference.json",
    "phase-exec-live-r012-r009-contract-reference.json",
    "phase-exec-live-r012-paper-ledger-preview-request-contract.json",
    "phase-exec-live-r012-paper-ledger-preview-response-contract.json",
    "phase-exec-live-r012-paper-ledger-preview-line-contract.json",
    "phase-exec-live-r012-hypothetical-position-delta-contract.json",
    "phase-exec-live-r012-hypothetical-cash-impact-contract.json",
    "phase-exec-live-r012-hypothetical-exposure-preview-contract.json",
    "phase-exec-live-r012-paper-ledger-preview-audit-contract.json",
    "phase-exec-live-r012-paper-ledger-preview-artifact-envelope.json",
    "phase-exec-live-r012-paper-ledger-preview-generation-rules.json",
    "phase-exec-live-r012-paper-ledger-preview-boundary-guard.json",
    "phase-exec-live-r012-artifact-writer-contract.json",
    "phase-exec-live-r012-sample-paper-ledger-preview-request.json",
    "phase-exec-live-r012-sample-paper-ledger-preview-response.json",
    "phase-exec-live-r012-sample-paper-ledger-preview-artifact.json",
    "phase-exec-live-r012-held-ledger-preview-sample.json",
    "phase-exec-live-r012-rejected-ledger-preview-sample.json",
    "phase-exec-live-r012-idempotency-replay-results.json",
    "phase-exec-live-r012-conflict-rejection-results.json",
    "phase-exec-live-r012-no-paper-ledger-commit-audit.json",
    "phase-exec-live-r012-no-ledger-mutation-audit.json",
    "phase-exec-live-r012-no-trading-state-mutation-audit.json",
    "phase-exec-live-r012-no-order-domain-persistence-audit.json",
    "phase-exec-live-r012-no-route-submission-persistence-audit.json",
    "phase-exec-live-r012-no-fill-report-persistence-audit.json",
    "phase-exec-live-r012-no-broker-activation-audit.json",
    "phase-exec-live-r012-no-live-marketdata-audit.json",
    "phase-exec-live-r012-no-scheduler-service-polling-audit.json",
    "phase-exec-live-r012-no-order-created-audit.json",
    "phase-exec-live-r012-no-child-order-audit.json",
    "phase-exec-live-r012-no-executable-schedule-audit.json",
    "phase-exec-live-r012-no-route-no-submission-audit.json",
    "phase-exec-live-r012-no-fill-execution-report-audit.json",
    "phase-exec-live-r012-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-live-r012-legacy-compatibility-preservation.json",
    "phase-exec-live-r012-direct-cross-exclusion-preservation.json",
    "phase-exec-live-r012-usd-pair-netting-requirement.json",
    "phase-exec-live-r012-usdjpy-caveat-preservation.json",
    "phase-exec-live-r012-cost-guidance-preservation.json",
    "phase-exec-live-r012-nonmajor-calibration-preservation.json",
    "phase-exec-live-r012-no-external-audit.json",
    "phase-exec-live-r012-forbidden-actions-audit.json",
    "phase-exec-live-r012-next-phase-recommendation.json",
    "phase-exec-live-r012-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$sourcePath = Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009EmsOmsDisabledScaffold.cs"
$testPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009PaperLedgerPreviewOnlyTests.cs"
if (-not (Test-Path -LiteralPath $sourcePath)) { Fail "Application scaffold missing" }
if (-not (Test-Path -LiteralPath $testPath)) { Fail "Focused R012 tests missing" }
$source = Get-Content -LiteralPath $sourcePath -Raw
$tests = Get-Content -LiteralPath $testPath -Raw
foreach ($token in @(
    "R009PaperLedgerPreviewRequest",
    "R009PaperLedgerPreviewResponse",
    "R009PaperLedgerPreviewLine",
    "R009HypotheticalPositionDeltaPreview",
    "R009HypotheticalCashImpactPreview",
    "R009HypotheticalExposurePreview",
    "R009PaperLedgerPreviewAuditRecord",
    "R009PaperLedgerPreviewArtifactEnvelope",
    "R009PaperLedgerPreviewArtifactWriter",
    "R009PaperLedgerPreviewBoundaryGuard"
)) {
    if ($source -notmatch [regex]::Escape($token)) { Fail "Source missing R012 contract token $token" }
}
foreach ($token in @(
    "R009PaperLedgerPreviewOnlyTests",
    "PaperLedgerCommitEnabled",
    "LedgerMutationAllowed",
    "TradingStateMutationAllowed",
    "OrderDomainInputAllowed",
    "ReplaySafe",
    "SameRequestIdDifferentInputHash",
    "USDJPY",
    "15:06:00",
    "EURGBP"
)) {
    if ($tests -notmatch [regex]::Escape($token)) { Fail "Focused R012 tests missing token $token" }
}

$reference = Read-Json "phase-exec-live-r012-r011-preledger-reference.json"
if ($reference.PaperLedgerPreviewGateExplicitlyOpened -ne $true -or $reference.PaperLedgerCommitApprovalInherited -ne $false) { Fail "R011 reference does not open preview-only gate safely" }

$contract = Read-Json "phase-exec-live-r012-r009-contract-reference.json"
if ($contract.NonExecutable -ne $true -or $contract.NotAnOrder -ne $true -or $contract.NoBrokerRoute -ne $true) { Fail "R009 contract weakens non-executable status" }
if ($contract.BrokerReady -ne $false -or $contract.LiveReady -ne $false -or $contract.ExecutablePromotionAuthorized -ne $false) { Fail "R009 promoted or live/broker ready" }

$request = Read-Json "phase-exec-live-r012-sample-paper-ledger-preview-request.json"
if ($request.PreviewOnly -ne $true -or $request.PaperLedgerPreviewEnabled -ne $true -or $request.PaperLedgerCommitEnabled -ne $false -or $request.LedgerMutationAllowed -ne $false -or $request.TradingStateMutationAllowed -ne $false -or $request.OrderDomainInputAllowed -ne $false -or $request.NonExecutable -ne $true -or $request.NotAnOrder -ne $true -or $request.NoBrokerRoute -ne $true) {
    Fail "Sample request is not preview-only safe"
}

$response = Read-Json "phase-exec-live-r012-sample-paper-ledger-preview-response.json"
if ($response.PreviewOnly -ne $true -or $response.PaperLedgerCommit -ne $false -or $response.LedgerMutation -ne $false -or $response.TradingStateMutation -ne $false -or $response.NonExecutable -ne $true -or $response.NotAnOrder -ne $true -or $response.NoBrokerRoute -ne $true -or $response.NoFill -ne $true -or $response.NoExecutionReport -ne $true -or $response.NoRoute -ne $true -or $response.NoSubmission -ne $true) {
    Fail "Sample response is not preview-only safe"
}
if (@($response.PreviewLines).Count -lt 1 -or @($response.HypotheticalPositionDeltas).Count -lt 1 -or @($response.HypotheticalCashImpacts).Count -lt 1) { Fail "Sample response missing preview lines or hypothetical deltas" }

$artifact = Read-Json "phase-exec-live-r012-sample-paper-ledger-preview-artifact.json"
if ($artifact.ArtifactOnly -ne $true -or $artifact.NoDbPersistence -ne $true -or $artifact.NoPaperLedgerTableWrites -ne $true -or $artifact.NoOrderDomainPersistence -ne $true -or $artifact.NoRouteSubmissionPersistence -ne $true -or $artifact.NoFillReportPersistence -ne $true -or $artifact.NoTradingStateMutation -ne $true) {
    Fail "Sample artifact envelope allows forbidden persistence or mutation"
}
$previewArtifactPath = Join-Path $repoRoot "artifacts/readiness/execution-live/paper-ledger-preview/phase-exec-live-r012-sample.paper-ledger-preview.json"
if (-not (Test-Path -LiteralPath $previewArtifactPath)) { Fail "Sample paper-ledger-preview artifact not written to allowed path" }

$generation = Read-Json "phase-exec-live-r012-paper-ledger-preview-generation-rules.json"
if ($generation.NoLedgerStoreWrites -ne $true) { Fail "Generation rules allow ledger store writes" }
foreach ($required in @("produce hypothetical position/exposure/cash impact preview only; do not commit or mutate", "produce HeldLedgerPreview line with HoldReason; no cash/position mutation", "produce RejectedLedgerPreview line with rejection reason; no cash/position mutation", "reject hard")) {
    $found = $false
    foreach ($property in $generation.PSObject.Properties) {
        if (($property.Value -as [string]) -eq $required) { $found = $true }
    }
    if (-not $found) { Fail "Generation rule missing: $required" }
}

$guard = Read-Json "phase-exec-live-r012-paper-ledger-preview-boundary-guard.json"
if ($guard.PaperLedgerPreviewEnabled -ne $true -or $guard.PaperLedgerCommitEnabled -ne $false -or $guard.LedgerMutationAllowed -ne $false -or $guard.TradingStateMutationAllowed -ne $false -or $guard.OrderDomainInputAllowed -ne $false -or $guard.BrokerRoutingEnabled -ne $false -or $guard.LiveTradingEnabled -ne $false -or $guard.ExecutableScheduleEnabled -ne $false) {
    Fail "Boundary guard allows forbidden path"
}

$writer = Read-Json "phase-exec-live-r012-artifact-writer-contract.json"
if ($writer.RootPath -ne "artifacts/readiness/execution-live/paper-ledger-preview" -or $writer.ArtifactOnly -ne $true -or $writer.DbRequired -ne $false -or $writer.PaperLedgerTableWritesAllowed -ne $false -or $writer.OrderDomainPersistenceAllowed -ne $false -or $writer.RouteSubmissionPersistenceAllowed -ne $false -or $writer.FillReportPersistenceAllowed -ne $false -or $writer.TradingStateMutationAllowed -ne $false) {
    Fail "Artifact writer contract unsafe"
}

$held = Read-Json "phase-exec-live-r012-held-ledger-preview-sample.json"
if ($held.Status -ne "HeldLedgerPreview" -or $held.PaperLedgerCommit -ne $false -or $held.LedgerMutation -ne $false -or $held.TradingStateMutation -ne $false -or $held.NotAnOrder -ne $true) { Fail "Held preview sample unsafe" }
$rejected = Read-Json "phase-exec-live-r012-rejected-ledger-preview-sample.json"
if ($rejected.Status -ne "RejectedLedgerPreview" -or $rejected.PaperLedgerCommit -ne $false -or $rejected.LedgerMutation -ne $false -or $rejected.TradingStateMutation -ne $false -or $rejected.NotAnOrder -ne $true) { Fail "Rejected preview sample unsafe" }

$idempotency = Read-Json "phase-exec-live-r012-idempotency-replay-results.json"
if ($idempotency.SameRequestIdSameInputHash -ne "ReplaySafe" -or $idempotency.PersistedDuplicate -ne $false -or $idempotency.Conflict -ne $false -or $idempotency.AuditHashStable -ne $true) { Fail "Idempotency replay result unsafe" }
$conflict = Read-Json "phase-exec-live-r012-conflict-rejection-results.json"
if ($conflict.SameRequestIdDifferentInputHash -ne "Conflict" -or $conflict.Persisted -ne $false -or $conflict.RejectionReason -ne "SameRequestIdDifferentInputHash") { Fail "Conflict rejection result unsafe" }

$usdPair = Read-Json "phase-exec-live-r012-usd-pair-netting-requirement.json"
if ($usdPair.AudusdStatus -ne "SupportedAndNotFailed") { Fail "AUDUSD misclassified" }
$usdjpy = Read-Json "phase-exec-live-r012-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8") {
    Fail "USDJPY caveat weakened"
}
$legacy = Read-Json "phase-exec-live-r012-legacy-compatibility-preservation.json"
if ($legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 used as future canonical" }
$direct = Read-Json "phase-exec-live-r012-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed -ne $false -or $direct.DirectCrossExecutionIntentRejected -ne $true) { Fail "Direct-cross exclusion weakened" }
$cost = Read-Json "phase-exec-live-r012-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$nonmajor = Read-Json "phase-exec-live-r012-nonmajor-calibration-preservation.json"
if ($nonmajor.LiveCapableExecutionAllowed -ne $false) { Fail "Nonmajor/EM/scandi/CNH execution allowed" }

$forbidden = Read-Json "phase-exec-live-r012-forbidden-actions-audit.json"
foreach ($property in @(
    "ExternalApiCallsMade",
    "PolygonCallsMade",
    "LmaxCallsMade",
    "BrokerActivationOccurred",
    "LiveMarketDataRequested",
    "SchedulerServicePollingBackgroundJobIntroduced",
    "PmsEmsOmsProductionCycleRun",
    "ManualNoExternalCommandRun",
    "BacktestSimulationRun",
    "TcaResultLinesCreated",
    "ExecutableScheduleCreated",
    "OrdersChildOrdersRoutesSubmissionsFillsReportsCreated",
    "PaperLedgerCommitCreated",
    "PaperLedgerCommitRecordCreated",
    "PaperLedgerTableWriteOccurred",
    "LedgerMutationAllowed",
    "TradingStateMutationOccurred",
    "ArtifactWriterWritesOutsideAllowedPath",
    "OrderDomainPersistenceOccurred",
    "RouteSubmissionPersistenceOccurred",
    "FillReportPersistenceOccurred",
    "PaperLedgerPreviewMisclassifiedAsCommit",
    "R009PromotedToExecutableUse",
    "DirectCrossExecutionAllowed",
    "Legacy06AcceptedAsFutureCanonical"
)) {
    if ($forbidden.$property -ne $false) { Fail "Forbidden action observed or allowed: $property" }
}

$evidence = Read-Json "phase-exec-live-r012-build-test-validator-evidence.json"
if ([string]::IsNullOrWhiteSpace($evidence.Build) -or $evidence.Build -eq "Pending") { Fail "Build evidence missing" }
if ($evidence.Build -ne "Passed") {
    if ($evidence.NoExternalRestoreRun -ne $true) { Fail "Build did not pass and no no-external restore decision was recorded" }
    if (($evidence.BuildFailureReason -as [string]) -notmatch "project\.assets\.json") { Fail "Build did not pass without a recognized missing project.assets.json restore-state reason" }
}
if ($evidence.FocusedR012Tests -ne "Passed") { Fail "Focused R012 test evidence missing or not passed" }
if ($evidence.UnitTests -ne "Passed") { Fail "Unit test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-LIVE-R012 validator passed."
