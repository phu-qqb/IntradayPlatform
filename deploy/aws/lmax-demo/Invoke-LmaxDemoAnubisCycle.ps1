[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9]{8}T[0-9]{6}Z$')]
    [string]$CycleId,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$')]
    [string]$CutoffUtc,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^INFX(7|8|9|10)(,INFX(7|8|9|10)){0,3}$')]
    [string]$Programmes,

    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$bucket = 'qq-fund-platform-anubis-benchmark-stage-761018894194-eu-west-2'
$immutableRoot = 'D:\anubis-benchmark\immutable\82d48f018efb9803c1f448964e3f77421edaba1b4b547a2e866807c83bf791b6'
$operatorRoot = 'D:\anubis-benchmark\operator\lmax-demo-orchestration'
$materializer = Join-Path $operatorRoot 'Materialize-LmaxDemoV1.mjs'
$materializerSha256 = '15737571bb990a31f629f85b4c219aad11c9e66123089b76714d163fdde2ca57'
$toolingCommit = '33c878837ec504cc1be790b5ad72db03c48ed6c0'
$tooling = Join-Path 'D:\anubis-benchmark\tools' $toolingCommit
$toolRoot = Join-Path $tooling 'tools\anubis_gpu_benchmark'
$runSmoke = Join-Path $toolRoot 'src\run-smoke.mjs'
$hashManifest = Join-Path $toolRoot 'contracts\infx7-infx10-constituent-hashes-v1.json'
$inventory = Join-Path $tooling 'validation\benchmark_bundle_source_inventory.json'
$toolingBlobs = @{
    'cli.mjs' = 'ec3f87a2e9149482ec5d740bf26e5f5dfe2ec8a5'
    'contracts.mjs' = 'd41363eea82de856ec6f483c3222c36875b8a3ac'
    'daily-session-cli.mjs' = '53d75241968d938f0fd5b39d187d1c86cbd1af8f'
    'daily-session-guards.mjs' = '479cb861a10a563d84fb4c004f0a8888910e1809'
    'daily-session.mjs' = 'ea0095f58c184dc7a70a0ad916e75457edf92135'
    'evaluate-semantic-result.mjs' = '024ef5e6ea3af7af27ed7208eb62dd5f493c917e'
    'gpu-baseline.mjs' = '044dbb5886c458968e6ff080c869812fd028c097'
    'market-data-gap-fill-cli.mjs' = '22f461392a77e3e2b8e5c84f8551e7d7e1a25ef8'
    'market-data-gap-fill.mjs' = '77840ecb97d4c6ec4713859273eae7027f436446'
    'r083-decision-slice.mjs' = '7104d023e42d9b85bf06ea2c7415b7a3bb628cfb'
    'run-smoke.mjs' = '7e99a9533d2853813f9dd219a1d34873419c7715'
    'runtime-isolation.mjs' = '2851402deeecb7154ee326aa0de9d36dae9b5f03'
    'validate-constituents.mjs' = '8dce5b1d9ad39d44cf5fc5a46ff26b73e657e452'
}
$node = 'C:\Program Files\nodejs\node.exe'
$programRules = @{
    INFX7 = @{ Zone = 'Eastern Standard Time'; Open = '09:00'; Exit = '14:45'; Frequency = 15 }
    INFX8 = @{ Zone = 'Eastern Standard Time'; Open = '09:00'; Exit = '14:45'; Frequency = 30 }
    INFX9 = @{ Zone = 'GMT Standard Time'; Open = '07:00'; Exit = '16:45'; Frequency = 15 }
    INFX10 = @{ Zone = 'GMT Standard Time'; Open = '07:00'; Exit = '16:45'; Frequency = 60 }
}

function Require-File([string]$Path, [string]$Code) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw $Code }
}

function Get-Sha256([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-PinnedTooling {
    foreach ($name in $toolingBlobs.Keys) {
        $file = Join-Path $toolRoot ('src\' + $name)
        Require-File $file "LMAX_DEMO_TOOLING_SOURCE_MISSING:$name"
        $bytes = [IO.File]::ReadAllBytes($file)
        [byte[]]$payload = [Text.Encoding]::UTF8.GetBytes(('blob ' + $bytes.Length + [char]0)) + $bytes
        $sha1 = [Security.Cryptography.SHA1]::Create()
        try { $blob = ([BitConverter]::ToString($sha1.ComputeHash($payload))).Replace('-', '').ToLowerInvariant() }
        finally { $sha1.Dispose() }
        if ($blob -ne $toolingBlobs[$name]) { throw "LMAX_DEMO_TOOLING_SOURCE_COMMIT_MISMATCH:$name" }
    }
}

function Test-ProgrammeEligibility([string]$Programme, [DateTimeOffset]$Cutoff) {
    $rule = $programRules[$Programme]
    if ($null -eq $rule) { throw 'LMAX_DEMO_PROGRAMME_INVALID' }
    $zone = [TimeZoneInfo]::FindSystemTimeZoneById([string]$rule.Zone)
    $local = [TimeZoneInfo]::ConvertTime($Cutoff, $zone)
    $open = [TimeSpan]::Parse([string]$rule.Open, [Globalization.CultureInfo]::InvariantCulture)
    $exit = [TimeSpan]::Parse([string]$rule.Exit, [Globalization.CultureInfo]::InvariantCulture)
    if ($local.TimeOfDay -lt $open -or $local.TimeOfDay -ge $exit -or ($local.Minute % [int]$rule.Frequency) -ne 0 -or $local.Second -ne 0) {
        throw "LMAX_DEMO_PROGRAMME_NOT_ADMISSIBLE:$Programme"
    }
}

[DateTimeOffset]$cutoff = [DateTimeOffset]::MinValue
if (-not [DateTimeOffset]::TryParseExact($CutoffUtc, 'yyyy-MM-ddTHH:mm:ssZ', [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::AssumeUniversal -bor [Globalization.DateTimeStyles]::AdjustToUniversal, [ref]$cutoff)) {
    throw 'LMAX_DEMO_CUTOFF_UTC_INVALID'
}
if ($cutoff.Offset -ne [TimeSpan]::Zero -or $CycleId -ne $cutoff.ToString('yyyyMMddTHHmmssZ')) { throw 'LMAX_DEMO_CYCLE_CUTOFF_MISMATCH' }
$selected = @($Programmes.Split(',') | Select-Object -Unique)
if ($selected.Count -eq 0) { throw 'LMAX_DEMO_PROGRAMMES_REQUIRED' }
foreach ($programme in $selected) { Test-ProgrammeEligibility $programme $cutoff }

Require-File $materializer 'LMAX_DEMO_MATERIALIZER_MISSING'
Require-File $node 'LMAX_DEMO_NODE_MISSING'
Require-File (Join-Path $immutableRoot 'bin\PRODAnubisV4.exe') 'LMAX_DEMO_ANUBIS_EXECUTABLE_MISSING'
if ((Get-Sha256 $materializer) -ne $materializerSha256) { throw 'LMAX_DEMO_MATERIALIZER_SOURCE_MISMATCH' }
Assert-PinnedTooling
Require-File $hashManifest 'LMAX_DEMO_CONSTITUENT_HASH_MANIFEST_MISSING'
Require-File $inventory 'LMAX_DEMO_BUNDLE_INVENTORY_MISSING'
if ($ValidateOnly) {
    [pscustomobject]@{ marker = 'LMAX_DEMO_ANUBIS_WRAPPER_VALIDATED'; cycle_id = $CycleId; cutoff_utc = $CutoffUtc; programmes = $selected; no_order = $true } | ConvertTo-Json -Compress
    exit 0
}

$effective = $cutoff.AddMinutes(15)
if ([DateTimeOffset]::UtcNow -ge $effective) { throw 'LMAX_DEMO_EXECUTION_DEADLINE_EXPIRED' }
$mutex = [Threading.Mutex]::new($false, 'Global\QQ-LMAX-Demo-Anubis-Cycle')
if (-not $mutex.WaitOne(0)) { throw 'LMAX_DEMO_ANUBIS_CYCLE_ALREADY_RUNNING' }
try {
    if (@(Get-Process -Name PRODAnubisV4 -ErrorAction SilentlyContinue).Count -ne 0) { throw 'LMAX_DEMO_ANUBIS_ENGINE_ALREADY_RUNNING' }
    $candidate = "D:\anubis-benchmark\demo-runtime\$CycleId"
    if (Test-Path -LiteralPath $candidate) { throw 'LMAX_DEMO_CYCLE_REPLAY_OR_UNRESOLVED' }
    $inputs = Join-Path $candidate 'inputs'
    New-Item -ItemType Directory -Force -Path $inputs | Out-Null
    $baseKey = "intraday/lmax-input-transfer/$CycleId"
    foreach ($name in @('lmax-bbo-boundary.json', 'lmax-bbo-boundary-evidence.json')) {
        & aws s3api get-object --bucket $bucket --key "$baseKey/$name" --region eu-west-2 (Join-Path $inputs $name) | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "LMAX_DEMO_INPUT_DOWNLOAD_FAILED:$name" }
    }
    $inputPath = Join-Path $inputs 'lmax-bbo-boundary.json'
    $evidencePath = Join-Path $inputs 'lmax-bbo-boundary-evidence.json'
    $input = Get-Content -LiteralPath $inputPath -Raw | ConvertFrom-Json
    $evidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json
    if ($input.contract_version -ne 'lmax_bbo_boundary_input_v1' -or $input.boundary_utc -ne ($cutoff.ToString('yyyy-MM-ddTHH:mm:ss.fffZ')) -or
        $input.capture.cycle_id -ne $CycleId -or $evidence.normalized_input_sha256 -ne (Get-Sha256 $inputPath)) {
        throw 'LMAX_DEMO_INPUT_LINEAGE_INVALID'
    }
    $env:LMAX_DEMO_CANDIDATE_ROOT = $candidate
    $env:LMAX_DEMO_CYCLE_ID = $CycleId
    # V1 observations and native calculation end at the source cutoff, not the execution deadline.
    $env:LMAX_DEMO_TARGET_CLOSE_UTC = $cutoff.ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
    & $node $materializer
    if ($LASTEXITCODE -ne 0) { throw 'LMAX_DEMO_MATERIALIZATION_FAILED' }
    $candidateToolRoot = Join-Path $candidate 'tooling\tools\anubis_gpu_benchmark'
    $snapshot = Get-Content -LiteralPath (Join-Path $candidate 'evidence\materialization.json') -Raw | ConvertFrom-Json
    if ($snapshot.boundary_utc -ne $input.boundary_utc -or $snapshot.normalized_input_sha256 -ne (Get-Sha256 $inputPath)) {
        throw 'LMAX_DEMO_MATERIALIZED_SOURCE_CUTOFF_MISMATCH'
    }
    # Generated contracts are separately hashed configuration, not files authenticated by the Git commit.
    $configRoot = Join-Path $toolRoot "contracts\lmax-demo-runtime\$CycleId"
    if (Test-Path -LiteralPath $configRoot) { throw 'LMAX_DEMO_RUNTIME_CONFIGURATION_ALREADY_EXISTS' }
    New-Item -ItemType Directory -Path $configRoot -Force | Out-Null
    foreach ($programme in $selected) {
        $lower = $programme.ToLowerInvariant()
        $contractName = "lmax-demo-$($CycleId.ToLowerInvariant())-$lower.json"
        $sourceContract = Join-Path $candidateToolRoot ('contracts\' + $contractName)
        Require-File $sourceContract "LMAX_DEMO_GENERATED_CONTRACT_MISSING:$programme"
        $contract = Join-Path $configRoot $contractName
        [IO.File]::Copy($sourceContract, $contract, $false)
        $currentContract = Get-Content -LiteralPath $contract -Raw | ConvertFrom-Json
        if ($currentContract.strategy -ne $programme -or $currentContract.current_input.boundary_utc -ne $input.boundary_utc -or
            $currentContract.current_input.normalized_input_sha256 -ne (Get-Sha256 $inputPath) -or
            $currentContract.current_input.qubes_snapshot_sha256 -ne $snapshot.qubes_snapshot_sha256 -or
            $currentContract.argument_template[13] -ne $cutoff.ToString('yyyyMMddHHmm')) {
            throw "LMAX_DEMO_NATIVE_CONTRACT_SOURCE_CUTOFF_MISMATCH:$programme"
        }
        $runRoot = Join-Path 'D:\anubis-benchmark\runs' "lmax-demo-$CycleId-$programme"
        $list = Join-Path $immutableRoot "inputs\home\data\$programme\specs\listOfSUs.txt"
        if ([DateTimeOffset]::UtcNow -ge $effective) { throw 'LMAX_DEMO_EXECUTION_DEADLINE_EXPIRED' }
        & $node $runSmoke --no-order --execute --contract $contract --immutable-bundle-root $immutableRoot --tooling-root $tooling --run-root $runRoot --executable (Join-Path $immutableRoot 'bin\PRODAnubisV4.exe') --benchmark-bundle-root (Join-Path $immutableRoot 'inputs\home\data') --prod-bundle-root (Join-Path $immutableRoot 'inputs\home\prod') --list $list --hash-manifest $hashManifest --bundle-inventory-manifest $inventory --qubes-overlay-root (Join-Path $candidate 'qubes-overlay') --qubes-snapshot-sha256 $snapshot.qubes_snapshot_sha256
        if ($LASTEXITCODE -ne 0) { throw "LMAX_DEMO_ANUBIS_RUN_FAILED:$programme" }
        if ([DateTimeOffset]::UtcNow -ge $effective) { throw 'LMAX_DEMO_EXECUTION_DEADLINE_EXPIRED' }
        $weights = Join-Path $runRoot 'outputs\AggregatedWeights.txt'
        Require-File $weights "LMAX_DEMO_ANUBIS_OUTPUT_MISSING:$programme"
        $resultRoot = Join-Path $candidate "anubis-results\$programme"
        New-Item -ItemType Directory -Force -Path $resultRoot | Out-Null
        Copy-Item -LiteralPath $weights -Destination (Join-Path $resultRoot 'v1-aggregated-weights.txt')
        $resultManifest = [ordered]@{ schema = 'lmax_demo_anubis_result_v1'; cycle_id = $CycleId; cutoff_utc = $CutoffUtc; effective_at_utc = $effective.ToString('yyyy-MM-ddTHH:mm:ss.fffZ'); programme = $programme; aggregated_weights_sha256 = Get-Sha256 (Join-Path $resultRoot 'v1-aggregated-weights.txt'); no_order = $true; broker_send_status = 'DISABLED_NO_ORDER_ENTRY' }
        $resultManifest.tooling_commit = $toolingCommit
        $resultManifest.materializer_sha256 = $materializerSha256
        $resultManifest.generated_contract_sha256 = Get-Sha256 $contract
        $resultManifest.engine_asof_utc = $snapshot.boundary_utc
        $resultManifest.qubes_snapshot_sha256 = $snapshot.qubes_snapshot_sha256
        $resultManifest.run_root = $runRoot
        $manifestPath = Join-Path $resultRoot 'v1-result-manifest.json'
        [IO.File]::WriteAllText($manifestPath, ($resultManifest | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
        foreach ($file in @('v1-aggregated-weights.txt', 'v1-result-manifest.json')) {
            & aws s3api put-object --bucket $bucket --key "$baseKey/anubis-results/$programme/$file" --body (Join-Path $resultRoot $file) --region eu-west-2 | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "LMAX_DEMO_RESULT_UPLOAD_FAILED:${programme}:$file" }
        }
    }
    [pscustomobject]@{ marker = 'LMAX_DEMO_ANUBIS_CYCLE_COMPLETED'; cycle_id = $CycleId; cutoff_utc = $CutoffUtc; effective_at_utc = $effective.ToString('yyyy-MM-ddTHH:mm:ss.fffZ'); programmes = $selected; no_order = $true } | ConvertTo-Json -Compress
}
finally {
    if ($null -ne $mutex) { $mutex.ReleaseMutex() | Out-Null; $mutex.Dispose() }
}
