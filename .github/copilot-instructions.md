# LibraFoto - Copilot Instructions

## Agent Workflow Requirements

**Use sub-agents for:**

- Researching unfamiliar areas of the codebase before making changes
- Complex multi-file refactoring or feature implementation
- Running and analyzing test results
- Code review for accuracy, quality, and optimization

**Direct implementation (no sub-agent needed):**

- Simple single-file edits with clear patterns already visible
- Bug fixes with obvious root cause
- Adding new endpoints/services following existing module patterns
- Documentation updates

**Workflow checklist:**

1. Research → Understand existing patterns before coding
2. Implement → Follow module/endpoint patterns below
3. Test → Run `dotnet test apps/api/LibraFoto.slnx` and fix failures
4. Build → Verify `dotnet build apps/api/LibraFoto.slnx` succeeds

## Version Management

**Single source of truth**: `.version` file at repository root.

**Format**:

- Stable: `1.2.0` (main branch only)
- Prerelease: `1.2.0-alpha.1`, `1.2.0-beta.2`, `1.2.0-rc.1` (feature branches only)

**CI Enforcement**:

- Main branch: Fails if `.version` has prerelease suffix
- Feature branches: Fails if `.version` is stable
- Check locally: `cat .version`

**Starting a feature**:

```bash
git checkout -b feature/xyz
echo "1.3.0-alpha.1" > .version
git add .version && git commit -m "Start 1.3.0 development"
```

**Before merging to main**: Update `.version` to stable (e.g., `1.3.0`) and commit.

## Architecture Overview

**Modular monolith** in .NET 10 with clear module boundaries:

```
apps/api/
├── LibraFoto.Api/              # Host: Program.cs startup, global middleware
├── LibraFoto.Data/             # EF Core + SQLite, migrations, entities
├── LibraFoto.Shared/           # DTOs: PagedResult<T>, ApiError, PaginationInfo
├── LibraFoto.ServiceDefaults/  # Aspire: AddServiceDefaults(), MapDefaultEndpoints()
└── LibraFoto.Modules.*/        # Each module: Endpoints/, Services/, Models/
    ├── Admin/               # Photo/album/tag management
    ├── Auth/                # JWT auth, users, guest links
    ├── Display/             # Slideshow, display settings
    ├── Media/               # Thumbnails, metadata, ImageSharp processing
    └── Storage/             # IStorageProvider implementations (Local, GooglePhotos)
```

**Frontends**: Display = Vite/TS (`apps/display`), Admin = Angular 21 + Material (`apps/admin`). Nginx proxies `/api/*` in production.

## Development Workflows

### Local Development (Aspire)

```bash
# Start full stack (API + frontends + Aspire dashboard)
dotnet run --project apps/api/LibraFoto.AppHost

# Build solution
dotnet build apps/api/LibraFoto.slnx

# Run migrations
dotnet ef database update --project apps/api/LibraFoto.Data --startup-project apps/api/LibraFoto.Api

# Frontend dev servers (if running independently)
cd apps/display && npm run dev   # Port 3000
cd apps/admin && npm start       # Port 4200
```

**Important**: AppHost is **dev-only**. It uses Aspire for service orchestration, telemetry, and health checks. Never use in production.

### Production Deployment (Docker)

Production uses Docker Compose with two **deploy modes** auto-detected by install/update scripts:

**1. Build mode** (clone from GitHub):

- Uses `docker/docker-compose.yml`
- Builds images locally from source
- Requires Docker, git, and build tools
- Slower initial setup, suitable for development deployments

**2. Release mode** (download release zip):

- Uses `docker/docker-compose.release.yml`
- Loads pre-built images from `images/*.tar` files
- No compilation needed, faster deployment
- Suitable for Raspberry Pi and production

**Detection logic** (see `scripts/common.sh`):

```bash
# Auto-detect: checks if images/*.tar files exist
get_deploy_mode()  # Returns "build" or "release"
get_compose_file() # Returns docker-compose.yml or docker-compose.release.yml
```

**Manual deployment**:

```bash
cd docker
docker compose -f docker-compose.yml up -d  # Build mode
# OR
docker compose -f docker-compose.release.yml up -d  # Release mode
```

## Module Pattern

Each module follows this structure (see [AdminModule.cs](apps/api/LibraFoto.Modules.Admin/AdminModule.cs)):

```csharp
// Registration: Add{ModuleName}Module
public static IServiceCollection AddAdminModule(this IServiceCollection services)
{
    services.AddScoped<IPhotoService, PhotoService>();
    // ...
    return services;
}

// Endpoint mapping: Map{ModuleName}Endpoints
public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
{
    app.MapGroup("/api/admin").MapPhotoEndpoints().MapAlbumEndpoints();
    return app;
}
```

In `Program.cs`: call `builder.AddServiceDefaults()` before `Build()`, then `app.MapDefaultEndpoints()` after. Register modules with `builder.Services.Add{X}Module()` and map with `app.Map{X}Endpoints()`.

## API Endpoint Pattern

See [PhotoEndpoints.cs](apps/api/LibraFoto.Modules.Admin/Endpoints/PhotoEndpoints.cs):

```csharp
var group = app.MapGroup("/photos").WithTags("Photos");

group.MapGet("/", GetPhotos).WithName("GetPhotos");

private static async Task<Ok<PagedResult<PhotoListDto>>> GetPhotos(
    IPhotoService photoService, int page = 1, int pageSize = 50, ...)
{
    var result = await photoService.GetPhotosAsync(filter, ct);
    return TypedResults.Ok(result);
}

// For errors, use Results<Ok<T>, NotFound<ApiError>> union types
private static async Task<Results<Ok<PhotoDetailDto>, NotFound>> GetPhotoById(...)
```

Use `TypedResults.Ok(...)`, `TypedResults.NotFound()`, `TypedResults.NoContent()`. Wrap paginated responses in `PagedResult<T>` with `PaginationInfo`.

## Storage Provider Pattern

Implement [IStorageProvider](apps/api/LibraFoto.Modules.Storage/Interfaces/IStorageProvider.cs) for new storage backends:

```csharp
public interface IStorageProvider
{
    long ProviderId { get; }
    StorageProviderType ProviderType { get; }
    string DisplayName { get; }
    bool SupportsUpload { get; }
    void Initialize(long providerId, string displayName, string? configuration);
    Task<IEnumerable<StorageFileInfo>> GetFilesAsync(string? folderId, CancellationToken ct);
    Task<byte[]> DownloadFileAsync(string fileId, CancellationToken ct);
    Task<Stream> GetFileStreamAsync(string fileId, CancellationToken ct);
    // ...
}
```

See [LocalStorageProvider.cs](apps/api/LibraFoto.Modules.Storage/Providers/LocalStorageProvider.cs) and [GooglePhotosProvider.cs](apps/api/LibraFoto.Modules.Storage/Providers/GooglePhotosProvider.cs) for implementations.

## Testing

```bash
# Backend tests (TUnit)
dotnet test apps/api/LibraFoto.slnx

# Frontend unit tests
cd apps/display && npm test   # Vitest
cd apps/admin && npm test     # Angular test runner

# E2E tests (Playwright) - starts API automatically
cd tests/e2e && npm test

# E2E against manually-started API
$env:ENABLE_TEST_ENDPOINTS="true"; dotnet run --project apps/api/LibraFoto.Api
$env:SKIP_WEB_SERVER="true"; cd tests/e2e && npm test
```

Backend tests use TUnit with NSubstitute for mocking. Use in-memory SQLite for database tests:

```csharp
var options = new DbContextOptionsBuilder<LibraFotoDbContext>()
    .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}").Options;
await using var dbContext = new LibraFotoDbContext(options);
```

## Production Deployment

```bash
# Build and run with Docker Compose
docker-compose -f docker/docker-compose.yml up -d
```

AppHost is **dev-only**. Production uses Docker DNS for service discovery. See [aspire-and-docker.md](docs/development/aspire-and-docker.md).

## Shell Script Patterns

LibraFoto includes sophisticated shell scripts for installation, updates, and uninstallation. Key patterns:

**Common helpers** (`scripts/common.sh`):

```bash
# Always source from your script
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/scripts/common.sh"

# Use provided functions
log_info "Starting operation"    # Blue [INFO] + file logging
log_success "Operation complete" # Green [OK] + file logging
log_warn "Warning message"       # Yellow [WARN] + file logging
log_error "Error message"        # Red [ERROR] to stderr + file logging

# Deploy mode detection
mode=$(get_deploy_mode "$LIBRAFOTO_DIR")  # Returns "build" or "release"
compose_file=$(get_compose_file "$LIBRAFOTO_DIR")  # Returns correct compose file path
```

**Interactive-first design** (see `install.sh`, `update.sh`, `uninstall.sh`):

- Parse only `--help`/`-h` flag, show help and exit
- All behavior is interactive with clear prompts
- Use `N` (no) as default for destructive operations
- Show dry-run preview before asking for confirmation

**Error tracking** (for multi-step operations):

```bash
# Initialize tracking
declare -A operation_status
track_operation "operation_name" "success"  # or "failed"
show_operation_summary  # Print table of all tracked operations
```

**Testing with shUnit2**:

- All scripts have tests in `tests/shell/`
- Run with `bash tests/shell/run-tests.sh`
- Must run in bash, not sh (uses bash-specific syntax)
- On Windows: run inside WSL

Example test structure:

```bash
# tests/shell/common_test.sh
testGetDeployMode() {
    result=$(get_deploy_mode "/tmp/test-dir")
    assertEquals "build" "$result"
}
```

## Coding Standards

- Build after changes: `dotnet build apps/api/LibraFoto.slnx`
- Write unit tests for new functionality—test behavior, not framework code.
- Run tests after changes and fix any failures.
- Use records for DTOs (see `LibraFoto.Shared/DTOs/`).
- Follow existing module structure when adding new features.
- Use `CancellationToken` in async methods for proper cancellation support.

## Frontend Patterns

### Admin Frontend (Angular 21)

**Structure**: Standalone components with Angular Material, lazy-loaded routes.

```typescript
// Services extend ApiService for consistent error handling
@Injectable({ providedIn: 'root' })
export class PhotoService extends ApiService {
  getPhotos(params: PhotoFilterRequest): Observable<PagedResult<PhotoListDto>> {
    return this.get<PagedResult<PhotoListDto>>('/api/admin/photos', params);
  }
}

// Components use signals and inject()
@Component({ standalone: true, imports: [...] })
export class PhotoListComponent {
  private readonly photoService = inject(PhotoService);
  photos = signal<PhotoListDto[]>([]);
}
```

Key files: [api.service.ts](apps/admin/src/app/core/services/api.service.ts), [app.config.ts](apps/admin/src/app/app.config.ts)

### Display Frontend (Vanilla TypeScript)

Minimal footprint (~5KB bundle) for Raspberry Pi. Uses class-based architecture:

```typescript
// Component classes with clear lifecycle
export class Slideshow {
  constructor(private apiClient: ApiClient) {}
  async start(): Promise<void> { ... }
  pause(): void { ... }
  resume(): void { ... }
}
```

Key files: [main.ts](apps/display/src/main.ts), [slideshow.ts](apps/display/src/slideshow.ts), [api-client.ts](apps/display/src/api-client.ts)

## Environment Variables

| Variable                         | Purpose                            | Default                           |
| -------------------------------- | ---------------------------------- | --------------------------------- |
| `LIBRAFOTO_DATA_DIR`             | Sets both database and photos path | Platform-specific                 |
| `ConnectionStrings__LibraFotoDb` | SQLite connection string           | `LIBRAFOTO_DATA_DIR/librafoto.db` |
| `Storage__LocalPath`             | Photo storage directory            | `LIBRAFOTO_DATA_DIR/photos`       |
| `Jwt__Key`                       | JWT signing key (min 32 chars)     | Default dev key                   |
| `ENABLE_TEST_ENDPOINTS`          | Enable `/api/test/*` for E2E       | `false`                           |
| `OTEL_EXPORTER_OTLP_ENDPOINT`    | OpenTelemetry endpoint             | None (disabled)                   |

Docker example:

```yaml
environment:
  - LIBRAFOTO_DATA_DIR=/data
  - Jwt__Key=${JWT_KEY:-change-this-secret-key-in-production}
```

**Data directory resolution** (see `LibraFotoDefaults.cs`):

- Docker: `./data` (detected by `/app` or `/data` existence)
- Linux: `~/.local/share/LibraFoto`
- Windows: `%LOCALAPPDATA%\LibraFoto`
- macOS: `~/Library/Application Support/LibraFoto`

The API automatically creates data/photos subdirectories and expands `LIBRAFOTO_DATA_DIR` to set both database and storage paths at startup (see `Program.cs` lines 33-50).
