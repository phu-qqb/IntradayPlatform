# AWS1 Monitoring And Alerts

## Metrics

Namespace:

```text
Anubis/AWS1
```

Required metrics implemented by `Publish-AnubisAws1Metrics.ps1` and Terraform alarms:

- `ProcessAlive`
- `SessionStateOk`
- `BboCount`
- `LastQuoteAgeSeconds`
- `SequenceGapStatus`
- `WriterErrors`
- `Drops`
- `DiskFreePercent`
- `S3UploadBacklog`
- `ClockHealthOk`
- `RecorderShadowReady`

Dimensions:

- `Environment`
- `HostRole=m2-capture-only`

## Alarms

Terraform creates one alarm per required metric. All alarms use `treat_missing_data = "breaching"` so missing telemetry is fail-closed.

Alarm actions are parameterized with `alarm_action_arns`. Empty actions are allowed for dry-run infrastructure review but must be filled before production apply.

## Clock Health

User-data configures Windows Time against Amazon Time Sync:

```text
169.254.169.123
```

The status script checks `w32tm /query /status` and publishes `ClockHealthOk`.

## Raw Data Exclusion

The metric script does not send raw ticks, raw FIX frames, or chunk content to CloudWatch. S3 remains the archive path for finalized raw capture artifacts.
