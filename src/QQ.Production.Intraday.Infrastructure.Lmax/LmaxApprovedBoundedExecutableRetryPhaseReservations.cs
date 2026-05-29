namespace QQ.Production.Intraday.Infrastructure.Lmax;

public static class LmaxApprovedBoundedExecutableRetryPhaseReservations
{
    public const int MinimumRetryPhaseNumber = 43;
    public const int MaximumRetryPhaseNumber = 99;
    private static readonly string[] WorkspaceApprovedRetryPhases = ["LMAX-R105", "LMAX-R109", "LMAX-R115", "LMAX-R119", "LMAX-R123", "LMAX-R127", "LMAX-R131", "LMAX-R137", "LMAX-R141", "LMAX-R147", "LMAX-R151", "LMAX-R155", "LMAX-R161", "LMAX-R167", "LMAX-R171", "LMAX-R175", "LMAX-R177", "LMAX-R181", "LMAX-R187", "LMAX-R191", "LMAX-R195", "LMAX-R199", "LMAX-R203", "LMAX-R207", "LMAX-R211", "LMAX-R215", "LMAX-R221"];

    public static IReadOnlyList<string> ExplicitlyReservedPhases { get; } = Enumerable
        .Range(MinimumRetryPhaseNumber, MaximumRetryPhaseNumber - MinimumRetryPhaseNumber + 1)
        .Where(IsOdd)
        .Select(number => $"LMAX-R{number}")
        .Concat(WorkspaceApprovedRetryPhases)
        .ToArray();

    public const string NextApprovedRetryPhase = "LMAX-R55";

    public const string ReservationRule =
        "LMAX-R<number> where number is odd, >= 43, <= 99, plus explicitly approved workspace retry phases LMAX-R105, LMAX-R109, LMAX-R115, LMAX-R119, LMAX-R123, LMAX-R127, LMAX-R131, LMAX-R137, LMAX-R141, LMAX-R147, LMAX-R151, LMAX-R155, LMAX-R161, LMAX-R167, LMAX-R171, LMAX-R175, LMAX-R177, LMAX-R181, LMAX-R187, LMAX-R191, LMAX-R195, LMAX-R199, LMAX-R203, LMAX-R207, LMAX-R211, LMAX-R215, and LMAX-R221, and used only with ApprovedBoundedExecutableReadOnly plus the full bounded approval/composition chain and exact per-phase operator approval.";

    public static bool IsApproved(string phase)
        => ExplicitlyReservedPhases.Contains(phase, StringComparer.Ordinal);

    public static bool TryGetRetryPhaseNumber(string phase, out int number)
    {
        number = 0;
        const string prefix = "LMAX-R";
        if (string.IsNullOrWhiteSpace(phase) ||
            !phase.StartsWith(prefix, StringComparison.Ordinal) ||
            phase.Length == prefix.Length)
        {
            return false;
        }

        var numeric = phase[prefix.Length..];
        if (numeric.Length > 1 && numeric[0] == '0')
        {
            return false;
        }

        if (numeric.Any(static c => c < '0' || c > '9'))
        {
            return false;
        }

        return int.TryParse(numeric, out number);
    }

    private static bool IsOdd(int number) => number % 2 != 0;
}
