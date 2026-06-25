# M2C0 Feed State Machine

States implemented:

- Created
- Starting
- Connected
- Subscribing
- Synchronized
- Stale
- GapDetected
- Recovering
- Failed
- Stopping
- Stopped

Fail-closed rule for shadow intent:

`	ext
feed_state == Synchronized
book_valid == true
quote_age <= configured_limit
recorder_health == READY
`

If any condition fails, the host cannot produce a shadow decision or child intent. Offline tests cover nominal, gap, stale, recovery, PossDup, invalid book, and multi-instrument playback.
