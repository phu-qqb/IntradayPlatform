# qq-fund-platform AWS1 Read-Only Shadow Foundation

This Terraform stack prepares the AWS London foundation for the M2C1B LMAX Demo market-data-only smoke capture host.

Safety boundaries:

- no EC2 ingress rules;
- SSM Session Manager only for administration;
- market-data broker egress requires explicit CIDRs and rejects `/0`;
- AMI resolution is constrained by ID, owner allow-list, Windows platform, x86_64 architecture, and `available` state;
- no credential values in Terraform, user-data, logs, or repository files;
- a single archive/artifact bucket is used for deployment artifacts and finalized capture uploads;
- no Order Entry, Account API, RDS, Databento, Bloomberg EMSX, or Morgan Stanley components;
- no `terraform apply` is part of AWS1.

State model:

- `backend.tf` declares a partial S3 backend with encryption and Terraform lockfile support;
- backend bucket/key/region are supplied out of band during a future plan/apply ceremony;
- no AWS1 apply should be performed with local state.

Local validation with the verified Terraform binary:

```powershell
$tf = "artifacts\tools\terraform\1.10.5\terraform.exe"
Push-Location infra\aws\anubis-shadow
& $tf fmt -check -recursive
& $tf init -backend=false
& $tf validate
Pop-Location
```

The provider lock file is committed as `.terraform.lock.hcl`; the provider cache directory remains ignored.
