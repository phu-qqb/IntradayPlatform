param(
    [Parameter(Mandatory = $true)] [string]$ArtifactFileName,
    [Parameter(Mandatory = $true)] [string]$ArtifactS3Uri,
    [Parameter(Mandatory = $true)] [string]$ArtifactSha256,
    [Parameter(Mandatory = $true)] [string]$AwsCliMsiFileName,
    [Parameter(Mandatory = $true)] [string]$AwsCliMsiS3Uri,
    [Parameter(Mandatory = $true)] [string]$AwsCliMsiSha256,
    [ValidateSet("true", "false")] [string]$EnableAutoStart = "false",
    [string]$Region = "eu-west-2"
)

$ErrorActionPreference = "Stop"

function ConvertTo-SsmDownloadContentPath {
    param(
        [Parameter(Mandatory = $true)] [string]$Value,
        [Parameter(Mandatory = $true)] [string]$Region
    )

    if ([string]::IsNullOrWhiteSpace($Value)) { throw "s3_path_required" }
    $trimmed = $Value.Trim()

    if ($trimmed -match "^https://[^/]+\.s3[.-][a-z0-9-]+\.amazonaws\.com/.+") {
        return $trimmed
    }

    if ($trimmed -match "^s3://([^/]+)/(.+)$") {
        $bucket = $Matches[1]
        $key = $Matches[2]
        if ([string]::IsNullOrWhiteSpace($bucket) -or [string]::IsNullOrWhiteSpace($key)) {
            throw "malformed_s3_path:$Value"
        }
        return "https://$bucket.s3.$Region.amazonaws.com/$key"
    }

    throw "unsupported_s3_path_format:$Value"
}

$parameters = [ordered]@{
    ArtifactFileName = @($ArtifactFileName)
    ArtifactS3Uri = @((ConvertTo-SsmDownloadContentPath -Value $ArtifactS3Uri -Region $Region))
    ArtifactSha256 = @($ArtifactSha256.ToUpperInvariant())
    AwsCliMsiFileName = @($AwsCliMsiFileName)
    AwsCliMsiS3Uri = @((ConvertTo-SsmDownloadContentPath -Value $AwsCliMsiS3Uri -Region $Region))
    AwsCliMsiSha256 = @($AwsCliMsiSha256.ToUpperInvariant())
    EnableAutoStart = @($EnableAutoStart.ToLowerInvariant())
}

$parameters | ConvertTo-Json -Depth 5
