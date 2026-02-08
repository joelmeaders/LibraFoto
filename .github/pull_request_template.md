## Pull Request Title Format

**Important:** Your PR title should follow [Conventional Commits](https://conventionalcommits.org) format as it will become the commit message when squashed to main.

```
<type>(<scope>): <description>
```

Examples:
- `feat(media): add HEIC thumbnail support`
- `fix(auth): correct JWT expiration validation`
- `docs: update installation guide`
- `chore(deps): update Angular to v21.1`

### Types
- **feat**: New feature
- **fix**: Bug fix
- **docs**: Documentation only
- **style**: Code style changes (formatting, no logic change)
- **refactor**: Code change that neither fixes a bug nor adds a feature
- **perf**: Performance improvement
- **test**: Adding or updating tests
- **chore**: Maintenance tasks (dependencies, build, etc.)
- **build**: Build system or external dependencies
- **ci**: CI/CD configuration changes

### Scopes (Optional but Recommended)
`admin`, `auth`, `display`, `media`, `storage`, `api`, `docker`, `ci`, `scripts`, `docs`, `test`, `deps`

---

## PR Type

- [ ] 🚀 Feature (new functionality)
- [ ] 🐛 Bug fix
- [ ] 📚 Documentation update
- [ ] 🔧 Chore/maintenance
- [ ] ⚠️ Breaking change

## Scope
<!-- e.g., media, auth, display, admin -->

## Description

<!-- Describe what changed and why -->

## Motivation and Context

<!-- Why is this change needed? What problem does it solve? -->
<!-- Link to related issues: Closes #123 -->

## Testing

- [ ] Unit tests added/updated
- [ ] E2E tests added/updated (if applicable)
- [ ] Manual testing completed
- [ ] Coverage meets 80% threshold

## Checklist

> **Note:** By submitting this PR, you automatically agree to the [LibraFoto Contributor License Agreement (CLA)](../CLA.md). You retain full rights to use your contributions for any other purpose.

- [ ] Version set to prerelease (e.g., `1.3.0-alpha.1`)
- [ ] Code formatted (`dotnet format`)
- [ ] All tests passing locally
- [ ] Coverage ≥80% on new code
- [ ] Documentation updated (if applicable)
- [ ] CHANGELOG.md updated under `[Unreleased]` section

## Breaking Changes

<!-- If this PR includes breaking changes, describe them here -->
<!-- Include migration guide if applicable -->

## Screenshots/Examples

<!-- If applicable, add screenshots or code examples -->

