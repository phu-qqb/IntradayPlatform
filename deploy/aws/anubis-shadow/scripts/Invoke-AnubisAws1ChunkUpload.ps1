param(
    [Parameter(Mandatory = $true)]
    [string]$BucketName,

    [string]$RecorderRoot = "D:\Anubis\Recorder",
    [string]$Prefix = "m2-capture",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Get-Sha256 {
    param([string]$Path)
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToUpperInvariant()
}

if (-not $DryRun -and -not (Get-Command aws -ErrorAction SilentlyContinue)) {
    throw "aws_cli_required_for_s3_upload"
}

$finals = Get-ChildItem -LiteralPath $RecorderRoot -Recurse -Filter "final_manifest.json" -File -ErrorAction SilentlyContinue
$uploaded = @()
$skipped = @()

foreach ($final in $finals) {
    $runRoot = $final.DirectoryName
    $marker = Join-Path $runRoot ".s3_upload_verified"
    if (Test-Path -LiteralPath $marker) {
        $skipped += $runRoot
        continue
    }

    $files = Get-ChildItem -LiteralPath $runRoot -Recurse -File |
        Where-Object { $_.Name -ne ".s3_upload_verified" } |
        Sort-Object FullName

    $verified = @()
    foreach ($file in $files) {
        $relative = ([System.IO.Path]::GetFullPath($file.FullName).Substring(([System.IO.Path]::GetFullPath($runRoot).TrimEnd("\")).Length + 1)).Replace("\", "/")
        $key = "$Prefix/$($final.Directory.Parent.Name)/$($final.Directory.Name)/$relative"
        $sha = Get-Sha256 $file.FullName

        if (-not $DryRun) {
            aws s3 cp $file.FullName "s3://$BucketName/$key" --metadata "sha256=$sha" --only-show-errors
            if ($LASTEXITCODE -ne 0) { throw "s3_upload_failed:$key" }
            $metadata = aws s3api head-object --bucket $BucketName --key $key --query Metadata.sha256 --output text
            if ($LASTEXITCODE -ne 0 -or $metadata.ToUpperInvariant() -ne $sha) { throw "s3_remote_hash_metadata_mismatch:$key" }
        }

        $verified += [ordered]@{ path = $relative; s3_key = $key; sha256 = $sha }
    }

    if (-not $DryRun) {
        $verified | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $marker -Encoding UTF8
    }
    $uploaded += [ordered]@{ run_root = $runRoot; file_count = $verified.Count; files = $verified }
}

[ordered]@{
    status = if ($DryRun) { "DRY_RUN" } else { "UPLOAD_VERIFIED" }
    bucket = $BucketName
    prefix = $Prefix
    uploaded_runs = $uploaded
    skipped_runs = $skipped
    deletes_performed = 0
} | ConvertTo-Json -Depth 8
