param(
    [string]$RepoRoot = (Resolve-Path ".").Path,
    [string]$OutputPath = "artifacts\readiness\qq-fund-platform-aws2h-s3-archive-uploader-manifest-schema-compat-fix-no-upload\AWS2H_CHUNK_UPLOAD_TEST_REPORT.generated.json"
)

$ErrorActionPreference = "Stop"
$repo = [System.IO.Path]::GetFullPath($RepoRoot)
$uploadScript = Join-Path $repo "deploy\aws\anubis-shadow\scripts\Invoke-AnubisAws1ChunkUpload.ps1"
$tmpRoot = Join-Path $repo ("artifacts\tmp\aws2h-chunk-upload-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $tmpRoot | Out-Null

function New-Result {
    param([string]$Name, [bool]$Pass, [object]$Detail)
    [ordered]@{ name = $Name; status = $(if ($Pass) { "PASS" } else { "FAIL" }); detail = $Detail }
}

function Write-Json {
    param([string]$Path, [object]$Value)
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $Value | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Get-Sha256Hex {
    param([string]$Path)
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
}

function Convert-HexSha256ToBase64 {
    param([string]$Hex)
    $bytes = [byte[]]::new(32)
    for ($i = 0; $i -lt $Hex.Length; $i += 2) { $bytes[[int]($i / 2)] = [Convert]::ToByte($Hex.Substring($i, 2), 16) }
    return [Convert]::ToBase64String($bytes)
}

function Write-FakeAwsCli {
    param([string]$Path)
    $fake = @"
`$Rest = `$args
`$ErrorActionPreference = "Stop"
`$statePath = `$env:FAKE_AWS_STATE
if ([string]::IsNullOrWhiteSpace(`$statePath)) { Write-Error "FAKE_AWS_STATE_missing"; exit 2 }

function Read-State {
    if (Test-Path -LiteralPath `$statePath) { return Get-Content -LiteralPath `$statePath -Raw | ConvertFrom-Json }
    return [pscustomobject]@{ objects = [pscustomobject]@{}; puts = @() }
}
function Write-State(`$State) { `$State | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath `$statePath -Encoding UTF8 }
function Get-ArgValue([string[]]`$Items, [string]`$Name) {
    for (`$i = 0; `$i -lt `$Items.Count - 1; `$i++) { if (`$Items[`$i] -eq `$Name) { return `$Items[`$i + 1] } }
    return ""
}
function Convert-ObjectsToHashtable(`$Objects) {
    `$map = @{}
    if (`$null -ne `$Objects) {
        foreach (`$prop in `$Objects.PSObject.Properties) { `$map[`$prop.Name] = [string]`$prop.Value }
    }
    return `$map
}

if (`$Rest.Count -eq 1 -and `$Rest[0] -eq "--version") { Write-Output "aws-cli/fixture Python/fixture"; exit 0 }
if (`$Rest.Count -lt 2 -or `$Rest[0] -ne "s3api") { Write-Error "unsupported_fake_aws_args:`$(`$Rest -join ' ')"; exit 2 }

`$operation = `$Rest[1]
`$key = Get-ArgValue -Items `$Rest -Name "--key"
`$state = Read-State
`$objects = Convert-ObjectsToHashtable `$state.objects
`$puts = @(`$state.puts)

if (`$operation -eq "head-object") {
    if (`$objects.ContainsKey(`$key)) { Write-Output `$objects[`$key]; exit 0 }
    Write-Error "An error occurred (404) when calling the HeadObject operation: Not Found"
    exit 254
}

if (`$operation -eq "put-object") {
    `$checksum = Get-ArgValue -Items `$Rest -Name "--checksum-sha256"
    if ([string]::IsNullOrWhiteSpace(`$checksum)) { Write-Error "checksum_missing"; exit 2 }
    `$objects[`$key] = `$checksum
    `$puts += `$key
    Write-State ([ordered]@{ objects = `$objects; puts = `$puts })
    Write-Output "{}"
    exit 0
}

Write-Error "unsupported_fake_s3api_operation:`$operation"
exit 2
"@
    Set-Content -LiteralPath $Path -Value $fake -Encoding UTF8
}

function New-UploadFixture {
    param(
        [string]$CaseRoot,
        [string]$RunId,
        [string]$SizeMode = "missing",
        [string]$ShaMode = "missing",
        [string]$RelativePath = "chunks/events-000001.jsonl",
        [bool]$WriteChunk = $true,
        [bool]$Finalized = $true,
        [int]$WriterErrors = 0,
        [int]$EventsDropped = 0,
        [int]$SequenceGapCount = 0,
        [string]$RemoteMode = "none"
    )

    $recorderRoot = Join-Path $CaseRoot "recorder"
    $runRoot = Join-Path $recorderRoot "environment=DEMO\date=2026-06-26\recorder_run=$RunId"
    $chunkDir = Join-Path $runRoot "chunks"
    $healthDir = Join-Path $runRoot "health"
    New-Item -ItemType Directory -Force -Path $chunkDir, $healthDir | Out-Null

    $chunkPath = Join-Path $runRoot ($RelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
    if ($WriteChunk -and $RelativePath -notmatch "\.\.") {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $chunkPath) | Out-Null
        Set-Content -LiteralPath $chunkPath -Value "{`"event_type`":`"BBO_UPDATED`",`"bid`":1.1,`"ask`":1.2}" -Encoding UTF8
    }

    $actualSize = if ($WriteChunk -and (Test-Path -LiteralPath $chunkPath)) { (Get-Item -LiteralPath $chunkPath).Length } else { 0 }
    $actualSha = if ($WriteChunk -and (Test-Path -LiteralPath $chunkPath)) { Get-Sha256Hex -Path $chunkPath } else { "" }
    $chunk = [ordered]@{ file = $RelativePath }
    if ($SizeMode -eq "matching") { $chunk.size_bytes = $actualSize }
    elseif ($SizeMode -eq "mismatch") { $chunk.size_bytes = ($actualSize + 1) }
    elseif ($SizeMode -eq "empty") { $chunk.size_bytes = "" }
    if ($ShaMode -eq "matching") { $chunk.sha256 = $actualSha }
    elseif ($ShaMode -eq "mismatch") { $chunk.sha256 = ("0" * 64) }

    $finalPath = Join-Path $runRoot "final_manifest.json"
    Write-Json -Path $finalPath -Value ([ordered]@{
        recorder_manifest_version = "canonical_recorder_manifest_v2"
        recorder_run_id = $RunId
        finalized = $Finalized
        environment = "DEMO"
        start_utc = "2026-06-26T00:00:00Z"
        end_utc = "2026-06-26T00:05:00Z"
        writer_errors = $WriterErrors
        events_dropped = $EventsDropped
        event_counts = [ordered]@{ MARKET_DATA_RECEIVED = 1; BBO_UPDATED = 1 }
        chunks = @($chunk)
    })
    Write-Json -Path (Join-Path $runRoot "m2c1b_capture_manifest.json") -Value ([ordered]@{
        status = "GO_M2C2_CAPTURE_VALIDATED"
        recorder_run_id = $RunId
        no_order_entry = $true
        no_account_api = $true
        no_db = $true
        no_databento = $true
    })
    Write-Json -Path (Join-Path $healthDir "data_quality_report.json") -Value ([ordered]@{
        sequence_gap_count = $SequenceGapCount
        sequence_out_of_order_count = 0
        shadow_ready = $true
    })

    $finalSha = Get-Sha256Hex -Path $finalPath
    $finalKey = "m2-capture/environment=DEMO/date=2026-06-26/recorder_run=$RunId/final_manifest.json"
    $chunkKey = "m2-capture/environment=DEMO/date=2026-06-26/recorder_run=$RunId/$RelativePath"
    $objects = @{}
    if ($RemoteMode -eq "partial" -or $RemoteMode -eq "all" -or $RemoteMode -eq "all_match") { $objects[$finalKey] = Convert-HexSha256ToBase64 $finalSha }
    if (($RemoteMode -eq "all" -or $RemoteMode -eq "all_match") -and $WriteChunk -and (Test-Path -LiteralPath $chunkPath)) { $objects[$chunkKey] = Convert-HexSha256ToBase64 $actualSha }
    if ($RemoteMode -eq "mismatch") { $objects[$finalKey] = Convert-HexSha256ToBase64 ("1" * 64) }

    $statePath = Join-Path $CaseRoot "fake_aws_state.json"
    Write-Json -Path $statePath -Value ([ordered]@{ objects = $objects; puts = @() })
    [ordered]@{ recorder_root = $recorderRoot; run_root = $runRoot; state_path = $statePath; chunk_key = $chunkKey; final_key = $finalKey }
}

function Invoke-UploadCase {
    param(
        [string]$Name,
        [hashtable]$FixtureOptions = @{}
    )
    $caseRoot = Join-Path $tmpRoot $Name
    New-Item -ItemType Directory -Force -Path $caseRoot | Out-Null
    $fixture = New-UploadFixture -CaseRoot $caseRoot -RunId $Name @FixtureOptions
    $fakeAws = Join-Path $caseRoot "fake-aws.ps1"
    Write-FakeAwsCli -Path $fakeAws
    $env:FAKE_AWS_STATE = $fixture.state_path
    try {
        $output = @(& $uploadScript -BucketName "fixture-bucket" -RecorderRoot $fixture.recorder_root -Prefix "m2-capture" -Environment "DEMO" -AwsCliPath $fakeAws -DryRun)
        $exitCode = $LASTEXITCODE
        $json = ($output -join "`n") | ConvertFrom-Json
    }
    catch {
        $exitCode = 1
        $json = $null
        $output = @([string]$_.Exception.Message)
    }
    finally { Remove-Item Env:\FAKE_AWS_STATE -ErrorAction SilentlyContinue }
    $state = Get-Content -LiteralPath $fixture.state_path -Raw | ConvertFrom-Json
    [ordered]@{ name = $Name; exit_code = $exitCode; output = $output; report = $json; fake_state = $state; fixture = $fixture }
}

function Get-FirstRun($Case) { return @($Case.report.dry_run_runs)[0] }
function Get-FirstFile($Case, [string]$Path) { return @(Get-FirstRun $Case).files | Where-Object { $_.path -eq $Path } | Select-Object -First 1 }
function Has-BlockingIssue($Case, [string]$Pattern) { return ((@($Case.report.blocked_runs).blocking_issues -join "`n") -match $Pattern) }

$results = New-Object System.Collections.Generic.List[object]

$c1 = Invoke-UploadCase "size_present_matching" @{ SizeMode = "matching"; ShaMode = "matching" }
$results.Add((New-Result "manifest_chunk_size_present_matching_passes" ($c1.report.status -eq "DRY_RUN" -and (Get-FirstFile $c1 "chunks/events-000001.jsonl").size_source -eq "MANIFEST") $c1))

$c2 = Invoke-UploadCase "size_present_mismatch" @{ SizeMode = "mismatch"; ShaMode = "matching" }
$results.Add((New-Result "manifest_chunk_size_present_mismatch_no_go" ($c2.report.status -eq "NO_GO" -and (Has-BlockingIssue $c2 "manifest_size_mismatch")) $c2))

$c3 = Invoke-UploadCase "size_missing" @{ SizeMode = "missing"; ShaMode = "matching" }
$results.Add((New-Result "manifest_chunk_missing_size_passes_with_derived_size" ($c3.report.status -eq "DRY_RUN" -and (Get-FirstFile $c3 "chunks/events-000001.jsonl").size_source -eq "DERIVED_LOCAL_FILE") $c3))

$c4 = Invoke-UploadCase "size_empty" @{ SizeMode = "empty"; ShaMode = "matching" }
$results.Add((New-Result "manifest_chunk_empty_size_passes_with_derived_size" ($c4.report.status -eq "DRY_RUN" -and (Get-FirstFile $c4 "chunks/events-000001.jsonl").size_source -eq "DERIVED_LOCAL_FILE") $c4))

$c5 = Invoke-UploadCase "sha_present_matching" @{ SizeMode = "missing"; ShaMode = "matching" }
$results.Add((New-Result "manifest_chunk_sha_present_matching_passes" ($c5.report.status -eq "DRY_RUN" -and (Get-FirstFile $c5 "chunks/events-000001.jsonl").sha256_source -eq "MANIFEST") $c5))

$c6 = Invoke-UploadCase "sha_present_mismatch" @{ SizeMode = "missing"; ShaMode = "mismatch" }
$results.Add((New-Result "manifest_chunk_sha_present_mismatch_no_go" ($c6.report.status -eq "NO_GO" -and (Has-BlockingIssue $c6 "manifest_sha256_mismatch")) $c6))

$c7 = Invoke-UploadCase "sha_missing" @{ SizeMode = "matching"; ShaMode = "missing" }
$results.Add((New-Result "manifest_chunk_missing_sha_passes_with_derived_sha" ($c7.report.status -eq "DRY_RUN" -and (Get-FirstFile $c7 "chunks/events-000001.jsonl").sha256_source -eq "DERIVED_LOCAL_FILE") $c7))

$c8 = Invoke-UploadCase "unsafe_path" @{ RelativePath = "../escape.jsonl"; WriteChunk = $false }
$results.Add((New-Result "unsafe_chunk_path_no_go" ($c8.report.status -eq "NO_GO" -and (Has-BlockingIssue $c8 "unsafe_manifest_path")) $c8))

$c9 = Invoke-UploadCase "missing_chunk_file" @{ WriteChunk = $false }
$results.Add((New-Result "missing_chunk_file_no_go" ($c9.report.status -eq "NO_GO" -and (Has-BlockingIssue $c9 "manifest_file_missing")) $c9))

$c10 = Invoke-UploadCase "finalized_false" @{ Finalized = $false }
$results.Add((New-Result "finalized_false_no_go" ($c10.report.status -eq "NO_GO" -and (Has-BlockingIssue $c10 "final_manifest_not_finalized")) $c10))

$c11 = Invoke-UploadCase "remote_checksum_match" @{ SizeMode = "matching"; ShaMode = "matching"; RemoteMode = "all" }
$allVerified = @((Get-FirstRun $c11).files | Where-Object { $_.remote_status -ne "VERIFIED" }).Count -eq 0
$results.Add((New-Result "existing_remote_checksum_match_idempotent_ok" ($c11.report.status -eq "DRY_RUN" -and $allVerified -and (Get-FirstRun $c11).marker_action -eq "DRY_RUN_WOULD_WRITE_MARKER_ALL_REMOTE_VERIFIED") $c11))

$c12 = Invoke-UploadCase "remote_checksum_mismatch" @{ SizeMode = "matching"; ShaMode = "matching"; RemoteMode = "mismatch" }
$results.Add((New-Result "existing_remote_checksum_mismatch_no_go_no_overwrite" ($c12.report.status -eq "NO_GO" -and (Has-BlockingIssue $c12 "remote_checksum_mismatch") -and @($c12.fake_state.puts).Count -eq 0) $c12))

$c13 = Invoke-UploadCase "partial_remote_prefix" @{ SizeMode = "matching"; ShaMode = "matching"; RemoteMode = "partial" }
$wouldUpload = @((Get-FirstRun $c13).files | Where-Object { $_.action -eq "DRY_RUN_WOULD_UPLOAD" }).Count
$remoteVerified = @((Get-FirstRun $c13).files | Where-Object { $_.remote_status -eq "VERIFIED" }).Count
$results.Add((New-Result "partial_s3_prefix_dry_run_reports_remaining_uploads_no_marker" ($c13.report.status -eq "DRY_RUN" -and $wouldUpload -gt 0 -and $remoteVerified -gt 0 -and (Get-FirstRun $c13).marker_action -eq "DRY_RUN_WOULD_NOT_WRITE_MARKER_PENDING_UPLOADS") $c13))

$c14 = Invoke-UploadCase "all_remote_verified_marker_would_write" @{ SizeMode = "matching"; ShaMode = "matching"; RemoteMode = "all" }
$results.Add((New-Result "all_objects_verified_marker_would_be_written_in_real_mode" ($c14.report.status -eq "DRY_RUN" -and (Get-FirstRun $c14).marker_action -eq "DRY_RUN_WOULD_WRITE_MARKER_ALL_REMOTE_VERIFIED") $c14))

$failures = @($results.ToArray() | Where-Object { $_.status -ne "PASS" })
$report = [ordered]@{
    gate = if ($failures.Count -eq 0) { "GO_AWS2H_CHUNK_UPLOAD_OFFLINE_TESTS" } else { "NO_GO_AWS2H_CHUNK_UPLOAD_OFFLINE_TESTS" }
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    no_real_s3_upload = $true
    no_marker_write = $true
    no_broker_capture = $true
    fake_aws_cli = $true
    fixture_root = $tmpRoot
    results = $results.ToArray()
    failure_count = $failures.Count
}
Write-Json -Path (Join-Path $repo $OutputPath) -Value $report
$report | ConvertTo-Json -Depth 30
if ($failures.Count -ne 0) { exit 1 }



