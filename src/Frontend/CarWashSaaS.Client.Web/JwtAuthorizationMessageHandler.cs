using System.Net.Http.Headers;
using CarWashSaaS.Client.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CarWashSaaS.Client.Web;

public sealed class JwtAuthorizationMessageHandler(
    ITokenStorage tokenStorage,
    IServiceProvider serviceProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenStorage.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(token) && request.Headers.Authorization is null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var impersonationState = serviceProvider.GetService<ImpersonationSessionState>();
        if (impersonationState is not null && impersonationState.IsActive && impersonationState.TenantId.HasValue)
        {
            if (!request.Headers.Contains("X-Impersonate-Tenant-Id"))
            {
                request.Headers.Add("X-Impersonate-Tenant-Id", impersonationState.TenantId.Value.ToString());
            }
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized &&
            request.Headers.Authorization is not null)
        {
            // O token foi rejeitado pelo backend (expirado, inválido ou revogado).
            // Limpa o armazenamento e redireciona para login
            await tokenStorage.ClearAsync();

            var authProvider = serviceProvider.GetService<JwtAuthenticationStateProvider>();
            authProvider?.NotifyUserLogout();

            var navigation = serviceProvider.GetService<NavigationManager>();
            if (navigation is not null)
            {
                var relative = navigation.ToBaseRelativePath(navigation.Uri);
                if (!relative.StartsWith("login", StringComparison.OrdinalIgnoreCase) &&
                    !relative.StartsWith("authentication", StringComparison.OrdinalIgnoreCase))
                {
                    navigation.NavigateTo($"login?returnUrl={Uri.EscapeDataString(relative)}", replace: true);
                }
            }
        }

        return response;
    }
}

