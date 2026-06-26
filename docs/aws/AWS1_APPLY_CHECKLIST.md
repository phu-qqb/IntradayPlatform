# AWS1 Future Plan/Apply Checklist

AWS1 closeout does not apply infrastructure. This checklist is for a later, separately approved plan/apply ceremony.

## Required Before Plan

- [ ] Remote S3 Terraform backend bucket exists out of band.
- [ ] Backend config supplies `bucket`, `key`, and `region`.
- [ ] `terraform fmt -check -recursive` passes.
- [ ] `terraform init -backend=false` passes.
- [ ] `terraform validate` passes.
- [ ] `.terraform.lock.hcl` is present and committed.
- [ ] Static gate script passes with no skips.
- [ ] Windows AMI ID selected, owner allow-listed, available, Windows, and x86_64.
- [ ] LMAX market-data egress CIDRs supplied as DNS-resolved `/32` values for `fix-marketdata.london-demo.lmax.com:443`; no `/0` allowed.
- [ ] `deploy/aws/anubis-shadow/scripts/Test-LmaxMarketDataDnsCidrs.ps1` re-resolves the endpoint immediately before apply and matches the planned CIDRs exactly.
- [ ] AWS CLI MSI S3 URI and SHA-256 supplied.
- [ ] Plan-ready app artifact S3 URI and SHA-256 supplied.
- [ ] Market-data-only secret metadata exists.
- [ ] Secret value populated out of band by an authorized operator.
- [ ] Alarm action ARNs supplied if and only if `enable_cloudwatch_alarms=true`.
- [ ] Artifact ZIP SHA-256 matches deployment manifest.
- [ ] No AWS credentials or secret values in repo, user-data, or logs.
- [ ] No EC2 ingress rule.
- [ ] SSM Session Manager confirmed as the administration path.
- [ ] S3 bucket encryption, TLS-only policy, versioning, and multipart abort reviewed.
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
