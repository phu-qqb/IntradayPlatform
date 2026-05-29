$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"

function Fail {
    param([string]$Message)
    throw "EXEC-SANDBOX-R004 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-sandbox-r004-summary.md",
    "phase-exec-sandbox-r004-r003-reference.json",
    "phase-exec-sandbox-r004-logon-failure-diagnosis.json",
    "phase-exec-sandbox-r004-fix-session-config-inventory.json",
    "phase-exec-sandbox-r004-redacted-credential-envvar-validation.json",
    "phase-exec-sandbox-r004-non-secret-session-config-repair-result.json",
    "phase-exec-sandbox-r004-sandbox-guardrail-revalidation.json",
    "phase-exec-sandbox-r004-production-route-blocking-check.json",
    "phase-exec-sandbox-r004-fix-logon-trial-result.json",
    "phase-exec-sandbox-r004-r009-sandbox-execution-intent.json",
    "phase-exec-sandbox-r004-pretrade-sandbox-risk-check.json",
    "phase-exec-sandbox-r004-r009-sandbox-decision.json",
    "phase-exec-sandbox-r004-sandbox-order-intent.json",
    "phase-exec-sandbox-r004-sandbox-route.json",
    "phase-exec-sandbox-r004-sandbox-submission-result.json",
    "phase-exec-sandbox-r004-sandbox-ack-reject.json",
    "phase-exec-sandbox-r004-sandbox-execution-report.json",
    "phase-exec-sandbox-r004-sandbox-fill-report.json",
    "phase-exec-sandbox-r004-sandbox-reconciliation-result.json",
    "phase-exec-sandbox-r004-sandbox-audit-record.json",
    "phase-exec-sandbox-r004-no-secret-persistence-audit.json",
    "phase-exec-sandbox-r004-no-production-broker-audit.json",
    "phase-exec-sandbox-r004-no-production-order-audit.json",
    "phase-exec-sandbox-r004-no-production-route-audit.json",
    "phase-exec-sandbox-r004-no-production-fill-report-audit.json",
    "phase-exec-sandbox-r004-no-production-ledger-audit.json",
    "phase-exec-sandbox-r004-no-production-state-mutation-audit.json",
    "phase-exec-sandbox-r004-direct-cross-exclusion-preservation.json",
    "phase-exec-sandbox-r004-usd-pair-whitelist-preservation.json",
    "phase-exec-sandbox-r004-usdjpy-caveat-preservation.json",
    "phase-exec-sandbox-r004-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sandbox-r004-legacy-compatibility-preservation.json",
    "phase-exec-sandbox-r004-cost-guidance-preservation.json",
    "phase-exec-sandbox-r004-nonmajor-calibration-preservation.json",
    "phase-exec-sandbox-r004-forbidden-actions-audit.json",
    "phase-exec-sandbox-r004-next-phase-recommendation.json",
    "phase-exec-sandbox-r004-build-test-validator-evidence.json",
    "phase-exec-sandbox-r004-raw-lmax-demo-lifecycle-result.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$sourcePath = Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009LmaxSandboxOrderSmoke.cs"
$testPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009SandboxFixLogonDiagnosisTests.cs"
$builderPath = Join-Path $repoRoot "scripts/build-exec-sandbox-r004-lmax-demo-logon-retry.ps1"
if (-not (Test-Path -LiteralPath $sourcePath)) { Fail "Sandbox source missing" }
if (-not (Test-Path -LiteralPath $testPath)) { Fail "Focused R004 tests missing" }
if (-not (Test-Path -LiteralPath $builderPath)) { Fail "R004 artifact builder missing" }
$source = Get-Content -LiteralPath $sourcePath -Raw
$tests = Get-Content -LiteralPath $testPath -Raw
foreach ($token in @(
    "R009SandboxFixLogonFailureDiagnosis",
    "R009SandboxFixSessionConfigInventory",
    "R009SandboxNonSecretSessionRepairResult",
    "DiagnoseFixTradingLogonFailure",
    "CreateNonSecretSessionRepairResult"
)) {
    if ($source -notmatch [regex]::Escape($token)) { Fail "Source missing R004 token $token" }
}
foreach ($token in @(
    "Diagnosis_identifies_generic_demo_target_override_candidate",
    "Non_secret_session_repair_uses_local_order_target",
    "Non_secret_session_repair_blocks_when_order_target_is_not_discoverable"
)) {
    if ($tests -notmatch [regex]::Escape($token)) { Fail "Focused R004 tests missing scenario $token" }
}

$diagnosis = Read-Json "phase-exec-sandbox-r004-logon-failure-diagnosis.json"
if ($diagnosis.PriorLogonConfirmed -ne $false -or $diagnosis.PriorNewOrderSingleSent -ne $false) { Fail "R003 failure diagnosis misstates prior logon/order state" }
if (@($diagnosis.Findings) -notcontains "GenericDemoTargetCompIdMayOverrideLocalOrderTargetCompId") { Fail "R003 logon diagnosis did not identify target override candidate" }
if ($diagnosis.ExpectedLogonAckMessageType -ne "35=A" -or $diagnosis.CredentialValuesRedacted -ne $true) { Fail "Logon diagnosis missing ack expectation or redaction" }

$inventory = Read-Json "phase-exec-sandbox-r004-fix-session-config-inventory.json"
if ($inventory.ExistingLmaxProfileIsSandbox -ne $true) { Fail "Existing LMAX profile not classified as sandbox" }
if ($inventory.SandboxClassificationSource -ne "OperatorAttestationAndDemoCredentialProfile") { Fail "Sandbox classification source incorrect" }
if ($inventory.BeginString -ne "FIX.4.4") { Fail "FIX begin string inventory incorrect" }
if ($inventory.FixOrderTargetCompIdConfigured -ne $true) { Fail "FIX order target comp id not configured" }
if ($inventory.UseTlsConfigured -ne $true) { Fail "TLS setting missing" }
if ($inventory.ExpectedLogonAckMessageType -ne "35=A") { Fail "Expected logon ack not recorded" }
if ($inventory.EndpointValuesRedacted -ne $true -or $inventory.CredentialValuesRedacted -ne $true) { Fail "Inventory endpoint/credential redaction missing" }
if ($inventory.ProductionEndpointDetected -ne $false) { Fail "Production endpoint detected" }

$credential = Read-Json "phase-exec-sandbox-r004-redacted-credential-envvar-validation.json"
if ($credential.CredentialSourceType -ne "EnvVars") { Fail "Credential source must be EnvVars" }
if ($credential.CredentialValuesRedacted -ne $true -or $credential.CredentialValuesPrintedOrPersisted -ne $false) { Fail "Credential values are not redacted" }
if ($credential.ProductionCredentialDetected -ne $false) { Fail "Production credential detected" }
if ($credential.SandboxCredentialPresent -ne $true) { Fail "Sandbox credential env var presence not confirmed" }
foreach ($name in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    if ($credential.CredentialVariablePresence.$name -ne $true) { Fail "Credential env var presence missing for $name" }
}

$secretAudit = Read-Json "phase-exec-sandbox-r004-no-secret-persistence-audit.json"
if ($secretAudit.SecretValuesSerialized -ne $false -or $secretAudit.CredentialVariableNamesOnly -ne $true -or $secretAudit.CredentialValuesRedacted -ne $true) { Fail "Secret persistence audit failed" }

$repair = Read-Json "phase-exec-sandbox-r004-non-secret-session-config-repair-result.json"
if ($repair.Status -ne "Ready" -or $repair.RepairApplied -ne $true) { Fail "Non-secret session repair was not applied" }
if ($repair.UsesLocalOrderTarget -ne $true -or $repair.AvoidsGenericTargetOverride -ne $true) { Fail "Repair does not use local order target safely" }
if ($repair.ProductionRouteBlocked -ne $true) { Fail "Repair did not preserve production route block" }
if (@($repair.MissingNonSecretFields).Count -gt 0) { Fail "Repair still has missing non-secret fields" }

$guardrail = Read-Json "phase-exec-sandbox-r004-sandbox-guardrail-revalidation.json"
if ($guardrail.CurrentLmaxSetupOperatorAttestedSandbox -ne $true -or $guardrail.ExistingLmaxProfileIsSandbox -ne $true) { Fail "Sandbox guardrail attestation missing" }
if ($guardrail.CredentialSourceType -ne "EnvVars" -or $guardrail.CredentialValuesRedacted -ne $true) { Fail "Guardrail credential handling unsafe" }
if ($guardrail.SandboxOrderSubmissionEnabled -ne $true -or $guardrail.SandboxKillSwitchOpen -ne $true) { Fail "Sandbox submission/kill switch guardrails not ready" }
if ($guardrail.MaxSandboxOrderCount -ne 1 -or $guardrail.MaxSandboxNotional -ne 10) { Fail "Sandbox count/notional guardrails incorrect" }
foreach ($property in @("ProductionVenueAllowed", "ProductionCredentialsAllowed", "DirectCrossExecutionAllowed", "NonmajorExecutionAllowed", "PaperLedgerCommitAllowed", "ProductionLedgerCommitAllowed", "StateMutationAllowed", "SchedulerAllowed")) {
    if ($guardrail.$property -ne $false) { Fail "Forbidden guardrail enabled: $property" }
}

$production = Read-Json "phase-exec-sandbox-r004-production-route-blocking-check.json"
foreach ($property in @("NoProductionEndpoint", "NoProductionCredentialProfile", "NoProductionRoute", "NoProductionLedger", "NoProductionStateMutation", "ProductionRouteBlocked")) {
    if ($production.$property -ne $true) { Fail "Production blocking check failed: $property" }
}

$logon = Read-Json "phase-exec-sandbox-r004-fix-logon-trial-result.json"
if ($logon.LogonAttempted -ne $true) { Fail "Logon was not attempted" }
if ($logon.LogonConfirmed -ne $true) { Fail "Sandbox FIX logon was not confirmed" }
if ($logon.SessionStatus -ne "LogonConfirmedBeforeNewOrderSingle") { Fail "Session status does not prove logon before order" }
if ($logon.NewOrderSingleSentAfterLogonConfirmed -ne $true) { Fail "NewOrderSingle was not gated behind confirmed logon" }
if ($logon.RedactedSessionMetadata.EndpointValuesRedacted -ne $true -or $logon.RedactedSessionMetadata.SenderCompIdRedacted -ne $true) { Fail "Logon session metadata not redacted" }
if ($logon.RedactedSessionMetadata.TargetCompIdSource -ne "LocalFixOrderTargetCompId") { Fail "Logon did not use local order target comp id source" }

$risk = Read-Json "phase-exec-sandbox-r004-pretrade-sandbox-risk-check.json"
if ($risk.LogonConfirmedBeforeOrder -ne $true) { Fail "Risk gate did not require confirmed logon before order" }
if ($risk.DemoEnvVarsPresent -ne $true) { Fail "Risk gate did not confirm DEMO env vars" }
if ($risk.SymbolWhitelisted -ne $true -or $risk.DirectCrossRejected -ne $true -or $risk.NonmajorRejected -ne $true) { Fail "Risk gate weakened symbol checks" }
if ($risk.CanonicalQuarterHourTargetClose -ne $true -or $risk.Legacy06Rejected -ne $true) { Fail "Risk gate weakened canonical timing" }
if ($risk.OrderCountWithinLimit -ne $true -or $risk.NotionalWithinConfiguredCap -ne $true) { Fail "Risk gate did not enforce count/notional" }
if ($risk.NoProductionRoute -ne $true -or $risk.NoProductionLedger -ne $true) { Fail "Risk gate did not block production route/ledger" }

$intent = Read-Json "phase-exec-sandbox-r004-r009-sandbox-execution-intent.json"
if ($intent.Symbol -ne "EURUSD" -or $intent.TargetNotional -ne 10 -or $intent.CanonicalTargetCloseUtc -ne "2026-05-26T15:15:00Z") { Fail "Smoke intent is not the canonical tiny EURUSD sandbox intent" }
if ($intent.SandboxOnly -ne $true -or $intent.ProductionOrder -ne $false -or $intent.IsLiveProduction -ne $false -or $intent.NoProductionLedgerCommit -ne $true -or $intent.NoProductionStateMutation -ne $true) { Fail "Smoke intent allows production path" }

$decision = Read-Json "phase-exec-sandbox-r004-r009-sandbox-decision.json"
if ($decision.R009DecisionProduced -ne $true -or $decision.DecisionStatus -ne "PreviewReadyForSandboxSmoke") { Fail "R009 sandbox decision not ready" }
if ($decision.PrimaryPolicyCandidate -ne "CloseSeeking15mAdaptive_BalancedAdaptive_v0") { Fail "R009 primary policy not preserved" }
if ($decision.ProductionOrder -ne $false -or $decision.SandboxOnly -ne $true) { Fail "R009 decision allows production path" }

$order = Read-Json "phase-exec-sandbox-r004-sandbox-order-intent.json"
if ($order.SandboxOnly -ne $true -or $order.BrokerVenue -ne "ExistingLmaxDemoProfile") { Fail "Order intent not clearly sandbox/demo" }
if ($order.ProductionOrder -ne $false -or $order.IsLiveProduction -ne $false -or $order.NoProductionLedgerCommit -ne $true -or $order.NoProductionStateMutation -ne $true) { Fail "Order intent artifact allows production path" }
if ($order.TargetNotional -gt $order.MaxSandboxNotional) { Fail "Order intent exceeds sandbox notional cap" }

$route = Read-Json "phase-exec-sandbox-r004-sandbox-route.json"
if ($route.RouteCreated -ne $true -or $route.SandboxOnly -ne $true -or $route.BrokerVenue -ne "ExistingLmaxDemoProfile") { Fail "Sandbox route artifact not created or not marked sandbox/demo" }
if ($route.ProductionRoute -ne $false -or $route.NonSandboxBrokerRoute -ne $false -or $route.ProductionCredentialsUsed -ne $false) { Fail "Route artifact unsafe" }

$submission = Read-Json "phase-exec-sandbox-r004-sandbox-submission-result.json"
if ($submission.SubmissionAttempted -ne $true) { Fail "Sandbox submission was not attempted after confirmed logon" }
if ($submission.ProductionSubmission -ne $false -or $submission.SandboxOnly -ne $true) { Fail "Submission artifact unsafe" }
if ($submission.SubmittedOrderCount -ne 1) { Fail "Submitted sandbox order count is not exactly one" }
if ($submission.SubmittedNotional -gt 10) { Fail "Sandbox notional exceeds configured cap" }
if ($submission.Status -ne "SubmittedAndRejectedCaptured") { Fail "Expected submitted-and-rejected sandbox result not captured" }
if ($submission.AckOrRejectReason -ne "QUANTITY_NOT_VALID") { Fail "Expected sandbox rejection reason was not captured" }

$ack = Read-Json "phase-exec-sandbox-r004-sandbox-ack-reject.json"
if ($ack.RejectCaptured -ne $true -or $ack.Status -ne "Rejected" -or $ack.RejectReason -ne "QUANTITY_NOT_VALID") { Fail "Sandbox reject not captured correctly" }
if ($ack.ProductionAckReject -ne $false -or $ack.SandboxOnly -ne $true) { Fail "Ack/reject artifact unsafe" }

$execReport = Read-Json "phase-exec-sandbox-r004-sandbox-execution-report.json"
if ($execReport.ExecutionReportCaptured -ne $true -or $execReport.ExecutionReportCount -ne 1) { Fail "Sandbox execution report was not captured" }
if ($execReport.FinalOrdStatus -ne "Rejected" -or $execReport.FinalExecType -ne "Rejected") { Fail "Sandbox execution report status/type not captured" }
if ($execReport.ProductionExecutionReport -ne $false -or $execReport.SandboxOnly -ne $true) { Fail "Execution report artifact unsafe" }

$fill = Read-Json "phase-exec-sandbox-r004-sandbox-fill-report.json"
if ($fill.FillCaptured -ne $false -or $fill.FillCount -ne 0 -or $fill.Status -ne "NoFillReturned") { Fail "Fill artifact should record no fill returned" }
if ($fill.ProductionFill -ne $false -or $fill.SandboxOnly -ne $true) { Fail "Fill artifact unsafe" }

$reconciliation = Read-Json "phase-exec-sandbox-r004-sandbox-reconciliation-result.json"
if ($reconciliation.LogonConfirmed -ne $true -or $reconciliation.SubmittedSandboxOrder -ne $true -or $reconciliation.SubmittedSandboxOrderCount -ne 1) { Fail "Reconciliation does not match one confirmed sandbox submission" }
if ($reconciliation.AckOrRejectCaptured -ne $true -or $reconciliation.ExecutionReportCaptured -ne $true -or $reconciliation.FillCaptured -ne $false) { Fail "Reconciliation did not capture reject/report/no-fill correctly" }
if ($reconciliation.ProductionOrderCreated -ne $false -or $reconciliation.ProductionRouteCreated -ne $false -or $reconciliation.ProductionFillOrReportCreated -ne $false -or $reconciliation.ProductionLedgerMutation -ne $false -or $reconciliation.ProductionStateMutation -ne $false -or $reconciliation.SandboxOnly -ne $true) {
    Fail "Reconciliation artifact unsafe"
}

$audit = Read-Json "phase-exec-sandbox-r004-sandbox-audit-record.json"
if ($audit.SandboxOnly -ne $true -or $audit.LogonConfirmed -ne $true -or $audit.SandboxOrderSubmitted -ne $true -or $audit.SandboxOrderRejected -ne $true) { Fail "Sandbox audit record does not reflect R004 result" }
if ($audit.ProductionOrderCreated -ne $false -or $audit.ProductionRouteCreated -ne $false -or $audit.ProductionFillOrReportCreated -ne $false -or $audit.ProductionLedgerCommit -ne $false -or $audit.ProductionStateMutation -ne $false) { Fail "Sandbox audit record allows production path" }
if ($audit.CredentialValuesRedacted -ne $true -or $audit.CredentialValuesPrintedOrPersisted -ne $false) { Fail "Sandbox audit record credential handling unsafe" }

$raw = Read-Json "phase-exec-sandbox-r004-raw-lmax-demo-lifecycle-result.json"
if ($raw.environment -ne "Demo" -or $raw.captureMode -ne "DemoLifecycleEvidence" -or $raw.redaction -ne "SanitizedNoCredentialsNoRawLogon") { Fail "Raw lifecycle artifact is not sanitized DEMO evidence" }
if ($raw.dryRun -ne $false) { Fail "Raw lifecycle did not record actual bounded sandbox attempt" }
if ($raw.clientOrderId -ne "R004SMOKE2605261515" -or $raw.instrumentSymbol -ne "EURUSD") { Fail "Raw lifecycle artifact is not the R004 EURUSD smoke order" }
if (@($raw.executionReports).Count -ne 1) { Fail "Raw lifecycle execution report count must be one" }
if ($raw.executionReports[0].executionType -ne "Rejected" -or $raw.executionReports[0].orderStatus -ne "Rejected") { Fail "Raw lifecycle did not capture rejected execution report" }
if ($raw.executionReports[0].payload.text -ne "QUANTITY_NOT_VALID") { Fail "Raw lifecycle rejection reason mismatch" }
if (@($raw.tradeCaptureReports).Count -ne 0) { Fail "Raw lifecycle unexpectedly contains trade capture/fill reports" }

$direct = Read-Json "phase-exec-sandbox-r004-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed -ne $false -or $direct.DirectCrossExecutionIntentRejected -ne $true) { Fail "Direct-cross exclusion weakened" }
$whitelist = Read-Json "phase-exec-sandbox-r004-usd-pair-whitelist-preservation.json"
if ($whitelist.NonWhitelistedSymbolAllowed -ne $false -or $whitelist.AudusdStatus -ne "SupportedAndNotFailed") { Fail "Whitelist/AUDUSD preservation failed" }
foreach ($symbol in @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")) {
    if (@($whitelist.WhitelistedSymbols) -notcontains $symbol) { Fail "Whitelisted symbol missing: $symbol" }
}
$usdjpy = Read-Json "phase-exec-sandbox-r004-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8") { Fail "USDJPY caveat weakened" }
$legacy = Read-Json "phase-exec-sandbox-r004-legacy-compatibility-preservation.json"
if ($legacy.Legacy06AcceptedAsFutureCanonical -ne $false -or $legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 accepted as future canonical" }
$canonical = Read-Json "phase-exec-sandbox-r004-canonical-quarter-hour-policy-preservation.json"
if ($canonical.CandidateIsCanonical -ne $true -or @($canonical.FutureCanonicalMinutes | Where-Object { $_ -eq 6 }).Count -gt 0) { Fail "Canonical quarter-hour policy weakened" }
$cost = Read-Json "phase-exec-sandbox-r004-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$nonmajor = Read-Json "phase-exec-sandbox-r004-nonmajor-calibration-preservation.json"
if ($nonmajor.SandboxExecutionAllowed -ne $false) { Fail "Nonmajor/EM/scandi/CNH execution allowed" }

$forbidden = Read-Json "phase-exec-sandbox-r004-forbidden-actions-audit.json"
foreach ($property in @(
    "LmaxProductionUsed",
    "ProductionCredentialsUsed",
    "NonSandboxBrokerRouteUsed",
    "CredentialValuesPrintedOrPersisted",
    "PolygonCalled",
    "UnrelatedExternalApiCalled",
    "SchedulerServicePollingBackgroundJobIntroduced",
    "NewOrderSingleSentBeforeLogonConfirmed",
    "MoreThanOneSandboxOrderSubmitted",
    "SandboxNotionalExceedsConfiguredCap",
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

$allText = Get-ChildItem -LiteralPath $artifactDir -File -Filter "phase-exec-sandbox-r004-*.json" | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = $allText -join "`n"
foreach ($banned in @("QQ_LMAX_FIX_PASSWORD", "FixPassword`":`"", "FixUsername`":`"", "Password`":`"", "Username`":`"")) {
    if ($combined -match [regex]::Escape($banned)) { Fail "Possible credential value field persisted: $banned" }
}

$evidence = Read-Json "phase-exec-sandbox-r004-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedSandboxTests -ne "Passed") { Fail "Focused sandbox test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-SANDBOX-R004 validator passed."
