using Microsoft.EntityFrameworkCore;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;

namespace QQ.Production.Intraday.Infrastructure.SqlServer;

public sealed class SqlServerOperatorGovernanceRepository(IntradayDbContext dbContext) : IOperatorGovernanceRepository
{
    public async Task<IReadOnlyList<OperatorUser>> GetOperatorsAsync(CancellationToken cancellationToken)
        => await dbContext.OperatorUsers.AsNoTracking().OrderBy(x => x.OperatorId).ToListAsync(cancellationToken);

    public Task<OperatorUser?> GetOperatorByIdAsync(string operatorId, CancellationToken cancellationToken)
        => dbContext.OperatorUsers.AsNoTracking().FirstOrDefaultAsync(x => x.OperatorId == operatorId, cancellationToken);

    public async Task<IReadOnlyList<OperatorUserRole>> GetRolesAsync(OperatorUserId operatorUserId, CancellationToken cancellationToken)
        => await dbContext.OperatorUserRoles.AsNoTracking().Where(x => x.OperatorUserId == operatorUserId).OrderBy(x => x.Role).ToListAsync(cancellationToken);

    public async Task UpsertOperatorAsync(OperatorUser user, IReadOnlyList<OperatorUserRole> roles, CancellationToken cancellationToken)
    {
        var existing = await dbContext.OperatorUsers.FirstOrDefaultAsync(x => x.OperatorId == user.OperatorId, cancellationToken);
        if (existing is null)
        {
            dbContext.OperatorUsers.Add(user);
        }
        else
        {
            dbContext.Entry(existing).CurrentValues.SetValues(user);
        }

        var existingRoles = await dbContext.OperatorUserRoles.Where(x => x.OperatorUserId == user.Id).ToListAsync(cancellationToken);
        dbContext.OperatorUserRoles.RemoveRange(existingRoles);
        dbContext.OperatorUserRoles.AddRange(roles);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddApprovalRequestAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        dbContext.ApprovalRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateApprovalRequestAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        var existing = await dbContext.ApprovalRequests.FirstAsync(x => x.Id == request.Id, cancellationToken);
        dbContext.Entry(existing).CurrentValues.SetValues(request);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddApprovalDecisionAsync(ApprovalDecision decision, CancellationToken cancellationToken)
    {
        dbContext.ApprovalDecisions.Add(decision);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<ApprovalRequest?> GetApprovalRequestAsync(ApprovalRequestId id, CancellationToken cancellationToken)
        => dbContext.ApprovalRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ApprovalRequest>> GetApprovalRequestsAsync(ApprovalRequestFilter filter, CancellationToken cancellationToken)
    {
        var query = dbContext.ApprovalRequests.AsNoTracking().AsQueryable();
        if (filter.Status is not null) query = query.Where(x => x.Status == filter.Status);
        if (filter.Type is not null) query = query.Where(x => x.Type == filter.Type);
        if (!string.IsNullOrWhiteSpace(filter.RequestedBy)) query = query.Where(x => x.RequestedByOperatorId == filter.RequestedBy);
        if (!string.IsNullOrWhiteSpace(filter.EntityType)) query = query.Where(x => x.EntityType == filter.EntityType);
        if (!string.IsNullOrWhiteSpace(filter.EntityId)) query = query.Where(x => x.EntityId == filter.EntityId);
        return await query.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(filter.Limit, 1, 500)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovalDecision>> GetApprovalDecisionsAsync(ApprovalRequestId id, CancellationToken cancellationToken)
        => await dbContext.ApprovalDecisions.AsNoTracking().Where(x => x.ApprovalRequestId == id).OrderBy(x => x.DecidedAtUtc).ToListAsync(cancellationToken);
}
