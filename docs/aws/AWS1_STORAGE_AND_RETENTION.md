# AWS1 Storage And Retention

## Local Spool

Recorder output is written to a dedicated encrypted gp3 EBS volume:

```text
D:\Anubis\Recorder
```

The path is configurable through Terraform and install script parameters.

Local write behavior:

- append-only recorder chunks;
- chunk rotation follows M2 config;
- local finalized runs are never deleted by the AWS1 upload script;
- local retention is a documented operator policy, default `14` days, not an automatic delete in AWS1.

## S3 Archive

The archive bucket is:

- versioned;
- encrypted with SSE-S3;
- blocked from public access;
- used for finalized chunks, manifests, upload markers, and deployment artifacts.

SSE-S3 is chosen for AWS1 to reduce key-policy complexity while still making encryption explicit. Customer-managed KMS can be introduced in a later AWS hardening step if key separation is required.

## Upload Verification

`Invoke-AnubisAws1ChunkUpload.ps1` uploads finalized files only after `final_manifest.json` exists. For each uploaded file it:

1. computes local SHA-256;
2. uploads the object with `sha256` metadata;
3. reads the remote metadata back;
4. writes `.s3_upload_verified` only when metadata matches.

No local deletion is performed in AWS1.

## CloudWatch Boundary

CloudWatch receives process, health, count, age, backlog, disk, and clock metrics only. Raw tick payloads and raw FIX messages are not logged to CloudWatch.
