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
    $env:LMAX_DEMO_TARGET_CLOSE_UTC = $effective.ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
    & $node $materializer
    if ($LASTEXITCODE -ne 0) { throw 'LMAX_DEMO_MATERIALIZATION_FAILED' }
    $tooling = Join-Path $candidate 'tooling'
    $toolRoot = Join-Path $tooling 'tools\anubis_gpu_benchmark'
    $runSmoke = Join-Path $toolRoot 'src\run-smoke.mjs'
    $hashManifest = Join-Path $toolRoot 'contracts\infx7-infx10-constituent-hashes-v1.json'
    $inventory = Join-Path $tooling 'validation\benchmark_bundle_source_inventory.json'
    $snapshot = Get-Content -LiteralPath (Join-Path $candidate 'evidence\materialization.json') -Raw | ConvertFrom-Json
    foreach ($programme in $selected) {
        $lower = $programme.ToLowerInvariant()
        $contract = Join-Path $toolRoot "contracts\lmax-demo-$($CycleId.ToLowerInvariant())-$lower.json"
        $runRoot = Join-Path $candidate "anubis-runs\$programme"
        $list = Join-Path $immutableRoot "inputs\home\data\$programme\specs\ticker.txt"
        & $node $runSmoke --no-order --execute --contract $contract --immutable-bundle-root $immutableRoot --tooling-root $tooling --run-root $runRoot --executable (Join-Path $immutableRoot 'bin\PRODAnubisV4.exe') --benchmark-bundle-root (Join-Path $immutableRoot 'inputs\home\data') --prod-bundle-root (Join-Path $immutableRoot 'inputs\home\prod') --list $list --hash-manifest $hashManifest --bundle-inventory-manifest $inventory --qubes-overlay-root (Join-Path $candidate 'qubes-overlay') --qubes-snapshot-sha256 $snapshot.qubes_snapshot_sha256
        if ($LASTEXITCODE -ne 0) { throw "LMAX_DEMO_ANUBIS_RUN_FAILED:$programme" }
        $weights = Join-Path $runRoot 'outputs\AggregatedWeights.txt'
        Require-File $weights "LMAX_DEMO_ANUBIS_OUTPUT_MISSING:$programme"
        $resultRoot = Join-Path $candidate "anubis-results\$programme"
        New-Item -ItemType Directory -Force -Path $resultRoot | Out-Null
        Copy-Item -LiteralPath $weights -Destination (Join-Path $resultRoot 'v1-aggregated-weights.txt')
        $resultManifest = [ordered]@{ schema = 'lmax_demo_anubis_result_v1'; cycle_id = $CycleId; cutoff_utc = $CutoffUtc; effective_at_utc = $effective.ToString('yyyy-MM-ddTHH:mm:ss.fffZ'); programme = $programme; aggregated_weights_sha256 = Get-Sha256 (Join-Path $resultRoot 'v1-aggregated-weights.txt'); no_order = $true; broker_send_status = 'DISABLED_NO_ORDER_ENTRY' }
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
