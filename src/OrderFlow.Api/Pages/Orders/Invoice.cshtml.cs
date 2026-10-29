using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrderFlow.Application;
using OrderFlow.Domain;

namespace OrderFlow.Api.Pages.Orders;

[Authorize]
public class InvoiceModel(IOrderFlowStore store, VariantOptions variant) : PageModel
{
    public Order Order { get; set; } = new();
    public Invoice Invoice { get; set; } = new();
    public string BillToName { get; set; } = "";
    public string BillToAddress { get; set; } = "";
    public string IssuedText { get; set; } = "";
    public bool Legacy => variant.InvoiceLegacyTemplate;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var order = await store.GetOrderAsync(id);
        if (order is null) return NotFound();
        var me = CurrentUser.From(User);
        if (!me.Owns(order)) return NotFound();
        if (order.Invoice is null)
        {
            TempData["Error"] = "No invoice has been issued for this order yet.";
            return RedirectToPage("/Orders/Details", new { id });
        }
        Order = order;
        Invoice = order.Invoice;
        BillToName = variant.InvoiceReadsLiveAddress ? order.Customer!.Name : Invoice.BillToName;
        BillToAddress = variant.InvoiceReadsLiveAddress ? order.Customer!.FullAddress : Invoice.BillToAddress;
        IssuedText = variant.InvoiceShowsLocalTime
            ? Invoice.IssuedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
            : Invoice.IssuedAtUtc.ToString("yyyy-MM-dd HH:mm") + " UTC";
        return Page();
    }
}
