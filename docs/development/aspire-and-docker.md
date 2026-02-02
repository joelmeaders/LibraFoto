# Aspire and Docker: Development vs Production

This guide explains how LibraFoto uses .NET Aspire for development and Docker Compose for production deployment.

## Overview

LibraFoto uses different orchestration strategies for different environments:

- **Development**: .NET Aspire orchestrates services as processes
- **Production**: Docker Compose orchestrates services as containers

### Visual Architecture

#### Development with Aspire

```
┌──────────────────────────────────────────────────────────┐
│                                                          │
│                  Your Development Machine                │
│                                                          │
│  ┌─────────────────────────────────────────────────┐   │
│  │          .NET Aspire AppHost                    │   │
│  │              (Orchestrator)                     │   │
│  │                                                 │   │
│  │  ┌──────────────────────────────────────────┐  │   │
│  │  │         Aspire Dashboard                 │  │   │
│  │  │  📊 Telemetry 📜 Logs 🔍 Traces         │  │   │
│  │  └──────────────────────────────────────────┘  │   │
│  │                                                 │   │
│  │  Services (as processes):                      │   │
│  │  ┌──────────┐  ┌─────────┐  ┌──────────┐     │   │
│  │  │   API    │  │ Display │  │  Admin   │     │   │
│  │  │ (dotnet) │  │  (npm)  │  │  (npm)   │     │   │
│  │  │ :5179    │  │ :3000   │  │  :4200   │     │   │
│  │  └──────────┘  └─────────┘  └──────────┘     │   │
│  │                                                 │   │
│  │  ┌──────────┐                                  │   │
│  │  │  SQLite  │                                  │   │
│  │  │librafoto │                                  │   │
│  │  │  .db     │                                  │   │
│  │  └──────────┘                                  │   │
│  └─────────────────────────────────────────────────┘   │
│                                                          │
│  Start: dotnet run --project src/LibraFoto.AppHost     │
│  Debug: F5 in VS Code                                   │
│  Hot Reload: ✅ Automatic                               │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

#### Production with Docker Compose

```
┌──────────────────────────────────────────────────────────┐
│                                                          │
│                    Raspberry Pi / Server                 │
│                                                          │
│  ┌─────────────────────────────────────────────────┐   │
│  │          Docker Compose                         │   │
│  │                                                 │   │
│  │  Services (as containers):                      │   │
│  │                                                 │   │
│  │  ┌──────────────────────────────────┐          │   │
│  │  │     Frontend Container           │          │   │
│  │  │        (Nginx :80)               │          │   │
│  │  │  • Serves static files           │          │   │
│  │  │  • Proxies /api/* to API         │          │   │
│  │  └────────────┬─────────────────────┘          │   │
│  │               │                                 │   │
│  │               ▼                                 │   │
│  │  ┌──────────────────────────────────┐          │   │
│  │  │      API Container               │          │   │
│  │  │      (LibraFoto.Api :8080)       │          │   │
│  │  │  • Modular Monolith              │          │   │
│  │  │  • All modules in one process    │          │   │
│  │  └────────────┬─────────────────────┘          │   │
│  │               │                                 │   │
│  │               ▼                                 │   │
│  │  ┌────────────────────────┐                    │   │
│  │  │   Volumes (Docker)     │                    │   │
│  │  │  • librafoto-data      │                    │   │
│  │  │    (SQLite database)   │                    │   │
│  │  │  • librafoto-photos    │                    │   │
│  │  │    (Photo storage)     │                    │   │
│  │  └────────────────────────┘                    │   │
│  │                                                 │   │
│  └─────────────────────────────────────────────────┘   │
│                                                          │
│  Start: docker-compose -f docker/docker-compose.yml up -d
│  Debug: Remote debugging only                           │
│  Updates: docker-compose pull && docker-compose up -d   │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

## Development Workflow (Aspire)

### Standard Development (Recommended)

Run all services through Aspire for the best development experience:

```bash
dotnet run --project src/LibraFoto.AppHost
```

This launches:

- **API**: As a .NET process (fast, debuggable)
- **Display Frontend**: Vite dev server (port 3000)
- **Admin Frontend**: Angular dev server (port 4200)
- **Aspire Dashboard**: Telemetry, logs, traces (shown in terminal output)

**Benefits:**

- ⚡ Fast startup and hot reload
- 🐛 Full debugging support
- 📊 Rich telemetry dashboard
- 🔄 Automatic service discovery
- 💾 Minimal resource usage

### Testing Container Builds

If you want to test your Dockerfile during development, you can modify `AppHost.cs` to run the API as a container:

```csharp
// Replace this line in AppHost.cs:
var api = builder.AddProject<Projects.LibraFoto_Api>("api")
    .WithReference(db);

// With this:
var api = builder.AddDockerfile("api", "../LibraFoto.Api")
    .WithReference(db)
    .WithHttpEndpoint(port: 5179, targetPort: 8080, name: "http")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPNETCORE_URLS", "http://+:8080");
```

**When to use this:**

- Testing Dockerfile changes
- Validating production build
- Debugging container-specific issues

**Trade-offs:**

- ❌ Slower startup (container build time)
- ❌ Slower iteration (rebuild for changes)
- ✅ Matches production environment exactly

## Production Deployment (Docker Compose)

### Building and Running

From the root directory:

```bash
# Build and start all services
docker-compose -f docker/docker-compose.yml up -d

# View logs
docker-compose -f docker/docker-compose.yml logs -f

# Stop services
docker-compose -f docker/docker-compose.yml down
```

### Production Configuration

The production setup (`docker/docker-compose.yml`) includes:

- **API Container**: Built from `src/LibraFoto.Api/Dockerfile`
  - Port 8080 exposed
  - SQLite database volume
  - Photos volume
  - Production environment

- **Frontend Container**: Built from `docker/Dockerfile.frontend`
  - Port 80 exposed
  - Nginx serves static files
  - Proxies API requests

### Development-like Docker Compose

For testing the containerized setup locally with hot reload:

```bash
# Use dev overrides
docker-compose -f docker/docker-compose.yml -f docker/docker-compose.dev.yml up

# This mounts source code as volumes for faster iteration
```

## Key Differences at a Glance

| Aspect                | Development (Aspire)                            | Production (Docker Compose)                         |
| --------------------- | ----------------------------------------------- | --------------------------------------------------- |
| **Command**           | `dotnet run --project src/LibraFoto.AppHost`    | `docker-compose -f docker/docker-compose.yml up -d` |
| **Services Run As**   | Processes (dotnet, npm)                         | Docker containers                                   |
| **Dashboard**         | ✅ Aspire Dashboard (telemetry, logs)           | ❌ Use Portainer/logs                               |
| **Hot Reload**        | ✅ Yes (automatic)                              | ❌ No (requires rebuild)                            |
| **Debugging**         | ✅ Full IDE support (F5)                        | ⚠️ Remote debugging only                            |
| **Startup Time**      | ⚡ Fast (~5 seconds)                            | 🐢 Slower (build + start)                           |
| **Resource Usage**    | 💾 Lower                                        | 💾 Higher (Docker overhead)                         |
| **Service Discovery** | Aspire (automatic)                              | Docker DNS (`http://api:8080`)                      |
| **AppHost Included**  | ✅ Yes (orchestrator)                           | ❌ No (not deployed)                                |
| **Database**          | Local file `librafoto.db`                       | Docker volume `librafoto-data`                      |
| **Photos**            | Local folder `photos/`                          | Docker volume `librafoto-photos`                    |
| **Port Exposure**     | - API: 5179<br>- Display: 3000<br>- Admin: 4200 | - Nginx: 80<br>- API: not exposed                   |

## Transition from Dev to Prod

### What Changes?

1. **Aspire AppHost**: Not used in production (dev-time only)
2. **Service Discovery**: Docker DNS replaces Aspire service discovery
3. **Configuration**: Environment variables via docker-compose.yml
4. **Telemetry**: Configure OTEL_EXPORTER_OTLP_ENDPOINT if using external observability
5. **Database**: Volume-mounted SQLite (persistent across restarts)

### What Stays the Same?

1. **API Code**: Same codebase, same container
2. **Frontends**: Same build output
3. **Configuration System**: Still uses appsettings.json + environment variables

## Debugging Scenarios

### Debugging API Locally

```bash
# Run API directly (no Aspire)
dotnet run --project src/LibraFoto.Api

# Or use F5 in VS Code with launch configuration
```

### Debugging API in Container

```bash
# Build and run API container manually
docker build -f src/LibraFoto.Api/Dockerfile -t librafoto-api .
docker run -p 5179:8080 -e ASPNETCORE_ENVIRONMENT=Development librafoto-api

# Then attach remote debugger from VS Code
```

Workflow Summary

### Daily Development

````bash
# 1. Run Aspire
dotnet run --project src/LibraFoto.AppHost

# 2. Open Aspire dashboard (URL shown in terminal)
# 3. Make code changes (hot reload happens automatically)
# 4. Set breakpoints and debug with F5
```Scenarios

### "I want fast iteration during development"

→ Use Aspire (default setup)

### "I want to test my Dockerfile locally"

→ Build manually with `docker build` or modify AppHost to use containers

### "I want to deploy to production"

→ Use Docker Compose (Aspire not involved)

### "I want to debug a production issue"

→ Check Docker logs: `docker-compose logs -f api`

### "I want to simulate production locally"

→ Run docker-compose on your dev machine

## Common

### Testing Container Build

```bash
# 1. Build API container
docker build -f src/LibraFoto.Api/Dockerfile -t librafoto-api .

# 2. Run it locally
docker run -p 5179:8080 librafoto-api

# 3. Test with browser
curl http://localhost:5179/health
````

### Production Deployment

```bash
# 1. On your Raspberry Pi / server
git pull origin main

# 2. Build and start containers
docker-compose -f docker/docker-compose.yml up -d

# 3. Check status
docker-compose -f docker/docker-compose.yml ps

# 4. View logs
docker-compose -f docker/docker-compose.yml logs -f api
```

##

### Debugging Full Stack with Aspire

```bash
# Just run Aspire - it orchestrates everything
dotnet run --project src/LibraFoto.AppHost

# Set breakpoints in your API code
# F5 attaches to the running process
```

## Common Questions

### Q: Why not use Aspire in production?

**A:** Aspire is a development tool. It requires the .NET SDK and is designed for local development workflows. Production deployments should use production orchestration tools (Docker Compose, Kubernetes, etc.).

### Q: Can I debug containerized services with Aspire?

**A:** Yes, but with limitations. Remote debugging is possible but slower and less integrated than debugging processes. For best experience, debug services as processes during development.

### Q: Do I need to test with containers before deploying?

**A:** Recommended but not required. Your Dockerfile is validated in CI/CD. However, testing container builds locally can catch issues earlier.

### Q: How does service discovery work differently?

**A:**

- **Aspire**: Uses `http://api` (resolved via Aspire's service discovery)
- **Docker Compose**: Uses `http://api:8080` (resolved via Docker DNS)
- Both work transparently to your code thanks to `AddServiceDefaults()`

## Best Practices

### Development

1. ✅ Use Aspire for daily development
2. ✅ Run services as processes (not containers)
3. ✅ Leverage hot reload and fast iteration
4. ✅ Use Aspire dashboard for observability

### Pre-Deployment Testing

1. ✅ Build containers locally: `docker build -f src/LibraFoto.Api/Dockerfile .`
2. ✅ Test docker-compose setup: `docker-compose -f docker/docker-compose.yml up`
3. ✅ Verify volumes and persistence
4. ✅ Test on target architecture (ARM64 for Raspberry Pi)

### Production

1. ✅ Use Docker Compose (or Kubernetes)
2. ✅ Remove or don't deploy AppHost
3. ✅ Configure external telemetry (optional)
4. ✅ Set up proper backups for SQLite volume
5. ✅ Use secrets management (not .env files)

## Related Documentation

- [README.md](../../README.md) - Getting started
- [ADR-0001: Modular Monolith](../decisions/0001-modular-monolith.md)
- [ADR-0005: Project Structure](../decisions/0005-project-structure.md)
- [Deployment Planning](../planning/08-Deployment.md)
