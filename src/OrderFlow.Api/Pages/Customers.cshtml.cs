using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrderFlow.Application;
using OrderFlow.Domain;

namespace OrderFlow.Api.Pages;

[Authorize(Roles = "manager")]
public class CustomersModel(IOrderFlowStore store) : PageModel
{
    public List<Customer> Customers { get; set; } = new();
    public async Task OnGetAsync() => Customers = await store.GetCustomersAsync();
}
