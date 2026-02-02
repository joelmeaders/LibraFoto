import { defineConfig, devices } from "@playwright/test";

/**
 * LibraFoto E2E Integration Test Configuration
 *
 * This configuration runs integration tests against the full stack:
 * - Display Frontend (Vite): http://localhost:3000
 * - Admin Frontend (Angular): http://localhost:4200
 * - API Backend: http://localhost:5179
 *
 * IMPORTANT: The API backend must be running for these tests to work.
 * Start it with: dotnet run --project src/LibraFoto.Api
 *
 * The webServer config will auto-start frontends unless they're already running.
 * Set SKIP_WEB_SERVER=true to skip auto-starting servers.
 */

const skipWebServer = process.env.SKIP_WEB_SERVER === "true";

export default defineConfig({
  testDir: "./tests",
  // Run tests serially to avoid conflicts with shared database state
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1, // Single worker for integration tests to maintain database state
  reporter: [["html", { outputFolder: "playwright-report" }], ["list"]],
  // Global setup to initialize test data before tests
  globalSetup: "./tests/global-setup.ts",
  // Global teardown to clean up test data after tests
  globalTeardown: "./tests/global-teardown.ts",
  use: {
    // Base URL for API calls
    baseURL: "http://localhost:4200",
    // Collect trace when retrying the failed test
    trace: "on-first-retry",
    // Screenshot on failure
    screenshot: "only-on-failure",
    // Video on failure
    video: "on-first-retry",
    // Longer timeout for integration tests
    actionTimeout: 15000,
    navigationTimeout: 30000,
  },

  // Expect settings
  expect: {
    timeout: 10000,
  },

  // Configure projects for different frontends
  projects: [
    // Display Frontend tests (Chromium - kiosk mode target)
    {
      name: "display",
      testDir: "./tests/display",
      use: {
        ...devices["Desktop Chrome"],
        baseURL: "http://localhost:3000",
        // Simulate fullscreen kiosk mode
        viewport: { width: 1920, height: 1080 },
      },
    },
    // Admin Frontend tests (Desktop Chrome)
    {
      name: "admin",
      testDir: "./tests/admin",
      use: {
        ...devices["Desktop Chrome"],
        baseURL: "http://localhost:4200",
      },
    },
  ],

  // Web server configuration - starts the frontends and API for testing
  // Skip if SKIP_WEB_SERVER=true or servers are already running
  webServer: skipWebServer
    ? undefined
    : [
        {
          command: "dotnet run --project ../../src/LibraFoto.Api",
          url: "http://localhost:5179/health",
          reuseExistingServer: true,
          timeout: 120000,
          stdout: "pipe",
          stderr: "pipe",
          env: {
            ENABLE_TEST_ENDPOINTS: "true",
            // Use isolated test data directory (cleaned up after tests)
            LIBRAFOTO_DATA_DIR: "test-data",
          },
        },
        {
          command: "npm run dev",
          cwd: "../display",
          url: "http://localhost:3000",
          reuseExistingServer: true,
          timeout: 120000,
        },
        {
          command: "npm start",
          cwd: "../admin",
          url: "http://localhost:4200",
          reuseExistingServer: true,
          timeout: 120000,
        },
      ],
});
