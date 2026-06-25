# AWS1 Deployment Scripts

These scripts package, install, operate, and roll back the M2C1B LMAX Demo market-data-only capture host.

Important boundaries:

- no credentials are stored in the repository, AMI, user-data, or logs;
- the recorder is started with `--no-order-entry --no-account-api --no-db`;
- Secrets Manager is read at runtime into process environment variables only;
- S3 upload verifies remote SHA-256 metadata before marking local chunks as uploaded;
- local spool deletion is not automatic in AWS1.

Typical local packaging:

```powershell
.\deploy\aws\anubis-shadow\scripts\Package-M2CaptureHost.ps1
```

Typical local static gate:

```powershell
.\deploy\aws\anubis-shadow\scripts\Test-AnubisAws1Local.ps1
```
