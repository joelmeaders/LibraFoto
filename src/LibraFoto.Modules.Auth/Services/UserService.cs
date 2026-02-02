using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Modules.Auth.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Auth.Services;

/// <summary>
/// User management service implementation using EF Core and SQLite.
/// </summary>
public class UserService : IUserService
{
    private readonly LibraFotoDbContext _dbContext;
    private readonly ILogger<UserService> _logger;

    public UserService(
        LibraFotoDbContext dbContext,
        ILogger<UserService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<UserDto> Users, int TotalCount)> GetUsersAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await _dbContext.Users.CountAsync(cancellationToken);

        var users = await _dbContext.Users
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserDto(
                u.Id,
                u.Email,
                u.Role,
                u.DateCreated,
                u.LastLogin))
            .ToListAsync(cancellationToken);

        return (users.AsEnumerable(), totalCount);
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetUserByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        // TODO: Consider security implications of exposing sequential user IDs
        var user = await _dbContext.Users.FindAsync(new object[] { id }, cancellationToken);

        if (user == null) return null;

        return new UserDto(
            user.Id,
            user.Email,
            user.Role,
            user.DateCreated,
            user.LastLogin);
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);

        if (user == null) return null;

        return new UserDto(
            user.Id,
            user.Email,
            user.Role,
            user.DateCreated,
            user.LastLogin);
    }

    /// <inheritdoc />
    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        // Check if email already exists
        var exists = await _dbContext.Users
            .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower(), cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Email '{request.Email}' is already registered.");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new User
        {
            Email = request.Email,
            PasswordHash = passwordHash,
            Role = request.Role,
            DateCreated = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created user: {Email} with role {Role}", request.Email, request.Role);

        return new UserDto(
            user.Id,
            user.Email,
            user.Role,
            user.DateCreated,
            user.LastLogin);
    }

    /// <inheritdoc />
    public async Task<UserDto?> UpdateUserAsync(long id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { id }, cancellationToken);

        if (user == null) return null;

        // Check email availability if changing
        if (request.Email != null && request.Email != user.Email)
        {
            var exists = await _dbContext.Users
                .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower(), cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException($"Email '{request.Email}' is already registered.");
            }
            user.Email = request.Email;
        }

        if (request.Password != null) user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        if (request.Role != null) user.Role = request.Role.Value;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated user: {Email}", user.Email);

        return new UserDto(
            user.Id,
            user.Email,
            user.Role,
            user.DateCreated,
            user.LastLogin);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteUserAsync(long id, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { id }, cancellationToken);

        if (user == null) return false;

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted user: {Email}", user.Email);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> GetUserCountAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users.CountAsync(cancellationToken);
    }
}
