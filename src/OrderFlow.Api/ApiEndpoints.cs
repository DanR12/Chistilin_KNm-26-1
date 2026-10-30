using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Application;
using OrderFlow.Domain;
using OrderFlow.Infrastructure;

namespace OrderFlow.Api;

public record LoginRequest(string Username, string Password);
public record LineRequest(int ProductId, int Quantity);
public record CreateOrderRequest(int? CustomerId, string? PromoCode, List<LineRequest>? Lines);
public record AddressRequest(string Address, string City, string Country);

public record ProductDto(int Id, string Sku, string Name, decimal Price, int Stock)
{
    public static ProductDto From(Product p) => new(p.Id, p.Sku, p.Name, p.Price, p.Stock);
}

public record CustomerDto(int Id, string Name, string Email, string Address, string City, string Country, string Tier)
{
    public static CustomerDto From(Customer c) => new(c.Id, c.Name, c.Email, c.Address, c.City, c.Country, c.Tier.ToString());
}

public record LineDto(int ProductId, string ProductName, decimal UnitPrice, int Quantity, double LineTotal);
public record TotalsDto(double Subtotal, double Discount, double Shipping, double Net, double Vat, double Total);
public record OrderDto(int Id, string Number, int CustomerId, string CustomerName, string Status, string? PromoCode, bool PromoApplied,
    DateTime CreatedAtUtc, DateTime? PlacedAtUtc, DateTime? PaidAtUtc, DateTime? ShippedAtUtc, DateTime? CancelledAtUtc,
    List<LineDto> Lines, TotalsDto Totals);
public record PageDto<T>(List<T> Items, int Page, int PageSize, int Total, int TotalPages);
public record InvoiceDto(string Number, DateTime IssuedAtUtc, string OrderNumber, string BillToName, string BillToAddress,
    List<LineDto> Lines, TotalsDto Totals);

public static class ApiEndpoints
{
    public static void MapApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/auth/login", async (LoginRequest req, HttpContext http, OrderFlowDbContext db) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username && u.Password == req.Password);
            if (user is null) return Results.Json(new { error = "Invalid username or password." }, statusCode: 401);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Principal(user));
            return Results.Ok(new { user.Username, user.Role, user.CustomerId });
        }).AllowAnonymous();

        api.MapPost("/auth/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization();

        api.MapGet("/catalog", async (string? q, IOrderFlowStore store) =>
            Results.Ok((await store.SearchProductsAsync(q)).Select(ProductDto.From))).AllowAnonymous();

        api.MapGet("/customers", async (IOrderFlowStore store) =>
            Results.Ok((await store.GetCustomersAsync()).Select(CustomerDto.From))).RequireAuthorization("Manager");

        api.MapGet("/customers/{id:int}", async (int id, HttpContext http, IOrderFlowStore store) =>
        {
            var me = CurrentUser.From(http.User);
            if (!me.IsManager && me.CustomerId != id) return Results.Forbid();
            var c = await store.GetCustomerAsync(id);
            return c is null ? NotFound("Customer not found.") : Results.Ok(CustomerDto.From(c));
        }).RequireAuthorization();

        api.MapPut("/customers/{id:int}/address", async (int id, AddressRequest req, HttpContext http, IOrderFlowStore store) =>
        {
            var me = CurrentUser.From(http.User);
            if (!me.IsManager && me.CustomerId != id) return Results.Forbid();
            var c = await store.GetCustomerAsync(id);
            if (c is null) return NotFound("Customer not found.");
            c.Address = req.Address?.Trim() ?? ""; c.City = req.City?.Trim() ?? ""; c.Country = req.Country?.Trim() ?? "";
            await store.SaveChangesAsync();
            return Results.Ok(CustomerDto.From(c));
        }).RequireAuthorization();

        api.MapGet("/orders", async (string? q, int? page, int? pageSize, HttpContext http, IOrderFlowStore store,
            OrderService orders, VariantOptions variant, OrdersListCache cache) =>
        {
            var me = CurrentUser.From(http.User);
            int p = Math.Max(1, page ?? 1), ps = Math.Clamp(pageSize ?? 10, 1, 50);
            var (items, total) = await OrdersLookup.SearchAsync(store, cache, variant, http.Response, me.IsManager ? null : me.CustomerId, q, p, ps);
            var dtos = new List<OrderDto>();
            foreach (var o in items) dtos.Add(await ToDto(o, orders, variant));
            return Results.Ok(new PageDto<OrderDto>(dtos, p, ps, total, (int)Math.Ceiling(total / (double)ps)));
        }).RequireAuthorization();

        api.MapPost("/orders", async (CreateOrderRequest req, HttpContext http, OrderService orders, VariantOptions variant) =>
        {
            var me = CurrentUser.From(http.User);
            int customerId;
            if (me.IsManager)
            {
                if (req.CustomerId is null) return BadRequest("customerId is required.");
                customerId = req.CustomerId.Value;
            }
            else customerId = me.CustomerId!.Value;
            var lines = (req.Lines ?? []).Select(l => (l.ProductId, l.Quantity));
            var order = await orders.CreateAsync(customerId, req.PromoCode, lines, validateQuantity: !variant.ApiSkipsQuantityValidation);
            return Results.Created($"/api/orders/{order.Id}", await ToDto(order, orders, variant));
        }).RequireAuthorization().WithSummary("Create a draft order").AddEndpointFilter<DomainErrorFilter>();

        api.MapGet("/orders/{id:int}", async (int id, HttpContext http, OrderService orders, VariantOptions variant) =>
        {
            var me = CurrentUser.From(http.User);
            var order = await orders.RequireAsync(id);
            if (!me.CanSee(order, variant)) return Results.Forbid();
            return Results.Ok(await ToDto(order, orders, variant));
        }).RequireAuthorization().AddEndpointFilter<DomainErrorFilter>();

        api.MapPost("/orders/{id:int}/lines", async (int id, LineRequest req, HttpContext http, OrderService orders, VariantOptions variant) =>
        {
            var me = CurrentUser.From(http.User);
            var order = await orders.RequireAsync(id);
            if (!me.Owns(order)) return Results.Forbid();
            order = await orders.AddLineAsync(id, req.ProductId, req.Quantity, validateQuantity: !variant.ApiSkipsQuantityValidation);
            return Results.Ok(await ToDto(order, orders, variant));
        }).RequireAuthorization().WithSummary("Add a line to a draft order").AddEndpointFilter<DomainErrorFilter>();

        api.MapPost("/orders/{id:int}/place", async (int id, HttpContext http, OrderService orders, VariantOptions variant) =>
        {
            var me = CurrentUser.From(http.User);
            var order = await orders.RequireAsync(id);
            if (!me.Owns(order)) return Results.Forbid();
            return Results.Ok(await ToDto(await orders.PlaceAsync(id), orders, variant));
        }).RequireAuthorization().WithSummary("Place a draft order").AddEndpointFilter<DomainErrorFilter>();

        api.MapPost("/orders/{id:int}/pay", async (int id, HttpContext http, OrderService orders, VariantOptions variant) =>
        {
            var me = CurrentUser.From(http.User);
            var order = await orders.RequireAsync(id);
            if (!me.Owns(order)) return Results.Forbid();
            return Results.Ok(await ToDto(await orders.PayAsync(id), orders, variant));
        }).RequireAuthorization().WithSummary("Pay a placed order (issues the invoice)").AddEndpointFilter<DomainErrorFilter>();

        api.MapPost("/orders/{id:int}/ship", async (int id, OrderService orders, VariantOptions variant) =>
            Results.Ok(await ToDto(await orders.ShipAsync(id), orders, variant)))
            .RequireAuthorization("Manager").WithSummary("Ship a paid order (manager)").AddEndpointFilter<DomainErrorFilter>();

        api.MapPost("/orders/{id:int}/cancel", async (int id, HttpContext http, OrderService orders, VariantOptions variant) =>
        {
            var me = CurrentUser.From(http.User);
            var order = await orders.RequireAsync(id);
            if (!me.Owns(order)) return Results.Forbid();
            return Results.Ok(await ToDto(await orders.CancelAsync(id), orders, variant));
        }).RequireAuthorization().ExcludeFromDescription().AddEndpointFilter<DomainErrorFilter>();

        api.MapGet("/orders/{id:int}/invoice", async (int id, HttpContext http, OrderService orders, VariantOptions variant) =>
        {
            var me = CurrentUser.From(http.User);
            var order = await orders.RequireAsync(id);
            if (!me.CanSee(order, variant)) return Results.Forbid();
            if (order.Invoice is null) return NotFound("No invoice has been issued for this order.");
            var inv = order.Invoice;
            var address = variant.InvoiceReadsLiveAddress ? order.Customer!.FullAddress : inv.BillToAddress;
            var name = variant.InvoiceReadsLiveAddress ? order.Customer!.Name : inv.BillToName;
            var totals = new OrderTotals(inv.Subtotal, inv.Discount, inv.Shipping, inv.Net, inv.Vat, inv.Total);
            return Results.Ok(new InvoiceDto(inv.Number, inv.IssuedAtUtc, order.Number, name, address,
                Lines(order, variant), Totals(order, totals, variant)));
        }).RequireAuthorization().WithSummary("Invoice of a paid or shipped order").AddEndpointFilter<DomainErrorFilter>();

        // Release 1.1 — diagnostics
        var diag = app.MapGroup("/api").WithTags("Diagnostics");

        diag.MapPost("/test/reset", async (IOrderFlowStore store, Outbox outbox, OrdersListCache cache, RequestLog log) =>
        {
            await store.ResetAsync();
            outbox.Clear();
            cache.Clear();
            log.Clear();
            return Results.Ok(new { message = "Demo data restored." });
        }).RequireAuthorization("Manager").WithSummary("Restore the demo data; clear the outbox, the list cache and the request log (manager)");

        diag.MapGet("/outbox", (Outbox outbox) => Results.Ok(outbox.Snapshot()))
            .RequireAuthorization().WithSummary("Copies of the e-mails sent since start-up or the last reset, newest first");

        diag.MapGet("/log", (RequestLog log) => Results.Ok(log.Snapshot()))
            .RequireAuthorization("Manager").WithSummary("The request log, newest first (manager)");
    }

    private static IResult NotFound(string message) => Results.Json(new { error = message }, statusCode: 404);
    private static IResult BadRequest(string message) => Results.Json(new { error = message }, statusCode: 400);

    public static ClaimsPrincipal Principal(UserAccount user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role),
        };
        if (user.CustomerId is not null) claims.Add(new Claim("customerId", user.CustomerId.Value.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static async Task<OrderDto> ToDto(Order o, OrderService orders, VariantOptions variant)
    {
        var totals = await orders.TotalsAsync(o);
        var promo = await orders.PromoAppliesAsync(o);
        return new OrderDto(o.Id, o.Number, o.CustomerId, o.Customer?.Name ?? "", o.Status.ToString(), o.PromoCode, promo,
            o.CreatedAtUtc, o.PlacedAtUtc, o.PaidAtUtc, o.ShippedAtUtc, o.CancelledAtUtc, Lines(o, variant), Totals(o, totals, variant));
    }

    private static List<LineDto> Lines(Order o, VariantOptions variant) =>
        o.Lines.Select(l => new LineDto(l.ProductId, l.ProductName, l.UnitPrice, l.Quantity,
            variant.ApiMoneyArithmeticInDouble ? (double)l.UnitPrice * l.Quantity : (double)l.LineTotal)).ToList();

    private static TotalsDto Totals(Order o, OrderTotals t, VariantOptions variant)
    {
        if (!variant.ApiMoneyArithmeticInDouble)
            return new TotalsDto((double)t.Subtotal, (double)t.Discount, (double)t.Shipping, (double)t.Net, (double)t.Vat, (double)t.Total);
        double subtotal = 0;
        foreach (var l in o.Lines) subtotal += (double)l.UnitPrice * l.Quantity;
        double rate = t.Subtotal == 0 ? 0 : (double)(t.Discount / t.Subtotal);
        double discount = subtotal * rate;
        double net = subtotal - discount;
        double shipping = (double)t.Shipping;
        double vat = (net + shipping) * 0.2;
        return new TotalsDto(subtotal, discount, shipping, net, vat, net + shipping + vat);
    }
}

/// <summary>Translates rule violations into HTTP problem responses.</summary>
public sealed class DomainErrorFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        try { return await next(ctx); }
        catch (DomainException ex) { return Results.Json(new { error = ex.Message }, statusCode: 400); }
        catch (NotFoundException ex) { return Results.Json(new { error = ex.Message }, statusCode: 404); }
        catch (ForbiddenException ex) { return Results.Json(new { error = ex.Message }, statusCode: 403); }
    }
}
