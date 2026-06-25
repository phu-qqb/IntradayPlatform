# AWS1 Existing Infra Audit

Baseline audited: `7e87f3b17c84ac8a0aeb79422e4caa97b915fbb6`

## Findings

- No existing Terraform, CloudFormation, Pulumi, or CDK files were present in the M2 final worktree.
- No existing `infra/` or `deploy/` ownership paths were present.
- The repository does contain many local PowerShell build/check/run scripts, so AWS1 follows that operational style for packaging, install, status, rollback, and gates.
- The deployable host surface is the existing M2 capture-only project:
  `tools/QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly`.
- The capture-only code validates `--no-order-entry`, `--no-account-api`, and `--no-db`, uses the endpoint alias `LMAX_DEMO_MARKET_DATA_ONLY`, and writes canonical recorder v2 output.

## Decision

AWS1 adds new, isolated paths:

- `infra/aws/anubis-shadow/` for Terraform;
- `deploy/aws/anubis-shadow/` for Windows host packaging and operations;
- `docs/aws/` for this gate pack.

Terraform was chosen even though no prior IaC exists because the target is AWS infrastructure and the prompt permits local `terraform validate`. PowerShell is used for host lifecycle because that is the existing repo convention and the target host is Windows.

## Reuse

Reused directly:

- M2C1B capture-only host project;
- M2 config shape and config-hash semantics;
- M2 outbound FIX allowlist;
- existing PowerShell gate-script convention.

Not reused:

- OMS/PMS/reconciliation scripts;
- execution, Account API, Databento, EMSX, Morgan Stanley, or RDS paths.
