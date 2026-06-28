$ErrorActionPreference = "Stop"

$scriptPath = Join-Path $PSScriptRoot "New-AnubisAws1InstallRunbookParameters.ps1"
if (-not (Test-Path -LiteralPath $scriptPath)) { throw "parameter_generator_missing:$scriptPath" }

function Invoke-Generator {
    param(
        [string]$ArtifactS3Uri,
        [string]$AwsCliMsiS3Uri
    )

    $json = & $scriptPath `
        -ArtifactFileName "app.zip" `
        -ArtifactS3Uri $ArtifactS3Uri `
        -ArtifactSha256 "abc123" `
        -AwsCliMsiFileName "AWSCLIV2.msi" `
        -AwsCliMsiS3Uri $AwsCliMsiS3Uri `
        -AwsCliMsiSha256 "def456" `
        -EnableAutoStart "false" `
        -Region "eu-west-2"

    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "generator_failed:$LASTEXITCODE" }
    return ($json | ConvertFrom-Json)
}

function Assert-Equal {
    param([object]$Actual, [object]$Expected, [string]$Name)
    if ($Actual -ne $Expected) { throw "$Name expected '$Expected' got '$Actual'" }
}

function Assert-Throws {
    param([scriptblock]$Block, [string]$Name)
    try {
        & $Block | Out-Null
    }
    catch {
        return
    }
    throw "$Name expected throw"
}

$results = New-Object System.Collections.Generic.List[object]

$r1 = Invoke-Generator `
    -ArtifactS3Uri "s3://qq-fund-platform-demo-aws1-archive-761018894194-eu-west-2/deploy-artifacts/aws3d/app.zip" `
    -AwsCliMsiS3Uri "s3://qq-fund-platform-demo-aws1-archive-761018894194-eu-west-2/bootstrap/tools/AWSCLIV2.msi"
Assert-Equal $r1.ArtifactS3Uri[0] "https://qq-fund-platform-demo-aws1-archive-761018894194-eu-west-2.s3.eu-west-2.amazonaws.com/deploy-artifacts/aws3d/app.zip" "artifact_s3_uri_conversion"
Assert-Equal $r1.AwsCliMsiS3Uri[0] "https://qq-fund-platform-demo-aws1-archive-761018894194-eu-west-2.s3.eu-west-2.amazonaws.com/bootstrap/tools/AWSCLIV2.msi" "aws_cli_s3_uri_conversion"
$results.Add([ordered]@{ name = "s3_uri_inputs_convert_to_downloadcontent_https"; status = "PASS" })

$https = "https://qq-fund-platform-demo-aws1-archive-761018894194-eu-west-2.s3.eu-west-2.amazonaws.com/bootstrap/tools/AWSCLIV2.msi"
$r2 = Invoke-Generator -ArtifactS3Uri $https -AwsCliMsiS3Uri $https
Assert-Equal $r2.ArtifactS3Uri[0] $https "artifact_https_preserved"
Assert-Equal $r2.AwsCliMsiS3Uri[0] $https "aws_cli_https_preserved"
$results.Add([ordered]@{ name = "https_inputs_are_preserved"; status = "PASS" })

Assert-Throws { Invoke-Generator -ArtifactS3Uri "s3://bucket-only" -AwsCliMsiS3Uri $https } "malformed_artifact_rejected"
Assert-Throws { Invoke-Generator -ArtifactS3Uri $https -AwsCliMsiS3Uri "file://AWSCLIV2.msi" } "malformed_msi_rejected"
$results.Add([ordered]@{ name = "malformed_paths_rejected_before_ssm_execution"; status = "PASS" })

$names = @($r1.PSObject.Properties.Name | Sort-Object)
$expectedNames = @("ArtifactFileName", "ArtifactS3Uri", "ArtifactSha256", "AwsCliMsiFileName", "AwsCliMsiS3Uri", "AwsCliMsiSha256", "EnableAutoStart") | Sort-Object
Assert-Equal (($names -join ",")) (($expectedNames -join ",")) "parameter_names"
foreach ($name in $expectedNames) {
    if ($null -eq $r1.$name) { throw "parameter_missing:$name" }
    if ($r1.$name.Count -ne 1) { throw "parameter_not_single_value_array:$name" }
}
$results.Add([ordered]@{ name = "ssm_parameter_names_and_array_shape_match_document"; status = "PASS" })

$serialized = $r1 | ConvertTo-Json -Depth 5
foreach ($forbidden in @("CredentialSecretId", "SecretString", "password", "NewOrderSingle", "35=D", "Start-AnubisAws1Recorder", "send-command")) {
    if ($serialized -match [regex]::Escape($forbidden)) { throw "forbidden_value_in_parameter_payload:$forbidden" }
}
$results.Add([ordered]@{ name = "no_secrets_or_runtime_capture_surface_in_payload"; status = "PASS" })

[ordered]@{
    status = "PASS"
    tests = $results
} | ConvertTo-Json -Depth 6

