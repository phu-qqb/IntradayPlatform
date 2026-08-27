using Microsoft.EntityFrameworkCore;
using Npgsql;
using QQ.Production.Intraday.Infrastructure.PostgreSql;
using QQ.Production.Intraday.Lmax.ConnectivityLab;

var arch7bObserver = new DeferredArch7bPostgreSqlFixLifecycleObserver(
    CreateArch7bObserver);
var runner = new LmaxConnectivityLabRunner(
    new PlaceholderLmaxPublicDataClient(),
    new LmaxAccountApiClient(new LmaxConnectivityLabSafetyValidator()),
    new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator(), arch7bObserver),
    new LmaxConnectivityLabSafetyValidator());

return await runner.RunAsync(args, CancellationToken.None);

static Arch7bPostgreSqlFixLifecycleObserver CreateArch7bObserver(LmaxFixArch7bKnownOrderRequest request)
{
    var environmentVariable = Arch7bPostgreSqlPersistenceTarget.ConnectionEnvironmentVariable(request);
    var value = Environment.GetEnvironmentVariable(environmentVariable);
    if (string.IsNullOrWhiteSpace(value))
        throw new InvalidOperationException(
            $"ARCH7B_POSTGRESQL_CONNECTION_MISSING:{environmentVariable}");
    var connection = new NpgsqlConnectionStringBuilder(value);
    Arch7bPostgreSqlPersistenceTarget.ValidateResolvedConnection(
        request, connection.Host, connection.Port, connection.Database);
    var options = new DbContextOptionsBuilder<PmsShadowDbContext>()
        .UseNpgsql(connection.ConnectionString, npgsql => npgsql.SetPostgresVersion(16, 0))
        .Options;
    var factory = new Arch7bContextFactory(options);
    return new(factory, new EfArch7bKnownOrderLifecycleStore(factory), connection.Database ?? string.Empty);
}

file sealed class Arch7bContextFactory(
    DbContextOptions<PmsShadowDbContext> options) : IDbContextFactory<PmsShadowDbContext>
{
    public PmsShadowDbContext CreateDbContext() => new(options);

    public Task<PmsShadowDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}
