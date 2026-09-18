namespace QQ.Production.Intraday.Application;

public static class LmaxDemoDaySchedule
{
    public static DateTimeOffset FinalClose(DateTimeOffset date)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var local = TimeZoneInfo.ConvertTime(date, zone).Date.AddHours(15);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone));
    }
    public static bool IsFinalExit(DateTimeOffset cutoff) => cutoff == FinalClose(cutoff).AddMinutes(-15);
    public static bool IsEligible(string programme, DateTimeOffset cutoff)
    {
        if (cutoff.Offset != TimeSpan.Zero || cutoff.UtcTicks % TimeSpan.FromMinutes(15).Ticks != 0) return false;
        var (zone, open, exit, frequency) = programme switch
        {
            "INFX7" => ("America/New_York", new TimeSpan(9, 0, 0), new TimeSpan(14, 45, 0), 15),
            "INFX8" => ("America/New_York", new TimeSpan(9, 0, 0), new TimeSpan(14, 45, 0), 30),
            "INFX9" => ("Europe/London", new TimeSpan(7, 0, 0), new TimeSpan(16, 45, 0), 15),
            "INFX10" => ("Europe/London", new TimeSpan(7, 0, 0), new TimeSpan(16, 45, 0), 60),
            _ => throw new InvalidOperationException("DEMO_PROGRAMME_UNKNOWN")
        };
        var local = TimeZoneInfo.ConvertTime(cutoff, TimeZoneInfo.FindSystemTimeZoneById(zone));
        return local.TimeOfDay >= open && local.TimeOfDay < exit && local.Minute % frequency == 0;
    }
}
