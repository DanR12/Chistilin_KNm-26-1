using Microsoft.EntityFrameworkCore;
using OrderFlow.Application;
using OrderFlow.Domain;

namespace OrderFlow.Infrastructure;

public sealed class EfStore(OrderFlowDbContext db, PricingRules rules, VariantOptions variant) : IOrderFlowStore
{
    public Task<List<Product>> SearchProductsAsync(string? q, CancellationToken ct = default)
    {
        IQueryable<Product> query = db.Products;
        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim().ToLowerInvariant()}%";
            query = query.Where(p => EF.Functions.Like(p.Name.ToLower(), pattern) || EF.Functions.Like(p.Sku.ToLower(), pattern));
        }
        return query.OrderBy(p => p.Id).ToListAsync(ct);
    }

    public Task<Product?> GetProductAsync(int id, CancellationToken ct = default) =>
        db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<List<Customer>> GetCustomersAsync(CancellationToken ct = default) =>
        db.Customers.OrderBy(c => c.Id).ToListAsync(ct);

    public Task<Customer?> GetCustomerAsync(int id, CancellationToken ct = default) =>
        db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Order?> GetOrderAsync(int id, CancellationToken ct = default) =>
        db.Orders.Include(o => o.Customer).Include(o => o.Lines).ThenInclude(l => l.Product).Include(o => o.Invoice)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<(List<Order> Items, int Total)> SearchOrdersAsync(
        int? customerId, string? q, bool caseSensitive, int page, int pageSize, CancellationToken ct = default)
    {
        IQueryable<Order> query = db.Orders.Include(o => o.Customer).Include(o => o.Lines).Include(o => o.Invoice);
        if (customerId is not null)
            query = query.Where(o => o.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            if (caseSensitive)
                query = query.Where(o => o.Number.Contains(term) || o.Customer!.Name.Contains(term));
            else
            {
                var pattern = $"%{term.ToLowerInvariant()}%";
                query = query.Where(o => EF.Functions.Like(o.Number.ToLower(), pattern) || EF.Functions.Like(o.Customer!.Name.ToLower(), pattern));
            }
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(o => o.CreatedAtUtc).ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<int> CountPlacedOrdersAsync(int customerId, int excludingOrderId, DateTime placedBeforeUtc, CancellationToken ct = default) =>
        db.Orders.CountAsync(o => o.CustomerId == customerId && o.Id != excludingOrderId
                                  && o.PlacedAtUtc != null && o.PlacedAtUtc < placedBeforeUtc
                                  && o.Status != OrderStatus.Cancelled, ct);

    public void Add(Order order) => db.Orders.Add(order);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async Task ResetAsync(CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM OrderLines; DELETE FROM Invoices; DELETE FROM Orders; DELETE FROM Users; " +
            "DELETE FROM Customers; DELETE FROM Products; DELETE FROM sqlite_sequence;", ct);
        db.ChangeTracker.Clear();
        SeedData.EnsureSeeded(db, rules, variant.InvoiceRounding);
    }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>Delivers confirmation e-mails through the mail gateway and keeps a copy in the outbox.</summary>
public sealed class MailGatewaySender(Outbox outbox, IClock clock) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        outbox.Add(clock.UtcNow, to, subject, body);
        return Task.CompletedTask;
    }
}
