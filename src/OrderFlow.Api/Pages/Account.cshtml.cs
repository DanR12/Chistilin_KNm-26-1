using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrderFlow.Application;
using OrderFlow.Domain;

namespace OrderFlow.Api.Pages;

[Authorize(Roles = "customer")]
public class AccountModel(IOrderFlowStore store) : PageModel
{
    public Customer Customer { get; set; } = new();
    [BindProperty] public string? Address { get; set; }
    [BindProperty] public string? City { get; set; }
    [BindProperty] public string? Country { get; set; }

    private async Task<Customer?> LoadAsync()
    {
        var me = CurrentUser.From(User);
        return me.CustomerId is null ? null : await store.GetCustomerAsync(me.CustomerId.Value);
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var c = await LoadAsync();
        if (c is null) return NotFound();
        Customer = c;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var c = await LoadAsync();
        if (c is null) return NotFound();
        c.Address = Address?.Trim() ?? ""; c.City = City?.Trim() ?? ""; c.Country = Country?.Trim() ?? "";
        await store.SaveChangesAsync();
        TempData["Flash"] = "Address saved.";
        return RedirectToPage();
    }
}
