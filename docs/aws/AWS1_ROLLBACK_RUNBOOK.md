# AWS1 Rollback Runbook

Rollback is local to the Windows host and does not mutate business pipelines.

## Triggers

- recorder process fails repeatedly;
- clock health alarm;
- writer errors or drops;
- S3 upload verification failure;
- unexpected M2 preflight failure;
- operator detects any forbidden runtime surface.

## Steps

1. Stop recorder:

```powershell
.\Stop-AnubisAws1Recorder.ps1
```

2. Preserve local spool:

```text
D:\Anubis\Recorder
```

Do not delete local chunks.

3. Roll back release:

```powershell
.\Invoke-AnubisAws1Rollback.ps1
```

or specify a release:

```powershell
.\Invoke-AnubisAws1Rollback.ps1 -TargetReleaseId <sha-prefix>
```

4. Verify status:

```powershell
.\Get-AnubisAws1Status.ps1
```

5. Restart only after operator approval.

## Data Safety

Rollback does not:

- delete local recorder data;
- delete S3 objects;
- rotate or reveal credentials;
- enable Order Entry;
- mutate OMS/PMS state.
