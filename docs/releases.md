# LibraFoto Release Process

## Overview

Releases are triggered by the `.version` file:
- **Stable releases**: Push stable version (e.g., `1.2.0`) to `main` branch
- **Prereleases**: Push version tag (e.g., `v1.2.0-alpha.1`) to any branch

Workflow: Validates version → Builds Docker images (amd64/arm64) → Creates GitHub Release with platform-specific zips.

## Version Management

Single source of truth: `.version` file at repository root.

**Format**: `MAJOR.MINOR.PATCH[-{alpha|beta|rc}.NUMBER]`
- Stable: `1.2.0` (main branch only)
- Prerelease: `1.2.0-alpha.1`, `1.2.0-beta.2`, `1.2.0-rc.1` (feature branches)

**CI enforcement**:
- `main` branch: Fails if version has prerelease suffix
- Feature branches: Fails if version is stable

## CI/CD Workflows

**CI (`.github/workflows/ci.yml`)**: Runs on all pushes/PRs — validates version, builds/tests all components with ≥80% coverage requirement.

**Docker Build (`.github/workflows/docker-build.yml`)**: Validates image builds for amd64/arm64 — does not push images.

**Release (`.github/workflows/release.yml`)**:
1. **Validate**: Checks version format and branch requirements
2. **Build Images**: Parallel builds for amd64/arm64, saves as .tar files
3. **Create Release**: Packages zips with images/scripts, generates changelog, publishes to GitHub

## Creating a Release

### Stable Release

```bash
git checkout main && git pull
echo "1.2.0" > .version
git add .version && git commit -m "Release 1.2.0"
git push origin main  # Triggers release automatically
```

### Prerelease

```bash
echo "1.2.0-alpha.1" > .version
git add .version && git commit -m "Prepare prerelease 1.2.0-alpha.1"
git push

# Tag must match .version exactly (with 'v' prefix)
git tag -a "v$(cat .version)" -m "Prerelease $(cat .version)"
git push origin "v$(cat .version)"
```

Monitor workflow in Actions tab (~15-25 minutes for builds).

## Release Package Contents

Each release includes two platform-specific zips: `librafoto-v{VERSION}-{amd64|arm64}.zip`

Contents:
- `images/*.tar` — Pre-built Docker images (load with `docker load < file.tar`)
- `docker/docker-compose.release.yml` — Production compose file
- `install.sh`, `update.sh`, `uninstall.sh` — Deployment scripts
- `scripts/` — Helper scripts (kiosk setup, IP updates, etc.)
- `.version` — Version file

## Troubleshooting

**Common issues**:

| Issue | Fix |
|-------|-----|
| Tag version ≠ `.version` file | Update `.version` to match tag or recreate tag |
| Tag already exists | Delete: `git tag -d v1.2.0-alpha.1 && git push origin --delete v1.2.0-alpha.1` |
| Prerelease on `main` | Ensure `main` uses stable version only |
| Stable release not triggering | Confirm `.version` has no prerelease suffix |
| Build failure | Check Actions logs, fix issue, push correction |
| Corrupted zip | Re-download or delete release and retrigger |
| Wrong architecture | `uname -m` — download matching zip (amd64/x86_64 or arm64/aarch64) |

**Verify images**: `tar -tzf images/librafoto-api.tar && docker load < images/librafoto-api.tar`

## Version Incrementing

| Change | Version Bump | Example |
|--------|--------------|---------|
| Bug fixes | Patch | `1.2.0` → `1.2.1` |
| New features | Minor | `1.2.0` → `1.3.0` |
| Breaking changes | Major | `1.2.0` → `2.0.0` |

**Prerelease flow**: `1.0.0-alpha.1` → `alpha.2` → `beta.1` → `rc.1` → `1.0.0`

## Integration with Update Script

`update.sh` checks GitHub API for latest releases, downloads correct architecture, loads images, and restarts services. Respects semantic versioning and filters prereleases by default.
