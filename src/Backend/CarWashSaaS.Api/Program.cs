using CarWashSaaS.Identity.Infrastructure;
using CarWashSaaS.Tenants.Infrastructure;
using CarWashSaaS.YardOperations.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("CarWashSaaS")
    ?? throw new InvalidOperationException("Connection string 'CarWashSaaS' is required.");

builder.Services.AddDbContext<TenantsDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants")));
builder.Services.AddDbContext<IdentityModuleDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));
builder.Services.AddDbContext<YardOperationsDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "yard")));

builder.Services.AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<IdentityModuleDbContext>();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
