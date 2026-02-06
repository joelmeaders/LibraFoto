# LibraFoto API — Architecture Overview

## High-Level Architecture

LibraFoto is a **modular monolith** built on .NET 10 with ASP.NET Core Minimal APIs. The API serves two frontend applications (Admin Angular SPA and Display vanilla TypeScript) through an Nginx reverse proxy in production.

```mermaid
graph TD
    subgraph Clients
        AdminSPA["Admin Frontend<br/>(Angular 21 + Material)"]
        DisplaySPA["Display Frontend<br/>(Vanilla TS, ~5KB)"]
    end

    subgraph Infrastructure
        Nginx["Nginx Reverse Proxy"]
        Aspire["Aspire AppHost<br/>(Dev Only)"]
    end

    subgraph "LibraFoto API (.NET 10)"
        API["LibraFoto.Api<br/>(Host / Entry Point)"]

        subgraph Modules
            Auth["Auth Module"]
            Admin["Admin Module"]
            Display["Display Module"]
            Media["Media Module"]
            Storage["Storage Module"]
        end

        subgraph "Shared Infrastructure"
            Data["LibraFoto.Data<br/>(EF Core + SQLite)"]
            Shared["LibraFoto.Shared<br/>(DTOs, Configuration)"]
            Defaults["ServiceDefaults<br/>(Aspire Telemetry)"]
        end
    end

    subgraph "External Services"
        GooglePhotos["Google Photos API"]
        Nominatim["OpenStreetMap Nominatim<br/>(Reverse Geocoding)"]
        GitHub["GitHub API<br/>(Update Checks)"]
    end

    subgraph "Persistence"
        SQLite["SQLite Database"]
        LocalFS["Local File System<br/>(Photos + Thumbnails)"]
        Cache["Local Disk Cache<br/>(Cloud File Cache)"]
    end

    AdminSPA -->|"/api/*"| Nginx
    DisplaySPA -->|"/api/*"| Nginx
    Nginx --> API
    Aspire -.->|"Dev orchestration"| API

    API --> Auth
    API --> Admin
    API --> Display
    API --> Media
    API --> Storage

    Auth --> Data
    Admin --> Data
    Admin --> Media
    Admin --> Storage
    Display --> Data
    Media --> Data
    Media --> Storage
    Storage --> Data

    Auth --> Shared
    Admin --> Shared
    Display --> Shared
    Media --> Shared
    Storage --> Shared

    API --> Defaults

    Storage --> GooglePhotos
    Media --> Nominatim
    Admin --> GitHub

    Data --> SQLite
    Storage --> LocalFS
    Storage --> Cache
    Media --> LocalFS
```

## Module Responsibilities

| Module      | Purpose                        | Key Capabilities                                                                                                                                       |
| ----------- | ------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Auth**    | Authentication & authorization | JWT login/logout, token refresh, user CRUD, role-based access (Admin/Editor/Guest), guest upload links, first-run setup                                |
| **Admin**   | Content management             | Photo CRUD, album management, tag management, bulk operations, system info & updates                                                                   |
| **Display** | Slideshow engine               | Photo sequencing with in-memory queue, display settings CRUD, QR code config, multiple display configurations                                          |
| **Media**   | Media processing               | Photo/video file serving, thumbnail generation (400×400 JPEG), EXIF metadata extraction, reverse geocoding, image processing (resize, rotate, convert) |
| **Storage** | Storage abstraction            | Multi-provider support (Local, Google Photos), file upload (single/batch/guest), sync engine, OAuth flows, Google Photos Picker, LRU disk cache        |

## Request Flow

```mermaid
sequenceDiagram
    participant Client
    participant Nginx
    participant API as LibraFoto.Api
    participant MW as Middleware
    participant Module as Module Endpoint
    participant Service as Module Service
    participant DB as SQLite (EF Core)
    participant FS as File System

    Client->>Nginx: HTTP Request /api/*
    Nginx->>API: Forward Request
    API->>MW: Exception Handler
    MW->>MW: Authentication (JWT)
    MW->>MW: Authorization
    MW->>Module: Route to Endpoint

    alt Read Operation
        Module->>Service: Call Service Method
        Service->>DB: EF Core Query
        DB-->>Service: Entity Data
        Service-->>Module: DTO Response
        Module-->>Client: 200 OK + JSON
    end

    alt File Serve
        Module->>Service: Get File
        Service->>DB: Lookup Photo Record
        Service->>FS: Open File Stream
        FS-->>Service: FileStream
        Service-->>Module: Stream
        Module-->>Client: 200 OK + File (Range Support)
    end

    alt Write Operation
        Module->>Module: Validate Request
        Module->>Service: Call Service Method
        Service->>DB: EF Core Mutation
        Service->>FS: Write/Delete Files
        Service-->>Module: Result DTO
        Module-->>Client: 200/201/204 + JSON
    end
```

## Startup Pipeline

```mermaid
flowchart TD
    Start([Application Start]) --> Serilog["Configure Serilog<br/>(Console + File Logging)"]
    Serilog --> EnvCheck{"LIBRAFOTO_DATA_DIR<br/>set?"}

    EnvCheck -->|Yes| SetPaths["Derive ConnectionString<br/>& Storage Path"]
    EnvCheck -->|No| Builder["Create WebApplication Builder"]
    SetPaths --> Builder

    Builder --> ServiceDefaults["AddServiceDefaults()<br/>(Aspire Telemetry, Health Checks)"]
    ServiceDefaults --> ExHandler["Add GlobalExceptionHandler"]
    ExHandler --> JWT["Configure JWT Authentication"]
    JWT --> RegisterModules["Register Modules:<br/>Data → Display → Admin<br/>→ Storage → Media → Auth"]
    RegisterModules --> Build["Build Application"]
    Build --> Migrate["Apply Database Migrations"]
    Migrate --> Middleware["UseExceptionHandler<br/>UseAuthentication<br/>UseAuthorization"]
    Middleware --> MapEndpoints["Map Endpoints:<br/>Aspire Defaults → Display<br/>→ Admin → Storage<br/>→ Media → Auth"]
    MapEndpoints --> TestEndpoints{"ENABLE_TEST_ENDPOINTS<br/>== true?"}
    TestEndpoints -->|Yes| MapTest["Map Test Endpoints"]
    TestEndpoints -->|No| Run
    MapTest --> Run([Run Application])
```

## Authentication & Authorization Model

```mermaid
flowchart LR
    subgraph Roles
        Guest["Guest (0)"]
        Editor["Editor (1)"]
        AdminRole["Admin (2)"]
    end

    subgraph Capabilities
        ViewOnly["View Photos"]
        GuestUpload["Upload via Guest Link"]
        ManageContent["Manage Photos/Albums/Tags"]
        ManageGuests["Manage Guest Links"]
        ManageUsers["Manage Users"]
        SystemAdmin["System Settings"]
    end

    Guest --> ViewOnly
    Guest --> GuestUpload
    Editor --> ViewOnly
    Editor --> GuestUpload
    Editor --> ManageContent
    Editor --> ManageGuests
    AdminRole --> ViewOnly
    AdminRole --> GuestUpload
    AdminRole --> ManageContent
    AdminRole --> ManageGuests
    AdminRole --> ManageUsers
    AdminRole --> SystemAdmin
```

## Technology Stack

| Layer                   | Technology                                 |
| ----------------------- | ------------------------------------------ |
| **Runtime**             | .NET 10                                    |
| **Web Framework**       | ASP.NET Core Minimal APIs                  |
| **Database**            | SQLite via EF Core 10                      |
| **Authentication**      | JWT Bearer (BCrypt password hashing)       |
| **Image Processing**    | SixLabors.ImageSharp                       |
| **Metadata Extraction** | MetadataExtractor                          |
| **Geocoding**           | OpenStreetMap Nominatim                    |
| **Logging**             | Serilog (Console + Rolling File)           |
| **Observability**       | .NET Aspire (OpenTelemetry, Health Checks) |
| **Unique IDs**          | NanoId (for guest links)                   |
| **Google Integration**  | Google.Apis.Auth (OAuth 2.0)               |
| **Admin Frontend**      | Angular 21 + Angular Material              |
| **Display Frontend**    | Vanilla TypeScript (Vite)                  |
| **Reverse Proxy**       | Nginx                                      |
| **Containerization**    | Docker + Docker Compose                    |
