using Microsoft.EntityFrameworkCore;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Domain;

namespace OrderProcessing.Infrastructure.Persistence;

public sealed class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _dbContext;

    public OrderRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.Orders
            .Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Order>> GetAsync(
        OrderStatus? status,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .AsQueryable();

        if (status is not null)
        {
            query = query.Where(order => order.Status == status);
        }

        return await query
            .OrderBy(order => order.CreatedAt)
            .ThenBy(order => order.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetPendingIdsAsync(
        int batchSize,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Orders
            .Where(order => order.Status == OrderStatus.Pending)
            .OrderBy(order => order.CreatedAt)
            .ThenBy(order => order.Id)
            .Take(batchSize)
            .Select(order => order.Id)
            .ToListAsync(cancellationToken);

    public void Add(Order order) => _dbContext.Orders.Add(order);
}
