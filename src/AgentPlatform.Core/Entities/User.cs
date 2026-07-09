namespace AgentPlatform.Core.Entities;

/// <summary>
/// 用户实体（Phase A 简化版本，仅用于 JWT 认证）
/// 完整用户管理（RBAC、CRUD）将在 Phase B 实现
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class UserRoles
{
    public const string Admin = "admin";
    public const string User = "user";
    public const string Viewer = "viewer";
}
