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
output "lmax_market_data_endpoint_host" {
  description = "Approved LMAX Demo market-data endpoint host used for DNS-resolved egress."
  value       = var.lmax_market_data_endpoint_host
}

output "lmax_market_data_planned_egress_cidrs" {
  description = "Planned explicit /32 egress CIDRs for the LMAX Demo market-data endpoint."
  value       = local.lmax_market_data_planned_cidrs
}

output "lmax_market_data_dns_resolved_cidrs" {
  description = "Current DNS A-record /32 CIDRs resolved by Terraform for the approved endpoint."
  value       = local.lmax_market_data_dns_resolved_cidrs
}

output "lmax_market_data_egress_cidr_source" {
  description = "CIDR source annotation."
  value       = var.lmax_market_data_egress_cidr_source
}

output "lmax_market_data_egress_cidr_stability" {
  description = "CIDR stability annotation."
  value       = var.lmax_market_data_egress_cidr_stability
}

output "lmax_market_data_apply_requires_revalidation" {
  description = "Whether apply requires DNS revalidation."
  value       = var.lmax_market_data_egress_apply_requires_revalidation
}
