# ARCH7B Live Authority Materialization

`arch7b_one_shot_live_plan_template_v2` is a static, content-addressed freeze artifact. It binds the runtime, repository, command, calendar, SLO, chronology, cleanup, target, no-order budget, and file authorities. It contains no run identity, slot, secret version, timestamp, or economic result.

`materialize-live-run-authorities` reads that template from a freeze, verifies the freeze manifest and packet hashes, and writes create-new artifacts into an empty future run root:

- `arch7b-one-shot-operator-authorization.json` using `arch7b_one_shot_operator_authorization_v2`;
- `arch7b-one-shot-live-execution-authority.json` using `arch7b_one_shot_live_execution_authority_v3`;
- a content-addressed materialization manifest.

The materializer is no-order and does not select a slot, create a run identity, read a secret, connect to AWS/RDS/LMAX, or produce economic output. The per-run authority binds the operator authorization ID to the static template SHA and all static authorities; the template itself never carries that ID.
