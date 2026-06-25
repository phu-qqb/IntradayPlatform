# AWS1 Deployment Scripts

These scripts package, install, operate, and roll back the M2C1B LMAX Demo market-data-only capture host for AWS1 plan-ready review.

Important boundaries:

- operation mode is `SMOKE_CAPTURE_BOUNDED`;
- the host package is `win-x64` self-contained and does not depend on a preinstalled .NET runtime;
- no credentials are stored in the repository, AMI, user-data, package manifests, or logs;
- the recorder is started with `--no-order-entry --no-account-api --no-db`;
- Secrets Manager is read at runtime into process environment variables only;
- AWS CLI v2 is provisioned through a verified MSI artifact, not assumed from the AMI;
- S3 upload is driven by `final_manifest.json` and verifies S3 `ChecksumSHA256` before writing `.s3_upload_verified`;
- local spool deletion is not automatic in AWS1;
- no continuous restart watchdog is claimed for this gate.

Typical local packaging:

```powershell
.\deploy\aws\anubis-shadow\scripts\Package-M2CaptureHost.ps1
```

Typical local static gate with the verified Terraform binary:

```powershell
.\deploy\aws\anubis-shadow\scripts\Test-AnubisAws1Local.ps1 -TerraformPath artifacts\tools\terraform\1.10.5\terraform.exe
```
