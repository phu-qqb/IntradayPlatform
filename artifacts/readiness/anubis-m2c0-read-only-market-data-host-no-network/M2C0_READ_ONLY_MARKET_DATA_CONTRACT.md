# M2C0 Read-Only Market Data Contract

## IReadOnlyMarketDataSource

Allowed capabilities only:

- StartAsync
- SubscribeAsync
- ReadMarketDataAsync
- Health
- StopAsync

Forbidden capabilities:

- SendOrder
- CancelOrder
- ReplaceOrder
- QueryAccount
- QueryPositions
- Any AccountAPI, Databento, R009, R018, R216, market order, socket implementation in M2C0.

## ReadOnlyMarketDataObservationV1

Required fields:

- environment
- venue
- session_id
- instrument_id
- symbol
- source_message_type
- source_timestamp_utc
- local_receive_utc
- local_monotonic_ticks
- fix_msg_seq_num
- poss_dup
- quote_event_id
- bid_price / bid_quantity
- ask_price / ask_quantity
- book_valid
- gap_status
- subscription_state
- raw_payload_hash

No raw payload or credential material is stored.
