using System.Globalization;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bPostgreSqlClockAuthorityTests
{
    private static readonly DateTimeOffset DatabaseUtc =
        new(2026, 7, 27, 18, 9, 38, 123, 456, TimeSpan.Zero);
    private static readonly DateTimeOffset DiagnosticStart =
        new(2026, 7, 27, 18, 9, 38, TimeSpan.Zero);
    private static readonly DateTimeOffset DiagnosticEnd =
        DiagnosticStart.AddMilliseconds(5);

    [Fact]
    public void T01_DateTimeUtcIsAccepted()
    {
        var sample = Create(DatabaseUtc.UtcDateTime, Epoch(DatabaseUtc));
        Assert.Equal(DatabaseUtc, sample.DatabaseUtc);
        Assert.Equal("System.DateTime", sample.ClrType);
        Assert.Equal("Utc", sample.ClrDateTimeKind);
        Assert.Equal("00:00:00", sample.ClrOffset);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void T02_NonUtcDateTimeIsRejected(DateTimeKind kind)
    {
        var value = new DateTime(
            2026, 7, 27, 18, 9, 38, kind);
        AssertBlocker(
            Arch7bPostgreSqlClockAuthorityContract.ClrMappingInvalid,
            () => Create(value, Epoch(DatabaseUtc)));
    }

    [Fact]
    public void T03_DateTimeOffsetUtcIsAccepted()
    {
        var sample = Create(DatabaseUtc, Epoch(DatabaseUtc));
        Assert.Equal(DatabaseUtc, sample.DatabaseUtc);
        Assert.Equal("System.DateTimeOffset", sample.ClrType);
        Assert.Null(sample.ClrDateTimeKind);
        Assert.Equal("00:00:00", sample.ClrOffset);
    }

    [Fact]
    public void T04_DateTimeOffsetNonUtcIsRejected()
    {
        AssertBlocker(
            Arch7bPostgreSqlClockAuthorityContract.ClrMappingInvalid,
            () => Create(DatabaseUtc.ToOffset(TimeSpan.FromHours(2)),
                Epoch(DatabaseUtc)));
    }

    [Theory]
    [MemberData(nameof(InvalidClrValues))]
    public void T05_InvalidClrValuesAreRejected(object? value)
    {
        AssertBlocker(
            Arch7bPostgreSqlClockAuthorityContract.ClrMappingInvalid,
            () => Create(value, Epoch(DatabaseUtc)));
    }

    public static TheoryData<object?> InvalidClrValues => new()
    {
        "2026-07-27T18:09:38Z",
        DBNull.Value,
        42,
        null
    };

    [Fact]
    public void T06_ExactTypedAndEpochUtcIsAccepted()
    {
        var sample = Create(DatabaseUtc.UtcDateTime, Epoch(DatabaseUtc));
        Assert.Equal(DatabaseUtc, sample.EpochDerivedUtc);
        Assert.Equal(0m, sample.TypedVsEpochDeltaMicroseconds);
    }

    [Fact]
    public void T07_OneMicrosecondDeltaIsAccepted()
    {
        var epochUtc = DatabaseUtc.AddMicroseconds(-1);
        var sample = Create(DatabaseUtc.UtcDateTime, Epoch(epochUtc));
        Assert.Equal(1m, sample.TypedVsEpochDeltaMicroseconds);
    }

    [Fact]
    public void T08_MoreThanOneMicrosecondDeltaIsRejected()
    {
        AssertBlocker(
            Arch7bPostgreSqlClockAuthorityContract.EpochMismatch,
            () => Create(DatabaseUtc.UtcDateTime,
                Epoch(DatabaseUtc.AddMicroseconds(-2))));
    }

    [Fact]
    public void T09_NegativeEpochIsPreserved()
    {
        var beforeEpoch = new DateTimeOffset(
            1969, 12, 31, 23, 59, 59, 123, 456, TimeSpan.Zero);
        var sample = Create(beforeEpoch.UtcDateTime, Epoch(beforeEpoch));
        Assert.True(sample.EpochSeconds < 0);
        Assert.Equal(beforeEpoch, sample.EpochDerivedUtc);
    }

    [Fact]
    public void T10_SubsecondPrecisionIsPreserved()
    {
        var sample = Create(DatabaseUtc.UtcDateTime, Epoch(DatabaseUtc));
        Assert.Equal(1234560, sample.DatabaseUtc.Ticks % TimeSpan.TicksPerSecond);
        Assert.Equal(sample.DatabaseUtc.Ticks, sample.EpochDerivedUtc.Ticks);
    }

    [Fact]
    public void T11_SubTickEpochIsRejectedInsteadOfRounded()
    {
        AssertBlocker(
            Arch7bPostgreSqlClockAuthorityContract.EpochMismatch,
            () => Create(DatabaseUtc.UtcDateTime,
                Epoch(DatabaseUtc) + 0.00000001m));
    }

    [Fact]
    public void T12_EpochClrMappingMustRemainDecimal()
    {
        AssertBlocker(
            Arch7bPostgreSqlClockAuthorityContract.EpochMismatch,
            () => Arch7bPostgreSqlClockAuthority.Create(
                DatabaseUtc.UtcDateTime,
                (double)Epoch(DatabaseUtc),
                Arch7bPostgreSqlClockAuthorityContract.PostgreSqlType,
                Arch7bPostgreSqlClockAuthorityContract.SessionTimeZone,
                DiagnosticStart,
                DiagnosticEnd));
    }

    [Fact]
    public void T13_PostgreSqlTypeMustBeTimestampWithTimeZone()
    {
        AssertBlocker(
            Arch7bPostgreSqlClockAuthorityContract.PostgreSqlTypeInvalid,
            () => Arch7bPostgreSqlClockAuthority.Create(
                DatabaseUtc.UtcDateTime,
                Epoch(DatabaseUtc),
                "timestamp without time zone",
                Arch7bPostgreSqlClockAuthorityContract.SessionTimeZone,
                DiagnosticStart,
                DiagnosticEnd));
    }

    [Fact]
    public void T14_SessionTimeZoneMustBeUtc()
    {
        AssertBlocker(
            Arch7bPostgreSqlClockAuthorityContract.SessionTimeZoneInvalid,
            () => Arch7bPostgreSqlClockAuthority.Create(
                DatabaseUtc.UtcDateTime,
                Epoch(DatabaseUtc),
                Arch7bPostgreSqlClockAuthorityContract.PostgreSqlType,
                "Europe/Paris",
                DiagnosticStart,
                DiagnosticEnd));
    }

    [Fact]
    public void T15_QueryUsesOneCteInstant()
    {
        var sql = Arch7bPostgreSqlClockAuthorityContract.Sql;
        Assert.Equal(1, Count(sql, "clock_timestamp()"));
        Assert.Contains("WITH sample AS", sql, StringComparison.Ordinal);
        Assert.Contains("EXTRACT(EPOCH FROM database_clock)::numeric",
            sql, StringComparison.Ordinal);
        Assert.Contains("pg_typeof(database_clock)::text",
            sql, StringComparison.Ordinal);
        Assert.Contains("current_setting('TimeZone')",
            sql, StringComparison.Ordinal);
    }

    [Fact]
    public void T16_CulturesDoNotChangeDatabaseUtc()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var name in new[] { "en-US", "fr-FR", "ar-SA" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
                Assert.Equal(DatabaseUtc,
                    Create(DatabaseUtc.UtcDateTime, Epoch(DatabaseUtc)).DatabaseUtc);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void T17_HostOffsetsCannotChangeTypedDatabaseUtc()
    {
        foreach (var hostOffset in new[]
                 {
                     TimeSpan.Zero,
                     TimeSpan.FromHours(2),
                     TimeSpan.FromHours(-5)
                 })
        {
            var simulatedHostDiagnostic = DiagnosticStart.ToOffset(hostOffset)
                .ToUniversalTime();
            var sample = Arch7bPostgreSqlClockAuthority.Create(
                DatabaseUtc.UtcDateTime,
                Epoch(DatabaseUtc),
                Arch7bPostgreSqlClockAuthorityContract.PostgreSqlType,
                Arch7bPostgreSqlClockAuthorityContract.SessionTimeZone,
                simulatedHostDiagnostic,
                simulatedHostDiagnostic.AddMilliseconds(5));
            Assert.Equal(DatabaseUtc, sample.DatabaseUtc);
        }
    }

    [Fact]
    public void T18_RegressionFixtureProvesLegacyMinusTwoHours()
    {
        var legacyDriverValue = new DateTime(
            2026, 7, 27, 18, 9, 38, DateTimeKind.Unspecified);
        var legacyString = legacyDriverValue.ToString(
            CultureInfo.InvariantCulture);
        var legacyCalculatedUtc = new DateTimeOffset(
            legacyDriverValue, TimeSpan.FromHours(2)).ToUniversalTime();
        var corrected = Create(
            new DateTime(2026, 7, 27, 18, 9, 38, DateTimeKind.Utc),
            Epoch(new DateTimeOffset(
                2026, 7, 27, 18, 9, 38, TimeSpan.Zero)));

        Assert.Equal("07/27/2026 18:09:38", legacyString);
        Assert.Equal(new DateTimeOffset(
            2026, 7, 27, 16, 9, 38, TimeSpan.Zero), legacyCalculatedUtc);
        Assert.Equal(TimeSpan.FromHours(-2),
            legacyCalculatedUtc - corrected.DatabaseUtc);
        Assert.Equal(new DateTimeOffset(
            2026, 7, 27, 18, 9, 38, TimeSpan.Zero), corrected.DatabaseUtc);
    }

    [Fact]
    public void T19_EvidenceShaIsCanonical()
    {
        var sample = Create(DatabaseUtc.UtcDateTime, Epoch(DatabaseUtc));
        Assert.Equal(sample.EvidenceSha256,
            Arch7bPostgreSqlClockAuthority.ComputeSha256(sample));
        Assert.Equal(64, sample.EvidenceSha256.Length);
    }

    [Fact]
    public void T20_ProductionClockPathHasNoStringRoundTrip()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "QQ.Production.Intraday.Infrastructure.PostgreSql",
            "Arch7bAppendOnlyGlobalFlatPositionImport.cs"));
        Assert.DoesNotContain(
            "ScalarAsync(context, \"SELECT clock_timestamp()\"",
            source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "DateTimeOffset.Parse",
            source, StringComparison.Ordinal);
        Assert.Contains(
            "Arch7bPostgreSqlClockAuthority.ReadAsync",
            source, StringComparison.Ordinal);
    }

    private static Arch7bPostgreSqlClockSample Create(
        object? value,
        decimal epoch) =>
        Arch7bPostgreSqlClockAuthority.Create(
            value,
            epoch,
            Arch7bPostgreSqlClockAuthorityContract.PostgreSqlType,
            Arch7bPostgreSqlClockAuthorityContract.SessionTimeZone,
            DiagnosticStart,
            DiagnosticEnd);

    private static decimal Epoch(DateTimeOffset value) =>
        (value.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) /
        (decimal)TimeSpan.TicksPerSecond;

    private static int Count(string value, string fragment)
    {
        var count = 0;
        var start = 0;
        while ((start = value.IndexOf(
                   fragment, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += fragment.Length;
        }
        return count;
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(
                    current.FullName, "QQ.Production.Intraday.sln")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("REPOSITORY_ROOT_NOT_FOUND");
    }

    private static void AssertBlocker(string blocker, Action action)
    {
        var error = Assert.Throws<InvalidDataException>(action);
        Assert.Equal(blocker, error.Message);
    }
}
