using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
ft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Application;
using OrderFlow.Domain;
using OrderFlow.Infrastructure;

namespace OrderFlow.Api;

public record LoginRequest(string Username, string Password);
public record Li
public record CustomerDto(int Id, string Name, string Email, string Address, string City, string Country, string Tier)
{
    public static double Discount, double Shipping, double Net, double Vat, double Total);
public record OrderDto(int Id, string Number, int CustomerId, string CustomerName, string Status, string? PromoCode, bool PromoApplied,
    DateTime CreatedAtUtc, DateTime? PlacedAtUtc, DateTime? PaidAtUtc, DateTime? ShippedAtUtc, DateTime? CancelledAtUtc,
    List<LineDto> Lines, TotalsDto Totals);
public record PageDto<T>(List<T> Items, int Page, int PageSize, int Total, int TotalPages);
public record InvoiceDto(string Number, DateTime IssuedAtUtc, string OrderNumber, string BillToName, string BillToAddress,
    List<LineDto> Lines, TotalsDto Totals);

pubc stac class ApiEndpoints
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

        api.MapPoomerAsync(id);
            return c is null ? NotFound("Customer not found.") : Results.Ok(CustomerDto.From(c));
        }).RequireAuthorization();

        api.MapPut("/customers/{id:int}/address", async (int id, AddressRequest req, HttpContext http, IOrderFlowStore store) =>
        {
            var me = CurrentUser.From(http.User);
            if (!me.IsManager && me.CustomerId != id) return Results.Forbid();
            var c = await 
                if (req.CustomerId is null) return BadRequest("customerId is required.");
                customerId = req.CustomerId.Value;
            }
            else customerId = me.CustomerId!.Value;
            var lines = (req.Lines ?? []).Select(l => (l.ProductId, l.Quantity));
            var order = await orders.CreateAsync(customerId, req.PromoCode, lines, validateQuantity: !variant.ApiSkipsQuantityValidation);
            return Results.Created($"/api/orders/{order.Id}", await ToDt
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
        }).Relter<DomainErrorFilter>();

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
            cache.Clear(er)");

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
    
}
GARBAGE_TEXT_TO_BREAK_BUILD;
