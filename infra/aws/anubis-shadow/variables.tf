variable "region" {
  description = "AWS region for AWS1. The initial target is London."
  type        = string
  default     = "eu-west-2"
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

variable "project_name" {
  description = "Project name prefix."
  type        = string
  default     = "anubis"
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

variable "instance_type" {
  description = "EC2 instance type for the Windows capture-only host."
  type        = string
  default     = "t3.large"
}

variable "ami_id" {
  description = "Windows AMI ID. Must be supplied explicitly by the operator."
  type        = string
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

variable "archive_bucket_name" {
  description = "Optional exact S3 archive bucket name. If null, a deterministic account/region scoped name is used."
  type        = string
  default     = null
}

variable "artifact_bucket_name" {
  description = "Optional existing bucket for deployment artifacts. Defaults to the archive bucket."
  type        = string
  default     = null
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
}

variable "lmax_market_data_port" {
  description = "Outbound TCP port used for the market-data FIX session."
  type        = number
  default     = 443
}

variable "lmax_market_data_egress_cidrs" {
  description = "Explicit broker market-data egress CIDRs. Empty means fail-closed: no external broker egress rule."
  type        = list(string)
  default     = []
}

variable "credential_secret_name" {
  description = "Secrets Manager secret name created when credential_secret_arn is not supplied. No secret value is created by Terraform."
  type        = string
  default     = "/anubis/aws1/lmax-market-data-only"
}

variable "credential_secret_arn" {
  description = "Existing market-data-only credential secret ARN. If null, Terraform creates secret metadata only."
  type        = string
  default     = null
}

variable "alarm_action_arns" {
  description = "SNS or incident action ARNs for CloudWatch alarms."
  type        = list(string)
  default     = []
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
  default     = "Anubis/AWS1"
}
