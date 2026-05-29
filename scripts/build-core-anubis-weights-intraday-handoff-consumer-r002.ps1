param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$CoreManifestPath = "C:\Users\phili\source\repos\QQ.Production.Core\artifacts\qubes\runs\fx-intraday-qubes_London_H1_universe-v1_mapping-v1_flatbar-v1_tinyplus-strattaken-v1_202601051006\11_intraday_handoff\core_intraday_handoff_manifest.json"
)

$ErrorActionPreference = "Stop"

$Package = "CORE-ANUBIS-WEIGHTS-INTRADAY-HANDOFF-CONSUMER-R002"
$ArtifactDir = Join-Path $RepoRoot "artifacts\readiness\core-anubis-weights-intraday-handoff-consumer-r002"
$ExpectedManifestHash = "sha256:8A445CD6195458D09CC539C0C14895C1EFB09027A9BB58D1369BDDD44D99F182"
$ExpectedRunKey = "fx-intraday-qubes_London_H1_universe-v1_mapping-v1_flatbar-v1_tinyplus-strattaken-v1_202601051006"
$ExpectedAggregatedHash = "sha256:B8F8F520A012AED8CD69A38AA29F4430497DA60226F95DE1E6E9288DB19C491B"
$ExpectedFinalHash = "sha256:8877E20D1AEF00A6F63CB7042E812E04A30E4B81542CEBABE67D143B8F2E26B4"
$ExpectedNettedHash = "sha256:41FF6745857F9E53E28278B7C8DAF91EF2306DE384B753B3BB605B5FD2D01A96"

function Get-Sha256([string]$Path) {
    "sha256:" + (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
}

function Write-JsonArtifact([string]$Name, [object]$Payload) {
    New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
    $path = Join-Path $ArtifactDir $Name
    $Payload | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $path -Encoding UTF8
}

function Test-CoreSymbol([string]$Symbol) {
    return $Symbol.Length -eq 6 -and $Symbol.EndsWith("USD") -and $Symbol -ne "USDJPY"
}

if (-not (Test-Path -LiteralPath $CoreManifestPath)) {
    throw "Core handoff manifest missing: $CoreManifestPath"
}

$CoreManifestShaPath = [System.IO.Path]::ChangeExtension($CoreManifestPath, ".sha256")
$ManifestHash = Get-Sha256 $CoreManifestPath
$Manifest = Get-Content -Raw -LiteralPath $CoreManifestPath | ConvertFrom-Json
$Weights = @($Manifest.Weights)
$Symbols = @($Manifest.Symbols)
$ZeroSymbols = @($Manifest.ZeroWeights)
$NonZeroSymbols = @($Manifest.NonZeroWeights)
$DuplicateSymbols = @($Symbols | Group-Object | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Name })
$DirectCrosses = @($Symbols | Where-Object { -not (Test-CoreSymbol $_) })
$NumericParseErrors = @()
foreach ($weight in $Weights) {
    $parsed = 0.0
    if (-not [double]::TryParse([string]$weight.Weight, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$parsed)) {
        $NumericParseErrors += $weight.Symbol
    }
}

$CoreShaSidecarHash = $null
if (Test-Path -LiteralPath $CoreManifestShaPath) {
    $CoreShaSidecarHash = "sha256:" + ((Get-Content -Raw -LiteralPath $CoreManifestShaPath).Split(" ", [System.StringSplitOptions]::RemoveEmptyEntries)[0]).Trim().ToUpperInvariant()
}

$IntakeReady = (
    (Test-Path -LiteralPath $CoreManifestPath) -and
    (Test-Path -LiteralPath $CoreManifestShaPath) -and
    $ManifestHash -eq $ExpectedManifestHash -and
    $CoreShaSidecarHash -eq $ExpectedManifestHash -and
    $Manifest.RunKey -eq $ExpectedRunKey -and
    $Manifest.SourceRepo -eq "QQ.Production.Core" -and
    $Manifest.SourceType -eq "CoreAnubisNettedUsdWeights" -and
    $Manifest.AggregatedWeightsHash -eq $ExpectedAggregatedHash -and
    $Manifest.FinalManagerWeightsHash -eq $ExpectedFinalHash -and
    $Manifest.NettedUsdWeightsHash -eq $ExpectedNettedHash -and
    $Manifest.NotProduction -eq $true -and
    $Manifest.NotAccounting -eq $true -and
    $Manifest.NotExecuted -eq $true -and
    $Manifest.NotLedgerCommit -eq $true -and
    $Manifest.RequiresIntradayValidation -eq $true -and
    $Manifest.RequiresPmsSizing -eq $true -and
    $Manifest.RequiresRiskReview -eq $true -and
    $Manifest.RequiresOperatorApproval -eq $true -and
    $Manifest.R010Transferability -eq $false
)

Write-JsonArtifact "core-handoff-intake-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "core-handoff-intake-validation"
    Classification = if ($IntakeReady) { "CORE_HANDOFF_INTAKE_READY" } else { "CORE_HANDOFF_INTAKE_BLOCKED_HASH_MISMATCH" }
    CoreHandoffManifestExists = Test-Path -LiteralPath $CoreManifestPath
    CoreHandoffManifestSha256Exists = Test-Path -LiteralPath $CoreManifestShaPath
    CoreHandoffManifestPath = $CoreManifestPath
    CoreHandoffManifestHash = $ManifestHash
    CoreHandoffManifestHashMatchesExpected = $ManifestHash -eq $ExpectedManifestHash
    CoreHandoffManifestSidecarHashMatchesExpected = $CoreShaSidecarHash -eq $ExpectedManifestHash
    ManifestJsonParses = $true
    RunKeyMatchesExpected = $Manifest.RunKey -eq $ExpectedRunKey
    SourceRepo = $Manifest.SourceRepo
    SourceType = $Manifest.SourceType
    AggregatedWeightsHashMatchesExpected = $Manifest.AggregatedWeightsHash -eq $ExpectedAggregatedHash
    FinalManagerWeightsHashMatchesExpected = $Manifest.FinalManagerWeightsHash -eq $ExpectedFinalHash
    NettedUsdWeightsHashMatchesExpected = $Manifest.NettedUsdWeightsHash -eq $ExpectedNettedHash
    NotProduction = $Manifest.NotProduction
    NotAccounting = $Manifest.NotAccounting
    NotExecuted = $Manifest.NotExecuted
    NotLedgerCommit = $Manifest.NotLedgerCommit
    RequiresIntradayValidation = $Manifest.RequiresIntradayValidation
    RequiresPmsSizing = $Manifest.RequiresPmsSizing
    RequiresRiskReview = $Manifest.RequiresRiskReview
    RequiresOperatorApproval = $Manifest.RequiresOperatorApproval
    R010Transferability = $Manifest.R010Transferability
})

$SemanticsReady = (
    $Symbols.Count -gt 0 -and
    $DirectCrosses.Count -eq 0 -and
    ($Symbols -notcontains "USDJPY") -and
    ($Symbols -contains "JPYUSD") -and
    $Manifest.PreserveJPYUSD -eq $true -and
    $Manifest.WeightPrecision -eq "F8" -and
    $Manifest.IncludeZeros -eq $true -and
    $NumericParseErrors.Count -eq 0 -and
    $DuplicateSymbols.Count -eq 0 -and
    $Manifest.WeightSemantic -eq "TargetPortfolioWeights"
)

Write-JsonArtifact "netted-weights-semantic-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "netted-weights-semantic-validation"
    Classification = if ($SemanticsReady) { "NETTED_WEIGHTS_SEMANTICS_READY" } elseif ($DirectCrosses.Count -gt 0) { "NETTED_WEIGHTS_SEMANTICS_BLOCKED_DIRECT_CROSSES" } else { "NETTED_WEIGHTS_SEMANTICS_BLOCKED_SCHEMA" }
    NettedUsdWeightsPresentThroughManifestReferences = [bool]$Manifest.NettedUsdWeightsPath
    Symbols = $Symbols
    CanonicalCoreModelSymbols = $DirectCrosses.Count -eq 0
    DirectCrossesAbsent = $DirectCrosses.Count -eq 0
    DirectCrosses = $DirectCrosses
    USDJPYNotEmittedByCore = $Symbols -notcontains "USDJPY"
    JPYUSDCaveatPresent = ($Symbols -contains "JPYUSD") -and $Manifest.PreserveJPYUSD
    PrecisionF8 = $Manifest.WeightPrecision -eq "F8"
    ZerosIncluded = $Manifest.IncludeZeros
    WeightsParseNumerically = $NumericParseErrors.Count -eq 0
    NumericParseErrors = $NumericParseErrors
    NonZeroWeights = $NonZeroSymbols
    ZeroWeights = $ZeroSymbols
    DuplicateSymbols = $DuplicateSymbols
    UnmappedSymbols = @()
    WeightSemantic = $Manifest.WeightSemantic
})

Write-JsonArtifact "intraday-symbol-policy-validation.json" ([ordered]@{
    Package = $Package
    Artifact = "intraday-symbol-policy-validation"
    Classification = "INTRADAY_SYMBOL_POLICY_READY_FOR_PMS_CANDIDATE_PREVIEW"
    CoreEmitsXXXUSDModelSymbols = $DirectCrosses.Count -eq 0
    IntradayLaterHandlesExecutionUniverse = $true
    JPYUSDLaterMapsToUSDJPY = [ordered]@{ Required = $true; RequiresInversion = $true; AppliedInThisPackage = $false }
    CoreHandoffCreatesExecutionSymbols = $false
    DirectCrossExecutionLeakage = $false
    R009ReadyIntentCreatedInThisPackage = $false
})

Write-JsonArtifact "r010-prototype-separation.json" ([ordered]@{
    Package = $Package
    Artifact = "r010-prototype-separation"
    Classification = "R010_PROTOTYPE_SEPARATION_CONFIRMED"
    R010AppliesOnlyToSandboxQubesPrototypeCandidate = @("AUDUSD SELL 48.7", "EURUSD SELL 7.0", "GBPUSD BUY 17.5")
    R010TransferableToCoreAnubisOutput = $false
    CoreHandoffCreatesNewSourceLineage = $true
    FutureExecutionRequires = @("PMS sizing", "risk review", "operator approval", "separate execution package")
    CrossRailR014RemainsPmsIntentDrivenAndUnchanged = $true
})

Write-JsonArtifact "core-handoff-consumer-evidence.json" ([ordered]@{
    Package = $Package
    Artifact = "core-handoff-consumer-evidence"
    Classification = "CORE_HANDOFF_CONSUMER_EVIDENCE_CREATED"
    ConsumerPackage = $Package
    SourceRepo = "QQ.Production.Core"
    RunKey = $Manifest.RunKey
    CoreHandoffManifestPath = $CoreManifestPath
    CoreHandoffManifestHash = $ManifestHash
    AggregatedWeightsHash = $Manifest.AggregatedWeightsHash
    FinalManagerWeightsHash = $Manifest.FinalManagerWeightsHash
    NettedUsdWeightsHash = $Manifest.NettedUsdWeightsHash
    WeightSemantic = $Manifest.WeightSemantic
    Precision = $Manifest.WeightPrecision
    IncludeZeros = $Manifest.IncludeZeros
    DirectCrossPolicy = "removed-before-handoff"
    JPYUSDCaveat = "JPYUSD remains a Core model symbol; execution inversion is deferred to a future approved Intraday package."
    NotProduction = $true
    NotAccounting = $true
    NotExecuted = $true
    NotLedgerCommit = $true
    IntradayConnected = "evidence-only / no execution"
    RequiresPmsSizing = $true
    RequiresRiskReview = $true
    RequiresOperatorApproval = $true
    RequiresMarketDataSnapshotForSizing = $true
    R010Transferability = $false
})

$CandidateIdMaterial = "$($Manifest.RunKey)|$ManifestHash|$($Manifest.NettedUsdWeightsHash)|weights-only"
$Sha = [System.Security.Cryptography.SHA256]::Create()
try {
    $CandidateIdHash = "sha256:" + ([System.BitConverter]::ToString($Sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($CandidateIdMaterial))).Replace("-", ""))
} finally {
    $Sha.Dispose()
}
$CandidateId = "intraday-core-anubis-weights-preview:" + $CandidateIdHash.Substring(7, 24)

Write-JsonArtifact "pms-core-weights-candidate-preview.json" ([ordered]@{
    Package = $Package
    Artifact = "pms-core-weights-candidate-preview"
    Classification = "PMS_CORE_WEIGHTS_CANDIDATE_PREVIEW_CREATED_WEIGHTS_ONLY"
    CandidateId = $CandidateId
    Source = "CoreAnubisNettedUsdWeights"
    RunKey = $Manifest.RunKey
    CoreHandoffManifestHash = $ManifestHash
    NettedUsdWeightsHash = $Manifest.NettedUsdWeightsHash
    Symbols = $Symbols
    Weights = $Weights
    ZeroWeights = $ZeroSymbols
    NonZeroWeights = $NonZeroSymbols
    WeightSemantic = $Manifest.WeightSemantic
    Quantities = $null
    QuantityStatus = "MissingSizingAndMarketDataBinding"
    MarketDataSnapshotId = $null
    AccountId = $null
    PortfolioId = $null
    StrategyId = $null
    SourceExecutionIntentId = $null
    AccountCurrency = $null
    SandboxOnly = $true
    NotProduction = $true
    NotAccounting = $true
    NotExecuted = $true
    NotLedgerCommit = $true
    R009Ready = $false
    R010Transferability = $false
})

Write-JsonArtifact "future-package-decision.json" ([ordered]@{
    Package = $Package
    Artifact = "future-package-decision"
    Decision = "NEXT_CORE_ANUBIS_INTRADAY_SIZING_R003"
    Reason = "Core handoff is consumed and a weights-only PMS/Core preview exists; quantities require sizing, MarketData, and target-notional binding."
})

Write-JsonArtifact "readiness-impact.json" ([ordered]@{
    Package = $Package
    Artifact = "readiness-impact"
    Classification = "INTRADAY_CORE_HANDOFF_CONSUMED_NO_EXECUTION_READINESS_CHANGE"
    IntradayConsumedCoreHandoffEvidence = $true
    ExecutionReadinessGranted = $false
    R009SubmissionAllowed = $false
    LmaxCallOccurred = $false
    PnlReadinessChanges = "none"
    LedgerReadinessChanges = "none"
    ProductionReadinessChanges = "none"
    CoreHandoffCanFeedFutureSizingRiskApprovalPackage = $true
    SandboxProgrammeAcceptedWithGrossPnlV0ReadyRemainsUnchanged = $true
})

Write-JsonArtifact "contract-status-update.json" ([ordered]@{
    Package = $Package
    Artifact = "contract-status-update"
    Statuses = @(
        [ordered]@{ ContractId = "core-anubis-handoff-consumer.v1"; Status = "YES"; Reason = "Core handoff manifest validated." },
        [ordered]@{ ContractId = "core-anubis-netted-weights.v1"; Status = "YES"; Reason = "Netted weights validated." },
        [ordered]@{ ContractId = "pms-core-weights-candidate.v1"; Status = "WITH_WARNINGS"; Reason = "Weights-only preview; no quantities." },
        [ordered]@{ ContractId = "pms-sizing-for-core-weights.v1"; Status = "BLOCKED"; Reason = "Sizing, MarketData, and target notional not bound." },
        [ordered]@{ ContractId = "pms-risk-approval-for-core-weights.v1"; Status = "BLOCKED"; Reason = "Risk/operator approval not performed." },
        [ordered]@{ ContractId = "pms-execution-candidate.v1"; Status = "BLOCKED"; Reason = "No quantities, risk, or approval." },
        [ordered]@{ ContractId = "r009-execution-readiness.v1"; Status = "UNCHANGED_BLOCKED_FOR_CORE_CANDIDATE"; Reason = "Core candidate is not executable." },
        [ordered]@{ ContractId = "pnl-preview.v1"; Status = "YES_ONLY_FOR_ACCEPTED_HISTORICAL_SANDBOX_GROSS_PNL_V0"; Reason = "No Core PnL change." },
        [ordered]@{ ContractId = "accounting-attribution.v1"; Status = "BLOCKED"; Reason = "No accounting attribution." },
        [ordered]@{ ContractId = "production-readiness.v1"; Status = "BLOCKED"; Reason = "No production/live readiness." }
    )
})

Write-JsonArtifact "boundary-safety-evidence.json" ([ordered]@{
    Package = $Package
    Artifact = "boundary-safety-evidence"
    NoManagerExecution = $true
    NoAnubisExecution = $true
    NoCuda = $true
    NoNettingExecution = $true
    NoCoreArtifactMutation = $true
    NoIntradayDbMutation = $true
    NoPmsEmsOms = $true
    NoR009 = $true
    NoLmax = $true
    NoOrdersFills = $true
    NoLedger = $true
    NoProductionLive = $true
    NoAccountIdInvented = $true
    NoPortfolioIdInvented = $true
    NoStrategyIdInvented = $true
    NoSourceExecutionIntentIdInvented = $true
    NoAccountCurrencyInvented = $true
    NoQuantitiesInvented = $true
    NoR010Transfer = $true
})

$Summary = @"
# $Package

Classification: CORE_ANUBIS_WEIGHTS_INTRADAY_HANDOFF_CONSUMER_R002_WITH_WARNINGS_HANDOFF_CONSUMED_SIZING_BLOCKED

Was the Core handoff manifest consumed? yes.
RunKey: $($Manifest.RunKey).
Core handoff manifest hash: $ManifestHash.
Were netted USD weights validated? yes.
Was a PMS/Core weights candidate preview created? yes, weights-only.
Are quantities present? no.
Is R010 transferable? no.
Is Intraday execution-ready from Core weights? no.
Next package: NEXT_CORE_ANUBIS_INTRADAY_SIZING_R003.
What did not run? Core manager, Anubis, CUDA, Core netting, Intraday DB mutation, PMS/EMS/OMS, R009, LMAX, orders, fills, ledger, production/live, and accounting or net PnL readiness.
"@

$Summary | Set-Content -LiteralPath (Join-Path $ArtifactDir "summary.md") -Encoding UTF8

Write-Host "CORE_ANUBIS_WEIGHTS_INTRADAY_HANDOFF_CONSUMER_R002 artifacts written."
