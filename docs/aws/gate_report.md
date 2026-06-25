# AWS1 Gate Report

Gate:

```text
GO_AWS1_PLAN_READ_ONLY
```

## Basis

The AWS1 foundation has been closed out for future read-only `terraform plan` preparation. P0 operational blockers from the lead review are addressed in code, Terraform, scripts, docs, and local validation evidence.

## Completed

- Baseline commit verified: `9f606beacbaab1a963a0f060e1ca3ebd09e6aba2`.
- Reviewed no-apply reference package SHA-256 verified: `1B71CB16966AF525456A270C8AD2020931EF1829FF13C699180741C62FE89B84`.
- Host package publishes `win-x64 --self-contained true` and launcher verifies the executable SHA-256.
- EBS bootstrap waits for the expected spool disk, rejects drive conflicts, verifies label, writes bootstrap report, and fails closed.
- Wrapper owns the child process, drains stdout/stderr without `Start-Job`, records verified PID JSON, and returns the child exit code.
- Stop path verifies PID, executable path, and start time; force kill requires explicit `-ForceAfterTimeout`.
- AWS CLI v2 is provisioned from a verified MSI artifact through SSM/downloadContent.
- Metrics are artifact-based and use `NOT_EVALUATED` for missing evidence.
- Watchdog is observer-only for `SMOKE_CAPTURE_BOUNDED`; continuous restart is out of scope.
- S3 upload uses only `final_manifest.json` plus listed chunks and verifies S3 `ChecksumSHA256`.
- Single archive/artifact bucket, TLS-only policy, abort multipart, versioning, encryption, and prevent-destroy controls are present.
- Remote encrypted S3 backend with lockfile support is declared and documented.
- Terraform `fmt -check`, `init -backend=false`, and `validate` pass with verified Terraform `1.10.5`.
- `.terraform.lock.hcl` is present for `hashicorp/aws` `5.100.0`.
- AMI, broker CIDR, alarm action, endpoint alias, and credential secret validations fail closed.

## Remaining Before Any AWS Apply

- Separate approval for AWS apply.
- Remote backend bucket bootstrapped out of band.
- Approved AMI ID/owner and explicit broker CIDRs supplied.
- App artifact and AWS CLI MSI staged to the archive/artifact bucket with reviewed hashes.
- Market-data-only secret value populated out of band.
- Alarm actions supplied only if alarms are enabled.

No AWS apply, push, or merge was performed.
