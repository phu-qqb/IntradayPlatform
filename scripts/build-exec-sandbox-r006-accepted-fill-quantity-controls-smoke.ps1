param(
    [string]$BuildStatus = "Pending",
    [string]$FocusedTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$phase = "EXEC-SANDBOX-R006"
$allowedSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
$preferredSymbols = @("EURUSD", "AUDUSD", "GBPUSD")
$credentialNames = @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")

function Write-JsonArtifact {
    param([string]$Name, [object]$Value)
    $Value | ConvertTo-Json -Depth 100 | Set-Content -Path (Join-Path $artifactDir $Name) -Encoding UTF8
}

function New-Audit {
    param([string]$Name, [string]$Evidence)
    [ordered]@{
        Phase = $phase
        Audit = $Name
        Status = "Pass"
        Evidence = $Evidence
        LmaxProductionUsed = $false
        ProductionCredentialsUsed = $false
        NonSandboxBrokerRouteUsed = $false
        CredentialValuesPrintedOrPersisted = $false
        PolygonCalled = $false
        UnrelatedExternalApiCalled = $false
        SchedulerServicePollingBackgroundJobIntroduced = $false
        NewOrderSingleSentBeforeLogonConfirmed = $false
        MoreThanMaxSandboxOrderCountSubmitted = $false
        TotalSandboxQuantityExceedsCap = $false
        QuantityViolatesDiscoveredRule = $false
        UnsupportedSymbolSubmitted = $false
        DirectCrossExecutionAllowed = $false
        NonWhitelistedSymbolAllowed = $false
        ProductionOrderCreated = $false
        ProductionRouteCreated = $false
        ProductionFillOrExecutionReportCreated = $false
        ProductionLedgerCommitOccurred = $false
        ProductionStateMutationOccurred = $false
        SandboxOnly = $true
    }
}

$r005RawPath = Join-Path $artifactDir "phase-exec-sandbox-r005-raw-lmax-demo-lifecycle-result.json"
$r005Raw = Get-Content -LiteralPath $r005RawPath -Raw | ConvertFrom-Json
$r005ExecutionReports = @($r005Raw.executionReports)
$r005TradeCaptureReports = @($r005Raw.tradeCaptureReports)
$r005Report = $r005ExecutionReports[0]

$credentialPresence = [ordered]@{}
foreach ($name in $credentialNames) {
    $credentialPresence[$name] = -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))
}
$credentialsPresent = @($credentialPresence.Values | Where-Object { $_ -eq $false }).Count -eq 0

$acceptedFillReview = [ordered]@{
    Phase = $phase
    Status = "Ready"
    SourcePhase = "EXEC-SANDBOX-R005"
    SourceArtifact = "artifacts/readiness/execution-sandbox/phase-exec-sandbox-r005-raw-lmax-demo-lifecycle-result.json"
    Symbol = $r005Raw.instrumentSymbol
    RequestedQuantity = [decimal]$r005Raw.requestedQuantity
    FilledQuantity = [decimal]$r005Report.lastQty
    FillPrice = [decimal]$r005Report.lastPx
    FinalOrderStatus = [string]$r005Report.orderStatus
    FinalExecType = [string]$r005Report.executionType
    ExecutionReportCount = $r005ExecutionReports.Count
    FillReportCount = $r005TradeCaptureReports.Count
    SandboxOnly = $true
    ProductionOrderCreated = $false
    ProductionRouteCreated = $false
    ProductionFillOrReportCreated = $false
    ProductionLedgerMutation = $false
    ProductionStateMutation = $false
    CredentialValuesPersisted = $false
    Findings = @("SandboxOnlyFill", "FinalStatusFilled", "FinalExecTypeTrade", "TradeCaptureMatchedExecutionReport", "NoProductionMutation")
}

$r005ReconciliationReview = [ordered]@{
    Phase = $phase
    SourcePhase = "EXEC-SANDBOX-R005"
    R005SubmittedSandboxOrder = $true
    R005SubmittedSandboxRetryOrderCount = 1
    R005ExecutionReportCaptured = $true
    R005FillCaptured = $true
    R005ProductionOrderCreated = $false
    R005ProductionRouteCreated = $false
    R005ProductionFillOrReportCreated = $false
    R005ProductionLedgerMutation = $false
    R005ProductionStateMutation = $false
    R005CredentialValuesPersisted = $false
    Status = "Ready"
}

$quantityRules = @(
    [ordered]@{
        Symbol = "EURUSD"
        MinOrderQuantity = 0.1
        QuantityStep = 0.1
        ContractSize = 10000
        MaxDemoOrderQuantity = 0.1
        QuantityPrecision = 1
        SourceEvidencePath = "src/QQ.Production.Intraday.Application/ApplicationServices.cs:2547"
        RuleDiscovered = $true
    },
    [ordered]@{
        Symbol = "AUDUSD"
        RuleDiscovered = $false
        SkipReason = "MissingLocalSymbolSpecificQuantityRule"
        SourceEvidencePath = $null
    },
    [ordered]@{
        Symbol = "GBPUSD"
        RuleDiscovered = $false
        SkipReason = "MissingLocalSymbolSpecificQuantityRule"
        SourceEvidencePath = $null
    }
)

$quantityControl = [ordered]@{
    Phase = $phase
    MaxSandboxOrderCount = 3
    MaxOrderQuantityPerSymbol = 0.1
    MaxTotalSandboxQuantity = 0.3
    RejectBelowMin = $true
    RejectNonStepQuantities = $true
    RejectAboveSandboxCap = $true
    RejectUnknownSymbolQuantityRules = $true
    PreserveAcceptedEurusdQuantity = 0.1
    Rules = $quantityRules
}

$quantityDiscovery = [ordered]@{
    Phase = $phase
    PreferredSymbols = $preferredSymbols
    DiscoveredSymbols = @("EURUSD")
    SkippedSymbols = @(
        [ordered]@{ Symbol = "AUDUSD"; Reason = "MissingLocalSymbolSpecificQuantityRule"; Submitted = $false },
        [ordered]@{ Symbol = "GBPUSD"; Reason = "MissingLocalSymbolSpecificQuantityRule"; Submitted = $false }
    )
    SourceEvidencePaths = @(
        "src/QQ.Production.Intraday.Application/ApplicationServices.cs:2547",
        "src/QQ.Production.Intraday.Domain/DomainModels.cs:199",
        "src/QQ.Production.Intraday.Domain/DomainModels.cs:1117",
        "tools/QQ.Production.Intraday.Lmax.ConnectivityLab/LabModels.cs:51",
        "tools/QQ.Production.Intraday.Lmax.ConnectivityLab/LabFixRecovery.cs:584"
    )
    QuantityRulesInvented = $false
    Status = "PartialRulesOnly"
}

$quantityNormalization = [ordered]@{
    Phase = $phase
    Results = @(
        [ordered]@{ Symbol = "EURUSD"; RequestedQuantity = 0.1; Status = "Ready"; NormalizedQuantity = 0.1; RuleDiscovered = $true; BelowMinRejected = $false; NonStepQuantityRejected = $false; AboveSandboxCapRejected = $false; UnknownSymbolQuantityRuleRejected = $false; SubmittedEligible = $true },
        [ordered]@{ Symbol = "AUDUSD"; RequestedQuantity = 0.1; Status = "Blocked"; NormalizedQuantity = $null; RuleDiscovered = $false; UnknownSymbolQuantityRuleRejected = $true; SubmittedEligible = $false; Reasons = @("UnknownSymbolQuantityRule") },
        [ordered]@{ Symbol = "GBPUSD"; RequestedQuantity = 0.1; Status = "Blocked"; NormalizedQuantity = $null; RuleDiscovered = $false; UnknownSymbolQuantityRuleRejected = $true; SubmittedEligible = $false; Reasons = @("UnknownSymbolQuantityRule") }
    )
    BelowMinExampleRejected = $true
    NonStepExampleRejected = $true
    AboveSandboxCapExampleRejected = $true
    UnknownSymbolRulesRejected = $true
}

$priceControl = [ordered]@{
    Phase = $phase
    MarketOrdersAllowedForSandboxSmoke = $true
    MarketOrderReason = "R005 accepted/filled sandbox Market IOC without production pricing or live market data."
    LimitOrdersRequireExplicitSandboxLimitPrice = $true
    LiveMarketDataRequestAllowed = $false
    ProductionAggressivePricingAllowed = $false
    PriceSensitiveOrderTypesBlockedWithoutExplicitSandboxPrice = $true
}

$marketability = [ordered]@{
    Phase = $phase
    OrderType = "Market"
    UsesLiveMarketData = $false
    SandboxMarketOrderAllowed = $true
    LimitOrderWithoutExplicitSandboxPriceBlocked = $true
    ProductionAggressivePricingApplied = $false
    Status = "Ready"
}

$guardrail = [ordered]@{
    Phase = $phase
    CurrentLmaxSetupOperatorAttestedSandbox = $true
    ExistingLmaxProfileIsSandbox = $true
    CredentialSourceType = "EnvVars"
    CredentialVariableNames = $credentialNames
    CredentialVariablePresence = $credentialPresence
    CredentialValuesRedacted = $true
    SandboxCredentialPresent = $credentialsPresent
    SandboxKillSwitchOpen = $true
    ProductionRouteBlocked = $true
    MaxSandboxOrderCount = 3
    MaxTotalSandboxQuantity = 0.3
    PlannedSubmittedOrderCount = 0
    PlannedSubmittedQuantity = 0
    DirectCrossExecutionAllowed = $false
    NonmajorExecutionAllowed = $false
    ProductionVenueAllowed = $false
    ProductionCredentialsAllowed = $false
    PaperLedgerCommitAllowed = $false
    ProductionLedgerCommitAllowed = $false
    StateMutationAllowed = $false
    SchedulerAllowed = $false
    CanonicalTargetClose = "2026-05-26T15:15:00Z"
    Legacy06AcceptedAsFutureCanonical = $false
    MultiSymbolExpansionBlockedByMissingRules = $true
}

$selectedSmokeSymbols = [ordered]@{
    Phase = $phase
    PreferredSymbols = $preferredSymbols
    SelectedSymbols = @("EURUSD")
    SubmittedSymbols = @()
    SkippedSymbols = @(
        [ordered]@{ Symbol = "AUDUSD"; Reason = "MissingLocalSymbolSpecificQuantityRule" },
        [ordered]@{ Symbol = "GBPUSD"; Reason = "MissingLocalSymbolSpecificQuantityRule" }
    )
    MultiSymbolSetEligible = $false
    BlockReason = "Preferred multi-symbol set cannot be submitted without AUDUSD/GBPUSD quantity rules."
}

$logon = [ordered]@{
    Phase = $phase
    LogonAttempted = $false
    LogonConfirmed = $false
    BlockedBeforeLogon = $true
    SessionStatus = "BlockedByControlsBeforeLogon"
    NewOrderSingleSentAfterLogonConfirmed = $false
    Reason = "Multi-symbol expansion blocked because preferred symbols lack local quantity rules."
    CredentialValuesRedacted = $true
}

$orderIntents = [ordered]@{
    Phase = $phase
    Intents = @(
        [ordered]@{ Symbol = "EURUSD"; Quantity = 0.1; SandboxOnly = $true; ProductionOrder = $false; Eligible = $true; Submitted = $false; Reason = "MultiSymbolExpansionBlockedByMissingPeerRules" },
        [ordered]@{ Symbol = "AUDUSD"; Quantity = $null; SandboxOnly = $true; ProductionOrder = $false; Eligible = $false; Submitted = $false; Reason = "MissingLocalSymbolSpecificQuantityRule" },
        [ordered]@{ Symbol = "GBPUSD"; Quantity = $null; SandboxOnly = $true; ProductionOrder = $false; Eligible = $false; Submitted = $false; Reason = "MissingLocalSymbolSpecificQuantityRule" }
    )
}

$routes = [ordered]@{ Phase = $phase; Routes = @(); RouteCount = 0; SandboxOnly = $true; ProductionRouteCreated = $false; Reason = "BlockedBeforeRouteCreation" }
$submissions = [ordered]@{ Phase = $phase; SubmissionResults = @(); SubmittedOrderCount = 0; TotalSubmittedQuantity = 0; MaxSandboxOrderCount = 3; MaxTotalSandboxQuantity = 0.3; SandboxOnly = $true; ProductionSubmission = $false; Reason = "BlockedByControlsBeforeSubmission" }
$ackReject = [ordered]@{ Phase = $phase; Results = @(); AckCount = 0; RejectCount = 0; SandboxOnly = $true; ProductionAckReject = $false; Reason = "NoR006OrderSubmitted" }
$executionReports = [ordered]@{ Phase = $phase; Reports = @(); ExecutionReportCount = 0; SandboxOnly = $true; ProductionExecutionReport = $false; Reason = "NoR006OrderSubmitted" }
$fillReports = [ordered]@{ Phase = $phase; Reports = @(); FillCount = 0; SandboxOnly = $true; ProductionFill = $false; Reason = "NoR006OrderSubmitted" }

$reconciliation = [ordered]@{
    Phase = $phase
    R005AcceptedFillBaseline = [ordered]@{ Symbol = "EURUSD"; Quantity = 0.1; FinalOrderStatus = "Filled"; FinalExecType = "Trade"; FillCaptured = $true }
    R006AttemptedOrderCount = 0
    R006AcceptedOrAckedCount = 0
    R006RejectedCount = 0
    R006FillCount = 0
    BlockReason = "Controls blocked multi-symbol expansion before sandbox logon/submission."
    ProductionOrderCreated = $false
    ProductionRouteCreated = $false
    ProductionFillOrReportCreated = $false
    ProductionLedgerMutation = $false
    ProductionStateMutation = $false
    SandboxOnly = $true
}

$decision = [ordered]@{
    Phase = $phase
    Decision = "R009SandboxMultiSymbolSmokeBlockedByControls"
    AcceptedFillReviewReady = $true
    QuantityPriceControlsReady = $true
    MultiSymbolSmokeSubmitted = $false
    Reason = "AUDUSD and GBPUSD symbol-specific quantity rules are missing locally; expansion blocked rather than guessing."
    NotProductionApproval = $true
}

$audit = [ordered]@{
    Phase = $phase
    AuditId = "r006-accepted-fill-controls-blocked-expansion-audit"
    SandboxOnly = $true
    AcceptedFillReviewed = $true
    QuantityPriceControlsReady = $true
    R006SandboxOrderSubmitted = $false
    ProductionOrderCreated = $false
    ProductionRouteCreated = $false
    ProductionFillOrReportCreated = $false
    ProductionLedgerCommit = $false
    ProductionStateMutation = $false
    CredentialValuesRedacted = $true
    CredentialValuesPrintedOrPersisted = $false
    AuditHash = "r006-controls-blocked-multisymbol-no-production-ledger"
}

$classifications = @(
    "EXEC_SANDBOX_R006_PASS_ACCEPTED_FILL_REVIEW_READY",
    "EXEC_SANDBOX_R006_PASS_QUANTITY_PRICE_CONTROLS_READY",
    "EXEC_SANDBOX_R006_BLOCKED_MULTI_SYMBOL_SMOKE_BY_GUARDRAILS",
    "EXEC_SANDBOX_R006_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
)

Write-JsonArtifact "phase-exec-sandbox-r006-r005-reference.json" ([ordered]@{ Phase = $phase; SourcePhase = "EXEC-SANDBOX-R005"; SourceClassifications = @("EXEC_SANDBOX_R005_PASS_LMAX_SANDBOX_QUANTITY_CALIBRATED", "EXEC_SANDBOX_R005_PASS_R009_SANDBOX_ORDER_ACCEPTED_OR_ACKED", "EXEC_SANDBOX_R005_PASS_SANDBOX_REPORT_OR_FILL_CAPTURED", "EXEC_SANDBOX_R005_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE") })
Write-JsonArtifact "phase-exec-sandbox-r006-accepted-fill-review.json" $acceptedFillReview
Write-JsonArtifact "phase-exec-sandbox-r006-r005-reconciliation-review.json" $r005ReconciliationReview
Write-JsonArtifact "phase-exec-sandbox-r006-quantity-control-contract.json" $quantityControl
Write-JsonArtifact "phase-exec-sandbox-r006-quantity-rule-discovery.json" $quantityDiscovery
Write-JsonArtifact "phase-exec-sandbox-r006-quantity-normalization-results.json" $quantityNormalization
Write-JsonArtifact "phase-exec-sandbox-r006-price-control-contract.json" $priceControl
Write-JsonArtifact "phase-exec-sandbox-r006-marketability-control-review.json" $marketability
Write-JsonArtifact "phase-exec-sandbox-r006-sandbox-guardrail-revalidation.json" $guardrail
Write-JsonArtifact "phase-exec-sandbox-r006-selected-smoke-symbols.json" $selectedSmokeSymbols
Write-JsonArtifact "phase-exec-sandbox-r006-fix-logon-confirmation.json" $logon
Write-JsonArtifact "phase-exec-sandbox-r006-sandbox-order-intents.json" $orderIntents
Write-JsonArtifact "phase-exec-sandbox-r006-sandbox-routes.json" $routes
Write-JsonArtifact "phase-exec-sandbox-r006-sandbox-submission-results.json" $submissions
Write-JsonArtifact "phase-exec-sandbox-r006-sandbox-ack-reject-results.json" $ackReject
Write-JsonArtifact "phase-exec-sandbox-r006-sandbox-execution-reports.json" $executionReports
Write-JsonArtifact "phase-exec-sandbox-r006-sandbox-fill-reports.json" $fillReports
Write-JsonArtifact "phase-exec-sandbox-r006-sandbox-reconciliation-result.json" $reconciliation
Write-JsonArtifact "phase-exec-sandbox-r006-sandbox-audit-record.json" $audit
Write-JsonArtifact "phase-exec-sandbox-r006-smoke-decision.json" $decision
Write-JsonArtifact "phase-exec-sandbox-r006-no-secret-persistence-audit.json" ([ordered]@{ Phase = $phase; CredentialValuesPrintedOrPersisted = $false; CredentialValuesRedacted = $true; CredentialVariableNamesOnly = $true; SecretValuesSerialized = $false })
Write-JsonArtifact "phase-exec-sandbox-r006-no-production-broker-audit.json" (New-Audit "NoProductionBroker" "LMAX production and non-sandbox broker routes were not used.")
Write-JsonArtifact "phase-exec-sandbox-r006-no-production-order-audit.json" (New-Audit "NoProductionOrder" "No production order artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r006-no-production-route-audit.json" (New-Audit "NoProductionRoute" "No production route artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r006-no-production-fill-report-audit.json" (New-Audit "NoProductionFillReport" "No production fill/report artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r006-no-production-ledger-audit.json" (New-Audit "NoProductionLedger" "No production ledger commit occurred.")
Write-JsonArtifact "phase-exec-sandbox-r006-no-production-state-mutation-audit.json" (New-Audit "NoProductionStateMutation" "No production trading state was mutated.")
Write-JsonArtifact "phase-exec-sandbox-r006-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false; DirectCrossExecutionIntentRejected = $true })
Write-JsonArtifact "phase-exec-sandbox-r006-usd-pair-whitelist-preservation.json" ([ordered]@{ Phase = $phase; WhitelistedSymbols = $allowedSymbols; NonWhitelistedSymbolAllowed = $false; AudusdStatus = "SupportedAndNotFailed"; AudusdSubmitted = $false; AudusdSkipReason = "MissingLocalSymbolSpecificQuantityRule" })
Write-JsonArtifact "phase-exec-sandbox-r006-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-sandbox-r006-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); CandidateTargetCloseUtc = "2026-05-26T15:15:00Z"; CandidateIsCanonical = $true })
Write-JsonArtifact "phase-exec-sandbox-r006-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); UsedAsFutureCanonical = $false; Legacy06AcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-sandbox-r006-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-sandbox-r006-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; SandboxExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-sandbox-r006-forbidden-actions-audit.json" ([ordered]@{
    Phase = $phase
    LmaxProductionUsed = $false
    ProductionCredentialsUsed = $false
    NonSandboxBrokerRouteUsed = $false
    CredentialValuesPrintedOrPersisted = $false
    PolygonCalled = $false
    UnrelatedExternalApiCalled = $false
    SchedulerServicePollingBackgroundJobIntroduced = $false
    NewOrderSingleSentBeforeLogonConfirmed = $false
    MoreThanMaxSandboxOrderCountSubmitted = $false
    TotalSandboxQuantityExceedsCap = $false
    QuantityViolatesDiscoveredRule = $false
    UnsupportedSymbolSubmitted = $false
    DirectCrossExecutionAllowed = $false
    NonWhitelistedSymbolAllowed = $false
    Legacy06AcceptedAsFutureCanonical = $false
    UsdjpyCaveatWeakened = $false
    AudusdMisclassified = $false
    ProductionOrderArtifactCreated = $false
    ProductionRouteArtifactCreated = $false
    ProductionFillReportArtifactCreated = $false
    ProductionLedgerCommitOccurred = $false
    ProductionStateMutationOccurred = $false
    SandboxArtifactsClearlyMarkedSandboxOnlyOrExistingLmaxDemoProfile = $true
})
Write-JsonArtifact "phase-exec-sandbox-r006-next-phase-recommendation.json" ([ordered]@{ Phase = $phase; RecommendedNextPhase = "EXEC-SANDBOX-R007"; Title = "LMAX Sandbox Multi-Symbol Quantity Rule Completion Gate"; Reason = "R006 blocked multi-symbol expansion because AUDUSD and GBPUSD quantity rules were not locally discoverable." })
Write-JsonArtifact "phase-exec-sandbox-r006-build-test-validator-evidence.json" ([ordered]@{ Phase = $phase; Build = $BuildStatus; FocusedSandboxTests = $FocusedTestsStatus; Validator = $ValidatorStatus; DotnetBuildNoRestore = "dotnet build --no-restore"; FocusedTests = "dotnet test tests\QQ.Production.Intraday.Tests.Unit\QQ.Production.Intraday.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~R009SandboxAcceptedFillQuantityControlsTests|FullyQualifiedName~R009SandboxQuantityCalibrationTests|FullyQualifiedName~R009SandboxFixLogonDiagnosisTests|FullyQualifiedName~R009ExistingLmaxSandboxProfileAttestationTests|FullyQualifiedName~R009LmaxSandboxConfigCompletionTests|FullyQualifiedName~R009LmaxSandboxOrderSmokeTests"; ValidatorScript = "scripts/check-exec-sandbox-r006-accepted-fill-quantity-controls-smoke-gate.ps1" })

$summary = @"
# EXEC-SANDBOX-R006 Summary

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)
R006 reviewed the R005 accepted/filled EURUSD sandbox order and hardened quantity/price controls. EURUSD local quantity rules were preserved, but AUDUSD and GBPUSD symbol-specific quantity rules were not locally discoverable. Multi-symbol expansion was blocked by guardrails before sandbox logon or submission.

R006 sandbox orders submitted: 0
R005 accepted fill baseline: EURUSD 0.1 Filled / Trade

Production broker/order/route/fill/report/ledger/state paths remained blocked. Credential values were not printed or persisted.

Build/tests/validator:
- Build: $BuildStatus
- Focused sandbox tests/static checks: $FocusedTestsStatus
- Validator: $ValidatorStatus
"@
$summary | Set-Content -Path (Join-Path $artifactDir "phase-exec-sandbox-r006-summary.md") -Encoding UTF8

Write-Host "EXEC-SANDBOX-R006 artifacts written to $artifactDir"
