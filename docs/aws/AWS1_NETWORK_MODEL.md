# AWS1 Network Model

## Region

`eu-west-2`

## Topology

- Dedicated VPC.
- Public subnet exists only for NAT gateway placement.
- Private subnet contains the Windows recorder.
- Recorder has no public IP.
- Optional NAT gateway provides one Elastic IP for stable broker egress.
- AWS service traffic uses VPC endpoints when enabled.

## Ingress

The recorder security group has no ingress rules. Administration uses SSM Session Manager only.

## Egress

Recorder egress is explicit:

- TCP 443 to VPC CIDR for AWS private endpoints;
- TCP `var.lmax_market_data_port` to `var.lmax_market_data_egress_cidrs`.

`lmax_market_data_egress_cidrs` defaults to empty and Terraform includes a precondition that prevents apply until explicit broker market-data CIDRs are supplied.

## Endpoint Alias

The LMAX endpoint is represented as an alias:

```text
LMAX_DEMO_MARKET_DATA_ONLY
```

The alias is stored in SSM Parameter Store. No broker host, account, or credential value is hardcoded in Terraform or source code.

## Windows Firewall

User-data sets inbound default to block and creates outbound rules for HTTPS/AWS endpoint traffic and the market-data TCP port. The primary network allowlist remains the EC2 security group plus explicit broker CIDRs.
