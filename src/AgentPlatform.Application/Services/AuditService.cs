using AgentPlatform.Core.Entities;
using AgentPlatform.Core.Interfaces;

namespace AgentPlatform.Application.Services;

public class AuditService
{
    private readonly IAuditLogRepository _repository;

    public AuditService(IAuditLogRepository repository) => _repository = repository;

    public async Task<List<AuditLog>> GetAllAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => await _repository.GetAllAsync(from, to, ct);

    public async Task LogAsync(string userId, string username, string action, string entityType,
        string? entityId = null, string? changes = null, string? ipAddress = null, string? userAgent = null,
        CancellationToken ct = default)
    {
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Username = username,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Changes = changes,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.AddAsync(log, ct);
    }
}

public class UserService
{
    private readonly IUserRepository _repository;

    public UserService(IUserRepository repository) => _repository = repository;

    public async Task<List<User>> GetAllAsync(CancellationToken ct = default)
        => await _repository.GetAllAsync(ct);

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _repository.GetByIdAsync(id, ct);

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => await _repository.GetByUsernameAsync(username, ct);

    public async Task<User> CreateAsync(string username, string password, string displayName, string email, string role = UserRoles.User,
        CancellationToken ct = default)
    {
        var existing = await _repository.GetByUsernameAsync(username, ct);
        if (existing is not null)
            throw new InvalidOperationException("Username already exists");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = BCryptHash(password), // simple hashing
            DisplayName = displayName,
            Email = email,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        return await _repository.AddAsync(user, ct);
    }

    public async Task<User?> UpdateAsync(Guid id, string? displayName, string? email, string? role, bool? isEnabled,
        CancellationToken ct = default)
    {
        var user = await _repository.GetByIdAsync(id, ct);
        if (user is null) return null;

        if (displayName is not null) user.DisplayName = displayName;
        if (email is not null) user.Email = email;
        if (role is not null) user.Role = role;
        if (isEnabled.HasValue) user.IsEnabled = isEnabled.Value;

        return await _repository.UpdateAsync(user, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
        return true;
    }

    /// <summary>简单密码哈希（开发环境用，生产应用 BCrypt/Argon2）</summary>
    private static string BCryptHash(string password)
    {
        // In-memory 开发环境使用简单哈希
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLower();
    }
}
