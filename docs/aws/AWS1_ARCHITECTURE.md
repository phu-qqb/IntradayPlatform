# AWS1 Architecture

AWS1 is a plan-ready, read-only shadow foundation for running the M2C1B LMAX Demo market-data-only recorder in `eu-west-2`.

## Baseline

- App source commit: `7e87f3b17c84ac8a0aeb79422e4caa97b915fbb6`
- Reviewed AWS1 no-apply reference artifact SHA-256: `1B71CB16966AF525456A270C8AD2020931EF1829FF13C699180741C62FE89B84`
- Host project: `tools/QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly`
- Runtime: `win-x64`, self-contained .NET publish
- Operation mode: `SMOKE_CAPTURE_BOUNDED`

## Resources

- One private Windows EC2 instance.
- One encrypted gp3 EBS data volume mounted as the recorder spool after fail-closed bootstrap checks.
- One versioned SSE-S3 bucket for deployment artifacts, finalized chunks, and manifests.
- IAM instance role with SSM, S3 archive, CloudWatch metrics/logs, endpoint-alias parameter read, and market-data-only secret read.
- Secrets Manager secret metadata for LMAX market-data-only credentials. Terraform never creates a secret value.
- CloudWatch log groups and optional metric alarms. Alarms are disabled by default until smoke metrics are operationally scheduled.
- SSM command document for verified AWS CLI MSI install and verified app artifact install.
- Optional NAT gateway with Elastic IP for stable broker egress.
- Private AWS endpoints for SSM, S3, CloudWatch, Secrets Manager, and KMS when enabled.

## Runtime Flow

1. Operator builds the plan-ready artifact zip.
2. Artifact and AWS CLI MSI are staged to the single archive/artifact bucket after review.
3. SSM runbook downloads both artifacts with `aws:downloadContent` and verifies SHA-256 locally.
4. Install script expands the self-contained release, materializes the M2 config, registers an explicit `SYSTEM` scheduled task, disables autostart by default, and writes an install manifest.
5. Start script reads Secrets Manager at runtime, injects credential labels into the child process environment only, and starts the self-contained capture host with mutation-disabled flags.
6. The wrapper remains alive through bounded finalization, drains stdout/stderr after the child exits, records verified PID state, and writes sanitized last-run state with both the raw child exit code and the artifact verdict.
7. The wrapper exits 0 only when recorder artifacts validate GO; missing, stale, or incoherent artifacts keep the wrapper non-zero even if the child process returned 0.
8. Post-run status and metrics are computed from `final_manifest.json`, `m2c1b_capture_manifest.json`, `health/data_quality_report.json`, and manifest-listed chunks.
9. Upload is an explicit post-capture/runbook step. It stores only `final_manifest.json` plus chunks listed in the final manifest under `environment=<env>/date=<yyyy-mm-dd>/recorder_run=<id>/...`, verifies S3 `ChecksumSHA256`, and writes the local archive marker only after every file is remotely verified.

## Boundary

AWS1 does not add Order Entry, Account API, OMS/PMS mutation, canonical RDS, Databento, Bloomberg EMSX, Morgan Stanley, AWS apply, push, or merge. Continuous recorder watchdog behavior remains out of scope for this gate.

