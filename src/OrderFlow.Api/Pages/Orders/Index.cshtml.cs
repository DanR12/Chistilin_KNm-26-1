using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrderFlow.Application;
using OrderFlow.Domain;

namespace OrderFlow.Api.Pages.Orders;

[Authorize]
public class IndexModel(IOrderFlowStore store, OrderService orders, VariantOptions variant, PricingRules rules, OrdersListCache cache) : PageModel
{
    public const int PageSize = 10;
    public record Row(Order Order, decimal Total);

    public string? Q { get; set; }
    public int PageNumber { get; set; } = 1;
    public int Total { get; set; }
    public int TotalPages { get; set; }
    public int From => Total == 0 ? 0 : (PageNumber - 1) * PageSize + 1;
    public int To => Math.Min(PageNumber * PageSize, Total);
    public List<Row> Rows { get; set; } = new();

    public async Task OnGetAsync(string? q, [FromQuery(Name = "page")] int? page)
    {
        Q = q;
        PageNumber = Math.Max(1, page ?? 1);
        var me = CurrentUser.From(User);
        var (items, total) = await OrdersLookup.SearchAsync(store, cache, variant, Response, me.IsManager ? null : me.CustomerId, q, PageNumber, PageSize);
        Total = total;
        TotalPages = variant.PagerFloorsPageCount ? total / PageSize : (int)Math.Ceiling(total / (double)PageSize);
        foreach (var o in items)
        {
            var t = await orders.TotalsAsync(o);
            var (_, displayTotal) = Display.ShippingAndTotal(t, variant, rules);
            Rows.Add(new Row(o, displayTotal));
        }
    }
}
