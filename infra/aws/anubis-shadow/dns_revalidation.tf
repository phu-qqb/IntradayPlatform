data "dns_a_record_set" "lmax_market_data" {
  host = var.lmax_market_data_endpoint_host
}

locals {
  lmax_market_data_dns_resolved_cidrs = sort([for address in data.dns_a_record_set.lmax_market_data.addrs : "${address}/32"])
  lmax_market_data_planned_cidrs      = sort(distinct(var.lmax_market_data_egress_cidrs))
}