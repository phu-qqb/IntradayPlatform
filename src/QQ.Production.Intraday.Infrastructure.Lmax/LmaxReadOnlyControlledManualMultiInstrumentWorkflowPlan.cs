using System.Text.RegularExpressions;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public enum LmaxReadOnlyControlledManualWorkflowPlanDecision
{
    PASS,
    PASS_WITH_KNOWN_WARNINGS,
    FAIL
}

public sealed record LmaxReadOnlyControlledManualInstrumentPlan(
    string Symbol,
    string SlashSymbol,
    string SecurityId,
    string SecurityIdSource,
    string PlanningPipelineDecision,
    bool SelectedForFutureManualConsideration,
    int ProposedSequenceOrder,
    bool OneInstrumentAtATime,
    int MaxAttemptsPerInstrument,
    bool RetryRequiresNewPhase,
    bool MarketHoursOnly,
    bool ManualOperatorCommandOnly,
    bool NoSchedulerOrPolling,
    bool NoRuntimeShadowReplaySubmit,
    bool NoOrderSubmission,
    bool NoTradingMutation,
    bool NoGatewayRegistration,
    bool CanRunExternalSnapshot,
    bool IsApprovedForExternalRun,
    bool EligibleForManualSnapshotAttempt);

public sealed record LmaxReadOnlyControlledManualMultiInstrumentWorkflowPlan(
    string PlanId,
    DateTimeOffset CreatedAtUtc,
    string RequestedByOperatorId,
    string Reason,
    string SourcePhase7AAdrPath,
    string SourceAdditionalInstrumentPlanningPipelinePath,
    string SourcePlanningStatusReportPath,
    IReadOnlyList<LmaxReadOnlyControlledManualInstrumentPlan> Instruments,
    int InstrumentCount,
    int SelectedCount,
    int ExecutableCount,
    bool BatchExecutionAllowed,
    bool SchedulerOrPolling,
    bool RuntimeShadowReplaySubmit,
    bool OrderSubmission,
    bool GatewayRegistration,
    bool TradingMutation,
    string ApiWorkerGatewayMode,
    bool NoSensitiveContent,
    LmaxReadOnlyControlledManualWorkflowPlanDecision FinalDecision);

public sealed record LmaxReadOnlyControlledManualWorkflowPlanCheck(
    string Name,
    LmaxReadOnlyControlledManualWorkflowPlanDecision Decision,
    string Detail);

public sealed record LmaxReadOnlyControlledManualWorkflowPlanValidation(
    LmaxReadOnlyControlledManualWorkflowPlanDecision FinalDecision,
    IReadOnlyList<LmaxReadOnlyControlledManualWorkflowPlanCheck> Checks);

public static class LmaxReadOnlyControlledManualWorkflowPlanValidator
{
    private static readonly IReadOnlyList<(string Symbol, string SlashSymbol, string SecurityId, int Sequence)> ExpectedSequence =
    [
        ("GBPUSD", "GBP/USD", "4002", 1),
        ("EURGBP", "EUR/GBP", "4003", 2),
        ("USDJPY", "USD/JPY", "4004", 3),
        ("AUDUSD", "AUD/USD", "4007", 4)
    ];

    private static readonly Regex SensitivePattern = new(
        "(password\\s*[:=]|secret\\s*[:=]|token\\s*[:=]|apikey\\s*[:=]|api_key\\s*[:=]|privatekey\\s*[:=]|private_key\\s*[:=]|authorization\\s*[:=]|bearer\\s+|\\b553=|\\b554=|host\\s*=|user\\s*=|account\\s*=|raw\\s*fix|sendercompid|targetcompid)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ForbiddenRuntimePattern = new(
        "(NewOrderSingle|OrderCancelRequest|OrderCancelReplaceRequest|OrderStatusRequest|TradeCaptureReportRequest|SubmitOrder|ReplaySubmitAsync|PeriodicTimer|LmaxScheduler|MarketDataPolling|SecurityListPolling)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static LmaxReadOnlyControlledManualWorkflowPlanValidation Validate(
        LmaxReadOnlyControlledManualMultiInstrumentWorkflowPlan plan,
        string rawPlanText = "")
    {
        var checks = new List<LmaxReadOnlyControlledManualWorkflowPlanCheck>
        {
            Check("OperatorAndReason", HasText(plan.RequestedByOperatorId) && HasText(plan.Reason), "Operator id and reason are required."),
            Check("SourceReferences", HasText(plan.SourcePhase7AAdrPath) && HasText(plan.SourceAdditionalInstrumentPlanningPipelinePath) && HasText(plan.SourcePlanningStatusReportPath), "Phase 7A ADR, pipeline, and status report references are required."),
            Check("InstrumentCount", plan.InstrumentCount == 4 && plan.Instruments.Count == 4, "Plan must cover exactly four additional instruments."),
            Check("SelectedCount", plan.SelectedCount == 4, "All four instruments should be selected for future manual consideration."),
            Check("ExecutableCountZero", plan.ExecutableCount == 0, "No instrument may be executable."),
            Check("NoBatchExecution", !plan.BatchExecutionAllowed, "Batch execution must remain disabled."),
            Check("AggregateSafetyFlags", !plan.SchedulerOrPolling && !plan.RuntimeShadowReplaySubmit && !plan.OrderSubmission && !plan.GatewayRegistration && !plan.TradingMutation, "Aggregate scheduler, replay, order, gateway, and mutation flags must remain false."),
            Check("FakeGatewayOnly", plan.ApiWorkerGatewayMode == "FakeLmaxGateway", "API/Worker gateway mode must remain FakeLmaxGateway."),
            Check("NoSensitiveContentFlag", plan.NoSensitiveContent, "noSensitiveContent must be true."),
            Check("FinalDecisionPass", plan.FinalDecision == LmaxReadOnlyControlledManualWorkflowPlanDecision.PASS, "Complete safe plans should be PASS.")
        };

        foreach (var expected in ExpectedSequence)
        {
            var instrument = plan.Instruments.FirstOrDefault(x => x.Symbol.Equals(expected.Symbol, StringComparison.OrdinalIgnoreCase));
            checks.Add(Check($"InstrumentPresent:{expected.Symbol}", instrument is not null, $"{expected.Symbol} must be present."));
            if (instrument is null)
            {
                continue;
            }

            checks.Add(Check($"Identity:{expected.Symbol}", instrument.SlashSymbol == expected.SlashSymbol && instrument.SecurityId == expected.SecurityId && instrument.SecurityIdSource == "8", $"{expected.Symbol} must have expected slash symbol, SecurityID, and SecurityIDSource=8."));
            checks.Add(Check($"PipelineDecision:{expected.Symbol}", instrument.PlanningPipelineDecision == "PASS", $"{expected.Symbol} pipeline decision must be PASS."));
            checks.Add(Check($"Sequence:{expected.Symbol}", instrument.ProposedSequenceOrder == expected.Sequence, $"{expected.Symbol} sequence order must be {expected.Sequence}."));
            checks.Add(Check($"Selected:{expected.Symbol}", instrument.SelectedForFutureManualConsideration, $"{expected.Symbol} must be selected for future manual consideration."));
            checks.Add(Check($"ManualOneAtATime:{expected.Symbol}", instrument.OneInstrumentAtATime && instrument.MaxAttemptsPerInstrument == 1 && instrument.RetryRequiresNewPhase && instrument.MarketHoursOnly && instrument.ManualOperatorCommandOnly, $"{expected.Symbol} must remain manual, one-at-a-time, market-hours only, one attempt, and retry-by-new-phase."));
            checks.Add(Check($"NoRuntimePower:{expected.Symbol}", instrument.NoSchedulerOrPolling && instrument.NoRuntimeShadowReplaySubmit && instrument.NoOrderSubmission && instrument.NoTradingMutation && instrument.NoGatewayRegistration, $"{expected.Symbol} must not allow scheduler, runtime replay submit, orders, mutation, or gateway registration."));
            checks.Add(Check($"NotExecutable:{expected.Symbol}", !instrument.CanRunExternalSnapshot && !instrument.IsApprovedForExternalRun && !instrument.EligibleForManualSnapshotAttempt, $"{expected.Symbol} run eligibility flags must remain false."));
        }

        var orderedSymbols = plan.Instruments
            .OrderBy(x => x.ProposedSequenceOrder)
            .Select(x => x.Symbol)
            .ToArray();
        checks.Add(Check("RecommendedSequence", orderedSymbols.SequenceEqual(ExpectedSequence.Select(x => x.Symbol), StringComparer.OrdinalIgnoreCase), "Recommended sequence must be GBPUSD, EURGBP, USDJPY, AUDUSD."));

        if (SensitivePattern.IsMatch(rawPlanText))
        {
            checks.Add(Fail("NoSensitiveText", "Plan contains credential-shaped or raw FIX content."));
        }

        if (ForbiddenRuntimePattern.IsMatch(rawPlanText))
        {
            checks.Add(Fail("NoRuntimeSurfaceText", "Plan contains forbidden runtime/order/scheduler marker."));
        }

        var final = checks.Any(x => x.Decision == LmaxReadOnlyControlledManualWorkflowPlanDecision.FAIL)
            ? LmaxReadOnlyControlledManualWorkflowPlanDecision.FAIL
            : LmaxReadOnlyControlledManualWorkflowPlanDecision.PASS;
        return new(final, checks);
    }

    private static bool HasText(string value) => !string.IsNullOrWhiteSpace(value);

    private static LmaxReadOnlyControlledManualWorkflowPlanCheck Check(string name, bool pass, string detail)
        => pass ? new(name, LmaxReadOnlyControlledManualWorkflowPlanDecision.PASS, detail) : Fail(name, detail);

    private static LmaxReadOnlyControlledManualWorkflowPlanCheck Fail(string name, string detail)
        => new(name, LmaxReadOnlyControlledManualWorkflowPlanDecision.FAIL, detail);
}
