using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrderFlow.Application;
using OrderFlow.Domain;

namespace OrderFlow.Api.Pages;

[AllowAnonymous]
public class ProductsModel(IOrderFlowStore store) : PageModel
{
    public string? Q { get; set; }
    public List<Product> Products { get; set; } = new();

    public async Task OnGetAsync(string? q)
    {
        Q = q;
        Products = await store.SearchProductsAsync(q);
    }
}
