param(
    [Parameter(Mandatory = $true)]
    [string]$BucketName,

    [string]$RecorderRoot = "D:\Anubis\Recorder",
    [string]$Prefix = "m2-capture",
    [string]$Environment = "",
    [string]$ExpectedAwsCliSha256 = "",
    [string]$AwsCliPath = "",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Get-JsonFile {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { throw "json_file_missing:$Path" }
    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
}

function Get-OptionalJsonFile {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try { return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json }
    catch { return $null }
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

function Convert-ToOptionalInt64 {
    param([object]$Value)
    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return [ordered]@{ present = $false; value = $null }
    }
    try { return [ordered]@{ present = $true; value = [int64]$Value } }
    catch { throw "invalid_int64_value:$Value" }
}

function Convert-ToInt64OrZero {
    param([object]$Value)
    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) { return [int64]0 }
    return [int64]$Value
}

function Get-SequenceGapStatus {
    param([object]$DataQuality)
    if ($null -eq $DataQuality) { return [int64]0 }
    $gap = Convert-ToInt64OrZero (Get-Prop $DataQuality "sequence_gap_count")
    $ooo = Convert-ToInt64OrZero (Get-Prop $DataQuality "sequence_out_of_order_count")
    return [int64]($gap + $ooo)
}

function New-UploadFileSpec {
    param(
        [string]$RunRoot,
        [string]$RelativePath,
        [object]$ExpectedSha256 = $null,
        [object]$ExpectedSizeBytes = $null
    )

    if (-not (Test-SafeRelativePath -Path $RelativePath)) { throw "unsafe_manifest_path:$RelativePath" }
    $fullPath = Join-Path $RunRoot ($RelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar))
    if (-not (Test-Path -LiteralPath $fullPath)) { throw "manifest_file_missing:$RelativePath" }

    $info = Get-Item -LiteralPath $fullPath
    $sizeInfo = Convert-ToOptionalInt64 -Value $ExpectedSizeBytes
    $sizeSource = if ($sizeInfo.present) { "MANIFEST" } else { "DERIVED_LOCAL_FILE" }
    if ($sizeInfo.present -and $info.Length -ne $sizeInfo.value) {
        throw "manifest_size_mismatch:$RelativePath expected=$($sizeInfo.value) actual=$($info.Length)"
    }

    $shaHex = Get-Sha256Hex -Path $fullPath
    $expectedSha = [string]$ExpectedSha256
    $shaSource = if ([string]::IsNullOrWhiteSpace($expectedSha)) { "DERIVED_LOCAL_FILE" } else { "MANIFEST" }
    if (-not [string]::IsNullOrWhiteSpace($expectedSha) -and -not [string]::Equals($shaHex, $expectedSha.ToUpperInvariant(), [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "manifest_sha256_mismatch:$RelativePath expected=$expectedSha actual=$shaHex"
    }

    return [ordered]@{
        relative_path = $RelativePath
        full_path = $fullPath
        size_bytes = [int64]$info.Length
        size_source = $sizeSource
        sha256_hex = $shaHex
        sha256_source = $shaSource
        sha256_base64 = Convert-HexSha256ToBase64 -Hex $shaHex
    }
}

function Resolve-AwsCliPath {
    param([bool]$RequireAwsCli)

    if (-not [string]::IsNullOrWhiteSpace($AwsCliPath)) {
        $prereq = & (Join-Path $PSScriptRoot "Test-AnubisAws1HostPrerequisites.ps1") -AwsCliPath $AwsCliPath -ExpectedAwsCliSha256 $ExpectedAwsCliSha256 -Json | ConvertFrom-Json
        if ($prereq.status -ne "PASS") { throw "host_prerequisites_failed:$($prereq | ConvertTo-Json -Compress)" }
        return [string]$prereq.aws_cli_path
    }

    try {
        $prereq = & (Join-Path $PSScriptRoot "Test-AnubisAws1HostPrerequisites.ps1") -ExpectedAwsCliSha256 $ExpectedAwsCliSha256 -Json | ConvertFrom-Json
        if ($prereq.status -eq "PASS") { return [string]$prereq.aws_cli_path }
        if ($RequireAwsCli) { throw "host_prerequisites_failed:$($prereq | ConvertTo-Json -Compress)" }
    }
    catch {
        if ($RequireAwsCli) { throw }
    }
    return ""
}

function Get-RemoteObjectState {
    param(
        [string]$AwsPath,
        [string]$Key,
        [string]$ExpectedChecksumBase64
    )

    if ([string]::IsNullOrWhiteSpace($AwsPath)) {
        return [ordered]@{
            status = "NOT_EVALUATED"
            checksum_sha256_base64 = $null
            issue = "aws_cli_not_available"
        }
    }

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = @(& $AwsPath s3api head-object --bucket $BucketName --key $Key --checksum-mode ENABLED --query ChecksumSHA256 --output text 2>&1)
        $exitCode = $LASTEXITCODE
        $text = (($output | ForEach-Object { [string]$_ }) -join "`n").Trim()
    }
    catch {
        $exitCode = 1
        $text = [string]$_.Exception.Message
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0) {
        if ($text -match "(?i)(NoSuchKey|Not\s+Found|NotFound|404)") {
            return [ordered]@{
                status = "MISSING"
                checksum_sha256_base64 = $null
                issue = $null
            }
        }
        return [ordered]@{
            status = "HEAD_FAILED"
            checksum_sha256_base64 = $null
            issue = $text
        }
    }

    if ([string]::IsNullOrWhiteSpace($text) -or $text -eq "None") {
        return [ordered]@{
            status = "CHECKSUM_MISSING"
            checksum_sha256_base64 = $text
            issue = "remote_checksum_missing"
        }
    }

    if ([string]::Equals($text, $ExpectedChecksumBase64, [System.StringComparison]::Ordinal)) {
        return [ordered]@{
            status = "VERIFIED"
            checksum_sha256_base64 = $text
            issue = $null
        }
    }

    return [ordered]@{
        status = "CHECKSUM_MISMATCH"
        checksum_sha256_base64 = $text
        issue = "remote_checksum_mismatch"
    }
}

function Invoke-UploadOrDryRun {
    param(
        [string]$AwsPath,
        [object]$FileSpec,
        [string]$Key
    )

    $before = Get-RemoteObjectState -AwsPath $AwsPath -Key $Key -ExpectedChecksumBase64 $FileSpec.sha256_base64
    $action = $null
    $blockingIssue = $null

    if ($DryRun) {
        switch ($before.status) {
            "VERIFIED" { $action = "DRY_RUN_REMOTE_ALREADY_VERIFIED" }
            "MISSING" { $action = "DRY_RUN_WOULD_UPLOAD" }
            "CHECKSUM_MISMATCH" { $action = "DRY_RUN_BLOCKED_REMOTE_CHECKSUM_MISMATCH"; $blockingIssue = "remote_checksum_mismatch:$Key" }
            "CHECKSUM_MISSING" { $action = "DRY_RUN_BLOCKED_REMOTE_CHECKSUM_MISSING"; $blockingIssue = "remote_checksum_missing:$Key" }
            "HEAD_FAILED" { $action = "DRY_RUN_BLOCKED_REMOTE_HEAD_FAILED"; $blockingIssue = "remote_head_failed:$Key" }
            default { $action = "DRY_RUN_REMOTE_NOT_EVALUATED" }
        }
    }
    else {
        switch ($before.status) {
            "VERIFIED" {
                $action = "REMOTE_ALREADY_VERIFIED"
            }
            "MISSING" {
                & $AwsPath s3api put-object --bucket $BucketName --key $Key --body $FileSpec.full_path --checksum-algorithm SHA256 --checksum-sha256 $FileSpec.sha256_base64 | Out-Null
                if ($LASTEXITCODE -ne 0) { throw "s3_put_object_failed:$Key" }
                $afterPut = Get-RemoteObjectState -AwsPath $AwsPath -Key $Key -ExpectedChecksumBase64 $FileSpec.sha256_base64
                if ($afterPut.status -ne "VERIFIED") { throw "s3_remote_checksum_mismatch_after_put:$Key status=$($afterPut.status)" }
                $action = "UPLOADED_AND_VERIFIED"
                $before = $afterPut
            }
            "CHECKSUM_MISMATCH" { throw "s3_remote_checksum_mismatch:$Key" }
            "CHECKSUM_MISSING" { throw "s3_remote_checksum_missing:$Key" }
            "HEAD_FAILED" { throw "s3_head_object_failed:$Key issue=$($before.issue)" }
            default { throw "s3_remote_state_not_evaluated:$Key status=$($before.status)" }
        }
    }

    return [ordered]@{
        path = $FileSpec.relative_path
        s3_key = $Key
        size_bytes = $FileSpec.size_bytes
        size_source = $FileSpec.size_source
        sha256 = $FileSpec.sha256_hex
        sha256_source = $FileSpec.sha256_source
        checksum_sha256_base64 = $FileSpec.sha256_base64
        remote_status = $before.status
        remote_checksum_sha256_base64 = $before.checksum_sha256_base64
        action = $action
        blocking_issue = $blockingIssue
    }
}

function Assert-RunIsSafeForUpload {
    param([string]$RunRoot, [object]$FinalManifest)

    if (-not [bool](Get-Prop $FinalManifest "finalized")) { throw "final_manifest_not_finalized:$RunRoot" }

    $writerErrors = Convert-ToInt64OrZero (Get-Prop $FinalManifest "writer_errors")
    if ($writerErrors -ne 0) { throw "final_manifest_writer_errors_nonzero:$RunRoot" }

    $drops = Convert-ToInt64OrZero (Get-Prop $FinalManifest "events_dropped")
    if ($drops -ne 0) { throw "final_manifest_events_dropped_nonzero:$RunRoot" }

    $dq = Get-OptionalJsonFile -Path (Join-Path $RunRoot "health\data_quality_report.json")
    $sequenceGapStatus = Get-SequenceGapStatus -DataQuality $dq
    if ($sequenceGapStatus -ne 0) { throw "sequence_gap_status_nonzero:$RunRoot" }

    $capture = Get-OptionalJsonFile -Path (Join-Path $RunRoot "m2c1b_capture_manifest.json")
    if ($null -ne $capture) {
        $captureStatus = [string](Get-Prop $capture "status")
        if (-not [string]::IsNullOrWhiteSpace($captureStatus) -and $captureStatus -ne "GO_M2C2_CAPTURE_VALIDATED") {
            throw "capture_manifest_status_not_go:$captureStatus"
        }
        foreach ($flag in @("no_order_entry", "no_account_api", "no_db", "no_databento")) {
            $value = Get-Prop $capture $flag
            if ($null -ne $value -and -not [bool]$value) { throw "safety_flag_not_true:$flag" }
        }
    }
}

$awsCliPath = Resolve-AwsCliPath -RequireAwsCli:(!$DryRun)

$finals = @(Get-ChildItem -LiteralPath $RecorderRoot -Recurse -Filter "final_manifest.json" -File -ErrorAction SilentlyContinue | Sort-Object FullName)
$uploaded = @()
$skipped = @()
$dryRunRuns = @()
$blockedRuns = @()

foreach ($finalFile in $finals) {
    $runRoot = $finalFile.DirectoryName
    $marker = Join-Path $runRoot ".s3_upload_verified"
    if (Test-Path -LiteralPath $marker) {
        $skipped += [ordered]@{ run_root = $runRoot; reason = "already_verified" }
        continue
    }

    $final = $null
    try {
        $final = Get-JsonFile -Path $finalFile.FullName
        Assert-RunIsSafeForUpload -RunRoot $runRoot -FinalManifest $final

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
            $size = Get-Prop $chunk "size_bytes"
            $sha = Get-Prop $chunk "sha256"
            $fileSpecs.Add((New-UploadFileSpec -RunRoot $runRoot -RelativePath $relative -ExpectedSha256 $sha -ExpectedSizeBytes $size))
        }

        $verified = @()
        $runBlockingIssues = @()
        foreach ($fileSpec in $fileSpecs) {
            $key = "{0}/environment={1}/date={2}/recorder_run={3}/{4}" -f $Prefix.TrimEnd('/'), (Convert-KeySegment $runEnvironment), $dateSegment, (Convert-KeySegment $runId), $fileSpec.relative_path
            $fileResult = Invoke-UploadOrDryRun -AwsPath $awsCliPath -FileSpec $fileSpec -Key $key
            if (-not [string]::IsNullOrWhiteSpace([string]$fileResult.blocking_issue)) { $runBlockingIssues += [string]$fileResult.blocking_issue }
            $verified += $fileResult
        }

        $allRemoteVerified = @($verified | Where-Object { $_.remote_status -ne "VERIFIED" }).Count -eq 0
        $wouldUpload = @($verified | Where-Object { $_.action -eq "DRY_RUN_WOULD_UPLOAD" }).Count
        $markerAction = if ($DryRun) {
            if ($runBlockingIssues.Count -gt 0) { "DRY_RUN_BLOCKED_NO_MARKER" }
            elseif ($allRemoteVerified) { "DRY_RUN_WOULD_WRITE_MARKER_ALL_REMOTE_VERIFIED" }
            elseif ($wouldUpload -gt 0) { "DRY_RUN_WOULD_NOT_WRITE_MARKER_PENDING_UPLOADS" }
            else { "DRY_RUN_WOULD_NOT_WRITE_MARKER_REMOTE_NOT_EVALUATED" }
        }
        else {
            "WRITE_MARKER_AFTER_VERIFICATION"
        }

        $runReport = [ordered]@{
            run_root = $runRoot
            recorder_run_id = $runId
            environment = $runEnvironment
            date = $dateSegment
            file_count = $verified.Count
            files = $verified
            blocking_issues = $runBlockingIssues
            marker_action = $markerAction
        }

        if ($DryRun) {
            if ($runBlockingIssues.Count -gt 0) { $blockedRuns += $runReport }
            $dryRunRuns += $runReport
        }
        else {
            if ($runBlockingIssues.Count -gt 0) { throw "run_blocked:$runId issues=$($runBlockingIssues -join ',')" }
            [ordered]@{
                status = "UPLOAD_VERIFIED"
                bucket = $BucketName
                prefix = $Prefix
                environment = $runEnvironment
                recorder_run_id = $runId
                verified_utc = (Get-Date).ToUniversalTime().ToString("o")
                files = $verified
            } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $marker -Encoding UTF8
            $uploaded += $runReport
        }
    }
    catch {
        if ($DryRun) {
            $blocked = [ordered]@{
                run_root = $runRoot
                recorder_run_id = if ($null -ne $final) { [string](Get-Prop $final "recorder_run_id") } else { Split-Path -Leaf $runRoot }
                blocking_issues = @([string]$_.Exception.Message)
                marker_action = "DRY_RUN_BLOCKED_NO_MARKER"
            }
            $blockedRuns += $blocked
            $dryRunRuns += $blocked
        }
        else {
            throw
        }
    }
}

$blockedCount = @($blockedRuns).Count
[ordered]@{
    status = if ($DryRun) { if ($blockedCount -gt 0) { "NO_GO" } else { "DRY_RUN" } } else { "UPLOAD_VERIFIED" }
    bucket = $BucketName
    prefix = $Prefix
    operation_mode = "SMOKE_CAPTURE_BOUNDED"
    uploaded_runs = $uploaded
    dry_run_runs = $dryRunRuns
    blocked_runs = $blockedRuns
    skipped_runs = $skipped
    deletes_performed = 0
    upload_scope = "final_manifest_and_manifest_listed_chunks_only"
    marker_write_policy = "write_only_after_every_file_s3_checksum_verified"
    dry_run_no_put_object = [bool]$DryRun
    dry_run_no_marker_write = [bool]$DryRun
} | ConvertTo-Json -Depth 12


