# AWS1 Security Model

## Identity

The EC2 host receives one IAM role:

- SSM managed instance core for Session Manager;
- least-privilege S3 read/write to the archive bucket;
- read-only access to one Secrets Manager market-data-only secret;
- read access to one SSM endpoint-alias parameter;
- CloudWatch log and metric write permissions.

No wildcard write access is granted to account infrastructure, IAM, EC2 mutation APIs, RDS, or order systems.

## Secrets

Credential values are not stored in:

- Terraform variables or state by AWS1 design;
- user-data;
- AMIs;
- repository files;
- logs;
- deployment manifests.

The secret value is expected to be a JSON object with these labels:

```json
{
  "LMAX_DEMO_SENDER_COMP_ID": "...",
  "LMAX_DEMO_TARGET_COMP_ID": "...",
  "LMAX_DEMO_FIX_USERNAME": "...",
  "LMAX_DEMO_FIX_PASSWORD": "..."
}
```

The start script reads the secret at process start and injects values only into the child process environment.

## Host Controls

- No public IP.
- No EC2 security group ingress.
- SSM Session Manager for administration.
- IMDSv2 required.
- Windows Firewall inbound default is blocked.
- Outbound broker access requires explicit CIDRs.
- Scheduled task is disabled unless the operator opts into autostart.
- Single-instance guard uses a PID file and process check.

## Data Controls

- EBS spool is encrypted.
- S3 archive is versioned and encrypted with SSE-S3.
- Raw tick/chunk content is not sent to CloudWatch.
- S3 upload writes SHA-256 metadata and marks local runs uploaded only after remote metadata verification.

## Fail-Closed Rules

- Missing broker CIDRs blocks Terraform apply through a precondition.
- Missing Terraform validation keeps the final gate at `NO_GO_AWS1`.
- Missing credential secret ID or secret labels prevents recorder start.
- Low disk or unhealthy clock causes watchdog fail-closed actions instead of blind restart.
