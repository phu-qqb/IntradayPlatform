# M1C Identity Scope Risk

The M1B audit found current Intraday uniqueness around:

- `Fill`: venue + broker execution id
- client order id uniqueness

M1C therefore reports Demo/Live/account collision risk explicitly. The current plan sets `future_db_apply_allowed=false` until environment/account/venue scoping is approved for any future DB replay.

Local isolated replay can still be planned for review when bundle identity is complete.
