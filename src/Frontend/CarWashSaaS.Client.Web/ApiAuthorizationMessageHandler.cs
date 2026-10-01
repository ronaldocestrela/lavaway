using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace CarWashSaaS.Client.Web;

public sealed class ApiAuthorizationMessageHandler : AuthorizationMessageHandler
{
    private readonly IConfiguration _configuration;
    private readonly NavigationManager _navigationManager;

    public ApiAuthorizationMessageHandler(
        IAccessTokenProvider tokenProvider,
        NavigationManager navigationManager,
        IConfiguration configuration) : base(tokenProvider, navigationManager)
    {
        _configuration = configuration;
        _navigationManager = navigationManager;
    }

    public ApiAuthorizationMessageHandler ConfigureApi()
    {
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? _navigationManager.BaseUri;
        ConfigureHandler(authorizedUrls: [apiBaseUrl]);
        return this;
    }
}