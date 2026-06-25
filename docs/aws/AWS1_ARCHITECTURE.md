# AWS1 Architecture

AWS1 prepares a no-apply foundation for running the M2C1B LMAX Demo market-data-only recorder in `eu-west-2`.

## Baseline

- M2 final commit: `7e87f3b17c84ac8a0aeb79422e4caa97b915fbb6`
- M2 package SHA-256: `F1F024563F29544124049A1CF7A980A93C7ED25842F71752F9F6A04E862163C8`
- Host project: `tools/QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly`

## Resources

- One private Windows EC2 instance.
- One encrypted gp3 EBS data volume mounted as the recorder spool.
- One versioned SSE-S3 bucket for finalized chunks, manifests, and deployment artifacts.
- IAM instance role with SSM, S3 archive, CloudWatch metrics/logs, endpoint-alias parameter read, and market-data-only secret read.
- Secrets Manager secret metadata for LMAX market-data-only credentials. Terraform never creates a secret value.
- CloudWatch log groups, custom metric alarms, and fail-closed missing-data behavior.
- SSM command document for artifact install.
- Optional NAT gateway with Elastic IP for stable broker egress.
- Private AWS endpoints for SSM, S3, CloudWatch, Secrets Manager, and KMS when enabled.

## Runtime Flow

1. Operator builds a deterministic AWS1 artifact zip.
2. Artifact is uploaded to the archive/artifact bucket after review.
3. SSM runbook downloads the artifact and verifies SHA-256.
4. Install script expands the release, materializes the M2 config, registers a disabled scheduled task by default, and writes an install manifest.
5. Start script reads Secrets Manager at runtime, injects credential labels into the child process environment only, and starts the M2 capture host with mutation-disabled flags.
6. Finalized chunks remain local until the upload script stores them in S3 and verifies remote SHA-256 metadata.

## Boundary

AWS1 is capture-only. It does not add Order Entry, Account API, OMS/PMS mutation, canonical RDS, Databento, Bloomberg EMSX, Morgan Stanley, AWS apply, push, or merge.
