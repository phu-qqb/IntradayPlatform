using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Application;

/// <summary>Opt-in seam for the explicitly configured continuing Demo gateway.
/// Preparation and repository writes remain serialized; only venue lifecycles overlap.</summary>
public interface ILmaxDemoBatchExecutionGateway : IVenueExecutionGateway
{
    Task<IReadOnlyList<VenueExecutionResult>> SendModelRunAsync(
        ModelRun run, IReadOnlyList<TargetPosition> targets,
        IReadOnlyList<VenueOrderRequest> orders, CancellationToken cancellationToken);
}
