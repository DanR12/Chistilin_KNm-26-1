using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrderFlow.Application;
using OrderFlow.Domain;

namespace OrderFlow.Api.Pages.Orders;

[Authorize]
public class DetailsModel(IOrderFlowStore store, OrderService orders, VariantOptions variant, PricingRules rules) : PageModel
{
    public Order Order { get; set; } = new();
    public OrderTotals Totals { get; set; }
    public bool PromoApplied { get; set; }
    public decimal DisplayShipping { get; set; }
    public decimal DisplayVat { get; set; }
    public decimal DisplayTotal { get; set; }
    public List<Product> Products { get; set; } = new();

    private async Task<bool> LoadAsync(int id)
    {
        var order = await store.GetOrderAsync(id);
        if (order is null) return false;
        var me = CurrentUser.From(User);
        if (!me.Owns(order)) return false;
        Order = order;
        Totals = await orders.TotalsAsync(order);
        PromoApplied = await orders.PromoAppliesAsync(order);
        var (shipping, total) = Display.ShippingAndTotal(Totals, variant, rules);
        DisplayShipping = shipping;
        DisplayTotal = total;
        DisplayVat = total - Totals.Net - shipping;
        if (order.Status == OrderStatus.Draft)
            Products = await store.SearchProductsAsync(null);
        return true;
    }

    public async Task<IActionResult> OnGetAsync(int id) => await LoadAsync(id) ? Page() : NotFound();

    private async Task<IActionResult> Act(int id, Func<Task> action, string success)
    {
        if (!await LoadAsync(id)) return NotFound();
        try
        {
            await action();
            TempData["Flash"] = success;
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    public Task<IActionResult> OnPostAddLineAsync(int id, int productId, int quantity) =>
        Act(id, () => orders.AddLineAsync(id, productId, quantity, validateQuantity: true), "Line added.");

    public Task<IActionResult> OnPostPlaceAsync(int id) =>
        Act(id, () => orders.PlaceAsync(id), "Order placed. A confirmation e-mail has been sent to the customer.");

    public Task<IActionResult> OnPostPayAsync(int id) =>
        Act(id, () => orders.PayAsync(id), "Payment recorded. The invoice has been issued.");

    public async Task<IActionResult> OnPostShipAsync(int id)
    {
        if (!User.IsInRole("manager")) return Forbid();
        return await Act(id, () => orders.ShipAsync(id), "Order marked as shipped.");
    }

    public Task<IActionResult> OnPostCancelAsync(int id) =>
        Act(id, () => orders.CancelAsync(id), "Order cancelled.");
}
