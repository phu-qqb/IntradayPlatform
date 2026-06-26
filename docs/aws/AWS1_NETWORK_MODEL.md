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

`lmax_market_data_egress_cidrs` defaults to empty and Terraform includes a precondition that prevents plan/apply until explicit broker market-data CIDRs are supplied.

For AWS1P, the approved source is the same M2C1B market-data-only capture path that produced `GO_M2C2_CAPTURE_VALIDATED` in the final M2C1B package. The concrete endpoint binding is:

```text
host = fix-marketdata.london-demo.lmax.com
port = 443
source = DNS_RESOLVED_CURRENT_LMAX_DEMO_MARKETDATA_ENDPOINT
stability = NOT_CONTRACTUALLY_GUARANTEED
apply_requires_revalidation = true
```

The current plan CIDRs are DNS-resolved A records converted to `/32`. Terraform resolves the host during planning and fails if `lmax_market_data_egress_cidrs` does not exactly match the current DNS `/32` set. Operators must also run `deploy/aws/anubis-shadow/scripts/Test-LmaxMarketDataDnsCidrs.ps1` immediately before any separately approved future apply and stop if the A records differ from the planned CIDRs.

## Endpoint Alias

The LMAX endpoint is represented as an alias:

```text
LMAX_DEMO_MARKET_DATA_ONLY
```

The alias is stored in SSM Parameter Store. The AWS1P network plan documents the broker market-data host only for DNS-resolved egress allowlisting. No account, credential value, or Order Entry endpoint is hardcoded in Terraform or source code.

## Windows Firewall

User-data sets inbound default to block and creates outbound rules for HTTPS/AWS endpoint traffic and the market-data TCP port. The primary network allowlist remains the EC2 security group plus explicit broker CIDRs.
