using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Modules.Auth.Services.Shared;
using Microsoft.EntityFrameworkCore;

namespace LibraFoto.Modules.Auth.Services.Repositories;

/// <summary>
/// Entity Framework implementation of user repository.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly LibraFotoDbContext _dbContext;

    public UserRepository(LibraFotoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

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

    public async Task<UserDto?> GetUserByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { id }, cancellationToken);

        if (user == null)
        {
            return null;
        }

        return new UserDto(
            user.Id,
            user.Email,
            user.Role,
            user.DateCreated,
            user.LastLogin);
    }

    public async Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);

        if (user == null)
        {
            return null;
        }

        return new UserDto(
            user.Id,
            user.Email,
            user.Role,
            user.DateCreated,
            user.LastLogin);
    }

    public Task<User?> GetUserEntityByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.FindAsync(new object[] { id }, cancellationToken).AsTask();
    }

    public Task<User?> GetUserEntityByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .AnyAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
    }

    public Task<bool> EmailExistsExcludingUserAsync(string email, long excludedUserId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .AnyAsync(u => u.Id != excludedUserId && u.Email.ToLower() == email.ToLower(), cancellationToken);
    }

    public async Task AddUserAsync(User user, CancellationToken cancellationToken = default)
    {
        await _dbContext.Users.AddAsync(user, cancellationToken);
    }

    public Task RemoveUserAsync(User user, CancellationToken cancellationToken = default)
    {
        _dbContext.Users.Remove(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<int> GetUserCountAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.CountAsync(cancellationToken);
    }
}
