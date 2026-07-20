param(
    [Parameter(Mandatory = $true)][string]$LineagePath,
    [Parameter(Mandatory = $true)][string]$StaticMappingPath,
    [Parameter(Mandatory = $true)][string]$SecurityExportPath,
    [Parameter(Mandatory = $true)][string]$LmaxCatalogPath,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)

$ErrorActionPreference = "Stop"

function Get-Sha256Text([string]$Text) {
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        return ([System.BitConverter]::ToString($algorithm.ComputeHash($bytes))).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Join-Canonical([object[]]$Values) {
    return (($Values | ForEach-Object { if ($null -eq $_) { "-" } else { [string]$_ } }) -join "|")
}

function Get-LegCanonical($Leg) {
    if ($null -eq $Leg) { return "-" }
    return Join-Canonical @($Leg.instrument_id, $Leg.instrument_name, $Leg.orientation)
}

function Write-Utf8NoBom([string]$Path, [string]$Content) {
    [System.IO.File]::WriteAllText($Path, $Content + "`n", [System.Text.UTF8Encoding]::new($false))
}

$mappingVersion = "qubes_security_id_to_lmax_market_instrument_mapping_v1"
$coverageVersion = "qubes_to_lmax_mapping_coverage_v1"
$planVersion = "lmax_market_data_subscription_plan_v1"
$authority = "QUBES_IDENTITY_AND_LMAX_REFERENCE_CROSS_VALIDATED"
$identityMatch = "EXACT_SECURITY_ID_AND_CANONICAL_SYMBOL"
$permissionScope = "ARCH6A_LMAX_DEMO_MARKET_DATA_ONLY_SCOPE"

$sourceDefinitions = @(
    [pscustomobject]@{
        source_contract = "ARCH5D1V_FRESH_LINEAGE_MANIFEST"
        logical_name = "core/arch5d1v/fresh_lineage_manifest.json"
        sha256 = (Get-FileHash -LiteralPath $LineagePath -Algorithm SHA256).Hash.ToLowerInvariant()
        authority_classification = "QUBES_TARGET_WEIGHT_OCCURRENCE_AUTHORITY"
    },
    [pscustomobject]@{
        source_contract = "QUBES_STATIC_MAPPING"
        logical_name = "qubes/home/data/static_mapping.txt"
        sha256 = (Get-FileHash -LiteralPath $StaticMappingPath -Algorithm SHA256).Hash.ToLowerInvariant()
        authority_classification = "QUBES_STATIC_IDENTITY_CROSS_CHECK"
    },
    [pscustomobject]@{
        source_contract = "QUBES_CORE_SECURITY_EXPORT"
        logical_name = "qubes/identity/full_core_Security.csv"
        sha256 = (Get-FileHash -LiteralPath $SecurityExportPath -Algorithm SHA256).Hash.ToLowerInvariant()
        authority_classification = "QUBES_CANONICAL_SECURITY_IDENTITY_AUTHORITY"
    },
    [pscustomobject]@{
        source_contract = "LMAX_INSTRUMENT_REFERENCE_20260528"
        logical_name = "lmax/operator-evidence/LMAX-Instruments-operator-20260528.csv"
        sha256 = (Get-FileHash -LiteralPath $LmaxCatalogPath -Algorithm SHA256).Hash.ToLowerInvariant()
        authority_classification = "LMAX_MARKET_INSTRUMENT_REFERENCE_AUTHORITY"
    }
)

$lineage = Get-Content -LiteralPath $LineagePath -Raw | ConvertFrom-Json
$occurrences = @($lineage.runs | ForEach-Object { $_.target_close_weights } | ForEach-Object { [string]$_.security_id })
$requiredIds = @($occurrences | Sort-Object @{ Expression = { [int]$_ } }, @{ Expression = { $_ } } -Unique)

$staticMapping = @{}
Get-Content -LiteralPath $StaticMappingPath | ForEach-Object {
    $parts = $_ -split "`t", 2
    $staticMapping[[string]$parts[0]] = ([string]$parts[1] -replace "\s+Curncy$", "").ToUpperInvariant()
}

$securityIdentity = @{}
Import-Csv -LiteralPath $SecurityExportPath | ForEach-Object {
    $securityIdentity[[string]$_.SecurityId] = ([string]$_.Symbol).Trim().ToUpperInvariant()
}

$lmaxByPair = @{}
Import-Csv -LiteralPath $LmaxCatalogPath |
    Where-Object { $_.'LMAX symbol ' -match "^[A-Z]{3}/[A-Z]{3}$" } |
    ForEach-Object {
        $pair = ([string]$_.'LMAX symbol ').Replace("/", "").ToUpperInvariant()
        if ($lmaxByPair.ContainsKey($pair)) { throw "Duplicate LMAX pair $pair" }
        $lmaxByPair[$pair] = $_
    }

$sourceContracts = @($sourceDefinitions.source_contract | Sort-Object)
$sourceHashes = @($sourceDefinitions.sha256 | Sort-Object)
$entries = @()

foreach ($id in $requiredIds) {
    if (-not $securityIdentity.ContainsKey($id)) { throw "Missing Security identity $id" }
    if (-not $staticMapping.ContainsKey($id)) { throw "Missing static identity $id" }
    $pair = $securityIdentity[$id]
    if ($pair -notmatch "^[A-Z]{6}$") { throw "Non-FX Security identity ${id}:$pair" }

    $staticPair = $staticMapping[$id]
    if ($staticPair -ne $pair -and -not ($id -eq "278" -and $staticPair -eq "PLNHUF CUNRCY" -and $pair -eq "PLNHUF")) {
        throw "Conflicting Qubes identities ${id}:${staticPair}:${pair}"
    }

    $base = $pair.Substring(0, 3)
    $quote = $pair.Substring(3, 3)
    $directId = $null
    $directName = $null
    $directOrientation = $null
    $leg1 = $null
    $leg2 = $null
    $formula = $null

    if ($lmaxByPair.ContainsKey($pair)) {
        $row = $lmaxByPair[$pair]
        $mode = "DIRECT_LMAX_INSTRUMENT"
        $directId = [string]$row.'LMAX ID'
        $directName = [string]$row.'LMAX symbol '
        $directOrientation = "DIRECT_BASE_QUOTE"
    }
    elseif ($lmaxByPair.ContainsKey($quote + $base)) {
        $row = $lmaxByPair[$quote + $base]
        $mode = "DIRECT_LMAX_INSTRUMENT"
        $directId = [string]$row.'LMAX ID'
        $directName = [string]$row.'LMAX symbol '
        $directOrientation = "INVERTED_QUOTE_BASE"
    }
    else {
        $mode = "LMAX_USD_LEG_RECONSTRUCTION"
        $legs = @()
        foreach ($currency in @($base, $quote)) {
            if ($lmaxByPair.ContainsKey($currency + "USD")) {
                $row = $lmaxByPair[$currency + "USD"]
                $legs += [pscustomobject]@{
                    instrument_id = [string]$row.'LMAX ID'
                    instrument_name = [string]$row.'LMAX symbol '
                    orientation = "DIRECT_TO_USD"
                    expression = "MID($currency/USD)"
                }
            }
            elseif ($lmaxByPair.ContainsKey("USD" + $currency)) {
                $row = $lmaxByPair["USD" + $currency]
                $legs += [pscustomobject]@{
                    instrument_id = [string]$row.'LMAX ID'
                    instrument_name = [string]$row.'LMAX symbol '
                    orientation = "INVERTED_TO_USD"
                    expression = "1 / MID(USD/$currency)"
                }
            }
            else {
                throw "Missing LMAX USD leg ${id}:$currency"
            }
        }
        $leg1 = [pscustomobject]@{
            instrument_id = $legs[0].instrument_id
            instrument_name = $legs[0].instrument_name
            orientation = $legs[0].orientation
        }
        $leg2 = [pscustomobject]@{
            instrument_id = $legs[1].instrument_id
            instrument_name = $legs[1].instrument_name
            orientation = $legs[1].orientation
        }
        $formula = "($($legs[0].expression)) / ($($legs[1].expression))"
    }

    $entries += [pscustomobject]@{
        qubes_security_id = $id
        qubes_instrument_key = "QUBES_SECURITY_ID:$id"
        canonical_pair_or_symbol = $pair
        quote_currency = $quote
        base_currency = $base
        mapping_mode = $mode
        lmax_direct_instrument_id = $directId
        lmax_direct_instrument_name = $directName
        lmax_direct_orientation = $directOrientation
        lmax_leg_1 = $leg1
        lmax_leg_2 = $leg2
        reconstruction_formula = $formula
        source_contracts = $sourceContracts
        source_artifact_sha256 = $sourceHashes
        identity_match_method = $identityMatch
        authority_classification = $authority
        validation_status = "VALID"
    }
}

$mappingLines = [System.Collections.Generic.List[string]]::new()
$mappingLines.Add($mappingVersion)
foreach ($source in ($sourceDefinitions | Sort-Object source_contract)) {
    $mappingLines.Add((Join-Canonical @($source.source_contract, $source.logical_name, $source.sha256, $source.authority_classification)))
}
foreach ($id in ($occurrences | Sort-Object @{ Expression = { [int]$_ } }, @{ Expression = { $_ } })) {
    $mappingLines.Add("O|$id")
}
foreach ($entry in ($entries | Sort-Object @{ Expression = { [int]$_.qubes_security_id } }, qubes_security_id)) {
    $mappingLines.Add((Join-Canonical @(
        "E",
        $entry.qubes_security_id,
        $entry.qubes_instrument_key,
        $entry.canonical_pair_or_symbol,
        $entry.base_currency,
        $entry.quote_currency,
        $entry.mapping_mode,
        $entry.lmax_direct_instrument_id,
        $entry.lmax_direct_instrument_name,
        $entry.lmax_direct_orientation,
        (Get-LegCanonical $entry.lmax_leg_1),
        (Get-LegCanonical $entry.lmax_leg_2),
        $entry.reconstruction_formula,
        (($entry.source_contracts | Sort-Object) -join ","),
        (($entry.source_artifact_sha256 | Sort-Object) -join ","),
        $entry.identity_match_method,
        $entry.authority_classification,
        $entry.validation_status
    )))
}
$mappingSha = Get-Sha256Text ($mappingLines -join "`n")

$mapping = [ordered]@{
    contract_version = $mappingVersion
    mapping_sha256 = $mappingSha
    required_security_id_occurrences = $occurrences
    sources = $sourceDefinitions
    entries = $entries
}

$references = @()
foreach ($entry in $entries) {
    if ($entry.mapping_mode -eq "DIRECT_LMAX_INSTRUMENT") {
        $references += [pscustomobject]@{ id = $entry.lmax_direct_instrument_id; name = $entry.lmax_direct_instrument_name; qubes_id = $entry.qubes_security_id }
    }
    else {
        $references += [pscustomobject]@{ id = $entry.lmax_leg_1.instrument_id; name = $entry.lmax_leg_1.instrument_name; qubes_id = $entry.qubes_security_id }
        $references += [pscustomobject]@{ id = $entry.lmax_leg_2.instrument_id; name = $entry.lmax_leg_2.instrument_name; qubes_id = $entry.qubes_security_id }
    }
}

$subscriptions = @(
    $references |
        Group-Object id |
        ForEach-Object {
            $names = @($_.Group.name | Sort-Object -Unique)
            if ($names.Count -ne 1) { throw "Ambiguous LMAX instrument identity $($_.Name)" }
            [pscustomobject]@{
                instrument_id = [string]$_.Name
                instrument_name = [string]$names[0]
                required_by_qubes_security_ids = @($_.Group.qubes_id | Sort-Object @{ Expression = { [int]$_ } }, @{ Expression = { $_ } } -Unique)
            }
        } |
        Sort-Object @{ Expression = { [int]$_.instrument_id } }, instrument_id
)

$planLines = [System.Collections.Generic.List[string]]::new()
$planLines.Add($planVersion)
$planLines.Add($mappingSha)
$planLines.Add([string]$references.Count)
$planLines.Add([string]$subscriptions.Count)
$planLines.Add([string]($references.Count - $subscriptions.Count))
foreach ($subscription in $subscriptions) {
    $planLines.Add((Join-Canonical @(
        $subscription.instrument_id,
        $subscription.instrument_name,
        (($subscription.required_by_qubes_security_ids | Sort-Object @{ Expression = { [int]$_ } }, @{ Expression = { $_ } }) -join ",")
    )))
}
$planSha = Get-Sha256Text ($planLines -join "`n")

$plan = [ordered]@{
    contract_version = $planVersion
    mapping_sha256 = $mappingSha
    subscription_plan_sha256 = $planSha
    requested_instrument_reference_count = $references.Count
    unique_subscription_count = $subscriptions.Count
    duplicate_subscription_count = $references.Count - $subscriptions.Count
    subscriptions = $subscriptions
}

$catalogInstruments = @(
    $subscriptions | ForEach-Object {
        [ordered]@{
            symbol = $_.instrument_name.Replace("/", "")
            security_id = $_.instrument_id
            security_id_source = "8"
            lmax_slash_symbol = $_.instrument_name
            evidence_source = "lmax/operator-evidence/LMAX-Instruments-operator-20260528.csv#sha256=$($sourceDefinitions[3].sha256)"
            permission_scope = $permissionScope
        }
    }
)
$catalog = [ordered]@{
    schema_version = "lmax-market-data-only-approved-instrument-catalog.v1"
    source = "arch6a_qubes_security_id_to_lmax_market_instrument_mapping.v1.json"
    source_mapping_sha256 = $mappingSha
    source_subscription_plan_sha256 = $planSha
    permission_scope = $permissionScope
    instruments = $catalogInstruments
}

$captureTemplate = [ordered]@{
    mode = "CAPTURE_ONLY"
    environment = "DEMO"
    venue = "LMAX_DEMO_READ_ONLY"
    market_data_endpoint_alias = "__MARKET_DATA_ENDPOINT_ALIAS__"
    market_data_session_alias = "LMAX_DEMO_MD_READ_ONLY"
    market_data_credential_reference = "__MARKET_DATA_CREDENTIAL_REFERENCE__"
    credential_scope = "MARKET_DATA_ONLY"
    instruments = @($catalogInstruments.symbol)
    output_root = "__RECORDER_ROOT__"
    max_duration_seconds = 300
    max_events = 100000
    max_total_bytes = 1073741824
    minimum_free_disk_bytes = 10737418240
    quote_age_threshold_ms = 1000
    rotate_after_bytes = 16777216
    flush_interval_ms = 1000
    allowed_outbound_fix_msg_types = @("A", "0", "1", "2", "4", "5", "V")
    tool_commit = "7e87f3b17c84ac8a0aeb79422e4caa97b915fbb6"
    config_hash = "__CONFIG_HASH__"
}

$coverage = [ordered]@{
    contract_version = $coverageVersion
    required_occurrences = $occurrences.Count
    required_unique_security_ids = $requiredIds.Count
    direct_mapping_count = @($entries | Where-Object mapping_mode -eq "DIRECT_LMAX_INSTRUMENT").Count
    usd_leg_reconstruction_count = @($entries | Where-Object mapping_mode -eq "LMAX_USD_LEG_RECONSTRUCTION").Count
    mapped_occurrences = $occurrences.Count
    mapped_unique_security_ids = $entries.Count
    missing_security_ids = @()
    ambiguous_security_ids = @()
    duplicate_security_ids = @()
    unavailable_lmax_legs = @()
    coverage_percent = 100
    final_success = $true
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
Write-Utf8NoBom (Join-Path $OutputDirectory "arch6a_qubes_security_id_to_lmax_market_instrument_mapping.v1.json") ($mapping | ConvertTo-Json -Depth 12)
Write-Utf8NoBom (Join-Path $OutputDirectory "arch6a_lmax_subscription_plan.v1.json") ($plan | ConvertTo-Json -Depth 12)
Write-Utf8NoBom (Join-Path $OutputDirectory "arch6a_qubes_to_lmax_mapping_coverage.v1.json") ($coverage | ConvertTo-Json -Depth 8)
Write-Utf8NoBom (Join-Path $OutputDirectory "lmax_demo_market_data_instrument_catalog.json") ($catalog | ConvertTo-Json -Depth 8)
Write-Utf8NoBom (Join-Path $OutputDirectory "m2c1b_aws_capture_config.template.json") ($captureTemplate | ConvertTo-Json -Depth 8)

[pscustomobject]@{
    mapping_sha256 = $mappingSha
    subscription_plan_sha256 = $planSha
    required_occurrences = $occurrences.Count
    required_unique_security_ids = $requiredIds.Count
    direct_mapping_count = @($entries | Where-Object mapping_mode -eq "DIRECT_LMAX_INSTRUMENT").Count
    usd_leg_reconstruction_count = @($entries | Where-Object mapping_mode -eq "LMAX_USD_LEG_RECONSTRUCTION").Count
    requested_instrument_references = $references.Count
    unique_subscriptions = $subscriptions.Count
} | ConvertTo-Json
