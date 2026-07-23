namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

public sealed record LmaxFixArch7bRecoveryPlan(
    bool MaySendOpeningNewOrderSingle,
    bool MaySendOpeningResidualCancel,
    bool MaySendFlattenNewOrderSingle,
    bool QueryOpeningKnownOrder,
    bool QueryFlattenKnownOrder);

public static class LmaxFixArch7bRecoveryPlanner
{
    public static LmaxFixArch7bRecoveryPlan Build(LmaxFixArch7bRecoveryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new(
            MaySendOpeningNewOrderSingle: !state.OpeningSendIntentExists,
            MaySendOpeningResidualCancel: !state.CancelSendIntentExists,
            MaySendFlattenNewOrderSingle: !state.FlattenSendIntentExists,
            QueryOpeningKnownOrder:
                state.OpeningSendIntentExists &&
                (!state.OpeningTerminal || state.OpeningLeavesQuantity > 0m),
            QueryFlattenKnownOrder:
                state.FlattenSendIntentExists &&
                (!state.FlattenTerminal ||
                 state.FlattenLeavesQuantity > 0m ||
                 state.FlattenCumulativeQuantity != state.OpeningCumulativeQuantity));
    }
}
