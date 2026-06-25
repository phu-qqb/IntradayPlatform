output "recorder_instance_id" {
  description = "Windows EC2 recorder instance ID."
  value       = aws_instance.recorder.id
}

output "archive_bucket_name" {
  description = "Versioned SSE-S3 archive bucket for finalized chunks and manifests."
  value       = aws_s3_bucket.archive.bucket
}

output "credential_secret_arn" {
  description = "Market-data-only credential secret ARN. Secret value is never managed by Terraform."
  value       = local.credential_secret_arn
}

output "ssm_install_runbook_name" {
  description = "SSM command document for reproducible host install."
  value       = aws_ssm_document.install_runbook.name
}

output "nat_egress_ip" {
  description = "Stable broker egress IP when NAT is enabled."
  value       = var.enable_nat_gateway ? aws_eip.nat[0].public_ip : null
}

output "recorder_security_group_id" {
  description = "Recorder security group with no ingress rules."
  value       = aws_security_group.recorder.id
}
