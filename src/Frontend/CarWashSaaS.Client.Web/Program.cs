using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using CarWashSaaS.Client.Core;
using CarWashSaaS.Client.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<CustomerVehicleApiClient>();

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
var authority = builder.Configuration["Authentication:Authority"];
if (!string.IsNullOrWhiteSpace(authority))
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
	builder.Services.AddScoped<ApiAuthorizationMessageHandler>();
	builder.Services.AddScoped(sp => new HttpClient(
		sp.GetRequiredService<ApiAuthorizationMessageHandler>().ConfigureApi())
	{
		BaseAddress = new Uri(apiBaseUrl)
	});
}
else
{
	builder.Services.AddScoped<AuthenticationStateProvider, UnauthenticatedAuthenticationStateProvider>();
	builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
}

await builder.Build().RunAsync();
