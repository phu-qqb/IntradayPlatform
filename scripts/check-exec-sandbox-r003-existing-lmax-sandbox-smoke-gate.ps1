$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"

function Fail {
    param([string]$Message)
    throw "EXEC-SANDBOX-R003 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-sandbox-r003-summary.md",
    "phase-exec-sandbox-r003-r002-reference.json",
    "phase-exec-sandbox-r003-operator-sandbox-attestation.json",
    "phase-exec-sandbox-r003-existing-lmax-profile-classification.json",
    "phase-exec-sandbox-r003-lmax-sandbox-config.json",
    "phase-exec-sandbox-r003-lmax-sandbox-config-validation.json",
    "phase-exec-sandbox-r003-credential-envvar-presence-validation.json",
    "phase-exec-sandbox-r003-production-route-blocking-check.json",
    "phase-exec-sandbox-r003-sandbox-guardrail-contract.json",
    "phase-exec-sandbox-r003-operator-sandbox-approval.json",
    "phase-exec-sandbox-r003-r009-sandbox-execution-intent.json",
    "phase-exec-sandbox-r003-pretrade-sandbox-risk-check.json",
    "phase-exec-sandbox-r003-r009-sandbox-decision.json",
    "phase-exec-sandbox-r003-sandbox-order-intent.json",
    "phase-exec-sandbox-r003-sandbox-route.json",
    "phase-exec-sandbox-r003-sandbox-submission-result.json",
    "phase-exec-sandbox-r003-sandbox-ack-reject.json",
    "phase-exec-sandbox-r003-sandbox-execution-report.json",
    "phase-exec-sandbox-r003-sandbox-fill-report.json",
    "phase-exec-sandbox-r003-sandbox-reconciliation-result.json",
    "phase-exec-sandbox-r003-sandbox-audit-record.json",
    "phase-exec-sandbox-r003-no-secret-persistence-audit.json",
    "phase-exec-sandbox-r003-no-production-broker-audit.json",
    "phase-exec-sandbox-r003-no-production-order-audit.json",
    "phase-exec-sandbox-r003-no-production-route-audit.json",
    "phase-exec-sandbox-r003-no-production-fill-report-audit.json",
    "phase-exec-sandbox-r003-no-production-ledger-audit.json",
    "phase-exec-sandbox-r003-no-production-state-mutation-audit.json",
    "phase-exec-sandbox-r003-direct-cross-exclusion-preservation.json",
    "phase-exec-sandbox-r003-usd-pair-whitelist-preservation.json",
    "phase-exec-sandbox-r003-usdjpy-caveat-preservation.json",
    "phase-exec-sandbox-r003-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sandbox-r003-legacy-compatibility-preservation.json",
    "phase-exec-sandbox-r003-cost-guidance-preservation.json",
    "phase-exec-sandbox-r003-nonmajor-calibration-preservation.json",
    "phase-exec-sandbox-r003-forbidden-actions-audit.json",
    "phase-exec-sandbox-r003-next-phase-recommendation.json",
    "phase-exec-sandbox-r003-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$sourcePath = Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009LmaxSandboxOrderSmoke.cs"
$testPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009ExistingLmaxSandboxProfileAttestationTests.cs"
if (-not (Test-Path -LiteralPath $sourcePath)) { Fail "Sandbox source missing" }
if (-not (Test-Path -LiteralPath $testPath)) { Fail "Focused R003 tests missing" }
$source = Get-Content -LiteralPath $sourcePath -Raw
$tests = Get-Content -LiteralPath $testPath -Raw
foreach ($token in @(
    "R009OperatorSandboxProfileAttestation",
    "R009ExistingLmaxSandboxProfileClassification",
    "ClassifyOperatorAttestedExistingLmaxProfile",
    "OperatorAttestationAndDemoCredentialProfile"
)) {
    if ($source -notmatch [regex]::Escape($token)) { Fail "Source missing R003 token $token" }
}
foreach ($token in @(
    "Operator_attested_demo_profile_is_accepted_without_literal_lmaxsandbox_section",
    "Demo_env_vars_are_presence_metadata_only",
    "Production_labeled_endpoint_blocks_existing_profile_classification",
    "Missing_non_secret_session_fields_block_before_submission"
)) {
    if ($tests -notmatch [regex]::Escape($token)) { Fail "Focused R003 tests missing scenario $token" }
}

$attestation = Read-Json "phase-exec-sandbox-r003-operator-sandbox-attestation.json"
if ($attestation.CurrentLmaxSetupOperatorAttestedSandbox -ne $true) { Fail "Operator sandbox attestation missing" }
if ($attestation.SandboxClassificationSource -ne "OperatorAttestationAndDemoCredentialProfile") { Fail "Sandbox classification source incorrect" }
if ($attestation.EndpointValuesRedacted -ne $true) { Fail "Endpoint redaction flag missing" }
if ($attestation.ProductionRouteBlocked -ne $true -or $attestation.ProductionLedgerBlocked -ne $true) { Fail "Production route/ledger not blocked in attestation" }

$credential = Read-Json "phase-exec-sandbox-r003-credential-envvar-presence-validation.json"
if ($credential.CredentialSourceType -ne "EnvVars") { Fail "Credential source must be EnvVars" }
if ($credential.CredentialValuesRedacted -ne $true -or $credential.CredentialValuesPrintedOrPersisted -ne $false) { Fail "Credential values are not redacted" }
if ($credential.ProductionCredentialDetected -ne $false) { Fail "Production credential detected" }
foreach ($name in @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")) {
    if ($credential.CredentialVariablePresence.$name -ne $true) { Fail "Credential env var presence missing for $name" }
}

$secretAudit = Read-Json "phase-exec-sandbox-r003-no-secret-persistence-audit.json"
if ($secretAudit.SecretValuesSerialized -ne $false -or $secretAudit.CredentialVariableNamesOnly -ne $true) { Fail "Secret persistence audit failed" }

$profile = Read-Json "phase-exec-sandbox-r003-existing-lmax-profile-classification.json"
if ($profile.LiteralLmaxSandboxSectionRequired -eq $true) { Fail "R003 incorrectly requires literal LmaxSandbox section" }
if ($profile.ProductionEndpointDetected -ne $false) { Fail "Production endpoint detected" }
if ($profile.ProductionCredentialsDetected -ne $false) { Fail "Production credentials detected" }
if ($profile.EndpointValuesRedacted -ne $true) { Fail "Profile endpoint values not redacted" }
if (@($profile.MissingNonSecretConfigurationNames).Count -gt 0 -and $profile.Status -ne "Blocked") { Fail "Missing endpoint/session config did not block" }

$config = Read-Json "phase-exec-sandbox-r003-lmax-sandbox-config.json"
if ($config.CurrentLmaxSetupOperatorAttestedSandbox -ne $true -or $config.Environment -ne "Sandbox") { Fail "Sandbox config not attested/sandbox" }
if ($config.CredentialSourceType -ne "EnvVars" -or $config.CredentialValuesRedacted -ne $true) { Fail "Sandbox config credential handling unsafe" }
if ($config.SandboxOrderSubmissionEnabled -ne $true -or $config.SandboxKillSwitchOpen -ne $true) { Fail "Sandbox smoke guardrails missing" }
if ($config.MaxSandboxOrderCount -ne 1 -or $config.MaxSandboxNotional -ne 10) { Fail "Sandbox count/notional caps incorrect" }
foreach ($property in @("ProductionVenueAllowed", "ProductionCredentialsAllowed", "DirectCrossExecutionAllowed", "NonmajorExecutionAllowed", "PaperLedgerCommitAllowed", "ProductionLedgerCommitAllowed", "StateMutationAllowed", "AutomaticExecutionAllowed", "SchedulerAllowed")) {
    if ($config.$property -ne $false) { Fail "Forbidden guardrail enabled: $property" }
}

$validation = Read-Json "phase-exec-sandbox-r003-lmax-sandbox-config-validation.json"
if ($validation.LiteralLmaxSandboxSectionRequired -ne $false) { Fail "Validation still requires literal LmaxSandbox section" }
if ($validation.ProductionRouteBlocked -ne $true -or $validation.ProductionLedgerBlocked -ne $true) { Fail "Validation did not block production route/ledger" }
if ($validation.EndpointValuesRedacted -ne $true) { Fail "Validation endpoint values not redacted" }

$risk = Read-Json "phase-exec-sandbox-r003-pretrade-sandbox-risk-check.json"
if ($risk.SymbolWhitelisted -ne $true -or $risk.DirectCrossRejected -ne $true -or $risk.NonmajorRejected -ne $true) { Fail "Risk gate weakened symbol checks" }
if ($risk.CanonicalQuarterHourTargetClose -ne $true -or $risk.Legacy06Rejected -ne $true) { Fail "Risk gate weakened canonical timing" }
if ($risk.OrderCountWithinLimit -ne $true -or $risk.NotionalWithinConfiguredCap -ne $true) { Fail "Risk gate did not enforce count/notional" }
if ($risk.NoProductionRoute -ne $true -or $risk.NoProductionLedger -ne $true) { Fail "Risk gate did not block production route/ledger" }

$intent = Read-Json "phase-exec-sandbox-r003-r009-sandbox-execution-intent.json"
if ($intent.Symbol -ne "EURUSD" -or $intent.TargetNotional -ne 10 -or $intent.SandboxOnly -ne $true) { Fail "Smoke intent is not the tiny EURUSD sandbox intent" }
if ($intent.ProductionOrder -ne $false -or $intent.IsLiveProduction -ne $false -or $intent.NoProductionLedgerCommit -ne $true -or $intent.NoProductionStateMutation -ne $true) { Fail "Smoke intent allows production path" }

$order = Read-Json "phase-exec-sandbox-r003-sandbox-order-intent.json"
if ($order.ProductionOrder -ne $false -or $order.IsLiveProduction -ne $false -or $order.NoProductionLedgerCommit -ne $true -or $order.NoProductionStateMutation -ne $true) { Fail "Order intent artifact allows production path" }
if ($order.TargetNotional -gt $order.MaxSandboxNotional) { Fail "Order intent exceeds sandbox notional cap" }

$route = Read-Json "phase-exec-sandbox-r003-sandbox-route.json"
if ($route.ProductionRoute -ne $false -or $route.NonSandboxBrokerRoute -ne $false -or $route.ProductionCredentialsUsed -ne $false -or $route.SandboxOnly -ne $true) { Fail "Route artifact unsafe" }
$submission = Read-Json "phase-exec-sandbox-r003-sandbox-submission-result.json"
if ($submission.ProductionSubmission -ne $false -or $submission.SandboxOnly -ne $true) { Fail "Submission artifact unsafe" }
if ($submission.SubmittedOrderCount -gt 1) { Fail "More than one sandbox order submitted" }
if ($submission.SubmittedNotional -gt 10) { Fail "Sandbox notional exceeds configured cap" }
$ack = Read-Json "phase-exec-sandbox-r003-sandbox-ack-reject.json"
if ($ack.ProductionAckReject -ne $false -or $ack.SandboxOnly -ne $true) { Fail "Ack/reject artifact unsafe" }
$execReport = Read-Json "phase-exec-sandbox-r003-sandbox-execution-report.json"
if ($execReport.ProductionExecutionReport -ne $false -or $execReport.SandboxOnly -ne $true) { Fail "Execution report artifact unsafe" }
$fill = Read-Json "phase-exec-sandbox-r003-sandbox-fill-report.json"
if ($fill.ProductionFill -ne $false -or $fill.SandboxOnly -ne $true) { Fail "Fill artifact unsafe" }
$reconciliation = Read-Json "phase-exec-sandbox-r003-sandbox-reconciliation-result.json"
if ($reconciliation.ProductionOrderCreated -ne $false -or $reconciliation.ProductionRouteCreated -ne $false -or $reconciliation.ProductionFillOrReportCreated -ne $false -or $reconciliation.ProductionLedgerMutation -ne $false -or $reconciliation.ProductionStateMutation -ne $false -or $reconciliation.SandboxOnly -ne $true) {
    Fail "Reconciliation artifact unsafe"
}

$direct = Read-Json "phase-exec-sandbox-r003-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed -ne $false -or $direct.DirectCrossExecutionIntentRejected -ne $true) { Fail "Direct-cross exclusion weakened" }
$whitelist = Read-Json "phase-exec-sandbox-r003-usd-pair-whitelist-preservation.json"
if ($whitelist.NonWhitelistedSymbolAllowed -ne $false -or $whitelist.AudusdStatus -ne "SupportedAndNotFailed") { Fail "Whitelist/AUDUSD preservation failed" }
$usdjpy = Read-Json "phase-exec-sandbox-r003-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8") { Fail "USDJPY caveat weakened" }
$legacy = Read-Json "phase-exec-sandbox-r003-legacy-compatibility-preservation.json"
if ($legacy.Legacy06AcceptedAsFutureCanonical -ne $false -or $legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 accepted as future canonical" }
$cost = Read-Json "phase-exec-sandbox-r003-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$nonmajor = Read-Json "phase-exec-sandbox-r003-nonmajor-calibration-preservation.json"
if ($nonmajor.SandboxExecutionAllowed -ne $false) { Fail "Nonmajor/EM/scandi/CNH execution allowed" }

$forbidden = Read-Json "phase-exec-sandbox-r003-forbidden-actions-audit.json"
foreach ($property in @(
    "LmaxProductionUsed",
    "ProductionCredentialsUsed",
    "NonSandboxBrokerRouteUsed",
    "CredentialValuesPrintedOrPersisted",
    "PolygonCalled",
    "UnrelatedExternalApiCalled",
    "SchedulerServicePollingBackgroundJobIntroduced",
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

$allText = Get-ChildItem -LiteralPath $artifactDir -File -Filter "phase-exec-sandbox-r003-*.json" | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
foreach ($banned in @("LMAX_DEMO_FIX_PASSWORD`":`"", "FixPassword`":`"", "FixUsername`":`"", "Password`":`"")) {
    if (($allText -join "`n") -match [regex]::Escape($banned)) { Fail "Possible credential value field persisted: $banned" }
}

$evidence = Read-Json "phase-exec-sandbox-r003-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedSandboxTests -ne "Passed") { Fail "Focused sandbox test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-SANDBOX-R003 validator passed."
