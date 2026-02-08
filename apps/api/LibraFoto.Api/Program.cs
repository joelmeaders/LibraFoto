using System.Reflection;
using System.Text;
using LibraFoto.Api;
using LibraFoto.Api.Endpoints;
using LibraFoto.Api.Infrastructure;
using LibraFoto.Data;
using LibraFoto.Modules.Admin;
using LibraFoto.Modules.Auth;
using LibraFoto.Modules.Display;
using LibraFoto.Modules.Media;
using LibraFoto.Modules.Storage;
using LibraFoto.Shared.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// Configure Serilog early to capture startup errors
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "logs/librafoto-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting LibraFoto API");

    // Support LIBRAFOTO_DATA_DIR environment variable as a shorthand for setting both database and storage paths
    var dataDir = Environment.GetEnvironmentVariable("LIBRAFOTO_DATA_DIR");
    if (!string.IsNullOrEmpty(dataDir))
    {
        // Create directory if it doesn't exist
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }

        // Set derived configuration values if not already set
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ConnectionStrings__LibraFotoDb")))
        {
            var dbPath = Path.Combine(dataDir, "librafoto.db");
            Environment.SetEnvironmentVariable("ConnectionStrings__LibraFotoDb", $"Data Source={dbPath}");
            Log.Information("LIBRAFOTO_DATA_DIR: Setting database path to {DbPath}", dbPath);
        }

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Storage__LocalPath")))
        {
            var photosPath = Path.Combine(dataDir, "photos");
            Environment.SetEnvironmentVariable("Storage__LocalPath", photosPath);
            Log.Information("LIBRAFOTO_DATA_DIR: Setting storage path to {PhotosPath}", photosPath);
        }
    }

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog();

    // Add Aspire service defaults (telemetry, health checks, resilience)
    builder.AddServiceDefaults();

    // Add global exception handler
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // Configure JWT authentication
    var jwtKey = builder.Configuration["Jwt:Key"] ??
        throw new InvalidOperationException("JWT key is not configured. Please set the 'Jwt:Key' configuration value.");
    var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "LibraFoto";
    var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "LibraFoto";

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

    // Register data module (SQLite database)
    var connectionString = builder.Configuration.GetConnectionString("LibraFotoDb") ?? $"Data Source={LibraFotoDefaults.GetDefaultDatabasePath()}";
    builder.Services.AddDataModule(connectionString);

    // Register module services
    builder.Services.AddDisplayModule();
    builder.Services.AddAdminModule();
    builder.Services.AddStorageModule();
    builder.Services.AddMediaModule();
    builder.Services.AddAuthModule();

    // Log configuration paths for debugging
    var dbConnectionString = builder.Configuration.GetConnectionString("LibraFotoDb") ?? $"Data Source={LibraFotoDefaults.GetDefaultDatabasePath()}";
    var storagePath = builder.Configuration["Storage:LocalPath"] ?? LibraFotoDefaults.GetDefaultPhotosPath();
    Log.Information("Database: {DbPath}", dbConnectionString);
    Log.Information("Storage: {StoragePath}", storagePath);

    var app = builder.Build();

    await app.Services.EnsureDatabaseMigratedAsync();

    // Use exception handler
    app.UseExceptionHandler();

    // Database migrations are applied automatically at startup.

    // Use authentication and authorization middleware
    app.UseAuthentication();
    app.UseAuthorization();

    // Map Aspire default endpoints (/health, /alive)
    app.MapDefaultEndpoints();

    // Map module endpoints
    app.MapDisplayEndpoints();
    app.MapAdminEndpoints();
    app.MapStorageEndpoints();
    app.MapMediaEndpoints();
    app.MapAuthEndpoints();

    // Only map test endpoints if explicitly enabled (e.g. for E2E tests)
    if (Environment.GetEnvironmentVariable("ENABLE_TEST_ENDPOINTS") == "true")
    {
        app.MapTestEndpoints();
    }

    // Root endpoint for API info
    var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
    app.MapGet("/", () => TypedResults.Ok(new ApiInfo("LibraFoto API", version)));

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
