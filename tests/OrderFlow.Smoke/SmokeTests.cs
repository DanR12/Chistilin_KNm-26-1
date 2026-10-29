using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OrderFlow.Smoke;

public class SmokeTests : IClassFixture<OrderFlowFactory>
{
    private readonly OrderFlowFactory _factory;

    public SmokeTests(OrderFlowFactory factory) => _factory = factory;

    private async Task<HttpClient> LoggedInAsync(string user)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = user, password = user });
        login.EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task Application_starts_and_lists_products()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(products.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Customer_can_log_in_and_see_own_orders()
    {
        var client = await LoggedInAsync("bob");
        var response = await client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(page.GetProperty("total").GetInt32() > 0);
    }

    [Fact]
    public async Task Valid_order_is_created_as_draft()
    {
        var client = await LoggedInAsync("bob");
        var response = await client.PostAsJsonAsync("/api/orders",
            new { promoCode = (string?)null, lines = new[] { new { productId = 5, quantity = 2 } } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", order.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Paid_order_has_an_invoice()
    {
        var client = await LoggedInAsync("bob");
        var response = await client.GetAsync("/api/orders/2/invoice");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var invoice = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.StartsWith("INV-", invoice.GetProperty("number").GetString());
    }

    [Fact]
    public async Task Unknown_order_returns_404()
    {
        var client = await LoggedInAsync("manager");
        var response = await client.GetAsync("/api/orders/9999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
