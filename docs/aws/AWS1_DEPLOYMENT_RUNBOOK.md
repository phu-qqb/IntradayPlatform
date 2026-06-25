# AWS1 Deployment Runbook

This runbook is ready for review but AWS1 does not perform `terraform apply`.

## 1. Build Artifact

From the AWS1 worktree:

```powershell
.\deploy\aws\anubis-shadow\scripts\Package-M2CaptureHost.ps1
```

Output:

```text
artifacts\readiness\anubis-aws1-read-only-shadow-foundation-no-apply\package\anubis_aws1_read_only_shadow_foundation_no_apply.zip
```

The script publishes the M2 capture-only host, embeds deploy/infra/docs, writes `deployment_manifest.json`, and emits the artifact SHA-256.

## 2. Local Gates

```powershell
.\deploy\aws\anubis-shadow\scripts\Test-AnubisAws1Local.ps1
```

Expected before apply approval:

- Terraform installed;
- `terraform fmt -check -recursive`;
- `terraform init -backend=false`;
- `terraform validate`;
- PowerShell parse checks;
- no EC2 ingress;
- no AWS apply command;
- no RDS initial path;
- no order mutation surface;
- no forbidden data vendor path;
- no secret values.

## 3. Operator Inputs

Required apply-time values:

- Windows AMI ID;
- explicit LMAX market-data egress CIDRs;
- alarm action ARNs;
- artifact S3 URI and SHA-256;
- market-data-only secret value populated out of band.

## 4. Terraform Review

```powershell
terraform init -backend=false infra\aws\anubis-shadow
terraform validate infra\aws\anubis-shadow
```

Remote plan/apply is outside AWS1 until the lead approves `GO_AWS1_APPLY_READ_ONLY`.

## 5. Install Through SSM

After an approved apply in a later step, use the generated SSM document:

```text
anubis-demo-aws1-install-runbook
```

The runbook downloads the artifact, verifies SHA-256, expands it, and invokes `Install-AnubisAws1Host.ps1`.

Autostart defaults to disabled. Enable only after operator review.

## 6. Start

```powershell
.\Start-AnubisAws1Recorder.ps1 -CredentialSecretId <secret-arn> -ArchiveBucketName <bucket>
```

The recorder starts with:

```text
--operator-approved-market-data-fix-logon --no-order-entry --no-account-api --no-db
```

## 7. Upload Finalized Runs

```powershell
.\Invoke-AnubisAws1ChunkUpload.ps1 -BucketName <bucket>
```

The script verifies remote SHA-256 metadata before marking local runs uploaded and performs no deletion.
