param(
    [Parameter(Mandatory = $true)]
    [string]$BucketName,

    [string]$RecorderRoot = "D:\Anubis\Recorder",
    [string]$Prefix = "m2-capture",
    [string]$Environment = "",
    [string]$ExpectedAwsCliSha256 = "",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Get-JsonFile {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { throw "json_file_missing:$Path" }
    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Get-Prop {
    param([object]$Object, [string]$Name)
    if ($null -eq $Object) { return $null }
    $prop = $Object.PSObject.Properties | Where-Object { $_.Name -ieq $Name } | Select-Object -First 1
    if ($null -eq $prop) { return $null }
    return $prop.Value
}

function Get-Sha256Hex {
    param([string]$Path)
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
}

function Convert-HexSha256ToBase64 {
    param([string]$Hex)
    if ($Hex.Length -ne 64) { throw "invalid_sha256_hex_length:$Hex" }
    $bytes = [byte[]]::new(32)
    for ($i = 0; $i -lt $Hex.Length; $i += 2) {
        $bytes[[int]($i / 2)] = [Convert]::ToByte($Hex.Substring($i, 2), 16)
    }
    return [Convert]::ToBase64String($bytes)
}

function Test-SafeRelativePath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    if ($Path.Contains("\")) { return $false }
    if ([System.IO.Path]::IsPathRooted($Path)) { return $false }
    foreach ($part in $Path.Split('/')) {
        if ($part -in @("", ".", "..")) { return $false }
    }
    return $true
}

function Convert-KeySegment {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return "unknown" }
    return ($Value -replace "[^A-Za-z0-9_.=-]", "_")
}

function Convert-ToDateTimeOffsetOrNull {
    param([object]$Value)
    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) { return $null }
    try { return [DateTimeOffset]::Parse([string]$Value, [Globalization.CultureInfo]::InvariantCulture) }
    catch { return $null }
}

function New-UploadFileSpec {
    param(
        [string]$RunRoot,
        [string]$RelativePath,
        [string]$ExpectedSha256 = "",
        [Nullable[int64]]$ExpectedSizeBytes = $null
    )

    if (-not (Test-SafeRelativePath -Path $RelativePath)) { throw "unsafe_manifest_path:$RelativePath" }
    $fullPath = Join-Path $RunRoot ($RelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
    if (-not (Test-Path -LiteralPath $fullPath)) { throw "manifest_file_missing:$RelativePath" }
    $info = Get-Item -LiteralPath $fullPath
    if ($null -ne $ExpectedSizeBytes -and $info.Length -ne $ExpectedSizeBytes.Value) {
        throw "manifest_size_mismatch:$RelativePath expected=$($ExpectedSizeBytes.Value) actual=$($info.Length)"
    }

    $shaHex = Get-Sha256Hex -Path $fullPath
    if (-not [string]::IsNullOrWhiteSpace($ExpectedSha256) -and -not [string]::Equals($shaHex, $ExpectedSha256.ToUpperInvariant(), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "manifest_sha256_mismatch:$RelativePath expected=$ExpectedSha256 actual=$shaHex"
    }

    return [ordered]@{
        relative_path = $RelativePath
        full_path = $fullPath
        size_bytes = [int64]$info.Length
        sha256_hex = $shaHex
        sha256_base64 = Convert-HexSha256ToBase64 -Hex $shaHex
    }
}

if (-not $DryRun) {
    $prereq = & (Join-Path $PSScriptRoot "Test-AnubisAws1HostPrerequisites.ps1") -ExpectedAwsCliSha256 $ExpectedAwsCliSha256 -Json | ConvertFrom-Json
    if ($prereq.status -ne "PASS") { throw "host_prerequisites_failed:$($prereq | ConvertTo-Json -Compress)" }
    $awsCliPath = [string]$prereq.aws_cli_path
}
else {
    $awsCliPath = "DRY_RUN"
}

$finals = @(Get-ChildItem -LiteralPath $RecorderRoot -Recurse -Filter "final_manifest.json" -File -ErrorAction SilentlyContinue | Sort-Object FullName)
$uploaded = @()
$skipped = @()

foreach ($finalFile in $finals) {
    $runRoot = $finalFile.DirectoryName
    $marker = Join-Path $runRoot ".s3_upload_verified"
    if (Test-Path -LiteralPath $marker) {
        $skipped += [ordered]@{ run_root = $runRoot; reason = "already_verified" }
        continue
    }

    $final = Get-JsonFile -Path $finalFile.FullName
    if (-not [bool](Get-Prop $final "finalized")) { throw "final_manifest_not_finalized:$($finalFile.FullName)" }

    $runId = [string](Get-Prop $final "recorder_run_id")
    if ([string]::IsNullOrWhiteSpace($runId)) { $runId = Split-Path -Leaf $runRoot }

    $runEnvironment = if (-not [string]::IsNullOrWhiteSpace($Environment)) { $Environment } else { [string](Get-Prop $final "environment") }
    if ([string]::IsNullOrWhiteSpace($runEnvironment)) { $runEnvironment = "unknown" }

    $runDate = Convert-ToDateTimeOffsetOrNull (Get-Prop $final "end_utc")
    if ($null -eq $runDate) { $runDate = Convert-ToDateTimeOffsetOrNull (Get-Prop $final "start_utc") }
    if ($null -eq $runDate) { $runDate = [DateTimeOffset]$finalFile.LastWriteTimeUtc }
    $dateSegment = $runDate.UtcDateTime.ToString("yyyy-MM-dd", [Globalization.CultureInfo]::InvariantCulture)

    $fileSpecs = New-Object System.Collections.Generic.List[object]
    $fileSpecs.Add((New-UploadFileSpec -RunRoot $runRoot -RelativePath "final_manifest.json"))

    foreach ($chunk in @(Get-Prop $final "chunks")) {
        $relative = [string](Get-Prop $chunk "file")
        $size = [int64](Get-Prop $chunk "size_bytes")
        $sha = [string](Get-Prop $chunk "sha256")
        $fileSpecs.Add((New-UploadFileSpec -RunRoot $runRoot -RelativePath $relative -ExpectedSha256 $sha -ExpectedSizeBytes $size))
    }

    $verified = @()
    foreach ($fileSpec in $fileSpecs) {
        $key = "{0}/environment={1}/date={2}/recorder_run={3}/{4}" -f $Prefix.TrimEnd('/'), (Convert-KeySegment $runEnvironment), $dateSegment, (Convert-KeySegment $runId), $fileSpec.relative_path

        if (-not $DryRun) {
            & $awsCliPath s3api put-object --bucket $BucketName --key $key --body $fileSpec.full_path --checksum-algorithm SHA256 --checksum-sha256 $fileSpec.sha256_base64 | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "s3_put_object_failed:$key" }

            $remoteChecksum = & $awsCliPath s3api head-object --bucket $BucketName --key $key --checksum-mode ENABLED --query ChecksumSHA256 --output text
            if ($LASTEXITCODE -ne 0) { throw "s3_head_object_failed:$key" }
            if (-not [string]::Equals(([string]$remoteChecksum).Trim(), $fileSpec.sha256_base64, [System.StringComparison]::Ordinal)) {
                throw "s3_remote_checksum_mismatch:$key"
            }
        }

        $verified += [ordered]@{
            path = $fileSpec.relative_path
            s3_key = $key
            size_bytes = $fileSpec.size_bytes
            sha256 = $fileSpec.sha256_hex
            checksum_sha256_base64 = $fileSpec.sha256_base64
        }
    }

    if (-not $DryRun) {
        [ordered]@{
            status = "UPLOAD_VERIFIED"
            bucket = $BucketName
            prefix = $Prefix
            environment = $runEnvironment
            recorder_run_id = $runId
            verified_utc = (Get-Date).ToUniversalTime().ToString("o")
            files = $verified
        } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $marker -Encoding UTF8
    }

    $uploaded += [ordered]@{
        run_root = $runRoot
        recorder_run_id = $runId
        environment = $runEnvironment
        date = $dateSegment
        file_count = $verified.Count
        files = $verified
    }
}

[ordered]@{
    status = if ($DryRun) { "DRY_RUN" } else { "UPLOAD_VERIFIED" }
    bucket = $BucketName
    prefix = $Prefix
    operation_mode = "SMOKE_CAPTURE_BOUNDED"
    uploaded_runs = $uploaded
    skipped_runs = $skipped
    deletes_performed = 0
    upload_scope = "final_manifest_and_manifest_listed_chunks_only"
} | ConvertTo-Json -Depth 8
