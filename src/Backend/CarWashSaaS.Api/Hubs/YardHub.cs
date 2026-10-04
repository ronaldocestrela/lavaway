using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CarWashSaaS.Api.Hubs;

[Authorize]
public sealed class YardHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantIdClaim = Context.User?.FindFirst("tenant_id")?.Value
            ?? Context.User?.FindFirst("TenantId")?.Value;

        if (Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetTenantGroupName(tenantId));
        }

        await base.OnConnectedAsync();
    }

    public static string GetTenantGroupName(Guid tenantId) => $"tenant_{tenantId:N}";
}
