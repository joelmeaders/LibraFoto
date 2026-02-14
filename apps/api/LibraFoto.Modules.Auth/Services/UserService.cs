using LibraFoto.Data.Entities;
using LibraFoto.Modules.Auth.Services.Repositories;
using LibraFoto.Modules.Auth.Services.Shared;
using Microsoft.Extensions.Logging;

namespace LibraFoto.Modules.Auth.Services;

/// <summary>
/// User management service implementation using EF Core and SQLite.
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository userRepository,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<UserDto> Users, int TotalCount)> GetUsersAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return await _userRepository.GetUsersAsync(page, pageSize, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetUserByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _userRepository.GetUserByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _userRepository.GetUserByEmailAsync(email, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var exists = await _userRepository.EmailExistsAsync(request.Email, cancellationToken);

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

        await _userRepository.AddUserAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

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
        var user = await _userRepository.GetUserEntityByIdAsync(id, cancellationToken);

        if (user == null)
        {
            return null;
        }

        // Check email availability if changing
        if (request.Email != null && request.Email != user.Email)
        {
            var exists = await _userRepository.EmailExistsExcludingUserAsync(request.Email, id, cancellationToken);
            if (exists)
            {
                throw new InvalidOperationException($"Email '{request.Email}' is already registered.");
            }
            user.Email = request.Email;
        }

        if (request.Password != null)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        if (request.Role != null)
        {
            user.Role = request.Role.Value;
        }

        await _userRepository.SaveChangesAsync(cancellationToken);

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
        var user = await _userRepository.GetUserEntityByIdAsync(id, cancellationToken);

        if (user == null)
        {
            return false;
        }

        await _userRepository.RemoveUserAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted user: {Email}", user.Email);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> GetUserCountAsync(CancellationToken cancellationToken = default)
    {
        return await _userRepository.GetUserCountAsync(cancellationToken);
    }
}
