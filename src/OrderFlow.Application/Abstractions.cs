using System.Collections.Concurrent;
using OrderFlow.Domain;

namespace OrderFlow.Application;

public interface IOrderFlowStore
{
    Task<List<Product>> SearchProductsAsync(string? q, CancellationToken ct = default);
    Task<Product?> GetProductAsync(int id, CancellationToken ct = default);
    Task<List<Customer>> GetCustomersAsync(CancellationToken ct = default);
    Task<Customer?> GetCustomerAsync(int id, CancellationToken ct = default);
    Task<Order?> GetOrderAsync(int id, CancellationToken ct = default);
    Task<(List<Order> Items, int Total)> SearchOrdersAsync(
        int? customerId, string? q, bool caseSensitive, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountPlacedOrdersAsync(int customerId, int excludingOrderId, DateTime placedBeforeUtc, CancellationToken ct = default);
    void Add(Order order);
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Removes all data and restores the demo data set (release 1.1).</summary>
    Task ResetAsync(CancellationToken ct = default);
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}

public interface IClock
{
    DateTime UtcNow { get; }
}

/// <summary>A message handed to the mail gateway. Release 1.1 keeps a copy of every message in the outbox.</summary>
public sealed record OutboxMessage(int Id, DateTime SentAtUtc, string To, string Subject, string Body);

/// <summary>Copies of the e-mails sent since the application started (or since the last reset).</summary>
public sealed class Outbox
{
    private readonly ConcurrentQueue<OutboxMessage> _messages = new();
    private int _seq;

    public OutboxMessage Add(DateTime sentAtUtc, string to, string subject, string body)
    {
        var m = new OutboxMessage(Interlocked.Increment(ref _seq), sentAtUtc, to, subject, body);
        _messages.Enqueue(m);
        while (_messages.Count > 500 && _messages.TryDequeue(out _)) { }
        return m;
    }

    public IReadOnlyList<OutboxMessage> Snapshot() => _messages.Reverse().ToList();

    public void Clear() { while (_messages.TryDequeue(out _)) { } }
}

/// <summary>Short-lived cache of order-list pages, keyed by user, search and page.</summary>
public sealed class OrdersListCache
{
    private sealed record Entry(List<Order> Items, int Total, DateTime ExpiresAtUtc);
    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public static string Key(int? customerId, string? q, int page, int pageSize) =>
        $"{customerId?.ToString() ?? "*"}|{q?.Trim().ToLowerInvariant()}|{page}|{pageSize}";

    public bool TryGet(string key, out (List<Order> Items, int Total) value)
    {
        if (_entries.TryGetValue(key, out var e) && e.ExpiresAtUtc > DateTime.UtcNow)
        {
            value = (e.Items, e.Total);
            return true;
        }
        _entries.TryRemove(key, out _);
        value = default;
        return false;
    }

    public void Set(string key, List<Order> items, int total, TimeSpan ttl) =>
        _entries[key] = new Entry(items, total, DateTime.UtcNow.Add(ttl));

    public void Clear() => _entries.Clear();
}

/// <summary>Serialises order placement so that stock is checked and reduced by one request at a time.</summary>
public sealed class StockGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public Task WaitAsync(CancellationToken ct) => _gate.WaitAsync(ct);
    public void Release() => _gate.Release();
}
