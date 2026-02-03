using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LibraFoto.Data;
using LibraFoto.Modules.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace LibraFoto.Modules.Auth.Services;

/// <summary>
/// JWT-based authentication service implementation.
/// </summary>
public class AuthService : IAuthService
{
    private readonly LibraFotoDbContext _dbContext;
    private readonly IUserService _userService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    // In-memory storage for refresh tokens (should be replaced with database storage)
    private static readonly Dictionary<string, (long UserId, DateTime ExpiresAt)> _refreshTokens = new();
    private static readonly Dictionary<long, HashSet<string>> _userRefreshTokens = new();

    // In-memory storage for invalidated tokens
    private static readonly HashSet<string> _invalidatedTokens = new();

    public AuthService(
        LibraFotoDbContext dbContext,
        IUserService userService,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _userService = userService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // Get user from database for validation
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower(), cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Login attempt for non-existent email: {Email}", request.Email);
            return null;
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Invalid password for user: {Email}", request.Email);
            return null;
        }

        // Update last login time
        user.LastLogin = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Generate tokens
        var userDto = new UserDto(
            user.Id,
            user.Email,
            user.Role,
            user.DateCreated,
            user.LastLogin);

        var (token, expiresAt) = GenerateJwtToken(userDto);
        var refreshToken = GenerateRefreshToken(userDto.Id);

        _logger.LogInformation("User logged in successfully: {Email}", request.Email);

        return new LoginResponse(token, refreshToken, expiresAt, userDto);
    }

    /// <inheritdoc />
    public Task LogoutAsync(long userId, CancellationToken cancellationToken = default)
    {
        // Invalidate all refresh tokens for the user
        if (_userRefreshTokens.TryGetValue(userId, out var tokens))
        {
            foreach (var token in tokens)
            {
                _refreshTokens.Remove(token);
            }
            _userRefreshTokens.Remove(userId);
        }

        _logger.LogInformation("User logged out: {UserId}", userId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetCurrentUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await _userService.GetUserByIdAsync(userId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<long?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_invalidatedTokens.Contains(token))
            {
                return Task.FromResult<long?>(null);
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = GetJwtKey();

            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = GetJwtIssuer(),
                ValidateAudience = true,
                ValidAudience = GetJwtAudience(),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && long.TryParse(userIdClaim.Value, out var userId))
            {
                return Task.FromResult<long?>(userId);
            }

            return Task.FromResult<long?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return Task.FromResult<long?>(null);
        }
    }

    /// <inheritdoc />
    public async Task<LoginResponse?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (!_refreshTokens.TryGetValue(refreshToken, out var tokenData))
        {
            _logger.LogWarning("Invalid refresh token");
            return null;
        }

        if (tokenData.ExpiresAt < DateTime.UtcNow)
        {
            _refreshTokens.Remove(refreshToken);
            _userRefreshTokens.GetValueOrDefault(tokenData.UserId)?.Remove(refreshToken);
            _logger.LogWarning("Expired refresh token for user: {UserId}", tokenData.UserId);
            return null;
        }

        var user = await _userService.GetUserByIdAsync(tokenData.UserId, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("User not found for refresh token: {UserId}", tokenData.UserId);
            return null;
        }

        // Remove old refresh token
        _refreshTokens.Remove(refreshToken);
        _userRefreshTokens.GetValueOrDefault(tokenData.UserId)?.Remove(refreshToken);

        // Generate new tokens
        var (token, expiresAt) = GenerateJwtToken(user);
        var newRefreshToken = GenerateRefreshToken(user.Id);

        _logger.LogInformation("Token refreshed for user: {UserId}", tokenData.UserId);

        return new LoginResponse(token, newRefreshToken, expiresAt, user);
    }

    private (string Token, DateTime ExpiresAt) GenerateJwtToken(UserDto user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = GetJwtKey();
        var expiresAt = DateTime.UtcNow.AddMinutes(GetJwtExpirationMinutes());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Email),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("role_level", ((int)user.Role).ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = GetJwtIssuer(),
            Audience = GetJwtAudience(),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return (tokenHandler.WriteToken(token), expiresAt);
    }

    private string GenerateRefreshToken(long userId)
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        var refreshToken = Convert.ToBase64String(randomBytes);
        var expiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenExpirationDays());

        _refreshTokens[refreshToken] = (userId, expiresAt);

        if (!_userRefreshTokens.ContainsKey(userId))
        {
            _userRefreshTokens[userId] = new HashSet<string>();
        }
        _userRefreshTokens[userId].Add(refreshToken);

        return refreshToken;
    }

    private byte[] GetJwtKey()
    {
        var key = _configuration["Jwt:Key"] ?? "LibraFoto-Default-Secret-Key-Change-In-Production-32chars";
        return Encoding.UTF8.GetBytes(key);
    }

    private string GetJwtIssuer() => _configuration["Jwt:Issuer"] ?? "LibraFoto";

    private string GetJwtAudience() => _configuration["Jwt:Audience"] ?? "LibraFoto";

    private int GetJwtExpirationMinutes() => int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60");

    private int GetRefreshTokenExpirationDays() => int.Parse(_configuration["Jwt:RefreshTokenExpirationDays"] ?? "7");
}
