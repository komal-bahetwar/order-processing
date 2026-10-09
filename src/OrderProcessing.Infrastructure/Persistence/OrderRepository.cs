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

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> GetPagedAsync(
        OrderStatus? status,
        int skip,
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

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(order => order.CreatedAt)
            .ThenBy(order => order.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Order>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default) =>
        await _dbContext.Orders
            .Where(order => order.Status == OrderStatus.Pending)
            .OrderBy(order => order.CreatedAt)
            .ThenBy(order => order.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public void Add(Order order) => _dbContext.Orders.Add(order);
}
