# AWS1 Apply Checklist

AWS1 does not apply infrastructure. This checklist is for the later approval ceremony.

## Required Before Apply

- [ ] Terraform CLI installed locally.
- [ ] `terraform fmt -check -recursive infra/aws/anubis-shadow` passes.
- [ ] `terraform init -backend=false infra/aws/anubis-shadow` passes.
- [ ] `terraform validate infra/aws/anubis-shadow` passes.
- [ ] Static gate script passes with no skips.
- [ ] Windows AMI ID selected and documented.
- [ ] LMAX market-data egress CIDRs supplied.
- [ ] Alarm action ARNs supplied.
- [ ] Market-data-only secret metadata exists.
- [ ] Secret value populated out of band by an authorized operator.
- [ ] Artifact ZIP SHA-256 matches deployment manifest.
- [ ] No AWS credentials or secret values in repo, user-data, or logs.
- [ ] No EC2 ingress rule.
- [ ] SSM Session Manager confirmed as the administration path.
- [ ] S3 bucket encryption and versioning reviewed.
- [ ] Rollback runbook reviewed.
- [ ] Cost components reviewed.

## Must Remain False

- [ ] No Order Entry session.
- [ ] No `NewOrderSingle`, cancel, or replace path.
- [ ] No Account API path.
- [ ] No OMS/PMS mutation.
- [ ] No canonical RDS in AWS1.
- [ ] No Databento path.
- [ ] No Bloomberg EMSX path.
- [ ] No Morgan Stanley path.
- [ ] No `terraform apply` from this ticket.
- [ ] No push or merge from this ticket.
