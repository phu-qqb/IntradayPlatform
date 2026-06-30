# M3AA Open Positions Contract Status

Status: CONTRACT_PENDING / candidate EOD evidence only.

## Observed LMAX Portal Header

```text
Instrument,CCY,Open Quantity,Margin on Open Position,Average Opening Price,Closing Price,Open Profit / Loss,MTM Valuation Rate to Base CCY,LMAX Symbol,Account Id,Position UTI
```

## Classification

A valid non-empty `open-positions.csv` is classified as:

```text
EOD_OPEN_POSITIONS_CANDIDATE_EVIDENCE
```

It is not:

- live broker-state authority;
- pre-trade authority;
- open-order authority;
- Order Entry evidence.

## Known Gaps

The open positions contract is not yet promoted to broker authority because the following remain unproven for production use:

- as-of timestamp semantics;
- full account scope semantics;
- relationship between open position rows and account summary margin rollups;
- handling of zero rows versus flat account;
- definitive position quantity sign and conversion rules for all instruments.

Margin rollup differences are warnings (`OPEN_POSITIONS_MARGIN_ROLLUP_WARNING`) and never upgrade authority.
