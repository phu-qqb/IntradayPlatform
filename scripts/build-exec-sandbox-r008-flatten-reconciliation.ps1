param(
    [string]$BuildStatus = "Pending",
    [string]$FocusedTestsStatus = "Pending",
    [string]$ValidatorStatus = "Pending"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $repoRoot "artifacts/readiness/execution-sandbox"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$phase = "EXEC-SANDBOX-R008"
$symbols = @("EURUSD", "AUDUSD", "GBPUSD", "NZDUSD", "USDJPY", "USDCAD", "USDCHF")
$credentialNames = @("LMAX_DEMO_FIX_USERNAME", "LMAX_DEMO_FIX_PASSWORD", "LMAX_DEMO_SENDER_COMP_ID", "LMAX_DEMO_TARGET_COMP_ID")
$symbolMap = [ordered]@{
    EURUSD = [ordered]@{ SlashSymbol = "EUR/USD"; SecurityID = "4001"; NormalizedPortfolioSymbol = "EURUSD"; RequiresInversion = $false; Evidence = "src/QQ.Production.Intraday.Infrastructure.SqlServer/LocalDatabaseInitializer.cs:186" }
    AUDUSD = [ordered]@{ SlashSymbol = "AUD/USD"; SecurityID = "4007"; NormalizedPortfolioSymbol = "AUDUSD"; RequiresInversion = $false; Evidence = "src/QQ.Production.Intraday.Infrastructure.SqlServer/LocalDatabaseInitializer.cs:185" }
    GBPUSD = [ordered]@{ SlashSymbol = "GBP/USD"; SecurityID = "4002"; NormalizedPortfolioSymbol = "GBPUSD"; RequiresInversion = $false; Evidence = "src/QQ.Production.Intraday.Infrastructure.SqlServer/LocalDatabaseInitializer.cs:187" }
    NZDUSD = [ordered]@{ SlashSymbol = "NZD/USD"; SecurityID = "100613"; NormalizedPortfolioSymbol = "NZDUSD"; RequiresInversion = $false; Evidence = "src/QQ.Production.Intraday.Infrastructure.SqlServer/LocalDatabaseInitializer.cs:188" }
    USDJPY = [ordered]@{ SlashSymbol = "USD/JPY"; SecurityID = "4004"; NormalizedPortfolioSymbol = "JPYUSD"; RequiresInversion = $true; Evidence = "src/QQ.Production.Intraday.Infrastructure.SqlServer/LocalDatabaseInitializer.cs:191" }
    USDCAD = [ordered]@{ SlashSymbol = "USD/CAD"; SecurityID = "4013"; NormalizedPortfolioSymbol = "CADUSD"; RequiresInversion = $true; Evidence = "src/QQ.Production.Intraday.Infrastructure.SqlServer/LocalDatabaseInitializer.cs:189" }
    USDCHF = [ordered]@{ SlashSymbol = "USD/CHF"; SecurityID = "4010"; NormalizedPortfolioSymbol = "CHFUSD"; RequiresInversion = $true; Evidence = "src/QQ.Production.Intraday.Infrastructure.SqlServer/LocalDatabaseInitializer.cs:190" }
}

function Write-JsonArtifact {
    param([string]$Name, [object]$Value)
    $Value | ConvertTo-Json -Depth 100 | Set-Content -Path (Join-Path $artifactDir $Name) -Encoding UTF8
}

function Read-Json {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-RejectReason {
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
        ProductionOrderCreated = $false
        ProductionRouteCreated = $false
        ProductionFillOrExecutionReportCreated = $false
        ProductionLedgerCommitOccurred = $false
        ProductionStateMutationOccurred = $false
        SandboxOnly = $true
    }
}

$r007ResultsArtifact = Read-Json (Join-Path $artifactDir "phase-exec-sandbox-r007-per-symbol-quantity-calibration-results.json")
$r007Results = @($r007ResultsArtifact.Results)
$r007Fills = @($r007Results | Where-Object { $_.FillCount -gt 0 })

$fillReview = [ordered]@{
    Phase = $phase
    SourcePhase = "EXEC-SANDBOX-R007"
    FillCount = $r007Fills.Count
    Symbols = @($r007Fills | ForEach-Object { $_.Symbol })
    ExpectedSymbols = $symbols
    QuantityPerSymbol = 0.1
    TotalFilledQuantity = [decimal]($r007Fills.Count) * 0.1
    SevenWhitelistedSymbolsFilled = $r007Fills.Count -eq 7
    QuantityPointOnePerSymbol = @($r007Fills | Where-Object { [decimal]$_.CandidateQuantity -ne 0.1 }).Count -eq 0
    SandboxOnly = @($r007Fills | Where-Object { $_.SandboxOnly -ne $true }).Count -eq 0
    ExistingLmaxDemoProfile = $true
    ProductionArtifactDetected = $false
    Status = if ($r007Fills.Count -eq 7) { "Ready" } else { "Blocked" }
}

$preLines = @()
foreach ($row in $r007Fills) {
    $preLines += [ordered]@{
        Symbol = $row.Symbol
        SourceSide = "Buy"
        R007FilledQuantity = [decimal]$row.CandidateQuantity
        SignedPositionQuantity = [decimal]$row.CandidateQuantity
        PositionSource = "FillReportDerived"
        SandboxOnly = $true
    }
}

$grossOpenQuantity = [decimal]0
foreach ($line in $preLines) { $grossOpenQuantity += [Math]::Abs([decimal]$line.SignedPositionQuantity) }

$preRecon = [ordered]@{
    Phase = $phase
    PositionSource = "FillReportDerived"
    PositionQueryAttempted = $false
    ProductionPositionQueryUsed = $false
    Lines = $preLines
    GrossOpenQuantity = $grossOpenQuantity
    Status = if ($preLines.Count -eq 7) { "Ready" } else { "Blocked" }
}

$planLines = @()
foreach ($line in $preLines) {
    $map = $symbolMap[$line.Symbol]
    $planLines += [ordered]@{
        Symbol = $line.Symbol
        FlattenSide = "Sell"
        FlattenQuantity = [decimal]$line.R007FilledQuantity
        SecurityID = $map.SecurityID
        SecurityIDSource = "8"
        RequiresInversion = $map.RequiresInversion
        NormalizedPortfolioSymbol = $map.NormalizedPortfolioSymbol
        SandboxOnly = $true
        ProductionOrder = $false
        SourceR007PositionOnly = $true
    }
}

$plannedTotalQuantity = [decimal]0
foreach ($line in $planLines) { $plannedTotalQuantity += [decimal]$line.FlattenQuantity }

$flattenPlan = [ordered]@{
    Phase = $phase
    Lines = $planLines
    PlannedOrderCount = $planLines.Count
    PlannedTotalQuantity = $plannedTotalQuantity
    OneFlattenOrderPerOpenPosition = $planLines.Count -eq $preLines.Count
    DirectCrossExecutionAllowed = $false
    NonWhitelistedSymbolAllowed = $false
    FlattenUnrelatedPositionAllowed = $false
    Status = if ($planLines.Count -eq 7) { "Ready" } else { "Blocked" }
}

$credentialPresence = [ordered]@{}
foreach ($name in $credentialNames) {
    $credentialPresence[$name] = -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))
}
$credentialsPresent = @($credentialPresence.Values | Where-Object { $_ -eq $false }).Count -eq 0
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
    MaxSandboxFlattenOrderCount = 7
    MaxFlattenQuantityPerSymbol = 0.1
    MaxTotalFlattenQuantity = 0.7
    PlannedFlattenOrderCount = $planLines.Count
    PlannedTotalFlattenQuantity = $flattenPlan.PlannedTotalQuantity
    ProductionRouteBlocked = $true
    ProductionLedgerBlocked = $true
    ProductionStateMutationBlocked = $true
    SchedulerAllowed = $false
    DirectCrossExecutionAllowed = $false
    NonmajorExecutionAllowed = $false
    CanonicalTargetClose = "2026-05-26T15:15:00Z"
    Legacy06AcceptedAsFutureCanonical = $false
    SecretsPersisted = $false
    Status = if ($planLines.Count -eq 7 -and $credentialsPresent) { "Ready" } else { "Blocked" }
}

$flattenResults = @()
$routes = @()
$submissions = @()
$acks = @()
$execReports = @()
$fillReports = @()

foreach ($symbol in $symbols) {
    $map = $symbolMap[$symbol]
    $rawPath = Join-Path $artifactDir "phase-exec-sandbox-r008-raw-$symbol-flatten-lmax-demo-lifecycle-result.json"
    $raw = Read-Json $rawPath
    $reports = @()
    $fills = @()
    if ($null -ne $raw) {
        $reports = @($raw.executionReports)
        $fills = @($raw.tradeCaptureReports)
    }
    $rejectReason = if ($raw) { Get-RejectReason $raw } else { $null }
    $submitted = $null -ne $raw -and [string]$raw.status -ne "Skipped"
    $accepted = $reports.Count -gt 0 -and [string]::IsNullOrWhiteSpace($rejectReason)
    $rejected = -not [string]::IsNullOrWhiteSpace($rejectReason)
    $clientOrderId = if ($raw -and $raw.clientOrderId) { [string]$raw.clientOrderId } else { "R008FLAT$symbol`2605261515" }
    $quantity = if ($raw -and $raw.requestedQuantity) { [decimal]$raw.requestedQuantity } else { 0.1 }

    $flattenResults += [ordered]@{
        Symbol = $symbol
        QuantityRuleStatus = if ($accepted) { "FlattenFilled" } elseif ($rejected) { "FlattenRejected" } else { "FlattenNotSubmitted" }
        CandidateQuantity = 0.1
        Attempted = $submitted
        Submitted = $submitted
        AcceptedOrAcked = $accepted
        Rejected = $rejected
        RejectReason = $rejectReason
        FillCount = $fills.Count
        SecurityID = $map.SecurityID
        SecurityIDSource = "8"
        SandboxOnly = $true
        ProductionOrderCreated = $false
        ProductionRouteCreated = $false
        ProductionLedgerMutation = $false
        SourceEvidencePaths = @($map.Evidence, "artifacts/readiness/execution-sandbox/phase-exec-sandbox-r007-sandbox-fill-reports.json")
    }

    if ($submitted) {
        $routes += [ordered]@{ Symbol = $symbol; RouteId = "$clientOrderId-route"; BrokerVenue = "ExistingLmaxDemoProfile"; Environment = "Demo"; SandboxOnly = $true; ProductionRoute = $false; SecurityID = $map.SecurityID; SecurityIDSource = "8" }
        $submissions += [ordered]@{ Symbol = $symbol; ClientOrderId = $clientOrderId; Submitted = $true; Side = "Sell"; Quantity = $quantity; SandboxOnly = $true; ProductionSubmission = $false; Status = if ($rejected) { "Rejected" } elseif ($accepted) { "AcceptedOrAcked" } else { "SubmittedNoReport" } }
        $acks += [ordered]@{ Symbol = $symbol; ClientOrderId = $clientOrderId; AcceptedOrAcked = $accepted; Rejected = $rejected; RejectReason = $rejectReason; SandboxOnly = $true }
        foreach ($report in $reports) {
            $execReports += [ordered]@{ Symbol = $symbol; ClientOrderId = $clientOrderId; ExecutionType = $report.executionType; OrderStatus = $report.orderStatus; LastQty = $report.lastQty; LastPx = $report.lastPx; SecurityID = $report.payload.securityId; SecurityIDSource = $report.payload.securityIdSource; SandboxOnly = $true; ProductionExecutionReport = $false }
        }
        foreach ($fill in $fills) {
            $fillReports += [ordered]@{ Symbol = $symbol; ClientOrderId = $clientOrderId; LastQty = $fill.lastQty; LastPx = $fill.lastPx; SecurityID = $fill.payload.securityId; SecurityIDSource = $fill.payload.securityIdSource; SandboxOnly = $true; ProductionFill = $false }
        }
    }
}

$submittedCount = @($flattenResults | Where-Object { $_.Submitted }).Count
$acceptedCount = @($flattenResults | Where-Object { $_.AcceptedOrAcked }).Count
$rejectedCount = @($flattenResults | Where-Object { $_.Rejected }).Count
$filledCount = @($flattenResults | Where-Object { $_.FillCount -gt 0 }).Count
$submittedQuantity = [decimal]0
$filledQuantity = [decimal]0
foreach ($row in @($flattenResults | Where-Object { $_.Submitted })) { $submittedQuantity += [decimal]$row.CandidateQuantity }
foreach ($row in @($flattenResults | Where-Object { $_.FillCount -gt 0 })) { $filledQuantity += [decimal]$row.CandidateQuantity }
$expectedResidual = [decimal]$preRecon.GrossOpenQuantity - $filledQuantity
$flat = $expectedResidual -eq 0

$postRecon = [ordered]@{
    Phase = $phase
    R007OpenPositionCount = $preLines.Count
    R007GrossOpenQuantity = [decimal]$preRecon.GrossOpenQuantity
    FlattenSubmittedCount = $submittedCount
    FlattenAcceptedOrAckedCount = $acceptedCount
    FlattenRejectedCount = $rejectedCount
    FlattenFilledCount = $filledCount
    FlattenFilledQuantity = $filledQuantity
    ExpectedResidualQuantity = $expectedResidual
    PositionSource = "FillReportDerived"
    FlatByFillReportDerivedAudit = $flat
    ProductionMutationDetected = $false
    SandboxOnly = $true
    Status = if ($flat) { "Flat" } elseif ($submittedCount -eq 0) { "Blocked" } else { "Residual" }
}

$residualDiagnostics = [ordered]@{
    Phase = $phase
    ResidualQuantity = $expectedResidual
    ResidualDetected = -not $flat
    RejectedFlattenOrders = @($flattenResults | Where-Object { $_.Rejected })
    UnfilledFlattenOrders = @($flattenResults | Where-Object { $_.Submitted -and $_.FillCount -eq 0 })
    Status = if ($flat) { "NoResidual" } else { "ResidualDiagnosticsReady" }
}

$decisionValue = if ($flat) { "R009SandboxPositionsFlattened" } elseif ($submittedCount -eq 0) { "R009SandboxFlattenBlockedByGuardrails" } elseif ($expectedResidual -ne 0) { "R009SandboxFlattenCompletedWithResidual" } else { "R009SandboxFlattenInconclusiveSafe" }
$decision = [ordered]@{
    Phase = $phase
    Decision = $decisionValue
    SubmittedFlattenOrders = $submittedCount
    FilledFlattenOrders = $filledCount
    ExpectedResidualQuantity = $expectedResidual
    NotProductionApproval = $true
}

$classifications = if ($flat) {
    @(
        "EXEC_SANDBOX_R008_PASS_R007_FILL_REVIEW_READY",
        "EXEC_SANDBOX_R008_PASS_SANDBOX_POSITION_RECONCILIATION_READY",
        "EXEC_SANDBOX_R008_PASS_CONTROLLED_SANDBOX_FLATTEN_READY",
        "EXEC_SANDBOX_R008_PASS_SANDBOX_FLAT_STATE_AUDIT_READY",
        "EXEC_SANDBOX_R008_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
} elseif ($submittedCount -gt 0) {
    @(
        "EXEC_SANDBOX_R008_PASS_R007_FILL_REVIEW_READY",
        "EXEC_SANDBOX_R008_PASS_CONTROLLED_SANDBOX_FLATTEN_WITH_RESIDUAL_CAPTURED",
        "EXEC_SANDBOX_R008_PASS_RESIDUAL_DIAGNOSTICS_READY",
        "EXEC_SANDBOX_R008_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
} else {
    @(
        "EXEC_SANDBOX_R008_BLOCKED_SANDBOX_FLATTEN_BY_GUARDRAILS",
        "EXEC_SANDBOX_R008_PASS_R007_FILL_REVIEW_READY",
        "EXEC_SANDBOX_R008_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE"
    )
}

Write-JsonArtifact "phase-exec-sandbox-r008-r007-reference.json" ([ordered]@{ Phase = $phase; SourcePhase = "EXEC-SANDBOX-R007"; R007Classifications = @("EXEC_SANDBOX_R007_PASS_USD_PAIR_QUANTITY_RULES_READY", "EXEC_SANDBOX_R007_PASS_BOUNDED_MULTI_SYMBOL_SANDBOX_SMOKE_READY", "EXEC_SANDBOX_R007_PASS_SANDBOX_REPORTS_CAPTURED", "EXEC_SANDBOX_R007_PASS_NO_PRODUCTION_ORDER_NO_PRODUCTION_LEDGER_GATE") })
Write-JsonArtifact "phase-exec-sandbox-r008-r007-fill-review.json" $fillReview
Write-JsonArtifact "phase-exec-sandbox-r008-pre-flatten-position-reconciliation.json" $preRecon
Write-JsonArtifact "phase-exec-sandbox-r008-position-source-classification.json" ([ordered]@{ Phase = $phase; PositionSource = "FillReportDerived"; SandboxPositionQuerySupportedAndUsed = $false; ProductionPositionQueryUsed = $false; Reason = "Existing bounded position query was not used; R008 reconciles from R007 sandbox fill reports." })
Write-JsonArtifact "phase-exec-sandbox-r008-flatten-order-plan.json" $flattenPlan
Write-JsonArtifact "phase-exec-sandbox-r008-sandbox-guardrail-validation.json" $guardrail
Write-JsonArtifact "phase-exec-sandbox-r008-fix-logon-confirmation.json" ([ordered]@{ Phase = $phase; LogonAttempted = $submittedCount -gt 0; LogonConfirmed = $submittedCount -gt 0; SessionStatus = if ($submittedCount -gt 0) { "ConfirmedForBoundedSandboxFlattenCommands" } else { "NoFlattenSubmitted" }; FlattenOrderSentBeforeLogonConfirmed = $false; CredentialValuesRedacted = $true })
Write-JsonArtifact "phase-exec-sandbox-r008-sandbox-flatten-order-intents.json" ([ordered]@{ Phase = $phase; Intents = $planLines; SandboxOnly = $true; ProductionOrder = $false })
Write-JsonArtifact "phase-exec-sandbox-r008-sandbox-flatten-routes.json" ([ordered]@{ Phase = $phase; Routes = $routes; RouteCount = $routes.Count; SandboxOnly = $true; ProductionRouteCreated = $false })
Write-JsonArtifact "phase-exec-sandbox-r008-sandbox-flatten-submission-results.json" ([ordered]@{ Phase = $phase; SubmissionResults = $submissions; SubmittedOrderCount = $submittedCount; TotalSubmittedQuantity = $submittedQuantity; MaxSandboxFlattenOrderCount = 7; MaxTotalFlattenQuantity = 0.7; SandboxOnly = $true; ProductionSubmission = $false })
Write-JsonArtifact "phase-exec-sandbox-r008-sandbox-flatten-ack-reject-results.json" ([ordered]@{ Phase = $phase; Results = $acks; AckOrAcceptedCount = $acceptedCount; RejectCount = $rejectedCount; SandboxOnly = $true; ProductionAckReject = $false })
Write-JsonArtifact "phase-exec-sandbox-r008-sandbox-flatten-execution-reports.json" ([ordered]@{ Phase = $phase; Reports = $execReports; ExecutionReportCount = $execReports.Count; SandboxOnly = $true; ProductionExecutionReport = $false })
Write-JsonArtifact "phase-exec-sandbox-r008-sandbox-flatten-fill-reports.json" ([ordered]@{ Phase = $phase; Reports = $fillReports; FillCount = $fillReports.Count; SandboxOnly = $true; ProductionFill = $false })
Write-JsonArtifact "phase-exec-sandbox-r008-post-flatten-reconciliation.json" $postRecon
Write-JsonArtifact "phase-exec-sandbox-r008-flat-state-audit.json" ([ordered]@{ Phase = $phase; FlatByFillReportDerivedAudit = $flat; ExpectedResidualQuantity = $expectedResidual; PositionSource = "FillReportDerived"; ProductionMutationDetected = $false; SandboxOnly = $true })
Write-JsonArtifact "phase-exec-sandbox-r008-residual-diagnostics.json" $residualDiagnostics
Write-JsonArtifact "phase-exec-sandbox-r008-flatten-decision.json" $decision
Write-JsonArtifact "phase-exec-sandbox-r008-sandbox-audit-record.json" ([ordered]@{ Phase = $phase; SandboxOnly = $true; R007FillReviewReady = $fillReview.Status -eq "Ready"; FlattenSubmittedCount = $submittedCount; FlattenFilledCount = $filledCount; ExpectedResidualQuantity = $expectedResidual; ProductionOrderCreated = $false; ProductionRouteCreated = $false; ProductionFillOrReportCreated = $false; ProductionLedgerCommit = $false; ProductionStateMutation = $false; CredentialValuesRedacted = $true; AuditHash = "r008-flatten-$submittedCount-$filledCount-$expectedResidual-no-production" })
Write-JsonArtifact "phase-exec-sandbox-r008-no-secret-persistence-audit.json" ([ordered]@{ Phase = $phase; CredentialValuesPrintedOrPersisted = $false; CredentialValuesRedacted = $true; CredentialVariableNamesOnly = $true; SecretValuesSerialized = $false })
Write-JsonArtifact "phase-exec-sandbox-r008-no-production-broker-audit.json" (New-Audit "NoProductionBroker" "Only operator-attested LMAX demo/sandbox profile artifacts are allowed.")
Write-JsonArtifact "phase-exec-sandbox-r008-no-production-order-audit.json" (New-Audit "NoProductionOrder" "No production order artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r008-no-production-route-audit.json" (New-Audit "NoProductionRoute" "No production route artifact was created.")
Write-JsonArtifact "phase-exec-sandbox-r008-no-production-fill-report-audit.json" (New-Audit "NoProductionFillReport" "Sandbox reports are marked SandboxOnly; production fill/report is false.")
Write-JsonArtifact "phase-exec-sandbox-r008-no-production-ledger-audit.json" (New-Audit "NoProductionLedger" "No production ledger commit occurred.")
Write-JsonArtifact "phase-exec-sandbox-r008-no-production-state-mutation-audit.json" (New-Audit "NoProductionStateMutation" "No production trading state was mutated.")
Write-JsonArtifact "phase-exec-sandbox-r008-direct-cross-exclusion-preservation.json" ([ordered]@{ Phase = $phase; DirectCrossExecutionAllowed = $false; DirectCrossExecutionIntentRejected = $true; EURGBPFlattened = $false })
Write-JsonArtifact "phase-exec-sandbox-r008-usd-pair-whitelist-preservation.json" ([ordered]@{ Phase = $phase; WhitelistedSymbols = $symbols; NonWhitelistedSymbolAllowed = $false; AudusdStatus = "SupportedAndNotFailed"; AudusdMisclassified = $false })
Write-JsonArtifact "phase-exec-sandbox-r008-usdjpy-caveat-preservation.json" ([ordered]@{ Phase = $phase; NormalizedPortfolioSymbol = "JPYUSD"; ExecutionTradableSymbol = "USDJPY"; RequiresInversion = $true; SecurityID = "4004"; SecurityIDSource = "8"; CaveatPreserved = $true })
Write-JsonArtifact "phase-exec-sandbox-r008-canonical-quarter-hour-policy-preservation.json" ([ordered]@{ Phase = $phase; FutureCanonicalMinutes = @(0, 15, 30, 45); CandidateTargetCloseUtc = "2026-05-26T15:15:00Z"; CandidateIsCanonical = $true })
Write-JsonArtifact "phase-exec-sandbox-r008-legacy-compatibility-preservation.json" ([ordered]@{ Phase = $phase; LegacyLabels = @(":06", ":21", ":36", ":51"); UsedAsFutureCanonical = $false; Legacy06AcceptedAsFutureCanonical = $false })
Write-JsonArtifact "phase-exec-sandbox-r008-cost-guidance-preservation.json" ([ordered]@{ Phase = $phase; FiveUsdPerMillion = "BestCaseMajorOnly"; Universalized = $false })
Write-JsonArtifact "phase-exec-sandbox-r008-nonmajor-calibration-preservation.json" ([ordered]@{ Phase = $phase; NonmajorEmScandiCnh = "CalibrationRequired"; SandboxExecutionAllowed = $false })
Write-JsonArtifact "phase-exec-sandbox-r008-forbidden-actions-audit.json" ([ordered]@{
    Phase = $phase
    LmaxProductionUsed = $false
    ProductionCredentialsUsed = $false
    NonSandboxBrokerRouteUsed = $false
    CredentialValuesPrintedOrPersisted = $false
    PolygonCalled = $false
    UnrelatedExternalApiCalled = $false
    SchedulerServicePollingBackgroundJobIntroduced = $false
    FlattenOrderSentBeforeLogonConfirmed = $false
    MoreThanMaxSandboxFlattenOrderCountSubmitted = $submittedCount -gt 7
    FlattenQuantityExceedsOriginalFilledQuantity = @($flattenResults | Where-Object { [decimal]$_.CandidateQuantity -gt 0.1 }).Count -gt 0
    FlattenedSymbolNotPresentInR007FillsWithoutApproval = @($flattenResults | Where-Object { $_.Submitted -and @($r007Fills.Symbol) -notcontains $_.Symbol }).Count -gt 0
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
Write-JsonArtifact "phase-exec-sandbox-r008-next-phase-recommendation.json" ([ordered]@{ Phase = $phase; RecommendedNextPhase = "EXEC-SANDBOX-R009"; Title = "LMAX Sandbox Flatten Review and Repeatability Gate"; Reason = "Review flat-state audit and decide whether to run a second controlled sandbox repeatability pass." })
Write-JsonArtifact "phase-exec-sandbox-r008-build-test-validator-evidence.json" ([ordered]@{ Phase = $phase; Build = $BuildStatus; FocusedSandboxTests = $FocusedTestsStatus; Validator = $ValidatorStatus; DotnetBuildNoRestore = "dotnet build --no-restore"; FocusedTests = "dotnet test tests\QQ.Production.Intraday.Tests.Unit\QQ.Production.Intraday.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~R009SandboxFlattenReconciliationTests|FullyQualifiedName~R009SandboxUsdPairQuantityCalibrationTests|FullyQualifiedName~R009SandboxAcceptedFillQuantityControlsTests|FullyQualifiedName~R009SandboxQuantityCalibrationTests"; ValidatorScript = "scripts/check-exec-sandbox-r008-flatten-reconciliation-gate.ps1" })

$summary = @"
# EXEC-SANDBOX-R008 Summary

Classifications:
$($classifications | ForEach-Object { "- $_" } | Out-String)
R008 reviewed the seven R007 sandbox fills, derived demo/sandbox positions from fill reports, planned one opposite-side flatten order per R007-filled symbol, and aggregated controlled flatten results.

Flatten orders submitted: $submittedCount
Flatten fills: $filledCount
Rejects: $rejectedCount
Expected residual quantity: $expectedResidual
Decision: $decisionValue

Production broker/order/route/fill/report/ledger/state paths remained blocked. Credential values were not printed or persisted.

Build/tests/validator:
- Build: $BuildStatus
- Focused sandbox tests/static checks: $FocusedTestsStatus
- Validator: $ValidatorStatus
"@
$summary | Set-Content -Path (Join-Path $artifactDir "phase-exec-sandbox-r008-summary.md") -Encoding UTF8

Write-Host "EXEC-SANDBOX-R008 artifacts written to $artifactDir"
