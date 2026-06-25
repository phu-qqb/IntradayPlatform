# Anubis AWS1 Read-Only Shadow Foundation

This Terraform stack prepares the AWS London foundation for the M2C1B LMAX Demo market-data-only capture host.

Safety boundaries:

- no EC2 ingress rules;
- SSM Session Manager only for administration;
- market-data broker egress requires explicit CIDRs;
- no credential values in Terraform, user-data, logs, or repository files;
- no Order Entry, Account API, RDS, Databento, Bloomberg EMSX, or Morgan Stanley components;
- no `terraform apply` is part of AWS1.

Local validation:

```powershell
terraform fmt -check -recursive infra/aws/anubis-shadow
terraform init -backend=false infra/aws/anubis-shadow
terraform validate infra/aws/anubis-shadow
```

The local AWS1 test script records these as skipped when Terraform is not installed.
