namespace QQ.Production.Intraday.Lmax.ConnectivityLab;

/// <summary>Resolves the non-secret persistence target from the validated lifecycle activation.</summary>
public static class Arch7bPostgreSqlPersistenceTarget
{
    public const string DemoConnectionEnvironmentVariable = "QQ_PMS_SHADOW_ARCH7B_CONNECTION_STRING";
    public const string ProductionConnectionEnvironmentVariable = "QQ_PMS_ARCH7B_PRODUCTION_CONNECTION_STRING";
    public const string DemoDatabase = "qq_pms_shadow_arch6d_test";

    public static string ConnectionEnvironmentVariable(LmaxFixArch7bKnownOrderRequest request)
        => request.Activation == LmaxFixArch7bActivation.ProductionAuthorizedOnce
            ? ProductionConnectionEnvironmentVariable
            : DemoConnectionEnvironmentVariable;

    public static void ValidateResolvedConnection(
        LmaxFixArch7bKnownOrderRequest request,
        string? host,
        int port,
        string? database)
    {
        if (request.Activation != LmaxFixArch7bActivation.ProductionAuthorizedOnce)
        {
            if (!string.Equals(database, DemoDatabase, StringComparison.Ordinal))
                throw new InvalidOperationException("ARCH7B_POSTGRESQL_DATABASE_NOT_TEST_TARGET");
            if (!IsLoopback(host))
                throw new InvalidOperationException("ARCH7B_POSTGRESQL_HOST_NOT_LOOPBACK");
            return;
        }

        var binding = request.ProductionBinding ??
            throw new InvalidOperationException("ARCH7B_PRODUCTION_BINDING_MISSING");
        if (string.Equals(database, DemoDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException("ARCH7B_POSTGRESQL_DATABASE_NOT_PRODUCTION_TARGET");
        if (!string.Equals(host, binding.PersistenceHost, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ARCH7B_PRODUCTION_PERSISTENCE_HOST_BINDING_MISMATCH");
        if (port != binding.PersistencePort)
            throw new InvalidOperationException("ARCH7B_PRODUCTION_PERSISTENCE_PORT_BINDING_MISMATCH");
        if (!string.Equals(database, binding.PersistenceDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException("ARCH7B_PRODUCTION_PERSISTENCE_DATABASE_BINDING_MISMATCH");
    }

    private static bool IsLoopback(string? host)
        => !string.IsNullOrWhiteSpace(host) &&
           (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("127.0.0.1", StringComparison.Ordinal) ||
            host.Equals("::1", StringComparison.Ordinal));
}
