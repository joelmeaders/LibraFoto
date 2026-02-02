# LibraFoto E2E Tests

This directory contains end-to-end integration tests for LibraFoto using [Playwright](https://playwright.dev/). These tests run against the full application stack (API + Frontends) to verify critical user flows.

## Prerequisites

- Node.js 20+
- .NET 10 SDK
- Docker (optional, for running in containers)

## Setup

1. Install dependencies:

   ```bash
   npm install
   ```

2. Install Playwright browsers:
   ```bash
   npx playwright install chromium
   ```

## Running Tests

The tests are configured to automatically start the API and frontends if they are not already running.

### Run all tests (headless)

```bash
npm test
```

### Run with UI Mode (Interactive)

Great for debugging and watching tests run.

```bash
npm run test:ui
```

### Run specific projects

```bash
npm run test:display    # Display frontend tests only
npm run test:admin      # Admin frontend tests only
```

## Test Environment

The tests require special API endpoints to reset the database state between runs. These endpoints are **disabled by default** for security.

When running tests via `npm test` (Playwright), the configuration automatically sets the `ENABLE_TEST_ENDPOINTS="true"` environment variable for the API process.

If you are running the API manually and want to run tests against it, you must start the API with this variable:

```bash
# PowerShell
$env:ENABLE_TEST_ENDPOINTS="true"; dotnet run --project src/LibraFoto.Api

# Bash
ENABLE_TEST_ENDPOINTS="true" dotnet run --project src/LibraFoto.Api
```

Then run tests with the web server skipped:

```bash
SKIP_WEB_SERVER=true npm test
```

## Project Structure

- `tests/` - Test files
  - `admin/` - Admin frontend tests
  - `display/` - Display frontend tests
  - `fixtures.ts` - Custom test fixtures and API client
  - `global-setup.ts` - Global initialization (creates admin user)
- `playwright.config.ts` - Configuration
