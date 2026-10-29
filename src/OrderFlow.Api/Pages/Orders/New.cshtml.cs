using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrderFlow.Application;
using OrderFlow.Domain;

namespace OrderFlow.Api.Pages.Orders;

[Authorize]
public class NewModel(IOrderFlowStore store, OrderService orders, VariantOptions variant, SubmitGuard guard) : PageModel
{
    public List<Product> Products { get; set; } = new();
    public List<Customer> Customers { get; set; } = new();
    [BindProperty] public int? CustomerId { get; set; }
    [BindProperty] public string? PromoCode { get; set; }
    [BindProperty] public int[] ProductId { get; set; } = new int[3];
    [BindProperty] public int[] Quantity { get; set; } = new int[3];
    [BindProperty] public string? FormToken { get; set; }

    private async Task LoadAsync()
    {
        Products = await store.SearchProductsAsync(null);
        if (User.IsInRole("manager")) Customers = await store.GetCustomersAsync();
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
        FormToken = variant.NoDuplicateSubmitGuard ? null : Guid.NewGuid().ToString("N");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var me = CurrentUser.From(User);
        if (!variant.NoDuplicateSubmitGuard && !guard.TryConsume(FormToken))
        {
            TempData["Error"] = "This order has already been submitted.";
            return RedirectToPage("/Orders/Index");
        }
        var lines = new List<(int, int)>();
        for (var i = 0; i < ProductId.Length; i++)
            if (ProductId[i] > 0) lines.Add((ProductId[i], i < Quantity.Length ? Quantity[i] : 0));
        var customerId = me.IsManager ? CustomerId ?? 0 : me.CustomerId ?? 0;
        try
        {
            if (lines.Count == 0) throw new DomainException("Choose at least one product.");
            var order = await orders.CreateAndPlaceAsync(customerId, PromoCode, lines);
            TempData["Flash"] = $"Order {order.Number} placed. A confirmation e-mail has been sent to {order.Customer?.Email}.";
            return RedirectToPage("/Orders/Details", new { id = order.Id });
        }
        catch (Exception ex) when (ex is DomainException or NotFoundException)
        {
            TempData["Error"] = ex.Message;
            await LoadAsync();
            FormToken = variant.NoDuplicateSubmitGuard ? null : Guid.NewGuid().ToString("N");
            return Page();
        }
    }
}
