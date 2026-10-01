using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Identity.Infrastructure;
using CarWashSaaS.Api.Middleware;
using CarWashSaaS.Api.Services;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Application;
using CarWashSaaS.Tenants.Infrastructure;
using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Infrastructure;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;

DotEnvConfiguration.LoadFromRepository();

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("CarWashSaaS")
    ?? DotEnvConfiguration.GetRequiredConnectionString();
var authority = builder.Configuration["Authentication:Authority"];
var audience = builder.Configuration["Authentication:Audience"];

if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience))
{
    throw new InvalidOperationException("Authentication:Authority and Authentication:Audience must be configured.");
}

builder.Services.AddScoped<CurrentTenantAccessor>();
builder.Services.AddScoped<ICurrentTenantAccessor>(services => services.GetRequiredService<CurrentTenantAccessor>());
builder.Services.AddScoped<IStoreProfileRepository, StoreProfileRepository>();
builder.Services.AddScoped<StoreProfileApplicationService>();
builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
builder.Services.AddScoped<ServiceCatalogApplicationService>();
builder.Services.AddScoped<IYardCapacityRepository, YardCapacityRepository>();
builder.Services.AddScoped<ITeamMemberRepository, TeamMemberRepository>();
builder.Services.AddScoped<ICommissionRuleRepository, CommissionRuleRepository>();
builder.Services.AddScoped<YardSetupApplicationService>();
builder.Services.AddScoped<IWhatsAppConnectionRepository, WhatsAppConnectionRepository>();
builder.Services.AddHttpClient<IWhatsAppPairingProvider, EvolutionApiWhatsAppPairingProvider>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["WhatsApp:EvolutionApi:BaseUrl"] ?? "http://localhost:8080/";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddScoped<WhatsAppConnectionApplicationService>();
builder.Services.AddSingleton(new TenantBrandingStorageService(Path.Combine(builder.Environment.ContentRootPath, "Storage")));
builder.Services.AddSingleton<IBackgroundQueue, InMemoryBackgroundQueue>();
builder.Services.AddDbContext<TenantsDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants")));
builder.Services.AddDbContext<IdentityModuleDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));
builder.Services.AddDbContext<YardOperationsDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "yard")));
builder.Services.AddDbContext<WhatsAppDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "whatsapp")));

builder.Services.AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<IdentityModuleDbContext>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicyNames.Administrator,
        policy => policy.RequireRole(ShopRole.Administrator.ToString()));
    options.AddPolicy(AuthorizationPolicyNames.Receptionist,
        policy => policy.RequireRole(ShopRole.Receptionist.ToString(), ShopRole.Administrator.ToString()));
    options.AddPolicy(AuthorizationPolicyNames.Operator,
        policy => policy.RequireRole(ShopRole.Operator.ToString(), ShopRole.Administrator.ToString()));

    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var storageRoot = Path.Combine(app.Environment.ContentRootPath, "Storage");
Directory.CreateDirectory(storageRoot);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storageRoot),
    RequestPath = "/storage"
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseMiddleware<TenantResolverMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.MapPost("/whatsapp/webhooks/evolution", async (
    HttpRequest request,
    IConfiguration configuration,
    CurrentTenantAccessor currentTenantAccessor,
    WhatsAppConnectionApplicationService service,
    CancellationToken ct) =>
{
    var expectedSecret = configuration["WhatsApp:EvolutionApi:WebhookSecret"];
    var providedSecret = request.Headers["X-Webhook-Secret"].ToString();
    if (string.IsNullOrWhiteSpace(expectedSecret))
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expectedSecret);
    var providedBytes = System.Text.Encoding.UTF8.GetBytes(providedSecret);
    if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
    {
        return Results.Unauthorized();
    }

    System.Text.Json.JsonDocument payload;
    try
    {
        payload = await System.Text.Json.JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
    }
    catch (System.Text.Json.JsonException)
    {
        return Results.BadRequest(new { error = "Invalid webhook JSON." });
    }

    using (payload)
    {
        var root = payload.RootElement;
        if (!TryGetJsonString(root, "event", out var eventName) ||
            !TryGetJsonString(root, "instance", out var instanceName))
        {
            return Results.BadRequest(new { error = "Webhook event and instance are required." });
        }

        if (!eventName.Replace('.', '_').Equals("connection_update", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Ok(new { ignored = true });
        }

        var instancePrefix = configuration["WhatsApp:EvolutionApi:InstanceNamePrefix"] ?? "lavaway";
        var expectedInstancePrefix = $"{instancePrefix}-";
        if (!instanceName.StartsWith(expectedInstancePrefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(instanceName[expectedInstancePrefix.Length..], "N", out var tenantId) ||
            tenantId == Guid.Empty)
        {
            return Results.BadRequest(new { error = "Webhook instance is invalid." });
        }

        if (!root.TryGetProperty("data", out var data) || !TryGetJsonString(data, "state", out var providerState))
        {
            return Results.BadRequest(new { error = "Connection state is required." });
        }

        currentTenantAccessor.SetTenant(tenantId);
        var result = await service.ApplyProviderStatusAsync(tenantId, instanceName, providerState, ct);
        if (result.IsSuccess || result.Error!.Code is "whatsapp.not_found" or "whatsapp.provider_session.mismatch")
        {
            return Results.NoContent();
        }

        return result.Error.Type == ErrorType.Validation
            ? Results.BadRequest(new { result.Error.Code, result.Error.Description })
            : Results.Problem(result.Error.Description, statusCode: StatusCodes.Status500InternalServerError);
    }
}).AllowAnonymous();

app.MapGet("/whatsapp/status", async (ICurrentTenantAccessor currentTenantAccessor, WhatsAppConnectionApplicationService service) =>
{
    if (currentTenantAccessor.TenantId is not Guid tenantId)
    {
        return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
    }

    var result = await service.GetStatusAsync(tenantId);
    return result.IsSuccess ? Results.Ok(new { status = result.Value!.ToString().ToLowerInvariant() }) : result.Error!.Type switch
    {
        ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
        _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}).RequireAuthorization();

app.MapPost("/whatsapp/pairing/start", async (ICurrentTenantAccessor currentTenantAccessor, WhatsAppConnectionApplicationService service) =>
{
    if (currentTenantAccessor.TenantId is not Guid tenantId)
    {
        return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
    }

    var result = await service.StartPairingAsync(tenantId);
    return result.IsSuccess ? Results.Ok(new { status = result.Value!.Status.ToString().ToLowerInvariant(), qrCode = result.Value.QrCodeValue }) : result.Error!.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
        ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
        _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}).RequireAuthorization(AuthorizationPolicyNames.Administrator);

app.MapPost("/whatsapp/pairing/refresh", async (ICurrentTenantAccessor currentTenantAccessor, WhatsAppConnectionApplicationService service) =>
{
    if (currentTenantAccessor.TenantId is not Guid tenantId)
    {
        return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
    }

    var result = await service.RefreshPairingAsync(tenantId);
    return result.IsSuccess ? Results.Ok(new { status = result.Value!.Status.ToString().ToLowerInvariant(), qrCode = result.Value.QrCodeValue }) : result.Error!.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
        ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
        _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}).RequireAuthorization(AuthorizationPolicyNames.Administrator);

app.MapGet("/tenants/profile", async (ICurrentTenantAccessor currentTenantAccessor, StoreProfileApplicationService service) =>
{
    if (currentTenantAccessor.TenantId is not Guid tenantId)
    {
        return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
    }

    var result = await service.GetAsync(tenantId);
    return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
        ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
        _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}).RequireAuthorization();

app.MapGet("/services", async (ICurrentTenantAccessor currentTenantAccessor, ServiceCatalogApplicationService service) =>
{
    if (currentTenantAccessor.TenantId is not Guid tenantId)
    {
        return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
    }

    var result = await service.ListAsync(tenantId);
    return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
    {
        ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
        _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}).RequireAuthorization();

app.MapGet("/yard/capacity", async (ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
{
    if (currentTenantAccessor.TenantId is not Guid tenantId)
    {
        return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
    }

    var result = await service.GetCapacityAsync(tenantId);
    return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
        ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
        _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}).RequireAuthorization();

app.MapPost("/yard/capacity", async (CreateYardCapacityCommand command, ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
{
    if (currentTenantAccessor.TenantId is not Guid tenantId)
    {
        return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
    }

    var result = await service.CreateCapacityAsync(tenantId, command);
    return result.IsSuccess ? Results.Created($"/yard/capacity", result.Value) : result.Error!.Type switch
    {
        ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
        ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
        _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}).RequireAuthorization(AuthorizationPolicyNames.Administrator);

app.MapGet("/team-members", async (ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
{
    if (currentTenantAccessor.TenantId is not Guid tenantId)
    {
        return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
    }

    var result = await service.ListTeamMembersAsync(tenantId);
    return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
    {
        ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
        _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}).RequireAuthorization();

app.MapPost("/team-members", async (CreateTeamMemberCommand command, ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
{
    if (currentTenantAccessor.TenantId is not Guid tenantId)
    {
        return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
    }

    var result = await service.CreateTeamMemberAsync(tenantId, command);
    return result.IsSuccess ? Results.Created($"/team-members/{result.Value!.Id}", result.Value) : result.Error!.Type switch
    {
        ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
        ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
        _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}).RequireAuthorization(AuthorizationPolicyNames.Administrator);

app.MapGet("/commission-rules", async (ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
{
    if (currentTenantAccessor.TenantId is not Guid tenantId)
    {
        return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
    }

    var result = await service.ListCommissionRulesAsync(tenantId);
    return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
    {
        ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
        _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}).RequireAuthorization();

app.MapPost("/commission-rules", async (CreateCommissionRuleCommand command, ICurrentTenantAccessor currentTenantAccessor, YardSetupApplicationService service) =>
{
    if (currentTenantAccessor.TenantId is not Guid tenantId)
    {
        return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
    }

    var result = await service.CreateCommissionRuleAsync(tenantId, command);
    return result.IsSuccess ? Results.Created($"/commission-rules/{result.Value!.Id}", result.Value) : result.Error!.Type switch
    {
        ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
        ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
        _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}).RequireAuthorization(AuthorizationPolicyNames.Administrator);

app.MapGet("/services/{id:guid}", async (Guid id, ICurrentTenantAccessor currentTenantAccessor, ServiceCatalogApplicationService service) =>
{
    if (currentTenantAccessor.TenantId is not Guid tenantId)
    {
        return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
    }

    var result = await service.GetAsync(tenantId, id);
    return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
        ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
        _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}).RequireAuthorization();

app.MapPost("/tenants/profile", async (CreateStoreProfileCommand command, ICurrentTenantAccessor currentTenantAccessor, StoreProfileApplicationService service) =>
{
    if (currentTenantAccessor.TenantId is not Guid tenantId)
    {
        return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
    }

    var result = await service.CreateAsync(tenantId, command);
    return result.IsSuccess ? Results.Created($"/tenants/profile", result.Value) : result.Error!.Type switch
    {
        ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
        ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
        _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}).RequireAuthorization(AuthorizationPolicyNames.Administrator);

app.MapPost("/services", async (CreateServiceCommand command, ICurrentTenantAccessor currentTenantAccessor, ServiceCatalogApplicationService service) =>
{
    if (currentTenantAccessor.TenantId is not Guid tenantId)
    {
        return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
    }

    var result = await service.CreateAsync(tenantId, command);
    return result.IsSuccess ? Results.Created($"/services/{result.Value!.Id}", result.Value) : result.Error!.Type switch
    {
        ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
        ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
        _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}).RequireAuthorization(AuthorizationPolicyNames.Administrator);

app.MapPut("/services/{id:guid}", async (Guid id, UpdateServiceCommand command, ICurrentTenantAccessor currentTenantAccessor, ServiceCatalogApplicationService service) =>
{
    if (currentTenantAccessor.TenantId is not Guid tenantId)
    {
        return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
    }

    var result = await service.UpdateAsync(tenantId, id, command);
    return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
        ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
        ErrorType.Conflict => Results.Conflict(new { result.Error.Code, result.Error.Description }),
        _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}).RequireAuthorization(AuthorizationPolicyNames.Administrator);

app.MapPut("/tenants/profile", async (UpdateStoreProfileCommand command, ICurrentTenantAccessor currentTenantAccessor, StoreProfileApplicationService service) =>
{
    if (currentTenantAccessor.TenantId is not Guid tenantId)
    {
        return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
    }

    var result = await service.UpdateAsync(tenantId, command);
    return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { result.Error.Code, result.Error.Description }),
        ErrorType.Validation => Results.BadRequest(new { result.Error.Code, result.Error.Description }),
        _ => Results.Problem(result.Error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}).RequireAuthorization(AuthorizationPolicyNames.Administrator);

app.MapPost("/tenants/profile/logo", async (
    IFormFile? logoFile,
    string? brandPrimaryColor,
    string? brandSecondaryColor,
    ICurrentTenantAccessor currentTenantAccessor,
    StoreProfileApplicationService service,
    TenantBrandingStorageService storageService) =>
{
    if (currentTenantAccessor.TenantId is not Guid tenantId)
    {
        return Results.Problem("A valid tenant is required.", statusCode: StatusCodes.Status403Forbidden);
    }

    if (logoFile is null || logoFile.Length == 0)
    {
        return Results.BadRequest(new { code = "store_profile.logo.required", description = "A logo file is required." });
    }

    var profileResult = await service.GetAsync(tenantId);
    if (!profileResult.IsSuccess)
    {
        return profileResult.Error!.Type switch
        {
            ErrorType.NotFound => Results.NotFound(new { profileResult.Error.Code, profileResult.Error.Description }),
            ErrorType.Validation => Results.BadRequest(new { profileResult.Error.Code, profileResult.Error.Description }),
            _ => Results.Problem(profileResult.Error.Description, statusCode: StatusCodes.Status400BadRequest)
        };
    }

    var existingProfile = profileResult.Value!;
    var savedLogoResult = storageService.SaveLogo(tenantId, logoFile);
    if (!savedLogoResult.IsSuccess)
    {
        return Results.BadRequest(new { savedLogoResult.Error!.Code, savedLogoResult.Error.Description });
    }

    var updatedCommand = new UpdateStoreProfileCommand(
        existingProfile.LegalName,
        existingProfile.TradeName,
        existingProfile.Cnpj,
        existingProfile.Phone,
        existingProfile.Street,
        existingProfile.City,
        existingProfile.State,
        existingProfile.PostalCode,
        savedLogoResult.Value,
        string.IsNullOrWhiteSpace(brandPrimaryColor) ? existingProfile.BrandPrimaryColor : brandPrimaryColor,
        string.IsNullOrWhiteSpace(brandSecondaryColor) ? existingProfile.BrandSecondaryColor : brandSecondaryColor);

    var updateResult = await service.UpdateAsync(tenantId, updatedCommand);
    return updateResult.IsSuccess ? Results.Ok(updateResult.Value) : updateResult.Error!.Type switch
    {
        ErrorType.NotFound => Results.NotFound(new { updateResult.Error.Code, updateResult.Error.Description }),
        ErrorType.Validation => Results.BadRequest(new { updateResult.Error.Code, updateResult.Error.Description }),
        _ => Results.Problem(updateResult.Error.Description, statusCode: StatusCodes.Status400BadRequest)
    };
}).RequireAuthorization(AuthorizationPolicyNames.Administrator);

app.Run();

static bool TryGetJsonString(System.Text.Json.JsonElement element, string propertyName, out string value)
{
    value = string.Empty;
    return element.ValueKind == System.Text.Json.JsonValueKind.Object &&
           element.TryGetProperty(propertyName, out var property) &&
           property.ValueKind == System.Text.Json.JsonValueKind.String &&
           !string.IsNullOrWhiteSpace(value = property.GetString() ?? string.Empty);
}
