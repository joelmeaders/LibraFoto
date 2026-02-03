import { test, expect, fillMaterialInput, API_BASE_URL } from "../fixtures";

/**
 * Admin Frontend - Setup Wizard Integration Tests
 *
 * NOTE: These tests only run when setup is required (fresh database).
 * Since the database is reset in global-setup and a test admin is created,
 * these tests will be skipped unless the database reset endpoint fails.
 *
 * These tests validate:
 * - Setup wizard UI components
 * - Form validation
 * - Stepper navigation
 */
test.describe("Admin Frontend - Setup Wizard", () => {
  // Check if setup is required before each test
  test.beforeEach(async ({ request }) => {
    // Check setup status via API
    const response = await request.get(`${API_BASE_URL}/api/setup/status`);
    const status = await response.json();

    if (!status.isSetupRequired) {
      test.skip(
        true,
        "Setup already complete (database was reset with test admin) - skipping setup wizard tests"
      );
    }
  });

  test("should redirect to setup page when setup required", async ({
    page,
  }) => {
    await page.goto("/");

    // Should redirect to setup page
    await expect(page).toHaveURL(/\/setup/);
  });

  test("should display welcome message", async ({ page }) => {
    await page.goto("/setup");

    await expect(page.getByText("Welcome to LibraFoto")).toBeVisible();
    await expect(
      page.getByText("Let's set up your digital picture frame")
    ).toBeVisible();
  });

  test("should show stepper with three steps", async ({ page }) => {
    await page.goto("/setup");

    // Check for step labels
    await expect(page.getByText("Create Admin Account")).toBeVisible();
    await expect(page.getByText("Configure Storage")).toBeVisible();
    await expect(page.getByText("Complete Setup")).toBeVisible();
  });

  test("should validate admin form fields", async ({ page }) => {
    await page.goto("/setup");

    // Click next without filling form
    await page.getByRole("button", { name: /next/i }).click();

    // Should show validation errors
    await expect(page.getByText("Email is required")).toBeVisible();
  });

  test("should validate password minimum length", async ({ page }) => {
    await page.goto("/setup");

    await fillMaterialInput(page, "Email", "admin@test.com");
    await fillMaterialInput(page, "Password", "short");
    await page.getByLabel("Confirm Password").fill("short");

    // Should show password length error
    await expect(
      page.getByText("Password must be at least 8 characters")
    ).toBeVisible();
  });

  test("should validate password match", async ({ page }) => {
    await page.goto("/setup");

    await fillMaterialInput(page, "Email", "admin@test.com");
    await fillMaterialInput(page, "Password", "password123");
    await page.getByLabel("Confirm Password").fill("differentpassword");

    // Should show password mismatch error
    await expect(page.getByText("Passwords do not match")).toBeVisible();
  });

  test("should allow navigation through stepper", async ({ page }) => {
    await page.goto("/setup");

    // Fill admin form
    await fillMaterialInput(page, "Email", "admin@test.com");
    await fillMaterialInput(page, "Display Name", "Admin User");
    await fillMaterialInput(page, "Password", "securepassword123");
    await page.getByLabel("Confirm Password").fill("securepassword123");

    // Go to next step
    await page.getByRole("button", { name: /next/i }).first().click();

    // Should be on storage step
    await expect(
      page.getByText("Choose where to store your photos")
    ).toBeVisible();

    // Local Storage should be selected by default
    await expect(page.getByText("Local Storage")).toBeVisible();

    // Go to next step
    await page.getByRole("button", { name: /next/i }).click();

    // Should be on complete step
    await expect(page.getByText("Ready to go!")).toBeVisible();
  });
});

/**
 * Test that actually completes setup - runs last and only once
 * This test is in a separate describe block to ensure it runs after form validation tests.
 *
 * NOTE: Since global-setup now resets the database and creates a test admin,
 * this test will typically be skipped unless the reset endpoint is unavailable.
 */
test.describe("Admin Frontend - Complete Setup Flow", () => {
  test.beforeEach(async ({ request }) => {
    // Check setup status via API
    const response = await request.get(`${API_BASE_URL}/api/setup/status`);
    const status = await response.json();

    if (!status.isSetupRequired) {
      test.skip(
        true,
        "Setup already complete (database was reset with test admin) - skipping setup completion test"
      );
    }
  });

  test("should complete setup successfully with real API", async ({ page }) => {
    await page.goto("/setup");

    // Fill admin form with unique credentials
    const timestamp = Date.now();
    const email = `setuptest_${timestamp}@test.com`;

    await fillMaterialInput(page, "Email", email);
    await fillMaterialInput(page, "Password", "SecurePassword123!");
    await page.getByLabel("Confirm Password").fill("SecurePassword123!");

    // Navigate through stepper
    await page.getByRole("button", { name: /next/i }).first().click();

    // Wait for storage step
    await expect(page.getByText(/storage|choose/i)).toBeVisible({
      timeout: 5000,
    });
    await page.getByRole("button", { name: /next/i }).click();

    // Wait for complete step
    await expect(page.getByText(/ready|complete/i)).toBeVisible({
      timeout: 5000,
    });

    // Complete setup
    await page.getByRole("button", { name: /complete setup/i }).click();

    // Should navigate to dashboard after successful setup
    await expect(page).toHaveURL(/\/(dashboard|photos)/, { timeout: 15000 });
  });
});
