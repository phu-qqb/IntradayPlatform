param(
    [string]$ArtifactsDir = "artifacts/readiness/execution-sim"
)

$ErrorActionPreference = "Stop"

function Fail([string]$Message) {
    Write-Error $Message
    exit 1
}

function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Fail "Missing required artifact: $Path"
    }

    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-True($Object, [string]$Property, [string]$Message) {
    if ($Object.PSObject.Properties.Name -notcontains $Property) {
        Fail "Missing property '$Property': $Message"
    }

    if (-not [bool]$Object.$Property) {
        Fail $Message
    }
}

function Assert-False($Object, [string]$Property, [string]$Message) {
    if ($Object.PSObject.Properties.Name -notcontains $Property) {
        Fail "Missing property '$Property': $Message"
    }

    if ([bool]$Object.$Property) {
        Fail $Message
    }
}

$required = @(
    "phase-exec-paper-r006-summary.md",
    "phase-exec-paper-r006-r005-diagnostics-reference.json",
    "phase-exec-paper-r006-r009-contract-reference.json",
    "phase-exec-paper-r006-manual-noexternal-line-emission-gap-analysis.json",
    "phase-exec-paper-r006-line-level-artifact-emission-contract.json",
    "phase-exec-paper-r006-code-change-summary.json",
    "phase-exec-paper-r006-command-safety-check.json",
    "phase-exec-paper-r006-current-manual-paper-plan-generation-result.json",
    "phase-exec-paper-r006-output-artifact-inventory.json",
    "phase-exec-paper-r006-current-paper-execution-plan.json",
    "phase-exec-paper-r006-current-paper-execution-plan-lines.json",
    "phase-exec-paper-r006-lineage-preservation.json",
    "phase-exec-paper-r006-usd-pair-normalization-result.json",
    "phase-exec-paper-r006-symbol-inversion-result.json",
    "phase-exec-paper-r006-canonical-target-close-readiness.json",
    "phase-exec-paper-r006-readiness-binding-search-results.json",
    "phase-exec-paper-r006-risk-operator-approval-readiness.json",
    "phase-exec-paper-r006-current-input-readiness-result.json",
    "phase-exec-paper-r006-present-inputs-report.json",
    "phase-exec-paper-r006-missing-inputs-diagnostics.json",
    "phase-exec-paper-r006-r009-dryrun-handoff-contract.json",
    "phase-exec-paper-r006-r009-dryrun-handoff-package.json",
    "phase-exec-paper-r006-r009-design-only-preview-contract.json",
    "phase-exec-paper-r006-r009-design-only-preview-lines.json",
    "phase-exec-paper-r006-next-paper-dryrun-recommendation.json",
    "phase-exec-paper-r006-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r006-legacy-compatibility-preservation.json",
    "phase-exec-paper-r006-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r006-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r006-cost-guidance-preservation.json",
    "phase-exec-paper-r006-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r006-no-broker-activation-audit.json",
    "phase-exec-paper-r006-no-live-marketdata-audit.json",
    "phase-exec-paper-r006-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r006-no-executable-schedule-audit.json",
    "phase-exec-paper-r006-no-child-slices-audit.json",
    "phase-exec-paper-r006-no-child-orders-audit.json",
    "phase-exec-paper-r006-no-order-created-audit.json",
    "phase-exec-paper-r006-no-real-fill-audit.json",
    "phase-exec-paper-r006-no-execution-report-audit.json",
    "phase-exec-paper-r006-no-route-no-submission-audit.json",
    "phase-exec-paper-r006-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r006-no-polygon-api-call-audit.json",
    "phase-exec-paper-r006-no-lmax-call-audit.json",
    "phase-exec-paper-r006-no-external-api-call-audit.json",
    "phase-exec-paper-r006-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r006-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r006-no-external-audit.json",
    "phase-exec-paper-r006-forbidden-actions-audit.json",
    "phase-exec-paper-r006-build-test-validator-evidence.json"
)

foreach ($file in $required) {
    $path = Join-Path $ArtifactsDir $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required R006 artifact: $file"
    }
}

$gap = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-manual-noexternal-line-emission-gap-analysis.json")
$contract = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-line-level-artifact-emission-contract.json")
$safety = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-command-safety-check.json")
$generation = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-current-manual-paper-plan-generation-result.json")
$inventory = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-output-artifact-inventory.json")
$plan = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-current-paper-execution-plan.json")
$linesDoc = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-current-paper-execution-plan-lines.json")
$lineage = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-lineage-preservation.json")
$normalization = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-usd-pair-normalization-result.json")
$inversion = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-symbol-inversion-result.json")
$canonical = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-canonical-target-close-readiness.json")
$bindings = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-readiness-binding-search-results.json")
$risk = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-risk-operator-approval-readiness.json")
$readiness = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-current-input-readiness-result.json")
$missing = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-missing-inputs-diagnostics.json")
$handoff = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-r009-dryrun-handoff-package.json")
$preview = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-r009-design-only-preview-lines.json")
$r009 = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-r009-contract-reference.json")
$legacy = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-legacy-compatibility-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-cost-guidance-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-usdjpy-caveat-preservation.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r006-build-test-validator-evidence.json")

Assert-True $gap "GapIdentified" "R006 gap analysis did not identify the summary-only output gap."
Assert-True $gap "NoInventedPlanLines" "Gap analysis must preserve no invented plan lines."
Assert-True $contract "EmitsCurrentPaperExecutionPlan" "Line-level contract does not emit a current paper execution plan."
Assert-True $contract "EmitsCurrentPaperExecutionPlanLines" "Line-level contract does not emit current paper execution plan lines."
Assert-True $contract "DerivedFromQubesFixtureAndManualNoExternalPipeline" "Line-level artifacts must derive from the Qubes fixture and pipeline."
Assert-True $contract "NonExecutable" "Line-level contract must be non-executable."
Assert-True $contract "NotAnOrder" "Line-level contract must not be an order."
Assert-True $contract "NoBrokerRoute" "Line-level contract must not create broker routes."

Assert-True $safety "SafeCommand" "ManualNoExternal command safety failed."
Assert-True $safety "UsesManualNoExternal" "Command does not use ManualNoExternal."
Assert-True $safety "IncludesNoPaperLedgerCommitTrue" "Command omits --no-paper-ledger-commit true."
Assert-True $safety "ExactlyOneSuccessfulPostFixInvocation" "R006 must have exactly one successful post-fix invocation."
if (-not [bool]$safety.NoBroker) { Fail "NoBroker must be true." }
if (-not [bool]$safety.NoExternal) { Fail "NoExternal must be true." }
if (-not [bool]$safety.NoOrders) { Fail "NoOrders must be true." }
if (-not [bool]$safety.NoFills) { Fail "NoFills must be true." }
if (-not [bool]$safety.NoRoutes) { Fail "NoRoutes must be true." }
if (-not [bool]$safety.NoSubmissions) { Fail "NoSubmissions must be true." }

Assert-True $generation "CommandExecuted" "ManualNoExternal generation did not run."
if ([int]$generation.InvocationCount -ne 1) {
    Fail "ManualNoExternal generation must run exactly once."
}
if ([int]$generation.ExitCode -ne 0) {
    Fail "ManualNoExternal generation failed."
}
if ($generation.CliStatus -ne "CompletedNoExternal") {
    Fail "ManualNoExternal generation did not complete no-external."
}
Assert-True $generation "NoExternal" "Generation was not no-external."
Assert-True $generation "NoPaperLedgerCommit" "Paper ledger commit occurred."
Assert-False $generation "CreatedOrder" "Order was created."
Assert-False $generation "CreatedFill" "Fill was created."
Assert-False $generation "CreatedExecutionReport" "Execution report was created."
Assert-False $generation "CreatedRoute" "Route was created."
Assert-False $generation "SubmittedOrder" "Submission occurred."
Assert-True $generation "ProducedCurrentPaperExecutionPlan" "Current paper execution plan was not produced."
Assert-True $generation "ProducedCurrentPaperExecutionPlanLines" "Current plan lines were not produced."

Assert-True $inventory "ContainsCurrentPaperExecutionPlan" "Output inventory missing current paper execution plan."
Assert-True $inventory "ContainsCurrentPaperExecutionPlanLines" "Output inventory missing current paper execution plan lines."
if ([int]$plan.LineCount -lt 1) {
    Fail "Line-level artifacts are missing after claimed fix."
}
if ([int]$plan.LineCount -ne @($linesDoc.Lines).Count) {
    Fail "Plan line count does not match emitted line count."
}

Assert-True $plan "DerivedFromQubesFixtureAndManualNoExternalPipeline" "Plan must be fixture/pipeline derived."
Assert-True $plan "DirectCrossExecutionDisabled" "Direct-cross exclusion weakened."
Assert-True $plan "USDPairNormalizedOnly" "Plan must contain USD-pair normalized outputs only."
Assert-True $plan "NonExecutable" "Plan became executable."
Assert-True $plan "NotAnOrder" "Plan is represented as an order."
Assert-True $plan "NoBrokerRoute" "Plan created broker route."
Assert-True $plan "NoPaperLedgerCommit" "Plan committed paper ledger."

Assert-True $lineage "DerivedFromAcceptedQubesFixture" "Lineage did not preserve accepted Qubes fixture."
Assert-True $lineage "DerivedFromManualNoExternalPipeline" "Lineage did not preserve ManualNoExternal pipeline derivation."
Assert-False $lineage "HistoricalR013R015LinesUsed" "Historical plan lines were reused as current."
Assert-False $lineage "PlanLinesInvented" "Current paper plan lines were invented."

$allowedNormalized = @("EURUSD", "JPYUSD", "AUDUSD", "GBPUSD", "NZDUSD", "CADUSD", "CHFUSD")
$allowedExecution = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
foreach ($line in @($linesDoc.Lines)) {
    if ($allowedNormalized -notcontains [string]$line.NormalizedPortfolioSymbol) {
        Fail "Unexpected normalized portfolio symbol: $($line.NormalizedPortfolioSymbol)"
    }
    if ($allowedExecution -notcontains [string]$line.ExecutionTradableSymbol) {
        Fail "Unexpected execution tradable symbol: $($line.ExecutionTradableSymbol)"
    }
    if ([bool]$line.NonExecutable -ne $true) { Fail "Line became executable: $($line.PaperExecutionPlanLineId)" }
    if ([bool]$line.NotAnOrder -ne $true) { Fail "Line represented an order: $($line.PaperExecutionPlanLineId)" }
    if ([bool]$line.NotSubmitted -ne $true) { Fail "Line was submitted: $($line.PaperExecutionPlanLineId)" }
    if ([bool]$line.NoBrokerRoute -ne $true) { Fail "Line created broker route: $($line.PaperExecutionPlanLineId)" }
    if ([bool]$line.NoChildSlices -ne $true) { Fail "Line created child slices: $($line.PaperExecutionPlanLineId)" }
    if ([bool]$line.NoExecutableSchedule -ne $true) { Fail "Line created executable schedule: $($line.PaperExecutionPlanLineId)" }
    if ([bool]$line.NoFill -ne $true) { Fail "Line created fill: $($line.PaperExecutionPlanLineId)" }
    if ([bool]$line.NoExecutionReport -ne $true) { Fail "Line created execution report: $($line.PaperExecutionPlanLineId)" }
    if ([bool]$line.NoRoute -ne $true) { Fail "Line created route: $($line.PaperExecutionPlanLineId)" }
    if ([bool]$line.NoSubmission -ne $true) { Fail "Line created submission: $($line.PaperExecutionPlanLineId)" }
    if ([bool]$line.NoPaperLedgerCommit -ne $true) { Fail "Line committed paper ledger: $($line.PaperExecutionPlanLineId)" }
    if ($line.CanonicalTargetCloseTimestamp -ne $null) {
        $minute = ([datetimeoffset]$line.CanonicalTargetCloseTimestamp).Minute
        if (@(0, 15, 30, 45) -notcontains $minute) {
            Fail "Non-canonical target close minute found: $($line.CanonicalTargetCloseTimestamp)"
        }
    }
}

Assert-True $normalization "USDPairNormalizedOnly" "USD-pair normalization weakened."
Assert-True $normalization "DirectCrossExecutionDisabled" "Direct-cross execution was enabled."
Assert-True $normalization "AUDUSDNotFailed" "AUDUSD was misclassified."
Assert-True $inversion.USDJPY "RequiresInversion" "USDJPY inversion caveat weakened."
if ($inversion.USDJPY.NormalizedPortfolioSymbol -ne "JPYUSD" -or $inversion.USDJPY.ExecutionTradableSymbol -ne "USDJPY") {
    Fail "USDJPY normalized/execution symbol caveat weakened."
}
if ($inversion.USDJPY.SecurityID -ne "4004" -or $inversion.USDJPY.SecurityIDSource -ne "8") {
    Fail "USDJPY SecurityID caveat weakened."
}

Assert-False $canonical "Legacy06UsedAsFutureCanonical" "Legacy :06 used as future canonical."
Assert-True $canonical "MissingCanonicalTargetClose" "Missing target close must remain explicit."
Assert-False $bindings "QuoteWindowReadinessBindingFound" "Unexpected quote-window readiness binding found."
Assert-False $bindings "CloseBenchmarkReadinessBindingFound" "Unexpected close-benchmark readiness binding found."
Assert-False $bindings "FeedQualityReadinessBindingFound" "Unexpected feed-quality readiness binding found."
Assert-True $bindings "MissingReadinessBinding" "Missing readiness binding not flagged."
Assert-False $risk "RiskReviewFound" "Unexpected risk review found."
Assert-False $risk "OperatorApprovalFound" "Unexpected operator approval found."

Assert-True $readiness "CurrentPaperExecutionPlanLinesReady" "Current paper execution plan lines must be ready."
Assert-True $readiness "CurrentPaperExecutionPlanLinesPartiallyReady" "Current paper execution plan lines must be partial-ready."
Assert-False $readiness "ReadyForR009Handoff" "R009 handoff should be blocked."
Assert-False $readiness "ReadyForR009DesignOnlyPreview" "R009 preview should be blocked."
Assert-True $missing "MissingInputsRemain" "Missing diagnostics must remain."
Assert-True $missing "CurrentPlanLinesExist" "Diagnostics should acknowledge current plan lines exist."

Assert-False $handoff "HandoffPackageReady" "R009 handoff package must not be ready without bindings."
if ([int]$handoff.LineCount -ne 0) {
    Fail "Handoff lines were created despite missing R009 readiness inputs."
}
Assert-False $preview "PreviewReady" "R009 preview must not be ready without bindings."
if ([int]$preview.PreviewLineCount -ne 0) {
    Fail "Preview lines were created despite missing R009 readiness inputs."
}
Assert-True $preview "NoOrdersRepresented" "Preview represented orders."
Assert-True $preview "NoSchedulesRepresented" "Preview represented schedules."
Assert-True $preview "NoFillsRepresented" "Preview represented fills."
Assert-True $preview "NoRoutesRepresented" "Preview represented routes."

Assert-True $r009 "DesignOnly" "R009 design-only status weakened."
Assert-True $r009 "NonExecutable" "R009 became executable."
Assert-False $r009 "ExecutablePromotionAuthorized" "R009 executable promotion authorized."
Assert-False $legacy "Legacy06UsedAsFutureCanonical" "Legacy compatibility policy weakened."
Assert-True $directCross "DirectCrossExecutionDisabled" "Direct-cross exclusion weakened."
Assert-False $directCross "DirectCrossExecutionLinesCreated" "Direct-cross execution lines were created."
Assert-False $cost "FiveUsdPerMillionUniversalized" "5 USD/million was universalized."
Assert-True $cost "FiveUsdPerMillionBestCaseMajorOnly" "5 USD/million caveat weakened."
Assert-True $usdjpy "USDJPYCaveatPreserved" "USDJPY caveat weakened."
Assert-False $forbidden "ProhibitedActionsDetected" "Forbidden action detected."
Assert-False $forbidden "ExternalApiCalled" "External API call detected."
Assert-False $forbidden "BrokerActivation" "Broker activation detected."
Assert-False $forbidden "LiveMarketData" "Live market data detected."
Assert-False $forbidden "SchedulerServicePolling" "Scheduler/service/polling detected."
Assert-False $forbidden "ExecutableSchedule" "Executable schedule created."
Assert-False $forbidden "ChildSlices" "Child slices created."
Assert-False $forbidden "ChildOrders" "Child orders created."
Assert-False $forbidden "Orders" "Orders created."
Assert-False $forbidden "Fills" "Fills created."
Assert-False $forbidden "ExecutionReports" "Execution reports created."
Assert-False $forbidden "Routes" "Routes created."
Assert-False $forbidden "Submissions" "Submissions created."
Assert-False $forbidden "PaperLedgerCommit" "Paper ledger commit created."
Assert-False $forbidden "StateMutation" "State mutation detected."
Assert-False $forbidden "R009PromotedExecutable" "R009 promoted to executable."
Assert-False $forbidden "AUDUSDMisclassified" "AUDUSD misclassified."

if ($evidence.PSObject.Properties.Name -notcontains "BuildTestsValidatorEvidencePresent" -or -not [bool]$evidence.BuildTestsValidatorEvidencePresent) {
    Fail "Build/tests/validator evidence missing."
}

Write-Host "EXEC-PAPER-R006 validator passed."
