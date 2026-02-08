# Conventional Commits Guide

This guide provides detailed instructions for using [Conventional Commits](https://conventionalcommits.org) in LibraFoto.

## Table of Contents

- [Why Conventional Commits?](#why-conventional-commits)
- [Format](#format)
- [Commit Types](#commit-types)
- [Scopes](#scopes)
- [Breaking Changes](#breaking-changes)
- [Examples](#examples)
- [Multi-Scope Changes](#multi-scope-changes)
- [Tips and Best Practices](#tips-and-best-practices)

---

## Why Conventional Commits?

LibraFoto uses Conventional Commits to:

1. **Generate structured changelogs** automatically
2. **Determine semantic version bumps** based on commit types
3. **Improve commit history readability** with clear structure
4. **Enable better tooling** for release automation
5. **Communicate intent clearly** through standardized formats

Since LibraFoto uses **squash merging** to main, the PR title becomes the commit message in the main branch history. Individual development commits can be informal, but **PR titles must follow Conventional Commits format**.

---

## Format

```
<type>(<scope>): <description>

[optional body]

[optional footer(s)]
```

### Components

| Component     | Required | Description                                    |
| ------------- | -------- | ---------------------------------------------- |
| **type**      | ✅       | Commit category (feat, fix, docs, etc.)        |
| **scope**     | ⚠️       | Module or area affected (optional but recommended) |
| **description** | ✅     | Short summary in imperative mood               |
| **body**      | ❌       | Detailed explanation (line-wrapped at 72 chars) |
| **footer**    | ❌       | Breaking changes, issue references             |

### Rules

- **Type and description are required**
- **Scope is optional but highly recommended** for clarity
- **Use lowercase** for type and scope
- **Use imperative mood** in description: "add" not "added" or "adds"
- **No period** at end of description
- **Separate scope with parentheses**: `feat(media):`
- **Add colon and space** after type or scope: `: `

---

## Commit Types

### Primary Types (Semantic Versioning Impact)

| Type     | Semantic Change | Description | Examples |
| -------- | --------------- | ----------- | -------- |
| **feat** | Minor (0.X.0)   | New feature or functionality | Add thumbnail support, implement search |
| **fix**  | Patch (0.0.X)   | Bug fix | Fix memory leak, correct validation |
| **BREAKING** | Major (X.0.0) | Breaking API change | Remove deprecated endpoints |

### Secondary Types (No Version Impact)

| Type         | Description | Use For |
| ------------ | ----------- | ------- |
| **docs**     | Documentation changes | README updates, API docs, comments |
| **style**    | Code style/formatting | Whitespace, formatting, semicolons (no logic change) |
| **refactor** | Code restructuring | Simplify code without changing behavior |
| **perf**     | Performance improvement | Optimize queries, reduce allocations |
| **test**     | Test changes | Add/update tests, improve test coverage |
| **chore**    | Maintenance tasks | Update dependencies, cleanup, tooling |
| **build**    | Build system changes | Modify Dockerfiles, build scripts |
| **ci**       | CI/CD changes | Update GitHub Actions, CI configuration |

### When to Use Each Type

**feat**: Any user-facing addition or enhancement
```
feat(media): add HEIC thumbnail generation support
feat(auth): implement two-factor authentication
feat(admin): add bulk photo tagging interface
```

**fix**: Correcting incorrect behavior
```
fix(storage): prevent duplicate file downloads
fix(auth): correct JWT token expiration handling
fix(display): resolve slideshow timing issues
```

**docs**: Documentation only (no code changes)
```
docs: update Docker installation guide
docs(api): add storage provider examples
docs: fix typo in CONTRIBUTING.md
```

**refactor**: Internal restructuring (no behavior change)
```
refactor(media): extract thumbnail service from controller
refactor(storage): simplify provider factory pattern
```

**perf**: Performance improvements
```
perf(media): cache decoded EXIF data
perf(api): add database query indexes
```

**test**: Test additions/improvements
```
test(storage): add Google Photos provider integration tests
test(admin): increase coverage for album service
```

**chore**: Maintenance without user impact
```
chore(deps): update Angular to v21.1
chore: cleanup unused imports
chore: update .gitignore patterns
```

**build**: Build system or dependency changes
```
build(docker): optimize multi-stage Dockerfile
build: update .NET SDK to 10.0.1
```

**ci**: CI/CD configuration
```
ci: add code coverage reporting
ci: optimize GitHub Actions caching
```

---

## Scopes

Scopes identify which module, component, or area of the codebase is affected. **Scopes are optional but strongly recommended** for maintainability.

### Module-Level Scopes (Primary)

Use these for changes within LibraFoto's modular architecture:

| Scope       | Description | Typical Changes |
| ----------- | ----------- | --------------- |
| `admin`     | Admin module (backend + frontend) | Photo/album management, system settings |
| `auth`      | Authentication/authorization | Users, roles, JWT, guest links |
| `display`   | Display module (backend + slideshow) | Slideshow engine, display settings |
| `media`     | Media processing | Thumbnails, EXIF extraction, image ops |
| `storage`   | Storage providers | Local/Google Photos sync, provider logic |

### Component-Level Scopes

Use when change is specific to one component:

| Scope          | Description |
| -------------- | ----------- |
| `api`          | API host, middleware, startup |
| `data`         | Database entities, migrations, EF Core |
| `admin-ui`     | Angular admin frontend specifically |
| `display-ui`   | Vanilla TS slideshow frontend specifically |

### Infrastructure Scopes

| Scope     | Description |
| --------- | ----------- |
| `docker`  | Docker/deployment configuration |
| `ci`      | GitHub Actions workflows |
| `scripts` | Installation/update bash scripts |

### Cross-Cutting Scopes

| Scope    | Description |
| -------- | ----------- |
| `docs`   | Documentation |
| `test`   | Test infrastructure (E2E, shell tests) |
| `deps`   | Dependency updates |
| `config` | Configuration files (tsconfig, etc.) |

### Scope Examples

```
feat(media): add HEIC image support
fix(auth): validate JWT expiration correctly
docs(storage): add Google Photos setup guide
chore(deps): update ImageSharp to v3.1.0
ci(release): improve changelog generation
refactor(api): simplify endpoint routing
test(admin): add album service unit tests
build(docker): reduce image size by 40%
```

### Choosing a Scope

**Module-first strategy**:
1. **If change is within a module** → use module name: `feat(media): ...`
2. **If change spans modules** → use most affected module or `api`
3. **If infrastructure/tooling** → use `docker`, `ci`, `scripts`
4. **If documentation** → use `docs`
5. **When in doubt** → omit scope (still valid)

---

## Breaking Changes

Breaking changes indicate incompatible API changes or behavior that requires user action.

### Format 1: Exclamation Mark

Add `!` before the colon:

```
feat(api)!: remove deprecated /api/v1/photos endpoint
```

### Format 2: BREAKING CHANGE Footer

Add footer with details:

```
feat(storage): redesign provider configuration

BREAKING CHANGE: Storage provider configuration now uses JSON format
instead of connection strings. Update docker-compose.yml:

Before:
  Storage__Config: "path=/data/photos"

After:
  Storage__Config: '{"Path": "/data/photos", "MaxCacheSize": 1000}'

Migration guide: docs/migration/storage-config-v2.md
```

### Breaking Change Checklist

- [ ] Add `!` to commit type or `BREAKING CHANGE:` footer
- [ ] Describe what breaks in body or footer
- [ ] Explain migration path
- [ ] Update major version (e.g., 1.0.0 → 2.0.0)
- [ ] Add migration guide to documentation

### Examples

```
feat(auth)!: require HTTPS for JWT tokens

BREAKING CHANGE: JWT tokens are now only issued over HTTPS connections.
Development environments must use HTTPS or set AllowInsecureTokens=true.

fix(api)!: correct photo deletion cascade behavior

BREAKING CHANGE: Deleting an album now permanently deletes all photos
within it. Previous behavior moved photos to "Uncategorized".

perf(storage)!: switch to streaming file downloads

BREAKING CHANGE: IStorageProvider.DownloadFileAsync() now returns Stream
instead of byte[]. Update custom providers accordingly.
```

---

## Examples

### Simple Changes

```
feat(media): add WebP thumbnail format support
fix(auth): prevent duplicate user email addresses
docs: add Raspberry Pi installation guide
chore(deps): update .NET to 10.0.1
test(storage): add local provider integration tests
```

### With Body

```
feat(display): add slideshow transition effects

Implements configurable transitions (fade, slide, zoom) for slideshow.
Transition duration and easing can be set via display settings API.

Closes #142
```

### With Multiple Issues

```
fix(media): correct EXIF orientation handling

Properly rotate thumbnails based on EXIF orientation tag values 3, 6, and 8.

Fixes #156, #189, #203
```

### With Breaking Change

```
feat(api)!: standardize error response format

All API errors now return consistent JSON structure with `error`,
`message`, and `details` fields.

BREAKING CHANGE: Error responses changed from:
  { "ErrorMessage": "..." }
To:
  { "error": "NotFound", "message": "...", "details": null }

Update error handling in admin UI and display frontend.

Closes #167
```

### Revert Commit

```
revert: feat(media): add WebP thumbnail format

This reverts commit a1b2c3d4. WebP support causes memory issues on
Raspberry Pi 3 devices with limited RAM.
```

---

## Multi-Scope Changes

When a change affects multiple modules, choose the **most impacted scope** or use a broader scope.

### Strategy 1: Most Affected Module

```
feat(media): add shared thumbnail cache service

This change touches media, storage, and admin modules, but the primary
logic lives in media module.
```

### Strategy 2: Broader Scope

```
refactor(api): standardize error handling across all modules

Updated Admin, Auth, Display, Media, and Storage modules to use
centralized error middleware.
```

### Strategy 3: Multiple Commits

For significant multi-module features, consider separate commits per module:

```
feat(storage): add thumbnail cache storage provider
feat(media): integrate thumbnail cache with storage
feat(admin): add cache management UI
```

Then squash into one PR with primary scope:

```
PR title: feat(media): add distributed thumbnail caching system
```

---

## Tips and Best Practices

### ✅ DO

- **Use present tense**: "add feature" not "added feature"
- **Start with lowercase**: `feat(media):` not `Feat(Media):`
- **Be specific**: `fix(auth): prevent null reference in JWT validator` not `fix: bug`
- **Reference issues**: `Closes #123` or `Fixes #456, #789`
- **Use scopes**: Helps identify affected areas quickly
- **Keep subject under 72 chars**: Readable in git log
- **Wrap body at 72 chars**: Readable in terminal

### ❌ DON'T

- **Don't use past tense**: ~"added feature"~
- **Don't capitalize type**: ~`Feat(media):`~
- **Don't end with period**: ~`feat: add feature.`~
- **Don't be vague**: ~`fix: bug`~ or ~`chore: update stuff`~
- **Don't mix multiple unrelated changes**: Separate into multiple commits/PRs
- **Don't use wrong type**: `feat` for bug fixes or `fix` for new features

### Subject Line Examples

✅ **Good:**
```
feat(media): add HEIC thumbnail support
fix(auth): prevent JWT token reuse after logout
docs: update Docker Compose installation guide
perf(storage): reduce Google Photos API calls by 60%
refactor(api): extract common middleware to shared library
```

❌ **Bad:**
```
Added HEIC support                          # Missing type/scope
fix: Bug                                    # Too vague
Feat(Media): Add HEIC support.              # Wrong capitalization, ends with period
feat(media) add HEIC support                # Missing colon
feat: add media HEIC thumbnail support API  # Too long (>72 chars)
```

### PR Title Strategy

Since LibraFoto uses squash merge:

1. **Individual commits during development**: Can be informal
2. **PR title**: Must follow Conventional Commits (becomes main branch commit)
3. **PR description**: Should include detailed body and footer content

Example workflow:
```bash
# During development (informal commits OK)
git commit -m "wip: working on HEIC support"
git commit -m "fix tests"
git commit -m "cleanup"

# Create PR with conventional title
PR Title: feat(media): add HEIC thumbnail support with ImageSharp

# PR Description becomes commit body
Implements HEIC/HEIF image decoding and thumbnail generation using
ImageSharp library. Supports iOS photos with HEIC encoding.

Closes #142
```

### When Scope Is Unclear

If you can't decide on a scope, it's OK to **omit it**:

```
docs: update contributing guidelines
chore: cleanup unused imports
build: update Docker base image
```

---

## Reference

- [Conventional Commits Specification](https://conventionalcommits.org)
- [Angular Commit Guidelines](https://github.com/angular/angular/blob/main/CONTRIBUTING.md#-commit-message-format) (basis for this convention)
- [Keep a Changelog](https://keepachangelog.com) (CHANGELOG.md format)
- [Semantic Versioning](https://semver.org) (versioning rules)

---

**Questions?** Open an issue or discussion on GitHub.
