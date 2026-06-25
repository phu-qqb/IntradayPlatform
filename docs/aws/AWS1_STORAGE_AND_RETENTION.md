# AWS1 Storage And Retention

## Local Spool

Recorder output is written to a dedicated encrypted gp3 EBS volume:

```text
D:\Anubis\Recorder
```

Bootstrap behavior is fail-closed:

- wait for the expected non-boot EBS volume with a bounded timeout;
- reject ambiguous disks and drive-letter conflicts;
- initialize/partition/format idempotently;
- assign the target drive letter only after the correct volume is identified;
- verify the expected volume label;
- create the recorder root only after spool success;
- write `C:\Anubis\State\aws1_bootstrap.json`.

Local finalized runs are never deleted by the AWS1 upload script. Local retention is a documented operator policy, default `14` days, not an automatic delete in AWS1.

## S3 Archive

The archive bucket is also the deployment artifact bucket. It is:

- versioned;
- encrypted with SSE-S3;
- blocked from public access;
- protected by a TLS-only bucket policy;
- configured to abort incomplete multipart uploads after 7 days;
- protected from Terraform destroy by lifecycle `prevent_destroy`.

SSE-S3 is chosen for AWS1 to reduce key-policy complexity while still making encryption explicit. Customer-managed KMS can be introduced in a later hardening step if key separation is required.

## Upload Verification

`Invoke-AnubisAws1ChunkUpload.ps1` uploads only finalized run files proven by `final_manifest.json`:

1. `final_manifest.json`;
2. each chunk listed in `final_manifest.json`.

For each chunk, the script verifies local size and SHA-256 before upload. Each S3 key includes:

```text
<Prefix>/environment=<env>/date=<yyyy-mm-dd>/recorder_run=<run_id>/<relative_path>
```

The script uses `s3api put-object --checksum-algorithm SHA256 --checksum-sha256 <base64>` and verifies with `head-object --checksum-mode ENABLED`. `.s3_upload_verified` is written only after every object in the run passes checksum verification. No local deletion is performed in AWS1.

## CloudWatch Boundary

CloudWatch receives process, health, count, age, backlog, disk, and clock metrics only. Raw tick payloads and raw FIX messages are not logged to CloudWatch.
