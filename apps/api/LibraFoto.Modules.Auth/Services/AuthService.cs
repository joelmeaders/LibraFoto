using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LibraFoto.Data;
using LibraFoto.Modules.Auth.Services.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace LibraFoto.Modules.Auth.Services;

/// <summary>
/// JWT-based authentication service implementation.
/// Registered as singleton to maintain token state, creates scoped DbContext per operation.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly ITokenStore _tokenStore;

    public AuthService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ITokenStore tokenStore,
        ILogger<AuthService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LibraFotoDbContext>();

        // Get user from database for validation
        var user = await dbContext.Users
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
        await dbContext.SaveChangesAsync(cancellationToken);

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
        _tokenStore.RemoveAllUserRefreshTokens(userId);

        _logger.LogInformation("User logged out: {UserId}", userId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetCurrentUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        return await userService.GetUserByIdAsync(userId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<long?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_tokenStore.IsTokenInvalidated(token))
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
        if (!_tokenStore.TryGetRefreshToken(refreshToken, out var userId, out var expiresAt))
        {
            _logger.LogWarning("Invalid refresh token");
            return null;
        }

        if (expiresAt < DateTime.UtcNow)
        {
            _tokenStore.RemoveRefreshToken(refreshToken);
            _logger.LogWarning("Expired refresh token for user: {UserId}", userId);
            return null;
        }

        using var scope = _scopeFactory.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

        var user = await userService.GetUserByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("User not found for refresh token: {UserId}", userId);
            return null;
        }

        // Remove old refresh token
        _tokenStore.RemoveRefreshToken(refreshToken);

        // Generate new tokens
        var (token, newExpiresAt) = GenerateJwtToken(user);
        var newRefreshToken = GenerateRefreshToken(user.Id);

        _logger.LogInformation("Token refreshed for user: {UserId}", userId);

        return new LoginResponse(token, newRefreshToken, newExpiresAt, user);
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

        _tokenStore.StoreRefreshToken(refreshToken, userId, expiresAt);

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
