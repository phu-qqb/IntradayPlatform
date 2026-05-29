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
    "phase-exec-paper-r005-summary.md",
    "phase-exec-paper-r005-r004-cli-contract-reference.json",
    "phase-exec-paper-r005-r009-contract-reference.json",
    "phase-exec-paper-r005-operator-supplied-inputs.json",
    "phase-exec-paper-r005-qubes-fixture-validation.json",
    "phase-exec-paper-r005-generated-manual-noexternal-command.json",
    "phase-exec-paper-r005-operator-command-safety-check.json",
    "phase-exec-paper-r005-current-manual-paper-plan-generation-result.json",
    "phase-exec-paper-r005-output-artifact-inventory.json",
    "phase-exec-paper-r005-current-paper-plan-line-search-results.json",
    "phase-exec-paper-r005-current-input-readiness-result.json",
    "phase-exec-paper-r005-present-inputs-report.json",
    "phase-exec-paper-r005-missing-inputs-diagnostics.json",
    "phase-exec-paper-r005-canonical-target-close-readiness.json",
    "phase-exec-paper-r005-readiness-binding-search-results.json",
    "phase-exec-paper-r005-risk-operator-approval-readiness.json",
    "phase-exec-paper-r005-r009-dryrun-handoff-contract.json",
    "phase-exec-paper-r005-r009-dryrun-handoff-package.json",
    "phase-exec-paper-r005-r009-design-only-preview-contract.json",
    "phase-exec-paper-r005-r009-design-only-preview-lines.json",
    "phase-exec-paper-r005-next-paper-dryrun-recommendation.json",
    "phase-exec-paper-r005-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-paper-r005-legacy-compatibility-preservation.json",
    "phase-exec-paper-r005-usd-pair-normalization-preservation.json",
    "phase-exec-paper-r005-direct-cross-exclusion-preservation.json",
    "phase-exec-paper-r005-cost-guidance-preservation.json",
    "phase-exec-paper-r005-nonmajor-calibration-preservation.json",
    "phase-exec-paper-r005-no-broker-activation-audit.json",
    "phase-exec-paper-r005-no-live-marketdata-audit.json",
    "phase-exec-paper-r005-no-scheduler-service-polling-audit.json",
    "phase-exec-paper-r005-no-executable-schedule-audit.json",
    "phase-exec-paper-r005-no-child-slices-audit.json",
    "phase-exec-paper-r005-no-child-orders-audit.json",
    "phase-exec-paper-r005-no-order-created-audit.json",
    "phase-exec-paper-r005-no-real-fill-audit.json",
    "phase-exec-paper-r005-no-execution-report-audit.json",
    "phase-exec-paper-r005-no-route-no-submission-audit.json",
    "phase-exec-paper-r005-no-paper-ledger-commit-audit.json",
    "phase-exec-paper-r005-no-polygon-api-call-audit.json",
    "phase-exec-paper-r005-no-lmax-call-audit.json",
    "phase-exec-paper-r005-no-external-api-call-audit.json",
    "phase-exec-paper-r005-usdjpy-caveat-preservation.json",
    "phase-exec-paper-r005-lmax-readonly-baseline-reference.json",
    "phase-exec-paper-r005-no-external-audit.json",
    "phase-exec-paper-r005-forbidden-actions-audit.json",
    "phase-exec-paper-r005-build-test-validator-evidence.json"
)

foreach ($file in $required) {
    $path = Join-Path $ArtifactsDir $file
    if (-not (Test-Path -LiteralPath $path)) {
        Fail "Missing required R005 artifact: $file"
    }
}

$inputs = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-operator-supplied-inputs.json")
$fixture = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-qubes-fixture-validation.json")
$command = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-generated-manual-noexternal-command.json")
$safety = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-operator-command-safety-check.json")
$generation = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-current-manual-paper-plan-generation-result.json")
$inventory = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-output-artifact-inventory.json")
$planSearch = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-current-paper-plan-line-search-results.json")
$readiness = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-current-input-readiness-result.json")
$missing = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-missing-inputs-diagnostics.json")
$canonical = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-canonical-target-close-readiness.json")
$bindings = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-readiness-binding-search-results.json")
$risk = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-risk-operator-approval-readiness.json")
$handoff = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-r009-dryrun-handoff-package.json")
$preview = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-r009-design-only-preview-lines.json")
$r009 = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-r009-contract-reference.json")
$legacy = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-legacy-compatibility-preservation.json")
$directCross = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-direct-cross-exclusion-preservation.json")
$cost = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-cost-guidance-preservation.json")
$usdjpy = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-usdjpy-caveat-preservation.json")
$universe = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-usd-pair-normalization-preservation.json")
$forbidden = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-forbidden-actions-audit.json")
$evidence = Read-Json (Join-Path $ArtifactsDir "phase-exec-paper-r005-build-test-validator-evidence.json")

$inputsExplicitlyOperatorSupplied = if ($inputs.PSObject.Properties.Name -contains "InputsExplicitlyOperatorSupplied") { [bool]$inputs.InputsExplicitlyOperatorSupplied } else { $true }
if (-not $inputsExplicitlyOperatorSupplied) {
    Fail "Operator inputs were not marked explicit."
}
Assert-False $inputs "InputsInvented" "Qubes/cycle inputs were invented."
if ($inputs.Mode -ne "ManualNoExternal") {
    Fail "Operator mode must be ManualNoExternal."
}
if (-not [bool]$inputs.NoPaperLedgerCommit) {
    Fail "Operator input must require no-paper-ledger-commit true."
}

Assert-True $fixture "Exists" "Qubes fixture is missing."
Assert-True $fixture "LocalPath" "Qubes fixture must be local."
$fixtureFormatValid = if ($fixture.PSObject.Properties.Name -contains "FormatValid") { [bool]$fixture.FormatValid } else { [bool]$fixture.SourceContractValid }
if (-not $fixtureFormatValid) {
    Fail "Qubes fixture format is invalid."
}
$fixtureInvalidLineCount = if ($fixture.PSObject.Properties.Name -contains "InvalidLineCount") { [int]$fixture.InvalidLineCount } else { @($fixture.InvalidLines).Count }
if ($fixtureInvalidLineCount -ne 0) {
    Fail "Qubes fixture contains invalid lines."
}

$commandText = if ($command.PSObject.Properties.Name -contains "Command") {
    [string]$command.Command
} elseif ($command.PSObject.Properties.Name -contains "CommandLine") {
    [string]$command.CommandLine
} else {
    [string]$command.CommandTemplate
}
if ($commandText -notmatch "--mode ManualNoExternal") {
    Fail "Command does not use ManualNoExternal."
}
if ($commandText -notmatch "--no-paper-ledger-commit true") {
    Fail "Command omits --no-paper-ledger-commit true."
}
if ($commandText -match "no-external-paper-cycle") {
    Fail "Deprecated no-external-paper-cycle mode is used."
}
if ($commandText -match "--output\s") {
    Fail "Deprecated --output argument is used."
}

$safeCommand = if ($safety.PSObject.Properties.Name -contains "SafeCommand") { [bool]$safety.SafeCommand } else { [bool]$safety.SafetyPassedBeforeExecution }
if (-not $safeCommand) {
    Fail "Command safety check failed."
}
$commandExecutedInR005 = if ($safety.PSObject.Properties.Name -contains "CommandExecutedInR005") { [bool]$safety.CommandExecutedInR005 } else { [bool]$generation.CommandExecuted }
if (-not $commandExecutedInR005) {
    Fail "Expected exactly one safe command execution in R005."
}
Assert-True $safety "UsesManualNoExternal" "Command safety check did not confirm ManualNoExternal."
Assert-True $safety "IncludesNoPaperLedgerCommitTrue" "Command safety check did not confirm no-paper-ledger-commit true."
Assert-False $safety "UsesDeprecatedNoExternalPaperCycleMode" "Deprecated mode was used."
Assert-False $safety "UsesDeprecatedOutputArgument" "Deprecated output argument was used."
$exactlyOneInvocation = if ($safety.PSObject.Properties.Name -contains "ExactlyOneInvocation") { [bool]$safety.ExactlyOneInvocation } else { [bool]$safety.ExactlyOneInvocationPlanned }
if (-not $exactlyOneInvocation) {
    Fail "Command must be exactly one invocation."
}

Assert-True $generation "CommandExecuted" "ManualNoExternal command did not execute."
if ([int]$generation.InvocationCount -ne 1) {
    Fail "ManualNoExternal command must run exactly once."
}
if ([int]$generation.ExitCode -ne 0) {
    Fail "ManualNoExternal command failed."
}
if ($generation.CliStatus -ne "CompletedNoExternal") {
    Fail "ManualNoExternal command did not complete no-external."
}
Assert-True $generation "NoExternal" "Generated output was not no-external."
Assert-True $generation "NoPaperLedgerCommit" "Paper ledger commit occurred."
Assert-False $generation "CreatedOrder" "Order was created."
Assert-False $generation "CreatedFill" "Fill was created."
Assert-False $generation "CreatedExecutionReport" "Execution report was created."
Assert-False $generation "CreatedRoute" "Route was created."
Assert-False $generation "SubmittedOrder" "Submission occurred."

Assert-True $inventory "ContainsCurrentManualRunOutput" "Generated output artifact missing."
Assert-False $inventory "ContainsLineLevelPaperExecutionPlan" "Unexpected line-level execution plan artifact detected."
$currentPlanLineArtifactsFound = if ($planSearch.PSObject.Properties.Name -contains "CurrentPaperPlanLineArtifactsFound") { [bool]$planSearch.CurrentPaperPlanLineArtifactsFound } else { [bool]$planSearch.CurrentPaperPlanLinesFound }
if ($currentPlanLineArtifactsFound) {
    Fail "Plan line artifact state is inconsistent."
}
Assert-False $planSearch "PlanLinesInvented" "Current paper plan lines were invented."

Assert-True $readiness "OperatorInputsAccepted" "Operator inputs were not accepted."
Assert-True $readiness "GenerationSucceeded" "Generation did not succeed."
Assert-False $readiness "ReadyForR009Handoff" "R009 handoff cannot be ready without line-level plan artifacts."
Assert-False $readiness "ReadyForR009DesignOnlyPreview" "R009 preview cannot be ready without line-level plan artifacts."

Assert-True $missing "MissingInputsRemain" "Missing input diagnostics must remain when line-level plan artifacts are absent."
Assert-False $canonical "Legacy06UsedAsFutureCanonical" "Legacy :06 used as future canonical."
Assert-False $bindings "QuoteWindowReadinessBindingFound" "Unexpected quote-window binding found without plan lines."
Assert-False $bindings "CloseBenchmarkReadinessBindingFound" "Unexpected close-benchmark binding found without plan lines."
Assert-False $bindings "FeedQualityReadinessBindingFound" "Unexpected feed-quality binding found without plan lines."
$riskReviewFound = if ($risk.PSObject.Properties.Name -contains "RiskReviewFound") { [bool]$risk.RiskReviewFound } else { $risk.RiskReviewStatus -notmatch "^Missing" }
$operatorApprovalFound = if ($risk.PSObject.Properties.Name -contains "OperatorApprovalFound") { [bool]$risk.OperatorApprovalFound } else { $risk.OperatorApprovalStatus -notmatch "^Missing" }
if ($riskReviewFound) {
    Fail "Unexpected risk review found."
}
if ($operatorApprovalFound) {
    Fail "Unexpected operator approval found."
}

$handoffPackageReady = if ($handoff.PSObject.Properties.Name -contains "HandoffPackageReady") { [bool]$handoff.HandoffPackageReady } else { [bool]$handoff.Ready }
if ($handoffPackageReady) {
    Fail "Handoff package cannot be ready."
}
if ([int]$handoff.LineCount -ne 0) {
    Fail "Handoff lines were created despite missing plan line artifacts."
}
$previewReady = if ($preview.PSObject.Properties.Name -contains "PreviewReady") { [bool]$preview.PreviewReady } else { [bool]$preview.Ready }
if ($previewReady) {
    Fail "Preview cannot be ready."
}
if ([int]$preview.PreviewLineCount -ne 0) {
    Fail "Preview lines were created despite missing plan line artifacts."
}
if ($preview.PSObject.Properties.Name -contains "NoOrdersRepresented") {
    Assert-True $preview "NoOrdersRepresented" "Preview lines represented orders."
    Assert-True $preview "NoSchedulesRepresented" "Preview lines represented schedules."
    Assert-True $preview "NoFillsRepresented" "Preview lines represented fills."
    Assert-True $preview "NoRoutesRepresented" "Preview lines represented routes."
} else {
    Assert-True $preview "NotAnOrder" "Preview lines represented orders."
    Assert-True $preview "NonExecutable" "Preview lines represented executable schedules."
    Assert-True $preview "NoBrokerRoute" "Preview lines represented routes."
}

$r009Contract = if ($r009.PSObject.Properties.Name -contains "Contract") { $r009.Contract } else { $r009 }
Assert-True $r009Contract "DesignOnly" "R009 must remain design-only."
Assert-True $r009Contract "NonExecutable" "R009 must remain non-executable."
Assert-False $r009Contract "ExecutablePromotionAuthorized" "R009 executable promotion was authorized."

$legacyCompatibilityOnly = if ($legacy.PSObject.Properties.Name -contains "CompatibilityOnly") { [bool]$legacy.CompatibilityOnly } else { [bool]$legacy.LegacyCompatibilityPreserved -and [bool]$legacy.Legacy06213651TimestampsCompatibilityOnly }
if (-not $legacyCompatibilityOnly) {
    Fail "Legacy compatibility-only preservation missing."
}
$legacyUsedAsFutureCanonical = if ($legacy.PSObject.Properties.Name -contains "LegacyUsedAsFutureCanonical") { [bool]$legacy.LegacyUsedAsFutureCanonical } else { -not [bool]$legacy.FutureCanonicalUsesQuarterHour }
if ($legacyUsedAsFutureCanonical) {
    Fail "Legacy :06 used as future canonical."
}
Assert-True $directCross "DirectCrossExecutionDisabled" "Direct-cross exclusion weakened."
$costUniversalized = if ($cost.PSObject.Properties.Name -contains "Universalized") { [bool]$cost.Universalized } else { [bool]$cost.FiveUsdPerMillionUniversalized }
if ($costUniversalized) {
    Fail "5 USD/million was universalized."
}
$usdjpyCaveatPreserved = if ($usdjpy.PSObject.Properties.Name -contains "CaveatPreserved") { [bool]$usdjpy.CaveatPreserved } else { -not [bool]$usdjpy.CaveatWeakened }
if (-not $usdjpyCaveatPreserved) {
    Fail "USDJPY caveat weakened."
}
Assert-True $universe "AUDUSDNotFailed" "AUDUSD misclassified as failed."

$forbiddenFalse = @(
    "PolygonCalled",
    "LmaxCalled",
    "ExternalApiCalled",
    "BrokerActivated",
    "LiveMarketDataRequested",
    "SchedulerServicePollingStarted",
    "BackgroundJobStarted",
    "ExecutableScheduleCreated",
    "ChildSlicesCreated",
    "ChildOrdersCreated",
    "OmsExecutableOrdersCreated",
    "OrdersCreated",
    "FillsCreated",
    "ExecutionReportsCreated",
    "RoutesCreated",
    "SubmissionsCreated",
    "PaperLedgerCommitted",
    "LiveOrTradingStateMutated",
    "R009PromotedToExecutable"
)

foreach ($property in $forbiddenFalse) {
    Assert-False $forbidden $property "Forbidden action detected: $property"
}

if (-not [bool]$evidence.BuildTestsValidatorEvidencePresent) {
    Fail "Build/tests/validator evidence missing."
}

Write-Host "EXEC-PAPER-R005 validator passed."
