# ARCH7B position-market live call graph

## Forensic result

The base at 'da287d9e616d8cdca4e9f97bb3841da6d86bf01d' defined and tested
'arch7b_position_market_slot_lineage_v1', but no operational process called
'BuildDraft', 'Finalize', 'RequireExactBinding', 'BindRevision', or
'RequireArch7aRevision'.

Base verdict: 'CONTRACT_DEFINED_BUT_NOT_WIRED'.

Candidate verdict: 'CONTRACT_FULLY_WIRED_BY_CANDIDATE_HEAD'.

Core owns production of the bracketed 99-line position snapshot. Intraday owns
runtime selection, the draft, market capture binding, final lineage, the
49-to-99 projection, economic revision binding, ARCH7A enforcement and
reporting. No Core change is required.

## Operational stages

1. 'Arch7bPositionSnapshotImport apply-import' persists the one-shot position
   snapshot append-only.
2. 'Arch6fEconomicReplay prearm-and-import' selects the exact 99/99 source and
   calls 'BuildAndPublishDraft'.
3. 'assert-prearmed' reads the draft by absolute path and expected SHA, then
   calls 'RequirePrearmedDraft'.
4. 'LmaxMarketDataCaptureOnly' repeats that validation before credential use or
   logon and gives 'CaptureLiveAsync' the draft 'MarketCaptureSessionId'.
5. 'publish-ready' invokes the existing 'PmsShadowRealSlotManifestFinalizer'.
6. The same process calls 'FinalizeMarket', which calls contract 'Finalize',
   writes 'position-market-slot-lineage.json' create-new and enriches the
   canonical manifest atomically.
7. 'PmsShadowFreshSlotReadyMarkerContract' requires and publishes the exact
   lineage path/SHA.
8. The prearmed importer calls 'RequireImportBinding' before projection.
9. The unchanged production builder projects 49 market sources to 99
   observations, 288 targets and 288 drifts.
10. 'BindAndPublishRevision' calls 'BindRevision', writes
    'position-market-revision-input-binding.json' idempotently and stores its
    SHA through the existing 'HandoffSha256' persistence field.
11. ARCH7A reads that file by path/SHA and calls 'RequireArch7aRevision'.
12. read-only reporting loads both files, exposes their identities and emits
    four explicitly classified breaks for absence or contradiction.

The machine-readable companion records executable, mode, source file, class,
method, contracts, paths, SHAs, process ownership and timing authority for
every stage.

## Command authority

Every applicable process receives absolute paths and lowercase SHA-256 values:

- '--position-market-draft-path'
- '--expected-position-market-draft-sha256'
- '--position-market-lineage-path'
- '--expected-position-market-lineage-sha256'
- '--position-market-revision-binding-path'
- '--expected-position-market-revision-binding-sha256'

Arguments are unique name/value pairs. There is no implicit historical root,
last-value-wins behavior, default old SHA or silent reconstruction.

All one-shot path, SHA, slot, snapshot, ingestion, session, clock, selection,
manifest and revision values are 'REGENERATE_JUST_BEFORE_LIVE_RUN'.

## Persistence and replay

No migration is required. The authority is carried by content-addressed files,
the canonical market manifest, ready marker, revision source manifest and the
existing persisted 'HandoffSha256'. An identical binding is idempotent.
Different lineage at the same revision path fails with
'ARCH7B_POSITION_MARKET_REPLAY_LINEAGE_MISMATCH'; a manifest mismatch is also
rejected before projection.

## Offline qualification

'Arch7bPositionMarketRuntimeWiringTests' exercises real writers/readers and
call-site services with qualification-only fixtures. It proves exact draft,
lineage and revision files, 99/288/288 binding, ARCH7A binding, deterministic
bytes across distinct roots and a 23-case negative matrix. It does not claim a
historical match or real market evidence.

Required success verdict:
'ARCH7B_POSITION_MARKET_LIVE_WIRING_QUALIFIED'.

## Safety

The candidate and its qualification are no-order. The offline campaign opens
no database, reads no secret, contacts no LMAX/FIX/AWS/Polygon/Databento/API,
and creates no Fill, PositionLedgerEvent or operational one-shot state.
