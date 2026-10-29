using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OrderFlow.Api.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Orders/Index");
}
