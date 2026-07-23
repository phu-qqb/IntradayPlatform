using Microsoft.EntityFrameworkCore;
using Npgsql;
using QQ.Production.Intraday.Infrastructure.PostgreSql;
using QQ.Production.Intraday.Lmax.ConnectivityLab;

const string Arch7bConnectionEnvironmentVariable = "QQ_PMS_SHADOW_ARCH7B_CONNECTION_STRING";
const string Arch7bTestDatabase = "qq_pms_shadow_arch6d_test";
var arch7bObserver = new DeferredArch7bPostgreSqlFixLifecycleObserver(CreateArch7bObserver);
var runner = new LmaxConnectivityLabRunner(
    new PlaceholderLmaxPublicDataClient(),
    new LmaxAccountApiClient(new LmaxConnectivityLabSafetyValidator()),
    new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator(), arch7bObserver),
    new LmaxConnectivityLabSafetyValidator());

return await runner.RunAsync(args, CancellationToken.None);

static Arch7bPostgreSqlFixLifecycleObserver CreateArch7bObserver()
{
    var value = Environment.GetEnvironmentVariable(Arch7bConnectionEnvironmentVariable);
    if (string.IsNullOrWhiteSpace(value))
        throw new InvalidOperationException(
            $"ARCH7B_POSTGRESQL_CONNECTION_MISSING:{Arch7bConnectionEnvironmentVariable}");
    var connection = new NpgsqlConnectionStringBuilder(value);
    if (connection.Database != Arch7bTestDatabase)
        throw new InvalidOperationException("ARCH7B_POSTGRESQL_DATABASE_NOT_TEST_TARGET");
    if (!IsLoopback(connection.Host))
        throw new InvalidOperationException("ARCH7B_POSTGRESQL_HOST_NOT_LOOPBACK");
    var options = new DbContextOptionsBuilder<PmsShadowDbContext>()
        .UseNpgsql(connection.ConnectionString, npgsql => npgsql.SetPostgresVersion(16, 0))
        .Options;
    var factory = new Arch7bContextFactory(options);
    return new(factory, new EfArch7bKnownOrderLifecycleStore(factory));
}

static bool IsLoopback(string? host)
    => !string.IsNullOrWhiteSpace(host) &&
       (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
       host.Equals("127.0.0.1", StringComparison.Ordinal) ||
       host.Equals("::1", StringComparison.Ordinal));

file sealed class Arch7bContextFactory(
    DbContextOptions<PmsShadowDbContext> options) : IDbContextFactory<PmsShadowDbContext>
{
    public PmsShadowDbContext CreateDbContext() => new(options);

    public Task<PmsShadowDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}
