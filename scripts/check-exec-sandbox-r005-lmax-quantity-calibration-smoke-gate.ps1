$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"

function Fail {
    param([string]$Message)
    throw "EXEC-SANDBOX-R005 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-sandbox-r005-summary.md",
    "phase-exec-sandbox-r005-r004-reference.json",
    "phase-exec-sandbox-r005-quantity-rejection-diagnosis.json",
    "phase-exec-sandbox-r005-local-quantity-rule-discovery.json",
    "phase-exec-sandbox-r005-calibrated-quantity-result.json",
    "phase-exec-sandbox-r005-credential-envvar-presence-validation.json",
    "phase-exec-sandbox-r005-sandbox-guardrail-revalidation.json",
    "phase-exec-sandbox-r005-production-route-blocking-check.json",
    "phase-exec-sandbox-r005-fix-logon-confirmation.json",
    "phase-exec-sandbox-r005-r009-sandbox-execution-intent.json",
    "phase-exec-sandbox-r005-pretrade-sandbox-risk-check.json",
    "phase-exec-sandbox-r005-r009-sandbox-decision.json",
    "phase-exec-sandbox-r005-sandbox-order-intent.json",
    "phase-exec-sandbox-r005-sandbox-route.json",
    "phase-exec-sandbox-r005-sandbox-submission-result.json",
    "phase-exec-sandbox-r005-sandbox-ack-reject.json",
    "phase-exec-sandbox-r005-sandbox-execution-report.json",
    "phase-exec-sandbox-r005-sandbox-fill-report.json",
    "phase-exec-sandbox-r005-sandbox-reconciliation-result.json",
    "phase-exec-sandbox-r005-sandbox-audit-record.json",
    "phase-exec-sandbox-r005-no-secret-persistence-audit.json",
    "phase-exec-sandbox-r005-no-production-broker-audit.json",
    "phase-exec-sandbox-r005-no-production-order-audit.json",
    "phase-exec-sandbox-r005-no-production-route-audit.json",
    "phase-exec-sandbox-r005-no-production-fill-report-audit.json",
    "phase-exec-sandbox-r005-no-production-ledger-audit.json",
    "phase-exec-sandbox-r005-no-production-state-mutation-audit.json",
    "phase-exec-sandbox-r005-direct-cross-exclusion-preservation.json",
    "phase-exec-sandbox-r005-usd-pair-whitelist-preservation.json",
    "phase-exec-sandbox-r005-usdjpy-caveat-preservation.json",
    "phase-exec-sandbox-r005-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sandbox-r005-legacy-compatibility-preservation.json",
    "phase-exec-sandbox-r005-cost-guidance-preservation.json",
    "phase-exec-sandbox-r005-nonmajor-calibration-preservation.json",
    "phase-exec-sandbox-r005-forbidden-actions-audit.json",
    "phase-exec-sandbox-r005-next-phase-recommendation.json",
    "phase-exec-sandbox-r005-build-test-validator-evidence.json",
    "phase-exec-sandbox-r005-raw-lmax-demo-lifecycle-result.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$sourcePath = Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009LmaxSandboxOrderSmoke.cs"
$testPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009SandboxQuantityCalibrationTests.cs"
$builderPath = Join-Path $repoRoot "scripts/build-exec-sandbox-r005-lmax-quantity-calibration-smoke.ps1"
if (-not (Test-Path -LiteralPath $sourcePath)) { Fail "Sandbox source missing" }
if (-not (Test-Path -LiteralPath $testPath)) { Fail "Focused R005 tests missing" }
if (-not (Test-Path -LiteralPath $builderPath)) { Fail "R005 artifact builder missing" }
$source = Get-Content -LiteralPath $sourcePath -Raw
$tests = Get-Content -LiteralPath $testPath -Raw
foreach ($token in @(
    "R009SandboxQuantityRejectionDiagnosis",
    "R009SandboxLocalQuantityRuleDiscovery",
    "R009SandboxCalibratedQuantityResult",
    "DiagnoseQuantityRejection",
    "DiscoverLocalQuantityRule",
    "CalibrateSandboxQuantity"
)) {
    if ($source -notmatch [regex]::Escape($token)) { Fail "Source missing R005 token $token" }
}
foreach ($token in @(
    "Quantity_rejection_diagnosis_requires_quantity_not_valid",
    "Local_quantity_rule_discovery_uses_seeded_mapping_and_lab_defaults",
    "Calibration_selects_local_minimum_when_within_demo_quantity_cap",
    "Calibration_blocks_when_local_quantity_rule_is_missing"
)) {
    if ($tests -notmatch [regex]::Escape($token)) { Fail "Focused R005 tests missing scenario $token" }
}

$reference = Read-Json "phase-exec-sandbox-r005-r004-reference.json"
if ($reference.RejectReason -ne "QUANTITY_NOT_VALID") { Fail "R004 reference did not preserve QUANTITY_NOT_VALID" }

$diagnosis = Read-Json "phase-exec-sandbox-r005-quantity-rejection-diagnosis.json"
if ($diagnosis.Symbol -ne "EURUSD" -or [decimal]$diagnosis.RejectedQuantity -ne 0.01) { Fail "R004 rejected quantity diagnosis mismatch" }
if ($diagnosis.RejectReason -ne "QUANTITY_NOT_VALID" -or $diagnosis.QuantityNotValidConfirmed -ne $true) { Fail "Quantity rejection reason not confirmed" }
if ($diagnosis.FixOrderQtyField -ne "38") { Fail "FIX OrderQty field not recorded" }

$discovery = Read-Json "phase-exec-sandbox-r005-local-quantity-rule-discovery.json"
if ($discovery.Status -ne "Ready") { Fail "Local quantity rule discovery not ready" }
if ([decimal]$discovery.MinOrderQuantity -ne 0.1 -or [decimal]$discovery.QuantityStep -ne 0.1 -or [decimal]$discovery.ContractSize -ne 10000) { Fail "Local quantity rule does not match seeded LMAX EURUSD mapping" }
if ([decimal]$discovery.LabDefaultMaxDemoOrderQuantity -ne 0.1) { Fail "Lab default max demo order quantity not preserved" }
if ($discovery.FixOrderQtyUsage -ne "OrderQty(38)" -or $discovery.CashOrderQtyUsage -ne "NotUsed" -or $discovery.MinQtyUsage -ne "NotUsed") { Fail "FIX quantity usage was weakened" }
if ($discovery.QuantityInventedWithoutLocalEvidence -ne $false) { Fail "Quantity was invented without local evidence" }
foreach ($path in @("src/QQ.Production.Intraday.Application/ApplicationServices.cs:2547", "tools/QQ.Production.Intraday.Lmax.ConnectivityLab/LabModels.cs:51", "tools/QQ.Production.Intraday.Lmax.ConnectivityLab/LabFixRecovery.cs:584")) {
    if (@($discovery.SourceEvidencePaths) -notcontains $path) { Fail "Quantity evidence path missing: $path" }
}
if (@($discovery.MissingCalibrationFields).Count -ne 0) { Fail "Quantity discovery has missing calibration fields" }

$calibrated = Read-Json "phase-exec-sandbox-r005-calibrated-quantity-result.json"
if ($calibrated.Status -ne "Ready" -or [decimal]$calibrated.CalibratedQuantity -ne 0.1) { Fail "Calibrated quantity is not ready or not 0.1" }
if ([decimal]$calibrated.OriginalRejectedQuantity -ne 0.01) { Fail "Original rejected quantity not preserved" }
if ($calibrated.WithinSandboxQuantityCap -ne $true -or $calibrated.WithinSandboxNotionalCap -ne $true) { Fail "Calibrated quantity/notional is outside sandbox cap" }
if ($calibrated.QuantityInventedWithoutLocalEvidence -ne $false) { Fail "Calibrated quantity was invented without local evidence" }
if ($calibrated.NotionalCapConflict -ne $false) { Fail "Notional cap conflict was ignored" }

$credential = Read-Json "phase-exec-sandbox-r005-credential-envvar-presence-validation.json"
if ($credential.CredentialSourceType -ne "EnvVars") { Fail "Credential source must be EnvVars" }
if ($credential.CredentialValuesRedacted -ne $true -or $credential.CredentialValuesPrintedOrPersisted -ne $false) { Fail "Credential values are not redacted" }
if ($credential.ProductionCredentialDetected -ne $false) { Fail "Production credential detected" }
if ($credential.SandboxCredentialPresent -ne $true) { Fail "Sandbox credential env var presence not confirmed" }
foreach ($name in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    if ($credential.CredentialVariablePresence.$name -ne $true) { Fail "Credential env var presence missing for $name" }
}

$guardrail = Read-Json "phase-exec-sandbox-r005-sandbox-guardrail-revalidation.json"
if ($guardrail.CurrentLmaxSetupOperatorAttestedSandbox -ne $true -or $guardrail.ExistingLmaxProfileIsSandbox -ne $true) { Fail "Sandbox guardrail attestation missing" }
if ($guardrail.SandboxOrderSubmissionEnabled -ne $true -or $guardrail.SandboxKillSwitchOpen -ne $true) { Fail "Sandbox submission/kill switch guardrails not ready" }
if ($guardrail.MaxSandboxOrderCount -ne 1 -or [decimal]$guardrail.MaxSandboxNotional -ne 10) { Fail "Sandbox count/notional guardrails incorrect" }
if ($guardrail.CalibratedOrderSizeWithinSandboxCap -ne $true) { Fail "Calibrated size not within sandbox cap" }
foreach ($property in @("ProductionVenueAllowed", "ProductionCredentialsAllowed", "DirectCrossExecutionAllowed", "NonmajorExecutionAllowed", "PaperLedgerCommitAllowed", "ProductionLedgerCommitAllowed", "StateMutationAllowed", "SchedulerAllowed")) {
    if ($guardrail.$property -ne $false) { Fail "Forbidden guardrail enabled: $property" }
}

$production = Read-Json "phase-exec-sandbox-r005-production-route-blocking-check.json"
foreach ($property in @("NoProductionEndpoint", "NoProductionCredentialProfile", "NoProductionRoute", "NoProductionLedger", "NoProductionStateMutation", "ProductionRouteBlocked")) {
    if ($production.$property -ne $true) { Fail "Production blocking check failed: $property" }
}

$logon = Read-Json "phase-exec-sandbox-r005-fix-logon-confirmation.json"
if ($logon.LogonAttempted -ne $true -or $logon.LogonConfirmed -ne $true) { Fail "Sandbox FIX logon was not confirmed" }
if ($logon.SessionStatus -ne "LogonConfirmedBeforeNewOrderSingle" -or $logon.NewOrderSingleSentAfterLogonConfirmed -ne $true) { Fail "NewOrderSingle was not gated behind confirmed logon" }
if ($logon.RedactedSessionMetadata.CredentialValuesRedacted -ne $true -or $logon.RedactedSessionMetadata.EndpointValuesRedacted -ne $true) { Fail "Logon metadata not redacted" }

$risk = Read-Json "phase-exec-sandbox-r005-pretrade-sandbox-risk-check.json"
if ($risk.QuantityCalibrationReady -ne $true -or $risk.LogonConfirmedBeforeOrder -ne $true) { Fail "Risk gate did not require quantity calibration and confirmed logon" }
if ($risk.SymbolWhitelisted -ne $true -or $risk.DirectCrossRejected -ne $true -or $risk.NonmajorRejected -ne $true) { Fail "Risk gate weakened symbol checks" }
if ($risk.CanonicalQuarterHourTargetClose -ne $true -or $risk.Legacy06Rejected -ne $true) { Fail "Risk gate weakened canonical timing" }
if ($risk.OrderCountWithinLimit -ne $true -or $risk.NotionalWithinConfiguredCap -ne $true -or $risk.QuantityWithinConfiguredCap -ne $true) { Fail "Risk gate did not enforce count/notional/quantity" }
if ($risk.NoProductionRoute -ne $true -or $risk.NoProductionLedger -ne $true) { Fail "Risk gate did not block production route/ledger" }

$intent = Read-Json "phase-exec-sandbox-r005-r009-sandbox-execution-intent.json"
if ($intent.Symbol -ne "EURUSD" -or [decimal]$intent.TargetQuantity -ne 0.1 -or [decimal]$intent.TargetNotional -ne 10) { Fail "Smoke intent is not calibrated EURUSD sandbox intent" }
if ($intent.CanonicalTargetCloseUtc -ne "2026-05-26T15:15:00Z") { Fail "Smoke intent target close is not expected canonical close" }
if ($intent.SandboxOnly -ne $true -or $intent.ProductionOrder -ne $false -or $intent.IsLiveProduction -ne $false -or $intent.NoProductionLedgerCommit -ne $true -or $intent.NoProductionStateMutation -ne $true) { Fail "Smoke intent allows production path" }

$decision = Read-Json "phase-exec-sandbox-r005-r009-sandbox-decision.json"
if ($decision.R009DecisionProduced -ne $true -or $decision.DecisionStatus -ne "PreviewReadyForSandboxQuantityCalibratedSmoke") { Fail "R009 sandbox decision not ready" }
if ($decision.PrimaryPolicyCandidate -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0") { Fail "R009 primary policy not preserved" }
if ($decision.ProductionOrder -ne $false -or $decision.SandboxOnly -ne $true) { Fail "R009 decision allows production path" }

$order = Read-Json "phase-exec-sandbox-r005-sandbox-order-intent.json"
if ($order.SandboxOnly -ne $true -or $order.BrokerVenue -ne "ExistingLmaxDemoProfile") { Fail "Order intent not clearly sandbox/demo" }
if ([decimal]$order.CalibratedQuantity -ne 0.1 -or [decimal]$order.TargetNotional -gt [decimal]$order.MaxSandboxNotional) { Fail "Order intent quantity/notional cap invalid" }
if ($order.ProductionOrder -ne $false -or $order.IsLiveProduction -ne $false -or $order.NoProductionLedgerCommit -ne $true -or $order.NoProductionStateMutation -ne $true) { Fail "Order intent artifact allows production path" }

$route = Read-Json "phase-exec-sandbox-r005-sandbox-route.json"
if ($route.RouteCreated -ne $true -or $route.SandboxOnly -ne $true -or $route.BrokerVenue -ne "ExistingLmaxDemoProfile") { Fail "Sandbox route artifact not created or not marked sandbox/demo" }
if ($route.ProductionRoute -ne $false -or $route.NonSandboxBrokerRoute -ne $false -or $route.ProductionCredentialsUsed -ne $false) { Fail "Route artifact unsafe" }

$submission = Read-Json "phase-exec-sandbox-r005-sandbox-submission-result.json"
if ($submission.SubmissionAttempted -ne $true) { Fail "Sandbox retry submission was not attempted after confirmed logon" }
if ($submission.ProductionSubmission -ne $false -or $submission.SandboxOnly -ne $true) { Fail "Submission artifact unsafe" }
if ($submission.SubmittedRetryOrderCount -ne 1) { Fail "Submitted sandbox retry order count is not exactly one" }
if ([decimal]$submission.SubmittedQuantity -ne 0.1) { Fail "Submitted quantity is not calibrated 0.1" }
if ([decimal]$submission.SubmittedNotional -gt 10) { Fail "Sandbox notional exceeds configured cap" }
if (@("SubmittedAndRejectedCaptured", "SubmittedAcceptedOrAckedCaptured") -notcontains $submission.Status) { Fail "Sandbox retry submission result not captured" }

$ack = Read-Json "phase-exec-sandbox-r005-sandbox-ack-reject.json"
if ($ack.ProductionAckReject -ne $false -or $ack.SandboxOnly -ne $true) { Fail "Ack/reject artifact unsafe" }

$execReport = Read-Json "phase-exec-sandbox-r005-sandbox-execution-report.json"
if ($execReport.ExecutionReportCaptured -ne $true -or $execReport.ExecutionReportCount -lt 1) { Fail "Sandbox execution report was not captured" }
if ($execReport.ProductionExecutionReport -ne $false -or $execReport.SandboxOnly -ne $true) { Fail "Execution report artifact unsafe" }

$fill = Read-Json "phase-exec-sandbox-r005-sandbox-fill-report.json"
if ($fill.ProductionFill -ne $false -or $fill.SandboxOnly -ne $true) { Fail "Fill artifact unsafe" }

$reconciliation = Read-Json "phase-exec-sandbox-r005-sandbox-reconciliation-result.json"
if ([decimal]$reconciliation.OriginalR004RejectedQuantity -ne 0.01 -or [decimal]$reconciliation.CalibratedRetryQuantity -ne 0.1) { Fail "Reconciliation did not preserve original/calibrated quantities" }
if ($reconciliation.SubmittedSandboxOrder -ne $true -or $reconciliation.SubmittedSandboxRetryOrderCount -ne 1) { Fail "Reconciliation does not match one retry submission" }
if ($reconciliation.AckOrRejectCaptured -ne $true -or $reconciliation.ExecutionReportCaptured -ne $true) { Fail "Reconciliation did not capture ack/reject/report" }
if ($reconciliation.ProductionOrderCreated -ne $false -or $reconciliation.ProductionRouteCreated -ne $false -or $reconciliation.ProductionFillOrReportCreated -ne $false -or $reconciliation.ProductionLedgerMutation -ne $false -or $reconciliation.ProductionStateMutation -ne $false -or $reconciliation.SandboxOnly -ne $true) {
    Fail "Reconciliation artifact unsafe"
}

$audit = Read-Json "phase-exec-sandbox-r005-sandbox-audit-record.json"
if ($audit.SandboxOnly -ne $true -or $audit.QuantityCalibrated -ne $true -or $audit.SandboxOrderSubmitted -ne $true) { Fail "Sandbox audit record does not reflect R005 result" }
if ($audit.ProductionOrderCreated -ne $false -or $audit.ProductionRouteCreated -ne $false -or $audit.ProductionFillOrReportCreated -ne $false -or $audit.ProductionLedgerCommit -ne $false -or $audit.ProductionStateMutation -ne $false) { Fail "Sandbox audit record allows production path" }
if ($audit.CredentialValuesRedacted -ne $true -or $audit.CredentialValuesPrintedOrPersisted -ne $false) { Fail "Sandbox audit record credential handling unsafe" }

$raw = Read-Json "phase-exec-sandbox-r005-raw-lmax-demo-lifecycle-result.json"
if ($raw.environment -ne "Demo" -or $raw.captureMode -ne "DemoLifecycleEvidence" -or $raw.redaction -ne "SanitizedNoCredentialsNoRawLogon") { Fail "Raw lifecycle artifact is not sanitized DEMO evidence" }
if ($raw.dryRun -ne $false) { Fail "Raw lifecycle did not record actual bounded sandbox retry" }
if ($raw.clientOrderId -ne "R005SMOKE2605261515" -or $raw.instrumentSymbol -ne "EURUSD") { Fail "Raw lifecycle artifact is not the R005 EURUSD retry order" }
if ([decimal]$raw.requestedQuantity -ne 0.1) { Fail "Raw lifecycle requested quantity is not calibrated 0.1" }
if (@($raw.executionReports).Count -lt 1) { Fail "Raw lifecycle execution report count must be at least one" }

$direct = Read-Json "phase-exec-sandbox-r005-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed -ne $false -or $direct.DirectCrossExecutionIntentRejected -ne $true) { Fail "Direct-cross exclusion weakened" }
$whitelist = Read-Json "phase-exec-sandbox-r005-usd-pair-whitelist-preservation.json"
if ($whitelist.NonWhitelistedSymbolAllowed -ne $false -or $whitelist.AudusdStatus -ne "SupportedAndNotFailed") { Fail "Whitelist/AUDUSD preservation failed" }
foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")) {
    if (@($whitelist.WhitelistedSymbols) -notcontains $symbol) { Fail "Whitelisted symbol missing: $symbol" }
}
$usdjpy = Read-Json "phase-exec-sandbox-r005-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8") { Fail "USDJPY caveat weakened" }
$legacy = Read-Json "phase-exec-sandbox-r005-legacy-compatibility-preservation.json"
if ($legacy.Legacy06AcceptedAsFutureCanonical -ne $false -or $legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 accepted as future canonical" }
$canonical = Read-Json "phase-exec-sandbox-r005-canonical-quarter-hour-policy-preservation.json"
if ($canonical.CandidateIsCanonical -ne $true -or @($canonical.FutureCanonicalMinutes | Where-Object { $_ -eq 6 }).Count -gt 0) { Fail "Canonical quarter-hour policy weakened" }
$cost = Read-Json "phase-exec-sandbox-r005-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$nonmajor = Read-Json "phase-exec-sandbox-r005-nonmajor-calibration-preservation.json"
if ($nonmajor.SandboxExecutionAllowed -ne $false) { Fail "Nonmajor/EM/scandi/CNH execution allowed" }

$forbidden = Read-Json "phase-exec-sandbox-r005-forbidden-actions-audit.json"
foreach ($property in @(
    "LmaxProductionUsed",
    "ProductionCredentialsUsed",
    "NonSandboxBrokerRouteUsed",
    "CredentialValuesPrintedOrPersisted",
    "PolygonCalled",
    "UnrelatedExternalApiCalled",
    "SchedulerServicePollingBackgroundJobIntroduced",
    "NewOrderSingleSentBeforeLogonConfirmed",
    "MoreThanOneSandboxRetryOrderSubmitted",
    "SandboxNotionalExceedsConfiguredCap",
    "QuantityInventedWithoutLocalEvidence",
    "DirectCrossExecutionAllowed",
    "NonWhitelistedSymbolAllowed",
    "Legacy06AcceptedAsFutureCanonical",
    "UsdjpyCaveatWeakened",
    "AudusdMisclassified",
    "ProductionOrderArtifactCreated",
    "ProductionRouteArtifactCreated",
    "ProductionFillReportArtifactCreated",
    "ProductionLedgerCommitOccurred",
    "ProductionStateMutationOccurred"
)) {
    if ($forbidden.$property -ne $false) { Fail "Forbidden action observed or allowed: $property" }
}
if ($forbidden.SandboxArtifactsClearlyMarkedSandboxOnlyOrExistingLmaxDemoProfile -ne $true) { Fail "Sandbox artifacts not clearly marked" }

$secretAudit = Read-Json "phase-exec-sandbox-r005-no-secret-persistence-audit.json"
if ($secretAudit.SecretValuesSerialized -ne $false -or $secretAudit.CredentialVariableNamesOnly -ne $true -or $secretAudit.CredentialValuesRedacted -ne $true) { Fail "Secret persistence audit failed" }

$allText = Get-ChildItem -LiteralPath $artifactDir -File -Filter "phase-exec-sandbox-r005-*.json" | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = $allText -join "`n"
foreach ($banned in @("QQ_LMAX_FIX_PASSWORD", "FixPassword`":`"", "FixUsername`":`"", "Password`":`"", "Username`":`"")) {
    if ($combined -match [regex]::Escape($banned)) { Fail "Possible credential value field persisted: $banned" }
}

$evidence = Read-Json "phase-exec-sandbox-r005-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedSandboxTests -ne "Passed") { Fail "Focused sandbox test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-SANDBOX-R005 validator passed."

