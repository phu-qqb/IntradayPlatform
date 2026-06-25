# AWS1 Test Report

Generated local JSON report:

```text
artifacts/readiness/anubis-aws1-read-only-shadow-foundation-no-apply/AWS1_TEST_REPORT.generated.json
```

## Results Run In This Worktree

- M2 package SHA-256 verified: `F1F024563F29544124049A1CF7A980A93C7ED25842F71752F9F6A04E862163C8`.
- Config hash stamping verified against the original M2 sample: `95888e5c18d8dd373fc4c02433ae603461fe23eca81dfe4d4171b5a5f9ac757e`.
- `dotnet publish` for `QQ.Production.Intraday.Tools.LmaxMarketDataCaptureOnly` completed.
- Deterministic packaging is produced by `Package-M2CaptureHost.ps1`; the final ZIP hash is written to the adjacent `.sha256` sidecar after packaging.

- Focused unit tests passed:

```text
dotnet test tests\QQ.Production.Intraday.Tests.Unit\QQ.Production.Intraday.Tests.Unit.csproj --filter FullyQualifiedName~M2C1ALmaxMarketDataOnlyTests
Passed: 54, Failed: 0, Skipped: 0
```

- Local AWS1 static gate passed all executable/IaC checks it could run:
  - PowerShell parse: PASS
  - no EC2 recorder ingress: PASS
  - no AWS apply commands: PASS
  - no RDS initial path: PASS
  - no order mutation surface: PASS
  - no forbidden data vendor paths: PASS
  - no secret values: PASS
  - deliverable docs present: PASS

## Skipped Checks

Terraform is not installed in this environment, so these mandatory checks were skipped:

```text
terraform_fmt
terraform_validate
```

Because mandatory IaC format/validate did not run, the gate remains:

```text
NO_GO_AWS1
```
