using CarWashSaaS.Shared.Contracts;

namespace CarWashSaaS.Identity.Domain;

public sealed class PlatformUser
{
    private PlatformUser()
    {
    }

    private PlatformUser(
        Guid id,
        string email,
        string fullName,
        PlatformRole role,
        string passwordHash,
        bool isActive,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        Email = email;
        FullName = fullName;
        Role = role;
        PasswordHash = passwordHash;
        IsActive = isActive;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public PlatformRole Role { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    public static Result<PlatformUser> Create(
        string email,
        string fullName,
        PlatformRole role,
        string passwordHash,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<PlatformUser>.Failure(new Error(
                "platform_user.email.required",
                "Email is required.",
                ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result<PlatformUser>.Failure(new Error(
                "platform_user.full_name.required",
                "Full name is required.",
                ErrorType.Validation));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return Result<PlatformUser>.Failure(new Error(
                "platform_user.password.required",
                "Password hash is required.",
                ErrorType.Validation));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedFullName = fullName.Trim();
        var userId = id.HasValue && id.Value != Guid.Empty ? id.Value : Guid.CreateVersion7();

        return Result<PlatformUser>.Success(new PlatformUser(
            userId,
            normalizedEmail,
            normalizedFullName,
            role,
            passwordHash,
            true,
            DateTimeOffset.UtcNow));
    }

    public Result ChangeRole(PlatformRole newRole)
    {
        Role = newRole;
        return Result.Success();
    }

    public Result Deactivate()
    {
        IsActive = false;
        return Result.Success();
    }

    public Result Activate()
    {
        IsActive = true;
        return Result.Success();
    }

    public void RecordLogin(DateTimeOffset loginTimeUtc)
    {
        LastLoginAtUtc = loginTimeUtc;
    }

    public void UpdatePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
    }
}
