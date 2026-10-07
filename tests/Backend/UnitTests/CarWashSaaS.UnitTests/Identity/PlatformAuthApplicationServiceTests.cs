using CarWashSaaS.Identity.Application;
using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.UnitTests.Identity;

public sealed class PlatformAuthApplicationServiceTests
{
    [Fact]
    public async Task AuthenticateAsync_ShouldSucceed_AndLogAuditEvent_WhenCredentialsAreValid()
    {
        var userRepo = new FakePlatformUserRepository();
        var auditRepo = new FakeAuditEventRepository();
        var tokenService = new FakeTokenService();
        var auditService = new AuditTrailApplicationService(auditRepo);
        var authService = new PlatformAuthApplicationService(userRepo, tokenService, auditService);

        var user = PlatformUser.Create(
            "admin@lavaway.com",
            "Super Administrador",
            PlatformRole.SuperAdmin,
            "hashed_Admin123!").Value!;
        await userRepo.AddAsync(user);

        var request = new PlatformLoginRequest("admin@lavaway.com", "Admin123!");
        var result = await authService.AuthenticateAsync(request, "127.0.0.1", "Mozilla");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("admin@lavaway.com", result.Value.Email);
        Assert.Null(result.Value.TenantId);
        Assert.Equal(PlatformRole.SuperAdmin.ToString(), result.Value.Role);

        // Verify audit event logged
        Assert.Single(auditRepo.Events);
        var audit = auditRepo.Events[0];
        Assert.Equal(PlatformActionConstants.AuthLoginSuccess, audit.Action);
        Assert.Equal("admin@lavaway.com", audit.ActorEmail);
        Assert.Equal("Success", audit.Outcome);
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldFail_AndLogFailureEvent_WhenPasswordIsIncorrect()
    {
        var userRepo = new FakePlatformUserRepository();
        var auditRepo = new FakeAuditEventRepository();
        var tokenService = new FakeTokenService();
        var auditService = new AuditTrailApplicationService(auditRepo);
        var authService = new PlatformAuthApplicationService(userRepo, tokenService, auditService);

        var user = PlatformUser.Create(
            "admin@lavaway.com",
            "Super Administrador",
            PlatformRole.SuperAdmin,
            "hashed_Admin123!").Value!;
        await userRepo.AddAsync(user);

        var request = new PlatformLoginRequest("admin@lavaway.com", "WrongPassword");
        var result = await authService.AuthenticateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);

        // Verify audit failure logged
        Assert.Single(auditRepo.Events);
        var audit = auditRepo.Events[0];
        Assert.Equal(PlatformActionConstants.AuthLoginFailed, audit.Action);
        Assert.Equal("Failure", audit.Outcome);
    }

    [Fact]
    public async Task CreatePlatformUserAsync_ShouldPersistUser_AndLogAuditEvent()
    {
        var userRepo = new FakePlatformUserRepository();
        var auditRepo = new FakeAuditEventRepository();
        var tokenService = new FakeTokenService();
        var auditService = new AuditTrailApplicationService(auditRepo);
        var authService = new PlatformAuthApplicationService(userRepo, tokenService, auditService);

        var adminId = Guid.NewGuid();
        var request = new CreatePlatformUserRequest(
            "support@lavaway.com",
            "Support123!",
            "Agente Suporte",
            PlatformRole.PlatformSupport.ToString());

        var result = await authService.CreatePlatformUserAsync(request, adminId, "superadmin@lavaway.com", "SuperAdmin");

        Assert.True(result.IsSuccess);
        Assert.Equal("support@lavaway.com", result.Value!.Email);
        Assert.Equal(PlatformRole.PlatformSupport.ToString(), result.Value.Role);

        // User persisted in repository
        var created = await userRepo.FindByEmailAsync("support@lavaway.com");
        Assert.NotNull(created);

        // Audit event recorded
        Assert.Single(auditRepo.Events);
        var audit = auditRepo.Events[0];
        Assert.Equal(PlatformActionConstants.PlatformUserCreated, audit.Action);
        Assert.Equal(adminId, audit.ActorId);
    }

    private sealed class FakePlatformUserRepository : IPlatformUserRepository
    {
        public List<PlatformUser> Users { get; } = [];

        public Task<PlatformUser?> FindByEmailAsync(string email, CancellationToken ct = default)
        {
            return Task.FromResult(Users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<PlatformUser?> FindByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
        }

        public Task<IReadOnlyCollection<PlatformUser>> ListAllAsync(CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyCollection<PlatformUser>>(Users.ToArray());
        }

        public Task AddAsync(PlatformUser user, CancellationToken ct = default)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }

        public void Update(PlatformUser user)
        {
            var idx = Users.FindIndex(u => u.Id == user.Id);
            if (idx >= 0)
            {
                Users[idx] = user;
            }
        }

        public bool VerifyPassword(PlatformUser user, string password)
        {
            return user.PasswordHash == $"hashed_{password}";
        }

        public string HashPassword(string password)
        {
            return $"hashed_{password}";
        }
    }

    private sealed class FakeAuditEventRepository : IAdministrativeAuditEventRepository
    {
        public List<AdministrativeAuditEvent> Events { get; } = [];

        public Task AddAsync(AdministrativeAuditEvent auditEvent, CancellationToken ct = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<AdministrativeAuditEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return Task.FromResult(Events.FirstOrDefault(e => e.Id == id));
        }

        public Task<PagedResult<AdministrativeAuditEvent>> SearchAsync(AuditQueryFilter filter, CancellationToken ct = default)
        {
            return Task.FromResult(new PagedResult<AdministrativeAuditEvent>(Events, Events.Count, filter.Page, filter.PageSize));
        }
    }

    private sealed class FakeTokenService : ITokenService
    {
        public GeneratedToken GenerateAccessToken(Guid userId, string email, Guid tenantId, ShopRole role, IReadOnlyCollection<ShopPermission> permissions)
        {
            return new GeneratedToken("token", 3600);
        }

        public GeneratedToken GeneratePlatformAccessToken(Guid userId, string email, string fullName, PlatformRole role, IReadOnlyCollection<PlatformPermission> permissions)
        {
            return new GeneratedToken("platform_jwt", 3600);
        }

        public string GenerateRefreshToken() => "refresh_token";

        public string HashToken(string token) => "hash";
    }
}
