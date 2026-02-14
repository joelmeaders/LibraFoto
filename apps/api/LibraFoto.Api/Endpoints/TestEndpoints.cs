using LibraFoto.Api.Repositories;
using LibraFoto.Shared.Configuration;

namespace LibraFoto.Api.Endpoints;

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
        ITestDataResetRepository testDataResetRepository,
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
            var storagePath = configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();

            await testDataResetRepository.ResetDatabaseAsync(
                storagePath,
                TestAdminEmail,
                TestAdminPassword);

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
