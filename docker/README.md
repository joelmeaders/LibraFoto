# Docker assets

This folder contains all Docker-related assets used for container builds and runtime configuration.

## What lives here

- `Dockerfile.api` — Builds the .NET API image.
- `Dockerfile.admin` — Builds the Admin UI image.
- `Dockerfile.display` — Builds the Display UI image.
- `Dockerfile.proxy` — Builds the Nginx reverse proxy image.
- `docker-compose.yml` — Production-style compose file wiring the API, UIs, and proxy together.
- `docker-compose.dev.yml` — Dev overlay for the API build stage and dev-friendly settings.
- `nginx*.conf` — Nginx configs for proxying `/api`, `/admin`, and `/display`.

## How they’re used

- Compose builds all images from the repository root, referencing Dockerfiles in this folder.
- The proxy image serves UI static assets and routes requests to internal API/UI containers.
- The API image exposes port 8080 internally and is only reachable via the proxy.

## Quick usage

From the `docker/` directory:

- Build and run: `docker compose up -d --build`
- View logs: `docker compose logs -f`
- Stop: `docker compose down`
