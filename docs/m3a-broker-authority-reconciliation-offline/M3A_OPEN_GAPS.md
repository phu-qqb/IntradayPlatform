# M3A Open Gaps

1. Broker runtime position authority is not wired.
   - Required in M3B: read-only broker position snapshot/report source.
   - AccountAPI remains banned unless explicitly approved by lead.

2. Broker open-order authority is not wired.
   - Required in M3B: broker open-order snapshot source or proof of complete session-state reconstruction across restart.

3. Broker execution feed integration is modelled but not activated as an authority source by M3A.
   - Required in M3B: read-only ingestion/replay source with sequence and freshness metadata.

4. Clock and source freshness policy must be configured when a real broker source exists.
   - M3A supports `MaxSourceAge` but does not choose production thresholds.

5. Operator break resolution workflow is represented by status/evidence refs but not UI-wired.
   - Required later: append-only break resolution records and operational dashboard/report.

No gap is hidden by synthetic zero positions or manual evidence promotion.
