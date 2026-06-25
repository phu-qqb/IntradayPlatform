# AWS1 Monitoring And Alerts

## Metrics

Namespace:

```text
Anubis/AWS1
```

`Get-AnubisAws1Status.ps1` computes status from real artifacts only:

- `final_manifest.json`;
- `m2c1b_capture_manifest.json`;
- `health/data_quality_report.json`;
- chunks listed in the final manifest;
- verified PID JSON;
- Windows Time status.

Metrics:

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

Every metric carries `evaluation_status`. Missing evidence becomes `NOT_EVALUATED`, never a synthetic zero or OK value. `Publish-AnubisAws1Metrics.ps1` publishes only metrics whose status is `EVALUATED`.

Dimensions:

- `Environment`
- `HostRole=m2-capture-only`
- `OperationMode=SMOKE_CAPTURE_BOUNDED`

## Alarms

Terraform declares alarms for the metric set, but `enable_cloudwatch_alarms` defaults to `false`. This prevents AWS1 from creating alarms on metrics until post-run publishing is scheduled and operationally approved.

If alarms are enabled later, `alarm_action_arns` must be non-empty and each alarm uses `treat_missing_data = "breaching"`.

## Clock Health

User-data configures Windows Time against Amazon Time Sync:

```text
169.254.169.123
```

`ClockHealthOk` is `EVALUATED=1` only when `w32tm /query /status` identifies Amazon Time Sync or `169.254.169.123`. Local CMOS or missing source does not pass.

## Smoke Mode

AWS1 plan-ready mode is bounded smoke capture. The watchdog script is observer-only and returns `NO_GO_CONTINUOUS_WATCHDOG_OUT_OF_SCOPE` if asked to restart a stopped process. Continuous recorder orchestration remains a later scope.

## Raw Data Exclusion

The metric script does not send raw ticks, raw FIX frames, or chunk content to CloudWatch. S3 remains the archive path for finalized raw capture artifacts.
