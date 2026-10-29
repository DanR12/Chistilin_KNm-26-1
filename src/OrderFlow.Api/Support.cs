using System.Collections.Concurrent;
using System.Security.Claims;
using OrderFlow.Application;
using OrderFlow.Domain;

namespace OrderFlow.Api;

/// <summary>Remembers form tokens that have already been accepted, so a repeated submission is ignored.</summary>
public sealed class SubmitGuard
{
    private readonly ConcurrentDictionary<string, DateTime> _seen = new();

    public bool TryConsume(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return true;
        foreach (var kv in _seen)
            if (kv.Value < DateTime.UtcNow.AddHours(-1)) _seen.TryRemove(kv.Key, out _);
        return _seen.TryAdd(token, DateTime.UtcNow);
    }
}

public readonly record struct CurrentUser(string Username, string Role, int? CustomerId)
{
    public bool IsManager => Role == "manager";

    public static CurrentUser From(ClaimsPrincipal principal)
    {
        var name = principal.Identity?.Name ?? "";
        var role = principal.FindFirstValue(ClaimTypes.Role) ?? "";
        var cid = principal.FindFirstValue("customerId");
        return new CurrentUser(name, role, int.TryParse(cid, out var id) ? id : null);
    }

    public bool CanSee(Order order, VariantOptions variant) =>
        IsManager || order.CustomerId == CustomerId || variant.SkipOrderOwnershipCheck;

    public bool Owns(Order order) => IsManager || order.CustomerId == CustomerId;
}

public static class Money
{
    public static string Fmt(decimal v) => v.ToString("0.00");
}

/// <summary>What the pages show for shipping and total.</summary>
public static class Display
{
    public static (decimal Shipping, decimal Total) ShippingAndTotal(OrderTotals t, VariantOptions variant, PricingRules rules)
    {
        var shipping = variant.ShippingBadgeOnSubtotal ? Pricing.ShippingFor(t.Subtotal, rules) : t.Shipping;
        var vat = Pricing.Round((t.Net + shipping) * rules.VatRate);
        return (shipping, t.Net + shipping + vat);
    }
}

/// <summary>Reads a page of the orders list, through the list cache when the build enables it.</summary>
public static class OrdersLookup
{
    public static async Task<(List<Order> Items, int Total)> SearchAsync(
        IOrderFlowStore store, OrdersListCache cache, VariantOptions variant, HttpResponse response,
        int? customerId, string? q, int page, int pageSize, CancellationToken ct = default)
    {
        if (!variant.OrdersListCached)
            return await store.SearchOrdersAsync(customerId, q, variant.OrderSearchCaseSensitive, page, pageSize, ct);

        var key = OrdersListCache.Key(customerId, q, page, pageSize);
        if (cache.TryGet(key, out var hit))
        {
            response.Headers["X-Cache"] = "HIT";
            return hit;
        }
        var result = await store.SearchOrdersAsync(customerId, q, variant.OrderSearchCaseSensitive, page, pageSize, ct);
        cache.Set(key, result.Items, result.Total, TimeSpan.FromSeconds(variant.CacheSeconds));
        response.Headers["X-Cache"] = "MISS";
        return result;
    }
}

/// <summary>One line of the request log (release 1.1).</summary>
public sealed record LogEntry(int Id, DateTime AtUtc, string Method, string Path, int Status, long ElapsedMs, string User,
    string? ErrorId, string? Error);

/// <summary>The last few hundred requests, newest first, with an error id for every failed one.</summary>
public sealed class RequestLog
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private int _seq;

    public void Add(DateTime atUtc, string method, string path, int status, long elapsedMs, string user, string? errorId, string? error)
    {
        _entries.Enqueue(new LogEntry(Interlocked.Increment(ref _seq), atUtc, method, path, status, elapsedMs, user, errorId, error));
        while (_entries.Count > 500 && _entries.TryDequeue(out _)) { }
    }

    public IReadOnlyList<LogEntry> Snapshot() => _entries.Reverse().ToList();

    public void Clear() { while (_entries.TryDequeue(out _)) { } }

    public static string NewErrorId() => Guid.NewGuid().ToString("N")[..8];
}

/// <summary>Records every request that reaches the application.</summary>
public sealed class RequestLogMiddleware(RequestDelegate next, RequestLog log)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "/";
        if (path.StartsWith("/site.css") || path.StartsWith("/swagger") || path.StartsWith("/favicon"))
        {
            await next(ctx);
            return;
        }
        var started = DateTime.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await next(ctx);
        }
        finally
        {
            sw.Stop();
            var user = ctx.User.Identity?.IsAuthenticated == true ? ctx.User.Identity!.Name ?? "?" : "anonymous";
            log.Add(started, ctx.Request.Method, path + ctx.Request.QueryString, ctx.Response.StatusCode, sw.ElapsedMilliseconds, user,
                ctx.Items["ErrorId"] as string, ctx.Items["Error"] as string);
        }
    }
}

/// <summary>Messages forwarded to the support inbox by the sales team. Each installation receives one.</summary>
public static class SupportInbox
{
    public sealed record Message(string From, string Subject, string ReceivedAtUtc, string Body);

    public static Message For(VariantOptions variant) => variant.IntermittentId switch
    {
        "I2" => new Message("Sales team", "Same last unit sold twice", "2026-09-21 08:12 UTC",
            "Hello,\n\nOn Friday we sold the same last unit twice. Two customers each got a confirmation e-mail for the " +
            "last laptop stand, both orders show as Placed, and the warehouse only had one to ship. One of the customers " +
            "is now very unhappy.\n\nHow did the system let this through? It has never happened before as far as we know, " +
            "and we could not make it happen again on Monday.\n\nRegards,\nSales"),
        "I3" => new Message("Accounting", "Invoice total does not match the order", "2026-09-22 15:40 UTC",
            "Hello,\n\nWe have found three invoices this month whose total due is one cent higher than the total shown on " +
            "the order in the system. It is only some invoices, not all of them, and we cannot see what those three have " +
            "in common. We need to know which figure is right before we close the month.\n\nCan you look into it?\n\nAccounting"),
        _ => new Message("Bob Melnyk (customer)", "My order was not in my list", "2026-09-19 10:03 UTC",
            "Hi,\n\nI placed an order this morning and when I went to my orders it was not there. I thought the order had " +
            "failed, so I nearly placed it again. I refreshed the page a couple of times and then it was there.\n\nThis has " +
            "happened twice this week. Is something wrong with my account?\n\nBob"),
    };
}
