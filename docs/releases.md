# LibraFoto Release Process

This document explains how LibraFoto handles releases through GitHub Actions, including version management, Docker image building, and release packaging.

## Table of Contents

- [Overview](#overview)
- [Version Management](#version-management)
- [Release Types](#release-types)
- [CI/CD Workflows](#cicd-workflows)
- [Creating a Release](#creating-a-release)
- [Release Package Contents](#release-package-contents)
- [Troubleshooting](#troubleshooting)

---

## Overview

LibraFoto uses an **automated release system** triggered by changes to the `.version` file:

- **Stable releases** are triggered by pushing a stable version (e.g., `1.2.0`) to the `main` branch
- **Prereleases** are triggered by pushing version tags (e.g., `v1.2.0-alpha.1`) to any branch

The release process:

1. **Validates** the version format and branch requirements
2. **Builds** Docker images for both amd64 and arm64 architectures
3. **Packages** platform-specific release zips with images, scripts, and compose files
4. **Creates** a GitHub Release with auto-generated changelog and installation instructions

All releases are self-contained zip files that users can download and install without needing to build from source.

---

## Version Management

### Single Source of Truth

LibraFoto uses a `.version` file in the repository root as the authoritative version source. This file must match any version tag you create.

**Current version:**
```bash
cat .version
# Example: 0.1.0-alpha.1
```

### Version Format

LibraFoto follows semantic versioning with prerelease support:

| Type   | Pattern         | Example         | Description                |
|--------|-----------------|-----------------|----------------------------|
| Stable | `#.#.#`         | `1.2.0`         | Production-ready releases  |
| Alpha  | `#.#.#-alpha.#` | `1.2.0-alpha.1` | Early development/testing  |
| Beta   | `#.#.#-beta.#`  | `1.2.0-beta.2`  | Feature-complete testing   |
| RC     | `#.#.#-rc.#`    | `1.2.0-rc.1`    | Release candidates         |

### Branch vs Version Rules

The CI system enforces version conventions based on the branch:

| Branch         | Required Version Type | CI Behavior               |
|----------------|----------------------|---------------------------|
| `main`         | Stable (`1.2.0`)     | ✅ Passes, ❌ Fails prerelease |
| Feature/PR     | Prerelease           | ✅ Passes prerelease, ❌ Fails stable |

This ensures stable versions only exist on `main`, and all development branches use prerelease versions to prevent version conflicts.

---

## Release Types

### Prerelease (Testing)

**Purpose:** Build Docker images for internal testing before stable release.

**Trigger:** Push a version tag with prerelease suffix to any branch

**Characteristics:** 
- Triggered by prerelease tags: `v1.2.0-alpha.1`, `v1.2.0-beta.2`, `v1.2.0-rc.1`
- Tag must match the version in `.version` file
- Builds Docker images tagged with exact version only
- Creates a GitHub Release marked as **prerelease**
- Package includes same contents as stable releases
- Useful for QA testing before stable release

**When to use:**
- Testing new features before production
- Beta testing with select users
- Release candidate validation

### Stable Release

**Purpose:** Create production-ready release packages for public distribution.

**Trigger:** Push a stable version to the `main` branch

**Characteristics:**
- Triggered by pushing commits to `main` with stable `.version` (e.g., `1.2.0`)
- **No tag required** - the workflow detects stable versions automatically
- Builds Docker images with the version as tag
- Creates a GitHub Release with downloadable platform-specific zips
- Generates automatic changelog from commits since previous release
- Includes installation instructions in release notes

**When to use:**
- Production-ready releases
- Major or minor version milestones
- Bug fix releases for end users

---

## CI/CD Workflows

LibraFoto uses three primary GitHub Actions workflows:

### 1. CI Workflow (`.github/workflows/ci.yml`)

**Trigger:** Every push to `main` and all pull requests

**Purpose:** Continuous integration validation

**Jobs:**
- **Version Validation** - Enforces branch version rules
- **Backend Build** - Compiles .NET solution with format checking
- **Backend Tests** - Runs unit tests with ≥80% coverage requirement
- **Shell Tests** - Validates bash scripts with shUnit2
- **Admin Frontend** - Builds and tests Angular app (≥80% coverage)
- **Display Frontend** - Builds and tests Vite app (≥80% coverage)

**Coverage Requirements:**
All components must maintain minimum 80% code coverage (lines, functions, branches, statements).

### 2. Docker Build Workflow (`.github/workflows/docker-build.yml`)

**Trigger:** Pushes to `main` and manual workflow dispatch

**Purpose:** Validate Docker image builds for both architectures

**What it does:**
- Validates all four Docker images can build successfully
- Tests both `linux/amd64` and `linux/arm64` platforms
- Does **NOT** push images or create releases
- Uses GitHub Actions cache to speed up builds

**Images validated:**
- `librafoto-api` - .NET 10 backend API
- `librafoto-display-ui` - Vite-built display frontend
- `librafoto-admin-ui` - Angular admin frontend  
- `librafoto-proxy` - Nginx reverse proxy

### 3. Release Workflow (`.github/workflows/release.yml`)

**Trigger:**
- **Stable releases:** Pushes to `main` branch with stable version in `.version`
- **Prereleases:** Tags matching `v*.*.*-{alpha|beta|rc}.*` (e.g., `v1.2.0-alpha.1`)

**Purpose:** Build and package complete releases

**Jobs:**

#### 1. Validate (`validate`)
- Reads `.version` file
- For tag-based triggers: Verifies tag version matches file version exactly
- For push-based triggers: Validates version is stable format for `main` branch
- Detects prerelease status based on version pattern
- **Fails fast** if validation fails

#### 2. Build Images (`build-images`)
- Runs in parallel for `amd64` and `arm64` architectures
- Uses QEMU for cross-platform builds
- Builds all four Docker images per architecture
- Saves images as `.tar` files (not pushed to registry)
- Uploads tar files as artifacts for next job
- Uses GitHub Actions cache for efficient rebuilds

**Strategy:**
```yaml
matrix:
  arch: [amd64, arm64]
  include:
    - arch: amd64
      platform: linux/amd64
    - arch: arm64
      platform: linux/arm64
```

**Docker export format:**
```yaml
outputs: type=docker,dest=images/librafoto-api.tar
```

This creates a self-contained tar file that can be loaded with `docker load`.

#### 3. Create Release (`create-release`)
- Downloads both amd64 and arm64 image artifacts
- Creates platform-specific directory structure
- Packages two release zips: `librafoto-v{VERSION}-amd64.zip` and `-arm64.zip`
- Generates changelog from git commits since previous tag
- Creates GitHub Release with assets and formatted install instructions
- Marks as prerelease if version contains `-alpha`, `-beta`, or `-rc`

**Package structure per architecture:**
```
librafoto-v{VERSION}-{ARCH}.zip
├── images/
│   ├── librafoto-api.tar
│   ├── librafoto-display-ui.tar
│   ├── librafoto-admin-ui.tar
│   └── librafoto-proxy.tar
├── docker/
│   └── docker-compose.release.yml
├── scripts/
│   ├── common.sh
│   ├── kiosk-setup.sh
│   ├── start-kiosk.sh
│   └── update-host-ip.sh
├── install.sh
├── update.sh
├── uninstall.sh
└── .version
```

---

## Creating a Release

### Prerequisites

- Write access to the repository
- Completed features merged to `main`
- All CI checks passing
- Ready to publish production release

### Stable Release (Production)

**Process:** Update `.version` on `main` branch and push

#### Step 1: Update Version File on Main

Make sure you're on the `main` branch with a stable version:

```bash
git checkout main
git pull

# Update to stable version
echo "1.2.0" > .version

git add .version
git commit -m "Release 1.2.0"
```

#### Step 2: Push to Main (Triggers Release)

Pushing the commit to `main` automatically triggers the release workflow:

```bash
git push origin main
```

**That's it!** The release workflow detects the stable version and creates a GitHub Release.

### Prerelease (Testing)

**Process:** Create and push a tag matching the version in `.version`

#### Step 1: Update Version File

On your feature branch:

```bash
# For prerelease
echo "1.2.0-alpha.1" > .version

git add .version
git commit -m "Prepare prerelease 1.2.0-alpha.1"
git push
```

#### Step 2: Create and Push Tag

The tag must exactly match the version in `.version` with a `v` prefix:

```bash
# Read current version
VERSION=$(cat .version)

# Create annotated tag
git tag -a "v${VERSION}" -m "Prerelease ${VERSION}"

# Push tag to trigger release workflow
git push origin "v${VERSION}"
```

**Alternative (single command):**
```bash
git tag -a "v$(cat .version)" -m "Prerelease $(cat .version)" && \
  git push origin "v$(cat .version)"
```

### Step 3: Monitor Workflow

1. Go to **Actions** tab in GitHub repository
2. Find the **Release** workflow run:
   - **Stable:** Triggered by the push to `main`
   - **Prerelease:** Triggered by the tag push
3. Monitor the three jobs:
   - ✅ **Validate Release** - Ensures version format and requirements
   - ✅ **Build Images (amd64)** - Builds x86_64 images (~10-15 minutes)
   - ✅ **Build Images (arm64)** - Builds ARM images (~10-15 minutes)
   - ✅ **Create GitHub Release** - Packages and publishes

**Typical timeline:** 15-25 minutes total (builds run in parallel)

### Step 4: Verify Release

Once complete, check:

1. **Releases page:** New release appears at `https://github.com/{org}/{repo}/releases`
2. **Assets:** Two platform-specific zips are attached
3. **Changelog:** Auto-generated commit list is formatted correctly
4. **Prerelease flag:** Set correctly for alpha/beta/rc versions
5. **Installation instructions:** Included in release body

### Common Issues

**Stable release not triggering:**
```
Pushed to main but no release workflow started
```
**Fix:** Verify `.version` contains a stable version (no `-alpha`, `-beta`, or `-rc` suffix).

**Version mismatch (prereleases):**
```
Error: Tag version (1.2.0-alpha.1) does not match .version file (1.2.0-alpha.2)
```
**Fix:** Update `.version` file to match the tag, or create a new tag matching the file.

**Tag already exists:**
```
error: tag 'v1.2.0-alpha.1' already exists
```
**Fix:** Delete tag if release failed:
```bash
git tag -d v1.2.0-alpha.1                    # Delete locally
git push origin --delete v1.2.0-alpha.1      # Delete remotely
```

**Wrong version format:**
```
Error: Prerelease version not allowed on main branch
```
**Fix:** Ensure `main` has stable version (`1.2.0`), not prerelease (`1.2.0-alpha.1`).

**CI not passing:**
```
Error: Backend coverage 76% is below 80% threshold
```
**Fix:** Improve test coverage before merging to `main`. The release workflow won't trigger if CI fails.

---

## Release Package Contents

### Platform-Specific Zips

Each release includes two downloads:

| File | Description | Platforms |
|------|-------------|-----------|
| `librafoto-v{VERSION}-amd64.zip` | x86_64 release | Intel/AMD processors, most servers |
| `librafoto-v{VERSION}-arm64.zip` | ARM64 release | Raspberry Pi 3/4/5, Apple Silicon servers |

### What's Inside Each Zip

#### 1. Docker Images (`images/`)

Pre-built Docker images as tar files (no registry needed):

| File | Image | Size (approx) |
|------|-------|---------------|
| `librafoto-api.tar` | .NET 10 backend | ~200-300 MB |
| `librafoto-display-ui.tar` | Display frontend | ~50-100 MB |
| `librafoto-admin-ui.tar` | Admin frontend | ~50-100 MB |
| `librafoto-proxy.tar` | Nginx proxy | ~20-40 MB |

**Total:** ~350-600 MB per zip (compressed)

These images are loaded with:
```bash
docker load < images/librafoto-api.tar
```

#### 2. Deployment Scripts

| File | Purpose |
|------|---------|
| `install.sh` | Interactive installation with kiosk mode option |
| `update.sh` | Update to newer versions with backup and rollback |
| `uninstall.sh` | Remove LibraFoto (preserves data by default) |

#### 3. Helper Scripts (`scripts/`)

| File | Purpose |
|------|---------|
| `common.sh` | Shared functions for all scripts |
| `kiosk-setup.sh` | Configure Raspberry Pi fullscreen kiosk mode |
| `start-kiosk.sh` | Launch kiosk mode (display frontend) |
| `update-host-ip.sh` | Update API URL for QR codes |

#### 4. Configuration

| File | Purpose |
|------|---------|
| `docker/docker-compose.release.yml` | Production compose file for release mode |
| `.version` | Version file for update detection |

### Release Mode vs Build Mode

LibraFoto supports two installation modes:

| Mode | Source | When Used | Size |
|------|--------|-----------|------|
| **Release** | Pre-built zip | Download release from GitHub | ~400-700 MB |
| **Build** | Git clone | Clone repo and build from source | Requires build tools |

The release zip is **release mode** - all images are pre-built, no compilation needed.

---

## Troubleshooting

### Release Workflow Failed

**Check the job logs:**
1. Go to Actions → Failed workflow run
2. Click failing job to see error details

**Common failures:**

| Error | Cause | Fix |
|-------|-------|-----|
| Version mismatch | Tag ≠ `.version` file | Update `.version` and retag |
| Build failure | Dockerfile error or missing dependency | Fix build locally, push fix, retag |
| Upload failure | GitHub storage limit or network issue | Retry workflow or contact GitHub support |
| Image too large | Docker image exceeds size limits | Optimize Dockerfile layers |

### Release Created But Zips Are Corrupted

**Symptoms:** Download completes but zip won't extract or reports errors

**Possible causes:**
- Incomplete upload
- GitHub storage corruption (rare)

**Fix:**
1. Delete the release via GitHub UI
2. For prereleases: Delete and recreate the tag
3. For stable releases: Fix the issue, commit, and push to `main` again (version will be rebuilt)

```bash
# For prereleases - delete and recreate tag:
git tag -d v1.2.0-alpha.1
git push origin --delete v1.2.0-alpha.1
git tag -a v1.2.0-alpha.1 -m "Prerelease 1.2.0-alpha.1"
git push origin v1.2.0-alpha.1

# For stable releases - just push a fix to main:
# The workflow will run again on the next push
```

### Images Don't Load After Installation

**Symptoms:** `docker load` fails or images not found

**Check:**
```bash
# Verify tar files are valid
tar -tzf images/librafoto-api.tar > /dev/null
echo $?  # Should print 0

# Try loading manually
docker load < images/librafoto-api.tar
docker images | grep librafoto
```

**If validation fails:**
- Re-download the zip (may have been corrupted during download)
- Check available disk space (`df -h`)
- Verify Docker is running (`docker ps`)

### Wrong Architecture Downloaded

**Symptoms:** Image won't run, or gives `exec format error`

**Check your architecture:**
```bash
uname -m
# amd64/x86_64: Use -amd64.zip
# aarch64/arm64: Use -arm64.zip
```

**Fix:** Download correct platform-specific zip.

### Prerelease Not Marked

**Symptoms:** Version `1.2.0-alpha.1` shows as stable release

**Cause:** Detection regex in workflow doesn't match version format

**Check workflow logic:**
```yaml
if [[ "$FILE_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+-(alpha|beta|rc)\.[0-9]+$ ]]; then
  echo "IS_PRERELEASE=true"
```

Ensure your version exactly matches the pattern (case-sensitive).

---

## Advanced: Manual Release Creation

If you need to create a release manually (e.g., workflow issue):

### 1. Build Images Locally

```bash
# Build for your current platform
export VERSION=$(cat .version)

docker build -t librafoto-api:$VERSION -f docker/Dockerfile.api \
  --build-arg VERSION=$VERSION .
docker build -t librafoto-display-ui:$VERSION -f docker/Dockerfile.display \
  --build-arg VERSION=$VERSION .
docker build -t librafoto-admin-ui:$VERSION -f docker/Dockerfile.admin \
  --build-arg VERSION=$VERSION .
docker build -t librafoto-proxy:$VERSION -f docker/Dockerfile.proxy \
  --build-arg VERSION=$VERSION .
```

### 2. Save Images to Tar

```bash
mkdir -p release/images

docker save -o release/images/librafoto-api.tar librafoto-api:$VERSION
docker save -o release/images/librafoto-display-ui.tar librafoto-display-ui:$VERSION
docker save -o release/images/librafoto-admin-ui.tar librafoto-admin-ui:$VERSION
docker save -o release/images/librafoto-proxy.tar librafoto-proxy:$VERSION
```

### 3. Package Release

```bash
# Copy scripts and config
cp install.sh update.sh uninstall.sh .version release/
cp -r scripts release/
mkdir -p release/docker
cp docker/docker-compose.release.yml release/docker/

# Create zip
cd release
zip -r ../librafoto-v${VERSION}-$(uname -m).zip .
cd ..
```

### 4. Create GitHub Release

Use GitHub CLI:
```bash
gh release create "v${VERSION}" \
  "librafoto-v${VERSION}-$(uname -m).zip" \
  --title "LibraFoto v${VERSION}" \
  --notes "Manual release" \
  --draft  # Remove --draft when ready to publish
```

Or upload via GitHub web UI: **Releases** → **Draft a new release**.

---

## Best Practices

### Before Creating a Release

- [ ] All features merged and tested
- [ ] CI passing on target branch (green checkmarks)
- [ ] Version updated in `.version` file
- [ ] CHANGELOG or release notes prepared (optional, auto-generated)
- [ ] Breaking changes documented (if any)

### Version Incrementing

Follow **semantic versioning rules:**

| Change Type | Version Bump | Example |
|-------------|--------------|---------|
| Bug fixes (backward compatible) | Patch | `1.2.0` → `1.2.1` |
| New features (backward compatible) | Minor | `1.2.0` → `1.3.0` |
| Breaking changes | Major | `1.2.0` → `2.0.0` |

### Prerelease Testing Flow

Recommended progression for major releases:

```
1.0.0-alpha.1  →  Tag on feature branch, test internally
1.0.0-alpha.2  →  Tag on feature branch, fix issues, re-test
1.0.0-beta.1   →  Tag on feature branch, external beta testing
1.0.0-beta.2   →  Tag on feature branch, address feedback
1.0.0-rc.1     →  Tag on main (or feature branch), release candidate
1.0.0          →  Merge/push to main, stable release (no tag needed)
```

**Note:** Only the final stable release requires pushing to `main`. All prereleases use tags and can be created from any branch.

### Communication

When releasing:
- **Stable releases:** Announce to users via GitHub discussions, social media, etc.
- **Prereleases:** Share testing instructions with beta testers
- **Breaking changes:** Provide migration guide in release notes

---

## Integration with Update Script

LibraFoto's `update.sh` script automatically detects and downloads releases:

```bash
sudo ./update.sh          # Interactive mode
sudo ./update.sh --check  # Check for updates only
```

**How it works:**

1. Reads current version from `/opt/librafoto/.version`
2. Queries GitHub API for latest release
3. Compares versions (ignores prereleases by default)
4. Downloads appropriate platform-specific zip
5. Loads Docker images from tars
6. Updates compose file and restarts services

**Update script respects:**
- Platform detection (downloads correct architecture)
- Prerelease filtering (stable-only by default)
- Version comparison (semantic versioning aware)

---

## Related Documentation

- [Contributing Guide](../CONTRIBUTING.md) - Complete development workflow
- [CI Configuration](.github/workflows/) - Workflow YAML files
- [Docker Documentation](../docker/README.md) - Container architecture
- [Installation Scripts](../scripts/) - Deployment script documentation

---

_For questions or issues with the release process, please open a GitHub issue or discussion._
