# ARCH7A working leaves authority decision

Classification: `LMAX_FIX_KNOWN_ORDER_STATUS_ONLY`.

The supplied LMAX Broker FIX 4.4 dictionary documents `OrderStatusRequest(35=H)` for one already-known order. It does not document `OrderMassStatusRequest(35=AF)`, tag 585 values, an explicit full-snapshot terminator, or a drop-copy session. Logon recovery retains at most 512 messages and is not available after a gateway failure or sequence reset.

ARCH7A may reconstruct and reconcile known platform orders offline, but it cannot discover all manual or external orders and cannot prove a broker-wide empty state. `ExternalOrManualOrderCoverage` therefore remains `UNPROVEN`; `BROKER_WORKING_LEAVES_UNOBSERVABLE` remains active, `Actionable=false`, `RiskDecision=BLOCK_NEW_ORDERS`, and `BrokerSendAllowed=false`.
