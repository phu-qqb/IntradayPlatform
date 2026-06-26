resource "aws_cloudwatch_log_group" "recorder" {
  name              = "/qq-fund-platform/${local.safe_environment}/aws1/recorder"
  retention_in_days = 30
}

resource "aws_cloudwatch_log_group" "install" {
  name              = "/qq-fund-platform/${local.safe_environment}/aws1/install"
  retention_in_days = 30
}

locals {
  metric_alarms = {
    process_alive = {
      metric_name        = "ProcessAlive"
      comparison         = "LessThanThreshold"
      threshold          = 1
      statistic          = "Minimum"
      period             = 60
      evaluation_periods = 2
      description        = "M2 capture host process is not alive."
    }
    session_state = {
      metric_name        = "SessionStateOk"
      comparison         = "LessThanThreshold"
      threshold          = 1
      statistic          = "Minimum"
      period             = 60
      evaluation_periods = 2
      description        = "Market-data session is not in an acceptable read-only state."
    }
    bbo_count = {
      metric_name        = "BboCount"
      comparison         = "LessThanThreshold"
      threshold          = 1
      statistic          = "Maximum"
      period             = 300
      evaluation_periods = 1
      description        = "No BBO updates observed in the evaluation window."
    }
    last_quote_age = {
      metric_name        = "LastQuoteAgeSeconds"
      comparison         = "GreaterThanThreshold"
      threshold          = 60
      statistic          = "Maximum"
      period             = 60
      evaluation_periods = 2
      description        = "Last quote is stale."
    }
    sequence_gap_status = {
      metric_name        = "SequenceGapStatus"
      comparison         = "GreaterThanThreshold"
      threshold          = 0
      statistic          = "Maximum"
      period             = 60
      evaluation_periods = 1
      description        = "FIX market-data sequence gap or out-of-order status detected."
    }
    writer_errors = {
      metric_name        = "WriterErrors"
      comparison         = "GreaterThanThreshold"
      threshold          = 0
      statistic          = "Maximum"
      period             = 60
      evaluation_periods = 1
      description        = "Recorder writer errors detected."
    }
    drops = {
      metric_name        = "Drops"
      comparison         = "GreaterThanThreshold"
      threshold          = 0
      statistic          = "Maximum"
      period             = 60
      evaluation_periods = 1
      description        = "Recorder dropped events detected."
    }
    disk_free = {
      metric_name        = "DiskFreePercent"
      comparison         = "LessThanThreshold"
      threshold          = 20
      statistic          = "Minimum"
      period             = 60
      evaluation_periods = 2
      description        = "Recorder spool disk free space is low."
    }
    s3_upload_backlog = {
      metric_name        = "S3UploadBacklog"
      comparison         = "GreaterThanThreshold"
      threshold          = 0
      statistic          = "Maximum"
      period             = 300
      evaluation_periods = 3
      description        = "Finalized chunks are waiting for verified S3 upload."
    }
    clock_health = {
      metric_name        = "ClockHealthOk"
      comparison         = "LessThanThreshold"
      threshold          = 1
      statistic          = "Minimum"
      period             = 60
      evaluation_periods = 2
      description        = "Windows clock is not healthy against Amazon Time Sync."
    }
    recorder_shadow_ready = {
      metric_name        = "RecorderShadowReady"
      comparison         = "LessThanThreshold"
      threshold          = 1
      statistic          = "Minimum"
      period             = 60
      evaluation_periods = 2
      description        = "Recorder is not shadow-ready."
    }
  }
}

resource "aws_cloudwatch_metric_alarm" "recorder" {
  for_each            = var.enable_cloudwatch_alarms ? local.metric_alarms : {}
  alarm_name          = "${local.name_prefix}-${each.key}"
  alarm_description   = each.value.description
  namespace           = var.cloudwatch_namespace
  metric_name         = each.value.metric_name
  statistic           = each.value.statistic
  period              = each.value.period
  evaluation_periods  = each.value.evaluation_periods
  threshold           = each.value.threshold
  comparison_operator = each.value.comparison
  treat_missing_data  = "breaching"
  alarm_actions       = var.alarm_action_arns
  ok_actions          = var.alarm_action_arns

  dimensions = {
    Environment = local.safe_environment
    HostRole    = "m2-capture-only"
  }
}
