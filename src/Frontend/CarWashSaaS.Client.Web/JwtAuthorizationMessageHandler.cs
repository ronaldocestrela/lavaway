using System.Net.Http.Headers;
using CarWashSaaS.Client.Core;

namespace CarWashSaaS.Client.Web;

public sealed class JwtAuthorizationMessageHandler(ITokenStorage tokenStorage) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenStorage.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(token) && request.Headers.Authorization is null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
