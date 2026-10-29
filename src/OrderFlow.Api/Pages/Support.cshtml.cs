using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrderFlow.Application;

namespace OrderFlow.Api.Pages;

[Authorize(Roles = "manager")]
public class SupportModel(VariantOptions variant) : PageModel
{
    public SupportInbox.Message Message { get; set; } = SupportInbox.For(variant);

    public void OnGet() { }
}
