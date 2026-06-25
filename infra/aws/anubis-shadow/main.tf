data "aws_caller_identity" "current" {}

data "aws_availability_zones" "available" {
  state = "available"
}

locals {
  account_id          = data.aws_caller_identity.current.account_id
  selected_az         = coalesce(var.availability_zone, data.aws_availability_zones.available.names[0])
  safe_environment    = lower(var.environment)
  name_prefix         = "${var.project_name}-${local.safe_environment}-aws1"
  archive_bucket_name = coalesce(var.archive_bucket_name, "${local.name_prefix}-archive-${local.account_id}-${var.region}")
  artifact_bucket     = coalesce(var.artifact_bucket_name, local.archive_bucket_name)
  credential_secret_arn = coalesce(
    var.credential_secret_arn,
    try(aws_secretsmanager_secret.market_data_only[0].arn, null)
  )

  tags = {
    Project          = "Anubis"
    Workstream       = "AWS_READ_ONLY"
    Environment      = local.safe_environment
    Boundary         = "market-data-read-only"
    OrderEntry       = "disabled"
    ManagedBy        = "terraform"
    BaselineCommit   = "7e87f3b17c84ac8a0aeb79422e4caa97b915fbb6"
    ApplyGate        = "manual-review-required"
  }
}

resource "aws_instance" "recorder" {
  ami                         = var.ami_id
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
      error_message = "lmax_market_data_egress_cidrs must be set before apply so broker egress is explicit."
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
