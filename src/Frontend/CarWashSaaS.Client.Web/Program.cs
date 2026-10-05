using CarWashSaaS.Client.Core;
using CarWashSaaS.Client.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<CustomerVehicleApiClient>();
builder.Services.AddScoped<ReceptionSessionState>();
builder.Services.AddScoped<StoreProfileApiClient>();
builder.Services.AddScoped<ServiceCatalogApiClient>();
builder.Services.AddScoped<WorkOrderApiClient>();
builder.Services.AddScoped<YardSetupApiClient>();
builder.Services.AddScoped<WhatsAppApiClient>();
builder.Services.AddScoped<InspectionApiClient>();
builder.Services.AddScoped<SchedulingApiClient>();
builder.Services.AddScoped<AfterSalesApiClient>();
builder.Services.AddScoped<BillingApiClient>();
builder.Services.AddScoped<CashierApiClient>();
builder.Services.AddScoped<LoyaltyApiClient>();
builder.Services.AddScoped<SubscriptionApiClient>();
builder.Services.AddScoped<IToastService, ToastService>();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
var authority = builder.Configuration["Authentication:Authority"];

builder.Services.AddScoped<ITokenStorage, LocalStorageTokenStorage>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddScoped<JwtAuthorizationMessageHandler>();

builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<JwtAuthorizationMessageHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = new Uri(apiBaseUrl) };
});

builder.Services.AddScoped<AuthApiClient>();

if (!string.IsNullOrWhiteSpace(authority) && builder.Configuration.GetValue<bool>("Authentication:UseOidc"))
{
    builder.Services.AddOidcAuthentication(options =>
    {
        builder.Configuration.Bind("Authentication", options.ProviderOptions);
        options.ProviderOptions.ResponseType = "code";
        var apiScope = builder.Configuration["Authentication:ApiScope"];
        if (!string.IsNullOrWhiteSpace(apiScope))
        {
            options.ProviderOptions.DefaultScopes.Add(apiScope);
        }
    });
}

await builder.Build().RunAsync();
