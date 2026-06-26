data "aws_caller_identity" "current" {}

data "aws_availability_zones" "available" {
  state = "available"
}

data "aws_ami" "recorder" {
  owners = var.allowed_ami_owner_ids

  filter {
    name   = "image-id"
    values = [var.ami_id]
  }

  filter {
    name   = "state"
    values = ["available"]
  }

  filter {
    name   = "architecture"
    values = ["x86_64"]
  }

  filter {
    name   = "platform"
    values = ["windows"]
  }
}

locals {
  account_id          = data.aws_caller_identity.current.account_id
  selected_az         = coalesce(var.availability_zone, data.aws_availability_zones.available.names[0])
  safe_environment    = lower(var.environment)
  name_prefix         = "${var.project_name}-${local.safe_environment}-aws1"
  archive_bucket_name = coalesce(var.archive_bucket_name, "${local.name_prefix}-archive-${local.account_id}-${var.region}")
  artifact_bucket     = local.archive_bucket_name
  credential_secret_arn = coalesce(
    var.credential_secret_arn,
    try(aws_secretsmanager_secret.market_data_only[0].arn, null)
  )

  tags = {
    Project         = "qq-fund-platform"
    Workstream      = "AWS_READ_ONLY"
    Environment     = local.safe_environment
    Boundary        = "market-data-read-only"
    OperationMode   = var.operation_mode
    OrderEntry      = "disabled"
    ManagedBy       = "terraform"
    AppSourceCommit = "7e87f3b17c84ac8a0aeb79422e4caa97b915fbb6"
    ApplyGate       = "manual-review-required"
  }
}

resource "aws_instance" "recorder" {
  ami                         = data.aws_ami.recorder.id
  instance_type               = var.instance_type
  subnet_id                   = aws_subnet.private.id
  vpc_security_group_ids      = [aws_security_group.recorder.id]
  iam_instance_profile        = aws_iam_instance_profile.recorder.name
  associate_public_ip_address = false
  monitoring                  = true
  user_data_replace_on_change = true
  user_data = templatefile("${path.module}/user_data.ps1.tftpl", {
    install_root         = var.install_root
    recorder_root        = var.recorder_root
    environment          = local.safe_environment
    cloudwatch_namespace = var.cloudwatch_namespace
    local_retention_days = var.local_retention_days
    ebs_size_gb          = var.ebs_size_gb
    spool_volume_label   = var.spool_volume_label
  })

  metadata_options {
    http_endpoint               = "enabled"
    http_tokens                 = "required"
    http_put_response_hop_limit = 1
  }

  root_block_device {
    encrypted   = true
    volume_type = "gp3"
    volume_size = 80
  }

  lifecycle {
    precondition {
      condition     = length(var.lmax_market_data_egress_cidrs) > 0
      error_message = "lmax_market_data_egress_cidrs must be set before plan/apply so broker egress is explicit."
    }
    precondition {
      condition     = local.lmax_market_data_planned_cidrs == local.lmax_market_data_dns_resolved_cidrs
      error_message = "lmax_market_data_egress_cidrs must exactly match current DNS A-record /32 CIDRs for the approved LMAX Demo market-data endpoint. Re-resolve before apply."
    }
    precondition {
      condition     = var.lmax_market_data_egress_cidr_source == "DNS_RESOLVED_CURRENT_LMAX_DEMO_MARKETDATA_ENDPOINT" && var.lmax_market_data_egress_cidr_stability == "NOT_CONTRACTUALLY_GUARANTEED" && var.lmax_market_data_egress_apply_requires_revalidation
      error_message = "DNS-resolved LMAX Demo market-data CIDRs must be documented as non-contractual and apply-revalidated."
    }
    precondition {
      condition     = !var.enable_cloudwatch_alarms || length(var.alarm_action_arns) > 0
      error_message = "alarm_action_arns must be provided when enable_cloudwatch_alarms is true."
    }
  }

  tags = {
    Name = "${local.name_prefix}-windows-recorder"
  }
}

resource "aws_ebs_volume" "spool" {
  availability_zone = local.selected_az
  size              = var.ebs_size_gb
  type              = "gp3"
  encrypted         = true

  lifecycle {
    prevent_destroy = true
  }

  tags = {
    Name    = "${local.name_prefix}-spool"
    Purpose = "append-only-recorder-spool"
  }
}

resource "aws_volume_attachment" "spool" {
  device_name = "/dev/sdf"
  volume_id   = aws_ebs_volume.spool.id
  instance_id = aws_instance.recorder.id
}
