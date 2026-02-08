using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraFoto.Data
{
    /// <summary>
    /// Data module registration for database services.
    /// </summary>
    public static class DataModule
    {
        /// <summary>
        /// Registers the LibraFoto database context with SQLite.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="connectionString">SQLite connection string. Defaults to "Data Source=librafoto.db".</param>
        /// <returns>Service collection for chaining.</returns>
        public static IServiceCollection AddDataModule(this IServiceCollection services, string? connectionString = null)
        {
            connectionString ??= "Data Source=librafoto.db";

            services.AddDbContext<LibraFotoDbContext>(options =>
            {
                options.UseSqlite(connectionString);
            });

            return services;
        }

        /// <summary>
        /// Ensures the database is created.
        /// Run migrations separately during development/deployment with dotnet-ef.
        /// </summary>
        /// <param name="serviceProvider">Service provider.</param>
        public static async Task EnsureDatabaseCreatedAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LibraFotoDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        /// <summary>
        /// Ensures the database is created (synchronous).
        /// Run migrations separately during development/deployment with dotnet-ef.
        /// </summary>
        /// <param name="serviceProvider">Service provider.</param>
        public static void EnsureDatabaseCreated(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LibraFotoDbContext>();
            dbContext.Database.EnsureCreated();
        }

        /// <summary>
        /// Ensures the database is created and all migrations are applied.
        /// </summary>
        /// <param name="serviceProvider">Service provider.</param>
        public static async Task EnsureDatabaseMigratedAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LibraFotoDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        /// <summary>
        /// Ensures the database is created and all migrations are applied (synchronous).
        /// </summary>
        /// <param name="serviceProvider">Service provider.</param>
        public static void EnsureDatabaseMigrated(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<LibraFotoDbContext>();
            dbContext.Database.Migrate();
        }
    }
}
