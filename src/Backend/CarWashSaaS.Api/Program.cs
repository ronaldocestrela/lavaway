using System.Text;
using CarWashSaaS.Api.Endpoints;
using CarWashSaaS.Api.Hubs;
using CarWashSaaS.Api.Middleware;
using CarWashSaaS.Api.Services;
using CarWashSaaS.Billing.Application;
using CarWashSaaS.Billing.Infrastructure;
using CarWashSaaS.Billing.Infrastructure.Gateways;
using CarWashSaaS.Billing.Infrastructure.Repositories;
using CarWashSaaS.Billing.Infrastructure.Webhooks;
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
builder.Services.AddScoped<ITenantStoreProfileLookup>(sp => sp.GetRequiredService<StoreProfileApplicationService>());
builder.Services.AddScoped<IServiceRepository, ServiceRepository>();
builder.Services.AddScoped<ServiceCatalogApplicationService>();
builder.Services.AddScoped<ICustomerVehicleSearchRepository, CustomerVehicleSearchRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();
builder.Services.AddScoped<IUnitOfWork, YardOperationsUnitOfWork>();
builder.Services.AddScoped<CustomerVehicleApplicationService>();
builder.Services.AddScoped<IWorkOrderReceiptPdfGenerator, WorkOrderReceiptPdfGenerator>();
builder.Services.AddScoped<WorkOrderApplicationService>();
builder.Services.AddScoped<WorkOrderNotificationApplicationService>();
builder.Services.AddScoped<IVehicleInspectionRepository, VehicleInspectionRepository>();
builder.Services.AddScoped<VehicleInspectionApplicationService>();
builder.Services.AddScoped<IYardCapacityRepository, YardCapacityRepository>();
builder.Services.AddScoped<ITeamMemberRepository, TeamMemberRepository>();
builder.Services.AddScoped<ICommissionRuleRepository, CommissionRuleRepository>();
builder.Services.AddScoped<YardSetupApplicationService>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<BookingApplicationService>();
builder.Services.AddScoped<ISchedulingBookingLookup>(sp => sp.GetRequiredService<BookingApplicationService>());
builder.Services.AddScoped<BookingReminderApplicationService>();
builder.Services.AddScoped<IReactivationCampaignRepository, ReactivationCampaignRepository>();
builder.Services.AddScoped<IFrequencyCappingService, FrequencyCappingService>();
builder.Services.AddScoped<AfterSalesApplicationService>();
builder.Services.AddScoped<IAfterSalesLookup>(sp => sp.GetRequiredService<AfterSalesApplicationService>());
builder.Services.AddScoped<ReactivationCampaignApplicationService>();
builder.Services.AddScoped<IWorkOrderPaymentLookup>(sp => sp.GetRequiredService<WorkOrderApplicationService>());
builder.Services.AddScoped<IWorkOrderPaymentSettlementService>(sp => sp.GetRequiredService<WorkOrderApplicationService>());
builder.Services.AddScoped<IPixChargeRepository, PixChargeRepository>();
builder.Services.AddScoped<IProcessedPaymentWebhookRepository, ProcessedPaymentWebhookRepository>();
builder.Services.AddScoped<IPaymentWebhookValidator, PaymentWebhookValidator>();
builder.Services.AddHttpClient<MercadoPagoPixGatewayProvider>();
builder.Services.AddScoped<IPixGatewayProvider, SimulatedPixGatewayProvider>();
builder.Services.AddScoped<PixBillingApplicationService>();
builder.Services.AddScoped<IPixBillingLookup>(sp => sp.GetRequiredService<PixBillingApplicationService>());
builder.Services.AddSignalR();
builder.Services.AddScoped<IYardRealtimeNotifier, SignalRYardRealtimeNotifier>();
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
builder.Services.AddHostedService<BookingReminderHostedService>();
builder.Services.AddHostedService<AfterSalesSurveyHostedService>();
builder.Services.AddHostedService<ReactivationCampaignHostedService>();
builder.Services.AddHttpClient<IWhatsAppPairingProvider, EvolutionApiWhatsAppPairingProvider>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["WhatsApp:EvolutionApi:BaseUrl"] ?? "http://localhost:8080/";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddHttpClient<IWhatsAppMessageSender, EvolutionApiWhatsAppMessageSender>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["WhatsApp:EvolutionApi:BaseUrl"] ?? "http://localhost:8080/";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddScoped<IWhatsAppConnectionRepository, WhatsAppConnectionRepository>();
builder.Services.AddScoped<IOutboundWhatsAppMessageRepository, OutboundWhatsAppMessageRepository>();
builder.Services.AddScoped<ITenantWhatsAppQuotaRepository, TenantWhatsAppQuotaRepository>();
builder.Services.AddScoped<ICustomerCommunicationPreferenceRepository, CustomerCommunicationPreferenceRepository>();
builder.Services.AddScoped<ICustomerCommunicationPreferenceLookup, CustomerCommunicationPreferenceLookupService>();
builder.Services.AddScoped<IChatbotSessionRepository, ChatbotSessionRepository>();
builder.Services.AddScoped<WhatsAppConnectionApplicationService>();
builder.Services.AddScoped<WhatsAppMessageApplicationService>();
builder.Services.AddScoped<ChatbotConversationEngine>();
builder.Services.AddScoped<IOutboundWhatsAppDispatcher>(sp => sp.GetRequiredService<WhatsAppMessageApplicationService>());
builder.Services.AddScoped<ITenantQueueMessageHandler, OutboundWhatsAppMessageQueueHandler>();
builder.Services.AddScoped<ITenantQueueMessageHandler, InboundWhatsAppMessageHandler>();
builder.Services.AddScoped<ITenantQueueMessageHandler, WorkOrderReadyNotificationQueueHandler>();
builder.Services.AddScoped<ITenantQueueMessageHandler, WorkOrderReceiptNotificationQueueHandler>();

builder.Services.AddDbContext<TenantsDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants")));
builder.Services.AddDbContext<IdentityModuleDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));
builder.Services.AddDbContext<YardOperationsDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "yard")));
builder.Services.AddDbContext<WhatsAppDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "whatsapp")));
builder.Services.AddDbContext<BillingDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "billing")));

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
    options.AddPolicy(AuthorizationPolicyNames.UpdateWorkOrderStatus,
        policy => policy.RequireRole(ShopRole.Administrator.ToString(), ShopRole.Receptionist.ToString(), ShopRole.Operator.ToString()));

    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddCors(options => options.AddPolicy("Client", policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
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
app.MapSchedulingEndpoints();
app.MapAfterSalesEndpoints();
app.MapBillingEndpoints();
app.MapHub<YardHub>("/hubs/yard").RequireCors("Client");

app.Run();
