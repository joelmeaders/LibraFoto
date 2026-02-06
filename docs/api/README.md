# LibraFoto API Documentation

Architecture documentation for the LibraFoto API — a modular monolith built on .NET 10.

## Documents

| Document                                          | Description                                                                      |
| ------------------------------------------------- | -------------------------------------------------------------------------------- |
| [Architecture Overview](architecture-overview.md) | High-level system architecture, request flow, startup pipeline, technology stack |
| [Module Dependencies](module-dependencies.md)     | Project dependency graph, NuGet packages, service registration & lifetimes       |
| [Data Model](data-model.md)                       | Entity relationship diagram, enumerations, delete behavior map                   |
| [Endpoint Map](endpoint-map.md)                   | Complete list of all 100 API endpoints organized by module                       |
| [Module Details](module-details.md)               | Per-module service documentation, flow diagrams, state machines                  |
| [Storage Architecture](storage-architecture.md)   | Storage provider pattern, file flows, sync engine, cache architecture            |

## Quick Stats

- **5 modules**: Auth, Admin, Display, Media, Storage
- **100 endpoints** across all modules
- **11 database entities** in SQLite via EF Core
- **2 storage providers**: Local filesystem, Google Photos
- **4 external integrations**: Google Photos API, OpenStreetMap Nominatim, GitHub (updates), Aspire (telemetry)
