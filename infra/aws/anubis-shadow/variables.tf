variable "region" {
  description = "AWS region for AWS1. The initial target is London."
  type        = string
  default     = "eu-west-2"

  validation {
    condition     = var.region == "eu-west-2"
    error_message = "AWS1 is scoped to eu-west-2."
  }
}

variable "environment" {
  description = "Short environment label used in names and recorder metadata."
  type        = string
  default     = "demo"

  validation {
    condition     = can(regex("^[a-z0-9-]{2,16}$", var.environment))
    error_message = "environment must be 2-16 lower-case letters, numbers, or hyphens."
  }
}

variable "operation_mode" {
  description = "AWS1 closeout target. Continuous service mode is out of scope for this gate."
  type        = string
  default     = "SMOKE_CAPTURE_BOUNDED"

  validation {
    condition     = var.operation_mode == "SMOKE_CAPTURE_BOUNDED"
    error_message = "AWS1 closeout only supports SMOKE_CAPTURE_BOUNDED."
  }
}

variable "project_name" {
  description = "Project name prefix."
  type        = string
  default     = "qq-fund-platform"
}

variable "vpc_cidr" {
  description = "CIDR for the dedicated AWS1 VPC."
  type        = string
  default     = "10.71.0.0/24"
}

variable "public_subnet_cidr" {
  description = "CIDR for the NAT-only public subnet."
  type        = string
  default     = "10.71.0.0/27"
}

variable "private_subnet_cidr" {
  description = "CIDR for the recorder private subnet."
  type        = string
  default     = "10.71.0.32/27"
}

variable "availability_zone" {
  description = "Optional AZ override. Defaults to the first available AZ in the region."
  type        = string
  default     = null
}

variable "enable_nat_gateway" {
  description = "Creates a NAT gateway with an Elastic IP for stable broker egress."
  type        = bool
  default     = true
}

variable "enable_private_endpoints" {
  description = "Creates VPC endpoints for SSM, CloudWatch, S3, Secrets Manager, and KMS."
  type        = bool
  default     = true
}

variable "enable_cloudwatch_alarms" {
  description = "Enable CloudWatch alarms only after the smoke metrics publishing schedule is explicitly wired."
  type        = bool
  default     = false
}

variable "instance_type" {
  description = "EC2 instance type for the Windows capture-only host."
  type        = string
  default     = "t3.large"
}

variable "ami_id" {
  description = "Approved Windows AMI ID. Terraform resolves it through aws_ami with owner/state/platform/architecture filters."
  type        = string

  validation {
    condition     = can(regex("^ami-[0-9a-fA-F]{8,17}$", var.ami_id))
    error_message = "ami_id must be an AMI identifier such as ami-0123456789abcdef0."
  }
}

variable "allowed_ami_owner_ids" {
  description = "Allowed AMI owners for the Windows recorder AMI. Defaults to Amazon-owned Windows AMIs."
  type        = list(string)
  default     = ["801119661308"]

  validation {
    condition     = length(var.allowed_ami_owner_ids) > 0 && alltrue([for owner in var.allowed_ami_owner_ids : can(regex("^[0-9]{12}$", owner))])
    error_message = "allowed_ami_owner_ids must contain one or more 12-digit AWS account IDs."
  }
}

variable "ebs_size_gb" {
  description = "Dedicated encrypted gp3 spool volume size in GiB."
  type        = number
  default     = 250

  validation {
    condition     = var.ebs_size_gb >= 50
    error_message = "ebs_size_gb must be at least 50."
  }
}

variable "spool_volume_label" {
  description = "Expected Windows volume label for the recorder spool."
  type        = string
  default     = "ANUBIS_SPOOL"
}

variable "archive_bucket_name" {
  description = "Optional exact S3 archive/artifact bucket name. If null, a deterministic account/region scoped name is used."
  type        = string
  default     = null
}

variable "artifact_bucket_name" {
  description = "Deprecated. AWS1 closeout requires app artifacts to live in the archive bucket so IAM remains minimal."
  type        = string
  default     = null

  validation {
    condition     = var.artifact_bucket_name == null
    error_message = "artifact_bucket_name must be null in AWS1 closeout; use the archive bucket for artifacts."
  }
}

variable "local_retention_days" {
  description = "Local spool retention after remote hash verification. Deletion remains operator-controlled."
  type        = number
  default     = 14

  validation {
    condition     = var.local_retention_days >= 1
    error_message = "local_retention_days must be positive."
  }
}

variable "lmax_market_data_endpoint_alias" {
  description = "LMAX market-data endpoint alias consumed by host config. Endpoint values are not hardcoded here."
  type        = string
  default     = "LMAX_DEMO_MARKET_DATA_ONLY"

  validation {
    condition     = var.lmax_market_data_endpoint_alias == "LMAX_DEMO_MARKET_DATA_ONLY"
    error_message = "AWS1 only allows the LMAX demo market-data-only endpoint alias."
  }
}

variable "lmax_market_data_port" {
  description = "Outbound TCP port used for the market-data FIX session."
  type        = number
  default     = 443
}

variable "lmax_market_data_egress_cidrs" {
  description = "Explicit broker market-data egress IPv4 CIDRs. Empty means fail-closed: no external broker egress rule. /0 is rejected."
  type        = list(string)
  default     = []

  validation {
    condition     = alltrue([for cidr in var.lmax_market_data_egress_cidrs : can(cidrhost(cidr, 0)) && cidr != "0.0.0.0/0" && !endswith(cidr, "/0")])
    error_message = "lmax_market_data_egress_cidrs must be valid IPv4 CIDRs and must not include /0."
  }
}

variable "credential_secret_name" {
  description = "Secrets Manager secret name created when credential_secret_arn is not supplied. No secret value is created by Terraform."
  type        = string
  default     = "qq/fund-platform/demo/lmax/market-data"
}

variable "credential_secret_arn" {
  description = "Existing market-data-only credential secret ARN. If null, Terraform creates secret metadata only."
  type        = string
  default     = null

  validation {
    condition     = var.credential_secret_arn == null || can(regex(":secret:.*market-data", lower(var.credential_secret_arn)))
    error_message = "credential_secret_arn must clearly be market-data scoped."
  }
}

variable "alarm_action_arns" {
  description = "SNS or incident action ARNs for CloudWatch alarms. Required only when enable_cloudwatch_alarms=true."
  type        = list(string)
  default     = []

  validation {
    condition     = alltrue([for arn in var.alarm_action_arns : can(regex("^arn:aws[a-zA-Z-]*:", arn))])
    error_message = "alarm_action_arns must be AWS ARNs."
  }
}

variable "install_root" {
  description = "Windows install root for the capture-only host."
  type        = string
  default     = "C:\\Anubis\\M2Capture"
}

variable "recorder_root" {
  description = "Windows recorder spool root on the dedicated data volume."
  type        = string
  default     = "D:\\Anubis\\Recorder"
}

variable "cloudwatch_namespace" {
  description = "CloudWatch namespace used by the host metric script."
  type        = string
  default     = "QQFundPlatform/AWS1"
}

variable "aws_cli_msi_s3_uri" {
  description = "Optional s3:// URI for a pre-staged, verified AWS CLI v2 MSI used by the SSM install runbook."
  type        = string
  default     = ""
}

variable "aws_cli_msi_sha256" {
  description = "Expected SHA-256 for aws_cli_msi_s3_uri when supplied."
  type        = string
  default     = ""
}
