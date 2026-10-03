using System.Text;
using CarWashSaaS.Api.Endpoints;
using CarWashSaaS.Api.Middleware;
using CarWashSaaS.Api.Services;
using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Identity.Infrastructure;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Application;
using CarWashSaaS.Tenants.Infrastructure;
using CarWashSaaS.WhatsApp.Application;
using CarWashSaaS.WhatsApp.Infrastructure;
using CarWashSaaS.YardOperations.Application;
using CarWashSaaS.YardOperations.Domain;
using CarWashSaaS.YardOperations.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Minio;
using RabbitMQ.Client;

DotEnvConfiguration.LoadFromRepository();

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var connectionString = builder.Configuration.GetConnectionString("CarWashSaaS")
    ?? DotEnvConfiguration.GetRequiredConnectionString();
var authority = builder.Configuration["Authentication:Authority"];
var audience = builder.Configuration["Authentication:Audience"];

if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(audience))
{
    throw new InvalidOperationException("Authentication:Authority and Authentication:Audience must be configured.");
}

var minioEndpoint = builder.Configuration["Storage:Minio:Endpoint"];
var minioAccessKey = builder.Configuration["Storage:Minio:AccessKey"];
var minioSecretKey = builder.Configuration["Storage:Minio:SecretKey"];
var minioBucket = builder.Configuration["Storage:Minio:Bucket"];
var rabbitMqUri = builder.Configuration["Messaging:RabbitMq:Uri"];
if (string.IsNullOrWhiteSpace(minioEndpoint) ||
    string.IsNullOrWhiteSpace(minioAccessKey) ||
    string.IsNullOrWhiteSpace(minioSecretKey) ||
    string.IsNullOrWhiteSpace(minioBucket))
{
    throw new InvalidOperationException("Storage:Minio endpoint, credentials, and bucket must be configured.");
}
if (!Uri.TryCreate(rabbitMqUri, UriKind.Absolute, out var rabbitMqConnectionUri) ||
    rabbitMqConnectionUri.Scheme is not ("amqp" or "amqps"))
{
    throw new InvalidOperationException("Messaging:RabbitMq:Uri must be a valid AMQP or AMQPS URI.");
}

builder.Services.AddScoped<CurrentTenantAccessor>();
builder.Services.AddScoped<ICurrentTenantAccessor>(services => services.GetRequiredService<CurrentTenantAccessor>());
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IIdentityUserRepository, IdentityUserRepository>();
builder.Services.AddScoped<IdentityApplicationService>();
builder.Services.AddScoped<IStoreProfileRepository, StoreProfileRepository>();
builder.Services.AddScoped<StoreProfileApplicationService>();
builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
builder.Services.AddScoped<ServiceCatalogApplicationService>();
builder.Services.AddScoped<ICustomerVehicleSearchRepository, CustomerVehicleSearchRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();
builder.Services.AddScoped<IUnitOfWork, YardOperationsUnitOfWork>();
builder.Services.AddScoped<CustomerVehicleApplicationService>();
builder.Services.AddScoped<WorkOrderApplicationService>();
builder.Services.AddScoped<IYardCapacityRepository, YardCapacityRepository>();
builder.Services.AddScoped<ITeamMemberRepository, TeamMemberRepository>();
builder.Services.AddScoped<ICommissionRuleRepository, CommissionRuleRepository>();
builder.Services.AddScoped<YardSetupApplicationService>();
builder.Services.AddScoped<IWhatsAppConnectionRepository, WhatsAppConnectionRepository>();
builder.Services.AddSingleton<IMinioClient>(_ => new MinioClient()
    .WithEndpoint(minioEndpoint)
    .WithCredentials(minioAccessKey, minioSecretKey)
    .WithSSL(builder.Configuration.GetValue<bool>("Storage:Minio:UseSSL"))
    .Build());
builder.Services.AddSingleton<ITenantObjectStorage>(services => new MinioTenantObjectStorage(
    services.GetRequiredService<IMinioClient>(), minioBucket));
builder.Services.AddScoped<TenantBrandingStorageService>();
builder.Services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory
{
    Uri = rabbitMqConnectionUri,
    AutomaticRecoveryEnabled = true
});
builder.Services.AddSingleton<IBackgroundQueue, RabbitMqBackgroundQueue>();
builder.Services.AddScoped<ITenantQueueMessageHandler, TenantBrandingAuditQueueHandler>();
builder.Services.AddHostedService<TenantQueueWorker>();
builder.Services.AddHttpClient<IWhatsAppPairingProvider, EvolutionApiWhatsAppPairingProvider>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["WhatsApp:EvolutionApi:BaseUrl"] ?? "http://localhost:8080/";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddScoped<WhatsAppConnectionApplicationService>();
builder.Services.AddDbContext<TenantsDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants")));
builder.Services.AddDbContext<IdentityModuleDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));
builder.Services.AddDbContext<YardOperationsDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "yard")));
builder.Services.AddDbContext<WhatsAppDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "whatsapp")));

var signingKey = builder.Configuration["Authentication:SigningKey"] ?? JwtTokenService.DefaultSigningKey;
var issuer = builder.Configuration["Authentication:Issuer"] ?? authority;

builder.Services.AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<IdentityModuleDbContext>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (string.IsNullOrWhiteSpace(signingKey) && !string.IsNullOrWhiteSpace(authority))
        {
            options.Authority = authority;
        }

        options.Audience = audience;
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            NameClaimType = "sub",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30)
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
    options.AddPolicy(AuthorizationPolicyNames.CreateWorkOrders,
        policy => policy.RequireRole(ShopRole.Administrator.ToString(), ShopRole.Receptionist.ToString()));
    options.AddPolicy(AuthorizationPolicyNames.ViewCustomers,
        policy => policy.RequireRole(ShopRole.Administrator.ToString(), ShopRole.Receptionist.ToString(), ShopRole.Operator.ToString()));

    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddCors(options => options.AddPolicy("Client", policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    }
}));
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    try
    {
        await DevDatabaseSeeder.SeedAsync(app.Services);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Failed to run development database seeder.");
    }
}

app.UseHttpsRedirection();
app.UseCors("Client");
app.UseAuthentication();
app.UseMiddleware<TenantResolverMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.MapIdentityEndpoints();
app.MapWhatsAppEndpoints();
app.MapTenantEndpoints();
app.MapYardOperationsEndpoints();

app.Run();
