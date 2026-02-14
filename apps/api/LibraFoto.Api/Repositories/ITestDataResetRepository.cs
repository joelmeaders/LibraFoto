namespace LibraFoto.Api.Repositories;

/// <summary>
/// Repository abstraction for test-only database reset operations.
/// </summary>
public interface ITestDataResetRepository
{
    Task ResetDatabaseAsync(
        string storagePath,
        string testAdminEmail,
        string testAdminPassword,
        CancellationToken cancellationToken = default);
}
