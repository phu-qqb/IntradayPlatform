using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed class LmaxVenueGateway : IVenueExecutionGateway
{
    public Task<VenueExecutionResult> SendOrderAsync(VenueOrderRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException("Live LMAX connectivity is intentionally not implemented or registered.");

    public Task<VenueExecutionResult> CancelOrderAsync(VenueCancelRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException("Live LMAX connectivity is intentionally not implemented or registered.");

    public Task<IReadOnlyList<VenueOpenOrder>> GetOpenOrdersAsync(VenueId venueId, CancellationToken cancellationToken)
        => throw new NotImplementedException("Live LMAX connectivity is intentionally not implemented or registered.");
}
