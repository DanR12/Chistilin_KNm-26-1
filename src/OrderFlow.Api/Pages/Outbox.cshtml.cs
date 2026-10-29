using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrderFlow.Application;

namespace OrderFlow.Api.Pages;

[Authorize]
public class OutboxModel(Outbox outbox) : PageModel
{
    public IReadOnlyList<OutboxMessage> Messages { get; set; } = [];

    public void OnGet() => Messages = outbox.Snapshot();
}
