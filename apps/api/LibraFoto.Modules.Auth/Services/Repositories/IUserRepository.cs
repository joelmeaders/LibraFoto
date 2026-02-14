using LibraFoto.Data.Entities;
using LibraFoto.Modules.Auth.Services.Shared;

namespace LibraFoto.Modules.Auth.Services.Repositories;

/// <summary>
/// Repository abstraction for user data access operations.
/// </summary>
public interface IUserRepository
{
    Task<(IEnumerable<UserDto> Users, int TotalCount)> GetUsersAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<UserDto?> GetUserByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> GetUserEntityByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<User?> GetUserEntityByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsExcludingUserAsync(string email, long excludedUserId, CancellationToken cancellationToken = default);

    Task AddUserAsync(User user, CancellationToken cancellationToken = default);

    Task RemoveUserAsync(User user, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<int> GetUserCountAsync(CancellationToken cancellationToken = default);
}
