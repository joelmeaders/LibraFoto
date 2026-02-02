using LibraFoto.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LibraFoto.Tests.Helpers;

/// <summary>
/// Helper for creating test database contexts that bypass the compiled model.
/// The compiled model uses SetSentinelFromProviderValue which has initialization issues
/// in test scenarios. Using a runtime-built model avoids this problem.
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>
    /// Creates a DbContext options builder configured for testing with SQLite in-memory.
    /// </summary>
    /// <param name="connection">Open SQLite connection (must be kept open for the test duration)</param>
    /// <returns>DbContextOptions configured for testing</returns>
    public static DbContextOptions<LibraFotoDbContext> CreateInMemoryOptions(SqliteConnection connection)
    {
        return new DbContextOptionsBuilder<LibraFotoDbContext>()
            .UseSqlite(connection)
            .Options;
    }

    /// <summary>
    /// Creates a DbContext options builder configured for testing with SQLite file.
    /// </summary>
    /// <param name="connectionString">SQLite connection string</param>
    /// <returns>DbContextOptions configured for testing</returns>
    public static DbContextOptions<LibraFotoDbContext> CreateFileOptions(string connectionString)
    {
        return new DbContextOptionsBuilder<LibraFotoDbContext>()
            .UseSqlite(connectionString)
            .Options;
    }

    /// <summary>
    /// Creates an open SQLite in-memory connection for testing.
    /// The connection must be kept open for the entire test duration.
    /// </summary>
    public static async Task<SqliteConnection> CreateOpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }
}
