# LibraFoto

[![Conventional Commits](https://img.shields.io/badge/Conventional%20Commits-1.0.0-%23FE5196?logo=conventionalcommits&logoColor=white)](https://conventionalcommits.org)

A self-hosted digital picture frame application for Raspberry Pi and similar devices. Transform any display into a beautiful slideshow showcasing your memories from local storage or cloud services (Google Photos/Drive, OneDrive).

## Key Features

- **Beautiful Slideshow Display** - Fullscreen photo/video display with configurable transitions and overlays (date/time/location)
- **Multi-Source Storage** - Local uploads, Google Photos, Google Drive, and OneDrive (planned)
- **Web-Based Admin** - Manage photos, albums, tags, and settings from any device
- **Multi-User Auth** - Admin, Editor, and Guest roles with secure guest upload links
- **Raspberry Pi Optimized** - Minimal resource usage and efficient performance
- **Docker Deployment** - Single-command setup with Docker Compose

---

## Installation

LibraFoto offers two installation methods:

- **Release Zip** (recommended): Pre-built Docker images, no compilation needed, faster deployment
- **Clone & Build**: Build from source if you want the latest development code or need to customize

### Raspberry Pi (Recommended)

**Option 1: Release Zip (Recommended)**

Download pre-built release for fastest deployment:

```bash
# Download the latest release from:
# https://github.com/librafoto/librafoto/releases
# Choose: librafoto-vX.X.X-arm64.zip (Raspberry Pi 4/5)

unzip librafoto-v*-arm64.zip
cd librafoto
chmod +x install.sh
sudo bash install.sh
```

Includes pre-built Docker images — no compilation or build tools required.

**Option 2: Clone & Build**

For latest development code or customization:

```bash
git clone https://github.com/librafoto/librafoto
cd librafoto
chmod +x install.sh
sudo bash install.sh
```

Requires Docker and build tools. Images will be compiled locally.

---

The installer will ask if you want to enable **kiosk mode** (fullscreen slideshow on boot). You can also configure kiosk mode later:

```bash
sudo bash scripts/kiosk-setup.sh           # Enable kiosk mode
sudo bash scripts/kiosk-setup.sh --status  # Check kiosk status
sudo bash scripts/kiosk-setup.sh --uninstall  # Disable kiosk mode
```

> **Note**: Always use `bash` or `./script.sh` to run scripts. Using `sh` will fail on Debian-based systems due to bash-specific syntax.

### Docker (Any Platform)

**Option 1: Release Zip (No Build Required)**

```bash
# Download release from: https://github.com/librafoto/librafoto/releases
# Choose architecture:
#   - librafoto-vX.X.X-amd64.zip (Intel/AMD processors)
#   - librafoto-vX.X.X-arm64.zip (ARM processors)

unzip librafoto-v*-{amd64|arm64}.zip
cd librafoto/docker
docker compose -f docker-compose.release.yml up -d
```

**Option 2: Clone & Build From Source**

```bash
git clone https://github.com/librafoto/librafoto
cd librafoto/docker
docker compose up -d
```

Access at: http://localhost/display/ (slideshow) and http://localhost/admin/ (management)

## Updating

Check for and apply updates:

```bash
sudo bash update.sh          # Interactive update with backup
sudo bash update.sh --check  # Check for updates without installing
sudo bash update.sh --help   # View all options
```

The update script automatically backs up your data, pulls changes, rebuilds containers, and can rollback on failure.

> **Note**: Use `bash update.sh` not `sh update.sh` — the scripts require bash.

## Uninstalling

Remove LibraFoto while preserving your photos and database:

```bash
chmod +x uninstall.sh
sudo bash uninstall.sh          # Interactive uninstall
sudo bash uninstall.sh --help   # View help
```

The uninstall script is fully interactive and will guide you through the process, asking whether to:

- Remove Docker images (free up ~500MB disk space)
- Remove Docker volumes (deletes database, not photo files)
- Remove data directories (permanently deletes photos and backups)

By default, all data is preserved. To completely remove LibraFoto after uninstalling:

```bash
rm -rf ~/LibraFoto  # Adjust path as needed
```

## Development

### Quick Start

```bash
aspire run  # Starts API + frontends + Aspire dashboard
```

### Architecture

**Modular monolith** in .NET 10 - single deployable unit with clear module boundaries. SQLite database for zero-config portability. Vanilla TypeScript display frontend, Angular 21 admin frontend, both served via Nginx.

### Testing

- **Unit**: TUnit (backend), Vitest (frontends)
- **E2E**: Playwright - see [tests/e2e/README.md](tests/e2e/README.md)
- **Shell**: shUnit2 tests for install/update/uninstall and scripts in `tests/shell`

Run shell tests:

```bash
bash tests/shell/run-tests.sh
```

shUnit2 must be installed and discoverable (e.g., `shunit2` on PATH or `SHUNIT2_PATH` set).

On Windows, run the shell tests inside WSL (Ubuntu recommended):

```bash
wsl -d Ubuntu -- bash -lc "cd '/mnt/d/Just or fun/LibraFoto/LibraFoto' && bash tests/shell/run-tests.sh"
```

```bash
dotnet test apps/api/LibraFoto.slnx      # Backend tests
cd apps/admin && npm test  # Admin frontend tests
cd apps/display && npm test  # Display frontend tests
```

### Documentation

- [Contributing Guide](CONTRIBUTING.md) - Workflow, standards, and release process
- [Conventional Commits Guide](docs/conventions/conventional-commits.md) - Commit message format and guidelines
- [Aspire & Docker Guide](docs/development/aspire-and-docker.md) - Development workflows
- [Copilot Instructions](.github/copilot-instructions.md) - Complete technical guide for contributors

---

_LibraFoto - Your memories, beautifully displayed._
