using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AgentPlatform.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace AgentPlatform.Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    // Phase A 简化版：硬编码默认用户（Phase B 将迁移到数据库）
    private static readonly List<(string Username, string Password, string DisplayName, string Role)> DefaultUsers =
    [
        ("admin", "admin123", "管理员", "Admin"),
        ("user", "user123", "普通用户", "User")
    ];

    public AuthController(IConfiguration config, ILogger<AuthController> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 用户登录，返回 JWT Token
    /// </summary>
    [HttpPost("login")]
    public ActionResult<ApiResponse<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var user = DefaultUsers.FirstOrDefault(u =>
            u.Username == request.Username && u.Password == request.Password);

        if (user == default)
        {
            _logger.LogWarning("Login failed for user: {Username}", request.Username);
            return Unauthorized(new ApiResponse<LoginResponse>(false, "用户名或密码错误", null));
        }

        var token = GenerateJwtToken(user.Username, user.DisplayName, user.Role);
        _logger.LogInformation("User logged in: {Username} ({DisplayName})", user.Username, user.DisplayName);

        return Ok(new ApiResponse<LoginResponse>(true, "OK", new LoginResponse(
            token,
            user.Username,
            user.DisplayName,
            user.Role
        )));
    }

    private string GenerateJwtToken(string username, string displayName, string role)
    {
        var jwtSection = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, username),
            new Claim(ClaimTypes.Name, displayName),
            new Claim(ClaimTypes.Role, role),
            new Claim("username", username)
        };

        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(double.Parse(jwtSection["ExpireMinutes"] ?? "480")),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Username, string DisplayName, string Role);
