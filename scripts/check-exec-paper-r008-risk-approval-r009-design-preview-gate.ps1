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
    "phase-exec-paper-r008-summary.md",
    "phase-exec-paper-r008-r007-handoff-reference.json",
    "phase-exec-paper-r008-r006-plan-line-reference.json",
    "phase-exec-paper-r008-r009-contract-reference.json",
    "phase-exec-paper-r008-risk-review-for-design-preview.json",
    "phase-exec-paper-r008-operator-approval-for-design-preview.json",
    "phase-exec-paper-r008-readiness-binding-confirmation.json",
    "phase-exec-paper-r008-r009-dryrun-handoff-contract.json",
    "phase-exec-paper-r008-r009-dryrun-handoff-package.json",
    "phase-exec-paper-r008-r009-design-only-preview-contract.json",
    "phase-exec-paper-r008-r009-design-only-preview-lines.json",
    "phase-exec-paper-r008-readiness-result.json",
    "phase-exec-paper-r008-missing-inputs-diagnostics.json",
    "phase-exec-paper-r008-next-paper-dryrun-recommendation.json",
    "phase-exec-paper-r008-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r008-legacy-compatibility-preservation.json",
    "phase-exec-paper-r008-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r008-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r008-cost-guidance-preservation.json",
    "phase-exec-paper-r008-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r008-no-broker-activation-audit.json",
    "phase-exec-paper-r008-no-live-marketdata-audit.json",
    "phase-exec-paper-r008-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r008-no-executable-schedule-audit.json",
    "phase-exec-paper-r008-no-child-slices-audit.json",
    "phase-exec-paper-r008-no-child-orders-audit.json",
    "phase-exec-paper-r008-no-order-created-audit.json",
    "phase-exec-paper-r008-no-real-fill-audit.json",
    "phase-exec-paper-r008-no-execution-report-audit.json",
    "phase-exec-paper-r008-no-route-no-submission-audit.json",
    "phase-exec-paper-r008-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r008-no-polygon-api-call-audit.json",
    "phase-exec-paper-r008-no-lmax-call-audit.json",
    "phase-exec-paper-r008-no-external-api-call-audit.json",
    "phase-exec-paper-r008-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r008-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r008-no-external-audit.json",
    "phase-exec-paper-r008-forbidden-actions-audit.json",
    "phase-exec-paper-r008-build-test-validator-evidence.json"
)

foreach ($file in $required) {
    $path = Join-Path $ArtifactsDir $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required R008 artifact: $file"
    }
}

$r007 = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r008-r007-handoff-reference.json")
$r006 = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r008-r006-plan-line-reference.json")
$r009 = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r008-r009-contract-reference.json")
$risk = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r008-risk-review-for-design-preview.json")
$approval = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r008-operator-approval-for-design-preview.json")
$binding = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r008-readiness-binding-confirmation.json")
$handoff = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r008-r009-dryrun-handoff-package.json")
$preview = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r008-r009-design-only-preview-lines.json")
$readiness = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r008-readiness-result.json")
$missing = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r008-missing-inputs-diagnostics.json")
$legacy = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r008-legacy-compatibility-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r008-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r008-cost-guidance-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r008-usdjpy-caveat-preservation.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r008-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r008-build-test-validator-evidence.json")

Assert-True $r007 "R007ReadinessBindingsComplete" "R007 readiness bindings were not complete."
Assert-True $r007 "R007MissingOnlyRiskOperatorApproval" "R008 should only resolve risk/operator approval."
if ([int]$r007.R007LineCount -ne 7) { Fail "R007 line count must be 7." }
Assert-True $r006 "CurrentLineLevelPaperExecutionPlanLinesPresent" "R006 current plan lines missing."
if ([int]$r006.LineCount -ne 7) { Fail "R006 line count must be 7." }
Assert-False $r006 "LinesInvented" "Paper execution plan lines were invented."

Assert-True $r009 "DesignOnly" "R009 design-only status weakened."
Assert-True $r009 "PaperOnly" "R009 paper-only status weakened."
Assert-True $r009 "NonExecutable" "R009 became executable."
Assert-True $r009 "NotAnOrder" "R009 represented an order."
Assert-False $r009 "ExecutablePromotionAuthorized" "R009 executable promotion authorized."

if ($risk.RiskReviewScope -ne "R009DesignOnlyPreviewOnly") { Fail "Risk review scope is not preview-only." }
if ($risk.RiskStatus -ne "ApprovedForNonExecutablePreview") { Fail "Risk review status is not ApprovedForNonExecutablePreview." }
Assert-True $risk "RiskApprovalDoesNotAuthorizeExecution" "Risk review authorizes execution."
Assert-True $risk "RiskApprovalDoesNotAuthorizeOrders" "Risk review authorizes orders."
Assert-True $risk "RiskApprovalDoesNotAuthorizeRoutes" "Risk review authorizes routes."
Assert-True $risk "RiskApprovalDoesNotAuthorizeLedgerCommit" "Risk review authorizes ledger commit."
Assert-False $risk "ApprovedForExecutableUse" "Risk review approved executable use."
Assert-False $risk "ApprovedForOrderCreation" "Risk review approved order creation."
Assert-False $risk "ApprovedForBrokerRouting" "Risk review approved broker routing."
Assert-False $risk "ApprovedForSubmission" "Risk review approved submission."
Assert-False $risk "ApprovedForFillOrExecutionReport" "Risk review approved fills/reports."
Assert-False $risk "ApprovedForPaperLedgerCommit" "Risk review approved paper ledger commit."
Assert-False $risk "ApprovedForStateMutation" "Risk review approved state mutation."
Assert-False $risk "ApprovedForLiveTrading" "Risk review approved live trading."
Assert-True $risk "ApprovedForPreviewOnly" "Risk review did not approve preview-only."

if ($approval.ApprovalScope -ne "R009DesignOnlyPreviewOnly") { Fail "Operator approval scope is not preview-only." }
if ($approval.OperatorApprovalStatus -ne "ApprovedForDesignOnlyPreviewOnly") { Fail "Operator approval status is not preview-only." }
Assert-False $approval "ApprovedForExecutableUse" "Operator approval approved executable use."
Assert-False $approval "ApprovedForOrderCreation" "Operator approval approved order creation."
Assert-False $approval "ApprovedForScheduleCreation" "Operator approval approved schedule creation."
Assert-False $approval "ApprovedForChildSlices" "Operator approval approved child slices."
Assert-False $approval "ApprovedForBrokerRouting" "Operator approval approved broker routing."
Assert-False $approval "ApprovedForSubmission" "Operator approval approved submission."
Assert-False $approval "ApprovedForFillOrExecutionReport" "Operator approval approved fills/reports."
Assert-False $approval "ApprovedForPaperLedgerCommit" "Operator approval approved ledger commit."
Assert-False $approval "ApprovedForStateMutation" "Operator approval approved state mutation."
Assert-False $approval "ApprovedForLiveTrading" "Operator approval approved live trading."
Assert-True $approval "ApprovedForPreviewOnly" "Operator approval did not approve preview-only."

Assert-True $binding "AllReadinessBindingsPresent" "Readiness bindings are missing."
if ([int]$binding.QuoteWindowReadinessBindingsFound -ne 7 -or [int]$binding.CloseBenchmarkReadinessBindingsFound -ne 7 -or [int]$binding.FeedQualityReadinessBindingsFound -ne 7) {
    Fail "Expected 7 readiness bindings in each family."
}
Assert-True $binding "CanonicalQuarterHourConfirmed" "Canonical target close not confirmed."

Assert-True $handoff "HandoffPackageReady" "R009 handoff package is not ready."
if ([int]$handoff.LineCount -ne 7) { Fail "Handoff package must contain 7 lines." }
foreach ($line in @($handoff.Lines)) {
    if ($line.RiskReviewStatus -ne "ApprovedForNonExecutablePreview") { Fail "Handoff line risk status not preview-approved." }
    if ($line.OperatorApprovalStatus -ne "ApprovedForDesignOnlyPreviewOnly") { Fail "Handoff line operator status not preview-approved." }
    if ($line.QuoteWindowReadinessBinding -eq $null -or $line.CloseBenchmarkReadinessBinding -eq $null -or $line.FeedQualityReadinessBinding -eq $null) { Fail "Handoff line missing readiness binding." }
    if ([bool]$line.NonExecutable -ne $true) { Fail "Handoff line became executable." }
    if ([bool]$line.NotAnOrder -ne $true) { Fail "Handoff line represented an order." }
    if ([bool]$line.NotSubmitted -ne $true) { Fail "Handoff line was submitted." }
    if ([bool]$line.NoBrokerRoute -ne $true) { Fail "Handoff line created broker route." }
    if ([bool]$line.NoChildSlices -ne $true) { Fail "Handoff line created child slices." }
    if ([bool]$line.NoExecutableSchedule -ne $true) { Fail "Handoff line created executable schedule." }
    if ([bool]$line.NoFill -ne $true) { Fail "Handoff line created fill." }
    if ([bool]$line.NoExecutionReport -ne $true) { Fail "Handoff line created execution report." }
    if ([bool]$line.NoRoute -ne $true) { Fail "Handoff line created route." }
    if ([bool]$line.NoSubmission -ne $true) { Fail "Handoff line created submission." }
    if ([bool]$line.NoPaperLedgerCommit -ne $true) { Fail "Handoff line committed paper ledger." }
}

Assert-True $preview "PreviewReady" "R009 design-only preview is not ready."
if ([int]$preview.PreviewLineCount -ne 7) { Fail "Preview must contain 7 lines." }
Assert-True $preview "NoOrdersRepresented" "Preview represented orders."
Assert-True $preview "NoSchedulesRepresented" "Preview represented schedules."
Assert-True $preview "NoFillsRepresented" "Preview represented fills."
Assert-True $preview "NoRoutesRepresented" "Preview represented routes."
foreach ($line in @($preview.Lines)) {
    if ($line.SelectedDesignPolicy -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0") { Fail "Preview selected wrong primary policy." }
    Assert-True $line "DesignOnlyPreview" "Preview line not marked design-only."
    Assert-True $line "RiskApprovedForPreviewOnly" "Preview line missing risk preview approval."
    Assert-True $line "OperatorApprovedForPreviewOnly" "Preview line missing operator preview approval."
    Assert-True $line "NonExecutable" "Preview line became executable."
    Assert-True $line "NotAnOrder" "Preview line represented an order."
    Assert-True $line "NotSubmitted" "Preview line was submitted."
    Assert-True $line "NoBrokerRoute" "Preview line created broker route."
    Assert-True $line "NoChildSlices" "Preview line created child slices."
    Assert-True $line "NoExecutableSchedule" "Preview line created executable schedule."
    Assert-True $line "NoFill" "Preview line created fill."
    Assert-True $line "NoExecutionReport" "Preview line created execution report."
    Assert-True $line "NoRoute" "Preview line created route."
    Assert-True $line "NoSubmission" "Preview line created submission."
    Assert-True $line "NoPaperLedgerCommit" "Preview line committed paper ledger."
}

Assert-True $readiness "RiskReviewApprovedForDesignOnlyPreview" "Readiness result missing risk approval."
Assert-True $readiness "OperatorApprovalGrantedForDesignOnlyPreview" "Readiness result missing operator approval."
Assert-True $readiness "HandoffPackageReady" "Readiness result missing handoff ready."
Assert-True $readiness "DesignOnlyPreviewReady" "Readiness result missing preview ready."
Assert-False $readiness "MissingInputsRemain" "Missing inputs remain unexpectedly."
Assert-True $readiness "NoExecutablePromotion" "Executable promotion occurred."
Assert-False $missing "MissingInputsRemain" "Missing input diagnostics should be clear."

Assert-False $legacy "Legacy06UsedAsFutureCanonical" "Legacy :06 used as future canonical."
Assert-True $directCross "DirectCrossExecutionDisabled" "Direct-cross exclusion weakened."
Assert-False $directCross "DirectCrossExecutionLinesCreated" "Direct-cross execution lines created."
Assert-False $cost "FiveUsdPerMillionUniversalized" "5 USD/million universalized."
Assert-True $usdjpy "USDJPYCaveatPreserved" "USDJPY caveat weakened."
Assert-False $forbidden "ProhibitedActionsDetected" "Forbidden actions detected."
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
Assert-False $forbidden "PaperLedgerCommit" "Paper ledger commit occurred."
Assert-False $forbidden "StateMutation" "State mutation occurred."
Assert-False $forbidden "PreviewApprovalTreatedAsExecutableApproval" "Preview approval treated as executable approval."
Assert-False $forbidden "RiskReviewAuthorizesExecution" "Risk review authorizes execution."
Assert-False $forbidden "RiskReviewAuthorizesOrders" "Risk review authorizes orders."
Assert-False $forbidden "RiskReviewAuthorizesRoutes" "Risk review authorizes routes."
Assert-False $forbidden "RiskReviewAuthorizesLedgerCommit" "Risk review authorizes ledger commit."
Assert-False $forbidden "R009PromotedExecutable" "R009 promoted executable."
Assert-False $forbidden "PreviewLinesRepresentOrders" "Preview represented orders."
Assert-False $forbidden "AUDUSDMisclassified" "AUDUSD misclassified."

if ($evidence.PSObject.Properties.Name -notcontains "BuildTestsValidatorEvidencePresent" -or -not [bool]$evidence.BuildTestsValidatorEvidencePresent) {
    Fail "Build/tests/validator evidence missing."
}

Write-Host "EXEC-PAPER-R008 validator passed."
