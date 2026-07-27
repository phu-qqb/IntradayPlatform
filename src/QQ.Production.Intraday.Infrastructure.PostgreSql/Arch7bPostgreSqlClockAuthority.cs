using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public static class Arch7bPostgreSqlClockAuthorityContract
{
    public const string Version = "arch7b_postgresql_database_clock_authority_v1";
    public const string PostgreSqlType = "timestamp with time zone";
    public const string SessionTimeZone = "UTC";
    public const decimal MaximumTypedVsEpochDeltaMicroseconds = 1m;
    public const string ClrMappingInvalid =
        "ARCH7B_POSITION_IMPORT_DATABASE_CLOCK_CLR_MAPPING_INVALID";
    public const string EpochMismatch =
        "ARCH7B_POSITION_IMPORT_DATABASE_CLOCK_EPOCH_MISMATCH";
    public const string PostgreSqlTypeInvalid =
        "ARCH7B_POSITION_IMPORT_DATABASE_CLOCK_TYPE_INVALID";
    public const string SessionTimeZoneInvalid =
        "ARCH7B_POSITION_IMPORT_DATABASE_TIMEZONE_NOT_UTC";
    public const string Sql = """
        WITH sample AS
        (
            SELECT clock_timestamp() AS database_clock
        )
        SELECT
            database_clock,
            EXTRACT(EPOCH FROM database_clock)::numeric,
            pg_typeof(database_clock)::text,
            current_setting('TimeZone')
        FROM sample;
        """;
}

public sealed record Arch7bPostgreSqlClockSample(
    string ContractVersion,
    DateTimeOffset DatabaseUtc,
    string PostgreSqlType,
    string SessionTimeZone,
    string ClrType,
    string? ClrDateTimeKind,
    string? ClrOffset,
    decimal EpochSeconds,
    DateTimeOffset EpochDerivedUtc,
    decimal TypedVsEpochDeltaMicroseconds,
    DateTimeOffset QueryStartedAtDiagnosticHostUtc,
    DateTimeOffset QueryCompletedAtDiagnosticHostUtc,
    string EvidenceSha256);

public static class Arch7bPostgreSqlClockAuthority
{
    public static async Task<Arch7bPostgreSqlClockSample> ReadAsync(
        DbConnection connection,
        DbTransaction? transaction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var queryStartedAtDiagnosticHostUtc = DateTimeOffset.UtcNow;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Arch7bPostgreSqlClockAuthorityContract.Sql;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SingleResult, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.FieldCount != 4)
            throw new InvalidDataException(
                Arch7bPostgreSqlClockAuthorityContract.ClrMappingInvalid);
        var databaseClock = reader.GetValue(0);
        var epoch = reader.GetValue(1);
        var postgreSqlType = reader.GetString(2);
        var sessionTimeZone = reader.GetString(3);
        if (await reader.ReadAsync(cancellationToken) ||
            await reader.NextResultAsync(cancellationToken))
            throw new InvalidDataException(
                Arch7bPostgreSqlClockAuthorityContract.ClrMappingInvalid);
        var queryCompletedAtDiagnosticHostUtc = DateTimeOffset.UtcNow;
        return Create(
            databaseClock,
            epoch,
            postgreSqlType,
            sessionTimeZone,
            queryStartedAtDiagnosticHostUtc,
            queryCompletedAtDiagnosticHostUtc);
    }

    public static Arch7bPostgreSqlClockSample Create(
        object? databaseClock,
        object? epochValue,
        string postgreSqlType,
        string sessionTimeZone,
        DateTimeOffset queryStartedAtDiagnosticHostUtc,
        DateTimeOffset queryCompletedAtDiagnosticHostUtc)
    {
        RequireUtc(queryStartedAtDiagnosticHostUtc,
            Arch7bPostgreSqlClockAuthorityContract.ClrMappingInvalid);
        RequireUtc(queryCompletedAtDiagnosticHostUtc,
            Arch7bPostgreSqlClockAuthorityContract.ClrMappingInvalid);
        if (queryCompletedAtDiagnosticHostUtc < queryStartedAtDiagnosticHostUtc)
            throw new InvalidDataException(
                Arch7bPostgreSqlClockAuthorityContract.ClrMappingInvalid);
        if (postgreSqlType !=
            Arch7bPostgreSqlClockAuthorityContract.PostgreSqlType)
            throw new InvalidDataException(
                Arch7bPostgreSqlClockAuthorityContract.PostgreSqlTypeInvalid);
        if (sessionTimeZone !=
            Arch7bPostgreSqlClockAuthorityContract.SessionTimeZone)
            throw new InvalidDataException(
                Arch7bPostgreSqlClockAuthorityContract.SessionTimeZoneInvalid);

        var (databaseUtc, clrType, clrDateTimeKind, clrOffset) =
            ReadTypedDatabaseUtc(databaseClock);
        if (epochValue is not decimal epochSeconds)
            throw new InvalidDataException(
                Arch7bPostgreSqlClockAuthorityContract.EpochMismatch);
        var epochDerivedUtc = FromUnixEpochSeconds(epochSeconds);
        var deltaMicroseconds =
            Math.Abs((databaseUtc - epochDerivedUtc).Ticks) /
            (decimal)TimeSpan.TicksPerMicrosecond;
        if (deltaMicroseconds >
            Arch7bPostgreSqlClockAuthorityContract
                .MaximumTypedVsEpochDeltaMicroseconds)
            throw new InvalidDataException(
                Arch7bPostgreSqlClockAuthorityContract.EpochMismatch);

        var sample = new Arch7bPostgreSqlClockSample(
            Arch7bPostgreSqlClockAuthorityContract.Version,
            databaseUtc,
            postgreSqlType,
            sessionTimeZone,
            clrType,
            clrDateTimeKind,
            clrOffset,
            epochSeconds,
            epochDerivedUtc,
            deltaMicroseconds,
            queryStartedAtDiagnosticHostUtc,
            queryCompletedAtDiagnosticHostUtc,
            string.Empty);
        return sample with { EvidenceSha256 = ComputeSha256(sample) };
    }

    public static DateTimeOffset FromUnixEpochSeconds(decimal epochSeconds)
    {
        decimal ticks;
        try
        {
            ticks = epochSeconds * TimeSpan.TicksPerSecond;
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException(
                Arch7bPostgreSqlClockAuthorityContract.EpochMismatch, exception);
        }
        if (ticks != decimal.Truncate(ticks) ||
            ticks is < long.MinValue or > long.MaxValue)
            throw new InvalidDataException(
                Arch7bPostgreSqlClockAuthorityContract.EpochMismatch);
        try
        {
            return DateTimeOffset.UnixEpoch.AddTicks(decimal.ToInt64(ticks));
        }
        catch (Exception exception) when (
            exception is OverflowException or ArgumentOutOfRangeException)
        {
            throw new InvalidDataException(
                Arch7bPostgreSqlClockAuthorityContract.EpochMismatch, exception);
        }
    }

    public static string ComputeSha256(Arch7bPostgreSqlClockSample sample)
    {
        var canonical = string.Join("\n",
            sample.ContractVersion,
            sample.DatabaseUtc.ToString("O", CultureInfo.InvariantCulture),
            sample.PostgreSqlType,
            sample.SessionTimeZone,
            sample.ClrType,
            sample.ClrDateTimeKind ?? string.Empty,
            sample.ClrOffset ?? string.Empty,
            sample.EpochSeconds.ToString(CultureInfo.InvariantCulture),
            sample.EpochDerivedUtc.ToString("O", CultureInfo.InvariantCulture),
            sample.TypedVsEpochDeltaMicroseconds.ToString(
                CultureInfo.InvariantCulture),
            sample.QueryStartedAtDiagnosticHostUtc.ToString(
                "O", CultureInfo.InvariantCulture),
            sample.QueryCompletedAtDiagnosticHostUtc.ToString(
                "O", CultureInfo.InvariantCulture));
        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static (
        DateTimeOffset DatabaseUtc,
        string ClrType,
        string? ClrDateTimeKind,
        string? ClrOffset)
        ReadTypedDatabaseUtc(object? value)
    {
        switch (value)
        {
            case DateTime dateTime when dateTime.Kind == DateTimeKind.Utc:
                return (
                    new DateTimeOffset(dateTime),
                    typeof(DateTime).FullName!,
                    dateTime.Kind.ToString(),
                    TimeSpan.Zero.ToString("c", CultureInfo.InvariantCulture));
            case DateTimeOffset dateTimeOffset
                when dateTimeOffset.Offset == TimeSpan.Zero:
                return (
                    dateTimeOffset,
                    typeof(DateTimeOffset).FullName!,
                    null,
                    dateTimeOffset.Offset.ToString(
                        "c", CultureInfo.InvariantCulture));
            default:
                throw new InvalidDataException(
                    Arch7bPostgreSqlClockAuthorityContract.ClrMappingInvalid);
        }
    }

    private static void RequireUtc(DateTimeOffset value, string blocker)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new InvalidDataException(blocker);
    }
}
