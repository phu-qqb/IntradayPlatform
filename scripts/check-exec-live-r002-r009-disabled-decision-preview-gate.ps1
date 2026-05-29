$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-live"

function Fail {
    param([string]$Message)
    throw "EXEC-LIVE-R002 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing artifact $Name"
    }

    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-live-r002-summary.md",
    "phase-exec-live-r002-r001-scaffold-reference.json",
    "phase-exec-live-r002-r009-contract-reference.json",
    "phase-exec-live-r002-source-paper-plan-input-reference.json",
    "phase-exec-live-r002-input-selection-result.json",
    "phase-exec-live-r002-execution-intent-conversion-contract.json",
    "phase-exec-live-r002-execution-intents.json",
    "phase-exec-live-r002-disabled-adapter-decision-preview-contract.json",
    "phase-exec-live-r002-r009-decision-previews.json",
    "phase-exec-live-r002-decision-preview-coverage.json",
    "phase-exec-live-r002-held-readiness-decision-review.json",
    "phase-exec-live-r002-per-symbol-decision-review.json",
    "phase-exec-live-r002-bar-role-decision-review.json",
    "phase-exec-live-r002-direct-cross-exclusion-review.json",
    "phase-exec-live-r002-inversion-review.json",
    "phase-exec-live-r002-r009-policy-selection-review.json",
    "phase-exec-live-r002-disabled-boundary-guard-review.json",
    "phase-exec-live-r002-kill-switch-feature-flag-review.json",
    "phase-exec-live-r002-idempotency-audit-review.json",
    "phase-exec-live-r002-no-broker-activation-audit.json",
    "phase-exec-live-r002-no-live-marketdata-audit.json",
    "phase-exec-live-r002-no-scheduler-service-polling-audit.json",
    "phase-exec-live-r002-no-order-created-audit.json",
    "phase-exec-live-r002-no-child-order-audit.json",
    "phase-exec-live-r002-no-executable-schedule-audit.json",
    "phase-exec-live-r002-no-route-no-submission-audit.json",
    "phase-exec-live-r002-no-fill-execution-report-audit.json",
    "phase-exec-live-r002-no-paper-ledger-commit-audit.json",
    "phase-exec-live-r002-no-state-mutation-audit.json",
    "phase-exec-live-r002-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-live-r002-legacy-compatibility-preservation.json",
    "phase-exec-live-r002-direct-cross-exclusion-preservation.json",
    "phase-exec-live-r002-usd-pair-netting-requirement.json",
    "phase-exec-live-r002-usdjpy-caveat-preservation.json",
    "phase-exec-live-r002-cost-guidance-preservation.json",
    "phase-exec-live-r002-nonmajor-calibration-preservation.json",
    "phase-exec-live-r002-no-external-audit.json",
    "phase-exec-live-r002-forbidden-actions-audit.json",
    "phase-exec-live-r002-next-phase-recommendation.json",
    "phase-exec-live-r002-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$sourcePath = Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009EmsOmsDisabledScaffold.cs"
$testPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009DisabledDecisionPreviewIntegrationTests.cs"
if (-not (Test-Path -LiteralPath $sourcePath)) { Fail "Missing R009 scaffold source file" }
if (-not (Test-Path -LiteralPath $testPath)) { Fail "Missing focused R002 tests" }

$source = Get-Content -LiteralPath $sourcePath -Raw
if ($source -notmatch "R009PaperPlanExecutionIntentConverter") { Fail "Paper plan to execution intent converter missing" }
if ($source -notmatch "R009DisabledDecisionPreviewIntegrationService") { Fail "Disabled decision preview integration service missing" }
if ($source -notmatch "LiveTradingEnabled: false") { Fail "Intent conversion does not force live trading disabled" }
if ($source -notmatch "BrokerRoutingEnabled: false") { Fail "Intent conversion does not force broker routing disabled" }
if ($source -notmatch "OrderSubmissionEnabled: false") { Fail "Intent conversion does not force order submission disabled" }

$contract = Read-Json "phase-exec-live-r002-r009-contract-reference.json"
if ($contract.ExecutablePromotionAuthorized -ne $false) { Fail "R009 executable promotion was authorized" }
if ($contract.BrokerReady -ne $false -or $contract.LiveReady -ne $false) { Fail "R009 broker/live readiness was set true" }
if ($contract.NonExecutable -ne $true -or $contract.NotAnOrder -ne $true -or $contract.NoBrokerRoute -ne $true) {
    Fail "R009 non-executable contract flags weakened"
}

$inputSelection = Read-Json "phase-exec-live-r002-input-selection-result.json"
if ($inputSelection.MissingInputBlocked -ne $false) { Fail "Input selection is blocked" }
if ($inputSelection.SelectedLineCount -le 0) { Fail "No paper preview input lines selected" }

$intents = Read-Json "phase-exec-live-r002-execution-intents.json"
$decisions = Read-Json "phase-exec-live-r002-r009-decision-previews.json"
if ($intents.IntentCount -le 0) { Fail "No execution intents produced" }
if ($decisions.DecisionPreviewCount -ne $intents.IntentCount) { Fail "Decision preview count does not match intent count" }

foreach ($intent in @($intents.Intents)) {
    if ($intent.LiveTradingEnabled -ne $false) { Fail "Intent enables live trading: $($intent.ExecutionIntentId)" }
    if ($intent.BrokerRoutingEnabled -ne $false) { Fail "Intent enables broker routing: $($intent.ExecutionIntentId)" }
    if ($intent.OrderSubmissionEnabled -ne $false) { Fail "Intent enables order submission: $($intent.ExecutionIntentId)" }
    if ($intent.NonExecutable -ne $true) { Fail "Intent is executable: $($intent.ExecutionIntentId)" }
    $minute = ([DateTimeOffset]::Parse($intent.CanonicalTargetCloseUtc)).Minute
    if ($minute -notin @(0, 15, 30, 45)) { Fail "Intent target close is not quarter-hour: $($intent.ExecutionIntentId)" }
    if ($intent.CanonicalTargetCloseLocal -match ":06:" -or $intent.CanonicalTargetCloseLocal -match ":21:" -or $intent.CanonicalTargetCloseLocal -match ":36:" -or $intent.CanonicalTargetCloseLocal -match ":51:") {
        Fail "Intent uses legacy timestamp as future canonical: $($intent.ExecutionIntentId)"
    }
}

foreach ($decision in @($decisions.DecisionPreviews)) {
    if ($decision.NonExecutable -ne $true -or
        $decision.NotAnOrder -ne $true -or
        $decision.NotSubmitted -ne $true -or
        $decision.NoBrokerRoute -ne $true -or
        $decision.NoFill -ne $true -or
        $decision.NoExecutionReport -ne $true -or
        $decision.NoRoute -ne $true -or
        $decision.NoSubmission -ne $true -or
        $decision.NoPaperLedgerCommit -ne $true) {
        Fail "Decision preview non-executable safety flags weakened: $($decision.DecisionId)"
    }

    if ($decision.CreatesOrder -ne $false -or
        $decision.CreatesChildOrder -ne $false -or
        $decision.CreatesRoute -ne $false -or
        $decision.CreatesSubmission -ne $false -or
        $decision.CreatesFill -ne $false -or
        $decision.CreatesExecutionReport -ne $false -or
        $decision.CreatesExecutableSchedule -ne $false) {
        Fail "Decision preview creates executable/order-like output: $($decision.DecisionId)"
    }

    foreach ($output in @($decision.Outputs)) {
        if ($output -in @("Order", "ChildOrder", "Route", "Submission", "Fill", "ExecutionReport", "ExecutableSchedule")) {
            Fail "Decision preview represented as forbidden output $output"
        }
    }

    if ([string]::IsNullOrWhiteSpace($decision.Audit.R009DecisionHash) -or [string]::IsNullOrWhiteSpace($decision.Audit.InputHash)) {
        Fail "Decision preview missing idempotency hash: $($decision.DecisionId)"
    }
}

$decisionContract = Read-Json "phase-exec-live-r002-disabled-adapter-decision-preview-contract.json"
foreach ($forbidden in @("Order", "ChildOrder", "Route", "Submission", "Fill", "ExecutionReport", "ExecutableSchedule")) {
    if (@($decisionContract.ForbiddenOutputs) -notcontains $forbidden) {
        Fail "Decision preview contract omits forbidden output $forbidden"
    }
}

$flags = Read-Json "phase-exec-live-r002-kill-switch-feature-flag-review.json"
if ($flags.R009LiveTradingEnabled -ne $false) { Fail "R009 live trading flag defaults true" }
if ($flags.R009BrokerRoutingEnabled -ne $false) { Fail "R009 broker routing flag defaults true" }
if ($flags.R009OrderSubmissionEnabled -ne $false) { Fail "R009 order submission flag defaults true" }
if ($flags.R009ExecutableScheduleEnabled -ne $false) { Fail "R009 executable schedule flag defaults true" }
if ($flags.R009PaperLedgerCommitEnabled -ne $false) { Fail "R009 paper ledger commit flag defaults true" }
if ($flags.R009SchedulerEnabled -ne $false -or $flags.R009BackgroundWorkerEnabled -ne $false) { Fail "Scheduler/background flag enabled" }
if ($flags.R009DryRunOnly -ne $true) { Fail "DryRunOnly flag is not true" }

$guard = Read-Json "phase-exec-live-r002-disabled-boundary-guard-review.json"
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
    if ($guard.$property -ne $false) {
        Fail "Disabled boundary guard weakened: $property"
    }
}

$directCross = Read-Json "phase-exec-live-r002-direct-cross-exclusion-review.json"
if ($directCross.DirectCrossExecutionAllowed -ne $false) { Fail "Direct-cross execution is allowed" }
if ($directCross.DirectCrossExecutableDecisionCount -ne 0) { Fail "Direct-cross executable decisions found" }

$legacy = Read-Json "phase-exec-live-r002-legacy-compatibility-preservation.json"
if ($legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 used as future canonical" }

$cost = Read-Json "phase-exec-live-r002-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million was universalized" }

$usdjpy = Read-Json "phase-exec-live-r002-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or
    $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or
    $usdjpy.RequiresInversion -ne $true -or
    $usdjpy.SecurityID -ne "4004" -or
    $usdjpy.SecurityIDSource -ne "8") {
    Fail "USDJPY caveat weakened"
}

$symbolReview = Read-Json "phase-exec-live-r002-per-symbol-decision-review.json"
if ($symbolReview.AudusdStatus -ne "SupportedAndNotFailed") { Fail "AUDUSD misclassified" }

$forbiddenActions = Read-Json "phase-exec-live-r002-forbidden-actions-audit.json"
if ($forbiddenActions.ExternalApiCallsMade -ne $false) { Fail "External API call recorded" }
if ($forbiddenActions.BrokerLiveOrderRouteFillScheduleLedgerStatePathsEnabled -ne $false) { Fail "Broker/live/order path enabled" }
if ($forbiddenActions.PmsEmsOmsCycleRun -ne $false) { Fail "PMS/EMS/OMS cycle was run" }
if ($forbiddenActions.ManualNoExternalCommandRun -ne $false) { Fail "ManualNoExternal command was run" }
if ($forbiddenActions.R009PromotedToExecutableUse -ne $false) { Fail "R009 promoted to executable use" }

$evidence = Read-Json "phase-exec-live-r002-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedR002Tests -ne "Passed") { Fail "Focused R002 test evidence missing or not passed" }
if ($evidence.UnitTests -ne "Passed") { Fail "Unit test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-LIVE-R002 validator passed."
