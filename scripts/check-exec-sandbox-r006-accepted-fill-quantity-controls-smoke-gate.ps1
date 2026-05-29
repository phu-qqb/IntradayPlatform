$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"

function Fail {
    param([string]$Message)
    throw "EXEC-SANDBOX-R006 validator failed: $Message"
}

function Read-Json {
    param([string]$Name)
    $path = Join-Path $artifactDir $Name
    if (-not (Test-Path -LiteralPath $path)) { Fail "Missing artifact $Name" }
    Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

$requiredArtifacts = @(
    "phase-exec-sandbox-r006-summary.md",
    "phase-exec-sandbox-r006-r005-reference.json",
    "phase-exec-sandbox-r006-accepted-fill-review.json",
    "phase-exec-sandbox-r006-r005-reconciliation-review.json",
    "phase-exec-sandbox-r006-quantity-control-contract.json",
    "phase-exec-sandbox-r006-quantity-rule-discovery.json",
    "phase-exec-sandbox-r006-quantity-normalization-results.json",
    "phase-exec-sandbox-r006-price-control-contract.json",
    "phase-exec-sandbox-r006-marketability-control-review.json",
    "phase-exec-sandbox-r006-sandbox-guardrail-revalidation.json",
    "phase-exec-sandbox-r006-selected-smoke-symbols.json",
    "phase-exec-sandbox-r006-fix-logon-confirmation.json",
    "phase-exec-sandbox-r006-sandbox-order-intents.json",
    "phase-exec-sandbox-r006-sandbox-routes.json",
    "phase-exec-sandbox-r006-sandbox-submission-results.json",
    "phase-exec-sandbox-r006-sandbox-ack-reject-results.json",
    "phase-exec-sandbox-r006-sandbox-execution-reports.json",
    "phase-exec-sandbox-r006-sandbox-fill-reports.json",
    "phase-exec-sandbox-r006-sandbox-reconciliation-result.json",
    "phase-exec-sandbox-r006-sandbox-audit-record.json",
    "phase-exec-sandbox-r006-smoke-decision.json",
    "phase-exec-sandbox-r006-no-secret-persistence-audit.json",
    "phase-exec-sandbox-r006-no-production-broker-audit.json",
    "phase-exec-sandbox-r006-no-production-order-audit.json",
    "phase-exec-sandbox-r006-no-production-route-audit.json",
    "phase-exec-sandbox-r006-no-production-fill-report-audit.json",
    "phase-exec-sandbox-r006-no-production-ledger-audit.json",
    "phase-exec-sandbox-r006-no-production-state-mutation-audit.json",
    "phase-exec-sandbox-r006-direct-cross-exclusion-preservation.json",
    "phase-exec-sandbox-r006-usd-pair-whitelist-preservation.json",
    "phase-exec-sandbox-r006-usdjpy-caveat-preservation.json",
    "phase-exec-sandbox-r006-canonical-quarter-hour-policy-preservation.json",
    "phase-exec-sandbox-r006-legacy-compatibility-preservation.json",
    "phase-exec-sandbox-r006-cost-guidance-preservation.json",
    "phase-exec-sandbox-r006-nonmajor-calibration-preservation.json",
    "phase-exec-sandbox-r006-forbidden-actions-audit.json",
    "phase-exec-sandbox-r006-next-phase-recommendation.json",
    "phase-exec-sandbox-r006-build-test-validator-evidence.json"
)

foreach ($artifact in $requiredArtifacts) {
    if (-not (Test-Path -LiteralPath (Join-Path $artifactDir $artifact))) {
        Fail "Required artifact missing: $artifact"
    }
}

$sourcePath = Join-Path $repoRoot "src/QQ.Production.Intraday.Application/R009LmaxSandboxOrderSmoke.cs"
$testPath = Join-Path $repoRoot "tests/QQ.Production.Intraday.Tests.Unit/R009SandboxAcceptedFillQuantityControlsTests.cs"
if (-not (Test-Path -LiteralPath $sourcePath)) { Fail "Sandbox source missing" }
if (-not (Test-Path -LiteralPath $testPath)) { Fail "Focused R006 tests missing" }
$source = Get-Content -LiteralPath $sourcePath -Raw
$tests = Get-Content -LiteralPath $testPath -Raw
foreach ($token in @(
    "R009SandboxAcceptedFillReview",
    "R009SandboxQuantityControlContract",
    "R009SandboxQuantityNormalizationResult",
    "R009SandboxPriceControlContract",
    "R009SandboxMarketabilityControlReview",
    "ReviewAcceptedFill",
    "NormalizeSandboxQuantity",
    "ReviewMarketability"
)) {
    if ($source -notmatch [regex]::Escape($token)) { Fail "Source missing R006 token $token" }
}
foreach ($token in @(
    "Accepted_fill_review_requires_sandbox_fill_and_no_production_paths",
    "Quantity_control_rejects_below_min_quantity",
    "Quantity_control_rejects_non_step_quantity",
    "Quantity_control_rejects_unknown_symbol_rule",
    "Market_order_price_control_does_not_request_live_market_data",
    "Limit_order_price_control_requires_explicit_sandbox_price"
)) {
    if ($tests -notmatch [regex]::Escape($token)) { Fail "Focused R006 tests missing scenario $token" }
}

$fillReview = Read-Json "phase-exec-sandbox-r006-accepted-fill-review.json"
if ($fillReview.Status -ne "Ready") { Fail "Accepted fill review not ready" }
if ($fillReview.Symbol -ne "EURUSD" -or [decimal]$fillReview.RequestedQuantity -ne 0.1 -or [decimal]$fillReview.FilledQuantity -ne 0.1) { Fail "Accepted fill review did not preserve R005 EURUSD fill" }
if ($fillReview.FinalOrderStatus -ne "Filled" -or $fillReview.FinalExecType -ne "Trade") { Fail "Accepted fill final status/type incorrect" }
if ($fillReview.SandboxOnly -ne $true -or $fillReview.ProductionOrderCreated -ne $false -or $fillReview.ProductionRouteCreated -ne $false -or $fillReview.ProductionFillOrReportCreated -ne $false -or $fillReview.ProductionLedgerMutation -ne $false -or $fillReview.ProductionStateMutation -ne $false -or $fillReview.CredentialValuesPersisted -ne $false) {
    Fail "Accepted fill review unsafe"
}

$r005Recon = Read-Json "phase-exec-sandbox-r006-r005-reconciliation-review.json"
if ($r005Recon.R005SubmittedSandboxOrder -ne $true -or $r005Recon.R005ExecutionReportCaptured -ne $true -or $r005Recon.R005FillCaptured -ne $true) { Fail "R005 reconciliation review missing fill evidence" }
if ($r005Recon.R005ProductionOrderCreated -ne $false -or $r005Recon.R005ProductionRouteCreated -ne $false -or $r005Recon.R005ProductionLedgerMutation -ne $false -or $r005Recon.R005ProductionStateMutation -ne $false) { Fail "R005 reconciliation review unsafe" }

$quantity = Read-Json "phase-exec-sandbox-r006-quantity-control-contract.json"
if ($quantity.MaxSandboxOrderCount -ne 3 -or [decimal]$quantity.MaxOrderQuantityPerSymbol -ne 0.1 -or [decimal]$quantity.MaxTotalSandboxQuantity -ne 0.3) { Fail "Quantity caps incorrect" }
foreach ($property in @("RejectBelowMin", "RejectNonStepQuantities", "RejectAboveSandboxCap", "RejectUnknownSymbolQuantityRules")) {
    if ($quantity.$property -ne $true) { Fail "Quantity control missing $property" }
}
$eurRule = @($quantity.Rules | Where-Object { $_.Symbol -eq "EURUSD" })[0]
if ($null -eq $eurRule -or [decimal]$eurRule.MinOrderQuantity -ne 0.1 -or [decimal]$eurRule.QuantityStep -ne 0.1 -or [decimal]$eurRule.ContractSize -ne 10000) { Fail "EURUSD quantity rule not preserved" }
if (@($quantity.Rules | Where-Object { $_.Symbol -eq "AUDUSD" -and $_.RuleDiscovered -eq $false }).Count -ne 1) { Fail "AUDUSD missing quantity rule not documented" }
if (@($quantity.Rules | Where-Object { $_.Symbol -eq "GBPUSD" -and $_.RuleDiscovered -eq $false }).Count -ne 1) { Fail "GBPUSD missing quantity rule not documented" }

$discovery = Read-Json "phase-exec-sandbox-r006-quantity-rule-discovery.json"
if ($discovery.QuantityRulesInvented -ne $false) { Fail "Quantity rules were invented" }
if (@($discovery.DiscoveredSymbols) -notcontains "EURUSD") { Fail "EURUSD discovered quantity rule missing" }
if (@($discovery.SkippedSymbols | Where-Object { $_.Symbol -eq "AUDUSD" -and $_.Reason -eq "MissingLocalSymbolSpecificQuantityRule" }).Count -ne 1) { Fail "AUDUSD skip reason missing" }
if (@($discovery.SkippedSymbols | Where-Object { $_.Symbol -eq "GBPUSD" -and $_.Reason -eq "MissingLocalSymbolSpecificQuantityRule" }).Count -ne 1) { Fail "GBPUSD skip reason missing" }

$normalization = Read-Json "phase-exec-sandbox-r006-quantity-normalization-results.json"
if (@($normalization.Results | Where-Object { $_.Symbol -eq "EURUSD" -and $_.Status -eq "Ready" -and [decimal]$_.NormalizedQuantity -eq 0.1 }).Count -ne 1) { Fail "EURUSD quantity normalization not ready" }
if (@($normalization.Results | Where-Object { $_.Symbol -eq "AUDUSD" -and $_.UnknownSymbolQuantityRuleRejected -eq $true -and $_.SubmittedEligible -eq $false }).Count -ne 1) { Fail "AUDUSD unknown rule not rejected" }
if (@($normalization.Results | Where-Object { $_.Symbol -eq "GBPUSD" -and $_.UnknownSymbolQuantityRuleRejected -eq $true -and $_.SubmittedEligible -eq $false }).Count -ne 1) { Fail "GBPUSD unknown rule not rejected" }
if ($normalization.BelowMinExampleRejected -ne $true -or $normalization.NonStepExampleRejected -ne $true -or $normalization.AboveSandboxCapExampleRejected -ne $true) { Fail "Quantity hardening examples missing" }

$price = Read-Json "phase-exec-sandbox-r006-price-control-contract.json"
if ($price.MarketOrdersAllowedForSandboxSmoke -ne $true -or $price.LimitOrdersRequireExplicitSandboxLimitPrice -ne $true) { Fail "Price control contract incorrect" }
if ($price.LiveMarketDataRequestAllowed -ne $false -or $price.ProductionAggressivePricingAllowed -ne $false) { Fail "Price control allows live data or production pricing" }
$marketability = Read-Json "phase-exec-sandbox-r006-marketability-control-review.json"
if ($marketability.Status -ne "Ready" -or $marketability.UsesLiveMarketData -ne $false -or $marketability.LimitOrderWithoutExplicitSandboxPriceBlocked -ne $true) { Fail "Marketability control review unsafe" }

$guardrail = Read-Json "phase-exec-sandbox-r006-sandbox-guardrail-revalidation.json"
if ($guardrail.SandboxCredentialPresent -ne $true -or $guardrail.CredentialValuesRedacted -ne $true) { Fail "Sandbox credentials not present/redacted" }
if ($guardrail.SandboxKillSwitchOpen -ne $true -or $guardrail.ProductionRouteBlocked -ne $true) { Fail "Guardrails not ready" }
if ($guardrail.MaxSandboxOrderCount -ne 3 -or [decimal]$guardrail.MaxTotalSandboxQuantity -ne 0.3) { Fail "Guardrail caps incorrect" }
if ($guardrail.PlannedSubmittedOrderCount -ne 0 -or [decimal]$guardrail.PlannedSubmittedQuantity -ne 0) { Fail "R006 should be blocked before submission" }
foreach ($property in @("DirectCrossExecutionAllowed", "NonmajorExecutionAllowed", "ProductionVenueAllowed", "ProductionCredentialsAllowed", "PaperLedgerCommitAllowed", "ProductionLedgerCommitAllowed", "StateMutationAllowed", "SchedulerAllowed", "Legacy06AcceptedAsFutureCanonical")) {
    if ($guardrail.$property -ne $false) { Fail "Forbidden guardrail enabled: $property" }
}
if ($guardrail.MultiSymbolExpansionBlockedByMissingRules -ne $true) { Fail "Multi-symbol block reason missing" }

$selected = Read-Json "phase-exec-sandbox-r006-selected-smoke-symbols.json"
if ($selected.MultiSymbolSetEligible -ne $false) { Fail "Multi-symbol set should not be eligible without AUDUSD/GBPUSD rules" }
if (@($selected.SubmittedSymbols).Count -ne 0) { Fail "No R006 symbols should be submitted when blocked by controls" }
if (@($selected.SkippedSymbols | Where-Object { $_.Reason -eq "MissingLocalSymbolSpecificQuantityRule" }).Count -lt 2) { Fail "Missing-rule skips not recorded" }

$logon = Read-Json "phase-exec-sandbox-r006-fix-logon-confirmation.json"
if ($logon.LogonAttempted -ne $false -or $logon.LogonConfirmed -ne $false -or $logon.BlockedBeforeLogon -ne $true) { Fail "R006 should be blocked before sandbox logon" }
if ($logon.NewOrderSingleSentAfterLogonConfirmed -ne $false) { Fail "NewOrderSingle state unsafe" }

$submissions = Read-Json "phase-exec-sandbox-r006-sandbox-submission-results.json"
if ($submissions.SubmittedOrderCount -ne 0 -or [decimal]$submissions.TotalSubmittedQuantity -ne 0) { Fail "R006 submitted orders despite guardrail block" }
if ($submissions.ProductionSubmission -ne $false -or $submissions.SandboxOnly -ne $true) { Fail "Submission artifact unsafe" }
$routes = Read-Json "phase-exec-sandbox-r006-sandbox-routes.json"
if ($routes.RouteCount -ne 0 -or $routes.ProductionRouteCreated -ne $false) { Fail "Route artifact unsafe" }
$reports = Read-Json "phase-exec-sandbox-r006-sandbox-execution-reports.json"
if ($reports.ExecutionReportCount -ne 0 -or $reports.ProductionExecutionReport -ne $false) { Fail "Execution report artifact unsafe" }
$fills = Read-Json "phase-exec-sandbox-r006-sandbox-fill-reports.json"
if ($fills.FillCount -ne 0 -or $fills.ProductionFill -ne $false) { Fail "Fill artifact unsafe" }

$reconciliation = Read-Json "phase-exec-sandbox-r006-sandbox-reconciliation-result.json"
if ($reconciliation.R006AttemptedOrderCount -ne 0 -or $reconciliation.R006FillCount -ne 0) { Fail "R006 reconciliation should show no attempted/fill orders" }
if ($reconciliation.ProductionOrderCreated -ne $false -or $reconciliation.ProductionRouteCreated -ne $false -or $reconciliation.ProductionFillOrReportCreated -ne $false -or $reconciliation.ProductionLedgerMutation -ne $false -or $reconciliation.ProductionStateMutation -ne $false -or $reconciliation.SandboxOnly -ne $true) {
    Fail "Reconciliation artifact unsafe"
}
$decision = Read-Json "phase-exec-sandbox-r006-smoke-decision.json"
if ($decision.Decision -ne "R009SandboxMultiSymbolSmokeBlockedByControls" -or $decision.MultiSymbolSmokeSubmitted -ne $false -or $decision.NotProductionApproval -ne $true) { Fail "Smoke decision incorrect" }

$audit = Read-Json "phase-exec-sandbox-r006-sandbox-audit-record.json"
if ($audit.SandboxOnly -ne $true -or $audit.AcceptedFillReviewed -ne $true -or $audit.QuantityPriceControlsReady -ne $true -or $audit.R006SandboxOrderSubmitted -ne $false) { Fail "Sandbox audit record incorrect" }
if ($audit.ProductionOrderCreated -ne $false -or $audit.ProductionRouteCreated -ne $false -or $audit.ProductionFillOrReportCreated -ne $false -or $audit.ProductionLedgerCommit -ne $false -or $audit.ProductionStateMutation -ne $false) { Fail "Sandbox audit record allows production path" }

$direct = Read-Json "phase-exec-sandbox-r006-direct-cross-exclusion-preservation.json"
if ($direct.DirectCrossExecutionAllowed -ne $false -or $direct.DirectCrossExecutionIntentRejected -ne $true) { Fail "Direct-cross exclusion weakened" }
$whitelist = Read-Json "phase-exec-sandbox-r006-usd-pair-whitelist-preservation.json"
if ($whitelist.NonWhitelistedSymbolAllowed -ne $false -or $whitelist.AudusdStatus -ne "SupportedAndNotFailed") { Fail "Whitelist/AUDUSD preservation failed" }
$usdjpy = Read-Json "phase-exec-sandbox-r006-usdjpy-caveat-preservation.json"
if ($usdjpy.NormalizedPortfolioSymbol -ne "JPYUSD" -or $usdjpy.ExecutionTradableSymbol -ne "USDJPY" -or $usdjpy.RequiresInversion -ne $true -or $usdjpy.SecurityID -ne "4004" -or $usdjpy.SecurityIDSource -ne "8") { Fail "USDJPY caveat weakened" }
$legacy = Read-Json "phase-exec-sandbox-r006-legacy-compatibility-preservation.json"
if ($legacy.Legacy06AcceptedAsFutureCanonical -ne $false -or $legacy.UsedAsFutureCanonical -ne $false) { Fail "Legacy :06 accepted as future canonical" }
$cost = Read-Json "phase-exec-sandbox-r006-cost-guidance-preservation.json"
if ($cost.Universalized -ne $false) { Fail "5 USD/million universalized" }
$nonmajor = Read-Json "phase-exec-sandbox-r006-nonmajor-calibration-preservation.json"
if ($nonmajor.SandboxExecutionAllowed -ne $false) { Fail "Nonmajor/EM/scandi/CNH execution allowed" }

$forbidden = Read-Json "phase-exec-sandbox-r006-forbidden-actions-audit.json"
foreach ($property in @(
    "LmaxProductionUsed",
    "ProductionCredentialsUsed",
    "NonSandboxBrokerRouteUsed",
    "CredentialValuesPrintedOrPersisted",
    "PolygonCalled",
    "UnrelatedExternalApiCalled",
    "SchedulerServicePollingBackgroundJobIntroduced",
    "NewOrderSingleSentBeforeLogonConfirmed",
    "MoreThanMaxSandboxOrderCountSubmitted",
    "TotalSandboxQuantityExceedsCap",
    "QuantityViolatesDiscoveredRule",
    "UnsupportedSymbolSubmitted",
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

$secretAudit = Read-Json "phase-exec-sandbox-r006-no-secret-persistence-audit.json"
if ($secretAudit.SecretValuesSerialized -ne $false -or $secretAudit.CredentialVariableNamesOnly -ne $true -or $secretAudit.CredentialValuesRedacted -ne $true) { Fail "Secret persistence audit failed" }

$allText = Get-ChildItem -LiteralPath $artifactDir -File -Filter "phase-exec-sandbox-r006-*.json" | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }
$combined = $allText -join "`n"
foreach ($banned in @("QQ_LMAX_FIX_PASSWORD", "FixPassword`":`"", "FixUsername`":`"", "Password`":`"", "Username`":`"")) {
    if ($combined -match [regex]::Escape($banned)) { Fail "Possible credential value field persisted: $banned" }
}

$evidence = Read-Json "phase-exec-sandbox-r006-build-test-validator-evidence.json"
if ($evidence.Build -ne "Passed") { Fail "Build evidence missing or not passed" }
if ($evidence.FocusedSandboxTests -ne "Passed") { Fail "Focused sandbox test evidence missing or not passed" }
if ($evidence.Validator -ne "Passed") { Fail "Validator evidence missing or not passed" }

Write-Host "EXEC-SANDBOX-R006 validator passed."
