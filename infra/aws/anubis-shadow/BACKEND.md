# AWS1 Terraform Backend

AWS1 must not be applied with local state. The backend is intentionally a partial S3 backend configuration:

```hcl
terraform {
  backend "s3" {
    encrypt      = true
    use_lockfile = true
  }
}
```

Before any future `terraform plan`, the lead/operator must provide backend config outside the repository, for example:

```powershell
terraform init `
  -backend-config="bucket=<state-bucket>" `
  -backend-config="key=fund-platform/shadow-readonly/eu-west-2/terraform.tfstate" `
  -backend-config="region=eu-west-2" `
  -backend-config="encrypt=true" `
  -backend-config="use_lockfile=true"
```

Backend prerequisites:

- the state bucket already exists;
- bucket encryption is enabled;
- bucket versioning is enabled;
- TLS-only bucket policy is enabled;
- Terraform version is `>= 1.10.0` so S3 lockfile locking is supported;
- no AWS1 apply is run from an unreviewed local backend.
