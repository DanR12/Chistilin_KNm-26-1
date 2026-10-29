using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrderFlow.Application;

namespace OrderFlow.Api.Pages;

[Authorize(Roles = "manager")]
public class LogModel(RequestLog log, IOrderFlowStore store, Outbox outbox, OrdersListCache cache) : PageModel
{
    public IReadOnlyList<LogEntry> Entries { get; set; } = [];

    public void OnGet() => Entries = log.Snapshot();

    public async Task<IActionResult> OnPostResetAsync()
    {
        await store.ResetAsync();
        outbox.Clear();
        cache.Clear();
        log.Clear();
        TempData["Flash"] = "Demo data restored.";
        return RedirectToPage();
    }
}
