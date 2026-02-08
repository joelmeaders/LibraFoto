using LibraFoto.Data;
using LibraFoto.Data.Entities;
using LibraFoto.Data.Enums;
using LibraFoto.Modules.Auth.Services;
using LibraFoto.Shared.Configuration;
using Microsoft.EntityFrameworkCore;

namespace LibraFoto.Api.Endpoints
{
    /// <summary>
    /// Test-only endpoints for E2E testing.
    /// These endpoints are only available in Development environment.
    /// </summary>
    public static class TestEndpoints
    {
        /// <summary>
        /// Test admin credentials (must match fixtures.ts TEST_ADMIN)
        /// </summary>
        private const string TestAdminEmail = "testadmin@librafoto.local";
        private const string TestAdminPassword = "TestPassword123!";

        public static IEndpointRouteBuilder MapTestEndpoints(this IEndpointRouteBuilder app)
        {
            // Only register these endpoints in Development environment
            if (!app.ServiceProvider.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            {
                return app;
            }

            var group = app.MapGroup("/api/test")
                .WithTags("Test");

            group.MapPost("/reset", ResetDatabase)
                .WithName("ResetDatabase")
                .WithDescription("Resets the database to a clean state for E2E testing. Development only.");

            group.MapGet("/health", () => TypedResults.Ok(new { status = "ok", environment = "test" }))
                .WithName("TestHealth");

            return app;
        }

        /// <summary>
        /// Resets the database to a clean state:
        /// - Deletes all photos, albums, tags, users, guest links
        /// - Resets display settings to defaults
        /// - Creates a fresh test admin user
        /// - Resets the setup completed flag
        /// </summary>
        private static async Task<IResult> ResetDatabase(
            LibraFotoDbContext db,
            IWebHostEnvironment env,
            IConfiguration configuration,
            ILogger<Program> logger)
        {
            if (!env.IsDevelopment())
            {
                return TypedResults.Forbid();
            }

            logger.LogWarning("⚠️ Database reset requested - clearing all data for E2E tests");

            try
            {
                // Ensure database exists (creates schema if missing)
                // This is safe because we're in Development environment only
                await db.Database.EnsureCreatedAsync();

                // Delete all data in correct order (respecting foreign keys)
                // 1. Delete junction tables first
                await db.PhotoAlbums.ExecuteDeleteAsync();
                await db.PhotoTags.ExecuteDeleteAsync();

                // 2. Delete guest links (depends on users and albums)
                await db.GuestLinks.ExecuteDeleteAsync();

                // 3. Delete main entities
                await db.Photos.ExecuteDeleteAsync();
                await db.Albums.ExecuteDeleteAsync();
                await db.Tags.ExecuteDeleteAsync();
                await db.Users.ExecuteDeleteAsync();

                // 4. Reset display settings to defaults
                await db.DisplaySettings.ExecuteDeleteAsync();

                // Create default display settings
                var defaultSettings = new DisplaySettings
                {
                    Name = "Default",
                    SlideDuration = 10,
                    Transition = TransitionType.Fade,
                    TransitionDuration = 1000,
                    Shuffle = false,
                    SourceType = SourceType.All,
                    SourceId = null,
                    IsActive = true
                };
                db.DisplaySettings.Add(defaultSettings);

                // 5. Reset storage providers - delete all except recreate default local provider
                await db.StorageProviders.ExecuteDeleteAsync();

                var storagePath = configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();
                var localProvider = new StorageProvider
                {
                    Name = "Local Storage",
                    Type = StorageProviderType.Local,
                    IsEnabled = true,
                    Configuration = $"{{\"basePath\": \"{storagePath.Replace("\\", "\\\\")}\"}}",
                    LastSyncDate = null
                };
                db.StorageProviders.Add(localProvider);

                // 6. Create test admin user with hashed password
                var testAdmin = new User
                {
                    Email = TestAdminEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(TestAdminPassword),
                    Role = LibraFoto.Data.Enums.UserRole.Admin,
                    DateCreated = DateTime.UtcNow
                };
                db.Users.Add(testAdmin);

                await db.SaveChangesAsync();

                // 7. Clean up photo files on disk (optional - preserves disk space)
                var photosPath = configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();
                if (Directory.Exists(photosPath))
                {
                    // Delete all files in photos directory but keep the directory
                    foreach (var file in Directory.GetFiles(photosPath, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to delete photo file: {File}", file);
                        }
                    }

                    // Delete subdirectories
                    foreach (var dir in Directory.GetDirectories(photosPath))
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to delete photo directory: {Dir}", dir);
                        }
                    }
                }

                logger.LogInformation("✅ Database reset complete - test admin user '{Email}' created", TestAdminEmail);

                return TypedResults.Ok(new ResetResult(
                    Success: true,
                    Message: "Database reset successfully",
                    TestAdminEmail: TestAdminEmail
                ));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Database reset failed");
                return TypedResults.Problem(
                    detail: ex.Message,
                    title: "Database reset failed",
                    statusCode: 500
                );
            }
        }
    }

    /// <summary>
    /// Response from database reset operation.
    /// </summary>
    public record ResetResult(
        bool Success,
        string Message,
        string TestAdminEmail
    );
}
