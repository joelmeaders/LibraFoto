using LibraFoto.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LibraFoto.Data;

/// <summary>
/// Design-time factory for LibraFotoDbContext.
/// This allows EF Core tools (migrations, database commands) to create DbContext instances
/// without needing the full application DI container.
/// </summary>
public class LibraFotoDbContextFactory : IDesignTimeDbContextFactory<LibraFotoDbContext>
{
    public LibraFotoDbContext CreateDbContext(string[] args)
    {
        // Use default database path for design-time operations
        var connectionString = $"Data Source={LibraFotoDefaults.GetDefaultDatabasePath()}";

        var optionsBuilder = new DbContextOptionsBuilder<LibraFotoDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        return new LibraFotoDbContext(optionsBuilder.Options);
    }
}
