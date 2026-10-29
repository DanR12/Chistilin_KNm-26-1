using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Api;
using OrderFlow.Application;
using OrderFlow.Domain;
using OrderFlow.Infrastructure;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

// Content root = the folder of the compiled application, so wwwroot and the database are found
// no matter which directory the app is started from.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });

var variant = VariantOptions.FromEnvironment();
var rules = new PricingRules { ThresholdInclusive = variant.DiscountThresholdInclusive };
builder.Services.AddSingleton(variant);
builder.Services.AddSingleton(rules);

var dbPath = Environment.GetEnvironmentVariable("ORDERFLOW_DB")
             ?? builder.Configuration["OrderFlow:DatabasePath"]
             ?? Path.Combine(AppContext.BaseDirectory, "orderflow.db");
builder.Services.AddDbContext<OrderFlowDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<IOrderFlowStore, EfStore>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IEmailSender, MailGatewaySender>();
builder.Services.AddSingleton<SubmitGuard>();
builder.Services.AddSingleton<Outbox>();
builder.Services.AddSingleton<OrdersListCache>();
builder.Services.AddSingleton<StockGate>();
builder.Services.AddSingleton<RequestLog>();

builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new() { Title = "OrderFlow API", Version = "v1" }));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.LoginPath = "/Login";
    o.AccessDeniedPath = "/Login";
    o.Cookie.Name = "OrderFlow.Auth";
    o.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api")) { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; }
        ctx.Response.Redirect(ctx.RedirectUri); return Task.CompletedTask;
    };
    o.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api")) { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; }
        ctx.Response.Redirect(ctx.RedirectUri); return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization(o => o.AddPolicy("Manager", p => p.RequireRole("manager")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderFlowDbContext>();
    db.Database.EnsureCreated();
    SeedData.EnsureSeeded(db, rules, variant.InvoiceRounding);
}

app.UseMiddleware<RequestLogMiddleware>();
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var feature = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    var errorId = RequestLog.NewErrorId();
    ctx.Items["ErrorId"] = errorId;
    ctx.Items["Error"] = feature?.Error is { } ex ? $"{ex.GetType().Name}: {ex.Message}" : "unknown error";
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    if (ctx.Request.Path.StartsWithSegments("/api"))
    {
        await ctx.Response.WriteAsJsonAsync(new { error = "Something went wrong.", errorId });
        return;
    }
    ctx.Response.ContentType = "text/html; charset=utf-8";
    await ctx.Response.WriteAsync(
        "<!doctype html><html><head><title>OrderFlow</title><link rel=\"stylesheet\" href=\"/site.css\"></head>" +
        "<body><main class=\"container\"><h1>Something went wrong.</h1><p>Please try again later. Error id: <code>" + errorId + "</code></p>" +
        "<p><a href=\"/Orders\">Back to orders</a></p></main></body></html>");
}));
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI(o => { o.SwaggerEndpoint("/swagger/v1/swagger.json", "OrderFlow v1"); o.RoutePrefix = "swagger"; });
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapApi();

app.Run();

public partial class Program { }
