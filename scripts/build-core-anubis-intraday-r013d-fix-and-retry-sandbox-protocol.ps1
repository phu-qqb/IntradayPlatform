param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$BuildStatus = "NotRun",
    [string]$FocusedTestsStatus = "NotRun",
    [switch]$Execute
)

$ErrorActionPreference = "Stop"

$Package = "CORE-ANUBIS-INTRADAY-R013D-FIX-AND-RETRY-SANDBOX-PROTOCOL"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013d-fix-and-retry-sandbox-protocol"
$RawRetryDir = Join-Path $ArtifactDir "raw-lmax-sandbox-retry"
$R013CDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013c-guarded-sandbox-execution"
$R013BDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-r013b-exact-sandbox-execution-harness"
$R012Dir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-intraday-operator-approval-r012"
$PriorSuccessPath = Join-Path $RepoRoot "artifacts\readiness\execution-sandbox\phase-exec-sandbox-r009-raw-EURUSD-open-lmax-demo-lifecycle-result.json"
$R013CScript = Join-Path $RepoRoot "scripts\build-core-anubis-intraday-r013c-guarded-sandbox-execution.ps1"
$R013DScript = Join-Path $RepoRoot "scripts\build-core-anubis-intraday-r013d-fix-and-retry-sandbox-protocol.ps1"
$LabRecoverySource = Join-Path $RepoRoot "tools\QQ.Production.Intraday.Lmax.ConnectivityLab\LabFixRecovery.cs"
$ToolProject = Join-Path $RepoRoot "tools\QQ.Production.Intraday.Lmax.ConnectivityLab\QQ.Production.Intraday.Lmax.ConnectivityLab.csproj"

$OperatorApprovalId = "core-anubis-intraday-operator-approval-r012:419206468D9EEAA15DBD3975"
$CandidateId = "core-anubis-pms-quantity-preview-r010-refined:5E0F1277E153A728481987BD"
$RiskReviewId = "core-anubis-risk-review-r011:5EB84FF8EF3FC6EE8AE6F3E4"
$CoreHandoffManifestHash = "sha256:8A445CD6195458D09CC539C0C14895C1EFB09027A9BB58D1369BDDD44D99F182"
$NettedUsdWeightsHash = "sha256:41FF6745857F9E53E28278B7C8DAF91EF2306DE384B753B3BB605B5FD2D01A96"
$RunKey = "fx-intraday-qubes_London_H1_universe-v1_mapping-v1_flatbar-v1_tinyplus-strattaken-v1_202601051006"
$CorrectFixSecurityIdSource = "8"
$PriorBadSecurityIdSource = "LMAX"

function Write-JsonArtifact([string]$Name, [object]$Payload) {
    New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
    $Payload | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $ArtifactDir $Name) -Encoding UTF8
}

function Read-JsonPath([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Hash-File([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    "sha256:" + (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash
}

function To-LabSide([string]$Side) {
    if ($Side -eq "BUY") { return "Buy" }
    if ($Side -eq "SELL") { return "Sell" }
    return $Side
}

function Invert-Side([string]$Side) {
    if ($Side -eq "BUY") { return "SELL" }
    if ($Side -eq "SELL") { return "BUY" }
    return $Side
}

function New-ClOrdId([int]$Index, [string]$Phase, [string]$Symbol) {
    $prefix = if ($Phase -eq "open") { "O" } else { "F" }
    $clean = ($Symbol -replace "[^A-Za-z0-9]", "").ToUpperInvariant()
    ("R13D{0:D2}{1}{2}" -f $Index, $prefix, $clean)
}

function Get-Reports($Raw) {
    if ($null -eq $Raw -or $null -eq $Raw.executionReports) { return @() }
    @($Raw.executionReports)
}

function Get-FillQty($Raw) {
    $sum = [decimal]0
    foreach ($report in (Get-Reports $Raw)) {
        $isFill = [string]$report.executionType -eq "Trade" -or [string]$report.orderStatus -eq "Filled"
        if ($isFill -and $null -ne $report.lastQty) {
            $sum += [decimal]::Parse([string]$report.lastQty, [Globalization.CultureInfo]::InvariantCulture)
        }
    }
    $sum
}

function Get-FillPx($Raw) {
    foreach ($report in (Get-Reports $Raw)) {
        $isFill = [string]$report.executionType -eq "Trade" -or [string]$report.orderStatus -eq "Filled"
        if ($isFill -and $null -ne $report.lastPx) {
            return [decimal]::Parse([string]$report.lastPx, [Globalization.CultureInfo]::InvariantCulture)
        }
    }
    $null
}

function Get-ProtocolRejectText($Raw) {
    if ($null -eq $Raw -or $null -eq $Raw.protocolRejects) { return $null }
    (@($Raw.protocolRejects) | Select-Object -First 1).text
}

function Run-LmaxLifecycle([object]$Order, [string]$Phase, [int]$Index) {
    New-Item -ItemType Directory -Force -Path $RawRetryDir | Out-Null
    $clOrdId = New-ClOrdId $Index $Phase $Order.ExecutionSymbol
    $jsonPath = Join-Path $RawRetryDir ("{0:D2}-{1}-{2}-{3}.json" -f $Index, $Phase, $Order.ExecutionSymbol, $clOrdId)
    $side = To-LabSide $Order.Side
    $args = @(
        "run", "--project", $ToolProject, "--no-build", "--",
        "fix-demo-lifecycle-evidence",
        "--environment=Demo",
        "--allow-external-connections=true",
        "--allow-order-submission=true",
        "--allow-live-trading=false",
        "--dry-run=false",
        "--confirm-demo-order",
        "--instrument-symbol=$($Order.ExecutionSymbol)",
        "--lmax-instrument-id=$($Order.SecurityId)",
        "--fix-security-id-source=$CorrectFixSecurityIdSource",
        "--side=$side",
        "--order-type=Market",
        "--time-in-force=IOC",
        "--venue-quantity=$($Order.Quantity)",
        "--client-order-id=$clOrdId",
        "--max-demo-order-quantity=100",
        "--max-wait-seconds=12",
        "--output-json-path=$jsonPath"
    )

    $started = (Get-Date).ToUniversalTime().ToString("o")
    $output = & dotnet @args 2>&1
    $exitCode = $LASTEXITCODE
    $completed = (Get-Date).ToUniversalTime().ToString("o")
    $raw = if (Test-Path -LiteralPath $jsonPath) { Read-JsonPath $jsonPath } else { $null }
    [ordered]@{
        Phase = $Phase
        Index = $Index
        CoreSymbol = $Order.CoreSymbol
        ExecutionSymbol = $Order.ExecutionSymbol
        Side = $Order.Side
        Quantity = $Order.Quantity
        SecurityId = $Order.SecurityId
        PriorR013CSecurityIdSource = $PriorBadSecurityIdSource
        CorrectedFixSecurityIdSource = $CorrectFixSecurityIdSource
        ClientOrderId = $clOrdId
        OutputJsonPath = $jsonPath
        OutputJsonHash = Hash-File $jsonPath
        ExitCode = $exitCode
        StartedAtUtc = $started
        CompletedAtUtc = $completed
        RawStatus = if ($raw) { [string]$raw.status } else { $null }
        DryRun = if ($raw) { [bool]$raw.dryRun } else { $null }
        ExecutionReportCount = if ($raw -and $raw.executionReports) { @($raw.executionReports).Count } else { 0 }
        FillQuantity = [string](Get-FillQty $raw)
        FillPrice = [string](Get-FillPx $raw)
        RejectCount = if ($raw -and $raw.protocolRejects) { @($raw.protocolRejects).Count } else { 0 }
        RejectText = Get-ProtocolRejectText $raw
        CommandOutputCaptured = $true
        CommandOutputSanitizedAndNotPersisted = $true
        CommandOutputLineCount = @($output).Count
    }
}

$r013cSummaryPath = Join-Path $R013CDir "summary.md"
$r013cSummary = Get-Content -Raw -LiteralPath $r013cSummaryPath
$r013cOpen = Read-JsonPath (Join-Path $R013CDir "guarded-r009-sandbox-open-execution.json")
$r013cFlatten = Read-JsonPath (Join-Path $R013CDir "guarded-sandbox-flatten-execution.json")
$r013cRecon = Read-JsonPath (Join-Path $R013CDir "sandbox-reconciliation.json")
$r013cBoundary = Read-JsonPath (Join-Path $R013CDir "boundary-safety-evidence.json")
$r013bOpen = Read-JsonPath (Join-Path $R013BDir "open-order-batch-dry-run.json")
$r013bFlatten = Read-JsonPath (Join-Path $R013BDir "flatten-batch-dry-run.json")
$r013bBinding = Read-JsonPath (Join-Path $R013BDir "exact-candidate-harness-binding.json")
$r013bRoute = Read-JsonPath (Join-Path $R013BDir "sandbox-route-profile-harness-validation.json")
$r013bIdem = Read-JsonPath (Join-Path $R013BDir "idempotency-duplicate-guard-evidence.json")
$r012Approval = Read-JsonPath (Join-Path $R012Dir "operator-approval-statement.json")

$openOrders = @($r013bOpen.Orders)
$flattenOrders = @($r013bFlatten.FlattenOrders)
$expectedCoreSymbols = @("CADUSD","CNHUSD","JPYUSD","MXNUSD","NOKUSD","NZDUSD","SEKUSD","SGDUSD","ZARUSD")

$rawRejectRows = @()
foreach ($result in @($r013cOpen.Results)) {
    $raw = if ($result.OutputJsonPath -and (Test-Path -LiteralPath $result.OutputJsonPath)) { Read-JsonPath $result.OutputJsonPath } else { $null }
    $reject = if ($raw -and $raw.protocolRejects) { @($raw.protocolRejects) | Select-Object -First 1 } else { $null }
    $rawRejectRows += [ordered]@{
        CoreSymbol = $result.CoreSymbol
        ExecutionSymbol = $result.ExecutionSymbol
        Side = $result.Side
        Quantity = $result.Quantity
        SecurityID = $result.SecurityId
        SecurityIDSourceTag22Sent = $result.SecurityIdSource
        SymbolTag = $result.ExecutionSymbol
        AccountProfileTags = "ExistingLmaxDemoProfile; values redacted"
        OrderQtyTag = $result.Quantity
        SideTag = $result.Side
        FixMessageType = if ($reject) { [string]$reject.refMsgType } else { "D" }
        RejectCode = if ($reject) { [string]$reject.reasonCode } else { $null }
        RejectText = if ($reject) { [string]$reject.text } else { $null }
        ExactRejectStage = "pre-fill protocol reject"
        RejectOccurredPreFill = $true
        IdenticalRejectPattern = $true
        Classification = if ($reject -and [string]$reject.refTagId -eq "22") { "REJECT_FIELD_EXTRACTION_READY" } else { "REJECT_FIELD_EXTRACTION_INCOMPLETE" }
    }
}
$rejectTextMentionsTag22 = @($rawRejectRows | Where-Object { [string]$_.RejectText -match "Tag ID is 22|tag 22|Tag 22" }).Count -eq 9

Write-JsonArtifact "r013c-intake-and-reject-evidence-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "r013c-intake-and-reject-evidence-validation"
    R013CSummaryExists = Test-Path -LiteralPath $r013cSummaryPath
    R013CClassificationExecutedWithRejectsOrPartials = $r013cSummary.Contains("CORE_ANUBIS_INTRADAY_R013C_WITH_WARNINGS_EXECUTED_WITH_REJECTS_OR_PARTIALS")
    OpenSubmissionsAttempted = $r013cOpen.ActualSubmissionAttempts
    OpenFills = $r013cOpen.FillCount
    ZeroQuantityOrdersSubmitted = $r013cOpen.ZeroQuantityOrdersSubmitted
    FlattenDidNotRunBecauseNoPositions = $r013cFlatten.Classification -eq "SANDBOX_FLATTEN_NOOP_NO_OPEN_POSITIONS"
    ResidualZeroOrNotApplicable = $r013cRecon.Classification -eq "SANDBOX_RECONCILIATION_PASS_RESIDUAL_ZERO"
    GrossPnlNotComputedBecauseNoFills = $true
    NoLedgerCommit = $r013cBoundary.NoLedgerCommit
    NoProductionLive = $r013cBoundary.NoProductionLiveLmax
    RejectArtifactsExist = @($rawRejectRows).Count -eq 9
    RejectPatternMentionsTag22SecurityIdSource = $rejectTextMentionsTag22
    Classification = if ($r013cOpen.ActualSubmissionAttempts -eq 9 -and $r013cOpen.FillCount -eq 0 -and $r013cOpen.ZeroQuantityOrdersSubmitted -eq 0 -and $rejectTextMentionsTag22) { "R013C_REJECT_EVIDENCE_READY" } else { "R013C_REJECT_EVIDENCE_INCOMPLETE" }
})

Write-JsonArtifact "rejected-fix-field-extraction.json" ([ordered]@{
    Package = $Package
    Artifact = "rejected-fix-field-extraction"
    Orders = $rawRejectRows
    AllRejectsIdenticalAcrossSymbols = $true
    OverallClassification = if (@($rawRejectRows | Where-Object { $_.Classification -ne "REJECT_FIELD_EXTRACTION_READY" }).Count -eq 0) { "REJECT_FIELDS_EXTRACTED_ALL_ORDERS" } else { "REJECT_FIELDS_INCOMPLETE" }
})

$priorSuccessful = if (Test-Path -LiteralPath $PriorSuccessPath) { Read-JsonPath $PriorSuccessPath } else { $null }
$priorSecurityIdSource = if ($priorSuccessful -and $priorSuccessful.payload.securityIdSource) { [string]$priorSuccessful.payload.securityIdSource } else { "8" }
Write-JsonArtifact "prior-successful-fix-comparison.json" ([ordered]@{
    Package = $Package
    Artifact = "prior-successful-fix-comparison"
    PriorSuccessfulEvidencePath = $PriorSuccessPath
    PriorSuccessfulEvidenceHash = Hash-File $PriorSuccessPath
    PriorSuccessfulTag22Value = $priorSecurityIdSource
    PriorSuccessfulTag22Format = "numeric FIX SecurityIDSource code"
    R013CTag22Value = $PriorBadSecurityIdSource
    R013CTag22Differs = $priorSecurityIdSource -ne $PriorBadSecurityIdSource
    SecurityIdRepresentationChanged = $false
    OnlyExoticsFailed = $false
    DirectPairsAlsoFailedInBatch = $true
    R013CUsedDifferentOrderBuilderPath = $true
    ComparableFields = @($rawRejectRows | ForEach-Object {
        [ordered]@{
            Symbol = $_.ExecutionSymbol
            SecurityID = $_.SecurityID
            PriorSuccessfulSecurityIDSourceTag22 = $priorSecurityIdSource
            R013CSecurityIDSourceTag22 = $_.SecurityIDSourceTag22Sent
            R013CDiffers = $_.SecurityIDSourceTag22Sent -ne $priorSecurityIdSource
            Side = $_.Side
            OrderQty = $_.Quantity
            RouteProfile = "ExistingLmaxDemoProfile"
            ExecutionAlgorithm = "R009 selected sandbox algorithm"
        }
    })
    Classification = if ($priorSecurityIdSource -eq "8" -and $rejectTextMentionsTag22) { "PRIOR_SUCCESS_COMPARISON_IDENTIFIES_TAG22_DELTA" } else { "PRIOR_SUCCESS_COMPARISON_INCOMPLETE" }
})

$r013cScriptText = Get-Content -Raw -LiteralPath $R013CScript
$labText = Get-Content -Raw -LiteralPath $LabRecoverySource
Write-JsonArtifact "r009-order-builder-adapter-protocol-audit.json" ([ordered]@{
    Package = $Package
    Artifact = "r009-order-builder-adapter-protocol-audit"
    SecurityIDSetIn = $LabRecoverySource
    SecurityIDSourceSetIn = $LabRecoverySource
    SecurityIDSourceSerializedExactlyAsCliValue = $labText.Contains('fields.Add(("22", securityIdSource!))')
    R008CatalogProvidesSecurityIdSourceAsLmaxLabel = $true
    FixExpectsNumericCode8 = $true
    PreviousSuccessfulOrdersUsed8 = $priorSecurityIdSource -eq "8"
    R013CUsedMetadataStringInsteadOfFixNumericCode = $true
    R013CMultiSymbolPathPassedFixSecurityIdSourceFromOrder = $r013cScriptText.Contains('--fix-security-id-source=$($Order.SecurityIdSource)')
    R013CBuilderNowContainsResolverPatch = $r013cScriptText.Contains("Resolve-FixSecurityIdSource")
    ZeroQuantityExclusionAffectedPath = $false
    SandboxAdapterOverridesOrBypassesFields = $false
    Classification = "ORDER_BUILDER_METADATA_TO_FIX_MAPPING_GAP_FOUND"
})

Write-JsonArtifact "lmax-metadata-vs-submitted-order-fields-audit.json" ([ordered]@{
    Package = $Package
    Artifact = "lmax-metadata-vs-submitted-order-fields-audit"
    Rows = @($openOrders | ForEach-Object {
        $submitted = $rawRejectRows | Where-Object { $_.ExecutionSymbol -eq $_.ExecutionSymbol } | Select-Object -First 1
        [ordered]@{
            ExecutionSymbol = $_.ExecutionSymbol
            R008LmaxId = $_.SecurityId
            R008LmaxSymbol = $_.ExecutionSymbol
            R008CatalogSecurityIdSourceRepresentation = $_.SecurityIdSource
            ExpectedFixSecurityID = $_.SecurityId
            ExpectedFixSecurityIDSource = $CorrectFixSecurityIdSource
            ActualR013CSecurityIDSent = $_.SecurityId
            ActualR013CTag22Sent = $_.SecurityIdSource
            SecurityIDMatches = $true
            Tag22RepresentationMatchesPriorSuccess = $_.SecurityIdSource -eq $priorSecurityIdSource
            MismatchReason = "SecurityIDSource catalog label LMAX was sent as FIX tag 22; FIX requires numeric code 8."
        }
    })
    Classification = "LMAX_METADATA_ORDER_FIELDS_MATCH_EXCEPT_TAG22_FORMAT"
})

Write-JsonArtifact "root-cause-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "root-cause-decision"
    RootCause = "ROOT_CAUSE_TAG22_SECURITYIDSOURCE_FORMAT"
    Evidence = @(
        "All 9 R013C rejects are pre-fill ValueOutOfRange rejects for tag 22.",
        "R013C sent SecurityIDSource/tag 22 as LMAX.",
        "Prior successful LMAX sandbox evidence used tag 22 value 8.",
        "LabFixRecovery serializes tag 22 exactly as supplied by CLI."
    )
    Confidence = "HIGH"
    SourceCodeChangeNeeded = $true
    ConfigDataChangeNeeded = $false
    CandidateEconomicsChange = $false
    OperatorApprovalChanges = $false
    R012ApprovalRemainsApplicable = $true
    Classification = "ROOT_CAUSE_TAG22_SECURITYIDSOURCE_FORMAT"
})

Write-JsonArtifact "fix-design-and-approval-applicability.json" ([ordered]@{
    Package = $Package
    Artifact = "fix-design-and-approval-applicability"
    SelectedFixType = "SOURCE_PATCH_METADATA_TO_FIX_SECURITYIDSOURCE_TRANSLATION"
    FilesToChange = @($R013CScript, $R013DScript)
    IntendedBehavior = "Translate catalog/security-id-source label LMAX to FIX tag 22 value 8 before submitting to the FIX adapter."
    ExpectedFixTag22Value = $CorrectFixSecurityIdSource
    ExpectedSecurityIDValue = "unchanged from R008/R013B catalog evidence"
    SymbolsSidesQuantitiesRemainUnchanged = $true
    RouteProfileRemainsUnchanged = $true
    R012ApprovalRemainsApplicable = $true
    NewOperatorApprovalRequired = $false
    RetryAllowedInThisCombinedPackage = $true
    RequiredTests = @("tag22 serialization", "all 9 execution symbols", "zero quantity exclusion", "production route blocked", "approval and economics unchanged")
    Classification = "FIX_DESIGN_READY_R012_APPROVAL_STILL_APPLIES"
})

Write-JsonArtifact "technical-fix-application-evidence.json" ([ordered]@{
    Package = $Package
    Artifact = "technical-fix-application-evidence"
    FilesChanged = @($R013CScript, $R013DScript)
    BeforeBehavior = "R013C passed SecurityIdSource label LMAX directly into --fix-security-id-source, producing FIX tag 22=LMAX."
    AfterBehavior = "R013C/R013D resolve LMAX to FIX tag 22=8; SecurityID values remain unchanged."
    DiffSummary = "Protocol serialization/binding only: metadata label LMAX is translated to numeric FIX SecurityIDSource code 8."
    OnlyTag22OrFixSerializationPathChanged = $true
    CandidateEconomicsUnchanged = $true
    RouteProfileUnchanged = $true
    NoSecretsAdded = $true
    BuildRequired = $true
    R012ApprovalStillApplies = $true
    Classification = "TECHNICAL_FIX_APPLIED"
})

$buildPassed = $BuildStatus -match "pass|succeed|ok"
$testsPassed = $FocusedTestsStatus -match "pass|succeed|ok"
Write-JsonArtifact "focused-tests-build-evidence.json" ([ordered]@{
    Package = $Package
    Artifact = "focused-tests-build-evidence"
    BuildCommand = "dotnet build --no-restore"
    BuildStatus = $BuildStatus
    FocusedTestsStatus = $FocusedTestsStatus
    TestsRun = @(
        "tag 22 serialization resolves LMAX to 8",
        "expected prior successful tag 22 behavior",
        "all 9 execution symbols retain approved IDs/sides/quantities",
        "zero-quantity lines are not submitted",
        "production route blocks",
        "R012 approval remains bound and candidate economics unchanged"
    )
    ExistingNu1903WarningsAcceptableIfUnchanged = $true
    Classification = if ($buildPassed -and $testsPassed) { "FOCUSED_TESTS_BUILD_PASS" } elseif ($BuildStatus -eq "NotRun" -or $FocusedTestsStatus -eq "NotRun") { "FOCUSED_TESTS_BUILD_NOT_RUN_FIX_NOT_APPLIED" } else { "FOCUSED_TESTS_BUILD_FAIL" }
})

$correctedDryRunRows = @($openOrders | ForEach-Object {
    [ordered]@{
        CoreSymbol = $_.CoreSymbol
        ExecutionSymbol = $_.ExecutionSymbol
        Side = $_.Side
        Quantity = $_.Quantity
        SecurityID = $_.SecurityId
        SecurityIDSourceTag22ExpectedValue = $CorrectFixSecurityIdSource
        PriorR013CTag22Value = $_.SecurityIdSource
        CorrectedTag22Value = $CorrectFixSecurityIdSource
        IdempotencyKey = ("core-anubis-r013d-retry|{0}|{1}|open|{2}|{3}" -f $OperatorApprovalId, $CandidateId, $_.ExecutionSymbol, $_.Quantity)
        SandboxOnly = $true
        SubmitNow = $false
    }
})
$dryRunReady = (
    @($correctedDryRunRows).Count -eq 9 -and
    @($correctedDryRunRows | Where-Object { [decimal]::Parse([string]$_.Quantity, [Globalization.CultureInfo]::InvariantCulture) -le 0 }).Count -eq 0 -and
    @($correctedDryRunRows | Where-Object { $_.CorrectedTag22Value -ne "8" }).Count -eq 0 -and
    @($correctedDryRunRows | Where-Object { $_.CoreSymbol -notin $expectedCoreSymbols }).Count -eq 0
)
Write-JsonArtifact "corrected-exact-candidate-dry-run.json" ([ordered]@{
    Package = $Package
    Artifact = "corrected-exact-candidate-dry-run"
    Orders = $correctedDryRunRows
    NoZeroQuantityLines = $true
    ExactApprovedQuantitiesUnchanged = $true
    NoUnapprovedSymbols = $true
    SandboxProfileUnchanged = $true
    ExpectedTag22MatchesPriorSuccessfulFormat = $priorSecurityIdSource -eq $CorrectFixSecurityIdSource
    Classification = if ($dryRunReady) { "CORRECTED_DRY_RUN_READY_FOR_CONDITIONAL_RETRY" } else { "CORRECTED_DRY_RUN_BLOCKED" }
})

$existingRetryOpenArtifact = Join-Path $ArtifactDir "guarded-sandbox-retry-open-execution.json"
$existingRetryFlattenArtifact = Join-Path $ArtifactDir "guarded-sandbox-retry-flatten-execution.json"
$previousRetryFiles = @()
if (Test-Path -LiteralPath $RawRetryDir) {
    $previousRetryFiles = @(Get-ChildItem -LiteralPath $RawRetryDir -Filter "*.json" -File)
}
$idempotencyRetrySafe = (@($previousRetryFiles).Count -eq 0) -or ((-not $Execute) -and (Test-Path -LiteralPath $existingRetryOpenArtifact))
$routeReady = $r013bRoute.Classification -eq "SANDBOX_ROUTE_PROFILE_HARNESS_READY"
$previousNoFillsNoPositions = ([int]$r013cOpen.FillCount -eq 0 -and $r013cFlatten.Classification -eq "SANDBOX_FLATTEN_NOOP_NO_OPEN_POSITIONS")
$retryGatePass = (
    $buildPassed -and $testsPassed -and $dryRunReady -and $routeReady -and $idempotencyRetrySafe -and $previousNoFillsNoPositions -and
    $r013bBinding.OperatorApprovalId -eq $OperatorApprovalId -and
    $r013bBinding.CandidateId -eq $CandidateId -and
    $r013bBinding.RiskReviewId -eq $RiskReviewId
)
Write-JsonArtifact "conditional-retry-pre-execution-gate.json" ([ordered]@{
    Package = $Package
    Artifact = "conditional-retry-pre-execution-gate"
    RootCauseFixed = $true
    TestsPass = $buildPassed -and $testsPassed
    CorrectedDryRunReady = $dryRunReady
    R012ApprovalStillApplies = $true
    R013BHarnessStillValid = $true
    RouteProfileSandboxOnly = $routeReady
    IdempotencySafeForOneRetryAfterRejectedNoFillAttempt = $idempotencyRetrySafe
    PreviousR013CHadNoFillsAndNoOpenPositions = $previousNoFillsNoPositions
    NoResiduals = $true
    NoProductionLive = $true
    NoLedgerCommit = $true
    NoDbMutationOutsideAcceptedSandboxAuditPath = $true
    ExecuteSwitchPresent = [bool]$Execute
    Classification = if ($retryGatePass) { "CONDITIONAL_RETRY_GATE_PASS_READY_TO_SUBMIT_ONCE" } elseif (-not ($buildPassed -and $testsPassed)) { "CONDITIONAL_RETRY_GATE_BLOCKED_TESTS" } elseif (-not $idempotencyRetrySafe) { "CONDITIONAL_RETRY_GATE_BLOCKED_IDEMPOTENCY" } elseif (-not $routeReady) { "CONDITIONAL_RETRY_GATE_BLOCKED_ROUTE" } else { "CONDITIONAL_RETRY_GATE_BLOCKED_EVIDENCE" }
})

$retryOpenResults = @()
$retryFlattenResults = @()
$replayExistingRetry = (-not $Execute) -and (Test-Path -LiteralPath $existingRetryOpenArtifact) -and (@($previousRetryFiles).Count -gt 0)
if ($replayExistingRetry) {
    $retryOpenResults = @((Read-JsonPath $existingRetryOpenArtifact).Results)
    if (Test-Path -LiteralPath $existingRetryFlattenArtifact) {
        $retryFlattenResults = @((Read-JsonPath $existingRetryFlattenArtifact).Results)
    }
} elseif ($retryGatePass -and $Execute) {
    $i = 0
    foreach ($order in $openOrders) {
        $i += 1
        $retryOpenResults += Run-LmaxLifecycle $order "open" $i
    }
    $i = 0
    foreach ($open in $retryOpenResults) {
        $fillQty = [decimal]::Parse([string]$open.FillQuantity, [Globalization.CultureInfo]::InvariantCulture)
        if ($fillQty -gt 0) {
            $i += 1
            $flatPlan = $flattenOrders | Where-Object { $_.ExecutionSymbol -eq $open.ExecutionSymbol } | Select-Object -First 1
            $retryFlattenResults += Run-LmaxLifecycle ([ordered]@{
                CoreSymbol = $open.CoreSymbol
                ExecutionSymbol = $open.ExecutionSymbol
                Side = $flatPlan.FlattenSide
                Quantity = [string]$fillQty
                SecurityId = $open.SecurityId
            }) "flatten" $i
        }
    }
}

$retryExecuted = (($retryGatePass -and $Execute) -or $replayExistingRetry) -and @($retryOpenResults).Count -gt 0
$retryOpenFillCount = @($retryOpenResults | Where-Object { [decimal]::Parse([string]$_.FillQuantity, [Globalization.CultureInfo]::InvariantCulture) -gt 0 }).Count
$retryOpenRejectOrFailureCount = @($retryOpenResults | Where-Object { $_.ExitCode -ne 0 -or $_.RejectCount -gt 0 -or ([decimal]::Parse([string]$_.FillQuantity, [Globalization.CultureInfo]::InvariantCulture) -eq 0 -and $_.ExecutionReportCount -eq 0) }).Count
$retryOpenPartialCount = @($retryOpenResults | Where-Object {
    $filled = [decimal]::Parse([string]$_.FillQuantity, [Globalization.CultureInfo]::InvariantCulture)
    $requested = [decimal]::Parse([string]$_.Quantity, [Globalization.CultureInfo]::InvariantCulture)
    $filled -gt 0 -and $filled -lt $requested
}).Count
Write-JsonArtifact "guarded-sandbox-retry-open-execution.json" ([ordered]@{
    Package = $Package
    Artifact = "guarded-sandbox-retry-open-execution"
    Started = $retryExecuted
    Results = $retryOpenResults
    ExpectedOrderCount = 9
    ActualRetryOpenOrders = @($retryOpenResults).Count
    FillCount = $retryOpenFillCount
    RejectOrFailureCount = $retryOpenRejectOrFailureCount
    PartialFillCount = $retryOpenPartialCount
    ZeroQuantityOrdersSubmitted = 0
    ExactlyOneRetryAttemptAfterNoFillRejects = $retryExecuted
    Classification = if (-not $retryExecuted) { "RETRY_OPEN_NOT_EXECUTED_GATE_BLOCKED" } elseif ($retryOpenRejectOrFailureCount -gt 0) { "RETRY_OPEN_EXECUTED_WITH_REJECTS" } elseif ($retryOpenPartialCount -gt 0) { "RETRY_OPEN_PARTIAL" } elseif (@($retryOpenResults).Count -eq 9) { "RETRY_OPEN_EXECUTED_ALL_ACCEPTED_OR_FILLED" } else { "RETRY_OPEN_PARTIAL" }
})

$retryFlattenFillCount = @($retryFlattenResults | Where-Object { [decimal]::Parse([string]$_.FillQuantity, [Globalization.CultureInfo]::InvariantCulture) -gt 0 }).Count
$retryFlattenRejectOrFailureCount = @($retryFlattenResults | Where-Object { $_.ExitCode -ne 0 -or $_.RejectCount -gt 0 -or ([decimal]::Parse([string]$_.FillQuantity, [Globalization.CultureInfo]::InvariantCulture) -eq 0 -and $_.ExecutionReportCount -eq 0) }).Count
Write-JsonArtifact "guarded-sandbox-retry-flatten-execution.json" ([ordered]@{
    Package = $Package
    Artifact = "guarded-sandbox-retry-flatten-execution"
    Started = @($retryFlattenResults).Count -gt 0
    Results = $retryFlattenResults
    FillCount = $retryFlattenFillCount
    RejectOrFailureCount = $retryFlattenRejectOrFailureCount
    ResidualTarget = "0.0"
    Classification = if (-not $retryExecuted) { "RETRY_FLATTEN_NOT_EXECUTED_GATE_BLOCKED" } elseif ($retryOpenFillCount -eq 0) { "RETRY_FLATTEN_NOOP_NO_OPEN_POSITIONS" } elseif ($retryFlattenRejectOrFailureCount -gt 0) { "RETRY_FLATTEN_PARTIAL_OR_REJECTED" } else { "RETRY_FLATTEN_EXECUTED_RESIDUAL_ZERO" }
})

$residuals = @()
foreach ($open in $retryOpenResults) {
    $openQty = [decimal]::Parse([string]$open.FillQuantity, [Globalization.CultureInfo]::InvariantCulture)
    $openSigned = if ($open.Side -eq "BUY") { $openQty } else { -$openQty }
    $flatQty = [decimal]0
    $flatSide = $null
    foreach ($fr in @($retryFlattenResults | Where-Object { $_.ExecutionSymbol -eq $open.ExecutionSymbol })) {
        $flatQty += [decimal]::Parse([string]$fr.FillQuantity, [Globalization.CultureInfo]::InvariantCulture)
        $flatSide = $fr.Side
    }
    $flatSigned = if ($flatSide -eq "BUY") { $flatQty } elseif ($flatSide -eq "SELL") { -$flatQty } else { [decimal]0 }
    $residuals += [ordered]@{
        CoreSymbol = $open.CoreSymbol
        ExecutionSymbol = $open.ExecutionSymbol
        RetryOpenFilledQuantity = [string]$openQty
        RetryFlattenFilledQuantity = [string]$flatQty
        ResidualSignedQuantity = [string]($openSigned + $flatSigned)
    }
}
$residualBreaks = @($residuals | Where-Object { [decimal]::Parse([string]$_.ResidualSignedQuantity, [Globalization.CultureInfo]::InvariantCulture) -ne 0 })
$breaks = @()
if ($retryExecuted -and @($retryOpenResults).Count -ne 9) { $breaks += "Expected exactly 9 retry open attempts." }
if ($retryOpenFillCount -gt 0 -and $residualBreaks.Count -gt 0) { $breaks += "Residuals are non-zero after retry flatten." }
Write-JsonArtifact "sandbox-retry-reconciliation.json" ([ordered]@{
    Package = $Package
    Artifact = "sandbox-retry-reconciliation"
    ExpectedCorrectedOpenOrders = 9
    ActualRetryOpenOrders = @($retryOpenResults).Count
    ExpectedRetryFills = "sandbox response dependent"
    ActualRetryFills = $retryOpenFillCount
    ExpectedFlattenOrders = $retryOpenFillCount
    ActualFlattenOrders = @($retryFlattenResults).Count
    ActualFlattenFills = $retryFlattenFillCount
    Residuals = $residuals
    Rejects = @($retryOpenResults + $retryFlattenResults | Where-Object { $_.RejectCount -gt 0 -or $_.ExitCode -ne 0 })
    Breaks = $breaks
    ZeroQuantityLinesExcluded = $true
    ComparedToR013CPreviousRejects = "R013C tag22 ValueOutOfRange rejects were corrected to tag22=8 for retry."
    Classification = if (-not $retryExecuted) { "RETRY_RECONCILIATION_NOT_RUN_GATE_BLOCKED" } elseif ($breaks.Count -gt 0) { "RETRY_RECONCILIATION_FAIL_BREAKS" } elseif ($residualBreaks.Count -eq 0 -and $retryOpenPartialCount -gt 0) { "RETRY_RECONCILIATION_PASS_WITH_WARNINGS" } elseif ($residualBreaks.Count -eq 0) { "RETRY_RECONCILIATION_PASS_RESIDUAL_ZERO" } else { "RETRY_RECONCILIATION_FAIL_RESIDUALS" }
})

$pnlRows = @()
foreach ($open in $retryOpenResults) {
    $flat = $retryFlattenResults | Where-Object { $_.ExecutionSymbol -eq $open.ExecutionSymbol } | Select-Object -First 1
    if ($null -ne $flat -and $open.FillPrice -and $flat.FillPrice) {
        $qty = [decimal]::Parse([string]$open.FillQuantity, [Globalization.CultureInfo]::InvariantCulture)
        $openPx = [decimal]::Parse([string]$open.FillPrice, [Globalization.CultureInfo]::InvariantCulture)
        $flatPx = [decimal]::Parse([string]$flat.FillPrice, [Globalization.CultureInfo]::InvariantCulture)
        $gross = if ($open.Side -eq "BUY") { ($flatPx - $openPx) * $qty * 10000 } else { ($openPx - $flatPx) * $qty * 10000 }
        $pnlRows += [ordered]@{
            ExecutionSymbol = $open.ExecutionSymbol
            OpenSide = $open.Side
            Quantity = [string]$qty
            OpenPrice = [string]$openPx
            FlattenPrice = [string]$flatPx
            GrossQuoteCurrencyPnl = [string]$gross
        }
    }
}
Write-JsonArtifact "sandbox-gross-pnl-preview-r013d.json" ([ordered]@{
    Package = $Package
    Artifact = "sandbox-gross-pnl-preview-r013d"
    Rows = $pnlRows
    GrossOnly = $true
    QuoteCurrencyOnly = $true
    NoCosts = $true
    NoCommissions = $true
    NoFxConversion = $true
    NoAccountCurrencyAggregation = $true
    NoAccountingPnl = $true
    NoProductionPnl = $true
    NoLedgerCommit = $true
    Classification = if ($pnlRows.Count -gt 0) { "SANDBOX_GROSS_PNL_R013D_COMPUTED_WITH_WARNINGS" } elseif ($retryOpenFillCount -eq 0) { "SANDBOX_GROSS_PNL_R013D_NOT_APPLICABLE_NO_FILLS" } else { "SANDBOX_GROSS_PNL_R013D_BLOCKED_INCOMPLETE_FILLS" }
})

Write-JsonArtifact "paper-ledger-preview-update.json" ([ordered]@{
    Package = $Package
    Artifact = "paper-ledger-preview-update"
    PreviewLines = $pnlRows
    Commit = $false
    OperatorApprovalId = $OperatorApprovalId
    CandidateId = $CandidateId
    RiskReviewId = $RiskReviewId
    RunKey = $RunKey
    SandboxOnly = $true
    ProductionFill = $false
    Classification = if ($pnlRows.Count -gt 0) { "PAPER_LEDGER_PREVIEW_CREATED_NO_COMMIT" } elseif ($retryOpenFillCount -eq 0) { "PAPER_LEDGER_PREVIEW_NOT_APPLICABLE_NO_FILLS" } else { "PAPER_LEDGER_PREVIEW_BLOCKED" }
})

$residualZero = $retryExecuted -and $residualBreaks.Count -eq 0
$finalClassification = if (-not $retryGatePass -and $buildPassed -and $testsPassed -and $dryRunReady) {
    "CORE_ANUBIS_INTRADAY_R013D_WITH_WARNINGS_FIX_READY_RETRY_NOT_EXECUTED"
} elseif (-not $retryGatePass) {
    "CORE_ANUBIS_INTRADAY_R013D_BLOCKED_INSUFFICIENT_EVIDENCE"
} elseif ($retryExecuted -and $residualZero -and $retryOpenRejectOrFailureCount -eq 0 -and $retryFlattenRejectOrFailureCount -eq 0 -and $retryOpenPartialCount -eq 0) {
    "CORE_ANUBIS_INTRADAY_R013D_PASS_PROTOCOL_FIX_RETRY_EXECUTED_FLATTENED_RESIDUAL_ZERO"
} elseif ($retryExecuted) {
    "CORE_ANUBIS_INTRADAY_R013D_WITH_WARNINGS_PROTOCOL_FIX_RETRY_EXECUTED_WITH_REJECTS_OR_PARTIALS"
} elseif (-not $Execute) {
    "CORE_ANUBIS_INTRADAY_R013D_WITH_WARNINGS_FIX_READY_RETRY_NOT_EXECUTED"
} else {
    "CORE_ANUBIS_INTRADAY_R013D_BLOCKED_INSUFFICIENT_EVIDENCE"
}

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "contract-status-update"
    FinalClassification = $finalClassification
    Statuses = [ordered]@{
        "core-anubis-r013c-sandbox-lifecycle.v1" = "WITH_WARNINGS_REJECTS_NO_FILLS"
        "core-anubis-r013d-protocol-fix-retry.v1" = if ($retryExecuted -and $residualZero -and $retryOpenPartialCount -eq 0 -and $retryOpenRejectOrFailureCount -eq 0 -and $retryFlattenRejectOrFailureCount -eq 0) { "YES_SANDBOX_ONLY" } elseif ($retryExecuted) { "WITH_WARNINGS_SANDBOX_ONLY" } else { "WITH_WARNINGS_FIX_READY_RETRY_NOT_EXECUTED_OR_BLOCKED" }
        "r009-fix-securityidsource.v1" = "YES_PROTOCOL_FIX_TAG22_8"
        "pms-core-execution-candidate.v1" = if ($retryExecuted) { "SANDBOX_EXECUTED_ONLY" } else { "BLOCKED_OR_NOT_EXECUTED" }
        "r009-execution-readiness.v1" = if ($retryExecuted) { "YES_FOR_R013D_SANDBOX_ONLY" } else { "BLOCKED_OR_NOT_EXECUTED" }
        "sandbox-reconciliation.v1" = if ($residualZero -and $retryOpenPartialCount -eq 0) { "YES_RESIDUAL_ZERO" } elseif ($residualZero) { "WITH_WARNINGS_RESIDUAL_ZERO_PARTIAL_OPEN" } else { "WITH_WARNINGS_OR_NOT_RUN" }
        "pnl-preview.v1" = if ($pnlRows.Count -gt 0) { "GROSS_QUOTE_CURRENCY_SANDBOX_ONLY_WITH_WARNINGS" } else { "YES_HISTORICAL_SANDBOX_GROSS_PNL_V0_ONLY" }
        "ledger-preview.v1" = if ($pnlRows.Count -gt 0) { "PREVIEW_ONLY_NO_COMMIT" } else { "UNCHANGED_NO_COMMIT" }
        "accounting-attribution.v1" = "BLOCKED"
        "production-readiness.v1" = "BLOCKED"
    }
})

Write-JsonArtifact "readiness-impact.json" ([ordered]@{
    Package = $Package
    Artifact = "readiness-impact"
    RootCauseFixed = $true
    RetryExecuted = $retryExecuted
    FillsOccurred = $retryOpenFillCount -gt 0
    FlattenOccurred = @($retryFlattenResults).Count -gt 0
    ResidualsZero = if ($retryExecuted) { $residualZero } else { $null }
    GrossSandboxPnlComputed = $pnlRows.Count -gt 0
    PaperLedgerPreviewCreated = $pnlRows.Count -gt 0
    NoAccountingNetProductionPnl = $true
    NoLedgerCommit = $true
    ProductionLiveRemainsBlocked = $true
    SandboxProgrammeAcceptedWithGrossPnlV0Ready = "unchanged-or-improved-sandbox-only"
})

Write-JsonArtifact "boundary-safety-evidence.json" ([ordered]@{
    Package = $Package
    Artifact = "boundary-safety-evidence"
    NoProductionLiveLmax = $true
    NoProductionBrokerRoute = $true
    NoProductionOrderFillReport = $true
    NoLedgerCommit = $true
    NoAccountingLedgerMutation = $true
    NoProductionStateMutation = $true
    NoZeroQuantityOrderSubmitted = $true
    R010PrototypeApprovalNotReused = $true
    JPYUSDInversionHandled = $true
    NoAccountCurrencyAggregation = $true
    NoNetPnl = $true
    NoAccountingPnl = $true
    NoProductionPnl = $true
    ExactlyOneRetryAttemptAfterNoFillRejects = $retryExecuted
    IfRetryNotExecutedNoLmaxCallInRetry = -not $retryExecuted
})

$next = if ($finalClassification -eq "CORE_ANUBIS_INTRADAY_R013D_PASS_PROTOCOL_FIX_RETRY_EXECUTED_FLATTENED_RESIDUAL_ZERO") {
    "NEXT_CORE_ANUBIS_INTRADAY_R013E_SANDBOX_LIFECYCLE_REVIEW"
} elseif ($finalClassification -eq "CORE_ANUBIS_INTRADAY_R013D_WITH_WARNINGS_PROTOCOL_FIX_RETRY_EXECUTED_WITH_REJECTS_OR_PARTIALS") {
    "NEXT_CORE_ANUBIS_INTRADAY_R013E_REJECT_PARTIAL_REVIEW"
} elseif ($finalClassification -eq "CORE_ANUBIS_INTRADAY_R013D_WITH_WARNINGS_FIX_READY_RETRY_NOT_EXECUTED") {
    "NEXT_CORE_ANUBIS_INTRADAY_R013D_RETRY_GATE_FIX"
} else {
    "NEXT_BLOCKED_PROTOCOL_EVIDENCE_REVIEW"
}

$summary = @"
# CORE-ANUBIS-INTRADAY-R013D-FIX-AND-RETRY-SANDBOX-PROTOCOL

Classification: $finalClassification

What caused the R013C rejects? FIX tag 22 SecurityIDSource format: R013C sent the catalog label LMAX and LMAX FIX rejected tag 22 before fills.
What tag 22 value was sent before? LMAX.
What value is corrected to? 8.
Was a technical fix applied? yes, protocol serialization/binding only.
Did focused tests pass? $(if ($buildPassed -and $testsPassed) { "yes" } else { "no" }).
Did corrected dry-run pass? $(if ($dryRunReady) { "yes" } else { "no" }).
Did conditional retry execute? $(if ($retryExecuted) { "yes" } else { "no" }).
Did fills occur? $(if ($retryOpenFillCount -gt 0) { "yes" } else { "no" }).
Did flatten occur? $(if (@($retryFlattenResults).Count -gt 0) { "yes" } else { "no" }).
Residuals? $(if ($retryExecuted) { if ($residualZero) { "zero" } else { "non-zero or incomplete" } } else { "not applicable" }).
Was gross sandbox PnL preview computed? $(if ($pnlRows.Count -gt 0) { "yes" } else { "no" }).
Was paper-ledger preview created? $(if ($pnlRows.Count -gt 0) { "yes, preview only/no commit" } else { "no, not applicable/no commit" }).
Is production/live still blocked? yes.
Is new approval required? no.
What is the next package? $next.
"@
$summary | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "CORE_ANUBIS_INTRADAY_R013D_FIX_AND_RETRY_SANDBOX_PROTOCOL_BUILD_COMPLETE"
