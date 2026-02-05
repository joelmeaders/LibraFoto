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
async function getSetupStatus(request: any) {
  const response = await request.get(`${API_BASE_URL}/api/setup/status`);
  return response.json();
}

test.describe("Admin Frontend - Setup Wizard", () => {

  test("should redirect to setup page when setup required", async ({
    page,
    request,
  }) => {
    const status = await getSetupStatus(request);
    await page.goto("/");

    if (status.isSetupRequired) {
      // Should redirect to setup page
      await expect(page).toHaveURL(/\/setup/);
    } else {
      await expect(page).toHaveURL(/\/(dashboard|photos|login)/);
    }
  });

  test("should display welcome message", async ({ page, request }) => {
    const status = await getSetupStatus(request);
    await page.goto("/setup");

    if (status.isSetupRequired) {
      await expect(page.getByText("Welcome to LibraFoto")).toBeVisible();
      await expect(
        page.getByText("Let's set up your digital picture frame")
      ).toBeVisible();
    } else {
      await expect(page).toHaveURL(/\/(dashboard|photos|login)/);
    }
  });

  test("should show stepper with three steps", async ({ page, request }) => {
    const status = await getSetupStatus(request);
    await page.goto("/setup");

    if (status.isSetupRequired) {
      // Check for step labels
      await expect(page.getByText("Create Admin Account")).toBeVisible();
      await expect(page.getByText("Configure Storage")).toBeVisible();
      await expect(page.getByText("Complete Setup")).toBeVisible();
    } else {
      await expect(page).toHaveURL(/\/(dashboard|photos|login)/);
    }
  });

  test("should validate admin form fields", async ({ page, request }) => {
    const status = await getSetupStatus(request);
    await page.goto("/setup");

    if (!status.isSetupRequired) {
      await expect(page).toHaveURL(/\/(dashboard|photos|login)/);
      return;
    }

    // Click next without filling form
    await page.getByRole("button", { name: /next/i }).click();

    // Should show validation errors
    await expect(page.getByText("Email is required")).toBeVisible();
  });

  test("should validate password minimum length", async ({ page, request }) => {
    const status = await getSetupStatus(request);
    await page.goto("/setup");

    if (!status.isSetupRequired) {
      await expect(page).toHaveURL(/\/(dashboard|photos|login)/);
      return;
    }

    await fillMaterialInput(page, "Email", "admin@test.com");
    await fillMaterialInput(page, "Password", "short");
    await page.getByLabel("Confirm Password").fill("short");

    // Should show password length error
    await expect(
      page.getByText("Password must be at least 8 characters")
    ).toBeVisible();
  });

  test("should validate password match", async ({ page, request }) => {
    const status = await getSetupStatus(request);
    await page.goto("/setup");

    if (!status.isSetupRequired) {
      await expect(page).toHaveURL(/\/(dashboard|photos|login)/);
      return;
    }

    await fillMaterialInput(page, "Email", "admin@test.com");
    await fillMaterialInput(page, "Password", "password123");
    await page.getByLabel("Confirm Password").fill("differentpassword");

    // Should show password mismatch error
    await expect(page.getByText("Passwords do not match")).toBeVisible();
  });

  test("should allow navigation through stepper", async ({ page, request }) => {
    const status = await getSetupStatus(request);
    await page.goto("/setup");

    if (!status.isSetupRequired) {
      await expect(page).toHaveURL(/\/(dashboard|photos|login)/);
      return;
    }

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
  test("should complete setup successfully with real API", async ({ page, request }) => {
    const status = await getSetupStatus(request);
    if (!status.isSetupRequired) {
      await page.goto("/setup");
      await expect(page).toHaveURL(/\/(dashboard|photos|login)/);
      return;
    }

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
