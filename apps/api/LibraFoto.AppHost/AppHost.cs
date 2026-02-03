using LibraFoto.Shared.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Don't use Aspire's AddSqlite resource - it adds incompatible connection string parameters
// Instead, let the API project handle its own database configuration via appsettings/environment
// The API will use LIBRAFOTO_DATA_DIR or LibraFotoDefaults paths

var api = builder.AddProject<Projects.LibraFoto_Api>("api");

// Add frontend dev servers for development
// Display frontend (Vite on port 3000)
var displayFrontend = builder.AddNpmApp("display", "../../apps/display", "dev")
    .WithHttpEndpoint(port: 3000, targetPort: 3000, isProxied: false)
    .WithExternalHttpEndpoints();

// Admin frontend (Angular on port 4200)
var adminFrontend = builder.AddNpmApp("admin", "../../apps/admin", "start")
    .WithHttpEndpoint(port: 4200, targetPort: 4200, isProxied: false)
    .WithExternalHttpEndpoints();

builder.Build().Run();
