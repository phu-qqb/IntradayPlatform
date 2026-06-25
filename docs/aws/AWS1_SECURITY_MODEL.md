# AWS1 Security Model

## Identity

The EC2 host receives one IAM role:

- SSM managed instance core for Session Manager;
- least-privilege S3 read/write to the single archive/artifact bucket;
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
- Outbound broker access requires explicit CIDRs and rejects `/0`.
- Scheduled task runs under explicit `SYSTEM` principal and is disabled unless the operator opts into autostart.
- Single-instance guard uses PID, executable path, and process start time.
- AWS CLI v2 is installed from a verified MSI artifact rather than assumed from the AMI.

## Data Controls

- EBS spool is encrypted and protected from Terraform destroy.
- S3 archive is versioned, encrypted with SSE-S3, TLS-only, and protected from Terraform destroy.
- Raw tick/chunk content is not sent to CloudWatch.
- S3 upload verifies local manifest-listed files and remote S3 `ChecksumSHA256` before marking local runs uploaded.

## Fail-Closed Rules

- Missing broker CIDRs blocks Terraform apply through a precondition.
- Missing or invalid AMI ID/owner/platform/architecture blocks Terraform validation or planning.
- `artifact_bucket_name` must be null; deployment artifacts and archives use the same bucket/IAM surface.
- Missing credential secret ID or secret labels prevents recorder start.
- Missing metric evidence becomes `NOT_EVALUATED` and is not published as a false OK value.
- Low disk or unhealthy clock causes watchdog fail-closed actions instead of blind restart.
