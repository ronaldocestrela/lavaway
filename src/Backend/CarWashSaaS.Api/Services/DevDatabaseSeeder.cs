using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Identity.Infrastructure;
using CarWashSaaS.Shared.Configuration;
using CarWashSaaS.Shared.Contracts;
using CarWashSaaS.Tenants.Application;
using CarWashSaaS.Tenants.Domain;
using CarWashSaaS.Tenants.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CarWashSaaS.Api.Services;

public static class DevDatabaseSeeder
{
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        var configuration = sp.GetRequiredService<IConfiguration>();
        var tenantsContext = sp.GetRequiredService<TenantsDbContext>();
        var userRepo = sp.GetRequiredService<IIdentityUserRepository>();
        var currentTenantAccessor = (CurrentTenantAccessor)sp.GetRequiredService<ICurrentTenantAccessor>();

        // Resolver configurações do .env / ambiente
        var tenantIdString = configuration["Seed:TenantId"]
            ?? configuration["SEED_TENANT_ID"]
            ?? Environment.GetEnvironmentVariable("Seed__TenantId")
            ?? Environment.GetEnvironmentVariable("SEED_TENANT_ID");

        var tenantId = Guid.TryParse(tenantIdString, out var parsedId) ? parsedId : DefaultTenantId;

        var tenantName = configuration["Seed:TenantName"]
            ?? configuration["SEED_TENANT_NAME"]
            ?? Environment.GetEnvironmentVariable("Seed__TenantName")
            ?? Environment.GetEnvironmentVariable("SEED_TENANT_NAME")
            ?? "Lavaway Matriz - Estética & Lava-Jato";

        var adminEmail = configuration["Seed:AdminEmail"]
            ?? configuration["SEED_ADMIN_EMAIL"]
            ?? Environment.GetEnvironmentVariable("Seed__AdminEmail")
            ?? Environment.GetEnvironmentVariable("SEED_ADMIN_EMAIL")
            ?? "admin@lavaway.com";

        var adminPassword = configuration["Seed:AdminPassword"]
            ?? configuration["SEED_ADMIN_PASSWORD"]
            ?? Environment.GetEnvironmentVariable("Seed__AdminPassword")
            ?? Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD")
            ?? "Admin123!";

        var receptionEmail = configuration["Seed:ReceptionEmail"]
            ?? configuration["SEED_RECEPTION_EMAIL"]
            ?? Environment.GetEnvironmentVariable("Seed__ReceptionEmail")
            ?? Environment.GetEnvironmentVariable("SEED_RECEPTION_EMAIL")
            ?? "recepcao@lavaway.com";

        var receptionPassword = configuration["Seed:ReceptionPassword"]
            ?? configuration["SEED_RECEPTION_PASSWORD"]
            ?? Environment.GetEnvironmentVariable("Seed__ReceptionPassword")
            ?? Environment.GetEnvironmentVariable("SEED_RECEPTION_PASSWORD")
            ?? "Recepcao123!";

        // Set tenant context for tenant-scoped operations
        currentTenantAccessor.SetTenant(tenantId);

        // 1. Ensure Tenant exists in tenants.Tenants
        var tenantExists = await tenantsContext.Tenants.AnyAsync(t => t.Id == tenantId);
        if (!tenantExists)
        {
            var tenantResult = Tenant.Create(tenantId, tenantName);
            if (tenantResult.IsSuccess && tenantResult.Value is not null)
            {
                tenantsContext.Tenants.Add(tenantResult.Value);
                await tenantsContext.SaveChangesAsync();
            }
        }

        // 2. Ensure StoreProfile exists in tenants.StoreProfiles
        var profileExists = await tenantsContext.StoreProfiles.IgnoreQueryFilters().AnyAsync(p => p.TenantId == tenantId);
        if (!profileExists)
        {
            var storeProfileResult = StoreProfile.Create(
                tenantId,
                "Lavaway Auto Care Ltda",
                tenantName,
                "11222333000181",
                "(11) 98765-4321",
                "Av. Paulista, 1000",
                "São Paulo",
                "SP",
                "01310-100",
                null,
                "#c6f277",
                "#182c2b");

            if (storeProfileResult.IsSuccess && storeProfileResult.Value is not null)
            {
                tenantsContext.StoreProfiles.Add(storeProfileResult.Value);
                await tenantsContext.SaveChangesAsync();
            }
        }

        // 3. Ensure Administrator User exists in identity.AspNetUsers
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var adminUser = await userRepo.FindByEmailAsync(adminEmail);
            if (adminUser is null)
            {
                await userRepo.CreateUserAsync(tenantId, adminEmail, adminPassword, ShopRole.Administrator);
            }
        }

        // 4. Ensure Receptionist User exists in identity.AspNetUsers
        if (!string.IsNullOrWhiteSpace(receptionEmail) && !string.IsNullOrWhiteSpace(receptionPassword))
        {
            var receptionUser = await userRepo.FindByEmailAsync(receptionEmail);
            if (receptionUser is null)
            {
                await userRepo.CreateUserAsync(tenantId, receptionEmail, receptionPassword, ShopRole.Receptionist);
            }
        }

        // 5. Ensure Platform SuperAdmin exists in identity.PlatformUsers
        var platformUserRepo = sp.GetRequiredService<IPlatformUserRepository>();
        var auditTrailService = sp.GetRequiredService<AuditTrailApplicationService>();

        var platformAdminEmail = configuration["Seed:PlatformAdminEmail"]
            ?? configuration["SEED_PLATFORM_ADMIN_EMAIL"]
            ?? configuration["Seed:PlatformSuperAdminEmail"]
            ?? configuration["SEED_PLATFORM_SUPERADMIN_EMAIL"]
            ?? "platform@lavaway.com";

        var platformAdminPassword = configuration["Seed:PlatformAdminPassword"]
            ?? configuration["SEED_PLATFORM_ADMIN_PASSWORD"]
            ?? configuration["Seed:PlatformSuperAdminPassword"]
            ?? configuration["SEED_PLATFORM_SUPERADMIN_PASSWORD"]
            ?? "PlatformAdmin123!";

        var existingPlatformAdmin = await platformUserRepo.FindByEmailAsync(platformAdminEmail);
        if (existingPlatformAdmin is null)
        {
            var passwordHash = platformUserRepo.HashPassword(platformAdminPassword);
            var superAdminResult = PlatformUser.Create(
                platformAdminEmail,
                "Administrador da Plataforma Lavaway",
                PlatformRole.SuperAdmin,
                passwordHash);


            if (superAdminResult.IsSuccess && superAdminResult.Value is not null)
            {
                await platformUserRepo.AddAsync(superAdminResult.Value);

                await auditTrailService.RecordEventAsync(new RecordAuditEventRequest(
                    ActorId: superAdminResult.Value.Id,
                    ActorEmail: superAdminResult.Value.Email,
                    ActorRole: PlatformRole.SuperAdmin.ToString(),
                    ActorRealm: "Platform",
                    Action: PlatformActionConstants.PlatformUserCreated,
                    TargetType: PlatformTargetTypeConstants.PlatformUser,
                    TargetId: superAdminResult.Value.Id.ToString(),
                    DetailsJson: "{\"seed\":true,\"description\":\"Initial Platform SuperAdmin seeded for development\"}",
                    Outcome: "Success"));
            }
        }
    }
}

