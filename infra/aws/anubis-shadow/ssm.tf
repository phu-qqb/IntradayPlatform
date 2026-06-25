resource "aws_ssm_document" "install_runbook" {
  name            = "${local.name_prefix}-install-runbook"
  document_type   = "Command"
  document_format = "JSON"

  content = jsonencode({
    schemaVersion = "2.2"
    description   = "Install or update Anubis AWS1 M2 market-data-only capture host. No apply or credential value is embedded."
    parameters = {
      ArtifactS3Uri = {
        type        = "String"
        description = "s3:// URI for the signed AWS1 deployment artifact zip."
      }
      ArtifactSha256 = {
        type        = "String"
        description = "Expected SHA-256 of the deployment artifact zip."
      }
      EnableAutoStart = {
        type          = "String"
        allowedValues = ["true", "false"]
        default       = "false"
        description   = "Whether to enable the recorder scheduled task after install."
      }
    }
    mainSteps = [
      {
        action = "aws:runPowerShellScript"
        name   = "PrepareDeployDirectory"
        inputs = {
          runCommand = [
            "New-Item -ItemType Directory -Force -Path C:\\Anubis\\Deploy | Out-Null"
          ]
        }
      },
      {
        action = "aws:runPowerShellScript"
        name   = "DownloadArtifact"
        inputs = {
          runCommand = [
            "$artifact = '{{ ArtifactS3Uri }}'",
            "$dest = 'C:\\Anubis\\Deploy\\aws1_artifact.zip'",
            "if (-not (Get-Command aws -ErrorAction SilentlyContinue)) { throw 'aws_cli_required_for_artifact_download' }",
            "aws s3 cp $artifact $dest --only-show-errors",
            "$actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $dest).Hash.ToUpperInvariant()",
            "if ($actual -ne '{{ ArtifactSha256 }}'.ToUpperInvariant()) { throw \"artifact_sha256_mismatch:$actual\" }"
          ]
        }
      },
      {
        action = "aws:runPowerShellScript"
        name   = "InstallCaptureHost"
        inputs = {
          runCommand = [
            "$dest = 'C:\\Anubis\\Deploy\\aws1_artifact.zip'",
            "$expand = 'C:\\Anubis\\Deploy\\aws1_artifact'",
            "if (Test-Path -LiteralPath $expand) { Remove-Item -LiteralPath $expand -Recurse -Force }",
            "Expand-Archive -LiteralPath $dest -DestinationPath $expand -Force",
            "$enable = [System.String]::Equals('{{ EnableAutoStart }}','true',[System.StringComparison]::OrdinalIgnoreCase)",
            "& \"$expand\\deploy\\aws\\anubis-shadow\\scripts\\Install-AnubisAws1Host.ps1\" -ArtifactZipPath $dest -ArtifactSha256 '{{ ArtifactSha256 }}' -InstallRoot '${var.install_root}' -RecorderRoot '${var.recorder_root}' -CredentialSecretId '${local.credential_secret_arn}' -MarketDataEndpointAlias '${var.lmax_market_data_endpoint_alias}' -ArchiveBucketName '${local.archive_bucket_name}' -Environment '${local.safe_environment}' -CloudWatchNamespace '${var.cloudwatch_namespace}' -EnableAutoStart:$enable"
          ]
        }
      }
    ]
  })
}
