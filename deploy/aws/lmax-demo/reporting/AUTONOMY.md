# LMAX Demo reporting autonomy: verified gap and proposed change

Status at 2026-09-18 08:01:42 UTC: credential access blocked; no portal login attempted. This is a proposal, not an IAM deployment or a successful unattended capture.

## Verified runtime

- Host: EC2AMAZ-1QPHTD8, Administrator; Demo account 1754288005.
- Downloader: Core PR #61, commit `6dce3375aaaa7e8042c61a36467e81fe5e97cb49`, still open and unmerged. Its AWS Secrets bootstrap and session-recovery implementation already exists.
- The report wrapper currently omits `--session-recovery aws-secrets` and the exact credential reference. The installed daily pipeline consumes local official exports; it does not acquire them.
- The default AWS CLI identity was the legacy IAM user `qq-intraday-ec2`; the default SDK credential-access check was denied. Repeating the access check with the launcher's documented child-only role binding proved the intended instance role `qq-role-ec2-intraday` on instance `i-05626133ca7892fb8`.
- At 08:01:42.807 UTC that exact role received `AccessDeniedException` for `secretsmanager:GetSecretValue`; the classified AWS reason was `NO_IDENTITY_BASED_ALLOW`. No credential values were obtained. Private receipt: `D:\data\lmax-demo-orchestration\reconciliation-20260918\portal-credential-access-1789718502807.json`.
- The daily EOD task remains Ready, Administrator / Interactive / Limited, next run 2026-09-18 20:15 UTC. It requires an existing Windows logon. It does not establish recovery after Windows logoff or reboot.

## Concrete IAM proposal; owner authorization required before application

Attach the adjacent [proposed inline policy](../iam/QQ-LMAX-Demo-PortalReports-Read-v1.proposed.json) to the existing role `qq-role-ec2-intraday`, with policy name `QQ-LMAX-Demo-PortalReports-Read-v1`. It permits only `GetSecretValue`, only the exact Demo portal-report secret ARN, and only explicitly requested `AWSCURRENT`. Do not attach it to the legacy IAM user.

This adds no ListSecrets, write, Production, FIX, Databento, KMS or IAM-management permission. The secret's encryption-key configuration has not been established by this check. If a customer-managed KMS key independently blocks decryption, obtain its exact metadata and prepare a separate, constrained proposal; do not widen this policy speculatively.

Before applying: read the current role and this policy name, preserve any existing policy, check for a collision, and require exact owner approval because the standing scope excludes IAM modifications. After applying: read back and compare the policy, repeat a secret-access-only check under the verified instance role, keep credential values solely in process memory, and record only contract-validity/presence booleans. Success is not evidence of a portal login or a download. Rollback may remove only a newly created matching policy after checking that it has not been changed concurrently.

The proposal has been checked as JSON, not deployed or qualified by an AWS access test. See [AWS GetSecretValue permissions and KMS dependency](https://docs.aws.amazon.com/secretsmanager/latest/apireference/API_GetSecretValue.html).

## Remaining implementation and qualification

1. Bind report processes to the documented EC2 role before secret access and verify that identity. Do not persistently alter machine/user environment or expose credentials.
2. Qualify the existing AWS Secrets bootstrap on the approved Demo runtime, using the supported authentication surface and respecting prior browser/download refusals. Confirm the exact account and an authentic account-scoped report response, then close/reopen the same profile and qualify reuse. An MFA challenge without the supported seed remains a block.
3. Integrate qualified report acquisition before the existing daily import: select the actual report date, verify manifest/account/date/hashes, and preserve bounded retries, exclusive ownership, immutable receipts, and provisional reporting on failure. Do not treat old exports or header-only files as current account evidence.
4. Prepare and separately authorize a Windows execution mode that works without an interactive Administrator logon. Qualify Chrome/profile access, networking, and the same LocalDB under that execution identity. Changing the scheduled task to S4U alone is not an established solution: Microsoft documents network/encrypted-file limitations. No OS, account, password-storage or task-principal change is part of this proposal.
5. Verify the complete report-only flow from an expired portal session, then after a controlled Windows logoff/restart. Do not perform a disruptive restart or kill unrelated processes merely to test autonomy.

[Microsoft task logon modes](https://learn.microsoft.com/en-us/windows/win32/api/taskschd/ne-taskschd-task_logon_type).

## Separate trading gate

Reporting autonomy does not resolve the four broker executions missing internally, certify the old actual-send session, qualify the new FIX runtime, or create a fresh official flat/no-orders observation. Trading stays blocked until those existing requirements are satisfied. Production credentials are outside this change. Emails remain deferred.
