using CarWashSaaS.Identity.Domain;
using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.UnitTests.Identity;

public sealed class AdministrativeAuditEventTests
{
    [Fact]
    public void Create_ShouldSucceed_WhenValidParametersProvided()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var result = AdministrativeAuditEvent.Create(
            actorId: actorId,
            actorEmail: "admin@lavaway.com",
            actorRole: "SuperAdmin",
            actorRealm: "Platform",
            action: PlatformActionConstants.TenantCreated,
            targetType: PlatformTargetTypeConstants.Tenant,
            targetId: tenantId.ToString(),
            tenantId: tenantId,
            ipAddress: "192.168.1.100",
            userAgent: "Mozilla/5.0",
            detailsJson: "{\"name\":\"Auto Clean\"}",
            outcome: "Success");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);

        var evt = result.Value!;
        Assert.NotEqual(Guid.Empty, evt.Id);
        Assert.Equal(actorId, evt.ActorId);
        Assert.Equal("admin@lavaway.com", evt.ActorEmail);
        Assert.Equal("SuperAdmin", evt.ActorRole);
        Assert.Equal("Platform", evt.ActorRealm);
        Assert.Equal(PlatformActionConstants.TenantCreated, evt.Action);
        Assert.Equal(PlatformTargetTypeConstants.Tenant, evt.TargetType);
        Assert.Equal(tenantId.ToString(), evt.TargetId);
        Assert.Equal(tenantId, evt.TenantId);
        Assert.Equal("192.168.1.100", evt.IpAddress);
        Assert.Equal("Mozilla/5.0", evt.UserAgent);
        Assert.Equal("{\"name\":\"Auto Clean\"}", evt.DetailsJson);
        Assert.Equal("Success", evt.Outcome);
        Assert.Null(evt.ErrorMessage);
        Assert.True(evt.TimestampUtc <= DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData("", "admin@lavaway.com", "SuperAdmin", "Action", "Target", "1", "audit.actor_id.required")]
    [InlineData("00000000-0000-0000-0000-000000000000", "admin@lavaway.com", "SuperAdmin", "Action", "Target", "1", "audit.actor_id.required")]
    [InlineData("11111111-1111-1111-1111-111111111111", "", "SuperAdmin", "Action", "Target", "1", "audit.actor_email.required")]
    [InlineData("11111111-1111-1111-1111-111111111111", "admin@lavaway.com", "", "Action", "Target", "1", "audit.actor_role.required")]
    [InlineData("11111111-1111-1111-1111-111111111111", "admin@lavaway.com", "SuperAdmin", "", "Target", "1", "audit.action.required")]
    [InlineData("11111111-1111-1111-1111-111111111111", "admin@lavaway.com", "SuperAdmin", "Action", "", "1", "audit.target_type.required")]
    [InlineData("11111111-1111-1111-1111-111111111111", "admin@lavaway.com", "SuperAdmin", "Action", "Target", "", "audit.target_id.required")]
    public void Create_ShouldFail_WhenRequiredFieldIsMissing(
        string actorIdString,
        string email,
        string role,
        string action,
        string targetType,
        string targetId,
        string expectedErrorCode)
    {
        var actorId = Guid.TryParse(actorIdString, out var parsed) ? parsed : Guid.Empty;

        var result = AdministrativeAuditEvent.Create(
            actorId: actorId,
            actorEmail: email,
            actorRole: role,
            actorRealm: "Platform",
            action: action,
            targetType: targetType,
            targetId: targetId);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedErrorCode, result.Error!.Code);
    }

    [Fact]
    public void Create_ShouldRecordFailureOutcomeAndErrorMessage()
    {
        var actorId = Guid.NewGuid();

        var result = AdministrativeAuditEvent.Create(
            actorId: actorId,
            actorEmail: "support@lavaway.com",
            actorRole: "PlatformSupport",
            actorRealm: "Platform",
            action: PlatformActionConstants.TenantStatusChanged,
            targetType: PlatformTargetTypeConstants.Tenant,
            targetId: Guid.NewGuid().ToString(),
            outcome: "Failure",
            errorMessage: "Unauthorized status transition");

        Assert.True(result.IsSuccess);
        Assert.Equal("Failure", result.Value!.Outcome);
        Assert.Equal("Unauthorized status transition", result.Value.ErrorMessage);
    }
}
