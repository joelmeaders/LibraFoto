# Contributing to LibraFoto

Thank you for your interest in contributing to LibraFoto! This guide covers the development workflow, coding standards, and release process.

## Table of Contents

- [Development Setup](#development-setup)
- [Version Management](#version-management)
- [Branch Strategy](#branch-strategy)
- [CI Requirements](#ci-requirements)
- [Coding Standards](#coding-standards)
- [Pull Request Process](#pull-request-process)
- [Release Process](#release-process)

---

## Development Setup

### Prerequisites

- .NET 10 SDK
- Node.js 22+
- Docker (for container testing)

### Quick Start

```bash
# Clone and start full stack
git clone https://github.com/librafoto/librafoto
cd librafoto
dotnet run --project src/LibraFoto.AppHost  # Starts API + frontends + Aspire dashboard
```

### Running Tests

```bash
# Backend tests (TUnit)
dotnet test LibraFoto.slnx

# Frontend tests (Vitest)
cd frontends/admin && npm test
cd frontends/display && npm test

# E2E tests (Playwright)
cd frontends/e2e && npm test
```

---

## Version Management

LibraFoto uses a `.version` file in the repository root to track the current version. This file is the single source of truth for versioning.

### Version Format

| Type   | Pattern         | Example         | Usage                         |
| ------ | --------------- | --------------- | ----------------------------- |
| Stable | `#.#.#`         | `1.2.0`         | Production releases on `main` |
| Alpha  | `#.#.#-alpha.#` | `1.2.0-alpha.1` | Early development/testing     |
| Beta   | `#.#.#-beta.#`  | `1.2.0-beta.2`  | Feature-complete testing      |
| RC     | `#.#.#-rc.#`    | `1.2.0-rc.1`    | Release candidates            |

### Branch Version Rules

| Branch     | Allowed Versions                  | CI Enforcement         |
| ---------- | --------------------------------- | ---------------------- |
| `main`     | Stable only (`1.2.0`)             | ✅ Fails on prerelease |
| Feature/PR | Prerelease only (`1.2.0-alpha.1`) | ✅ Fails on stable     |

---

## Branch Strategy

```
main (stable versions only)
  │
  ├── feature/xyz (prerelease versions)
  │     └── PR → main
  │
  └── Tags
        ├── v1.2.0-alpha.1 → Docker images only
        ├── v1.2.0-beta.1  → Docker images only
        └── v1.2.0         → Docker images + GitHub Release
```

### Creating a Feature Branch

```bash
git checkout main
git pull
git checkout -b feature/my-feature

# Set prerelease version
echo "1.3.0-alpha.1" > .version
git add .version
git commit -m "Start 1.3.0 development"
```

---

## CI Requirements

Every push and pull request triggers CI validation. **All checks must pass** before merging.

### 1. Version Validation

- Main branch: Must have stable version (`1.2.0`)
- Feature branches: Must have prerelease version (`1.2.0-alpha.1`)

### 2. Code Formatting

```bash
dotnet format --solution LibraFoto.slnx --verify-no-changes
```

Run locally before pushing:

```bash
dotnet format --solution LibraFoto.slnx
```

### 3. Build

All projects must compile without errors:

```bash
dotnet build LibraFoto.slnx --configuration Release
```

### 4. Test Coverage (80% Minimum)

All three components require **≥80% line coverage**:

| Component        | Coverage Tool | Threshold                                  |
| ---------------- | ------------- | ------------------------------------------ |
| Backend (.NET)   | Cobertura     | 80% lines                                  |
| Admin Frontend   | Vitest v8     | 80% lines, functions, branches, statements |
| Display Frontend | Vitest v8     | 80% lines, functions, branches, statements |

Run coverage locally:

```bash
# Backend
dotnet test LibraFoto.slnx --collect:"XPlat Code Coverage"

# Admin frontend
cd frontends/admin && npm run test:coverage

# Display frontend
cd frontends/display && npm run test:coverage
```

---

## Coding Standards

### Backend (.NET)

- **Formatting**: Enforced via `dotnet format` (EditorConfig)
- **DTOs**: Use records (see `LibraFoto.Shared/DTOs/`)
- **Async**: Include `CancellationToken` in async methods
- **Endpoints**: Use `TypedResults` for OpenAPI documentation
- **Testing**: TUnit with NSubstitute for mocking

### Frontend (TypeScript)

- **Admin**: Angular 21 with standalone components, signals, Material
- **Display**: Vanilla TypeScript with class-based architecture
- **Testing**: Vitest with jsdom environment

### Module Structure

Each backend module follows this pattern:

```
LibraFoto.Modules.{Name}/
├── {Name}Module.cs      # Add{Name}Module() + Map{Name}Endpoints()
├── Endpoints/           # API endpoint definitions
├── Services/            # Business logic
└── Models/              # Module-specific DTOs
```

---

## Pull Request Process

### Before Submitting

1. **Version**: Ensure `.version` has a prerelease suffix
2. **Format**: Run `dotnet format --solution LibraFoto.slnx`
3. **Build**: Run `dotnet build LibraFoto.slnx`
4. **Tests**: Run `dotnet test LibraFoto.slnx` and frontend tests
5. **Coverage**: Verify ≥80% coverage on new code

### PR Checklist

- [ ] Version set to prerelease (e.g., `1.3.0-alpha.1`)
- [ ] Code formatted (`dotnet format`)
- [ ] All tests passing
- [ ] Coverage meets 80% threshold
- [ ] Descriptive PR title and description
- [ ] Documentation updated (if applicable)

### Review Process

1. CI must pass all checks
2. At least one approval required
3. Squash merge to main
4. Update `.version` to stable before/after merge

---

## Release Process

### Pre-release (Testing)

Tag a prerelease to build Docker images for testing:

```bash
# Ensure .version matches
echo "1.3.0-alpha.1" > .version
git add .version && git commit -m "Prepare 1.3.0-alpha.1"
git push

# Create tag
git tag v1.3.0-alpha.1
git push --tags
```

**Result**: Docker images tagged `1.3.0-alpha.1` (no GitHub Release)

### Stable Release

1. **Update version to stable** on main:

   ```bash
   git checkout main
   git pull
   echo "1.3.0" > .version
   git add .version && git commit -m "Release 1.3.0"
   git push
   ```

2. **Create release tag**:

   ```bash
   git tag v1.3.0
   git push --tags
   ```

**Result**:

- Docker images: `1.3.0`, `1.3`, `1`, `latest`
- GitHub Release created with:
  - `docker-compose.yml`
  - `install.sh`
  - `update.sh`
  - `.env.template`

### Docker Image Tags

| Tag              | Description                           |
| ---------------- | ------------------------------------- |
| `v1.2.0-alpha.1` | Pre-release: `1.2.0-alpha.1` only     |
| `v1.2.0`         | Stable: `1.2.0`, `1.2`, `1`, `latest` |
| Push to `main`   | Branch: `main`                        |

---

## Getting Help

- **Technical Guide**: [.github/copilot-instructions.md](.github/copilot-instructions.md)
- **Architecture**: [docs/development/aspire-and-docker.md](docs/development/aspire-and-docker.md)
- **Issues**: [GitHub Issues](https://github.com/librafoto/librafoto/issues)

---

_Thank you for contributing to LibraFoto!_
