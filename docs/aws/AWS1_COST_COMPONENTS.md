# AWS1 Cost Components

This is a qualitative report only. No live AWS pricing lookup was performed.

## Primary Drivers

- Windows EC2 instance runtime hours.
- gp3 EBS spool volume GiB-months and provisioned throughput if increased later.
- S3 archive storage and request costs.
- NAT gateway hourly and data processing costs when stable broker egress is enabled.
- Interface VPC endpoint hourly and data processing costs.
- CloudWatch custom metrics, alarms, and log ingestion.
- Secrets Manager monthly secret charge.
- SSM is normally low/no incremental cost for Session Manager, subject to account features.

## Cost Controls

- Single instance only.
- No RDS.
- No load balancer.
- No autoscaling group in AWS1.
- S3 lifecycle transitions noncurrent versions after 30 days.
- CloudWatch log retention set to 30 days.
- Local retention is operator-controlled and does not trigger cloud spend directly.

## Known Tradeoff

NAT gateway plus private endpoints is conservative for stable egress and AWS private access, but NAT is a meaningful fixed cost. If the broker supports private connectivity or fixed allowlisted endpoints another route can be reviewed later.
