resource "aws_ssm_document" "install_runbook" {
  name            = "${local.name_prefix}-install-runbook"
  document_type   = "Command"
  document_format = "JSON"

  content = jsonencode({
    schemaVersion = "2.2"
    description   = "Install qq-fund-platform AWS1 M2 market-data-only smoke capture host from verified artifacts. No apply or credential value is embedded."
    parameters = {
      ArtifactS3Uri = {
        type        = "String"
        description = "s3:// URI for the signed AWS1 deployment artifact zip in the archive bucket."
      }
      ArtifactFileName = {
        type        = "String"
        default     = "anubis_aws1_read_only_shadow_foundation_plan_ready.zip"
        description = "Downloaded application artifact filename."
      }
      ArtifactSha256 = {
        type        = "String"
        description = "Expected SHA-256 of the deployment artifact zip."
      }
      AwsCliMsiS3Uri = {
        type        = "String"
        default     = var.aws_cli_msi_s3_uri
        description = "s3:// URI for a pre-staged AWS CLI v2 MSI artifact."
      }
      AwsCliMsiFileName = {
        type        = "String"
        default     = "AWSCLIV2.msi"
        description = "Downloaded AWS CLI MSI filename."
      }
      AwsCliMsiSha256 = {
        type        = "String"
        default     = var.aws_cli_msi_sha256
        description = "Expected SHA-256 of the AWS CLI v2 MSI."
      }
      EnableAutoStart = {
        type          = "String"
        allowedValues = ["true", "false"]
        default       = "false"
        description   = "Must remain false for AWS1 SMOKE_CAPTURE_BOUNDED unless the lead approves otherwise."
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
        action = "aws:downloadContent"
        name   = "DownloadAwsCliMsi"
        inputs = {
          sourceType      = "S3"
          sourceInfo      = "{\"path\":\"{{ AwsCliMsiS3Uri }}\"}"
          destinationPath = "C:\\Anubis\\Deploy"
        }
      },
      {
        action = "aws:runPowerShellScript"
        name   = "InstallAwsCli"
        inputs = {
          runCommand = [
            "$msi = Join-Path 'C:\\Anubis\\Deploy' '{{ AwsCliMsiFileName }}'",
            "if (-not (Test-Path -LiteralPath $msi)) { throw 'aws_cli_msi_missing' }",
            "$actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $msi).Hash.ToUpperInvariant()",
            "if ($actual -ne '{{ AwsCliMsiSha256 }}'.ToUpperInvariant()) { throw \"aws_cli_msi_sha256_mismatch:$actual\" }",
            "Start-Process -FilePath msiexec.exe -ArgumentList @('/i', $msi, '/qn', '/norestart') -Wait -WindowStyle Hidden",
            "$aws = 'C:\\Program Files\\Amazon\\AWSCLIV2\\aws.exe'",
            "if (-not (Test-Path -LiteralPath $aws)) { throw 'aws_cli_install_failed' }",
            "& $aws --version"
          ]
        }
      },
      {
        action = "aws:downloadContent"
        name   = "DownloadAppArtifact"
        inputs = {
          sourceType      = "S3"
          sourceInfo      = "{\"path\":\"{{ ArtifactS3Uri }}\"}"
          destinationPath = "C:\\Anubis\\Deploy"
        }
      },
      {
        action = "aws:runPowerShellScript"
        name   = "InstallCaptureHost"
        inputs = {
          runCommand = [
            "$dest = Join-Path 'C:\\Anubis\\Deploy' '{{ ArtifactFileName }}'",
            "if (-not (Test-Path -LiteralPath $dest)) { throw 'app_artifact_missing' }",
            "$actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $dest).Hash.ToUpperInvariant()",
            "if ($actual -ne '{{ ArtifactSha256 }}'.ToUpperInvariant()) { throw \"artifact_sha256_mismatch:$actual\" }",
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
