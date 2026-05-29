$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"

function Fail {
    param([string]$Message)
    throw "EXEC-LIVE-R004 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-live-r004-summary.md",
    "phase-exec-live-r004-r003-contract-reference.json",
    "phase-exec-live-r004-r009-contract-reference.json",
    "phase-exec-live-r004-batch-api-contract.json",
    "phase-exec-live-r004-batch-request-dto-contract.json",
    "phase-exec-live-r004-batch-response-dto-contract.json",
    "phase-exec-live-r004-batch-item-contract.json",
    "phase-exec-live-r004-batch-item-result-contract.json",
    "phase-exec-live-r004-batch-validation-contract.json",
    "phase-exec-live-r004-sample-batch-request.json",
    "phase-exec-live-r004-sample-batch-response.json",
    "phase-exec-live-r004-invalid-batch-rejection-results.json",
    "phase-exec-live-r004-batch-output-audit.json",
    "phase-exec-live-r004-idempotency-audit-review.json",
    "phase-exec-live-r004-kill-switch-feature-flag-review.json",
    "phase-exec-live-r004-disabled-boundary-guard-review.json",
    "phase-exec-live-r004-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-live-r004-legacy-compatibility-preservation.json",
    "phase-exec-live-r004-direct-cross-exclusion-preservation.json",
    "phase-exec-live-r004-usd-pair-netting-requirement.json",
    "phase-exec-live-r004-usdjpy-caveat-preservation.json",
    "phase-exec-live-r004-cost-guidance-preservation.json",
    "phase-exec-live-r004-nonmajor-calibration-preservation.json",
    "phase-exec-live-r004-no-broker-activation-audit.json",
    "phase-exec-live-r004-no-live-marketdata-audit.json",
    "phase-exec-live-r004-no-scheduler-service-polling-audit.json",
    "phase-exec-live-r004-no-order-created-audit.json",
    "phase-exec-live-r004-no-child-order-audit.json",
    "phase-exec-live-r004-no-executable-schedule-audit.json",
    "phase-exec-live-r004-no-route-no-submission-audit.json",
    "phase-exec-live-r004-no-fill-execution-report-audit.json",
    "phase-exec-live-r004-no-paper-ledger-commit-audit.json",
    "phase-exec-live-r004-no-state-mutation-audit.json",
    "phase-exec-live-r004-no-external-audit.json",
    "phase-exec-live-r004-forbidden-actions-audit.json",
    "phase-exec-live-r004-next-phase-recommendation.json",
    "phase-exec-live-r004-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$sourcePath = Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009EmsOmsDisabledScaffold.cs"
$testPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009DisabledPreviewBatchServiceTests.cs"
if (-not (Test-Path -LiteralPath $sourcePath)) { Fail "Missing R009 scaffold source file" }
if (-not (Test-Path -LiteralPath $testPath)) { Fail "Missing focused R004 tests" }

$source = Get-Content -LiteralPath $sourcePath -Raw
foreach ($needle in @(
    "R009DisabledPreviewBatchRequest",
    "R009DisabledPreviewBatchResponse",
    "R009DisabledPreviewBatchItem",
    "R009DisabledPreviewBatchItemResult",
    "R009DisabledPreviewBatchValidationResult",
    "R009DisabledPreviewBatchService",
    "MaxBatchSizeExceeded",
    "DirectCrossExecutionIntentRejected",
    "UnsupportedInstrumentRejected",
    "CanonicalQuarterHourTargetCloseRequired"
)) {
    if ($source -notmatch [regex]::Escape($needle)) { Fail "Source missing $needle" }
}

$contract = Read-Json "phase-exec-live-r004-batch-api-contract.json"
if ($contract.LiveTradingAllowed -ne $false) { Fail "Batch contract allows live trading" }
if ($contract.BrokerRoutingAllowed -ne $false) { Fail "Batch contract allows broker routing" }
if ($contract.OrderSubmissionAllowed -ne $false) { Fail "Batch contract allows order submission" }
if ($contract.ExecutableScheduleAllowed -ne $false) { Fail "Batch contract allows executable schedule" }
if ($contract.PaperLedgerCommitAllowed -ne $false) { Fail "Batch contract allows paper ledger commit" }
foreach ($status in @("PreviewReady", "HeldMissingReadiness", "Rejected")) {
    if (@($contract.AllowedStatuses) -notcontains $status) { Fail "Batch contract missing status $status" }
}

$request = Read-Json "phase-exec-live-r004-sample-batch-request.json"
if ($request.RequestMode -ne "DisabledPreviewOnly") { Fail "Sample batch request mode invalid" }
if ($request.DryRunOnly -ne $true) { Fail "Sample batch request missing DryRunOnly=true" }
if ($request.LiveTradingEnabled -ne $false -or
    $request.BrokerRoutingEnabled -ne $false -or
    $request.OrderSubmissionEnabled -ne $false -or
    $request.ExecutableScheduleEnabled -ne $false -or
    $request.PaperLedgerCommitEnabled -ne $false) {
    Fail "Sample batch request enables forbidden path"
}
if ($request.NoBrokerRoute -ne $true) { Fail "Sample batch request missing NoBrokerRoute=true" }

$response = Read-Json "phase-exec-live-r004-sample-batch-response.json"
if ($response.BatchStatus -ne "PreviewBatchGenerated") { Fail "Sample batch response status unexpected" }
if ($response.Validation.IsValid -ne $true) { Fail "Sample batch response validation not valid" }
if ($response.PreviewReadyCount -ne 2) { Fail "Sample batch preview-ready count unexpected" }
if ($response.HeldMissingReadinessCount -ne 1) { Fail "Sample batch held-missing-readiness count unexpected" }
if ($response.RejectedCount -ne 0) { Fail "Sample batch rejected count unexpected" }
if ($response.NonExecutable -ne $true -or
    $response.NotAnOrder -ne $true -or
    $response.NotSubmitted -ne $true -or
    $response.NoBrokerRoute -ne $true -or
    $response.NoFill -ne $true -or
    $response.NoExecutionReport -ne $true -or
    $response.NoRoute -ne $true -or
    $response.NoSubmission -ne $true -or
    $response.NoPaperLedgerCommit -ne $true) {
    Fail "Sample batch response safety flags weakened"
}
if ([string]::IsNullOrWhiteSpace($response.IdempotencyHash) -or [string]::IsNullOrWhiteSpace($response.AuditHash)) {
    Fail "Sample batch response missing hash"
}

$invalid = Read-Json "phase-exec-live-r004-invalid-batch-rejection-results.json"
$allReasons = @($invalid.Results | ForEach-Object { $_.RejectionReasons } | ForEach-Object { $_ })
foreach ($expected in @(
    "LiveTradingMustRemainDisabled",
    "MaxBatchSizeExceeded",
    "DirectCrossExecutionIntentRejected",
    "UnsupportedInstrumentRejected",
    "CanonicalQuarterHourTargetCloseRequired",
    "InversionMetadataInvalid",
    "ForbiddenOutputRequested:ExecutableSchedule"
)) {
    if ($allReasons -notcontains $expected) { Fail "Invalid batch rejection missing $expected" }
}

$outputAudit = Read-Json "phase-exec-live-r004-batch-output-audit.json"
if ($outputAudit.ResponseCanBeRepresentedAsOrderRouteFillSchedule -ne $false) { Fail "Batch output can be represented as order/route/fill/schedule" }
if ($outputAudit.CreatesOrder -ne $false -or
    $outputAudit.CreatesChildOrder -ne $false -or
    $outputAudit.CreatesRoute -ne $false -or
    $outputAudit.CreatesSubmission -ne $false -or
    $outputAudit.CreatesFill -ne $false -or
    $outputAudit.CreatesExecutionReport -ne $false -or
    $outputAudit.CreatesExecutableSchedule -ne $false) {
    Fail "Batch output audit creates executable artifact"
}

$hashAudit = Read-Json "phase-exec-live-r004-idempotency-audit-review.json"
if ($hashAudit.BatchIdempotencyHashPresent -ne $true -or
    $hashAudit.BatchAuditHashPresent -ne $true -or
    $hashAudit.ItemHashesPresent -ne $true) {
    Fail "Batch idempotency/audit hashes missing"
}

$flags = Read-Json "phase-exec-live-r004-kill-switch-feature-flag-review.json"
if ($flags.R009LiveTradingEnabled -ne $false -or
    $flags.R009BrokerRoutingEnabled -ne $false -or
    $flags.R009OrderSubmissionEnabled -ne $false -or
    $flags.R009ExecutableScheduleEnabled -ne $false -or
    $flags.R009PaperLedgerCommitEnabled -ne $false -or
    $flags.R009SchedulerEnabled -ne $false -or
    $flags.R009BackgroundWorkerEnabled -ne $false -or
    $flags.R009DryRunOnly -ne $true) {
    Fail "Kill-switch feature flags weakened"
}

$guard = Read-Json "phase-exec-live-r004-disabled-boundary-guard-review.json"
foreach ($property in @(
    "BrokerRouteCreationAllowed",
    "OrderCreationAllowed",
    "ChildSliceCreationAllowed",
    "ChildOrderCreationAllowed",
    "ScheduleExecutionAllowed",
    "SubmissionAllowed",
    "FillCreationAllowed",
    "ExecutionReportCreationAllowed",
    "StateMutationAllowed",
    "PaperLedgerCommitAllowed"
)) {
    if ($guard.$property -ne $false) { Fail "Disabled boundary guard weakened: $property" }
}

$legacy = Read-Json "phase-exec-live-r004-legacy-compatibility-preservation.json"
if ($legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 used as future canonical" }

$direct = Read-Json "phase-exec-live-r004-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed -ne $false) { Fail "Direct-cross execution allowed" }

$cost = Read-Json "phase-exec-live-r004-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }

$usdjpy = Read-Json "phase-exec-live-r004-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or
    $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or
    $usdjpy.RequiresInversion -ne $true -or
    $usdjpy.SecurityID -ne "4004" -or
    $usdjpy.SecurityIDSource -ne "8") {
    Fail "USDJPY caveat weakened"
}

$forbidden = Read-Json "phase-exec-live-r004-forbidden-actions-audit.json"
if ($forbidden.ExternalApiCallsMade -ne $false) { Fail "External API call recorded" }
if ($forbidden.BrokerLiveOrderRouteFillScheduleLedgerStatePathsEnabled -ne $false) { Fail "Broker/live/order path enabled" }
if ($forbidden.PmsEmsOmsCycleRun -ne $false) { Fail "PMS/EMS/OMS cycle was run" }
if ($forbidden.ManualNoExternalCommandRun -ne $false) { Fail "ManualNoExternal command was run" }
if ($forbidden.R009PromotedToExecutableUse -ne $false) { Fail "R009 promoted to executable use" }

$evidence = Read-Json "phase-exec-live-r004-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedR004Tests -ne "Passed") { Fail "Focused R004 test evidence missing or not passed" }
if ($evidence.UnitTests -ne "Passed") { Fail "Unit test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-LIVE-R004 validator passed."
