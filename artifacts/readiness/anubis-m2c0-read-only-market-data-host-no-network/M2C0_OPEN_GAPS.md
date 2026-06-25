# M2C0 Open Gaps

No M2C0 gate blocker remains.

Open for M2C1, not M2C0:

- Real read-only LMAX binding still requires lead/operator approval.
- Real endpoint alias/session alias must be supplied without credential material in the config artifact.
- M2C1 must prove no order-entry session, no AccountAPI, no Databento API, and no DB apply at runtime.
- Existing LMAX read-only infra needs a split/adapter review before reuse because several classes include TCP/FIX/logon/credential boundaries.
