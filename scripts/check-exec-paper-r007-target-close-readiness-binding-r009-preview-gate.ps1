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
    "phase-exec-paper-r007-summary.md",
    "phase-exec-paper-r007-r006-plan-line-reference.json",
    "phase-exec-paper-r007-r009-contract-reference.json",
    "phase-exec-paper-r007-operator-target-close-input.json",
    "phase-exec-paper-r007-canonical-target-close-binding.json",
    "phase-exec-paper-r007-canonical-session-binding.json",
    "phase-exec-paper-r007-bar-role-binding.json",
    "phase-exec-paper-r007-readiness-binding-search-results.json",
    "phase-exec-paper-r007-quote-window-readiness-bindings.json",
    "phase-exec-paper-r007-close-benchmark-readiness-bindings.json",
    "phase-exec-paper-r007-feed-quality-readiness-bindings.json",
    "phase-exec-paper-r007-risk-operator-approval-readiness.json",
    "phase-exec-paper-r007-r009-dryrun-handoff-contract.json",
    "phase-exec-paper-r007-r009-dryrun-handoff-package.json",
    "phase-exec-paper-r007-r009-design-only-preview-contract.json",
    "phase-exec-paper-r007-r009-design-only-preview-lines.json",
    "phase-exec-paper-r007-readiness-result.json",
    "phase-exec-paper-r007-missing-inputs-diagnostics.json",
    "phase-exec-paper-r007-next-paper-dryrun-recommendation.json",
    "phase-exec-paper-r007-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r007-legacy-compatibility-preservation.json",
    "phase-exec-paper-r007-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r007-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r007-cost-guidance-preservation.json",
    "phase-exec-paper-r007-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r007-no-broker-activation-audit.json",
    "phase-exec-paper-r007-no-live-marketdata-audit.json",
    "phase-exec-paper-r007-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r007-no-executable-schedule-audit.json",
    "phase-exec-paper-r007-no-child-slices-audit.json",
    "phase-exec-paper-r007-no-child-orders-audit.json",
    "phase-exec-paper-r007-no-order-created-audit.json",
    "phase-exec-paper-r007-no-real-fill-audit.json",
    "phase-exec-paper-r007-no-execution-report-audit.json",
    "phase-exec-paper-r007-no-route-no-submission-audit.json",
    "phase-exec-paper-r007-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r007-no-polygon-api-call-audit.json",
    "phase-exec-paper-r007-no-lmax-call-audit.json",
    "phase-exec-paper-r007-no-external-api-call-audit.json",
    "phase-exec-paper-r007-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r007-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r007-no-external-audit.json",
    "phase-exec-paper-r007-forbidden-actions-audit.json",
    "phase-exec-paper-r007-build-test-validator-evidence.json"
)

foreach ($file in $required) {
    $path = Join-Path $ArtifactsDir $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required R007 artifact: $file"
    }
}

$r006 = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-r006-plan-line-reference.json")
$r009 = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-r009-contract-reference.json")
$target = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-operator-target-close-input.json")
$targetBinding = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-canonical-target-close-binding.json")
$session = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-canonical-session-binding.json")
$barRole = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-bar-role-binding.json")
$search = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-readiness-binding-search-results.json")
$quote = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-quote-window-readiness-bindings.json")
$close = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-close-benchmark-readiness-bindings.json")
$feed = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-feed-quality-readiness-bindings.json")
$risk = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-risk-operator-approval-readiness.json")
$handoff = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-r009-dryrun-handoff-package.json")
$preview = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-r009-design-only-preview-lines.json")
$readiness = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-readiness-result.json")
$missing = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-missing-inputs-diagnostics.json")
$legacy = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-legacy-compatibility-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-cost-guidance-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-usdjpy-caveat-preservation.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r007-build-test-validator-evidence.json")

Assert-True $r006 "CurrentLineLevelPaperExecutionPlanLinesPresent" "R006 current line-level plan lines are missing."
if ([int]$r006.LineCount -ne 7) {
    Fail "R007 must bind exactly seven current plan lines."
}
Assert-False $r006 "LinesInvented" "Current paper plan lines were invented."

Assert-True $target "ExplicitOperatorSupplied" "Target close must be explicitly operator supplied."
Assert-False $target "InputInvented" "Target close input was invented."
if ($target.CanonicalTargetCloseUtc -ne "2025-12-17T02:00:00Z") {
    Fail "Unexpected canonical target close UTC."
}
if ($target.BarRole -ne "ClosingFlatten") {
    Fail "BarRole must be ClosingFlatten."
}
Assert-True $target "MustEndFlat" "MustEndFlat must be true."
Assert-False $target "OvernightAllowed" "OvernightAllowed must be false."

Assert-True $targetBinding "CanonicalQuarterHourTimestampConfirmed" "Target close is not confirmed canonical quarter-hour."
Assert-False $targetBinding "Legacy06UsedAsFutureCanonical" "Legacy :06 used as future canonical."
if ([int]$targetBinding.BoundLineCount -ne 7) {
    Fail "Target close was not bound to all seven lines."
}
foreach ($line in @($targetBinding.Lines)) {
    if ($line.CanonicalTargetCloseTimestamp -ne "2025-12-17T02:00:00Z") {
        Fail "Line has wrong target close: $($line.PaperExecutionPlanLineId)"
    }
    if ([bool]$line.CanonicalQuarterHourTimestampConfirmed -ne $true) {
        Fail "Line target close is not canonical: $($line.PaperExecutionPlanLineId)"
    }
}

if ([int]$session.BoundLineCount -ne 7) {
    Fail "Canonical session was not bound to all lines."
}
Assert-True $session "MustEndFlat" "Session binding must preserve MustEndFlat."
Assert-False $session "OvernightAllowed" "Session binding must preserve no overnight."
if ($barRole.BarRole -ne "ClosingFlatten") {
    Fail "Bar role binding is not ClosingFlatten."
}

Assert-True $search "ReadinessBindingsFoundNoExternal" "Expected R053 readiness bindings were not all found."
Assert-False $search "ReadinessBindingsMissingNoExternal" "Readiness bindings unexpectedly missing."
if ([int]$search.QuoteWindowReadinessBindingsFound -ne 7 -or [int]$search.CloseBenchmarkReadinessBindingsFound -ne 7 -or [int]$search.FeedQualityReadinessBindingsFound -ne 7) {
    Fail "Expected seven bindings in each readiness family."
}
Assert-True $quote "AllBindingsFound" "Quote-window bindings missing."
Assert-True $close "AllBindingsFound" "Close-benchmark bindings missing."
Assert-True $feed "AllBindingsFound" "Feed-quality bindings missing."
foreach ($binding in @($quote.Bindings)) {
    if (-not [bool]$binding.BindingFound) { Fail "Missing quote-window binding for $($binding.ExecutionTradableSymbol)" }
    if ($binding.Binding.ReadinessStatus -ne "Ready") { Fail "Quote-window binding not ready for $($binding.ExecutionTradableSymbol)" }
}
foreach ($binding in @($close.Bindings)) {
    if (-not [bool]$binding.BindingFound) { Fail "Missing close-benchmark binding for $($binding.ExecutionTradableSymbol)" }
    if ($binding.Binding.ReadinessStatus -ne "Ready") { Fail "Close-benchmark binding not ready for $($binding.ExecutionTradableSymbol)" }
}
foreach ($binding in @($feed.Bindings)) {
    if (-not [bool]$binding.BindingFound) { Fail "Missing feed-quality binding for $($binding.ExecutionTradableSymbol)" }
    if ($binding.Binding.FeedQualityStatus -ne "Ready") { Fail "Feed-quality binding not ready for $($binding.ExecutionTradableSymbol)" }
}

Assert-False $risk "RiskReviewFound" "Risk review should not be fabricated."
Assert-False $risk "OperatorApprovalFound" "Operator approval should not be fabricated."
Assert-True $risk "PlaceholderOnlyNotApproved" "Risk/operator status must be placeholder-only and not approved."
Assert-False $risk "ApprovalInvented" "Approval was invented."
Assert-True $risk "BlocksR009Preview" "Missing risk/operator approval must block preview."

Assert-False $handoff "HandoffPackageReady" "Handoff must not be fully ready without risk/operator approval."
Assert-True $handoff "HandoffPackagePartiallyReady" "Handoff should be partially ready with readiness bindings."
if ([int]$handoff.LineCount -ne 7) {
    Fail "Handoff package must contain seven partially ready lines."
}
foreach ($line in @($handoff.Lines)) {
    if ($line.CanonicalTargetCloseTimestamp -ne "2025-12-17T02:00:00Z") {
        Fail "Handoff line target close mismatch: $($line.PaperExecutionPlanLineId)"
    }
    if ([bool]$line.CanonicalQuarterHourTimestampConfirmed -ne $true) {
        Fail "Handoff line target close not canonical: $($line.PaperExecutionPlanLineId)"
    }
    if ($line.RiskReviewStatus -ne "RequiredMissing" -or $line.OperatorApprovalStatus -ne "RequiredMissing") {
        Fail "Handoff line must keep risk/operator approval missing."
    }
    if ($line.QuoteWindowReadinessBinding -eq $null -or $line.CloseBenchmarkReadinessBinding -eq $null -or $line.FeedQualityReadinessBinding -eq $null) {
        Fail "Handoff line missing readiness binding: $($line.PaperExecutionPlanLineId)"
    }
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

Assert-False $preview "PreviewReady" "Preview must remain blocked without risk/operator approval."
if ([int]$preview.PreviewLineCount -ne 0) {
    Fail "Preview lines were created despite missing risk/operator approval."
}
Assert-True $preview "NoOrdersRepresented" "Preview represented orders."
Assert-True $preview "NoSchedulesRepresented" "Preview represented schedules."
Assert-True $preview "NoFillsRepresented" "Preview represented fills."
Assert-True $preview "NoRoutesRepresented" "Preview represented routes."

Assert-True $readiness "CanonicalTargetCloseBound" "Readiness result did not bind target close."
Assert-True $readiness "ReadinessBindingsFound" "Readiness result did not find bindings."
Assert-False $readiness "RiskOperatorApprovalReady" "Risk/operator approval was fabricated."
Assert-False $readiness "R009DesignOnlyPreviewReady" "Preview should be blocked."
Assert-True $missing "MissingInputsRemain" "Missing diagnostics must remain."
Assert-False $missing "ReadinessBindingsMissing" "Readiness diagnostics should not claim missing readiness bindings."

Assert-True $r009 "DesignOnly" "R009 design-only status weakened."
Assert-True $r009 "NonExecutable" "R009 became executable."
Assert-False $r009 "ExecutablePromotionAuthorized" "R009 executable promotion authorized."
Assert-False $legacy "Legacy06UsedAsFutureCanonical" "Legacy :06 used as future canonical."
Assert-True $directCross "DirectCrossExecutionDisabled" "Direct-cross exclusion weakened."
Assert-False $directCross "DirectCrossExecutionLinesCreated" "Direct-cross execution lines created."
Assert-False $cost "FiveUsdPerMillionUniversalized" "5 USD/million was universalized."
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
Assert-False $forbidden "StateMutation" "State mutated."
Assert-False $forbidden "PlanLinesInvented" "Current paper plan lines invented."
Assert-False $forbidden "R009PromotedExecutable" "R009 promoted executable."
Assert-False $forbidden "PreviewLinesRepresentOrders" "Preview lines represented orders."
Assert-False $forbidden "AUDUSDMisclassified" "AUDUSD misclassified."

if ($evidence.PSObject.Properties.Name -notcontains "BuildTestsValidatorEvidencePresent" -or -not [bool]$evidence.BuildTestsValidatorEvidencePresent) {
    Fail "Build/tests/validator evidence missing."
}

Write-Host "EXEC-PAPER-R007 validator passed."
