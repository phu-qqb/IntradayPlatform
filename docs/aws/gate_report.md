# AWS1 Gate Report

Gate:

```text
NO_GO_AWS1
```

## Reason

The AWS1 read-only foundation, deployment scripts, documentation, and deterministic artifact were prepared, but local mandatory Terraform validation could not be completed because Terraform is not installed in this environment.

## Completed

- Control pack read.
- M2 package SHA-256 verified.
- Dedicated AWS1 branch/worktree created from `7e87f3b17c84ac8a0aeb79422e4caa97b915fbb6`.
- Existing infra audit completed.
- Terraform foundation added with no EC2 recorder ingress and no RDS.
- Deployment scripts added for packaging, install, start, stop, status, metrics, S3 upload verification, watchdog, and rollback.
- Config hash stamping verified against the known M2 hash.
- Focused M2 market-data-only unit tests passed: 54/54.
- Deployment artifact packaging is deterministic; the final ZIP hash is written to `anubis_aws1_read_only_shadow_foundation_no_apply.zip.sha256`.

## Gaps To Close Before GO

- Install Terraform and run `terraform fmt`, `terraform init -backend=false`, and `terraform validate`.
- Rerun `Test-AnubisAws1Local.ps1` with no failures and no skips.
- Supply apply-time values: Windows AMI, LMAX market-data egress CIDRs, alarm actions, and artifact S3 URI.
- Review generated artifact SHA-256 and deployment manifest after final packaging.

No AWS apply, push, or merge was performed.
