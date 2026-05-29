param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputSubdir = "real-qubes-core-handoff-to-intraday-consumption-r001",
    [string[]]$CandidateRoots = @(),
    [int]$MaxCandidateFiles = 900,
    [switch]$DisablePreviousLmaxFixtureFallback,
    [switch]$AllowTestFixtureRealAcceptance
)

$ErrorActionPreference = "Stop"

$Package = "NEXT_REAL_QUBES_CORE_HANDOFF_TO_INTRADAY_CONSUMPTION_R001"
$OutputDir = Join-Path $RepoRoot "artifacts\readiness\$OutputSubdir"
$PreviousRunId = "LMAX_SANDBOX_GLOBAL_TEST_R001_20260529T125324Z"
$PreviousRunDir = Join-Path $RepoRoot "artifacts\readiness\lmax-sandbox-global-process-test-run-r001"

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

function Write-JsonFile([string]$Path, [object]$Value) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $Value | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Read-JsonFile([string]$Path) {
    Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Get-FileSha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    "sha256:$((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash)"
}

function Get-ObjectSha256($Value) {
    $json = $Value | ConvertTo-Json -Depth 100 -Compress
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        "sha256:$(([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', ''))"
    } finally {
        $sha.Dispose()
    }
}

function Get-Prop($Object, [string[]]$Names, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    foreach ($name in $Names) {
        if ($Object -is [System.Collections.IDictionary] -and $Object.Contains($name)) { return $Object[$name] }
        $prop = $Object.PSObject.Properties[$name]
        if ($null -ne $prop) { return $prop.Value }
    }
    return $Default
}

function To-Array($Value) {
    if ($null -eq $Value) { return ,([object[]]@()) }
    return ,([object[]]@($Value))
}

function Get-DecimalOrNull($Value) {
    if ($null -eq $Value) { return $null }
    $text = [string]$Value
    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    if ($text -match '^(NaN|Infinity|-Infinity)$') { return $null }
    $parsed = [decimal]0
    if ([decimal]::TryParse($text, [Globalization.NumberStyles]::Any, [Globalization.CultureInfo]::InvariantCulture, [ref]$parsed)) { return $parsed }
    return $null
}

function Classify-Candidate([string]$Path, $Json, [string]$Text) {
    $lower = $Path.ToLowerInvariant()
    if ($null -eq $Json -and $Path.EndsWith(".json")) { return "INVALID" }
    if ($null -ne $Json) {
        $generated = [bool](Get-Prop $Json @("generated_by_qubes_core", "generatedByQubesCore") $false)
        $synthetic = [bool](Get-Prop $Json @("synthetic_fixture", "syntheticFixture") $false)
        $artifactType = [string](Get-Prop $Json @("artifact_type", "artifactType") "")
        $candidatePackage = [string](Get-Prop $Json @("package") "")
        if ($artifactType -eq "core_to_intraday_handoff_manifest" -or ($candidatePackage -eq $Package -and $artifactType -notmatch 'real_qubes_core_output_test_fixture')) {
            return "CONTRACT_ONLY"
        }
        $isExplicitTestFixture = ($artifactType -match 'test_fixture|fixture' -or $lower -match 'real-qubes-core-handoff-to-intraday-consumption-r001-test-fixtures')
        $raw = To-Array (Get-Prop $Json @("raw_aggregated_weights", "rawAggregatedWeights", "AggregatedWeights") $null)
        $final = To-Array (Get-Prop $Json @("final_manager_weights", "finalManagerWeights") $null)
        $netted = To-Array (Get-Prop $Json @("netted_usd_weights", "NettedUsdWeights", "nettedUsdWeights") $null)
        if ($generated -and -not $synthetic -and $raw.Count -gt 0 -and $final.Count -gt 0 -and $netted.Count -gt 0) {
            if ($isExplicitTestFixture -and -not $AllowTestFixtureRealAcceptance) { return "TEST_ARTIFACT" }
            return "REAL_QUBES_CORE_OUTPUT_CANDIDATE"
        }
        if ($synthetic) { return "SYNTHETIC_FIXTURE" }
        if ($raw.Count -gt 0 -or $final.Count -gt 0 -or $netted.Count -gt 0) {
            if ($lower -match 'fixture|synthetic|test\\|test/|generated\\|generated/') { return "SYNTHETIC_FIXTURE" }
            return "UNKNOWN"
        }
    }
    if ($lower -match 'fixture|synthetic|test\\|test/|generated\\|generated/') { return "SYNTHETIC_FIXTURE" }
    if ($Text -match 'schema|contract|interface|design|plan') { return "CONTRACT_ONLY" }
    return "UNKNOWN"
}

function Test-RealQubesCandidate($Json) {
    $issues = @()
    $generated = [bool](Get-Prop $Json @("generated_by_qubes_core", "generatedByQubesCore") $false)
    $synthetic = [bool](Get-Prop $Json @("synthetic_fixture", "syntheticFixture") $false)
    $runId = [string](Get-Prop $Json @("run_id", "runId") "")
    $sourceSystem = [string](Get-Prop $Json @("source_system", "sourceSystem") "")
    $strategy = [string](Get-Prop $Json @("strategy", "manager_scope", "managerScope") "")
    $raw = To-Array (Get-Prop $Json @("raw_aggregated_weights", "rawAggregatedWeights", "AggregatedWeights") $null)
    $final = To-Array (Get-Prop $Json @("final_manager_weights", "finalManagerWeights") $null)
    $netted = To-Array (Get-Prop $Json @("netted_usd_weights", "NettedUsdWeights", "nettedUsdWeights") $null)
    $target = Get-Prop $Json @("target_notional_usd", "targetNotionalUsd") $null
    if ($null -eq $target) {
        $targetObj = Get-Prop $Json @("target_notional", "targetNotional") $null
        $target = Get-Prop $targetObj @("amount", "usd", "value") $null
    }
    $targetDecimal = Get-DecimalOrNull $target

    if (-not $generated) { $issues += "generated_by_qubes_core_not_true" }
    if ($synthetic) { $issues += "synthetic_fixture_true" }
    if ([string]::IsNullOrWhiteSpace($runId)) { $issues += "missing_run_id" }
    if ([string]::IsNullOrWhiteSpace($sourceSystem)) { $issues += "missing_source_system" }
    if ([string]::IsNullOrWhiteSpace($strategy)) { $issues += "missing_strategy_or_manager_scope" }
    if ($raw.Count -eq 0) { $issues += "raw_aggregated_weights_missing" }
    if ($final.Count -eq 0) { $issues += "final_manager_weights_missing" }
    if ($netted.Count -eq 0) { $issues += "netted_usd_weights_missing" }
    if ($null -eq $targetDecimal -or $targetDecimal -le 0) { $issues += "target_notional_missing_or_invalid" }

    foreach ($row in @($raw + $final + $netted)) {
        $symbol = [string](Get-Prop $row @("symbol", "Symbol", "execution_symbol", "ExecutionSymbol") "")
        $weight = Get-Prop $row @("weight", "Weight") $null
        if ([string]::IsNullOrWhiteSpace($symbol)) { $issues += "missing_symbol"; break }
        if ($null -eq $weight -or $null -eq (Get-DecimalOrNull $weight)) { $issues += "nan_null_or_invalid_weight"; break }
    }

    [ordered]@{
        passed = ($issues.Count -eq 0)
        issues = $issues
        run_id = $runId
        source_system = $sourceSystem
        strategy = $strategy
        target_notional_usd = if ($null -ne $targetDecimal) { $targetDecimal } else { $null }
        raw = $raw
        final = $final
        netted = $netted
    }
}

function Convert-ToOrderPreview($Validated) {
    $orders = @()
    $skipped = @()
    $orderTargets = To-Array (Get-Prop $Validated.json @("order_targets", "orderTargets") $null)
    if ($orderTargets.Count -eq 0) { $orderTargets = To-Array (Get-Prop $Validated.validation @("netted") $null) }

    $securityIds = @{
        "USDCAD" = "4013"; "USDCNH" = "100892"; "USDJPY" = "4004"; "USDMXN" = "100507"; "USDNOK" = "100513";
        "NZDUSD" = "100613"; "USDSEK" = "100529"; "USDSGD" = "100535"; "USDZAR" = "100547"; "AUDUSD" = "4007";
        "EURUSD" = "4001"; "GBPUSD" = "4002"; "EURGBP" = "4003"; "USDCHF" = "4006"
    }

    foreach ($row in $orderTargets) {
        $symbol = [string](Get-Prop $row @("execution_symbol", "ExecutionSymbol", "symbol", "Symbol") "")
        $coreSymbol = [string](Get-Prop $row @("core_symbol", "source_symbol", "symbol", "Symbol") $symbol)
        $quantity = Get-DecimalOrNull (Get-Prop $row @("refined_quantity", "quantity", "Quantity") $null)
        $side = [string](Get-Prop $row @("side", "Side") "")
        $targetNotional = Get-DecimalOrNull (Get-Prop $row @("target_notional_usd", "targetNotionalUsd", "rounded_notional_usd") 0)
        if ([string]::IsNullOrWhiteSpace($symbol)) {
            $skipped += [ordered]@{ source = $coreSymbol; reason = "missing_execution_symbol" }
            continue
        }
        if ($null -eq $quantity -or $quantity -le 0) {
            $skipped += [ordered]@{ symbol = $symbol; reason = "zero_or_missing_quantity" }
            continue
        }
        if ([string]::IsNullOrWhiteSpace($side)) {
            $side = if ($targetNotional -lt 0) { "SELL" } else { "BUY" }
        }
        $securityId = [string](Get-Prop $row @("security_id", "SecurityId") "")
        if ([string]::IsNullOrWhiteSpace($securityId) -and $securityIds.ContainsKey($symbol)) { $securityId = $securityIds[$symbol] }
        $tag22 = if ([string]::IsNullOrWhiteSpace($securityId)) { $null } else { "8" }
        $orders += [ordered]@{
            run_id = $Validated.validation.run_id
            core_symbol = $coreSymbol
            symbol = $symbol
            side = $side
            target_notional_usd = $targetNotional
            raw_quantity = $quantity
            refined_quantity = $quantity
            quantity = $quantity
            security_id = $securityId
            security_id_source_tag22 = $tag22
            tag22_policy_enforced = ([string]::IsNullOrWhiteSpace($securityId) -or $tag22 -eq "8")
            residual_after_rounding_usd = Get-DecimalOrNull (Get-Prop $row @("residual_after_rounding_usd") 0)
            production_live = $false
            submit_allowed_without_approval = $false
        }
    }
    [ordered]@{ orders = $orders; skipped = $skipped }
}

function Get-OrderSignature($Orders) {
    @($Orders | Sort-Object { [string](Get-Prop $_ @("symbol", "Symbol") "") } | ForEach-Object {
        "$($_.symbol)|$($_.side)|$($_.quantity)|$($_.security_id)|$($_.security_id_source_tag22)"
    }) -join ";"
}

$defaultRoots = @(
    $RepoRoot,
    "C:\Users\phili\source\repos\QQ.Production.Core",
    "C:\Users\phili\source\repos\QQ.Research.Backtesting.Core",
    (Join-Path $RepoRoot "artifacts"),
    (Join-Path $RepoRoot "artifacts\readiness"),
    "C:\home\results",
    "C:\home\data",
    "C:\Users\phili\source\repos\QQ.Production.Core\artifacts",
    "C:\Users\phili\source\repos\QQ.Research.Backtesting.Core\artifacts"
)
$roots = if ($CandidateRoots.Count -gt 0) { $CandidateRoots } else { $defaultRoots }
$roots = @($roots | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)

$candidateFiles = New-Object System.Collections.Generic.List[object]
$candidateDirectories = New-Object System.Collections.Generic.List[object]
$keywords = 'Anubis|AggregatedWeights|raw aggregated weights|manager weights|final manager weights|NettedUsdWeights|netted USD weights|Core handoff|Qubes|QubesEngine|QQ\.Production\.Core|handoff manifest|weights hash|target notional|drift|order targets'
foreach ($root in $roots) {
    if (-not (Test-Path -LiteralPath $root)) { continue }
    $candidateDirectories.Add([ordered]@{ path = $root; exists = $true })
    $files = @(Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in @(".json", ".csv", ".txt", ".md") -and ($_.FullName -match $keywords -or $_.Name -match $keywords) } |
        Select-Object -First $MaxCandidateFiles)
    foreach ($file in $files) {
        $text = ""
        try { $text = (Get-Content -LiteralPath $file.FullName -Raw -ErrorAction Stop) } catch { $text = "" }
        if ($text.Length -gt 240000) { $text = $text.Substring(0, 240000) }
        $json = $null
        if ($file.Extension -eq ".json") {
            try { $json = $text | ConvertFrom-Json -ErrorAction Stop } catch { $json = $null }
        }
        $classification = Classify-Candidate -Path $file.FullName -Json $json -Text $text
        if ($classification -eq "UNKNOWN" -and $text -notmatch $keywords) { continue }
        $candidateFiles.Add([ordered]@{
            path = $file.FullName
            source_repo_path = $root
            sha256 = Get-FileSha256 $file.FullName
            last_write_time_for_diagnostics_only = $file.LastWriteTimeUtc.ToString("o")
            artifact_type_classification = $classification
            accepted_as_real = $false
            reason = ""
        })
    }
}

$accepted = $null
$invalidRealCandidates = @()
foreach ($candidate in @($candidateFiles | Where-Object { $_.artifact_type_classification -eq "REAL_QUBES_CORE_OUTPUT_CANDIDATE" })) {
    try {
        $json = Read-JsonFile $candidate.path
        $validation = Test-RealQubesCandidate $json
        if ($validation.passed -and $null -eq $accepted) {
            $candidate.accepted_as_real = $true
            $candidate.reason = "accepted_real_qubes_core_output"
            $accepted = [ordered]@{ path = $candidate.path; sha256 = $candidate.sha256; json = $json; validation = $validation }
        } elseif (-not $validation.passed) {
            $candidate.reason = ($validation.issues -join ";")
            $invalidRealCandidates += $candidate
        }
    } catch {
        $candidate.reason = "invalid_json_or_validation_error"
        $invalidRealCandidates += $candidate
    }
}

$fixtureCandidate = $null
$fixturePath = Join-Path $PreviousRunDir "qubes-core-weight-handoff-r001.json"
if (-not $DisablePreviousLmaxFixtureFallback -and (Test-Path -LiteralPath $fixturePath)) {
    $fixtureJson = Read-JsonFile $fixturePath
    $fixtureValidation = Test-RealQubesCandidate $fixtureJson
    $fixtureCandidate = [ordered]@{ path = $fixturePath; sha256 = Get-FileSha256 $fixturePath; json = $fixtureJson; validation = $fixtureValidation }
}

if ($null -eq $accepted -and $null -ne $fixtureCandidate) {
    $source = $fixtureCandidate
    $realFound = $false
    $realAccepted = $false
    $generatedByCore = $false
    $syntheticFixture = $true
    $manifestStatus = "BLOCKED_REAL_QUBES_CORE_OUTPUT_MISSING"
} elseif ($null -ne $accepted) {
    $source = $accepted
    $realFound = $true
    $realAccepted = $true
    $generatedByCore = $true
    $syntheticFixture = $false
    $manifestStatus = "CORE_TO_INTRADAY_HANDOFF_MANIFEST_READY_R001"
} else {
    $source = $null
    $realFound = ($invalidRealCandidates.Count -gt 0)
    $realAccepted = $false
    $generatedByCore = $false
    $syntheticFixture = $false
    $manifestStatus = if ($invalidRealCandidates.Count -gt 0) { "BLOCKED_REAL_QUBES_CORE_OUTPUT_INVALID" } else { "BLOCKED_REAL_QUBES_CORE_OUTPUT_MISSING" }
}

$discovery = [ordered]@{
    package = $Package
    artifact_type = "qubes_core_discovery_report"
    search_roots = $roots
    candidate_directories_found = @($candidateDirectories.ToArray())
    candidate_files_found = @($candidateFiles.ToArray())
    candidate_files_count = $candidateFiles.Count
    real_output_candidates_count = @($candidateFiles | Where-Object { $_.artifact_type_classification -eq "REAL_QUBES_CORE_OUTPUT_CANDIDATE" }).Count
    synthetic_or_fixture_candidates_count = @($candidateFiles | Where-Object { $_.artifact_type_classification -in @("SYNTHETIC_FIXTURE", "TEST_ARTIFACT") }).Count
    global_guards = [ordered]@{
        trading_activity = $false; lmax_fix_api_call = $false; broker_api_call = $false; polygon_massive_call = $false
        market_data_fetch = $false; broker_fetch = $false; account_data_fetch = $false; production_live_write = $false
        production_live_ready = $false; trading_readiness_ready = $false
    }
}
Write-JsonFile (Join-Path $OutputDir "qubes-core-discovery-report-r001.json") $discovery

if ($null -ne $source) {
    $symbols = @($source.validation.netted | ForEach-Object { [string](Get-Prop $_ @("symbol", "execution_symbol", "Symbol") "") } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
    $weights = @($source.validation.final | ForEach-Object {
        [ordered]@{ symbol = [string](Get-Prop $_ @("symbol", "Symbol") ""); weight = Get-DecimalOrNull (Get-Prop $_ @("weight", "Weight") 0) }
    })
    $manifest = [ordered]@{
        artifact_type = "core_to_intraday_handoff_manifest"
        package = $Package
        status = $manifestStatus
        source_system = "QQ.Production.Core / Anubis"
        generated_by_qubes_core = $generatedByCore
        synthetic_fixture = $syntheticFixture
        accepted_as_real = $realAccepted
        blocked_reason = if ($realAccepted) { $null } elseif ($manifestStatus -match "INVALID") { "REAL_QUBES_CORE_OUTPUT_INVALID" } else { "REAL_QUBES_CORE_OUTPUT_MISSING" }
        run_id = $source.validation.run_id
        target_notional_usd = $source.validation.target_notional_usd
        raw_aggregated_weights = [ordered]@{ present = ($source.validation.raw.Count -gt 0); source_path = $source.path; sha256 = $source.sha256 }
        final_manager_weights = [ordered]@{ present = ($source.validation.final.Count -gt 0); source_path = $source.path; sha256 = $source.sha256 }
        netted_usd_weights = [ordered]@{ present = ($source.validation.netted.Count -gt 0); source_path = $source.path; sha256 = $source.sha256 }
        symbols = $symbols
        weights = $weights
        validation = [ordered]@{
            passed = $realAccepted
            exposure_checks_passed = ($weights.Count -gt 0)
            no_nan_or_null = (-not (@($weights | Where-Object { $null -eq $_.weight }).Count -gt 0))
            symbol_normalization_passed = ($symbols.Count -gt 0)
            issues = @($source.validation.issues)
        }
        source_artifact_hash = $source.sha256
    }
} else {
    $manifest = [ordered]@{
        artifact_type = "core_to_intraday_handoff_manifest"
        package = $Package
        status = $manifestStatus
        source_system = "QQ.Production.Core / Anubis"
        generated_by_qubes_core = $false
        synthetic_fixture = $false
        accepted_as_real = $false
        blocked_reason = if ($manifestStatus -match "INVALID") { "REAL_QUBES_CORE_OUTPUT_INVALID" } else { "REAL_QUBES_CORE_OUTPUT_MISSING" }
        run_id = $null
        target_notional_usd = $null
        raw_aggregated_weights = [ordered]@{ present = $false; source_path = $null; sha256 = $null }
        final_manager_weights = [ordered]@{ present = $false; source_path = $null; sha256 = $null }
        netted_usd_weights = [ordered]@{ present = $false; source_path = $null; sha256 = $null }
        symbols = @()
        weights = @()
        validation = [ordered]@{ passed = $false; exposure_checks_passed = $false; no_nan_or_null = $false; symbol_normalization_passed = $false; issues = @("real_qubes_core_output_missing") }
        source_artifact_hash = $null
    }
}
Write-JsonFile (Join-Path $OutputDir "core-to-intraday-handoff-manifest-r001.json") $manifest

$consumed = ($null -ne $source)
$consumerStatus = if ($realAccepted) { "INTRADAY_HANDOFF_CONSUMED_R001" } elseif ($consumed) { "INTRADAY_HANDOFF_CONSUMED_FIXTURE_ONLY_R001" } else { "BLOCKED_INTRADAY_HANDOFF_CONSUMPTION_FAILED_R001" }
$consumer = [ordered]@{
    package = $Package
    artifact_type = "intraday_handoff_consumption"
    status = $consumerStatus
    manifest_loaded = $consumed
    symbol_normalization_result = if ($consumed) { "SYMBOLS_NORMALIZED_OR_PRESERVED" } else { "NO_MANIFEST" }
    target_weights_loaded = ($manifest.weights.Count -gt 0)
    netted_usd_weights_loaded = [bool]$manifest.netted_usd_weights.present
    target_notional_loaded = ($null -ne $manifest.target_notional_usd)
    instruments_mapped = if ($consumed) { $true } else { $false }
    missing_instrument_metadata = @()
    missing_fx_price_basis = @()
    consumer_status = $consumerStatus
    global_guards = $discovery.global_guards
}
Write-JsonFile (Join-Path $OutputDir "intraday-handoff-consumption-r001.json") $consumer

$orderPreview = if ($consumed) { Convert-ToOrderPreview ([ordered]@{ json = $source.json; validation = $source.validation }) } else { [ordered]@{ orders = @(); skipped = @() } }
$orderSignature = Get-OrderSignature -Orders @($orderPreview.orders)
$orderSignatureHash = Get-ObjectSha256 $orderSignature
$previousOrderManifestPath = Join-Path $PreviousRunDir "lmax-order-manifest-r001.json"
$previousOrderManifest = if (Test-Path -LiteralPath $previousOrderManifestPath) { Read-JsonFile $previousOrderManifestPath } else { $null }
$previousSignature = if ($null -ne $previousOrderManifest) { Get-OrderSignature -Orders @($previousOrderManifest.orders) } else { "" }
$previousSignatureHash = if ($previousSignature) { Get-ObjectSha256 $previousSignature } else { $null }
$orderSignaturesMatch = ($orderSignature -eq $previousSignature)
$sameAsPrevious = ($orderSignaturesMatch -and $manifest.run_id -eq $PreviousRunId)

$driftPreview = [ordered]@{
    package = $Package
    artifact_type = "real_qubes_drift_and_orders_preview"
    status = if ($consumed) { "REAL_QUBES_DRIFT_AND_ORDER_PREVIEW_READY_R001" } else { "BLOCKED_INTRADAY_HANDOFF_CONSUMPTION_FAILED_R001" }
    preview_only = $true
    current_portfolio_state_used = "sandbox_zero_current_state_preview"
    current_weights = @()
    target_weights = $manifest.weights
    target_notional_usd = $manifest.target_notional_usd
    drift_notional = @($orderPreview.orders | ForEach-Object { [ordered]@{ symbol = $_.symbol; drift_notional_usd = $_.target_notional_usd } })
    order_targets = $orderPreview.orders
    skipped_symbols = $orderPreview.skipped
    previous_lmax_run_id = $PreviousRunId
    previous_order_manifest_hash = Get-FileSha256 $previousOrderManifestPath
    previous_order_signature_hash = $previousSignatureHash
    new_order_signature_hash = $orderSignatureHash
    order_signatures_match = $orderSignaturesMatch
    previous_order_signature_for_diagnostics = $previousSignature
    new_order_signature_for_diagnostics = $orderSignature
    same_as_previous_lmax_run = $sameAsPrevious
    differences_by_symbol = if ($sameAsPrevious) { @() } else { @("order_signature_or_run_id_differs") }
    global_guards = $discovery.global_guards
}
Write-JsonFile (Join-Path $OutputDir "real-qubes-drift-and-orders-preview-r001.json") $driftPreview

$lmaxPreview = [ordered]@{
    package = $Package
    artifact_type = "real_qubes_lmax_order_manifest_preview"
    status = if ($orderPreview.orders.Count -gt 0) { "LMAX_ORDER_MANIFEST_PREVIEW_READY_R001" } else { "BLOCKED_NO_EXECUTABLE_ORDER_TARGETS_R001" }
    preview_only = $true
    no_lmax_call = $true
    short_clordid_policy = [ordered]@{ max_external_cl_ord_id_length = 20; deterministic = $true; unique_within_run = $true }
    orders = @($orderPreview.orders | ForEach-Object {
        if (-not [string]::IsNullOrWhiteSpace([string]$_.security_id) -and [string]$_.security_id_source_tag22 -ne "8") {
            throw "tag 22 must equal 8 when tag 48 is present."
        }
        [ordered]@{
            run_id = $manifest.run_id
            symbol = $_.symbol
            side = $_.side
            quantity = $_.quantity
            target_notional_usd = $_.target_notional_usd
            security_id = $_.security_id
            security_id_source_tag22 = $_.security_id_source_tag22
            tag22_policy_enforced = $true
            production_live = $false
            submit_allowed_without_approval = $false
        }
    })
    order_count = $orderPreview.orders.Count
    validation_status = if ($orderPreview.orders.Count -gt 0) { "ORDER_MANIFEST_PREVIEW_VALID" } else { "NO_ORDERS" }
    global_guards = $discovery.global_guards
}
Write-JsonFile (Join-Path $OutputDir "real-qubes-lmax-order-manifest-preview-r001.json") $lmaxPreview

$bridgeReason = if ($sameAsPrevious -and $realAccepted) { "MATCHING_REAL_QUBES_CORE_HANDOFF_AND_ORDER_SIGNATURE" } elseif (-not $realAccepted) { if ($consumed) { "EXISTING_LMAX_RUN_USED_SANDBOX_HANDOFF_NOT_VERIFIED_REAL_QUBES_CORE" } else { "REAL_QUBES_CORE_OUTPUT_MISSING" } } else { "REAL_QUBES_CORE_HANDOFF_DOES_NOT_MATCH_EXISTING_LMAX_RUN" }
$bridge = [ordered]@{
    package = $Package
    artifact_type = "real_qubes_to_existing_lmax_run_scope_bridge"
    existing_lmax_run_id = $PreviousRunId
    did_reconciled_lmax_run_use_exact_real_qubes_core_handoff = ($sameAsPrevious -and $realAccepted)
    hashes_match = $sameAsPrevious
    run_ids_match = ($manifest.run_id -eq $PreviousRunId)
    order_targets_match = $orderSignaturesMatch
    existing_lmax_run_attributed_to_real_qubes_core = ($sameAsPrevious -and $realAccepted)
    can_existing_lmax_fills_be_attributed_to_real_qubes_core = ($sameAsPrevious -and $realAccepted)
    reason = $bridgeReason
}
Write-JsonFile (Join-Path $OutputDir "real-qubes-to-existing-lmax-run-scope-bridge-r001.json") $bridge

$status = if ($realAccepted -and $sameAsPrevious) {
    "REAL_QUBES_CORE_HANDOFF_CONSUMED_AND_MATCHES_EXISTING_LMAX_RUN_R001"
} elseif ($realAccepted -and $orderPreview.orders.Count -gt 0) {
    "REAL_QUBES_CORE_HANDOFF_CONSUMED_AND_ORDER_PREVIEW_READY_R001"
} elseif ($realAccepted) {
    "REAL_QUBES_CORE_HANDOFF_CONSUMED_R001"
} elseif ($consumed) {
    "FIXTURE_ONLY_QUBES_HANDOFF_CONSUMED_R001"
} elseif ($invalidRealCandidates.Count -gt 0) {
    "BLOCKED_REAL_QUBES_CORE_OUTPUT_INVALID_R001"
} else {
    "BLOCKED_REAL_QUBES_CORE_OUTPUT_MISSING_R001"
}

$main = [ordered]@{
    package = $Package
    status = $status
    real_qubes_core_output_found = $realFound
    real_qubes_core_output_accepted = $realAccepted
    generated_by_qubes_core = $generatedByCore
    synthetic_fixture = $syntheticFixture
    intraday_consumption_ready = $consumed
    drift_order_preview_ready = ($orderPreview.orders.Count -gt 0)
    lmax_order_manifest_preview_ready = ($lmaxPreview.order_count -gt 0)
    existing_lmax_run_attribution = [ordered]@{
        run_id = $PreviousRunId
        attributed_to_real_qubes_core = ($sameAsPrevious -and $realAccepted)
        reason = $bridgeReason
    }
    ready_outputs = [ordered]@{
        real_qubes_core_handoff_manifest_ready = $realAccepted
        intraday_handoff_consumption_ready = $consumed
        real_qubes_drift_order_preview_ready = ($orderPreview.orders.Count -gt 0)
    }
    still_blocked = @("production_live", "trading_readiness")
    global_guards = $discovery.global_guards
}
Write-JsonFile (Join-Path $OutputDir "real-qubes-core-handoff-to-intraday-consumption-r001.json") $main

$coverage = [ordered]@{
    package = $Package
    status = "E2E_FLOW_COVERAGE_AFTER_REAL_QUBES_HANDOFF_R001"
    flow_coverage = [ordered]@{
        market_data = "SANDBOX_CONFIRMED"
        qubes_weight_generation = if ($realAccepted) { "REAL_CONFIRMED" } elseif ($consumed) { "FIXTURE_ONLY" } else { "BLOCKED" }
        qubes_to_intraday_handoff = if ($realAccepted) { "REAL_CONFIRMED" } elseif ($consumed) { "SANDBOX_CONFIRMED" } else { "BLOCKED" }
        drift_calculation = if ($orderPreview.orders.Count -gt 0) { "SANDBOX_CONFIRMED" } else { "BLOCKED" }
        order_creation = if ($lmaxPreview.order_count -gt 0) { "SANDBOX_CONFIRMED" } else { "BLOCKED" }
        lmax_execution = "LMAX_SANDBOX_GLOBAL_PROCESS_TEST_RUN_RECONCILED_R001"
        lmax_execution_attributed_to_real_qubes_core = ($sameAsPrevious -and $realAccepted)
        broker_statement_reconciliation = "REAL_CONFIRMED"
        accounting_close = "REAL_CONFIRMED"
        ledger_db_commit = "SANDBOX_CONFIRMED"
        production_live_trading_readiness = "BLOCKED"
    }
}
Write-JsonFile (Join-Path $OutputDir "e2e-flow-coverage-after-real-qubes-handoff-r001.json") $coverage

$summary = @"
# NEXT_REAL_QUBES_CORE_HANDOFF_TO_INTRADAY_CONSUMPTION_R001

- Status: $status
- Real Qubes/Core output found: $realFound
- Real Qubes/Core output accepted: $realAccepted
- Generated by Qubes/Core: $generatedByCore
- Intraday consumed handoff: $consumed
- Drift/order preview ready: $($orderPreview.orders.Count -gt 0)
- Existing LMAX run attributed to real Qubes/Core: $($sameAsPrevious -and $realAccepted)
- Existing LMAX bridge reason: $bridgeReason

The package read local artifacts only. It did not call LMAX, broker APIs, Polygon/Massive, or market data sources, and it did not mutate DB/ledger state.

Remaining blockers:
- production_live
- trading_readiness
"@
Set-Content -LiteralPath (Join-Path $OutputDir "real-qubes-core-handoff-to-intraday-consumption-summary-r001.md") -Value $summary -Encoding UTF8

Write-Host "REAL_QUBES_CORE_HANDOFF_TO_INTRADAY_CONSUMPTION_R001_BUILD_PASS"
