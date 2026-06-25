# AWS1 Test Report

Generated local JSON report:

```text
artifacts/readiness/anubis-aws1-read-only-shadow-foundation-plan-ready/AWS1_TEST_REPORT.generated.json
```

The plan-ready package includes a copy at:

```text
artifacts/readiness/AWS1_TEST_REPORT.generated.json
```

## Toolchain

- Terraform: `1.10.5` official Windows amd64 zip, SHA-256 verified against HashiCorp `SHA256SUMS`.
- AWS provider lock: `hashicorp/aws` `5.100.0` in `.terraform.lock.hcl`.

## Results Run In This Worktree

- Reviewed no-apply reference artifact SHA-256 verified: `1B71CB16966AF525456A270C8AD2020931EF1829FF13C699180741C62FE89B84`.
- App source commit recorded: `7e87f3b17c84ac8a0aeb79422e4caa97b915fbb6`.
- `terraform fmt -check -recursive`: PASS.
- `terraform init -backend=false`: PASS.
- `terraform validate`: PASS.
- PowerShell parse: PASS.
- `ssm/aws1-install-runbook.json` parse: PASS.
- Status fixture using real `final_manifest.json`, `m2c1b_capture_manifest.json`, DQ report, and manifest-listed chunk: PASS.
- No EC2 recorder ingress: PASS.
- No AWS apply commands: PASS.
- No RDS initial path: PASS.
- No order mutation surface: PASS.
- No forbidden data vendor paths: PASS.
- No secret values: PASS.
- No `Start-Job`: PASS.
- Self-contained package controls: PASS.
- Verified self-contained launcher/PID ownership checks: PASS.
- Manifest-only S3 upload with checksum verification: PASS.
- Watchdog observer-only smoke-mode contract: PASS.
- Backend, S3 protection, AMI fail-closed, alarm-action, broker-CIDR, and EBS bootstrap checks: PASS.

## Gate

```text
GO_AWS1_PLAN_READ_ONLY
```

No AWS apply, push, or merge was performed.
