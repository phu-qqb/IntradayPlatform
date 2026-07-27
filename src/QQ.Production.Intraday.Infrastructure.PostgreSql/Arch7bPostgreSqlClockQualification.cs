namespace QQ.Production.Intraday.Infrastructure.PostgreSql;

public sealed record Arch7bPostgreSqlClockQualification(
    string ContractVersion,
    string PostgreSqlVersion,
    bool TransactionReadOnly,
    IReadOnlyList<Arch7bPostgreSqlClockSample> Samples,
    bool SamplesMonotonic,
    bool NoDatabaseWrite);
