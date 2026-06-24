# M1C Lineage Rules

- No `ModelRun` is synthesized.
- No R018/R216 quantity is reverse-engineered into a model target.
- ClOrdID/tag11 is preserved as source identity.
- Broker OrderID/tag37 and ExecID/tag17 are preserved when present.
- Parent/child/phase/wave/clip remain evidence dimensions in the plan.
- Ambiguous catalog matches are not selected arbitrarily.
- Missing `deadline_utc` remains missing and is not inferred from filenames.
- Ledger applicability can be `NOT_APPLICABLE`, `INCOMPLETE_HISTORY`, or `COMPLETE_ISOLATED_REPLAY_CANDIDATE`.
