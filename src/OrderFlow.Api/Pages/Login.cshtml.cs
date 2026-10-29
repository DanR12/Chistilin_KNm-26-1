using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Infrastructure;

namespace OrderFlow.Api.Pages;

[AllowAnonymous]
public class LoginModel(OrderFlowDbContext db) : PageModel
{
    [BindProperty] public string? Username { get; set; }
    [BindProperty] public string? Password { get; set; }
    [BindProperty] public string? ReturnUrl { get; set; }
    public string? Message { get; set; }

    public void OnGet(string? returnUrl) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == (Username ?? "") && u.Password == (Password ?? ""));
        if (user is null)
        {
            Message = "Invalid username or password.";
            return Page();
        }
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, ApiEndpoints.Principal(user));
        return LocalRedirect(string.IsNullOrEmpty(ReturnUrl) ? "/Orders" : ReturnUrl);
    }
}
