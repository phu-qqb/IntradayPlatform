namespace QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

public static class Arch7bOneShotInvariantGuard
{
    public static void ValidateSlotCandidate(DateTimeOffset observedUtc, DateTimeOffset slotStartUtc,
        int requiredMarginSeconds, bool operational = true, bool ambiguous = false)
    {
        if (slotStartUtc <= observedUtc)
            throw new Arch7bQualificationException(Arch7bBlockers.SlotAlreadyStarted);
        if (slotStartUtc < observedUtc.AddSeconds(requiredMarginSeconds))
            throw new Arch7bQualificationException(Arch7bBlockers.PreparationMarginInsufficient);
        if (!operational)
            throw new Arch7bQualificationException(Arch7bBlockers.SlotOutsideOperationalSession);
        if (ambiguous)
            throw new Arch7bQualificationException(Arch7bBlockers.CalendarAmbiguous);
    }

    public static void ValidateWakeLateness(TimeSpan lateness)
    {
        if (lateness > TimeSpan.FromMilliseconds(Arch7bGlobalSloRegistry.GlobalSchedulerMaximumWakeLatenessMilliseconds))
            throw new Arch7bQualificationException(Arch7bBlockers.SchedulerWakeLatenessExceeded);
    }

    public static void ValidateCleanupDeadline(TimeSpan elapsed)
    {
        if (elapsed > TimeSpan.FromSeconds(Arch7bGlobalSloRegistry.GlobalTerminalCleanupDeadlineSeconds))
            throw new Arch7bQualificationException(Arch7bBlockers.CleanupDeadlineExceeded);
    }

    public static void ValidateStageOrder(string before, string after, IReadOnlyList<string> observedOrder,
        string blocker)
    {
        var values = observedOrder.ToList();
        if (values.IndexOf(before) < 0 || values.IndexOf(after) < 0 || values.IndexOf(before) >= values.IndexOf(after))
            throw new Arch7bQualificationException(blocker);
    }

    public static void ValidatePrimaryFailurePreserved(string primaryBlocker, string reportedBlocker)
    {
        if (primaryBlocker != reportedBlocker)
            throw new Arch7bQualificationException(Arch7bBlockers.PrimaryBlockerMasked);
    }

    public static void ValidateKnownMode(string mode)
    {
        if (mode is not ("qualify-static-authorities" or "validate-one-shot-plan" or "simulate-one-shot" or
            "materialize-supervisor-candidate-packet"))
            throw new Arch7bQualificationException(Arch7bBlockers.SupervisorModeUnknown, mode);
    }
}
