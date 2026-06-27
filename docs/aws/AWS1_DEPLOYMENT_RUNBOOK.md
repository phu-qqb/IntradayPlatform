# AWS1 Deployment Runbook

This runbook is plan-ready for review. AWS1 still does not perform `terraform apply`.

## 1. Build Artifact

From the AWS1 worktree:

```powershell
.\deploy\aws\anubis-shadow\scripts\Package-M2CaptureHost.ps1
```

Output:

```text
artifacts\readiness\anubis-aws1-read-only-shadow-foundation-plan-ready\package\anubis_aws1_read_only_shadow_foundation_plan_ready.zip
```

The script publishes the M2 capture-only host as `win-x64 --self-contained true`, verifies the app executable SHA-256, embeds deploy/infra/docs/test evidence, writes `deployment_manifest.json`, and emits the artifact SHA-256 sidecar.

## 2. Local Gates

```powershell
.\deploy\aws\anubis-shadow\scripts\Test-AnubisAws1Local.ps1 -TerraformPath artifacts\tools\terraform\1.10.5\terraform.exe
```

Required PASS checks:

- `terraform fmt -check -recursive`;
- `terraform init -backend=false`;
- `terraform validate`;
- `.terraform.lock.hcl` present;
- PowerShell parse checks;
- status metrics fixture from real recorder artifacts;
- no EC2 ingress;
- no AWS apply command;
- no RDS initial path;
- no order mutation surface;
- no forbidden data vendor path;
- no secret values;
- self-contained package controls;
- manifest-only S3 upload with checksum verification.

## 3. Backend Prerequisites

Before any future plan/apply ceremony, create or identify the remote S3 state bucket out of band and supply backend config for:

```text
bucket
key
region
```

The stack declares an encrypted S3 backend with Terraform lockfile support. Do not apply AWS1 with local state.

## 4. Operator Inputs

Required future plan/apply values:

- approved Windows AMI ID and owner allow-list;
- explicit LMAX market-data egress CIDRs;
- artifact S3 URI and SHA-256;
- AWS CLI MSI S3 URI and SHA-256;
- market-data-only secret value populated out of band;
- alarm action ARNs only if `enable_cloudwatch_alarms=true`.

## 5. Install Through SSM

After a separately approved AWS apply in a later step, use the generated SSM document:

```text
qq-fund-platform-demo-aws1-install-runbook
```

The runbook downloads the AWS CLI MSI and app artifact using `aws:downloadContent`, verifies SHA-256 for both, installs AWS CLI v2, expands the app artifact, and invokes `Install-AnubisAws1Host.ps1`.

Autostart defaults to disabled for `SMOKE_CAPTURE_BOUNDED`.

## 6. Start Bounded Capture

```powershell
.\Start-AnubisAws1Recorder.ps1 -CredentialSecretId <secret-arn> -ArchiveBucketName <bucket>
```

The recorder starts with:

```text
--operator-approved-market-data-fix-logon --no-order-entry --no-account-api --no-db
```

## 7. Publish Metrics And Upload

After the bounded capture exits cleanly:

```powershell
.\Publish-AnubisAws1Metrics.ps1 -RecorderRoot D:\Anubis\Recorder
.\Invoke-AnubisAws1ChunkUpload.ps1 -BucketName <bucket> -RecorderRoot D:\Anubis\Recorder
```

Metrics with missing evidence are reported as `NOT_EVALUATED` and are not published to CloudWatch. Upload verifies local chunk size/SHA from `final_manifest.json` and remote S3 `ChecksumSHA256`; it performs no deletion.
