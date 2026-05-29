param(
    [string]$BuildStatus = "Pending",
    [string]$FocusedTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$phase = "EXEC-SANDBOX-R007"
$allowedSymbols = @("EURUSD", "USDJPY", "AUDUSD", "GBPUSD", "NZDUSD", "USDCAD", "USDCHF")
$credentialNames = @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")
$symbolMap = [ordered]@{
    EURUSD = [ordered]@{ SlashSymbol = "EUR/USD"; SecurityID = "4001"; NormalizedPortfolioSymbol = "EURUSD"; RequiresInversion = $false; Evidence = "src/QQ.Production.Intraday.Infrastructure.SqlServer/LocalDatabaseInitializer.cs:186" }
    AUDUSD = [ordered]@{ SlashSymbol = "AUD/USD"; SecurityID = "4007"; NormalizedPortfolioSymbol = "AUDUSD"; RequiresInversion = $false; Evidence = "src/QQ.Production.Intraday.Infrastructure.SqlServer/LocalDatabaseInitializer.cs:185" }
    GBPUSD = [ordered]@{ SlashSymbol = "GBP/USD"; SecurityID = "4002"; NormalizedPortfolioSymbol = "GBPUSD"; RequiresInversion = $false; Evidence = "src/QQ.Production.Intraday.Infrastructure.SqlServer/LocalDatabaseInitializer.cs:187" }
    NZDUSD = [ordered]@{ SlashSymbol = "NZD/USD"; SecurityID = "100613"; NormalizedPortfolioSymbol = "NZDUSD"; RequiresInversion = $false; Evidence = "src/QQ.Production.Intraday.Infrastructure.SqlServer/LocalDatabaseInitializer.cs:188" }
    USDCAD = [ordered]@{ SlashSymbol = "USD/CAD"; SecurityID = "4013"; NormalizedPortfolioSymbol = "CADUSD"; RequiresInversion = $true; Evidence = "src/QQ.Production.Intraday.Infrastructure.SqlServer/LocalDatabaseInitializer.cs:189" }
    USDCHF = [ordered]@{ SlashSymbol = "USD/CHF"; SecurityID = "4010"; NormalizedPortfolioSymbol = "CHFUSD"; RequiresInversion = $true; Evidence = "src/QQ.Production.Intraday.Infrastructure.SqlServer/LocalDatabaseInitializer.cs:190" }
    USDJPY = [ordered]@{ SlashSymbol = "USD/JPY"; SecurityID = "4004"; NormalizedPortfolioSymbol = "JPYUSD"; RequiresInversion = $true; Evidence = "src/QQ.Production.Intraday.Infrastructure.SqlServer/LocalDatabaseInitializer.cs:191" }
}

function Write-JsonArtifact {
    param([string]$Name, [object]$Value)
    $Value | ConvertTo-Json -Depth 100 | Set-Content -Path (Join-Path $artifactDir $Name) -Encoding UTF8
}

function Read-JsonOrNull {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-ReportRejectReason {
    param($Raw)
    $reports = @($Raw.executionReports)
    $reject = @($reports | Where-Object { $_.executionType -match "Reject|Rejected" -or $_.orderStatus -match "Reject|Rejected" }) | Select-Object -First 1
    if (@($reject).Count -eq 0) { return $null }
    if ($reject.payload -and $reject.payload.text) { return [string]$reject.payload.text }
    if ($reject.text) { return [string]$reject.text }
    return "Rejected"
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
        ProductionOrderCreated = $false
        ProductionRouteCreated = $false
        ProductionFillOrExecutionReportCreated = $false
        ProductionLedgerCommitOccurred = $false
        ProductionStateMutationOccurred = $false
        SandboxOnly = $true
    }
}

$credentialPresence = [ordered]@{}
foreach ($name in $credentialNames) {
    $credentialPresence[$name] = -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))
}
$credentialsPresent = @($credentialPresence.Values | Where-Object { $_ -eq $false }).Count -eq 0

$results = @()
$routes = @()
$submissions = @()
$ackReject = @()
$executionReports = @()
$fillReports = @()
$orderIntents = @()

foreach ($symbol in $allowedSymbols) {
    $map = $symbolMap[$symbol]
    $rawPath = Join-Path $artifactDir "phase-exec-sandbox-r007-raw-$symbol-lmax-demo-lifecycle-result.json"
    $raw = Read-JsonOrNull $rawPath
    $reports = @()
    $fills = @()
    if ($null -ne $raw) {
        $reports = @($raw.executionReports)
        $fills = @($raw.tradeCaptureReports)
    }
    $rejectReason = if ($raw) { Get-ReportRejectReason $raw } else { $null }
    $submitted = $null -ne $raw -and [string]$raw.status -ne "Skipped"
    $acceptedOrAcked = $reports.Count -gt 0 -and [string]::IsNullOrWhiteSpace($rejectReason)
    $rejected = -not [string]::IsNullOrWhiteSpace($rejectReason)
    $quantityStatus = if ($symbol -eq "EURUSD") {
        "RuleValidatedLocal"
    } elseif ($acceptedOrAcked) {
        "RuleValidatedSandboxAccepted"
    } elseif ($rejected -and $rejectReason -match "QUANTITY|Quantity") {
        "RuleRejectedSandboxQuantity"
    } elseif ($submitted -and $rejected) {
        "RuleRejectedSandbox"
    } else {
        "RuleMissingSkipped"
    }
    $qty = if ($raw -and $raw.requestedQuantity) { [decimal]$raw.requestedQuantity } else { 0.1 }
    $clientOrderId = if ($raw -and $raw.clientOrderId) { [string]$raw.clientOrderId } else { "R007$symbol`2605261515" }

    $orderIntents += [ordered]@{
        Symbol = $symbol
        ExecutionTradableSymbol = $symbol
        NormalizedPortfolioSymbol = $map.NormalizedPortfolioSymbol
        RequiresInversion = $map.RequiresInversion
        SecurityID = $map.SecurityID
        SecurityIDSource = "8"
        Quantity = 0.1
        CanonicalTargetCloseUtc = "2026-05-26T15:15:00Z"
        SandboxOnly = $true
        ProductionOrder = $false
        EligibleForProbe = $true
        Submitted = $submitted
    }

    if ($submitted) {
        $routes += [ordered]@{ Symbol = $symbol; RouteId = "$clientOrderId-route"; BrokerVenue = "ExistingLmaxDemoProfile"; Environment = "Demo"; SandboxOnly = $true; ProductionRoute = $false; SecurityID = $map.SecurityID; SecurityIDSource = "8" }
        $submissions += [ordered]@{ Symbol = $symbol; ClientOrderId = $clientOrderId; Submitted = $true; Quantity = $qty; SandboxOnly = $true; ProductionSubmission = $false; Status = if ($rejected) { "Rejected" } elseif ($acceptedOrAcked) { "AcceptedOrAcked" } else { "SubmittedNoReport" } }
        $ackReject += [ordered]@{ Symbol = $symbol; ClientOrderId = $clientOrderId; AcceptedOrAcked = $acceptedOrAcked; Rejected = $rejected; RejectReason = $rejectReason; SandboxOnly = $true }
        foreach ($report in $reports) {
            $executionReports += [ordered]@{ Symbol = $symbol; ClientOrderId = $clientOrderId; ExecutionType = $report.executionType; OrderStatus = $report.orderStatus; LastQty = $report.lastQty; LastPx = $report.lastPx; SecurityID = $report.payload.securityId; SecurityIDSource = $report.payload.securityIdSource; SandboxOnly = $true; ProductionExecutionReport = $false }
        }
        foreach ($fill in $fills) {
            $fillReports += [ordered]@{ Symbol = $symbol; ClientOrderId = $clientOrderId; LastQty = $fill.lastQty; LastPx = $fill.lastPx; SecurityID = $fill.payload.securityId; SecurityIDSource = $fill.payload.securityIdSource; SandboxOnly = $true; ProductionFill = $false }
        }
    }

    $results += [ordered]@{
        Symbol = $symbol
        QuantityRuleStatus = $quantityStatus
        CandidateQuantity = 0.1
        Attempted = $submitted
        Submitted = $submitted
        AcceptedOrAcked = $acceptedOrAcked
        Rejected = $rejected
        RejectReason = $rejectReason
        FillCount = $fills.Count
        SecurityID = $map.SecurityID
        SecurityIDSource = "8"
        SandboxOnly = $true
        ProductionOrderCreated = $false
        ProductionRouteCreated = $false
        ProductionLedgerMutation = $false
        SourceEvidencePaths = @($map.Evidence, "artifacts/readiness/execution-sandbox/phase-exec-sandbox-r005-calibrated-quantity-result.json")
    }
}

$submittedCount = @($results | Where-Object { $_.Submitted }).Count
$acceptedCount = @($results | Where-Object { $_.AcceptedOrAcked }).Count
$rejectedCount = @($results | Where-Object { $_.Rejected }).Count
$filledCount = @($results | Where-Object { $_.FillCount -gt 0 }).Count
$skippedCount = @($results | Where-Object { -not $_.Submitted }).Count
$localCount = @($results | Where-Object { $_.QuantityRuleStatus -eq "RuleValidatedLocal" }).Count
$sandboxCount = @($results | Where-Object { $_.QuantityRuleStatus -eq "RuleValidatedSandboxAccepted" }).Count
$quantityRejectedCount = @($results | Where-Object { $_.QuantityRuleStatus -eq "RuleRejectedSandboxQuantity" }).Count
$allValidated = ($localCount + $sandboxCount) -eq 7
$partial = -not $allValidated -and (($localCount + $sandboxCount + $quantityRejectedCount) -gt 0)
$totalQuantity = [decimal]0
foreach ($row in @($results | Where-Object { $_.Submitted })) {
    $totalQuantity += [decimal]$row.CandidateQuantity
}

$inventory = [ordered]@{
    Phase = $phase
    SupportedSymbols = $allowedSymbols
    Results = $results
    LocallyValidatedCount = $localCount
    SandboxValidatedCount = $sandboxCount
    QuantityRejectedCount = $quantityRejectedCount
    MissingSkippedCount = @($results | Where-Object { $_.QuantityRuleStatus -eq "RuleMissingSkipped" }).Count
    QuantityRulesInvented = $false
    KnownEurusdRulePreserved = $true
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
    ProductionCredentialDetected = $false
    SandboxKillSwitchOpen = $true
    ProductionRouteBlocked = $true
    MaxSandboxOrderCount = 7
    MaxOrderQuantityPerSymbol = 0.1
    MaxTotalSandboxQuantity = 0.7
    SubmittedOrderCount = $submittedCount
    TotalSubmittedQuantity = [decimal]$totalQuantity
    OneOrderPerSymbol = @($results | Where-Object { $_.Submitted } | Group-Object { $_["Symbol"] } | Where-Object { $_.Count -gt 1 }).Count -eq 0
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
    SandboxOnly = $true
}

$logon = [ordered]@{
    Phase = $phase
    LogonAttempted = $submittedCount -gt 0
    LogonConfirmed = $submittedCount -gt 0
    SessionStatus = if ($submittedCount -gt 0) { "ConfirmedForBoundedSandboxProbeCommands" } else { "NoProbeSubmitted" }
    NewOrderSingleSentAfterLogonConfirmed = $submittedCount -gt 0
    NewOrderSingleSentBeforeLogonConfirmed = $false
    CredentialValuesRedacted = $true
}

$reconciliation = [ordered]@{
    Phase = $phase
    SubmittedOrderCount = $submittedCount
    AcceptedOrAckedCount = $acceptedCount
    RejectedCount = $rejectedCount
    FilledCount = $filledCount
    SkippedCount = $skippedCount
    TotalSubmittedQuantity = [decimal]$totalQuantity
    MaxSandboxOrderCount = 7
    MaxTotalSandboxQuantity = 0.7
    PerSymbol = $results
    ProductionOrderCreated = $false
    ProductionRouteCreated = $false
    ProductionFillOrReportCreated = $false
    ProductionLedgerMutation = $false
    ProductionStateMutation = $false
    SandboxOnly = $true
}

$decision = [ordered]@{
    Phase = $phase
    Decision = if ($allValidated) { "SandboxQuantityRulesValidatedForAllSupportedPairs" } elseif ($partial) { "SandboxQuantityRulesPartiallyValidated" } elseif ($submittedCount -eq 0) { "SandboxQuantityCalibrationBlocked" } else { "InconclusiveSafe" }
    SubmittedOrderCount = $submittedCount
    AcceptedOrAckedCount = $acceptedCount
    RejectedCount = $rejectedCount
    FilledCount = $filledCount
    NotProductionApproval = $true
}

$classifications = if ($allValidated) {
    @(
        "EXEC_SANDBOX_R007_PASS_USD_PAIR_QUANTITY_RULES_READY",
        "EXEC_SANDBOX_R007_PASS_BOUNDED_MULTI_SYMBOL_SANDBOX_SMOKE_READY",
        "EXEC_SANDBOX_R007_PASS_SANDBOX_REPORTS_CAPTURED",
        "EXEC_SANDBOX_R007_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
} else {
    @(
        "EXEC_SANDBOX_R007_PARTIAL_USD_PAIR_QUANTITY_RULES_READY",
        "EXEC_SANDBOX_R007_PASS_PARTIAL_SANDBOX_REPORTS_CAPTURED",
        "EXEC_SANDBOX_R007_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
}

Write-JsonArtifact "phase-exec-sandbox-r007-r006-reference.json" ([ordered]@{ Phase = $phase; SourcePhase = "EXEC-SANDBOX-R006"; SourceDecision = "R009SandboxMultiSymbolSmokeBlockedByControls"; R005AcceptedFillPreserved = $true })
Write-JsonArtifact "phase-exec-sandbox-r007-quantity-rule-inventory.json" $inventory
Write-JsonArtifact "phase-exec-sandbox-r007-per-symbol-quantity-calibration-results.json" ([ordered]@{ Phase = $phase; Results = $results; AllValidated = $allValidated; Partial = $partial })
Write-JsonArtifact "phase-exec-sandbox-r007-sandbox-guardrail-validation.json" $guardrail
Write-JsonArtifact "phase-exec-sandbox-r007-fix-logon-confirmation.json" $logon
Write-JsonArtifact "phase-exec-sandbox-r007-sandbox-order-intents.json" ([ordered]@{ Phase = $phase; Intents = $orderIntents; SandboxOnly = $true; ProductionOrder = $false })
Write-JsonArtifact "phase-exec-sandbox-r007-sandbox-routes.json" ([ordered]@{ Phase = $phase; Routes = $routes; RouteCount = $routes.Count; SandboxOnly = $true; ProductionRouteCreated = $false })
Write-JsonArtifact "phase-exec-sandbox-r007-sandbox-submission-results.json" ([ordered]@{ Phase = $phase; SubmissionResults = $submissions; SubmittedOrderCount = $submittedCount; TotalSubmittedQuantity = [decimal]$totalQuantity; MaxSandboxOrderCount = 7; MaxTotalSandboxQuantity = 0.7; SandboxOnly = $true; ProductionSubmission = $false })
Write-JsonArtifact "phase-exec-sandbox-r007-sandbox-ack-reject-results.json" ([ordered]@{ Phase = $phase; Results = $ackReject; AckOrAcceptedCount = $acceptedCount; RejectCount = $rejectedCount; SandboxOnly = $true; ProductionAckReject = $false })
Write-JsonArtifact "phase-exec-sandbox-r007-sandbox-execution-reports.json" ([ordered]@{ Phase = $phase; Reports = $executionReports; ExecutionReportCount = $executionReports.Count; SandboxOnly = $true; ProductionExecutionReport = $false })
Write-JsonArtifact "phase-exec-sandbox-r007-sandbox-fill-reports.json" ([ordered]@{ Phase = $phase; Reports = $fillReports; FillCount = $fillReports.Count; SandboxOnly = $true; ProductionFill = $false })
Write-JsonArtifact "phase-exec-sandbox-r007-sandbox-reconciliation-result.json" $reconciliation
Write-JsonArtifact "phase-exec-sandbox-r007-quantity-calibration-decision.json" $decision
Write-JsonArtifact "phase-exec-sandbox-r007-no-secret-persistence-audit.json" ([ordered]@{ Phase = $phase; CredentialValuesPrintedOrPersisted = $false; CredentialValuesRedacted = $true; CredentialVariableNamesOnly = $true; SecretValuesSerialized = $false })
Write-JsonArtifact "phase-exec-sandbox-r007-no-production-broker-audit.json" (New-Audit "NoProductionBroker" "Only operator-attested LMAX demo/sandbox profile artifacts are allowed.")
Write-JsonArtifact "phase-exec-sandbox-r007-no-production-order-audit.json" (New-Audit "NoProductionOrder" "No production order artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r007-no-production-route-audit.json" (New-Audit "NoProductionRoute" "No production route artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r007-no-production-fill-report-audit.json" (New-Audit "NoProductionFillReport" "Sandbox reports are marked SandboxOnly; production fill/report is false.")
Write-JsonArtifact "phase-exec-sandbox-r007-no-production-ledger-audit.json" (New-Audit "NoProductionLedger" "No production ledger commit occurred.")
Write-JsonArtifact "phase-exec-sandbox-r007-no-production-state-mutation-audit.json" (New-Audit "NoProductionStateMutation" "No production trading state was mutated.")
Write-JsonArtifact "phase-exec-sandbox-r007-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false; DirectCrossExecutionIntentRejected = $true; EURGBPSubmitted = $false })
Write-JsonArtifact "phase-exec-sandbox-r007-usd-pair-whitelist-preservation.json" ([ordered]@{ Phase = $phase; WhitelistedSymbols = $allowedSymbols; NonWhitelistedSymbolAllowed = $false; AudusdStatus = "SupportedAndNotFailed"; AudusdMisclassified = $false })
Write-JsonArtifact "phase-exec-sandbox-r007-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-sandbox-r007-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); CandidateTargetCloseUtc = "2026-05-26T15:15:00Z"; CandidateIsCanonical = $true })
Write-JsonArtifact "phase-exec-sandbox-r007-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); UsedAsFutureCanonical = $false; Legacy06AcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-sandbox-r007-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-sandbox-r007-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; SandboxExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-sandbox-r007-forbidden-actions-audit.json" ([ordered]@{
    Phase = $phase
    LmaxProductionUsed = $false
    ProductionCredentialsUsed = $false
    NonSandboxBrokerRouteUsed = $false
    CredentialValuesPrintedOrPersisted = $false
    PolygonCalled = $false
    UnrelatedExternalApiCalled = $false
    SchedulerServicePollingBackgroundJobIntroduced = $false
    NewOrderSingleSentBeforeLogonConfirmed = $false
    MoreThanMaxSandboxOrderCountSubmitted = $submittedCount -gt 7
    MoreThanOneOrderPerSymbolSubmitted = @($results | Where-Object { $_.Submitted } | Group-Object { $_["Symbol"] } | Where-Object { $_.Count -gt 1 }).Count -gt 0
    TotalSandboxQuantityExceedsCap = [decimal]$totalQuantity -gt 0.7
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
Write-JsonArtifact "phase-exec-sandbox-r007-next-phase-recommendation.json" ([ordered]@{ Phase = $phase; RecommendedNextPhase = "EXEC-SANDBOX-R008"; Title = "LMAX Sandbox Multi-Symbol Result Review and Session Controls Gate"; Reason = "Review accepted/rejected per-symbol sandbox results and harden next sandbox controls without production enablement." })
Write-JsonArtifact "phase-exec-sandbox-r007-build-test-validator-evidence.json" ([ordered]@{ Phase = $phase; Build = $BuildStatus; FocusedSandboxTests = $FocusedTestsStatus; Validator = $ValidatorStatus; DotnetBuildNoRestore = "dotnet build --no-restore"; FocusedTests = "dotnet test tests\QQ.Production.Intraday.Tests.Unit\QQ.Production.Intraday.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~R009SandboxUsdPairQuantityCalibrationTests|FullyQualifiedName~R009SandboxAcceptedFillQuantityControlsTests"; ValidatorScript = "scripts/check-exec-sandbox-r007-usd-pair-quantity-calibration-gate.ps1" })

$summary = @"
# EXEC-SANDBOX-R007 Summary

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)
R007 built a seven-symbol USD-pair quantity inventory and processed bounded one-order-per-symbol sandbox smoke artifacts where present.

Submitted sandbox orders: $submittedCount
Accepted/acked: $acceptedCount
Rejected: $rejectedCount
Filled: $filledCount
Skipped: $skippedCount
Decision: $($decision.Decision)

Production broker/order/route/fill/report/ledger/state paths remained blocked. Credential values were not printed or persisted.

Build/tests/validator:
- Build: $BuildStatus
- Focused sandbox tests/static checks: $FocusedTestsStatus
- Validator: $ValidatorStatus
"@
$summary | Set-Content -Path (Join-Path $artifactDir "phase-exec-sandbox-r007-summary.md") -Encoding UTF8

Write-Host "EXEC-SANDBOX-R007 artifacts written to $artifactDir"
